# NOVAIS FPS — Competitive Performance Optimizer (PRO - solid v1)

Otimizador **seguro** e **anti-cheat friendly** para Windows, focado em:

- FPS médio e **1% lows**
- Frametime mais estável (menos jitter / micro-stutter)
- Redução de latência do sistema **sem tweaks perigosos**
- Mudanças **reversíveis** (rollback) e com **backup**

## Requisitos

- Windows 10/11 x64
- **.NET SDK 8+** (o runtime sozinho não compila)
- **PowerShell 7+** (recomendado e alvo). O app tenta usar `pwsh`; se não existir, faz fallback para `powershell.exe` (5.1) apenas para não bloquear.

## Como compilar

```powershell
dotnet --version
dotnet build -c Release
```

Opcional (gerar `.exe` self-contained):

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## Como executar

Abra o terminal **como Administrador**:

```powershell
.\bin\Release\net8.0\NovaisFPS.exe
```

## Segurança e Anti-cheat

- Não injeta DLL, não mexe em arquivos de jogo, não tenta bypass.
- Evita “tweaks meme” e chaves obscuras sem evidência.
- Mudanças no `bcdedit` (HPET/TSC) são **opt-in**, registradas e revertíveis via `bcdedit /deletevalue`.

## Estrutura

```
NOVAIS FPS/
├── NovaisFPS.csproj
├── Core/
│   ├── Program.cs
│   ├── AdminCheck.cs
│   ├── Logger.cs
│   ├── HardwareDetector.cs
│   ├── PowerShellExecutor.cs
│   ├── RunContext.cs
│   └── TimerResolution.cs
├── Scripts/
│   ├── _Common.ps1
│   ├── 01_Backup.ps1
│   ├── 02_Diagnosis.ps1
│   ├── 03_SystemPower.ps1
│   ├── 04_TimersLatency.ps1
│   ├── 05_CPUScheduler.ps1
│   ├── 06_GPUDrivers.ps1
│   ├── 07_InputUSB.ps1
│   ├── 08_Network.ps1
│   ├── 09_RegistryTweaks.ps1
│   └── 10_Validation.ps1
├── Logs/
├── Backup/
└── Profiles/
```



