# Rise Overlay — HUD 视觉特效升级设计

Date: 2026-09-28  
Status: draft (research)  
Scope: Rise 紧凑 HUD（战斗 / 简报 / DPS）视觉层重做，特效分级开关，渲染路径解锁  
前置阅读: `2026-09-02-rise-overlay-themes-motion-design.md`

## Goal

把 Rise 紧凑 HUD 从「单层色块 + 换色主题」升级为**分层视觉系统**：有纵深、有材质、有事件反馈，
并在不拖累游戏帧率的前提下提供可切换的特效强度档位。

## Non-goals

- 不改动任何战斗数据采集 / 解析逻辑（`RiseOverlay.Domain`、`HunterPie.Integrations` 不动）
- 不改动 HUD 的信息项与布局语义（部位行、异常行、捕获线信息必须保持等价）
- 不引入第三方 UI 框架或商业控件库
- 不做逐帧粒子系统

## 现状诊断

| 项 | 现状 | 问题 |
| --- | --- | --- |
| 视觉语法 | `MonsterHudView.xaml` 中 `<Border Background>` 嵌套 | 零层次，扁平贴纸感 |
| 血条 | 8px `Border` + `ScaleTransform ScaleX` | 无渐变、高光、刻度、拖尾 |
| 主题 | `Theme.*.xaml` × 7，每套 46 行仅含 Color / Brush / FontSize / CornerRadius | 是「换色板」不是「换皮肤」 |
| 「玻璃」主题 | 圆角 2→12 + 边框 `#66FFFFFF` | 无 BlurEffect，名不副实 |
| 动效 | 仅 4 处：捕获边框呼吸、捕获闪红、晕眩 ±2px 抖动、破部位 opacity 闪 3 次 | 停留在「闪烁」层级 |
| 事件反馈 | 无受击闪、无掉血拖尾、无破部位冲击、无飘字 | 战斗信息变化缺乏视觉重量 |

## 关键约束：渲染路径（P0 阻塞项）

**`HunterPie.Core/Client/Configuration/ClientConfig.cs:63`**

```csharp
public Observable<RenderingStrategy> Render { get; set; } = RenderingStrategy.Software;
```

**`HunterPie/App.xaml.cs:119-124`**

```csharp
private void SetupRenderingMode()
{
    RenderOptions.ProcessRenderMode = ClientConfig.Config.Client.Render == RenderingStrategy.Hardware
        ? RenderMode.Default
        : RenderMode.SoftwareOnly;
}
```

默认 `Software` → `SoftwareOnly` → **GPU 合成被禁用**。此状态下任何 `Effect`（`DropShadowEffect` /
`BlurEffect`）都由 CPU 逐像素计算，是本项目至今不敢引入特效的直接原因。

**处置**：特效档位为「标准 / 拉满」时强制 `RenderMode.Default`。需注意 overlay 窗口
（`WidgetView.xaml`）为 `AllowsTransparency=True` 的分层窗口，Windows 8+ 下 WPF 支持其硬件加速，
但须实测验证。若实测不达标，降级方案为：仅在静态元素上使用纹理层，动效全部走 Transform / Opacity。

**其他已有机制（勿重复造）**

- `WidgetView.xaml.cs` 已在 `CompositionTarget.Rendering` 注册每帧回调（ForceAlwaysOnTop，500ms 节流）。
  **禁止再往该事件追加逐帧逻辑。**
- 性能仪表已存在：`DevelopmentSettings.IsRenderTimeEnabled` → WidgetView 左上角显示帧间隔 ms。
- 帧率上限：`ClientConfig.Client.IsFramePerSecondLimitEnabled` / `RenderFramePerSecond`
  → `Timeline.DesiredFrameRateProperty.OverrideMetadata`（`App.xaml.cs:108-117`）。

## 布局方案（三种，待选）

当前布局：300px 固定宽，纵向堆叠（标题 → 血条 → 数值 → 状态 → 部位 → 异常）。

以下三种结构的信息项**完全等价**（不增删任何数据），仅重排：

| 方案 | 结构 | 特点 | 适用场景 |
| --- | --- | --- | --- |
| **A 横幅式** | 600px 超宽，血条主导，部位行压成紧凑单行 | 信息密度最高 | 屏幕顶部横置 |
| **B 卡片分层** | 300px，怪物卡 / 部位卡 / 状态卡分离 | 层次感最强，各卡独立辉光 | 默认推荐 |
| **C 极简浮空** | 无面板底，元素直接浮于游戏画面之上 | 最不挡画面 | 追求沉浸 |

**~~A 方案额外收益~~（已否决）**：底部「全属性」五行矩阵曾考虑复用闲置的 `OverallElements`，
但**老大已明确否决——不要全属性，只要推荐属性**。推荐属性保留在标题行（怪物级）与部位行的弱属性 chip。
`OverallElements` 继续闲置，本轮不启用。

**C 方案注意**：无面板底时文字可读性完全依赖描边，需为所有文本叠加 1px 深色描边层
（多层 `TextBlock` 错位实现，纯 XAML）。

**宽度影响**：`MonsterHudView.xaml` 与 `RiseCompactMonsterView.xaml` 均硬编码 `Width="300"`，
改宽需同步修改（纯 XAML 改动，`WidgetView` 的 `SizeToContent` 会自动适配）。

### 规整版列栅格（六列，已画稿）

老大反馈首版「太乱」。根因不是信息多，而是**没有统一栅格**：各行名称长度不同导致状态文字起始位置
浮动，纵向对齐轴散乱；同时使用金/橙/红/灰/青/蓝/紫/黄八色，视觉噪音过载。

规整手段四条：

1. **固定列宽**——所有部位行共用同一套列定义，状态列不再随名称长度浮动
2. **四条纵向对齐轴**——状态 / 弱点 / 进度条 / 数值各自严格对齐
3. **配色收敛**——语义色只用于 3px 色轨与状态文字；进度条统一中性青，仅怪异化用红
4. **加表头**——「部位 / 状态 / 弱点 / 剩余」淡灰小字，强化栅格感知

列定义（600px 面板，内容区 x=56..624，共 568px）：

| 列 | 起始 x | 宽度 | 对齐 |
| --- | --- | --- | --- |
| 状态色轨 | 56 | 3 | — |
| 部位名 | 68 | 76 | 左 |
| 状态 | 148 | 62 | 左 |
| 弱点 chip | 216 | 26 | 居中 |
| 进度条 | 250 | 252 | — |
| 剩余值 | 502 | 122 | 右 |

行高 26px（视觉行高 24 + 间距 2）。标题行与血条整行通栏；分组用 1px 分隔线，不用色块。
对应 WPF 实现：`Grid` + `ColumnDefinition` 固定宽（`Width="76"` / `Width="62"` / `Width="26"` /
`Width="252"` / `Width="122"`），保证跨行对齐；进度条用 `Width="252"` 的 `Border` + 内部
`ScaleTransform ScaleX` 绑定比例。

> **注**：上述 600px 方案已被老大否决——**宽度必须保持 300px 不变**。以下八版均为 300px 方案。

### 300px 八版方案（已画稿）

硬约束：**宽 300px 固定**，所有内容必须在框内完整显示，**不得溢出、不得截断**（不依赖
`TextTrimming` 兜底）。

| 版 | 结构 | 亮点 | 取舍 |
| --- | --- | --- | --- |
| **A 表格栅格** | 六列严格对齐 + 淡灰表头 | 最规整 | 略死板 |
| **B 双行舒展** | 部位占两行，进度条通栏 | 条最长，冲击强 | 部位多时高度膨胀 |
| **C 极简双列** | 去掉状态列，靠色轨表达 | 留白最多，最透气 | 状态语义弱化 |
| **D 卡片分区** | 怪物卡 / 部位卡 / 状态卡 | 层次感强 | 双重边框偏重 |
| **E 沉浸嵌名** | 血条通栏 56px，怪物名嵌入条内 | 视觉冲击最强，省一行 | 低血量时名字对比度下降 |
| **F 双栏分区** | 左栏部位 / 右栏状态，竖线分隔 | 信息密度最高 | 部位条仅 128px |
| **G 胶囊网格** | 部位做成 2×2 胶囊卡 | 卡片感强 | 一屏仅容纳 4 个部位 |
| **H 极简线条** | 2px 细条 + 描边胶囊血条 | 最轻，最不挡视野 | 细条在游戏画面中偏弱 |

**推荐**：E（最好看）或 A（最规整）；亦可取 **E 的血条嵌名 + A 的部位栅格**做混合。

**300px 关键列宽参考**（内容区 280px，padding 10）：

| 元素 | 宽 | 说明 |
| --- | --- | --- |
| 状态色轨 | 3 | 语义色唯一载体 |
| 部位名 | 46–52 | 容 3–4 字 |
| 状态文字 | 42–44 | 容 4 字（如「可破坏」） |
| 弱点 chip | 18–20 | 单字元素 |
| 进度条 | 92–136 | 视方案而定 |
| 剩余值 | 58–65 | 「620/1200」约 48px |

### 部位行样式（八种）

老大选定 **A 表格栅格** 作为整体骨架，但指出：「八版里部位行看起来就没变过」——确实如此，
八版的部位行都是同一套（色轨 + 名称 + 状态 + chip + 条 + 值），只换了位置没换零件。

以下八种为部位行的**独立样式**探索，可任选替换 A 的部位行：

| # | 样式 | 亮点 | 取舍 | 实现要点 |
| --- | --- | --- | --- | --- |
| 1 | 栅格 | 信息最全，六列对齐 | 无视觉惊喜 | 基准 |
| 2 | **药丸填充** | 整行即进度条，液态感 | 行高 26→30 | 外层 `Border rx=12` + 内层填充 `Border` |
| 3 | 双轨仪表 | 硬直 + 部位血量双轨 | 行高翻倍 | 复用 `FlinchRatio` + `HealthRatio` |
| 4 | 文字填充 | 名称被进度染色 | 信息承载少 | `TextBlock` + `OpacityMask` 裁剪 |
| 5 | 分段格子 | 仪表感，可数格 | 格子数固定 | 12 个 `Rectangle`，逐格 `DataTrigger` |
| 6 | 左侧竖条 | 横向最省空间 | 竖向差异不直观 | `Border` + 高度绑定比例 |
| 7 | 卡片块 | 每部位独立卡 | 一屏仅 4–6 个 | 双列 `UniformGrid` |
| 8 | 徽章式 | 首字徽章快速定位 | 多一个装饰元素 | 方形 `Border` + 首字文本 |

**推荐：样式 2 药丸填充。** 视觉变化最明显、实现最简单（纯圆角 `Border` + 内部填充 `Border` +
`ScaleTransform`），且完整保留名称 / 状态 / 弱点 / 数值四项信息。怪异化时描边与填充同步转红，
状态辨识度不降。

**次选：样式 3 双轨仪表。** 项目已有 `FlinchRatio` 与 `HealthRatio` 两个绑定属性，现有 HUD 本就是
双轨设计，双轨等于把现有数据全用上，信息量最大；代价是行高翻倍。

#### 双轨语义（重要澄清）

**老大原话**：「双轨的意思是一个是血条，一个是硬直。」

即部位行的两条轨是**两个不同维度**，不是同一条进度的两段：

| 轨 | 绑定属性 | 含义 | 现有粗细 |
| --- | --- | --- | --- |
| 上轨 | `FlinchRatio` | 硬直积累进度 | 2px |
| 下轨 | `HealthRatio` | 部位血量 | 3px |

对照 `MonsterHudView.xaml`：`ShowFlinchBar` 的 2px 轨绑 `FlinchRatio`，`ShowBreakBar` 的 3px 轨绑
`HealthRatio`；另有第三条 `ShowBrokenRail`（3px 灰轨）标记已破坏态。

> 设计文档初稿曾把下轨误述为「破坏进度」，实为**部位血量**——本文已修正。

**双轨的四种标注方式**（防止两条轨看混）：

| 方式 | 画法 | 优点 | 缺点 |
| --- | --- | --- | --- |
| **甲** 粗细 + 颜色 | 上轨 2px 浅白 / 下轨 5px 语义色 | 无额外元素，最干净 | 首次接触需适应 |
| 乙 左端微型标签 | 轨左端加「硬」「血」小字 | 最不易误读 | 轨短 24px |
| 丙 合并轨 | 一条 8px 轨，上 4px 硬直、下 4px 血量 | 省 3px 高度 | 易被误读为一条进度 |
| 丁 主从式 | 血量 7px 主轨 + 硬直贴底 2px 细线 | 主次分明 | 硬直轨在主轨内，对比度低 |

