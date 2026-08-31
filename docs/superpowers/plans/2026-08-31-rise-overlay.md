# Rise Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 基于 HunterPie 瘦身 Fork，交付 Rise 专用紧凑叠层：任务简报 + 战斗怪物面板 + 组队 DPS，吸附游戏窗口。

**Architecture:** 保留 `HunterPie.Core` / Rise 集成中的读内存与伤害计量；新增 `RiseOverlay.Domain`（纯逻辑 DTO/算法，可单测）与 `RiseOverlay.UI`（紧凑 WPF 视图）。用新 Widget 替换默认 Monster / Damage 外观；禁用 World/Wilds 与无关 Widget。静态弱点表从百科 `monsters.json` 导入。

**Tech Stack:** C# / .NET（与上游 HunterPie 一致）、WPF、xUnit（或上游已有测试框架）、JSON 静态表。

**Spec:** `docs/superpowers/specs/2026-08-31-rise-overlay-design.md`  
**建议仓库根目录:** `F:\rise-overlay`（独立 git；可将本 `docs/` 拷入或 submodule 引用）

---

## File structure (目标树)

```
F:\rise-overlay\
  HunterPie.sln                          # 上游解决方案（裁剪后）
  RiseOverlay.Domain\                    # 新建：纯逻辑
    ElementIds.cs
    ElementRecommend.cs
    PartWeakness.cs
    OverlayDtos.cs                       # MonsterHudDto, PartDto, AilmentDto, StatusLineDto, DpsDto…
    CaptureRules.cs
    StatusLineFormatter.cs
  RiseOverlay.Domain.Tests\
    ElementRecommendTests.cs
    PartWeaknessTests.cs
    StatusLineFormatterTests.cs
    CaptureRulesTests.cs
  RiseOverlay.Data\
    MonsterStaticStore.cs                # 加载 static/monsters-overlay.json
    static\monsters-overlay.json         # 从百科导出的精简表
  RiseOverlay.UI\                        # 新建：紧凑叠层 UI
    Themes\CompactTeal.xaml
    Controls\ElementChip.xaml(.cs)
    Controls\StatusLineText.xaml(.cs)
    Views\MonsterHudView.xaml(.cs)
    Views\DpsPanelView.xaml(.cs)
    Views\QuestBriefingView.xaml(.cs)
    Views\CombatStackView.xaml(.cs)      # MonsterHud + Dps 纵向堆叠
    ViewModels\MonsterHudViewModel.cs
    ViewModels\DpsPanelViewModel.cs
    ViewModels\QuestBriefingViewModel.cs
    Mapping\MonsterHudMapper.cs          # IMonster / 静态表 → DTO → VM
  Scripts\
    export_overlay_monsters.py           # 从 mhrise-app monsters.json 生成静态表
  docs\superpowers\specs\...
  docs\superpowers\plans\...
```

上游关键路径（克隆后核对，名称随版本可能微调）：

- Rise 怪物：`HunterPie.Integrations` / `HunterPie.Core` 下 `Game`/`Rise`/`MHRMonster*`
- 伤害：`HunterPie.UI/Overlay/Widgets/Damage/`
- 怪物 Widget：`HunterPie.UI/Overlay/Widgets/Monster/`
- 窗口吸附：`HunterPie.UI` Overlay Widget 基类 / `WidgetManager`

---

### Task 1: 克隆仓库并建立可编译基线

