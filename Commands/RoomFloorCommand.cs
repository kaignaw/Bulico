using System;
using System.Collections.Generic;
using System.Linq;
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
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!CommandGuard.TryGetDocument(commandData, "房间建板", out UIDocument uidoc, out Document doc))
                return Result.Cancelled;

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

            FloorSettings settings = FloorSettingsDialog.Show(
                doc, "房间建板", floorTypes, levels, showModeOption: false);
            if (settings == null)
                return Result.Cancelled;

            FloorType selectedType = settings.FloorType;
            Level selectedLevel = settings.Level;
            double selectedOffset = settings.OffsetMeters;

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
                HashSet<ElementId> seenRoomIds = new HashSet<ElementId>();
                foreach (Reference selRef in selectedRefs)
                {
                    Room room = doc.GetElement(selRef) as Room;
                    if (room != null && seenRoomIds.Add(room.Id))
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
                int skippedExistingCount = 0;
                Dictionary<ElementId, List<CurveArray>> roomHolesMap = new Dictionary<ElementId, List<CurveArray>>();

                // 目标标高上已有的楼板只收集一次，用于避免重复创建重叠楼板。
                double selectedOffsetFeet = UnitUtils.ConvertToInternalUnits(selectedOffset, UnitTypeId.Meters);
                List<Floor> existingFloors = new FilteredElementCollector(doc)
                    .OfClass(typeof(Floor))
                    .Cast<Floor>()
                    .Where(f => f.LevelId == selectedLevel.Id &&
                                Math.Abs(GetFloorHeightAboveLevel(f) - selectedOffsetFeet) <= 0.003)
                    .ToList();

                progress = new ProgressWindow("房间建板");
                progress.Show();
                progress.SetText("正在创建楼板...");
                progress.Pump();

                int totalSteps = rooms.Count;
                int currentStep = 0;

                TransactionGroup group = new TransactionGroup(doc, "房间建板");
                bool groupActive = false;
                bool createFailed = false;
                Exception createException = null;

                try
                {
                    group.Start();
                    groupActive = true;

                    using (Transaction trans = new Transaction(doc, "房间建板"))
                    {
                        TransactionUtil.Prepare(trans, true);
                        trans.Start();

                        progress.SetRange(totalSteps);

                        foreach (Room room in rooms)
                        {
                            currentStep++;
                            progress.Update(currentStep, totalSteps);

                            try
                            {
                                if (HasExistingFloor(existingFloors, room.get_BoundingBox(null),
                                        doc.Application.ShortCurveTolerance))
                                {
                                    skippedExistingCount++;
                                    continue;
                                }

                                IList<IList<BoundarySegment>> boundarySegments = room.GetBoundarySegments(boundaryOptions);
                                if (boundarySegments == null || boundarySegments.Count == 0)
                                {
                                    createFailed = true;
                                    throw new InvalidOperationException("房间缺少有效边界。");
                                }

                                CurveLoop outerProfile = new CurveLoop();
                                foreach (BoundarySegment seg in boundarySegments[0])
                                {
                                    outerProfile.Append(seg.GetCurve());
                                }

                                Floor floor = Floor.Create(
                                    doc,
                                    new List<CurveLoop> { outerProfile },
                                    selectedType.Id,
                                    selectedLevel.Id);
                                if (floor == null)
                                {
                                    createFailed = true;
                                    throw new InvalidOperationException("楼板创建失败。");
                                }

                                Parameter offsetParam = floor.get_Parameter(
                                    BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                                if (offsetParam != null)
                                    offsetParam.Set(UnitUtils.ConvertToInternalUnits(selectedOffset, UnitTypeId.Meters));

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
                            catch (Exception ex)
                            {
                                createFailed = true;
                                createException = ex;
                                break;
                            }
                        }

                        if (createFailed)
                            trans.RollBack();
                        else
                            trans.Commit();
                    }

                    if (createFailed)
                    {
                        progress.SetText("创建失败，正在回滚...");
                        progress.Pump();
                        group.RollBack();
                        groupActive = false;

                        if (createException != null)
                            ErrorReporter.Log("房间建板", createException);

                        progress.Finish();

                        TaskDialog.Show("房间建板", "房间建板失败，本次操作已全部回滚。\n\n详细信息已写入本地日志：\n" + ErrorReporter.LogPath);
                        return Result.Failed;
                    }

                    if (roomHolesMap.Count > 0)
                    {
                        progress.SetText("正在添加洞口...");
                        progress.Pump();

                        int totalHoles = 0;
                        foreach (var kvp in roomHolesMap)
                            totalHoles += kvp.Value.Count;

                        bool holeFailed = false;
                        Exception holeException = null;

                        using (Transaction holeTrans = new Transaction(doc, "房间建板-开洞"))
                        {
                            TransactionUtil.Prepare(holeTrans, true);
                            holeTrans.Start();

                            progress.SetRange(totalHoles);
                            int holeIndex = 0;

                            try
                            {
                                foreach (var kvp in roomHolesMap)
                                {
                                    Floor floor = doc.GetElement(kvp.Key) as Floor;
                                    if (floor == null)
                                        continue;

                                    foreach (CurveArray holeProfile in kvp.Value)
                                    {
                                        doc.Create.NewOpening(floor, holeProfile, true);
                                        holeIndex++;
                                        progress.Update(holeIndex, totalHoles);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                holeFailed = true;
                                holeException = ex;
                            }

                            if (holeFailed)
                                holeTrans.RollBack();
                            else
                                holeTrans.Commit();
                        }

                        if (holeFailed)
                        {
                            progress.SetText("添加洞口失败，正在回滚...");
                            progress.Pump();
                            group.RollBack();
                            groupActive = false;

                            if (holeException != null)
                                ErrorReporter.Log("房间建板-开洞", holeException);

                            progress.Finish();

                            TaskDialog.Show("房间建板", "添加洞口失败，本次操作已全部回滚。\n\n详细信息已写入本地日志：\n" + ErrorReporter.LogPath);
                            return Result.Failed;
                        }
                    }

                    group.Assimilate();
                    groupActive = false;
                }
                finally
                {
                    if (groupActive)
                    {
                        try { group.RollBack(); } catch { }
                    }
                    group.Dispose();
                }

                progress.Finish();

                string summary;
                if (successCount > 0)
                {
                    summary = string.Format("成功创建 {0} 个房间的楼板！", successCount);
                }
                else
                {
                    summary = "未创建新的楼板。";
                }

                if (skippedExistingCount > 0)
                {
                    summary += string.Format(
                        "\n跳过 {0} 个在目标标高上已有楼板的房间。",
                        skippedExistingCount);
                }

                TaskDialog.Show("房间建板", summary);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                ErrorReporter.Show("房间建板", "房间建板失败，本次操作未保存。", ex);
                message = "房间建板失败，请查看本地日志。";
                return Result.Failed;
            }
            finally
            {
                if (progress != null)
                    progress.Dispose();
            }
        }

        private static double GetFloorHeightAboveLevel(Floor floor)
        {
            Parameter param = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            return param != null ? param.AsDouble() : 0.0;
        }

        private static bool HasExistingFloor(
            List<Floor> existingFloors,
            BoundingBoxXYZ roomBox,
            double tolerance)
        {
            if (roomBox == null)
                return false;

            foreach (Floor floor in existingFloors)
            {
                BoundingBoxXYZ floorBox = floor.get_BoundingBox(null);
                if (floorBox == null)
                    continue;

                // 只在房间轮廓完全落在已有楼板范围内时判定为“已有楼板”，
                // 避免相邻房间的楼板被误判。
                if (floorBox.Min.X <= roomBox.Min.X + tolerance &&
                    floorBox.Max.X >= roomBox.Max.X - tolerance &&
                    floorBox.Min.Y <= roomBox.Min.Y + tolerance &&
                    floorBox.Max.Y >= roomBox.Max.Y - tolerance)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class OverlapWarningSuppressor : IFailuresPreprocessor
    {
        private readonly bool _suppressFloorOverlapWarnings;

        public OverlapWarningSuppressor(bool suppressFloorOverlapWarnings = true)
        {
            _suppressFloorOverlapWarnings = suppressFloorOverlapWarnings;
        }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            IList<FailureMessageAccessor> failures = failuresAccessor.GetFailureMessages();
            foreach (FailureMessageAccessor failure in failures)
            {
                FailureSeverity severity = failure.GetSeverity();

                // 出现 Error 时直接回滚，避免留下半成品。
                if (severity == FailureSeverity.Error)
                    return FailureProcessingResult.ProceedWithRollBack;

                if (severity == FailureSeverity.Warning && _suppressFloorOverlapWarnings)
                {
                    FailureDefinitionId id = failure.GetFailureDefinitionId();
                    if (id == BuiltInFailures.OverlapFailures.FloorsOverlap)
                    {
                        failuresAccessor.DeleteWarning(failure);
                    }
                }
            }
            return FailureProcessingResult.Continue;
        }
    }
}
