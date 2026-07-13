using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Bulico
{
    [Transaction(TransactionMode.Manual)]
    public class MullionCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds.Count == 0)
            {
                TaskDialog.Show("提示", "请先选择幕墙！");
                return Result.Cancelled;
            }

            List<Wall> curtainWalls = new List<Wall>();
            foreach (ElementId id in selectedIds)
            {
                Element elem = doc.GetElement(id);
                if (elem is Wall wall && wall.WallType.Kind == WallKind.Curtain)
                    curtainWalls.Add(wall);
            }

            if (curtainWalls.Count == 0)
            {
                TaskDialog.Show("提示", "未选择幕墙！");
                return Result.Cancelled;
            }

            MullionWindow window = new MullionWindow();
            WindowInteropHelper helper = new WindowInteropHelper(window)
            {
                Owner = uiapp.MainWindowHandle
            };

            bool? dialogResult = window.ShowDialog();
            if (dialogResult != true)
                return Result.Cancelled;

            List<double> offsets = ParseInput(window.InputText);
            if (offsets.Count == 0)
            {
                TaskDialog.Show("提示", "输入格式无效！请检查输入值。");
                return Result.Cancelled;
            }

            bool fromBottom = window.IsFromBottom;

            using (Transaction trans = new Transaction(doc, "一键横挺"))
            {
                trans.Start();
                int gridCount = 0;

                foreach (Wall wall in curtainWalls)
                {
                    gridCount += CreateHorizontalGridLines(doc, wall, offsets, fromBottom);
                }

                trans.Commit();

                TaskDialog.Show("完成", $"已为 {curtainWalls.Count} 面幕墙创建 {gridCount} 条横向网格线。");
            }

            return Result.Succeeded;
        }

        private List<double> ParseInput(string input)
        {
            List<double> values = new List<double>();
            string[] tokens = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string token in tokens)
            {
                if (token.Contains('*'))
                {
                    string[] parts = token.Split('*');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], out int count) &&
                        double.TryParse(parts[1], out double interval) &&
                        count > 0 && interval > 0)
                    {
                        for (int i = 0; i < count; i++)
                            values.Add(interval);
                    }
                }
                else
                {
                    if (double.TryParse(token, out double value) && value > 0)
                        values.Add(value);
                }
            }

            return values;
        }

        private int CreateHorizontalGridLines(Document doc, Wall wall,
            List<double> offsets, bool fromBottom)
        {
            CurtainGrid grid = wall.CurtainGrid;
            if (grid == null) return 0;

            BoundingBoxXYZ bb = wall.get_BoundingBox(null);
            if (bb == null) return 0;
            double bottomZ = bb.Min.Z;
            double topZ = bb.Max.Z;

            LocationCurve locCurve = wall.Location as LocationCurve;
            if (locCurve == null) return 0;

            Curve baseCurve = locCurve.Curve;
            XYZ midPt = (baseCurve.GetEndPoint(0) + baseCurve.GetEndPoint(1)) / 2;

            XYZ facingDir = wall.Orientation;
            double faceOffset = wall.WallType.Width / 2.0;
            if (faceOffset < 0.01) faceOffset = 0.05;

            const double mmToFeet = 1.0 / 304.8;
            int count = 0;
            double cumulative = 0;

            foreach (double offsetMm in offsets)
            {
                double offsetFeet = offsetMm * mmToFeet;
                cumulative += offsetFeet;

                double targetZ = fromBottom ? bottomZ + cumulative : topZ - cumulative;

                if (targetZ < bottomZ - 0.001 || targetZ > topZ + 0.001)
                    continue;

                XYZ pos;
                if (faceOffset > 0.001)
                    pos = new XYZ(midPt.X + facingDir.X * faceOffset,
                                  midPt.Y + facingDir.Y * faceOffset, targetZ);
                else
                    pos = new XYZ(midPt.X, midPt.Y, targetZ);

                try
                {
                    grid.AddGridLine(true, pos, false);
                    count++;
                }
                catch
                {
                    try
                    {
                        grid.AddGridLine(false, pos, false);
                        count++;
                    }
                    catch
                    {
                    }
                }
            }

            return count;
        }
    }
}
