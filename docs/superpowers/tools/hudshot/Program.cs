using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RiseOverlay.Data;
using RiseOverlay.Domain;
using RiseOverlay.UI.Themes;
using RiseOverlay.UI.ViewModels;
using RiseOverlay.UI.Views;

namespace HudShot;

/// <summary>
/// Rise Overlay HUD/简报面板的离屏渲染验证工装。
///
/// 数据全部来自真实静态库 monsters-overlay.json + 真实 Mapper，不手写任何推荐属性或部位。
///
/// 用法（留档副本在 docs/superpowers/tools/hudshot/，实际跑要放到仓库外，避免污染被验证工程）：
///   1. 把留档目录整个拷到 $TEMP/hudshot/
///   2. cd $TEMP/hudshot
///   3. "/c/Program Files/dotnet/dotnet.exe" build -c Debug
///   4. "/c/Program Files/dotnet/dotnet.exe" run -c Debug --no-build
///   输出：$TEMP/hudshot/out/*.png + report.txt
///   样例报告见同目录 report-sample.txt（2026-09-28 真实静态库跑出的完整结果）。
///
/// 宽度判据说明（重要）：
///   TextBlock.DesiredSize.Width 会被父级列宽夹紧，绝不能拿来当「文本完整宽度」。
///   必须用 FormattedText 独立测量（不受布局约束），再与 TextBlock.ActualWidth 比较。
/// </summary>
internal static class Program
{
    const string OutDir = @"C:\Users\Administrator\AppData\Local\Temp\hudshot\out";
    const string StaticJson = @"F:\rise-overlay\RiseOverlay.Data\static\monsters-overlay.json";
    const double PanelWidth = 300;

    static MonsterStaticStore? _store;

    private sealed record Row(
        string Title, int Len, int Rec, string Badge,
        double Need, double Got, double Chips, double BadgeW, bool Capturable);

    [STAThread]
    static void Main(string[] args)
    {
        var log = new StringBuilder();
        Directory.CreateDirectory(OutDir);
        string theme = args.Length > 0 ? args[0] : RiseThemeIds.Classic;

        try
        {
            _store = MonsterStaticStore.Load(StaticJson);
            var app = new Application();
            RiseThemeService.ApplyTheme(theme);

            log.AppendLine("theme = " + theme);
            log.AppendLine("静态库 = " + StaticJson + "   （" + _store.All.Count + " 条）");
            log.AppendLine();

            foreach (var s in Scenarios())
                Render(s.File, s.Dto, s.IconPx, s.HideIcon, s.Note, log);

            log.AppendLine();
            log.AppendLine("========== 图标边长研究（真实最坏情况） ==========");
            var worst = Scenarios().First(x => x.File == "03-worst-case").Dto;
            foreach (int px in new[] { 16, 20, 24, 28, 32 })
                Render("size-" + px.ToString("00"), worst, px, false, "图标 " + px + "px", log);

            log.AppendLine();
            log.AppendLine("========== 任务简报面板 ==========");
            RenderBriefing(log);

            log.AppendLine();
            log.AppendLine("========== 宽度预算研究 ==========");
            BudgetStudy(worst, log);

            FullScan(log);

            log.AppendLine();
            log.AppendLine("========== 部位行结构解剖 ==========");
            PartRowDetail(RealHud("monster_086_08", 12), log);
            PartRowDetail(RealHud("monster_089_05", 60), log);

            log.AppendLine();
            log.AppendLine("========== 部位行改造提案（真实 WPF 渲染） ==========");
            RenderProposals(log);
        }
        catch (Exception ex)
        {
            log.AppendLine("EXCEPTION: " + ex);
        }

        File.WriteAllText(Path.Combine(OutDir, "report.txt"), log.ToString(), Encoding.UTF8);
        Console.WriteLine(log.ToString());
    }

    // ---------------------------------------------------------------- 真实数据

    /// <summary>静态库 → MonsterStaticMapped（真实推荐属性 / 真实部位）。</summary>
    static MonsterStaticMapped RealStatic(string id)
    {
        var dto = _store!.FindById(id) ?? throw new InvalidOperationException("静态库无此 id: " + id);
        return MonsterHudMapper.BuildFromStatic(MonsterStaticAdapter.ToSnapshot(dto));
    }

    /// <summary>静态库 + 合成战斗帧 → MonsterHudDto。推荐属性与部位全部来自静态库。</summary>
    static MonsterHudDto RealHud(string id, double hpPercent, double? threshold = 25)
    {
        var st = RealStatic(id);
        return MonsterHudMapper.MergeLive(st, SynthLive(st, hpPercent, threshold));
    }

    static LiveMonsterSnapshot SynthLive(MonsterStaticMapped st, double hpPercent, double? threshold)
    {
        var parts = st.Parts.Select((p, i) => new LivePartSnapshot(
            Name: p.Name,
            CurrentHp: i % 3 == 1 ? 0 : Math.Max(0, 3200 - i * 260),
            MaxHp: 4000,
            IsBroken: i == 2,
            IsSeverable: p.IsSeverable,
            Flinch: 1200,
            MaxFlinch: 3000)).ToArray();

        return new LiveMonsterSnapshot(
            HealthCurrent: Math.Round(40000 * hpPercent / 100.0),
            HealthMax: 40000,
            Parts: parts,
            Ailments: new[]
            {
                new LiveAilmentSnapshot("poison", "毒", 62, true),
                new LiveAilmentSnapshot("blast", "爆破", 31, true),
            },
            Status: new StatusLineModel(null, null, false, null, null, null),
            QuestAllowsCapture: true,
            CaptureThresholdPercent: threshold);
    }

