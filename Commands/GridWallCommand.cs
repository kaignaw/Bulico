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

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

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
                        break;

                    List<XYZ> intersectionPoints = new List<XYZ>();
                    HashSet<string> seen = new HashSet<string>();

                    for (int i = 0; i < selectedGrids.Count; i++)
                    {
                        for (int j = i + 1; j < selectedGrids.Count; j++)
                        {
                            Curve curve1 = selectedGrids[i].Curve;
                            Curve curve2 = selectedGrids[j].Curve;

                            IntersectionResultArray results;
                            SetComparisonResult result = curve1.Intersect(curve2, out results);

                            if (result == SetComparisonResult.Overlap && results != null && results.Size > 0)
                            {
                                foreach (IntersectionResult ir in results)
                                {
                                    XYZ pt = ir.XYZPoint;
                                    string key = $"{pt.X:F4},{pt.Y:F4}";
                                    if (seen.Add(key))
                                        intersectionPoints.Add(pt);
                                }
                            }
                        }
                    }

                    if (intersectionPoints.Count < 2)
                        continue;

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

                    using (Transaction trans = new Transaction(doc, "反选轴网生成墙"))
                    {
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
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private WallType SelectWallType(Document doc)
        {
            WallType preselectedType = null;

            if (_lastWallTypeId != null)
            {
                Element elem = doc.GetElement(_lastWallTypeId);
                if (elem is WallType wt)
                    preselectedType = wt;
            }

            WallTypeSelector dialog = new WallTypeSelector(doc, preselectedType);
            dialog.ShowDialog();

            if (dialog.SelectedWallType != null)
                _lastWallTypeId = dialog.SelectedWallType.Id;

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
