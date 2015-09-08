using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Windows.Forms;
using Microsoft.Dynamics.Integration.AdapterAbstractionLayer;
using Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration.Properties;
using Microsoft.Dynamics.Integration.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Discovery;
using System.Diagnostics;

namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration
{
    public partial class StatusForm : Form
    {
        private List<OrganizationDetail> integratedOrgs;
        private ConfigurationOption operationOption;

        public StatusForm()
        {
            InitializeComponent();
            Program.InstallUtils.ConfigurationEventPre += new EventHandler<ConfigurationEventArgs>(this.ConfigurationPreEventHandler);
            Program.InstallUtils.ConfigurationEventPost += new EventHandler<ConfigurationEventArgs>(this.ConfigurationPostEventHandler);
            Program.InstallUtils.ConfigurationEventException += new EventHandler<ConfigurationEventArgs>(this.ConfigurationEventException);
            this.operationOption = ConfigurationOption.Install;
            this.integratedOrgs = Program.InstallUtils.Arguments[Constants.IntegratedOrganizations] as List<OrganizationDetail>;
            this.FormClosing += new FormClosingEventHandler(this.StatusForm_FormClosing);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                Program.InstallUtils.SetOrgProperties(this.integratedOrgs.First());
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }
        private void StatusForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                ConfigurationNavigation.ShowCancelVerifyMessageBox();
                e.Cancel = true;
            }
        }

        private void ConfigurationPreEventHandler(object src, ConfigurationEventArgs args)
        {
            this.statusCheckListBox.Items.Add(args.Message);
            if (this.statusCheckListBox.Items.Count > 0)
            {
                this.statusCheckListBox.SelectedItem = this.statusCheckListBox.Items[this.statusCheckListBox.Items.Count - 1];
            }

            this.statusCheckListBox.Refresh();
        }

        private void ConfigurationPostEventHandler(object src, ConfigurationEventArgs args)
        {
            if (this.statusCheckListBox.Items.Count > 0)
            {
                this.statusCheckListBox.SetItemChecked(this.statusCheckListBox.Items.Count - 1, true);
            }

            this.statusCheckListBox.Refresh();
        }

        private void ConfigurationEventException(object sender, ConfigurationEventArgs args)
        {
            MessageBox.Show(string.Format(CultureInfo.CurrentCulture, args.Message), string.Format(CultureInfo.CurrentCulture, Resources.ExceptionCaption), MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, 0);
        }

        private void btnConfigure_Click(object sender, EventArgs e)
        {
            this.btnCancel.Enabled = false;
            this.btnConfigure.Enabled = false;
            int counter = 0;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                foreach (OrganizationDetail detail in this.integratedOrgs)
                {
                    Program.InstallUtils.InstallAdapter.OrganizationName = detail.UniqueName;
                    this.ConfigureObjectProviders(detail.UniqueName);
                    counter++;
                    if (counter < this.integratedOrgs.Count)
                    {
                        this.statusCheckListBox.Items.Clear();
                        this.Refresh();
                    }
                }
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }

            this.btnFinish.Enabled = true;
            this.AcceptButton = this.btnFinish;
            this.Focus();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private void ConfigureObjectProviders(string organizationName)
        {
            try
            {
                if (Program.InstallUtils.SelectedProviders != null)
                {
                    Program.InstallUtils.InstallAdapter.ImpersonateIntegrationUser = false;
                    var providers = Program.InstallUtils.SelectedProviders.FindAll(p => p.Name == organizationName);
                    foreach (NameValuePair<ObjectProvider> provider in providers)
                    {
                        if (this.operationOption == ConfigurationOption.Install)
                        {
                            ObjectProvider providerDetail = provider.Value;

                            if (providerDetail.Name.Contains(","))
                            {
                                Program.InstallUtils.WriteMToMRelationshipObjectDefinition(providerDetail);
                            }
                            else
                            {
                                // Only write the object definitions if we are installing
                                //Program.InstallUtils.WriteObjectDefinition(provider.Value);
                                Program.InstallUtils.WriteObjectDefinition(providerDetail);
                            }
                        }
                    }

                    // Possible location of picklist object provider creation.
                    Program.InstallUtils.WritePicklistObjectDefinition();
                }
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                TraceLog.Error(ex.Message, ex);
                MessageBox.Show(ex.Message, string.Format(CultureInfo.CurrentCulture, Resources.SoapExceptionCaption), MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, 0);
            }
            catch (Exception ex)
            {
                TraceLog.Error(ex.Message, ex);
                MessageBox.Show(ex.Message, string.Format(CultureInfo.CurrentCulture, Resources.ExceptionCaption), MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, 0);
            }
        }

        private void btnFinish_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.StatusForm_FormClosing(sender, new FormClosingEventArgs(CloseReason.UserClosing, false));
        }
    }
}
