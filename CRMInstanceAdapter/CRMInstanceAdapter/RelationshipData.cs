namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm
{
    using System.Collections.Generic;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelationshipData"/> class.
    /// </summary>
    public class RelationshipData
    {
        /// <summary>
        /// Gets or sets the <c>Dictionary</c> that contains the relationship data.
        /// </summary>
        public Dictionary<string, object> ReturnedDictionary
        {
            get;            
            set;
        }
    }
}
