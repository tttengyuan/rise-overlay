# -*- coding: utf-8 -*-
"""生成 部位行改造 交付页（真实 WPF 渲染 PNG 内联 + 真实静态库数据）。

用法： D:/Python311/python.exe genparts_page.py
输出： F:/rise-overlay/docs/superpowers/previews/rise-parts-redesign.html
"""
import base64, io, os, html
from PIL import Image

OUT = "C:/Users/Administrator/AppData/Local/Temp/hudshot/out"
DST = "F:/rise-overlay/docs/superpowers/previews/rise-parts-redesign.html"

# ---------------------------------------------------------------- 图片内联

def inline(name, max_colors=160):
    p = os.path.join(OUT, name)
    im = Image.open(p).convert("RGBA")
    # 深色底 + 少量强调色，调色板量化几乎无损，体积能砍掉一大半
    q = im.convert("RGB").quantize(colors=max_colors, method=Image.MEDIANCUT, dither=Image.NONE)
    buf = io.BytesIO()
    q.save(buf, "PNG", optimize=True)
    b = buf.getvalue()
    return "data:image/png;base64," + base64.b64encode(b).decode(), len(b)

IMGS = {}
SIZE_REPORT = []
for key, fn in [
    ("g1-p1", "prop-monster_086_08-p1-current.png"),
    ("g1-p2", "prop-monster_086_08-p2-barled.png"),
    ("g1-p3", "prop-monster_086_08-p3-grsingle.png"),
    ("g1-p4", "prop-monster_086_08-p4-grouped.png"),
    ("g1-p5", "prop-monster_086_08-p5-twocol.png"),
    ("g2-p1", "prop-monster_042_00-p1-current.png"),
    ("g2-p2", "prop-monster_042_00-p2-barled.png"),
    ("g2-p3", "prop-monster_042_00-p3-grsingle.png"),
    ("g2-p4", "prop-monster_042_00-p4-grouped.png"),
    ("g2-p5", "prop-monster_042_00-p5-twocol.png"),
]:
    data, n = inline(fn)
    IMGS[key] = data
    SIZE_REPORT.append((fn, n // 1024))

# ---------------------------------------------------------------- 样式

CSS = """
:root{
  --bg:#0E1014; --panel:#14171C; --panel2:#181C22; --line:#232830;
  --fg:#E6E9EE; --fg2:#B4BCC9; --muted:#7C8698;
  --teal:#4FA3B8; --gold:#C9A227; --red:#B85C5C; --orange:#D2762F;
  --purple:#7D6A96; --blue:#4F87A6;
}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--fg);
  font:14px/1.75 "Segoe UI","Microsoft YaHei",system-ui,sans-serif;
  -webkit-font-smoothing:antialiased}
.wrap{max-width:1100px;margin:0 auto;padding:48px 28px 96px}
h1{font-size:30px;line-height:1.35;margin:0 0 6px;font-weight:600;letter-spacing:.2px}
h2{font-size:21px;margin:56px 0 16px;padding-bottom:10px;border-bottom:1px solid var(--line);font-weight:600}
h3{font-size:16px;margin:34px 0 10px;font-weight:600;color:var(--fg)}
h4{font-size:14px;margin:22px 0 8px;font-weight:600;color:var(--fg2)}
p{margin:10px 0;color:var(--fg2)}
.lead{color:var(--muted);font-size:15px;margin-bottom:26px}
ul,ol{margin:10px 0 10px 22px;color:var(--fg2)}
li{margin:5px 0}
code{background:#1C2129;border:1px solid var(--line);border-radius:4px;
  padding:1px 5px;font:12.5px/1.6 Consolas,"Cascadia Mono",monospace;color:#9FD4E0}
b,strong{color:var(--fg);font-weight:600}
em{color:var(--gold);font-style:normal}

.card{background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:18px 20px;margin:16px 0}
.card.hi{border-color:#2E5A66;background:linear-gradient(180deg,#16232A,#14171C)}
.card.warn{border-color:#5A3A2A;background:linear-gradient(180deg,#231A15,#14171C)}

.grid{display:grid;gap:16px}
.g2{grid-template-columns:1fr 1fr}
.g3{grid-template-columns:repeat(3,1fr)}
@media(max-width:820px){.g2,.g3{grid-template-columns:1fr}}

table{width:100%;border-collapse:collapse;margin:14px 0;font-size:13.5px}
th,td{text-align:left;padding:9px 10px;border-bottom:1px solid var(--line);vertical-align:top}
th{color:var(--muted);font-weight:600;font-size:12.5px;letter-spacing:.3px;
  background:var(--panel2);border-bottom:1px solid #2C323B}
tbody tr:hover{background:#171B21}
td.num,th.num{text-align:right;font-variant-numeric:tabular-nums;
  font-family:Consolas,"Cascadia Mono",monospace}
.win{color:#7FD1A8;font-weight:600}
.bad{color:#E08B6B}
.dim{color:var(--muted)}

.shot{background:#0E1014;border:1px solid var(--line);border-radius:10px;padding:10px;margin:12px 0}
.shot img{display:block;width:100%;height:auto;border-radius:6px}
.shot .cap{font-size:12.5px;color:var(--muted);margin-top:8px;display:flex;
  justify-content:space-between;gap:12px;flex-wrap:wrap}
.shot .cap b{color:var(--fg2);font-weight:600}

.pill{display:inline-block;font-size:11.5px;padding:2px 9px;border-radius:99px;
  border:1px solid var(--line);color:var(--muted);margin-right:6px}
.pill.ok{border-color:#2E5A46;color:#7FD1A8}
.pill.no{border-color:#5A3A2A;color:#E08B6B}
.pill.star{border-color:#2E5A66;color:#9FD4E0}

.kv{display:flex;flex-wrap:wrap;gap:8px 20px;margin:12px 0}
.kv div{font-size:13px;color:var(--muted)}
.kv div b{color:var(--fg);font-family:Consolas,monospace;font-size:14px}

.bar{height:6px;border-radius:3px;background:#1E242C;overflow:hidden;margin-top:4px}
.bar i{display:block;height:100%;border-radius:3px;background:var(--teal)}
.bar.g i{background:var(--gold)}

.hr{height:1px;background:var(--line);margin:30px 0}
.note{font-size:12.5px;color:var(--muted);margin-top:6px}
.foot{margin-top:60px;padding-top:18px;border-top:1px solid var(--line);
  font-size:12.5px;color:var(--muted)}
"""

# ---------------------------------------------------------------- 数据

PART_COUNT = [
    (1,16),(2,9),(3,9),(4,2),(5,2),(6,2),(7,9),(8,1),(9,9),(10,4),
    (11,9),(12,13),(13,12),(14,9),(15,4),(16,2),
]
NAME_LEN = [(1,95),(2,616),(3,124),(4,37),(5,12),(6,11),(7,5),(8,6),(9,2),(10,2),(11,1),(20,1)]
GROUP_CNT = [(1,37),(2,42),(3,24),(4,8),(5,1)]

def bars(rows, total, color="teal", unit="只"):
    out = []
    for k, v in rows:
        pct = v / total * 100
        out.append(
            f'<div style="margin:9px 0">'
            f'<div style="display:flex;justify-content:space-between;font-size:13px">'
            f'<span>{k} {unit}</span>'
            f'<span class="dim">{v}　{pct:.1f}%</span></div>'
            f'<div class="bar {"" if color=="teal" else "g"}"><i style="width:{pct:.2f}%"></i></div>'
            f'</div>')
    return "".join(out)

def table(head, rows, cls=None):
    NUM = ' class="num"'
    th = "".join("<th%s>%s</th>" % (NUM if c else "", h) for h, c in head)
    tr = ""
    for r in rows:
        tds = "".join("<td%s>%s</td>" % (NUM if head[i][1] else "", v)
                      for i, v in enumerate(r))
        tr += "<tr>%s</tr>" % tds
    return "<table><thead><tr>%s</tr></thead><tbody>%s</tbody></table>" % (th, tr)

# 变体实测（来自 out/report.txt，真实 WPF 布局）
VARIANTS = [
    dict(tag="p1-current", name="现状（基线）", star=False,
         avg=268.1, h16=648.7, lo=172.0, hi=648.7, trunc=12,
         note="名字列 70px 写死 + 状态列 38px + 弱点胶囊占满剩余宽度；双轨通栏 118px"),
    dict(tag="p2-barled", name="条主导", star=False,
         avg=255.9, h16=604.4, lo=184.4, hi=604.4, trunc=1,
         note="单行结构；弱点不再是独立列，改为压在轨道正上方的 5 等分底纹带"),
    dict(tag="p3-grsingle", name="分组 · 组内单列", star=False,
         avg=270.3, h16=609.4, lo=203.4, hi=609.4, trunc=1,
         note="行数不变；弱点胶囊只画在组头一次，组内每行拿到 151px 的条"),
    dict(tag="p4-grouped", name="分组 · 组内双列", star=True,
         avg=212.9, h16=492.7, lo=209.1, hi=515.7, trunc=1,
         note="组头共享弱点胶囊；组内两列并排，条跨满整张卡 135px"),
    dict(tag="p5-twocol", name="纯双列", star=False,
         avg=158.4, h16=394.0, lo=186.1, hi=394.0, trunc=12,
         note="每张卡自带 5 格弱点色块；不分组，部位顺序完全不动"),
]

def vrow(v):
    base = 268.1
    d = (v["avg"] - base) / base * 100
    sign = "win" if d < -3 else ("bad" if d > 1 else "dim")
    cls = ' class="win"' if v["trunc"] <= 1 else ' class="bad"'
    star = ' <span class="pill star">推荐</span>' if v["star"] else ""
    return (f'<tr><td><b>{v["tag"]}</b><br><span class="dim" style="font-size:12.5px">'
            f'{html.escape(v["name"])}{star}</span></td>'
            f'<td class="num">{v["avg"]:.1f}</td>'
            f'<td class="num">{v["h16"]:.1f}</td>'
            f'<td class="num {sign}">{d:+.1f}%</td>'
            f'<td class="num"{cls}>{v["trunc"]} / 910</td></tr>')

def shot(key, title, sub):
    return (f'<div class="shot"><img src="{IMGS[key]}" alt="{html.escape(title)}">'
            f'<div class="cap"><b>{html.escape(title)}</b><span>{sub}</span></div></div>')

def scheme(v, imgs, pros, cons, detail):
    star = ' <span class="pill star">推荐</span>' if v["star"] else ""
    return f"""
<div class="card{' hi' if v['star'] else ''}">
  <h3 style="margin-top:0">{v['tag']} · {html.escape(v['name'])}{star}</h3>
  <p>{html.escape(v['note'])}</p>
  <div class="kv">
    <div>全库均高 <b>{v['avg']:.1f}px</b></div>
    <div>16 部位时 <b>{v['h16']:.1f}px</b></div>
    <div>最省 / 最费 <b>{v['lo']:.0f} / {v['hi']:.0f}px</b></div>
    <div>名字截断 <b>{v['trunc']} / 910</b></div>
  </div>
  {detail}
  {imgs}
  <div class="grid g2" style="margin-top:14px">
    <div><h4 style="margin-top:0">好在哪</h4><ul style="margin-bottom:0">{pros}</ul></div>
    <div><h4 style="margin-top:0">代价</h4><ul style="margin-bottom:0">{cons}</ul></div>
  </div>
</div>"""

# ---------------------------------------------------------------- 页面

BODY = f"""
<div class="wrap">

<h1>部位行改造 · 4 个方案</h1>
<p class="lead">全部用真实静态库（112 只 / 912 个部位行）+ 真实 WPF 控件渲染，不是 HTML 模拟。
宽度锁死 300px，所有尺寸都是 WPF 实测值。</p>

<div class="card hi">
  <h3 style="margin-top:0">结论先行</h3>
  <ul style="margin-bottom:0">
    <li>现状真正的痛点不是「丑」，是 <b>冰牙龙这种 16 部位怪要 648.7px 高</b>，
        而且 <b>9 个部位名被截断</b>（「肉质部位05　左右前肢」缺 32.3px）。</li>
    <li>全库 912 个部位行里，现状有 <b>12 个名字被截断</b>。</li>
    <li>弱点胶囊的信息密度低得离谱：<b>57.7% 的行和上一行完全一样</b>，
        68.5% 和怪物整体推荐一样，14.1% 是空的 —— 但它吃掉了每行三分之一的宽度。</li>
    <li>推荐 <b>p4 分组 · 组内双列</b>：全库均高 <b>−20.6%</b>，最坏情况 <b>−24.0%</b>，
        名字截断 <b>12 → 1</b>。</li>
    <li>要最省高度选 <b>p5 纯双列</b>（−40.9%），但截断没改善（12 → 12）。</li>
    <li>要保留「逐行往下扫」的习惯选 <b>p2 条主导</b>：行数不变，条 118 → 151px，截断 12 → 1。</li>
  </ul>
</div>

<h2>一、问题在哪（真实静态库实测）</h2>

<h3>1.1 部位数：平均 8.1 个，最多 16 个</h3>
<p>112 只怪里，<b>12 个部位</b>是最常见的一档（13 只），但 13-16 部位的怪合计 27 只。
部位数直接决定面板高度，所以最坏情况才是设计目标。</p>
<div class="card">{bars(PART_COUNT, 112)}</div>

<h3>1.2 弱点胶囊的信息密度极低</h3>
<div class="card">
  {table([("口径", False), ("行数", True), ("占比", True)], [
      ("与<b>上一行</b>的弱点完全相同的行", "526 / 912", "57.7%"),
      ("与<b>怪物整体推荐</b>相同的行", "625 / 912", "68.5%"),
      ("<b>空胶囊</b>（该部位没有任何弱点）", "129 / 912", "14.1%"),
  ])}
  <p class="note">每 10 行里有近 6 行的胶囊和上一行一模一样。胶囊占了每行约 1/3 的宽度，
  换回来的是重复信息 —— 这是本次改造最大的空间来源。</p>
</div>

<h3>1.3 去重后的弱点组合只有 1-2 种（70% 的怪）</h3>
<div class="grid g2">
  <div class="card">{bars(GROUP_CNT, 112, color="gold", unit="种组合")}</div>
  <div class="card">
    <p style="margin-top:0"><b>这意味着「分组」是安全的：</b></p>
    <ul>
      <li>37 只（33%）只有 1 种组合 → 分组后<b>只有 1 个组头</b>，等于没分</li>
      <li>42 只（37.5%）2 种组合 → 2 个组头</li>
      <li>只有 9 只（8%）有 4-5 种组合 → 组头会变多</li>
    </ul>
    <p style="margin-bottom:0" class="note">所以「按弱点分组」不会把面板切得七零八落，
    最坏情况也只有 5 个组头。</p>
  </div>
</div>

<h3>1.4 部位名大多很短，但长尾很长</h3>
<div class="grid g2">
  <div class="card">{bars(NAME_LEN, 912, unit="行")}</div>
  <div class="card">
    <p style="margin-top:0"><b>78% 的部位名 ≤ 2 个字</b>（1 字 95 行 + 2 字 616 行），
    所以名字列看起来「70px 够用」。</p>
    <p>但长尾很难看：</p>
    <ul>
      <li>20 字　1 行</li>
      <li>11 字　1 行</li>
      <li>10 字　2 行</li>
      <li>9 字　 2 行</li>
    </ul>
    <p style="margin-bottom:0">冰牙龙的 <code>肉质部位05　左右前肢</code> 要 102px，
    而现状只给 70px → 缺 32.3px。<b>名字列必须自适应，不能写死。</b></p>
  </div>
</div>

<h3>1.5 现状最坏情况</h3>
<div class="grid g2">
  {shot("g1-p1", "现状 · 怪异克服天彗龙（14 部位）", "面板 581.4px　截断 0/14")}
  {shot("g2-p1", "现状 · 冰牙龙（16 部位）", "面板 648.7px　截断 9/16")}
</div>
<p>冰牙龙左边这一列 <code>肉质部位00…</code>、<code>肉质部位01…</code> 全被切掉了，
而右边一列 <code>左</code>、<code>右</code> 因为名字短反而完整 —— 同一张表里一半完整一半残缺，
视觉上极不协调。天彗龙因为名字都短（最长 6 字）反而没这个问题。</p>
<p>这两只怪会贯穿下面四个方案的对比图。</p>

<h2>二、四个方案</h2>

<h3>2.1 先说两个被数据否掉的方案</h3>
<div class="card warn">
  <p style="margin-top:0"><b>「瘦身」（名字列 70→48px）已作废。</b>
  实测把全库截断从 12/910 推到 <b>34/910</b> —— 这是负优化，不是优化。</p>
  <p style="margin-bottom:0"><b>「条主导 v1」（弱点做成独立色块列）也作废。</b>
  5 格色块组占 39px，条反而从 118px 缩到 112px，设计意图自杀。
  现在的 v2 把弱点改成压在轨道上方的底纹带，条才真正变长。</p>
</div>

{scheme(VARIANTS[1],
  shot("g1-p2", "p2 条主导 · 怪异克服天彗龙", "面板 548.4px　截断 0/14") +
  shot("g2-p2", "p2 条主导 · 冰牙龙", "面板 604.4px　截断 0/16"),
  "<li>条 <b>118 → 151px</b>（+28%），数字内嵌进条里</li>"
  "<li>弱点变成条顶的 5 等分底纹带，<b>零额外宽度</b></li>"
  "<li>名字列改成自适应 + 封顶 110px，截断 <b>12 → 1</b></li>"
  "<li>行数、部位顺序完全不变 —— 改动风险最低</li>",
  "<li>一行里同时出现 5 格底纹 + 粉红硬直条 + 青绿血条，<b>颜色噪音偏大</b></li>"
  "<li>高度只省 4.6%，主要收益在条长而不是高度</li>"
  "<li>底纹带的格子很窄（30px/格），属性色只能靠位置记忆</li>",
  "")}

{scheme(VARIANTS[2],
  shot("g1-p3", "p3 分组·组内单列 · 怪异克服天彗龙", "面板 561.4px　截断 0/14") +
  shot("g2-p3", "p3 分组·组内单列 · 冰牙龙", "面板 609.4px　截断 0/16"),
  "<li>弱点胶囊从每行重复 → <b>只在组头出现一次</b></li>"
  "<li>条 118 → 151px，名字列自适应</li>"
  "<li>行数不变，仍然可以「从头往下扫」</li>"
  "<li>截断 12 → 1</li>",
  "<li>加了组头（3 组约 69px），<b>全库均高反而 +0.8%</b></li>"
  "<li>没有高度收益，只有信息质量收益</li>"
  "<li>部位顺序被分组重排</li>",
  "")}

{scheme(VARIANTS[3],
  shot("g1-p4", "p4 分组·组内双列 · 怪异克服天彗龙", "面板 433.3px　截断 0/14") +
  shot("g2-p4", "p4 分组·组内双列 · 冰牙龙", "面板 492.7px　截断 0/16"),
  "<li><b>全库均高 −20.6%，最坏情况 −24.0%</b>（648.7 → 492.7px）</li>"
  "<li>名字截断 <b>12 → 1</b>，是唯一几乎零截断的双列方案</li>"
  "<li>条跨满整张卡 <b>135px</b>（现状 118px），比现状还长</li>"
  "<li>弱点信息集中在组头，扫一眼就知道「打哪块」</li>"
  "<li>硬直条 + 部位血条双轨完整保留，信息零丢失</li>",
  "<li>部位顺序按弱点分组重排 —— 跨组比较更清楚，但「盯着一个点打」的位置会变</li>"
  "<li>组内成员为奇数时，最后一张卡右边空半格</li>"
  "<li>4-5 种弱点组合的怪（8%）组头会变多</li>",
  "")}

{scheme(VARIANTS[4],
  shot("g1-p5", "p5 纯双列 · 怪异克服天彗龙", "面板 364.3px　截断 0/14") +
  shot("g2-p5", "p5 纯双列 · 冰牙龙", "面板 394.0px　截断 9/16"),
  "<li><b>高度最省：全库均高 −40.9%，最坏 −39.3%</b></li>"
  "<li>部位顺序完全不动</li>"
  "<li>每张卡自带弱点色块，不需要抬头看组头</li>",
  "<li>弱点色块占 39px，名字只剩 69.5px → <b>截断仍是 12/910</b>，一点没改善</li>"
  "<li>色块每格只有 5px 宽，属性色基本靠猜</li>"
  "<li>比 p4 少 100px 高度，但信息完整度差一档</li>",
  "")}

<h2>三、全库对比</h2>
<table>
  <thead><tr>
    <th>变体</th><th class="num">部位块均高</th><th class="num">16 部位时</th>
    <th class="num">相对现状</th><th class="num">名字截断</th>
  </tr></thead>
  <tbody>
    {''.join(vrow(v) for v in VARIANTS)}
  </tbody>
</table>
<p class="note">「部位块均高」= 112 只怪逐只布局后取平均（不含 128px 顶栏）。
「名字截断」= 该变体下被 <code>TextTrimming</code> 切掉的行数 / 全库 912 行。
判据用 <code>FormattedText</code> 独立测量，不是被列宽夹紧的 <code>DesiredSize.Width</code>。</p>

<div class="grid g2">
  <div class="card">
    <h4 style="margin-top:0">高度排行（越短越好）</h4>
    {bars([(v["tag"], int(v["h16"])) for v in VARIANTS], 648, color="teal", unit="px @16部位")}
  </div>
  <div class="card">
    <h4 style="margin-top:0">名字截断排行（越少越好）</h4>
    {bars([(v["tag"], v["trunc"]) for v in VARIANTS], 12, color="gold", unit="行")}
  </div>
</div>

<h2>四、推荐与待你拍板</h2>

<div class="card hi">
  <h3 style="margin-top:0">我的推荐：p4 分组 · 组内双列</h3>
  <p>理由是它同时赢了三个指标：高度（−20.6%）、名字截断（12 → 1）、条长（118 → 135px），
  而且没有丢任何信息 —— 状态轨、状态文字、硬直条、部位血条、剩余值全在。</p>
  <p style="margin-bottom:0">如果只想改最少的东西，选 p2；如果只想压高度不在乎名字截断，选 p5。</p>
</div>

<h3>需要你拍板的四件事</h3>
<ol>
  <li><b>组头排序规则</b><br>
      现在是「按组大小降序」（大组在前）。另一种是「按组内剩余血量降序」——
      更贴近当前战斗，但<b>部位位置会随血量变化而跳动</b>。
      我倾向保持组大小排序，因为位置稳定。</li>
  <li><b>超长部位名怎么处理</b><br>
      全库最长 20 字（1 行）、11 字（1 行）。现在是截断 + ToolTip 显示全名。
      要彻底不截断，名字列得给到 200px 以上，条就废了。
      也可以只在超长时才用更小字号。</li>
  <li><b>组内奇数成员的半格空白</b><br>
      比如某组 3 个部位 → 2 行，第 2 行右边空着。
      接受，还是让最后一张卡横向拉满？拉满会破坏双列的对齐感。</li>
  <li><b>要不要同时提供「单列模式」</b><br>
      p2 的底纹带方案可以作为一个开关（「紧凑/舒适」），
      让不习惯双列的玩家退回单列。这会增加 XAML 的复杂度。</li>
</ol>

<h2>五、验证过 / 没验证</h2>
<div class="grid g2">
  <div class="card">
    <h4 style="margin-top:0">验证过的</h4>
    <ul style="margin-bottom:0">
      <li>数据源：<code>RiseOverlay.Data/static/monsters-overlay.json</code>（116 条，112 条有 title），
          经真实 <code>MonsterStaticAdapter</code> + <code>MonsterHudMapper</code> 映射</li>
      <li>部位行是真实 <code>PartRowViewModel</code>，主题画刷来自真实 <code>RiseThemeService</code>，
          弱点胶囊是真实 <code>ElementChip</code> 控件</li>
      <li>出图用 <code>RenderTargetBitmap</code>，不是 HTML 重画</li>
      <li>全库 112 只 × 5 变体逐只 measure/arrange（两轮），高度与截断都是实测</li>
      <li>现状基线直接克隆 <code>MonsterHudView</code> 里部位 <code>ItemsControl</code> 的隐式
          <code>DataTemplate</code>，与实机一致</li>
      <li>面板宽度恒为 300px，无横向溢出（越界探针 = 0）</li>
    </ul>
  </div>
  <div class="card">
    <h4 style="margin-top:0">没验证的</h4>
    <ul style="margin-bottom:0">
      <li><b>实机观感</b> —— 没有在 MHR 游戏内跑过，主题对比度、字号在真实场景下的可读性待你实机确认</li>
      <li><b>分组重排对肌肉记忆的影响</b> —— 数据只能证明「位置会变」，
          变了对狩猎体验是好是坏，只能靠你判断</li>
      <li><b>动画/过渡</b> —— 双列下部位数变化（比如断尾后）会不会有跳动，没测</li>
      <li><b>20 字部位名的专门处理</b> —— 全库只有 1 行，没为它做特殊分支</li>
      <li><b>性能</b> —— 双列只是布局变化，控件数量不变，理论上无影响，但没实测帧率</li>
    </ul>
  </div>
</div>

<div class="foot">
  生成方式：<code>docs/superpowers/tools/hudshot/</code> 里的离屏渲染工装
  （真实静态库驱动）。所有数字可在该工装的 report.txt 里复现。<br>
  图片总大小：{sum(n for _, n in SIZE_REPORT)} KB（已调色板量化）。
</div>

</div>
"""

HTML = f"""<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>部位行改造 · 4 个方案</title>
<style>{CSS}</style>
</head>
<body>
{BODY}
</body>
</html>
"""

os.makedirs(os.path.dirname(DST), exist_ok=True)
with io.open(DST, "w", encoding="utf-8") as f:
    f.write(HTML)

print("写出:", DST)
print("大小: %.1f KB" % (os.path.getsize(DST) / 1024))
print("图片数:", len(SIZE_REPORT))
for fn, n in SIZE_REPORT:
    print("   %-44s %4d KB" % (fn, n))
