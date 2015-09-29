using System;
using System.Linq;
using System.Text;
using System.Threading;
using Autofac;
using BackgroundJob.Core;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace BackgroundJob.Host.Example
{
    public interface ITestJob
    {
        void Process(string someParameter, CancellationToken cancellationToken);
    }

    public class TestJob : ITestJob, IDisposable
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
                var inst = Guid.NewGuid();
                _logger.Info("Work {1} started at {0}", DateTime.Now, inst);
                _logger.Info("Parameter:{0}", someParameter);
                Thread.Sleep(30000);
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Info("Work canceled by host");
                    cancellationToken.ThrowIfCancellationRequested();
                }
                _logger.Info("Work ended at {0}", DateTime.Now);
            }
            catch (Exception e)
            {
                _logger.Fatal(e);
            }
        }

        public void Dispose()
        {
            _logger.Info("Dispose");
        }
    }

    public class TestJobContainerConfigurer : IBackgroundJobConfigurer
    {
        private static Logger GetLogger()
        {
            const string name = "ExampleJob";
            const string targetName = name + "Target";
            if (LogManager.Configuration == null)
                LogManager.Configuration = new LoggingConfiguration();
            var logConfig = LogManager.Configuration;

            if(logConfig.AllTargets.Any(c=> c.Name==targetName))
                return LogManager.GetLogger(name);

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
                Name = targetName,
            };

            logConfig.AddTarget(targetName, fileTarget);
            logConfig.LoggingRules.Add(new LoggingRule(name, LogLevel.Trace, fileTarget));

            LogManager.Configuration = logConfig;

            return LogManager.GetLogger(name);
        }

        public ContainerBuilder ConfigureContainer(ContainerBuilder childContainer)
        {
            childContainer.RegisterInstance(GetLogger()).SingleInstance();
            childContainer.RegisterType<TestJob>().As<ITestJob>();
            return childContainer;
        }
    }
}