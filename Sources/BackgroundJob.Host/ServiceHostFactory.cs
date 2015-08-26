using System.ServiceModel;
using Microsoft.Practices.Unity;
using Unity.Wcf;

namespace BackgroundJob.Host
{
    public class ServiceHostFactory
    {
        private readonly IUnityContainer _container;

        public ServiceHostFactory(IUnityContainer container)
        {
            _container = container;
        }

        public ServiceHost Create()
        {
            return new UnityServiceHost(_container, typeof(EnqueueService));
        }
    }
}