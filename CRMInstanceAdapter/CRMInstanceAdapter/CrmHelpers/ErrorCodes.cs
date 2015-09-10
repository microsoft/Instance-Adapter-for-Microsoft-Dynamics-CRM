namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm
{
    using System;

    /// <summary>
    /// The <c>ErrorCodes</c> class holds a collection of well known error codes for CRM integrations.
    /// </summary>
    internal static class ErrorCodes
    {
        internal static Guid PicklistMappingNotFound = new Guid("{0EFCB77C-A3BA-4b31-9158-BF85CBF4BDBA}");
        internal static Guid InvalidDictionary = new Guid("{634E659D-104D-48a2-A587-47D51A8DE18E}");
        internal static Guid CrmPlatformException = new Guid("{F809D1C1-075F-46e3-9A2E-2A05695916C7}");
        internal static Guid MultipleCustomerAddressResult = new Guid("{B254198B-6DF5-4939-B19B-29718107F243}");
        internal static Guid QueryEntityNameEmpty = new Guid("{1B32BC91-4AE5-4e6d-8701-89769C02334E}");
        internal static Guid QueryPropertyNameEmpty = new Guid("{517653C2-6F7C-4b97-AB7B-EF946C5F7E76}");
        internal static Guid MultipleEntityResult = new Guid("{F7E7F1F1-06F9-4336-8FC5-2A0E033969A0}");
        internal static Guid EmptyEntityName = new Guid("{F6E20C03-C378-4ec5-9A21-B17C97D6F6AB}");
        internal static Guid InvalidComplexType = new Guid("{BF38D3B9-4244-4574-A157-C3B60F937B39}");
        internal static Guid InvalidDictionaryCast = new Guid("{92E6356B-CF5A-4cae-8FC5-051E61445251}");
        internal static Guid InvalidGlobalOptionSet = new Guid("{92E6356B-CF5A-4cae-8FC5-051E61445252}");
        internal static Guid StateSettingError = new Guid("{D662CB2A-0ADA-4784-8EAB-E4D24BC55F51}");
        internal static Guid DeleteResponseMultipleResult = new Guid("{CE2938B7-E2AB-4031-9F24-704207A2B778}");
        internal static Guid AdapterCast = new Guid("{656FC7EE-81C4-450b-B8C7-2469BAB1BE60}");
        internal static Guid OrganizationNotFound = new Guid("{DC2CB60C-8DEE-468e-A0BF-574020344E00}");
        internal static Guid UpdateRequestException = new Guid("{DD9D1D0C-28FD-49e1-AA4A-B460C39EF47E}");
        internal static Guid CreateRequestException = new Guid("{ACDD2936-CA7B-4ede-A600-357D0E52B7CC}");
        internal static Guid SuppliedKeyCastException = new Guid("{FF75787D-0C9F-45e5-8812-34004A4632CA}");
        internal static Guid BinaryComparisonNotSupported = new Guid("{F77046A7-0B76-4052-A587-52F5E6AD038B}");
        internal static Guid NullICriterion = new Guid("{9EF91998-F236-4a9a-9C91-5DB33DE86DE6}");
        internal static Guid DuplicateDetected = new Guid("{24FFEC7D-9395-40fb-BD82-A1901F383C89}");
        internal static Guid DeleteRequestException = new Guid("{A20C36C5-EB2E-4e07-B98F-CE1D18210CE5}");
        internal static Guid CancelOrderRequestException = new Guid("{AED3BAB0-FF4B-40d3-9817-4E721F1C4BDA}");
        internal static Guid CrmTypeNotSupportedException = new Guid("{3A032288-3492-4b25-863E-F58F3DC82446}");
        internal static Guid AssignOwnerRequestException = new Guid("{9B7653B5-9561-48ae-9C6D-5DF48F4B47C9}");
        internal static Guid ProductNotFound = new Guid("{352C0DBB-69C2-4d05-91DA-6D75BB68E69F}");
        internal static Guid RetrieveAllException = new Guid("{F095F4F8-9971-4901-88F6-040C506C3D70}");
        internal static Guid RetrieveEntityException = new Guid("{D73A5A92-A72E-4C2F-BF00-FA4B42E9A4A2}");
        internal static Guid PicklistMetadataRetrieval = new Guid("{8123A72A-7126-4CBF-9895-B63961CE2BA1}");
        internal static Guid PicklistMetadataCreation = new Guid("{9053722C-E68F-48CD-994B-7D205649C75D}");
        internal static Guid MetadataPublishException = new Guid("{75B19927-6F41-46FE-BF3E-189267619F52}");
        internal static Guid AddressIntegrationKeyPropertyException = new Guid("{4EAA5F0D-9A3C-42B1-AB6B-DC560F65FD77}");
        internal static Guid CrmOnlineUserConfigurationException = new Guid("{FE4C5CFD-9A7A-4EB6-8EEC-E718B4774DFE}");
        internal static Guid NegativeValueSupplied = new Guid("{C18B062B-C03A-4FEB-8512-BFB0462B9AD4}");
        internal static Guid SecurityTokenException = new Guid("{06D92091-0021-42DC-9930-C3D9C1E6924C}");
    }
}
