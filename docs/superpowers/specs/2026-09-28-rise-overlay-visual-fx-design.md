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
