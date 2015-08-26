$DeleteCommand = 'http delete urlacl url=http://+:8000/EnqueueService/service'
$DeleteCommand | netsh

$AddCommand = 'http add urlacl url=http://+:8000/EnqueueService/service user="LOCAL SERVICE"'
$AddCommand |netsh

$path = $args[0]

Write-Host ("Path:"+$path)


$xml = [xml](Get-Content -ReadCount -1 $path)

Write-Host ("xmlConfigurationContent "+ $xml.configuration.OuterXml)

$MSMQUsers = "LOCAL SERVICE;BUILTIN\Administrators"
$MSMQPermAllow = "WriteMessage;ReceiveMessage;PeekMessage;DeleteMessage"
$MSMQPermDeny = "TakeQueueOwnership"
$MSMQPermFull = "FullControl"

Add-Type -AssemblyName System.Messaging

foreach($job in $xml.configuration.jobSettings.jobs.job)
{
    Write-Host ("job "+$job.OuterXml)
    Write-Host ("queue " + $job.queuename)
    $Queue = $job.QueueName
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
}