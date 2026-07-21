# LocalPhotoManager

一个完全本地运行的 Windows 图片管理器。它只读取原始图片；索引、缩略图和日志均保存在 `%LOCALAPPDATA%\LocalPhotoManager`，不会上传或修改原始文件。

## 当前阶段

已完成的第一阶段基础能力：

- WinUI 3 / MVVM 桌面壳，包含本地图库、扫描与取消入口。
- 本地 SQLite 初始化、版本表、目录与图片索引，以及参数化 upsert。
- 可取消的递归文件系统扫描：过滤首批图片扩展名、跳过常见开发/系统目录、不跟随重解析点。
- 256/1024/2048 像素缩略图缓存路径和稳定缓存键。
- 本地数据目录和自动化测试。

尚未完成：图片内容解码验证、EXIF 提取、实际缩略图像素生成、文件实时监听、文件夹/时间线查询视图和大图查看。

## 开发环境

- .NET SDK: `D:\CodexTools\dotnet-10.0.302`
- NuGet 缓存: `D:\CodexTools\nuget-packages`
- 源码: `E:\Picso\LocalPhotoManager`

使用 `D:\CodexTools\dotnet-10.0.302\dotnet.exe build LocalPhotoManager.sln` 构建。

运行自动测试：`D:\CodexTools\dotnet-10.0.302\dotnet.exe test LocalPhotoManager.sln -p:Platform=x64`。
