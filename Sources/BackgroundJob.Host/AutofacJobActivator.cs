using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Monads;
using System.Reflection;
using Autofac;
using BackgroundJob.Core;
using BackgroundJob.Core.Serialization;

namespace BackgroundJob.Host
{
    internal class AutofacJobActivator : IJobActivator
    {
        private readonly ILifetimeScope _baseScope;
        private readonly ConcurrentDictionary<string, Action<ContainerBuilder>> _jobContainerBuilders;

        
        public AutofacJobActivator(ILifetimeScope baseScope)
        {
            _baseScope = baseScope;
            _jobContainerBuilders = new ConcurrentDictionary<string, Action<ContainerBuilder>>();
        }
        
        public object ActivateJob(Type jobType, MethodInfo method, object[] deserializedArguments)
        {
            using (var scope = GetScopeForAssembly(jobType))
            {
                var jobObject = scope.Resolve(jobType);
                try
                {
                    return method.Invoke(jobObject, deserializedArguments);
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException is OperationCanceledException)
                        throw ex.InnerException;
                    throw new JobFailedException("При выполнении задачи произошла ошибка", ex.InnerException);
                }
            }
        }

        public ILifetimeScope GetScopeForAssembly(Type jobType)
        {
            var jobName = jobType.FullName;
            Action<ContainerBuilder> scopeConfig;
            if (!_jobContainerBuilders.TryGetValue(jobName, out scopeConfig))
            {
                scopeConfig = _jobContainerBuilders.GetOrAdd(jobName, InitChildContainer(jobType));
            }
            return _baseScope.BeginLifetimeScope(scopeConfig);
        }

        public static Action<ContainerBuilder> InitChildContainer(Type jobType)
        {
            var configurerType = jobType.CustomAttributes.FirstOrDefault(c => c.AttributeType.IsAssignableTo<ContainerConfigurerTypeAttribute>()).With(c=>c.AttributeType);            
            if (configurerType == null)
            {
                var jobAssembly = jobType.Assembly;
                configurerType = jobAssembly.GetTypes().SingleOrDefault(t => typeof (IBackgroundJobConfigurer).IsAssignableFrom(t));
            }
            if (configurerType == null)
                return c=>{};
            var containerConfigurer = Activator.CreateInstance(configurerType) as IBackgroundJobConfigurer;
            return containerConfigurer == null
                ? (Action<ContainerBuilder>) (c => { })
                : (c => { containerConfigurer.ConfigureContainer(c); });
        }
    }
}