**推荐：甲。** 两条轨同起点、等长、无间隙干扰，长度可直接横向对比，且不占横向空间。
若实测辨识困难，退到**乙**加两个字的标签即可。

**样式 4 说明**：文字填充靠 `TextBlock` 作 `OpacityMask`、底下垫一条进度色块实现，纯 XAML 可行，
但它把「名称」和「进度」绑成一个元素，信息承载量下降，适合作为点缀而非主力。

## 视觉架构：六层堆叠

自底向上，每层独立可开关：

| # | 层 | 内容 | 实现 | 成本 |
| --- | --- | --- | --- | --- |
| 6 | 特效层 | 破部位径向爆发、捕获扫描线、受击闪白 | `Canvas` + 条件触发 Storyboard | 低 |
| 5 | 数据层 | 血条七层、Ghost 拖尾、部位双轨 | `Rectangle` + 动画 `Brush` | 极低 |
| 4 | 结构层 | 部位行、元素 chip、状态徽章 | 现有 `Border` 体系 + 内描边 | 极低 |
| 3 | 发光层 | 面板外发光 / 内投影 | `DropShadowEffect`（`ShadowDepth=0`），全局 ≤2 处 | 中 |
| 2 | 纹理层 | 斜纹 / 点阵 / 金属噪点 | `DrawingBrush` + `TileMode=Tile`，**静态** | 极低 |
| 1 | 面板底 | 底色 + 双层描边 + 顶部反光 | `LinearGradientBrush` + 亮线 | 极低 |

## 血条规格（七层）

自底向上：

1. **外发光**：血条外扩 2–4px 的强调色低透明度圆角矩形（替代 `DropShadowEffect`，成本更低）
2. **槽**：`#0B0F14`，圆角 4
3. **Ghost 拖尾**：记录上一帧 HP 比例，`DoubleAnimation` 以 0.35–0.6s 缓动追赶当前值。
   掉血瞬间主条立刻缩短、Ghost 留在原位再收缩 → 产生「伤口」视觉
4. **主体**：三段 `LinearGradientBrush`（暗 → 亮 → 暗），中段亮度峰值
5. **顶部高光带**：主体上方 1/3 高度，白色 → 透明纵向渐变
6. **分段刻度**：每 10% 一条 1px `#000` `Opacity=0.28` 竖线
7. **虚弱线**：金色竖线 + 顶部三角标记（现有实现，保留并加呼吸）

附加状态：

- **低血脉冲**：HP < 25% 时主体透明度与边框辉光同步呼吸（受动效总开关控制）
- **受击闪白**：HP 下降事件触发一次 0.12s 白色 `OpacityMask` 覆盖

## 特效矩阵

| 触发 | 表现 | 时长 | 层 | 成本 |
| --- | --- | --- | --- | --- |
| 受击掉血 | Ghost 拖尾收缩 + 主体闪白 | 0.5s | 数据 | 极低 |
| HP < 25% | 血条呼吸 + 边框红辉光 | 持续 | 数据/发光 | 低 |
| 破部位 / 断尾 | 该行径向爆发（3 条放射线 + 1 个扩散圆环）+ 行背景横向扫光 | 0.8s | 特效 | 低 |
| 越过虚弱线 | 全条金色扫描线自上而下 + 虚弱线脉冲 | 1.0s | 特效 | 低 |
| 可捕获 | 面板边框金红脉冲（现有）+ 血条右端呼吸光晕 | 持续 | 发光 | 低 |
| 晕眩 | 保留现有抖动；叠加黄绿渐变覆盖条 | 持续 | 数据 | 极低 |
| 御龙 | 青色覆盖条 + 面板边框青辉光 | 持续 | 发光 | 低 |
| 怪异化 | 感染部位红轨呼吸 + 面板底部红色暗角 | 持续 | 数据/发光 | 低 |

**统一受 `EnableCombatMotion` 总开关控制**（现有关闭行为：保留语义色，去除脉冲/抖动/爆发）。

## 完整特效菜单

按实现可行性分级：**✅ 纯 XAML 可直接实现** / **⚠️ 需少量辅助** / **❌ 需改逻辑**

### 血条

| 特效 | 实现要点 | 级别 |
| --- | --- | --- |
| Ghost 掉血拖尾 | 主条即时、残影条延迟收缩（Storyboard 追赶） | ✅ |
| 受击闪白 | HP 下降触发白色 `OpacityMask` 覆盖 0.12s | ✅ |
| 低血脉冲 + 边框辉光 | `ColorAnimation` on 边框 Brush，`RepeatBehavior=Forever` | ✅ |
| 渐变流动（能量感） | 动画 `GradientStop.Offset` | ✅ |
| 高光扫过 | `OpacityMask` 位移 | ✅ |
| 分段格子跳变 | 每格独立 `ScaleTransform` | ✅ |
| 虚弱线脉冲 / 越线金色闪光 | 现有 `PastWeakenLine` 触发 | ✅ |
| 端部火花 / 能量溢光 | 血条右端多层低透明度矩形叠加 | ✅ |
| 环形 / 弧形血条 | `Path` + `ArcSegment`（需预计算弧长） | ✅ |

### 面板

| 特效 | 实现要点 | 级别 |
| --- | --- | --- |
| 外发光（随状态变色） | 外扩低透明度圆角矩形，比 `DropShadowEffect` 更省 | ✅ |
| 顶部反光 / 斜纹 / 点阵纹理 | `LinearGradientBrush` + `DrawingBrush TileMode` | ✅ |
| 状态氛围色（怪异红 / 御龙青） | 背景叠加层 + `ColorAnimation` | ✅ |
| 边框流光（跑马灯） | 渐变 Brush 沿边框位移 | ✅ |
| 暗角 vignette / 入场滑入 | 径向渐变 + `TranslateTransform` | ✅ |

### 事件

| 特效 | 实现要点 | 级别 |
| --- | --- | --- |
| 破部位径向爆发 + 冲击圆环 | 现有 `BreakFlash` 触发，3 放射线 + 1 扩散环 | ✅ |
| 行背景扫光 | 行内 `OpacityMask` 位移 | ✅ |
| 破坏后碎裂错位 | `RowOpacity` + 多层错位文本 | ✅ |
| 推荐部位呼吸描边 | 现有 `IsRecommendedTarget` 触发 | ✅ |

### 文字

| 特效 | 实现要点 | 级别 |
| --- | --- | --- |
| 文字描边 / 发光 | 多层 `TextBlock` 错位（C 方案必备） | ✅ |
| 数字跳动 / 滚动 | 需值转换器驱动 | ⚠️ |
| 伤害飘字 | 需伤害事件流 + 新增 Canvas 层 | ❌ |

### 本轮明确不做（越界）

| 特效 | 原因 |
| --- | --- |
| CRT 扫描线 / 色差 / 故障 | 需 `ShaderEffect`，超出「只改 UI」范围 |
| 粒子火花 | 需逐帧绘制，性能不可接受 |
| 屏幕级闪光 | 需独立窗口，超出 HUD 范围 |

## 主题系统升级：换色 → 换皮肤

在现有 `Theme.*.xaml` 基础上扩展资源键（保持向后兼容，缺省回退）：

```xml
<!-- 新增键（示例） -->
<Brush x:Key="Brushes.PanelTexture" />          <!-- DrawingBrush 斜纹 -->
<Brush x:Key="Brushes.PanelGloss" />            <!-- 顶部反光渐变 -->
<sys:Double x:Key="Theme.PanelBorderWidth" />
<Color x:Key="Colors.GlowAccent" />             <!-- 发光层基色 -->
<sys:Double x:Key="Theme.GlowRadius" />         <!-- 0 = 该主题不发光 -->
<sys:Double x:Key="Theme.TextureOpacity" />
```

主题可据此表达不同材质：`Parchment` 用羊皮纸噪点纹理、`OledCoral` 用纯黑 + 珊瑚辉光、
`Glass` 用高反光 + 冷色描边（仍不引入 `BlurEffect`）。

`RiseThemeService.ApplyTheme()` 逻辑不变，仅字典内容扩展。

## 特效档位

`RiseCompactMonsterWidgetConfig` 新增：

```csharp
public Observable<FxTier> FxLevel { get; set; } = FxTier.Standard;
// Off / Standard / Max
```

| 档位 | 行为 |
| --- | --- |
| `Off` | 无纹理层、无发光层、无动效（等同 `EnableCombatMotion=false` + 精简视觉） |
| `Standard` | 六层全开，动效走 Transform / Opacity / Color，不强制切渲染模式 |
| `Max` | 追加 ShaderEffect 扫描线 / 色差，**强制 `RenderMode.Default`**，并在壳窗口提示性能影响 |

壳窗口 `RiseShellWindow` 的「HUD 主题」区块下方增加档位选择器。

## 性能预算与红线

| 规则 | 理由 |
| --- | --- |
| 单帧渲染预算 ≤ 4ms（144Hz 游戏场景） | overlay 不应抢占游戏 GPU 时间 |
| `DropShadowEffect` 全局 ≤ 2 处 | 每个 Effect 触发一次离屏渲染 |
| 禁止给 `ItemsControl` 内每行挂 `Effect` | 部位行数量随怪物变化，成本不可控 |
| 动画仅限 `Transform` / `Opacity` / `Color` | 动 `Width`/`Height`/`Margin` 会触发 layout 重排 |
| 纹理层必须静态 | `DrawingBrush` 一旦参与动画即退化为逐帧重绘 |
| 禁用 `BitmapEffect` | 已废弃 API |
| 禁止逐帧粒子 | `CompositionTarget.Rendering` 内绘制在软件渲染下是性能自杀 |

## 分期路线图

### P0 — 结构基础（肉眼变化最大，成本最低）

1. 面板六层骨架：面板底 + 纹理层 + 顶部反光 + 双层描边
2. 血条七层（含 Ghost 拖尾、分段刻度）
3. 低血脉冲
4. 渲染路径解锁：新增档位，`Max` 档强制 `RenderMode.Default`

### P1 — 事件反馈

5. 受击闪白 + Ghost 联动
6. 破部位径向爆发 + 行扫光
7. 越过虚弱线扫描线
8. 御龙 / 怪异化面板级辉光

### P2 — 极限与可选

9. `ShaderEffect` 特效包（扫描线、色差）
10. 主题皮肤化（纹理 / 发光参数随主题）
11. 伤害飘字（需新增 Canvas 覆盖层）

## Testing

- 各主题 × 各档位切换：简报 / 战斗 / DPS 全部正确更新，重启后恢复
- `Max` 档下开启 `IsRenderTimeEnabled`，帧间隔与 `Standard` 档对比，劣化 ≤ 1ms
- 动效关闭：无脉冲 / 抖动 / 爆发，语义色保留
- Ghost 拖尾在连续掉血时不抖动、不反向
- 部位行数量多（≥8 行）时帧率无可见下降
- 怪异化怪：感染部位红轨呼吸在各主题下均可见
- 分层窗口在 `RenderMode.Default` 下正常合成，无黑边 / 闪烁

## 风险

| 风险 | 应对 |
| --- | --- |
| 分层窗口硬件加速不达标 | 降级：仅保留静态纹理层，动效全走 Transform |
| `DropShadowEffect` 在部分集显上掉帧 | 档位降级 + 发光层改用「外扩低透明度矩形」模拟 |
| 档位强制切渲染模式导致重启需求 | 渲染模式变更需重启时，壳窗口明确提示并记录待生效 |

---

## 12 版风格稿（动效稿）—— 画廊交付

Date: 2026-09-28（第二轮）  
交付物: `docs/superpowers/previews/rise-hud-gallery.html`（单文件，零 CDN、零 JS 依赖，双击即开）

老大对首轮八版的两条批评：**「审美还是差点意思」**（八版都是同一套深色扁平风）、
**「你把特效加哪了」**（列了 21 条特效清单，稿子却全是静止的）。本轮据此重做。

### 方法修正

| 问题 | 修正 |
| --- | --- |
| 内联稿一次只能看一版，无法横向对比筛选 | 改为**单页画廊**，12 版并排，一屏可比 |
| 动效只写在文档里，稿子上看不见 | 每版由**真实 `@keyframes`** 驱动，页面里真在动（实测 25 个动画并发） |
| 八版风格同质 | 12 版跨冷暖 / 明暗 / 极简 / 繁复 / 东方 / 像素，风格差异最大化 |
| 特效与实现脱节 | 每版卡下标注「本版动效」+「WPF 落地手段」；页首附完整对照表 |

### 12 版清单

