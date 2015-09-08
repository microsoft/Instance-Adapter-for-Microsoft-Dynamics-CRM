namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration
{
    partial class StatusForm
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
            this.lblConfigStatusForOrganization = new System.Windows.Forms.Label();
            this.statusCheckListBox = new System.Windows.Forms.CheckedListBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnFinish = new System.Windows.Forms.Button();
            this.btnConfigure = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblConfigStatusForOrganization
            // 
            this.lblConfigStatusForOrganization.AutoSize = true;
            this.lblConfigStatusForOrganization.Location = new System.Drawing.Point(13, 13);
            this.lblConfigStatusForOrganization.Name = "lblConfigStatusForOrganization";
            this.lblConfigStatusForOrganization.Size = new System.Drawing.Size(179, 13);
            this.lblConfigStatusForOrganization.TabIndex = 0;
            this.lblConfigStatusForOrganization.Text = "Configuration Status for Organization";
            // 
            // statusCheckListBox
            // 
            this.statusCheckListBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.statusCheckListBox.FormattingEnabled = true;
            this.statusCheckListBox.Location = new System.Drawing.Point(13, 30);
            this.statusCheckListBox.Name = "statusCheckListBox";
            this.statusCheckListBox.Size = new System.Drawing.Size(651, 274);
            this.statusCheckListBox.TabIndex = 1;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(589, 310);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "&Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnFinish
            // 
            this.btnFinish.Enabled = false;
            this.btnFinish.Location = new System.Drawing.Point(508, 309);
            this.btnFinish.Name = "btnFinish";
            this.btnFinish.Size = new System.Drawing.Size(75, 23);
            this.btnFinish.TabIndex = 3;
            this.btnFinish.Text = "&Finish";
            this.btnFinish.UseVisualStyleBackColor = true;
            this.btnFinish.Click += new System.EventHandler(this.btnFinish_Click);
            // 
            // btnConfigure
            // 
            this.btnConfigure.Location = new System.Drawing.Point(427, 309);
            this.btnConfigure.Name = "btnConfigure";
            this.btnConfigure.Size = new System.Drawing.Size(75, 23);
            this.btnConfigure.TabIndex = 4;
            this.btnConfigure.Text = "Co&nfigure";
            this.btnConfigure.UseVisualStyleBackColor = true;
            this.btnConfigure.Click += new System.EventHandler(this.btnConfigure_Click);
            // 
            // StatusForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(676, 367);
            this.Controls.Add(this.btnConfigure);
            this.Controls.Add(this.btnFinish);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.statusCheckListBox);
            this.Controls.Add(this.lblConfigStatusForOrganization);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "StatusForm";
            this.Text = "Microsoft Dynamics CRM Dynamic Adapter Configuration";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblConfigStatusForOrganization;
        internal System.Windows.Forms.CheckedListBox statusCheckListBox;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnFinish;
        private System.Windows.Forms.Button btnConfigure;
    }
}