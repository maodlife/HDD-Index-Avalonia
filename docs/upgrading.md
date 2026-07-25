# 升级说明

## 标准升级步骤

1. 在旧版本中保存所有未保存修改并退出应用。
2. 按照[数据目录说明](data-directory.md)备份配置文件和全部索引 JSON。
3. 从 GitHub Releases 下载目标版本的 `HDD-Index-<版本tag>-win-x64.zip`。
4. 可选：使用同一 Release 中的 `.sha256` 文件校验 ZIP。
5. 将 ZIP 解压到一个新的空目录，不要直接覆盖正在使用的旧程序目录。
6. 启动新目录中的 `HDD-Index.exe`，确认 Repository 和各磁盘索引能够正常加载。
7. 确认无误后再删除旧程序目录。

发布包为 Windows x64 自包含版本，通常不需要另外安装 .NET。

## 数据兼容

程序目录和数据目录彼此独立，正常升级不会主动删除用户数据。若某个版本包含 JSON 格式迁移或其他不兼容变更，应以该版本的 Release Notes 为准。

不要在没有备份的情况下手工批量修改 Repository 或 File Tree JSON。

## 回退

如果新版本无法正常使用：

1. 退出新版本。
2. 恢复升级前备份的数据。
3. 重新启动旧版本目录中的程序。

如果新版本已经保存过数据，直接使用旧程序读取这些数据前，应先确认对应 Release Notes 是否说明可以向后兼容。

## Windows 安全提示

当前发布包未进行代码签名。Windows SmartScreen 可能显示未知发布者提示。请只从本项目的 GitHub Releases 页面下载，并使用 Release 附带的 SHA-256 文件核对下载内容。
