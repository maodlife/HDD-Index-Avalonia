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

## 创建版本

确认 `master` 分支上的 CI 已经通过，并且工作区干净：

```powershell
git status
```

创建带说明的版本 tag：

```powershell
git tag -a v1.1 -m "HDD Index v1.1"
```

推送 tag：

```powershell
git push origin v1.1
```

随后在 GitHub Actions 中查看 `Windows Release` 工作流。成功后，GitHub Releases 页面会出现：

```text
HDD-Index-v1.1-win-x64.zip
HDD-Index-v1.1-win-x64.zip.sha256
```

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
