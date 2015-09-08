using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Xrm.Sdk.Discovery;
using System.Diagnostics;

namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            this.txtPassword.Text = Program.InstallUtils.InstallAdapter.UserPassword;
            this.txtUserName.Text = Program.InstallUtils.InstallAdapter.UserName;
            this.txtUrl.Text = Program.InstallUtils.InstallAdapter.DiscoveryServiceAddress.ToString();
            RenderOrgDetails();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private void btnSelectOrg_Click(object sender, EventArgs e)
        {
            Program.InstallUtils.InstallAdapter.OrganizationName = (string)lstOrganizations.SelectedValue;
            List<OrganizationDetail> selectedOrgs = new List<OrganizationDetail>();
            selectedOrgs.Add((OrganizationDetail)lstOrganizations.SelectedItem);
            Program.InstallUtils.Arguments[Constants.IntegratedOrganizations] = selectedOrgs;
            var provider = new ObjectProviderForm();
            provider.Show();
            this.Hide();            
        }

        private void btnRefreshServices_Click(object sender, EventArgs e)
        {
            Program.InstallUtils.InstallAdapter.UserPassword = this.txtPassword.Text;
            Program.InstallUtils.InstallAdapter.UserName = this.txtUserName.Text;
            Program.InstallUtils.InstallAdapter.DiscoveryServiceAddress = new Uri(this.txtUrl.Text);
            RenderOrgDetails();
        }

        private void RenderOrgDetails()
        {
            var details = Program.InstallUtils.RetrieveOrgDetails();
            lstOrganizations.DataSource = details;
            lstOrganizations.DisplayMember = "FriendlyName";
            lstOrganizations.ValueMember = "UniqueName";
        }

    }
}
