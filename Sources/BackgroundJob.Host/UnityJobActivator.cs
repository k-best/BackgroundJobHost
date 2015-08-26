using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BackgroundJob.Core;
using Microsoft.Practices.Unity;

namespace BackgroundJob.Host
{
    internal class UnityJobActivator : IJobActivator
    {
        private readonly IUnityContainer _baseContainer;
        private readonly ConcurrentDictionary<string, IUnityContainer> _jobContainers;


        public UnityJobActivator(IUnityContainer container)
        {
            _baseContainer = container;
            _jobContainers = new ConcurrentDictionary<string, IUnityContainer>();
        }

        public UnityJobActivator(IUnityContainer container, Dictionary<string, IUnityContainer> jobContainers)
        {
            _baseContainer = container;
            _jobContainers = new ConcurrentDictionary<string, IUnityContainer>(jobContainers);
        }

        public object ActivateJob(Type jobType)
        {
            var jobAssembly = jobType.Assembly;
            var container = GetContainerForAssembly(jobAssembly);
            return container.Resolve(jobType);
        }

        private IUnityContainer GetContainerForAssembly(Assembly jobAssembly)
        {
            var assemblyName = jobAssembly.FullName;
            return _jobContainers.GetOrAdd(assemblyName, c=>InitChildContainer(_baseContainer, jobAssembly));
        }

        public static IUnityContainer InitChildContainer(IUnityContainer baseContainer, Assembly jobAssembly)
        {
            var configurerType =jobAssembly.GetTypes().SingleOrDefault(t => typeof (IBackgroundJobConfigurer).IsAssignableFrom(t));
            IUnityContainer container = baseContainer.CreateChildContainer();
            if (configurerType == null)
                return container;
            var containerConfigurer = Activator.CreateInstance(configurerType) as IBackgroundJobConfigurer;
            return containerConfigurer == null ? container : containerConfigurer.ConfigureContainer(container);
        }
    }

    public interface IJobActivator
    {
        object ActivateJob(Type jobType);
    }
}