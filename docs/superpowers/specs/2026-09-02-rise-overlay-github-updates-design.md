# Rise Overlay — GitHub Releases 更新设计

Date: 2026-09-02  
Status: approved  
Repo: `tttengyuan/rise-overlay`

## Goal

壳窗口「检查更新」→ GitHub Releases 下载 zip → 外部脚本替换并重启。

## Non-goals

- 不用官方 Poogie / api.hunterpie.com
- 不启动时静默强制更新

## Flow

`release.ps1` 发版 → 朋友点「检查更新」→ 下载 `RiseOverlay-*.zip` → `rise-overlay-updater.ps1` 替换 → 重启

## Notes

国内访问 GitHub 可能需 VPN；失败时可打开 Releases 页手动下载。
