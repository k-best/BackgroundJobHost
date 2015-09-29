Write-Host "Uninstall service"
$serviceName = "QA_BackgroundJobHost"
$description = "Сервис для выполнения периодических задач"
# verify if the service already exists, and if yes remove it first
$TheService = Get-Service $ServiceName -ErrorAction SilentlyContinue
if($TheService)
{
    if ($TheService.Status -eq "Running")
    {
        Write-Host "Stopping $ServiceName ..."
        $TheService.Stop()
    }
	# using WMI to remove Windows service because PowerShell does not have CmdLet for this
    $serviceToRemove = Get-WmiObject -Class Win32_Service -Filter "name='$serviceName'"
    $serviceToRemove.delete()
    Write-Host "service removed"
}
else
{
	# just do nothing
    Write-Host "service does not exists"
}