| # | 风格 | 主色 | 演示动效 | 关键 WPF 手段 |
| --- | --- | --- | --- | --- |
| 01 | NEON CYBER 霓虹赛博 | 青 `#00E5FF` → 品红 `#FF2BD6` | 血条扫光 / 全屏扫描线 / 怪物名发光呼吸 / 切角外形 | `OpacityMask`+`DoubleAnimation` / `TranslateTransform.Y` / `DropShadowEffect` / `Path`+`Clip` |
| 02 | GLASS 玻璃拟态 | 青绿 `#5EE7C4` → 蓝 `#4FA8E8` | 高光缓入缓出斜扫 / 磨砂透光层 | 高光同扫光；磨砂**用多层半透明 `Border` 模拟**，不用 `BlurEffect` |
| 03 | HOLOGRAM 全息投影 | 青→蓝→紫→粉 四段流动 | 色相流动渐变 / 干涉条纹滚动 / 全息底座呼吸 | `LinearGradientBrush` 的 `PointAnimation` / `DrawingBrush(TileMode=Tile)` Viewport 动画 |
| 04 | MOLTEN 熔岩熔金 | 橙 `#FF6A00` → 金 `#FFC400` | 熔岩渐变横向流动 / 火花周期掠过 | `PointAnimation` 循环 / 小 `Border`+`TranslateTransform.X` |
| 05 | MINIMAL LINE 极简线条 | 纯白 / 1px | 端点游标明暗呼吸（最克制） | 1px `Border`+`Opacity` 动画。**零 Effect、零离屏渲染，叠层窗口下最稳** |
| 06 | PIXEL RETRO 像素复古 | 绿 `#5DFF8F` | CRT 扫描线下移 / 方块步进闪烁 | `DrawingBrush` 位移 / `DiscreteDoubleKeyFrame` 做步进 |
| 07 | SEGMENT GAUGE 分段仪表 | 绿 `#39FF88` | LED 格子亮度呼吸 / 峰值保持线闪烁 | `DrawingBrush` 分段 + `Opacity` 动画 / 峰值线绑峰值缓存值 |
| 08 | LIQUID FILL 液态填充 | 青蓝 `#5FD8FF` | 液面斜纹流动 / 气泡上浮（3 颗错峰） | `DrawingBrush` 斜纹位移 / `Canvas`+3 个 `Ellipse`+`TranslateTransform.Y`（**数量必须封顶**） |
| 09 | ENERGY CORE 能量核心 | 紫 `#8C7AFF` → 青 `#4DE0FF` | 核心脉冲缩放 / 虚线环旋转 / 血条由中心向两侧展开 | `ScaleTransform`+`RotateTransform`；血条两侧各一条 `Border` 绑半值 |
| 10 | DARK SHARP 暗黑锐利 | 红 `#FF2D2D` → 橙 `#FF7A00` | 边框告警脉冲 / 血条亮度抖动 / 全局切角 | `Border.BorderBrush` 的 `ColorAnimation`（须用可动画的 `SolidColorBrush` 副本）/ `Path` 切角 |
| 11 | CRIMSON WA 朱红和风 | 朱红 `#B8322A` / 米白 `#F3ECE0` | 宣纸底高光缓扫 / 和纸纹理 | 同扫光；**唯一浅色版**，叠层窗口下 Background 用 `#F3F3ECE0` 不能全透明 |
| 12 | FROST 冰霜 | 冰蓝 `#6FC9FF` → 白 `#CDF2FF` | 霜晶点阵呼吸 / 冰面高光缓扫 | `DrawingBrush` 点阵平铺 + 整层 `Opacity` 动画 |

### 验证结果（无头 Chrome 实测）

| 项 | 结果 |
| --- | --- |
| 每版宽度 | **12/12 = 精确 300px** |
| 每版高度 | 12/12 = 134px（统一） |
| 容器横向溢出 | 12/12 `scrollWidth == clientWidth` → **零溢出** |
| 文字裁切 | 下钻检查 `.tag` / `.pname` / `.pv` / `.mname` / `.mpct` → **12/12 零裁切** |
| 文档横向滚动 | `scrollWidth == clientWidth == 1904` → 无 |
| 并发动画数 | 25（证明动效真实存在，非静态稿） |

> 唯一被诊断标为「越界」的是扫光条 `.gloss`（如 `L17 R-289`），这是**设计意图**：
> 扫光需从条外扫入、扫出条外，由父级 `overflow:hidden` 裁掉，非缺陷。

### 前置条件（复述）

所有发光 / 模糊 / 高频扫光要真正流畅，必须先解决渲染路径：把 `ClientConfig.Render` 从
`Software` 切到 `Hardware`（`App.xaml.cs:121` 会把 `ProcessRenderMode` 设为 `SoftwareOnly`，
GPU 合成被关闭）。此项属**配置项**，不触碰逻辑代码，符合「只改 UI」约束。
若不便改配置，则只能选 **05 MINIMAL** 这类零 Effect 方案。

---

## 落地实施：11 个风格包接入 Hub 主题选择器

Date: 2026-09-28（第三轮）  
老大决策：**12 版里剔除 09「能量核心」，其余 11 版全部要，且要放进 Hub 主题里供选择。**

### 核心问题：主题系统原本做不出这些效果

读码结论——现有主题系统只能换颜色：

| 层 | 原本能力 | 缺口 |
| --- | --- | --- |
| `Theme.*.xaml` | 仅 Color / Brush / FontSize / CornerRadius（46 行） | 无法表达扫光、发光、纹理、切角、血条高度 |
| `MonsterHudView.xaml` | 15 类键走 `DynamicResource`，但**另有 19 处硬编码颜色** | 轨底槽 `#1A222C`、硬直轨 `#B8C0CC`、状态金 `#FFD0B778`、捕获色等不随主题走 |

所以「换主题 = 换皮肤」必须**同时动主题层和 HUD 结构层**，只改一边都不成立。

### 实现分层

**第一层 · 主题资源契约扩展（21 个新键）**

新增两类键，全部 17 个主题文件都具备（共 59 个键）：

*皮肤参数（11 个）*

| 键 | 类型 | 作用 |
| --- | --- | --- |
| `Brushes.PanelGlow` + `Theme.GlowOpacity` | Brush + Double | 面板内发光描边 + 右上斜角标记的颜色与强度 |
| `Brushes.PanelGloss` + `Theme.GlossOpacity` | Brush + Double | 顶部反光强度 |
| `Theme.PanelTexture` + `Theme.TextureOpacity` | Brush + Double | 纹理层（DrawingBrush 平铺：扫描线 / 斜纹 / 点阵），**静态零动画** |
| `Theme.CornerSlashOpacity` | Double | 右上斜角标记强度（0 = 不显示） |
| `Brushes.HpBarGloss` + `Theme.HpGlossOpacity` | Brush + Double | 主血条扫光强度 |
| `Theme.HpHeight` | Double | 血条高度（1 = 极简细线，12 = 液态厚条） |
| `Fonts.Family.Hud` | FontFamily | 主题字体（像素版 Consolas、极简版 Segoe UI Light） |

*色板外提（10 个）*

`Brushes.RailTrack`（轨底槽）/ `FlinchFill`（硬直轨）/ `RailBroken`（破坏灰轨）/ `RailDefault`（部位轨默认）/ `StatusText`（状态文字）/ `HpTrack`（血条槽）/ `WeakenLine`（虚弱线）/ `CaptureBorder` / `CaptureFill` / `CaptureText`（捕获三件套）。

**第二层 · HUD 结构升级（`MonsterHudView.xaml`）**

- 19 处硬编码颜色全部改为 `DynamicResource`，**残留硬编码颜色 = 0**
- `PanelRoot` 内包一层 `Grid`，插入 4 个皮肤层：**A 纹理 / B 顶部反光 / C 内发光描边 / D 右上斜角标记**
- 主血条加扫光层 `HpGloss`（`TranslateTransform.X` 循环动画，`From=-46 To=300`，2.4s）
- 血条高度改绑 `Theme.HpHeight`
- 晕眩 / 御龙 chip 的半透明底改为 `<SolidColorBrush Color="{DynamicResource Colors.*}" Opacity="0.22" />`，浅色主题下不再刺眼

**关键设计**：所有皮肤层都带**中性默认值**（`GlowOpacity=0`、`TextureOpacity=0`、`GlossOpacity=0`、`HpGlossOpacity=0`、`HpHeight=8`），
所以**现有 7 个主题的观感与改动前完全一致**，零回归。

**宽度约束**：皮肤层用 `Margin="-8,-6"` 贴合 `PanelRoot` 的 `Padding="8,6"`，
**不外扩、不参与布局测量**，`Width="300"` 保持不变。

### 11 个主题清单

| ID | 名称 | 面板圆角 | 血条高 | 关键皮肤参数 |
| --- | --- | --- | --- | --- |
| `NeonCyber` | 霓虹赛博 | 2 | 9 | 青→紫→品红渐变血条，发光 0.55，扫描线纹理，斜角 0.85，Consolas |
| `Glass` | 玻璃（**升级现有**） | 15 | 10 | 青绿→蓝渐变，反光 0.24，发光 0.22 |
| `Hologram` | 全息投影 | 3 | 9 | 四段色相渐变，扫描线纹理 1.0，发光 0.45 |
| `Molten` | 熔岩熔金 | 4 | 10 | 暗→橙→金渐变，发光 0.5 |
| `MinimalLine` | 极简线条 | 2 | **1** | **全部皮肤参数归零**，血条 1px 纯白，Segoe UI Light |
| `PixelRetro` | 像素复古 | **0** | 10 | 绿色，CRT 扫描线 1.0，Consolas |
| `SegmentGauge` | 分段仪表 | 6 | 11 | 绿色，扫描线 0.5，Consolas |
| `LiquidFill` | 液态填充 | 13 | **12** | 青蓝渐变，反光 0.16，扫光 0.85 |
| `DarkSharp` | 暗黑锐利 | 0 | 9 | 红→橙渐变，发光 0.4，斜角 0.9 |
| `CrimsonWa` | 朱红和风 | 3 | 10 | **唯一浅色主题**，米白底 + 朱红，斜纹纹理 |
| `Frost` | 冰霜 | 9 | 10 | 冰蓝→白渐变，点阵纹理 0.9，发光 0.35 |

### 改动文件清单

| 文件 | 改动 |
| --- | --- |
| `Themes/Theme.{10 个新主题}.xaml` | 新增 |
| `Themes/Theme.Glass.xaml` | 重写为玻璃拟态（ID 不变，原「圆角+边框」版本升级） |
| `Themes/Theme.{Classic,Parchment,Soft,OledCoral,GlassCoral,Transparent}.xaml` | 各追加 21 个中性默认键 |
| `Views/MonsterHudView.xaml` | 硬编码色外提 + 4 个皮肤层 + 血条扫光层 |
| `Themes/RiseThemeIds.cs` | **+10 个常量 + All 数组 + DisplayName 中文名** |
| `Shell/RiseShellWindow.xaml.cs` | **+10 个主题预览渐变色分支** |

> **关于「只改 UI」约束的说明**：本轮改动了 2 个 `.cs` 文件。
> 二者均为**纯声明式主题注册**（常量表 / 显示名 / 预览色），**不含任何逻辑分支**，不触碰数据采集、解析、ViewModel 或配置。
> 不改这两处，新主题不会出现在 Hub 列表中——这是「放进 Hub 供选择」这一需求的硬性前提。
> `RiseThemeService.ApplyTheme()`、`HudConfig.ThemeId` 读写链路**一行未动**。

### 验证结果

| 项 | 结果 |
| --- | --- |
| `dotnet build RiseOverlay.UI` | **0 错误**（初次报 `MC3072`：`Border` 无 `FontFamily` 属性 → 已改挂到 `UserControl`） |
| 主题字典编译 | **18 个 BAML 全部生成** → 不只是 XML 合法，类型与语法均通过 XAML 编译器 |
| 资源键完整性 | **17/17 主题各 59 键，缺失 0** |
| HUD 残留硬编码颜色 | **0 处** |
| XML 合法性 | 18/18 通过 |
| Hub 列表容量 | `ThemeGrid` 为 2 列 `UniformGrid` + `ScrollViewer`，17 个主题自动换行可滚动 |
| 现有测试影响 | `RiseOverlay.UI.Tests` 只涉及 `MonsterHudViewModel`，**与主题/XAML 无关** |

### 已知限制

1. **动效需硬件渲染才流畅**：扫光、发光在 `SoftwareOnly` 下仍会走 CPU。见本文档「关键约束」节。
2. **切角是「角标」而非真裁切**：HUD 高度为 Auto，`Geometry` 无法自适应，故用右上角三角标记表达（`Theme.CornerSlashOpacity`）。
3. **捕获徽章 / 完成横幅用近似语义键**：复用了 `Brushes.RailTrack` / `CaptureFill` 等，语义不完全精确但各主题下可读。
4. **`Glass` 主题观感已变**：从「圆角 + 白边框」升级为完整玻璃拟态（渐变血条、反光、内发光、圆角 15）。若需保留旧观感，需另立新 ID。

