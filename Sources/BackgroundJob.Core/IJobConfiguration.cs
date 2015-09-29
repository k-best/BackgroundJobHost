namespace BackgroundJob.Core
{
    public interface IJobConfiguration
    {
        string Name { get; set; }
        string Type { get; set; }
        string SchedulingTime { get; set; }
        string QueueName { get; set; }
        int? MaxReplay { get; set; }
    }
}