**Files:**
- Create: `F:\rise-overlay\`（git clone）
- Create: `F:\rise-overlay\README.RiseOverlay.md`
- Copy: spec + 本 plan 到 `F:\rise-overlay\docs\superpowers\`

- [ ] **Step 1: 克隆 HunterPie**

```powershell
git clone --recurse-submodules https://github.com/HunterPie/HunterPie.git F:\rise-overlay
cd F:\rise-overlay
git remote rename origin upstream
```

- [ ] **Step 2: 用 Visual Studio / `dotnet build` 确认上游可编译**

```powershell
cd F:\rise-overlay
dotnet build HunterPie.sln -c Debug
```

Expected: Build succeeded（若失败，先按上游 README 安装所需 .NET SDK / VS 工作负载后再继续，不要改业务代码）。

- [ ] **Step 3: 写清许可证与归因**

在 `README.RiseOverlay.md` 写明：基于 HunterPie（其 LICENSE）、本仓库修改目的为 Rise 专用紧凑 HUD，并链接上游。

- [ ] **Step 4: 拷贝文档**

```powershell
mkdir F:\rise-overlay\docs\superpowers\specs -Force
mkdir F:\rise-overlay\docs\superpowers\plans -Force
copy F:\mhrise-app\docs\superpowers\specs\2026-08-31-rise-overlay-design.md F:\rise-overlay\docs\superpowers\specs\
copy F:\mhrise-app\docs\superpowers\plans\2026-08-31-rise-overlay.md F:\rise-overlay\docs\superpowers\plans\
```

- [ ] **Step 5: Commit**

```bash
git add README.RiseOverlay.md docs
git commit -m "docs: add Rise Overlay spec, plan, and attribution README"
```

---

### Task 2: Domain — 元素推荐与部位弱点（TDD）

**Files:**
- Create: `RiseOverlay.Domain/ElementIds.cs`
- Create: `RiseOverlay.Domain/ElementRecommend.cs`
- Create: `RiseOverlay.Domain/PartWeakness.cs`
- Create: `RiseOverlay.Domain.Tests/ElementRecommendTests.cs`
- Create: `RiseOverlay.Domain.Tests/PartWeaknessTests.cs`
- Modify: `HunterPie.sln`（加入两个新项目）

- [ ] **Step 1: 创建 classlib + xunit 项目并加入解决方案**

```powershell
cd F:\rise-overlay
dotnet new classlib -n RiseOverlay.Domain -f net8.0
dotnet new xunit -n RiseOverlay.Domain.Tests -f net8.0
dotnet sln HunterPie.sln add RiseOverlay.Domain\RiseOverlay.Domain.csproj
dotnet sln HunterPie.sln add RiseOverlay.Domain.Tests\RiseOverlay.Domain.Tests.csproj
dotnet add RiseOverlay.Domain.Tests\RiseOverlay.Domain.Tests.csproj reference RiseOverlay.Domain\RiseOverlay.Domain.csproj
```

（若上游 TFM 不是 net8.0，把 `-f` 改成与 `HunterPie.Core` 相同的 TFM。）

- [ ] **Step 2: 写失败测试 — 并列推荐**

```csharp
// RiseOverlay.Domain.Tests/ElementRecommendTests.cs
using RiseOverlay.Domain;

public class ElementRecommendTests
{
    [Fact]
    public void Valstrax_head_all_25_except_dragon_recommends_four()
    {
        var values = new Dictionary<ElementId, int>
        {
            [ElementId.Fire] = 25,
            [ElementId.Water] = 25,
            [ElementId.Ice] = 25,
            [ElementId.Thunder] = 25,
            [ElementId.Dragon] = 0,
        };
        var result = ElementRecommend.FromHitzones(values);
        Assert.Equal(
            new[] { ElementId.Fire, ElementId.Water, ElementId.Ice, ElementId.Thunder },
            result);
    }

    [Fact]
    public void Single_peak_recommends_only_that_element()
    {
        var values = new Dictionary<ElementId, int>
        {
            [ElementId.Fire] = 5,
            [ElementId.Water] = 10,
            [ElementId.Ice] = 15,
            [ElementId.Thunder] = 30,
            [ElementId.Dragon] = 0,
        };
        Assert.Equal(new[] { ElementId.Thunder }, ElementRecommend.FromHitzones(values));
    }

