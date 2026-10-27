# Codex HUDs 开源发布说明 / Open-source release notes

## 中文说明

### 项目定位

`daxian-w/codex-huds` 是基于 [D1NOOO/codex-usage-monitor](https://github.com/D1NOOO/codex-usage-monitor) 的二次开发版本。原项目及本项目均采用 MIT License；原作者版权与许可证保留在仓库中。本项目是社区工具，不是 OpenAI 官方产品，也不代表 OpenAI。

### 本次新增与改造

- 奶油玻璃风格悬浮 HUD：紧凑态、鼠标悬停展开、移出收回，保留拖拽和点击行为。
- 展开方向会根据屏幕左右边缘自动调整，避免卡片被裁切。
- 尺寸档位：极小（约 50%）、小（约 75%）、迷你、紧凑（默认）、标准、大、大号；同一套布局按比例缩放。
- 三套皮肤：奶油、冰川、薰衣草；状态栏使用纯白色。
- 紧凑态显示 Codex 标识、连接状态、5 小时额度环和 7 天额度条；5 小时环下方显示本地重置时间。
- 展开态显示 5 小时、7 天额度、重置券及到期时间，并显示可观测的真实任务状态；无法读取时明确显示错误/不可用，不伪造任务数量。
- 托盘菜单、设置窗口、更新窗口统一玻璃风格；支持简体中文、繁體中文和 English。
- 增加诊断日志、更新校验、额度/任务状态恢复和多组自动化回归测试。

### 主要配置

| 配置 | 可选值/默认值 |
|---|---|
| 悬浮模式 | 桌面悬浮 / 窗口吸附 / 玻璃悬浮球 |
| 尺寸 | 极小、小、迷你、紧凑（默认）、标准、大、大号 |
| 皮肤 | 奶油、冰川、薰衣草 |
| 展开 | 鼠标移入展开、展开/收起延迟、动画、任务状态区开关 |
| 数据 | 5 小时重置时间、7 天额度、重置券、真实可观测任务状态 |
| 语言 | 自动、简体中文、繁體中文、English |

### 安全与隐私

发布包不包含 `auth.json`、访问令牌、运行时 `settings.json`、本地日志、`.build`、用户桌面截图或旧版本备份。诊断日志默认关闭；启用后写入 `%LOCALAPPDATA%\\CodexRateMonitor\\logs`，并按保留期限自动清理。发布前运行 `scripts/verify.ps1`，对文档编码、敏感信息和本地化键进行检查。

### 包内容

- 源代码、配置模板、MIT License。
- 中英文/繁体中文 README、贡献指南、安全政策和发布指南。
- HUD 规格、能力报告、验收报告、风险审查、性能记录和示例截图。
- `scripts/verify.ps1`、构建脚本和回归测试脚本。
- `assets/THIRD-PARTY-NOTICES.md` 及 OpenAI 标志的第三方许可说明。

## English

### Project position

`daxian-w/codex-huds` is a derivative project based on [D1NOOO/codex-usage-monitor](https://github.com/D1NOOO/codex-usage-monitor). The upstream project and this repository use the MIT License; the original attribution and license are retained. This is an unofficial community utility and is not an OpenAI product.

### What was added

- Cream-glass floating HUD with compact, hover-expanded and auto-collapse states; drag and click behavior remain available.
- Edge-aware expansion direction so the card stays inside the monitor work area.
- Seven proportional size presets: Tiny (~50%), Small (~75%), Mini, Compact (default), Standard, Large and Extra large.
- Cream, Glacier and Lavender skins; the task status bar is solid white.
- Compact mode shows the Codex mark, connection state, 5-hour quota ring and 7-day bar; the ring subtitle shows the local 5-hour reset time.
- Expanded mode shows both quotas, reset credits and expiry, plus real observable task state. Unavailable task data is shown as an explicit error/unknown state rather than invented counts.
- Unified glass styling for tray menus, settings and update dialogs, with Simplified Chinese, Traditional Chinese and English localization.
- Diagnostic logging, safer update verification, quota/task recovery behavior and automated regression coverage.

### Security and privacy

The release package excludes `auth.json`, access tokens, runtime `settings.json`, local logs, `.build`, user desktop screenshots and backup files. Diagnostics are disabled by default; when enabled, redacted logs are written under `%LOCALAPPDATA%\\CodexRateMonitor\\logs` and cleaned up according to retention settings. Run `scripts/verify.ps1` before publishing.

### Attribution

See `LICENSE`, `SECURITY.md`, `CONTRIBUTING.md` and `assets/THIRD-PARTY-NOTICES.md` for licensing, disclosure, contribution and third-party mark details.
