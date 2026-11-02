# AGENTS.md

Bulico is a **Revit 2026.5+** add-in (.NET 10, x64, WPF). Revit 2026.0–2026.4 use .NET 8 and are not targeted by this branch. There is no test project, CI, or lint/format config; compiling is the only automated verification.

## Build

```bash
dotnet build -c Release
```

- Requires .NET SDK 10.0+ and Revit 2026 installed at `C:\Program Files\Autodesk\Revit 2026\` (the API references are taken from there; override with `-p:RevitInstallDir=...` if needed).
- Output: `bin\Release\net10.0-windows\Bulico.dll`.
- You cannot run or functionally test the add-in without Revit 2026.5; a clean build is the expected check.
- MSB3277 reference-version warnings from Autodesk's own assemblies are benign; do not copy `RevitAPI*.dll` to output (`Private=False`).

## Project layout

- `App.cs` is the `IExternalApplication` entrypoint; it creates the ribbon panel and registers every button.
- `Commands/` holds `IExternalCommand` implementations, `UI/` holds WPF windows, `Utils/` holds shared helpers (`RegionFinder`, filters, `ProgressWindow`).
- All code uses the flat `namespace Bulico`, regardless of folder.
- SDK-style default globbing is enabled, so new `.cs` and `.xaml` files are picked up automatically. **Icons are the exception:** every 32/16 px PNG must have its own `<EmbeddedResource>` entry in `Bulico.csproj`. Resource names are `Bulico.Resources.icon_*.png`.

## Adding or changing a command

Keep these in sync — missing any one produces a half-working feature:

1. Implement the command in `Commands/` with `[Transaction(TransactionMode.Manual)]`.
2. Register the button in `App.OnStartup` (`AddPushButton`), including an icon pair.
3. Add 32/16 px icons under `Resources/` and both `EmbeddedResource` entries in `Bulico.csproj`.
4. Update the feature table and project tree in `README.md`, and the feature list in `Commands/AboutCommand.cs`.

## Revit 2026 API notes

Revit 2026 removed several legacy APIs. Do not reintroduce them:

- `ElementId.IntegerValue` and `ElementId(int)` are gone. Use `ElementId.Value` (`long`) and `new ElementId(long)`.
- `DisplayUnitType` is gone. Use `UnitTypeId`, e.g. `UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Meters)`.
- `Document.Create.NewFloor` is obsolete. Use `Floor.Create(doc, IList<CurveLoop>, floorTypeId, levelId)`; its profile elevation is ignored.
- `Curve.Intersect(curve, out IntersectionResultArray)` is deprecated. Use `curve.Intersect(other, CurveIntersectResultOption.Detailed)` and read `CurveOverlapPoint.Point`.
- Commands create their own `Transaction` (manual mode); keep dialogs and selections outside active transactions.
- User-facing text (TaskDialog, window labels, ribbon tooltips) is Chinese; keep new UI strings Chinese.
- Long operations use `Utils/ProgressWindow.cs` with `SetRange` / `Update` / `Pump`.

## Versioning and release

- `Properties/AssemblyInfo.cs` (`AssemblyVersion` / `AssemblyFileVersion`) and the `V1.x.yy` string in `Commands/AboutCommand.cs` must match.
- `ReleasePackage/` contains committed release binaries (`Bulico.dll`, `Bulico.pdb`, `Bulico.addin`). Do not hand-edit them; refresh from build output only when preparing a release.
- Deploy by copying `Bulico.dll` and `Bulico.addin` to `%AppData%\Autodesk\Revit\Addins\2026\`.
