param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Log {
    param(
        [Parameter(Mandatory=$true)][string]$Message,
        [ValidateSet("INFO","WARN","ERROR")][string]$Level = "INFO",
        [string]$LogFile = $script:LogFile
    )
    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff') [$Level] $Message"
    Write-Host $line
    if ($LogFile -and (Test-Path (Split-Path -Parent $LogFile))) {
        Add-Content -Path $LogFile -Value $line -Encoding UTF8
    }
}

function Load-JsonFile {
    param([Parameter(Mandatory=$true)][string]$Path)
    if (-not (Test-Path $Path)) { return $null }
    $raw = Get-Content -Path $Path -Raw -Encoding UTF8
    if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
    return ($raw | ConvertFrom-Json -Depth 20)
}

function Save-JsonFile {
    param(
        [Parameter(Mandatory=$true)][object]$Obj,
        [Parameter(Mandatory=$true)][string]$Path
    )
    $json = $Obj | ConvertTo-Json -Depth 20
    $dir = Split-Path -Parent $Path
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    Set-Content -Path $Path -Value $json -Encoding UTF8
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
        $v = (Get-ItemProperty -Path $Path -Name $Name -ErrorAction Stop).$Name
        return $v
    } catch {
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
    New-Item -Path $Path -Force | Out-Null
    $before = Get-RegistryValueSafe -Path $Path -Name $Name
    Set-ItemProperty -Path $Path -Name $Name -Type DWord -Value $Value -Force
    $after = Get-RegistryValueSafe -Path $Path -Name $Name
    Add-Change -Ctx $Ctx -Category "registry" -Key "$Path\$Name" -Before $before -After $after -Note $Note
}

function Set-RegistryString {
    param(
        [Parameter(Mandatory=$true)][object]$Ctx,
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$Value,
        [string]$Note = ""
    )
    New-Item -Path $Path -Force | Out-Null
    $before = Get-RegistryValueSafe -Path $Path -Name $Name
    Set-ItemProperty -Path $Path -Name $Name -Type String -Value $Value -Force
    $after = Get-RegistryValueSafe -Path $Path -Name $Name
    Add-Change -Ctx $Ctx -Category "registry" -Key "$Path\$Name" -Before $before -After $after -Note $Note
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
    $p = Start-Process -FilePath $File -ArgumentList $Args -Wait -PassThru -NoNewWindow
    return $p.ExitCode
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


