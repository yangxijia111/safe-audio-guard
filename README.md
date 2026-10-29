# SafeAudioGuard

**[中文](#中文) | [English](#english)**

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![platform](https://img.shields.io/badge/platform-Windows%20x64-0078D4?logo=windows&logoColor=white)
![dependencies](https://img.shields.io/badge/dependencies-none-brightgreen)
![license](https://img.shields.io/badge/license-MIT-blue)
![build](https://github.com/yangxijia111/safe-audio-guard/actions/workflows/windows-build.yml/badge.svg)

---

## 中文

**SafeAudioGuard** 是一个常驻托盘的小工具：当你连接耳机、音箱等音频输出设备，或切换默认输出设备时，如果系统音量超过 **45%**，它会自动把音量降到 45%，避免音量突然跳到 100% 刺伤耳朵。连接之后你可以随时自行调高音量——它只在你连接/切换设备的那一小段时间内起作用，不会持续压制你手动调节的音量。

### 功能特性

- **连接时限音**：输出端点音量超过 45% 时降到 45%，低于 45% 不动；只减不增，绝不偷偷调高。
- **不碰无关状态**：不改变静音状态，不修改麦克风等采集设备，只处理 `DataFlow.Render` 输出端点。
- **覆盖所有输出设备**：蓝牙耳机、3.5mm 有线耳机、USB 声卡、HDMI/DP 输出等。
- **短窗口复查**：设备事件后在 0 / 60 / 180 / 450 / 900 / 1800 毫秒各复查一次，覆盖部分驱动“先激活、后恢复旧音量”的行为；窗口之外不持续限音。
- **托盘驻留**：无主窗口、单实例运行，右键托盘图标即可暂停/恢复、手动检查、查看日志。
- **开机自启**：通过当前用户 `HKCU\...\Run` 注册表项实现，正式版首次运行默认开启，托盘菜单可随时关闭，**不需要管理员权限**。
- **隐私友好日志**：设备 ID 只记录 SHA-256 指纹，不写入完整设备 ID；日志保存在本地 `%LOCALAPPDATA%\SafeAudioGuard\app.log`。
- **零第三方依赖**：C# + .NET 10 WinForms + 原生 Core Audio COM 互操作，没有任何 NuGet 包。

### 实际运行日志（真实输出，非示意）

```
[2026-10-29 13:15:09.383 +08:00] 已注册音频设备事件；输出设备保护上限 45%。
[2026-10-29 13:15:09.412 +08:00] 已限制 [292C347C] 启动时安全检查: 74% -> 45%
```

> 项目暂无截图 / GIF。以上为真实运行日志节选（`[292C347C]` 为设备 ID 的哈希指纹）。
> 已验证：Windows 11 x64 上启动安全检查实测将 74% 钳制为 45%。**尚未**在实体蓝牙耳机上验证连接瞬间效果，硬件验收清单见 [docs/ACCEPTANCE.md](docs/ACCEPTANCE.md)。

### 环境要求

| 项目 | 要求 |
|---|---|
| 操作系统 | Windows 10 / 11，x64 |
| 运行 | 无需安装 .NET 运行时（自包含单文件发布） |
| 构建 | [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) x64 |

### 安装

1. 获取 `SafeAudioGuard.exe`（自行构建，见下文；或从信任的渠道获取构建产物）。仓库暂未发布 Release。
2. **先把它移动到固定目录**再运行，例如 `%LOCALAPPDATA%\Programs\SafeAudioGuard\`。不要放在临时/下载目录，**不要改名**（开机自启注册要求文件名为 `SafeAudioGuard.exe`）。
3. 双击运行。没有窗口，任务栏托盘会出现一个盾牌图标（若隐藏，点托盘区“^”展开）。
4. 首次运行会自动注册开机自启；如失败，托盘会弹出提示，可在右键菜单中重试。

### 使用

右键托盘盾牌图标：

| 菜单项 | 作用 |
|---|---|
| 暂停保护 / 恢复保护 | 临时关闭/开启自动限音；恢复时会立即做一次安全检查 |
| 立即检查所有活动输出 | 手动对所有活动输出端点执行一次钳制检查 |
| 开机自动启动 | 切换 HKCU Run 注册表项 |
| 查看运行日志 | 用记事本打开 `app.log` |
| 退出程序 | 退出并停止监听 |

**卸载**：右键托盘 → 取消“开机自动启动”→ 退出程序 → 删除程序文件夹；如需清理配置与日志，删除 `%LOCALAPPDATA%\SafeAudioGuard\`。

### 工作原理

```
系统音频事件（设备新增 / 设备激活 / 默认输出切换）
  → IMMNotificationClient 回调（只入队，不阻塞、不做 COM 操作）
  → Channel 队列 + 代际去重
  → 后台 MTA 线程：激活端点 IAudioEndpointVolume
  → 确认 DataFlow.Render → 读取当前音量 → 超过 45% 则写入 45% → 回读校验
  → 0–1800ms 短窗口内按 6 个时间点复查，之后不再干预
```

- 遵循 Microsoft 对 `IMMNotificationClient` 回调的规定：系统回调内不做 COM 查询、文件 IO 或锁等待。
- 每次检查独立创建/释放 COM 对象，不跨线程共享 RCW。
- 先注册事件回调，再枚举活动输出，降低启动期漏事件的概率。
- 音量写入后会回读校验，若驱动回读仍高于阈值则记录日志。

### 已知限制（请务必阅读）

**本工具不能保证“刚连上时绝对没有一瞬间巨响”。** Windows Core Audio 的设备状态/默认设备通知是在事件发生**之后**才到达应用程序的，无法在系统播放音频之前原子拦截。蓝牙 Absolute Volume、耳机独立硬件音量、WASAPI 独占模式、驱动延迟、休眠恢复等因素都可能影响结果。短窗口复查只是降低风险。

调试验时**请不要佩戴耳机**进行高音量测试。

### 项目结构

```text
SafeAudioGuard/
├── SafeAudioGuard/                 # 主程序（WinForms 托盘应用）
│   ├── Audio/
│   │   ├── CoreAudioInterop.cs     # IMMDeviceEnumerator / IMMNotificationClient / IAudioEndpointVolume 原生 COM 互操作
│   │   ├── AudioGuardMonitor.cs    # 设备事件监听、检查队列、短窗口复查、音量钳制
│   │   └── ProtectionPolicy.cs     # 45% 上限策略（只减不增 + 浮点容差）
│   ├── UI/
│   │   └── TrayApplicationContext.cs  # 托盘菜单与交互
│   ├── Infrastructure/
│   │   ├── AppLog.cs               # 日志与设备 ID 指纹（SHA-256）
│   │   ├── SettingsStore.cs        # 设置持久化（settings.json）
│   │   └── StartupManager.cs       # HKCU Run 开机自启注册
│   ├── Program.cs                  # 入口、单实例（命名 Mutex）
│   └── app.manifest                # asInvoker，无需管理员权限
├── SafeAudioGuard.PolicyTests/     # 纯逻辑自测，不修改系统音量
├── .github/workflows/              # GitHub Actions：Windows 自测 + 单文件发布
├── docs/                           # 硬件验收清单、开发规格
└── build.ps1                       # 一键：策略自测 + 发布 dist/SafeAudioGuard.exe
```

### 构建 / 测试

```powershell
# 1. 运行纯逻辑自测（不触碰真实硬件音量）
dotnet run --project SafeAudioGuard.PolicyTests/SafeAudioGuard.PolicyTests.csproj -c Release

# 2. 一键构建（自测 + 发布自包含单文件 EXE 到 dist/）
./build.ps1

# 或手动发布
dotnet publish SafeAudioGuard/SafeAudioGuard.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist
```

发布产物为约 50 MB 的自包含单文件 EXE，这是预期行为（内含 .NET 运行时）；不建议为减体积随意开启 trimming 或 NativeAOT。

硬件层面的验收测试（T01–T12）见 [docs/ACCEPTANCE.md](docs/ACCEPTANCE.md)。

### Roadmap

- [ ] 在实体蓝牙耳机上完成 T01–T12 硬件验收（尤其蓝牙 Absolute Volume 场景）
- [ ] 音量上限可配置（托盘界面）
- [ ] 可选的“更保守模式”：启动时预调低所有已连接输出端点（需用户显式同意）
- [ ] 休眠/唤醒后事件丢失的兜底补偿检查

### 贡献

欢迎 Issue 和 Pull Request。提交前请：

1. 运行策略自测：`dotnet run --project SafeAudioGuard.PolicyTests/SafeAudioGuard.PolicyTests.csproj -c Release`
2. 保持“零第三方 NuGet 依赖”和“只减不增”的语义
3. 不引入网络通信、遥测或敏感信息采集

开发背景与固定需求见 [docs/AGENT_PROMPT.md](docs/AGENT_PROMPT.md)。

### 致谢

- [IMMNotificationClient (Microsoft Docs)](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nn-mmdeviceapi-immnotificationclient)
- [RegisterEndpointNotificationCallback](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nf-mmdeviceapi-immdeviceenumerator-registerendpointnotificationcallback)
- [IAudioEndpointVolume::SetMasterVolumeLevelScalar](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nf-endpointvolume-iaudioendpointvolume-setmastervolumelevelscalar)
- [Endpoint Volume Controls](https://learn.microsoft.com/en-us/windows/win32/coreaudio/endpoint-volume-controls)
- [.NET single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/)

### License

本项目采用 [MIT License](LICENSE) 开源。

---

## English

**SafeAudioGuard** is a small tray-resident utility for Windows. When you connect an audio output device (headset, speaker, USB DAC, HDMI/DP output, …) or switch the default output device, and the system volume is above **45%**, it lowers the volume to 45% automatically — so the volume never jumps to 100% and blasts your ears. After connecting, you can raise the volume yourself anytime: the tool only acts in the short window right after a connect/switch event, and never keeps suppressing your manual adjustments.

### Features

- **Volume clamp on connect**: any render endpoint above 45% is lowered to 45%; below 45% nothing changes. Reduce-only — it never raises your volume.
- **Respects unrelated state**: never touches the mute state, never modifies capture devices (microphones); only `DataFlow.Render` endpoints are processed.
- **All output devices**: Bluetooth headsets, 3.5mm headphones, USB sound cards, HDMI/DP outputs, etc.
- **Short-window re-checks**: after a device event, checks are repeated at 0 / 60 / 180 / 450 / 900 / 1800 ms to cover drivers that restore an old volume shortly after activation. Outside that window the volume is not continuously limited.
- **Tray resident**: no main window, single instance. Right-click the tray icon to pause/resume, run a manual check, or view logs.
- **Start with Windows**: registers a per-user `HKCU\...\Run` entry; enabled by default on first run of a Release build, toggleable from the tray menu, **no administrator rights required**.
- **Privacy-friendly logging**: device IDs are recorded as SHA-256 fingerprints only, never in full. Logs stay local at `%LOCALAPPDATA%\SafeAudioGuard\app.log`.
- **Zero third-party dependencies**: C# + .NET 10 WinForms + raw Core Audio COM interop. No NuGet packages at all.

### Real log output (not a mockup)

```
[2026-10-29 13:15:09.383 +08:00] 已注册音频设备事件；输出设备保护上限 45%。
[2026-10-29 13:15:09.412 +08:00] 已限制 [292C347C] 启动时安全检查: 74% -> 45%
```

> No screenshots/GIF yet. The lines above are an excerpt of a real run (`[292C347C]` is a hash fingerprint of the device ID).
> Verified on Windows 11 x64: the startup safety check lowered 74% to 45%. **Not yet verified** on physical Bluetooth hardware — see the hardware acceptance checklist in [docs/ACCEPTANCE.md](docs/ACCEPTANCE.md).

### Requirements

| Item | Requirement |
|---|---|
| OS | Windows 10 / 11, x64 |
| Running | No .NET runtime installation needed (self-contained single file) |
| Building | [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) x64 |

### Installation

1. Obtain `SafeAudioGuard.exe` (build it yourself, see below, or get a build from a trusted source). No Release has been published yet.
2. **Move it to a fixed directory first**, e.g. `%LOCALAPPDATA%\Programs\SafeAudioGuard\`. Do not run it from a temp/download folder, and **do not rename it** (the auto-start registration requires the file name `SafeAudioGuard.exe`).
3. Double-click to run. There is no window; a shield icon appears in the taskbar tray (click “^” if hidden).
4. The first run registers the auto-start entry automatically; if that fails, the tray shows a balloon tip and you can retry from the tray menu.

### Usage

Right-click the tray shield icon:

| Menu item | Action |
|---|---|
| Pause / Resume protection | Temporarily disable/enable auto clamping; resuming triggers an immediate safety check |
| Check all active outputs now | Run one clamp pass over all active render endpoints |
| Start with Windows | Toggle the HKCU Run registry entry |
| View log | Open `app.log` in Notepad |
| Exit | Stop monitoring and quit |

**Uninstall**: right-click the tray → uncheck “Start with Windows” → Exit → delete the program folder. To also remove settings and logs, delete `%LOCALAPPDATA%\SafeAudioGuard\`.

### How it works

```
Windows audio event (device added / device ACTIVE / default device changed)
  → IMMNotificationClient callback (enqueue only — never block, no COM work)
  → Channel queue + per-device generation dedup
  → Background MTA thread: activate IAudioEndpointVolume on the endpoint
  → Verify DataFlow.Render → read current level → set to 45% if above → read back to verify
  → Re-checks at 6 time points within 0–1800 ms, then hands control back to the user
```

- Follows Microsoft's rules for `IMMNotificationClient`: no COM queries, file IO, or lock waits inside system callbacks.
- Each check creates and releases its own COM objects; no RCW is shared across threads.
- Event callbacks are registered *before* enumerating active outputs, reducing missed events at startup.
- After writing the volume, the value is read back and verified; a driver that reports a higher level is logged.

### Known limitations (please read)

**This tool cannot guarantee “absolutely no loud burst at the moment of connection”.** Windows Core Audio device-state/default-device notifications arrive *after* the event has already happened; an ordinary application cannot atomically intercept audio before the system plays it. Bluetooth Absolute Volume, independent hardware volume on the headset, WASAPI exclusive mode, driver latency, and sleep/resume can all affect the outcome. The short-window re-checks only reduce the risk.

**Do not wear headphones during high-volume testing.**

### Project structure

```text
SafeAudioGuard/
├── SafeAudioGuard/                 # Main app (WinForms tray application)
│   ├── Audio/
│   │   ├── CoreAudioInterop.cs     # Raw COM interop: IMMDeviceEnumerator / IMMNotificationClient / IAudioEndpointVolume
│   │   ├── AudioGuardMonitor.cs    # Device events, check queue, short-window re-checks, volume clamping
│   │   └── ProtectionPolicy.cs     # 45% cap policy (reduce-only + float tolerance)
│   ├── UI/
│   │   └── TrayApplicationContext.cs  # Tray menu and interaction
│   ├── Infrastructure/
│   │   ├── AppLog.cs               # Logging with SHA-256 device fingerprints
│   │   ├── SettingsStore.cs        # Settings persistence (settings.json)
│   │   └── StartupManager.cs       # HKCU Run auto-start registration
│   ├── Program.cs                  # Entry point, single instance (named mutex)
│   └── app.manifest                # asInvoker — no admin rights needed
├── SafeAudioGuard.PolicyTests/     # Pure-logic self-tests, never touch real hardware volume
├── .github/workflows/              # GitHub Actions: Windows tests + single-file publish
├── docs/                           # Hardware acceptance checklist, development spec
└── build.ps1                       # One shot: policy tests + publish dist/SafeAudioGuard.exe
```

### Build / Test

```powershell
# 1. Run the pure-logic self-tests (no real hardware volume is modified)
dotnet run --project SafeAudioGuard.PolicyTests/SafeAudioGuard.PolicyTests.csproj -c Release

# 2. One-shot build (tests + publish self-contained single-file EXE to dist/)
./build.ps1

# Or publish manually
dotnet publish SafeAudioGuard/SafeAudioGuard.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist
```

The published artifact is a ~50 MB self-contained single-file EXE — expected, because it embeds the .NET runtime. Enabling trimming or NativeAOT to shrink it is not recommended.

Hardware acceptance tests (T01–T12) are documented in [docs/ACCEPTANCE.md](docs/ACCEPTANCE.md).

### Roadmap

- [ ] Complete the T01–T12 hardware acceptance on physical Bluetooth headsets (especially Bluetooth Absolute Volume)
- [ ] Configurable volume cap via the tray UI
- [ ] Optional “conservative mode”: pre-lower all connected render endpoints at startup (requires explicit user consent)
- [ ] Fallback compensation check after sleep/resume in case events are lost

### Contributing

Issues and pull requests are welcome. Before submitting:

1. Run the policy self-tests: `dotnet run --project SafeAudioGuard.PolicyTests/SafeAudioGuard.PolicyTests.csproj -c Release`
2. Keep the “zero third-party NuGet dependencies” and “reduce-only” semantics intact
3. Do not add network communication, telemetry, or collection of sensitive information

Development background and fixed requirements: [docs/AGENT_PROMPT.md](docs/AGENT_PROMPT.md).

### Acknowledgements

- [IMMNotificationClient (Microsoft Docs)](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nn-mmdeviceapi-immnotificationclient)
- [RegisterEndpointNotificationCallback](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nf-mmdeviceapi-immdeviceenumerator-registerendpointnotificationcallback)
- [IAudioEndpointVolume::SetMasterVolumeLevelScalar](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nf-endpointvolume-iaudioendpointvolume-setmastervolumelevelscalar)
- [Endpoint Volume Controls](https://learn.microsoft.com/en-us/windows/win32/coreaudio/endpoint-volume-controls)
- [.NET single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/)

### License

This project is licensed under the [MIT License](LICENSE).
