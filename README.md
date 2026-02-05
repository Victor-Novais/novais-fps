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

O `NovaisFPS.exe` suporta modos helper para automação e diagnósticos:

```powershell
# Modo normal (menu interativo)
.\bin\Release\net8.0-windows\NovaisFPS.exe

# MSI-X Enforcer (força MSI/MSI-X em dispositivos críticos)
.\bin\Release\net8.0-windows\NovaisFPS.exe --msi-enforce

# Limpar Standby List (purge de memória)
.\bin\Release\net8.0-windows\NovaisFPS.exe --memory-clean

# Health Check (DPC/ISR latency + timer resolution)
.\bin\Release\net8.0-windows\NovaisFPS.exe --health

# PCIe TLP Size Diagnostics (otimização de nível zero)
.\bin\Release\net8.0-windows\NovaisFPS.exe --pcie-tlp-diagnose

# Memory Latency Diagnostics (otimização de nível zero)
.\bin\Release\net8.0-windows\NovaisFPS.exe --memory-latency-diagnose

# Micro-Architecture Optimization (Branch Prediction / L3 Cache)
.\bin\Release\net8.0-windows\NovaisFPS.exe --micro-arch-optimize

# End-to-End Latency Monitor
.\bin\Release\net8.0-windows\NovaisFPS.exe --latency
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

## Otimizações de Nível Zero (Zero-Level Optimizations)

As otimizações de nível zero são configurações que requerem acesso à BIOS/UEFI ou são altamente dependentes do hardware. O NOVAIS FPS fornece diagnósticos detalhados e guias passo a passo para essas otimizações, mas não pode aplicá-las programaticamente por questões de segurança e estabilidade.

### PCIe TLP Size Optimization

**O que é:** O TLP (Transaction Layer Packet) Size controla o tamanho dos pacotes de comunicação PCIe entre CPU, GPU e NVMe SSDs. Reduzir o tamanho do TLP pode diminuir a latência de comunicação.

**Impacto:** Redução de latência frame-to-photon, menor input lag, frametimes mais consistentes.

**Como aplicar:**
1. Execute o diagnóstico: `NovaisFPS.exe --pcie-tlp-diagnose`
2. Siga o guia interativo fornecido no console
3. Acesse a BIOS/UEFI durante o boot (F2, DEL, F10)
4. Navegue até: Advanced → PCIe Configuration → TLP Size
5. Configure para: 128 bytes (latência mínima)
6. Salve e reinicie

**Caminhos na BIOS por fabricante:**
- **ASUS:** Advanced → PCI Subsystem Settings → PCIe TLP Size
- **MSI:** Advanced → PCI Subsystem Settings → PCIe TLP Size
- **Gigabyte:** Advanced → PCIe Configuration → TLP Size
- **ASRock:** Advanced → Chipset Configuration → PCIe TLP Size

**Reverter:** Repita os passos e configure para 256 bytes (padrão).

### Memory Latency Optimization (Timings de RAM)

**O que é:** Otimização de timings primários (tCL, tRCD, tRP, tRAS) e secundários (tRFC, tREFI) da memória RAM para reduzir latência.

**Impacto:** Redução significativa de latência de memória, melhoria de 1% lows, frametimes mais consistentes.

**Como aplicar:**
1. Execute o diagnóstico: `NovaisFPS.exe --memory-latency-diagnose`
2. Revise as recomendações personalizadas baseadas no seu hardware
3. Acesse a BIOS/UEFI durante o boot
4. Navegue até: Advanced → Memory Configuration → DRAM Timing Control
5. Ajuste os timings conforme as recomendações (comece conservadoramente)
6. Teste estabilidade com MemTest86 ou HCI MemTest (4-8 horas)
7. Salve e reinicie

**Timings Primários (maior impacto):**
- **tCL (CAS Latency):** Reduzir por 1-2 ciclos
- **tRCD (RAS to CAS Delay):** Reduzir por 1 ciclo
- **tRP (RAS Precharge):** Reduzir por 1 ciclo
- **tRAS (Active to Precharge):** Reduzir por 2-4 ciclos

**Timings Secundários (fine-tuning):**
- **tRFC (Refresh Cycle Time):** Reduzir por 10-20 ciclos (crítico para latência)
- **tREFI (Refresh Interval):** Aumentar para máximo (65535 DDR4, 32767 DDR5)
- **tFAW, tWR, tRTP:** Reduzir conforme estabilidade permitir

**Links úteis:**
- AMD: [Ryzen Memory Tuning Guide](https://www.techpowerup.com/forums/threads/amd-ryzen-memory-tuning-guide.235110/)
- Intel: [Memory Overclocking Guide](https://www.overclock.net/threads/memory-overclocking-guide.1751608/)

**⚠️ AVISO:** Timings incorretos podem causar instabilidade, falhas de boot ou corrupção de dados. Sempre teste extensivamente antes de considerar estável.

### Hyper-Threading/SMT Disable

**O que é:** Desativação de Hyper-Threading (Intel) ou SMT (AMD) para reduzir latência e melhorar consistência de frametime em jogos competitivos.

**Impacto:** Redução de latência em alguns jogos competitivos, mas **redução significativa** de performance multi-core.

**Como aplicar:**
1. Durante a fase 5 (CPU Scheduler), opte por desativar Hyper-Threading/SMT
2. O NOVAIS FPS tentará aplicar via `bcdedit` (workaround limitado)
3. Para desativação completa, acesse a BIOS/UEFI:
   - **Intel:** Advanced → CPU Configuration → Hyper-Threading → Disabled
   - **AMD:** Advanced → AMD CBS → CPU Common Options → SMT Control → Disabled
4. Salve e reinicie

**⚠️ AVISO:** Desativar HT/SMT reduz drasticamente a performance em aplicações multi-threaded. Apenas recomendado para sistemas dedicados exclusivamente a jogos competitivos.

### CPU C-States Disable

**O que é:** Desativação de estados de economia de energia profundos da CPU (C3, C6, C7) para reduzir latência de transição.

**Impacto:** Redução de latência spikes, frametimes mais consistentes, mas **aumento de consumo de energia e temperatura**.

**Como aplicar:**
1. Durante a fase 5 (CPU Scheduler), opte por diagnosticar C-States
2. O NOVAIS FPS aplicará via `powercfg` e registro
3. Para máxima eficácia, desative também na BIOS/UEFI:
   - Advanced → CPU Configuration → CPU C-States
   - Desative: C3, C6, C7
   - Mantenha: C1/C1E (opcional)
4. Salve e reinicie
5. Monitore temperaturas após desativação

**⚠️ AVISO:** Desativar C-States aumenta significativamente o consumo de energia e temperatura da CPU. Monitore temperaturas e garanta refrigeração adequada.

### Core Isolation / Memory Integrity Disable

**O que é:** Desativação de proteções de segurança baseadas em virtualização (VBS/HVCI) para reduzir latência de acesso à memória.

**Impacto:** Pequena redução de latência (<1%), mas **aumento significativo de risco de segurança**.

**Como aplicar:**
1. Durante a fase 9 (Registry Tweaks), opte por desativar Core Isolation
2. Confirme explicitamente os avisos de segurança
3. O NOVAIS FPS aplicará via registro
4. **Reinicie o computador** para que as mudanças tenham efeito

**⚠️ AVISO CRÍTICO:** 
- Remove proteções críticas contra malware de nível kernel
- Sistema fica mais vulnerável a ataques avançados (APTs)
- **NÃO RECOMENDADO** para sistemas conectados a redes não confiáveis
- **NÃO RECOMENDADO** para sistemas com dados sensíveis
- Benefício de performance é mínimo para a maioria dos usuários

**Reverter:** Windows Security → Device Security → Core Isolation → Memory Integrity (On)

### Micro-Architecture Optimization (Branch Prediction / L3 Cache)

**O que é:** Ajustes finos de registro para otimizar prefetch da CPU e uso do cache L3.

**Impacto:** Melhoria marginal de eficiência de branch prediction e utilização de cache L3.

**Como aplicar:**
1. Execute: `NovaisFPS.exe --micro-arch-optimize`
2. As otimizações são aplicadas automaticamente via registro
3. Todas as mudanças são reversíveis

**Otimizações aplicadas:**
- Enhanced CPU Prefetch (EnablePrefetcher = 3)
- Large System Cache (LargeSystemCache = 1)
- Disable Paging Executive (DisablePagingExecutive = 1)

**⚠️ NOTA:** Essas otimizações têm impacto mínimo e podem não ser perceptíveis em todos os sistemas.

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

### Nível Zero (Zero-Level - Requer BIOS/UEFI)
- PCIe TLP Size diagnostics e guias interativos
- Memory timings diagnostics (primários e secundários)
- Hyper-Threading/SMT disable (com instruções BIOS)
- CPU C-States disable (com verificação pós-aplicação)
- Core Isolation / Memory Integrity disable (com avisos de segurança)
- Micro-architecture optimization (Branch Prediction / L3 Cache)

### Validação & Health
- Health metrics (DPC/ISR %, timer resolution)
- Relatório de status de otimizações aplicadas
- Sistema de rollback completo
- End-to-end latency monitoring

---

## Preparação para Deploy e Distribuição

### Build para Produção

```powershell
# Build otimizado para release
dotnet build -c Release