### 如何新增一个主题

1. 复制任一 `Theme.*.xaml`，改 59 个键的值
2. `RiseThemeIds.cs` 加常量 + 进 `All` 数组 + 补 `DisplayName`
3. `RiseShellWindow.xaml.cs` 的 `ThemePreviewBrush` 加一个 `Gradient(...)` 分支
4. 重新构建即可，Hub 列表自动出现

---

## 状态行 / 异常状态色块（第四轮）

老大指出：**「愤怒晕眩耐力御龙还有各种异常状态样式你怎么没改呢？」** —— 确实漏了。

### 为什么纯 XAML 改不了

这两行在 ViewModel 里**已经被压扁成字符串**：

| 显示行 | 绑定属性 | 生成方式 |
| --- | --- | --- |
| `愤怒 1:18 · 晕眩 7% · 耐力 98% · 御龙 34%` | `StatusLineText`（`string`） | `StatusLineFormatter.Format(StatusLineModel)` → `string.Join(" · ", …)` |
| `减气 39%  毒 66%` | `AilmentsLineText`（`string`） | `AilmentLineFormatter.Format(IEnumerable<AilmentDto>)` → `string.Join(" ", …)` |

XAML 只拿到一个 `string`，**没有任何办法给其中「愤怒」两个字单独上色**。而数据本身是结构化的：

- `StatusLineModel`：`EnrageRemaining` / `StunBuildupPercent` / `StunActive` + `StunActiveRemaining` / `StaminaPercent` / `RideBuildupPercent` / `RideActive` + `RideActiveRemaining`
- `AilmentDto`：`Key` / `DisplayName` / `Percent` / `IsActive`

**结论：要在 HUD 上分段着色，必须在 ViewModel 多暴露一份结构化数据。这是硬性前提，不是偷懒。**

### 实现：纯新增，不动任何现有行为

**新增文件** `ViewModels/HudStatusChip.cs`

```csharp
public enum HudChipKind { Enrage, Stun, Stamina, Ride, Ailment }

public sealed record HudStatusChip(string Label, string Value, HudChipKind Kind, bool IsActive = false);
```

**`MonsterHudViewModel` 新增**（原属性一行未改）

| 新增 | 说明 |
| --- | --- |
| `ObservableCollection<HudStatusChip> StatusChips` | 与 `StatusLineText` 内容等价 |
| `ObservableCollection<HudStatusChip> AilmentChips` | 与 `AilmentsLineText` 内容等价 |
| `BuildStatusChips(StatusLineModel)` | **逐条对齐** `StatusLineFormatter.Format` 的分支逻辑（含「激活态优先、否则显示累积值」） |
| `BuildAilmentChips(IEnumerable<AilmentDto>?)` | **逐条对齐** `AilmentLineFormatter.Format` 的过滤（排除 `stun`、只留 `IsActive \|\| Percent > 0`） |
| `SyncChips(...)` | 内容未变化时不重建集合，避免每秒多次 `Clear/Add` 造成 UI 抖动 |

> **两个字符串属性原样保留并继续赋值**（`ApplyDto` 第 532 / 537 行），未使用新集合的宿主仍可正常工作。

**`StatusLineFormatter.FormatClock` 由 `private` 改为 `public`** —— 让 UI 层复用同一套时钟格式化，避免两处逻辑漂移。纯可见性变更，零行为变化。

### 视觉设计

色块 = **左侧 2px 语义色条 + 标签（语义色）+ 数值（前景色）**，比原纯文本更易扫读，且横向占用更小：

```
▍愤怒 1:18  ▍晕眩 7%  ▍耐力 98%  ▍御龙 34%
▍减气 39%  ▍毒 66%
```

- 用 `ItemsControl` + **`WrapPanel`** 承载，超宽自动换行，**不溢出 300px**
- 激活态（`IsActive`：异常生效中 / 晕眩中 / 御龙中）标签加粗
- 数值用 `Typography.NumeralAlignment="Tabular"` 等宽数字，跳动时不抖
- 模板抽为共享 `DataTemplate x:Key="StatusChipTemplate"`，状态行与异常行复用

### 主题键（5 个，引用既有强调色）

```xml
<SolidColorBrush x:Key="Brushes.StatusEnrage"  Color="{StaticResource Colors.AccentBreak}" />
<SolidColorBrush x:Key="Brushes.StatusStun"    Color="{StaticResource Colors.AccentStun}" />
<SolidColorBrush x:Key="Brushes.StatusStamina" Color="{StaticResource Colors.AccentTeal}" />
<SolidColorBrush x:Key="Brushes.StatusRide"    Color="{StaticResource Colors.AccentCapture}" />
<SolidColorBrush x:Key="Brushes.StatusAilment" Color="{StaticResource Colors.AccentQurio}" />
```

**关键**：色值用 `StaticResource` 引用各主题**既有的** `Colors.*`，所以 **17 个主题自动跟随，无需逐主题定色**。
`XAML` 侧按 `HudChipKind` 用 `DataTrigger` 选键。

### 验证结果

| 项 | 结果 |
| --- | --- |
| `dotnet build RiseOverlay.UI` | **0 错误** |
| `RiseOverlay.Domain.Tests` | **261 通过 / 0 失败** |
| `RiseOverlay.UI.Tests` | **18 通过 / 0 失败** |
| 主题键完整性 | **17/17 主题均含 5 个状态键，缺 0** |
| `MonsterHudView.xaml` | XML 合法 |
| 现有行为 | `StatusLineText` / `AilmentsLineText` 仍在赋值，**未删除、未改语义** |

### 边界说明

- 本轮改动了 3 个 `.cs`：新增 `HudStatusChip.cs`、`MonsterHudViewModel.cs`（**纯新增成员**）、`StatusLineFormatter.cs`（**仅 `private`→`public`**）。
  未修改任何现有属性、未改任何分支条件、未动数据采集与解析。
- `StatusLineModel.DownRemaining`（倒地剩余）**在原有 formatter 中本就未被使用**，本轮同样不启用，保持输出一致。

---

## 第五章 · 部位行分级折叠（设计稿，未实施）

> 稿子：`docs/superpowers/previews/rise-parts-tiering.html`
> 状态：**仅设计稿，代码零改动**，等确认后再实施。

### 5.1 先撤回一条错误建议：推荐目标置顶

上一轮提出的「推荐目标自动置顶」**已撤回**。撤回依据是完整来源链核查：

| 环节 | 代码位置 | 事实 |
|---|---|---|
| 推荐属性来源 | `MonsterHudMapper.cs:211` | `recommended = staticSnapshot.Recommended`，来自**静态快照** |
| 部位判定 | `MonsterHudMapper.cs:223-224` | `recommendedSet.Contains(partWeak)`，只比静态弱点属性；`CurrentHp` / `IsBroken` / `Flinch` **一个都没参与** |
| 推荐算法 | `ElementRecommend.FromHitzones` | 取弱点数值最大者（并列全取） |

**两个结论：**

1. **推荐目标整场战斗恒定不变。** 不存在「打着打着突然跳走」——这一点原担心不成立。
2. **推荐目标是一组，不是单个，且命中率极高。**
   真实 sample（`CreateSampleScornedMagnamalo()`，嗟怨震天怨虎龙）的推荐属性是**水**，
   而五个部位（头部 / 前肢 / 腹部 / 尾巴 / 背鳍）弱点**全部含水** →
   `IsRecommendedTarget` **5 / 5 = 100% 命中**。

第 2 条直接判了置顶的死刑：**100% 命中率的分组，排序收益为 0 行。**
视觉上也能佐证——现状稿里五个部位名**全部是高亮白**，等于没有高亮。

### 5.2 分级依据：换成「部位有没有第二维状态」

推荐维度不可用之后，改用真正有区分度的维度：

> **血量是所有部位都有的第一维（人人平等）；稀缺的是第二条进度轨** ——
> 可断尾进度、怪异化进度、硬直积攒。有第二维的部位才值得占一整行。

**判定规则（三条，全部基于 `PartRowViewModel` 已有字段，无需新增数据源）：**

| 条件 | 层级 |
|---|---|
| 可断尾未断（`IsSeverable`） | **焦点行**（完整：名字 / 状态 / 弱属性 / 双轨） |
| 怪异化（`IsQurio`） | **焦点行** |
| 其余 | **摘要行**（一行紧凑文本：`头部 52% · 腹部 73% · 背鳍 64%`） |

**破坏后的两种策略（本稿唯一需拍板处）：**

- **策略 A（推荐）· 留在焦点层** —— 行不移动，轨道变灰 + 状态改「已断」。**零位移。**
- **策略 B · 降级到摘要层** —— 整行缩进摘要行，焦点层收窄。省空间，但行会跳一次。

**边界情况（易错点）**：已破坏但**从来不是焦点**的部位（如本例「前肢」，既不可断尾也不怪异化）
**永远不进焦点层**。若写成「`IsBroken` → 进焦点层」，会把所有破过的部位一起捞上来。
正确写法是 `(IsSeverable || IsQurio) && (!IsBroken || keepBroken)`。

### 5.3 与「空间恒定性」原则的关系

本方案**不违反**第四章确立的空间恒定性原则：

- 焦点层的构成由 `IsSeverable` / `IsQurio` 决定，两者都是**静态属性** →
  **同一只怪整场战斗焦点层构成恒定**，行位置稳定。
- 唯一的位移发生在「部位被破坏」——不可逆事件、只发生一次、且玩家注意力本就在该部位。

### 5.4 实测收益（无头 Chrome，1944×2249 视口）

| 指标 | 现状 | 分级折叠 |
|---|---|---|
| HUD 卡片总高 | **238px** | **150px** |
| 省下 | — | **88px（37%）** |
| 卡片宽度 | 300px | 300px |
| 横向溢出 | 0px | 0px |

**7 张卡片全部实测 300px 宽、横向溢出 0、控制台 0 错误、文档横向溢出 0。**

各卡片行数核对（`.prow` 焦点行 / `.psummary` 摘要行）：

| 卡片 | 尺寸 | 焦点行 | 摘要行 |
|---|---|---|---|
| 现状 | 300×238 | 5 | 0 |
| 分级折叠 | 300×150 | 1 | 1 |
| 常态 | 300×150 | 1 | 1 |
| 破坏瞬间（策略 A） | 300×150 | 1 | 1 |
| 怪异化 | 300×178 | 2 | 1 |
| 策略 A | 300×150 | 1 | 1 |
| 策略 B | 300×122 | 0 | 1 |

### 5.5 遗留待定项

- 摘要行用**百分比**（`头部 52%`），完整行用**绝对值**（`620`）。语义不统一是宽度所迫
  （`620 / 1200` 在 300px 内放不下）。百分比对「哪个快破了」其实更直观，但需确认可接受。
- 摘要行字号 9.5px，在游戏实机上可能偏小（玩家离屏较远），实施时可上调至 10–11px。
- 摘要行**不含弱属性徽章**，弱属性信息在非焦点部位上被放弃。如需保留，得改成第二行或 tooltip。

---

## 第六章 · 12 版信息架构重构稿（设计探索，未实施）

> 稿子：`docs/superpowers/previews/rise-hud-layouts.html`
> 状态：**仅设计探索**，等筛选后再定方案。

### 6.1 与前三章的区别

前几章是**换皮**——骨架始终是「竖排 StackPanel，一行一个部位」，只换颜色、纹理、发光。
本章是**换骨架**——部位区不再假设「必须一行一个部位」。

### 6.2 十二种架构

