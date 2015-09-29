using System;
using System.Reflection;

namespace BackgroundJob.Core.Serialization
{
    public interface IJobActivator
    {
        //object ActivateJob(Type jobType);
        object ActivateJob(Type jobType, MethodInfo method, object[] deserializedArguments);
    }
}