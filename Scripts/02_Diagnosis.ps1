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

Write-Log "═══════════════════════════════════════════════════════════════════════════════"
Write-Log "System Diagnosis - Starting"
Write-Log "═══════════════════════════════════════════════════════════════════════════════"
Write-Log "Diagnosis mode=$Mode run=$RunId"
Write-Log ""

# OS Information
Write-Log "[1/6] Collecting OS information..."
try {
    $osInfo = Get-ComputerInfo -ErrorAction Stop | Select-Object OsName, OsVersion, OsBuildNumber
    foreach ($item in $osInfo) {
        Write-Log ("  OS: " + ($item | ConvertTo-Json -Compress))
    }
} catch {
    Write-Log -Level "WARN" -Message "Failed to collect OS information: $($_.Exception.Message)"
}
Write-Log ""

# CPU Information
Write-Log "[2/6] Collecting CPU information..."
try {
    $cpuInfo = Get-CimInstance Win32_Processor -ErrorAction Stop | Select-Object Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed
    foreach ($cpu in $cpuInfo) {
        Write-Log ("  CPU: " + ($cpu | ConvertTo-Json -Compress))
    }
} catch {
    Write-Log -Level "WARN" -Message "Failed to collect CPU information: $($_.Exception.Message)"
}
Write-Log ""

# GPU Information
Write-Log "[3/6] Collecting GPU information..."
try {
    $gpuInfo = Get-CimInstance Win32_VideoController -ErrorAction Stop | Select-Object Name, DriverVersion
    foreach ($gpu in $gpuInfo) {
        Write-Log ("  GPU: " + ($gpu | ConvertTo-Json -Compress))
    }
} catch {
    Write-Log -Level "WARN" -Message "Failed to collect GPU information: $($_.Exception.Message)"
}
Write-Log ""

# RAM Information
Write-Log "[4/6] Collecting RAM information..."
try {
    $ramInfo = Get-CimInstance Win32_ComputerSystem -ErrorAction Stop | Select-Object TotalPhysicalMemory
    foreach ($ram in $ramInfo) {
        $ramGB = [math]::Round([long]$ram.TotalPhysicalMemory / 1GB, 2)
        Write-Log ("  RAM: TotalPhysicalMemory=$ramGB GB (" + ($ram | ConvertTo-Json -Compress) + ")")
    }
} catch {
    Write-Log -Level "WARN" -Message "Failed to collect RAM information: $($_.Exception.Message)"
}
Write-Log ""

# Disk Information
Write-Log "[5/6] Collecting disk information..."
try {
    $diskInfo = Get-CimInstance Win32_DiskDrive -ErrorAction Stop | Select-Object Model, InterfaceType, MediaType
    foreach ($disk in $diskInfo) {
        Write-Log ("  Disk: " + ($disk | ConvertTo-Json -Compress))
    }
} catch {
    Write-Log -Level "WARN" -Message "Failed to collect disk information: $($_.Exception.Message)"
}
Write-Log ""

# Power Scheme Information
Write-Log "[6/6] Collecting power scheme information..."
try {
    $active = (powercfg /getactivescheme) 2>$null
    if ($active) {
        Write-Log "  Power scheme: $active"
    } else {
        Write-Log -Level "WARN" -Message "Could not determine active power scheme"
    }
} catch {
    Write-Log -Level "WARN" -Message "powercfg read failed: $($_.Exception.Message)"
}
Write-Log ""