    [Fact]
    public void All_zero_returns_empty()
    {
        var values = Enum.GetValues<ElementId>().ToDictionary(e => e, _ => 0);
        Assert.Empty(ElementRecommend.FromHitzones(values));
    }
}
```

- [ ] **Step 3: 运行测试确认失败**

```powershell
dotnet test RiseOverlay.Domain.Tests\RiseOverlay.Domain.Tests.csproj --filter ElementRecommendTests -v n
```

Expected: FAIL（类型/方法不存在）

- [ ] **Step 4: 实现 Domain**

```csharp
// RiseOverlay.Domain/ElementIds.cs
namespace RiseOverlay.Domain;

public enum ElementId { Fire, Water, Thunder, Ice, Dragon }

// RiseOverlay.Domain/ElementRecommend.cs
namespace RiseOverlay.Domain;

public static class ElementRecommend
{
    public static IReadOnlyList<ElementId> FromHitzones(IReadOnlyDictionary<ElementId, int> values)
    {
        if (values.Count == 0) return Array.Empty<ElementId>();
        var max = values.Values.Max();
        if (max <= 0) return Array.Empty<ElementId>();
        return values
            .Where(kv => kv.Value == max)
            .Select(kv => kv.Key)
            .OrderBy(e => e) // Fire,Water,Thunder,Ice,Dragon 枚举序
            .ToList();
    }
}

// RiseOverlay.Domain/PartWeakness.cs
namespace RiseOverlay.Domain;

public static class PartWeakness
{
    /// <summary>同推荐算法；UI 前加「弱」字。</summary>
    public static IReadOnlyList<ElementId> ForPart(IReadOnlyDictionary<ElementId, int> partElements)
        => ElementRecommend.FromHitzones(partElements);
}
```

- [ ] **Step 5: 测试通过并 Commit**

```powershell
dotnet test RiseOverlay.Domain.Tests\RiseOverlay.Domain.Tests.csproj -v n
```

```bash
git add RiseOverlay.Domain RiseOverlay.Domain.Tests HunterPie.sln
git commit -m "feat(domain): element recommend and part weakness helpers"
```

---

### Task 3: Domain — 状态行格式化与捕获规则（TDD）

**Files:**
- Create: `RiseOverlay.Domain/StatusLineFormatter.cs`
- Create: `RiseOverlay.Domain/CaptureRules.cs`
- Create: `RiseOverlay.Domain/OverlayDtos.cs`
- Create: `RiseOverlay.Domain.Tests/StatusLineFormatterTests.cs`
- Create: `RiseOverlay.Domain.Tests/CaptureRulesTests.cs`

- [ ] **Step 1: 定义 DTO（先写再测格式化）**

```csharp
// RiseOverlay.Domain/OverlayDtos.cs
namespace RiseOverlay.Domain;

public sealed record StatusLineModel(
    TimeSpan? EnrageRemaining,
    double? StunBuildupPercent,   // 0-100, null=未知
    bool StunActive,
    TimeSpan? StunActiveRemaining,
    double? StaminaPercent,
    TimeSpan? DownRemaining);

public sealed record PartDto(
    string Name,
    double CurrentHp,
    double MaxHp,
    bool IsSeverable,
    bool IsBroken,
    IReadOnlyList<ElementId> WeakElements);

public sealed record AilmentDto(
    string Key,          // poison/para/sleep/blast/exhaust…
    string DisplayName,  // 毒/麻/…
    double Percent,      // 0-100
    bool IsActive);

public sealed record MonsterHudDto(
    string Name,
    double HealthCurrent,
    double HealthMax,
    bool IsCapturable,
    double? CaptureThresholdPercent, // 血量百分比阈值，如 25
    IReadOnlyList<ElementId> OverallElementsOrdered, // 五属展示用，含抗性
    IReadOnlyList<ElementId> Recommended,
    StatusLineModel Status,
    IReadOnlyList<PartDto> Parts,
    IReadOnlyList<AilmentDto> Ailments); // 不含 stun

