# 项目路线图

本文是 HDD Index 工程阶段、完成状态和下一阶段方向的唯一事实来源。这里只维护 P 级目标和完成证据；具体实现细节记录在对应的 pull request 中，避免重复维护容易过期的任务清单。

## 状态定义

- **已完成**：目标范围已经实现并验证。
- **待合入**：实现和本地验证已经完成，正在通过 pull request 集成到 `master`。
- **待规划**：方向已经确定，但范围和验收标准需要在后续对话中单独确认。

## P2：工程健康度和发布流程

**状态：待合入**

目标是让普通改动、架构约束和 Windows 发布都有可重复、可验证的自动化流程。

已完成的主体能力：

- CI 在 push 和 pull request 上恢复依赖、检查格式、执行严格 Release 构建和全部测试。
- 架构依赖测试保护 Model、Application、Services、ViewModels 和 Views 的分层边界。
- `master` Ruleset 强制 pull request、rebase、线性历史、最新分支和 `build-and-test` 状态检查。
- `vMAJOR.MINOR` 或 `vMAJOR.MINOR.PATCH` tag 自动生成 Windows x64 自包含 ZIP、SHA-256 和公开 GitHub Release。
- `v1.1` 已通过自动发布流程成功发布。
- README、数据目录、升级和维护者发布文档已经建立。

本轮收尾：

- [PR #9](https://github.com/maodlife/HDD-Index-Avalonia/pull/9)：退役已经完成使命的旧数据转换器，并让本地验证命令与 CI 对齐。
- [PR #10](https://github.com/maodlife/HDD-Index-Avalonia/pull/10)：让普通 CI 和 tag Release 共用发布打包脚本，并在 pull request 阶段验证 ZIP 内容和校验文件。
- 更新发布前检查、SHA-256 操作说明和本路线图。

以上收尾 pull request 合入 `master` 后，P2 状态改为**已完成**。P2 本身不创建新版本 tag。

## P3：数据安全与恢复

**状态：待规划**

下一阶段方向是保护用户不可替代的配置和索引数据，优先考虑逐文件原子保存、有限备份、故障恢复和失败场景测试。

P3 的具体范围、恢复交互和备份保留策略尚未确定，应在新的需求对话中单独设计。下一次用户版本暂定在 P3 完成后发布，版本号暂定为 `v1.2`。
