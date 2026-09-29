using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RiseOverlay.Domain;
using RiseOverlay.UI.Controls;
using RiseOverlay.UI.ViewModels;

namespace HudShot;

internal enum RowVariant
{
    Current,        // 现状：名字列 70 + 状态列 38 + 胶囊 + 双轨通栏
    BarLed,         // 条主导：去胶囊列，弱点压成色块组，条加长并把数字内嵌
    GroupedSingle,  // 分组 + 组内单列：行数不变，条 118 → 151px，弱点去重
    Grouped,        // 分组 + 组内双列：组头共享胶囊，行数减半 ★
    TwoCol,         // 双列卡片：每卡自带弱点色块组，16 个部位压成 8 行
}

/// <summary>
/// 部位行改造提案。全部用真实 PartRowViewModel 数据 + 真实主题画刷 + 真实 ElementChip 控件构建，
/// 再用 RenderTargetBitmap 渲染——不是 HTML 模拟。
/// </summary>
internal static class Proposals
{
    public const double PanelW = 300;
    public const double ContentW = 284;    // 300 - 左右各 8px padding

    static Brush B(string key) => Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;

    static readonly Dictionary<ElementId, string> ElemBrush = new()
    {
        [ElementId.Fire] = "Brushes.Element.Fire",
        [ElementId.Water] = "Brushes.Element.Water",
        [ElementId.Ice] = "Brushes.Element.Ice",
        [ElementId.Thunder] = "Brushes.Element.Thunder",
        [ElementId.Dragon] = "Brushes.Element.Dragon",
    };

    static readonly ElementId[] Order =
        [ElementId.Fire, ElementId.Water, ElementId.Ice, ElementId.Thunder, ElementId.Dragon];

    // ------------------------------------------------------------------ 公共零件

