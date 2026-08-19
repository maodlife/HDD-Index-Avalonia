# 项目路线图

本文是 HDD Index 工程阶段、完成状态和下一阶段方向的唯一事实来源。这里只维护 P 级目标和完成证据；具体实现细节记录在对应的 pull request 中，避免重复维护容易过期的任务清单。

## 状态定义

- **已完成**：目标范围已经实现并验证。
- **实施中**：阶段范围已经确定，并且至少一个切片正在实施或后续切片仍待完成。
- **待合入**：实现和本地验证已经完成，正在通过 pull request 集成到 `master`。
- **待规划**：方向已经确定，但范围、设计决策和验收标准需要在后续对话中单独确认。
- **候选**：价值和代码依据已经确认，是否进入正式阶段仍取决于前置工作和优先级。

## 执行原则

- 同一阶段内相互关联的收尾工作使用一个 pull request，并用多个原子提交保持历史清晰。
- 只有真正能够任意顺序合并的任务才拆成并行的独立 pull request。
- 有明确依赖链时才使用 stacked pull request，并在开始前显式确认。
- 路线图只承诺 P 级目标；具体产品和技术决策在对应阶段开始时再确定。

## 阶段概览

| 阶段 | 主题 | 状态 | 主要结果 |
| --- | --- | --- | --- |
| P2 | 工程健康度和发布流程 | 已完成 | 受保护的主分支、严格 CI、可复用且经过冒烟验证的 Windows 发布链路 |
| P5 | 应用编排边界重构 | 已完成 | P5.1 至 P5.6 已合入，应用用例、外部交互和持久化边界已经建立 |
| P4 | 首次启动与启动故障恢复 | 已完成 | 首次设置、路径修复、启动诊断和单索引故障隔离已经合入 |
| P3 | 数据安全与恢复 | 实施中 | P3.1 逐文件原子保存已完成；备份与应用内恢复仍为后续候选 |

## P2：工程健康度和发布流程

**状态：已完成（2026-07-26）**

目标是让普通改动、架构约束和 Windows 发布都有可重复、可验证的自动化流程。

完成结果：

