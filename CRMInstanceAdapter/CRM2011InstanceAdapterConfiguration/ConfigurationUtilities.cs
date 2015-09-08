using Microsoft.Dynamics.Integration.AdapterAbstractionLayer;
using Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration.Properties;
using Microsoft.Dynamics.Integration.Adapters.DynamicCrm.ObjectProviders;
using Microsoft.Dynamics.Integration.Common;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Discovery;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using XrmMetadata = Microsoft.Xrm.Sdk.Metadata;

namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "All classes are needed")]
    public class ConfigurationUtilities
    {
        #region Fields
        private const string ObjectConfigFolderName = "ObjectConfig";
        private string adapterType;
        private List<ObjectProvider> availableProviders;
        #endregion

        #region Public Fields
        public string AdapterType
        {
            get
            {
                return adapterType;
            }
            set
            {
                adapterType = value;
            }
        }
        #endregion

        #region Public Methods
        public void SetAdapter(string typeOfAdapter)
        {
            AdapterType = typeOfAdapter;

            if (AdapterType == "DynamicCrmAdapterSource")
            {
                this.InstallAdapter = new DynamicCrmAdapterSource();
            }
            if (AdapterType == "DynamicCrmAdapterDestination")
            {
                this.InstallAdapter = new DynamicCrmAdapterDestination();
            }

            // Create a new instance of the CRMAdapter class to use when configuring CRM and set any properties listed in the configuration utiltity
            // app.config file
            this.SetPropertiesFromAppConfig();

            // Initialize the TraceLog to always log the configuration process
            TraceLog.Info(FormattedString(Resources.StartMessage));
            Trace.Indent();

            // Initialize the configuration arguments list, threaded request class, and the install adapter
            this.InitArguments();
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the ConfigurationUtilities class.
        /// </summary>
        public ConfigurationUtilities()
        {
            
        }
        #endregion

        #region Events
        /// <summary>
        /// Event that is raised when a configuration event is starting
        /// </summary>
        internal event EventHandler<ConfigurationEventArgs> ConfigurationEventPre;

        /// <summary>
        /// Event that is raised when a configuration event has encountered an <c>Exception</c>
        /// </summary>
        internal event EventHandler<ConfigurationEventArgs> ConfigurationEventException;

        /// <summary>
        /// Event that is raised when a configuration event is finishing
        /// </summary>
        internal event EventHandler<ConfigurationEventArgs> ConfigurationEventPost;
        #endregion

        #region Internal Properties
        /// <summary>
        /// Gets or sets the list of available CRM <c>ObjectProvider</c>s
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal List<ObjectProvider> AvailableProviders
        {
            get { return this.GetObjectProviders(); }
            set { this.availableProviders = value; }
        }

        /// <summary>
        /// Gets or sets the list of selected CRM <c>ObjectProvider</c>s
        /// </summary>
        internal List<NameValuePair<ObjectProvider>> SelectedProviders
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the <c>CRMAdapter</c> being used to configure CRM
        /// </summary>
        internal DynamicCrmAdapter InstallAdapter
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the <c>Dictionary</c> that contains the configuration arguments
        /// </summary>
        internal Dictionary<string, object> Arguments
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the base language code for an organization in CRM
        /// </summary>
        internal int BaseLangCode
        {
            get;
            private set;
        }
        #endregion

        #region Internal Methods

        internal List<ObjectDefinition> LoadObjectDefinitionsFromConfigs()
        {
            string[] existingFiles = Directory.Exists(this.GetConfigPath(true)) ? Directory.GetFiles(this.GetConfigPath(true)) : Directory.GetFiles(this.GetConfigPath(false));
            List<ObjectDefinition> dynamicObjectDefinitions = new List<ObjectDefinition>();
            foreach (string path in existingFiles)
            {
                ObjectDefinition def = new ObjectDefinition();
                using (var fs = File.OpenRead(path))
                {
                    using (var xr = XmlReader.Create(fs))
                    {
                        var serializer = new XmlSerializer(typeof(ObjectDefinition));
                        def = (ObjectDefinition)serializer.Deserialize(xr);
                    }
                }

                dynamicObjectDefinitions.Add(def);

            }

            return dynamicObjectDefinitions;
        }

        internal void SetOrgProperties(OrganizationDetail detail)
        {
            // Create the column set object that indicates the properties to be retrieved and retrieve the current organization.
            ColumnSet cols = new ColumnSet(new string[] { "languagecode" });
            Entity org = this.InstallAdapter.OrganizationService.Retrieve("organization", detail.OrganizationId, cols) as Entity;

            if (org != null)
            {
                this.BaseLangCode = org.GetAttributeValue<int>("languagecode");
                TraceLog.Info(string.Format(CultureInfo.CurrentCulture, Resources.OrganizationBaseLanguageMessage, detail.FriendlyName, this.BaseLangCode));
            }
        }

        /// <summary>
        /// Copies the existing <c>ObjectProvider</c> configuration files to a company specific directory
        /// </summary>
        /// <param name="option">The current <c>ConfigurationOption</c> for this configuration run</param>
        internal void SetupCompanyConfig(ConfigurationOption option)
        {
            if (option == ConfigurationOption.Install)
            {
                // Create a company specific directory for the configuration files
                DirectoryInfo dir = Directory.CreateDirectory(this.GetConfigPath(true));
                this.PublishPreConfigurationMessage(string.Format(CultureInfo.CurrentCulture, Resources.StartCopyingConfigFileMessage, dir.FullName));

                // Copy the existing configuration files to the new directory and set their attributes so they can be updated later
                foreach (string fileName in Directory.GetFiles(this.GetConfigPath(false)))
                {
                    File.Copy(fileName, Path.Combine(dir.FullName, Path.GetFileName(fileName)), true);
                    File.SetAttributes(Path.Combine(dir.FullName, Path.GetFileName(fileName)), FileAttributes.Normal);
                }

                this.PublishPostConfigurationMessage(FormattedString(Resources.FinishedCopyingConfigFileMessage));
            }
            else
            {
                // This is a remove operation and we need to delete the company specific directory and configuration files
                this.PublishPreConfigurationMessage(string.Format(CultureInfo.CurrentCulture, Resources.StartDeletingConfigFileMessage, this.GetConfigPath(true)));
                if (Directory.Exists(this.GetConfigPath(true)))
                {
                    Directory.Delete(this.GetConfigPath(true), true);
                }

                this.PublishPostConfigurationMessage(FormattedString(Resources.FinishedDeletingConfigFileMessage));
            }
        }

        /// <summary>
        /// Returns a <c>List</c> of <c>OrganizationDetail</c> objects that contains the <c>OrganizationDetail</c>s for each of the <c>Organization</c>s 
        /// that the user specified on the install adapter is associated to
        /// </summary>
        /// <returns>Returns a <c>List</c> of <c>OrganizationDetail</c>s</returns>
        internal List<OrganizationDetail> RetrieveOrgDetails()
        {
            Common.ValidationResult vr = new Common.ValidationResult();
            List<OrganizationDetail> orgs = this.InstallAdapter.GetAllOrganizations(vr);
            if (vr.Errors.Count > 0)
            {
                TraceLog.Error(string.Format(CultureInfo.InvariantCulture, vr.Errors[0].Message));
                this.PublishExceptionConfigurationMessage(vr.Errors[0].Message);
            }

            return orgs;
        }

        /// <summary>
        /// Publishes an <c>Event</c>
        /// </summary>
        /// <param name="message">The message to be written to the <c>TraceLog</c> and any listeners</param>
        /// <remarks>Normally called when a configuration operation is starting</remarks>
        internal void PublishPreConfigurationMessage(string message)
        {
            // Copy to a temporary variable to be thread-safe.
            EventHandler<ConfigurationEventArgs> temp = this.ConfigurationEventPre;
            if (temp != null)
            {
                temp(this, new ConfigurationEventArgs(message));
            }
        }

        /// <summary>
        /// Publishes an <c>Event</c>
        /// </summary>
        /// <param name="message">The message to be written to the <c>TraceLog</c> and any listeners</param>
        /// <remarks>Normally called when a configuration operation has thrown an exception</remarks>
        internal void PublishExceptionConfigurationMessage(string message)
        {
            // Copy to a temporary variable to be thread-safe.
            EventHandler<ConfigurationEventArgs> temp = this.ConfigurationEventException;
            if (temp != null)
            {
                temp(this, new ConfigurationEventArgs(message));
            }
        }

        /// <summary>
        /// Publishes an <c>Event</c>
        /// </summary>
        /// <param name="message">The message to be written to the <c>TraceLog</c> and any listeners</param>
        /// <remarks>Normally called when a configuration operation has completed</remarks>
        internal void PublishPostConfigurationMessage(string message)
        {
            // Copy to a temporary variable to be thread-safe.
            EventHandler<ConfigurationEventArgs> temp = this.ConfigurationEventPost;
            if (temp != null)
            {
                temp(this, new ConfigurationEventArgs(message));
            }
        }


        /// <summary>
        /// Writes out the supplied <c>ObjectProvider</c>'s configuration file
        /// </summary>
        /// <param name="provider">The <c>ObjectProvider</c> to write out the configuration for</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        internal void WriteMToMRelationshipObjectDefinition(ObjectProvider provider)
        {
            try
            {
                // Check to see if the company specific directory has been created yet or not, if not create it and copy all of the base configs into it
                // if it has, then we assume that the base configs are there since the copy always occurs after the directory creation
                if (!Directory.Exists(this.GetConfigPath(true)))
                {
                    this.SetupCompanyConfig(ConfigurationOption.Install);
                }

                provider.Name = provider.Name.Replace(",Relationship", "");

                var providerObjectDefinition = this.LoadObjectDefinitionsFromConfigs().FirstOrDefault(str => str.RootDefinition.TypeName.ToUpperInvariant() == provider.Name.ToUpperInvariant());
                string configFileName = providerObjectDefinition == null ?
                    CultureInfo.CurrentCulture.TextInfo.ToTitleCase(provider.DisplayName.ConvertToValidFileName().Replace(" ", string.Empty)) + "ObjectProvider.config" :
                    providerObjectDefinition.RootDefinition.DisplayName.ConvertToValidFileName().Replace(" ", string.Empty) + "ObjectProvider.config";

                var filePath = Path.Combine(this.GetConfigPath(false), configFileName);
                Common.ObjectDefinition objDef = new Common.ObjectDefinition();
                this.PublishPreConfigurationMessage(string.Format(CultureInfo.CurrentCulture, Resources.StartGeneratingProviderConfiguration, provider.Name));

                // Check to see if the object def is stored at the root, which means it is a base or version 1 definition 
                if (File.Exists(filePath))
                {
                    provider.Name = Path.GetFileNameWithoutExtension(configFileName);
                    objDef = provider.ObjectDefinition;
                    FillEntityManytoManyObjectDef(objDef, provider.DisplayName);
                }
                else
                {
                    // This is a new configuration file and we need to add the id attribute to it so the framework knows what this object provider's id is
                    XmlDocument doc = new XmlDocument();
                    XmlAttribute idAttrib = doc.CreateAttribute(CRM2011AdapterUtilities.EntityMetadataId);
                    idAttrib.Value = provider.Id.ToString();
                    TypeDefinition typeDef = new ComplexType() { ClrType = typeof(System.Collections.Generic.Dictionary<string, object>), Name = provider.Name };
                    FieldDefinition rootDef = new FieldDefinition() { DisplayName = provider.DisplayName, Name = provider.Name, TypeDefinition = typeDef, TypeName = provider.Name, AdditionalAttributes = new XmlAttribute[] { idAttrib } };
                    objDef.Types.Add(typeDef);
                    objDef.RootDefinition = rootDef;
                    FillEntityManytoManyObjectDef(objDef, provider.DisplayName);
                }

                using (var fs = File.Create(Path.Combine(this.GetConfigPath(true), configFileName)))
                {
                    using (var xr = XmlWriter.Create(fs, new XmlWriterSettings() { Indent = true }))
                    {
                        var serializer = new XmlSerializer(typeof(ObjectDefinition));
                        serializer.Serialize(xr, objDef);
                    }
                }

                this.PublishPostConfigurationMessage(string.Format(CultureInfo.CurrentCulture, Resources.FinishedGeneratingProviderConfiguration, provider.Name));
            }
            catch (IOException e)
            {
                // Log any exceptions the occur and publish them to any listeners
                TraceLog.Error(string.Format(CultureInfo.InvariantCulture, Resources.ExceptionDump), e);
                this.PublishExceptionConfigurationMessage(string.Format(CultureInfo.CurrentCulture, Resources.ProviderConfigurationExceptionMessage, provider.Name));
            }
        }
        
        /// <summary>
        /// Write out a object definition for the picklist provider
        /// </summary>
        internal void WritePicklistObjectDefinition()
        {
            
            var provider = new PicklistObjectProvider();

            try
            {
                if (!Directory.Exists(this.GetConfigPath(true)))
                {
                    this.SetupCompanyConfig(ConfigurationOption.Install);
                }
                
                var providerObjectDefinition = this.LoadObjectDefinitionsFromConfigs().FirstOrDefault(str => str.RootDefinition.TypeName.ToUpperInvariant() == provider.Name.ToUpperInvariant());
                string configFileName = providerObjectDefinition == null ?
                    CultureInfo.CurrentCulture.TextInfo.ToTitleCase(provider.DisplayName.ConvertToValidFileName().Replace(" ", string.Empty)) + "ObjectProvider.config" :
                    providerObjectDefinition.RootDefinition.DisplayName.ConvertToValidFileName().Replace(" ", string.Empty) + "ObjectProvider.config";
                Common.ObjectDefinition objDef = new Common.ObjectDefinition();                
                this.PublishPreConfigurationMessage(string.Format(CultureInfo.CurrentCulture, Resources.StartGeneratingProviderConfiguration, provider.Name));

                // This is a new configuration file and we need to add the id attribute to it so the framework knows what this object provider's id is
                XmlDocument doc = new XmlDocument();
                XmlAttribute idAttrib = doc.CreateAttribute(CRM2011AdapterUtilities.EntityMetadataId);
                idAttrib.Value = provider.Id.ToString();
                TypeDefinition typeDef = new ComplexType() { ClrType = typeof(System.Collections.Generic.Dictionary<string, object>), Name = "Microsoft.Dynamics.Integration.Adapters.Crm2011.OptionList" };
                FieldDefinition rootDef = new FieldDefinition() { DisplayName = "Option list", Name = "OptionList", TypeDefinition = typeDef, TypeName = "Microsoft.Dynamics.Integration.Adapters.Crm2011.OptionList" };
                objDef.Types.Add(typeDef);
                objDef.RootDefinition = rootDef;
                this.FillPicklistObjectDef(objDef);

                using (var fs = File.Create(Path.Combine(this.GetConfigPath(true), configFileName)))
                {
                    using (var xr = XmlWriter.Create(fs, new XmlWriterSettings() { Indent = true }))
                    {
                        var serializer = new XmlSerializer(typeof(ObjectDefinition));
                        serializer.Serialize(xr, objDef);
                    }
                }

                this.PublishPostConfigurationMessage(string.Format(CultureInfo.CurrentCulture, Resources.FinishedGeneratingProviderConfiguration, provider.Name));

            }
            catch (IOException e)
            {
                // Log any exceptions the occur and publish them to any listeners
                TraceLog.Error(string.Format(CultureInfo.InvariantCulture, Resources.ExceptionDump), e);
                this.PublishExceptionConfigurationMessage(string.Format(CultureInfo.CurrentCulture, Resources.ProviderConfigurationExceptionMessage, provider.Name));
            }
        }

        /// <summary>
        /// Writes out the supplied <c>ObjectProvider</c>'s configuration file
        /// </summary>
        /// <param name="provider">The <c>ObjectProvider</c> to write out the configuration for</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        internal void WriteObjectDefinition(ObjectProvider provider)
        {
            
            try
            {
                // Check to see if the company specific directory has been created yet or not, if not create it and copy all of the base configs into it
                // if it has, then we assume that the base configs are there since the copy always occurs after the directory creation
                if (!Directory.Exists(this.GetConfigPath(true)))
                {
                    this.SetupCompanyConfig(ConfigurationOption.Install);
                }

                var providerObjectDefinition = this.LoadObjectDefinitionsFromConfigs().FirstOrDefault(str => str.RootDefinition.TypeName.ToUpperInvariant() == provider.Name.ToUpperInvariant());
                string configFileName = providerObjectDefinition == null ?
                    CultureInfo.CurrentCulture.TextInfo.ToTitleCase(provider.DisplayName.ConvertToValidFileName().Replace(" ", string.Empty)) + "ObjectProvider.config" :
                    providerObjectDefinition.RootDefinition.DisplayName.ConvertToValidFileName().Replace(" ", string.Empty) + "ObjectProvider.config";

                var filePath = Path.Combine(this.GetConfigPath(false), configFileName);
                Common.ObjectDefinition objDef = new Common.ObjectDefinition();
                this.PublishPreConfigurationMessage(string.Format(CultureInfo.CurrentCulture, Resources.StartGeneratingProviderConfiguration, provider.Name));

                // Check to see if the object def is stored at the root, which means it is a base or version 1 definition 
                if (File.Exists(filePath))
                {
                    provider.Name = Path.GetFileNameWithoutExtension(configFileName);
                    objDef = provider.ObjectDefinition;
                    this.FillObjectDef(objDef);
                }
                else
                {
                    // This is a new configuration file and we need to add the id attribute to it so the framework knows what this object provider's id is
                    XmlDocument doc = new XmlDocument();
                    XmlAttribute idAttrib = doc.CreateAttribute(CRM2011AdapterUtilities.EntityMetadataId);
                    idAttrib.Value = provider.Id.ToString();
                    TypeDefinition typeDef = new ComplexType() { ClrType = typeof(System.Collections.Generic.Dictionary<string, object>), Name = provider.Name };
                    FieldDefinition rootDef = new FieldDefinition() { DisplayName = provider.DisplayName, Name = provider.Name, TypeDefinition = typeDef, TypeName = provider.Name, AdditionalAttributes = new XmlAttribute[] { idAttrib } };
                    objDef.Types.Add(typeDef);
                    objDef.RootDefinition = rootDef;
                    this.FillObjectDef(objDef);
                }

                using (var fs = File.Create(Path.Combine(this.GetConfigPath(true), configFileName)))
                {
                    using (var xr = XmlWriter.Create(fs, new XmlWriterSettings() { Indent = true }))
                    {
                        var serializer = new XmlSerializer(typeof(ObjectDefinition));
                        serializer.Serialize(xr, objDef);
                    }
                }

                this.PublishPostConfigurationMessage(string.Format(CultureInfo.CurrentCulture, Resources.FinishedGeneratingProviderConfiguration, provider.Name));
            }
            catch (IOException e)
            {
                // Log any exceptions the occur and publish them to any listeners
                TraceLog.Error(string.Format(CultureInfo.InvariantCulture, Resources.ExceptionDump), e);
                this.PublishExceptionConfigurationMessage(string.Format(CultureInfo.CurrentCulture, Resources.ProviderConfigurationExceptionMessage, provider.Name));
            }
        }

        /// <summary>
        /// Gets the peth to the <c>ObjectProvider</c> configuration files
        /// </summary>
        /// <param name="forOrganization">True if the path is for the <c>organization</c> specific files and false otherwise</param>
        /// <returns>The relative path to the configuration files</returns>
        internal string GetConfigPath(bool forOrganization)
        {
            if (forOrganization)
            {
                var path = Path.Combine(Path.Combine(Path.Combine(Path.GetDirectoryName(typeof(DynamicCrmAdapter).Assembly.Location), ObjectConfigFolderName), AdapterType), this.InstallAdapter.OrganizationName);
                return path;
            }

            return Path.Combine(Path.GetDirectoryName(typeof(DynamicCrmAdapter).Assembly.Location), ObjectConfigFolderName);
        }
        #endregion

        #region Private Methods

        private static void GenerateGlobalPicklistObjectDefinitionEntries(ObjectDefinition objDef, RetrieveAllOptionSetsResponse response)
        {
            var customizableGlobalPicklists = response.OptionSetMetadata.Where(x => x.IsCustomizable.Value == true && x.DisplayName.LocalizedLabels.Count > 0);
            List<XmlAttribute> attribs = new List<XmlAttribute>();
            XmlDocument doc = new XmlDocument();
            XmlAttribute isGlobalAttrib = doc.CreateAttribute("IsGlobal");
            isGlobalAttrib.Value = "true";
            attribs.Add(isGlobalAttrib);

            foreach (var optionSet in customizableGlobalPicklists)
            {
                ComplexType complexType = CRM2011AdapterUtilities.ComplexTypeConvert(AttributeTypeCode.Picklist, objDef);
                FieldDefinition globalDef = new FieldDefinition() { Name = optionSet.Name, TypeDefinition = complexType, DisplayName = optionSet.DisplayName.LocalizedLabels[0].Label};
                globalDef.AdditionalAttributes = attribs.ToArray();
                objDef.Types.First().Children.Add(globalDef);
            }
            
        }
                
        private static void GenerateEntityManytoManyObjectDefinitionEntries(ObjectDefinition objDef, string entityDisplayName)
        {
            /////GUID Type defination/////
            TypeDefinition typeDefGUID = new SimpleType() { ClrType = typeof(System.Guid), Name = "System.Guid" };
            /////GUID Type defination/////

            /////String Type defination/////
            Type tempTypeString = CRM2011AdapterUtilities.SimpleTypeConvert(AttributeTypeCode.String);
            TypeDefinition typeDefString = new SimpleType() { ClrType = tempTypeString, Name = tempTypeString.FullName };
            /////String Type defination/////

            /////EntityReference Type defination/////
            ComplexType complexTypeER = new ComplexType() { ClrType = typeof(System.Collections.Generic.Dictionary<string, object>), Name = "EntityReference" };

            Type fieldType1 = CRM2011AdapterUtilities.SimpleTypeConvert(AttributeTypeCode.String);
            ComplexType fieldTypeER = new ComplexType() { ClrType = fieldType1, Name = fieldType1.FullName };

            FieldDefinition tempDefER1 = new FieldDefinition() { Name = "name", DisplayName = "name", TypeDefinition = fieldTypeER, TypeName = fieldTypeER.Name };
            complexTypeER.Fields.Add(tempDefER1);

            FieldDefinition tempDefER2 = new FieldDefinition() { Name = "type", DisplayName = "type", TypeDefinition = fieldTypeER, TypeName = fieldTypeER.Name };
            complexTypeER.Fields.Add(tempDefER2);

            FieldDefinition tempDefER3 = new FieldDefinition() { Name = "value", DisplayName = "value", TypeDefinition = fieldTypeER, TypeName = fieldTypeER.Name };
            complexTypeER.Fields.Add(tempDefER3);
            /////EntityReference Type defination/////

            /////ManyToManyRelationship Type defination/////
            ComplexType complexTypeR = new ComplexType() { ClrType = typeof(System.Collections.Generic.Dictionary<string, object>), Name = "ManyToManyRelationship" };

            ComplexType fieldTypeR = new ComplexType() { ClrType = fieldType1, Name = fieldType1.FullName };

            FieldDefinition tempDef1 = new FieldDefinition() { Name = "Target", DisplayName = "Target", TypeDefinition = complexTypeER, TypeName = complexTypeER.Name };
            complexTypeR.Fields.Add(tempDef1);

            FieldDefinition tempDef2 = new FieldDefinition() { Name = "RelatedEntity", DisplayName = "RelatedEntity", TypeDefinition = complexTypeER, TypeName = complexTypeER.Name };
            complexTypeR.Fields.Add(tempDef2);

            FieldDefinition tempDef3 = new FieldDefinition() { Name = "Relationship", DisplayName = "Relationship", TypeDefinition = fieldTypeR, TypeName = fieldTypeR.Name };
            complexTypeR.Fields.Add(tempDef3);
            /////ManyToManyRelationship Type defination/////  

            FieldDefinition entityDef = new FieldDefinition() { Name = entityDisplayName.ToString().Replace(" ", string.Empty), DisplayName = entityDisplayName, TypeDefinition = complexTypeR, TypeName = complexTypeR.Name, IsRequired = false };
            objDef.Types.First().Children.Add(entityDef);

            /////Adding GUID Type defination/////
            objDef.Types.Add(typeDefGUID);
            /////GUID Type defination/////

            /////Adding String Type defination/////
            objDef.Types.Add(typeDefString);
            /////String Type defination/////

            /////Adding EntityReference Type defination/////
            objDef.Types.Add(complexTypeER);
            /////EntityReference Type defination/////

            /////Adding ManyToManyRelationship Type defination/////
            objDef.Types.Add(complexTypeR);
            /////ManyToManyRelationship Type defination/////  
        }

        private static void GeneratePicklistObjectDefinitionEntries(ObjectDefinition objDef, RetrieveAllEntitiesResponse response)
        {
            
            var orderedEntityMetadata = ((EntityMetadata[])response.Results.First().Value).Where(x=>x.DisplayName.LocalizedLabels.Count > 0).OrderBy(x => x.DisplayName.LocalizedLabels[0].Label);
            
            // For each entity, add a Type
            foreach (var entityMetadata in orderedEntityMetadata)
            {
                var picklistAttributesMetaData = entityMetadata.Attributes.Where(x => x.AttributeType == AttributeTypeCode.Picklist && x.IsCustomizable.Value == true).OrderBy(x => x.LogicalName);
                var picklistAttributes = picklistAttributesMetaData.Where(x => ((PicklistAttributeMetadata)x).OptionSet.IsGlobal.Value == false);


                var entityDisplayName = entityMetadata.DisplayName.LocalizedLabels[0].Label;
                // All picklists for an entity are here.
                if (picklistAttributes.Count() > 0)
                {
                    FieldDefinition entityDef = new FieldDefinition() { Name = entityDisplayName.ToString().Replace(" ", string.Empty), DisplayName = entityDisplayName, TypeName = entityMetadata.LogicalName, IsRequired = false };
                    objDef.Types.First().Children.Add(entityDef);
                    ComplexType complexType = new ComplexType() { ClrType = typeof(System.Collections.Generic.Dictionary<string, object>), Name = entityMetadata.LogicalName };
                    objDef.Types.Add(complexType);
                    foreach (var picklist in picklistAttributes)
                    {
                        string displayName = picklist.DisplayName.LocalizedLabels[0].Label;
                        ComplexType fieldType = CRM2011AdapterUtilities.ComplexTypeConvert(picklist.AttributeType.Value, objDef);

                        if (complexType != null)
                        {
                            FieldDefinition tempDef = new FieldDefinition() { Name = picklist.LogicalName, DisplayName = displayName, TypeDefinition = fieldType, TypeName = fieldType.Name };
                            complexType.Fields.Add(tempDef);
                        }

                    }

                }
            }
            var optionSetValueType = new CollectionType() { ClrType = typeof(string[]), Name = "OptionSetValue" };
            optionSetValueType.Item = new FieldDefinition() { Name = "Item", TypeName = "System.String", DisplayName = "Options", IsRequired = false };

            objDef.Types.Add(optionSetValueType);

        }

        private void GenerateObjectDefinitionEntries(ObjectDefinition objDef, RetrieveEntityResponse response, string parentTypeName)
        {
            foreach (XrmMetadata.AttributeMetadata attributeMeta in response.EntityMetadata.Attributes.OrderBy(a => a.LogicalName))
            {
                if (attributeMeta.DisplayName.LocalizedLabels.Count > 0&& !CRM2011AdapterUtilities.GetUnmappedLookupFields().Contains(attributeMeta.LogicalName))
                {
                    string displayName = attributeMeta.DisplayName.LocalizedLabels[0].Label;
                    Type tempType = CRM2011AdapterUtilities.SimpleTypeConvert(attributeMeta.AttributeType.Value);
                    if (tempType != typeof(object))
                    {
                        TypeDefinition typeDef = new SimpleType() { ClrType = tempType, Name = tempType.FullName };
                        FieldDefinition tempDef = new FieldDefinition() { Name = attributeMeta.LogicalName, DisplayName = displayName, TypeDefinition = typeDef, TypeName = typeDef.ClrTypeName };
                        AddTypes(objDef, tempDef, typeDef, parentTypeName);
                    }
                    else
                    {
                        ComplexType complexType = CRM2011AdapterUtilities.ComplexTypeConvert(attributeMeta.AttributeType.Value, objDef);
                        if (complexType != null)
                        {
                            FieldDefinition tempDef = new FieldDefinition() { Name = attributeMeta.LogicalName, DisplayName = displayName, TypeDefinition = complexType, TypeName = complexType.Name };
                            LookupAttributeMetadata lookupMeta = attributeMeta as LookupAttributeMetadata;
                            if (lookupMeta != null && complexType.Name != "PartyList")
                            {
                                tempDef.AdditionalAttributes = GetLookupAttributes(lookupMeta).ToArray();
                            }
                            else if (lookupMeta != null && complexType.Name == "PartyList")
                            {
                                var attribs = GetLookupAttributes(lookupMeta);
                                foreach (var field in complexType.Fields)
                                {
                                    if (field.TypeName == "EntityReference")
                                    {
                                        field.AdditionalAttributes = attribs.ToArray();
                                    }
                                }
                            }

                            AddTypes(objDef, tempDef, complexType, parentTypeName);
                        }
                    }
                }
            }
        }

        private List<XmlAttribute> GetLookupAttributes(LookupAttributeMetadata lookupMeta)
        {
            // Need to use this to add additional attribute fields to ComplexType definition that contains complex types.
            List<XmlAttribute> attribs = new List<XmlAttribute>();
            XmlDocument doc = new XmlDocument();
            XmlAttribute typeAttrib = doc.CreateAttribute(CRM2011AdapterUtilities.LookupType);
            XmlAttribute fieldAttrib = doc.CreateAttribute(CRM2011AdapterUtilities.LookupField);
            if (lookupMeta.Targets.Length > 0)
            {
                string[] types = lookupMeta.Targets;

                for (int i = 0; i < types.Count(); i++)
                {

                    typeAttrib.Value += types[i];

                    if (types[i].Equals("transactioncurrency"))
                    {
                        fieldAttrib.Value += "isocurrencycode";
                    }
                    // Things like systemuser should be modified to utilize a different value captured in the config utility.
                    // thereby allowing the user to select the matching field prior to having the config created and 
                    // requiring manual modifications later.
                    else if (types[i].Equals("systemuser"))
                    {
                        fieldAttrib.Value += "fullname";
                    }
                    else if (types[i].Equals("businessunit") || types[i].Equals("uom") || types[i].Equals("uomschedule"))
                    {
                        fieldAttrib.Value += "name";
                    }
                    else if (types.Last().Equals("activitypointer"))
                    {
                        fieldAttrib.Value += "activityid";
                    }
                    else
                    {
                       var entityMetaDataRequest = new RetrieveEntityRequest()
                       {
                            LogicalName = types[i].ToString(),
                            EntityFilters = EntityFilters.Entity
                       };
                             
                       var entityMetaDataResponse = this.InstallAdapter.OrganizationService.Execute(entityMetaDataRequest) as RetrieveEntityResponse;

                      if (entityMetaDataResponse != null)
                          fieldAttrib.Value += entityMetaDataResponse.EntityMetadata.PrimaryIdAttribute;
                       else
                          fieldAttrib.Value += types[i] + "id";
                    }

                    if (types.Count() > 0 && i < types.Count() - 1)
                    {
                        typeAttrib.Value += ",";
                        fieldAttrib.Value += ",";
                    }

                    if (string.CompareOrdinal(typeAttrib.Value, "systemuser") == 0)
                    {
                        XmlAttribute skipValidation = doc.CreateAttribute(CRM2011AdapterUtilities.ReferenceValidationSkip);
                        skipValidation.Value = true.ToString(CultureInfo.InvariantCulture).ToUpperInvariant();
                        attribs.Add(skipValidation);
                    }
                }

                attribs.Add(typeAttrib);
                attribs.Add(fieldAttrib);
            }

            return attribs;
        }



        private static void AddTypes(ObjectDefinition objDef, FieldDefinition fieldDef, TypeDefinition typeDef, string parentTypeName)
        {
            var parentType = objDef.Types.SingleOrDefault(t => t.Name.ToUpperInvariant() == parentTypeName.ToUpperInvariant());
            if (parentType != null && !CRM2011AdapterUtilities.GetUnmappedLookupFields().Contains(fieldDef.Name))
            {
                var exists = parentType.Children.Find(fd => fd.Name == fieldDef.Name);
                if (exists == null)
                {
                    objDef.Types.Single(t => t.Name.ToUpperInvariant() == parentTypeName.ToUpperInvariant()).Children.Add(fieldDef);
                }

                if (objDef.Types.Find(f => f.Name.ToUpperInvariant() == typeDef.Name.ToUpperInvariant()) == null)
                {
                    objDef.Types.Add(typeDef);
                }
            }
        }

        private static string FormattedString(string stringToFormat)
        {
            return string.Format(CultureInfo.InvariantCulture, stringToFormat);
        }

        private void InitArguments()
        {
            this.Arguments = new Dictionary<string, object>();
            foreach (string argumentName in Constants.ArgumentNames)
            {
                this.Arguments.Add(argumentName, null);
            }
        }

        private void SetPropertiesFromAppConfig()
        {
            // The the value of the install adapter's Proxy Server property to the one listed in the app.config, if one is listed
            if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings.Get("ProxyServer")))
            {
                this.InstallAdapter.ProxyServer = ConfigurationManager.AppSettings.Get("ProxyServer");
            }
        }

        /// <summary>
        /// Query the metadata of the picklist objects to sync the source and destination orgs.
        /// </summary>
        /// <param name="objDef"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private void FillPicklistObjectDef(ObjectDefinition objDef)
        {
            
            var entityMetaDataRequest = new RetrieveAllEntitiesRequest();
            entityMetaDataRequest.EntityFilters = EntityFilters.Attributes;
            entityMetaDataRequest.RetrieveAsIfPublished = true;
            var entityMetaDataResponse = this.InstallAdapter.OrganizationService.Execute(entityMetaDataRequest) as RetrieveAllEntitiesResponse;

            GeneratePicklistObjectDefinitionEntries(objDef, entityMetaDataResponse);

            var allSetsRequest = new RetrieveAllOptionSetsRequest();
            var allSetsResponse = this.InstallAdapter.OrganizationService.Execute(allSetsRequest) as RetrieveAllOptionSetsResponse;
            GenerateGlobalPicklistObjectDefinitionEntries(objDef, allSetsResponse);
        }

        /// <summary>
        /// Query the metadata of the manytomany objects to sync the source and destination orgs.
        /// </summary>
        /// <param name="objDef"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private static void FillEntityManytoManyObjectDef(ObjectDefinition objDef, string entityDisplayName)
        {
            GenerateEntityManytoManyObjectDefinitionEntries(objDef, entityDisplayName);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private void FillObjectDef(ObjectDefinition objDef)
        {
            RetrieveEntityRequest request = new RetrieveEntityRequest() { LogicalName = objDef.RootDefinition.TypeName, EntityFilters = EntityFilters.Attributes | EntityFilters.Relationships, RetrieveAsIfPublished = true };
            RetrieveEntityResponse response = this.InstallAdapter.OrganizationService.Execute(request) as RetrieveEntityResponse;
            

            GenerateObjectDefinitionEntries(objDef, response, objDef.RootDefinition.TypeName);
            List<OneToManyRelationshipMetadata> parentChildRelationships = (from meta in response.EntityMetadata.OneToManyRelationships
                                                                            where (meta.CascadeConfiguration.Reparent.Value == CascadeType.NoCascade && meta.IsCustomRelationship.Value == true)
                                                                            || (meta.SecurityTypes == SecurityTypes.ParentChild && meta.IsCustomRelationship.Value == false)
                                                                            || meta.ReferencingEntity == "customeraddress"
                                                                            select meta).ToList();

            

            foreach (OneToManyRelationshipMetadata relation in parentChildRelationships)
            {
                if (!(relation.ReferencedEntity == "account" || relation.ReferencedEntity == "contact"))
                {
                    TypeDefinition typeDef = new CollectionType() { ClrType = typeof(System.Collections.Generic.Dictionary<string, object>[]), Name = relation.SchemaName };
                    FieldDefinition tempDef = new FieldDefinition() { Name = relation.SchemaName, DisplayName = typeDef.Name.Replace('_', ' '), TypeDefinition = typeDef, TypeName = typeDef.ClrTypeName };
                    if ("salesorder_details" != tempDef.Name && "invoice_details" != tempDef.Name && "price_level_product_price_levels" != tempDef.Name)
                    {
                        AddTypes(objDef, tempDef, typeDef, objDef.RootInstance.Name);
                        FieldDefinition itemDef = new FieldDefinition() { Name = "item", DisplayName = relation.SchemaName.Replace('_', ' ').TrimEnd('s'), TypeName = relation.ReferencingEntity };
                        ((CollectionType)typeDef).Item = itemDef;
                        if (objDef.Types.Find(f => f.Name.ToUpperInvariant() == relation.ReferencingEntity.ToUpperInvariant()) == null)
                        {
                            objDef.Types.Add(new ComplexType() { ClrType = typeof(System.Collections.Generic.Dictionary<string, object>), Name = relation.ReferencingEntity });
                        }
                    }
                }

                request = new RetrieveEntityRequest() { EntityFilters = EntityFilters.All, LogicalName = relation.ReferencingEntity, RetrieveAsIfPublished = true }; ;
                response = this.InstallAdapter.OrganizationService.Execute(request) as RetrieveEntityResponse;
                
                GenerateObjectDefinitionEntries(objDef, response, relation.ReferencingEntity);
                XmlDocument doc = new XmlDocument();
                XmlAttribute parentAttrib = doc.CreateAttribute(CRM2011AdapterUtilities.IsParentField);
                parentAttrib.Value = true.ToString(CultureInfo.InvariantCulture).ToUpperInvariant();
                var referencingType = objDef.Types.SingleOrDefault(t => t.Name == relation.ReferencingEntity);
                if (referencingType != null)
                {
                    var fieldDef = referencingType.Children.SingleOrDefault(field => field.Name == relation.ReferencingAttribute);
                    if (fieldDef != null)
                    {
                        // Add the parent attribute to the additional attributes array
                        List<XmlAttribute> attribs = fieldDef.AdditionalAttributes != null ? fieldDef.AdditionalAttributes.ToList() : new List<XmlAttribute>();
                        var existingAttrib = attribs.FirstOrDefault(att => att.Name == parentAttrib.Name);
                        if (existingAttrib == null)
                        {
                            attribs.Add(parentAttrib);
                            fieldDef.AdditionalAttributes = attribs.ToArray();
                        }
                    }
                }
            }
        }

        private List<ObjectProvider> GetObjectProviders()
        {
            if (this.InstallAdapter.MetadataTimestamp != this.InstallAdapter.RetrieveMetadataTimestamp())
            {
                List<ObjectProvider>  MtoMAvailableProviders = new List<ObjectProvider>();

                this.availableProviders = new List<ObjectProvider>();
                RetrieveAllEntitiesRequest retrieveAll = new RetrieveAllEntitiesRequest();
                RetrieveAllEntitiesResponse response = new RetrieveAllEntitiesResponse();
                RetrieveEntityRequest entityRequest = new RetrieveEntityRequest();

                this.PublishPreConfigurationMessage(string.Format(CultureInfo.CurrentCulture, Resources.StartRetrievingEntityMetadata));
                response = this.InstallAdapter.OrganizationService.Execute(retrieveAll) as RetrieveAllEntitiesResponse;
                foreach (EntityMetadata crmMetadata in response.EntityMetadata)
                {
                    entityRequest.MetadataId = crmMetadata.MetadataId.Value;
                    entityRequest.EntityFilters = EntityFilters.Relationships;
                    RetrieveEntityResponse entityResponse = this.InstallAdapter.OrganizationService.Execute(entityRequest) as RetrieveEntityResponse;
                    if (entityResponse.EntityMetadata.DisplayName.LocalizedLabels.Count > 0 && !entityResponse.EntityMetadata.LogicalName.StartsWith("dynamics_deleted", StringComparison.OrdinalIgnoreCase) && entityResponse.EntityMetadata.IsCustomizable.Value)
                    {
                        this.availableProviders.Add(new DynamicObjectProvider() { Adapter = this.InstallAdapter, Id = crmMetadata.MetadataId.Value, DisplayName = entityResponse.EntityMetadata.DisplayName.LocalizedLabels[0].Label, Name = entityResponse.EntityMetadata.LogicalName });
                    }


                    List<ManyToManyRelationshipMetadata> entityMToMRelationships = (from meta in entityResponse.EntityMetadata.ManyToManyRelationships
                                                                                         where (meta.IsCustomRelationship.Value == true)
                                                                                         || (meta.SecurityTypes == SecurityTypes.ParentChild && meta.IsCustomRelationship.Value == false)
                                                                                         select meta).ToList();
                    if (entityMToMRelationships.Count > 0)
                    {
                        foreach (ManyToManyRelationshipMetadata relation in entityMToMRelationships)
                        {
                            if (!MtoMAvailableProviders.Any(f=> f.DisplayName == relation.SchemaName))
                                MtoMAvailableProviders.Add(new DynamicObjectProvider() { Adapter = this.InstallAdapter, Id = relation.MetadataId.Value, DisplayName = relation.SchemaName, Name = relation.SchemaName + ",Relationship"});
                        }
                    }
                }

                foreach (ObjectProvider relationObjectProvider in MtoMAvailableProviders)
                {
                    this.availableProviders.Add(relationObjectProvider);
                }

                this.PublishPostConfigurationMessage(string.Format(CultureInfo.CurrentCulture, Resources.FinishedRetreivingEntityMetadata));
                this.InstallAdapter.MetadataTimestamp = this.InstallAdapter.RetrieveMetadataTimestamp();
                this.PublishPostConfigurationMessage(string.Format(CultureInfo.CurrentCulture, Resources.MetadataTimeStamp, this.InstallAdapter.MetadataTimestamp));
            }

            return this.availableProviders;
        }

        #endregion
    }
}