    /// <summary>静态库未命中时的真实回退路径：Name 是 HunterPie 原始名（英文），图标表必然不命中。</summary>
    static MonsterHudDto FallbackHud(string rawName, double hpPercent)
    {
        var fb = MonsterHudMapper.CreateFallbackStatic(rawName, isCapturable: true);
        return MonsterHudMapper.MergeLive(fb, SynthLive(fb, hpPercent, 25));
    }

    static IEnumerable<(string File, MonsterHudDto Dto, int IconPx, bool HideIcon, string Note)> Scenarios()
    {
        // 真实最常见：7 字名 + 2 推荐（静态库聚合后 water+thunder 并列最大）
        yield return ("01-scorned-magnamalo", RealHud("monster_089_05", 67), 20, false,
            "嗟怨震天怨虎龙 · 7字名 · 推荐 2 个 · 可捕");

        // 短名 + 1 推荐
        yield return ("02-rathian", RealHud("monster_001_00", 88), 20, false,
            "雌火龙 · 3字名 · 推荐 1 个");

        // 真实最坏：7 字名 + 4 推荐（fire+water+ice+thunder 并列最大）+ 不可捕
        yield return ("03-worst-case", RealHud("monster_086_08", 12), 20, false,
            "怪异克服天彗龙 · 7字名 · 推荐 4 个 ← 真实最坏情况");

        // 同场景关掉图标 → 隔离「图标吃掉多少名字」
        yield return ("03b-worst-case-noicon", RealHud("monster_086_08", 12), 20, true,
            "同上，图标隐藏（A/B 对照）");

        // 用户点名的怪：推荐属性应当只有 dragon 一个
        yield return ("08-risen-shagaru", RealHud("monster_072_08", 40), 20, false,
            "怪异克服天廻龙 · 用户质疑样本 · 推荐应仅 Dragon");

        // 推荐数最多：5 个（小型怪）
        yield return ("07-five-chips", RealHud("small-monster_027_00", 45), 20, false,
            "甲虫 · 推荐 5 个 ← 全库最多");

        // 静态库未命中 → 走 CreateFallbackStatic，Name 是 HunterPie 原始名（英文）→ 图标收起
        yield return ("04-fallback-no-icon", FallbackHud("Apex Rathalos", 55), 20, false,
            "静态库未命中（英文名）→ 图标应收起");

        // 别名：霸主名静态库用 U+30FB、HunterPie zh-cn 用 U+00B7
        yield return ("05-alias-middot", RealHud("monster_001_07", 80), 20, false,
            "霸主・雌火龙（U+30FB 静态库原名）");
    }

    // ---------------------------------------------------------------- 渲染

