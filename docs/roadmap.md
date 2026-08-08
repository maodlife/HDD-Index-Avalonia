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
| P5 | 应用编排边界重构 | 实施中 | P5.1 至 P5.3 已完成；下一切片为 P5.4 声明关系用例 |
| P4 | 首次启动与启动故障恢复 | 候选 | 新用户可直接启动，配置或路径问题有明确处理入口 |
| P3 | 数据安全与恢复 | 候选，暂缓 | 当前使用独立 Git 仓库同步 JSON，并接受手工回退与重做 |

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

**状态：候选，暂缓**

当前
[`AppConfigService`](../HDD-Index/Services/AppConfigService.cs) 和
[`TreeDataStore`](../HDD-Index/Services/TreeDataStore.cs)
会直接覆盖目标 JSON，而多个脏文件由
[`MainWindowViewModel`](../HDD-Index/ViewModels/MainWindowViewModel.cs)
顺序保存；进程中断或磁盘写入失败时，可能产生损坏文件或只保存部分关系数据。

实际使用中，JSON 数据由另一个 Git 仓库同步并保留版本历史。当前可以接受在异常后从 Git 恢复旧版本并重做操作，因此应用内原子保存、自动备份和恢复 UI 的收益不足以成为近期主线。

以下情况出现时再重新评估 P3：

- JSON 不再由可靠的外部版本库管理。
- 数据修改频率或单次操作成本提高，手工重做变得不可接受。
- 应用开始提供给无法自行使用 Git 恢复数据的用户。
- 实际发生重复或难以诊断的文件损坏。

届时候选范围包括：

- 在目标文件所在目录写入临时文件，完整刷新后再替换目标，保证逐文件原子性。
- 替换时保留数量受控的备份，避免索引体积导致备份无限增长。
- 区分文件缺失、JSON 损坏、权限不足和磁盘错误，不静默创建或覆盖旧数据。
- 启动时检测可恢复的主文件、备份和临时文件状态，并向用户明确说明恢复来源。
- 使用失败注入覆盖写入失败、损坏 JSON 和多文件保存中途失败。

设计边界：

- 第一阶段承诺“逐文件原子且可恢复”，不宣称多个 JSON 之间具备全局 ACID 事务。
- 备份保留数量、自动恢复还是用户确认、单个磁盘索引失败后的行为，都需要在 P3 开始时确认。

## P4：首次启动与启动故障恢复

**状态：候选**

当前应用启动时要求默认路径已经存在有效配置、Repository 数据和全部 File Tree JSON；相关限制也记录在[架构文档](architecture.md#当前限制)中。

候选范围：

- 首次运行时引导用户创建数据目录、初始配置和空 Repository 根节点。
- 将启动结果区分为正常加载、首次运行、配置无效和单个索引加载失败。
- 配置或索引损坏时允许选择备份恢复、重新选择路径或显式跳过，而不是静默重建。
- 迁移电脑、用户账户或磁盘盘符后，提供可理解的路径修复流程。

首次运行和路径修复可以独立实施；如果未来实现损坏数据恢复，则应复用 P3 的备份与恢复原语，避免分别实现两套逻辑。

## P5：应用编排边界重构

**状态：实施中（2026-07-28 开始；P5.1、P5.2、P5.3 已完成）**

P5.1 已将文件扫描从 `FileNode` 提取到独立边界，P5.2 隔离了外部交互，P5.3
建立了共享会话与持久化边界。当前剩余的主要压力集中在 `MainWindowViewModel`：
它仍负责声明关系、Repository 和 File Tree 的大量命令编排；
这些限制记录在[架构文档](architecture.md#当前限制)中。

目标是让 Model 只保存业务数据和领域规则，让应用用例、展示编排和外部能力之间形成可替换、可测试的边界，同时保持唯一 Model 数据源、`TreeChangeSet` 和 `TreeProjection` 投影协议。

执行切片：

| 切片 | 主题 | 状态 | 目标 |
| --- | --- | --- | --- |
| P5.1 | 文件扫描边界 | 已完成 | 从 `FileNode` 提取扫描服务，建立成功、取消、失败和警告契约 |
| P5.2 | 外部交互与组合根 | 已完成 | 隔离对话框、扫描进度和 Windows Explorer，由 `App` 手工组合依赖 |
| P5.3 | 会话与持久化边界 | 已完成 | 将加载、保存和 JSON 转换移出 ViewModel 与 Model |
| P5.4 | 声明关系用例 | 待实施 | 提取声明、放弃和策略修改的 UI 无关编排 |
| P5.5 | Repository 编辑用例 | 待实施 | 提取创建、复制、重命名、删除和搜索删除编排 |
| P5.6 | File Tree 编辑用例与收尾 | 待实施 | 提取刷新、新建、删除和路径操作，让主 ViewModel 收敛为窗口壳层 |

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

P5 除扫描失败安全策略外均为行为保持型重构，不包含首次启动、数据恢复、保存事务、界面改版或跨平台文件管理器支持。完整目标架构、已确认的设计决策和验收标准参见 [P5 应用编排边界重构计划](p5-application-orchestration.md)。

P5 本身不创建新版本 tag。后续是否发布 `v1.2`，由下一项用户可感知改进决定。

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
