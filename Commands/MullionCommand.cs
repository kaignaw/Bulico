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
            if (!CommandGuard.TryGetDocument(commandData, "一键横挺", out UIDocument uidoc, out Document doc))
                return Result.Cancelled;

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
                Owner = commandData.Application.MainWindowHandle
            };

            bool? dialogResult = window.ShowDialog();
            if (dialogResult != true)
                return Result.Cancelled;

            List<double> offsets = ParseInput(window.InputText, out bool exceedsLimit, out int invalidTokenCount);
            if (exceedsLimit)
            {
                TaskDialog.Show("一键横挺", string.Format(
                    "单次最多支持 {0} 条横向网格线，请减少输入。",
                    InputLimits.MaxMullionLines));
                return Result.Cancelled;
            }
            if (offsets.Count == 0)
            {
                TaskDialog.Show("提示", "输入格式无效！请检查输入值。");
                return Result.Cancelled;
            }

            bool fromBottom = window.IsFromBottom;

            int gridCount = 0;
            int failedCount = 0;

            using (Transaction trans = new Transaction(doc, "一键横挺"))
            {
                TransactionUtil.Prepare(trans, false);
                trans.Start();

                foreach (Wall wall in curtainWalls)
                {
                    gridCount += CreateHorizontalGridLines(
                        doc, wall, offsets, fromBottom, out int wallFailed);
                    failedCount += wallFailed;
                }

                trans.Commit();
            }

            string summary;
            if (gridCount > 0)
            {
                summary = string.Format(
                    "已为 {0} 面幕墙创建 {1} 条横向网格线。",
                    curtainWalls.Count,
                    gridCount);
            }
            else
            {
                summary = string.Format(
                    "未创建任何横向网格线。请检查偏移值是否超出幕墙高度（共 {0} 面幕墙）。",
                    curtainWalls.Count);
            }

            if (failedCount > 0)
            {
                summary += string.Format(
                    "\n有 {0} 条网格线创建失败（幕墙上可能已有相同位置的网格线）。",
                    failedCount);
            }

            if (invalidTokenCount > 0)
            {
                summary += string.Format(
                    "\n有 {0} 段输入无法识别，已忽略。",
                    invalidTokenCount);
            }

            TaskDialog.Show("一键横挺", summary);
            return Result.Succeeded;
        }

        private List<double> ParseInput(string input, out bool exceedsLimit, out int invalidTokenCount)
        {
            List<double> values = new List<double>();
            exceedsLimit = false;
            invalidTokenCount = 0;
            string[] tokens = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string token in tokens)
            {
                if (token.Contains('*'))
                {
                    string[] parts = token.Split('*');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], out int count) &&
                        double.TryParse(parts[1], out double interval) &&
                        count > 0 && interval > 0 && interval <= InputLimits.MaxMullionOffsetMm)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if (values.Count >= InputLimits.MaxMullionLines)
                            {
                                exceedsLimit = true;
                                break;
                            }
                            values.Add(interval);
                        }
                    }
                    else
                    {
                        invalidTokenCount++;
                    }
                }
                else
                {
                    if (double.TryParse(token, out double value) &&
                        value > 0 && value <= InputLimits.MaxMullionOffsetMm)
                    {
                        if (values.Count >= InputLimits.MaxMullionLines)
                        {
                            exceedsLimit = true;
                            break;
                        }
                        values.Add(value);
                    }
                    else
                    {
                        invalidTokenCount++;
                    }
                }

                if (exceedsLimit)
                    break;
            }

            return values;
        }

        private int CreateHorizontalGridLines(Document doc, Wall wall,
            List<double> offsets, bool fromBottom, out int failedCount)
        {
            failedCount = 0;

            CurtainGrid grid = wall.CurtainGrid;
            if (grid == null) return 0;

            BoundingBoxXYZ bb = wall.get_BoundingBox(null);
            if (bb == null) return 0;
            double bottomZ = bb.Min.Z;
            double topZ = bb.Max.Z;

            LocationCurve locCurve = wall.Location as LocationCurve;
            if (locCurve == null) return 0;

            Curve baseCurve = locCurve.Curve;
            if (baseCurve == null) return 0;

            XYZ midPt;
            try
            {
                midPt = baseCurve.Evaluate(0.5, true);
            }
            catch
            {
                midPt = (baseCurve.GetEndPoint(0) + baseCurve.GetEndPoint(1)) / 2;
            }

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
                        failedCount++;
                    }
                }
            }

            return count;
        }
    }
}
