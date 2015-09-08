// =====================================================================
//  This file is part of the Microsoft Dynamics CRM SDK code samples.
//
//  Copyright (C) Microsoft Corporation.  All rights reserved.
//
//  This source code is intended only as a supplement to Microsoft
//  Development Tools and/or on-line documentation.  See these other
//  materials for detailed information regarding Microsoft code samples.
//
//  THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
//  KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
//  IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
//  PARTICULAR PURPOSE.
// =====================================================================

//<snippetCrmServiceHelper>
using System;
using System.Collections.Generic;
using System.Globalization;
using System.ServiceModel.Description;
using System.ServiceModel.Security;
using Microsoft.Dynamics.Integration.AdapterAbstractionLayer;
using Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Properties;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Discovery;

namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm
{
    /// <summary>
    /// Provides server connection information.
    /// </summary>
    /// <remarks>This code implements partial (incomplete) support for federated and managed domain user accounts with CRM Online/O365 integration.</remarks>
    public class ServerConnection
    {
        public ServerConnection(Uri discoveryServiceAddress, string userName, string password)
        {
            this.serverAddress = discoveryServiceAddress;
            this.serverPassword = password;
            this.serverUserName = userName;
        }

        #region Inner classes
        /// <summary>
        /// Stores CRM server configuration information.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public class CrmServerConfiguration
        {
            public string ServerAddress
            {
                get;
                set;
            }

            public string OrganizationName
            {
                get;
                set;
            }

            public Uri DiscoveryUri
            {
                get;
                set;
            }

            public Uri OrganizationUri
            {
                get;
                set;
            }

            public Uri HomeRealmUri
            {
                get;
                set;
            }
            public ClientCredentials DeviceCredentials
            {
                get;
                set;
            }

            public ClientCredentials Credentials
            {
                get;
                set;
            }

            public AuthenticationProviderType EndpointType
            {
                get;
                set;
            }

            public override bool Equals(object obj)
            {
                //Check for null and compare run-time types.
                if (obj == null || GetType() != obj.GetType()) return false;

                CrmServerConfiguration c = (CrmServerConfiguration)obj;

                if (!this.ServerAddress.Equals(c.ServerAddress, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (!this.OrganizationName.Equals(c.OrganizationName, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (this.EndpointType != c.EndpointType)
                    return false;
                if (this.EndpointType == AuthenticationProviderType.ActiveDirectory)
                {
                    if (!this.Credentials.Windows.ClientCredential.Domain.Equals(
                        c.Credentials.Windows.ClientCredential.Domain, StringComparison.OrdinalIgnoreCase))
                        return false;
                    if (!this.Credentials.Windows.ClientCredential.UserName.Equals(
                        c.Credentials.Windows.ClientCredential.UserName, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else if (this.EndpointType == AuthenticationProviderType.LiveId)
                {
                    if (!this.Credentials.UserName.UserName.Equals(c.Credentials.UserName.UserName,
                        StringComparison.OrdinalIgnoreCase))
                        return false;
                    if (!this.DeviceCredentials.UserName.UserName.Equals(
                        c.DeviceCredentials.UserName.UserName, StringComparison.OrdinalIgnoreCase))
                        return false;
                    if (!this.DeviceCredentials.UserName.Password.Equals(
                        c.DeviceCredentials.UserName.Password, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else
                {
                    if (!this.Credentials.UserName.UserName.Equals(c.Credentials.UserName.UserName,
                        StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                return true;
            }

            public override int GetHashCode()
            {
                int returnHashCode = this.ServerAddress.GetHashCode() ^ this.OrganizationName.GetHashCode() ^ this.EndpointType.GetHashCode();
                if (this.EndpointType == AuthenticationProviderType.ActiveDirectory)
                {
                    returnHashCode = returnHashCode ^ this.Credentials.Windows.ClientCredential.UserName.GetHashCode() ^ this.Credentials.Windows.ClientCredential.Domain.GetHashCode();
                }
                else if (this.EndpointType == AuthenticationProviderType.LiveId)
                {
                    returnHashCode = returnHashCode ^ this.Credentials.UserName.UserName.GetHashCode() ^ this.DeviceCredentials.UserName.UserName.GetHashCode() ^ this.DeviceCredentials.UserName.Password.GetHashCode();
                }
                else
                {
                    returnHashCode = returnHashCode ^ this.Credentials.UserName.UserName.GetHashCode();
                }

                return returnHashCode;
            }
        }
        #endregion Inner classes

        #region Internal properties

        internal List<CrmServerConfiguration> configurations = new List<CrmServerConfiguration>();

        #endregion

        #region Private Fields

        private CrmServerConfiguration config = new CrmServerConfiguration();
        private Uri serverAddress;
        private string serverUserName;
        private string serverPassword;

        #endregion Private properties

        #region Public methods
        /// <summary>
        /// Obtains the server connection information including the target organization's
        /// Uri and user login credentials from the user.
        /// </summary>
        public virtual CrmServerConfiguration GetServerConfiguration()
        {
            // Get the server address.
            config.ServerAddress = this.serverAddress.Host;
            if (!this.serverAddress.IsDefaultPort)
            {
                config.ServerAddress += ":";
                config.ServerAddress += this.serverAddress.Port;
            }

            config.DiscoveryUri = this.serverAddress;

            // One of the Microsoft Dynamics CRM Online data centers.
            if (config.ServerAddress.EndsWith(".dynamics.com", StringComparison.OrdinalIgnoreCase))
            {
                // Set or get the device credentials. Required for Windows Live ID authentication. 
                config.DeviceCredentials = GetDeviceCredentials();
            }

            // Set the endpoint type.
            config.EndpointType = GetServerType(config.DiscoveryUri);

            // Get the user's logon credentials.
            config.Credentials = GetUserLogonCredentials(this.serverUserName, this.serverPassword);
            return config;
        }

        /// <summary>
        /// Discovers the organizations that the calling user belongs to.
        /// </summary>
        /// <param name="service">A Discovery service proxy instance.</param>
        /// <returns>Array containing detailed information on each organization that the user belongs to.</returns>
        public static OrganizationDetailCollection DiscoverOrganizations(IDiscoveryService service)
        {
            if (service == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ParamterNullExceptionMessage), new ArgumentNullException("service")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            RetrieveOrganizationsResponse orgResponse = null;
            try
            {
                orgResponse = (RetrieveOrganizationsResponse)service.Execute(new RetrieveOrganizationsRequest());
            }
            catch (SecurityNegotiationException ex)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, ex.InnerException != null ? ex.InnerException.Message : ex.Message), ex) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            return orgResponse != null ? orgResponse.Details : null;
        }

        /// <summary>
        /// Finds a specific organization detail in the array of organization details
        /// returned from the Discovery service.
        /// </summary>
        /// <param name="orgFriendlyName">The friendly name of the organization to find.</param>
        /// <param name="orgDetails">Array of organization detail object returned from the discovery service.</param>
        /// <returns>Organization details or null if the organization was not found.</returns>
        internal static OrganizationDetail FindOrganization(string orgUniqueName, OrganizationDetail[] orgDetails)
        {
            if (string.IsNullOrEmpty(orgUniqueName))
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ParamterNullExceptionMessage), new ArgumentNullException("orgUniqueName")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            if (orgDetails == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ParamterNullExceptionMessage), new ArgumentNullException("orgDetails")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            foreach (OrganizationDetail detail in orgDetails)
            {
                if (string.CompareOrdinal(detail.UniqueName, orgUniqueName) == 0)
                {
                    return detail;
                }
            }

            return null;
        }

        #endregion Public methods

        #region Private methods
        /// <summary>
        /// Obtains the authentication type of the CRM server.
        /// </summary>
        /// <param name="uri">Uri of the CRM Discovery service.</param>
        /// <returns>Authentication type.</returns>
        private static AuthenticationProviderType GetServerType(Uri uri)
        {
            if (uri == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ParamterNullExceptionMessage), new ArgumentNullException("uri")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            return ServiceConfigurationFactory.CreateConfiguration<IDiscoveryService>(uri).AuthenticationType;
        }

        /// <summary>
        /// Obtains the user's logon credentials for the target server.
        /// </summary>
        /// <returns>Logon credentials of the user.</returns>
        private ClientCredentials GetUserLogonCredentials(string userNameCredential, string passwordCredential)
        {
            if (string.IsNullOrEmpty(userNameCredential))
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ParamterInvalidExceptionMessage), new ArgumentOutOfRangeException("userNameCredential")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            ClientCredentials credentials = new ClientCredentials();
            string userName;
            string domain;
            bool isCredentialExist = (config.Credentials != null) ? true : false;

            // An on-premises Microsoft Dynamics CRM server deployment. 
            if (config.EndpointType == AuthenticationProviderType.ActiveDirectory)
            {
                string[] domainAndUserName = (isCredentialExist)
                                                            ? new string[] { config.Credentials.Windows.ClientCredential.Domain, config.Credentials.Windows.ClientCredential.UserName }
                                                            : userNameCredential.Split('\\');
                if (domainAndUserName.Length != 2 || string.IsNullOrWhiteSpace(domainAndUserName[0]) || string.IsNullOrWhiteSpace(domainAndUserName[1]))
                {
                    throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidADUserName)) { ExceptionId = AdapterException.SystemExceptionGuid };
                }

                domain = domainAndUserName[0];
                userName = domainAndUserName[1];
                credentials.Windows.ClientCredential = new System.Net.NetworkCredential(userName, passwordCredential, domain);
            }
            else if (config.EndpointType == AuthenticationProviderType.LiveId)
            {
                // An Microsoft Dynamics CRM Online server deployment.                
                userName = (isCredentialExist) ? config.Credentials.UserName.UserName : this.serverUserName;
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return null;
                }

                credentials.UserName.UserName = userName;
                credentials.UserName.Password = passwordCredential;
            }

            // An internet facing (IFD) Microsoft Dynamics CRM server deployment
            // Or
            // An Managed domain/Federated users using Office 365.
            else if (config.EndpointType == AuthenticationProviderType.Federation || config.EndpointType == AuthenticationProviderType.OnlineFederation)
            {
                userName = (isCredentialExist) ? config.Credentials.UserName.UserName : userNameCredential;
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return null;
                }

                credentials.UserName.UserName = userName;
                credentials.UserName.Password = passwordCredential;
            }
            else
            {
                return null;
            }

            return credentials;
        }

        /// <summary>
        /// Get the device credentials by either loading from local cache 
        /// or request new device credentials by registering the device.
        /// </summary>
        /// <returns>Device Credentials.</returns>
        private static ClientCredentials GetDeviceCredentials()
        {
            return DeviceIdManager.LoadOrRegisterDevice();
        }

        #endregion Private methods
    }
}
