#!/usr/bin/env python3
"""生成 Rise Overlay 的怪物图标资源。

产出两样东西：
  1) RiseOverlay.UI/Resources/Monsters/<emId>.png   —— 64x64 归一化图标
  2) RiseOverlay.UI/Resources/MonsterIcons.xaml     —— 纯 XAML 的 name→png 映射字典

数据来源：
  - 怪物清单   RiseOverlay.Data/static/monsters-overlay.json 的 id / title
  - 原始图标   F:/mhrise-app/assets/icons/<emId>_icon.png （256x256 RGBA 水墨原画）

id 映射规则（已核对 79/79 只大型怪零缺失）：
  monster_089_05       -> em089_05_icon.png
  small-monster_003_00 -> ems003_00_icon.png

注意：
  - 图标是《怪物猎人 崛起》的素材，仅在本机自用/自编译场景下打包；
    若要对外分发 Release，需自行确认版权风险。
  - _parts_group.png 不能用来定位「头部」做裁剪：每只怪的画布尺寸与长宽比都不同
    （633x209 / 785x433 / 684x245 ...），与 256² 的 icon 没有共同坐标系。

用法：
  python gen_monster_icons.py            # 全量重建
  python gen_monster_icons.py --xaml     # 只重出 XAML（图标已在仓库里）
"""
from __future__ import annotations

import argparse
import json
import os
import sys

try:
    from PIL import Image
except ImportError:  # pragma: no cover
    sys.exit("需要 Pillow：pip install pillow")

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STATIC_JSON = os.path.join(REPO, "RiseOverlay.Data", "static", "monsters-overlay.json")
SRC_ICONS = "F:/mhrise-app/assets/icons"
DST_ICONS = os.path.join(REPO, "RiseOverlay.UI", "Resources", "Monsters")
DST_XAML = os.path.join(REPO, "RiseOverlay.UI", "Resources", "MonsterIcons.xaml")

CANVAS = 64      # 输出边长
FIT = 0.94       # 内容占画布比例（统一视觉重量，避免大小怪差太多）
ICON_SIZE = 20   # HUD 里的渲染边长，改这一个数即可

PACK = "pack://application:,,,/RiseOverlay.UI;component/Resources/Monsters/{}.png"


def em_id(static_id: str) -> str | None:
    if static_id.startswith("monster_"):
        return static_id.replace("monster_", "em", 1)
    if static_id.startswith("small-monster_"):
        return static_id.replace("small-monster_", "ems", 1)
    return None


