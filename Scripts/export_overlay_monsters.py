#!/usr/bin/env python3
"""Export a compact monster overlay table from mhrise-app monsters.json."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from typing import Any

# Elder / special markers → not capturable (V1 heuristic; hand-tune JSON later).
NON_CAPTURABLE_MARKERS = (
    "古龙",
    "霸主",
    "天彗",
    "爵银",
    "奇绝",
)

PLACEHOLDER_PART = re.compile(r"^部位\s*\d+$")

ELEMENT_KEYS = ("fire", "water", "ice", "thunder", "dragon")


def is_capturable(title: str, blob: str) -> bool:
    text = f"{title}\n{blob}"
    return not any(marker in text for marker in NON_CAPTURABLE_MARKERS)


def export_hitzone(hz: dict[str, Any]) -> dict[str, Any]:
    out: dict[str, Any] = {"part": hz.get("part", "")}
    if "phase" in hz and hz["phase"] is not None:
        out["phase"] = hz["phase"]
    for key in ELEMENT_KEYS:
        if key in hz:
            out[key] = hz[key]
    return out


def export_part(part: dict[str, Any]) -> dict[str, Any] | None:
    name = part.get("part") or ""
    if PLACEHOLDER_PART.match(str(name)):
        return None
    out: dict[str, Any] = {"part": name}
    if "break" in part and part["break"] is not None:
        out["break"] = part["break"]
    if "sever" in part and part["sever"] is not None:
        out["sever"] = part["sever"]
    return out


def export_monster(monster_id: str, raw: dict[str, Any]) -> dict[str, Any]:
    title = raw.get("title") or ""
    description = raw.get("description") or ""
    summary = raw.get("summary") or ""
    fields_blob = " ".join(
        f"{f.get('label', '')} {f.get('value', '')}" for f in (raw.get("fields") or [])
    )
    species_blob = f"{description}\n{summary}\n{fields_blob}"

    hitzones = [export_hitzone(hz) for hz in (raw.get("hitzones") or [])]
    parts: list[dict[str, Any]] = []
    for p in raw.get("parts") or []:
        exported = export_part(p)
        if exported is not None:
            parts.append(exported)

    return {
        "id": monster_id,
        "title": title,
        "capturable": is_capturable(title, species_blob),
        "hitzones": hitzones,
        "parts": parts,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--input",
        type=Path,
        default=Path(r"F:\mhrise-app\assets\data\details\monsters.json"),
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path(__file__).resolve().parents[1]
        / "RiseOverlay.Data"
        / "static"
        / "monsters-overlay.json",
    )
    args = parser.parse_args()

    with args.input.open(encoding="utf-8") as f:
        source = json.load(f)

    if not isinstance(source, dict):
        raise SystemExit(f"Expected object map of monsters, got {type(source).__name__}")

    monsters = [export_monster(mid, raw) for mid, raw in source.items()]
    monsters.sort(key=lambda m: m["id"])

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8", newline="\n") as f:
        json.dump(monsters, f, ensure_ascii=False, separators=(",", ":"))
        f.write("\n")

    print(f"Wrote {len(monsters)} monsters -> {args.output}")


if __name__ == "__main__":
    main()