| # | 架构 | 部位区高度 | 类型 | 核心思路 |
|---|---|---|---|---|
| 01 | SEGMENTED RAIL 分段轨 | **≈24px** | 聚合 | 五部位压成一条水平轨；轨宽按部位**上限**分配（静态不变），填充按当前血量 |
| 02 | HEAT GRID 热力网格 | ≈68px | 矩阵 | 3×2 色块，块底色深浅 = 血量 |
| 03 | RADIAL 环形雷达 | ≈118px | 环形 | 部位按方位排成环段，弧长 = 血量，中心为总体血量 |
| 04 | RIBBON 等宽竖柱 | **≈38px** | 聚合 | 五等宽竖柱紧贴排开，柱高 = 血量 |
| 05 | FOCUS + STRIP 聚焦式 | ≈62px | 分级 | 当前目标放大成主卡，其余压成一条细带 |
| 06 | GAUGE CLUSTER 仪表盘集群 | ≈96px | 环形 | 主血量做中央大环，部位做右侧小环 |
| 07 | REVERSED BARS 反转条 | ≈48px | 条列 | 名右置贴边，条从右向左生长 |
| 08 | SPARKLINE 掉血时间轴 | ≈52px | 历史 | 每部位一条掉血历史曲线，**需新增历史缓冲** |
| 09 | STATE CARDS 状态卡片 | ≈44px | 分级 | 只显示需要动作的部位，其余收成一行汇总 |
| 10 | TWO-COLUMN 双列紧凑 | ≈48px | 矩阵 | 两列排布，5 部位只占 3 行 |
| 11 | ICON MATRIX 首字环 | ≈52px | 符号 | 每部位一个圆环 + 首字，环弧长 = 血量 |
| 12 | THIN BARS 极细条阵列 | ≈42px | 条列 | 每部位一条 3px 细条紧密堆叠 |

### 6.3 实测（无头 Chrome，1180 视口）

- **12 / 12 卡片全部 300px 宽，横向溢出 0px**
- 文档横向溢出 0px，控制台 0 错误
- 卡片总高：最矮 **01 · 124px**，最高 **03 · 218px**（现行竖排版约 **238px**）

### 6.4 需要注意的实现代价

| 架构 | 实现代价 |
|---|---|
| 01 / 04 / 12 | **纯 XAML 可做**，只需把 `ItemsControl` 换成自定义 `Panel` 或 `UniformGrid` |
| 02 / 10 | 纯 XAML 可做（`UniformGrid` + `DataTrigger` 控色） |
| 03 / 06 / 11 | 需要 `Path`/`ArcSegment` 手工算弧长，**建议在 ViewModel 里算好 `StartAngle`/`SweepAngle`**，XAML 只绑定 |
| 05 / 09 | 需要 ViewModel 暴露「焦点部位」与「需动作部位」两个集合（与第五章方案同源） |
| 07 | 纯 XAML 可做（`FlowDirection` 或反向 `StackPanel`） |
| 08 | **代价最高**：需要环形缓冲保存历史血量序列，且 300px 内曲线很挤 |

### 6.5 未定

- 尚未筛选。老大看过后选定的架构再进入实施评估。
- 部分架构可**组合**（例：主血条 + 05 焦点卡 + 01 分段轨），组合方案未展开。

---

## 第七章 · 怪物本体可视化（设计探索，未实施）

> 稿子：`docs/superpowers/previews/rise-hud-anatomy.html`
> 状态：**仅设计探索**，等筛选后再定方案。

### 7.1 为什么要转向

第六章那 12 版被判定为**排版实验**，不是设计想象 —— 本质是「把 5 个部位摆成轨 / 网格 / 环 / 柱 / 条」，
换的是**排队方式**，骨架仍假设「部位是一行文字或一个色块」。

本章换的是**表达媒介**：

> **不再用列表表示部位，直接把怪物画出来。**
> 部位不再是「第 3 行」，而是怪物身上的一块地方。

玩家一眼看到的是「这只怪身上哪儿在掉血、哪儿该打」，而不是「读一行字」。

### 7.2 六套视觉语言（共用同一套解剖剪影）

| # | 风格 | 视觉语言 |
|---|---|---|
| 01 | ANATOMY 解剖剪影 | 深色面板 + 部位按血量亮度着红，像生物课本解剖图 |
| 02 | X-RAY X 光透视 | 青色半透明填充 + 双层描边辉光，像阅片灯箱 |
| 03 | HOLOGRAM 全息投影 | 扫描线铺满剪影 + 轮廓外发光 |
| 04 | HUNTER NOTES 猎人笔记 | 暖棕墨线 + 手绘虚线轮廓，怪猎世界观 |
| 05 | QURIO VEINS 怪异化脉络 | 暗紫底 + 红色结晶脉络贯穿，血量越低脉络越亮 |
| 06 | WIREFRAME 线框模型 | 完全不填充，只画轮廓线，最不挡视野 |

### 7.3 实现方案（这是本节最有价值的部分）

**踩过的坑：用椭圆 / 三角 / 长条拼贴出来的「剪影」，不是剪影。**
第一版用 `<ellipse>` 做头、`<ellipse>` 做躯干、`<polygon>` 做背鳍、`<rect>` 做腿 ——
渲染出来是「两个分离的球 + 一根棍子」，完全不像生物。

**正确做法：一条连续的生物轮廓 `path` + 矩形区域被它裁剪。**

```js
var BODY_PATH = 'M12,64 C14,56 22,52 32,50 C40,48 46,40 52,24 ... Z';  // 角/双背刃/长尾/四足
var REGIONS = { '头部':{x:8,y:40,w:68,h:40}, '前肢':{x:86,y:83,w:26,h:29}, ... };

<defs><clipPath id="body-UID"><path d={BODY_PATH}/></clipPath></defs>
<path d={BODY_PATH} fill="rgba(255,255,255,.04)"/>          <!-- 剪影底 -->
<g clip-path="url(#body-UID)">
  {REGIONS 逐块着色}                                        <!-- 被裁成有机形状 -->
</g>
<path d={BODY_PATH} fill="none" stroke={theme.outline}/>     <!-- 轮廓线 -->
```

要点：

- **轮廓负责「像不像」，区域负责「分不分」** —— 两件事分开，各自好调。
- 区域用矩形即可，被轮廓裁完自然贴合身体；**不要试图手写 5 个有机形状的 path**。
- 区域边界**不能侵入躯干**：前肢区域初版 `y:76` 导致虚线框横跨躯干，像贴了个盒子；
  下移到 `y:83` 只覆盖腿部后正常。
- 已断部位用**斜线纹理**（`<pattern>` + `rotate(45)`）而非虚线框，语义更准。
- 血量→颜色**只用亮度分级**（`hsl(8,62%,17+32k%)`），越暗 = 血越少。
  初版额外做了色相偏移（低血偏橙），结果 35% 的尾巴比 52% 的头部更醒目 —— 反直觉，已回退。

### 7.4 实测（无头 Chrome，1180 视口）

- **6 / 6 卡片 300px 宽，横向溢出 0px**，文档横向溢出 0px，控制台 0 错误
- 6 个 SVG 全部渲染
- 卡片总高 **241px**（现行竖排版约 238px —— **基本持平，视觉价值是净增的**）

### 7.5 与现行版的关系

**不是替换关系，是新增一层。** 两种接法：

1. 文字行整体保留在剪影下方 —— 剪影负责「空间锚定」，文字负责「精确数值」
2. 文字行折叠掉，只留剪影 + 一行图例（稿子里每版底部那行）

### 7.6 实现代价

| 项 | 说明 |
|---|---|
| 剪影 `Path` | 一条静态 path，**纯 XAML 可做**（`Path.Data` 直接写） |
| 区域裁剪 | XAML 用 `UIElement.Clip`（`RectangleGeometry`）或把区域做成 `Path` 的 `GeometryGroup` |
| 区域着色 | `DataTrigger` 绑定 `CurrentHp/MaxHp` → 需要 **ViewModel 暴露一个 0..1 的 `HealthRatio`**（已存在） |
| 颜色插值 | 纯 XAML 只能做**分级**（`DataTrigger` 若干档）；连续插值需 ViewModel 算好 `Fill` |
| 斜线纹理 | `DrawingBrush` + `TileMode=Tile` + `RelativeTransform` 旋转 45° |
| 扫描线 / 发光 | `DrawingBrush` / `DropShadowEffect`，**建议硬件渲染** |

**结论：01 / 04 / 06 三版最容易落地**（纯描边 + 分级填色）；
02 / 03 / 05 需要纹理和发光，观感更好但对渲染路径有要求。

### 7.7 本方案已被否决（2026-09-28）

> **否决理由（正确）**：怪物轮廓是「一怪一形」的死路。
> Rise 有几十只怪，不可能为每只画一套剪影 —— 方案从根上就错了。

保留本节作为**技术记录**（连续轮廓 + 区域裁剪的手法本身可复用），
但方向作废，不再推进。后续方向见第八章。

---

## 第八章 · 不依赖外形的 6 种空间化表达（设计探索，未实施）

> 稿子：`docs/superpowers/previews/rise-hud-abstract.html`
> 状态：**仅设计探索**，等筛选后再定方案。

### 8.1 收紧后的约束

第七章被否决后，约束变成两条**必须同时满足**：

1. 要有**空间感和画面感**（不能退回列表）
2. **不得依赖任何具体外形**（不能一怪一画）

破题点：**只用所有怪物都共有的东西**——

- 部位的**方位**：头在前、尾在后、背在上、腹在下
- 部位的**连接关系**：头连躯干、尾连躯干、左右对称

这两样对任何一只怪都成立，**零美术资产**。

### 8.2 六版

| # | 名称 | 视觉隐喻 |
|---|---|---|
| 01 | NEXUS 神经节点 | 中心节点是本体，五部位节点按解剖方位环绕，突触连线粗细 = 血量 |
| 02 | CELLS 细胞组织 | 五块不规则有机区拼合成整体，像显微镜下组织切片 |
| 03 | PETALS 曼陀罗 | 五片花瓣辐射对称，花瓣亮度 = 血量 |
| 04 | SPECTRUM 能量柱 | 五根发光柱从基线升起，柱高 = 血量，柱顶光晕 |
| 05 | ORBIT 轨道行星 | 中心核心 + 五条透视椭圆轨道，部位是轨道上的行星 |
| 06 | CONSTELLATION 星图 | 五颗星点结成星座，星点亮度与大小 = 血量 |

### 8.3 通用性论证（本节核心）

六版**只消费两个数据**：

1. 部位的**方位**（在前 / 后 / 上 / 下）—— 用于决定节点坐标
2. 部位的**血量比例** `CurrentHp / MaxHp` —— 用于决定亮度、弧长、柱高、星球大小

**换成任何一只怪，只是节点数量与百分比变，布局代码一行不改、零美术资产。**
这正是第七章做不到而本章做得到的。

### 8.4 统一配色模型

只给**色相 + 饱和度**，亮度由血量决定（越低越暗），六版共用一套函数：

```js
var HUES = { nexus:{h:168,s:58}, cells:{h:280,s:52}, petals:{h:40,s:62},
             spec:{h:198,s:62}, orbit:{h:252,s:58}, star:{h:220,s:22} };
fillOf(key, n)   // → hsl(h, s, 16 + 30*ratio %)
strokeOf(key, n) // → hsl(h, s, 30 + 32*ratio %)
glowOf(key, n)   // → hsl(h, s, 38 + 30*ratio %)
```

已断部位统一：`rgba(255,255,255,.045)` 填充 + 虚线/斜线描边。

### 8.5 实测（无头 Chrome，1180 视口）

- **6 / 6 卡片 300px 宽，横向溢出 0px**，文档横向溢出 0px，控制台 0 错误
- 6 个 SVG 全部渲染
- 卡片总高 **233 ~ 257px**（现行竖排版约 238px）

### 8.6 实现代价

| 版 | 落地难度 | 说明 |
|---|---|---|
| 01 NEXUS | 低 | 圆环 + `<Line>`，坐标**建议 ViewModel 算好** |
| 02 CELLS | **低** | 手写 5 条 `Path` 即可，**纯 XAML 可做** |
| 03 PETALS | **低** | `Ellipse` + `RotateTransform`，纯 XAML 可做 |
| 04 SPECTRUM | **低** | `Rectangle` + `Ellipse` 光晕，纯 XAML 可做 |
| 05 ORBIT | 中 | 需要三角函数定位，**ViewModel 算好坐标** |
| 06 CONSTELLATION | **低** | `Ellipse` + `Line` + 星尘，纯 XAML 可做 |

**总体：比第七章容易得多** —— 没有轮廓 path、没有 clipPath、没有纹理 pattern，
主要是「圆/矩形/线 + 按血量控亮度」，**ViewModel 只需多暴露一个 `HealthRatio`**。

---

## 九、等距 3D 表达（isometric）—— 3D 感，零 3D 引擎

**交付物**：`docs/superpowers/previews/rise-hud-iso3d.html`（6 版）
**触发**：老大问「你能找到怪物的 3d 模型吗？能找到做个 3d 显示就更牛逼了」

### 9.1 模型：拿不到，也不该拿

调查结论（三条硬证据）：

| 项 | 结果 |
|---|---|
| 3D 代码 | `grep -r "Viewport3D\|MeshGeometry3D\|PerspectiveCamera\|Media3D"` → **零命中** |
| 模型文件 | `.mod3 / .obj / .fbx / .gltf / .glb / .dae` → **零文件** |
| 渲染管线 | `HunterPie/App.xaml.cs:121` → `Hardware ? RenderMode.Default : RenderMode.SoftwareOnly`；`ClientConfig.cs:63` 默认 **Software** |