    static ElementChip Chip(ElementId e)
        => new() { Element = e, Small = true, Highlighted = true, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>状态缩写：现状最多 3 字，这里压到 2 字以省下列宽。</summary>
    static string ShortStatus(PartRowViewModel p)
    {
        if (p.IsQurio) return "怪异";
        if (p.IsBroken) return p.IsSeverable ? "已断" : "已破";
        return p.IsSeverable ? "可断" : "可破";
    }

    static TextBlock Txt(string text, double size, Brush fg, FontWeight? w = null, bool right = false)
        => new()
        {
            Text = text,
            FontSize = size,
            Foreground = fg,
            FontWeight = w ?? FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = right ? TextAlignment.Right : TextAlignment.Left,
            HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };

    static Brush RailFill(PartRowViewModel p)
        => p.IsQurio ? B("Brushes.AccentQurio")
           : p.IsSeverable ? B("Brushes.AccentSever")
           : B("Brushes.AccentTeal");

    static Brush StateRail(PartRowViewModel p)
        => p.IsQurio ? B("Brushes.AccentQurio")
           : p.IsBroken ? B("Brushes.RailBroken")
           : p.IsSeverable ? B("Brushes.AccentSever")
           : B("Brushes.RailDefault");

    /// <summary>轨条：底轨 + 按比例填充；宽度在 arrange 后按轨道实宽对齐。</summary>
    static Border TrackWithFill(double h, double ratio, Brush fill, double marginBottom)
    {
        var track = new Border
        {
            Height = h,
            Background = B("Brushes.RailTrack"),
            CornerRadius = new CornerRadius(1),
            Margin = new Thickness(0, 0, 0, marginBottom),
        };
        var inner = new Border
        {
            Height = h,
            Background = fill,
            CornerRadius = new CornerRadius(1),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        track.Child = inner;
        track.Tag = (ratio, inner);
        track.SizeChanged += (_, e) =>
        {
            if (track.Tag is ValueTuple<double, Border> t)
                t.Item2.Width = Math.Max(0, t.Item1) * e.NewSize.Width;
        };
        return track;
    }

    /// <summary>双轨（硬直 2px + 部位血），与现状同构；血条可加高并把数字内嵌进去。</summary>
    static FrameworkElement DualRails(PartRowViewModel p, double gap = 2, double hpHeight = 3, bool embedNumber = false)
    {
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        if (p.ShowFlinchBar)
        {
            var fr = TrackWithFill(2, p.FlinchRatio, B("Brushes.FlinchFill"), gap);
            fr.Opacity = 0.75;
            sp.Children.Add(fr);
        }
        if (p.ShowBreakBar)
            sp.Children.Add(embedNumber
                ? BarWithNumber(p, hpHeight, inset: 0, size: 8)
                : TrackWithFill(hpHeight, p.HealthRatio, RailFill(p), 0));
        else if (p.ShowBrokenRail)
            sp.Children.Add(new Border
            {
                Height = hpHeight,
                Background = B("Brushes.RailBroken"),
                Opacity = 0.5,
                CornerRadius = new CornerRadius(1),
            });
        return sp;
    }

    /// <summary>弱点 5 格色块组：有弱点的属性是实心块，缺的属性是矮暗点。</summary>
    static FrameworkElement WeakStrip(PartRowViewModel p, double w = 8, double h = 11)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var e in Order)
        {
            bool on = p.WeakElements.Contains(e);
            sp.Children.Add(new Border
            {
                Width = w,
                Height = on ? h : 3,
                CornerRadius = new CornerRadius(1),
                Margin = new Thickness(0, 0, 2, 0),
                Background = on ? B(ElemBrush[e]) : B("Brushes.RailTrack"),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        return sp;
    }

    /// <summary>弱点 5 格底纹：等分铺满可用宽度，弱点格用属性色，其余是暗轨。</summary>
    static FrameworkElement WeakTint(PartRowViewModel p, double h)
    {
        var grid = new UniformGrid { Rows = 1, Columns = 5 };
        foreach (var e in Order)
        {
            bool on = p.WeakElements.Contains(e);
            grid.Children.Add(new Border
            {
                Margin = new Thickness(0, 0, 1, 0),
                Background = on ? B(ElemBrush[e]) : B("Brushes.RailTrack"),
                Opacity = on ? 0.9 : 0.22,
            });
        }
        return new Border { Height = h, CornerRadius = new CornerRadius(1), ClipToBounds = true, Child = grid };
    }

    /// <summary>部位血条 + 内嵌数字。</summary>
    static FrameworkElement BarWithNumber(PartRowViewModel p, double h, double inset, double size = 9)
    {
        var host = new Grid { Height = h };
        host.Children.Add(new Border { Background = B("Brushes.RailTrack"), CornerRadius = new CornerRadius(2) });

        var fill = new Border
        {
            Background = RailFill(p),
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        host.Children.Add(fill);
        host.Tag = (p.HealthRatio, fill);
        host.SizeChanged += (_, e) =>
        {
            if (host.Tag is ValueTuple<double, Border> t)
                t.Item2.Width = Math.Max(0, t.Item1) * e.NewSize.Width;
        };

        var hp = Txt(p.HealthText, size, Brushes.White, right: true);
        hp.Margin = new Thickness(0, 0, inset + 4, 0);
        hp.HorizontalAlignment = HorizontalAlignment.Right;
        hp.Opacity = 0.95;
        host.Children.Add(hp);
        return host;
    }

    // ------------------------------------------------------------------ 各行变体

    static FrameworkElement RowCurrent(PartRowViewModel p)
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var rail = new Border { Background = StateRail(p), CornerRadius = new CornerRadius(1), Margin = new Thickness(0, 1, 0, 1), MinHeight = 20 };
        Grid.SetRowSpan(rail, 2);
        g.Children.Add(rail);

        var line = new Grid { Margin = new Thickness(5, 0, 0, 2) };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nm = Txt(p.Name, 10, B("Brushes.ForegroundMuted"));
        nm.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(nm, 0);
        line.Children.Add(nm);

        var st = Txt(p.StatusText, 9, B("Brushes.StatusText"));
        st.Margin = new Thickness(2, 0, 2, 0);
        Grid.SetColumn(st, 1);
        line.Children.Add(st);

        var chips = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var e in p.WeakElements) chips.Children.Add(Chip(e));
        Grid.SetColumn(chips, 2);
        line.Children.Add(chips);

        var hp = Txt(p.HealthText, 9, B("Brushes.ForegroundMuted"), right: true);
        hp.Margin = new Thickness(4, 0, 0, 0);
        Grid.SetColumn(hp, 3);
        line.Children.Add(hp);

        Grid.SetRow(line, 0);
        Grid.SetColumn(line, 1);
        g.Children.Add(line);

        var rails = DualRails(p);
        rails.Margin = new Thickness(5, 0, 0, 0);
        Grid.SetRow(rails, 1);
        Grid.SetColumn(rails, 1);
        g.Children.Add(rails);
        g.Opacity = p.RowOpacity;
        return g;
    }

    /// <summary>
    /// 条主导：单行结构。弱点不再是独立列，而是压在轨道正上方的 5 等分底纹带——
    /// 于是名字、状态、条三者分完宽度，条拿到 ~151px（现状 118px）。
    /// </summary>
    static FrameworkElement RowBarLed(PartRowViewModel p)
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3), Height = 22 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(StatusW) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var rail = new Border { Background = StateRail(p), CornerRadius = new CornerRadius(1), Margin = new Thickness(0, 2, 0, 2) };
        Grid.SetColumn(rail, 0);
        g.Children.Add(rail);

        var nm = Txt(p.Name, 10, p.IsBroken ? B("Brushes.ForegroundMuted") : B("Brushes.Foreground"),
                     p.IsBroken ? null : FontWeights.SemiBold);
        nm.TextTrimming = TextTrimming.CharacterEllipsis;
        nm.MaxWidth = 110;
        nm.Margin = new Thickness(5, 0, 4, 0);
        Grid.SetColumn(nm, 1);
        g.Children.Add(nm);

        var st = Txt(ShortStatus(p), 8, B("Brushes.StatusText"));
        st.Margin = new Thickness(0, 0, 4, 0);
        Grid.SetColumn(st, 2);
        g.Children.Add(st);

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var tint = WeakTint(p, 4);
        tint.Margin = new Thickness(0, 0, 0, 1);
        stack.Children.Add(tint);
        stack.Children.Add(DualRails(p, gap: 1, hpHeight: 11, embedNumber: true));
        Grid.SetColumn(stack, 3);
        g.Children.Add(stack);

        g.Opacity = p.RowOpacity;
        g.ToolTip = Tooltip(p);
        return g;
    }

