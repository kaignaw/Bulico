# AGENTS.md

Bulico is a **Revit 2026.5+** add-in (.NET 10, x64, WPF). Revit 2026.0–2026.4 use .NET 8 and are not targeted by this mainline. There is no test project, CI, or lint/format config; compiling is the only automated verification.

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
- SDK-style default globbing is enabled, so new `.cs` and `.xaml` files are picked up automatically.
- `Brand/Icons/` contains the command icon system: SVG sources in `src/`, generated 32/16 px PNGs, `generate-icons.mjs`, and a `preview.html`. PNGs must be listed explicitly as `<EmbeddedResource>` in `Bulico.csproj`; resource names are `Bulico.Brand.Icons.<name>_<size>.png`.

## Visual identity

All visual assets live in `Brand/`; read `Brand/README.md` before adding or changing logo/icon work.

- Grid Stack: three rounded isometric floor plates; top solid Teal, middle full Ink, bottom 50% Ink; no core hole. Keep left/right symmetry and visually equal gaps — the logo compensates for the top plate having no stroke.
- Palette: Ink `#0B1727`, Teal `#14B8A6`, Paper `#F8FAFC`. Do not reintroduce the old multicolor icon tiles.
- Command icons: 32×32 dark Ink tile, white line art, one Teal accent; 4 px safe area; generated to 32/16 px from SVG.
- Use the supplied dark/mono logo variants for dark backgrounds; keep the lockup free of version text.

## Command icons

- Visual system: 32 × 32 canvas, dark Ink tile `#0B1727` (30 × 30, `rx=7`), white line art `#F8FAFC`, Teal accent `#14B8A6`.
- Edit SVGs in `Brand/Icons/src/`; never hand-edit the generated PNGs.
- Regenerate both sizes with:

```bash
cd Brand/Icons
npm install
npm run build
```

- Preview the set at `Brand/Icons/preview.html`.
- Resource names follow `Bulico.Brand.Icons.<name>_<size>.png`; `App.cs` builds them through its `Icon(name, size)` helper.

## Adding or changing a command

Keep these in sync — missing any one produces a half-working feature:

1. Implement the command in `Commands/` with `[Transaction(TransactionMode.Manual)]`.
2. Register the button in `App.OnStartup` (`AddPushButton`) with an icon pair from `Icon("<name>", 32/16)`.
3. Add the SVG source under `Brand/Icons/src/`, run `npm run build` in `Brand/Icons`, and add both generated PNGs as `<EmbeddedResource>` entries in `Bulico.csproj`.
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
- `ReleasePackage/` is a local-only staging folder for release binaries (`Bulico.dll`, `Bulico.pdb`, `Bulico.addin`). It is ignored by Git and must not be committed. Do not hand-edit the binaries; refresh them from build output only when preparing a release.
- Deploy by copying `Bulico.dll` and `Bulico.addin` to `%AppData%\Autodesk\Revit\Addins\2026\`.
