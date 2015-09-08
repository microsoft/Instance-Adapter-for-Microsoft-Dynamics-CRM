namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.lblDescription = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.txtUserName = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.txtUrl = new System.Windows.Forms.TextBox();
            this.lblAvailableOrganizations = new System.Windows.Forms.Label();
            this.btnRefreshServices = new System.Windows.Forms.Button();
            this.lstOrganizations = new System.Windows.Forms.ListBox();
            this.btnSelectOrg = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblDescription
            // 
            this.lblDescription.Location = new System.Drawing.Point(13, 13);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(723, 31);
            this.lblDescription.TabIndex = 0;
            this.lblDescription.Text = resources.GetString("lblDescription.Text");
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(16, 88);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(56, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Password:";
            // 
            // txtPassword
            // 
            this.txtPassword.Location = new System.Drawing.Point(19, 104);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.Size = new System.Drawing.Size(400, 20);
            this.txtPassword.TabIndex = 2;
            this.txtPassword.UseSystemPasswordChar = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(16, 48);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(63, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "User Name:";
            // 
            // txtUserName
            // 
            this.txtUserName.Location = new System.Drawing.Point(19, 65);
            this.txtUserName.Name = "txtUserName";
            this.txtUserName.Size = new System.Drawing.Size(400, 20);
            this.txtUserName.TabIndex = 2;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(446, 48);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(32, 13);
            this.label3.TabIndex = 1;
            this.label3.Text = "URL:";
            // 
            // txtUrl
            // 
            this.txtUrl.Location = new System.Drawing.Point(449, 64);
            this.txtUrl.Name = "txtUrl";
            this.txtUrl.Size = new System.Drawing.Size(254, 20);
            this.txtUrl.TabIndex = 2;
            // 
            // lblAvailableOrganizations
            // 
            this.lblAvailableOrganizations.AutoSize = true;
            this.lblAvailableOrganizations.Location = new System.Drawing.Point(16, 145);
            this.lblAvailableOrganizations.Name = "lblAvailableOrganizations";
            this.lblAvailableOrganizations.Size = new System.Drawing.Size(117, 13);
            this.lblAvailableOrganizations.TabIndex = 1;
            this.lblAvailableOrganizations.Text = "Available Organizations";
            // 
            // btnRefreshServices
            // 
            this.btnRefreshServices.Location = new System.Drawing.Point(449, 102);
            this.btnRefreshServices.Name = "btnRefreshServices";
            this.btnRefreshServices.Size = new System.Drawing.Size(150, 23);
            this.btnRefreshServices.TabIndex = 3;
            this.btnRefreshServices.Text = "Refresh Services";
            this.btnRefreshServices.UseVisualStyleBackColor = true;
            this.btnRefreshServices.Click += new System.EventHandler(this.btnRefreshServices_Click);
            // 
            // lstOrganizations
            // 
            this.lstOrganizations.FormattingEnabled = true;
            this.lstOrganizations.Location = new System.Drawing.Point(19, 162);
            this.lstOrganizations.Name = "lstOrganizations";
            this.lstOrganizations.Size = new System.Drawing.Size(653, 251);
            this.lstOrganizations.TabIndex = 4;
            // 
            // btnSelectOrg
            // 
            this.btnSelectOrg.Location = new System.Drawing.Point(19, 420);
            this.btnSelectOrg.Name = "btnSelectOrg";
            this.btnSelectOrg.Size = new System.Drawing.Size(150, 23);
            this.btnSelectOrg.TabIndex = 5;
            this.btnSelectOrg.Text = "Configure";
            this.btnSelectOrg.UseVisualStyleBackColor = true;
            this.btnSelectOrg.Click += new System.EventHandler(this.btnSelectOrg_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(748, 472);
            this.Controls.Add(this.btnSelectOrg);
            this.Controls.Add(this.lstOrganizations);
            this.Controls.Add(this.btnRefreshServices);
            this.Controls.Add(this.txtUrl);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtUserName);
            this.Controls.Add(this.lblAvailableOrganizations);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.lblDescription);
            this.Name = "MainForm";
            this.Text = "Microsoft Dynamics CRM Instance Adapter Configuration";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtUserName;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtUrl;
        private System.Windows.Forms.Label lblAvailableOrganizations;
        private System.Windows.Forms.Button btnRefreshServices;
        private System.Windows.Forms.ListBox lstOrganizations;
        private System.Windows.Forms.Button btnSelectOrg;
    }
}