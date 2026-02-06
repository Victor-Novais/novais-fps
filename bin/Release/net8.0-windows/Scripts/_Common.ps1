param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Ensure $PSScriptRoot is an absolute path and handle edge cases
if (-not $PSScriptRoot) {
    # Fallback: try to get script root from MyInvocation
    $PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if ($PSScriptRoot) {
    # Convert to absolute path to avoid issues with relative paths
    $PSScriptRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
}

# Robust error handling wrapper for critical functions
function Invoke-Safe {
    param(
        [Parameter(Mandatory=$true)][scriptblock]$ScriptBlock,
        [string]$ErrorMessage = "Operation failed",
        [object]$ErrorObject = $null
    )
    try {
        return & $ScriptBlock
    } catch {
        $errorDetails = $_.Exception.Message
        if ($ErrorObject) {
            Write-Log -Level "ERROR" -Message "$ErrorMessage`: $errorDetails (Object: $ErrorObject)"
        } else {
            Write-Log -Level "ERROR" -Message "$ErrorMessage`: $errorDetails"
        }
        throw
    }
}

function Write-Log {
    param(
        [Parameter(Mandatory=$true)][string]$Message,
        [ValidateSet("INFO","WARN","ERROR")][string]$Level = "INFO",
        [string]$LogFile = $script:LogFile
    )
    try {
        $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff') [$Level] $Message"
        Write-Host $line -ErrorAction SilentlyContinue
        
        if ($LogFile) {
            try {
                $logDir = Split-Path -Parent $LogFile
                if ($logDir -and (Test-Path $logDir)) {
                    # Use absolute path for log file
                    $absLogFile = [System.IO.Path]::GetFullPath($LogFile)
                    Add-Content -Path $absLogFile -Value $line -Encoding UTF8 -ErrorAction SilentlyContinue
                } elseif ($logDir) {
                    # Try to create directory if it doesn't exist
                    try {
                        New-Item -ItemType Directory -Force -Path $logDir -ErrorAction Stop | Out-Null
                        $absLogFile = [System.IO.Path]::GetFullPath($LogFile)
                        Add-Content -Path $absLogFile -Value $line -Encoding UTF8 -ErrorAction SilentlyContinue
                    } catch {
                        # If directory creation fails, just write to console
                        Write-Host "WARNING: Could not write to log file: $($_.Exception.Message)" -ForegroundColor Yellow
                    }
                }
            } catch {
                # If log writing fails, continue without logging (don't break execution)
                Write-Host "WARNING: Log write failed: $($_.Exception.Message)" -ForegroundColor Yellow -ErrorAction SilentlyContinue
            }
        }
    } catch {
        # Last resort: just try to write to console
        Write-Host "[$Level] $Message" -ErrorAction SilentlyContinue
    }
}

function Load-JsonFile {
    param([Parameter(Mandatory=$true)][string]$Path)
    try {
        # Convert to absolute path
        $absPath = [System.IO.Path]::GetFullPath($Path)
        if (-not (Test-Path $absPath)) { return $null }
        $raw = Get-Content -Path $absPath -Raw -Encoding UTF8 -ErrorAction Stop
        if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
        return ($raw | ConvertFrom-Json -Depth 20 -ErrorAction Stop)
    } catch {
        Write-Log -Level "WARN" -Message "Failed to load JSON file '$Path': $($_.Exception.Message)"
        return $null
    }
}

function Save-JsonFile {
    param(
        [Parameter(Mandatory=$true)][object]$Obj,
        [Parameter(Mandatory=$true)][string]$Path
    )
    try {
        # Convert to absolute path
        $absPath = [System.IO.Path]::GetFullPath($Path)
        $json = $Obj | ConvertTo-Json -Depth 20 -ErrorAction Stop
        $dir = Split-Path -Parent $absPath
        if ($dir) { 
            New-Item -ItemType Directory -Force -Path $dir -ErrorAction Stop | Out-Null 
        }
        Set-Content -Path $absPath -Value $json -Encoding UTF8 -ErrorAction Stop
    } catch {
        Write-Log -Level "ERROR" -Message "Failed to save JSON file '$Path': $($_.Exception.Message)"
        throw
    }
}

function Ensure-ArrayProp {
    param([Parameter(Mandatory=$true)][object]$Obj, [Parameter(Mandatory=$true)][string]$PropName)
    if (-not ($Obj.PSObject.Properties.Name -contains $PropName)) {
        $Obj | Add-Member -NotePropertyName $PropName -NotePropertyValue @()
    } elseif ($null -eq $Obj.$PropName) {
        $Obj.$PropName = @()
    }
}

function Add-Change {
    param(
        [Parameter(Mandatory=$true)][object]$Ctx,
        [Parameter(Mandatory=$true)][string]$Category,
        [Parameter(Mandatory=$true)][string]$Key,
        [Parameter(Mandatory=$true)][object]$Before,
        [Parameter(Mandatory=$true)][object]$After,
        [string]$Note = ""
    )
    Ensure-ArrayProp -Obj $Ctx -PropName "changes"
    $entry = [pscustomobject]@{
        ts = (Get-Date).ToString("o")
        category = $Category
        key = $Key
        before = $Before
        after = $After
        note = $Note
    }
    $Ctx.changes = @($Ctx.changes) + @($entry)
}

function Get-RegistryValueSafe {
    param([Parameter(Mandatory=$true)][string]$Path, [Parameter(Mandatory=$true)][string]$Name)
    try {
        # Ensure path exists before trying to read
        if (-not (Test-Path $Path)) { return $null }
        $v = (Get-ItemProperty -Path $Path -Name $Name -ErrorAction Stop).$Name
        return $v
    } catch {
        # Silently return null on any error (key doesn't exist, permission denied, etc.)
        return $null
    }
}

function Set-RegistryDword {
    param(
        [Parameter(Mandatory=$true)][object]$Ctx,
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][int]$Value,
        [string]$Note = ""
    )
    try {
        New-Item -Path $Path -Force -ErrorAction Stop | Out-Null
        $before = Get-RegistryValueSafe -Path $Path -Name $Name
        Set-ItemProperty -Path $Path -Name $Name -Type DWord -Value $Value -Force -ErrorAction Stop
        $after = Get-RegistryValueSafe -Path $Path -Name $Name
        Add-Change -Ctx $Ctx -Category "registry" -Key "$Path\$Name" -Before $before -After $after -Note $Note
    } catch {
        Write-Log -Level "WARN" -Message "Failed to set registry DWord '$Path\$Name': $($_.Exception.Message)"
        throw
    }
}

