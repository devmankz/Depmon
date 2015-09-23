Function indicator-process
{
    param (
        $sourceCode,
        $groupCode,
        $resourceCode,
        $indicator
    )
    $indicatorCode = $indicator.indicatorCode
    
    Write-Host "    [${indicatorCode}]: starting"

    $command = [string]::Format("command-{0}", $indicator.command)

    $contextProps = @{
        sourceCode = $sourceCode
        groupCode = $groupCode
        resourceCode = $resourceCode
        indicatorCode = $indicatorCode
    }
    $commandContext = New-Object -TypeName psobject -Property $contextProps
    $commandArgs = $indicator.args
    
    $rows = &$command -context $commandContext -contextArgs $commandArgs
    foreach($row in $rows)
    {
        Export-Csv -Path $attachmentName -InputObject $row -NoTypeInformation -Append
    }
    
    Write-Host "    [${indicatorCode}]: finished"
}

Function file-create
{
    param($fileName)

    New-Item $fileName -type file -force
}

Function command-web-state
{
    param(
        $context,
        $contextArgs
    )

    $uri = $contextArgs.uri

    Write-Host "        Connecting [${uri}]... " -NoNewline

    $response = Invoke-WebRequest $uri
    $statusCode = $response.StatusCode
    $statusDesc = $response.StatusDescription
    if ($statusCode = 200)
    {
        $level = 'Normal'
    }
    else
    {
        $level = 'Warning'
    }

    $result = [PSCustomObject]@{
                    'SourceCode' = $context.sourceCode;
                    'GroupCode' = $context.groupCode;
                    'ResourceCode' = $context.resourceCode;
                    'IndicatorCode' = $context.indicatorCode;
                    'IndicatorValue' = $statusCode;
                    'IndicatorDescription' = $statusDesc;
                    'Level' = $level;
                    'CheckedAt' = [DateTime]::Now
                }

    Write-Host "${statusCode} ${statusDesc}" -ForegroundColor Yellow
    return $result
}

Function command-harddrive-state
{
    param (
        $context,
        $contextArgs
    )

    $server = $contextArgs.server
    Write-Host "        Checking HDDs for [${server}]"

    if ($contextArgs.username -ne '')
    {
        $secPass = $contextArgs.password | ConvertTo-SecureString -AsPlainText -Force
        $cred = New-Object PSCredential($contextArgs.username, $secPass)
        $disks = Get-WmiObject -Class Win32_LogicalDisk -ComputerName $server -Filter "DriveType=3" -Credential $cred
    }
    else
    {
        $disks = Get-WmiObject -Class Win32_LogicalDisk -ComputerName $server -Filter "DriveType=3"
    }

    
    [PSCustomObject[]]$result = @()
    foreach($disk in $disks)
    {
        if ($contextArgs.driveToCheck.�ount -gt 0 -and ! $contextArgs.driveToCheck.contains([String]$disk.DeviceID[0]))
        {
            continue
        }
        $level = 'Normal'
        $result += [PSCustomObject]@{
                    'SourceCode' = $context.sourceCode;
                    'GroupCode' = $context.groupCode;
                    'ResourceCode' = $context.resourceCode;
                    'IndicatorCode' = $context.indicatorCode;
                    'IndicatorValue' = $disk.size;
                    'IndicatorDescription' = $disk.DeviceID + ' Total Space';
                    'Level' = $level;
                    'CheckedAt' = [DateTime]::Now
                }
        $result += [PSCustomObject]@{
                    'SourceCode' = $context.sourceCode;
                    'GroupCode' = $context.groupCode;
                    'ResourceCode' = $context.resourceCode;
                    'IndicatorCode' = $context.indicatorCode;
                    'IndicatorValue' = $disk.FreeSpace;
                    'IndicatorDescription' = $disk.DeviceID + ' Free Space';
                    'Level' = $level;
                    'CheckedAt' = [DateTime]::Now
                }
        $perc = $disk.FreeSpace * 100 / $disk.Size;
        if ($perc -lt 10) 
        {
            $level = 'Warning';
        } 
        else 
        { 
            if ($perc -lt 5) 
            {
                $level = 'Error';
            }
        }
        $result += [PSCustomObject]@{
                    'SourceCode' = $context.sourceCode;
                    'GroupCode' = $context.groupCode;
                    'ResourceCode' = $context.resourceCode;
                    'IndicatorCode' = $context.indicatorCode;
                    'IndicatorValue' = $perc;
                    'IndicatorDescription' = $disk.DeviceID + ' Percentage';
                    'Level' = $level;
                    'CheckedAt' = [DateTime]::Now
                }
    }
    return $result
}

Function Send-Result 
{
     $reportersFile = "reporters.json"
     $fileExist = Test-Path $reportersFile
     if (!$fileExist) 
     {
        Write-Host "$reportersFile don't exitst" -ForegroundColor Red
        return
     }
     $reportersParams = Get-Content $reportersFile | ConvertFrom-Json
     foreach($reporter in $reportersParams.reporters) 
     {
        $reporter
        Write-Host "Sending Email"
        [String[]]$toList = @()
        foreach ($to in $reporter.recipients)
        {
            $toList += $to
        }
        Send-Result-ToEmail -to $toList -from $reporter.from -username $reporter.user -password $reporter.password -smtpHost $reporter.smtpHost -port $reporter.port -useSsl $reporter.useSsl -subject $reporter.subject -attachments $attachmentName
     }
}

Function Send-Result-ToEmail
{
    param(
        $to,
        $from,

        $username,
        $password,
        $smtpHost,
        $port,
        $useSsl,

        $subject,
        $body,
        $attachments
    )
    $secStrPassword = 'healthmonitor123' | ConvertTo-SecureString -AsPlainText -Force
    $cred = New-Object PSCredential($username, $secStrPassword)

    if ($useSsl) 
    {
        Send-MailMessage -To $to -Subject $subject -Attachments $attachments -From $from -SmtpServer $smtpHost -Port $port -UseSsl -Credential $cred 
    }
    else 
    {
        Send-MailMessage -To $to -Subject $subject -Attachments $attachments -From $from -SmtpServer $smtpHost -Port $port -Credential $cred
    }
}