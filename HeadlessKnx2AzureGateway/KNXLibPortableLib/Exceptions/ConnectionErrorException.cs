namespace KNXLibPortableLib.Exceptions
{
    using System;
    using System.Net;

    public class ConnectionErrorException : Exception
    {
        public ConnectionErrorException(IPEndPoint configuration)
            : base(string.Format("ConnectionErrorException: Error connecting to {0}:{1}", configuration.Address, configuration.Port))
        {
        }

        public ConnectionErrorException(IPEndPoint configuration, Exception innerException)
            : base(string.Format("ConnectionErrorException: Error connecting to {0}:{1}", configuration.Address, configuration.Port), innerException)
        {
        }

        public override string ToString()
        {
            return this.Message;
        }
    }
}