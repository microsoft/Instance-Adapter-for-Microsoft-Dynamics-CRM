using System;

namespace Microsoft.Dynamics.Integration.Adapters.DynamicCrm.Configuration
{
    internal class ExceptionThrownEventArgs : EventArgs
    {
        public Exception Exception
        {
            get;
            set;
        }

        public bool Handled
        {
            get;
            set;
        }
    }
}