MHR 官方模型是 RE Engine 的 `.mod3`，版权归 Capcom 不可分发。且就算自行提取，
又回到「一怪一模型」——**与 7.7 被否决的怪物剪影是同一个坑**。

### 9.2 结论：3D 感不需要模型，等距投影就够

等距投影（isometric）的本质是**把 3D 坐标算成 2D 多边形**：

```
sx = (x − y) · cos30°
sy = (x + y) · sin30° − z
```

- 一个长方体 = **3 个 `Polygon`**（顶 / 左 / 右），按 **顶亮 / 左中 / 右暗** 上色 → 立体感来自明暗差，不来自光照
- 前后遮挡：按 `x + y` **升序绘制**（越小越远，先画）。这是等距投影唯一的排序规则，一行 `OrderBy` 搞定
- **没有摄像机、没有光照、没有 GPU** → 软渲染下性能与画 2D 多边形完全一致

### 9.3 六版

| 版 | 名称 | 结构 | 落地面 |
|---|---|---|---|
| 01 | TOWERS · 能量塔 | 5 根柱体沿屏幕水平等距轴排开，柱高 = 血量 | 每柱 3 `Path` |
| 02 | ORBITAL · 轨道球 | 3 条等距椭圆轨道 + 核心球，节点球半径 = 血量 | `Ellipse` + `Path` |
| 03 | CORE FIELD · 核心力场 | 核心向 5 部位射光束，光柱粗细 = 血量 | `Line` + `Path` |
| 04 | HOLO PANELS · 全息面板 | 5 块面板沿纵深后退，板内进度条 = 血量 | 平行四边形 = 4 点 `Path` |
| 05 | PYRAMIDS · 金字塔阵 | 同 01 但换四棱锥，锥面明暗差更强 | 每锥 3 `Path` |
| 06 | HELIX · 螺旋阶梯 | 5 方块沿螺旋逐级上升，纵深最明显 | 需 `x+y` 排序 |

### 9.4 几何约束（踩坑记录）

第一版参数**全线超标**，六版全部被 `viewBox` 裁切 —— 而当时的诊断脚本只测文档级溢出，
报 `overflowX=0` 是**假绿**。真实的失败形态：

- 01/05 柱子底半截被裁 → 屏幕上只剩「顶面菱形」，看起来像金字塔（**误导性极强**）
- 04 面板宽度是视口的 2 倍以上
- 03 高节点 `sy` 为负，整个飞出上边界

修正后确立的**体检口径**（已写进 HTML 的 `geoAudit()`）：

1. **图形越界** = 任一实体面 / 文字的实际 `bbox` 超出 SVG 视口（> 0.5px 即计）
2. **标签压图** = 文字 `bbox` 与实体面 `bbox` 双向重叠超过 1px
3. 刻意写在实体上的数字（核心 `19.7`）打 `onfill` 类，从压图检查中豁免，但仍参与越界检查

**必须两项都为 0**，否则就是「看着能跑、上线被裁」。

几何设计要点：

- 柱子沿 **x = −y 的等距轴**排开 → `(x+y)` 恒定 → 底边是一条**水平线**而非阶梯
- 标签锚点**按包围盒算**（HELIX 的 `SIDE` 查表 + 8 顶点投影求 bbox），不用手调魔数
- ORBITAL 的核心球上移到 `z=22`，与节点球留出净空；头部标签改挂球下方避开核心

### 9.5 实现代价

投影数学需要**一个地方住**。项目已有 70+ 个 `IValueConverter`（`HunterPie.UI/Architecture/Converters/`），
且已有 `DynamicMonsterBarSizeConverter` / `CurrentValueToWidthConverter` 这类「数值 → 几何」的先例。

**结论：加一个 `IsoProjectConverter`（约 30 行，UI 层新增文件，不动任何既有逻辑）**，
在 XAML 里声明为资源即可。这与「只改 UI」的约束不冲突 —— 它不碰 `MonsterHudMapper` / DTO / 判定逻辑。

### 9.6 实测（无头 Chrome，1180 视口）

| 版本 | 卡片高 | 宽度 | 横向溢出 | 图形越界 | 标签压图 |
|---|---|---|---|---|---|
| 01 TOWERS | 242px | 300px | 0px | 0 | 0 |
| 02 ORBITAL | 242px | 300px | 0px | 0 | 0 |
| 03 CORE FIELD | 242px | 300px | 0px | 0 | 0 |
| 04 HOLO PANELS | 242px | 300px | 0px | 0 | 0 |
| 05 PYRAMIDS | 242px | 300px | 0px | 0 | 0 |
| 06 HELIX | 242px | 300px | 0px | 0 | 0 |

6 SVG / 56 polygon / 控制台 0 错误 / 文档溢出 0。**全部通过。**

### 9.7 待老大裁决

- 六版选哪一版（或哪两版进主题供切换）
- 是否接受「UI 层新增一个转换器」这个前提

---

## 十、怪物素材：本地那份是金矿（2026-09-28 老大提供）

**交付物**：`docs/superpowers/previews/rise-hud-monster-assets.html`（7 版，真实素材）
**来源**：`F:/mhrise-app/assets/icons/`（老大提供）+ The Models Resource

### 10.1 素材盘点

| 文件 | 数量 | 尺寸 | 内容 |
|---|---|---|---|
| `em###_##_icon.png` | 78 | 256×256 RGBA | 怪物头像，透明底，**零映射问题，可直接用** |
| `em###_##_parts_group.png` | 78 | ~600–800 × 200–790 | **游戏原生部位剪影**，按部位切成 7–8 个纯色块 |
| `em###_##_meat.png` | 78 | 同上 | 同一剪影、不同配色（肉質图） |

变体覆盖：`_00` 原种 / `_01` 亚种 / `_02` 稀少种 / `_05` 特殊个体 / `_07` 霸主 / `_08` 傀异克服。
**嗟怨震天怨虎龙 = `em089_05`**，全套素材齐备。

色块是标准 8 色高对比调色板：`#E6194B` `#F58231` `#FFE119` `#3CB44B` `#42D4F4` `#4363D8` `#911EB4` `#FFFFFF`。

### 10.2 The Models Resource：是真 3D，但用不起

页面（如 `amatsu`）提供 `amatsu.obj/.fbx/.dae/.mtl` + 全套贴图 + 骨骼动画，
单只 **98.64 MB / 41 个文件**，命名同为 `em058` 体系。

| 路线 | 可行性 | 原因 |
|---|---|---|
| 运行时加载 3D | ❌ | 单只 ~100MB，78 只 ≈ 8GB；项目零 3D 代码、默认 `SoftwareOnly` 软渲染 |
| **离线烘焙成 2D** | ✅ | 用模型离线渲出剪影/部位图层，只打包几百 KB 的 PNG。本质即 `_parts_group.png` 的来源 |

### 10.3 染色技术：alpha 当遮罩（关键）

CSS `mask-image: url(png)` —— 拿 PNG 的 **alpha 通道**当遮罩、整体填一个纯色。
**WPF 等价物 = `<Rectangle Fill="..."><Rectangle.OpacityMask><ImageBrush/></Rectangle.OpacityMask></Rectangle>`**，
一行 XAML，运行时随便改颜色。

`rise-hud-monster-assets.html` 里的 A7 版更进一步：**用 canvas 在浏览器里把 `parts_group.png`
按色块实时拆成 7 张单色遮罩**，每层按对应部位血量单独染色 → 残血部位自己暗下去、断掉的部位变灰。
（WPF 里等价于预切 7 张 mask PNG。）

### 10.4 七版 HUD 用法

| 版 | 名称 | 高度 | 映射依赖 |
|---|---|---|---|
| A1 | 头像徽章（26px 进标题行） | 112px（零增长） | 无 |
| A2 | 剪影水印（10% 透明度压底） | 98px（零增长） | 无 |
| A3 | 剪影面板 · 按总血量染色 | 185px | 无 |
| A4 | 头像 + 剪影面板 | 199px | 无 |
| A5 | 剪影面板 · 游戏原色 | 185px | 无（但只是装饰） |
| A7 | **剪影面板 · 部位血量直接染色** | 210px | **需映射表** |
| A6 | 满血对比（A4 同款） | 199px | 无 |

### 10.5 映射问题：比想象的小

**已证实**：色号 0（红）在 **78/78** 只怪里都出现，质心永远偏右（mean 0.748, sd 0.134）
→ 所有剪影头朝右、红色 = 头部。

**未在数据里**：色号 1–7 ↔ 部位名。不是固定顺序（嗟怨怨虎龙逐色拆层人工确认：
红=头部、紫=尾巴、橙=背部鬃毛、青=后脚、绿=躯干、黄/蓝=左右臂）。

**好消息**：语义侧已经现成 ——
`HunterPie.Core/Game/Data/Definitions/MonsterPartDefinition.cs` 里每个部位
**已带 `PartGroupType Group`**（Head / Horn / Neck / Body / Wings / Arms / Legs / Tail / Special / Misc）。

→ 映射不是「271 个部位名 → 7 个色块」，而是 **「78 × 7 个色块 → 10 个 PartGroupType」**，
一张小表即可收口。

### 10.6 修正：样例数据是假的

`MonsterHudViewModel.CreateSampleScornedMagnamalo()` 用的部位是
**头部 / 前肢 / 腹部 / 尾巴 / 背鳍**，但嗟怨震天怨虎龙的**真实部位表**是
**头部 / 躯干 / 右臂 / 左臂 / 背部 / 尾巴 / 后脚**（7 个）。

且 `PartNameSanitizer.cs` 有 **271 条**词条，且**状态相关**
（`Head (Ice)` / `Head (Mud)` / `Golden Left Arm` / `Front Right Arm (Oil)` …）。

**此前所有设计稿都是拿那份假数据验证的，需修正。**

### 10.7 版权

`F:/rise-overlay` 仓库目前**一张游戏图都没有**（唯一 `qurio_mask.png` 是 256×256 噪点遮罩）。
Capcom 素材是否随 GitHub Release 分发，需老大拍板。预览页**未复制任何图片进仓库**，
全部 `file:///F:/mhrise-app/...` 外链。

### 10.8 实测

83/83 图片加载成功 · 78 只头像网格完整 · 拆层 7 masks · 7/7 卡片 300px、横向溢出 0、
文档溢出 0、控制台 0 错误。

---

## 十一、怪物图标落地：放在名称前面（**已实施：2 个 XAML + 1 个工程文件 + 1 个显示字符串**）

第十章把「怪物图块」判了死刑（老大 2026-09-28 拍板：*「算了 不要怪物图块了 保持原本的线条吧。
你把怪物的icon放在怪物名称前面」*）。剪影/部位染色那一整套方向作废，只保留**头像图标放名字前面**这一条。

交付页：`docs/superpowers/previews/rise-monster-icon.html`

### 11.1 数据链：不需要怪物 ID

一开始看起来是死路 —— `MonsterHudDto` 只有 `string Name`，没有 ID：

```csharp
public sealed record MonsterHudDto(string Name, double HealthCurrent, ...);   // OverlayDtos.cs:42
```

但顺着 `Name` 的来源往下追，它是**确定的**：

```
monsters-overlay.json  { id: "monster_089_05", title: "嗟怨震天怨虎龙" }
        │
        ├─ RiseMonsterHudController.ResolveStatic()  :492  _staticStore.FindByTitle(monster.Name)
        └─ MonsterHudMapper.BuildFromStatic()        :33   Name: monster.Title   ← 逐字就是 dto.Title
```

于是：**`Name` 本身就是可用键**。112 个非空 `title` **零重名**，键唯一。

### 11.2 文件名映射：id 换个前缀

```
monster_089_05        → em089_05_icon.png
small-monster_003_00  → ems003_00_icon.png
```

核对结果：**大型怪 79/79 零缺失**；小型怪 35/37（缺的 2 只 `title` 本来就是空串）。
4 条空 `title`（`monster_131_00` / `small-monster_003_05` / `_051_05` / `_091_05`）直接跳过。

### 11.3 实现：纯 DataTrigger 查表

新增 `RiseOverlay.UI/Resources/MonsterIcons.xaml`（625 行）：

