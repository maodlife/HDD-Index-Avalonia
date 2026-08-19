# 发布流程

本文供项目维护者使用。普通用户升级应用请阅读[升级说明](upgrading.md)。

## 工作原理

推送符合 `vMAJOR.MINOR` 或 `vMAJOR.MINOR.PATCH` 格式的 Git tag 后，GitHub Actions 会在全新的 Windows runner 上执行：

1. 检出 tag 对应的提交
2. 安装并选择 .NET 9 SDK
3. 恢复依赖、执行严格 Release 构建和全部测试
4. 发布自包含的 Windows x64 程序
5. 将程序、README、LICENSE、文档和截图压缩为 ZIP
6. 计算 ZIP 的 SHA-256
7. 创建 GitHub Release 并上传 ZIP 与校验文件

Release 工作流不会修改或上传用户的配置和索引数据。

## v1.2 发布清单

`v1.2` 是 P4 与 P3.1 的可靠性版本，包含：

- 首次启动设置和空 Repository 创建。
- 数据目录与单个索引本地目录的路径修复。
- 配置、Repository 与单个 File Tree 的启动诊断。
- 单个缺失、损坏或不可读索引的故障隔离。
- 配置、Repository 和 File Tree JSON 的逐文件原子保存。

创建 tag 前必须满足：

1. 路线图已将 P4、P4.1 至 P4.3、P3.1 标为已完成。
2. 发布准备 PR 已合入，并且最新 `master` 的 CI 成功。
3. 使用临时数据完成以下 Windows 冒烟验证：
   - 无默认配置时能进入首次启动设置，并创建可再次加载的空 Repository。
   - 配置指向不存在的数据目录时，选择迁移后的目录能够恢复启动。
   - 两个索引中损坏一个时，健康索引仍可打开，损坏文件不会被保存覆盖。
   - 修改并保存健康数据后，退出和重新启动均能正常加载。
4. Windows x64 自包含发布包能够生成、解压并启动，ZIP 的 SHA-256 与校验文件一致。
5. `v1.2` tag 和 GitHub Release 尚不存在。

首次启动会使用当前 Windows 用户的“文档”目录。为了不影响真实配置，专项冒烟验证应在 Windows Sandbox、一次性 Windows 用户或其他隔离环境中进行，不要临时删除或移动正在使用的真实配置。

全部满足后，按照下文通用流程发布，版本变量使用：

```powershell
$versionTag = "v1.2"
```

`v1.2` 会被工作流转换为程序集版本 `1.2.0`，并生成：

```text
HDD-Index-v1.2-win-x64.zip
HDD-Index-v1.2-win-x64.zip.sha256
```

## 发布前检查

先设置本次准备创建的新版本号。`vX.Y.Z` 是占位符，必须替换为实际版本：

```powershell
$versionTag = "vX.Y.Z"
```

同步远端 tag 和 `master`，并确认工作区干净：

```powershell
git fetch origin --tags
git switch master
git pull --ff-only origin master
git status --short
git tag --list $versionTag
```

`git status --short` 和 `git tag --list` 都应没有输出。随后确认 GitHub Actions 中该 `master` 提交的 CI 已经成功。

不要从任务分支或旧提交创建发布 tag，也不要移动或重复使用已经发布的 tag。Release 工作流会拒绝不指向最新 `origin/master` 提交的 tag。

## 创建版本

在确认上述条件后，创建带说明的版本 tag 并推送：

```powershell
git tag -a $versionTag -m "HDD Index $versionTag"
git push origin $versionTag
```

随后在 GitHub Actions 中查看 `Windows Release` 工作流。成功后，工作流会使用自动生成的 Release Notes 直接创建公开 Release，不会先创建 draft。GitHub Releases 页面会出现：

```text
HDD-Index-<版本tag>-win-x64.zip
HDD-Index-<版本tag>-win-x64.zip.sha256
```

工作流完成后还应下载这两个附件，核对 SHA-256，并从解压后的 ZIP 启动一次程序。确认 Release 页面、版本 tag 和附件都正确后，发布才算结束。

## 版本规则

当前工作流接受：

```text
v1.1
v1.0.0
v1.2.3
```

两段式 tag 会在程序集版本中补零，例如 `v1.1` 对应程序集版本 `1.1.0`。不要移动或重复使用已经发布的 tag。如果发布后发现问题，应修复代码并创建新的补丁版本，例如从 `v1.1` 升到 `v1.1.1`。

## 权限

基础 CI 只需要读取仓库。Release 工作流需要 `contents: write`，因为它必须创建 GitHub Release 并上传附件。这个权限只授予 Release 工作流。
