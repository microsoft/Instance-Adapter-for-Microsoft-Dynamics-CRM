namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm
{
    using AdapterAbstractionLayer;
    using Common;
    using Properties;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.ServiceModel.Description;
    using System.Xml;
    using System.Xml.Serialization;
    using Win32;
    using Xrm.Sdk;
    using Xrm.Sdk.Client;
    using Xrm.Sdk.Discovery;
    using Xrm.Sdk.Messages;
    using Xrm.Sdk.Query;

    /// <summary>
    /// The <c>Adapter</c> used for interacting with Dynamics CRM 2011.
    /// </summary>
    public abstract class DynamicCrmAdapter : Adapter, IDisposable, ISettingsExtender
    {
        private bool disposed;
        private const string ConfigUtilityDefaultFilePath = @"Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration.exe";
        private const string Version = "2011";
        private static Dictionary<string, List<NameStringValuePair>> companyListCache = new Dictionary<string, List<NameStringValuePair>>();
        private object syncObject = new object();
        private OrganizationDetail orgExtDetail;
        private string endpoint;
        private IDiscoveryService discoveryService;
        private IOrganizationService orgService;
        private Guid integrationUserId = Guid.Empty;
        private Guid systemUserId = Guid.Empty;
        private System.Collections.ObjectModel.Collection<ObjectProvider> providers;
        private ServerConnection.CrmServerConfiguration serverConfiguration;
        private ServerConnection serverConnection;
        private AuthenticationCredentials authenticationCredentials;
        private const string CompanySettingName = "Company";
        private const string DiscoveryURLSettingName = "DiscoveryServiceAddress";
        private const string UserSettingName = "UserName";
        private const string PasswordSettingName = "UserPassword";
        private const string ConfigUtilitySettingName = "ConfigUtility";

        #region FromCRMBaseAdapter
        private const string ObjectConfigFolderName = "ObjectConfig";
        private Guid baseCurrencyKey = Guid.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this instance should impersonate the INTEGRATION user or not.
        /// </summary>
        public bool ImpersonateIntegrationUser
        {
            get
            {
                return Convert.ToBoolean(this.Settings["ImpersonateIntegrationUser"].ToString(), CultureInfo.CurrentCulture);
            }

            set
            {
                this.Settings["ImpersonateIntegrationUser"] = value.ToString();
            }
        }

        public string GetSettingsHash()
        {
            // This adapter's settings are distinct based on OrganizationName.
            return this.OrganizationName;
        }

        /// <summary>
        /// Gets or sets this <c>Adapter</c>'s base currency id.
        /// </summary>
        public Guid BaseCurrencyId
        {
            get { return this.baseCurrencyKey == Guid.Empty ? this.GetBaseCurrencyId() : this.baseCurrencyKey; }
            set { this.baseCurrencyKey = value; }
        }

        /// <summary>
        /// Gets or sets this <c>Adapter</c>'s <c>organization</c>'s name.
        /// </summary>
        public string OrganizationName
        {
            get
            {
                return this.Settings["OrganizationName"];
            }

            set
            {
                this.Settings["OrganizationName"] = value;
            }
        }

        public string UserName
        {
            get
            {
                return this.Settings["UserName"];
            }

            set
            {
                this.Settings["UserName"] = value;
            }
        }

        /// <summary>
        /// Gets or sets this user's password to use when calling the CRM services.
        /// </summary>
        public string UserPassword
        {
            get
            {
                return this.Settings["UserPassword"];
            }

            set
            {
                this.Settings["UserPassword"] = value;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter")]
        public string GetConfigPath<T>()
        {
            if (!string.IsNullOrEmpty(this.OrganizationName))
            {
                var adapter = this.GetType().Name;
                string companyPath = Path.Combine(Path.Combine(Path.Combine(Path.GetDirectoryName(typeof(T).Assembly.Location), ObjectConfigFolderName), adapter), this.OrganizationName);
                if (Directory.Exists(companyPath))
                {
                    return companyPath;
                }
            }

            return Path.Combine(Path.GetDirectoryName(typeof(T).Assembly.Location), ObjectConfigFolderName);
        }

        /// <summary>
        /// Gets or sets this <c>Adapter</c>'s base currency iso code.
        /// </summary>
        public string BaseCurrency
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets this <c>Adapter</c>'s proxy server.
        /// </summary>
        public string ProxyServer
        {
            get
            {
                return this.Settings["ProxyServerName"];
            }

            set
            {
                this.Settings["ProxyServerName"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the timestamp for calling the CRM Metadata Service.
        /// </summary>
        public string MetadataTimestamp
        {
            get
            {
                return this.Settings["MetadataTimestamp"];
            }

            set
            {
                this.Settings["MetadataTimestamp"] = value;
            }
        }
        #endregion        

        #region Properties

        public AuthenticationCredentials CrmAuthenticationCredentials
        {
            get
            {
                this.authenticationCredentials = new AuthenticationCredentials();
                this.authenticationCredentials.ClientCredentials = this.ServerConfiguration.Credentials;
                this.authenticationCredentials.ClientCredentials.SupportInteractive = false;
                if (this.ServerConfiguration.DeviceCredentials != null)
                {
                    this.authenticationCredentials.ClientCredentials = new ClientCredentials();
                    this.authenticationCredentials.ClientCredentials.UserName.UserName = this.UserName;
                    this.authenticationCredentials.ClientCredentials.UserName.Password = this.UserPassword;
                    this.authenticationCredentials.SupportingCredentials = new AuthenticationCredentials();
                    this.authenticationCredentials.SupportingCredentials.ClientCredentials = this.ServerConfiguration.DeviceCredentials;
                }

                return this.authenticationCredentials;
            }
        }

        /// <summary>
        /// Gets the URL for calling the CRM Discovery Service.
        /// </summary>
        public string DiscoveryServiceUrl
        {
            get
            {
                return this.DiscoveryServiceAddress.ToString();
            }
        }

        public override System.Collections.ObjectModel.Collection<ObjectProvider> ObjectReaderProviders
        {
            get
            {
                //Had to place these methods on the source and destination, they'll need to be re-used when the inherited adapters are removed
                return base.ObjectReaderProviders;
            }
        }

        public override System.Collections.ObjectModel.Collection<ObjectProvider> ObjectWriterProviders
        {
            get
            {
                //Had to place these methods on the source and destination, they'll need to be re-used when the inherited adapters are removed
                return base.ObjectWriterProviders;
            }
        }

        public string ConfigUtilityFilePath
        {
            get
            {
                return this.Settings[ConfigUtilitySettingName];
            }

            set
            {
                this.Settings[ConfigUtilitySettingName] = value;
            }
        }


        /// <summary>
        /// Gets or sets the URL for calling the CRM Discovery Service.
        /// </summary>
        public Uri DiscoveryServiceAddress
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.Settings["DiscoveryServiceAddress"]))
                {
                    return new Uri(string.Empty, UriKind.Relative);
                }
                else
                {
                    return new Uri(this.Settings["DiscoveryServiceAddress"]);
                }

            }

            set
            {
                this.Settings["DiscoveryServiceAddress"] = value != null ? value.ToString() : null;
            }
        }

        /// <summary>
        /// Gets the instance of the <c>CrmDiscoveryService</c> for this <c>CRMAdapter</c>.
        /// </summary>
        public IDiscoveryService DiscoveryClient
        {
            get { return this.GetCrmDiscoveryWebServiceClient(); }
        }

        /// <summary>
        /// Gets the instance of the <c>CrmService</c> for this <c>CRMAdapter</c>.
        /// </summary>
        public IOrganizationService OrganizationService
        {
            get
            {
                return this.GetCrmWebServiceClient();
            }
        }

        public ServerConnection ServerConnection
        {
            get
            {
                if (this.serverConnection == null)
                {
                    this.serverConnection = new ServerConnection(this.DiscoveryServiceAddress, this.UserName, this.UserPassword);
                }

                return this.serverConnection;
            }
        }

        public ServerConnection.CrmServerConfiguration ServerConfiguration
        {
            get
            {
                if (this.serverConfiguration == null)
                {
                    this.serverConfiguration = this.ServerConnection.GetServerConfiguration();
                }

                return this.serverConfiguration;
            }
        }

        /// <summary>
        /// Gets the systemuserid for the INTEGRATION user for the current organization.
        /// </summary>
        public Guid IntegrationUserId
        {
            get
            {
                if (this.integrationUserId == Guid.Empty)
                {
                    this.integrationUserId = this.GetSystemUserId(string.Empty, null);
                }

                return this.integrationUserId;
            }
        }

        /// <summary>
        /// Gets the systemuserid for the SYSTEM user for the current organization.
        /// </summary>
        public Guid SystemUserId
        {
            get
            {
                if (this.systemUserId == Guid.Empty)
                {
                    this.systemUserId = this.GetSystemUserId("SYSTEM", null);
                }

                return this.systemUserId;
            }
        }

        #endregion

        #region Methods

        public override ObjectProvider GetObjectProvider(ObjectProviderBinder binder)
        {
            if (binder == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage), new ArgumentNullException("binder")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            // Check to see if this is a custom entity
            if (binder.ObjectProviderProxy.ObjectDefinition != null && !IsStaticObjectProvider(binder.ObjectProviderProxy.ObjectDefinition))
            {
                return this.GetObjectProviders().SingleOrDefault(p => p.Id == GetDynamicProviderId(binder.ObjectProviderProxy.ObjectDefinition));
            }

            return base.GetObjectProvider(binder);
        }

        public string RetrieveMetadataTimestamp()
        {
            if (string.IsNullOrEmpty(this.OrganizationName))
            {
                NameStringValuePair org = this.GetConfiguredOrganizations(true, null).FirstOrDefault();
                if (org == null)
                {
                    return string.Empty;
                }

                this.OrganizationName = org.Value;
            }

            OrganizationRequest req = new OrganizationRequest("RetrieveTimestamp");
            OrganizationResponse res = this.OrganizationService.Execute(req);
            return res.Results["Timestamp"].ToString();
        }

        /// <summary>
        /// Initialize this <c>Adapter</c>'s settings list.
        /// </summary>
        /// <returns>An <c>IList</c> of <c>SettingsValue</c> objects.</returns>
        protected override IList<SettingsValue> InitializeSettings()
        {
            // CRM 2011 will just use a deiscovery service URL provided by the user rather than piecing it together
            var list = base.InitializeSettings();
            var setting = new SettingsValue();
            var propertyChangedSettings = new List<SettingsValue>();

            setting.FieldDefinition = GetCompanyDefField();
            setting.Attributes = SettingsValueAttributes.Site | SettingsValueAttributes.SupportsValueList;
            setting.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(this.OrganizationNamePropertyChanged);
            list.Add(setting);

            setting = new SettingsValue();
            setting.FieldDefinition = GetUserNameDefField();
            setting.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(this.UserNameOrPasswordPropertyChanged);
            list.Add(setting);

            setting = new SettingsValue();
            setting.FieldDefinition = GetUserPasswordDefField();
            setting.Attributes = SettingsValueAttributes.Password;
            setting.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(this.UserNameOrPasswordPropertyChanged);
            list.Add(setting);

            setting = new SettingsValue();
            setting.Value = true.ToString();
            setting.FieldDefinition = GetImpersonateIntegrationUserDefField();
            setting.Attributes = SettingsValueAttributes.HiddenFromUI;
            setting.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(this.ImpersonateIntegrationUserPropertyChanged);
            list.Add(setting);

            setting = new SettingsValue();
            setting.FieldDefinition = GetProxyServerName();
            setting.Attributes = SettingsValueAttributes.HiddenFromUI;
            setting.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(this.MetadataTimestampPropertyChanged);
            list.Add(setting);

            setting = new SettingsValue();
            setting.FieldDefinition = GetMetadataTimestamp();
            setting.Attributes = SettingsValueAttributes.HiddenFromUI;
            setting.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(this.MetadataTimestampPropertyChanged);
            list.Add(setting);

            setting = new SettingsValue();
            setting.FieldDefinition = GetDiscoveryServiceUrlSetting();
            setting.Value = @"https://<host>:<port>/XRMServices/2011/Discovery.svc";
            propertyChangedSettings.Add(setting);
            //setting.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(DiscoveryUrlPropertyChanged);
            list.Add(setting);

            setting = new SettingsValue();
            setting.FieldDefinition = GetConfigUtilityLocationSetting();
            setting.Attributes = SettingsValueAttributes.Informational;
            setting.Value = ConfigUtilityDefaultFilePath;
            propertyChangedSettings.Add(setting);
            list.Add(setting);

            var userNameSetting = list.FirstOrDefault(s => s.FieldDefinition.Name == "UserName");
            if (userNameSetting != null)
            {
                userNameSetting.FieldDefinition.Description = Resources.CRMAdapterSettingsUserNameDesc;
                propertyChangedSettings.Add(userNameSetting);
            }

            foreach (var s in propertyChangedSettings)
            {
                s.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(setting_PropertyChanged);
            }

            return list;
        }

        void setting_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var setting = sender as SettingsValue;
            if (setting != null && (setting.FieldDefinition.Name == DiscoveryURLSettingName || setting.FieldDefinition.Name == UserSettingName || setting.FieldDefinition.Name == PasswordSettingName))
            {
                this.ConfigUtilityFilePath = GetConfigUtilityPath();
            }
        }

        private string GetConfigUtilityPath()
        {
            string serverArg = string.IsNullOrWhiteSpace(this.Settings["DiscoveryServiceAddress"]) ? string.Empty : "DISCOVERYSERVICEADDRESS=" + this.Settings["DiscoveryServiceAddress"];
            string username = string.IsNullOrWhiteSpace(this.UserName) ? string.Empty : "USERNAME=" + this.UserName;
            string password = string.IsNullOrWhiteSpace(this.UserPassword) ? string.Empty : "USERPASSWORD=" + this.UserPassword;
            string type = "ADAPTERTYPE=" + this.GetType().Name;
            // TODO: Add argument for initial organization.

            return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4}", ConfigUtilityDefaultFilePath, serverArg, username, password, type);
        }

        internal static bool IsStaticObjectProvider(ObjectDefinition objDef)
        {
            return objDef.RootDefinition.FindAttribute("EntityMetadataId") == null;
        }

        internal static Guid GetDynamicProviderId(ObjectDefinition objDef)
        {
            return new Guid(objDef.RootDefinition.FindAttribute("EntityMetadataId").Value);
        }

        private static FieldDefinition GetCompanyDefField()
        {
            var fld = new FieldDefinition();
            fld.DisplayName = Resources.CRMAdapterSettingsOrganizationDisplayName;
            fld.Name = "OrganizationName";
            fld.IsRequired = true;
            fld.TypeDefinition = new SimpleType() { ClrType = typeof(string), Name = "string" };
            return fld;
        }

        private static FieldDefinition GetMetadataTimestamp()
        {
            var fld = new FieldDefinition();
            fld.Name = "MetadataTimestamp";
            fld.IsRequired = false;
            fld.TypeDefinition = new SimpleType() { ClrType = typeof(string), Name = "string" };
            return fld;
        }

        private static FieldDefinition GetProxyServerName()
        {
            var fld = new FieldDefinition();
            fld.Name = "ProxyServerName";
            fld.IsRequired = false;
            fld.TypeDefinition = new SimpleType() { ClrType = typeof(string), Name = "string" };
            return fld;
        }

        private static FieldDefinition GetUserPasswordDefField()
        {
            var fld = new FieldDefinition();
            fld.DisplayName = Resources.CRMAdapterSettingsUserPasswordDisplayName;
            fld.Description = Resources.CRMAdapterSettingsUserPasswordDesc;
            fld.Name = PasswordSettingName;
            fld.IsRequired = true;
            fld.TypeDefinition = new SimpleType() { ClrType = typeof(string), Name = "string" };
            return fld;
        }

        private static FieldDefinition GetImpersonateIntegrationUserDefField()
        {
            var fld = new FieldDefinition();
            fld.DisplayName = Resources.CRMAdapterSettingsIntegrationUserId;
            fld.Name = "ImpersonateIntegrationUser";
            fld.IsRequired = false;
            fld.TypeDefinition = new SimpleType() { ClrType = typeof(bool), Name = "bool" };
            return fld;
        }

        private static FieldDefinition GetConfigUtilityLocationSetting()
        {
            var fld = new FieldDefinition();
            fld.DisplayName = Resources.CRMAdapterSettingsConfigDisplayName;
            fld.Description = Resources.CRMAdapterSettingsConfigDesc;
            fld.Name = "ConfigUtility";
            fld.IsRequired = false;
            fld.TypeDefinition = new SimpleType() { ClrType = typeof(Uri), Name = "Uri" };
            return fld;
        }

        private static FieldDefinition GetDiscoveryServiceUrlSetting()
        {
            var fld = new FieldDefinition();
            fld.DisplayName = Resources.CRMAdapterSettingsDiscoveryServiceUrlDisplayName;
            fld.Description = Resources.CRMAdapterSettingsDiscoveryServiceUrlDesc;
            fld.Name = DiscoveryURLSettingName;
            fld.IsRequired = false;
            fld.TypeDefinition = new SimpleType() { ClrType = typeof(Uri), Name = "Uri" };
            return fld;
        }

        private static FieldDefinition GetUserNameDefField()
        {
            var fld = new FieldDefinition();
            fld.DisplayName = Resources.CRMAdapterSettingsUserNameDisplayName;
            fld.Description = Resources.CRMAdapterSettingsUserNameDesc;
            fld.Name = UserSettingName;
            fld.IsRequired = true;
            fld.TypeDefinition = new SimpleType() { ClrType = typeof(string), Name = "string" };
            return fld;
        }

        // TODO: Split this so that the source and destination adapters work independently and look one level lower.

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        private System.Collections.ObjectModel.Collection<ObjectProvider> GetObjectProviders()
        {

            this.providers = new System.Collections.ObjectModel.Collection<ObjectProvider>();
            ObjectDefinition objDef = null;
            foreach (string configFileName in Directory.GetFiles(this.GetConfigPath<DynamicCrmAdapter>()))
            {
                using (var fs = File.OpenRead(configFileName))
                {
                    using (var xr = XmlReader.Create(fs))
                    {
                        var serializer = new XmlSerializer(typeof(ObjectDefinition));
                        objDef = (ObjectDefinition)serializer.Deserialize(xr);
                    }
                }

                if (!IsStaticObjectProvider(objDef))
                {
                    DynamicObjectProvider dynObject = new DynamicObjectProvider() { Adapter = this, Id = GetDynamicProviderId(objDef), DisplayName = objDef.RootDefinition.DisplayName, Name = CRM2011AdapterUtilities.GetObjectProviderName(objDef) };
                    this.providers.Add(dynObject);
                }
            }

            return this.providers;
        }

        // Retrieve a CRM object that can be used to get the various endpoints for calling the CRM web services
        private OrganizationDetail GetOrgExtDetail()
        {
            if (this.orgExtDetail == null)
            {
                // Retrieve the list of organizations that the logged on user belongs to.                
                this.orgExtDetail = ServerConnection.FindOrganization(this.OrganizationName, ServerConnection.DiscoverOrganizations(this.DiscoveryClient).ToArray());

                // Check whether a matching organization was not found.
                if (this.orgExtDetail == null)
                {
                    throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.OrganizationNotFoundExceptionMessage, this.OrganizationName)) { ExceptionId = ErrorCodes.OrganizationNotFound };
                }
            }

            return this.orgExtDetail;
        }

        // If the organization has multiple versions installed, we need to get the endpoint for the version(s) that we support
        private string GetOrgEndpoint()
        {
            if (this.endpoint == null)
            {
                this.endpoint = this.GetOrgExtDetail().Endpoints[EndpointType.OrganizationService];

                // Check whether a matching endpoint was not found. 
                if (this.endpoint == null)
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.EndpointVersionNotFoundExceptionMessage, Version));
                }
            }

            return this.endpoint;
        }

        /// <summary>
        /// Returns an instance of a <c>CrmService</c> for interacting with the CRM web service.
        /// </summary>
        /// <returns>A <c>CrmService</c> that is initialized using the supplied <c>List</c> of <c>SettingsValue</c> objects.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private IOrganizationService GetCrmWebServiceClient()
        {
            if (this.orgService == null)
            {
                lock (this.syncObject)
                {
                    if (this.orgService == null)
                    {
                        var config = ServiceConfigurationFactory.CreateManagement<IOrganizationService>(new Uri(this.GetOrgEndpoint()));
                        AuthenticationCredentials authCreds = config.Authenticate(this.CrmAuthenticationCredentials);
                        this.orgService = authCreds.SecurityTokenResponse == null ? new OrganizationServiceProxy(config, authCreds.ClientCredentials) : new OrganizationServiceProxy(config, authCreds.SecurityTokenResponse);
                        ((OrganizationServiceProxy)this.orgService).Timeout = new TimeSpan(1, 0, 0);
                        if (this.ImpersonateIntegrationUser)
                        {
                            ((OrganizationServiceProxy)this.orgService).CallerId = this.IntegrationUserId;
                        }
                    }
                }
            }

            return this.orgService;
        }

        /// <summary>
        /// Returns an instance of a <c>CrmDiscoveryService</c> for interacting with the CRM discovery web service.
        /// </summary>
        /// <returns>A <c>CrmDiscoveryService</c> that is initialized using the supplied <c>List</c> of <c>SettingsValue</c> objects.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private IDiscoveryService GetCrmDiscoveryWebServiceClient()
        {
            if (this.discoveryService == null)
            {
                lock (this.syncObject)
                {
                    if (this.discoveryService == null)
                    {
                        var config = ServiceConfigurationFactory.CreateManagement<IDiscoveryService>(this.DiscoveryServiceAddress);
                        AuthenticationCredentials authCreds = config.Authenticate(this.CrmAuthenticationCredentials);
                        this.discoveryService = authCreds.SecurityTokenResponse == null ? new DiscoveryServiceProxy(config, authCreds.ClientCredentials) : new DiscoveryServiceProxy(config, authCreds.SecurityTokenResponse);
                    }
                }
            }

            return this.discoveryService;
        }

        internal void ResetOrgService()
        {
            if (this.orgService != null)
            {
                lock (this.syncObject)
                {
                    if (this.orgService != null)
                    {
                        this.orgService = null;
                    }
                }
            }
        }

        protected void OrganizationNamePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e != null && e.PropertyName == "Value")
            {
                // When the Org name changes, need to reset everything except the discovery service
                this.endpoint = null;
                this.orgExtDetail = null;
                this.integrationUserId = Guid.Empty;
                this.systemUserId = Guid.Empty;
                this.orgService = null;
                this.BaseCurrency = string.Empty;
            }
        }

        protected void DiscoveryUrlPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e != null && e.PropertyName == "Value")
            {
                // When the discovery service URL changes, need to reset everything
                this.endpoint = null;
                this.orgExtDetail = null;
                this.integrationUserId = Guid.Empty;
                this.systemUserId = Guid.Empty;
                this.orgService = null;
                this.discoveryService = null;
                this.BaseCurrency = string.Empty;
                this.serverConfiguration = null;
                this.serverConnection = null;
            }
        }

        protected void UserNameOrPasswordPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e != null && e.PropertyName == "Value")
            {
                // If the UserName and/or Password changes, need to reset the Crm Service and the Metadata service
                this.orgService = null;
                this.discoveryService = null;
                this.serverConfiguration = null;
                this.serverConnection = null;
            }
        }

        protected void ImpersonateIntegrationUserPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e != null && e.PropertyName == "Value")
            {
                // When the Integration user id changes, need to reset the services
                this.orgService = null;
                this.serverConfiguration = null;
                this.serverConnection = null;
            }
        }

        protected void MetadataTimestampPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e != null && e.PropertyName == "Value")
            {
                // When the timestamp id changes, need to reset the meatadata service
                this.orgService = null;
            }
        }

        protected Guid GetBaseCurrencyId()
        {
            if (this.baseCurrencyKey == Guid.Empty)
            {
                ColumnSet cols = new ColumnSet();
                cols.AddColumn("basecurrencyid");
                QueryByAttribute queryAtrib = new QueryByAttribute("organization") { ColumnSet = cols };
                try
                {
                    EntityCollection organizations = this.OrganizationService.RetrieveMultiple(queryAtrib);
                    Entity setupOrg = organizations.Entities.FirstOrDefault();
                    this.baseCurrencyKey = ((EntityReference)setupOrg["basecurrencyid"]).Id;
                }
                catch (Exception ex)
                {
                    throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.CurrencyRetrievalExceptionMessage, this.OrganizationName), ex) { ExceptionId = ErrorCodes.CrmPlatformException };
                }
            }

            return this.baseCurrencyKey;
        }

        /// <summary>
        /// Returns a list of configured organizations and a validation result set for organizations that aren't configured.
        /// </summary>
        /// <param name="forceRefresh">True to refresh the organization list cache, false otherwise</param>
        /// <param name="vr">A <c>ValidationResult</c> to add any exceptions to</param>
        /// <returns>a <c>List</c> of configured organizations</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Need to catch all exceptions here")]
        public List<NameStringValuePair> GetConfiguredOrganizations(bool forceRefresh, Common.ValidationResult vr)
        {
            List<NameStringValuePair> orgs = null;

            lock (companyListCache)
            {
                {
                    string cacheKey = this.DiscoveryServiceUrl.ToString();

                    if (companyListCache.ContainsKey(cacheKey))
                    {
                        orgs = companyListCache[cacheKey];
                        if (forceRefresh)
                        {
                            orgs.Clear();
                        }
                    }
                    else
                    {
                        orgs = new List<NameStringValuePair>();
                        companyListCache.Add(cacheKey, orgs);
                    }

                    // We requery tras set to true.
                    if (orgs.Count == 0)
                    {
                        ColumnSet cols = new ColumnSet();
                        cols.AddColumn("name");
                        var query = new QueryExpression("organization");
                        query.ColumnSet = cols;

                        string originalOrganizationName = this.OrganizationName;
                        try
                        {
                            // Loop over all of the organizations an find the ones that have been setup for integration
                            // Save off the current orgname so we can reset it.
                            foreach (OrganizationDetail detail in this.GetAllOrganizations(vr))
                            {
                                try
                                {
                                    // Set the proxy to use the proper organization
                                    this.OrganizationName = detail.UniqueName;

                                    EntityCollection organizations = this.OrganizationService.RetrieveMultiple(query);
                                    Entity setupOrg = organizations.Entities.FirstOrDefault();
                                    if (setupOrg != null)
                                    {
                                        orgs.Add(new NameStringValuePair() { Name = detail.FriendlyName, Value = detail.UniqueName });
                                    }
                                    else if (vr != null)
                                    {
                                        vr.AddWarning(string.Format(CultureInfo.CurrentUICulture, Resources.ConfiguredOrganizationExceptionMessage, detail.FriendlyName));
                                    }
                                }
                                catch (Exception ex)
                                {
                                    if (vr != null)
                                    {
                                        vr.AddError(string.Format(CultureInfo.CurrentUICulture, Resources.RetrieveConfiguredCompanyExceptionMessage, detail.FriendlyName, ex.Message));
                                    }
                                }
                            }
                        }
                        finally
                        {
                            // set it back to the original value
                            this.OrganizationName = originalOrganizationName;
                        }
                    }
                }
            }

            return orgs;
        }

        /// <summary>
        /// Gets all of the <c>OrganizationDetail</c>s for the current user
        /// </summary>
        /// <param name="validationResult">A <c>ValidationResult</c> to add exceptions to</param>
        /// <returns>a <c>List</c> of <c>OrganizationDetail</c>s for the current user</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Need to catch all exceptions here")]
        public List<OrganizationDetail> GetAllOrganizations(Common.ValidationResult validationResult)
        {
            // Retrieve the list of all organizations
            List<OrganizationDetail> detail = new List<OrganizationDetail>();
            try
            {
                detail = ServerConnection.DiscoverOrganizations(this.DiscoveryClient).ToList();
            }
            catch (Exception ex)
            {
                if (validationResult != null)
                {
                    validationResult.AddError(string.Format(CultureInfo.CurrentUICulture, Resources.RetrieveAllCompaniesExceptionMessage, ex.Message));
                }
            }

            return detail;
        }

        public Common.ValidationResult ValidateSettings()
        {

            var vr = new Common.ValidationResult();
            this.ValidateUserName(vr);
            if (this.DiscoveryServiceUrl.IsNullOrEmptyTrim())
            {
                var msg = string.Format(CultureInfo.CurrentUICulture, Resources.SettingsErrorMesssage, Resources.CRMAdapterSettingsWebServiceUrlDisplayName);
                vr.AddError(msg);
            }
            
            if (Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows Identity Foundation", false) == null)
            {
                // WIF is not installed but the CRM 2011 adapter requires it
                var msg = string.Format(CultureInfo.CurrentCulture, Resources.WIFInstallationNotFoundExceptionMessage);
                vr.AddError(msg);
            }
            
//            // allow for disconnected mode, which never makes a connection to the web service.
//#if DEBUG
//            bool skipWsValidation = System.Environment.GetEnvironmentVariables().Contains("RUN_DISCONNECTED");
//#else
                const bool skipWsValidation = false;
//#endif
            
            bool validWsConnection = this.ValidateWebServiceConnection();
            
            // Validate the user has proper web service priviledges
            if (!skipWsValidation && !validWsConnection)
            {
                var msg = string.Format(CultureInfo.CurrentCulture, Resources.ConnectionValidationExceptionMessage, this.UserName, this.DiscoveryServiceUrl);
                vr.AddError(msg);
            }
            
            List<OrganizationDetail> details = this.GetAllOrganizations(vr);
            
            // Validate that there are organizations in CRM and that the user can retrieve them
            if (!skipWsValidation && validWsConnection && details.Count == 0)
            {
                var msg = string.Format(CultureInfo.CurrentCulture, Resources.NoOrganizationsExceptionMessage);
                vr.AddError(msg);
            }
            
            // Validate the user has proper web service priviledges
            if (!skipWsValidation && validWsConnection && details.Count > 0)
            {
                this.GetConfiguredOrganizations(true, vr);
            }
            
            return vr;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Need to catch all exceptions here")]
        private bool ValidateWebServiceConnection()
        {
            try
            {
                ServerConnection.DiscoverOrganizations(this.DiscoveryClient);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private void ValidateUserName(Microsoft.Dynamics.Integration.Common.ValidationResult vr)
        {
            // Verify a username is set
            // UserName reads from this.Settings["UserName"]
            if (this.UserName.IsNullOrEmptyTrim())
            {
                var msg = string.Format(CultureInfo.CurrentUICulture, Resources.SettingsErrorMesssage, Resources.CRMAdapterSettingsUserNameDisplayName);
                vr.AddError(msg);
            }
        }

        private Guid GetSystemUserId(string userFullName, IOrganizationService serviceInstance)
        {
            string attribute = string.CompareOrdinal(userFullName, "SYSTEM") == 0 ? "fullname" : "domainname";
            string userName = string.IsNullOrEmpty(userFullName) ? this.UserName : userFullName;
            QueryByAttribute attQuery = new QueryByAttribute() { Attributes = { attribute }, ColumnSet = new ColumnSet(true), EntityName = "systemuser", Values = { userName } };
            RetrieveMultipleRequest multiRequest = new RetrieveMultipleRequest() { Query = attQuery };
            RetrieveMultipleResponse retrieveResponse = null;
            if (serviceInstance != null)
            {
                retrieveResponse = (RetrieveMultipleResponse)serviceInstance.Execute(multiRequest);
            }
            else
            {
                retrieveResponse = (RetrieveMultipleResponse)this.OrganizationService.Execute(multiRequest);
            }

            Entity user = retrieveResponse.EntityCollection.Entities.FirstOrDefault();
            if (user == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.UserNotFoundExceptionMessage, userName)) { ExceptionId = ErrorCodes.CrmOnlineUserConfigurationException };
            }

            return (Guid)user["systemuserid"];
        }
        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            this.Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (this.discoveryService != null)
                    {
                        ((DiscoveryServiceProxy)this.discoveryService).Dispose();
                        this.discoveryService = null;
                    }

                    if (this.orgService != null)
                    {
                        ((OrganizationServiceProxy)this.orgService).Dispose();
                        this.orgService = null;
                    }
                }

                this.disposed = true;
            }
        }
        #endregion      
 
        public IEnumerable<NameStringValuePair> GetSettingsValueList(string settingsName, bool forceRefresh)
        {
            List<NameStringValuePair> results = null;

            switch (settingsName)
            {
                case "OrganizationName":
                    results = this.GetConfiguredOrganizations(forceRefresh, null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("settingsName");
            }
            return results;
        }
    }
}
