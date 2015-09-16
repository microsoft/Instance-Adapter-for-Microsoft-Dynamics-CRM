namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration
{
    using System;

    /// <summary>
    /// Class to encapsulate exceptions being thrown in a spawned thread.
    /// </summary>
    internal class ExceptionThrownEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets the Exception for these event args.
        /// </summary>
        public Exception Exception
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the <c>Exception</c> was handled or not.
        /// </summary>
        public bool Handled
        {
            get;
            set;
        }
    }
}