public sealed record DpsEntryDto(string Name, bool IsSelf, double Dps, long TotalDamage);
public sealed record DpsPanelDto(IReadOnlyList<DpsEntryDto> Entries);
```

- [ ] **Step 2: 失败测试 — 状态行**

```csharp
public class StatusLineFormatterTests
{
    [Fact]
    public void Formats_enrage_stun_stamina()
    {
        var s = new StatusLineModel(
            EnrageRemaining: TimeSpan.FromSeconds(14),
            StunBuildupPercent: 88,
            StunActive: false,
            StunActiveRemaining: null,
            StaminaPercent: 62,
            DownRemaining: null);
        Assert.Equal("愤怒 0:14 · 晕眩 88% · 耐力 62%", StatusLineFormatter.Format(s));
    }

    [Fact]
    public void Inserts_active_stun_and_down()
    {
        var s = new StatusLineModel(
            TimeSpan.FromSeconds(9), null, true, TimeSpan.FromSeconds(3), 18,
            TimeSpan.FromSeconds(2));
        Assert.Equal("愤怒 0:09 · 晕眩中 0:03 · 耐力 18% · 倒地 0:02", StatusLineFormatter.Format(s));
    }
}
```

- [ ] **Step 3: 实现 Formatter + Capture 辅助**

```csharp
public static class StatusLineFormatter
{
    public static string Format(StatusLineModel s)
    {
        var parts = new List<string>();
        if (s.EnrageRemaining is { } e)
            parts.Add($"愤怒 {e.Minutes}:{e.Seconds:D2}");
        if (s.StunActive && s.StunActiveRemaining is { } sa)
            parts.Add($"晕眩中 {sa.Minutes}:{sa.Seconds:D2}");
        else if (s.StunBuildupPercent is { } sp)
            parts.Add($"晕眩 {Math.Round(sp):0}%");
        if (s.StaminaPercent is { } st)
            parts.Add($"耐力 {Math.Round(st):0}%");
        if (s.DownRemaining is { } d)
            parts.Add($"倒地 {d.Minutes}:{d.Seconds:D2}");
        return string.Join(" · ", parts);
    }
}

public static class CaptureRules
{
    public static bool IsPastThreshold(double healthPercent, double thresholdPercent)
        => healthPercent <= thresholdPercent;

    // 静态表标记；运行时再与任务类型求交
    public static bool ShowCaptureUi(bool speciesCapturable, bool questAllowsCapture)
        => speciesCapturable && questAllowsCapture;
}
```

- [ ] **Step 4: 测试通过并 Commit**

```powershell
dotnet test RiseOverlay.Domain.Tests\RiseOverlay.Domain.Tests.csproj -v n
```

```bash
git add RiseOverlay.Domain RiseOverlay.Domain.Tests
git commit -m "feat(domain): status line formatter, capture helpers, overlay DTOs"
```

---

### Task 4: 导出静态怪物表

**Files:**
- Create: `Scripts/export_overlay_monsters.py`
- Create: `RiseOverlay.Data/static/monsters-overlay.json`
- Create: `RiseOverlay.Data/MonsterStaticStore.cs`
- Create: `RiseOverlay.Data/RiseOverlay.Data.csproj`

- [ ] **Step 1: 编写导出脚本**

从 `F:\mhrise-app\assets\data\details\monsters.json` 读取每个怪：

- `id`, `title`
- 每个 hitzone：`part`, `phase`, fire/water/ice/thunder/dragon
- parts：`part`, `break`, `sever`
- 启发式 `capturable`: 名称/种类不含古龙、霸主、天彗、爵银等时为 true（导出后再人工校一版常见表；V1 允许 JSON 内手改 `capturable` 字段）

输出精简 JSON 数组。

- [ ] **Step 2: 运行导出**

```powershell
python F:\rise-overlay\Scripts\export_overlay_monsters.py `
  --input F:\mhrise-app\assets\data\details\monsters.json `
  --output F:\rise-overlay\RiseOverlay.Data\static\monsters-overlay.json
