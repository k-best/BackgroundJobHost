using System;
using Autofac;

namespace BackgroundJob.Core
{
    public interface IBackgroundJobConfigurer
    {
        ContainerBuilder ConfigureContainer(ContainerBuilder childContainer);
    }

    public abstract class ContainerConfigurerTypeAttribute:Attribute, IBackgroundJobConfigurer
    {
        public abstract ContainerBuilder ConfigureContainer(ContainerBuilder childContainer);
    }
}
