<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="Brand/Logo/bulico-lockup-dark.svg">
    <img src="Brand/Logo/bulico-lockup.svg" alt="Bulico — plugin collection" width="640">
  </picture>
</p>

<p align="center">
  一套轻量级的 Revit 2026.5+ 插件工具集，提供轴网生墙、框墙生梁、批量建板、立面轴网标注、幕墙横挺生成、类型标记、构件过滤等实用功能。<br>
  <sub>A lightweight Revit 2026.5+ plugin collection for Chinese AEC workflows.</sub>
</p>

> **Development mainline / 开发主线:** Revit 2026.5+ (.NET 10). The Revit 2020 implementation is retired and kept only in git history. / Revit 2020 版本已停止维护，仅保留在 Git 历史中。

---

## Features / 功能

| Command | 功能 | Description |
|---------|------|-------------|
| GridWallCommand | 框轴生墙 | Generate walls from grid intersections |
| BeamCommand | 框墙生梁 | Generate beams on top of walls |
| WallBeamFloorCommand | 墙梁建板 | Batch floor slabs from wall/beam enclosed regions |
| RoomFloorCommand | 房间建板 | Batch floor slabs from room boundaries (with opening support) |
| ElevationGridMarkCommand | 立面轴网标注 | Batch dimension grids in elevation/section views |
| MullionCommand | 一键横挺 | Curtain wall horizontal mullions |
| DoorWindowMarkCommand | 一键门窗类型标记 | Batch type marking for doors/windows |
| SameTypeCommand | 同型过滤 | Filter elements of the same type |
| CategoryFilterCommand | 类别过滤 | Filter elements of the same category |
| FineFilterCommand | 精细过滤 | Precise filter with type tree selection |

## Requirements / 环境要求

| Component | Version |
|-----------|---------|
| Revit | 2026.5+ |
| .NET | 10.0 |
| Visual Studio | 2022 17.12+ / 2026 |
| .NET SDK | 10.0+ |

**API references / 引用:**
- `C:\Program Files\Autodesk\Revit 2026\RevitAPI.dll`
- `C:\Program Files\Autodesk\Revit 2026\RevitAPIUI.dll`

Revit 2026.5+ is the active development mainline. Earlier 2026.0–2026.4 updates use .NET 8 and are legacy-only. Override the install path with `-p:RevitInstallDir=<path>` if Revit is not under the default location.

## Build / 编译

```bash
dotnet build -c Release
```

`dotnet build` restores implicitly. Avoid running `dotnet clean` before the first restore on a fresh clone; it fails without `obj/project.assets.json`.

Output: `bin\Release\net10.0-windows\Bulico.dll`

## Deploy / 部署

Copy these files to `%AppData%\Autodesk\Revit\Addins\2026\`:

- `Bulico.dll`
- `Bulico.addin`

The **Bulico** panel will appear under the "Add-Ins" tab in Revit.

启动 Revit 后，在"附加模块"选项卡中可以看到 **Bulico** 面板。

## Project Structure / 项目结构

```
Bulico/
├── App.cs                       # IExternalApplication entry / 入口
├── Bulico.csproj                # Project file (.NET 10, x64, WPF)
├── Bulico.addin                 # Deployment manifest / 部署清单
├── README.md
├── Commands/                    # IExternalCommand implementations
│   ├── AboutCommand.cs
│   ├── BeamCommand.cs
│   ├── CategoryFilterCommand.cs
│   ├── DoorWindowMarkCommand.cs
│   ├── ElevationGridMarkCommand.cs
│   ├── FineFilterCommand.cs
│   ├── GridWallCommand.cs
│   ├── MullionCommand.cs
│   ├── RoomFloorCommand.cs
│   ├── SameTypeCommand.cs
│   └── WallBeamFloorCommand.cs
├── UI/                          # WPF windows (XAML + code-behind)
│   ├── BeamTypeSelector.xaml/.cs
│   ├── DimTypeSelector.xaml/.cs
│   ├── FilterDialog.xaml/.cs
│   ├── MullionWindow.xaml/.cs
│   └── WallTypeSelector.xaml/.cs
├── Utils/                       # Utility classes
├── Brand/                       # Visual identity system
│   ├── README.md                # Design system + rationale
│   ├── Logo/                    # Grid Stack logo assets
│   └── Icons/                   # Command icon system
│       ├── src/                 # 32×32 SVG sources
│       ├── *_32.png / *_16.png  # Generated Revit icons
│       ├── generate-icons.mjs   # SVG → PNG generator
│       └── preview.html
└── Properties/
    └── AssemblyInfo.cs          # Version info / 版本号
```

## Visual Identity / 视觉识别

所有视觉资产集中在 [`Brand/`](Brand/)，完整设计规则见 [`Brand/README.md`](Brand/README.md)。

- **Grid Stack 标志**：三层圆角等轴测楼板；顶部实心 Teal，中层完整 Ink，底层弱化；无核心筒开孔。
- **色彩**：Ink `#0B1727`、Teal `#14B8A6`、Paper `#F8FAFC`。
- **功能图标**：32×32 深色圆角底板 + 白色线稿 + Teal 强调，保证浅色 / 深色 Ribbon 都可读。
- **延展规则**：新增图标必须沿用同一底板、线宽、安全区与配色，并从 SVG 源生成 32/16 px PNG，禁止直接手改 PNG。

Logo 资产：[`Brand/Logo/`](Brand/Logo/) · 预览：[`Brand/Logo/preview.html`](Brand/Logo/preview.html)  
功能图标：[`Brand/Icons/`](Brand/Icons/) · 预览：[`Brand/Icons/preview.html`](Brand/Icons/preview.html)

## License / 许可证

The MIT License (MIT)

Copyright © 2026 Kaignaw