    static (Border Root, MonsterHudView View) BuildPanel(MonsterHudDto dto)
    {
        var vm = new MonsterHudViewModel();
        vm.ApplyDto(dto);
        var view = new MonsterHudView { DataContext = vm };
        var host = new Grid { Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x14)), Margin = new Thickness(14) };
        host.Children.Add(view);
        var root = new Border { Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x14)), Child = host };
        Layout(root);
        return (root, view);
    }

    /// <summary>必须跑两轮：首轮 ItemsControl 未实例化容器，高度会偏小。</summary>
    static void Layout(Border root)
    {
        for (int p = 0; p < 2; p++)
        {
            root.Measure(new Size(PanelWidth + 28, double.PositiveInfinity));
            root.Arrange(new Rect(0, 0, PanelWidth + 28, root.DesiredSize.Height));
            root.UpdateLayout();
        }
    }

    static void Render(string file, MonsterHudDto dto, int iconPx, bool hideIcon, string note, StringBuilder log)
    {
        var (root, view) = BuildPanel(dto);

        var icon = FindImages(view).FirstOrDefault();
        if (icon is not null && (iconPx > 0 || hideIcon))
        {
            if (hideIcon) icon.Visibility = Visibility.Collapsed;
            else { icon.Width = iconPx; icon.Height = iconPx; }
            Layout(root);
        }

        double h = root.DesiredSize.Height;
        var overflow = new List<string>();
        Probe(root, view, overflow, clipped: false);
        var nameTb = FindByText(view, dto.Name);
        var vm = (MonsterHudViewModel)view.DataContext;
        var badgeTb = FindByText(view, vm.CaptureBadgeText);
        var chips = FindChips(view);

        Save(root, h, file);

        log.AppendLine("--- " + file + "   [" + note + "]");
        log.AppendLine("    Name = " + dto.Name + "  (" + dto.Name.Length + " 字)   " +
                       "推荐 = " + (dto.Recommended.Count == 0 ? "无" : string.Join("+", dto.Recommended)) +
                       "   部位 = " + dto.Parts.Count + " 个");
        log.AppendLine("    " + Verdict(nameTb) +
                       "   徽章[" + vm.CaptureBadgeText + "]=" + Px(badgeTb) +
                       "   胶囊=" + (chips is null ? "?" : chips.Items.Count + "个/" + chips.ActualWidth.ToString("0.#") + "px") +
                       "   图标=" + (icon is null ? "?" : icon.Visibility + "/" + icon.ActualWidth.ToString("0.#") + "px") +
                       "   面板高 = " + h.ToString("0.#") + "px");
        log.AppendLine("    图标资源 = " + (icon is null ? "(none)" : Last(icon.Source)) +
                       "   名字字号 = " + (nameTb?.FontSize.ToString("0.#") ?? "?") +
                       "   名字字体 = " + (nameTb?.FontFamily.Source ?? "?") +
                       "   名字字重 = " + (nameTb?.FontWeight.ToString() ?? "?"));
        log.AppendLine("    越界 = " + (overflow.Count == 0 ? "0" : string.Join(" | ", overflow)));
    }

    static void RenderBriefing(StringBuilder log)
    {
        var targets = new[]
        {
            MonsterHudMapper.ToBriefingTarget(RealStatic("monster_086_08")),   // 7字 + 4推荐
            MonsterHudMapper.ToBriefingTarget(RealStatic("monster_089_05")),   // 7字 + 2推荐
            MonsterHudMapper.ToBriefingTarget(RealStatic("monster_001_00")),   // 3字
        };
        var vm = new QuestBriefingViewModel();
        vm.ApplyDto(new QuestBriefingDto(targets));
        var view = new QuestBriefingView { DataContext = vm };

        var host = new Grid { Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x14)), Margin = new Thickness(14) };
        host.Children.Add(view);
        var root = new Border { Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x14)), Child = host };
        Layout(root);

        double h = root.DesiredSize.Height;
        var overflow = new List<string>();
        Probe(root, view, overflow, clipped: false);
        Save(root, h, "10-briefing");

        log.AppendLine("--- 10-briefing   面板高 = " + h.ToString("0.#") + "px");
        log.AppendLine("    图标 = " + string.Join(" | ", FindImages(view).Select(i => i.Visibility + ":" + Last(i.Source))));
        log.AppendLine("    越界 = " + (overflow.Count == 0 ? "0" : string.Join(" | ", overflow)));
        foreach (var n in new[] { "怪异克服天彗龙", "嗟怨震天怨虎龙", "雌火龙" })
            log.AppendLine("    简报名字 " + n.PadRight(9) + Verdict(FindByText(view, n)));
    }

    /// <summary>
    /// 宽度预算归因。oldText 必须用 VM 实际给出的徽章文本 —— 傀异化/霸主怪的
    /// CaptureState 是 SpeciesUncapturable（「不可捕」），不是「可捕」。
    /// </summary>
    static void BudgetStudy(MonsterHudDto worst, StringBuilder log)
    {
        // ---- A. 真实最坏情况（怪异克服天彗龙：7字名 + 4推荐 + 不可捕徽章）----
        log.AppendLine("【A】真实最坏情况 = " + worst.Name + "（推荐 " + worst.Recommended.Count +
                       " 个，徽章「不可捕」）");
        RunVariants(worst, new (string, string, Action<MonsterHudView>)[]
        {
            ("b0-baseline", "现状", _ => { }),
            ("b1-noicon", "图标整列隐藏", v => HideIcon(v)),
            ("b2-icon16", "图标 20 → 16px", v => SetIconSize(v, 16)),
            ("b3-nochips", "推荐胶囊整列隐藏（模拟挪到第二行）", v => HideChips(v)),
            ("b4-shortbadge", "徽章「不可捕」→「否」（压到极限）", v => SetText(v, "不可捕", "否")),
            ("b5-icon16-nochips", "图标 16px + 胶囊隐藏", v => { SetIconSize(v, 16); HideChips(v); }),
        }, log);

        ChipDetail(worst, log);

        // ---- B. 可捕的长名怪（嗟怨震天怨虎龙：7字名 + 2推荐 + 「可捕」徽章）----
        // hp 取 60（高于 25% 阈值）以免触发「可捕获」横幅，横幅会把标题行挤下去、污染对照。
        var capturable = RealHud("monster_089_05", 60);
        log.AppendLine();
        log.AppendLine("【B】可捕长名怪 = " + capturable.Name + "（推荐 " + capturable.Recommended.Count +
                       " 个，徽章「可捕」）");
        RunVariants(capturable, new (string, string, Action<MonsterHudView>)[]
        {
            ("c0-baseline", "现状（徽章已缩短为「可捕」）", _ => { }),
            ("c1-oldbadge", "对照：徽章还原成「当前任务可捕」", v => SetText(v, "可捕", "当前任务可捕")),
        }, log);

        BadgeDetail(log);
    }

    /// <summary>量徽章文案本身的宽度，把「缩短省了多少」钉死。</summary>
    static void BadgeDetail(StringBuilder log)
    {
        var (_, view) = BuildPanel(RealHud("monster_089_05", 60));
        var vm = (MonsterHudViewModel)view.DataContext;
        var tb = FindByText(view, vm.CaptureBadgeText);
        log.AppendLine();
        if (tb is null) { log.AppendLine("    徽章探针：未找到徽章 TextBlock"); return; }

        log.AppendLine("    徽章探针：字号 " + tb.FontSize.ToString("0.#") +
                       "  FontWeight " + tb.FontWeight +
                       "  FontFamily " + tb.FontFamily.Source);
        foreach (var s in new[] { "可捕", "当前任务可捕", "不可捕", "怪异化" })
            log.AppendLine("      [" + s.PadRight(6) + "] 文本宽 " + MeasureText(s, tb).ToString("0.#").PadLeft(6) + "px" +
                           (s == "可捕" ? "   ← 现文案" : s == "当前任务可捕" ? "   ← 原文案" : ""));
    }

    /// <summary>量胶囊内部 padding，判断「缩胶囊」还有多少余量可榨。</summary>
    static void ChipDetail(MonsterHudDto dto, StringBuilder log)
    {
        var (_, view) = BuildPanel(dto);
        var chips = FindChips(view);
        if (chips is null) { log.AppendLine("    胶囊探针：未找到胶囊列"); return; }

        var tb = FindTextBlocks(chips).FirstOrDefault(x => x.ActualWidth > 0);
        var bd = FindBorders(chips).FirstOrDefault(x => x.ActualWidth is > 10 and < 60);
        int n = chips.Items.Count;

        log.AppendLine();
        log.AppendLine("    胶囊探针：列宽 " + chips.ActualWidth.ToString("0.#") + "px / " + n + " 个" +
                       "（均摊 " + (chips.ActualWidth / n).ToString("0.#") + "px）");
        if (tb is not null)
            log.AppendLine("    胶囊内文字 [" + tb.Text + "] 实宽 " + tb.ActualWidth.ToString("0.#") +
                           "px  字号 " + tb.FontSize.ToString("0.#"));
        if (bd is not null)
            log.AppendLine("    胶囊 Border 实宽 " + bd.ActualWidth.ToString("0.#") + "px  Padding=" + bd.Padding);
        if (tb is not null && bd is not null)
        {
            double pad = bd.ActualWidth - tb.ActualWidth;
            double minChip = tb.ActualWidth + 4;      // 左右各 2px 的极限 padding
            double save = n * (bd.ActualWidth - minChip);
            log.AppendLine("    → 单个胶囊可榨 padding = " + pad.ToString("0.#") + "px；压到极限后单个 " +
                           minChip.ToString("0.#") + "px，共省 " + save.ToString("0.#") + "px");
            log.AppendLine("    → 名字可用宽度将从 63.1px 提升到约 " + (63.1 + save).ToString("0.#") +
                           "px（需要 84px）→ " + (63.1 + save >= 84 ? "够用" : "仍不够"));
        }
    }

    /// <summary>量一条部位行的每个元素：轨条 / 名字 / 状态 / 属性胶囊 / 血量数字。</summary>
    static void PartRowDetail(MonsterHudDto dto, StringBuilder log)
    {
        var (_, view) = BuildPanel(dto);
        log.AppendLine("--- " + dto.Name + "  共 " + dto.Parts.Count + " 个部位行");

        // 全部「以 ElementId 为项」的 ItemsControl：第 1 个是标题行的大胶囊，其后是各部位行的小胶囊
        var all = FindAllChips(view).ToList();
        log.AppendLine("    含属性胶囊的 ItemsControl 共 " + all.Count + " 个（1 个标题行 + " +
                       (all.Count - 1) + " 个部位行）");

        for (int i = 0; i < Math.Min(all.Count, 4); i++)
        {
            var ic = all[i];
            var chip = FindBorders(ic).FirstOrDefault(b => b.ActualWidth is > 6 and < 60);
            var lbl = FindTextBlocks(ic).FirstOrDefault(x => x.ActualWidth > 0);
            log.AppendLine("      [" + i + "] " + ic.Items.Count + " 个 / 列宽 " + ic.ActualWidth.ToString("0.#") +
                           "px  单胶囊 " + (chip?.ActualWidth.ToString("0.#") ?? "?") + "px" +
                           "  内字 " + (lbl?.ActualWidth.ToString("0.#") ?? "?") + "px@" +
                           (lbl?.FontSize.ToString("0.#") ?? "?") + "  高 " + ic.ActualHeight.ToString("0.#"));
        }

        // 逐元素解剖第 1 个部位行
        var first = dto.Parts.FirstOrDefault();
        if (first is null) return;
        var nameTb = FindByText(view, first.Name);
        log.AppendLine("    第 1 个部位 [" + first.Name + "] 弱点 = " +
                       (first.WeakElements.Count == 0 ? "无" : string.Join("+", first.WeakElements)));

        if (nameTb is not null)
        {
            // 名字列的实际分配宽 = 它自己的 ActualWidth；父 Grid 的列定义是 70/38/*/Auto
            var row = VisualTreeHelper.GetParent(nameTb) as FrameworkElement;   // 行内 Grid
            var outer = row is null ? null : VisualTreeHelper.GetParent(row) as FrameworkElement;
            log.AppendLine("      行内 Grid 宽 " + (row?.ActualWidth.ToString("0.#") ?? "?") +
                           "  高 " + (row?.ActualHeight.ToString("0.#") ?? "?"));
            log.AppendLine("      名字 TextBlock 实宽 " + nameTb.ActualWidth.ToString("0.#") +
                           "px / 文本 " + FullWidth(nameTb).ToString("0.#") + "px @" + nameTb.FontSize.ToString("0.#") +
                           "  高 " + nameTb.ActualHeight.ToString("0.#"));
            if (outer is not null)
                log.AppendLine("      行外层（含双轨）宽 " + outer.ActualWidth.ToString("0.#") +
                               "  高 " + outer.ActualHeight.ToString("0.#"));
        }

        // 部位行里每个元素的实际位置，看横向预算
        var seen = new HashSet<string>();
        var chips = all.Skip(1).FirstOrDefault();
        if (chips is not null)
        {
            var parent = VisualTreeHelper.GetParent(chips) as FrameworkElement;
            log.AppendLine("      胶囊所在 Grid 宽 " + (parent?.ActualWidth.ToString("0.#") ?? "?") +
                           "，胶囊起点 X = " + chips.TransformToAncestor(view).Transform(new Point(0, 0)).X.ToString("0.#") +
                           "，占 " + chips.ActualWidth.ToString("0.#") + "px");
        }
        log.AppendLine();
    }

    static IEnumerable<ItemsControl> FindAllChips(DependencyObject d)
    {
        if (d is ItemsControl ic && ic.Items.Count > 0 && ic.Items[0] is ElementId) yield return ic;
        int n = VisualTreeHelper.GetChildrenCount(d);
        for (int i = 0; i < n; i++)
            foreach (var x in FindAllChips(VisualTreeHelper.GetChild(d, i))) yield return x;
    }

    // ---------------------------------------------------------------- 提案渲染

    static readonly (string Id, string Name)[] PropSamples =
    {
        ("monster_086_08", "怪异克服天彗龙"),
        ("monster_089_05", "嗟怨震天怨虎龙"),
        ("monster_042_00", "冰牙龙"),
        ("monster_135_00", "冥渊龙"),
    };

    static void RenderProposals(StringBuilder log)
    {
        var variants = new (RowVariant V, string Tag, string Note)[]
        {
            (RowVariant.Current,       "p1-current",   "现状（真实模板，作基线）"),
            (RowVariant.BarLed,        "p2-barled",    "条主导：单行，弱点压成条顶底纹带，条 118→151px"),
            (RowVariant.GroupedSingle, "p3-grsingle",  "分组·组内单列：行数不变，条 118→151px，弱点去重"),
            (RowVariant.Grouped,       "p4-grouped",   "分组·组内双列：组头共享胶囊，行数减半  ★"),
            (RowVariant.TwoCol,        "p5-twocol",    "纯双列：每卡自带弱点色块，行数减半，高度最省"),
        };

        // 全库高度对比
        log.AppendLine("【全库面板高度对比】（顶栏固定 128px + 部位块高度）");
        log.AppendLine("    " + "变体".PadRight(14) + "部位块均高".PadRight(12) + "16部位时".PadRight(12) +
                       "最省/最费".PadRight(24) + "名字截断(全库/总行)");

        foreach (var (v, tag, note) in variants)
        {
            double sum = 0;
            int n = 0;
            double h16 = 0;
            double min = double.MaxValue, max = 0;
            int truncAll = 0, rowsAll = 0;
            foreach (var dto in _store!.All)
            {
                if (string.IsNullOrWhiteSpace(dto.Title)) continue;
                var hud = RealHud(dto.Id, 50);
                var vm = new MonsterHudViewModel();
                vm.ApplyDto(hud);
                var (block, hh) = RenderParts(vm, v, null, null);
                sum += hh; n++;
                if (hud.Parts.Count == 16 && h16 == 0) h16 = hh;
                min = Math.Min(min, hh); max = Math.Max(max, hh);
                var (t, tot, _, _) = CountTruncatedParts(block, vm.Parts);
                truncAll += t; rowsAll += tot;
            }
            log.AppendLine("    " + tag.PadRight(14) + (sum / n).ToString("0.#").PadRight(12) +
                           (h16 == 0 ? "-" : (128 + h16).ToString("0.#") + "px").PadRight(12) +
                           ((128 + min).ToString("0.#") + " / " + (128 + max).ToString("0.#") + "px").PadRight(24) +
                           truncAll + " / " + rowsAll);
        }
        log.AppendLine();

        // 各样本渲染
        foreach (var (id, name) in PropSamples)
        {
            var dto = RealHud(id, 50);
            var vm = new MonsterHudViewModel();
            vm.ApplyDto(dto);

            log.AppendLine("--- " + name + "（" + dto.Parts.Count + " 部位）");
            foreach (var (v, tag, note) in variants)
            {
                var (block, h) = RenderParts(vm, v, "prop-" + id + "-" + tag, name);
                var (t, tot, worst, worstName) = CountTruncatedParts(block, vm.Parts);
                log.AppendLine("    " + tag.PadRight(14) + note.PadRight(40) +
                               "部位块高 " + h.ToString("0.#").PadLeft(7) + "px   " +
                               "面板高约 " + (128 + h).ToString("0.#").PadLeft(7) + "px   " +
                               "名字截断 " + t + "/" + tot +
                               (t == 0 ? "" : "  最惨 " + worstName + " 缺 " + worst.ToString("0.#") + "px"));
                GC.KeepAlive(block);
            }
            log.AppendLine();
        }
    }

    static (FrameworkElement Block, double H) BuildParts(MonsterHudViewModel vm, RowVariant v)
    {
        if (v == RowVariant.Current)
        {
            // 用真实模板，保证与实机一致：从活的 MonsterHudView 里取出部位 ItemsControl 的 ItemTemplate
            var probe = new MonsterHudView { DataContext = vm };
            var host = new Border { Child = probe, Width = Proposals.PanelW, Padding = new Thickness(8, 6, 8, 6) };
            for (int p = 0; p < 2; p++)
            {
                host.Measure(new Size(Proposals.PanelW, double.PositiveInfinity));
                host.Arrange(new Rect(0, 0, Proposals.PanelW, host.DesiredSize.Height));
                host.UpdateLayout();
            }
            var ic = FindItemsControlOf(probe, typeof(PartRowViewModel));
            // 部位 ItemsControl 用的是「隐式模板」：ItemTemplate 为 null，靠 DataType 在资源里解析。
            // 所以要用 DataTemplateKey 显式取出来。
            var tpl = ic?.ItemTemplate
                      ?? probe.TryFindResource(new DataTemplateKey(typeof(PartRowViewModel))) as DataTemplate;
            if (tpl is null)
            {
                var dbg = new StringBuilder();
                dbg.AppendLine("vm.Parts.Count = " + vm.Parts.Count);
                DumpItems(probe, dbg, 0);
                throw new InvalidOperationException("未找到部位行的真实 DataTemplate\n" + dbg);
            }
            var clone = new ItemsControl { ItemsSource = vm.Parts, ItemTemplate = tpl, Width = Proposals.ContentW };
            return (clone, 0);
        }
        return (Proposals.Build(vm.Parts, v), 0);
    }

    static void DumpItems(DependencyObject d, StringBuilder sb, int depth)
    {
        if (d is ItemsControl ic)
            sb.AppendLine(new string(' ', depth * 2) + ic.GetType().Name + " Items=" + ic.Items.Count +
                          " first=" + (ic.Items.Count > 0 ? ic.Items[0].GetType().Name : "-") +
                          " tpl=" + (ic.ItemTemplate?.GetType().Name ?? "null") +
                          " vis=" + ic.Visibility + " w=" + ic.ActualWidth.ToString("0.#"));
        int n = VisualTreeHelper.GetChildrenCount(d);
        for (int i = 0; i < n; i++) DumpItems(VisualTreeHelper.GetChild(d, i), sb, depth + 1);
    }

    static ItemsControl? FindItemsControlOf(DependencyObject d, Type itemType)
    {
        if (d is ItemsControl ic && ic.Items.Count > 0 && itemType.IsInstanceOfType(ic.Items[0])) return ic;
        int n = VisualTreeHelper.GetChildrenCount(d);
        for (int i = 0; i < n; i++)
        {
            var hit = FindItemsControlOf(VisualTreeHelper.GetChild(d, i), itemType);
            if (hit is not null) return hit;
        }
        return null;
    }

    static double MeasureParts(MonsterHudViewModel vm, RowVariant v)
        => RenderParts(vm, v, null, null).H;

    static (FrameworkElement Block, double H) RenderParts(MonsterHudViewModel vm, RowVariant v, string? file, string? name)
    {
        var (block, _) = BuildParts(vm, v);
        var host = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x14)),
            Padding = new Thickness(8, 6, 8, 6),
            Width = Proposals.PanelW,
            Child = block,
        };
        for (int p = 0; p < 2; p++)
        {
            host.Measure(new Size(Proposals.PanelW, double.PositiveInfinity));
            host.Arrange(new Rect(0, 0, Proposals.PanelW, host.DesiredSize.Height));
            host.UpdateLayout();
        }
        double h = host.DesiredSize.Height;
        if (file is not null)
        {
            var wrap = new Border { Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x14)), Child = host, Padding = new Thickness(6) };
            wrap.Measure(new Size(Proposals.PanelW + 12, double.PositiveInfinity));
            wrap.Arrange(new Rect(0, 0, Proposals.PanelW + 12, wrap.DesiredSize.Height));
            wrap.UpdateLayout();
            Save(wrap, wrap.DesiredSize.Height, file);
        }
        GC.KeepAlive(name);
        return (block, h);
    }

    static IEnumerable<T> Walk<T>(DependencyObject d) where T : DependencyObject
    {
        if (d is T t) yield return t;
        int n = VisualTreeHelper.GetChildrenCount(d);
        for (int i = 0; i < n; i++)
            foreach (var x in Walk<T>(VisualTreeHelper.GetChild(d, i))) yield return x;
    }

    /// <summary>
    /// 数一数这个变体里有多少个部位名被截断、最惨的那个缺多少 px。
    /// 判据用 FormattedText（不受列宽夹紧），不是 DesiredSize。
    /// </summary>
    static (int Truncated, int Total, double WorstMissing, string WorstName) CountTruncatedParts(
        FrameworkElement block, IReadOnlyList<PartRowViewModel> parts)
    {
        var names = new HashSet<string>(parts.Select(p => p.Name));
        int trunc = 0, total = 0;
        double worst = 0;
        string worstName = "-";
        foreach (var tb in Walk<TextBlock>(block))
        {
            if (tb.Text is null || !names.Contains(tb.Text)) continue;
            total++;
            double need = MeasureText(tb.Text, tb);
            if (need > tb.ActualWidth + 0.5)
            {
                trunc++;
                double miss = need - tb.ActualWidth;
                if (miss > worst) { worst = miss; worstName = tb.Text; }
            }
        }
        return (trunc, total, worst, worstName);
    }

    static void RunVariants(MonsterHudDto dto, (string File, string Note, Action<MonsterHudView> Mutate)[] variants, StringBuilder log)    {
        foreach (var (file, note, mutate) in variants)
        {
            var (root, view) = BuildPanel(dto);
            mutate(view);
            Layout(root);

            var tb = FindByText(view, dto.Name);
            Save(root, root.DesiredSize.Height, file);
            log.AppendLine("    " + file.PadRight(19) + note.PadRight(36) + Verdict(tb));
        }
    }

    // ---------------------------------------------------------------- 全库扫描

    static void FullScan(StringBuilder log)
    {
        log.AppendLine();
        log.AppendLine("========== 全库扫描（" + _store!.All.Count + " 条静态库 / 真实推荐属性） ==========");

        var rows = new List<Row>();
        var recDist = new SortedDictionary<int, int>();
        var lenDist = new SortedDictionary<int, int>();
        int scanned = 0;

        foreach (var dto in _store.All)
        {
            if (string.IsNullOrWhiteSpace(dto.Title)) continue;

            var hud = RealHud(dto.Id, 50);
            var (_, view) = BuildPanel(hud);
            var vm = (MonsterHudViewModel)view.DataContext;
            var tb = FindByText(view, hud.Name);
            var badgeTb = FindByText(view, vm.CaptureBadgeText);
            var chips = FindChips(view);

            rows.Add(new Row(
                hud.Name, hud.Name.Length, hud.Recommended.Count, vm.CaptureBadgeText,
                tb is null ? 0 : FullWidth(tb), tb?.ActualWidth ?? 0,
                chips?.ActualWidth ?? 0, badgeTb?.ActualWidth ?? 0, dto.Capturable));

            recDist[hud.Recommended.Count] = recDist.GetValueOrDefault(hud.Recommended.Count) + 1;
            lenDist[hud.Name.Length] = lenDist.GetValueOrDefault(hud.Name.Length) + 1;
            scanned++;
        }

        log.AppendLine("扫描成功 = " + scanned + " 只（title 为空者已跳过）");
        log.AppendLine("推荐数分布 = " + string.Join("  ", recDist.Select(kv => kv.Key + "个:" + kv.Value + "只")));
        log.AppendLine("名字字数分布 = " + string.Join("  ", lenDist.Select(kv => kv.Key + "字:" + kv.Value + "只")));
        log.AppendLine();

        // --- 推荐数 >= 3 的怪（用户的质疑焦点）---
        var heavy = rows.Where(r => r.Rec >= 3).OrderByDescending(r => r.Rec).ThenBy(r => r.Title).ToList();
        log.AppendLine("【推荐 ≥ 3 个的怪】共 " + heavy.Count + " 只");
        foreach (var r in heavy)
            log.AppendLine("    " + r.Title.PadRight(10) + r.Len + "字  推荐 " + r.Rec + " 个  徽章[" + r.Badge + "]");
        log.AppendLine();

        // --- 会截断的怪 ---
        var truncated = rows.Where(r => r.Need > r.Got + 0.5).OrderBy(r => r.Got - r.Need).ToList();
        log.AppendLine("【名字会被截断的怪】" + truncated.Count + " / " + rows.Count + " 只");
        if (truncated.Count == 0) log.AppendLine("    （无）");
        foreach (var r in truncated)
            log.AppendLine("    " + r.Title.PadRight(10) + r.Len + "字  推荐" + r.Rec + "个  徽章[" + r.Badge + "]" +
                           "  需要 " + r.Need.ToString("0.#").PadLeft(6) + "px  可用 " + r.Got.ToString("0.#").PadLeft(6) +
                           "px  差 " + (r.Got - r.Need).ToString("0.#") + "px");
        log.AppendLine();

        // --- 余量最紧的 12 只（含没被截断的）---
        log.AppendLine("【余量最紧的 12 只】可用 - 需要");
        foreach (var r in rows.OrderBy(r => r.Got - r.Need).Take(12))
            log.AppendLine("    " + r.Title.PadRight(10) + r.Len + "字  推荐" + r.Rec + "个  " +
                           "余量 " + (r.Got - r.Need).ToString("0.#").PadLeft(7) + "px" +
                           (r.Got - r.Need < 0 ? "  ← 截断" : ""));
        log.AppendLine();

        // --- 徽章 / 胶囊 宽度统计 ---
        log.AppendLine("【徽章宽度】");
        foreach (var g in rows.GroupBy(r => r.Badge))
            log.AppendLine("    [" + g.Key + "] " + g.Count() + " 只  实宽 " + g.Average(r => r.BadgeW).ToString("0.#") + "px");
        log.AppendLine("【推荐胶囊列宽度】按胶囊数");
        foreach (var g in rows.GroupBy(r => r.Rec).OrderBy(g => g.Key))
            log.AppendLine("    " + g.Key + " 个胶囊 → 列宽 " + g.Average(r => r.Chips).ToString("0.#") + "px" +
                           "（均摊 " + (g.Key == 0 ? "-" : (g.Average(r => r.Chips) / g.Key).ToString("0.#")) + "px/个）");
    }

    // ---------------------------------------------------------------- 测量与探针

    /// <summary>
    /// 文本「完整宽度」—— 用 FormattedText 独立测量，完全不受父级布局约束。
    /// 绝不能改用 TextBlock.DesiredSize.Width：它会被列宽夹紧，产生假阳性/假阴性。
    /// </summary>
    static double FullWidth(TextBlock tb) => MeasureText(tb.Text, tb);

    /// <summary>按给定 TextBlock 的字体属性测量任意文本的完整宽度（不受布局约束）。</summary>
    static double MeasureText(string text, TextBlock proto)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            proto.FlowDirection,
            new Typeface(proto.FontFamily, proto.FontStyle, proto.FontWeight, proto.FontStretch),
            proto.FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(proto).PixelsPerDip);
        return ft.WidthIncludingTrailingWhitespace;
    }

    static string Verdict(TextBlock? tb)
    {
        if (tb is null) return "名字元素未找到";
        double need = FullWidth(tb);
        double got = tb.ActualWidth;
        return "名字 " + got.ToString("0.#").PadLeft(6) + "px / 完整 " + need.ToString("0.#").PadLeft(6) + "px  " +
               (need > got + 0.5
                   ? "截断，缺 " + (need - got).ToString("0.#") + "px"
                   : "完整，余 " + (got - need).ToString("0.#") + "px");
    }

    static string Px(TextBlock? tb) => tb is null ? "?" : tb.ActualWidth.ToString("0.#") + "px";

    /// <summary>clipped=true 说明已在 ClipToBounds 容器内，子树溢出不算事故。</summary>
    static void Probe(DependencyObject node, FrameworkElement panel, List<string> hits, bool clipped)
    {
        if (node is FrameworkElement fe)
        {
            if (!clipped && fe.ActualWidth > 0 && fe.ActualHeight > 0)
            {
                try
                {
                    var q = fe.TransformToAncestor(panel).TransformBounds(new Rect(0, 0, fe.ActualWidth, fe.ActualHeight));
                    if (q.Right > PanelWidth + 0.5 || q.Left < -0.5)
                    {
                        string label = fe.GetType().Name;
                        if (fe is TextBlock tb) label += "[" + tb.Text + "]";
                        else if (fe is Image im) label += "[" + Last(im.Source) + "]";
                        hits.Add(label + " L=" + q.Left.ToString("0.#") + " R=" + q.Right.ToString("0.#"));
                    }
                }
                catch { }
            }
            if (fe.ClipToBounds || fe.Clip is not null) clipped = true;
        }
        int n = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < n; i++) Probe(VisualTreeHelper.GetChild(node, i), panel, hits, clipped);
    }

    static IEnumerable<Image> FindImages(DependencyObject d)
    {
        if (d is Image im) yield return im;
        int n = VisualTreeHelper.GetChildrenCount(d);
        for (int i = 0; i < n; i++)
            foreach (var x in FindImages(VisualTreeHelper.GetChild(d, i))) yield return x;
    }

    static TextBlock? FindByText(DependencyObject d, string text)
    {
        TextBlock? hit = null;
        void Walk(DependencyObject node)
        {
            if (node is TextBlock tb && tb.Text == text && tb.ActualWidth > 0) hit ??= tb;
            int n = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < n; i++) Walk(VisualTreeHelper.GetChild(node, i));
        }
        Walk(d);
        return hit;
    }

    static ItemsControl? FindChips(DependencyObject d)
    {
        if (d is ItemsControl ic && ic.Items.Count > 0 && ic.Items[0] is ElementId) return ic;
        int n = VisualTreeHelper.GetChildrenCount(d);
        for (int i = 0; i < n; i++)
        {
            var hit = FindChips(VisualTreeHelper.GetChild(d, i));
            if (hit is not null) return hit;
        }
        return null;
    }

    static IEnumerable<TextBlock> FindTextBlocks(DependencyObject d)
    {
        if (d is TextBlock tb) yield return tb;
        int n = VisualTreeHelper.GetChildrenCount(d);
        for (int i = 0; i < n; i++)
            foreach (var x in FindTextBlocks(VisualTreeHelper.GetChild(d, i))) yield return x;
    }

    static IEnumerable<Border> FindBorders(DependencyObject d)
    {
        if (d is Border b) yield return b;
        int n = VisualTreeHelper.GetChildrenCount(d);
        for (int i = 0; i < n; i++)
            foreach (var x in FindBorders(VisualTreeHelper.GetChild(d, i))) yield return x;
    }

    static void SetText(DependencyObject d, string oldText, string newText)
    {
        if (d is TextBlock tb && tb.Text == oldText) { tb.Text = newText; return; }
        int n = VisualTreeHelper.GetChildrenCount(d);
        for (int i = 0; i < n; i++) SetText(VisualTreeHelper.GetChild(d, i), oldText, newText);
    }

    static void SetIconSize(DependencyObject d, double px)
    {
        foreach (var im in FindImages(d).Take(1)) { im.Width = px; im.Height = px; }
    }

    static void HideIcon(DependencyObject d)
    {
        foreach (var im in FindImages(d).Take(1)) im.Visibility = Visibility.Collapsed;
    }

    static void HideChips(DependencyObject d)
    {
        var ic = FindChips(d);
        if (ic is not null) ic.Visibility = Visibility.Collapsed;
    }

    static void Save(FrameworkElement root, double h, string file)
    {
        var rtb = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * 2),
            (int)Math.Ceiling(h * 2), 192, 192, PixelFormats.Pbgra32);
        rtb.Render(root);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(Path.Combine(OutDir, file + ".png"));
        enc.Save(fs);
    }

    static string Last(object? source) => source?.ToString()?.Split('/').LastOrDefault() ?? "null";
}
