namespace searchAll
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TextBox emailTextBox;
        private System.Windows.Forms.Button searchButton;
        private System.Windows.Forms.Button connectButton;  // Connect button for authentication
        private System.Windows.Forms.Label connectionStatusLabel;
        private System.Windows.Forms.TextBox userDetailsTextBox;
        private System.Windows.Forms.Button nextPageButton;
        private System.Windows.Forms.Button previousPageButton;
        private System.Windows.Forms.Label paginationInfoLabel;  // Label to show page x/y
        private System.Windows.Forms.Label totalResultsLabel;   // Label to show total results found
        private System.Windows.Forms.ListBox userListBox;  // ListBox to show search results
        private System.Windows.Forms.Label searchStatusLabel;  // Label for search status
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            emailTextBox = new TextBox();
            searchButton = new Button();
            connectButton = new Button();
            connectionStatusLabel = new Label();
            userDetailsTextBox = new TextBox();
            nextPageButton = new Button();
            previousPageButton = new Button();
            paginationInfoLabel = new Label();
            totalResultsLabel = new Label();
            userListBox = new ListBox();
            searchStatusLabel = new Label();
            cancelButton = new Button();
            SuspendLayout();
            // 
            // emailTextBox
            // 
            emailTextBox.Location = new Point(12, 58);
            emailTextBox.Name = "emailTextBox";
            emailTextBox.Size = new Size(401, 31);
            emailTextBox.TabIndex = 0;
            emailTextBox.Enter += RemovePlaceholder;
            emailTextBox.Leave += SetPlaceholder;
            // 
            // searchButton
            // 
            searchButton.Location = new Point(419, 58);
            searchButton.Name = "searchButton";
            searchButton.Size = new Size(75, 31);
            searchButton.TabIndex = 1;
            searchButton.Text = "Search";
            searchButton.UseVisualStyleBackColor = true;
            searchButton.Click += searchButton_Click;
            // 
            // connectButton
            // 
            connectButton.Location = new Point(12, 18);
            connectButton.Name = "connectButton";
            connectButton.Size = new Size(100, 34);
            connectButton.TabIndex = 6;
            connectButton.Text = "Connect";
            connectButton.UseVisualStyleBackColor = true;
            connectButton.Click += connectButton_Click;
            // 
            // connectionStatusLabel
            // 
            connectionStatusLabel.AutoSize = true;
            connectionStatusLabel.BackColor = SystemColors.Control;
            connectionStatusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            connectionStatusLabel.ForeColor = Color.Red;
            connectionStatusLabel.Location = new Point(228, 24);
            connectionStatusLabel.Name = "connectionStatusLabel";
            connectionStatusLabel.Size = new Size(266, 25);
            connectionStatusLabel.TabIndex = 2;
            connectionStatusLabel.Text = "Not Connected. Click to login.";
            // 
            // userDetailsTextBox
            // 
            userDetailsTextBox.Location = new Point(12, 278);
            userDetailsTextBox.Multiline = true;
            userDetailsTextBox.Name = "userDetailsTextBox";
            userDetailsTextBox.ReadOnly = true;
            userDetailsTextBox.ScrollBars = ScrollBars.Vertical;
            userDetailsTextBox.Size = new Size(482, 241);
            userDetailsTextBox.TabIndex = 3;
            // 
            // nextPageButton
            // 
            nextPageButton.Location = new Point(399, 235);
            nextPageButton.Name = "nextPageButton";
            nextPageButton.Size = new Size(95, 32);
            nextPageButton.TabIndex = 4;
            nextPageButton.Text = "Next";
            nextPageButton.UseVisualStyleBackColor = true;
            nextPageButton.Click += nextPageButton_Click;
            // 
            // previousPageButton
            // 
            previousPageButton.Location = new Point(298, 235);
            previousPageButton.Name = "previousPageButton";
            previousPageButton.Size = new Size(95, 32);
            previousPageButton.TabIndex = 5;
            previousPageButton.Text = "Previous";
            previousPageButton.UseVisualStyleBackColor = true;
            previousPageButton.Click += previousPageButton_Click;
            // 
            // paginationInfoLabel
            // 
            paginationInfoLabel.AutoSize = true;
            paginationInfoLabel.Location = new Point(298, 522);
            paginationInfoLabel.Name = "paginationInfoLabel";
            paginationInfoLabel.Size = new Size(60, 25);
            paginationInfoLabel.TabIndex = 7;
            paginationInfoLabel.Text = "Ready";
            // 
            // totalResultsLabel
            // 
            totalResultsLabel.AutoSize = true;
            totalResultsLabel.Location = new Point(12, 522);
            totalResultsLabel.Name = "totalResultsLabel";
            totalResultsLabel.Size = new Size(161, 25);
            totalResultsLabel.TabIndex = 8;
            totalResultsLabel.Text = "No results to show";
            // 
            // userListBox
            // 
            userListBox.ItemHeight = 25;
            userListBox.Location = new Point(12, 100);
            userListBox.Name = "userListBox";
            userListBox.Size = new Size(482, 129);
            userListBox.TabIndex = 9;
            userListBox.SelectedIndexChanged += userListBox_SelectedIndexChanged;
            // 
            // searchStatusLabel
            // 
            searchStatusLabel.AutoSize = true;
            searchStatusLabel.LiveSetting = System.Windows.Forms.Automation.AutomationLiveSetting.Polite;
            searchStatusLabel.Location = new Point(12, 239);
            searchStatusLabel.Name = "searchStatusLabel";
            searchStatusLabel.Size = new Size(0, 25);
            searchStatusLabel.TabIndex = 10;
            searchStatusLabel.Visible = false;
            // 
            // cancelButton
            // 
            cancelButton.Location = new Point(118, 18);
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new Size(100, 34);
            cancelButton.TabIndex = 11;
            cancelButton.Text = "Cancel";
            cancelButton.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            BackColor = SystemColors.Control;
            ClientSize = new Size(506, 556);
            Controls.Add(cancelButton);
            Controls.Add(userListBox);
            Controls.Add(searchStatusLabel);
            Controls.Add(totalResultsLabel);
            Controls.Add(paginationInfoLabel);
            Controls.Add(previousPageButton);
            Controls.Add(nextPageButton);
            Controls.Add(userDetailsTextBox);
            Controls.Add(connectionStatusLabel);
            Controls.Add(searchButton);
            Controls.Add(emailTextBox);
            Controls.Add(connectButton);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Account and Device Lookup";
            ResumeLayout(false);
            PerformLayout();
        }

        private Button cancelButton;
    }
}
