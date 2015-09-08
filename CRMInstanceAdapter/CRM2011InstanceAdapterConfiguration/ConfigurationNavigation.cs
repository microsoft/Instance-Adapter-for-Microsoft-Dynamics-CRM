using Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration.Properties;
using Microsoft.Dynamics.Integration.Common;
using System.Globalization;
using System.Windows.Forms;

namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration
{
    /// <summary>
    /// A static class that holds the instances of the configuration forms that are currently in use
    /// </summary>
    internal static class ConfigurationNavigation
    {
        /// <summary>
        /// Displays the cancle verification dialog
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions")]
        internal static void ShowCancelVerifyMessageBox()
        {
            DialogResult result = MessageBox.Show(string.Format(CultureInfo.CurrentCulture, Resources.CancelConfimationMessage), string.Format(CultureInfo.CurrentCulture, Resources.CRMAdapterConfigurationCaption), MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2);
            if (result == DialogResult.Yes)
            {
                TraceLog.Info(string.Format(CultureInfo.CurrentCulture, Resources.ConfigurationCanceledMessage));
                Application.Exit();
            }
        }
    }
}