def normalize(path: str) -> Image.Image:
    im = Image.open(path).convert("RGBA")
    box = im.getchannel("A").point(lambda v: 255 if v > 16 else 0).getbbox()
    if box:
        im = im.crop(box)
    side = int(CANVAS * FIT)
    im.thumbnail((side, side), Image.LANCZOS)
    out = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    out.paste(im, ((CANVAS - im.width) // 2, (CANVAS - im.height) // 2), im)
    return out


def load_monsters() -> list[dict]:
    with open(STATIC_JSON, encoding="utf-8") as fh:
        return json.load(fh)


def build_icons(monsters: list[dict]) -> list[dict]:
    os.makedirs(DST_ICONS, exist_ok=True)
    kept, missing = [], []
    for m in monsters:
        eid = em_id(m["id"])
        src = os.path.join(SRC_ICONS, eid + "_icon.png") if eid else None
        if not src or not os.path.exists(src) or not m["title"]:
            missing.append((m["id"], m["title"]))
            continue
        normalize(src).save(os.path.join(DST_ICONS, eid + ".png"), optimize=True)
        kept.append({"id": eid, "title": m["title"]})

    # 清掉上一轮遗留、这一轮不再引用的孤儿（静态库删条目时会剩下）
    wanted = {r["id"] + ".png" for r in kept}
    for name in os.listdir(DST_ICONS):
        if name.endswith(".png") and name not in wanted:
            os.remove(os.path.join(DST_ICONS, name))
            print("  清理孤儿:", name)

    total = sum(os.path.getsize(os.path.join(DST_ICONS, f)) for f in os.listdir(DST_ICONS))
    print("图标写入 %d 张，共 %.2f MB" % (len(kept), total / 1048576))
    if missing:
        print("跳过 %d 条（无原图或无名字）：" % len(missing))
        for i, t in missing:
            print("   ", i, repr(t))
    return kept


def emit_xaml(rows: list[dict]) -> None:
    # 6 个霸主名在 HunterPie 侧写 U+00B7、静态库写 U+30FB，两种中点都挂上
    aliases = [{"id": r["id"], "title": r["title"].replace("\u30fb", "\u00b7")}
               for r in rows if "\u30fb" in r["title"]]

    L: list[str] = []
    w = L.append
    w('<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"')
    w('                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"')
    w('                    xmlns:sys="clr-namespace:System;assembly=System.Runtime">')
    w('')
    w('    <!--')
    w('        由 Scripts/gen_monster_icons.py 生成，请勿手改结构。')
    w('')
    w('        触发键：MonsterHudViewModel.Name / QuestBriefingTargetViewModel.Name')
    w('                == RiseOverlay.Data 的 MonsterStaticDto.Title（逐字相等，112 条零重名）')
    w('        图标  ：Resources/Monsters/<emId>.png，原画裁 alpha 边界后归一化到 64px')
    w('')
    w('        ⚠ 纯展示，不参与任何战斗数值或映射逻辑。')
    w('           Name 命中不到时 Image 整体 Collapsed，宽度回退到加图标前的水平。')
    w('    -->')
    w('')
    w('    <!-- HUD 里图标的渲染边长；想放大就改这一个数 -->')
    w('    <sys:Double x:Key="Hud.MonsterIconSize">%d</sys:Double>' % ICON_SIZE)
    w('')
    w('    <!-- ===== 位图源 ===== -->')
    for r in rows:
        w('    <BitmapImage x:Key="Mon.{}" UriSource="{}" />'.format(r["id"], PACK.format(r["id"])))
    w('')
    w('    <!-- ===== 名字 → 图标 ===== -->')
    w('    <Style x:Key="MonsterIconStyle" TargetType="Image">')
    w('        <Setter Property="Width" Value="{StaticResource Hud.MonsterIconSize}" />')
    w('        <Setter Property="Height" Value="{StaticResource Hud.MonsterIconSize}" />')
    w('        <Setter Property="Stretch" Value="Uniform" />')
    w('        <Setter Property="VerticalAlignment" Value="Center" />')
    w('        <Setter Property="Margin" Value="0,0,5,0" />')
    w('        <Setter Property="RenderOptions.BitmapScalingMode" Value="HighQuality" />')
    w('        <Setter Property="SnapsToDevicePixels" Value="True" />')
    w('        <Setter Property="Visibility" Value="Collapsed" />')
    w('        <Style.Triggers>')
    for r in rows:
        w('            <DataTrigger Binding="{{Binding Name}}" Value="{}">'.format(r["title"]))
        w('                <Setter Property="Source" Value="{{StaticResource Mon.{}}}" />'.format(r["id"]))
        w('                <Setter Property="Visibility" Value="Visible" />')
        w('            </DataTrigger>')
    for a in aliases:
        w('            <!-- 别名：HunterPie 侧 U+00B7 写法 -->')
        w('            <DataTrigger Binding="{{Binding Name}}" Value="{}">'.format(a["title"]))
        w('                <Setter Property="Source" Value="{{StaticResource Mon.{}}}" />'.format(a["id"]))
        w('                <Setter Property="Visibility" Value="Visible" />')
        w('            </DataTrigger>')
    w('        </Style.Triggers>')
    w('    </Style>')
    w('')
    w('</ResourceDictionary>')

    with open(DST_XAML, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(L) + "\n")
    print("XAML 写入 %s（%d 行，%d 条触发）" % (DST_XAML, len(L), len(rows) + len(aliases)))


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--xaml", action="store_true", help="只重出 XAML，不重做 PNG")
    args = ap.parse_args()

    monsters = load_monsters()
    if args.xaml:
        rows = [{"id": em_id(m["id"]), "title": m["title"]}
                for m in monsters if m["title"] and em_id(m["id"])
                and os.path.exists(os.path.join(DST_ICONS, em_id(m["id"]) + ".png"))]
    else:
        rows = build_icons(monsters)
    emit_xaml(rows)


if __name__ == "__main__":
    main()
