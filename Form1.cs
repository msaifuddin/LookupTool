using System.Text;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Azure.Core;
using Azure.Identity;

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
        private InteractiveBrowserCredential? _interactiveCredential;
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

            // Wire the cancel button event
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
                cancelButton.Text = "Logout";
                cancelButton.Enabled = true;  // Enable the cancel button after successful authentication
                return;
            }

            _isCancelRequested = false; // Reset the cancel flag
            countdownTime = 60; // Reset countdown

            cancelButton.Text = "Cancel"; // Ensure cancel button shows "Cancel"
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
                    _interactiveCredential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
                    {
                        ClientId = _clientId
                    });
                }

                // Countdown task
                var countdownTask = Task.Run(async () =>
                {
                    while (countdownTime > 0 && !_isConnected && !_isCancelRequested) // Check for cancellation
                    {
                        await Task.Delay(1000); // Wait for 1 second
                        countdownTime--;

                        this.Invoke((Action)(() =>
                        {
                            if (!_isConnected && !_isCancelRequested) // Update only if not connected or cancelled
                            {
                                UpdateConnectionStatus($"Authenticating... ({countdownTime}s)", Color.Orange, FontStyle.Bold);
                            }
                        }));
                    }

                    // Only trigger timeout if not connected and not cancelled
                    if (!_isConnected && countdownTime == 0 && !_isCancelRequested)
                    {
                        this.Invoke((Action)(() =>
                        {
                            UpdateConnectionStatus("Authentication timed out.", Color.Red, FontStyle.Bold);
                            connectButton.Enabled = true;  // Allow retry after cancel or timeout
                            cancelButton.Enabled = false;  // Disable the cancel button
                            cancelButton.Text = "Cancel";  // Reset button text
                            searchButton.Enabled = false;  // Keep the search button disabled if not connected
                        }));
                        _cancellationTokenSource?.Cancel(); // Cancel the token source on timeout
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

                        await GetAccessTokenAsync(_cancellationTokenSource.Token); // Use the cancellation token

                        // Fetch the UPN (connected user's given name)
                        await FetchUserUPNAsync(_cancellationTokenSource.Token);

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
                            cancelButton.Text = "Logout";
                            cancelButton.Enabled = true;  // Enable the cancel button after successful authentication
                        }));
                    }
                    catch (OperationCanceledException)
                    {
                        this.Invoke((Action)(() =>
                        {
                            UpdateConnectionStatus("Login cancelled.", Color.Red, FontStyle.Bold);
                            connectButton.Enabled = true;  // Re-enable connect button
                            cancelButton.Enabled = false;  // Disable cancel button
                            cancelButton.Text = "Cancel";  // Reset button text
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
                            cancelButton.Text = "Cancel";  // Reset button text
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
            if (cancelButton.Text == "Cancel")
            {
                _isCancelRequested = true;
                _cancellationTokenSource?.Cancel();

                // Update the status label immediately
                UpdateConnectionStatus("Login cancelled.", Color.Red, FontStyle.Bold);

                // Disable buttons appropriately
                connectButton.Enabled = true;  // Re-enable connect button
                cancelButton.Enabled = false;  // Disable cancel button
                searchButton.Enabled = false;  // Keep search button disabled
            }
            else if (cancelButton.Text == "Logout")
            {
                // Handle logout
                _isConnected = false;
                _accessToken = null;
                _tokenExpiration = DateTime.MinValue;
                _interactiveCredential = null; // Optionally reset the credential

                UpdateConnectionStatus("Logged out.", Color.Red, FontStyle.Bold);

                connectButton.Enabled = true;  // Re-enable connect button
                cancelButton.Enabled = false;  // Disable cancel button
                cancelButton.Text = "Cancel";  // Reset button text
                searchButton.Enabled = false;  // Disable search button

                // Clear any UI elements that need to be cleared
                ClearResults();
                totalResultsLabel.Text = "";
                paginationInfoLabel.Text = "";
                userDetailsTextBox.Visible = false;
                _userUPN = null;
            }
        }

        // Adjust GetAccessTokenAsync to take CancellationToken and implement token caching
        private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            if (_interactiveCredential == null)
            {
                throw new InvalidOperationException("Interactive credential is not initialized.");
            }

            if (_accessToken != null && DateTime.UtcNow < _tokenExpiration)
            {
                return _accessToken;
            }

            var tokenRequestContext = new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" });
            var tokenResponse = await _interactiveCredential.GetTokenAsync(tokenRequestContext, cancellationToken);

            _accessToken = tokenResponse.Token;
            _tokenExpiration = tokenResponse.ExpiresOn.UtcDateTime; // Use ExpiresOn

            return _accessToken;
        }

        // Fetch UPN after authentication
        private async Task FetchUserUPNAsync(CancellationToken cancellationToken)
        {
            var token = await GetAccessTokenAsync(cancellationToken);
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me", cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var user = JObject.Parse(jsonResponse);
                    _userUPN = user["givenName"]?.ToString(); // Use givenName
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

                var token = await GetAccessTokenAsync(CancellationToken.None);
                using (HttpClient httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    // Escape searchText to prevent OData errors
                    string escapedSearchText = Uri.EscapeDataString(searchText.Replace("'", "''"));

                    var query = $"https://graph.microsoft.com/v1.0/users?$filter=startswith(userPrincipalName,'{escapedSearchText}') or startswith(displayName,'{escapedSearchText}') or startswith(givenName,'{escapedSearchText}') or startswith(surname,'{escapedSearchText}')";

                    var response = await httpClient.GetAsync(query);

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

                var token = await GetAccessTokenAsync(CancellationToken.None);
                using (HttpClient httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    // Escape searchText to prevent OData errors
                    string escapedSearchText = searchText.Replace("'", "''");

                    // Search by serial number
                    var query = $"https://graph.microsoft.com/v1.0/deviceManagement/managedDevices?$filter=serialNumber eq '{escapedSearchText}'";
                    var response = await httpClient.GetAsync(query);

                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        var devices = JObject.Parse(jsonResponse)["value"];

                        if (devices != null && devices.Any())
                        {
                            _deviceSearchResults = devices.Select(d => d?.ToObject<JObject>()).ToList();
                            return; // Devices found by serial number
                        }
                    }
                    else
                    {
                        UpdateSearchStatus($"Error searching device by serial number: {response.ReasonPhrase}", true);
                        return;
                    }

                    // If no devices found by serial number, search by device name
                    query = $"https://graph.microsoft.com/v1.0/deviceManagement/managedDevices?$filter=deviceName eq '{escapedSearchText}'";
                    response = await httpClient.GetAsync(query);

                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        var devices = JObject.Parse(jsonResponse)["value"];

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

            if (totalResults == 0)
            {
                UpdatePaginationButtons(0, 0);
                return;
            }

            int totalPages = (int)Math.Ceiling((double)totalResults / _pageSize);

            if (_currentPage > totalPages) _currentPage = totalPages;

            var pagedResults = GetPagedResults();

            foreach (var (result, index) in pagedResults.Select((result, i) => (result, i)))
            {
                if (result != null)
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
                        DisplayUserDetails(selectedResult);
                        await FetchAndDisplayUserDevicesAsync(selectedResult);
                    }
                    else if (selectedResult.ContainsKey("deviceName"))
                    {
                        DisplayDeviceDetails(selectedResult);
                        await DisplayAssociatedUserAsync(selectedResult);
                    }
                }
            }
        }

        private void DisplayUserDetails(JObject user)
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

        private void DisplayDeviceDetails(JObject device)
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

            var token = await GetAccessTokenAsync(CancellationToken.None);
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var userId = device["userId"]?.ToString();

                if (!string.IsNullOrEmpty(userId))
                {
                    var response = await httpClient.GetAsync($"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(userId)}");

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
                else
                {
                    sb.AppendLine("No associated user ID.");
                }
            }

            userDetailsTextBox.AppendText(sb.ToString());
        }

        private async Task FetchAndDisplayUserDevicesAsync(JObject user)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\n\nRegistered Devices:");

            var token = await GetAccessTokenAsync(CancellationToken.None);
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var userId = user["id"]?.ToString();

                if (!string.IsNullOrEmpty(userId))
                {
                    var response = await httpClient.GetAsync($"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(userId)}/managedDevices");

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
                else
                {
                    sb.AppendLine("User ID is not available.");
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
            if (totalResults == 0)
            {
                previousPageButton.Enabled = false;
                nextPageButton.Enabled = false;
                paginationInfoLabel.Text = "No results found";
            }
            else
            {
                previousPageButton.Enabled = _currentPage > 1;
                nextPageButton.Enabled = _currentPage < totalPages;
                paginationInfoLabel.Text = $"Page {_currentPage} of {totalPages}";
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
