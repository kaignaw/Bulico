# Bulico — Revit 2020 Plugin Collection

一套轻量级的 Revit 2020 插件工具集，提供轴网生墙、框墙生梁、批量建板、幕墙横挺生成、类型标记、构件过滤等实用功能。

## 项目结构

```
Bulico/
├── App.cs                          # IExternalApplication 入口，注册 Ribbon 面板与按钮
├── Bulico.csproj                   # 项目文件 (.NET Framework 4.8, x64)
├── Bulico.addin                    # 部署清单 (复制到 Revit 插件目录)
├── README.md                       # 本文件
│
├── Commands/                       # IExternalCommand 命令实现（每个功能一个文件）
│   ├── AboutCommand.cs             # 关于对话框
│   ├── BatchFloorCommand.cs        # 批量建板 - 选区围合区域创建楼板
│   ├── BeamCommand.cs              # 框墙生梁 - 墙体顶部生成结构梁
│   ├── CategoryFilterCommand.cs    # 类别过滤 - 框选同类别构件
│   ├── DoorWindowMarkCommand.cs    # 一键门窗标记 - 类型名称写入类型标记参数
│   ├── FineFilterCommand.cs        # 精细过滤 - 类型树精确筛选
│   ├── GridWallCommand.cs          # 框轴生墙 - 轴网交点生成墙体
│   ├── MullionCommand.cs           # 一键横挺 - 幕墙横向网格线
│   └── SameTypeCommand.cs          # 同型过滤 - 框选同类型构件
│
├── UI/                             # WPF 窗口（XAML + Code-behind）
│   ├── BeamTypeSelector.xaml/.cs   # 梁类型选择窗口
│   ├── FilterDialog.xaml/.cs       # 精细过滤树形窗口（含 CategoryNode / FamilyNode / TypeNode）
│   ├── MullionWindow.xaml/.cs      # 横挺偏移参数窗口
│   └── WallTypeSelector.xaml/.cs   # 墙体类型选择窗口
│
├── Utils/                          # 工具类
│   ├── ProgressWindow.cs           # 进度条窗口（用于批量操作反馈）
│   ├── RegionFinder.cs             # 闭合区域查找算法（用于批量建板）
│   └── WallBeamFilter.cs           # ISelectionFilter — 墙+梁选择过滤器
│
├── Resources/                      # 图标资源（自动嵌入程序集）
│   ├── icon_about_16/32.png        # 关于
│   ├── icon_beam_16/32.png         # 框墙生梁
│   ├── icon_category_16/32.png     # 类别过滤
│   ├── icon_doorwin_16/32.png      # 门窗标记
│   ├── icon_filter_16/32.png       # 同型过滤
│   ├── icon_fine_16/32.png         # 精细过滤
│   ├── icon_floor_16/32.png        # 批量建板
│   ├── icon_gridwall_16/32.png     # 轴网生墙
│   └── icon_mullion_16/32.png      # 一键横挺
│
├── Properties/
│   └── AssemblyInfo.cs             # 程序集信息、版本号
│
├── bin/Release/net48/              # 编译输出目录
│   ├── Bulico.dll
│   └── Bulico.pdb
│
└── obj/                            # 编译中间文件（gitignore）
```

## 环境要求

| 组件 | 版本 |
|------|------|
| Revit | 2020 |
| .NET Framework | 4.8 |
| Visual Studio | 2019 或更高（可选） |
| .NET Core SDK | 3.1+（用于命令行编译） |

**引用路径：**
- `C:\Program Files\Autodesk\Revit 2020\RevitAPI.dll`
- `C:\Program Files\Autodesk\Revit 2020\RevitAPIUI.dll`

## 编译

### 命令行编译

```bash
# 清理并编译 Release
dotnet clean -c Release
dotnet build -c Release
```

输出位置：`bin\Release\net48\Bulico.dll`

### Visual Studio 编译

1. 双击 `Bulico.csproj` 打开
2. 确保解决方案配置为 `Release | x64`
3. 生成 → 生成解决方案 (Ctrl+Shift+B)

## 部署

将以下两个文件复制到 Revit 2020 插件目录：

```
%AppData%\Autodesk\Revit\Addins\2020\
├── Bulico.dll
└── Bulico.addin
```

启动 Revit 后，在"附加模块"选项卡中可以看到 **Bulico** 面板。

## 功能说明

### 1. 框轴生墙 (GridWallCommand)

框选两根以上轴网 → 自动计算所有交点 → 找到最远两点 → 生成墙体。

- 首次运行会弹出墙体类型选择窗口（记忆上次选择）
- 可多次框选，ESC 退出
- 墙体高度为当前视图标高至上一标高

### 2. 框墙生梁 (BeamCommand)

框选墙体 → 在墙体顶部生成结构梁。

- 弹出梁类型选择窗口（记忆上次选择）
- 自动定位到墙顶标高
- 梁位于墙体核心层中心线
- 进度条显示生成进度

### 3. 批量建板 (BatchFloorCommand)

框选墙和梁 → 自动查找闭合区域 → 批量生成楼板。

- 选择楼板类型、标高、偏移值
- 支持"墙梁中心线"和"墙梁内边线"两种生成模式
- 内边线模式自动计算墙梁平均半厚度进行偏移
- 进度条实时反馈
- 记忆上一次选择的参数

### 4. 一键横挺 (MullionCommand)

选中幕墙 → 输入偏移值 → 生成横向网格线。