function Set-RegistryString {
    param(
        [Parameter(Mandatory=$true)][object]$Ctx,
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$Value,
        [string]$Note = ""
    )
    try {
        New-Item -Path $Path -Force -ErrorAction Stop | Out-Null
        $before = Get-RegistryValueSafe -Path $Path -Name $Name
        Set-ItemProperty -Path $Path -Name $Name -Type String -Value $Value -Force -ErrorAction Stop
        $after = Get-RegistryValueSafe -Path $Path -Name $Name
        Add-Change -Ctx $Ctx -Category "registry" -Key "$Path\$Name" -Before $before -After $after -Note $Note
    } catch {
        Write-Log -Level "WARN" -Message "Failed to set registry String '$Path\$Name': $($_.Exception.Message)"
        throw
    }
}

function Rollback-RegistryFromChanges {
    param(
        [Parameter(Mandatory=$true)][object]$TargetCtx,
        [Parameter(Mandatory=$true)][string]$PrefixMatch
    )
    if (-not ($TargetCtx.PSObject.Properties.Name -contains "changes")) { return }
    foreach ($c in @($TargetCtx.changes) | Where-Object { $_.category -eq "registry" -and $_.key -like "$PrefixMatch*" }) {
        $full = [string]$c.key
        $idx = $full.LastIndexOf("\")
        if ($idx -lt 0) { continue }
        $path = $full.Substring(0, $idx)
        $name = $full.Substring($idx + 1)
        try {
            if ($null -eq $c.before) {
                Remove-ItemProperty -Path $path -Name $name -ErrorAction SilentlyContinue
            } else {
                New-Item -Path $path -Force | Out-Null
                if ($c.before -is [int] -or $c.before -is [long]) {
                    Set-ItemProperty -Path $path -Name $name -Type DWord -Value ([int]$c.before) -Force
                } else {
                    Set-ItemProperty -Path $path -Name $name -Value $c.before -Force
                }
            }
        } catch {
            Write-Log -Level "WARN" -Message "Rollback registry failed for $($c.key): $($_.Exception.Message)"
        }
    }
}

function Run-Cmd {
    param([Parameter(Mandatory=$true)][string]$File, [Parameter(Mandatory=$true)][string]$Args)
    try {
        # Use absolute path for executable if it's a file path
        $absFile = $File
        if (Test-Path $File) {
            $absFile = [System.IO.Path]::GetFullPath($File)
        }
        $p = Start-Process -FilePath $absFile -ArgumentList $Args -Wait -PassThru -NoNewWindow -ErrorAction Stop
        return $p.ExitCode
    } catch {
        Write-Log -Level "ERROR" -Message "Failed to run command '$File $Args': $($_.Exception.Message)"
        return -1
    }
}

function Set-ServiceSafe {
    param(
        [Parameter(Mandatory=$true)][object]$Ctx,
        [Parameter(Mandatory=$true)][string]$Name,
        [ValidateSet("Automatic","Manual","Disabled")][string]$StartupType,
        [ValidateSet("Start","Stop","NoChange")][string]$Action = "NoChange",
        [string]$Note = ""
    )
    try {
        $svc = Get-Service -Name $Name -ErrorAction Stop
        $before = [pscustomobject]@{
            status = $svc.Status.ToString()
            startType = (Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $Name) | Select-Object -ExpandProperty StartMode)
        }

        if ($Action -eq "Stop" -and $svc.Status -ne "Stopped") {
            Stop-Service -Name $Name -Force -ErrorAction SilentlyContinue
        } elseif ($Action -eq "Start" -and $svc.Status -ne "Running") {
            Start-Service -Name $Name -ErrorAction SilentlyContinue
        }

        Set-Service -Name $Name -StartupType $StartupType -ErrorAction SilentlyContinue

        $svc2 = Get-Service -Name $Name -ErrorAction Stop
        $after = [pscustomobject]@{
            status = $svc2.Status.ToString()
            startType = (Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $Name) | Select-Object -ExpandProperty StartMode)
        }

        Add-Change -Ctx $Ctx -Category "service" -Key $Name -Before $before -After $after -Note $Note
        Write-Log "Service $Name: $($before.status)/$($before.startType) -> $($after.status)/$($after.startType)"
    } catch {
        Write-Log -Level "WARN" -Message "Service tweak skipped for $Name: $($_.Exception.Message)"
    }
}

