using System;
using System.Text;
using System.Threading;
using BackgroundJob.Core;
using Microsoft.Practices.Unity;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace BackgroundJob.Host.Example
{
    public interface ITestJob
    {
        void Process(string someParameter, CancellationToken cancellationToken);
    }

    public class TestJob : ITestJob
    {
        private Logger _logger;

        public TestJob(Logger logger)
        {
            _logger = logger;
        }

        public void Process(string someParameter, CancellationToken cancellationToken)
        {
            try
            {
                _logger.Info("Work started at {0}", DateTime.Now);
                _logger.Info("Parameter:{0}", someParameter);
                Thread.Sleep(10000);
                cancellationToken.ThrowIfCancellationRequested();
                _logger.Info("Work ended at {0}", DateTime.Now);
            }
            catch (OperationCanceledException e)
            {
                _logger.Fatal(e);
            }
        }
    }

    public class ContainerConfigurer : IBackgroundJobConfigurer
    {
        public IUnityContainer ConfigureContainer(IUnityContainer container)
        {
            var logger = GetLogger();
            container.RegisterInstance(logger);
            container.RegisterType<ITestJob, TestJob>();
            return container;
        }

        private static Logger GetLogger()
        {
            const string name = "ExampleJob";
            if (LogManager.Configuration == null)
                LogManager.Configuration = new LoggingConfiguration();
            var logConfig = LogManager.Configuration;

            var fileTarget = new FileTarget
            {
                FileName = "${basedir}" + name + ".log",
                ArchiveAboveSize = 100000,
                ArchiveNumbering = ArchiveNumberingMode.Date,
                MaxArchiveFiles = 10,
                ConcurrentWrites = true,
                Layout =
                    "${longdate} ${pad:padding=-5:inner=${level:uppercase=true}} ${event-context:item=context} ${logger} ${message}${onexception:${exception:format=tostring}}",
                Encoding = Encoding.UTF8,
                Name = name + "Target",
            };

            logConfig.AddTarget(name + "Target", fileTarget);
            logConfig.LoggingRules.Add(new LoggingRule(name, LogLevel.Trace, fileTarget));

            LogManager.Configuration = logConfig;

            return LogManager.GetLogger(name);
        }
    }
}