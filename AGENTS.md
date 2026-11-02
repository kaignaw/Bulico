# AGENTS.md

Bulico 是一套 **Revit 2026.5+** 插件（.NET 10、x64、WPF）。Revit 2026.0–2026.4 使用 .NET 8，不属于本主线。仓库没有测试项目、CI、lint/格式化配置，**编译通过是唯一的自动化验证**，不要在无 Revit 环境时声称功能已验证。

## 编译

```bash
dotnet build -c Release
```

- 需要 .NET SDK 10.0+，并默认从 `C:\Program Files\Autodesk\Revit 2026\` 引用 `RevitAPI.dll` / `RevitAPIUI.dll`；路径不同时用 `-p:RevitInstallDir=<path>`。
- 产物：`bin\Release\net10.0-windows\Bulico.dll`。
- 新克隆上首次不要先执行 `dotnet clean`（缺少 `obj/project.assets.json` 会失败），直接 build 即可（会隐式 restore）。
- Autodesk 程序集产生的 MSB3277 版本警告可忽略；`RevitAPI*.dll` 必须保持 `Private=False`，不要复制到输出目录。

## 工程结构

- `App.cs` 是 `IExternalApplication` 入口，创建 Ribbon 面板并注册全部按钮（`AddPushButton` + `Icon(name, size)`）。
- `Commands/` 放 `IExternalCommand`，`UI/` 放 WPF 窗口，`Utils/` 放共享逻辑（`RegionFinder`、选择过滤器、`ProgressWindow`、`CommandGuard`、`ErrorReporter`、`InputLimits`、`DocumentScope`、`TransactionUtil`、`FloorSettingsCache`）。
- 所有 `.cs` 都在扁平命名空间 `Bulico` 下，不随目录分层。
- `Bulico.csproj` 开启了 SDK 默认通配，新增 `.cs` / `.xaml` 无需手工登记；但 PNG 图标必须显式写成 `<EmbeddedResource>`。
- `ImplicitUsings` 与 `Nullable` 均为 disable：每个文件要写完整 `using`，代码按可空关闭处理。
- `GenerateAssemblyInfo=false`，版本只来自 `Properties/AssemblyInfo.cs`。

## 新增或修改命令

以下步骤缺一不可，否则会出现“按钮能点但没图标/没入口”的半成品：

1. 在 `Commands/` 实现命令，标注 `[Transaction(TransactionMode.Manual)]`。
2. 在 `App.OnStartup` 中注册按钮，图标用 `Icon("<name>", 32)` 与 `Icon("<name>", 16)`。
3. 在 `Brand/Icons/src/` 增加同名 SVG，重新生成 PNG，并在 `Bulico.csproj` 添加两条 `<EmbeddedResource>`。
4. 同步 `README.md` 的功能表/项目树，以及 `Commands/AboutCommand.cs` 里的功能列表。
5. 用户可见文案（TaskDialog、窗口、Ribbon 提示）一律用中文。
6. 命令入口统一用 `CommandGuard.TryGetDocument` 判空；异常用 `ErrorReporter` 写本地日志（`%LOCALAPPDATA%\Bulico\logs\error.log`），不要向用户直接显示 `ex.Message`。输入上限集中在 `InputLimits`；批量创建失败必须整组回滚。

## 图标生成

```bash
cd Brand/Icons
npm install
npm run build   # node generate-icons.mjs，输出 32/16 px PNG
```

- 只改 `Brand/Icons/src/` 下的 SVG，禁止手改生成的 PNG。
- 视觉规范：32×32 画布、Ink 圆角底板 `#0B1727`（30×30，`rx=7`）、白色线稿 `#F8FAFC`、单一 Teal 强调 `#14B8A6`、四周 4 px 安全区。
- 资源名固定为 `Bulico.Brand.Icons.<name>_<size>.png`，靠 `App.cs` 的 `Icon()` 拼接。
- 预览：`Brand/Icons/preview.html`。Logo 与整体视觉规则见 `Brand/README.md`，不要恢复旧的彩色图标底。

## Revit 2026 API 注意事项

以下旧 API 已移除/废弃，不要重新引入：

- `ElementId.IntegerValue`、`ElementId(int)` → 用 `ElementId.Value`（`long`）和 `new ElementId(long)`。
- `DisplayUnitType` → 用 `UnitTypeId`，如 `UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Meters)`。
- `Document.Create.NewFloor` → 用 `Floor.Create(doc, IList<CurveLoop>, floorTypeId, levelId)`；其轮廓高程会被忽略。
- `Curve.Intersect(curve, out IntersectionResultArray)` → 用 `curve.Intersect(other, CurveIntersectResultOption.Detailed)` 并读取 `CurveOverlapPoint.Point`。
- 命令自己开 `Transaction`；对话框与选择操作必须放在事务之外。
- 耗时操作使用 `Utils/ProgressWindow.cs` 的 `SetRange` / `Update` / `Pump` 维持界面响应。

## 版本与发布

- `Properties/AssemblyInfo.cs` 的 `AssemblyVersion` / `AssemblyFileVersion`（如 `1.3.0.0`）必须与 `AboutCommand.cs` 中的 `V1.x.yy`（如 `V1.3.00`）一致。
- `ReleasePackage/` 是本地暂存的发布目录（`Bulico.dll`、`Bulico.pdb`、`Bulico.addin`），已被 `.gitignore` 忽略，**不要提交**；也不要手改其中二进制，只在发版时从 `bin\Release\net10.0-windows\` 复制。
- 部署：把 `Bulico.dll` 和 `Bulico.addin` 复制到 `%AppData%\Autodesk\Revit\Addins\2026\`，重启 Revit 后在独立的“Bulico”选项卡查看；面板分为“建模”“注释”“效率”“其他”四组。
