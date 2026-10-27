using System;
using System.Collections.Generic;
using System.Globalization;

namespace CodexRateMonitorNative
{
    internal static class I18n
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Tables =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "zh-CN", new Dictionary<string, string>
                    {
                        {"AppTitle", "Codex 用量监视器"},
                        {"RefreshNow", "立即刷新"},
                        {"CheckUpdates", "检测更新…"},
                        {"CheckingUpdates", "正在检测更新…"},
                        {"UpdateAvailableMenu", "发现新版本 v{0}"},

                        {"UpdateTitle", "软件更新"},
                        {"UpdateAvailableTitle", "发现新版本 v{0}"},
                        {"UpdateVersionLine", "当前版本 v{0}  →  最新版本 v{1}"},
                        {"UpdateWhatsNew", "新版本功能"},
                        {"UpdateAction", "更新"},
                        {"AlreadyLatest", "当前 v{0} 已是最新版本。"},
                        {"UpdateCheckFailed", "暂时无法检测更新，请稍后重试。"},
                        {"UpdateDownloading", "正在下载并校验 Release 包…"},
                        {"UpdateInstalling", "正在退出并安装更新…"},
                        {"UpdateInstallFailed", "更新失败：{0}"},
                        {"UpdateAssetsMissing", "Release 中缺少 Windows 更新包或校验文件。"},
                        {"UpdateChecksumMissing", "校验文件中没有找到更新包。"},
                        {"UpdateChecksumFailed", "更新包 SHA-256 校验失败。"},
                        {"ReloadStyle", "重新加载样式"},
                        {"AppearanceMenu", "外观设置…"},
                        {"TopPosition", "顶部标题栏"},
                        {"BottomPosition", "右下角"},
                        {"TopRecommended", "顶部标题栏（推荐）"},
                        {"DesktopFloatingMode", "桌面悬浮模式"},
                        {"DisplayMode", "显示模式"},
                        {"WindowAttachMode", "窗口吸附模式"},
                        {"DisplayLines", "显示行数"},
                        {"UsagePanelsLabel", "额度面板"},
                        {"UsagePanelsAuto", "自动（推荐）"},
                        {"UsagePanelsAll", "全部显示（5h+7d）"},
                        {"UsagePanelsWeekly", "仅显示周用量"},
                        {"UsageNotProvided", "未提供"},
                        {"UsageLoading", "正在读取额度…"},
                        {"OneLine", "1行显示"},
                        {"TwoLines", "多行显示"},
                        {"Startup", "开机启动"},
                        {"Exit", "退出"},
                        {"Connecting", "连接中"},
                        {"Unavailable", "尽情发挥！"},
                        {"ServiceError", "服务错误"},
                        {"StartFailed", "启动失败：{0}"},
                        {"UsageTray", "Codex {0}：5小时 {1} · 7天 {2}"},
                        {"UsageTraySummary", "Codex {0}：{1}"},
                        {"TrayUsageTitle", "Codex {0}："},
                        {"UpdatedAt", "更新于 {0}"},
                        {"RefreshFailedCached", "刷新失败，当前显示上次成功的数据"},
                        {"RefreshFailedShort", "刷新失败"},
                        {"CachedDataStale", "数据已过期，当前显示缓存"},
                        {"CachedShort", "缓存"},
                        {"LastConfirmedAgo", "距上次确认 {0}"},
                        {"AgeSeconds", "{0}秒"},
                        {"AgeMinutes", "{0}分"},
                        {"AgeHours", "{0}时"},
                        {"AgeDays", "{0}天"},
                        {"TrayCreditsTitle", "重置券："},
                        {"TrayCreditsDetail", "剩余 {0} 张，最早 {1} 过期"},
                        {"CreditsBadge", "重置券 ×{0}"},
                        {"CreditsExpire", "{0} 到期"},
                        {"TrayCredits", " · 券{0} {1}"},
                        {"ShowResetCredits", "显示重置券（只读查询）"},
                        {"TrayStatus", "Codex 用量监视器：{0}"},
                        {"StyleReloaded", "Codex 用量监视器：样式已重新加载"},
                        {"AppearanceSaved", "Codex 用量监视器：外观已保存"},
                        {"FiveHour", "5小时"},
                        {"SevenDay", "7天"},
                        {"CliMissing", "未找到原生 Codex 可执行文件。请打开或更新 ChatGPT 桌面端，或安装 Codex CLI。"},
                        {"CommunicationError", "通信错误：{0}"},
                        {"InitializationFailed", "初始化失败"},
                        {"NotSignedIn", "未登录"},
                        {"AuthUnavailable", "登录状态失效，请登录后刷新"},
                        {"ChatGptAuthRequired", "需要 ChatGPT 账户登录；API Key 登录无法读取 5小时/7天用量"},
                        {"SettingsTitle", "Codex 用量监视器 · 外观设置"},
                        {"SettingsSaveFailed", "无法保存设置，请检查目录写入权限。当前编辑内容已保留。"},
                        {"OrbMode", "玻璃悬浮球"},
                        {"OrbSettings", "悬浮球设置"},
                        {"OrbSize", "窗口尺寸"},
                        {"OrbSkin", "悬浮球皮肤"},
                        {"OrbSkinCream", "奶油白"},
                        {"OrbSkinGlacier", "冰川蓝"},
                        {"OrbSkinLavender", "薰衣草紫"},
                        {"OrbTiny", "极小"},
                        {"OrbSmall", "小"},
                        {"OrbMini", "迷你"},
                        {"OrbCompact", "紧凑（默认）"},
                        {"OrbStandard", "标准"},
                        {"OrbLarge", "大"},
                        {"OrbExtraLarge", "大号"},
                        {"OrbHover", "鼠标移入时展开"},
                        {"OrbExpandDelay", "展开延迟（毫秒）"},
                        {"OrbCollapseDelay", "收起延迟（毫秒）"},
                        {"OrbAnimation", "平滑展开动画"},
                        {"OrbAnimationDuration", "动画时长（毫秒）"},
                        {"OrbShowTasks", "显示任务状态区"},
                        {"OrbGlass", "玻璃透明度"},
                        {"OrbGlow", "柔光强度"},
                        {"OrbRadius", "圆角系数"},
                        {"OrbPreviewExpanded", "展开预览"},
                        {"OrbPreviewCompact", "折叠预览"},
                        {"OrbFitToScreen", "已适配屏幕"},
                        {"OrbCredits", "券"},
                        {"OrbStripOnly", "以下面板/行数与字体颜色选项仅用于原悬浮条"},
                        {"TaskLoading", "正在读取任务状态…"},
                        {"TaskIdle", "空闲 · 0 项进行中"},
                        {"TaskNoRecentActivity", "未观测到近期活动"},
                        {"TaskRunning", "任务进行中"},
                        {"TaskObserved", "已观测 {0} 项进行中"},
                        {"TaskWaiting", "等待确认"},
                        {"TaskWaitingInput", "等待输入"},
                        {"TaskCompleted", "已完成"},
                        {"TaskEnded", "本轮已结束"},
                        {"TaskFailed", "任务失败"},
                        {"TaskInterrupted", "已中断"},
                        {"TaskUnavailable", "任务状态不可用"},
                        {"TaskStale", "任务数据已过期"},
                        {"TaskSourceUnavailable", "无法读取任务来源"},
                        {"TaskAccountVerifying", "正在确认账号，暂不显示任务"},
                        {"TaskLifecycleMissing", "尚未观测到可验证的任务记录"},
                        {"TaskSteps", "步骤"},
                        {"TaskNoSteps", "未提供步骤"},
                        {"TaskPartialScope", "仅显示已观测的本地活动"},
                        {"CreditsUnknown", "尚未确认"},
                        {"CreditsReadFailed", "读取失败"},
                        {"CreditsNone", "暂无可用券"},
                        {"SettingsLoadFailed", "设置文件无法读取，已使用默认设置。原文件会保留，保存时将另外备份。"},
                        {"DisplayPosition", "显示位置"},
                        {"DisplaySettings", "显示设置"},
                        {"ProgressDisplay", "进度显示"},
                        {"ShowRemaining", "显示剩余（默认）"},
                        {"ShowUsed", "显示已用"},
                        {"Remaining", "剩余"},
                        {"Used", "已用"},
                        {"Typography", "排版与尺寸"},
                        {"Colors", "颜色"},
                        {"SettingsHint", "可在上方预览样式效果。保存后写入 settings.json；取消会恢复打开窗口前的样式。"},
                        {"AppearanceTitle", "外观设置"},
                        {"AppearanceSubtitle", "调整悬浮球、悬浮条和玻璃外观"},
                        {"Font", "字体"},
                        {"MainFontSize", "主字号"},
                        {"TimeFontSize", "时间字号"},
                        {"Scale", "整体缩放"},
                        {"Opacity", "透明度"},
                        {"CornerRadius", "圆角"},
                        {"OuterBackground", "外框背景"},
                        {"RowBackground", "行背景"},
                        {"Border", "边框"},
                        {"ProgressTrack", "进度槽"},
                        {"MainText", "主要文字"},
                        {"TimeText", "时间文字"},
                        {"FiveHourColor", "5 小时"},
                        {"SevenDayColor", "7 天"},
                        {"Warning", "警告"},
                        {"Danger", "危险"},
                        {"DarkPreset", "深色预设"},
                        {"LightPreset", "浅色预设"},
                        {"SaveClose", "保存并关闭"},
                        {"Cancel", "取消"},
                        {"RestoreDefault", "恢复默认"},
                        {"LivePreview", "预览"},
                        {"PreviewSample", "示例数据"},
                        {"PreviewReduced", "缩小至 {0}%"},
                        {"Language", "界面语言(Language)"},
                        {"LanguageAuto", "自动（跟随系统）"}
                    }
                },
                {
                    "zh-TW", new Dictionary<string, string>
                    {
                        {"AppTitle", "Codex 用量監視器"},
                        {"RefreshNow", "立即重新整理"},
                        {"CheckUpdates", "檢查更新…"},
                        {"CheckingUpdates", "正在檢查更新…"},
                        {"UpdateAvailableMenu", "發現新版本 v{0}"},

                        {"UpdateTitle", "軟體更新"},
                        {"UpdateAvailableTitle", "發現新版本 v{0}"},
                        {"UpdateVersionLine", "目前版本 v{0}  →  最新版本 v{1}"},
                        {"UpdateWhatsNew", "新版本功能"},
                        {"UpdateAction", "更新"},
                        {"AlreadyLatest", "目前 v{0} 已是最新版本。"},
                        {"UpdateCheckFailed", "暫時無法檢查更新，請稍後再試。"},
                        {"UpdateDownloading", "正在下載並驗證 Release 套件…"},
                        {"UpdateInstalling", "正在結束並安裝更新…"},
                        {"UpdateInstallFailed", "更新失敗：{0}"},
                        {"UpdateAssetsMissing", "Release 中缺少 Windows 更新套件或驗證檔案。"},
                        {"UpdateChecksumMissing", "驗證檔案中找不到更新套件。"},
                        {"UpdateChecksumFailed", "更新套件 SHA-256 驗證失敗。"},
                        {"ReloadStyle", "重新載入樣式"},
                        {"AppearanceMenu", "外觀設定…"},
                        {"TopPosition", "頂部標題列"},
                        {"BottomPosition", "右下角"},
                        {"TopRecommended", "頂部標題列（建議）"},
                        {"DesktopFloatingMode", "桌面懸浮模式"},
                        {"DisplayMode", "顯示模式"},
                        {"WindowAttachMode", "視窗吸附模式"},
                        {"DisplayLines", "顯示行數"},
                        {"UsagePanelsLabel", "額度面板"},
                        {"UsagePanelsAuto", "自動（推薦）"},
                        {"UsagePanelsAll", "全部顯示（5h+7d）"},
                        {"UsagePanelsWeekly", "僅顯示週用量"},
                        {"UsageNotProvided", "未提供"},
                        {"UsageLoading", "正在讀取額度…"},
                        {"OneLine", "1行顯示"},
                        {"TwoLines", "多行顯示"},
                        {"Startup", "開機啟動"},
                        {"Exit", "結束"},
                        {"Connecting", "連線中"},
                        {"Unavailable", "盡情發揮！"},
                        {"ServiceError", "服務錯誤"},
                        {"StartFailed", "啟動失敗：{0}"},
                        {"UsageTray", "Codex {0}：5小時 {1} · 7天 {2}"},
                        {"UsageTraySummary", "Codex {0}：{1}"},
                        {"TrayUsageTitle", "Codex {0}："},
                        {"UpdatedAt", "更新於 {0}"},
                        {"RefreshFailedCached", "重新整理失敗，目前顯示上次成功的資料"},
                        {"RefreshFailedShort", "重新整理失敗"},
                        {"CachedDataStale", "資料已過期，目前顯示快取"},
                        {"CachedShort", "快取"},
                        {"LastConfirmedAgo", "距上次確認 {0}"},
                        {"AgeSeconds", "{0}秒"},
                        {"AgeMinutes", "{0}分"},
                        {"AgeHours", "{0}時"},
                        {"AgeDays", "{0}天"},
                        {"TrayCreditsTitle", "重置券："},
                        {"TrayCreditsDetail", "剩餘 {0} 張，最早 {1} 過期"},
                        {"CreditsBadge", "重置券 ×{0}"},
                        {"CreditsExpire", "{0} 到期"},
                        {"TrayCredits", " · 券{0} {1}"},
                        {"ShowResetCredits", "顯示重置券（唯讀查詢）"},
                        {"TrayStatus", "Codex 用量監視器：{0}"},
                        {"StyleReloaded", "Codex 用量監視器：樣式已重新載入"},
                        {"AppearanceSaved", "Codex 用量監視器：外觀已儲存"},
                        {"FiveHour", "5小時"},
                        {"SevenDay", "7天"},
                        {"CliMissing", "找不到原生 Codex 可執行檔。請開啟或更新 ChatGPT 桌面版，或安裝 Codex CLI。"},
                        {"CommunicationError", "通訊錯誤：{0}"},
                        {"InitializationFailed", "初始化失敗"},
                        {"NotSignedIn", "尚未登入"},
                        {"AuthUnavailable", "登入狀態失效，請登入後重新整理"},
                        {"ChatGptAuthRequired", "需要 ChatGPT 帳戶登入；API Key 登入無法讀取 5小時/7天用量"},
                        {"SettingsTitle", "Codex 用量監視器 · 外觀設定"},
                        {"SettingsSaveFailed", "無法儲存設定，請檢查目錄寫入權限。目前編輯內容已保留。"},
                        {"OrbMode", "玻璃懸浮球"},
                        {"OrbSettings", "懸浮球設定"},
                        {"OrbSize", "視窗尺寸"},
                        {"OrbSkin", "懸浮球配色"},
                        {"OrbSkinCream", "奶油白"},
                        {"OrbSkinGlacier", "冰川藍"},
                        {"OrbSkinLavender", "薰衣草紫"},
                        {"OrbTiny", "極小"},
                        {"OrbSmall", "小"},
                        {"OrbMini", "迷你"},
                        {"OrbCompact", "緊湊（預設）"},
                        {"OrbStandard", "標準"},
                        {"OrbLarge", "大"},
                        {"OrbExtraLarge", "大號"},
                        {"OrbHover", "滑鼠移入時展開"},
                        {"OrbExpandDelay", "展開延遲（毫秒）"},
                        {"OrbCollapseDelay", "收合延遲（毫秒）"},
                        {"OrbAnimation", "平滑展開動畫"},
                        {"OrbAnimationDuration", "動畫時間（毫秒）"},
                        {"OrbShowTasks", "顯示任務狀態區"},
                        {"OrbGlass", "玻璃透明度"},
                        {"OrbGlow", "柔光強度"},
                        {"OrbRadius", "圓角係數"},
                        {"OrbPreviewExpanded", "展開預覽"},
                        {"OrbPreviewCompact", "收合預覽"},
                        {"OrbFitToScreen", "已適配螢幕"},
                        {"OrbCredits", "券"},
                        {"OrbStripOnly", "以下面板/行數與字型配色選項僅用於原懸浮列"},
                        {"TaskLoading", "正在讀取任務狀態…"},
                        {"TaskIdle", "閒置 · 0 項進行中"},
                        {"TaskNoRecentActivity", "未觀測到近期活動"},
                        {"TaskRunning", "任務進行中"},
                        {"TaskObserved", "已觀測 {0} 項進行中"},
                        {"TaskWaiting", "等待確認"},
                        {"TaskWaitingInput", "等待輸入"},
                        {"TaskCompleted", "已完成"},
                        {"TaskEnded", "本輪已結束"},
                        {"TaskFailed", "任務失敗"},
                        {"TaskInterrupted", "已中斷"},
                        {"TaskUnavailable", "任務狀態無法取得"},
                        {"TaskStale", "任務資料已過期"},
                        {"TaskSourceUnavailable", "無法讀取任務來源"},
                        {"TaskAccountVerifying", "正在確認帳號，暫不顯示任務"},
                        {"TaskLifecycleMissing", "尚未觀測到可驗證的任務記錄"},
                        {"TaskSteps", "步驟"},
                        {"TaskNoSteps", "未提供步驟"},
                        {"TaskPartialScope", "僅顯示已觀測的本機活動"},
                        {"CreditsUnknown", "尚未確認"},
                        {"CreditsReadFailed", "讀取失敗"},
                        {"CreditsNone", "暫無可用券"},
                        {"SettingsLoadFailed", "無法讀取設定檔，已使用預設值。原檔會保留，儲存時將另外備份。"},
                        {"DisplayPosition", "顯示位置"},
                        {"DisplaySettings", "顯示設定"},
                        {"ProgressDisplay", "進度顯示"},
                        {"ShowRemaining", "顯示剩餘（預設）"},
                        {"ShowUsed", "顯示已用"},
                        {"Remaining", "剩餘"},
                        {"Used", "已用"},
                        {"Typography", "排版與尺寸"},
                        {"Colors", "顏色"},
                        {"SettingsHint", "可在上方預覽樣式效果。儲存後寫入 settings.json；取消會還原開啟視窗前的樣式。"},
                        {"AppearanceTitle", "外觀設定"},
                        {"AppearanceSubtitle", "調整懸浮球、懸浮列與玻璃外觀"},
                        {"Font", "字型"},
                        {"MainFontSize", "主要字級"},
                        {"TimeFontSize", "時間字級"},
                        {"Scale", "整體縮放"},
                        {"Opacity", "透明度"},
                        {"CornerRadius", "圓角"},
                        {"OuterBackground", "外框背景"},
                        {"RowBackground", "列背景"},
                        {"Border", "邊框"},
                        {"ProgressTrack", "進度槽"},
                        {"MainText", "主要文字"},
                        {"TimeText", "時間文字"},
                        {"FiveHourColor", "5 小時"},
                        {"SevenDayColor", "7 天"},
                        {"Warning", "警告"},
                        {"Danger", "危險"},
                        {"DarkPreset", "深色預設"},
                        {"LightPreset", "淺色預設"},
                        {"SaveClose", "儲存並關閉"},
                        {"Cancel", "取消"},
                        {"RestoreDefault", "還原預設"},
                        {"LivePreview", "預覽"},
                        {"PreviewSample", "範例資料"},
                        {"PreviewReduced", "縮小至 {0}%"},
                        {"Language", "介面語言(Language)"},
                        {"LanguageAuto", "自動（跟隨系統）"}
                    }
                },
                {
                    "en", new Dictionary<string, string>
                    {
                        {"AppTitle", "Codex Rate Monitor"},
                        {"RefreshNow", "Refresh now"},
                        {"CheckUpdates", "Check for updates…"},
                        {"CheckingUpdates", "Checking for updates…"},
                        {"UpdateAvailableMenu", "Update v{0} available"},

                        {"UpdateTitle", "Software update"},
                        {"UpdateAvailableTitle", "Update v{0} is available"},
                        {"UpdateVersionLine", "Current v{0}  →  Latest v{1}"},
                        {"UpdateWhatsNew", "What's new"},
                        {"UpdateAction", "Update"},
                        {"AlreadyLatest", "Version v{0} is already the latest version."},
                        {"UpdateCheckFailed", "Unable to check for updates. Please try again later."},
                        {"UpdateDownloading", "Downloading and verifying the Release package…"},
                        {"UpdateInstalling", "Exiting and installing the update…"},
                        {"UpdateInstallFailed", "Update failed: {0}"},
                        {"UpdateAssetsMissing", "The Release is missing its Windows package or checksum file."},
                        {"UpdateChecksumMissing", "The update package is not listed in the checksum file."},
                        {"UpdateChecksumFailed", "The update package SHA-256 checksum does not match."},
                        {"ReloadStyle", "Reload style"},
                        {"AppearanceMenu", "Appearance settings…"},
                        {"TopPosition", "Top title bar"},
                        {"BottomPosition", "Bottom-right"},
                        {"TopRecommended", "Top title bar (recommended)"},
                        {"DesktopFloatingMode", "Desktop floating mode"},
                        {"DisplayMode", "Display mode"},
                        {"WindowAttachMode", "Window attach mode"},
                        {"DisplayLines", "Display lines"},
                        {"UsagePanelsLabel", "Usage panels"},
                        {"UsagePanelsAuto", "Auto (recommended)"},
                        {"UsagePanelsAll", "Show all (5h + 7d)"},
                        {"UsagePanelsWeekly", "Weekly usage only"},
                        {"UsageNotProvided", "Not provided"},
                        {"UsageLoading", "Reading usage…"},
                        {"OneLine", "1 line"},
                        {"TwoLines", "Multi-row"},
                        {"Startup", "Start with Windows"},
                        {"Exit", "Exit"},
                        {"Connecting", "Connecting"},
                        {"Unavailable", "enjoy coding"},
                        {"ServiceError", "Service error"},
                        {"StartFailed", "Start failed: {0}"},
                        {"UsageTray", "Codex {0}: 5h {1} · 7d {2}"},
                        {"UsageTraySummary", "Codex {0}: {1}"},
                        {"TrayUsageTitle", "Codex {0}:"},
                        {"UpdatedAt", "Updated {0}"},
                        {"RefreshFailedCached", "Refresh failed; showing the last successful data"},
                        {"RefreshFailedShort", "Retrying"},
                        {"CachedDataStale", "Data is stale; showing cached usage"},
                        {"CachedShort", "Cached"},
                        {"LastConfirmedAgo", "Last confirmed {0} ago"},
                        {"AgeSeconds", "{0}s"},
                        {"AgeMinutes", "{0}m"},
                        {"AgeHours", "{0}h"},
                        {"AgeDays", "{0}d"},
                        {"TrayCreditsTitle", "Reset:"},
                        {"TrayCreditsDetail", "{0} left, exp {1}"},
                        {"CreditsBadge", "{0} resets"},
                        {"CreditsExpire", "exp {0}"},
                        {"TrayCredits", " · reset ×{0} {1}"},
                        {"ShowResetCredits", "Show reset credits (read-only)"},
                        {"TrayStatus", "Codex Rate Monitor: {0}"},
                        {"StyleReloaded", "Codex Rate Monitor: style reloaded"},
                        {"AppearanceSaved", "Codex Rate Monitor: appearance saved"},
                        {"FiveHour", "5h"},
                        {"SevenDay", "7d"},
                        {"CliMissing", "Native Codex executable not found. Open or update ChatGPT desktop, or install Codex CLI."},
                        {"CommunicationError", "Communication error: {0}"},
                        {"InitializationFailed", "Initialization failed"},
                        {"NotSignedIn", "Not signed in"},
                        {"AuthUnavailable", "Sign-in expired; sign in and refresh"},
                        {"ChatGptAuthRequired", "ChatGPT account sign-in required; API key auth cannot read 5h/7d usage"},
                        {"SettingsTitle", "Codex Rate Monitor · Appearance"},
                        {"SettingsSaveFailed", "Could not save settings. Check folder permissions. Your edits have been kept."},
                        {"OrbMode", "Glass widget"},
                        {"OrbSettings", "Glass widget settings"},
                        {"OrbSize", "Widget size"},
                        {"OrbSkin", "Orb skin"},
                        {"OrbSkinCream", "Cream"},
                        {"OrbSkinGlacier", "Glacier blue"},
                        {"OrbSkinLavender", "Lavender"},
                        {"OrbTiny", "Tiny (50%)"},
                        {"OrbSmall", "Small (75%)"},
                        {"OrbMini", "Mini"},
                        {"OrbCompact", "Compact (default)"},
                        {"OrbStandard", "Standard"},
                        {"OrbLarge", "Large"},
                        {"OrbExtraLarge", "Extra large"},
                        {"OrbHover", "Expand on hover"},
                        {"OrbExpandDelay", "Expand delay (ms)"},
                        {"OrbCollapseDelay", "Collapse delay (ms)"},
                        {"OrbAnimation", "Animate transitions"},
                        {"OrbAnimationDuration", "Animation duration (ms)"},
                        {"OrbShowTasks", "Show task status"},
                        {"OrbGlass", "Glass opacity"},
                        {"OrbGlow", "Soft glow"},
                        {"OrbRadius", "Corner radius"},
                        {"OrbPreviewExpanded", "Expanded preview"},
                        {"OrbPreviewCompact", "Compact preview"},
                        {"OrbFitToScreen", "Fit to screen"},
                        {"OrbCredits", "Reset"},
                        {"OrbStripOnly", "Panel/row and font/color options below apply to the original strip"},
                        {"TaskLoading", "Reading task status…"},
                        {"TaskIdle", "Idle · 0 active"},
                        {"TaskNoRecentActivity", "No recent activity observed"},
                        {"TaskRunning", "Task in progress"},
                        {"TaskObserved", "Observed {0} active"},
                        {"TaskWaiting", "Awaiting approval"},
                        {"TaskWaitingInput", "Awaiting input"},
                        {"TaskCompleted", "Completed"},
                        {"TaskEnded", "Turn ended"},
                        {"TaskFailed", "Task failed"},
                        {"TaskInterrupted", "Interrupted"},
                        {"TaskUnavailable", "Task status unavailable"},
                        {"TaskStale", "Task data is stale"},
                        {"TaskSourceUnavailable", "Task source unavailable"},
                        {"TaskAccountVerifying", "Verifying account; tasks temporarily hidden"},
                        {"TaskLifecycleMissing", "No verifiable task lifecycle observed yet"},
                        {"TaskSteps", "Steps"},
                        {"TaskNoSteps", "Steps unavailable"},
                        {"TaskPartialScope", "Observed local activity only"},
                        {"CreditsUnknown", "Not confirmed"},
                        {"CreditsReadFailed", "Read failed"},
                        {"CreditsNone", "No credits available"},
                        {"SettingsLoadFailed", "Settings could not be read. Defaults are in use; the original file will be preserved and backed up when saving."},
                        {"DisplayPosition", "Display position"},
                        {"DisplaySettings", "Display settings"},
                        {"ProgressDisplay", "Progress display"},
                        {"ShowRemaining", "Show remaining (default)"},
                        {"ShowUsed", "Show used"},
                        {"Remaining", "remaining"},
                        {"Used", "used"},
                        {"Typography", "Typography and size"},
                        {"Colors", "Colors"},
                        {"SettingsHint", "Preview the appearance above. Save writes settings.json; Cancel restores the previous appearance."},
                        {"AppearanceTitle", "Appearance"},
                        {"AppearanceSubtitle", "Customize your widget, strip, and glass appearance"},
                        {"Font", "Font"},
                        {"MainFontSize", "Main size"},
                        {"TimeFontSize", "Time size"},
                        {"Scale", "Scale"},
                        {"Opacity", "Opacity"},
                        {"CornerRadius", "Corner radius"},
                        {"OuterBackground", "Outer bg"},
                        {"RowBackground", "Row bg"},
                        {"Border", "Border"},
                        {"ProgressTrack", "Track"},
                        {"MainText", "Main text"},
                        {"TimeText", "Time text"},
                        {"FiveHourColor", "5-hour"},
                        {"SevenDayColor", "7-day"},
                        {"Warning", "Warning"},
                        {"Danger", "Danger"},
                        {"DarkPreset", "Dark preset"},
                        {"LightPreset", "Light preset"},
                        {"SaveClose", "Save and close"},
                        {"Cancel", "Cancel"},
                        {"RestoreDefault", "Restore defaults"},
                        {"LivePreview", "Preview"},
                        {"PreviewSample", "Sample data"},
                        {"PreviewReduced", "Reduced to {0}%"},
                        {"Language", "Language"},
                        {"LanguageAuto", "Auto (system)"}
                    }
                }
            };

        private static string currentLanguage = "en";

        public static string CurrentLanguage
        {
            get { return currentLanguage; }
        }

        public static void SetLanguage(string setting)
        {
            currentLanguage = ResolveLanguage(setting);
        }

        public static string NormalizeSetting(string setting)
        {
            if (string.Equals(setting, "zh-CN", StringComparison.OrdinalIgnoreCase))
                return "zh-CN";
            if (string.Equals(setting, "zh-TW", StringComparison.OrdinalIgnoreCase))
                return "zh-TW";
            if (string.Equals(setting, "en", StringComparison.OrdinalIgnoreCase))
                return "en";
            return "auto";
        }

        public static string ResolveLanguage(string setting)
        {
            string normalized = NormalizeSetting(setting);
            if (normalized != "auto")
                return normalized;
            string name = CultureInfo.CurrentUICulture.Name;
            if (name.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("zh-MO", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase))
                return "zh-TW";
            if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return "zh-CN";
            return "en";
        }

        public static string T(string key)
        {
            return Translate(key, currentLanguage);
        }

        public static string Translate(string key, string languageSetting)
        {
            string language = ResolveLanguage(languageSetting);
            Dictionary<string, string> table;
            string value;
            if (Tables.TryGetValue(language, out table) && table.TryGetValue(key, out value))
                return value;
            if (Tables["en"].TryGetValue(key, out value))
                return value;
            return key;
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, T(key), args);
        }

        public static string FormatDate(DateTime value)
        {
            return FormatDate(value, currentLanguage);
        }

        public static string FormatDate(DateTime value, string languageSetting)
        {
            string language = ResolveLanguage(languageSetting);
            if (language == "en")
                return value.ToString("MMM d HH:mm", CultureInfo.GetCultureInfo("en-US"));
            return value.ToString("M.d HH:mm", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class LanguageOption
    {
        public string Code { get; private set; }
        public string Label { get; private set; }

        public LanguageOption(string code, string label)
        {
            Code = code;
            Label = label;
        }

        public override string ToString()
        {
            return Label;
        }
    }
}