```xml
<sys:Double x:Key="Hud.MonsterIconSize">20</sys:Double>
<BitmapImage x:Key="Mon.em089_05" UriSource="pack://application:,,,/RiseOverlay.UI;component/Resources/Monsters/em089_05.png" />
<Style x:Key="MonsterIconStyle" TargetType="Image">
    <Setter Property="Width"  Value="{StaticResource Hud.MonsterIconSize}" />
    <Setter Property="Height" Value="{StaticResource Hud.MonsterIconSize}" />
    <Setter Property="Visibility" Value="Collapsed" />       <!-- 未命中：不占宽 -->
    <Style.Triggers>
        <DataTrigger Binding="{Binding Name}" Value="嗟怨震天怨虎龙">
            <Setter Property="Source" Value="{StaticResource Mon.em089_05}" />
            <Setter Property="Visibility" Value="Visible" />
        </DataTrigger>
        ...
    </Style.Triggers>
</Style>
```

**118 条触发** = 112 条正式 + 6 条别名（霸主名静态库用 U+30FB「・」、HunterPie zh-cn 用 U+00B7「·」，
两种中点都挂，见 `MonsterStaticStore.NormalizeTitle` 的注释）。

视图侧（`MonsterHudView.xaml` 标题行 / `QuestBriefingView.xaml` 目标行，同一改法）：

```xml
<Grid Grid.Column="0" VerticalAlignment="Center">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />            <!-- 图标 -->
        <ColumnDefinition Width="*" MinWidth="0" />
    </Grid.ColumnDefinitions>
    <Image Grid.Column="0" Style="{StaticResource MonsterIconStyle}" />
    <TextBlock Grid.Column="1" Text="{Binding Name}" TextTrimming="CharacterEllipsis" ... />
</Grid>
```

图标列是 `Auto` → 命中不到时 `Collapsed`，**宽度归零，布局回退到加图标前的水平**。

### 11.4 踩坑：PNG 不会自动打包成资源

`<Resource Include="Resources\Monsters\*.png" />` **必须显式写进 `RiseOverlay.UI.csproj`**。
不写的话，WPF SDK 不会把 PNG 收进 `.g.resources`，`pack URI` 在**运行时**抛
`IOException: 找不到资源"resources/monsters/em001_00.png"` —— 而且**编译期毫无提示**。

（原先 csproj 里只有 `<Resource Include="Resources\rise-hp-icon.png" />` 这一条，
一度让人以为存在自动 glob。实测：没有。）

判断资源到底进没进程序集：看 dll 体积。加图标后 `RiseOverlay.UI.dll` 从 399 KB → **1.51 MB**。

### 11.5 验证工装：真实 WPF 离屏渲染

沙箱起不了 WPF overlay，但**可以**离屏渲染：新建临时工程（仓库外）引用 `RiseOverlay.UI`，
`new Application()` + `RiseThemeService.ApplyTheme("Classic")`，
`RenderTargetBitmap` 出图，同时遍历视觉树核对每个元素的实际边界。

两个必须踩到的点：

1. **必须 measure/arrange 两轮**。首轮 `ItemsControl` 还没实例化容器，`DesiredSize.Height` 会偏小
   （实测 189.1 vs 261.2，差 72px）。
2. **越界探针要认识 `ClipToBounds`**。血条扫光 `HpGloss`（`Width=46` + `TranslateTransform X=-46`）
   在 `HpTrack`（`ClipToBounds="True"`）内部，不裁剪地看会报假阳性 `L=-37 R=9`。

### 11.6 实测（真实静态库驱动，全部越界 0）

> **本节数据于 2026-09-28 晚全部重跑。** 初版用的是手写夹具，推荐属性/部位/徽章文案均有编造，
> 已作废。现在工装直接 `MonsterStaticStore.Load(monsters-overlay.json)` →
> `MonsterHudMapper.BuildFromStatic` → `MergeLive`，不手写任何一条数据。详见 11.15。

| 场景 | 名字 | 字数 | 推荐（真实） | 图标 | 名字可用 / 完整需要 | 面板高 |
|---|---|---|---|---|---|---|
| 01-scorned-magnamalo | 嗟怨震天怨虎龙 | 7 | Water+Thunder | em089_05 | 130.1 / 84px 完整 | 511.8px |
| 02-rathian | 雌火龙 | 3 | Dragon | em001_00 | 159.1 / 42px 完整 | 485.2px |
| 03-worst-case | 怪异克服天彗龙 | 7 | Fire+Water+Ice+Thunder | em086_08 | **63.1 / 84px 截断，缺 20.9px** | 581.2px |
| 03b 同场景**无图标** | 怪异克服天彗龙 | 7 | 同上 | — | 88.1 / 84px 完整（余 4.1px） | 581.2px |
| 07-five-chips | 甲虫 | 2 | 五个全推荐 | ems027_00 | 43.1 / 24px 完整 | 170.6px |
| 08-risen-shagaru | 怪异克服天廻龙 | 7 | **Dragon（仅 1 个）** | em072_08 | 150.1 / 84px 完整 | 426.6px |
| 04-fallback | Apex Rathalos | 13 | 无 | — | 213.1 / 77.8px 完整 | 132.5px |
| 05 别名 U+30FB | 霸主・雌火龙 | 6 | Dragon | em001_07 | 150.1 / 72px 完整 | 485.2px |
| 10 简报面板 | 3 个目标 | — | — | 3 visible | 怪异克服天彗龙 77/98px **截断**；其余完整 | 287.4px |

**名字字号 12px（Segoe UI SemiBold），一个汉字 = 12px**，7 字名完整需要 84px。
简报面板名字字号 14px，同样 7 字需要 98px。

**结论修正：常见情况零代价；最坏情况只影响 2 只怪（全库 112 只扫描结果见 11.12）。**

`03b` 一行是关键对照：**图标（20px + 5px 间距）恰好吃掉 25px，而缺口只有 20.9px** ——
图标几乎就是全部代价，但**单靠缩图标救不回来**（见 11.7）。

### 11.7 图标边长权衡

| 边长 | 名字可用（最坏情况） | 结果 | 面板高 |
|---|---|---|---|
| 16px | 67.1px | 仍缺 16.9px | 581.2px |
| **20px（当前默认）** | 63.1px | 仍缺 20.9px | 581.2px |
| 24px | 59.1px | 仍缺 24.9px | 583.2px |
| 28px | 55.1px | 仍缺 28.9px | 587.2px |
| 32px | 51.1px | 仍缺 32.9px | 591.2px |

原画是**全身水墨**而非头部特写，16px 下读作「一块有颜色的纹章」，24px 起才隐约看出是只怪。
边长每 +4px 吃掉名字 4px；**缩到 16px 也只拿回 4px，不足以救回截断**。


### 11.8 试过但否决：按头部裁剪

想法：用 `_parts_group.png` 里恒为头部（质心永远偏右，mean 0.748）的红色块定位，
把 `_icon.png` 裁到头部，小尺寸可读性会好很多。

**否决原因**：两张图**没有共同坐标系**。`_parts_group.png` 每只怪的画布尺寸与长宽比都不同
（`em001_00` 633×209 / `em089_05` 785×433 / `em007_00` 684×245 / `em037_00` 764×238），
而 `_icon.png` 统一 256×256 且是**独立的绘画构图**。做包围盒配准会逐怪错位，风险大于收益。

### 11.9 可复现

`Scripts/gen_monster_icons.py` 落在仓库里，一条命令重建 PNG + XAML，并清理孤儿文件：

```
python Scripts/gen_monster_icons.py          # 重出 PNG + XAML
python Scripts/gen_monster_icons.py --xaml   # 只重出 XAML
```

验证工装也留了档：`docs/superpowers/tools/hudshot/`（`Program.cs` + `hudshot.csproj`
+ `report-sample.txt`）。它是仓库外的临时工程，用的时候整个拷到 `$TEMP/hudshot/` 再跑
（避免污染被验证工程）。样例报告是 2026-09-28 真实静态库跑出的完整结果，
本页所有数字都出自它。

### 11.10 待裁决

1. **版权** —— 图标是 Capcom《怪物崛起》原素材。此前仓库**一张游戏图都没有**，
   现在打进去 112 张（1.04 MB）。本机自用没问题，**对外发 Release 要老大拍板**。
2. **那 2 只天彗龙的截断要不要修、怎么修** —— 真实数据下只有 2/112 只受影响
   （神秘红光天彗龙、怪异克服天彗龙），可行方案与实测见 11.12。
   **注意：已实施的「徽章缩短」对它们零收益**（它们徽章是「不可捕」，本来就短）。
3. **图标边长** 20px 是否合适（改 `Hud.MonsterIconSize` 一个数即可）。
4. **未实机验证** —— 沙箱起不了 WPF overlay，但渲染路径与实机一致。


### 11.11 顺带修正的旧问题

第十章 10.6 提到 `CreateSampleScornedMagnamalo()` 的部位数据是假的
（头部/前肢/腹部/尾巴/背鳍 vs 真实的头部/躯干/右臂/左臂/背部/尾巴/后脚）。
本轮验证工装**没有用**那份样例，改从静态库取嗟怨震天怨虎龙的**真实部位表（12 个）**灌 DTO。
**假样例数据本身仍未修正。**

### 11.12 宽度预算：名字到底被谁挤了（真实数据版）

老大追问「有什么办法可以让图标和名字一行」。**先纠正前提：图标和名字本来就是一行**
（标题行 Grid：图标列 `Width="Auto"` + 名字列 `Width="*"`，两个列定义并排，物理上不可能换行）。
真正的问题是「名字会不会被截断」，于是把标题行的宽度预算逐项量开。

#### 11.12.1 全库扫描：112 只怪里谁会被截断

对静态库每一条非空标题真实渲染一遍，读名字列的**实际可用宽度**，
再用 `FormattedText` 独立算文本的**完整宽度**作比对：

| 指标 | 结果 |
|---|---|
| 推荐数分布 | 1 个：89　2 个：13　3 个：1　4 个：8　5 个：1 |
| 名字字数分布 | 2 字：26　3 字：59　4 字：8　5 字：6　6 字：6　7 字：7 |
| **会被截断** | **2 / 112 只**：神秘红光天彗龙、怪异克服天彗龙（HUD 缺 20.9px，简报缺 21px） |

**两个面板都截断**：HUD 名字字号 12（7 字需要 84px，只分到 63.1px）；
任务简报名字字号 14（7 字需要 98px，只分到 77px）。同一批怪、同一根因。

推荐 ≥3 个的共 10 只，其中只有 **2 只是长名（7 字）**，且**都是 `capturable=false`**：

| 怪物 | 字数 | 推荐 | 徽章 |
|---|---|---|---|
| 甲虫 | 2 | 5 | 可捕 |
| 艾露猫 / 精灵鹿 / 蓝速龙 / 梅拉露 | 3 | 4 | 可捕 |
| 狸兽 / 雪鹿 | 2 | 4 | 可捕 |
| 硬甲龙 | 3 | 3 | 可捕 |
| **神秘红光天彗龙** | 7 | 4 | **不可捕** |
| **怪异克服天彗龙** | 7 | 4 | **不可捕** |

**关键组合洞察：全库 7 字名的怪只有 7 只，其中 6 只 `capturable=false`。**
「7 字名 + 可捕徽章」这个组合**几乎不存在** —— 唯一可捕的 7 字名怪（嗟怨震天怨虎龙）
只有 2 推荐，根本不缺宽度。这直接决定了下面徽章改动的价值。

#### 11.12.2 标题行消耗分解（怪异克服天彗龙）

| 消耗项 | 实测占宽 | 说明 |
|---|---|---|
| 图标（20px + 5px 间距） | 25px | 本次新增 |
| 徽章「不可捕」（3 字 @ 9px） | 27px | **真实最坏情况下是这个，不是「可捕」** |
| **4 个属性胶囊** | **116px** | 每个 29px（Border 26px + 间距 3px）——**真正的大头** |
| 血量百分比 | ≈34px | 不可压缩 |
| 名字（7 字 @ 12px） | 84px | 只分到 63.1px |

#### 11.12.3 变体实测（同一只怪，只改一处）

| 变体 | 名字可用 | 结果 | 代价 |
|---|---|---|---|
| `b0` 现状 | 63.1px | **截断，缺 20.9px** | — |
| `b1` 图标整列隐藏 | 88.1px | 完整，余 4.1px（太紧） | 丢掉图标 |
| `b2` 图标 20 → 16px | 67.1px | 仍缺 16.9px | 图标更糊 |
| `b3` 徽章压到 1 字「否」 | 81.1px | 仍缺 2.9px | 文案变哑谜 |
| `b4` 胶囊整列让位（挪第二行） | **183.1px** | 完整，余 99.1px | 面板高约 +30px |
| `b5` 图标 16px + 胶囊让位 | 187.1px | 完整，余 103.1px | 同上 + 图标小 |

#### 11.12.4 胶囊内部还有多少可榨

