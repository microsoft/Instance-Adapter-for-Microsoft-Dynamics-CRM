namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm
{
    using AdapterAbstractionLayer;
    using Properties;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Xrm.Sdk;
    using Xrm.Sdk.Messages;
    using Xrm.Sdk.Metadata;
    using Xrm.Sdk.Query;

    /// <summary>
    /// Provides interaction with the Dynamics CRM <c>DynamicEntity</c> object.
    /// </summary>
    public sealed class DynamicObjectProvider : CrmObjectProvider, IObjectReader, IObjectWriter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicObjectProvider"/> class.
        /// </summary>
        public DynamicObjectProvider()
            : base()
        {
            this.DoesDetectDuplicates = false;
        }

        /// <summary>
        /// Writes an <c>object</c> to the stream
        /// </summary>
        /// <param name="value">The object to be written</param>
        public void WriteObject(object value)
        {
            this.Initialize();

            Dictionary<string, object> dictionary = value as Dictionary<string, object>;
            if (dictionary.ContainsKey("Record"))
            {
                RelationshipData relatedData = dictionary["Record"] as RelationshipData;

                string providedEntityName = relatedData.ReturnedDictionary["ProvidedEntityName"].ToString();
                string entityRelationshipName = relatedData.ReturnedDictionary["EntityRelationshipName"].ToString();
                string entity1Name = relatedData.ReturnedDictionary["Entity1Name"].ToString();
                string entity1IntersectAtt = relatedData.ReturnedDictionary["Entity1IntersectAtt"].ToString();
                string entity2Name = relatedData.ReturnedDictionary["Entity2Name"].ToString();
                string entity2IntersectAtt = relatedData.ReturnedDictionary["Entity2IntersectAtt"].ToString();

                string aliaseEntity1IntersectAtt = "aa." + entity1IntersectAtt;
                string aliaseEntity2IntersectAtt = "aa." + entity2IntersectAtt;
                var entityMetaDataResponse = this.AssociatedRetrival(entityRelationshipName, entity1Name, entity1IntersectAtt);

                string entity1ID = relatedData.ReturnedDictionary[entity1IntersectAtt].ToString();
                string entity2ID = relatedData.ReturnedDictionary[entity2IntersectAtt].ToString();
                bool create = true;

                if (entityMetaDataResponse.EntityCollection.Entities.Count > 0)
                {
                    List<Entity> FindRecord = entityMetaDataResponse.EntityCollection.Entities.Where(p => p.GetAttributeValue<AliasedValue>(aliaseEntity1IntersectAtt).Value.ToString() == entity1ID && p.GetAttributeValue<AliasedValue>(aliaseEntity2IntersectAtt).Value.ToString() == entity2ID).ToList();
                    create = FindRecord.Count <= 0;
                }

                if (create)
                {
                    this.CreateAssociatedRequest(providedEntityName, entity1Name, entity1ID, entity2Name, entity2ID);
                }
            }
            else
            {
                this.WriteParentEntity(value);
            }
        }

        /// <summary>
        /// Deletes an <c>object</c>
        /// </summary>
        /// <param name="key">The <c>object</c> to be deleted</param>
        public void DeleteObject(object key)
        {
            this.Initialize();
            if (key.GetType() == Type.GetType("Microsoft.Dynamics.Integration.Adapters.DynamicCrm.RelationshipData"))
            {
                RelationshipData relationshipData = key as RelationshipData;

                string providedEntityName = relationshipData.ReturnedDictionary["ProvidedEntityName"].ToString();
                string entityRelationshipName = relationshipData.ReturnedDictionary["EntityRelationshipName"].ToString();
                string entity1Name = relationshipData.ReturnedDictionary["Entity1Name"].ToString();
                string entity1IntersectAtt = relationshipData.ReturnedDictionary["Entity1IntersectAtt"].ToString();
                string entity2Name = relationshipData.ReturnedDictionary["Entity2Name"].ToString();
                string entity2IntersectAtt = relationshipData.ReturnedDictionary["Entity2IntersectAtt"].ToString();

                string aliaseEntity1IntersectAtt = "aa." + entity1IntersectAtt;
                string aliaseEntity2IntersectAtt = "aa." + entity2IntersectAtt;
                var entityMetaDataResponse = this.AssociatedRetrival(entityRelationshipName, entity1Name, entity1IntersectAtt);

                string entity1ID = relationshipData.ReturnedDictionary[entity1IntersectAtt].ToString();
                string entity2ID = relationshipData.ReturnedDictionary[entity2IntersectAtt].ToString();
                bool create = false;

                if (entityMetaDataResponse.EntityCollection.Entities.Count > 0)
                {
                    List<Entity> findRecord = entityMetaDataResponse.EntityCollection.Entities.Where(p => p.GetAttributeValue<AliasedValue>(aliaseEntity1IntersectAtt).Value.ToString() == entity1ID && p.GetAttributeValue<AliasedValue>(aliaseEntity2IntersectAtt).Value.ToString() == entity2ID).ToList();
                    create = findRecord.Count > 0;
                }

                if (create)
                {
                    this.CreateDisassociateRequest(providedEntityName, entity1Name, entity1ID, entity2Name, entity2ID);
                }
            }
            else
            {
                this.DeleteEntity(key, this.ProvidedEntityName + "id");
            }
        }

        /// <summary>
        /// Read an <c>object</c> from the stream
        /// </summary>
        /// <param name="key">The key for the <c>object</c> to be read</param>
        /// <returns>An instance of the <c>object</c> provided by this <c>ObjectProvicer</c> that has the key provided</returns>
        public object ReadObject(object key)
        {
            this.Initialize();

            if (key == null || (key.GetType() != Type.GetType("Microsoft.Dynamics.Integration.Adapters.DynamicCrm.RelationshipData") && key.GetType() != typeof(Guid)))
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, "The type of the object supplied to the ReadObject method is not of type Key or RelationshipData."), new ArgumentException(Resources.SuppliedKeyTypeExceptionMessage, "key")) { ExceptionId = ErrorCodes.SuppliedKeyCastException };
            }

            if (key.GetType() == typeof(Guid))
            {
                Guid dynamicKey = (Guid)key;
                Entity target = new Entity() { Id = dynamicKey, LogicalName = this.ProvidedEntityName };
                Dictionary<string, object> returnedDictionary = this.RetrieveEntityAsDictionary(target);

                return returnedDictionary;
            }
            else
            {
                Dictionary<string, object> returnedDictionary = new Dictionary<string, object>();
                returnedDictionary.Add("Record", key);

                return returnedDictionary;
            }
        }

        /// <summary>
        /// Gets an <c>ICollection</c> containing <c>Key</c> objects that match the supplied modifiedDate
        /// </summary>
        /// <param name="modifiedDate">A <c>DateTime</c> containing the last modified date for the object keys to be returned</param>
        /// <returns>An <c>ICollection</c> containing keys that can be used to retrieve instances of the provided object from the source system</returns>
        public ICollection ReadObjectKeys(DateTime modifiedDate)
        {
            this.Initialize();
            var entitysMetaDataRequest = new RetrieveAllEntitiesRequest();
            entitysMetaDataRequest.EntityFilters = EntityFilters.Relationships;
            entitysMetaDataRequest.RetrieveAsIfPublished = true;
            var entitysMetaDataResponse = this.CrmAdapter.OrganizationService.Execute(entitysMetaDataRequest) as RetrieveAllEntitiesResponse;
            List<ManyToManyRelationshipMetadata> entityMToMRelationships = new List<ManyToManyRelationshipMetadata>();

            foreach (EntityMetadata crmMetadata in entitysMetaDataResponse.EntityMetadata)
            {
                entityMToMRelationships = (from meta in crmMetadata.ManyToManyRelationships
                                           where meta.SchemaName == this.ProvidedEntityName
                                           select meta).ToList();

                if (entityMToMRelationships.Count > 0)
                {
                    break;
                }
            }

            if (entityMToMRelationships.Count > 0)
            {
                string strEntityRelationshipName = entityMToMRelationships[0].IntersectEntityName;
                string entity1Name = entityMToMRelationships[0].Entity1LogicalName;
                string entity1IntersectAtt = entityMToMRelationships[0].Entity1IntersectAttribute;
                string entity2Name = entityMToMRelationships[0].Entity2LogicalName;
                string entity2IntersectAtt = entityMToMRelationships[0].Entity2IntersectAttribute;

                var relationshipMetaDataResponse = this.AssociatedRetrival(strEntityRelationshipName, entity1Name, entity1IntersectAtt);

                List<RelationshipData> keyList = new List<RelationshipData>();
                if (relationshipMetaDataResponse.EntityCollection.Entities.Count > 0)
                {
                    Dictionary<string, object> returnedDictionary = new Dictionary<string, object>();
                    string aliaseEntity1IntersectAtt = "aa." + entity1IntersectAtt;
                    string aliaseEntity2IntersectAtt = "aa." + entity2IntersectAtt;

                    foreach (Entity entity in relationshipMetaDataResponse.EntityCollection.Entities)
                    {
                        Dictionary<string, object> entityDictionary = new Dictionary<string, object>();
                        entityDictionary.Add(entity1IntersectAtt, entity.GetAttributeValue<AliasedValue>(aliaseEntity1IntersectAtt).Value);
                        entityDictionary.Add(entity2IntersectAtt, entity.GetAttributeValue<AliasedValue>(aliaseEntity2IntersectAtt).Value);

                        entityDictionary.Add("ProvidedEntityName", this.ProvidedEntityName);
                        entityDictionary.Add("EntityRelationshipName", strEntityRelationshipName);
                        entityDictionary.Add("Entity1Name", entity1Name);
                        entityDictionary.Add("Entity1IntersectAtt", entity1IntersectAtt);
                        entityDictionary.Add("Entity2Name", entity2Name);
                        entityDictionary.Add("Entity2IntersectAtt", entity2IntersectAtt);

                        RelationshipData relationshipData = new RelationshipData();
                        relationshipData.ReturnedDictionary = entityDictionary;
                        keyList.Add(relationshipData);
                    }

                    return keyList.ToArray();
                }
            }

            return this.IsActivityEntity != true ? this.GetModifiedEntityKeys(modifiedDate, this.ProvidedEntityName + "id") : this.GetModifiedEntityKeys(modifiedDate, "activityid");
        }

        /// <summary>
        /// Gets an <c>ICollection</c> containing <c>Key</c> objects that match the supplied modifiedDate.
        /// </summary>
        /// <param name="modifiedDate">A <c>DateTime</c> containing the last modified date for the object keys to be returned.</param>
        /// <returns>An <c>ICollection</c> of keys for entities that have been deleted in the source system.</returns>
        public ICollection ReadDeletedObjectKeys(DateTime modifiedDate)
        {
            this.Initialize();

            // Currently the dynamic object provider does not support the retrieval of deleted entities
            var entitysMetaDataRequest = new RetrieveAllEntitiesRequest();
            entitysMetaDataRequest.EntityFilters = EntityFilters.Relationships;
            entitysMetaDataRequest.RetrieveAsIfPublished = true;
            var entitysMetaDataResponse = this.CrmAdapter.OrganizationService.Execute(entitysMetaDataRequest) as RetrieveAllEntitiesResponse;
            List<ManyToManyRelationshipMetadata> entityMToMRelationships = new List<ManyToManyRelationshipMetadata>();

            foreach (EntityMetadata crmMetadata in entitysMetaDataResponse.EntityMetadata)
            {
                entityMToMRelationships = (from meta in crmMetadata.ManyToManyRelationships
                                           where meta.SchemaName == this.ProvidedEntityName
                                           select meta).ToList();

                if (entityMToMRelationships.Count > 0)
                {
                    break;
                }
            }

            if (entityMToMRelationships.Count > 0)
            {
                return this.GetDissassociateRelationship(modifiedDate);
            }

            return this.GetDeletedEntityKeys(modifiedDate);
        }

        protected override OrganizationRequest GetSetStateRequest(int stateToSet, int statusToSet, Guid entityId)
        {
            OrganizationRequest returnedRequest = new OrganizationRequest();
            Entity requestEntity = new Entity() { Id = entityId };
            if (this.ProvidedEntityName == "opportunity")
            {
                requestEntity.LogicalName = "opportunityclose";
                returnedRequest.Parameters["OpportunityClose"] = requestEntity;
                if (stateToSet == 2)
                {
                    returnedRequest.RequestName = "LoseOpportunity";
                }

                if (stateToSet == 1)
                {
                    returnedRequest.RequestName = "WinOpportunity";
                }

                return returnedRequest;
            }

            if (this.ProvidedEntityName == "quote")
            {
                requestEntity.LogicalName = "quoteclose";
                returnedRequest.Parameters["QuoteClose"] = requestEntity;
                if (stateToSet == 2)
                {
                    returnedRequest.RequestName = "WinQuote";
                }

                if (stateToSet == 3)
                {
                    returnedRequest.RequestName = "CloseQuote";
                }

                return returnedRequest;
            }

            return base.GetSetStateRequest(stateToSet, statusToSet, entityId);
        }

        private void Initialize()
        {
            this.ProvidedEntityName = this.ObjectDefinition.RootDefinition.TypeDefinition.Name;
            var field = this.ObjectDefinition.RootDefinition.TypeDefinition.Children.FirstOrDefault(fd => fd.Name == "activityid" && fd.TypeName == "System.Guid");
            if (field != null)
            {
                this.IsActivityEntity = true;
            }
        }

        private void CreateAssociatedRequest(string relationshipName, string entity1Name, string entity1Guid, string entity2Name, string entity2Guid)
        {
            AssociateRequest areq = new AssociateRequest();

            // Target is the entity that you are associating your entities to.
            areq.Target = new EntityReference(entity1Name, new Guid(entity1Guid));

            // RelatedEntities are the entities you are associating to your target (can be more than 1)
            areq.RelatedEntities = new EntityReferenceCollection();
            areq.RelatedEntities.Add(new EntityReference(entity2Name, new Guid(entity2Guid)));

            // The relationship schema name in CRM you are using to associate the entities. 
            // Found in settings - customization - entity - relationships
            areq.Relationship = new Relationship(relationshipName);

            // Execute the request
            this.CrmAdapter.OrganizationService.Execute(areq);
        }

        private RetrieveMultipleResponse AssociatedRetrival(string strEntityRelationshipName, string entity1Name, string entity1IntersectAtt)
        {
            // Setup Fetch XML.
            StringBuilder linkFetch = new StringBuilder();
            linkFetch.Append("<fetch version=\"1.0\" output-format=\"xml-platform\" mapping=\"logical\" distinct=\"true\">");
            linkFetch.Append("<entity name=\"" + entity1Name + "\">");
            linkFetch.Append("<all-attributes />");
            linkFetch.Append("<link-entity name=\"" + strEntityRelationshipName + "\" from=\"" + entity1IntersectAtt + "\" to=\"" + entity1IntersectAtt + "\" visible=\"false\" intersect=\"true\" alias=\"aa\">");
            linkFetch.Append("<all-attributes />");
            linkFetch.Append("</link-entity>");
            linkFetch.Append("</entity>");
            linkFetch.Append("</fetch>");

            // Build fetch request and obtain results.
            RetrieveMultipleRequest efr = new RetrieveMultipleRequest()
            {
                Query = new FetchExpression(linkFetch.ToString())
            };

            var entityMetaDataResponse = this.CrmAdapter.OrganizationService.Execute(efr) as RetrieveMultipleResponse;
            return entityMetaDataResponse;
        }

        private ICollection GetDissassociateRelationship(DateTime modifiedDate)
        {
            RetrieveMultipleRequest retrieveRequest = new RetrieveMultipleRequest();
            var query = new QueryExpression("audit");
            query.ColumnSet = new ColumnSet(true);
            query.Criteria.AddCondition(new ConditionExpression("createdon", ConditionOperator.GreaterEqual, modifiedDate));
            query.Criteria.AddCondition(new ConditionExpression("action", ConditionOperator.Equal, 34));
            query.PageInfo = new PagingInfo();
            query.PageInfo.Count = 5000;
            query.PageInfo.PageNumber = 1;
            query.PageInfo.PagingCookie = null;

            retrieveRequest.Query = query;
            var retrievedEntities = this.GetDissassociateRelationshipKeys(retrieveRequest);

            var entitysMetaDataRequest = new RetrieveAllEntitiesRequest();
            entitysMetaDataRequest.EntityFilters = EntityFilters.Relationships;
            entitysMetaDataRequest.RetrieveAsIfPublished = true;
            var entitysMetaDataResponse = this.CrmAdapter.OrganizationService.Execute(entitysMetaDataRequest) as RetrieveAllEntitiesResponse;

            List<ManyToManyRelationshipMetadata> entityMToMRelationships = new List<ManyToManyRelationshipMetadata>();
            foreach (EntityMetadata crmMetadata in entitysMetaDataResponse.EntityMetadata)
            {
                entityMToMRelationships = (from meta in crmMetadata.ManyToManyRelationships
                                           where meta.SchemaName == this.ProvidedEntityName
                                           select meta).ToList();

                if (entityMToMRelationships.Count > 0)
                {
                    break;
                }
            }

            if (entityMToMRelationships.Count > 0)
            {
                string strEntityRelationshipName = entityMToMRelationships[0].IntersectEntityName;
                string entity1Name = entityMToMRelationships[0].Entity1LogicalName;
                string entity1IntersectAtt = entityMToMRelationships[0].Entity1IntersectAttribute;
                string entity2Name = entityMToMRelationships[0].Entity2LogicalName;
                string entity2IntersectAtt = entityMToMRelationships[0].Entity2IntersectAttribute;

                List<RelationshipData> keyList = new List<RelationshipData>();
                foreach (Entity crmEntity in retrievedEntities)
                {
                    string transactionID = crmEntity.Attributes["transactionid"].ToString();
                    var retrievedTransactionEntities = (from meta in retrievedEntities
                                                        where meta.Attributes["transactionid"].ToString() == transactionID.ToString()
                                                        select meta).ToList();

                    Dictionary<string, object> entityDictionary = new Dictionary<string, object>();
                    if (retrievedTransactionEntities.Count < 2)
                    {
                        continue;
                    }

                    bool entity1NameID = false;
                    bool entity2NameID = false;
                    foreach (Entity crmTransEntity in retrievedTransactionEntities)
                    {
                        EntityReference objectID = (EntityReference)crmTransEntity.Attributes["objectid"];

                        if (objectID.LogicalName == entity1Name)
                        {
                            entityDictionary.Add(entity1IntersectAtt, objectID.Id.ToString());
                            entity1NameID = true;
                        }

                        if (objectID.LogicalName == entity2Name)
                        {
                            entityDictionary.Add(entity2IntersectAtt, objectID.Id.ToString());
                            entity2NameID = true;
                        }
                    }

                    if (!entity1NameID || !entity2NameID)
                    {
                        continue;
                    }

                    entityDictionary.Add("ProvidedEntityName", this.ProvidedEntityName);
                    entityDictionary.Add("EntityRelationshipName", strEntityRelationshipName);
                    entityDictionary.Add("Entity1Name", entity1Name);
                    entityDictionary.Add("Entity1IntersectAtt", entity1IntersectAtt);
                    entityDictionary.Add("Entity2Name", entity2Name);
                    entityDictionary.Add("Entity2IntersectAtt", entity2IntersectAtt);

                    RelationshipData relationshipData = new RelationshipData();
                    relationshipData.ReturnedDictionary = entityDictionary;
                    keyList.Add(relationshipData);
                }

                return keyList;
            }

            return null;
        }

        private List<Entity> GetDissassociateRelationshipKeys(RetrieveMultipleRequest request)
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

        private void CreateDisassociateRequest(string relationshipName, string entity1Name, string entity1Guid, string entity2Name, string entity2Guid)
        {
            DisassociateRequest dreq = new DisassociateRequest();

            // Target is the entity that you are disassociating your entities with.
            dreq.Target = new EntityReference(entity1Name, new Guid(entity1Guid));

            // RelatedEntities are the entities you are disassociating to your target (can be more than 1)
            dreq.RelatedEntities = new EntityReferenceCollection();
            dreq.RelatedEntities.Add(new EntityReference(entity2Name, new Guid(entity2Guid)));

            // The relationship schema name in CRM you are using to disassociate the entities. 
            // Found in settings - customization - entity - relationships
            dreq.Relationship = new Relationship(relationshipName);

            // Execute the request
            this.CrmAdapter.OrganizationService.Execute(dreq);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RelationshipData"/> class.
    /// </summary>
    public class RelationshipData
    {
        public Dictionary<string, object> ReturnedDictionary;
    }
}
