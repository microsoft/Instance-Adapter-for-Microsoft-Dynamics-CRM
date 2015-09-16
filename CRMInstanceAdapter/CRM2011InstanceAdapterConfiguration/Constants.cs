namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration
{
    using System.Collections.Generic;

    /// <summary>
    /// Enumeration to hold the various event types
    /// </summary>
    public enum ConfigurationEventType
    {
        Event, Exception, Info
    }

    /// <summary>
    /// Enumeration to hold the configuration options
    /// </summary>
    public enum ConfigurationOption : int
    {
        Remove = 0,
        Install = 1
    }

    /// <summary>
    /// Enumeration to hold the various AD object classes
    /// </summary>
    public enum ObjectClass
    {
        User, Group, Computer
    }

    /// <summary>
    /// Enumeration to hold the various AD return types
    /// </summary>
    public enum ReturnType
    {
        DistinguishedName, ObjectGuid
    }

    /// <summary>
    /// Holds the parameter names as constants
    /// </summary>
    internal class Constants
    {
        public const string InstallType = "INSTALLTYPE";
        public const string DiscoveryServiceAddress = "DISCOVERYSERVICEADDRESS";
        public const string UserDomain = "DOMAIN";
        public const string UserName = "USERNAME";
        public const string Password = "USERPASSWORD";
        public const string Organization = "ORGANIZATION";
        public const string CrmAdminName = "CRMADMINNAME";
        public const string AdapterType = "ADAPTERTYPE";
        public const string CrmAdminPassword = "CRMADMINPASSWORD";
        public const string IntegratedOrganizations = "ORGS";
        public const string ConfigurationOption = "CONFIGOPTION";
        public const string IsProviderConfigureOnly = "ISPROVIDERCONFIG";
        public const string PrivCheckErrorCode = "0x80040220";
        public const string RegisterXmlFileName = "REGISTERFILENAME";

        /// <summary>
        /// Prevents a default instance of the <see cref="Constants"/> class from being created.
        /// </summary>
        private Constants()
        {
        }

        /// <summary>
        /// Gets a <c>List</c> that contains all of the parameter names
        /// </summary>
        internal static List<string> ArgumentNames
        {
            get
            {
                return new List<string> 
				{
				  InstallType, UserDomain, UserName, Password, Organization, CrmAdminName, CrmAdminPassword,
				  IntegratedOrganizations, ConfigurationOption, IsProviderConfigureOnly, RegisterXmlFileName, DiscoveryServiceAddress
				};
            }
        }
    }
}
