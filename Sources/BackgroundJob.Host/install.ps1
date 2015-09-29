Write-Host "Install MSMQ features"
$requiredFeatures = "MSMQ, MSMQ-Services, MSMQ-Server, MSMQ-Triggers, MSMQ-Multicast, MSMQ-DCOMProxy".split(",") | foreach { $_.trim() }
if(! $requiredFeatures) {
    Write-Host "No required Windows Features specified..."
    exit
}
$requiredFeatures | foreach { $feature = DISM.exe /ONLINE /Get-FeatureInfo /FeatureName:$_; if($feature -like "*Feature name $_ is unknown*") { throw $feature } }

Write-Host "Retrieving all Windows Features..."
$allFeatures = DISM.exe /ONLINE /Get-Features /FORMAT:List | Where-Object { $_.StartsWith("Feature Name") -OR $_.StartsWith("State") } 
$features = new-object System.Collections.ArrayList
for($i = 0; $i -lt $allFeatures.length; $i=$i+2) {
    $feature = $allFeatures[$i]
    $state = $allFeatures[$i+1]
    $features.add(@{feature=$feature.split(":")[1].trim();state=$state.split(":")[1].trim()}) | OUT-NULL
}

Write-Host "Checking for missing Windows Features..."
$missingFeatures = new-object System.Collections.ArrayList
$features | foreach { if( $requiredFeatures -contains $_.feature -and $_.state -eq 'Disabled') { $missingFeatures.add($_.feature) | OUT-NULL } }
if(! $missingFeatures) {
    Write-Host "All required Windows Features are installed"
}
else
{
	Write-Host "Installing missing Windows Features..."
	$featureNameArgs = ""
	$missingFeatures | foreach { $featureNameArgs = $featureNameArgs + " /FeatureName:" + $_ }
	$dism = "DISM.exe"
	$arguments = ""
	$arguments = $arguments + "/ONLINE /Enable-Feature $featureNameArgs"
	Write-Host "Calling DISM with arguments: $arguments"
	start-process -NoNewWindow $dism $arguments -Wait
}
#$DeleteCommand = 'http delete urlacl url=http://+:8000/EnqueueService/service'
#$DeleteCommand | netsh

#$AddCommand = 'http add urlacl url=http://+:8000/EnqueueService/service user="LOCAL SERVICE"'
#$AddCommand |netsh

#$path = $args[0]

#Write-Host ("Path:"+$path)


#$xml = [xml](Get-Content -ReadCount -1 $path)

#Write-Host ("xmlConfigurationContent "+ $xml.configuration.OuterXml)

Write-Host "Create Transactional Queue"

$MSMQUsers = "LOCAL SERVICE;BUILTIN\Administrators"
$MSMQPermAllow = "WriteMessage;ReceiveMessage;PeekMessage;DeleteMessage"
$MSMQPermDeny = "TakeQueueOwnership"
$MSMQPermFull = "FullControl"
$QueueName = ".\Private$\CommonQueue"

Add-Type -AssemblyName System.Messaging

#foreach($job in $xml.configuration.jobSettings.jobs.job)
#{
#    Write-Host ("job "+$job.OuterXml)
#    Write-Host ("queue " + $job.queuename)
    $Queue = $QueueName #$job.QueueName
    if (![System.Messaging.MessageQueue]::Exists($Queue))
    {
        #not found, create
        Write-Output "Creating Queue: " $Queue
        #New-MsmqQueue -Name "$Queue" -Transactional | Out-Null
        $thisQueue = [System.Messaging.MessageQueue]::Create($Queue, $true)
    }
    else
    {
        $thisQueue = New-Object System.Messaging.MessageQueue($Queue)
        #keep rolling
        Write-Output "Queue Exists: " $thisQueue.QueueName
    }

    #set acl for users
    $arrUsers = $MSMQUsers.split(";")
    foreach ($User in $arrUsers)     
    {    
        if ($User)
        {    
            Write-Output "Adding ACL for User: " $User        
            $enumType = (New-Object System.Messaging.MessageQueueAccessRights).GetType()
            #fullcontrol
            if ($User.Contains('BUILTIN\Administrators'))
            {
                $arrPermissions = $MSMQPermFull.split(";")
                foreach ($Permission in $arrPermissions)    
                {                    
                    $typedPermission = [System.Enum]::Parse($enumType, $Permission)
                    $thisQueue.SetPermissions($User, $typedPermission, [System.Messaging.AccessControlEntryType]::Allow)
                    #$thisQueue | Set-MsmqQueueAcl -UserName $User -Allow $Permission | Out-Null            
                    Write-Output "ACL Allow set: $Permission"
                }
            }
            else            
            {
                #allows
                $arrPermissions = $MSMQPermAllow.split(";")
                $ACLS = ""
                foreach ($Permission in $arrPermissions)     
                {
                    #$thisQueue | Set-MsmqQueueAcl -UserName $User -Allow $Permission | Out-Null                
                    $typedPermission = [System.Enum]::Parse($enumType, $Permission)
                    $thisQueue.SetPermissions($User, $typedPermission, [System.Messaging.AccessControlEntryType]::Allow)
                    $ACLS = "$Permission,$ACLS"                
                }    
                Write-Output "ACL Allow set: $ACLS"
                
                #denies
                $arrPermissions = $MSMQPermDeny.split(";")
                foreach ($Permission in $arrPermissions)     
                {
                    #$thisQueue | Set-MsmqQueueAcl -UserName $User -Deny $Permission | Out-Null
                    $typedPermission = [System.Enum]::Parse($enumType, $Permission)
                    $thisQueue.SetPermissions($User, $typedPermission, [System.Messaging.AccessControlEntryType]::Deny)
                    Write-Output "ACL Deny  set: $Permission"
                }
            }
        }
    }
#}
Write-Host "Install service"
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

"installing service"
# creating credentials which can be used to run my windows service
$login = "NT AUTHORITY\LOCAL SERVICE"
#### #just set a dummy psw since it's just used to get credentials

$psw = "dummy"

$scuritypsw = ConvertTo-SecureString $psw -AsPlainText -Force

$mycreds = New-Object System.Management.Automation.PSCredential($login, $scuritypsw)
#### #then you can use the cred to new a windows service
$invocation = (Get-Variable MyInvocation).Value
$directorypath = Split-Path $invocation.MyCommand.Path
$binaryPath = $directorypath + "\BackgroundJob.Host.exe"
# creating widnows service using all provided parameters
New-Service -name $serviceName -binaryPathName $binaryPath -displayName $serviceName -startupType Automatic -credential $mycreds -Description $description
Restart-Service -Name $serviceName 
Write-Host "installation completed"