using Autofac;
using BackgroundJob.Core;
using NLog;
using NLog.Config;
using Quantumart.SmsSubscription.DataModel.Managers;

namespace BackgroundJob.Jobs.ContentsAvailabilityMonitoring
{
    public class ContentsAvailabilityConfigurerAttribute : ContainerConfigurerTypeAttribute
    {
        private static Logger GetLogger()
        {
            const string name = "ContentsAvailabilityMonitor";
            if (LogManager.Configuration == null)
                LogManager.Configuration = new XmlLoggingConfiguration("\\NLog.config", true);
            return LogManager.GetLogger(name);
        }

        public override ContainerBuilder ConfigureContainer(ContainerBuilder childContainer)
        {
            childContainer.RegisterInstance(GetLogger()).SingleInstance();
            childContainer.RegisterType<ContentsAvailabilityMonitor>().As<IContentsAvailabilityMonitor>().InstancePerLifetimeScope();
            childContainer.Register(c=>new SpecialReportManager(300)).As<ISpecialReportManager>().InstancePerLifetimeScope();
            return childContainer;
        }
    }
}