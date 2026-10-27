# M0：任务数据源能力报告

更新：2026-10-24。基准：0.3.2 / `88644bb6cfcf43e114100a466634b44cee801017`。下文2026-10-18结果保留为历史证据。

## 2026-10-24 只读复核

当前PATH指向本地OpenAI安装目录的Codex CLI，版本为`0.163.0-alpha.11.2`，因此重新验证，而非直接沿用旧版本结论。证据目录：`.build/capability-refresh-ce7f7992e6fe4d2f9cdfdf8f8c3004bb`。

- 独立stdio：initialize成功，loaded=0、无下一页；当前会话摘要仍为`e20287ce7531`，`thread/read`返回notLoaded。这只反映该独立服务的内存状态，不证明桌面空闲。
- 现有daemon proxy：initialize在8秒期限内超时。只读`codex app-server daemon version`查询控制socket失败，OS错误10050。未启动、重启daemon或更改网络设置。
- WhiteFix实际程序集独立只读Provider三次快照均为partial/running/active=1/stale=false，目标摘要一致。证据`.build/task-state-probe/6a8c2a61e4c1480493acd61dccffc7eb`绑定EXE SHA256 `56081a93593baacf4cb1f1538d87acea759f579809784f68d65682f277e5d519`。SourceVersion仍为该会话文件创建时的`0.163.0-alpha.10.2`元数据，不等同于当前PATH中的CLI版本。

此次重查[官方App Server文档](https://learn.chatgpt.com/docs/app-server)：thread/read不加载会话、不订阅事件，loaded/list列出对应服务内存中已加载的线程。上述本机探测仍未取得当前桌面会话的完整实时状态或真实结构化计划；不能因CLI更新而提升D02等验收状态。

本次Provider不是已运行进程内部缓存快照，不作为完整运行界面、跨账号归属、等待/失败细分或计划进度证明。此前用户确认“已更新”的真实恢复证据仍保留。

## 环境和证据

- 系统：Windows 10，.NET Framework 系统编译器可用。
- Codex CLI：`0.163.0-alpha.10.2`，来自当前桌面安装所提供的 bin 目录。
- 桌面安装包版本：`26.1013.4894.0`。Electron可执行文件版本不作为产品版本。
- 协议schema：由该CLI的 `app-server generate-json-schema` 生成，保存在忽略提交的 `.build/protocol`。
- 已保留原版构建：`artifacts/CodexRateMonitor-Orb-Baseline`。

## 已执行的只读探测

`scripts/probe-task-source.cjs` 只发送 initialize、initialized、thread/loaded/list、thread/read，不创建、恢复或中断会话，不打印响应原文。

| 路径 | 实测结果 | 能力结论 |
|---|---|---|
| 独立stdio app-server | 初始化成功，loaded数量0；读取正在开发的桌面会话返回notLoaded | 不能据此监控桌面实时任务；0不是桌面空闲 |
| 现有daemon proxy | 初始化8秒内无响应；daemon版本查询提示控制socket连接失败 | 当前电脑该共享连接不可用；没有启动/重启daemon |
| 本地会话事件记录 | 当前桌面会话元数据originator为Codex Desktop；存在task_started、task_complete、turn_aborted，携带轮次标识与时间 | 是真实生命周期候选源；仍需验证增量更新、过期、账号隔离和范围 |
| 本地结构化计划 | 当前样本没有发现可直接使用的计划步骤更新 | 计划计数能力未证实；禁止硬编码2/5 |

脱敏目标标识摘要为 `e20287ce7531`。本次活跃会话读取时最后一条生命周期事件是task_started，后续有持续item_completed事件。读取事件时只检查类型、标识和时间，未将正文作为任务判断依据。

## 实现决策和限制

1. 额度继续使用原有账号确认和只读额度接口，不受任务源探测影响。
2. 先验证本地结构化生命周期事件的只读Provider；覆盖范围始终标为partial/已观测，不能声称全局空闲。
3. 不读取提示内容猜进度，不启动新会话测试，不更改桌面运行方式。
4. 必须过滤旧轮次/账号切换后的旧事件，缺少近期确认时显示过期，未找到活动文件不等于没有任务。
5. 已实现只读增量Provider，并用真实当前会话验证running、active=1、stale=false及同一摘要关联。实时完整起止、等待状态、计划更新及跨账号归属仍未全部通过验收。

## 增量Provider验证补充

- 真实探测已多次关联上述同一会话，来源版本0.163.0-alpha.10.2，coverage固定partial。
- 流式初始化按每轮8MiB预算读取历史；没有追到文件尾时显示读取中。不能仅截最后几MiB，因为长任务的开始事件可能更早。
- 初始化后只读增量，保留跨UTF-8字节/半行的解码状态；检测截断和已读边界改变。未写完的JSON行不会发布。
- 近期日期目录发现与读取使用单调计时；时钟突变先标未知/过期，在新的结构化活动确认后恢复。物理追加顺序与轮次身份用于防止旧轮次复活。
- task_complete只证明本轮结束，界面显示“本轮已结束”，不冒充成功完成。turn_aborted显示中断。
- 不同形式的subagent来源不作为顶层任务重复计数。没有活动文件不代表全局0任务。
- 追加重复/旧轮次、部分行、文件替换、过期、账号epoch、时钟后退和子代理的合成用例已加入test-orb.ps1。
- 对近期本地记录的脱敏检查没有找到结构化计划更新；不解析步骤文本猜测状态。

本地日志不含可用的账号归属证明，epoch隔离和重新确认不能替代账号身份关联。等待审批、失败细分及完整全局任务统计仍是能力缺口。

## 后续验证

- 用当前真实开发会话持续增量事件证明同一轮次的观测关联。
- 增量读取应支持半行、文件增长、截断和滚动；避免每次扫描完整历史。
- 验证任务完成/中断后不再显示进行中；等待状态缺少明确事件时不得猜测。
- 对不存在、格式变化和不可读文件给出明确不可用提示。
- 多会话/子代理/归档/账号边界尚待验证；计划结构如不可获得，D02保持阻塞。

协议术语参考：[官方App Server文档](https://learn.chatgpt.com/docs/app-server)。本机schema和实测优先用于判断当前版本可用能力。

## 用户截图中的过期状态复核

用户展开截图显示任务确认早于额度刷新。过期本身不能证明轮询停止：该来源要求新的相关结构化事件，文件可读、额度连接有效都不续期任务状态；没有确认时不能维持进行中或猜测全局空闲。

新增只读诊断`probe-local-task-state.ps1`分别加载初版预览及最终程序集，每版独立Provider的三次快照均为partial/running/active=1/stale=false，脱敏关联仍为e20287ce7531。只输出状态枚举、时间、版本和摘要，不打印会话正文或原始协议。它不读取已运行监视器的缓存，因此仍不能判定截图实例是否存在调度/缓存问题。新增过期及同轮次恢复回归通过（核心共478项），真实常驻UI恢复证据待补。