# Publicar como executável único (self-contained)
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

# O executável estará em: bin\Release\net8.0-windows\win-x64\publish\NovaisFPS.exe
```

### Estrutura de Distribuição

Para distribuir o NOVAIS FPS, inclua:

```
NOVAIS FPS/
├── NovaisFPS.exe                    # Executável principal
├── Scripts/                          # Scripts PowerShell (obrigatório)
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
├── README.md                         # Documentação
└── Licença.txt                       # (opcional)
```

### Requisitos do Sistema

- **Windows 10/11 x64** (build 1903 ou superior recomendado)
- **.NET 8.0 Runtime** (incluído se usar `--self-contained true`)
- **PowerShell 5.1+** (incluído no Windows) ou **PowerShell 7+** (recomendado)
- **Privilégios de Administrador** (obrigatório)

### Checklist de Deploy

- [ ] Build executado com sucesso (`dotnet build -c Release`)
- [ ] Testes executados em ambiente limpo (VM ou sistema de teste)
- [ ] Todos os scripts PowerShell estão presentes na pasta `Scripts/`
- [ ] README.md atualizado com informações relevantes
- [ ] Verificar que o executável funciona sem .NET SDK instalado (se self-contained)
- [ ] Testar rollback em ambiente de teste
- [ ] Verificar logs em `Logs/` após execução
- [ ] Documentar requisitos e avisos de segurança

### Avisos Importantes para Usuários Finais

1. **Sempre execute como Administrador** - O NOVAIS FPS requer privilégios elevados
2. **Faça backup antes de aplicar** - O sistema cria backups automáticos, mas é recomendado criar um ponto de restauração do Windows
3. **Teste estabilidade após otimizações** - Algumas otimizações podem causar instabilidade em sistemas específicos
4. **Otimizações de nível zero requerem BIOS/UEFI** - Siga os guias interativos fornecidos
5. **Core Isolation disable reduz segurança** - Apenas para sistemas dedicados a jogos competitivos
6. **Monitore temperaturas** - Desativar C-States aumenta consumo de energia e temperatura

### Suporte e Troubleshooting

**Problemas comuns:**

- **"Administrator privileges required"**: Execute o terminal como Administrador
- **Scripts bloqueados**: O NOVAIS FPS tenta desbloquear automaticamente, mas se falhar, execute: `Get-ChildItem -Path Scripts -Filter *.ps1 | Unblock-File`
- **Rollback não funciona**: Verifique se o arquivo `context-<runid>.json` existe em `Logs/`
- **BIOS/UEFI não acessível**: Consulte o manual da sua placa-mãe para a tecla correta

**Logs:**
- Logs principais: `Logs/novaisfps-<runid>.log`
- Context JSON: `Logs/context-<runid>.json`
- Logs de módulos específicos: `Logs/<module>-<timestamp>.log`

---

## Licença

Este projeto é fornecido "como está" para fins educacionais e de otimização de performance. Use por sua conta e risco.