```

Expected: 文件存在且含 `神秘红光天彗龙` / `怨虎龙` 等条目。

- [ ] **Step 3: `MonsterStaticStore.Load(path)` 按 id/名查找；单测加载不抛错**

- [ ] **Step 4: Commit**

```bash
git add Scripts RiseOverlay.Data
git commit -m "feat(data): static monster overlay table export and loader"
```

---

### Task 5: UI — ElementChip + 主题（设计时数据）

**Files:**
- Create: `RiseOverlay.UI/Themes/CompactTeal.xaml`
- Create: `RiseOverlay.UI/Controls/ElementChip.xaml`
- Create: `RiseOverlay.UI/Controls/ElementChip.xaml.cs`
- Create: `RiseOverlay.UI/RiseOverlay.UI.csproj`（WPF 库，TFM 对齐上游 UI）

- [ ] **Step 1: 建 WPF 类库并引用 Domain**

```powershell
dotnet new wpf -n RiseOverlay.UI
# 若模板是 App，改成 SDK `UseWPF` 的 classlib 更合适：手动建 csproj UseWPF=true
dotnet sln add RiseOverlay.UI\RiseOverlay.UI.csproj
dotnet add RiseOverlay.UI reference RiseOverlay.Domain
```

- [ ] **Step 2: 实现 `ElementChip`**

依赖属性：`Element`（enum）、`Dimmed`（bool）、`Small`（bool）。  
背景色：Fire `#B85C5C` …（与定稿一致）；前景白字；**无动画**。

- [ ] **Step 3: `CompactTeal.xaml` 资源**：面板背景 `#0A0E16` 约 95% 不透明、边框、字体大小 10–12。

- [ ] **Step 4: 在 Playground 或临时 Window 里放一排 Chip 目视确认**

- [ ] **Step 5: Commit**

```bash
git add RiseOverlay.UI
git commit -m "feat(ui): compact teal theme and ElementChip control"
```

---

### Task 6: UI — MonsterHudView（完整布局）

**Files:**
- Create: `RiseOverlay.UI/Views/MonsterHudView.xaml`
- Create: `RiseOverlay.UI/ViewModels/MonsterHudViewModel.cs`
- Create: `RiseOverlay.UI/Controls/StatusLineText.xaml`

- [ ] **Step 1: ViewModel 属性对齐 `MonsterHudDto`**（Observable 包装，便于设计时 Sample）

含：`CaptureBannerVisible`、`HealthPercent`、`WeakenLineVisible`、`WeakenLinePosition`（0–1）、`StatusLineText`、`Parts`、`Ailments`、`Recommended`、`OverallElements`（带 IsDimmed）。

- [ ] **Step 2: XAML 结构严格按 spec §3.3**

顺序：Banner → Name+Pct → Weak+Recommend → HP+WeakenLine → Meta → StatusLineText → Parts（弱+色块+条+血）→ Ailments（扁行，无晕眩）。

宽度：`Width="272"`。可捕获时 `BorderBrush` 青脉冲可用简单 Storyboard（仅边框，不强闪金）。

- [ ] **Step 3: 用硬编码 Sample DTO（嗟怨震天怨虎龙最全态）设计时预览**

- [ ] **Step 4: Commit**

```bash
git add RiseOverlay.UI
git commit -m "feat(ui): MonsterHudView compact layout matching spec"
```

---

### Task 7: UI — DpsPanelView + CombatStackView

**Files:**
- Create: `RiseOverlay.UI/Views/DpsPanelView.xaml`
- Create: `RiseOverlay.UI/ViewModels/DpsPanelViewModel.cs`
- Create: `RiseOverlay.UI/Views/CombatStackView.xaml`

- [ ] **Step 1: 单人模式一行 `DPS` + `总伤`；组队模式扁列表**

`Entries` 按 `TotalDamage` 降序；`IsSelf` 高亮；条宽 = `TotalDamage / sum`。

- [ ] **Step 2: `CombatStackView` = `MonsterHudView` 上 + `DpsPanelView` 下，间距 5**

- [ ] **Step 3: Sample 4 人数据目视确认高度仍紧凑**

