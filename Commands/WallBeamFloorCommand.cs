using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Bulico
{
    [Transaction(TransactionMode.Manual)]
    public class WallBeamFloorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!CommandGuard.TryGetDocument(commandData, "墙梁建板", out UIDocument uidoc, out Document doc))
                return Result.Cancelled;

            FilteredElementCollector floorTypeCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType));
            List<FloorType> floorTypes = floorTypeCollector.Cast<FloorType>().ToList();

            if (floorTypes.Count == 0)
            {
                TaskDialog.Show("墙梁建板", "项目中无可用楼板类型！");
                return Result.Cancelled;
            }

            FilteredElementCollector levelCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(Level));
            List<Level> levels = levelCollector.Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            if (levels.Count == 0)
            {
                TaskDialog.Show("墙梁建板", "项目中无标高！");
                return Result.Cancelled;
            }

            FloorSettings settings = FloorSettingsDialog.Show(
                doc, "墙梁建板", floorTypes, levels, showModeOption: true);
            if (settings == null)
                return Result.Cancelled;

            FloorType selectedType = settings.FloorType;
            Level selectedLevel = settings.Level;
            double selectedOffset = settings.OffsetMeters;
            bool useCenterlineMode = settings.UseCenterline;

            ProgressWindow progress = null;
            try
            {
                IList<Reference> selectedRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new WallBeamFilter(),
                    "框选或点选墙和梁（仅能选中墙和梁）");

                if (selectedRefs == null || selectedRefs.Count == 0)
                {
                    TaskDialog.Show("墙梁建板", "未选中任何构件！");
                    return Result.Cancelled;
                }

                List<Curve> curves = new List<Curve>();
                foreach (Reference selRef in selectedRefs)
                {
                    Element elem = doc.GetElement(selRef);
                    if (elem == null) continue;

                    Curve curve = GetCenterCurve(elem, doc);
                    if (curve != null)
                        curves.Add(curve);
                }

                if (curves.Count < 3)
                {
                    TaskDialog.Show("墙梁建板", "选中的构件无法形成闭合区域（至少需要3条线）！");
                    return Result.Cancelled;
                }

                if (curves.Count > InputLimits.MaxRegionCurves)
                {
                    TaskDialog.Show(
                        "墙梁建板",
                        string.Format(
                            "单次最多处理 {0} 条墙/梁线，当前选择了 {1} 条。请分批处理。",
                            InputLimits.MaxRegionCurves,
                            curves.Count));
                    return Result.Cancelled;
                }

                progress = new ProgressWindow("墙梁建板");
                progress.Show();
                progress.SetText("正在计算闭合区域...");
                progress.Pump();

                List<CurveLoop> regions = RegionFinder.FindClosedRegions(
                    curves, doc.Application.ShortCurveTolerance);

                if (regions.Count == 0)
                {
                    progress.Finish();

                    TaskDialog.Show("墙梁建板", "未找到任何闭合区域！请确保墙或梁围成了封闭空间。");
                    return Result.Cancelled;
                }

                if (!useCenterlineMode)
                {
                    double halfThick = ComputeAverageHalfThickness(selectedRefs, doc);
                    if (halfThick > doc.Application.ShortCurveTolerance)
                    {
                        List<CurveLoop> insetRegions = new List<CurveLoop>();
                        foreach (var loop in regions)
                        {
                            try
                            {
                                CurveLoop offset = CurveLoop.CreateViaOffset(loop, -halfThick, XYZ.BasisZ);
                                if (offset != null && !offset.IsOpen())
                                    insetRegions.Add(offset);
                                else
                                    insetRegions.Add(loop);
                            }
                            catch
                            {
                                insetRegions.Add(loop);
                            }
                        }
                        regions = insetRegions;
                    }
                }

                progress.SetText("正在创建楼板...");

                int count = 0;
                bool createFailed = false;
                Exception createException = null;

                using (Transaction trans = new Transaction(doc, "墙梁建板"))
                {
                    TransactionUtil.Prepare(trans, false);
                    trans.Start();
                    progress.SetRange(regions.Count);
                    for (int fi = 0; fi < regions.Count; fi++)
                    {
                        try
                        {
                            Floor floor = Floor.Create(
                                doc,
                                new List<CurveLoop> { regions[fi] },
                                selectedType.Id,
                                selectedLevel.Id);
                            if (floor == null)
                                throw new InvalidOperationException("楼板创建失败。");

                            Parameter offsetParam = floor.get_Parameter(
                                BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                            if (offsetParam != null)
                                offsetParam.Set(UnitUtils.ConvertToInternalUnits(selectedOffset, UnitTypeId.Meters));

                            count++;
                        }
                        catch (Exception ex)
                        {
                            createFailed = true;
                            createException = ex;
                            break;
                        }
                        progress.Update(fi + 1, regions.Count);
                    }

                    if (createFailed)
                        trans.RollBack();
                    else
                        trans.Commit();
                }

                progress.Finish();

                if (createFailed)
                {
                    if (createException != null)
                        ErrorReporter.Log("墙梁建板", createException);

                    TaskDialog.Show("墙梁建板", "创建楼板失败，本次操作已全部回滚。\n\n详细信息已写入本地日志：\n" + ErrorReporter.LogPath);
                    return Result.Failed;
                }

                TaskDialog.Show("墙梁建板", string.Format("成功创建 {0} 个楼板！", count));
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                ErrorReporter.Show("墙梁建板", "墙梁建板失败，本次操作未保存。", ex);
                message = "墙梁建板失败，请查看本地日志。";
                return Result.Failed;
            }
            finally
            {
                if (progress != null)
                    progress.Dispose();
            }
        }

        private Curve GetCenterCurve(Element elem, Document doc)
        {
            Wall wall = elem as Wall;
            if (wall != null)
            {
                LocationCurve locCurve = wall.Location as LocationCurve;
                if (locCurve == null) return null;

                Curve baseCurve = locCurve.Curve;
                if (baseCurve == null) return null;

                try
                {
                    CompoundStructure cs = wall.WallType.GetCompoundStructure();
                    if (cs != null)
                    {
                        double offset = cs.GetOffsetForLocationLine(WallLocationLine.CoreCenterline);
                        if (Math.Abs(offset) > doc.Application.ShortCurveTolerance)
                        {
                            Line line = baseCurve as Line;
                            if (line != null)
                            {
                                XYZ dir = (line.GetEndPoint(1) - line.GetEndPoint(0)).Normalize();
                                return baseCurve.CreateOffset(offset, dir);
                            }
                        }
                    }
                }
                catch
                {
                }

                return baseCurve;
            }
            else
            {
                FamilyInstance fi = elem as FamilyInstance;
                if (fi != null)
                {
                    LocationCurve locCurve = fi.Location as LocationCurve;
                    if (locCurve == null) return null;
                    return locCurve.Curve;
                }
            }

            return null;
        }

        private double ComputeAverageHalfThickness(IList<Reference> refs, Document doc)
        {
            double total = 0;
            int count = 0;
            foreach (var r in refs)
            {
                Element e = doc.GetElement(r);
                Wall wall = e as Wall;
                if (wall != null)
                {
                    total += wall.WallType.Width;
                    count++;
                    continue;
                }
                FamilyInstance fi = e as FamilyInstance;
                if (fi != null && fi.StructuralType == StructuralType.Beam)
                {
                    FamilySymbol symbol = fi.Symbol;
                    if (symbol == null)
                        continue;

                    double bw = 0;
                    Parameter p = symbol.LookupParameter("B");
                    if (p != null) bw = p.AsDouble();
                    if (bw < doc.Application.ShortCurveTolerance)
                    {
                        Parameter p2 = symbol.LookupParameter("b");
                        if (p2 != null) bw = p2.AsDouble();
                    }
                    if (bw > doc.Application.ShortCurveTolerance)
                    {
                        total += bw; count++;
                    }
                }
            }
            if (count == 0) return 0;
            return total / count / 2.0;
        }
    }
}
