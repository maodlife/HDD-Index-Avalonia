# 数据目录说明

HDD Index 的发布包只包含程序文件，不包含用户的磁盘索引数据。升级或移动程序目录不会自动移动或删除数据。

## 默认配置位置

应用默认从以下位置读取配置：

```text
用户文档/HDD-Index/config.json
```

在 Windows 上通常对应：

```text
C:\Users\<用户名>\Documents\HDD-Index\config.json
```

配置中的 `JsonFilePath` 指向 Repository 和各磁盘 File Tree JSON 所在目录。这个目录可以位于用户文档目录，也可以位于其他本地磁盘。

## 建议备份的内容

升级前建议同时备份：

1. `用户文档/HDD-Index/config.json`
2. `config.json` 中 `JsonFilePath` 指向的整个目录
3. 如果某个磁盘索引使用了其他独立路径，也一并备份对应 JSON

只备份程序安装目录不能保护索引数据，因为程序与数据默认分开保存。

## 更换电脑或用户账户

迁移时先复制配置文件和全部索引 JSON，再检查 `config.json` 中的绝对路径是否仍然有效。磁盘盘符或本地文件夹位置发生变化时，需要相应更新 `LocalFolderPath`。
