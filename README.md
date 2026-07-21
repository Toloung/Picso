# LocalPhotoManager

一个完全本地运行的 Windows 图片管理器。它只读取原始图片；索引、缩略图和日志均保存在 `%LOCALAPPDATA%\LocalPhotoManager`，不会上传或修改原始文件。

## 当前阶段

已完成的第一阶段基础能力：

- WinUI 3 / MVVM 桌面壳，包含本地图库、扫描与取消入口。
- 本地 SQLite 初始化、版本表、目录与图片索引，以及参数化 upsert。
- 可取消的递归文件系统扫描：过滤首批图片扩展名、跳过常见开发/系统目录、不跟随重解析点。
- 防抖 `FileSystemWatcher` 服务，包含文件稳定性确认和缓冲区溢出后的增量扫描信号。
- 使用 Windows 原生 Imaging API 生成 256/1024/2048 像素缩略图缓存及稳定缓存键。
- 使用 Windows 原生解码器验证图片内容并提取 MIME、尺寸、方向、拍摄时间和相机信息。
- 数据库迁移与启动时索引恢复；重启后会从本地 SQLite 恢复最近图片。
- 监听扫描目录中的新增、修改、删除和重命名，并同步更新本地索引。
- 文件夹汇总、时间线月份汇总，以及对应的 WinUI 浏览入口。
- 本地数据目录和自动化测试。

尚未完成：大图查看、收藏、搜索和更完整的整理功能。

## 开发环境

- .NET SDK: `D:\CodexTools\dotnet-10.0.302`
- NuGet 缓存: `D:\CodexTools\nuget-packages`
- 源码: `E:\Picso\LocalPhotoManager`

使用 `D:\CodexTools\dotnet-10.0.302\dotnet.exe build LocalPhotoManager.sln` 构建。

运行自动测试：`D:\CodexTools\dotnet-10.0.302\dotnet.exe test LocalPhotoManager.sln -p:Platform=x64`。

## 安装包

运行 `powershell -ExecutionPolicy Bypass -File scripts\package-msix.ps1` 生成 Release x64 测试安装包。

默认输出：

- MSIX: `E:\Picso\LocalPhotoManager\artifacts\packages\LocalPhotoManager.App_1.0.0.0_x64_Test\LocalPhotoManager.App_1.0.0.0_x64.msix`
- 安装脚本: `E:\Picso\LocalPhotoManager\artifacts\packages\LocalPhotoManager.App_1.0.0.0_x64_Test\Install.ps1`
- 交付压缩包: `E:\Picso\LocalPhotoManager\artifacts\LocalPhotoManager_1.0.0.0_x64_Test.zip`

这是本地自签名测试包。安装时右键 `Install.ps1`，选择“使用 PowerShell 运行”，按提示信任证书并安装。
