namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm
{
    using AdapterAbstractionLayer;
    using Common;
    using Properties;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.ServiceModel;
    using System.ServiceModel.Security;
    using System.Xml;
    using System.Xml.Serialization;
    using Xrm.Sdk;
    using Xrm.Sdk.Messages;
    using Xrm.Sdk.Metadata;
    using Xrm.Sdk.Query;

    /// <summary>
    /// Provides a base class for creating Dynamics CRM <c>ObjectProvider</c>s.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "Required for proper functioning")]
    public abstract class CrmObjectProvider : ObjectProvider
    {
        private static readonly DateTime MinDateTime = new DateTime(1900, 1, 1);
        private List<FieldDefinition> collectionFields;
        private object syncObject = new object();        

        /// <summary>
        /// The default protected base constructor for creating a new instance of this class.
        /// </summary>
        protected CrmObjectProvider()
        {
            this.KeySerializer = new DelegateKeySerializer(
                (string key) => 
                {
                    if (!key.IsNullOrEmptyTrim())
                    {
                        try
                        {
                            return new Guid(key);
                        }
                        // the supplied key is not a Guid, so it will be returned as a string instead.
                        catch (FormatException)
                        {
                            return key;
                        }
                    }
                    throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.SuppliedKeyTypeExceptionMessage), new ArgumentException(Resources.SuppliedKeyTypeExceptionMessage, "key")) { ExceptionId = ErrorCodes.SuppliedKeyCastException };
                });
        }

        /// <summary>
        /// Gets this object provider's <c>Adapter</c> as a <c>CRMAdapter</c>.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1065:DoNotRaiseExceptionsInUnexpectedLocations")]
        protected DynamicCrmAdapter CrmAdapter
        {
            get
            {
                if (this.Adapter as DynamicCrmAdapter == null)
                {
                    throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidCRMAdapterCastException)) { ExceptionId = ErrorCodes.AdapterCast };
                }

                return this.Adapter as DynamicCrmAdapter;
            }
        }

        /// <summary>
        /// Gets or sets the logical name of the entity that this <c>ObjectProvider</c> provides.
        /// </summary>
        protected string ProvidedEntityName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating if this object provider allows for duplicate detection logic.
        /// </summary>
        protected bool DoesDetectDuplicates
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating if this object provider is for an activity entity.
        /// </summary>
        protected bool IsActivityEntity
        {
            get;
            set;
        }
        
        /// <summary>
        /// Gets a <c>List</c> of the <c>CollectionType</c>s in this provider's configuration file.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists")]
        protected List<FieldDefinition> CollectionFields
        {
            get
            {
                if (this.collectionFields == null)
                {
                    lock (this.syncObject)
                    {
                        if (this.collectionFields == null)
                        {
                            this.InitCollectionFields();
                        }
                    }
                }

                return this.collectionFields;
            }
        }


        internal string KeyAttribute
        {
            get
            {
                return this.IsActivityEntity != true ? this.ProvidedEntityName + "id" : "activityid";
            }
        }
        

        /// <summary>
        /// Gets this <c>ObjectProvider</c>'s configuration file as an <c>ObjectDefinitionConfiguration</c> <c>object</c>.
        /// </summary>
        /// <returns>An <c>ObjectDefinitionConfiguration</c> <c>object</c>.</returns>
        /// <exception cref="ConfigurationErrorsException">Thrown if the configuration file does not exist in the location searched.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
        protected override ObjectDefinition GetObjectDefinitionInternal()
        {
            if (this.Adapter != null && !string.IsNullOrEmpty(this.CrmAdapter.OrganizationName))
            {
                var filePath = Path.Combine(this.CrmAdapter.GetConfigPath<DynamicCrmAdapter>(), this.Name + ".config");
                ObjectDefinition def = new ObjectDefinition();
                try
                {
                    using (var fs = File.OpenRead(filePath))
                    {
                        using (var xr = XmlReader.Create(fs))
                        {
                            var serializer = new XmlSerializer(typeof(ObjectDefinition));
                            def = (ObjectDefinition)serializer.Deserialize(xr);
                        }
                    }
                }
                catch (FileNotFoundException)
                {
                    //"CrmServerConfiguration file for the " + this.Name + " is not present in the " + filePath + " directory."
                    throw new ConfigurationErrorsException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidObjectProviderConfigPath, new object[] { this.Name, filePath }));
                }

                return def;
            }

            return base.GetObjectDefinitionInternal();
        }

        /// <summary>
        /// Gets an instance of the <c>DynamicsEntity</c> class that conatins either an instance of an object that 
        /// exists in the system or a new instance of one if the requested key is not present in the system.
        /// </summary>
        /// <param name="queryProperty">The property on the <c>entity</c> to be querried</param>
        /// <param name="dictionary">The <c>Dictionary</c> that contains the data from the source system</param>
        /// <param name="entityName">The logical name of the entity type to get</param>
        /// <returns>A <c>Entity</c> that conatins a new or existing instance of a <c>BusinessEntity</c></returns>
        internal virtual Entity GetDynamicInstance(string queryProperty, Dictionary<string, object> dictionary, string entityName)
        {
            if (dictionary.ContainsKey(this.KeyAttribute))
            {
                return this.GetDynamicInstance(this.KeyAttribute, ((Guid)dictionary[this.KeyAttribute]).ToString(), entityName);
            }

            return this.GetDynamicInstance(queryProperty, (string)dictionary[queryProperty], entityName);
        }

        /// <summary>
        /// Gets an instance of the <c>DynamicsEntity</c> class that conatins either an instance of an object that 
        /// exists in the system or a new instance of one if the requested key is not present in the system.
        /// </summary>
        /// <param name="queryProperty">The property on the <c>entity</c> to be querried</param>
        /// <param name="queryValue">The key to reference or to use as a new key if the system does not contain the key supplied.</param>
        /// <param name="entityName">The logical name of the entity type to get</param>
        /// <param name="attributes">The attributes to be returned on the <c>Entity</c> instance</param>
        /// <returns>A <c>Entity</c> that conatins a new or existing instance of a <c>BusinessEntity</c></returns>
        internal Entity GetDynamicInstance(string queryProperty, string queryValue, string entityName, params string[] attributes)
        {
            RetrieveMultipleResponse retrieveResponse = this.RetrieveMultipleDynamicEntities(queryValue, queryProperty, entityName, attributes);
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
                returnedEntity = this.GetNewEntityInstance(queryProperty, queryValue, entityName);
                returnedEntity[CRM2011AdapterUtilities.IsNew] = true;
                return returnedEntity;
            }
            else
            {
                throw new AdapterException(
                    string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.MultipleDynamicEntitiesReturnedExceptionMessage,
                    entityName,
                    queryProperty,
                    queryValue))
                {
                    ExceptionId = ErrorCodes.MultipleEntityResult
                };
            }
        }

        /// <summary>
        /// Gets an instance of the <c>DynamicsEntity</c> class that conatins either an instance of an object that 
        /// exists in the system or a new instance of one if the requested key is not present in the system.
        /// </summary>
        /// <param name="queryProperty">The property on the <c>entity</c> to be querried</param>
        /// <param name="dictionary">The <c>Dictionary</c> that contains the data from the source system</param>
        /// <param name="entityName">The logical name of the entity type to get</param>
        /// <param name="criterion">An instance of an <c>ICriterion</c> to use in duplicate detection</param>
        /// <returns>A <c>Entity</c> that conatins a new or existing instance of a <c>BusinessEntity</c></returns>
        internal Entity GetDynamicInstance(string queryProperty, Dictionary<string, object> dictionary, string entityName, ICriterion criterion)
        {
            Entity returnedEntity = this.GetDynamicInstance(queryProperty, dictionary, entityName);
            List<Entity> match = this.FindEntities(criterion) as List<Entity>;
            if (match.Count > 1)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.DuplicateDetected, this.ProvidedEntityName)) { ExceptionId = ErrorCodes.DuplicateDetected };
            }

            // this means the entity does not have an integration key yet and an entity matching the criterion was found 
            if ((bool)returnedEntity[CRM2011AdapterUtilities.IsNew] == true && match.Count == 1)
            {
                returnedEntity = match[0];
                returnedEntity[CRM2011AdapterUtilities.IsNew] = false;
            }

            return returnedEntity;
        }

        /// <summary>
        /// Assigns an object to be the value for a property on a <c>Entity</c>.
        /// </summary>
        /// <param name="reference">The <c>object</c> be assigned as the value</param>
        /// <param name="entity">The <c>Entity</c> to be aissgned to</param>
        /// <param name="propertyToBeAssignedValue">The name of the property on the <c>Entity</c> to assign the supplied object to</param>
        /// <remarks>If the <c>object</c> is null, nothing is assigned to the property</remarks>
        protected static void AssignReferencePropertyValue(EntityReference reference, Entity entity, string propertyToBeAssignedValue)
        {
            if (entity == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage), new ArgumentNullException("entity")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            // These checks are only for updates
            if (entity.Contains(CRM2011AdapterUtilities.IsNew) && !(bool)entity[CRM2011AdapterUtilities.IsNew])
            {
                if (entity.Contains(propertyToBeAssignedValue))
                {                    
                    if (reference == null)
                    {
                        // Since the reference entity supplied is null, remove the property from the retrieved entity to avoid a potential update storm
                        entity.Attributes.Remove(propertyToBeAssignedValue);
                    }
                    else if (((EntityReference)entity[propertyToBeAssignedValue]).Id == reference.Id)
                    {
                        // Since this property has the same value we are trying to assign it, remove it to avoid a potential update storm
                        entity.Attributes.Remove(propertyToBeAssignedValue);
                    }
                    else
                    {
                        entity[propertyToBeAssignedValue] = reference;
                    }

                    return;
                }
            }

            // This is a new instance or the existing instance did not contain this property when it was retrieved
            if (reference != null)
            {
                entity[propertyToBeAssignedValue] = reference;
            }
        }

        /// <summary>
        /// Get a new instance of a <c>Entity</c> with a property initialized.
        /// </summary>
        /// <param name="propertyName">The name of the property to initialize.</param>
        /// <param name="propertyValue">The value to set the property to be intitialized to.</param>
        /// <param name="entityName">The logical name of the entity type to create.</param>
        /// <returns>A new instance of the <c>Entity</c> class that has one property initialized to the provided value.</returns> 
        protected virtual Entity GetNewEntityInstance(string propertyName, string propertyValue, string entityName)
        {
            Entity returnedEntity = new Entity(entityName);
            if (propertyName == entityName + "id" || propertyName == "activityid")
            {
                returnedEntity.Id = new Guid(propertyValue);
            }
            else
            {
                returnedEntity[propertyName] = propertyValue;
            }
            return returnedEntity;
        }

        /// <summary>
        /// Sets all of the properties on the <c>Entity</c> to the values listed in the <c>Dictionary</c>.
        /// </summary>
        /// <param name="dictionary">The <c>Dictionary</c> that conatins the properties to be set (as keys) and the data to set those properties to (as objects).</param>
        /// <param name="entity">The <c>Entity</c> to set the <c>Dictionary</c> values on.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Complexity here is unavoidable")]
        protected void SetProperties(Dictionary<string, object> dictionary, Entity entity)
        {
            if (entity == null || string.IsNullOrEmpty(entity.LogicalName) || dictionary == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidEntitySupplied)) { ExceptionId = ErrorCodes.EmptyEntityName };
            }

            // Loop over all of the properties defined in the Dictionary and match them to the properties in this provider's ObjectDefinition
            foreach (string propertyName in dictionary.Keys)
            {
                ComplexType ct = this.GetComplexTypeFromEntity(entity);
                var field = ct.Fields.FirstOrDefault(f => f.Name == propertyName);
                if (field == null)
                {
                    TraceLog.Error(string.Format(CultureInfo.CurrentCulture, Resources.InvalidPropertySupplied, propertyName, entity.LogicalName));
                }
                else
                {
                    if (field.TypeDefinition is ComplexType)
                    {
                        // This is the dictionary that holds the data for the complex type and is mapped as the value for a property key in the provided dictionary
                        Dictionary<string, object> mappedValue = dictionary[propertyName] as Dictionary<string, object>;
                        if (mappedValue != null)
                        {
                            switch (field.TypeDefinition.Name)
                            {
                                case "EntityReference":
                                    AssignReferencePropertyValue(this.MapLookup(dictionary, entity, propertyName, ct, field, mappedValue), entity, propertyName);
                                    break;
                                case "PartyList":
                                    var parties = this.MapActivityPartyReferences(dictionary, entity, propertyName, field);
                                    if (parties != null)
                                    {
                                        entity[propertyName] = parties.Entities.ToArray();
                                    }
                                    else
                                    {
                                        entity.Attributes.Remove(propertyName);
                                    }
                                    break;
                                case "Money":
                                    entity[propertyName] = CRM2011AdapterUtilities.MapCrmMoney(mappedValue);
                                    break;
                                case "OptionSetValue":
                                    OptionSetValue pickList = CRM2011AdapterUtilities.MapPicklist(entity, field, mappedValue, this.CrmAdapter, this.ProvidedEntityName);
                                    if (pickList != null)
                                    {
                                        entity[propertyName] = pickList;
                                    }
                                    else
                                    {
                                        entity.Attributes.Remove(propertyName);
                                    }
                                    break;
                                default:
                                    throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.UnsupportedMappedObjectExceptionMessage, field.TypeDefinition.Name)) { ExceptionId = ErrorCodes.CrmTypeNotSupportedException };
                            }
                        }
                    }
                    else if (field.Name == "statecode" && dictionary[propertyName] != null)
                    {
                        CheckStateAndStatus(dictionary, entity, propertyName, this.CrmAdapter);
                    }
                    else if (!(field.TypeDefinition is CollectionType))
                    {
                        entity[propertyName] = dictionary[propertyName];
                    }
                }
            }

            RemoveProperties(entity, dictionary);
        }

        protected EntityCollection MapActivityPartyReferences(Dictionary<string, object> dictionary, Entity entity, string propertyName, FieldDefinition field)
        {
            if (field == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage), new ArgumentNullException("field")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            var partyEntityCollection = dictionary[propertyName] as Dictionary<string, object>;
            var entities = partyEntityCollection["ActivityParties"] as Dictionary<string, object>;

            if (entities.Count > 0)
            {
                var tempEntityCollection = new EntityCollection();

                foreach (var partyEntity in entities)
                {
                    Entity tempEntity = new Entity("activityparty");
                    var attributes = partyEntity.Value as Dictionary<string, object>;

                    foreach (var attribute in attributes)
                    {
                        var fieldDef = field.TypeDefinition.Children.FirstOrDefault<FieldDefinition>(x => x.Name == attribute.Key);
                        if (fieldDef != null)
                        {
                            if (fieldDef.TypeDefinition.Name == "EntityReference")
                            {
                                var mappedValue = attribute.Value as Dictionary<string, object>;
                                var entityReference = MapEntityReference(fieldDef, mappedValue);

                                if (entityReference != null)
                                {
                                    tempEntity.Attributes.Add(new KeyValuePair<string, object>(attribute.Key, entityReference));
                                }
                                else
                                {
                                    throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage), new ArgumentNullException("activityparty")) { ExceptionId = AdapterException.SystemExceptionGuid };
                                }
                            }
                            else if (fieldDef.TypeDefinition.Name == "OptionSetValue")
                            {
                                var mappedValue = attribute.Value as Dictionary<string, object>;
                                OptionSetValue pickList = CRM2011AdapterUtilities.MapPicklist(entity, fieldDef, mappedValue, this.CrmAdapter, this.ProvidedEntityName);
                                if (pickList != null)
                                {
                                    entity[propertyName] = pickList;
                                }
                                else
                                {
                                    throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage), new ArgumentNullException("activityparty")) { ExceptionId = AdapterException.SystemExceptionGuid };
                                }
                            }
                            else
                            {
                                tempEntity.Attributes.Add(attribute);
                            }
                        }
                    }
                    tempEntityCollection.Entities.Add(tempEntity);
                }

                return tempEntityCollection;
            }
            return null;
        }
  

        /// <summary>
        /// Gets a new <c>Lookup</c> object that represents an instance of an entity within the target system.
        /// </summary>
        /// <param name="dictionary">The <c>Dictionary</c> that contains the data to be used in mapping this entity.</param>
        /// <param name="entity">The <c>Entity</c> that is currently being mapped.</param>
        /// <param name="propertyName">The name of the current property from the <c>ObjectDefinition</c>.</param>
        /// <param name="ct">An instance of a <c>ComplexType</c> for this field.</param>
        /// <param name="field">The field that is currently being set to a <c>Lookup</c>.</param>
        /// <param name="mappedValue">The <c>Dictionary</c> that contains the data for populating the returned <c>Lookup</c>.</param>
        /// <returns>A new instance of a <c>Lookup</c> object initialized with the proper values from the target system.</returns> 
        /// <remarks>The overload can be used to map properties in a specific order.</remarks>
        /// <example>Parent child relationships might need to have the parent mapped first, or contained entities might need their container
        /// to be mapped first.</example>
        protected virtual EntityReference MapLookup(Dictionary<string, object> dictionary, Entity entity, string propertyName, ComplexType ct, FieldDefinition field, Dictionary<string, object> mappedValue)
        {
            return this.MapEntityReference(field, mappedValue);
        }

        /// <summary>
        /// Sets the type property on a given <c>Lookup</c> instance.
        /// </summary>
        /// <param name="field">The field who's name will determine that type of the <c>Lookup</c>.</param>
        /// <param name="returnedLookup">The <c>Lookup</c> to set the type property on.</param> 
        protected virtual void SetLookupType(FieldDefinition field, EntityReference returnedLookup)
        {
            if(field == null || returnedLookup == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage)) { ExceptionId = AdapterException.SystemExceptionGuid };
            }
            
            returnedLookup.LogicalName = field.FindAttribute(CRM2011AdapterUtilities.LookupType).Value;
        }

        /// <summary>
        /// Gets the SetState request message for this entity.
        /// </summary>
        /// <param name="stateToSet">An <c>int</c> that represents the <c>statecode</c> to set.</param>
        /// <param name="statusToSet">An <c>int</c> that represents the <c>Ststus</c> to set.</param>
        /// <param name="entityId">The <c>Guid</c> the refers to this instance.</param>
        /// <returns>A <c>Request</c> to set the state of this instance.</returns>
        protected virtual OrganizationRequest GetSetStateRequest(int stateToSet, int statusToSet, Guid entityId)
        {
            OrganizationRequest returnedRequest = new OrganizationRequest("SetState");
            returnedRequest.Parameters["Status"] = new OptionSetValue(statusToSet);
            returnedRequest.Parameters["EntityMoniker"] = new EntityReference(this.ProvidedEntityName, entityId);
            returnedRequest.Parameters["State"] = new OptionSetValue(stateToSet);
            return returnedRequest;
        }

        /// <summary>
        /// Issues an <c>UpdateRequest</c> to the target <c>CrmService</c>.
        /// </summary>
        /// <param name="entity">The <c>Entity</c> to be updated.</param> 
        protected void UpdateEntity(Entity entity)
        {
            this.PrepEntityForUpdate(entity);
            UpdateRequest request = new UpdateRequest() { Target = entity };
            this.CallCrmExecuteWebMethod(request);            
        }


        // TODO: Double check and remove if not needed.
        /// <summary>
        /// Issues an <c>UpdateCompoundRequest</c> to the target <c>CrmService</c>.
        /// </summary>
        /// <param name="entity">The <c>Entity</c> to be created.</param> 
        /// <param name="children">The <c>Array</c> of <c>Entity</c> objects that contains the children.</param>
        protected void UpdateCompoundEntity(Entity entity, Entity[] children)
        {
            if (entity == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage)) { ExceptionId = AdapterException.SystemExceptionGuid };
            }
            
            this.PrepEntityForUpdate(entity);
            Relationship linesRelationship = new Relationship((entity.LogicalName == "salesorder" ? "order" : entity.LogicalName) + "_details");
            CreateNewLines(children);
            entity.RelatedEntities.Add(linesRelationship, new EntityCollection(children));
            UpdateRequest request = new UpdateRequest() { Target = entity };
            this.CallCrmExecuteWebMethod(request);
        }

        private static void CreateNewLines(Entity[] children)
        {
            var newLines = from newLine in children where newLine.Id == Guid.Empty select newLine;
            if (newLines != null && newLines.Count() > 0)
            {
                foreach (Entity line in newLines)
                {
                    line.EntityState = EntityState.Created;
                }
            }
        }

        /// <summary>
        /// Issues a <c>CreateRequest</c> to the target <c>CrmService</c>.
        /// </summary>
        /// <param name="entity">The <c>Entity</c> to be created.</param> 
        protected void CreateNewEntity(Entity entity)
        {
            if (entity == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage), new ArgumentNullException("entity")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }
            
            entity.Attributes.Remove(CRM2011AdapterUtilities.IsNew);
            OptionSetValue state = null;
            OptionSetValue status = null;

            state = RemoveStateCode(entity);
            if (state != null)
            {
                status = RemoveStatusCode(entity);
            }
            

            CreateRequest request = new CreateRequest() { Target = entity };
            if(this.DoesDetectDuplicates == true)
            {
                request["SuppressDuplicateDetection"] = false;
            }

            
            try
            {
                PrepEntityForCreate(entity);
                this.CallCrmExecuteWebMethod(request);
                this.ApplyStateAndStatus(entity, state, status);
            }
            catch (AdapterException ex)
            {
                if (ex.ExceptionId == ErrorCodes.DuplicateDetected)
                {
                    entity = this.DetectDuplicates(request);
                }
                else
                {
                    throw;
                }
            }                     
        }

        /// <summary>
        /// Issues a <c>CompundCreateRequest</c> to the target <c>CrmService</c>.
        /// </summary>
        /// <param name="entity">The <c>Entity</c> to be created.</param> 
        /// <param name="children">The <c>Array</c> of <c>Entity</c> objects that contains the children.</param>
        protected void CreateNewCompoundEntity(Entity entity, Entity[] children)
        {
            if (entity == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage), new ArgumentNullException("entity")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }
            
            entity.Attributes.Remove(CRM2011AdapterUtilities.IsNew);
            OptionSetValue state = null;
            OptionSetValue status = null;

            state = RemoveStateCode(entity);
            if (state != null)
            {
                status = RemoveStatusCode(entity);
            }

            entity.RelatedEntities.Add(new Relationship((entity.LogicalName == "salesorder" ? "order" : entity.LogicalName) + "_details"), new EntityCollection(children.ToList()));
            CreateRequest request = new CreateRequest() { Target = entity };
            if (this.DoesDetectDuplicates == true)
            {
                request["SuppressDuplicateDetection"] = false;
            }

            PrepEntityForCreate(entity);
            this.CallCrmExecuteWebMethod(request);
            this.ApplyStateAndStatus(entity, state, status);
        }

        /// <summary>
        /// Builds up the <c>ICollection</c> of <c>FieldInstances</c> that are used in this object's criteria.
        /// </summary>
        /// <returns>An <c>ICollection</c> of <c>FieldInstance</c>s to be used in duplicate detection for this object.</returns>
        protected ICollection<FieldInstance> BuildCriteriaFields()
        {
            List<FieldInstance> fields = new List<FieldInstance>();
            foreach (FieldInstance field in this.ObjectDefinition.RootInstance.Children)
            {
                var temp = field.Definition.FindAttribute(CRM2011AdapterUtilities.IsCriteria);
                if (temp != null)
                {
                    if (field.Definition.AdditionalAttributes.First().Value.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        fields.Add(field);
                    }
                }
            }

            return fields;
        }        

        /// <summary>
        /// Issues an <c>DeleteRequest</c> to the target <c>CrmService</c>.
        /// </summary>
        /// <param name="value">The dictionary that conatins the entity to be deleted.</param>
        /// <param name="keyProperty">The property on the entity that holds the entity's key.</param>
        protected void DeleteEntity(object value, string keyProperty)
        {
            string dictionary = value as string;

            if (dictionary == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidDictionaryCastException)) { ExceptionId = ErrorCodes.InvalidDictionaryCast };
            }

            Entity deletedEntity = this.GetDynamicInstanceToDelete(dictionary, keyProperty, this.ProvidedEntityName);
            if (deletedEntity != null)
            {
                DeleteRequest request = new DeleteRequest() { Target = new EntityReference(deletedEntity.LogicalName, deletedEntity.Id) };
                this.CallCrmExecuteWebMethod(request);
            }
        }

        /// <summary>
        /// Calls the target <c>CrmService</c>.
        /// </summary>
        /// <param name="request">The <c>Request</c> object to use when calling the target <c>CrmService</c>.</param>
        /// <returns>A <c>Response</c> object.</returns>
        /// <exception cref="AdapterException">Thrown if there is a <c>SoapException</c> thrown during the call to the web service.</exception>
        protected OrganizationResponse CallCrmExecuteWebMethod(OrganizationRequest request)
        {
            if (request == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.NullRequestExceptionMessage)) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            try
            {
                return this.CrmAdapter.OrganizationService.Execute(request);
            }
            catch (MessageSecurityException ex)
            {
                // Null out this adapter's org service instance to retreive a new Security Context Token, and throw the error in order to get this record into retry
                this.CrmAdapter.ResetOrgService();
                throw new AdapterException(ex.Message, ex) { ExceptionId = ErrorCodes.SecurityTokenException };
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                if (request is UpdateRequest)
                {
                    throw new AdapterException(ex.Message, ex) { ExceptionId = ErrorCodes.UpdateRequestException };
                }
                else if (request is CreateRequest)
                {
                    if (ex.Detail != null && (ex.Detail.ErrorCode == -2147220937 || ex.Detail.ErrorCode == -2147220685))
                    {
                        throw new AdapterException(ex.Message, ex) { ExceptionId = ErrorCodes.DuplicateDetected };
                    }
                    else if (ex.Detail != null && ex.Detail.ErrorCode == -2147204304)
                    {
                        throw new AdapterException(ex.Message, ex) { ExceptionId = ErrorCodes.NegativeValueSupplied };
                    }
                    else
                    {
                        throw new AdapterException(ex.Message, ex) { ExceptionId = ErrorCodes.CreateRequestException };
                    }
                }
                else if (request is DeleteRequest)
                {
                    throw new AdapterException(ex.Message, ex) { ExceptionId = ErrorCodes.DeleteRequestException };
                }
                else if (request.RequestName.Equals("CancelSalesOrder", StringComparison.OrdinalIgnoreCase))
                {
                    throw new AdapterException(ex.Message, ex) { ExceptionId = ErrorCodes.CancelOrderRequestException };
                }
                else if (request.RequestName.Equals("Assign", StringComparison.OrdinalIgnoreCase))
                {
                    throw new AdapterException(ex.Message, ex) { ExceptionId = ErrorCodes.AssignOwnerRequestException };
                }

                throw new AdapterException(ex.Message, ex) { ExceptionId = ErrorCodes.CrmPlatformException };
            }
        }

        /// <summary>
        /// Calls the target <c>MetadataService</c>.
        /// </summary>
        /// <param name="request">The <c>Request</c> object to use when calling the target <c>MetadataService</c>.</param>
        /// <returns>A <c>Response</c> object.</returns>
        /// <exception cref="AdapterException">Thrown if there is a <c>SoapException</c> thrown during the call to the web service.</exception>
        protected OrganizationResponse CallMetadataExecuteWebMethod(OrganizationRequest request)
        {
            try
            {
                return this.CrmAdapter.OrganizationService.Execute(request);
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                if (request is RetrieveAllEntitiesRequest)
                {
                    throw new AdapterException(ex.Message, ex) { ExceptionId = ErrorCodes.RetrieveAllException };
                }
                else if (request is RetrieveEntityRequest)
                {
                    throw new AdapterException(ex.Message, ex) { ExceptionId = ErrorCodes.RetrieveEntityException };
                }

                throw new AdapterException(ex.Message, ex) { ExceptionId = ErrorCodes.CrmPlatformException };
            }
        }

        private Entity DetectDuplicates(OrganizationRequest request)
        {
            Entity target = ((CreateRequest)request).Target;
            OrganizationRequest dupRequest = new OrganizationRequest("RetrieveDuplicates") { Parameters = new ParameterCollection() };
            dupRequest.Parameters.Add("BusinessEntity", target);
            dupRequest.Parameters.Add("MatchingEntityName", target.LogicalName);
            dupRequest.Parameters.Add("PagingInfo", new PagingInfo() { Count = 2, PageNumber = 1 });
            OrganizationResponse response = this.CallCrmExecuteWebMethod(dupRequest);
            if (((EntityCollection)response.Results["DuplicateCollection"]).Entities.Count == 1)
            {
                Entity entityToUpdate = ((EntityCollection)response.Results["DuplicateCollection"]).Entities[0];
                foreach (string prop in target.Attributes.Keys)
                {
                    if(prop != this.KeyAttribute)
                    {
                        entityToUpdate[prop] = target[prop];
                    }
                }

                this.UpdateEntity(entityToUpdate);
                return entityToUpdate;
            }
            else
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.DuplicateDetected, this.ProvidedEntityName)) { ExceptionId = ErrorCodes.DuplicateDetected };
            }
        }

        /// <summary>
        /// Sets the statecode for this instance.
        /// </summary>
        /// <param name="entity">The <c>Entity</c> to set the statecode for.</param>
        protected void SetState(Entity entity)
        {
            if (entity != null && entity.Contains("statecode"))
            {
                if (!entity.Contains("statuscode"))
                {
                    entity["statuscode"] = new OptionSetValue(-1);
                }

                OrganizationRequest request = this.GetSetStateRequest(((OptionSetValue)entity["statecode"]).Value, ((OptionSetValue)entity["statuscode"]).Value, entity.Id);
                if (request != null)
                {
                    try
                    {
                        this.CallCrmExecuteWebMethod(request);
                    }
                    catch (AdapterException ae)
                    {
                        if (ae.ExceptionId == ErrorCodes.CrmPlatformException)
                        {
                            ae.ExceptionId = ErrorCodes.StateSettingError;
                        }

                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Gets a new <c>EntityReference</c> object that represents an instance of an entity within the target system.
        /// </summary>
        /// <param name="field">The field that is currently being set to a <c>EntityReference</c>.</param>
        /// <param name="mappedLookupObject">The <c>Dictionary</c> that contains the data for populating the returned <c>EntityReference</c>.</param>
        /// <typeparam name="T">Any of the <c>EntityReference</c> implementation types.</typeparam>
        /// <returns>A new instance of a <c>EntityReference</c> object initialized with the proper values from the target system or null
        /// if the dynamics_integrationkey in the supplied <c>Dictionary</c> is null or empty.</returns> 
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "1")]
        protected EntityReference MapEntityReference(FieldDefinition field, Dictionary<string, object> mappedLookupObject)
        {
            if (field == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage), new ArgumentNullException("field")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }
            
            CRM2011AdapterUtilities.ValidateDictionary(mappedLookupObject);
            EntityReference reference = GetReferenceInstanceType(field);
            
            var lookupField = field.AdditionalAttributes.FirstOrDefault(x => x.Name == "LookupField");
            var lookupEntity = field.AdditionalAttributes.FirstOrDefault(x => x.Name == "LookupType");

            var typeSplit = lookupEntity.Value.Split(',');
            var fieldSplit = lookupField.Value.Split(',');
            var typeFieldList = new List<KeyValuePair<string, string>>();

            if (typeSplit.Count() > 1 && fieldSplit.Count() > 1)
            {
                for(int i=0; i<typeSplit.Count(); i++)
                {
                    typeFieldList.Add(new KeyValuePair<string, string>(typeSplit[i], fieldSplit[i]));
                }

                lookupEntity.Value = mappedLookupObject["LogicalName"].ToString();
                lookupField.Value = typeFieldList.FirstOrDefault(x => x.Key == lookupEntity.Value).Value;
            }

            if (lookupField != null && lookupEntity != null)
            {
                var entityCollection = RetrieveEntityReferenceValue(field, lookupEntity, lookupField, mappedLookupObject);
                if (entityCollection != null)
                {
                    if (entityCollection.Entities.Count != 0)
                    {
                        var integrationKeyValue = entityCollection.Entities.First().Id;

                        if (integrationKeyValue != Guid.Empty && integrationKeyValue != null)
                        {
                            return new EntityReference(lookupEntity.Value, integrationKeyValue);
                        }
                    }
                }
            }
            else
            {
                CRM2011AdapterUtilities.SetRelationshipValuesFromDictionary(mappedLookupObject, reference);
            }

            if (reference.Id == Guid.Empty)
            {
                if (field.Name.Contains("pricelevelid"))
                {
                    reference = new EntityReference("pricelevel", (Guid)this.GetBaseCurrencyPriceLevel()["pricelevelid"]);
                    return reference;
                }

                if (field.Name == "transactioncurrencyid")
                {
                    reference = new EntityReference("transactioncurrency", this.CrmAdapter.BaseCurrencyId);
                    return reference;
                }

                return null;
            }

            if (reference.Id == Guid.Empty)
            {
                return null;
            }

            return reference;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1059:MembersShouldNotExposeCertainConcreteTypes", MessageId = "System.Xml.XmlNode"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "3"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "2"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "1"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0")]
        protected EntityCollection RetrieveEntityReferenceValue(FieldDefinition field, XmlAttribute lookupName, XmlAttribute lookupFields, Dictionary<string,object> dictionary)
        {
            if (field.Name.Equals("attachmentid"))
                return null;

            if(field.Name.Equals("ownerid"))
            {
                if (dictionary["LogicalName"].ToString() == "team")
                {
                    lookupName.Value = "team";
                    lookupFields.Value = "teamid";
                }
                if (dictionary["LogicalName"].ToString() == "systemuser")
                {
                    lookupName.Value = "systemuser";
                    lookupFields.Value = "fullname";
                }
            }


            RetrieveMultipleRequest retrieveRequest = new RetrieveMultipleRequest();

            if (lookupName.Value == "activitymimeattachment" && field.Name == "objectid")
                return null;
            //retrieveRequest.Query = CRM2011AdapterUtilities.GetQueryExpression("email", "activityid", dictionary["activityid"].ToString(), new string[] { "activityid" });
            else if (dictionary.ContainsKey(lookupFields.Value))
                retrieveRequest.Query = CRM2011AdapterUtilities.GetQueryExpression(lookupName.Value, lookupFields.Value, dictionary[lookupFields.Value].ToString(), new string[] { lookupFields.Value });
            else
                return null;

            var returned =  (RetrieveMultipleResponse)this.CallCrmExecuteWebMethod(retrieveRequest);

            return returned.EntityCollection;
            
        }

        /// <summary>
        /// Writes an <c>object</c> to the stream.
        /// </summary>
        /// <param name="value">The object to be written.</param>
        /// <exception cref="InvalidCastException">Thrown if the object to be written is not of the correct type for this provider.</exception>
        /// <remarks>The entity's primaryKey property is set to a new random <c>Guid</c> which is then passed into the CreateUpdateChildren() method.  This
        /// overload uses the DynamicsIntegrationKey constant as the property to be querried.</remarks>
        protected void WriteParentEntity(object value)
        {
            this.WriteParentEntity(value, this.KeyAttribute, null);            
        }

        /// <summary>
        /// Writes an <c>object</c> to the stream
        /// </summary>
        /// <param name="value">The <c>object</c> to be written</param>
        /// <param name="queryProperty">The property to be querried to discover if this is a new instance or an existing one.</param>
        /// <param name="criterion">The <c>ICriterion</c> to use in duplicate detection, or <c>null</c> to not use duplicate detection.</param>
        /// <exception cref="AdapterException">Thrown if the <c>object</c> to be written is not of the <c>Dictionary</c> of <c>string</c><c>object</c> type</exception>
        /// <remarks>The entity's primaryKey property is set to a new random <c>Guid</c> which is then passed into the CreateUpdateChildren() method.</remarks>
        protected void WriteParentEntity(object value, string queryProperty, ICriterion criterion)
        {
            Dictionary<string, object> dictionary = value as Dictionary<string, object>;    

            if (dictionary == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidDictionaryCastException)) { ExceptionId = ErrorCodes.InvalidDictionaryCast };
            }
            
            Entity entity = criterion == null ? this.GetDynamicInstance(queryProperty, dictionary, this.ProvidedEntityName) : this.GetDynamicInstance(queryProperty, dictionary, this.ProvidedEntityName, criterion);
            this.SetProperties(dictionary, entity);
            Guid parentKey = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;

            if ((bool)entity[CRM2011AdapterUtilities.IsNew] == true)
            {
                entity[this.KeyAttribute]= parentKey;
				entity.Id = parentKey;
                this.CreateNewEntity(entity);
                parentKey = entity.Id;

            }
            else
            {
                parentKey = entity.Id;
                this.UpdateEntity(entity);
            }

            if (this.CollectionFields != null)
            {
                this.CollectionFields.ForEach(cf => this.CreateUpdateChildEntities(dictionary, parentKey, cf));
            }
        }

        /// <summary>
        /// Used to actually create instances of the child entities in an inherited class.
        /// </summary>
        /// <param name="parentKey">The <c>Key</c> of the parent entity.</param>
        /// <param name="childEntity">The child entity to be created in the form of a <c>Entity</c>.</param>
        /// <param name="collectionFieldName">The name of the field in the <c>ObjectProvider</c>'s configuration file that is being mapped currently.</param>
        protected virtual void CreateUpdateChildInstanceForField(Guid parentKey, Entity childEntity, string collectionFieldName)
        {
            if (parentKey == null || childEntity == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage)) { ExceptionId = AdapterException.SystemExceptionGuid };
            }
            
            if (!childEntity.Contains(childEntity.LogicalName + "id"))
            {
                // Set a default value of parnet entity name + id
                string parentAttribName = this.ProvidedEntityName + "id";

                // Select the child entity's type def
                TypeDefinition childType = this.ObjectDefinition.Types.SingleOrDefault(td => td.Name == childEntity.LogicalName);
                if (childType != null)
                {
                    // Query to limit the number of fields on the child type we iterate over when looking for the parent field
                    var childFields = from childField in childType.Children
                                      where childField.AdditionalAttributes != null
                                      select childField;
                    foreach (FieldDefinition fieldDef in childFields)
                    {
                        XmlAttribute attrib = fieldDef.AdditionalAttributes.FirstOrDefault(at => at.Name == CRM2011AdapterUtilities.IsParentField);
                        if (attrib != null && attrib.Value.ToUpperInvariant() == true.ToString().ToUpperInvariant())
                        {
                            // Set the parent field's name and break out of the foreach loop
                            parentAttribName = fieldDef.Name;
                            break;
                        }
                    }
                }

                childEntity[parentAttribName] = new EntityReference(this.ProvidedEntityName, parentKey);
                this.CreateNewEntity(childEntity);
            }
            else
            {
                this.UpdateEntity(childEntity);
            }
        }

        //TODO: Update this method to no longer utilize the DynamicsIntegrationKey.  We don't use it, so it's not needed.
        /// <summary>
        /// Called by the WriteParentEntity method after the parent enty has been created to allow for the creation of any associated children entities.
        /// </summary>
        /// <param name="dictionary">The <c>Dictionary</c> that conatins the parent entity's data.</param>
        /// <param name="parentKey">The <c>Key</c> that was used in creating the parent entity.</param>
        /// <param name="collectionField">The <c>DefinitionFiled</c> that defines the collection.</param>
        protected virtual void CreateUpdateChildEntities(Dictionary<string, object> dictionary, Guid parentKey, FieldDefinition collectionField)
        {
            if (dictionary == null || collectionField == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage)) { ExceptionId = AdapterException.SystemExceptionGuid };
            }
            
            if (dictionary.ContainsKey(collectionField.Name))
            {
                List<string> childKeys = new List<string>();
                CollectionType containedType = collectionField.TypeDefinition as CollectionType;

                foreach (Dictionary<string, object> entityDictionary in (Dictionary<string, object>[])dictionary[collectionField.Name])
                {                    
                    Entity childEntity = this.GetDynamicInstance(CRM2011AdapterUtilities.DynamicsIntegrationKey, entityDictionary, containedType.Item.TypeName);
                    this.SetProperties(entityDictionary, childEntity);
                    this.CreateUpdateChildInstanceForField(parentKey, childEntity, collectionField.Name);
                    childKeys.Add(childEntity[CRM2011AdapterUtilities.DynamicsIntegrationKey].ToString());
                }

                if (childKeys.Count > 0)
                {
                    var childType = this.ObjectDefinition.Types.Single(t => t.Name == containedType.Item.TypeName);
                    FieldDefinition parentField = childType.Children.SingleOrDefault(def => def.FindAttribute(CRM2011AdapterUtilities.IsParentField) != null && Convert.ToBoolean(def.FindAttribute(CRM2011AdapterUtilities.IsParentField).Value, CultureInfo.InvariantCulture) == true);

                    if (parentField != null)
                    {
                        this.RemoveDeletedChildren(containedType.Item.TypeName, childKeys, CRM2011AdapterUtilities.DynamicsIntegrationKey, parentKey, parentField.Name);
                    }
                    else
                    {
                        this.RemoveDeletedChildren(containedType.Item.TypeName, childKeys, parentKey);
                    }
                }
            }
        }

        // TODO: Look at how I can remove the integration key from this method
        
        /// <summary>
        /// Removes children from a parent that appear to be deleted in the source system.
        /// </summary>
        /// <param name="childEntityName">The name of the child entity to be deleted</param>
        /// <param name="childKeys">An <c>ICollection</c> of <c>string</c>s that contains the valid child keys (the ones that have not been deleted).</param>
        /// <param name="parentKey">The <c>Key</c> of the parent entity</param>
        protected void RemoveDeletedChildren(string childEntityName, ICollection<string> childKeys, Guid parentKey)
        {
            this.RemoveDeletedChildren(childEntityName, childKeys, CRM2011AdapterUtilities.DynamicsIntegrationKey, parentKey, this.ProvidedEntityName + "id");
        }

        // TODO: Look at how I can remove the integration key from this method

        /// <summary>
        /// Removes children from a parent that appear to be deleted in the source system.
        /// </summary>
        /// <param name="childEntityName">The name of the child entity to be deleted</param>
        /// <param name="childEntities">An <c>ICollection</c> of <c>DynamicEntities</c> that contains the child entities currently in CRM</param>
        /// <param name="parentKey">The <c>Key</c> of the parent entity</param>
        protected void RemoveDeletedChildren(string childEntityName, ICollection<Entity> childEntities, Guid parentKey)
        {
            var childKeys = from entity in childEntities select entity[CRM2011AdapterUtilities.DynamicsIntegrationKey].ToString();
            this.RemoveDeletedChildren(childEntityName, childKeys.ToList(), CRM2011AdapterUtilities.DynamicsIntegrationKey, parentKey, this.ProvidedEntityName + "id");
        }

        /// <summary>
        /// Removes children from a parent that appear to be deleted in the source system.
        /// </summary>
        /// <param name="childEntityName">The name of the child entity to be deleted</param>
        /// <param name="childKeys">An <c>ICollection</c> of <c>string</c>s that contains the valid child keys (the ones that have not been deleted).</param>
        /// <param name="compAttribute">The attribute to use when determining if a child in the collection has been deleted or not</param>
        /// <param name="parentKey">The <c>Key</c> of the parent entity</param>
        /// <param name="parentAttributeName">The name of the parent entities key attribute</param>
        protected void RemoveDeletedChildren(string childEntityName, ICollection<string> childKeys, string compAttribute, Guid parentKey, string parentAttributeName)
        {
            var deletedEntities = this.GetDeletedChildren(childEntityName, childKeys, compAttribute, parentKey, parentAttributeName);
            foreach (Entity entity in deletedEntities)
            {                
                this.CrmAdapter.OrganizationService.Delete(childEntityName, (Guid)entity[childEntityName + "id"]);                
            }
        }


        // TODO: Look at how I can remove the integration key from this method

        /// <summary>
        /// Removes deleted addresses from <c>contact</c> and <c>account</c> entities
        /// </summary>
        /// <param name="childEntityName">The name of the child entity to be deleted</param>
        /// <param name="childKeys">An <c>ICollection</c> of <c>string</c>s that contains the valid child keys (the ones that have not been deleted).</param>
        /// <param name="compAttribute">The attribute to use when determining if a child in the collection has been deleted or not</param>
        /// <param name="parentKey">The <c>Key</c> of the parent entity</param>
        /// <param name="parentAttributeName">The name of the parent entities key attribute</param>
        /// <param name="primaryAddressDelete">Determines whether or not the address1 fields are blanked out.</param>
        /// <param name="secondaryAddressDelete">Determines whether or not the address2 fields are blanked out.</param>
        /// <remarks>If the <c>customeraddress</c> detected for deletion is address number 1 or 2, it's values are blanked out except for it's name
        /// and integration key and the address is not deleted.</remarks>
        protected void RemoveDeletedAddresses(string childEntityName, ICollection<string> childKeys, string compAttribute, Guid parentKey, string parentAttributeName, bool primaryAddressDelete, bool secondaryAddressDelete)
        {
            var deletedEntities = this.GetDeletedChildren(childEntityName, childKeys, compAttribute, parentKey, parentAttributeName);
            int addressNumber = 0;
            foreach (Entity entity in deletedEntities)
            {
                addressNumber = (int)entity["addressnumber"];
                if (addressNumber > 2)
                {
                    this.CrmAdapter.OrganizationService.Delete(childEntityName, (Guid)entity[childEntityName + "id"]);
                }

                if ((addressNumber == 1 && !primaryAddressDelete) || (addressNumber == 2 && !secondaryAddressDelete))
                {
                    ComplexType ct = this.GetComplexTypeFromEntity(entity);

                    foreach (FieldDefinition def in ct.Fields)
                    {
                        if (!def.IsReadOnly && def.Name != CRM2011AdapterUtilities.DynamicsIntegrationKey && def.Name != "name" && def.Name != "customeraddressid" && def.Name != "parentid")
                        {
                            entity[def.Name] = null;
                        }
                    }

                    this.CallCrmExecuteWebMethod(new UpdateRequest() { Target = entity });
                }
            }
        }

        // TODO: Look at how I can remove the integration key from this method

        /// <summary>
        /// Called by the WriteParentEntity method after the parent enty has been created to allow for the creation of any associated children entities.
        /// </summary>
        /// <param name="dictionary">The <c>Dictionary</c> that conatins the parent entity's data.</param>
        /// <param name="parentKey">The <c>Key</c> that was used in creating the parent entity.</param>
        /// <param name="collectionField">The <c>DefinitionFiled</c> that defines the collection.</param>
        /// <returns>A <c>List</c> of <c>DynamicEntities</c> that contains the children for this entity</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists")]
        protected virtual List<Entity> RetrieveChildEntities(Dictionary<string, object> dictionary, Guid parentKey, FieldDefinition collectionField)
        {
            if (dictionary == null || collectionField == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage)) { ExceptionId = AdapterException.SystemExceptionGuid };
            }
            
            List<Entity> children = new List<Entity>();
            if (dictionary.ContainsKey(collectionField.Name))
            {
                foreach (Dictionary<string, object> entityDictionary in (Dictionary<string, object>[])dictionary[collectionField.Name])
                {
                    CollectionType containedType = collectionField.TypeDefinition as CollectionType;
                    Entity childEntity = this.GetDynamicInstance(CRM2011AdapterUtilities.DynamicsIntegrationKey, entityDictionary, containedType.Item.TypeName);

                    this.SetProperties(entityDictionary, childEntity);

                    children.Add(childEntity);
                }
            }

            return children;
        }

        /// <summary>
        /// Gets a <c>Dictionary</c> that contains the <c>Entity</c> instance that was retrieved using the supplied <c>RetrieveTarget</c>.
        /// </summary>
        /// <param name="target">A <c>TargetRetrive</c> to use when calling the CrmWebservice.</param>
        /// <returns>A <c>Dictionary</c> that contains the retrieved <c>Entity</c>'s property names as Keys and property values 
        /// as Values for those Keys.</returns>
        protected Dictionary<string, object> RetrieveEntityAsDictionary(Entity target)
        {

            if (target == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage)) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            List<string> attributes = new List<string>();
            List<FieldDefinition> defs = new List<FieldDefinition>();
            foreach(FieldDefinition def in this.ObjectDefinition.ToComplexType().Fields)
            {
                if (!(def.TypeDefinition is CollectionType))
                {
                    attributes.Add(def.Name);
                    defs.Add(def);
                }
            }
            
            ColumnSet cols = new ColumnSet(attributes.ToArray());
            RetrieveRequest request = new RetrieveRequest() { ColumnSet = cols, Target = new EntityReference(target.LogicalName, target.Id) };
            RetrieveResponse response = this.CallCrmExecuteWebMethod(request) as RetrieveResponse;
            Entity parent = response.Entity;
            return GetDictionary(parent, this.CrmAdapter, defs);
        }

        /// <summary>
        /// Gets an array of <c>Dictionary</c> objects that contains the specified child entity type instances for this entity as Dictionaries.
        /// </summary>
        /// <param name="childEntityName">The <c>EntityName</c> of the child objects to be retrieved.</param>
        /// <param name="parentPropertyName">The name of the property on the child entity that holds the parent's id.</param>
        /// <param name="parentKey">The parent's <c>Key</c> value as a <c>string</c>.</param>
        /// <returns>An Array of <c>Dictionary</c> objects that each contain a child entity instance.</returns>
        //protected Dictionary<string, object>[] GetChildDictionaries(string childEntityName, string parentPropertyName, string parentKey)
        //{
        //    return this.GetChildDictionaries(childEntityName, parentPropertyName, parentKey);
        //}

        /// <summary>
        /// Gets an array of <c>Dictionary</c> objects that contains the specified child entity type instances for this entity as Dictionaries.
        /// </summary>
        /// <param name="childEntityName">The name of the child objects to be retrieved.</param>
        /// <param name="parentPropertyName">The name of the property on the child entity that holds the parent's id.</param>
        /// <param name="parentKey">The parent's <c>Key</c> value as a <c>string</c>.</param>
        /// <returns>An Array of <c>Dictionary</c> objects that each contain a child entity instance.</returns>
        
        // This will be necessary when we fix the many to many issue.  One of the reasons it's currently commented out is that it's not used
        // the other is because the GetDictionary method signature has been changed to allow for field definitions to be passed to look up Entity Reference
        // lookup fields (which replace the DynamicsIntegrationKey used previously).

        //protected Dictionary<string, object>[] GetChildDictionaries(string childEntityName, string parentPropertyName, string parentKey)
        //{
        //    RetrieveMultipleResponse retrieveResponse = this.RetrieveMultipleDynamicEntities(parentKey, parentPropertyName, childEntityName);
        //    List<Entity> children = retrieveResponse.EntityCollection.Entities.ToList();
        //    List<Dictionary<string, object>> childEntities = new List<Dictionary<string, object>>();

        //    // Convert the DynamicEntities found to dictionaries and place them into the returned array
        //    children.ForEach(be => childEntities.Add(GetDictionary(be, this.CrmAdapter)));
        //    return childEntities.ToArray();
        //}

        /// <summary>
        /// Gets an array of <c>Key</c> objects that represent the <c>Key</c>s for entities provided by this provider that have been modified since the 
        /// date provided in the modifiedDate <c>string</c>.
        /// </summary>
        /// <param name="modifiedDate">The <c>DateTime</c> to be used as the modifiedon criteria for the retrieve request.</param>
        /// <param name="keyPropertyName">The name of the property on the provided entity that hold the primary key.</param>
        /// <returns>An Array of <c>Key</c> objects that correspond to entities that have been modified since the supplied modifiedDate date.</returns>
        protected Guid[] GetModifiedEntityKeys(DateTime modifiedDate, string keyPropertyName)
        {
            RetrieveMultipleRequest retrieveRequest = new RetrieveMultipleRequest();
            DateTime date = modifiedDate < MinDateTime ? DateTime.SpecifyKind(MinDateTime, DateTimeKind.Utc) : DateTime.SpecifyKind(modifiedDate, DateTimeKind.Utc);
            bool isDynamic = this is DynamicObjectProvider;
            ColumnSet cols = new ColumnSet() { Columns = { keyPropertyName } };
            retrieveRequest.Query = CRM2011AdapterUtilities.GetReaderQueryExpression(this.ProvidedEntityName, date, this.CrmAdapter, isDynamic, cols);

            var retrievedEntities = GetKeys(retrieveRequest);
            
            List<Guid> keyList = new List<Guid>();
            retrievedEntities.ForEach(be => keyList.Add(be.Id));
            return keyList.ToArray();
            
        }

        private List<Entity> GetKeys(RetrieveMultipleRequest request)
        {
            List<Entity> retrievedEntities = new List<Entity>();

            while (true)
            {
                RetrieveMultipleResponse retrieveResponse = (RetrieveMultipleResponse)this.CallCrmExecuteWebMethod(request);

                var results = retrieveResponse.EntityCollection;

                if (results != null)
                {
                    retrievedEntities.AddRange(retrieveResponse.EntityCollection.Entities.ToList());
                }

                if (results.MoreRecords)
                {
                    ((QueryExpression)request.Query).PageInfo.PageNumber++;
                    ((QueryExpression)request.Query).PageInfo.PagingCookie = results.PagingCookie;
                }
                else
                {
                    break;
                }
            }

            return retrievedEntities;
            
        }

        /* TODO: Should probably refactor this to use the CRM2011AdapterUtilities like the other methods */
        /// <summary>
        /// Gets an array of <c>Key</c> objects that represent the <c>Key</c>s for entities provided by this provider that have been deleted since the 
        /// date provided in the modifiedDate <c>string</c>.
        /// </summary>
        /// <param name="modifiedDate">The <c>string</c> representation of a <c>DateTime</c> to be used as the modifiedon criteria for the retrieve request.</param>
        /// <returns>An <c>ICollection</c> of <c>Key</c> objects that correspond to entities that have been deleted since the supplied modifiedDate date.</returns>
        protected ICollection GetDeletedEntityKeys(DateTime modifiedDate)
        {
            RetrieveMultipleRequest retrieveRequest = new RetrieveMultipleRequest();
            var query = new QueryExpression("audit");
            query.ColumnSet = new ColumnSet(true);
            query.Criteria.AddCondition(new ConditionExpression("createdon", ConditionOperator.GreaterEqual, modifiedDate));
            query.Criteria.AddCondition(new ConditionExpression("operation", ConditionOperator.Equal, 3));
            query.Criteria.AddCondition(new ConditionExpression("action", ConditionOperator.Equal, 3));
            query.Criteria.AddCondition(new ConditionExpression("objecttypecode", ConditionOperator.Equal, this.ProvidedEntityName));
            query.PageInfo = new PagingInfo();
            query.PageInfo.Count = 5000;
            query.PageInfo.PageNumber = 1;
            query.PageInfo.PagingCookie = null;

            retrieveRequest.Query = query;
            var retrievedEntities = GetKeys(retrieveRequest);

            List<string> keyList = new List<string>();
            retrievedEntities.ForEach(be => keyList.Add(((EntityReference)be.Attributes["objectid"]).Id.ToString()));
            return keyList;
        }

        /// <summary>
        /// Gets an array of <c>Key</c> objects that represent the <c>Key</c>s for entities provided by this provider that have been deleted since the 
        /// date provided in the modifiedDate <c>string</c>.
        /// </summary>
        /// <param name="criterion">The <c>string</c> representation of a <c>DateTime</c> to be used as the modifiedon criteria for the retrieve request.</param>
        /// <returns>An <c>ICollection</c> of <c>Key</c> objects that correspond to entities that have been deleted since the supplied modifiedDate date.</returns>
        protected ICollection FindEntities(ICriterion criterion)
        {
            RetrieveMultipleRequest retrieveRequest = new RetrieveMultipleRequest();
            retrieveRequest.Query = CRM2011AdapterUtilities.GetQueryExpression(this.ProvidedEntityName, criterion, new ColumnSet(true));
            RetrieveMultipleResponse retrieveResponse = (RetrieveMultipleResponse)this.CallCrmExecuteWebMethod(retrieveRequest);
            return retrieveResponse.EntityCollection.Entities.ToList();
        }

        private static void CheckStateAndStatus(Dictionary<string, object> dictionary, Entity entity, string propertyName, DynamicCrmAdapter adapter)
        {
            int stateToSet = (int)dictionary[propertyName];
            if (!entity.Contains(propertyName) || CRM2011AdapterUtilities.ConvertStateNameToValue(entity[propertyName].ToString(), entity.LogicalName, adapter) != stateToSet)
            {
                entity[propertyName] = new OptionSetValue(stateToSet);
            }
            else
            {
                entity.Attributes.Remove(propertyName);
                if (entity.Contains("statuscode"))
                {
                    entity.Attributes.Remove("statuscode");
                }
            }
        }
       
        
        /// <summary>
        /// Removes the statecode from a <c>Entity</c>
        /// </summary>
        /// <param name="entity">The <c>Entity</c> to remove the state code from</param>
        /// <returns>The value of the state code that was removed</returns>
        private static OptionSetValue RemoveStateCode(Entity entity)
        {
            OptionSetValue state = null;
            if (entity.Contains("statecode") && entity["statecode"] != null)
            {
                state = (OptionSetValue)entity["statecode"];
                entity.Attributes.Remove("statecode");
            }

            return state;
        }

        /// <summary>
        /// Removes the status code from a <c>Entity</c>
        /// </summary>
        /// <param name="entity">The <c>Entity</c> to remove the status code from</param>
        /// <returns>The value of the status code that was removed</returns>
        private static OptionSetValue RemoveStatusCode(Entity entity)
        {
            OptionSetValue status = null;
            if (entity.Contains("statuscode") && entity["statuscode"] != null)
            {
                status = (OptionSetValue)entity["statuscode"];
                entity.Attributes.Remove("statuscode");
            }

            return status;
        }

        /// <summary>
        /// Removes properties from the <c>Entity</c> that are not needed.
        /// </summary>
        /// <param name="entity">The <c>Entity</c> that is being created / updated.</param>
        /// <param name="dictionary">The <c>Dictionary</c> that conatins the data to placed into the supplied <c>DynmicEntity</c></param>
        private static void RemoveProperties(Entity entity, Dictionary<string, object> dictionary)
        {
            // Only loop if this is an update
            if (entity.Contains(CRM2011AdapterUtilities.IsNew) && !(bool)entity[CRM2011AdapterUtilities.IsNew])
            {
                var updateProperties = from property in entity.Attributes where dictionary.Keys.Contains(property.Key) || property.Key == CRM2011AdapterUtilities.IsNew || property.Key == entity.LogicalName + "id" || property.Key == "addressnumber" || CRM2011AdapterUtilities.GetSpecialAddressPicklistFields().Contains(property.Key) select property;
                entity.Attributes = new AttributeCollection();
                updateProperties.ToList().ForEach(prop => entity.Attributes.Add(prop));
            }
        }

        private static void PrepEntityForCreate(Entity entity)
        {
            // on a create, it appears we need to set the owningid
            if (entity.Attributes.Contains("owninguser"))
            {
                entity["ownerid"] = entity["owninguser"];
            }
            if (entity.Attributes.Contains("createdon") && entity.Attributes.Contains("overriddencreatedon"))
            {
                entity["overriddencreatedon"] = entity["createdon"];
            }
            
        }

        /// <summary>
        /// Removes properties and sets state for an entity
        /// </summary>
        /// <param name="entity">A <c>Entity</c> to prep for an update operation.</param>
        private void PrepEntityForUpdate(Entity entity)
        {
            entity.Attributes.Remove(CRM2011AdapterUtilities.IsNew);
            this.SetState(entity);
            if (entity.Contains("owninguser") && entity["owninguser"].GetType().Equals(typeof(EntityReference)))
            {
                this.SetOwner(entity);
            }

            RemoveStateCode(entity);
            RemoveStatusCode(entity);
        }

        private void SetOwner(Entity entity)
        {
            OrganizationRequest request = new OrganizationRequest("Assign") { Parameters = new ParameterCollection() };
            request.Parameters.Add("Assignee", (EntityReference)entity["owninguser"]);
            request.Parameters.Add("Target", new EntityReference(entity.LogicalName, entity.Id));            
            this.CallCrmExecuteWebMethod(request);
        }

        /// <summary>
        /// Updates a <c>DynmicEntity</c>'s state and status code property and then calls the service to persist the values
        /// </summary>
        /// <param name="entity">The <c>Entity</c> to set the values on</param>
        /// <param name="state">The state to set</param>
        /// <param name="status">The status to set</param>
        private void ApplyStateAndStatus(Entity entity, OptionSetValue state, OptionSetValue status)
        {
            if (state != null)
            {
                entity["statecode"] = state;
                if (status != null)
                {
                    entity["statuscode"] = status;
                }

                this.SetState(entity);
            }
        }        
        
        /// <summary>
        /// Gets the <c>ComplexType</c> for an <c>Entity</c>
        /// </summary>
        /// <param name="entity">The <c>Entity</c> to retrieve the <c>ComplexType</c> for</param>
        /// <returns>A <c>ComplexType</c> that encapsulates the <c>Entity</c> instance supplied</returns>
        private ComplexType GetComplexTypeFromEntity(Entity entity)
        {
            ComplexType ct = ObjectDefinition.Types.FirstOrDefault(f => f.Name == entity.LogicalName) as ComplexType;
            if (ct == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ComplexTypeNotFoundExceptionMessage, entity.LogicalName, this.Name)) { ExceptionId = ErrorCodes.InvalidComplexType };
            }

            return ct;
        }

        /// <summary>
        /// Gets the children that are in CRM currently that were not in the supplied collection on an update operation
        /// </summary>
        /// <param name="childEntityName">The name of the child entity</param>
        /// <param name="childKeys">The collection of child keys supplied on the updated <c>Dictionary</c></param>
        /// <param name="compAttribute">The attribute used for comparing the child entities</param>
        /// <param name="parentKey">The <c>Key</c> for the parent entity</param>
        /// <param name="parentAttributeName">The name of the attribute on the parent that hold the supplied <c>Key</c></param>
        /// <returns>An <c>IEnumerable</c> that can be used to iterate over the collection of child entities</returns>
        private IEnumerable<Entity> GetDeletedChildren(string childEntityName, ICollection<string> childKeys, string compAttribute, Guid parentKey, string parentAttributeName)
        {
            RetrieveMultipleResponse response = this.RetrieveMultipleDynamicEntities(parentKey.ToString(), parentAttributeName, childEntityName);
            var deletedEntities = from entity in response.EntityCollection.Entities.ToList() where entity.Contains(compAttribute) && !childKeys.Contains(entity[compAttribute].ToString(), StringComparer.CurrentCultureIgnoreCase) select entity;
            return deletedEntities;
        }

        /// <summary>
        /// Gets an instance of a <c>EntityReference</c> type that has its type property set.
        /// </summary>
        /// <typeparam name="T">The concrete type of the <c>EntityReference</c> to create and initialize.</typeparam>
        /// <param name="field">The current <c>DefinitionField</c> being mapped.  This determines the type property when creating a <c>Lookup</c> instance.</param>
        /// <returns>A new instance of a <c>EntityReference</c> type object.</returns>
        private EntityReference GetReferenceInstanceType(FieldDefinition field)
        {
            // set the returned reference to a Lookup initialy, since that is the most common crm reference entity
            EntityReference reference = new EntityReference();
            this.SetLookupType(field, reference);
            return reference;
        }

        // TODO: QueryByEntityKey utilizes the DynamicsIntegrationKey, see if this is necessary.
        /// <summary>
        /// Gets an instance of the <c>Entity</c> class that can be used for delete operations
        /// </summary>
        /// <param name="queryValue">The value of the integration key for a deleted entity.</param>
        /// /// <param name="keyProperty">The name of the entiy's key property.</param>
        /// <param name="entityName">The name of the entity.</param>
        /// <param name="attributes">The atributes to include on the <c>Entity</c> instance</param>
        /// <returns>A <c>Entity</c> that conatins a deleted instance of a <c>BusinessEntity</c>.</returns>
        internal Entity GetDynamicInstanceToDelete(string queryValue, string keyProperty, string entityName, params string[] attributes)
        {
            RetrieveMultipleResponse retrieveResponse = this.RetrieveMultipleDynamicEntities(queryValue, keyProperty, entityName, attributes);

            if (retrieveResponse.EntityCollection.Entities.Count == 1)
            {
                return retrieveResponse.EntityCollection.Entities[0] as Entity;
            }
            else if (retrieveResponse.EntityCollection.Entities.Count < 1)
            {
                return QueryByEntityKey(queryValue, entityName);
            }
            else
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.MultipleDynamicEntitiesReturnedExceptionMessage, entityName, keyProperty, queryValue)) { ExceptionId = ErrorCodes.DeleteResponseMultipleResult };
            }
        }

        // TODO: See if one of the values being passed in here actually has the GUID for the object rather than using the DynamicsIntegrationKey
        private Entity QueryByEntityKey(string queryValue, string entityName)
        {
            if (IsValidGuidString(queryValue))
            {
                RetrieveMultipleResponse retrieveResponse = this.RetrieveMultipleDynamicEntities(queryValue, entityName + "id", entityName);
                if (retrieveResponse.EntityCollection.Entities.Count == 1)
                {
                    return retrieveResponse.EntityCollection.Entities[0] as Entity;
                }

                if (retrieveResponse.EntityCollection.Entities.Count > 1)
                {
                    throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.MultipleDynamicEntitiesReturnedExceptionMessage, entityName, CRM2011AdapterUtilities.DynamicsIntegrationKey, queryValue)) { ExceptionId = ErrorCodes.DeleteResponseMultipleResult };
                }
            }

            return null;
        }

        /// <summary>
        /// Calls the CRM service and retrieves the entities that match the modifiedDateValue expression that is built up based on the supplied values
        /// </summary>
        /// <param name="queryValue">The value of the property to be querried for</param>
        /// <param name="keyProperty">The attribute to use to use when querring</param>
        /// <param name="entityName">The name of the entity to be querried for</param>
        /// <param name="attributes">The atributes to include on the <c>Entity</c> instance</param>
        /// <returns>A <c>RetrieveMaultipleResponse</c> that contains the <c>DynamicEntities</c> that matched the supplied values</returns>
        internal RetrieveMultipleResponse RetrieveMultipleDynamicEntities(string queryValue, string keyProperty, string entityName, params string[] attributes)
        {
            RetrieveMultipleRequest retrieveRequest = new RetrieveMultipleRequest();
            retrieveRequest.Query = CRM2011AdapterUtilities.GetQueryExpression(entityName, keyProperty, queryValue, attributes);
            return (RetrieveMultipleResponse)this.CallCrmExecuteWebMethod(retrieveRequest);
        }

        /// <summary>
        /// Initializes this provider's list of collection fields
        /// </summary>
        private void InitCollectionFields()
        {
            this.collectionFields = new List<FieldDefinition>();
            var complexTypes = ObjectDefinition.Types.OfType<ComplexType>();
            foreach (ComplexType type in complexTypes)
            {
                foreach (FieldDefinition fieldDef in type.Fields)
                {
                    if (fieldDef.TypeDefinition is CollectionType)
                    {
                        this.collectionFields.Add(fieldDef);
                    }
                }
            }
        }

        private Entity GetBaseCurrencyPriceLevel()
        {
            ColumnSet cols = new ColumnSet() { Columns = { "pricelevelid" } };
            QueryByAttribute queryAtrib = new QueryByAttribute() { EntityName = "pricelevel", Attributes = { "transactioncurrencyid", "enddate" }, Values = { this.CrmAdapter.BaseCurrencyId, null }, ColumnSet = cols };
            RetrieveMultipleRequest request = new RetrieveMultipleRequest() { Query = queryAtrib };
            RetrieveMultipleResponse response = (RetrieveMultipleResponse)this.CallCrmExecuteWebMethod(request);
            return response.EntityCollection.Entities.FirstOrDefault();
        }

        private static bool IsValidGuidString(string guidString)
        {
            try
            {
                Guid newGuid = new Guid(guidString);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a <c>Dictionary</c> that represents a <c>DynamicEntity</c>.
        /// </summary>
        /// <param name="adapter">An instance of a <c>CRMAdapter</c> to use when getting dynamics_integrationkey data for a <c>Lookup</c> type.</param>
        /// <returns>A <c>Dictionary</c> that has Keys that are the property names of the <c>DynamicEntity</c> supplied and 
        /// Values that are the values of those properties on the <c>DynamicEntity</c>.</returns>
        internal static Dictionary<string, object> GetDictionary(Entity entity, DynamicCrmAdapter adapter, List<FieldDefinition> fieldDefinitions)
        {
            Dictionary<string, object> dictionary = new Dictionary<string, object>();

            // This dictionary is for holding a complexType that might be the type for the current property on the entity
            Dictionary<string, object> holdingDictionary;
            foreach (KeyValuePair<string, object> property in entity.Attributes)
            {
                // CrmMoney needs the dictionary to be converted but it also starts with the same prefix as the property types that do not,so handle it separately
                // else if the property is not one of the Built-in types and is also not of the StringProperty type, use the holding dictionary
                Type propertyType = property.Value.GetType();
                holdingDictionary = new Dictionary<string, object>();
                if (propertyType == typeof(Money))
                {
                    PopulateDictionary(entity, holdingDictionary, property);
                }
                else if (propertyType == typeof(OptionSetValue))
                {
                    PopulateOptionSetValueDictionary(entity, holdingDictionary, property, adapter);
                }
                else if (propertyType == typeof(EntityReference))
                {
                    FieldDefinition definition = fieldDefinitions.FirstOrDefault(x => x.Name == property.Key);
                    PopulateDictionary(entity, holdingDictionary, property, adapter, definition);
                }
                else if (propertyType == typeof(EntityCollection))
                {
                    FieldDefinition definition = fieldDefinitions.FirstOrDefault(x => x.Name == property.Key);
                    PopulatePartyList(entity, holdingDictionary, property, adapter, definition);
                }

                // The property is of a ComplexType and the holding dictionary was populated
                // else if the property is one of the Built-in CRM types just convert it
                // else if the property was a string property, just use its value
                if (holdingDictionary.Count > 0)
                {
                    dictionary.Add(property.Key, holdingDictionary);
                }
                else
                {
                    dictionary.Add(property.Key, property.Value);
                }
            }

            if (fieldDefinitions.Any(x => x.Name == "overriddencreatedon"))
            {
                dictionary.Add("overriddencreatedon", null);
            }

            return dictionary;
        }

        /// <summary>
        /// Populates a <c>Dictionary</c> with the values contained in a <c>DynamicEntity</c>.
        /// </summary>
        /// <param name="dynamicEntity">The <c>DynamicEntity</c> to get the properties and data from.</param>
        /// <param name="complexTypeDictionary">The <c>Dictionary</c> to be populated.</param>
        /// <param name="property">The property on the <c>DynamicEntity</c> to populate the supplied <c>Dictionary</c> for.</param>
        private static void PopulateDictionary(Entity dynamicEntity, Dictionary<string, object> complexTypeDictionary, KeyValuePair<string, object> property)
        {
            List<PropertyInfo> propertyList = new List<PropertyInfo>(dynamicEntity[property.Key].GetType().GetProperties());
            foreach (PropertyInfo info in propertyList)
            {
                // Properties in the list that are instances of the actual type are not needed for our purposes
                if (!dynamicEntity[property.Key].GetType().Name.Contains(info.PropertyType.Name))
                {
                    object propertyValue = info.GetValue(dynamicEntity[property.Key], null);

                    // Do not map null properties
                    if (propertyValue != null)
                    {
                        complexTypeDictionary.Add(info.Name, propertyValue);
                    }
                }
            }
        }

        /// <summary>
        /// Populates a <c>Dictionary</c> with the values contained in a <c>DynamicEntity</c>.
        /// </summary>
        /// <param name="dynamicEntity">The <c>DynamicEntity</c> to get the properties and data from.</param>
        /// <param name="complexTypeDictionary">The <c>Dictionary</c> to be populated.</param>
        /// <param name="property">The property on the <c>DynamicEntity</c> to populate the supplied <c>Dictionary</c> for.</param>
        /// <param name="adapter">An instance of a <c>CRMAdapter</c> to use when getting dynamics_integrationkey data for a <c>Lookup</c> type.</param>
        private static void PopulateDictionary(Entity dynamicEntity, Dictionary<string, object> complexTypeDictionary, KeyValuePair<string, object> property, DynamicCrmAdapter adapter, FieldDefinition definition)
        {
            PopulateDictionary(dynamicEntity, complexTypeDictionary, property);
            
            if (complexTypeDictionary.ContainsKey("Id") && complexTypeDictionary.ContainsKey("LogicalName"))
            {
                if (complexTypeDictionary["LogicalName"].ToString() != "attachment" && complexTypeDictionary["LogicalName"].ToString() != "activitymimeattachment")
                {
                ColumnSet cols = new ColumnSet(true);
                if (!CRM2011AdapterUtilities.GetIntegratedEntities().Contains(complexTypeDictionary["LogicalName"].ToString()))
                {
                    cols = new ColumnSet(true);
                }
                RetrieveRequest request = new RetrieveRequest() { ColumnSet = cols, Target = new EntityReference(complexTypeDictionary["LogicalName"].ToString(), new Guid(complexTypeDictionary["Id"].ToString())) };

                RetrieveResponse response = adapter.OrganizationService.Execute(request) as RetrieveResponse;
                Entity entity = response.Entity;
                    
                var lookupType = definition.AdditionalAttributes.FirstOrDefault(x => x.Name == "LookupType");
                var lookupField = definition.AdditionalAttributes.FirstOrDefault(x => x.Name == "LookupField");

                var typeSplit = lookupType.Value.Split(',');
                var fieldSplit = lookupField.Value.Split(',');

                var keyValueLookup = new List<KeyValuePair<string,string>>();

                if (typeSplit.Count() > 1 && fieldSplit.Count() > 1)
                {
                    for (int i = 0; i < typeSplit.Count(); i++)
                    {
                        keyValueLookup.Add(new KeyValuePair<string, string>(typeSplit[i], fieldSplit[i]));
                    }

                    lookupField.Value = keyValueLookup.FirstOrDefault(x=> x.Key == entity.LogicalName).Value;
                }

                if (lookupField != null)
                {
                    if (lookupField.Value == "domainname" || entity.LogicalName == "systemuser")
                    {
                        lookupField.Value = "fullname";
                    }
                    else if (entity.LogicalName == "activitypointer")
                    {
                        lookupField.Value = "activityid";
                    }

                    complexTypeDictionary.Add(lookupField.Value, entity[lookupField.Value]);
                }
                }
            }
        }

        /// <summary>
        /// Populates a <c>Dictionary</c> with the values contained in a <c>DynamicEntity</c>.
        /// </summary>
        /// <param name="dynamicEntity">The <c>DynamicEntity</c> to get the properties and data from.</param>
        /// <param name="complexTypeDictionary">The <c>Dictionary</c> to be populated.</param>
        /// <param name="property">The property on the <c>DynamicEntity</c> to populate the supplied <c>Dictionary</c> for.</param>
        /// <param name="adapter">An instance of a <c>CRMAdapter</c> to use when getting dynamics_integrationkey data for a <c>Lookup</c> type.</param>
        private static void PopulateOptionSetValueDictionary(Entity dynamicEntity, Dictionary<string, object> complexTypeDictionary, KeyValuePair<string, object> property, DynamicCrmAdapter adapter)
        {
            PopulateDictionary(dynamicEntity, complexTypeDictionary, property);

            if (!complexTypeDictionary.ContainsKey("name"))
            {
                RetrieveAttributeRequest attribReq = new RetrieveAttributeRequest() { EntityLogicalName = dynamicEntity.LogicalName, LogicalName = property.Key, RetrieveAsIfPublished = true };
                
                // Get the attribute metadata for the state attribute.
                RetrieveAttributeResponse metadataResponse = (RetrieveAttributeResponse)adapter.OrganizationService.Execute(attribReq);

                PicklistAttributeMetadata picklistAttrib = metadataResponse.AttributeMetadata as PicklistAttributeMetadata;
                StateAttributeMetadata stateAttrib = metadataResponse.AttributeMetadata as StateAttributeMetadata;
                IEnumerable<string> picklistValue = null;
                if (picklistAttrib != null)
                {
                    picklistValue = from option in picklistAttrib.OptionSet.Options
                                    where option.Value == ((OptionSetValue)property.Value).Value
                                    select option.Label.UserLocalizedLabel.Label;
                }
                else if(stateAttrib != null)
                {
                    picklistValue = from option in stateAttrib.OptionSet.Options
                                    where option.Value == ((OptionSetValue)property.Value).Value
                                    select option.Label.UserLocalizedLabel.Label;
                }                

                // ensure that both the returned list and the first item in the returned list are not null or empty.
                if (picklistValue != null && picklistValue.Count() > 0 && picklistValue.First() != null)
                {
                    complexTypeDictionary.Add("name", picklistValue.First());
                }
            }
        }

        private static void PopulatePartyList(Entity dynamicEntity, Dictionary<string, object> complexTypeDictionary, KeyValuePair<string, object> property, DynamicCrmAdapter adapter, FieldDefinition definition)
        {
            PopulateDictionary(dynamicEntity, complexTypeDictionary, property);
            var tempDictionary = new Dictionary<string, object>();

            foreach (var entity in ((DataCollection<Entity>)complexTypeDictionary["Entities"]))
            {
                var entityDictionary = GetDictionary(entity, adapter, definition.TypeDefinition.Children.ToList());

                var name = entityDictionary["activitypartyid"].ToString();
                if (name != null && name != string.Empty)
                {
                    tempDictionary.Add(name, entityDictionary);
                }
            }

            complexTypeDictionary.Add("ActivityParties", tempDictionary);
        }
    }
}
