using Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration.Properties;
using Microsoft.Dynamics.Integration.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration
{
    static class Program
    {
        static AssemblyResolver resolver = new AssemblyResolver(true);

        public static ConfigurationUtilities InstallUtils = new ConfigurationUtilities();
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            System.Windows.Forms.Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);
            System.AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            InstallUtils = new ConfigurationUtilities();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Dictionary<string, string> commandLineValues = new Dictionary<string, string>();
            foreach (string arg in args)
            {
                string[] tempSetting = arg.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                if (tempSetting.Length == 2)
                {
                    commandLineValues[tempSetting[0]] = tempSetting[1];
                }
            }

            if (commandLineValues.ContainsKey(Constants.AdapterType))
            {
                InstallUtils.SetAdapter(commandLineValues[Constants.AdapterType]);
            }

            if (commandLineValues.ContainsKey(Constants.DiscoveryServiceAddress))
            {
                InstallUtils.InstallAdapter.DiscoveryServiceAddress = new Uri(commandLineValues[Constants.DiscoveryServiceAddress]);
            }

            if (commandLineValues.ContainsKey(Constants.UserName))
            {
                InstallUtils.InstallAdapter.UserName = commandLineValues[Constants.UserName];
            }

            if (commandLineValues.ContainsKey(Constants.Password))
            {
                InstallUtils.InstallAdapter.UserPassword = commandLineValues[Constants.Password];
            }

            if (commandLineValues.ContainsKey(Constants.Organization))
            {
                InstallUtils.InstallAdapter.OrganizationName = commandLineValues[Constants.Organization];
            }

            Application.Run(new MainForm());
            TraceArguments();
        }
    
        internal static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show(string.Format(CultureInfo.CurrentCulture, Resources.UnhandledExceptionMessage), string.Format(CultureInfo.CurrentCulture, Resources.ApplicationExceptionCaption), MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, 0);
            Application.Exit();
        }

        internal static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            MessageBox.Show(string.Format(CultureInfo.CurrentCulture, Resources.UnhandledThreadExceptionMessage, e.Exception.Message), string.Format(CultureInfo.CurrentCulture, Resources.ThreadExceptionCaption), MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, 0);
            Application.Exit();
        }

        private static void TraceArguments()
        {
            Trace.Unindent();
            TraceLog.Info(string.Empty);
            foreach (string key in InstallUtils.Arguments.Keys)
            {
                if (InstallUtils.Arguments[key] != null && !key.Contains("PASSWORD"))
                {
                    TraceLog.Info(string.Format(CultureInfo.CurrentCulture, Resources.ConfigArgumentValueMessage, key, InstallUtils.Arguments[key].ToString()));
                }
            }

            TraceLog.Info(string.Format(CultureInfo.CurrentCulture, Resources.ConfigurationFinishedMessage));
        }

        // resolves adapters and mapping helpers.
        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return resolver.Resolve(new AssemblyName(args.Name));
        }
    }
}
