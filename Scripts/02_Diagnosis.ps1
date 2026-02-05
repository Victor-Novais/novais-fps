param(
    [Parameter(Mandatory=$true)][ValidateSet("Apply","Rollback")][string]$Mode,
    [Parameter(Mandatory=$true)][string]$RunId,
    [Parameter(Mandatory=$true)][string]$WorkspaceRoot,
    [Parameter(Mandatory=$true)][string]$LogFile,
    [Parameter(Mandatory=$true)][string]$ContextJson,
    [string]$TargetContextJson = ""
)

. (Join-Path $PSScriptRoot "_Common.ps1")
$script:LogFile = $LogFile

Write-Log "Diagnosis mode=$Mode run=$RunId"

Write-Log "OS:"; Get-ComputerInfo | Select-Object OsName, OsVersion, OsBuildNumber | ForEach-Object { Write-Log ("  " + ($_ | ConvertTo-Json -Compress)) }
Write-Log "CPU:"; Get-CimInstance Win32_Processor | Select-Object Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed | ForEach-Object { Write-Log ("  " + ($_ | ConvertTo-Json -Compress)) }
Write-Log "GPU:"; Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion | ForEach-Object { Write-Log ("  " + ($_ | ConvertTo-Json -Compress)) }
Write-Log "RAM:"; Get-CimInstance Win32_ComputerSystem | Select-Object TotalPhysicalMemory | ForEach-Object { Write-Log ("  " + ($_ | ConvertTo-Json -Compress)) }
Write-Log "Disk:"; Get-CimInstance Win32_DiskDrive | Select-Object Model, InterfaceType, MediaType | ForEach-Object { Write-Log ("  " + ($_ | ConvertTo-Json -Compress)) }

try {
    $active = (powercfg /getactivescheme) 2>$null
    Write-Log "Power scheme: $active"
} catch {
    Write-Log -Level "WARN" -Message "powercfg read failed: $($_.Exception.Message)"
}

Write-Log "No changes applied in diagnosis."
exit 0