胶囊探针直接读视觉树：

| 项 | 实测 |
|---|---|
| 胶囊列 | 116px / 4 个 → 均摊 29px（Border 26px + 间距 3px） |
| 单个 Border | 26px，`Padding=0`，内文字 12px → **左右各 7px 留白** |
| padding 压到各 4px | 单个 20px → 省 24px → 名字 **87.1px（够，余 3.1px）** |
| padding 压到各 2px | 单个 16px → 省 40px → 名字 **103.1px（余 19.1px）** |

**这是性价比最高的一条路**：不需要挪行、不需要动图标，把胶囊左右留白从 7px 压到 4px 就够。
可以只对「胶囊数 ≥ 4」生效，其余 102 只怪不受影响。

#### 11.12.5 徽章缩短：省了 36px，但一只怪都没救回来

徽章文案探针（字号 9px）：`可捕` 18px（88 只）/ `当前任务可捕` 54px / `不可捕` 27px（24 只）。

| 变体（嗟怨震天怨虎龙） | 名字可用 | 结果 |
|---|---|---|
| `c0` 现状（徽章「可捕」18px） | 130.1px | 完整，余 46.1px |
| `c1` 徽章还原「当前任务可捕」54px | 94.1px | **完整，余 10.1px** |

实施（本次唯一碰到的 `.cs`，只改一个显示字符串，未触碰判断逻辑）：

```csharp
// RiseOverlay.UI/ViewModels/MonsterHudViewModel.cs
public string CaptureBadgeText => _captureState switch
{
    CaptureDisplayState.Capturable => "可捕",          // 原为 "当前任务可捕"
    CaptureDisplayState.SpeciesUncapturable => "不可捕",
    CaptureDisplayState.Anomaly => "怪异化",
    _ => "不可捕",
};
```

**结论（推翻初版说法）：省 36px，但没有任何一只怪因此从「截断」变「完整」。**

- 唯一可捕的 7 字名怪用**旧文案也不截断**（余 10.1px）——缩短只是把余量从 10.1px 抬到 46.1px。
- 全库真正会截断的 2 只怪，徽章都是「**不可捕**」（3 字），缩短对它们**零收益**。

**这是余量保险，不是截断修复。** 保留无害（信息由面板上方「可捕获」横幅兜底），
但若目标是解决那 2 只天彗龙，得从 11.12.3 / 11.12.4 里选方案。

### 11.13 工装能力：宽度归因 + 全库扫描

**宽度归因（`BudgetStudy` / `RunVariants`）**：渲染真实控件后，用 `VisualTreeHelper` 定位并
**动态改写**元素（`SetText` / `SetIconSize` / `HideChips` / `HideIcon`），再跑两轮 measure/arrange，
量目标元素实宽。这是隔离「哪个元素吃掉了多少宽度」的通用手法，比估算准得多。

**全库扫描（`FullScan`）**：对静态库每一条非空标题真实渲染一遍，输出截断清单、
余量排行、推荐数/字数分布、徽章与胶囊的宽度统计。**这是把「最坏情况」从猜测变成穷举的关键** ——
初版只测了 1 个手写的最坏样本，真实扫描才知道全库只有 2 只受影响。

**判据必须用 `FormattedText`，不能用 `DesiredSize.Width`**（见 11.15 第 2 条）。

### 11.14 交付页的一个坑：占位符撞车

交付页生成脚本里 `<img src="__SIZES__">` 与表格占位符 `__SIZES__` **同名**，
先替换图片 base64 后，表格占位符也被填成 base64 —— 一长串无空格的 base64 当文本渲染，
把页面撑到 **2,619,985px 宽**。

教训：**多占位符模板必须用唯一名字**（表格改 `__SIZESTABLE__`），
并且生成后一定要用无头 Chrome 验 `scrollWidth - innerWidth`。
本次靠这道检查抓到，否则肉眼完全看不出（页面看起来只是「有点怪」）。

### 11.15 事故记录：测试夹具造假，结论全错

**老大一句「怪异克服天廻龙 + 4 推荐，这个是真实数据吗？我记得这个推荐属性是龙」直接掀了桌子。**

#### 错在哪

初版工装用手写夹具造 `MonsterHudDto`，编造了 3 类数据：

| 项 | 我写的（错） | 真实（静态库） |
|---|---|---|
| 怪异克服天廻龙 推荐属性 | fire + water + ice + thunder（4 个） | **dragon（1 个）** |
| 嗟怨震天怨虎龙 推荐属性 | fire（1 个） | water + thunder（2 个） |
| 嗟怨震天怨虎龙 部位 | 头部/前肢/腹部/尾巴/背鳍（7 个） | 12 个（头部/躯干/右臂/左臂/背部/尾巴/后脚 …） |
| 「最坏情况」样本 | 怪异克服天廻龙 | **怪异克服天彗龙 / 神秘红光天彗龙** |
| 「最坏情况」徽章文案 | 「当前任务可捕」 | **「不可捕」**（该怪 `capturable=false`，根本不进 Capturable 分支） |

推荐属性的真实算法是 `ElementRecommend.FromHitzones`：**只取等于最大值的属性**。
怪异克服天廻龙聚合后 fire 25 / water 0 / ice 5 / thunder 15 / **dragon 30** → 只剩 dragon。老大完全正确。

#### 三个连带 bug

1. **改了一个不存在的文本** —— 预算实验里 `SetText(v, "可捕", "当前任务可捕")` 在真实画面上
   找不到目标，**静默失败**，于是「改前/改后」两张图完全一样，却被当成有效对照读出了「省 66px」的结论。
   修法：变体实验加「需要的宽度 vs 拿到的宽度」判决，让静默失败暴露成「截断」而不是「没变化」。
2. **判据用了会被夹紧的量** —— 拿 `TextBlock.DesiredSize.Width` 当「文本完整宽度」，
   它会**被父级列宽夹紧**：同一个 7 字名在有图标时报 63.8px、无图标时报 90px，自相矛盾。
   换成 `FormattedText`（不受布局约束）后稳定报 **84px**。
3. **最坏样本选错** —— 真正的最坏是「7 字名 + 4 推荐 + 不可捕徽章」，
   而徽章缩短这一改动对这类怪**零收益**（见 11.12.5）。初版宣称的「缩短徽章 = 缩图标效果的 9 倍」
   在真实数据下不成立 —— 那是拿一个不存在的场景算出来的。

#### 教训

**凡是要拿数据下结论的工装，夹具必须来自真实数据源。** 手写夹具不是「简化」，
是把结论建在沙子上 —— 而且它**不会报错**，只会安静地给出一个看起来很合理的错数。


---

## 十二、部位行改造：4 个方案（⛔ 老大已否决，2026-09-28 19:39）

> **结论：不做。** 老大原话：「算了算了 改的撤掉吧。就保留那个 icon 放名字前面的改动 其它不要了」
>
> 本章只作为**决策记录**保留 —— 记下「为什么这块没动」以及诊断数据，
> 免得以后有人再提一遍同样的方案。
> 交付页 `rise-parts-redesign.html` 已随否决一起删除。
>
> 工装：`docs/superpowers/tools/hudshot/`（`Proposals.cs` 是本次新增的提案模块）。
> 全部数字来自真实静态库（112 只 / 912 个部位行）+ 真实 WPF 控件离屏渲染，
> 可用该工装重跑复现。

### 12.1 问题诊断：胶囊的宽度是白给的

| 口径 | 行数 | 占比 |
|---|---|---|
| 弱点与**上一行**完全相同的行 | 526 / 912 | **57.7%** |
| 弱点与**怪物整体推荐**相同的行 | 625 / 912 | **68.5%** |
| **空胶囊**（该部位无任何弱点） | 129 / 912 | **14.1%** |

弱点胶囊占了每行约 1/3 宽度，换回来的信息有近六成是重复的。

**部位数分布**：平均 8.1 个 / 怪，最多 16 个。12 部位是最常见的一档（13 只），
13-16 部位的怪合计 27 只 —— 最坏情况才是设计目标。

**去重后的弱点组合数**：1 组 37 只 / 2 组 42 只 / 3 组 24 只 / 4 组 8 只 / 5 组 1 只。
**70.5% 的怪最多只有 2 种组合** → 「按弱点分组」不会把面板切碎，最坏 5 个组头。

**部位名字数**：1 字 95 行、2 字 616 行（67.5%）、3 字 124 行 …… 最长 20 字 1 行。
78% ≤ 2 字，所以名字列「看起来 70px 够用」；但长尾很长，
冰牙龙的 `肉质部位05　左右前肢` 需要 102px，现状只给 70px → **缺 32.3px**。

**现状最坏情况**：冰牙龙 16 部位 → 面板 **648.7px**，**9/16 名字被截断**。

### 12.2 全库实测对比

| 变体 | 部位块均高 | 16 部位时 | 相对现状 | 名字截断 |
|---|---|---|---|---|
| p1-current（基线） | 268.1 | 648.7px | — | 12 / 910 |
| p2-barled 条主导 | 255.9 | 604.4px | −4.6% | **1 / 910** |
| p3-grsingle 分组·组内单列 | 270.3 | 609.4px | +0.8% | **1 / 910** |
| **p4-grouped 分组·组内双列** ★ | **212.9** | **492.7px** | **−20.6%** | **1 / 910** |
| p5-twocol 纯双列 | **158.4** | **394.0px** | **−40.9%** | 12 / 910 |

「名字截断」= 该变体下被 `TextTrimming` 切掉的行数 / 全库 912 行。
判据用 `FormattedText` 独立测量，不是被列宽夹紧的 `DesiredSize.Width`。

**推荐 p4**：同时赢了高度（−20.6%）、名字截断（12→1）、条长（118→135px），
且信息零丢失（状态轨、状态文字、硬直条、部位血条、剩余值全在）。

### 12.3 两个被数据否掉的方案（诚实记录）

**「瘦身」名字列 70→48px —— 作废。** 全库截断从 12/910 推到 **34/910**，是负优化。

**「条主导 v1」弱点做成独立色块列 —— 作废。**
5 格色块组（5×5px + 4×2px 间距）= **39px**，条反而从 118px **缩到 112px**，
比现状还短 —— 设计意图自杀。v2 把弱点改成「压在轨道正上方的 5 等分底纹带」，
占 0 额外宽度，条才真正涨到 151px。

**规律：名字列只要是写死的 `GridLength(像素)`，截断数几乎必然爆炸。**
唯一安全的是 `1*`（吃剩余宽度）或 `Auto` + 给 `TextBlock` 设 `MaxWidth`。
实测：p3/p4 把名字列从写死改成 `Auto + MaxWidth 110` 后，截断从 22 降到 1。

### 12.4 实现要点（`Proposals.cs`）

- 五版全部用**真实 `PartRowViewModel`** + 真实主题画刷 + 真实 `ElementChip` 构建，
  再 `RenderTargetBitmap` 出图 —— 不是 HTML 重画。
- 现状基线直接克隆 `MonsterHudView` 里部位 `ItemsControl` 的**隐式** `DataTemplate`
  （`ic.ItemTemplate` 为 null，必须用 `new DataTemplateKey(typeof(PartRowViewModel))` 从资源里取），
  保证与实机一致。
- 双列卡片结构（两行，信息零丢失）：
  ```
  行0 = 状态轨(跨两行) | 名字(*) | [弱点色块组] | 状态 2 字
  行1 =                  | 硬直条 2px + 部位血条 9px（剩余值内嵌）
  ```
  条跨满整张卡 **135px**，比现状单列的 118px 还长。
- `DualRails(p, gap, hpHeight, embedNumber)`：血条可加高并把数字内嵌，
  双列/单列两种形态复用同一函数。
- 组头按组大小降序（位置稳定，不随血量跳动）。

### 12.5 当时提出的 4 个待裁决点（已随否决作废）

1. ~~组头排序：按组大小降序 vs 按组内剩余血量降序~~
2. ~~超长部位名（20 字 1 行 / 11 字 1 行）怎么处理~~
3. ~~组内奇数成员的半格空白怎么处理~~
4. ~~是否同时提供「单列模式」开关~~

### 12.6 未验证

实机观感、分组重排对肌肉记忆的影响、部位数变化（断尾）时的跳动、20 字部位名的专门处理、帧率。

### 12.7 方法论沉淀

本次新增两条工装纪律，已写进 `verify-wpf-xaml-offscreen` 技能：

- **纪律 4：布局变体必须报全库文本截断数。** 不报就会把负优化当优化交出去。
- **纪律 5：每加一个横向装饰件，都要重算主内容的实际宽度。**
  装饰件占的宽度是从主内容里扣的，别只看它自己好不好看。
