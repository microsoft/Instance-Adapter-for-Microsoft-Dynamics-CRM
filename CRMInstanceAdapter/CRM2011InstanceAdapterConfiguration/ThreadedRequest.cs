using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using System;
using System.Globalization;
using System.ServiceModel;
using System.Text;

namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration
{
    /// <summary>
    /// Class used to wrap return data and parameters to the Crm web service calls so that they can be threaded more easily
    /// </summary>
    internal class ThreadedRequest
    {
        private object sync = new object();

        public event EventHandler<ExceptionThrownEventArgs> ExceptionThrown;

        #region Properties
        /// <summary>
        /// Gets or sets the <c>CRMAdapter</c> to use in calling the Crm platform
        /// </summary>
        internal DynamicCrmAdapter InstallAdapter
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the <c>DynmicEntities</c> to be dectiviated when calling the Deactivate method.
        /// </summary>
        internal Entity[] EntitiesToUpdate
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the attribute that is updated when renaming entities.
        /// </summary>
        internal string UpdatedEntityAttribute
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the <c>MetadataServiceRequest</c> to use when calling the crm metadata service
        /// </summary>
        internal OrganizationRequest Metarequest
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the <c>MetadataServiceResponse</c> to use when calling the crm metadata service
        /// </summary>
        internal OrganizationResponse Metaresponse
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the <c>ExportCompressedAllXmlResponse</c> from the <c>Request</c> to export all the current customizations in the crm system
        /// </summary>
        //internal ExportCompressedAllXmlResponse ExportResponse
        //{
        //    get;
        //    set;
        //}

        /// <summary>
        /// Gets or sets the array of <c>byte</c>s that contain the compressed customizations.xml file to be imported into the target crm system
        /// </summary>
        internal byte[] ImportFile
        {
            get;
            set;
        }
        #endregion

        /// <summary>
        /// Calls the Execute method on the Crm Metadata service using the Metadatarequest property's value for the request object
        /// </summary>
        /// <exception cref="SoapException">Thrown if there is a problem calling the Crm Metadata service</exception>
        internal void MetadataExecute()
        {
            try
            {
                lock (sync)
                {
                    this.Metaresponse = this.InstallAdapter.OrganizationService.Execute(this.Metarequest);
                }
            }
            catch (System.ServiceModel.FaultException e)
            {
                // The error code 0x80047013 means that this attribute already exists on the entity and is returned when we are doing a re-install,
                // and if the attribute cannot be deleted we just move on with the removal and eat the exception, the error code 0x8004f026 means that the solution already exists
                FaultException<OrganizationServiceFault> orgFault = e as FaultException<OrganizationServiceFault>;
                if (orgFault != null)
                {
                    // The error code 0x80047013 means that this attribute already exists on the entity and is returned when we are doing a re-install,
                    // and if the attribute cannot be deleted we just move on with the removal and eat the exception, the error code 0x8004f026 means that the solution already exists
                    if ((uint)orgFault.Detail.ErrorCode == 0x80047013 || (uint)orgFault.Detail.ErrorCode == 0x8004f026)
                    {
                        return;
                    }

                }


                if (!(this.Metarequest is DeleteAttributeRequest))
                {
                    if (!this.ThrowException(e).Handled)
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Calls the Crm Service Execute method with an <c>importCompressedAllXmlRequest</c> using the contents of the ImportFile property as the the <c>Byte</c>
        /// array to use when creating the request
        /// </summary>
        internal void Import()
        {
            ImportSolutionRequest request = new ImportSolutionRequest() { CustomizationFile = this.ImportFile, OverwriteUnmanagedCustomizations = true, PublishWorkflows = true };
            lock (this.sync)
            {
                try
                {
                    this.InstallAdapter.OrganizationService.Execute(request);
                }
                catch (Exception e)
                {
                    if (!this.ThrowException(e).Handled)
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Calls the Crm Service Execute method with a <c>PublishAllXmlRequest</c> to publish the changes that have been made
        /// </summary>
        internal void PublishMetadata()
        {
            lock (this.sync)
            {
                try
                {
                    this.InstallAdapter.OrganizationService.Execute(new OrganizationRequest() { RequestName = "PublishAllXml" });
                }
                catch (Exception e)
                {
                    if (!this.ThrowException(e).Handled)
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Calls the Crm Service Execute method with a <c>Request</c> to execute the changes that have been made
        /// </summary>
        internal void Deactivate()
        {
            lock (this.sync)
            {
                try
                {
                    foreach (Entity entity in this.EntitiesToUpdate)
                    {
                        if (((OptionSetValue)entity["statecode"]).Value != 1)
                        {
                            SetStateRequest request = new SetStateRequest() { EntityMoniker = new EntityReference(entity.LogicalName, entity.Id), State = new OptionSetValue(1), Status = new OptionSetValue(-1) };
                            this.InstallAdapter.OrganizationService.Execute(request);
                        }
                    }
                }
                catch (FaultException<OrganizationServiceFault> e)
                {
                    // This error code if for a failed priv check which is normally due to having already run the configuration
                    if (e.Message != string.Format(CultureInfo.CurrentCulture, Constants.PrivCheckErrorCode))
                    {
                        if (!this.ThrowException(e).Handled)
                        {
                            throw;
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!this.ThrowException(e).Handled)
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Calls the Crm Service Execute method with a <c>Request</c> to execute the changes that have been made
        /// </summary>
        internal void Update()
        {
            lock (this.sync)
            {
                try
                {
                    foreach (Entity entity in this.EntitiesToUpdate)
                    {
                        if (entity.Contains(this.UpdatedEntityAttribute) && ((OptionSetValue)entity["statecode"]).Value == 0 && ((EntityReference)entity["modifiedby"]).Id != this.InstallAdapter.IntegrationUserId)
                        {
                            int length = entity[this.UpdatedEntityAttribute].ToString().Length <= 60 ? entity[this.UpdatedEntityAttribute].ToString().Length : 60;
                            StringBuilder builder = new StringBuilder(entity[this.UpdatedEntityAttribute].ToString().Substring(0, length));
                            builder.Append(" (");
                            if (entity.Contains("createdon") && entity["createdon"] != null)
                            {
                                builder.Append(((DateTime?)entity["createdon"]).Value);
                            }

                            builder.Append(" - ");
                            builder.Append(DateTime.UtcNow.Date.ToShortDateString());
                            builder.Append(")");
                            Random random = new Random();
                            builder.Append(" " + random.Next(1, 1000).ToString(CultureInfo.CurrentCulture));
                            entity[this.UpdatedEntityAttribute] = builder.ToString();
                            if (entity.LogicalName == "pricelevel")
                            {
                                entity["enddate"] = DateTime.UtcNow;
                            }

                            this.InstallAdapter.OrganizationService.Update(entity);
                        }
                    }
                }
                catch (FaultException<OrganizationServiceFault> e)
                {
                    // This error code if for a failed priv check which is normally due to having already run the configuration
                    if (e.Message != string.Format(CultureInfo.CurrentCulture, Constants.PrivCheckErrorCode))
                    {
                        if (!this.ThrowException(e).Handled)
                        {
                            throw;
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!this.ThrowException(e).Handled)
                    {
                        throw;
                    }
                }
            }
        }

        private ExceptionThrownEventArgs ThrowException(Exception e)
        {
            ExceptionThrownEventArgs args = new ExceptionThrownEventArgs() { Exception = e };
            if (this.ExceptionThrown != null)
            {
                this.ExceptionThrown(this, args);
            }

            return args;
        }
    }
}
