using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Dynamics.Integration.AdapterAbstractionLayer;
using Microsoft.Dynamics.Integration.Common;
using System.Diagnostics;

namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration
{
    public partial class ObjectProviderForm : Form
    {
        private List<NameValuePair<ObjectProvider>> allCompanyProviders;

        public ObjectProviderForm()
        {
            InitializeComponent();
            this.allCompanyProviders = new List<NameValuePair<ObjectProvider>>();
            this.FormClosing += new FormClosingEventHandler(ObjectProviderForm_FormClosing);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                FillTree();
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            } 

            this.Refresh();
        }

        internal void FillTree()
        {
            List<ObjectDefinition> dynamicObjectDefinitions = Program.InstallUtils.LoadObjectDefinitionsFromConfigs();
            foreach (ObjectProvider provider in Program.InstallUtils.AvailableProviders.OrderBy(p => p.DisplayName))
            {
                var configFile = dynamicObjectDefinitions.FirstOrDefault(str => str.RootDefinition.TypeName.ToUpperInvariant() == provider.Name.ToUpperInvariant());
                this.companyTreeView.Nodes.Add(new TreeNode(provider.DisplayName) { Checked = configFile != null });
                this.allCompanyProviders.Add(new NameValuePair<ObjectProvider>() { Name = Program.InstallUtils.InstallAdapter.OrganizationName, Value = provider });
            }
        }

        private void ObjectProviderForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                ConfigurationNavigation.ShowCancelVerifyMessageBox();
                e.Cancel = true;
            }
        }

        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            foreach (var node in companyTreeView.Nodes)
            {
                ((TreeNode)node).Checked = true;
            }
        }

        private void btnDeselectAll_Click(object sender, EventArgs e)
        {
            foreach (var node in companyTreeView.Nodes)
            {
                ((TreeNode)node).Checked = false;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private void btnNext_Click(object sender, EventArgs e)
        {
            GetConfiguredProviders();
            new StatusForm().Show();          

            this.Hide();
        }

        internal void GetConfiguredProviders()
        {
            Program.InstallUtils.SelectedProviders = new List<NameValuePair<ObjectProvider>>();
            var selectedCompanyProviders = from TreeNode node in this.companyTreeView.Nodes where node.Checked select node;
            foreach (TreeNode selectedNode in selectedCompanyProviders)
            {
                var tempNode = this.allCompanyProviders.Single(p => p.Value.DisplayName == selectedNode.Text);
                Program.InstallUtils.SelectedProviders.Add(tempNode);
            }
        }

    }
}
