# Rise Overlay（Rise血条）

**当前版本：2.0.0** · [GitHub Releases](https://github.com/tttengyuan/rise-overlay/releases/latest)

基于 **[HunterPie](https://github.com/HunterPie/HunterPie)** 的 Monster Hunter Rise 专用紧凑叠层。

## 归属与许可

本仓库是 HunterPie 的**瘦身 Fork**：在原版架构上主要提取 / 强化 **Rise 紧凑战斗 HUD**（任务简报、怪物血条 / 部位 / 异常、DPS），默认关闭多游戏客户端、Discord、以及 Rise 侧大量非必要控件。

上游：https://github.com/HunterPie/HunterPie  
本 Fork 发布页：https://github.com/tttengyuan/rise-overlay/releases  

HunterPie 原版许可证条款适用于上游代码；详见根目录 `LICENSE`。

## 下载与更新

1. 从 [Releases](https://github.com/tttengyuan/rise-overlay/releases/latest) 下载 `RiseOverlay-*.zip`
2. 解压到任意有写权限的目录（勿放桌面）
3. 运行 `HunterPie.exe`，再启动 Monster Hunter Rise
4. **Ctrl+Alt+O** 开关叠层；壳窗口可「检查更新」（读 GitHub Releases）

需要 [.NET Desktop Runtime 10](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-10.0.3-windows-x64-installer)。

## 2.0 新内容

- **六套可切换 HUD 主题**（简报 / 战斗卡 / DPS 共用，写入配置下次自动恢复）
- **战斗动效总开关**（虚弱越过线、晕眩抖动、破部位闪、推荐弱属性脉冲等）
- 任务简报内边距与属性 chip 加大；「弱 / 推荐」单行对齐
- **怪异化**：啮生虫阈值 + 感染部位「怪异化」红轨
- **部位状态校正**：已破坏 / 已断尾锁定；打掉虫核不会把已破部位变回「可破」；讨伐后残血 1 点显示为 0
- 壳窗口主题网格 + 退出按钮布局修复

## 做什么

Rise-only 叠层，聚焦：

- 任务简报（弱属性 / 推荐属性）
- 怪物 HP、部位破坏 / 断尾、异常状态
- 怪异化啮生虫与感染部位
- 队伍 DPS / 总伤
- 捕获线（怪异化任务不可捕）
- 壳窗口主题、动效、检查更新

**仅支持 Monster Hunter Rise。** World / Wilds 附加默认关闭。

## UI 主题

壳窗口 **HUD 主题** 可选，简报 + 战斗 HUD + DPS 共用：

| ThemeId | 名称 |
| --- | --- |
| `Classic` | 现版金青（默认） |
| `Glass` | 玻璃 |
| `Parchment` | 羊皮纸 |
| `Soft` | 日系浅色 |
| `OledCoral` | OLED 珊瑚 |
| `GlassCoral` | 玻璃珊瑚 |

**战斗动效**：关闭后保留语义色（怪异化红、断尾橙等），去掉脉冲 / 抖动类动画。

设计说明：`docs/superpowers/specs/2026-09-02-rise-overlay-themes-motion-design.md`

## 快捷键

| 操作 | 默认 | 配置 |
| --- | --- | --- |
| 显示 / 隐藏叠层 | **Ctrl+Alt+O** | `OverlayClientConfig.ToggleVisibility` |
| 设计模式（拖拽控件） | ScrollLock | `OverlayClientConfig.ToggleDesignMode` |

改键后需重启。配置一般在客户端目录 `config.json`。

## 简报 ↔ 战斗切换

同一宿主控件在 **任务简报** 与 **战斗叠层** 间切换：

| 场景 | 时机 |
| --- | --- |
| **简报** | 任务已开始、尚未出现存活大怪，且能解析到目标 |
| **战斗** | 任务中且存在 `Health > 0` 的大怪（锁定目标优先） |
| **隐藏** | 任务结束 / 空闲，或不满足上述条件 |

## 构建与发布

### 前置

- .NET SDK（`net10.0-windows`）
- VS2022 C++ 桌面工作负载（编译 `HunterPie.Native` x64）
- Rise 地址表（与上游 HunterPie 相同，放在应用 `Address` 目录）

### 常用命令

```powershell
dotnet build HunterPie/HunterPie.csproj -c Release
powershell -ExecutionPolicy Bypass -File Scripts\sync-publish.ps1
```

发版（需 `gh auth login`）：

```powershell
powershell -ExecutionPolicy Bypass -File Scripts\release.ps1 -Version 2.0.0
```

会 bump `AssemblyInformationalVersion`、打 `RiseOverlay-*.zip` 并创建 GitHub Release。若本机 `HunterPie.exe` 正占用 `publish\`，请先退出再同步 / 打包。

Native DLL 需单独用 VS 编好后放到 `libs\HunterPie.Native.dll`。确认 `static\monsters-overlay.json` 存在。

### QA

`docs/superpowers/specs/2026-08-31-rise-overlay-qa-checklist.md`

## 技术摘要（开发者）

- 紧凑控件：`MHROverlayConfig.RiseCompactMonsterWidget`（Rise 默认开启）
- 静态弱点表：`static/monsters-overlay.json`（来自 `RiseOverlay.Data`）
- 叠层仍走 HunterPie `OverlayManager` / 屏幕坐标路径，**不会**随游戏窗口拖动自动吸附（已知限制，见 QA §7）
- 更多设计文档：`docs/superpowers/`
