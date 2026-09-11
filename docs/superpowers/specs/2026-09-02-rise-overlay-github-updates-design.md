# Rise Overlay — GitHub Releases 更新设计

Date: 2026-09-02（2026-09-11 修订）  
Status: approved  
Repo: `tttengyuan/rise-overlay`

## Goal

壳窗口启动 / 「检查更新」→ GitHub Releases 下载 zip → 外部脚本替换并重启。

## Flow

1. 启动后后台检查最新 Release；若有更新，弹出「建议立即更新」窗口（可「稍后」）。
2. 用户点「立即更新」或手动「检查更新」确认 → 下载 `RiseOverlay-*.zip` → `rise-overlay-updater.ps1` 替换 → 重启。

## Notes

国内访问 GitHub 可能需 VPN；失败时可打开 Releases 页手动下载。  
「稍后」仅跳过本次会话弹窗，状态栏仍提示有新版本。