    /// <summary>状态列固定宽度：保证同一列里名字起点一致、数字右端对齐。</summary>
    const double StatusW = 20;

    /// <summary>组内单列行：胶囊已在组头，这里 = 状态轨 + 名字 + 长条（数字内嵌）+ 状态。</summary>
    static FrameworkElement RowGroupedItem(PartRowViewModel p)
    {
        var g = new Grid { Margin = new Thickness(0, 2, 0, 2), Height = 20 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(StatusW) });

        var rail = new Border { Background = StateRail(p), CornerRadius = new CornerRadius(1), Margin = new Thickness(0, 2, 0, 2) };
        Grid.SetColumn(rail, 0);
        g.Children.Add(rail);

        // 名字列 Auto（列宽取全组最长名字），但封顶 MaxWidth——超过就截断，
        // 条拿剩下的全部宽度。写死 66px 会把「肉质部位05　左右前肢」这类超长名切掉。
        var nm = Txt(p.Name, 10, p.IsBroken ? B("Brushes.ForegroundMuted") : B("Brushes.Foreground"),
                     p.IsBroken ? null : FontWeights.SemiBold);
        nm.TextTrimming = TextTrimming.CharacterEllipsis;
        nm.MaxWidth = 112;
        nm.Margin = new Thickness(5, 0, 4, 0);
        Grid.SetColumn(nm, 1);
        g.Children.Add(nm);

        var bar = BarWithNumber(p, 11, inset: 0);
        Grid.SetColumn(bar, 2);
        g.Children.Add(bar);

        var st = Txt(ShortStatus(p), 8, B("Brushes.StatusText"), right: true);
        Grid.SetColumn(st, 3);
        g.Children.Add(st);

