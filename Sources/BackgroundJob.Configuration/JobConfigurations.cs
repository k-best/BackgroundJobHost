using System.Configuration;
using BackgroundJob.Core;

namespace BackgroundJob.Configuration
{
    public class JobConfigurations : ConfigurationSection
    {
        public JobConfigurations()
        {
        }

        [ConfigurationProperty("jobs")]
        [ConfigurationCollection(typeof(JobConfigurationCollection))]
        public JobConfigurationCollection Jobs { get { return this["jobs"] as JobConfigurationCollection; } }
    }

    public class JobConfigurationCollection:ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new JobConfiguration();
        }

        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.BasicMap;
            }
        }

        protected override string ElementName
        {
            get
            {
                return "job";
            }
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var typedElement = element as JobConfiguration;
            return typedElement == null ? "" : typedElement.Name;
        }
    }

    public class JobConfiguration : ConfigurationElement, IJobConfiguration
    {
        [ConfigurationProperty("name", IsRequired = true, IsKey = true)]
        public string Name
        {
            get { return (string)this["name"]; }
            set { this["name"] = value; }
            
        }

        [ConfigurationProperty("type", IsRequired = true, IsKey = true)]
        public string Type
        {
            get { return (string)this["type"]; }
            set { this["type"] = value; }
        }

        [ConfigurationProperty("schedulingtime", IsRequired = true, IsKey = true)]
        public string SchedulingTime
        {
            get { return (string)this["schedulingtime"]; }
            set { this["schedulingtime"] = value; }
        }

        [ConfigurationProperty("queuename", IsRequired = false, IsKey = false)]
        public string QueueName
        {
            get
            {
                var queueName = (string)this["queuename"];
                if (string.IsNullOrWhiteSpace(queueName))
                    queueName = ".\\Private$\\CommonQueue";
                return queueName;
            }
            set { this["queuename"] = value; }
        }

        [ConfigurationProperty("maxreplay", IsRequired = false, IsKey = false)]
        public int? MaxReplay
        {
            get
            {
                return (int?)this["maxreplay"] ?? 0; 
                
            }
            set { this["maxreplay"] = value; }
        }
    }
}
