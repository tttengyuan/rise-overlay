# Rise Overlay（Rise血条）

[![Release](https://img.shields.io/github/v/release/tttengyuan/rise-overlay?label=Release&color=4A9B9B)](https://github.com/tttengyuan/rise-overlay/releases/latest)
[![License](https://img.shields.io/github/license/tttengyuan/rise-overlay?color=725E39)](LICENSE)

**Monster Hunter Rise 专用紧凑战斗叠层** · 当前版本 **2.0.0**

> 本项目是 [HunterPie](https://github.com/HunterPie/HunterPie) 的瘦身 Fork：只做 Rise，默认关掉多游戏客户端、Discord 以及大量非必要控件。  
> 上游代码仍遵循其许可证，见 [`LICENSE`](LICENSE)。

---

## 下载

1. 打开 [Releases](https://github.com/tttengyuan/rise-overlay/releases/latest)，下载 `RiseOverlay-*.zip`
2. 解压到有写权限的目录（**不要放桌面**）
3. 安装 [.NET Desktop Runtime 10](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-10.0.3-windows-x64-installer)（若尚未安装）
4. 运行 `HunterPie.exe`，再启动《怪物猎人：崛起》
5. **Ctrl+Alt+O** 开关叠层

壳窗口可点 **检查更新**（读取本仓库 GitHub Releases）。

---

## 功能

| 模块 | 说明 |
| --- | --- |
| 任务简报 | 接任务后显示弱属性 / 推荐属性、可否捕获、断尾等 |
| 战斗 HUD | 怪物血量、虚弱线、愤怒 / 晕眩 / 耐力、异常积累 |
| 部位 | 可破 / 可断、已破坏 / 已断尾锁定；怪异化啮生虫与感染部位 |
| DPS | 单人 / 组队总伤与占比 |
| 主题 | 七套 HUD 主题，简报 + 战斗 + DPS 共用并持久化 |
| 动效 | 虚弱越过线、晕眩、破部位、推荐弱属性脉冲（可关） |

**仅支持 Monster Hunter Rise / 破晓。** 不附带 World、Wilds。

---

## 2.0 要点

- 七套主题：`Classic` / `Glass` / `Parchment` / `Soft` / `OledCoral` / `GlassCoral` / `Transparent`
- 壳窗口主题网格 + 战斗动效开关
- 怪异化：啮生虫阈值、感染部位红轨；打掉虫核不会把已破部位变回「可破」
- 部位破坏 / 断尾状态锁定；讨伐后内存残血 1 点显示为 0
- 简报内边距与属性 chip 加大

---

## 主题一览

| ThemeId | 名称 |
| --- | --- |
| `Classic` | 现版金青（默认） |
| `Glass` | 玻璃 |
| `Parchment` | 羊皮纸 |
| `Soft` | 日系浅色 |
| `OledCoral` | OLED 珊瑚 |
| `GlassCoral` | 玻璃珊瑚 |
| `Transparent` | 透明（浅底不挡画面） |

设计说明：[`docs/superpowers/specs/2026-09-02-rise-overlay-themes-motion-design.md`](docs/superpowers/specs/2026-09-02-rise-overlay-themes-motion-design.md)

---

## 快捷键

| 操作 | 默认 |
| --- | --- |
| 显示 / 隐藏叠层 | **Ctrl+Alt+O** |
| 设计模式（拖拽位置） | ScrollLock |

改键后需重启客户端。配置一般在程序目录 `config.json`。

---

## 构建（开发者）

```powershell
dotnet build HunterPie/HunterPie.csproj -c Release
powershell -ExecutionPolicy Bypass -File Scripts\sync-publish.ps1
```

发版：

```powershell
powershell -ExecutionPolicy Bypass -File Scripts\release.ps1 -Version 2.0.0
```

Native 模块需用 VS2022 单独编译 `HunterPie.Native`，输出放到 `libs\HunterPie.Native.dll`。更多细节见 [`README.RiseOverlay.md`](README.RiseOverlay.md)。

---

## 与上游的关系

| | Rise Overlay（本仓库） | HunterPie 原版 |
| --- | --- | --- |
| 定位 | Rise 专用紧凑 HUD | 多游戏全能叠层 |
| 下载 | [本仓库 Releases](https://github.com/tttengyuan/rise-overlay/releases) | [HunterPie 官网 / Releases](https://github.com/HunterPie/HunterPie) |
| 文档 | 本文 + `README.RiseOverlay.md` | [docs.hunterpie.com](https://docs.hunterpie.com) |

感谢 [HunterPie](https://github.com/HunterPie/HunterPie) 作者与社区提供的架构与 Rise 数据源。
