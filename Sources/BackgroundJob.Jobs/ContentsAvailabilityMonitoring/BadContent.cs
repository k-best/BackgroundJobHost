using System;

namespace BackgroundJob.Jobs.ContentsAvailabilityMonitoring
{
    public class BadContent
    {
        public Guid ProviderId { get; set; }
        public Guid ContentId { get; set; }
        public string Prefix { get; set; }
        public int ErrorCount { get; set; }
        public int SuccessCount { get; set; }
    }
}