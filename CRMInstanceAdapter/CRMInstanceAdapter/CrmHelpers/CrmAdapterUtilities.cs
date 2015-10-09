namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm
{
    using AdapterAbstractionLayer;
    using Common;
    using Properties;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Xml;
    using Xrm.Sdk;
    using Xrm.Sdk.Messages;
    using Xrm.Sdk.Metadata;
    using Xrm.Sdk.Query;

    /// <summary>
    /// Utility class to hold various operations that are common when working with Dynamics CRM.
    /// </summary>
    public static class CRM2011AdapterUtilities
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2211:NonConstantFieldsShouldNotBeVisible", Justification = "Guid fields cannot be made constant.")]
        public const string DynamicsIntegrationKey = "dynamics_integrationkey";
        public const string DeletedPrefix = "dynamics_deleted";
        public const string DynamicsIntegrationReady = "dynamics_isreadyforintegration";
        public const string IsCriteria = "IsCriteria";
        public const string IsGlobal = "IsGlobal";
        public const string ReferenceValidationSkip = "ReferenceValidationSkip";        
        public const string IsParentField = "IsParentField";
        public const string LookupType = "LookupType";
        public const string LookupField = "LookupField";
        public const string EntityMetadataId = "EntityMetadataId";
        internal const string IsNew = "IsNewObjectProviderInstance";
        internal static Guid DynamicObjectProviderID = new Guid("{C3D8DC9A-E7F9-4132-BC7E-F7991ABDEFE3}");        
        private const string InactiveState = "Inactive";

        private static string[] integratedEntities = 
        { 
          "product", "contact", "account", 
          "salesorder", "invoice", "customeraddress",
          "salesorderdetail", "invoicedetail", "systemuser"
        };

        private static string[] nameKeyedEntities = 
        { 
          "pricelevel", "productpricelevel", "discounttype", 
          "discount", "uom", "uomschedule", "businessunit",
          "organization", "transactioncurrency"
        };

        private static string[] unmappedLookupFields = { "createdby", "modifiedby" };
        private static string[] specialAddressPicklists = { "shippingmethodcode", "addresstypecode", "freighttermscode" };

        /// <summary>
        /// Gets the array of entity names that have a dynamics_integrationkey property added to them
        /// </summary>
        /// <returns>The array of entity names that have a dynamics_integrationkey property added to them</returns>
        public static string[] GetIntegratedEntities()
        {
            return integratedEntities;
        }

        /// <summary>
        /// Gets the array of entity names that use the "name" property to represent a dynamics_integrationkey
        /// </summary>
        /// <returns>The array of entity names that use the "name" property to represent a dynamics_integrationkey</returns>
        public static string[] GetNameKeyedEntities()
        {
            return nameKeyedEntities;
        }

        /// <summary>
        /// Gets the array of <c>Lookup</c> fields that are not mapped
        /// </summary>
        /// <returns>The array of <c>Lookup</c> fields that are not mapped</returns>
        public static string[] GetUnmappedLookupFields()
        {
            return unmappedLookupFields;
        }

        /// <summary>
        /// Gets the array of <c>Picklist</c> fields that are on address1 and address2 attributes
        /// </summary>
        /// <returns>The array of <c>Picklist</c> fields that are on address1 and address2 attributes</returns>
        public static string[] GetSpecialAddressPicklistFields()
        {
            return specialAddressPicklists;
        }

        /// <summary>
        /// Validates that a product is not new when retrieving from the product catalog
        /// </summary>
        /// <param name="product">The <c>Entity</c> that contains the <c>product</c> that was retrieved</param>
        /// <param name="productKey">The value of the product's dynamics_integrationkey</param>
        public static void ValidateInventoriedProduct(Entity product, string productKey)
        {
            if (product == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage), new ArgumentNullException("product")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            if (product.Contains(CRM2011AdapterUtilities.IsNew) && (bool)product[CRM2011AdapterUtilities.IsNew] == true)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.NonIntegratedProductSuppliedExceptionMessage, productKey)) { ExceptionId = ErrorCodes.ProductNotFound };
            }
        }

        /// <summary>
        /// Validates that a <c>Dictionary</c> not not contain 0 objects.
        /// </summary>
        /// <param name="mappedType">The <c>Dictionary</c> to validate</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the Count property of the <c>Dictionary</c> supplied is 0.</exception>
        public static void ValidateDictionary(Dictionary<string, object> mappedType)
        {
            if (mappedType == null || mappedType.Count == 0)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.DictionaryForComplexTypeMappingNotInitializedProperly)) { ExceptionId = ErrorCodes.InvalidDictionary };
            }
        }

        /// <summary>
        /// Gets a <c>QueryExpression</c> instance with it's properties set to the values supplied
        /// </summary>
        /// <param name="entityName">The name of the <c>BusinessEntity</c> as a <c>string</c> to be queried</param>
        /// <param name="queryProperty">The property on the <c>BusinessEntity</c> to be queried</param>
        /// <param name="propertyValue">The value for the property being queried for</param>
        /// <param name="attributes">The attributes to include on the <c>Entity</c> instance</param>
        /// <returns>An instance of a <c>QueryExpression</c> that can used as a parameter to a <c>RetrieveMultipleRequest</c></returns>
        /// <exception cref="ArgumentException">Thrown if the entityName or queryProperty parameters are null or empty</exception>
        /// <remarks>This method sets the Distinct property of the returned <c>QueryExpression</c> to <c>false</c>.  This overload
        /// also assume that the <c>LogicalOperator</c> is set to Equal</remarks>
        public static QueryExpression GetQueryExpression(string entityName, string queryProperty, string propertyValue, params string[] attributes)
        {
            return GetQueryExpression(entityName, queryProperty, propertyValue, ConditionOperator.Equal, attributes);
        }

        /// <summary>
        /// Gets a <c>QueryExpression</c> instance with it's properties set to the values supplied
        /// </summary>
        /// <param name="entityName">The name of the <c>BusinessEntity</c> as a <c>string</c> to be queried</param>
        /// <param name="queryProperty">The property on the <c>BusinessEntity</c> to be queried</param>
        /// <param name="propertyValue">The value for the property being queried for</param>
        /// <param name="conditionOperator">A logical condition operator to use for the condition expression</param>
        /// <param name="attributes">The attributes to include on the <c>Entity</c> instance, if none are supplied then all columns are returned</param>
        /// <returns>An instance of a <c>QueryExpression</c> that can used as a parameter to a <c>RetrieveMultipleRequest</c></returns>
        /// <exception cref="ArgumentException">Thrown if the entityName or queryProperty parameters are null or empty</exception>
        /// <remarks>This method sets the Distinct property of the returned <c>QueryExpression</c> to <c>false</c></remarks>
        public static QueryExpression GetQueryExpression(string entityName, string queryProperty, object propertyValue, ConditionOperator conditionOperator, params string[] attributes)
        {
            ValidatePropertyQueryParameters(entityName, queryProperty);
            QueryExpression queryHelper = new QueryExpression(entityName) { Distinct = false, Criteria = new FilterExpression() };
            if (attributes != null && attributes.Length > 0)
            {
                queryHelper.ColumnSet.AddColumns(attributes);
            }
            else
            {
                queryHelper.ColumnSet.AllColumns = true;
            }

            queryHelper.Criteria.Conditions.Add(new ConditionExpression(queryProperty, conditionOperator, propertyValue));
            return queryHelper;
        }

        /// <summary>
        /// Gets a <c>QueryExpression</c> instance with it's properties set to the values supplied
        /// </summary>
        /// <param name="entityName">The name of the <c>BusinessEntity</c> as a <c>string</c> to be queried</param>
        /// <param name="criterion">An <c>ICiterion</c> instance to use for duplicate detection</param>
        /// <param name="columnSet">A CRM <c>ColumnSet</c> containing the set of columns to be returned from the query.</param>
        /// <exception cref="AdapterException">Thrown if the <c>ICriterion</c> parameter is null</exception>
        /// <returns>A <c>QueryExpression</c> that can used to retrieve entities from the CRM Service</returns>
        /// <remarks>This method sets the Distinct property of the returned <c>QueryExpression</c> to <c>false</c></remarks>
        public static QueryExpression GetQueryExpression(string entityName, ICriterion criterion, ColumnSet columnSet)
        {
            if (criterion == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.NullICriterionExceptionMessage)) { ExceptionId = ErrorCodes.NullICriterion };
            }

            QueryExpression queryHelper = new QueryExpression(entityName) { Distinct = false, Criteria = new FilterExpression(), ColumnSet = columnSet };
            foreach (BinaryComparison comparison in criterion.ToBinaryComparisonList())
            {
                if (comparison.Value != null)
                {
                    queryHelper.ColumnSet.AddColumn(comparison.Field.FullName);
                    queryHelper.Criteria.Conditions.Add(new ConditionExpression(comparison.Field.FullName, GetConditionOperator(comparison.Operator), comparison.Value));
                }
            }

            return queryHelper;
        }

        /// <summary>
        /// Gets a <c>QueryExpression</c> instance with it's properties set to the values supplied
        /// </summary>
        /// <param name="entityName">The name of the <c>BusinessEntity</c> as a <c>string</c> to be queried</param>
        /// <param name="modifiedDate">The <c>string</c> value for the date to be queried from</param>
        /// <param name="adapter">An instance of a <c>CRM2011Adapter</c> used to retrieve the INTEGRATION user's <c>Guid</c></param>
        /// <param name="isDynamic">True if the object provider calling into this method is a <c>DynamicObbjectProvider</c> and false otherwise</param>
        /// <param name="columnSet">A CRM <c>ColumnSet</c> that contains the columns to be returned from the query.</param>
        /// <returns>An instance of a <c>QueryExpression</c> that can used as a parameter to a <c>RetrieveMultipleRequest</c></returns>
        /// <remarks>The method generates a query that is retrieved by the modified date supplied as well as the IntegrationReady flag and the modified by
        /// property not being equal to the INTEGRATION user's id</remarks>
        public static QueryExpression GetReaderQueryExpression(string entityName, DateTime modifiedDate, DynamicCrmAdapter adapter, bool isDynamic, ColumnSet columnSet)
        {
            if (adapter == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage), new ArgumentNullException("adapter")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            ValidatePropertyQueryParameters(entityName, "modifiedon");
            QueryExpression queryHelper = new QueryExpression(entityName) { Distinct = false, Criteria = new FilterExpression(), ColumnSet = columnSet };
            queryHelper.ColumnSet.AllColumns = true;

            if (entityName != null)
            {
                if (entityName == "activitymimeattachment")
                {
                    LinkEntity emailLink = new LinkEntity()
                    {
                        LinkFromEntityName = "activitymimeattachment",
                        LinkFromAttributeName = "activityid",
                        LinkToEntityName = "email",
                        LinkToAttributeName = "activityid",
                        LinkCriteria =
                        {
                            Conditions =
                            {
                              new ConditionExpression(
                                "modifiedon", ConditionOperator.GreaterEqual, modifiedDate)
                            }
                        }
                    };

                    queryHelper.LinkEntities.Add(emailLink);
                }
                else
                {
                    queryHelper.Criteria.Conditions.Add(new ConditionExpression("modifiedon", ConditionOperator.GreaterEqual, modifiedDate));
                }
            }

            if (!isDynamic)
            {
                queryHelper.Criteria.Conditions.Add(new ConditionExpression(CRM2011AdapterUtilities.DynamicsIntegrationReady, ConditionOperator.Equal, true));
            }

            return queryHelper;
        }

        /// <summary>
        /// Gets a <c>QueryExpression</c> instance for retrieving a <c>uom</c>.
        /// </summary>
        /// <param name="operand">The <c>LogicalOperand</c> to use for this query.</param>
        /// <param name="entityName">The logical name for the entity to be queried for.</param>        
        /// <param name="propertyValues">The array of <c>KeyValuePair</c>s that contains the properties and their values to be queried for.</param>
        /// <returns>An instance of a <c>QueryExpression</c> that can used as a parameter to a <c>RetrieveMultipleRequest</c>.</returns>
        /// <exception cref="ArgumentException">Thrown if the unitName or scheduleId parameters are null or empty.</exception>
        /// <remarks>This method sets the Distinct property of the returned <c>QueryExpression</c> to <c>true</c>.</remarks>
        public static QueryExpression GetMultipartQueryExpression(LogicalOperator operand, string entityName, KeyValuePair<string, string>[] propertyValues)
        {
            if (propertyValues == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage), new ArgumentNullException("propertyValues")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            QueryExpression queryHelper = new QueryExpression(entityName) { Distinct = false };
            queryHelper.Criteria = new FilterExpression() { FilterOperator = operand };
            queryHelper.ColumnSet.AllColumns = true;
            foreach (KeyValuePair<string, string> propertyValue in propertyValues)
            {
                ValidatePropertyQueryParameters(entityName, propertyValue.Key);
                queryHelper.Criteria.Conditions.Add(new ConditionExpression(propertyValue.Key, ConditionOperator.Equal, propertyValue.Value));
            }

            return queryHelper;
        }

        /// <summary>
        /// Gets a <c>Picklist</c> instance who's values are set based on the parameters provided.
        /// </summary>
        /// <param name="entity">The current <c>Entity</c>.</param>
        /// <param name="field">The current <c>DefinitionField</c> for the property being mapped to this <c>Picklist</c>.</param>
        /// <param name="mappedLookupObject">The <c>Dictionary</c> containing the lookup information for the <c>Picklist</c>.</param>
        /// <param name="crm2011Adapter">The <c>CRM2011Adapter</c> to use when calling the CRM web service.</param>
        /// <param name="providedEntityName">The name of the <c>Entity</c> that the current object provider is for.</param>
        /// <returns>A <c>Picklist</c> with it's value property set to the one requested in the <c>Dictionary</c>, if one is found and <c>null</c> otherwise.</returns>
        /// <exception cref="ArgumentException">Thrown if the requested value is not currently in the <c>Picklist</c> within the CRM system.</exception>
        public static OptionSetValue MapPicklist(Entity entity, FieldDefinition field, Dictionary<string, object> mappedLookupObject, DynamicCrmAdapter crm2011Adapter, string providedEntityName)
        {
            if (entity == null || field == null || crm2011Adapter == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage)) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            ValidateDictionary(mappedLookupObject);

            if (!mappedLookupObject.Keys.Contains("Value"))
            {
                if (!mappedLookupObject.Keys.Contains("name") || string.IsNullOrEmpty(mappedLookupObject["name"] as string))
                {
                    return null;
                }

                RetrieveAttributeRequest attribReq = new RetrieveAttributeRequest() { EntityLogicalName = entity.LogicalName, LogicalName = field.Name, RetrieveAsIfPublished = true };
                if (IsSpecialAddressPicklist(entity, field))
                {
                    attribReq.EntityLogicalName = providedEntityName;
                    attribReq.LogicalName = string.Format(CultureInfo.CurrentCulture, "address{0}_{1}", ((int?)entity["addressnumber"]).Value.ToString(CultureInfo.CurrentCulture), field.Name);
                }

                // Get the attribute metadata for the state attribute.
                RetrieveAttributeResponse metadataResponse = (RetrieveAttributeResponse)crm2011Adapter.OrganizationService.Execute(attribReq);
                PicklistAttributeMetadata picklistAttrib = (PicklistAttributeMetadata)metadataResponse.AttributeMetadata;

                var picklistValue = from option in picklistAttrib.OptionSet.Options
                                    where option.Label.UserLocalizedLabel.Label.ToUpperInvariant() == mappedLookupObject["name"].ToString().ToUpperInvariant()
                                    select option.Value;

                // ensure that both the returned list and the first item in the returned list are not null or empty.
                if ((picklistValue.Count() > 0) && (picklistValue.First() != null))
                {
                    return new OptionSetValue(picklistValue.First().Value);
                }

                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.PicklistValueNotFound, mappedLookupObject["name"].ToString(), field.DisplayName, entity.LogicalName)) { ExceptionId = ErrorCodes.PicklistMappingNotFound };
            }

            OptionSetValue mapping = new OptionSetValue();
            SetRelationshipValuesFromDictionary(mappedLookupObject, mapping);
            return mapping;
        }

        /// <summary>
        /// Maps a <c>Dictionary</c> containing <c>CrmMoney</c> information onto an instance of a <c>CrmMoney</c> entity.
        /// </summary>
        /// <param name="mappedMoneyObject">The <c>Dictionary</c> containing the <c>CrmMoney</c> information.</param>
        /// <returns>A <c>CrmMoney</c> entity with it's properties set to the ones specified in the supplied <c>Dictionary</c>.</returns>
        public static Money MapCrmMoney(Dictionary<string, object> mappedMoneyObject)
        {
            ValidateDictionary(mappedMoneyObject);
            Money returnedMoney = new Money();
            SetRelationshipValuesFromDictionary(mappedMoneyObject, returnedMoney);
            return returnedMoney;
        }

        /// <summary>
        /// Sets the properties of an <c>object</c> that has a relationship to another <c>BusinessEntity</c> to the values contained in the supplied <c>Dictionary</c>.
        /// </summary>
        /// <param name="mappedLookupObject">The <c>Dictionary</c> that hold the values to be set.</param>
        /// <param name="relationshipEntity">The <c>CrmReference</c> that needs its values set.</param>
        public static void SetRelationshipValuesFromDictionary(Dictionary<string, object> mappedLookupObject, object relationshipEntity)
        {
            if (mappedLookupObject == null || relationshipEntity == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage)) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            // foreach key contained in the dictionary, set it's corresponding property on the CrmReference type to the value from the dictionary
            foreach (KeyValuePair<string, object> entry in mappedLookupObject)
            {
                PropertyInfo p = relationshipEntity.GetType().GetProperty(entry.Key);
                if (p != null)
                {
                    p.SetValue(relationshipEntity, entry.Value, null);
                }
            }
        }

        /// <summary>
        /// Gets an instance of the <c>customeraddress</c> class.
        /// </summary>
        /// <param name="addressIntegrationKeyValue">The value of the address's dynamics_integrationkey property to query for.</param>
        /// <param name="parentKey">The parent of this address to use when querying for the existence of this address instance.</param>
        /// <param name="adapter">An instance of the <c>CRM2011Adapter</c> class to use when calling CRM.</param>
        /// <param name="addressIntegrationKeyProperty">The key attribute on the <c>customeraddress</c> that the supplied value is for.</param>
        /// <returns>A new instance with it's dynamics_integrationkey initialized to the value supplied or an existing instance.</returns>
        public static Entity GetDynamicAddressInstance(string addressIntegrationKeyValue, Guid parentKey, DynamicCrmAdapter adapter, string addressIntegrationKeyProperty)
        {
            if (parentKey == null || adapter == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage)) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            if (string.IsNullOrEmpty(addressIntegrationKeyProperty))
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.AddressIntegrationKeyPropertyInvalidMessage)) { ExceptionId = ErrorCodes.AddressIntegrationKeyPropertyException };
            }

            RetrieveMultipleRequest retrieveRequest = new RetrieveMultipleRequest();
            KeyValuePair<string, string>[] propertyValues = new KeyValuePair<string, string>[2];
            propertyValues[0] = new KeyValuePair<string, string>(addressIntegrationKeyProperty, addressIntegrationKeyValue);
            propertyValues[1] = new KeyValuePair<string, string>("parentid", parentKey.ToString());
            retrieveRequest.Query = CRM2011AdapterUtilities.GetMultipartQueryExpression(LogicalOperator.And, "customeraddress", propertyValues);
            RetrieveMultipleResponse retrieveResponse = (RetrieveMultipleResponse)adapter.OrganizationService.Execute(retrieveRequest);
            Entity returnedEntity = null;

            if (retrieveResponse.EntityCollection.Entities.Count == 1)
            {
                returnedEntity = retrieveResponse.EntityCollection.Entities[0] as Entity;
                returnedEntity[CRM2011AdapterUtilities.IsNew] = false;
                return returnedEntity;
            }
            else if (retrieveResponse.EntityCollection.Entities.Count < 1)
            {
                // this is a new entity instance and we need to map the provided data onto the DynamicsEntity       
                returnedEntity = new Entity() { LogicalName = "customeraddress" };
                if (addressIntegrationKeyProperty.Equals("customeraddressid", StringComparison.OrdinalIgnoreCase))
                {
                    returnedEntity[addressIntegrationKeyProperty] = new Guid(addressIntegrationKeyValue);
                }
                else
                {
                    returnedEntity[addressIntegrationKeyProperty] = addressIntegrationKeyValue;
                }

                returnedEntity[CRM2011AdapterUtilities.IsNew] = true;
                return returnedEntity;
            }
            else
            {
                throw new AdapterException(
                    string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.MultipleDynamicEntitiesReturnedExceptionMessage,
                    "customeraddress",
                    addressIntegrationKeyProperty,
                    addressIntegrationKeyValue))
                {
                    ExceptionId = ErrorCodes.MultipleCustomerAddressResult
                };
            }
        }

        /// <summary>
        /// Converts all representations of the state property to an integer.
        /// </summary>
        /// <param name="stateName">The state to get the integer value of as a <c>string</c>.</param>
        /// <param name="entityName">The name of the entity type to get the state code for.</param>
        /// <param name="adapter">The <see cref="DynamicCrmAdapter"/> to use for calling into a CRM for resolving that state.</param>
        /// <returns>An <c>int</c> the represents the state.</returns>
        public static int ConvertStateNameToValue(string stateName, string entityName, DynamicCrmAdapter adapter)
        {
            if (adapter == null)
            {
                throw new ArgumentNullException("adapter");
            }

            RetrieveAttributeRequest attribReq = new RetrieveAttributeRequest() { EntityLogicalName = entityName, LogicalName = "statecode", RetrieveAsIfPublished = true };

            // Get the attribute metadata for the state attribute.
            RetrieveAttributeResponse metadataResponse = (RetrieveAttributeResponse)adapter.OrganizationService.Execute(attribReq);
            StateAttributeMetadata picklistAttrib = (StateAttributeMetadata)metadataResponse.AttributeMetadata;

            var picklistValue = from option in picklistAttrib.OptionSet.Options
                                where option.Label.UserLocalizedLabel.Label.ToUpperInvariant() == stateName.ToUpperInvariant()
                                select option.Value;

            // Ensure that both the returned list and the first item in the returned list are not null or empty.
            if ((picklistValue.Count() > 0) && (picklistValue.First() != null))
            {
                return picklistValue.First().Value;
            }

            return CRM2011AdapterUtilities.GetDefaultStateCodeValue(stateName);
        }

        /// <summary>
        /// Returns the name of an object provider base on the type name specified in it's configuration file.
        /// </summary>
        /// <param name="objDef">The <c>ObjectDefinition</c> to get the type name from</param>
        /// <returns>A <c>string</c> that is the proper <c>ObjectProvider</c> name</returns>
        public static string GetObjectProviderName(ObjectDefinition objDef)
        {
            if (objDef == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage), new ArgumentNullException("objDef")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            StringBuilder builder = new StringBuilder(objDef.RootDefinition.DisplayName.ConvertToValidFileName().Replace(" ", string.Empty));
            builder.Append(string.Format(CultureInfo.InvariantCulture, Resources.ObjectProviderSuffix));
            return builder.ToString();
        }

        /// <summary>
        /// Returns the Inner text from the 'code' node inside the supplied error node.
        /// </summary>
        /// <param name="errorInfo">The <c>XmlNode</c> that contains the error code</param>
        /// <returns>A <c>string</c> that contains the error code</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1059:MembersShouldNotExposeCertainConcreteTypes", MessageId = "System.Xml.XmlNode", Justification = "This is the type that is returned from the CRM web service when an error occurs.")]
        public static string GetErrorCode(XmlNode errorInfo)
        {
            if (errorInfo == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage), new ArgumentNullException("errorInfo")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            XmlNode code = errorInfo.SelectSingleNode("//code");
            if (code != null)
            {
                return code.InnerText;
            }

            return string.Empty;
        }

        /// <summary>
        /// Converts a CRM <c>AttributeType</c> into a simple .NET type.
        /// </summary>
        /// <param name="typeToConvert">The CRM <c>AttributeType</c> to be converted.</param>
        /// <returns>A .NET <see cref="Type"/> that represents the CRM type.</returns>
        public static Type SimpleTypeConvert(AttributeTypeCode typeToConvert)
        {
            switch (typeToConvert)
            {
                case AttributeTypeCode.Boolean:
                    return typeof(bool?);
                case AttributeTypeCode.DateTime:
                    return typeof(DateTime?);
                case AttributeTypeCode.Decimal:
                    return typeof(decimal?);
                case AttributeTypeCode.Double:
                    return typeof(double?);
                case AttributeTypeCode.Integer:
                    return typeof(int?);
                case AttributeTypeCode.Memo:
                case AttributeTypeCode.String:
                    return typeof(string);
                case AttributeTypeCode.Uniqueidentifier:
                    return typeof(Guid);
                default:
                    return typeof(object);
            }
        }

        /// <summary>
        /// Converts a CRM complex type into a <see cref="ComplexType"/>.
        /// </summary>
        /// <param name="typeToConvert">The CRM <c>AttributeTypeCode</c> to be converted.</param>
        /// <param name="objDef">The <see cref="ObjectDefinition"/> that holds the type data.</param>
        /// <returns>A <see cref="ComplexType"/> that contains the converted CRM type information.</returns>
        public static ComplexType ComplexTypeConvert(AttributeTypeCode typeToConvert, ObjectDefinition objDef)
        {
            switch (typeToConvert)
            {
                case AttributeTypeCode.Customer:
                case AttributeTypeCode.Lookup:
                case AttributeTypeCode.Owner:
                    return GetComplexTypeForReference(objDef, "EntityReference");
                case AttributeTypeCode.Money:
                    return GetComplexTypeForMoney(objDef);
                case AttributeTypeCode.Picklist:
                case AttributeTypeCode.State:
                    return GetComplexTypeForPicklist(objDef);
                case AttributeTypeCode.PartyList:
                    return GetComplexTypeForPartyList(objDef, typeToConvert.ToString());                
                case AttributeTypeCode.Status:
                    return GetComplexTypeForPicklist(objDef); // Added support for the Status Code field for any entity with a status code setting
                default:
                    return null;
            }
        }

        /// <summary>
        /// Determines if the supplied CRM <c>Entity</c> is address 1 or 2.
        /// </summary>
        /// <param name="childEntity">The CRM <c>customeraddress</c> to be checked.</param>
        /// <returns>True if the <c>customeraddress</c> that was supplied is address 1 or 2.</returns>
        internal static bool IsAddressOneOrTwo(Entity childEntity)
        {
            if (childEntity.Contains("addressnumber"))
            {
                int addressNumber = (int)childEntity["addressnumber"];
                return addressNumber == 1 || addressNumber == 2;
            }

            return false;
        }

        /// <summary>
        /// Determines if an option set is one of the customer address 1 or 2 option sets.
        /// </summary>
        /// <param name="entity">The CRM <c>Entity</c> currently being integrated.</param>
        /// <param name="field">The <see cref="FieldDefinition"/> for the CRM attribute currently being integrated.</param>
        /// <returns>True if the field is one of the customer address 1 or 2 fields, false otherwise.</returns>
        internal static bool IsSpecialAddressPicklist(Entity entity, FieldDefinition field)
        {
            return entity.LogicalName == "customeraddress" && GetSpecialAddressPicklistFields().Contains(field.Name) && entity.Contains("addressnumber") && IsAddressOneOrTwo(entity);
        }

        /// <summary>
        /// Gets a CRM <c>OrganizationRequest</c> that can be used to cancel a <c>contract</c>.
        /// </summary>
        /// <param name="statusToSet">The <c>statuscode</c> to set on the cancel request.</param>
        /// <param name="contractEntityId">The unique id of the <c>contract</c> to be canceled.</param>
        /// <returns>A CRM <c>OrganizationRequest</c> for canceling the supplied <c>contract</c>.</returns>
        internal static OrganizationRequest GetContractCancelRequest(int statusToSet, Guid contractEntityId)
        {
            OrganizationRequest request = new OrganizationRequest("CancelContract") { Parameters = new ParameterCollection() };
            request.Parameters.Add(new KeyValuePair<string, object>("Status", statusToSet));
            request.Parameters.Add(new KeyValuePair<string, object>("CancelDate", DateTime.Now.ToUniversalTime()));
            request.Parameters.Add(new KeyValuePair<string, object>("ContractId", contractEntityId));
            return request;
        }

        /// <summary>
        /// Gets a CRM <c>OrganizationRequest</c> that can be used to close an <c>incident</c>.
        /// </summary>
        /// <param name="statusToSet">The <c>statuscode</c> to set on the close request.</param>
        /// <param name="incidentEntityId">The unique id of the <c>incident</c> to be closed.</param>
        /// <returns>A CRM <c>OrganizationRequest</c> for closing the supplied <c>incident</c>.</returns>
        internal static OrganizationRequest GetIncidentCloseRequest(int statusToSet, Guid incidentEntityId)
        {
            OrganizationRequest request = new OrganizationRequest("CloseIncident") { Parameters = new ParameterCollection() };
            request.Parameters.Add(new KeyValuePair<string, object>("Status", statusToSet));
            request.Parameters.Add(new KeyValuePair<string, object>("IncidentResolution", new EntityReference("incident", incidentEntityId)));
            return request;
        }

        /// <summary>
        /// Gets a CRM <c>OrganizationRequest</c> that can be used to lose an <c>opportunity</c>.
        /// </summary>
        /// <param name="statusToSet">The <c>statuscode</c> to set on the lose request.</param>
        /// <param name="opportunityEntityId">The unique id of the <c>opportunity</c> to be closed.</param>
        /// <returns>A CRM <c>OrganizationRequest</c> for losing the supplied <c>opportunity</c>.</returns>
        internal static OrganizationRequest GetOpportunityLoseRequest(int statusToSet, Guid opportunityEntityId)
        {
            OrganizationRequest request = new OrganizationRequest("LoseOpportunity") { Parameters = new ParameterCollection() };
            request.Parameters.Add(new KeyValuePair<string, object>("Status", statusToSet));
            request.Parameters.Add(new KeyValuePair<string, object>("OpportunityClose", new EntityReference("opportunity", opportunityEntityId)));
            return request;
        }

        /// <summary>
        /// Gets a <see cref="ComplexType"/> for a CRM <c>EntityReference</c>.
        /// </summary>
        /// <param name="objDef">The <see cref="ObjectDefinition"/> to retrieve the CRM <c>EntityReference</c> values from.</param>
        /// <param name="complexTypeName">The name for the returned <see cref="ComplexType"/>.</param>
        /// <returns>A <see cref="ComplexType"/> that contains the values of the attributes from the CRM <c>EntityReference</c>.</returns>
        private static ComplexType GetComplexTypeForReference(ObjectDefinition objDef, string complexTypeName)
        {
            System.Collections.ObjectModel.ObservableCollection<FieldDefinition> fields = new System.Collections.ObjectModel.ObservableCollection<FieldDefinition>();
            fields.Add(GenerateFieldAndAddType(objDef, typeof(string), "name", "Name", false, true));
            fields.Add(GenerateFieldAndAddType(objDef, typeof(string), "type", "Type", false, true));
            fields.Add(GenerateFieldAndAddType(objDef, typeof(Guid), "Value", "ID", false, true));

            var tempType = new ComplexType() { ClrType = typeof(Dictionary<string, object>), Name = complexTypeName };
            tempType.Fields.AddRange(fields);
            return tempType;
        }

        /// <summary>
        /// Produces a <see cref="ComplexType"/> from the <see cref="ObjectDefinition"/> for the CRM option set type.
        /// </summary>
        /// <param name="objDef">The <see cref="ObjectDefinition"/> for the option set.</param>
        /// <returns>A <see cref="ComplexType"/> that can contain a CRM option set instance.</returns>
        private static ComplexType GetComplexTypeForPicklist(ObjectDefinition objDef)
        {
            System.Collections.ObjectModel.ObservableCollection<FieldDefinition> fields = new System.Collections.ObjectModel.ObservableCollection<FieldDefinition>();
            fields.Add(GenerateFieldAndAddType(objDef, typeof(string), "name", "Name", false, false));
            fields.Add(GenerateFieldAndAddType(objDef, typeof(int?), "Value", "Value", false, false));

            var tempType = new ComplexType() { ClrType = typeof(Dictionary<string, object>), Name = "OptionSetValue" };
            tempType.Fields.AddRange(fields);
            return tempType;
        }

        /// <summary>
        /// Produces a <see cref="ComplexType"/> from the <see cref="ObjectDefinition"/> for the CRM status type.
        /// </summary>
        /// <param name="objDef">The <see cref="ObjectDefinition"/> for the status entity.</param>
        /// <returns>A <see cref="ComplexType"/> that can contain a CRM status instance.</returns>
        private static ComplexType GetComplexTypeForStatus(ObjectDefinition objDef)
        {
            System.Collections.ObjectModel.ObservableCollection<FieldDefinition> fields = new System.Collections.ObjectModel.ObservableCollection<FieldDefinition>();
            fields.Add(GenerateFieldAndAddType(objDef, typeof(int?), "Value", "Value", false, false));

            var tempType = new ComplexType() { ClrType = typeof(System.Collections.Generic.Dictionary<string, object>), Name = "Status" };
            tempType.Fields.AddRange(fields);
            return tempType;
        }

        /// <summary>
        /// Produces a <see cref="ComplexType"/> from the <see cref="ObjectDefinition"/> for the CRM <c>Money</c> type.
        /// </summary>
        /// <param name="objDef">The <see cref="ObjectDefinition"/> for the option set.</param>
        /// <returns>A <see cref="ComplexType"/> that can contain a CRM <c>money</c> instance.</returns>
        private static ComplexType GetComplexTypeForMoney(ObjectDefinition objDef)
        {
            System.Collections.ObjectModel.ObservableCollection<FieldDefinition> fields = new System.Collections.ObjectModel.ObservableCollection<FieldDefinition>();
            fields.Add(GenerateFieldAndAddType(objDef, typeof(decimal?), "Value", "Value", false, false));

            var tempType = new ComplexType() { ClrType = typeof(System.Collections.Generic.Dictionary<string, object>), Name = "Money" };
            tempType.Fields.AddRange(fields);
            return tempType;
        }

        private static FieldDefinition GenerateFieldAndAddType(ObjectDefinition objDef, Type typeToGen, string name, string displayName, bool isRequired, bool isReadOnly)
        {
            SimpleType simpleType = new SimpleType() { ClrType = typeToGen, Name = typeToGen.FullName };
            if (objDef.Types.Find(f => f.Name == simpleType.Name) == null)
            {
                objDef.Types.Add(simpleType);
            }

            return new FieldDefinition() { Name = name, TypeDefinition = simpleType, DisplayName = displayName, IsRequired = isRequired, TypeName = simpleType.Name, IsReadOnly = isReadOnly };
        }

        private static int GetDefaultStateCodeValue(string stateName)
        {
            if (stateName.ToUpperInvariant() == InactiveState.ToUpperInvariant())
            {
                return 1;
            }

            return 0;
        }

        private static ConditionOperator GetConditionOperator(BinaryComparisonOperator binaryComparisonOperator)
        {
            switch (binaryComparisonOperator)
            {
                case BinaryComparisonOperator.Equal:
                    return ConditionOperator.Equal;
                case BinaryComparisonOperator.GreaterThan:
                    return ConditionOperator.GreaterThan;
                case BinaryComparisonOperator.GreaterThanOrEqual:
                    return ConditionOperator.GreaterEqual;
                case BinaryComparisonOperator.Contains:
                    return ConditionOperator.In;
                case BinaryComparisonOperator.LessThan:
                    return ConditionOperator.LessThan;
                case BinaryComparisonOperator.LessThanOrEqual:
                    return ConditionOperator.LessEqual;
                case BinaryComparisonOperator.NotEqual:
                    return ConditionOperator.NotEqual;
                default:
                    throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.BinaryComparisonNotSupportedExceptionMessage, Enum.GetName(typeof(BinaryComparisonOperator), binaryComparisonOperator))) { ExceptionId = ErrorCodes.BinaryComparisonNotSupported };
            }
        }

        /// <summary>
        /// Validates query parameters
        /// </summary>
        /// <param name="entityName">The name of the entity to be queried.</param>
        /// <param name="queryProperty">The attribute on the entity to query.</param>
        /// <exception cref="AdapterException">Thrown if either paramter is null or empty.</exception>
        private static void ValidatePropertyQueryParameters(string entityName, string queryProperty)
        {
            if (string.IsNullOrEmpty(entityName))
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.GetQueryExpressionEntityNameExceptionMessage)) { ExceptionId = ErrorCodes.QueryEntityNameEmpty };
            }

            if (string.IsNullOrEmpty(queryProperty))
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.GetQueryExpressionQueryPropertyExceptionMessage)) { ExceptionId = ErrorCodes.QueryPropertyNameEmpty };
            }
        }

        private static ComplexType GetComplexTypeForPartyList(ObjectDefinition objDef, string complexTypeName)
        {
            System.Collections.ObjectModel.ObservableCollection<FieldDefinition> fields = new System.Collections.ObjectModel.ObservableCollection<FieldDefinition>();
            fields.Add(GenerateFieldAndAddType(objDef, typeof(Guid), "activitypartyid", "Activity Party ID", false, true));
            fields.Add(GenerateFieldAndAddType(objDef, typeof(EntityReference), "ownerid", "Owner ID", "EntityReference", false, true));
            fields.Add(GenerateFieldAndAddType(objDef, typeof(OptionSetValue), "participationtypemask", "Participation Type Mask", "OptionSetValue", false, true));
            fields.Add(GenerateFieldAndAddType(objDef, typeof(EntityReference), "activityid", "Activity ID", "EntityReference", false, true));
            fields.Add(GenerateFieldAndAddType(objDef, typeof(bool), "ispartydeleted", "IsPartyDeleted", false, true));
            fields.Add(GenerateFieldAndAddType(objDef, typeof(EntityReference), "partyid", "Party ID", "EntityReference", false, true));
            fields.Add(GenerateFieldAndAddType(objDef, typeof(OptionSetValue), "instancetypecode", "Instance Type Code", "OptionSetValue", false, true));

            var tempType = new ComplexType() { ClrType = typeof(Dictionary<string, object>), Name = complexTypeName };
            tempType.Fields.AddRange(fields);
            return tempType;
        }

        private static FieldDefinition GenerateFieldAndAddType(ObjectDefinition objDef, Type typeToGen, string name, string displayName, string typeName, bool isRequired, bool isReadOnly)
        {
            SimpleType simpleType = new SimpleType() { ClrType = typeToGen, Name = typeToGen.FullName };
            if (objDef.Types.Find(f => f.Name == simpleType.Name) == null)
            {
                objDef.Types.Add(simpleType);
            }

            return new FieldDefinition() { Name = name, DisplayName = displayName, IsReadOnly = isReadOnly, IsRequired = isRequired, TypeName = typeName };
        }
    }
}
