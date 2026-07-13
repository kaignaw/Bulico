using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Bulico
{
    [Transaction(TransactionMode.Manual)]
    public class BatchFloorCommand : IExternalCommand
    {
        private static ElementId prevFloorTypeId = null;
        private static ElementId prevLevelId = null;
        private static double prevOffset = 0.0;
        private static bool prevUseCenterline = true;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            FilteredElementCollector floorTypeCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType));
            List<FloorType> floorTypes = floorTypeCollector.Cast<FloorType>().ToList();

            if (floorTypes.Count == 0)
            {
                TaskDialog.Show("批量建板", "项目中无可用楼板类型！");
                return Result.Cancelled;
            }

            FilteredElementCollector levelCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(Level));
            List<Level> levels = levelCollector.Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            if (levels.Count == 0)
            {
                TaskDialog.Show("批量建板", "项目中无标高！");
                return Result.Cancelled;
            }

            FloorType selectedType = null;
            Level selectedLevel = null;
            double selectedOffset = 0.0;
            bool useCenterlineMode = true;
            if (!ShowSelectionWindow(floorTypes, levels, out selectedType, out selectedLevel, out selectedOffset, out useCenterlineMode))
                return Result.Cancelled;

            try
            {
                IList<Reference> selectedRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new WallBeamFilter(),
                    "框选或点选墙和梁（仅能选中墙和梁）");

                if (selectedRefs == null || selectedRefs.Count == 0)
                {
                    TaskDialog.Show("批量建板", "未选中任何构件！");
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
                    TaskDialog.Show("批量建板", "选中的构件无法形成闭合区域（至少需要3条线）！");
                    return Result.Cancelled;
                }

                List<CurveLoop> regions = RegionFinder.FindClosedRegions(
                    curves, doc.Application.ShortCurveTolerance);

                if (regions.Count == 0)
                {
                    TaskDialog.Show("批量建板", "未找到任何闭合区域！请确保墙或梁围成了封闭空间。");
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

                ProgressWindow progress = new ProgressWindow("批量建板");
                progress.Show();
                progress.SetText("正在计算闭合区域...");
                progress.Pump();

                int count = 0;
                using (Transaction trans = new Transaction(doc, "批量建板"))
                {
                    trans.Start();
                    progress.SetRange(regions.Count);
                    for (int fi = 0; fi < regions.Count; fi++)
                    {
                        try
                        {
                            CurveArray profile = new CurveArray();
                            foreach (Curve c in regions[fi])
                            {
                                profile.Append(c);
                            }
                            Floor floor = doc.Create.NewFloor(
                                profile, selectedType, selectedLevel, false);
                            if (floor != null)
                            {
                                floor.get_Parameter(
                                    BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)
                                    .Set(UnitUtils.ConvertToInternalUnits(selectedOffset, DisplayUnitType.DUT_METERS));
                            }
                            count++;
                        }
                        catch (Exception)
                        {
                        }
                        progress.Update(fi + 1, regions.Count);
                        progress.Pump();
                    }
                    trans.Commit();
                }

                progress.Close();
                TaskDialog.Show("批量建板", string.Format("成功创建 {0} 个楼板！\n未成功创建 {1} 个。", count, regions.Count - count));
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
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

        private bool ShowSelectionWindow(
            List<FloorType> floorTypes,
            List<Level> levels,
            out FloorType selectedType,
            out Level selectedLevel,
            out double selectedOffset,
            out bool useCenterlineMode)
        {
            selectedType = null;
            selectedLevel = null;
            selectedOffset = 0.0;
            useCenterlineMode = true;

            FloorType localType = null;
            Level localLevel = null;
            double localOffset = 0.0;
            bool localCenterline = true;

            System.Windows.Controls.Grid grid = new System.Windows.Controls.Grid();
            grid.Margin = new Thickness(12);
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Label typeLabel = new Label
            {
                Content = "楼板类型：", VerticalAlignment = VerticalAlignment.Center
            };
            System.Windows.Controls.Grid.SetRow(typeLabel, 0);
            System.Windows.Controls.Grid.SetColumn(typeLabel, 0);

            System.Windows.Controls.ComboBox typeCombo = new System.Windows.Controls.ComboBox
            {
                Margin = new Thickness(4, 4, 0, 4),
                DisplayMemberPath = "Name",
                ItemsSource = floorTypes
            };
            if (prevFloorTypeId != null)
            {
                int idx = floorTypes.FindIndex(ft => ft.Id == prevFloorTypeId);
                if (idx >= 0) typeCombo.SelectedIndex = idx; else typeCombo.SelectedIndex = 0;
            }
            else if (floorTypes.Count > 0) typeCombo.SelectedIndex = 0;
            System.Windows.Controls.Grid.SetRow(typeCombo, 0);
            System.Windows.Controls.Grid.SetColumn(typeCombo, 1);

            Label levelLabel = new Label
            {
                Content = "标高：", VerticalAlignment = VerticalAlignment.Center
            };
            System.Windows.Controls.Grid.SetRow(levelLabel, 1);
            System.Windows.Controls.Grid.SetColumn(levelLabel, 0);

            System.Windows.Controls.ComboBox levelCombo = new System.Windows.Controls.ComboBox
            {
                Margin = new Thickness(4, 4, 0, 4),
                DisplayMemberPath = "Name",
                ItemsSource = levels
            };
            if (prevLevelId != null)
            {
                int idx = levels.FindIndex(lv => lv.Id == prevLevelId);
                if (idx >= 0) levelCombo.SelectedIndex = idx; else levelCombo.SelectedIndex = 0;
            }
            else if (levels.Count > 0) levelCombo.SelectedIndex = 0;
            System.Windows.Controls.Grid.SetRow(levelCombo, 1);
            System.Windows.Controls.Grid.SetColumn(levelCombo, 1);

            Label offsetLabel = new Label
            {
                Content = "标高偏移（m）：", VerticalAlignment = VerticalAlignment.Center
            };
            System.Windows.Controls.Grid.SetRow(offsetLabel, 2);
            System.Windows.Controls.Grid.SetColumn(offsetLabel, 0);

            System.Windows.Controls.TextBox offsetBox = new System.Windows.Controls.TextBox
            {
                Text = prevOffset.ToString("F3"),
                Margin = new Thickness(4, 4, 0, 4),
                Height = 22,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            System.Windows.Controls.Grid.SetRow(offsetBox, 2);
            System.Windows.Controls.Grid.SetColumn(offsetBox, 1);

            Label modeLabel = new Label
            {
                Content = "生成方式：", VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 0, 0)
            };
            System.Windows.Controls.Grid.SetRow(modeLabel, 3);
            System.Windows.Controls.Grid.SetColumn(modeLabel, 0);

            RadioButton centerlineRadio = new RadioButton
            {
                Content = "墙梁中心线", IsChecked = prevUseCenterline,
                Margin = new Thickness(4, 4, 0, 2), GroupName = "mode"
            };
            RadioButton innerRadio = new RadioButton
            {
                Content = "墙梁内边线", IsChecked = !prevUseCenterline,
                Margin = new Thickness(4, 2, 0, 4), GroupName = "mode"
            };
            StackPanel modePanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 2, 0, 0) };
            modePanel.Children.Add(centerlineRadio);
            modePanel.Children.Add(innerRadio);
            System.Windows.Controls.Grid.SetRow(modePanel, 3);
            System.Windows.Controls.Grid.SetColumn(modePanel, 1);

            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            };

            Button okButton = new Button
            {
                Content = "确定", Width = 80, Height = 28, Margin = new Thickness(0, 0, 8, 0), IsDefault = true
            };
            Button cancelButton = new Button
            {
                Content = "取消", Width = 80, Height = 28, IsCancel = true
            };
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            System.Windows.Controls.Grid.SetRow(buttonPanel, 4);
            System.Windows.Controls.Grid.SetColumnSpan(buttonPanel, 2);

            bool result = false;
            Window window = new Window
            {
                Title = "批量建板",
                Width = 380,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Content = grid
            };

            okButton.Click += (s, e) =>
            {
                FloorType ft = typeCombo.SelectedItem as FloorType;
                Level lv = levelCombo.SelectedItem as Level;
                if (ft == null || lv == null)
                {
                    MessageBox.Show("请选择楼板类型和标高。", "提示");
                    return;
                }
                double val;
                if (!double.TryParse(offsetBox.Text, out val))
                {
                    MessageBox.Show("标高偏移请输入有效数值。", "提示");
                    return;
                }
                localType = ft;
                localLevel = lv;
                localOffset = val;
                localCenterline = centerlineRadio.IsChecked == true;
                prevFloorTypeId = ft.Id;
                prevLevelId = lv.Id;
                prevOffset = val;
                prevUseCenterline = localCenterline;
                result = true;
                window.Close();
            };

            cancelButton.Click += (s, e) => { window.Close(); };

            grid.Children.Add(typeLabel);
            grid.Children.Add(typeCombo);
            grid.Children.Add(levelLabel);
            grid.Children.Add(levelCombo);
            grid.Children.Add(offsetLabel);
            grid.Children.Add(offsetBox);
            grid.Children.Add(modeLabel);
            grid.Children.Add(modePanel);
            grid.Children.Add(buttonPanel);

            window.ShowDialog();

            selectedType = localType;
            selectedLevel = localLevel;
            selectedOffset = localOffset;
            useCenterlineMode = localCenterline;
            return result;
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
                    double bw = 0;
                    Parameter p = fi.Symbol.LookupParameter("B");
                    if (p != null) bw = p.AsDouble();
                    if (bw < doc.Application.ShortCurveTolerance)
                    {
                        Parameter p2 = fi.Symbol.LookupParameter("b");
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