- CI 在 pull request 和 `master` 更新时恢复依赖、检查格式、执行严格 Release 构建和全部测试；PR 分支不会因为同时触发 push 和 pull request 而重复运行。
- 架构依赖测试保护 Model、Application、Services、ViewModels 和 Views 的分层边界。
- `master` Ruleset 强制 pull request、rebase、线性历史、最新分支和 `build-and-test` 状态检查。
- 普通 CI 和 tag Release 共用发布打包脚本，并在 pull request 阶段验证 Windows x64 自包含 ZIP 的关键内容和 SHA-256。
- Release 工作流只接受指向最新 `origin/master` 的 `vMAJOR.MINOR` 或 `vMAJOR.MINOR.PATCH` tag，并自动创建公开 GitHub Release。
- `v1.1` 已通过自动发布流程成功发布。
- 已退役完成使命且不再受支持的旧数据转换器。
- README、数据目录、升级、维护者发布流程和本路线图已经建立。
- [PR #9](https://github.com/maodlife/HDD-Index-Avalonia/pull/9)：退役已经完成使命的旧数据转换器，并让本地验证命令与 CI 对齐。
- [PR #10](https://github.com/maodlife/HDD-Index-Avalonia/pull/10)：让普通 CI 和 tag Release 共用发布打包脚本，并在 pull request 阶段验证 ZIP 内容和校验文件。
- [PR #11](https://github.com/maodlife/HDD-Index-Avalonia/pull/11)：补齐发布前检查、SHA-256 操作说明和路线图入口。

P2 不单独创建新版本 tag。

## P3：数据安全与恢复

**状态：实施中（2026-08-15 开始；P3.1 于 2026-08-19 完成）**

当前
[`AppConfigService`](../HDD-Index/Services/AppConfigService.cs) 和
[`TreeDataStore`](../HDD-Index/Services/TreeDataStore.cs)
原来会直接覆盖目标 JSON；如果进程在写入中途退出，目标文件可能只剩半份内容。
P3 现在拆成可独立验证的安全层级：

| 切片 | 主题 | 状态 | 目标 |
| --- | --- | --- | --- |
| P3.1 | 逐文件原子保存 | 已完成 | 同目录写临时文件、刷新到磁盘后原子替换；失败时保留原文件 |
| P3.2 | 受控备份 | 候选 | 在不让大索引备份无限增长的前提下保留可回退版本 |
| P3.3 | 应用内恢复 | 候选 | 识别主文件、备份和临时文件状态，并由用户确认恢复来源 |

P3.1 完成结果：

- `AppConfigService` 和 `TreeDataStore` 共用 `AtomicFileWriter`，配置、Repository 和每个 File Tree 都先写入目标所在目录的唯一临时文件。
- 临时文件使用 write-through 写入并显式刷新到磁盘；已有目标通过文件系统原子替换，新目标通过同目录移动发布。
- 替换失败时清理临时文件并保留旧目标；失败注入测试验证不会先截断旧 JSON。
- `ApplicationSessionManager` 仍按配置、Repository、File Tree 顺序保存，任一失败后保留整批脏状态以便重试。
- [PR #22](https://github.com/maodlife/HDD-Index-Avalonia/pull/22)：完成 P3.1 逐文件原子保存以及 P4 启动恢复，并通过完整 CI 和 Windows x64 发布包验证。

设计边界：

- P3.1 只承诺“每个 JSON 文件单独原子”，不宣称多个 JSON 之间具备全局 ACID 事务；多文件保存中途失败时，已经成功替换的文件不会自动回滚。
- 当前外部 Git 历史仍是主要版本恢复手段。P3.2、P3.3 是否实施，取决于真实恢复成本和非 Git 用户需求。

## P4：首次启动与启动故障恢复

**状态：已完成（2026-08-15 正式确定，2026-08-19 完成）**

目标是让应用在没有配置、数据迁移或部分索引损坏时仍能给出安全、可理解的下一步，不以静默覆盖旧数据换取“成功启动”。

执行切片：

| 切片 | 主题 | 状态 | 目标 |
| --- | --- | --- | --- |
| P4.1 | 首次启动 | 已完成 | 从启动窗口选择数据目录，创建默认配置和空 Repository |
| P4.2 | 路径修复与启动诊断 | 已完成 | 区分配置、数据目录和 Repository 故障；验证后修复数据路径，并支持修改每个索引的本地目录 |
| P4.3 | 单个索引故障隔离 | 已完成 | 跳过缺失、损坏或不可读的单个 File Tree，加载其余会话并汇总警告 |

P4 完成结果：

- 默认配置不存在时显示首次启动窗口；用户选择目录后创建 `config.json`、`RepoTreeData.json` 和名为 `Repository` 的空根节点。已有同名 Repository 时拒绝覆盖。
- 启动结果使用强类型状态区分首次运行、配置无效、数据目录不可用、Repository 故障和单索引故障，并在启动窗口显示实际路径与原因。
- 配置仍可读但数据目录或 Repository 位置失效时，可重新选择迁移后的数据目录；只有新目录中的 Repository 成功加载后才原子写回配置。
- 主界面的“修复路径”可重新选择当前磁盘索引对应的真实本地目录，只将配置登记为待保存。
- 单个 File Tree 缺失、JSON 损坏或不可读时不会阻断整个应用；健康索引继续加载，主窗口汇总列出被隔离文件，且本次会话不会保存或覆盖这些文件。
- 已配置但未加载的索引标签不能被当作新索引重复创建。

设计边界：

- 配置和 Repository 是建立共享会话的必需数据，故障时阻断进入主界面；单个 File Tree 是可隔离数据。
- P4 不静默重建损坏文件，也不自动猜测迁移位置。首次创建只写入不存在的默认文件，路径修复先验证再保存。
- 备份选择和损坏文件恢复属于 P3.2/P3.3；后续实现时应复用同一启动诊断契约。

## P5：应用编排边界重构

**状态：已完成（2026-07-28 开始，2026-08-13 完成）**

P5.1 已将文件扫描从 `FileNode` 提取到独立边界，P5.2 隔离了外部交互，P5.3
建立了共享会话与持久化边界，P5.4 提取了声明持有用例，P5.5 提取了 Repository
编辑用例，P5.6 提取了 File Tree 编辑用例并完成架构收尾。

目标是让 Model 只保存业务数据和领域规则，让应用用例、展示编排和外部能力之间形成可替换、可测试的边界，同时保持唯一 Model 数据源、`TreeChangeSet` 和 `TreeProjection` 投影协议。

执行切片：

| 切片 | 主题 | 状态 | 目标 |
| --- | --- | --- | --- |
| P5.1 | 文件扫描边界 | 已完成 | 从 `FileNode` 提取扫描服务，建立成功、取消、失败和警告契约 |
| P5.2 | 外部交互与组合根 | 已完成 | 隔离对话框、扫描进度和 Windows Explorer，由 `App` 手工组合依赖 |
| P5.3 | 会话与持久化边界 | 已完成 | 将加载、保存和 JSON 转换移出 ViewModel 与 Model |
| P5.4 | 声明关系用例 | 已完成 | 提取声明、放弃和策略修改的 UI 无关编排 |
| P5.5 | Repository 编辑用例 | 已完成 | 提取创建、复制、重命名、删除和搜索删除编排 |
| P5.6 | File Tree 编辑用例与收尾 | 已完成 | 提取刷新、新建、删除和路径操作，让主 ViewModel 收敛为窗口壳层 |

各切片顺序合入；前一项合入 `master` 后，下一项才从最新 `origin/master` 开始，不使用 stacked pull request。引入依赖注入容器不是 P5 目标，依赖先由 `App` 显式手工组合。

P5.1 完成结果：

- `FileNode` 不再访问本地文件系统，`TreeDataStore` 也不再兼任目录扫描入口。
- 新增 UI 无关的扫描请求、进度、结果和问题契约，以及可替换、可测试的文件系统读取边界。
- 新建索引和局部刷新统一使用扫描服务；取消、根失败或阻断性局部错误不会产生可应用结果，非阻断性警告在成功应用后汇总展示。
- 目录重解析点不再递归进入；隐藏过滤、顶层进度、跳过已声明子树、JSON 格式、`TreeChangeSet` 和 `TreeProjection` 行为保持兼容。
- 单元测试覆盖成功、取消、访问失败、局部失败、警告和跳过子树等结果；架构测试禁止 Models 重新访问本地文件系统。
- [PR #14](https://github.com/maodlife/HDD-Index-Avalonia/pull/14)：完成 P5.1 文件扫描边界提取，并通过与 CI 等价的完整检查和 Windows x64 发布包验证。

P5.2 完成结果：

- 新增消息与确认、Repository 交互、File Tree 交互、扫描进度和路径打开的 UI 无关强类型端口。
- Avalonia 对话框、文件夹选择器和扫描进度窗口由外部适配器实现；Windows Explorer 调用收敛到独立平台适配器。
- `App` 成为显式手工组合根，负责加载现有会话数据、创建服务、投影、子 ViewModel 和外部适配器，再通过构造函数注入主 ViewModel。
- `MainWindowViewModel` 不再创建具体 View，不再查找 `Application.Current`，也不再直接调用 `Process`；扫描运行状态仍由它作为窗口展示状态维护。
- 架构测试取消 ViewModels → Views 临时例外，并禁止 ViewModels 依赖外部适配器、全局应用对象和进程 API。
- 文件夹选择 ViewModel 改用注入的选择与关闭操作，并用单元测试覆盖选择成功、取消和确认结果；JSON、AXAML 命令和现有交互行为保持兼容。
- [PR #17](https://github.com/maodlife/HDD-Index-Avalonia/pull/17)：完成 P5.2 外部交互与组合根重构，并通过与 CI 等价的完整检查和 Windows x64 发布包验证。

P5.3 完成结果：

- 新增 `ApplicationSession`，集中持有本次运行共享的配置、Repository 根和 File Tree 集合。
- 新增逻辑持久化目标、`ApplicationSessionManager` 和 `IApplicationSessionStore`，将脏状态登记、未保存路径解析和选择性保存编排移出主 ViewModel。
- `JsonApplicationSessionStore` 组合配置与树数据存储；`App` 只加载一个会话，并围绕同一组 Model 创建服务、投影和 ViewModel。
- 删除路径型 `DirtyJsonFileTracker` 和 `AppConfig.IsDirty`；新增磁盘索引可以直接通过共享会话参与脏状态和保存，不再单独登记路径。
- `RepoNode` 和 `FileNode` 不再调用 JSON 序列化器，持久化实现负责反序列化及父引用恢复，架构测试防止该依赖回退。
- 测试覆盖逻辑目标、保存顺序、失败后保留整批脏状态、旧配置目录枚举、JSON 多态结构和共享会话加载；现有启动失败、路径和 JSON 行为保持兼容。
- [PR #18](https://github.com/maodlife/HDD-Index-Avalonia/pull/18)：完成 P5.3 会话与持久化边界重构，并通过与 CI 等价的完整检查和 Windows x64 发布包验证。

P5.4 完成结果：

- 新增 UI 无关的 `DeclarationUseCases` 与最小领域操作端口，覆盖建立声明、放弃声明和修改策略。
- 用例结果统一携带失败原因、`TreeChangeSet` 和逻辑持久化目标，主 ViewModel 不再为三条声明命令硬编码保存范围。
- 策略修改使用“计划、确认、应用”两阶段流程，验证阶段不修改 Model；有失效关系时仍由展示层确认后再删除。
- 声明服务继续作为双向关系维护原语供 Repository、File Tree 和刷新流程复用，现有 Model、投影、JSON 与交互行为保持兼容。
- 单元测试覆盖初始策略、验证失败、放弃选择、去重路径、策略计划无副作用、应用后双向清理和持久化目标。
- [PR #19](https://github.com/maodlife/HDD-Index-Avalonia/pull/19)：完成 P5.4 声明关系用例提取，并通过与 CI 等价的完整检查和 Windows x64 发布包验证。

P5.5 完成结果：

- 新增 UI 无关的 `RepositoryUseCases` 与最小编辑端口，覆盖创建目录、复制 File 子树、重命名、删除和搜索删除。
- 用例结果统一携带失败原因、`TreeChangeSet`、建议保持选中的节点和逻辑持久化目标，主 ViewModel 不再为 Repository 编辑命令硬编码保存范围。
- 搜索删除使用“计划、确认、应用”两阶段流程，确认前不修改 Model，并向展示层提供稳定的命中路径快照。
- 创建、重命名和删除继续标记 Repository 与当前全部 File Tree；复制子树继续只标记 Repository，现有搜索刷新、选中节点和路径导航行为保持不变。
- 单元测试覆盖动态磁盘列表、复制范围、重命名冲突、根节点删除保护、搜索计划无副作用、批量删除和失败时不标记持久化目标。
- [PR #20](https://github.com/maodlife/HDD-Index-Avalonia/pull/20)：完成 P5.5 Repository 编辑用例提取，并通过与 CI 等价的完整检查和 Windows x64 发布包验证。

P5.6 完成结果：

- 新增 UI 无关的 `FileTreeUseCases`、编辑端口与路径端口，覆盖新建索引、普通刷新、跳过声明子树刷新、删除节点、同步检查和本地路径计算。
- 新建和刷新使用“计划、扫描、应用”三阶段流程；扫描取消、失败或用户拒绝失效声明确认时不修改会话 Model，也不登记持久化目标。
- 用例结果统一携带失败原因、`TreeChangeSet`、新增 `FileData` 和逻辑持久化目标，主 ViewModel 不再直接依赖扫描器、File Tree 编辑器、声明同步服务或本地文件系统。
- 普通刷新只标记当前 File Tree；新建标记配置与新 File Tree；声明失效刷新和删除同时标记 Repository 与当前 File Tree。
- 单元测试覆盖输入验证、旧配置迁移、扫描失败无副作用、路径计算、跳过声明子树、确认前无修改、声明清理、删除保护与持久化范围；架构测试禁止 File Tree 用例依赖外部交互端口。
- [PR #21](https://github.com/maodlife/HDD-Index-Avalonia/pull/21)：完成 P5.6 File Tree 编辑用例提取与 P5 架构收尾，并通过与 CI 等价的完整检查和 Windows x64 发布包验证。

P5 除扫描失败安全策略外均为行为保持型重构，不包含首次启动、数据恢复、保存事务、界面改版或跨平台文件管理器支持。完整目标架构、已确认的设计决策和验收标准参见 [P5 应用编排边界重构计划](p5-application-orchestration.md)。

P5 本身不创建新版本 tag。P4 和 P3.1 组成下一次用户可感知的可靠性更新，计划通过 `v1.2` 发布；具体步骤参见[发布流程](releasing.md#v12-发布清单)。

## 持续工程项

这些事项具有价值，但尚未升级为独立 P 阶段：

- 收集测试覆盖率并决定是否设置逐步提高的阈值。
- 增加首次启动、关键 Avalonia UI 路径和发布产物启动级冒烟测试。
- 增加依赖更新自动化、依赖审计和可复现依赖策略。
- 评估将 GitHub Actions 固定到不可变提交，以加强供应链安全。
- 当公开分发规模扩大时，再评估 Windows 代码签名。

## 暂缓方向

- **跨平台发布**：当前核心使用场景和发布链路都是 Windows，且打开文件夹功能直接依赖 Windows Explorer。
- **撤销/重做**：应排在数据原子保存、备份和恢复之后。
- **全局事务框架**：先解决逐文件安全和可恢复性，再根据实际故障场景决定是否需要更复杂的事务协议。
