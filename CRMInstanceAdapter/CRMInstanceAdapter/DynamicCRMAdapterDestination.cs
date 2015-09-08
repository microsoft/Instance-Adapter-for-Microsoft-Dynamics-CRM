namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm
{
    using AdapterAbstractionLayer;
    using Common;
    using ObjectProviders;
    using Properties;
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using System.Xml.Serialization;

    /// <summary>
    /// This adapter is used to connect to a destination Microsoft Dynamics CRM system.
    /// </summary>
    [Adapter]
    public class DynamicCrmAdapterDestination : DynamicCrmAdapter
    {
        private const string Version = "2011";
        private const string ObjectConfigFolderName = "ObjectConfig\\DynamicCRMAdapterDestination";
        private readonly Guid adapterId = new Guid("93b89c42-91af-49de-a9da-15d3318fb430");
        private System.Collections.ObjectModel.Collection<ObjectProvider> providers;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicCrmAdapterDestination"/> class.
        /// </summary>
        public DynamicCrmAdapterDestination()
        {
            this.DisplayName = Resources.DynamicCRMDestinationAdapterDisplayName;
            this.Id = this.adapterId;
            this.Name = Resources.DynamicCRMDestinationAdapterName;
        }

        /// <summary>
        /// Gets the collection of reader <see cref="ObjectProvider"/>s for the Microsoft Dynamics CRM source adapter.
        /// </summary>
        public override System.Collections.ObjectModel.Collection<ObjectProvider> ObjectReaderProviders
        {
            get
            {
                System.Collections.ObjectModel.Collection<ObjectProvider> readers = base.ObjectReaderProviders;
                return readers;
            }
        }

        /// <summary>
        /// Gets the collection of writer <see cref="ObjectProvider"/>s for the Microsoft Dynamics CRM source adapter.
        /// </summary>
        public override System.Collections.ObjectModel.Collection<ObjectProvider> ObjectWriterProviders
        {
            get
            {
                System.Collections.ObjectModel.Collection<ObjectProvider> writers = base.ObjectWriterProviders;
                this.GetObjectProviders().ForEach(op => writers.Add(op));
                return writers;
            }
        }

        /// <summary>
        /// Retrieves an <see cref="ObjectProvider"/> based on the provider binder.
        /// </summary>
        /// <param name="binder">The <see cref="ObjectProviderBinder"/> to retrieve the provider for.</param>
        /// <returns>An <see cref="ObjectProvider"/> base on the binder supplied.</returns>
        public override ObjectProvider GetObjectProvider(ObjectProviderBinder binder)
        {
            if (binder == null)
            {
                throw new AdapterException(string.Format(CultureInfo.CurrentCulture, Resources.ArgumentNullExceptionMessage), new ArgumentNullException("binder")) { ExceptionId = AdapterException.SystemExceptionGuid };
            }

            if (binder.ObjectProviderProxy.ObjectDefinition != null && !IsStaticObjectProvider(binder.ObjectProviderProxy.ObjectDefinition))
            {
                return this.GetObjectProviders().SingleOrDefault(p => p.Id == GetDynamicProviderId(binder.ObjectProviderProxy.ObjectDefinition));
            }

            return base.GetObjectProvider(binder);
        }

        /// <summary>
        /// Gets the collection of <see cref="ObjectProvider"/>s associated with this <see cref="Adapter"/>.
        /// </summary>
        /// <returns>A generic collection of <see cref="ObjectProvider"/>s.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "The number of files is unknown at design time.")]
        private System.Collections.ObjectModel.Collection<ObjectProvider> GetObjectProviders()
        {
            this.providers = new System.Collections.ObjectModel.Collection<ObjectProvider>();
            ObjectDefinition objDef = null;

            // This will need to be modified to reflect utilizing a different directory
            foreach (string configFileName in Directory.GetFiles(this.GetConfigPath<DynamicCrmAdapterDestination>()))
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

                if (objDef.RootDefinition.Name.Equals("OptionList"))
                {
                    PicklistObjectProvider pickObject = new PicklistObjectProvider() { Adapter = this };
                    this.providers.Add(pickObject);
                }
            }

            return this.providers;
        }
    }
}
