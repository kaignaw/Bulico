using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Bulico
{
    [Transaction(TransactionMode.Manual)]
    public class GridWallCommand : IExternalCommand
    {
        private static ElementId _lastWallTypeId = null;
        private static string _lastWallDocKey = null;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!CommandGuard.TryGetDocument(commandData, "框轴生墙", out UIDocument uidoc, out Document doc))
                return Result.Cancelled;

            try
            {
                WallType wallType = SelectWallType(doc);
                if (wallType == null)
                    return Result.Cancelled;

                Level baseLevel = uidoc.ActiveView is ViewPlan viewPlan
                    ? viewPlan.GenLevel
                    : new FilteredElementCollector(doc).OfClass(typeof(Level))
                        .Cast<Level>().OrderBy(l => l.Elevation).FirstOrDefault();

                if (baseLevel == null)
                {
                    TaskDialog.Show("错误", "项目中未找到标高或当前视图无关联标高。");
                    return Result.Cancelled;
                }

                List<Level> allLevels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level)).Cast<Level>()
                    .OrderBy(l => l.Elevation).ToList();

                Level topLevel = null;
                for (int i = 0; i < allLevels.Count - 1; i++)
                {
                    if (allLevels[i].Id == baseLevel.Id)
                    {
                        topLevel = allLevels[i + 1];
                        break;
                    }
                }

                GridSelectionFilter gridFilter = new GridSelectionFilter();
                HashSet<string> createdWallKeys = new HashSet<string>();
                int createdCount = 0;
                int skippedDuplicateCount = 0;

                while (true)
                {
                    ICollection<Element> picked;
                    try
                    {
                        picked = uidoc.Selection.PickElementsByRectangle(
                            gridFilter, "按住左键拖拽框选轴网（ESC退出）");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    List<Grid> selectedGrids = picked.OfType<Grid>().ToList();

                    if (selectedGrids.Count < 2)
                    {
                        TaskDialog.Show("框轴生墙", "请至少框选两条轴网。命令已结束。");
                        break;
                    }

                    List<XYZ> intersectionPoints = new List<XYZ>();
                    HashSet<string> seen = new HashSet<string>();

                    for (int i = 0; i < selectedGrids.Count; i++)
                    {
                        for (int j = i + 1; j < selectedGrids.Count; j++)
                        {
                            Curve curve1 = selectedGrids[i].Curve;
                            Curve curve2 = selectedGrids[j].Curve;

                            using (CurveIntersectResult result = curve1.Intersect(
                                curve2, CurveIntersectResultOption.Detailed))
                            {
                                if (result.Result == SetComparisonResult.Disjoint ||
                                    result.Result == SetComparisonResult.Equal)
                                    continue;

                                foreach (CurveOverlapPoint overlap in result.GetOverlaps())
                                {
                                    if (overlap.Type != CurveOverlapPointType.Intersection)
                                        continue;

                                    XYZ pt = overlap.Point;
                                    string key = $"{pt.X:F4},{pt.Y:F4}";
                                    if (seen.Add(key))
                                        intersectionPoints.Add(pt);
                                }
                            }
                        }
                    }

                    if (intersectionPoints.Count < 2)
                    {
                        TaskDialog.Show("框轴生墙", "所选轴网没有形成足够的交点，请重新框选。");
                        continue;
                    }

                    XYZ farPt1 = null, farPt2 = null;
                    double maxDist = 0;

                    for (int i = 0; i < intersectionPoints.Count; i++)
                    {
                        for (int j = i + 1; j < intersectionPoints.Count; j++)
                        {
                            double dist = intersectionPoints[i].DistanceTo(intersectionPoints[j]);
                            if (dist > maxDist)
                            {
                                maxDist = dist;
                                farPt1 = intersectionPoints[i];
                                farPt2 = intersectionPoints[j];
                            }
                        }
                    }

                    if (farPt1 == null || farPt2 == null)
                    {
                        TaskDialog.Show("框轴生墙", "无法确定轴网交点范围，请重新框选。");
                        continue;
                    }

                    if (farPt1.DistanceTo(farPt2) < doc.Application.ShortCurveTolerance)
                    {
                        TaskDialog.Show("框轴生墙", "轴网交点距离过小，无法生成墙体，请重新框选。");
                        continue;
                    }

                    string wallKey = BuildLineKey(farPt1, farPt2);
                    if (!createdWallKeys.Add(wallKey))
                    {
                        skippedDuplicateCount++;
                        continue;
                    }

                    using (Transaction trans = new Transaction(doc, "反选轴网生成墙"))
                    {
                        TransactionUtil.Prepare(trans, false);
                        trans.Start();

                        XYZ start = new XYZ(farPt1.X, farPt1.Y, 0);
                        XYZ end = new XYZ(farPt2.X, farPt2.Y, 0);
                        Line wallLine = Line.CreateBound(start, end);
                        Wall wall = Wall.Create(doc, wallLine, wallType.Id,
                            baseLevel.Id, 3000.0 / 304.8, 0, false, false);

                        if (topLevel != null)
                        {
                            Parameter topParam = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
                            if (topParam != null && !topParam.IsReadOnly)
                                topParam.Set(topLevel.Id);
                        }

                        trans.Commit();
                    }

                    createdCount++;
                }

                if (createdCount == 0 && skippedDuplicateCount == 0)
                    return Result.Cancelled;

                string summary = string.Format("已生成 {0} 面墙体。", createdCount);
                if (skippedDuplicateCount > 0)
                    summary += string.Format("\n跳过 {0} 组重复框选。", skippedDuplicateCount);

                TaskDialog.Show("框轴生墙", summary);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                ErrorReporter.Show("框轴生墙", "生成墙体失败，本次操作未保存。", ex);
                message = "生成墙体失败，请查看本地日志。";
                return Result.Failed;
            }
        }

        private static string BuildLineKey(XYZ p1, XYZ p2)
        {
            string a = string.Format("{0:F3},{1:F3}", p1.X, p1.Y);
            string b = string.Format("{0:F3},{1:F3}", p2.X, p2.Y);
            return string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
        }

        private WallType SelectWallType(Document doc)
        {
            WallType preselectedType = null;
            string docKey = DocumentScope.Key(doc);

            if (_lastWallTypeId != null && _lastWallDocKey == docKey)
            {
                Element elem = doc.GetElement(_lastWallTypeId);
                if (elem is WallType wt)
                    preselectedType = wt;
            }

            WallTypeSelector dialog = new WallTypeSelector(doc, preselectedType);
            dialog.ShowDialog();

            if (dialog.SelectedWallType != null)
            {
                _lastWallTypeId = dialog.SelectedWallType.Id;
                _lastWallDocKey = docKey;
            }

            return dialog.SelectedWallType;
        }
    }

    public class GridSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Grid;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
