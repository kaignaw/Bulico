# Bulico — Revit 2020 Plugin Collection

一套轻量级的 Revit 2020 插件工具集，提供轴网生墙、框墙生梁、批量建板、幕墙横挺生成、类型标记、构件过滤等实用功能。

A lightweight Revit 2020 plugin collection for Chinese AEC workflows, featuring grid-to-wall, wall-to-beam, batch floor slab, curtain wall mullion, type marking, and element filtering.

---

## Features / 功能

| Command | 功能 | Description |
|---------|------|-------------|
| GridWallCommand | 框轴生墙 | Generate walls from grid intersections |
| BeamCommand | 框墙生梁 | Generate beams on top of walls |
| WallBeamFloorCommand | 墙梁建板 | Batch floor slabs from wall/beam enclosed regions |
| RoomFloorCommand | 房间建板 | Batch floor slabs from room boundaries |
| MullionCommand | 一键横挺 | Curtain wall horizontal mullions |
| DoorWindowMarkCommand | 一键门窗类型标记 | Batch type marking for doors/windows |
| SameTypeCommand | 同型过滤 | Filter elements of the same type |
| CategoryFilterCommand | 类别过滤 | Filter elements of the same category |
| FineFilterCommand | 精细过滤 | Precise filter with type tree selection |

## Requirements / 环境要求

| Component | Version |
|-----------|---------|
| Revit | 2020 |
| .NET Framework | 4.8 |
| Visual Studio | 2019+ (optional) |
| .NET Core SDK | 3.1+ (for CLI build) |

**References / 引用路径:**
- `C:\Program Files\Autodesk\Revit 2020\RevitAPI.dll`
- `C:\Program Files\Autodesk\Revit 2020\RevitAPIUI.dll`

## Build / 编译

```bash
dotnet clean -c Release
dotnet build -c Release
```

Output: `bin\Release\net48\Bulico.dll`

## Deploy / 部署

Copy these files to `%AppData%\Autodesk\Revit\Addins\2020\`:

- `Bulico.dll`
- `Bulico.addin`

The **Bulico** panel will appear under the "Add-Ins" tab in Revit.

启动 Revit 后，在"附加模块"选项卡中可以看到 **Bulico** 面板。

## Project Structure / 项目结构

```
Bulico/
├── App.cs                       # IExternalApplication entry / 入口
├── Bulico.csproj                # Project file (.NET Framework 4.8, x64)
├── Bulico.addin                 # Deployment manifest / 部署清单
├── README.md
├── Commands/                    # IExternalCommand implementations
│   ├── AboutCommand.cs
│   ├── BeamCommand.cs
│   ├── RoomFloorCommand.cs
│   ├── WallBeamFloorCommand.cs
│   ├── CategoryFilterCommand.cs
│   ├── DoorWindowMarkCommand.cs
│   ├── FineFilterCommand.cs
│   ├── GridWallCommand.cs
│   ├── MullionCommand.cs
│   └── SameTypeCommand.cs
├── UI/                          # WPF windows (XAML + code-behind)
├── Utils/                       # Utility classes
├── Resources/                   # Icon resources (embedded)
│   └── icon_*.png
└── Properties/
    └── AssemblyInfo.cs          # Version info / 版本号
```

## License / 许可证

The MIT License (MIT)

Copyright © 2026 Kaignaw
