using System;

namespace BackgroundJob.Core.Serialization
{
    public class JobFailedException : Exception
    {
        public JobFailedException(string message, Exception innerException):base(message, innerException)
        {
        }
    }
}