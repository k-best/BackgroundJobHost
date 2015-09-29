using BackgroundJob.Configuration;
using BackgroundJob.Core;

namespace BackgroundJob.Host
{
    public class DbScheduleJobConfiguration : IJobConfiguration
    {
        public DbScheduleJobConfiguration(string name, string type, string schedulingTime, string queueName, int? maxReplay)
        {
            Name = name;
            Type = type;
            SchedulingTime = schedulingTime;
            QueueName = queueName;
            MaxReplay = maxReplay;
        }
        public string Name { get; set; }
        public string Type { get; set; }
        public string SchedulingTime { get; set; }
        public string QueueName { get; set; }
        public int? MaxReplay { get; set; }
    }
}