        g.Opacity = p.RowOpacity;
        g.ToolTip = Tooltip(p);
        return g;
    }

    static string Tooltip(PartRowViewModel p)
        => p.Name + " · " + p.StatusText + " · " + p.HealthText +
           (p.WeakElements.Count == 0 ? " · 无弱点" : " · 弱 " + string.Join("/", p.WeakElements.Select(e => e.ToString())));

    /// <summary>一行并排放两个部位卡。</summary>
    static FrameworkElement RowPair(PartRowViewModel? a, PartRowViewModel? b, bool withWeak)
    {
        var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (a is not null) { var c = PartCard(a, withWeak); Grid.SetColumn(c, 0); g.Children.Add(c); }
        if (b is not null) { var c = PartCard(b, withWeak); Grid.SetColumn(c, 2); g.Children.Add(c); }
        return g;
    }

    /// <summary>
    /// 单张部位卡（两行结构，信息零丢失）：
    ///   行0 = 状态轨(跨两行) | 名字(*) | [弱点色块组] | 状态 2 字
    ///   行1 =                  | 硬直条 2px + 部位血条 9px（数字内嵌）
    /// 条跨满整个卡宽（~135px），比现状单列的 118px 还长。
    /// </summary>
    static FrameworkElement PartCard(PartRowViewModel p, bool withWeak)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int col = 2;
        int weakCol = -1;
        if (withWeak)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            weakCol = col++;
        }
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(StatusW) });
        int statusCol = col;
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var rail = new Border { Background = StateRail(p), CornerRadius = new CornerRadius(1), Margin = new Thickness(0, 2, 0, 2) };
        Grid.SetColumn(rail, 0);
        Grid.SetRowSpan(rail, 2);
        g.Children.Add(rail);

        var nm = Txt(p.Name, 10, p.IsBroken ? B("Brushes.ForegroundMuted") : B("Brushes.Foreground"),
                     p.IsBroken ? null : FontWeights.SemiBold);
        nm.TextTrimming = TextTrimming.CharacterEllipsis;
        nm.Margin = new Thickness(4, 0, 3, 0);
        Grid.SetColumn(nm, 1);
        g.Children.Add(nm);

        if (weakCol >= 0)
        {
            var strip = WeakStrip(p, w: 5, h: 10);
            strip.Margin = new Thickness(0, 0, 4, 0);
            Grid.SetColumn(strip, weakCol);
            g.Children.Add(strip);
        }

        var st = Txt(ShortStatus(p), 8, B("Brushes.StatusText"), right: true);
        Grid.SetColumn(st, statusCol);
        g.Children.Add(st);

        // 行1：双轨，跨满「名字 → 状态」整段（弱点色块那一列也跨，条才够长）
        var rails = DualRails(p, gap: 1, hpHeight: 9);
        rails.Margin = new Thickness(4, 1, 0, 0);
        Grid.SetRow(rails, 1);
        Grid.SetColumn(rails, 1);
        Grid.SetColumnSpan(rails, statusCol);
        g.Children.Add(rails);

        g.Opacity = p.RowOpacity;
        g.ToolTip = Tooltip(p);
        return g;
    }

    // ------------------------------------------------------------------ 组装

    public static FrameworkElement Build(IReadOnlyList<PartRowViewModel> parts, RowVariant v)
    {
        var root = new StackPanel { Width = ContentW };

        if (v != RowVariant.Current)
        {
            var head = Txt(v switch
            {
                RowVariant.BarLed => "部位 · 状态 · 弱属性 · 硬直 + 部位血",
                RowVariant.TwoCol => "部位 · 双列卡片（卡内弱点色块）",
                RowVariant.Grouped => "部位 · 按弱属性分组 · 组内双列",
                RowVariant.GroupedSingle => "部位 · 按弱属性分组 · 组内单列",
                _ => "部位",
            }, 9, B("Brushes.ForegroundMuted"));
            head.Margin = new Thickness(0, 0, 0, 5);
            root.Children.Add(head);
        }

        switch (v)
        {
            case RowVariant.Current:
                foreach (var p in parts) root.Children.Add(RowCurrent(p));
                break;

            case RowVariant.BarLed:
                foreach (var p in parts) root.Children.Add(RowBarLed(p));
                break;

            case RowVariant.TwoCol:
                for (int i = 0; i < parts.Count; i += 2)
                    root.Children.Add(RowPair(parts[i], i + 1 < parts.Count ? parts[i + 1] : null, withWeak: true));
                break;

            case RowVariant.Grouped:
            case RowVariant.GroupedSingle:
                bool pair = v == RowVariant.Grouped;
                var groups = parts
                    .GroupBy(p => p.WeakElements.Count == 0 ? "" : string.Join("+", p.WeakElements))
                    .OrderByDescending(g => g.Count());
                foreach (var g in groups)
                {
                    var gh = new Grid { Margin = new Thickness(0, 5, 0, 2), Height = 16 };
                    gh.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    gh.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    gh.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var sample = g.First();
                    var chips = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    if (sample.WeakElements.Count == 0)
                        chips.Children.Add(Txt("无弱点", 9, B("Brushes.ForegroundMuted")));
                    else
                        foreach (var e in sample.WeakElements) chips.Children.Add(Chip(e));
                    Grid.SetColumn(chips, 0);
                    gh.Children.Add(chips);

                    var rule = new Border
                    {
                        Height = 1,
                        Background = B("Brushes.RailTrack"),
                        Margin = new Thickness(6, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    Grid.SetColumn(rule, 1);
                    gh.Children.Add(rule);

                    var cnt = Txt(g.Count() + " 处", 9, B("Brushes.ForegroundMuted"));
                    Grid.SetColumn(cnt, 2);
                    gh.Children.Add(cnt);
                    root.Children.Add(gh);

                    var arr = g.ToList();
                    if (pair)
                        for (int i = 0; i < arr.Count; i += 2)
                            root.Children.Add(RowPair(arr[i], i + 1 < arr.Count ? arr[i + 1] : null, withWeak: false));
                    else
                        foreach (var p in arr) root.Children.Add(RowGroupedItem(p));
                }
                break;
        }

        return root;
    }
}