- [ ] **Step 4: Commit**

```bash
git add RiseOverlay.UI
git commit -m "feat(ui): compact DPS panel and combat stack"
```

---

### Task 8: UI — QuestBriefingView

**Files:**
- Create: `RiseOverlay.UI/Views/QuestBriefingView.xaml`
- Create: `RiseOverlay.UI/ViewModels/QuestBriefingViewModel.cs`

- [ ] **Step 1: 绑定目标列表；每块：名、弱点、推荐、三芯片**

- [ ] **Step 2: Sample ×1 与 ×4**

- [ ] **Step 3: Commit**

```bash
git add RiseOverlay.UI
git commit -m "feat(ui): quest briefing multi-target panel"
```

---

### Task 9: 映射层 — 静态 + 运行时 → DTO

**Files:**
- Create: `RiseOverlay.UI/Mapping/MonsterHudMapper.cs`
- Create: `RiseOverlay.Domain.Tests/MonsterHudMapperTests.cs`（对纯函数部分；或把 mapper 纯逻辑放到 Domain）

- [ ] **Step 1: `BuildFromStatic(monsterId)`** → 简报用：推荐、弱、可捕、断尾、重点部位

- [ ] **Step 2: `MergeLive(static, liveHp, parts, ailments, status…)`** → 战斗 HUD DTO  
  - 过滤异常：去掉 stun key；`Percent<=0 && !IsActive` 丢弃  
  - 部位弱：静态 hitzone 按部位名匹配  

- [ ] **Step 3: 单测天彗龙推荐四属；不可捕无虚弱线**

- [ ] **Step 4: Commit**

```bash
git add RiseOverlay.UI/Mapping RiseOverlay.Domain.Tests
git commit -m "feat: map static and live monster data into overlay DTOs"
```

---

### Task 10: 接入 HunterPie Rise 怪物上下文

**Files:**
- Create: `RiseOverlay.UI/Integration/RiseMonsterHudController.cs`（或放在 `HunterPie.UI/Overlay/Widgets/RiseCompact/`）
- Modify: Widget 注册处（克隆后搜索 `MonsterWidget` / `WidgetManager.Register`）

- [ ] **Step 1: 定位上游 `IMonster` / Quest 目标 API**（阅读 Integrations Rise 代码，在 plan 执行时把实际类型名填进控制器注释）

- [ ] **Step 2: 订阅 HP / Parts / Ailments / Enrage 等事件 → 调 Mapper → 更新 `MonsterHudViewModel`**

- [ ] **Step 3: 用游戏内实测：怨虎龙血条、捕获线、断尾行**

- [ ] **Step 4: 默认禁用旧 Monster Widget 配置项 `Initialize=false`**

- [ ] **Step 5: Commit**

```bash
git add RiseOverlay.UI HunterPie.UI HunterPie.Core
git commit -m "feat: wire compact monster HUD to Rise live context"
```

---

### Task 11: 接入伤害计量

**Files:**
- Create: `RiseOverlay.UI/Integration/RiseDpsController.cs`
- Modify: 禁用或隐藏默认 Damage Meter 图表型 UI

- [ ] **Step 1: 从 `IPartyMember`（或上游 Damage 控制器同源事件）读取 Name / Damage / 时间推导 DPS**

```csharp
// 伪代码逻辑写入控制器
dps = totalDamage / Math.Max(1.0, questElapsedSeconds);
```

- [ ] **Step 2: 映射为 `DpsPanelDto`，绑定 `DpsPanelViewModel`**

- [ ] **Step 3: 联机 2–4 人确认总伤列与合计**

- [ ] **Step 4: Commit**

```bash
git add RiseOverlay.UI
git commit -m "feat: wire compact DPS panel to party damage"
```

---

### Task 12: 任务简报触发与场景切换

**Files:**
- Create: `RiseOverlay.UI/Integration/QuestBriefingController.cs`