- 支持多个偏移值以空格分隔：`500 600 700`
- 支持批量公式：`3*600` 表示 3 根间距 600mm
- 可选择"自底部"或"自顶部"方式
- 幕墙两侧均尝试放置

### 5. 一键门窗类型标记 (DoorWindowMarkCommand)

自动遍历项目中所有门窗族类型 → 将类型名称写入"类型标记"参数。

- 显示进度条
- 跳过只读参数
- 汇总显示修改数量

### 6. 同型过滤 (SameTypeCommand)

预选构件 → 框选范围 → 仅保留与预选构件**同类型**的构件。

- 支持多种构件混合预选
- 支持幕墙网格线 (CurtainGridLine) 过滤
- 结果保持选中状态

### 7. 类别过滤 (CategoryFilterCommand)

预选构件 → 框选范围 → 仅保留与预选构件**同类别**的构件。

- 基于 BuiltInCategory 过滤
- 结果保持选中状态

### 8. 精细过滤 (FineFilterCommand)

预选构件 → 打开类型树 → 勾选要保留的类型 → 精确筛选。

- 三层树结构：类别 → 族 → 类型
- 三态 CheckBox：全选 / 部分选中 / 未选
- 全选 / 清空 / 展开 / 折叠 快捷操作
- 实时显示选中类型计数

### 9. 关于 (AboutCommand)

显示插件版本信息与功能列表。

## 如何添加新功能

### 第一步：创建命令文件

在 `Commands/` 文件夹中新建文件（如 `MyNewCommand.cs`）：

```csharp
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Bulico
{
    [Transaction(TransactionMode.Manual)]
    public class MyNewCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,
            ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 你的业务逻辑...

            return Result.Succeeded;
        }
    }
}
```

### 第二步：准备图标

在 `Resources/` 文件夹中添加两个 PNG 图标文件：
- `icon_myfeature_32.png`（大图标，32×32）
- `icon_myfeature_16.png`（小图标，16×16）

### 第三步：注册按钮

编辑 `App.cs`，在 `OnStartup` 方法中使用 `AddPushButton` 注册：

```csharp
AddPushButton(panel, "MyInternalName", "显示文字",
    typeof(MyNewCommand).FullName,
    "鼠标悬停时显示的工具提示",
    "Bulico.Resources.icon_myfeature_32.png",
    "Bulico.Resources.icon_myfeature_16.png");
```

### 第四步：注册资源

编辑 `Bulico.csproj`，添加图标为嵌入资源：

```xml
<EmbeddedResource Include="Resources\icon_myfeature_32.png" />
<EmbeddedResource Include="Resources\icon_myfeature_16.png" />
```

### 第五步：编译

```bash
dotnet build -c Release
```

## 添加进度条

在批量操作中使用 `ProgressWindow`：

```csharp
ProgressWindow progress = new ProgressWindow("功能名称");
progress.Show();
progress.SetText("正在准备数据...");
progress.Pump();

using (Transaction trans = new Transaction(doc, "事务名称"))
{
    trans.Start();
    var items = GetItems(); // 你的数据集合
    progress.SetRange(items.Count);

    for (int i = 0; i < items.Count; i++)
    {
        // 处理每个项...
        progress.Update(i + 1, items.Count);
        progress.Pump();  // 强制刷新 UI
    }

    trans.Commit();
}

progress.Close();
```

## 添加 WPF 窗口

1. 在 `UI/` 文件夹中新建 XAML 文件（如 `MyDialog.xaml`）和 code-behind（`MyDialog.xaml.cs`）
2. 设置 `Window` 属性：`WindowStartupLocation="CenterScreen"`、`ResizeMode`
3. 在命令中调用：`new MyDialog().ShowDialog()`
4. SDK 项目会自动编译 XAML，无需手动配置

## ISelectionFilter 使用

过滤器统一放在 `Utils/` 或命令文件末尾。根据功能选择合适的过滤方式：

| 方式 | 方法 | 说明 |
|------|------|------|
| PickObjects | `ISelectionFilter` | 可控制允许选中的元素类型 |
| PickElementsByRectangle | `ISelectionFilter` | 框选过滤，不允许 Reference |
| SetElementIds | 直接设置 | 编程方式设置选中结果 |

## 版本管理

版本号在 `Properties\AssemblyInfo.cs` 中维护：

```csharp
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
```

更新版本时同步修改 `AboutCommand.cs` 中显示的内容。

## 图标设计指南

- 格式：32×32 / 16×16 PNG，透明背景
- 使用 PowerShell 脚本 `Resources\generate_icons.ps1` 可批量生成（基于 System.Drawing）

## 常见问题

### Revit 不加载插件
- 检查 `.addin` 文件中的 `Assembly` 和 `FullClassName` 是否正确
- 确保 DLL 路径正确
- 查看 Revit `Journal.log` 获取错误信息

### 图标不显示
- 检查 `.csproj` 中 `EmbeddedResource` 路径是否正确
- 检查 `App.cs` 中资源名称是否为 `命名空间.文件夹.文件名`
- 确保图标为 32×32 PNG

### 编译错误
- 确认 Revit 2020 API DLL 路径存在
- 确认安装了 .NET Framework 4.8 目标包
- 检查是否有重复类定义（根目录和子目录不要同时存在同名 .cs 文件）

### 窗口被 Revit 主窗口遮挡
- 使用 `WindowInteropHelper` 设置 Owner：

```csharp
WindowInteropHelper helper = new WindowInteropHelper(myWindow)
{
    Owner = uiapp.MainWindowHandle
};
```

## 许可证

Copyright © 2026 Bulico. All rights reserved.
