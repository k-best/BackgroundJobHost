using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace BackgroundJob.Host
{
	[RunInstaller(true)]
	public class ServiceInstaller : Installer
	{
		public ServiceInstaller()
		{
			Installers.AddRange(new Installer[]
			                    	{
			                    		new ServiceProcessInstaller {Account = ServiceAccount.LocalService},
										new System.ServiceProcess.ServiceInstaller {ServiceName = Program.ServiceName}
			                    	});
		}
	}
}
