using Microsoft.Dynamics.Integration.Common;
using System;

namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration
{
    /// <summary>
    /// The <c>ConfigurationEventArgs</c> <c>class</c> is used to pass information to event handlers about the status of the configuration.
    /// </summary>
    internal sealed class ConfigurationEventArgs : EventArgs
    {
        /// <summary>
        /// Create a new instance of the <c>ConfigurationEventArgs</c> <c>class</c>.
        /// </summary>
        /// <param name="messageData">The data to pass to the handlers.</param>
        internal ConfigurationEventArgs(string messageData)
        {
            this.Message = messageData;
            this.Type = ConfigurationEventType.Event;
            TraceLog.Info(this.GetTypeName() + ": " + this.Message);
        }

        /// <summary>
        /// Gets or sets the message data.
        /// </summary>
        internal string Message
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the <c>ConfigurationEventType</c> for this event
        /// </summary>
        internal ConfigurationEventType Type
        {
            get;
            set;
        }

        private string GetTypeName()
        {
            return Enum.GetName(typeof(ConfigurationEventType), this.Type);
        }
    }
}
