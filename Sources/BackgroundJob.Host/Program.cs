using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.ServiceProcess;
using Autofac;
using BackgroundJob.Configuration;
using BackgroundJob.Core;
using BackgroundJob.Core.Serialization;
using BackgroundJob.Host.Quartz;
using NLog;
using NLog.Config;
using NLog.Targets;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;

namespace BackgroundJob.Host
{
    class Program
    {
        public const string ServiceName = "BackgroundJob.Host";

        static void Main(string[] args)
        {
            var container = CreateDependencyContainer();
            
            if (Environment.UserInteractive)
            {
                try
                {
                    var service = container.Resolve<Service>();
                    service.StartImpl();
                    Console.WriteLine("Нажмите ENTER для остановки сервиса...");
                    Console.ReadLine();
                    service.StopImpl();
                    Console.WriteLine("Сервис остановлен. Нажмите ENTER");
                    Console.ReadLine();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Произошла ошибка: {0}", e.Message);
                    Console.WriteLine("StackTrace: {0}", e.StackTrace);
                    Console.WriteLine("Нажмите ENTER для завершения...");
                    Console.ReadLine();
                }
            }
            else
            {
                var servicesToRun = new ServiceBase[] { container.Resolve<Service>() };
                ServiceBase.Run(servicesToRun);
            }
        }
        
        public static void SomeTestingAction(int[] parameters, decimal divider, string name)
        {
            var result = parameters.Sum(i => i*2)/divider;
            Console.WriteLine("{0}: {1}",name, result);
        }

        private static Logger GetLogger()
        {
            var logConfig = LogManager.Configuration??new XmlLoggingConfiguration("NLog.config", true);

                var consoleTarget = new ColoredConsoleTarget
                {
                    Layout =
                        "${date} ${level:uppercase=true} ${event-context:item=context} ${message}${onexception:${exception:format=tostring}}"
                };

                logConfig.AddTarget("console", consoleTarget);
                logConfig.LoggingRules.Add(new LoggingRule("Host", LogLevel.Trace, consoleTarget));

                LogManager.Configuration = logConfig;

            return LogManager.GetLogger("Host");
        }

        internal static IContainer CreateDependencyContainer()
        {
            var logger = GetLogger();
            var container = new ContainerBuilder();
            container.RegisterInstance<ISchedulerFactory>(new StdSchedulerFactory());
            container.RegisterInstance(logger);
            var jobConfigurations = ((JobConfigurations)ConfigurationManager.GetSection("jobSettings")).Jobs.Cast<JobConfiguration>().ToArray();
            container.RegisterInstance(jobConfigurations.Select(c => new DbScheduleJobConfiguration(c.Name, c.Type, c.SchedulingTime, c.QueueName, c.MaxReplay)).Cast<IJobConfiguration>());
            container.RegisterType<AutofacJobActivator>().As<IJobActivator>();
            container.RegisterType<EnqueueService>().As<IEnqueueService>();
            container.RegisterType<WcfServiceHostFactory>();
            container.RegisterType<Service>();
            RegisterEnqueuers(jobConfigurations, container);
            var settings = Settings.Default;
            container.Register(c=>new SmtpService(settings.NotificationSmtpHost,
                settings.NotificationSmtpUser, settings.NotificationSmtpPassword, settings.NotificationFrom)).As<ISmtpService>();

            container.RegisterType<AutofacQuartzJobFactory>().As<IJobFactory>().InstancePerLifetimeScope();
            container.RegisterType<AutofacQuartzJobFactory>().As<IEnqueuerFactory>().InstancePerLifetimeScope();
            container.RegisterGeneric(typeof (RecurringJobWrapper<>)).InstancePerDependency();

            return container.Build();
        }

        private static void RegisterEnqueuers(IEnumerable<JobConfiguration> jobConfigurations, ContainerBuilder container)
        {
            foreach (var type in jobConfigurations.Select(c => c.Type))
            {
                container.RegisterType(Type.GetType(type));
            }
        }
    }
}