function Rollback-ServicesFromChanges {
    param([Parameter(Mandatory=$true)][object]$TargetCtx, [string[]]$ServiceNames)
    if (-not ($TargetCtx.PSObject.Properties.Name -contains "changes")) { return }
    foreach ($name in $ServiceNames) {
        $c = @($TargetCtx.changes) | Where-Object { $_.category -eq "service" -and $_.key -eq $name } | Select-Object -Last 1
        if (-not $c) { continue }
        try {
            $beforeStart = [string]$c.before.startType
            # CIM uses Auto/Manual/Disabled; map to Set-Service values
            $mapped =
                if ($beforeStart -eq "Auto") { "Automatic" }
                elseif ($beforeStart -eq "Manual") { "Manual" }
                elseif ($beforeStart -eq "Disabled") { "Disabled" }
                else { "Manual" }

            Set-Service -Name $name -StartupType $mapped -ErrorAction SilentlyContinue

            $beforeStatus = [string]$c.before.status
            if ($beforeStatus -eq "Running") {
                Start-Service -Name $name -ErrorAction SilentlyContinue
            } elseif ($beforeStatus -eq "Stopped") {
                Stop-Service -Name $name -Force -ErrorAction SilentlyContinue
            }
            Write-Log "Rollback service $name -> $beforeStatus/$beforeStart"
        } catch {
            Write-Log -Level "WARN" -Message "Rollback service failed for $name: $($_.Exception.Message)"
        }
    }
}


