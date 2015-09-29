using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using Autofac;

namespace BackgroundJob.Host
{
    public class WcfServiceHostFactory
    {
        private readonly IComponentContext _container;

        public WcfServiceHostFactory(IComponentContext container)
        {
            _container = container;
        }

        public ServiceHost Create()
        {
            return new WcfServiceHost(_container, typeof(EnqueueService));
        }
    }

    public class WcfServiceHost : ServiceHost
    {
        public WcfServiceHost(IComponentContext container, Type serviceType, params Uri[] baseAddresses):base(serviceType, baseAddresses)
        {
            if (container == null)
                throw new ArgumentNullException("container");
            var contracts = ImplementedContracts.Values;
            foreach (var contractDescription in contracts)
            {
                var instanceProvider = new WcfInstanceProvider(container);
                contractDescription.Behaviors.Add(instanceProvider);
            }

        }
    }

    public class WcfInstanceProvider : IContractBehavior, IInstanceProvider
    {
        private readonly IComponentContext _container;

        public WcfInstanceProvider(IComponentContext container)
        {
            _container = container;
        }

        public void Validate(ContractDescription contractDescription, ServiceEndpoint endpoint)
        {
        }

        public void ApplyDispatchBehavior(ContractDescription contractDescription, ServiceEndpoint endpoint,
            DispatchRuntime dispatchRuntime)
        {
            dispatchRuntime.InstanceProvider = this;
        }

        public void ApplyClientBehavior(ContractDescription contractDescription, ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
        }

        public void AddBindingParameters(ContractDescription contractDescription, ServiceEndpoint endpoint,
            BindingParameterCollection bindingParameters)
        {
        }

        public object GetInstance(InstanceContext instanceContext)
        {
            return _container.Resolve(typeof (EnqueueService));
        }

        public object GetInstance(InstanceContext instanceContext, Message message)
        {
            return GetInstance(instanceContext);
        }

        public void ReleaseInstance(InstanceContext instanceContext, object instance)
        {
            
        }
    }
}