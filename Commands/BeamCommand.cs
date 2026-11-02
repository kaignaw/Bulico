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
    public class BeamCommand : IExternalCommand
    {
        private static ElementId _lastBeamTypeId = null;
        private static string _lastBeamDocKey = null;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!CommandGuard.TryGetDocument(commandData, "框墙生梁", out UIDocument uidoc, out Document doc))
                return Result.Cancelled;

            ProgressWindow progress = null;
            try
            {
                FamilySymbol beamType = SelectBeamType(doc);
                if (beamType == null)
                    return Result.Cancelled;

                WallSelectionFilter wallFilter = new WallSelectionFilter();
                IList<Reference> selectedRefs;

                try
                {
                    selectedRefs = uidoc.Selection.PickObjects(
                        ObjectType.Element, wallFilter, "框选或点选墙体（仅可选择墙体，ESC退出）");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (selectedRefs == null || selectedRefs.Count == 0)
                    return Result.Cancelled;

                var walls = selectedRefs
                    .Select(r => doc.GetElement(r.ElementId))
                    .OfType<Wall>()
                    .Where(w => w.WallType.Kind == WallKind.Basic)
                    .ToList();

                if (walls.Count == 0)
                {
                    TaskDialog.Show("提示", "未选中任何基本墙体。");
                    return Result.Cancelled;
                }

                // 标高列表只收集一次，避免每面墙都做一次全模型扫描。
                List<Level> levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .ToList();

                if (levels.Count == 0)
                {
                    TaskDialog.Show("框墙生梁", "项目中无可用标高。");
                    return Result.Cancelled;
                }

                progress = new ProgressWindow("框墙生梁");
                progress.Show();
                progress.SetText("正在生成梁...");
                progress.Pump();

                bool hasError = false;
                int failedCount = 0;
                int completedCount = 0;
                string failureReason = null;
                using (Transaction trans = new Transaction(doc, "框墙生梁"))
                {
                    TransactionUtil.Prepare(trans, false);
                    trans.Start();

                    if (!beamType.IsActive)
                        beamType.Activate();

                    progress.SetRange(walls.Count);

                    for (int i = 0; i < walls.Count; i++)
                    {
                        Wall wall = walls[i];
                        try
                        {
                            Curve beamCurve = GetBeamCurve(wall, doc);
                            if (beamCurve == null)
                            {
                                failedCount++;
                                hasError = true;
                                failureReason = string.Format(
                                    "墙体 {0} 无法计算梁定位线（缺少定位曲线或墙体定位参数）。",
                                    wall.Id.Value);
                                break;
                            }

                            double? topElevation = GetWallTopElevation(wall, doc);
                            if (!topElevation.HasValue)
                            {
                                failedCount++;
                                hasError = true;
                                failureReason = string.Format(
                                    "墙体 {0} 无法确定顶部标高（缺少高度或顶部约束参数）。",
                                    wall.Id.Value);
                                break;
                            }

                            Level beamLevel = FindClosestLevel(levels, topElevation.Value);
                            if (beamLevel == null)
                            {
                                failedCount++;
                                hasError = true;
                                failureReason = "项目中无可用标高。";
                                break;
                            }

                            XYZ startPt = beamCurve.GetEndPoint(0);
                            XYZ endPt = beamCurve.GetEndPoint(1);
                            Curve elevatedCurve = Line.CreateBound(
                                new XYZ(startPt.X, startPt.Y, topElevation.Value),
                                new XYZ(endPt.X, endPt.Y, topElevation.Value));

                            doc.Create.NewFamilyInstance(elevatedCurve, beamType, beamLevel, StructuralType.Beam);
                            completedCount++;
                        }
                        catch (Exception ex)
                        {
                            failedCount++;
                            hasError = true;
                            failureReason = string.Format("墙体 {0} 生成梁时出错。", wall.Id.Value);
                            ErrorReporter.Log("框墙生梁", ex);
                            break;
                        }

                        progress.Update(i + 1, walls.Count);
                    }

                    if (hasError)
                        trans.RollBack();
                    else
                        trans.Commit();
                }

                if (progress != null)
                    progress.Finish();

                if (hasError)
                {
                    TaskDialog.Show(
                        "框墙生梁",
                        string.Format(
                            "{0}\n本次操作已全部回滚（已完成 {1} 面，失败 {2} 面）。",
                            failureReason ?? "生成梁失败。",
                            completedCount,
                            failedCount));
                    return Result.Failed;
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                ErrorReporter.Show("框墙生梁", "生成梁失败，本次操作未保存。", ex);
                message = "生成梁失败，请查看本地日志。";
                return Result.Failed;
            }
            finally
            {
                if (progress != null)
                    progress.Dispose();
            }
        }

        private Curve GetBeamCurve(Wall wall, Document doc)
        {
            LocationCurve loc = wall.Location as LocationCurve;
            if (loc == null) return null;

            Curve curve = loc.Curve;
            if (curve == null) return null;

            CompoundStructure cs = wall.WallType.GetCompoundStructure();
            if (cs == null) return curve;

            try
            {
                Parameter locLineParam = wall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM);
                WallLocationLine wallLocLine = locLineParam != null
                    ? (WallLocationLine)locLineParam.AsInteger()
                    : WallLocationLine.CoreCenterline;

                double offsetForLocationLine = cs.GetOffsetForLocationLine(wallLocLine);

                int firstCoreLayer = cs.GetFirstCoreLayerIndex();
                int lastCoreLayer = cs.GetLastCoreLayerIndex();

                double coreWidth = 0;
                if (firstCoreLayer >= 0 && lastCoreLayer > firstCoreLayer)
                {
                    for (int i = firstCoreLayer; i <= lastCoreLayer; i++)
                    {
                        coreWidth += cs.GetLayerWidth(i);
                    }
                }

                if (coreWidth <= 0) return curve;

                double coreCenterOffset = offsetForLocationLine - coreWidth / 2;
                if (Math.Abs(coreCenterOffset) < 0.001) return curve;

                XYZ curveDir = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
                if (curveDir.GetLength() < 1e-9) return curve;

                XYZ leftPerp = new XYZ(-curveDir.Y, curveDir.X, 0);

                XYZ exteriorDir = wall.Orientation.CrossProduct(XYZ.BasisZ).Normalize();
                if (exteriorDir.DotProduct(leftPerp) < 0)
                    exteriorDir = -exteriorDir;

                double absOffset = Math.Abs(coreCenterOffset);
                XYZ offsetDir = coreCenterOffset > 0 ? exteriorDir : -exteriorDir;

                return curve.CreateOffset(absOffset, offsetDir);
            }
            catch (Exception ex)
            {
                // 复合结构信息不完整时退回墙定位线，不中断整组操作。
                ErrorReporter.Log("框墙生梁-计算梁定位线", ex);
                return curve;
            }
        }

        private double? GetWallTopElevation(Wall wall, Document doc)
        {
            double baseElev = 0;
            Parameter baseLevelParam = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
            if (baseLevelParam != null)
            {
                Level baseLevel = doc.GetElement(baseLevelParam.AsElementId()) as Level;
                if (baseLevel != null)
                    baseElev = baseLevel.Elevation;
            }

            double baseOffset = 0;
            Parameter baseOffsetParam = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET);
            if (baseOffsetParam != null)
                baseOffset = baseOffsetParam.AsDouble();

            Parameter topLevelParam = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
            ElementId topLevelId = topLevelParam != null
                ? topLevelParam.AsElementId()
                : ElementId.InvalidElementId;

            if (topLevelId != null && topLevelId != ElementId.InvalidElementId)
            {
                Level topLevel = doc.GetElement(topLevelId) as Level;
                if (topLevel != null)
                {
                    double topOffset = 0;
                    Parameter topOffsetParam = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET);
                    if (topOffsetParam != null)
                        topOffset = topOffsetParam.AsDouble();

                    return topLevel.Elevation + topOffset;
                }
            }

            Parameter heightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
            if (heightParam == null)
                return null;

            return baseElev + baseOffset + heightParam.AsDouble();
        }

        private static Level FindClosestLevel(List<Level> levels, double elevation)
        {
            Level best = null;
            double bestDistance = double.MaxValue;

            foreach (Level level in levels)
            {
                double distance = Math.Abs(level.Elevation - elevation);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = level;
                }
            }

            return best;
        }

        private FamilySymbol SelectBeamType(Document doc)
        {
            FamilySymbol preselectedType = null;
            string docKey = DocumentScope.Key(doc);

            if (_lastBeamTypeId != null && _lastBeamDocKey == docKey)
            {
                Element elem = doc.GetElement(_lastBeamTypeId);
                FamilySymbol fs = elem as FamilySymbol;
                if (fs != null)
                    preselectedType = fs;
            }

            BeamTypeSelector dialog = new BeamTypeSelector(doc, preselectedType);
            dialog.ShowDialog();

            if (dialog.SelectedBeamType != null)
            {
                _lastBeamTypeId = dialog.SelectedBeamType.Id;
                _lastBeamDocKey = docKey;
            }

            return dialog.SelectedBeamType;
        }
    }

    public class WallSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Wall;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }
}