- [ ] **Step 1: 监听 `OnQuestStart`（或等价）：若尚未进图/仍在集会所 → 显示 Briefing**

- [ ] **Step 2: 进入狩猎区域 / 怪物生成 → 隐藏 Briefing，显示 CombatStack**

- [ ] **Step 3: 任务结束 → 全部隐藏**

- [ ] **Step 4: Commit**

```bash
git add RiseOverlay.UI
git commit -m "feat: auto quest briefing then swap to combat HUD"
```

---

### Task 13: 窗口吸附与热键

**Files:**
- Modify: 紧凑 Widget 配置（Position）
- Reuse: 上游 `OverlayManager` → `WidgetView`（置顶透明窗 + 屏幕坐标 `Position`）；**不是**游戏客户区 HWND 父子吸附

> **核实结论（2026-08-31）：** 上游并无 “attach-to-game-client-rect / Follow” 实现。Process attach 仅用于读进程内存。Compact HUD 走同一 `OverlayManager` 路径，因此**不会**随游戏窗口拖动而移动；`HideWhenUnfocus` 只在失焦时隐藏。客户区相对吸附仍为缺口（见 QA §7 #11 / README attach model）。

- [x] **Step 1: 配置默认左上；沿用 OverlayManager 托管（无独立屏幕坐标系）** — 已完成；~~“开启 AttachToGame/Follow”~~ 上游无此选项，已划掉

- [ ] **Step 2: 窗口模式拖动游戏客户区，确认 HUD 跟随** — **V1 预期失败**（屏幕绝对坐标）；勿当作实现完成项

- [x] **Step 3: 热键显示/隐藏** — 沿用 `OverlayClientConfig.ToggleVisibility`（默认 Ctrl+Alt+O）；无单独 `RiseOverlay.Toggle`

- [ ] **Step 4: Commit**（实现类改动如有则另提；文档澄清见 `docs: clarify overlay attach…`）
---

### Task 14: 裁剪非 Rise 表面功能

**Files:**
- Modify: 游戏可用性配置、Widget `availableGames`
- Modify: 启动默认仅检测 Rise

- [ ] **Step 1: World/Wilds Widget `Initialize` 默认 false；文档声明不支持**

- [ ] **Step 2: 移除或隐藏 Discord / 花结等入口（若一键可关配置则关配置，避免大删导致编译碎）**

- [ ] **Step 3: `dotnet build` 全绿**

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "chore: default to Rise-only surface; disable unused widgets"
```

---

### Task 15: 手工验收清单与打包

**Files:**
- Create: `docs/superpowers/specs/2026-08-31-rise-overlay-qa-checklist.md`
- Modify: Installer 或发布说明（若动 Installer 成本高，V1 可先 `dotnet publish` 文件夹分发）

- [ ] **Step 1: 按 spec §7 跑完 QA 清单并勾选**

- [ ] **Step 2: `dotnet publish` 产出可运行目录**

- [ ] **Step 3: Commit checklist + 发布说明**

```bash
git add docs
git commit -m "docs: QA checklist and Rise Overlay publish notes"
```

---

## Spec coverage check

| Spec 项 | Task |
|---------|------|
| 任务简报 | 8, 12, 4 |
| 战斗 HUD 全字段 | 5–6, 9–10 |
| 推荐/弱算法 | 2, 9 |
| 状态一行 / 无条块 | 3, 6 |
| 异常不含晕、扁行 | 6, 9 |
| DPS 总伤 | 7, 11 |
| 吸附窗口 | 13 |
| Fork 瘦身 | 1, 14 |
| 静态表 | 4 |

## 执行方式

Plan complete and saved to `F:\mhrise-app\docs\superpowers\plans\2026-08-31-rise-overlay.md`（执行 Task 1 时再拷入 `F:\rise-overlay`）。

**两种执行选项：**

1. **Subagent-Driven（推荐）** — 每任务新开子代理，任务间复查  
2. **Inline Execution** — 本会话按 executing-plans 连续做，设检查点  

你要哪一种？
