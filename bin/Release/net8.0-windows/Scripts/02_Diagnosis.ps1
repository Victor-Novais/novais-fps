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

# PCIe TLP Diagnostics (Zero-Level Optimization)
Write-Log "================================================================================"
Write-Log "PCIe TLP Size Diagnostics (Zero-Level Optimization)"
Write-Log "================================================================================"
try {
    $exe = Join-Path $WorkspaceRoot "NovaisFPS.exe"
    if (-not (Test-Path $exe)) { $exe = Join-Path $WorkspaceRoot "bin\Release\net8.0-windows\NovaisFPS.exe" }
    if (Test-Path $exe) {
        Write-Log "Collecting PCIe TLP diagnostics..."
        $proc = Start-Process -FilePath $exe -ArgumentList "--pcie-tlp-diagnose" -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput "$env:TEMP\novais_pcietlp.txt" -RedirectStandardError "$env:TEMP\novais_pcietlp.err"
        if (Test-Path "$env:TEMP\novais_pcietlp.txt") {
            Get-Content "$env:TEMP\novais_pcietlp.txt" | ForEach-Object { Write-Log $_ }
        }
    } else {
        Write-Log -Level "WARN" -Message "NovaisFPS.exe not found to run --pcie-tlp-diagnose"
    }
} catch {
    Write-Log -Level "WARN" -Message "PCIe TLP diagnostics failed: $($_.Exception.Message)"
}

# Memory Latency Diagnostics (Zero-Level Optimization)
Write-Log "================================================================================"
Write-Log "Memory Latency Diagnostics (Zero-Level Optimization)"
Write-Log "================================================================================"
try {
    $exe = Join-Path $WorkspaceRoot "NovaisFPS.exe"
    if (-not (Test-Path $exe)) { $exe = Join-Path $WorkspaceRoot "bin\Release\net8.0-windows\NovaisFPS.exe" }
    if (Test-Path $exe) {
        Write-Log "Collecting memory latency diagnostics..."
        $proc = Start-Process -FilePath $exe -ArgumentList "--memory-latency-diagnose" -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput "$env:TEMP\novais_memlatency.txt" -RedirectStandardError "$env:TEMP\novais_memlatency.err"
        if (Test-Path "$env:TEMP\novais_memlatency.txt") {
            Get-Content "$env:TEMP\novais_memlatency.txt" | ForEach-Object { Write-Log $_ }
        }
    } else {
        Write-Log -Level "WARN" -Message "NovaisFPS.exe not found to run --memory-latency-diagnose"
    }
} catch {
    Write-Log -Level "WARN" -Message "Memory latency diagnostics failed: $($_.Exception.Message)"
}

Write-Log "No changes applied in diagnosis."
exit 0







