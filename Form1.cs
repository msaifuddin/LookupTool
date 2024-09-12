using System.Text;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace searchAll
{
    public partial class Form1 : Form
    {
        private bool _isConnected = false;
        private List<JObject?> _userSearchResults = new List<JObject?>(); // Store user search results
        private List<JObject?> _deviceSearchResults = new List<JObject?>(); // Store device search results
        private int _currentPage = 1;
        private const int _pageSize = 5;

        // Cached credential and access token
        private Azure.Identity.InteractiveBrowserCredential? _interactiveCredential;
        private string? _accessToken;
        private DateTime _tokenExpiration;

        // Client ID
        private readonly string _clientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e";
        private string? _userUPN; // Store UPN for connection status

        public Form1()
        {
            InitializeComponent();  // This will initialize the cancelButton and other components

            SetPlaceholder(null, EventArgs.Empty);
            emailTextBox.Enter += RemovePlaceholder;
            emailTextBox.Leave += SetPlaceholder;
            previousPageButton.Enabled = false;
            nextPageButton.Enabled = false;
            userDetailsTextBox.Visible = false;
            cancelButton.Enabled = false;  // Disable cancel button initially
            searchButton.Enabled = false;  // Disable search button initially
            this.Shown += (s, e) => this.ActiveControl = null;

            // Just wire the event here, don't create a new button instance
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
        }

        // Authentication Logic
        private async void connectButton_Click(object sender, EventArgs e)
        {
            await AuthenticateAndInitializeGraphClient();
        }

        // Class-level variable for CancellationTokenSource
        private CancellationTokenSource? _cancellationTokenSource;
        private int countdownTime = 60; // Countdown in seconds

        private bool _isCancelRequested = false;

        private async Task AuthenticateAndInitializeGraphClient()
        {
            if (_isConnected)
            {
                UpdateConnectionStatus($"{_userUPN} is already connected.", Color.Green, FontStyle.Bold);
                connectButton.Enabled = false; // Disable the button if already connected
                searchButton.Enabled = true;   // Enable the search button after connection
                cancelButton.Enabled = false;  // Disable the cancel button after successful authentication
                return;
            }

            _isCancelRequested = false; // Reset the cancel flag
            countdownTime = 60; // Reset countdown

            cancelButton.Enabled = true;  // Enable the cancel button
            connectButton.Enabled = false; // Disable the connect button to prevent multiple clicks
            searchButton.Enabled = false;  // Disable the search button until connected

            try
            {
                // Initialize the cancellation token source properly
                _cancellationTokenSource = new CancellationTokenSource();

                UpdateConnectionStatus($"Authenticating... ({countdownTime}s)", Color.Orange, FontStyle.Bold);

                if (_interactiveCredential == null)
                {
                    _interactiveCredential = new Azure.Identity.InteractiveBrowserCredential(new Azure.Identity.InteractiveBrowserCredentialOptions
                    {
                        ClientId = _clientId
                    });
                }

                // Countdown task
                var countdownTask = Task.Run(async () =>
                {
                    while (countdownTime > 0 && !_isConnected) // Continue only if not connected
                    {
                        if (_isCancelRequested) // Check if cancel is requested
                        {
                            break; // Exit the loop immediately if cancelled
                        }

                        await Task.Delay(1000); // Wait for 1 second
                        countdownTime--;

                        this.Invoke((Action)(() =>
                        {
                            if (!_isConnected) // Continue updating only if not connected
                            {
                                UpdateConnectionStatus($"Authenticating... ({countdownTime}s)", Color.Orange, FontStyle.Bold);
                            }
                        }));
                    }

                    // Only trigger timeout if not connected
                    if (!_isConnected && countdownTime == 0)
                    {
                        this.Invoke((Action)(() =>
                        {
                            UpdateConnectionStatus("Authentication timed out.", Color.Red, FontStyle.Bold);
                            connectButton.Enabled = true;  // Allow retry after cancel or timeout
                            cancelButton.Enabled = false;  // Disable the cancel button
                            searchButton.Enabled = false;  // Keep the search button disabled if not connected
                        }));
                    }
                });

                // Authentication task
                var authTask = Task.Run(async () =>
                {
                    try
                    {
                        if (_isCancelRequested) // Check for cancellation before starting authentication
                        {
                            throw new OperationCanceledException("Authentication cancelled.");
                        }

                        await GetAccessTokenAsync(CancellationToken.None); // Skip using tokens for now

                        // Fetch the UPN (connected user's display name)
                        await FetchUserUPNAsync();

                        if (_isCancelRequested) // Check again after fetching UPN
                        {
                            throw new OperationCanceledException("Authentication cancelled.");
                        }

                        _isConnected = true; // Mark as connected

                        // Stop the countdown and update UI immediately after successful connection
                        this.Invoke((Action)(() =>
                        {
                            UpdateConnectionStatus($"{_userUPN} is connected.", Color.Green, FontStyle.Bold);
                            connectButton.Enabled = false; // Disable the connect button after successful authentication
                            searchButton.Enabled = true;   // Enable search button after successful authentication
                            cancelButton.Enabled = false;  // Disable the cancel button after successful authentication
                        }));
                    }
                    catch (OperationCanceledException)
                    {
                        this.Invoke((Action)(() =>
                        {
                            UpdateConnectionStatus("Authentication cancelled by user.", Color.Red, FontStyle.Bold);
                            connectButton.Enabled = true;  // Re-enable connect button
                            cancelButton.Enabled = false;  // Disable cancel button
                            searchButton.Enabled = false;  // Keep search button disabled
                        }));
                    }
                    catch (Exception ex)
                    {
                        this.Invoke((Action)(() =>
                        {
                            UpdateConnectionStatus($"Error: {ex.Message}", Color.Red, FontStyle.Bold);
                            connectButton.Enabled = true;  // Re-enable connect button after failure
                            cancelButton.Enabled = false;  // Disable cancel button after failure
                            searchButton.Enabled = false;  // Keep search button disabled
                        }));
                    }
                });

                await Task.WhenAny(countdownTask, authTask); // Ensure tasks run together and either cancels first
            }
            finally
            {
                // Safely dispose of the token
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                }
            }
        }

        // Cancel button event handler
        private void cancelButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // Start a new instance of the application
                System.Diagnostics.Process.Start(Application.ExecutablePath); // Relaunch the application

                // Forcefully exit the current instance
                System.Diagnostics.Process.GetCurrentProcess().Kill(); // This forcefully kills the current process
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restart the application. Error: {ex.Message}");
            }
        }

        // Adjust GetAccessTokenAsync to take CancellationToken
        private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            if (_interactiveCredential == null)
            {
                throw new InvalidOperationException("Interactive credential is not initialized.");
            }

            var tokenRequestContext = new Azure.Core.TokenRequestContext(new[] { "https://graph.microsoft.com/.default" });
            var tokenResponse = await _interactiveCredential.GetTokenAsync(tokenRequestContext, cancellationToken); // Use cancellationToken here
            return tokenResponse.Token;
        }

        // Fetch UPN after authentication
        private async Task FetchUserUPNAsync()
        {
            var token = await GetAccessTokenAsync(CancellationToken.None); // Pass CancellationToken.None here
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me");

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var user = JObject.Parse(jsonResponse);
                    _userUPN = user["givenName"]?.ToString();
                }
            }
        }

        // Placeholder Logic
        private void SetPlaceholder(object? sender, EventArgs e)
        {
            if (emailTextBox.Text == string.Empty || string.IsNullOrWhiteSpace(emailTextBox.Text))
            {
                emailTextBox.Text = "Search UPN/Email/Name/Device/Serial";
                emailTextBox.ForeColor = Color.Gray;
            }
        }

        private void RemovePlaceholder(object? sender, EventArgs e)
        {
            if (emailTextBox.Text == "Search UPN/Email/Name/Device/Serial")
            {
                emailTextBox.Text = "";
                emailTextBox.ForeColor = Color.Black;
            }
        }

        // Search Logic
        private async void searchButton_Click(object sender, EventArgs e)
        {
            if (IsSearchInputValid(emailTextBox, "Search UPN/Email/Name/Device/Serial"))
            {
                string searchText = emailTextBox.Text;

                // Clear previous search results and details box
                ClearResults();

                // Perform both user and device search
                await SearchUserAsync(searchText);
                await SearchSpecificDeviceAsync(searchText);

                // Calculate the total results count
                int totalResults = _userSearchResults.Count + _deviceSearchResults.Count;

                // Update total results label
                totalResultsLabel.Text = $"Total results: {totalResults}";

                // Handle results display based on what is found
                if (totalResults > 0)
                {
                    _currentPage = 1;
                    DisplaySearchResults();
                }
                else
                {
                    UpdateSearchStatus("No result", true);
                }
            }
        }

        private void ClearResults()
        {
            _userSearchResults.Clear();
            _deviceSearchResults.Clear();
            userListBox.Items.Clear();
            userDetailsTextBox.Clear();
        }

        // Search for users
        private async Task SearchUserAsync(string searchText)
        {
            if (!_isConnected)
            {
                UpdateSearchStatus("Please connect to Azure first.", true);
                return;
            }

            try
            {
                UpdateSearchStatus("Searching for user...", true);

                var token = await GetAccessTokenAsync(CancellationToken.None); // Use CancellationToken.None here
                using (HttpClient httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var response = await httpClient.GetAsync($"https://graph.microsoft.com/v1.0/users?$filter=startswith(userPrincipalName,'{searchText}') or startswith(displayName,'{searchText}') or startswith(givenName,'{searchText}') or startswith(surname,'{searchText}')");

                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        var users = JObject.Parse(jsonResponse)["value"];

                        if (users != null && users.Any())
                        {
                            _userSearchResults = users.Select(u => u?.ToObject<JObject>()).ToList();
                        }
                    }
                    else
                    {
                        UpdateSearchStatus($"Error searching users: {response.ReasonPhrase}", true);
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateSearchStatus($"Error retrieving user details: {ex.Message}", true);
            }
            finally
            {
                UpdateSearchStatus("", false);
            }
        }

        // Search for a specific device by deviceName or serialNumber
        private async Task SearchSpecificDeviceAsync(string searchText)
        {
            if (!_isConnected)
            {
                UpdateSearchStatus("Please connect to Azure first.", true);
                return;
            }

            try
            {
                UpdateSearchStatus($"Searching for device: {searchText}...", true);

                var token = await GetAccessTokenAsync(CancellationToken.None); // Pass CancellationToken.None here
                using (HttpClient httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var response = await httpClient.GetAsync($"https://graph.microsoft.com/v1.0/deviceManagement/managedDevices?$filter=serialNumber eq '{searchText}'");

                    if (!response.IsSuccessStatusCode)
                    {
                        UpdateSearchStatus($"Error searching device by serial number: {response.ReasonPhrase}", true);
                        return;
                    }

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var devices = JObject.Parse(jsonResponse)["value"];

                    if (devices != null && devices.Any())
                    {
                        _deviceSearchResults = devices.Select(d => d?.ToObject<JObject>()).ToList();
                    }
                    else
                    {
                        response = await httpClient.GetAsync($"https://graph.microsoft.com/v1.0/deviceManagement/managedDevices?$filter=deviceName eq '{searchText}'");

                        if (response.IsSuccessStatusCode)
                        {
                            jsonResponse = await response.Content.ReadAsStringAsync();
                            devices = JObject.Parse(jsonResponse)["value"];

                            if (devices != null && devices.Any())
                            {
                                _deviceSearchResults = devices.Select(d => d?.ToObject<JObject>()).ToList();
                            }
                            else
                            {
                                UpdateSearchStatus($"No device found with serial number or device name: {searchText}", true);
                            }
                        }
                        else
                        {
                            UpdateSearchStatus($"Error searching device by device name: {response.ReasonPhrase}", true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateSearchStatus($"Error searching for the device: {ex.Message}", true);
            }
            finally
            {
                UpdateSearchStatus("", false);
            }
        }

        // Display User and Device Results with Pagination
        private void DisplaySearchResults()
        {
            userListBox.Items.Clear();

            int totalResults = _userSearchResults.Count + _deviceSearchResults.Count;
            int totalPages = (int)Math.Ceiling((double)totalResults / _pageSize);

            if (_currentPage > totalPages) _currentPage = totalPages;

            var pagedResults = GetPagedResults();

            foreach (var (result, index) in pagedResults.Select((result, i) => (result, i)))
            {
                if (result.ContainsKey("deviceName"))
                {
                    userListBox.Items.Add($"{((_currentPage - 1) * _pageSize) + index + 1}. Device: {result["deviceName"]} (Serial: {result["serialNumber"]})");
                }
                else if (result.ContainsKey("displayName"))
                {
                    userListBox.Items.Add($"{((_currentPage - 1) * _pageSize) + index + 1}. User: {result["displayName"]} ({result["userPrincipalName"]})");
                }
            }

            UpdatePaginationButtons(totalResults, totalPages);
        }

        // Get paginated results
        private IEnumerable<JObject?> GetPagedResults()
        {
            var combinedResults = _userSearchResults.Concat(_deviceSearchResults).ToList();
            return combinedResults.Skip((_currentPage - 1) * _pageSize).Take(_pageSize);
        }

        private async void userListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (userListBox.SelectedIndex >= 0)
            {
                var selectedResult = GetPagedResults().ElementAt(userListBox.SelectedIndex);

                if (selectedResult != null)
                {
                    if (selectedResult.ContainsKey("displayName"))
                    {
                        await DisplayUserDetailsAsync(selectedResult);
                        await FetchAndDisplayUserDevicesAsync(selectedResult);
                    }
                    else if (selectedResult.ContainsKey("deviceName"))
                    {
                        await DisplayDeviceDetailsAsync(selectedResult);
                        await DisplayAssociatedUserAsync(selectedResult);
                    }
                }
            }
        }

        private async Task DisplayUserDetailsAsync(JObject user)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"DisplayName: {user["displayName"]}");
            sb.AppendLine($"GivenName: {user["givenName"]}");
            sb.AppendLine($"Surname: {user["surname"]}");
            sb.AppendLine($"JobTitle: {user["jobTitle"]}");
            sb.AppendLine($"Mail: {user["mail"]}");
            sb.AppendLine($"MobilePhone: {user["mobilePhone"]}");
            sb.AppendLine($"BusinessPhones: {string.Join(", ", (user["businessPhones"] as JArray)?.Select(bp => bp.ToString()) ?? Enumerable.Empty<string>())}");
            sb.AppendLine($"OfficeLocation: {user["officeLocation"]}");
            sb.AppendLine($"UserPrincipalName: {user["userPrincipalName"]}");
            sb.AppendLine("------------");

            userDetailsTextBox.Visible = true;
            userDetailsTextBox.Text = sb.ToString();
        }

        private async Task DisplayDeviceDetailsAsync(JObject device)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Device Name: {device["deviceName"]}");
            sb.AppendLine($"Model: {device["model"]}");
            sb.AppendLine($"Serial Number: {device["serialNumber"]}");
            sb.AppendLine("------------");

            userDetailsTextBox.Visible = true;
            userDetailsTextBox.Text = sb.ToString();
        }

        private async Task DisplayAssociatedUserAsync(JObject device)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\n\nAssociated User:");

            var token = await GetAccessTokenAsync(CancellationToken.None); // Use CancellationToken.None here
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await httpClient.GetAsync($"https://graph.microsoft.com/v1.0/users/{device["userId"]}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var user = JObject.Parse(jsonResponse);

                    sb.AppendLine($"UserPrincipalName: {user["userPrincipalName"]}");
                    sb.AppendLine($"DisplayName: {user["displayName"]}");
                }
                else
                {
                    sb.AppendLine("Associated user not found.");
                }
            }

            userDetailsTextBox.AppendText(sb.ToString());
        }

        private async Task FetchAndDisplayUserDevicesAsync(JObject user)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\n\nRegistered Devices:");

            var token = await GetAccessTokenAsync(CancellationToken.None); // Pass CancellationToken.None here
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await httpClient.GetAsync($"https://graph.microsoft.com/v1.0/users/{user["id"]}/managedDevices");

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var devices = JObject.Parse(jsonResponse)["value"];

                    if (devices != null && devices.Any())
                    {
                        foreach (var device in devices)
                        {
                            sb.AppendLine($"Device Name: {device["deviceName"]?.ToString() ?? "N/A"}");
                            sb.AppendLine($"Model: {device["model"]?.ToString() ?? "N/A"}");
                            sb.AppendLine($"Serial Number: {device["serialNumber"]?.ToString() ?? "N/A"}");
                            sb.AppendLine("------------");
                        }
                    }
                    else
                    {
                        sb.AppendLine("No devices found.");
                    }
                }
                else
                {
                    sb.AppendLine($"Error fetching devices: {response.ReasonPhrase}");
                }
            }

            userDetailsTextBox.AppendText(sb.ToString());
        }
        private void nextPageButton_Click(object sender, EventArgs e)
        {
            if (_currentPage * _pageSize < _userSearchResults.Count + _deviceSearchResults.Count)
            {
                _currentPage++;
                DisplaySearchResults();
            }
        }

        private void previousPageButton_Click(object sender, EventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                DisplaySearchResults();
            }
        }
        private void UpdatePaginationButtons(int totalResults, int totalPages)
        {
            previousPageButton.Enabled = _currentPage > 1;
            nextPageButton.Enabled = _currentPage < totalPages;

            paginationInfoLabel.Text = $"Page {_currentPage} of {totalPages}";

            if (totalResults == 0)
            {
                previousPageButton.Enabled = false;
                nextPageButton.Enabled = false;
                paginationInfoLabel.Text = "No results found";
            }
        }

        private bool IsSearchInputValid(TextBox textBox, string placeholder)
        {
            if (string.IsNullOrWhiteSpace(textBox.Text) || textBox.Text == placeholder)
            {
                UpdateSearchStatus($"Please enter a valid search.", true);
                return false;
            }
            return true;
        }

        private void UpdateConnectionStatus(string status, Color color, FontStyle fontStyle)
        {
            connectionStatusLabel.Text = status;
            connectionStatusLabel.ForeColor = color;
            connectionStatusLabel.Font = new Font(connectionStatusLabel.Font, fontStyle);
        }

        private void UpdateSearchStatus(string status, bool visible)
        {
            searchStatusLabel.Text = status;
            searchStatusLabel.Visible = visible;
        }
    }
}