# PCIe TLP Diagnostics (Zero-Level Optimization)
Write-Log "═══════════════════════════════════════════════════════════════════════════════"
Write-Log "PCIe TLP Size Diagnostics (Zero-Level Optimization)"
Write-Log "═══════════════════════════════════════════════════════════════════════════════"
try {
    $exe = Join-Path $WorkspaceRoot "NovaisFPS.exe"
    if (-not (Test-Path $exe)) { $exe = Join-Path $WorkspaceRoot "bin\Release\net8.0-windows\NovaisFPS.exe" }
    if (Test-Path $exe) {
        Write-Log "Collecting PCIe TLP diagnostics..."
        $outputFile = "$env:TEMP\novais_pcietlp_$RunId.txt"
        $errorFile = "$env:TEMP\novais_pcietlp_$RunId.err"
        
        $proc = Start-Process -FilePath $exe -ArgumentList "--pcie-tlp-diagnose" -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput $outputFile -RedirectStandardError $errorFile -NoNewWindow
        
        if (Test-Path $outputFile) {
            $lines = Get-Content $outputFile -ErrorAction SilentlyContinue
            if ($lines) {
                foreach ($line in $lines) {
                    if ($line) {
                        Write-Log $line
                    }
                }
            }
            # Clean up temp file
            Remove-Item $outputFile -ErrorAction SilentlyContinue
        }
        
        if (Test-Path $errorFile) {
            $errors = Get-Content $errorFile -ErrorAction SilentlyContinue
            if ($errors) {
                foreach ($err in $errors) {
                    if ($err) {
                        Write-Log -Level "WARN" -Message "PCIe TLP diagnostic error: $err"
                    }
                }
            }
            Remove-Item $errorFile -ErrorAction SilentlyContinue
        }
        
        if ($proc.ExitCode -ne 0) {
            Write-Log -Level "WARN" -Message "PCIe TLP diagnostics returned exit code $($proc.ExitCode)"
        }
    } else {
        Write-Log -Level "WARN" -Message "NovaisFPS.exe not found to run --pcie-tlp-diagnose"
    }
} catch {
    Write-Log -Level "WARN" -Message "PCIe TLP diagnostics failed: $($_.Exception.Message)"
}
Write-Log ""

# Memory Latency Diagnostics (Zero-Level Optimization)
Write-Log "═══════════════════════════════════════════════════════════════════════════════"
Write-Log "Memory Latency Diagnostics (Zero-Level Optimization)"
Write-Log "═══════════════════════════════════════════════════════════════════════════════"
try {
    $exe = Join-Path $WorkspaceRoot "NovaisFPS.exe"
    if (-not (Test-Path $exe)) { $exe = Join-Path $WorkspaceRoot "bin\Release\net8.0-windows\NovaisFPS.exe" }
    if (Test-Path $exe) {
        Write-Log "Collecting memory latency diagnostics..."
        $outputFile = "$env:TEMP\novais_memlatency_$RunId.txt"
        $errorFile = "$env:TEMP\novais_memlatency_$RunId.err"
        
        $proc = Start-Process -FilePath $exe -ArgumentList "--memory-latency-diagnose" -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput $outputFile -RedirectStandardError $errorFile -NoNewWindow
        
        if (Test-Path $outputFile) {
            $lines = Get-Content $outputFile -ErrorAction SilentlyContinue
            if ($lines) {
                foreach ($line in $lines) {
                    if ($line) {
                        Write-Log $line
                    }
                }
            }
            # Clean up temp file
            Remove-Item $outputFile -ErrorAction SilentlyContinue
        }
        
        if (Test-Path $errorFile) {
            $errors = Get-Content $errorFile -ErrorAction SilentlyContinue
            if ($errors) {
                foreach ($err in $errors) {
                    if ($err) {
                        Write-Log -Level "WARN" -Message "Memory latency diagnostic error: $err"
                    }
                }
            }
            Remove-Item $errorFile -ErrorAction SilentlyContinue
        }
        
        if ($proc.ExitCode -ne 0) {
            Write-Log -Level "WARN" -Message "Memory latency diagnostics returned exit code $($proc.ExitCode)"
        }
    } else {
        Write-Log -Level "WARN" -Message "NovaisFPS.exe not found to run --memory-latency-diagnose"
    }
} catch {
    Write-Log -Level "WARN" -Message "Memory latency diagnostics failed: $($_.Exception.Message)"
}
Write-Log ""

Write-Log "═══════════════════════════════════════════════════════════════════════════════"
Write-Log "System Diagnosis - Complete"
Write-Log "═══════════════════════════════════════════════════════════════════════════════"
Write-Log "No changes applied in diagnosis mode."
exit 0







