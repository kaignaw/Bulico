using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Bulico
{
    [Transaction(TransactionMode.Manual)]
    public class RoomFloorCommand : IExternalCommand
    {
        private static ElementId prevFloorTypeId = null;
        private static ElementId prevLevelId = null;
        private static double prevOffset = 0.0;

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
                TaskDialog.Show("房间建板", "项目中无可用楼板类型！");
                return Result.Cancelled;
            }

            FilteredElementCollector levelCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(Level));
            List<Level> levels = levelCollector.Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            if (levels.Count == 0)
            {
                TaskDialog.Show("房间建板", "项目中无标高！");
                return Result.Cancelled;
            }

            FloorType selectedType = null;
            Level selectedLevel = null;
            double selectedOffset = 0.0;
            if (!ShowSelectionWindow(floorTypes, levels, out selectedType, out selectedLevel, out selectedOffset))
                return Result.Cancelled;

            ProgressWindow progress = null;
            try
            {
                IList<Reference> selectedRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new RoomSelectionFilter(),
                    "框选或点选房间（可正选或反选）");

                if (selectedRefs == null || selectedRefs.Count == 0)
                {
                    TaskDialog.Show("房间建板", "未选中任何房间！");
                    return Result.Cancelled;
                }

                List<Room> rooms = new List<Room>();
                foreach (Reference selRef in selectedRefs)
                {
                    Room room = doc.GetElement(selRef) as Room;
                    if (room != null)
                        rooms.Add(room);
                }

                if (rooms.Count == 0)
                {
                    TaskDialog.Show("房间建板", "未选中任何房间！");
                    return Result.Cancelled;
                }

                SpatialElementBoundaryOptions boundaryOptions = new SpatialElementBoundaryOptions();
                boundaryOptions.SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish;

                int successCount = 0;
                int failCount = 0;
                Dictionary<ElementId, List<CurveArray>> roomHolesMap = new Dictionary<ElementId, List<CurveArray>>();

                progress = new ProgressWindow("房间建板");
                progress.Show();
                progress.SetText("正在创建楼板...");
                progress.Pump();

                int totalSteps = rooms.Count;
                int currentStep = 0;

                using (Transaction trans = new Transaction(doc, "房间建板"))
                {
                    FailureHandlingOptions options = trans.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(new OverlapWarningSuppressor());
                    trans.SetFailureHandlingOptions(options);
                    trans.Start();

                    progress.SetRange(totalSteps);

                    foreach (Room room in rooms)
                    {
                        currentStep++;
                        progress.Update(currentStep, totalSteps);
                        progress.Pump();
                        try
                        {
                            IList<IList<BoundarySegment>> boundarySegments = room.GetBoundarySegments(boundaryOptions);
                            if (boundarySegments == null || boundarySegments.Count == 0)
                            {
                                failCount++;
                                continue;
                            }

                            CurveArray outerProfile = new CurveArray();
                            foreach (BoundarySegment seg in boundarySegments[0])
                            {
                                outerProfile.Append(seg.GetCurve());
                            }

                            Floor floor = doc.Create.NewFloor(outerProfile, selectedType, selectedLevel, false);
                            if (floor == null)
                            {
                                failCount++;
                                continue;
                            }

                            floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)
                                .Set(UnitUtils.ConvertToInternalUnits(selectedOffset, DisplayUnitType.DUT_METERS));

                            List<CurveArray> holeProfiles = new List<CurveArray>();
                            for (int hi = 1; hi < boundarySegments.Count; hi++)
                            {
                                CurveArray holeProfile = new CurveArray();
                                foreach (BoundarySegment seg in boundarySegments[hi])
                                {
                                    holeProfile.Append(seg.GetCurve());
                                }
                                holeProfiles.Add(holeProfile);
                            }

                            ElementId floorId = floor.Id;
                            roomHolesMap[floorId] = holeProfiles;

                            successCount++;
                        }
                        catch (Exception)
                        {
                            failCount++;
                        }
                    }

                    trans.Commit();
                }

                if (roomHolesMap.Count > 0)
                {
                    progress.SetText("正在添加洞口...");
                    progress.Pump();

                    int totalHoles = 0;
                    foreach (var kvp in roomHolesMap)
                        totalHoles += kvp.Value.Count;

                    using (Transaction holeTrans = new Transaction(doc, "房间建板-开洞"))
                    {
                        FailureHandlingOptions holeOptions = holeTrans.GetFailureHandlingOptions();
                        holeOptions.SetFailuresPreprocessor(new OverlapWarningSuppressor());
                        holeTrans.SetFailureHandlingOptions(holeOptions);
                        holeTrans.Start();

                        progress.SetRange(totalHoles);
                        int holeIndex = 0;

                        foreach (var kvp in roomHolesMap)
                        {
                            Floor floor = doc.GetElement(kvp.Key) as Floor;
                            if (floor == null) continue;
                            foreach (CurveArray holeProfile in kvp.Value)
                            {
                                doc.Create.NewOpening(floor, holeProfile, true);
                                holeIndex++;
                                progress.Update(holeIndex, totalHoles);
                                progress.Pump();
                            }
                        }
                        holeTrans.Commit();
                    }
                }

                progress.Close();
                TaskDialog.Show("房间建板", string.Format("成功创建 {0} 个房间的楼板！\n未成功创建 {1} 个。", successCount, failCount));
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            finally
            {
                if (progress != null)
                {
                    try { progress.Close(); } catch { }
                }
            }
        }

        private bool ShowSelectionWindow(
            List<FloorType> floorTypes,
            List<Level> levels,
            out FloorType selectedType,
            out Level selectedLevel,
            out double selectedOffset)
        {
            selectedType = null;
            selectedLevel = null;
            selectedOffset = 0.0;

            FloorType localType = null;
            Level localLevel = null;
            double localOffset = 0.0;

            System.Windows.Controls.Grid grid = new System.Windows.Controls.Grid();
            grid.Margin = new Thickness(12);
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Label typeLabel = new Label
            {
                Content = "楼板类型：",
                VerticalAlignment = VerticalAlignment.Center
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
                if (idx >= 0) typeCombo.SelectedIndex = idx;
                else if (floorTypes.Count > 0) typeCombo.SelectedIndex = 0;
            }
            else if (floorTypes.Count > 0) typeCombo.SelectedIndex = 0;
            System.Windows.Controls.Grid.SetRow(typeCombo, 0);
            System.Windows.Controls.Grid.SetColumn(typeCombo, 1);

            Label levelLabel = new Label
            {
                Content = "标高：",
                VerticalAlignment = VerticalAlignment.Center
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
                if (idx >= 0) levelCombo.SelectedIndex = idx;
                else if (levels.Count > 0) levelCombo.SelectedIndex = 0;
            }
            else if (levels.Count > 0) levelCombo.SelectedIndex = 0;
            System.Windows.Controls.Grid.SetRow(levelCombo, 1);
            System.Windows.Controls.Grid.SetColumn(levelCombo, 1);

            Label offsetLabel = new Label
            {
                Content = "标高偏移（m）：",
                VerticalAlignment = VerticalAlignment.Center
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

            Button okButton = new Button
            {
                Content = "确定",
                Width = 80,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            Button cancelButton = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 28,
                IsCancel = true
            };
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            System.Windows.Controls.Grid.SetRow(buttonPanel, 3);
            System.Windows.Controls.Grid.SetColumnSpan(buttonPanel, 2);

            bool result = false;
            Window window = new Window
            {
                Title = "房间建板",
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
                prevFloorTypeId = ft.Id;
                prevLevelId = lv.Id;
                prevOffset = val;
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
            grid.Children.Add(buttonPanel);

            window.ShowDialog();

            selectedType = localType;
            selectedLevel = localLevel;
            selectedOffset = localOffset;
            return result;
        }
    }

    public class OverlapWarningSuppressor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            IList<FailureMessageAccessor> failures = failuresAccessor.GetFailureMessages();
            foreach (FailureMessageAccessor failure in failures)
            {
                FailureSeverity severity = failure.GetSeverity();
                if (severity == FailureSeverity.Warning)
                {
                    FailureDefinitionId id = failure.GetFailureDefinitionId();
                    if (id == BuiltInFailures.OverlapFailures.FloorsOverlap)
                    {
                        failuresAccessor.DeleteWarning(failure);
                    }
                    else
                    {
                        string desc = failure.GetDescriptionText();
                        if (desc.Contains("overlap") || desc.Contains("重叠"))
                        {
                            failuresAccessor.DeleteWarning(failure);
                        }
                    }
                }
            }
            return FailureProcessingResult.Continue;
        }
    }
}
