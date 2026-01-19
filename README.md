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

## Modos de Execução CLI

O `NovaisFPS.exe` suporta modos helper para automação:

```powershell
# Modo normal (menu interativo)
.\bin\Release\net8.0-windows\NovaisFPS.exe

# MSI-X Enforcer (força MSI/MSI-X em dispositivos críticos)
.\bin\Release\net8.0-windows\NovaisFPS.exe --msi-enforce

# Limpar Standby List (purge de memória)
.\bin\Release\net8.0-windows\NovaisFPS.exe --memory-clean

# Health Check (DPC/ISR latency + timer resolution)
.\bin\Release\net8.0-windows\NovaisFPS.exe --health
```

## Roadmap: Interface Gráfica (GUI) - Produto de Elite

### Arquitetura Planejada

O NOVAIS FPS foi projetado para evoluir para uma **GUI comercial de elite**. A arquitetura atual (CLI + PowerShell) já está preparada para consumo por uma interface gráfica.

#### Stack Tecnológico Proposto

- **Framework GUI**: WPF (.NET 8) ou WinUI 3
- **Comunicação**: Os módulos C# e scripts PowerShell já suportam:
  - Parâmetros de linha de comando claros
  - Output estruturado (JSON via `Core/JsonOutput.cs`)
  - Status codes de sucesso/falha

#### Componentes da GUI Futura

1. **Dashboard Principal**
   - Visualização de hardware detectado (CPU, GPU, RAM, Storage)
   - Status de otimizações aplicadas (checkboxes/indicadores)
   - Health metrics em tempo real (DPC/ISR %, timer resolution)

2. **Perfis de Otimização**
   - Perfis pré-configurados (Safe, Balanced, Elite Risk)
   - Perfis customizáveis pelo usuário
   - Aplicação/rollback com um clique

3. **Monitoramento em Tempo Real**
   - Gráficos de latência (DPC/ISR)
   - Frametime overlay (opcional)
   - Alertas de degradação de performance

4. **Automação Inteligente**
   - Detecção automática de jogos em execução
   - Aplicação de perfil ao lançar jogo
   - Rollback automático ao fechar jogo

#### Preparação Atual para GUI

- ✅ Módulos C# retornam dados estruturados
- ✅ Scripts PowerShell aceitam parâmetros via CLI
- ✅ Sistema de logging centralizado (`Core/Logger.cs`)
- ✅ Context JSON para estado persistente (`Core/RunContext.cs`)
- ✅ Helper JSON output (`Core/JsonOutput.cs`)

#### Próximos Passos para GUI

1. Criar projeto WPF/WinUI separado que referencia `NovaisFPS.Core`
2. Implementar ViewModels (MVVM) que consomem os módulos C# via processo ou DLL
3. Criar UI components para cada fase de otimização
4. Integrar gráficos de performance (usando bibliotecas como LiveCharts ou OxyPlot)

---

## Otimizações Implementadas

### Nível L1 (Kernel & Scheduling)
- MSI-X enforcement para dispositivos críticos
- Timer resolution (1ms via `timeBeginPeriod`)
- HPET/TSC configuration (opt-in via BCDEdit)

### Nível L2 (Barramentos & I/O)
- PCIe ASPM desativado
- USB Selective Suspend desativado
- USB Host Controller power management otimizado
- Network adapter tuning (TcpAckFrequency, TcpNoDelay por interface)

### Nível L3 (Memória & Kernel)
- Large System Cache habilitado
- Disable Paging Executive
- Standby List cleaner (opcional)
- Context Switch optimization (Win32PrioritySeparation = 2, quantum curto)

### Nível L4 (Elite Risk - Opt-in)
- Spectre/Meltdown mitigation overrides (FeatureSettingsOverride/Mask = 3)
- SpectreMitigation throttle = 0 (onde suportado)

### Sistema & Energia
- Ultimate Performance power scheme
- Serviços de telemetria desativados (com whitelist segura)
- SysMain (Superfetch) desativado para SSDs

### Input & Latência
- USB Enhanced Power Management desativado
- Game Mode habilitado
- GameDVR desativado

### Validação & Health
- Health metrics (DPC/ISR %, timer resolution)
- Relatório de status de otimizações aplicadas
- Sistema de rollback completo

---

## Licença

Este projeto é fornecido "como está" para fins educacionais e de otimização de performance. Use por sua conta e risco.


