# macOS 平台适配

存放必须使用 macOS 特定能力的实现。通过 Application 中定义的接口接入,
平台专属 API 必须隔离,保证 Windows 和 Linux 仍可编译。

通用设备、存储和媒体逻辑继续放在对应功能目录;打包材料放在仓库根目录的
`packaging/macos/`。当前目录尚无平台专属实现。
