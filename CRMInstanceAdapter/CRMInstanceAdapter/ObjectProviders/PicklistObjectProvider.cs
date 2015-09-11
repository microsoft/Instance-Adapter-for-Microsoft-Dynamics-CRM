namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm.ObjectProviders
{
    using AdapterAbstractionLayer;
    using Common;
    using Properties;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.ServiceModel;
    using Xrm.Sdk;
    using Xrm.Sdk.Messages;
    using Xrm.Sdk.Metadata;

    /// <summary>
    /// Provides interaction with the Dynamics CRM <c>picklist</c> and <c>DynamicEntity</c> objects.
    /// </summary>
    public class PicklistObjectProvider : CrmObjectProvider, IObjectReader, IObjectWriter, IBulkObjectWriter
    {
        private const string NotInErp = "(Not in ERP)";

        /// <summary>
        /// Initializes a new instance of the <see cref="PicklistObjectProvider"/> class.
        /// </summary>
        public PicklistObjectProvider()
            : base()
        {
            this.DisplayName = Resources.PicklistObjectProviderDisplayName;
            this.Name = Resources.PicklistObjectProviderName;
            this.Id = new Guid("{B06F080B-890D-49A2-BFA4-FCB866462673}");
            this.ProvidedEntityName = "Picklist";
            this.DoesDetectDuplicates = false;
        }

        /// <summary>
        /// Writes an <c>object</c> to the stream
        /// </summary>
        /// <param name="value">The object to be written</param>
        public void WriteObject(object value)
        {
            // The key string is TypeName
            Dictionary<string, object> suppliedList = value as Dictionary<string, object>;
            if (suppliedList == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidDictionaryCastException)) { ExceptionId = ErrorCodes.InvalidDictionaryCast };
            }

            foreach (string key in suppliedList.Keys)
            {
                try
                {
                    FieldDefinition optionSetDef = this.ObjectDefinition.RootDefinition.TypeDefinition.Children.Single(ch => ch.Name == key);
                    if (optionSetDef.FindAttribute(CRM2011AdapterUtilities.IsGlobal) != null && bool.Parse(optionSetDef.FindAttribute(CRM2011AdapterUtilities.IsGlobal).Value))
                    {
                        this.ProcessGlobalPicklists(suppliedList[key] as string[], key);
                    }
                    else
                    {
                        this.ProcessPickLists(suppliedList[key] as Dictionary<string, object>, optionSetDef.TypeName);
                    }
                }
                catch (InvalidOperationException)
                {
                    throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidGlobalOptionSet, key)) { ExceptionId = ErrorCodes.InvalidGlobalOptionSet };
                }
            }            
        }

        /// <summary>
        /// Deletes an <c>object</c>
        /// </summary>
        /// <param name="key">The <c>object</c> to be deleted</param>
        public void DeleteObject(object key)
        {
            return;
        }

        /// <summary>
        /// Read an <c>object</c> from the stream
        /// </summary>
        /// <param name="key">The key for the <c>object</c> to be read</param>
        /// <returns>An instance of the <c>object</c> provided by this <c>ObjectProvider</c> that has the key provided</returns>
        public object ReadObject(object key)
        {
            if (key == null || key.GetType() != typeof(Guid))
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.SuppliedKeyTypeExceptionMessage), new ArgumentException(Resources.SuppliedKeyTypeExceptionMessage, "key")) { ExceptionId = ErrorCodes.SuppliedKeyCastException };
            }

            Guid metaKey = (Guid)key;

            // Will throw exception if the GUID is not related to a Entity but rather to a global option set.
            try
            {
                RetrieveEntityRequest entityRequest = new RetrieveEntityRequest() { EntityFilters = EntityFilters.Attributes, MetadataId = metaKey, RetrieveAsIfPublished = true };
                RetrieveEntityResponse entityResponse = (RetrieveEntityResponse)this.CallMetadataExecuteWebMethod(entityRequest);
                var anyPicklist = entityResponse.EntityMetadata.Attributes.Where(x => x.AttributeType == AttributeTypeCode.Picklist);

                if (anyPicklist.Count() > 0)
                {
                    Dictionary<string, object> parentDictionary = new Dictionary<string, object>();
                    Dictionary<string, object> childDictionary = new Dictionary<string, object>();

                    foreach (var attributeMeta in anyPicklist)
                    {
                        var picklistMeta = attributeMeta as PicklistAttributeMetadata;
                        if (picklistMeta.OptionSet.IsGlobal.Value != true)
                        {
                            var options = new List<string>();
                            foreach (var option in picklistMeta.OptionSet.Options)
                            {
                                if (option.Label.LocalizedLabels.Count > 0)
                                {
                                    options.Add(option.Label.LocalizedLabels[0].Label);
                                }
                            }

                            childDictionary.Add(attributeMeta.LogicalName, options.ToArray<string>());
                        }
                    }

                    parentDictionary.Add(entityResponse.EntityMetadata.DisplayName.LocalizedLabels[0].Label.Replace(" ", string.Empty), childDictionary);

                    return parentDictionary;
                }
                else
                {
                    return null;
                }
            }            
            catch (AdapterException)
            {
                var optionSetMetaRequest = new RetrieveOptionSetRequest() { MetadataId = metaKey };
                var optionSetMetaResponse = (RetrieveOptionSetResponse)this.CallMetadataExecuteWebMethod(optionSetMetaRequest);
                var optionSetMeta = (OptionSetMetadata)optionSetMetaResponse.OptionSetMetadata;
                var metaOptions = optionSetMeta.Options;

                var options = new List<string>();

                foreach (var option in metaOptions)
                {
                    options.Add(option.Label.LocalizedLabels[0].Label);
                }

                Dictionary<string, object> optionSet = new Dictionary<string, object>();
                optionSet.Add(optionSetMeta.Name, options.ToArray<string>());
                return optionSet;
            }
        }

        /// <summary>
        /// Gets an <c>ICollection</c> containing <c>Key</c> objects that match the supplied modifiedDate
        /// </summary>
        /// <param name="modifiedDate">A <c>DateTime</c> containing the last modified date for the object keys to be returned</param>
        /// <returns>An <c>ICollection</c> containing keys that can be used to retrieve instances of the provided object from the source system</returns>
        public ICollection ReadObjectKeys(DateTime modifiedDate)
        {
            List<Guid> keys = new List<Guid>();

            // Retrieve all entities and parse for local picklists
            RetrieveAllEntitiesRequest allRequest = new RetrieveAllEntitiesRequest() { RetrieveAsIfPublished = true, EntityFilters = EntityFilters.Attributes };
            RetrieveAllEntitiesResponse allresponse = (RetrieveAllEntitiesResponse)this.CallMetadataExecuteWebMethod(allRequest);
            foreach (var meta in allresponse.EntityMetadata.OrderBy(x => x.LogicalName))
            {
                var picklistMeta = meta.Attributes.Where(x => x.AttributeType == AttributeTypeCode.Picklist && x.IsCustomizable.Value == true && meta.DisplayName.LocalizedLabels.Count > 0);
                if (picklistMeta.Count() > 0)
                {
                    if (picklistMeta.Cast<PicklistAttributeMetadata>().Any(x => x.OptionSet.IsGlobal.Value == false))
                    {
                        keys.Add(meta.MetadataId.Value);
                    }
                }
            }

            // Retrieve global option sets that are customizable
            var globalOptionSetRequest = new RetrieveAllOptionSetsRequest() { RetrieveAsIfPublished = true };
            var globalResponse = (RetrieveAllOptionSetsResponse)this.CallMetadataExecuteWebMethod(globalOptionSetRequest);
            var usableGlobalOptionSets = globalResponse.OptionSetMetadata.Where(x => x.IsCustomizable.Value == true && x.DisplayName.LocalizedLabels.Count > 0);

            foreach (var globalOption in usableGlobalOptionSets)
            {
                keys.Add(globalOption.MetadataId.Value);
            }

            return keys;
        }

        /// <summary>
        /// Gets an <c>ICollection</c> containing <c>Key</c> objects that match the supplied modifiedDate.
        /// </summary>
        /// <param name="modifiedDate">A <c>DateTime</c> containing the last modified date for the object keys to be returned.</param>
        /// <returns>An <c>ICollection</c> of keys for entities that have been deleted in the source system.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Interface implementation."), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "modifiedDate", Justification = "Interface implementation")]
        public ICollection ReadDeletedObjectKeys(DateTime modifiedDate)
        {
            return new List<string>();
        }

        /// <summary>
        /// Publishes the CRM metadata file
        /// </summary>
        public void PublishMetadata()
        {
            try
            {
                OrganizationRequest publishRequest = new OrganizationRequest("PublishAllXml");
                this.CrmAdapter.OrganizationService.Execute(publishRequest);
            }
            catch (Exception ex)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.PublishExceptionMessage, ex.Message), ex) { ExceptionId = ErrorCodes.MetadataPublishException };
            }
        }

        /// <summary>
        /// Begins a bulk operation
        /// </summary>
        /// <param name="expectedCount">The expected number of records to be integrated by the bulk operation.</param>
        public void Begin(int expectedCount)
        {            
        }

        /// <summary>
        /// Cancels a bulk operation
        /// </summary>
        public void Cancel()
        {            
        }

        /// <summary>
        /// Finishes a bulk operation
        /// </summary>
        public void Finish()
        {
            this.PublishMetadata();
        }

        /// <summary>
        /// Creates a CRM <c>Label</c>
        /// </summary>
        /// <param name="label">The test of the <c>Label</c> to be created.</param>
        /// <param name="langCode">The language code for the <c>Label</c>.</param>
        /// <returns>A new CRM <c>Label</c>.</returns>
        private static Label CreateSingleLabel(string label, int langCode)
        {
            return new Label(label, langCode);
        }

        /// <summary>
        /// Processes the global option sets
        /// </summary>
        /// <param name="dictionary">The <see cref="String"/> array that contains the global option set values.</param>
        /// <param name="key">The option set name to be created or updated.</param>
        private void ProcessGlobalPicklists(string[] dictionary, string key)
        {
            if (dictionary.Length > 0)
            {
                try
                {
                    RetrieveOptionSetRequest retrieveOptionSetRequest = new RetrieveOptionSetRequest { Name = key };
                    RetrieveOptionSetResponse retrieveOptionSetResponse = (RetrieveOptionSetResponse)this.CrmAdapter.OrganizationService.Execute(retrieveOptionSetRequest);
                    OptionSetMetadata retrievedOptionSetMetadata = (OptionSetMetadata)retrieveOptionSetResponse.OptionSetMetadata;
                    OptionMetadata[] optionList = retrievedOptionSetMetadata.Options.ToArray();
                    foreach (string label in dictionary)
                    {
                        var option = optionList.FirstOrDefault(opt => opt.Label.UserLocalizedLabel.Label == label);
                        if (option == null)
                        {
                            InsertOptionValueRequest insertOptionValueRequest = new InsertOptionValueRequest { OptionSetName = key, Label = new Label(label, retrievedOptionSetMetadata.DisplayName.UserLocalizedLabel.LanguageCode) };
                            this.CrmAdapter.OrganizationService.Execute(insertOptionValueRequest);
                        }
                    }
                }
                catch (FaultException ex)
                {
                    throw new AdapterException(ex.Message, ex) { ExceptionId = ErrorCodes.CrmPlatformException };
                }
            }
        }

        /// <summary>
        /// Processes the non-global option sets
        /// </summary>
        /// <param name="dictionary">The <C>Dictionary</C> that contains the name value pairs for the option set.</param>
        /// <param name="entityName">The name of the <C>Entity</C> that the option set belongs to.</param>
        private void ProcessPickLists(Dictionary<string, object> dictionary, string entityName)
        {
            if (dictionary == null)
            {
                return;
            }

            OrganizationResponse langRes = null;
            foreach (string attributeName in dictionary.Keys)
            {
                string[] values = dictionary[attributeName] as string[];
                if (values.Length < 1)
                {
                    continue;
                }

                RetrieveAttributeRequest attribReq = new RetrieveAttributeRequest();
                attribReq.EntityLogicalName = entityName;
                attribReq.LogicalName = attributeName;
                attribReq.RetrieveAsIfPublished = true;
                OrganizationRequest langReq = new OrganizationRequest("RetrieveAvailableLanguages");

                // Get the attribute metadata for the state attribute.
                RetrieveAttributeResponse metadataRespone = null;
                try
                {
                    metadataRespone = (RetrieveAttributeResponse)this.CrmAdapter.OrganizationService.Execute(attribReq);
                    if (langRes == null)
                    {
                        langRes = this.CrmAdapter.OrganizationService.Execute(langReq);
                    }
                }
                catch (Exception e)
                {
                    throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.MetadataClientPicklistExceptionMessage, entityName, e.Message), e) { ExceptionId = ErrorCodes.PicklistMetadataRetrieval };
                }

                if (metadataRespone != null)
                {
                    PicklistAttributeMetadata picklistAttrib = metadataRespone.AttributeMetadata as PicklistAttributeMetadata;
                    int optionNumber = 200000;
                    InsertOptionValueRequest insertRequest = new InsertOptionValueRequest();
                    insertRequest.AttributeLogicalName = picklistAttrib.LogicalName;
                    insertRequest.EntityLogicalName = entityName;
                    insertRequest.Label = new Label();
                    insertRequest.Value = new int?();
                    foreach (string picklistName in values)
                    {
                        try
                        {
                            var option = picklistAttrib.OptionSet.Options.FirstOrDefault(opt => opt.Label.UserLocalizedLabel.Label.Replace(NotInErp, string.Empty).Trim().ToUpperInvariant() == picklistName.ToUpperInvariant() || opt.Label.UserLocalizedLabel.Label.Replace("*", string.Empty).Trim().ToUpperInvariant() == picklistName.ToUpperInvariant());
                            optionNumber += picklistAttrib.OptionSet.Options.Count();

                            // Add new values
                            if (option == null)
                            {
                                insertRequest.Value = optionNumber++;
                                insertRequest.Label = CreateSingleLabel(picklistName, metadataRespone.AttributeMetadata.DisplayName.UserLocalizedLabel.LanguageCode);
                                this.CrmAdapter.OrganizationService.Execute(insertRequest);
                            }
                            else if (option.Label.UserLocalizedLabel.Label != picklistName)
                            {
                                // Update existing values if they are different
                                this.CrmAdapter.OrganizationService.Execute(new UpdateOptionValueRequest() { AttributeLogicalName = picklistAttrib.LogicalName, EntityLogicalName = entityName, Label = CreateSingleLabel(picklistName, option.Label.UserLocalizedLabel.LanguageCode), MergeLabels = false, Value = option.Value.Value });
                            }
                        }
                        catch (FaultException e)
                        {
                            if (e.Message.Contains("because another picklist or status option for this attribute already exists"))
                            {
                                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ValueExistsMessage, entityName, e.Message), e) { ExceptionId = ErrorCodes.PicklistMetadataCreation };
                            }
                            else
                            {
                                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.AddingValueExceptionMessage, picklistName, e.Message), e) { ExceptionId = ErrorCodes.PicklistMetadataCreation };
                            }
                        }
                    }
                }
            }
        }
    }
}
