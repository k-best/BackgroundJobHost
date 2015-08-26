using System.Configuration;
using System.Linq;

namespace BackgroundJob.Configuration
{
    public static class JobConfigurationHelper
    {
        public static string[] GetJobConfigurations()
        {
            var jobsConfig = ((JobConfigurations) ConfigurationManager.GetSection("jobSettings"));
            return jobsConfig.Jobs.OfType<JobConfiguration>().Select(c=>c.QueueName).ToArray();
        }
    }
}
