using Microsoft.Practices.Unity;

namespace BackgroundJob.Core
{
    public interface IBackgroundJobConfigurer
    {
        IUnityContainer ConfigureContainer(IUnityContainer childContainer);
    }
}
