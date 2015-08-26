using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using BackgroundJob.Configuration;
using BackgroundJob.Core;
using BackgroundJob.Host.Example;
using BackgroundJob.Host.Quartz;
using Microsoft.Practices.Unity;
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
                    Console.WriteLine("Сервис останавливается");
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
        
        private static Dictionary<string, IUnityContainer> GetModuleContainers(IUnityContainer baseContainer)
        {
            var dictionary = new Dictionary<string, IUnityContainer>
            {
                {
                    typeof (ITestJob).Assembly.FullName,
                    new ContainerConfigurer().ConfigureContainer(baseContainer.CreateChildContainer())
                },
            };

            return dictionary;
        }

        public static void SomeTestingAction(int[] parameters, decimal divider, string name)
        {
            var result = parameters.Sum(i => i*2)/divider;
            Console.WriteLine("{0}: {1}",name, result);
        }

        private static Logger GetLogger()
        {
            var logConfig = new LoggingConfiguration();

            var consoleTarget = new ColoredConsoleTarget
            {
                Layout =
                    "${date} ${level:uppercase=true} ${event-context:item=context} ${message}${onexception:${exception:format=tostring}}"
            };

            logConfig.AddTarget("console", consoleTarget);
            logConfig.LoggingRules.Add(new LoggingRule(typeof(Program).Name, LogLevel.Trace, consoleTarget));

            var fileTarget = new FileTarget
            {
                FileName = "${basedir}/host"+DateTime.Now.ToString("yyMMddHHmmssff") + ".log",
                Layout =
                    "${longdate} ${pad:padding=-5:inner=${level:uppercase=true}} ${event-context:item=context} ${logger} ${message}${onexception:${exception:format=tostring}}",
                Encoding = Encoding.UTF8,
                Name = "hostDatedLogTarget"
            };

            logConfig.AddTarget("hostDatedLogTarget", fileTarget);
            logConfig.LoggingRules.Add(new LoggingRule(typeof(Program).Name, LogLevel.Trace, fileTarget));
            LogManager.Configuration = logConfig;

            return LogManager.GetLogger(typeof(Program).Name);
        }
        
        internal static UnityContainer CreateDependencyContainer()
        {
            var logger = GetLogger();
            var container = new UnityContainer();
            container.RegisterInstance<ISchedulerFactory>(new StdSchedulerFactory());
            container.RegisterInstance(logger, new HierarchicalLifetimeManager());
            var unityJobFactory = new UnityJobFactory(container);
            container.RegisterInstance<IJobFactory>(unityJobFactory);
            container.RegisterInstance<IEnqueuerFactory>(unityJobFactory);
            container.RegisterInstance((JobConfigurations) ConfigurationManager.GetSection("jobSettings"));
            var childContainers = GetModuleContainers(container);
            container.RegisterInstance<IJobActivator>(new UnityJobActivator(container, childContainers));
            container.RegisterType<IEnqueueService, EnqueueService>();
            var settings = Settings.Default;
            container.RegisterInstance<ISmtpService>(new SmtpService(settings.NotificationSmtpHost,
                settings.NotificationSmtpUser, settings.NotificationSmtpPassword, settings.NotificationFrom));
            
            return container;
        }
    }
}
