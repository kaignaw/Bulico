using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DimView = Autodesk.Revit.DB.View;

namespace Bulico
{
    [Transaction(TransactionMode.Manual)]
    public class ElevationGridMarkCommand : IExternalCommand
    {
        private static ElementId _lastDimTypeId = null;
        private static string _lastDimDocKey = null;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!CommandGuard.TryGetDocument(commandData, "立面轴网标注", out UIDocument uidoc, out Document doc))
                return Result.Cancelled;

            DimView view = doc.ActiveView;
            if (view == null)
            {
                TaskDialog.Show("立面轴网标注", "当前没有可用的活动视图。");
                return Result.Cancelled;
            }

            if (view.ViewType != ViewType.Elevation && view.ViewType != ViewType.Section)
            {
                TaskDialog.Show("提示", "请在立面视图或剖面视图中使用此命令。");
                return Result.Cancelled;
            }

            DimensionType dimType = SelectDimensionType(doc);
            if (dimType == null) return Result.Cancelled;

            List<Grid> selectedGrids = SelectGrids(uidoc);
            if (selectedGrids == null)
                return Result.Cancelled;

            if (selectedGrids.Count < 2)
            {
                TaskDialog.Show("提示", "请至少选择两条轴网。");
                return Result.Cancelled;
            }

            XYZ pickPoint;
            try
            {
                pickPoint = uidoc.Selection.PickPoint(
                    ObjectSnapTypes.Endpoints, "点击尺寸线生成位置");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            using (Transaction trans = new Transaction(doc, "立面轴网标注"))
            {
                TransactionUtil.Prepare(trans, false);
                trans.Start();
                try
                {
                    if (!CreateDimension(doc, view, selectedGrids, pickPoint, dimType))
                    {
                        trans.RollBack();
                        return Result.Cancelled;
                    }
                }
                catch (Exception ex)
                {
                    ErrorReporter.Log("立面轴网标注", ex);
                    TaskDialog.Show("立面轴网标注", "创建标注失败，本次操作已回滚。\n\n详细信息已写入本地日志：\n" + ErrorReporter.LogPath);
                    trans.RollBack();
                    return Result.Failed;
                }
                trans.Commit();
            }

            return Result.Succeeded;
        }

        private DimensionType SelectDimensionType(Document doc)
        {
            DimensionType preselectedType = null;
            string docKey = DocumentScope.Key(doc);

            if (_lastDimTypeId != null && _lastDimDocKey == docKey)
            {
                Element elem = doc.GetElement(_lastDimTypeId);
                if (elem is DimensionType dt)
                    preselectedType = dt;
            }

            DimTypeSelector dialog = new DimTypeSelector(doc, preselectedType);
            dialog.ShowDialog();

            if (dialog.SelectedDimType != null)
            {
                _lastDimTypeId = dialog.SelectedDimType.Id;
                _lastDimDocKey = docKey;
            }

            return dialog.SelectedDimType;
        }

        private List<Grid> SelectGrids(UIDocument uidoc)
        {
            try
            {
                ICollection<Element> picked = uidoc.Selection.PickElementsByRectangle(
                    new GridSelectionFilter(),
                    "框选轴网（左键拖拽，松开即完成选择）");
                return picked.OfType<Grid>().ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }

        private bool CreateDimension(
            Document doc, DimView view,
            List<Grid> grids, XYZ pickPoint,
            DimensionType dimType)
        {
            var gridRefPairs = new List<Tuple<Grid, Reference>>();
            foreach (Grid g in grids)
            {
                if (g.Curve == null) continue;

                Reference curveRef = GetGridCurveReference(g, view);
                if (curveRef != null)
                    gridRefPairs.Add(Tuple.Create(g, curveRef));
            }

            if (gridRefPairs.Count < 2)
            {
                TaskDialog.Show("立面轴网标注", "选中的轴网在当前视图中无法引用，有效轴网少于两条。");
                return false;
            }

            Curve firstCurve = gridRefPairs[0].Item1.Curve;
            XYZ gridDir = GetCurveDirection(firstCurve);
            if (gridDir.GetLength() < 1e-9)
                gridDir = XYZ.BasisZ;

            XYZ dimDir = new XYZ(-gridDir.Y, gridDir.X, 0);
            if (dimDir.GetLength() < 1e-9)
                dimDir = XYZ.BasisX;
            dimDir = dimDir.Normalize();

            var sorted = new List<Tuple<double, Grid, Reference>>();
            foreach (var pair in gridRefPairs)
            {
                // 点不在轴线的可投影范围内时 Project 会返回 null。
                IntersectionResult proj = pair.Item1.Curve.Project(pickPoint);
                if (proj == null || proj.XYZPoint == null)
                    continue;

                double param = proj.XYZPoint.DotProduct(dimDir);
                sorted.Add(Tuple.Create(param, pair.Item1, pair.Item2));
            }

            if (sorted.Count < 2)
            {
                TaskDialog.Show("立面轴网标注", "轴网无法投影到点击位置，请重新选择尺寸线位置。");
                return false;
            }

            sorted.Sort((a, b) => a.Item1.CompareTo(b.Item1));

            double pickDot = pickPoint.DotProduct(dimDir);

            XYZ dimStart = pickPoint + (sorted[0].Item1 - pickDot) * dimDir;
            XYZ dimEnd = pickPoint + (sorted[sorted.Count - 1].Item1 - pickDot) * dimDir;

            if (dimStart.DistanceTo(dimEnd) < doc.Application.ShortCurveTolerance)
            {
                TaskDialog.Show("立面轴网标注", "轴网间距过小，无法创建标注。");
                return false;
            }

            Line dimLine = Line.CreateBound(dimStart, dimEnd);

            ReferenceArray refArr = new ReferenceArray();
            foreach (var s in sorted)
                refArr.Append(s.Item3);

            Dimension dim = doc.Create.NewDimension(view, dimLine, refArr);
            if (dim != null && dimType != null)
                dim.DimensionType = dimType;

            return true;
        }

        private Reference GetGridCurveReference(Grid grid, DimView view)
        {
            try
            {
                Options opts = new Options
                {
                    View = view,
                    ComputeReferences = true
                };
                GeometryElement geoElem = grid.get_Geometry(opts);
                if (geoElem != null)
                {
                    foreach (GeometryObject obj in geoElem)
                    {
                        if (obj is Curve curve && curve.Reference != null)
                            return curve.Reference;
                    }
                }
            }
            catch
            {
            }

            try
            {
                Reference gridRef = grid.Curve.Reference;
                if (gridRef != null) return gridRef;
            }
            catch
            {
            }

            return null;
        }

        private XYZ GetCurveDirection(Curve curve)
        {
            if (curve is Line line)
                return line.Direction;

            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);
            XYZ dir = p1 - p0;
            double len = dir.GetLength();
            return len > 1e-9 ? dir / len : XYZ.Zero;
        }
    }

}
