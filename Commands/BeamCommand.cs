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

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

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

                ProgressWindow progress = new ProgressWindow("框墙生梁");
                progress.Show();
                progress.SetText("正在生成梁...");
                progress.Pump();

                bool hasError = false;
                using (Transaction trans = new Transaction(doc, "框墙生梁"))
                {
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
                            if (beamCurve == null) continue;

                            double topElevation = GetWallTopElevation(wall, doc);
                            Level beamLevel = FindClosestLevel(doc, topElevation);
                            if (beamLevel == null) continue;

                            XYZ startPt = beamCurve.GetEndPoint(0);
                            XYZ endPt = beamCurve.GetEndPoint(1);
                            Curve elevatedCurve = Line.CreateBound(
                                new XYZ(startPt.X, startPt.Y, topElevation),
                                new XYZ(endPt.X, endPt.Y, topElevation));

                            doc.Create.NewFamilyInstance(elevatedCurve, beamType, beamLevel, StructuralType.Beam);
                        }
                        catch (Exception ex)
                        {
                            hasError = true;
                            TaskDialog.Show("错误", "生成梁时出错：" + ex.Message);
                        }

                        progress.Update(i + 1, walls.Count);
                        progress.Pump();
                    }

                    trans.Commit();
                }

                progress.Close();

                if (hasError)
                    return Result.Failed;

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
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

            Parameter locLineParam = wall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM);
            WallLocationLine wallLocLine = (WallLocationLine)locLineParam.AsInteger();
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
            XYZ leftPerp = new XYZ(-curveDir.Y, curveDir.X, 0);

            XYZ exteriorDir = wall.Orientation.CrossProduct(XYZ.BasisZ).Normalize();
            if (exteriorDir.DotProduct(leftPerp) < 0)
                exteriorDir = -exteriorDir;

            double absOffset = Math.Abs(coreCenterOffset);
            XYZ offsetDir = coreCenterOffset > 0 ? exteriorDir : -exteriorDir;

            return curve.CreateOffset(absOffset, offsetDir);
        }

        private double GetWallTopElevation(Wall wall, Document doc)
        {
            Parameter baseLevelParam = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
            ElementId baseLevelId = baseLevelParam.AsElementId();
            Level baseLevel = doc.GetElement(baseLevelId) as Level;
            double baseElev = 0;
            if (baseLevel != null)
                baseElev = baseLevel.Elevation;

            Parameter baseOffsetParam = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET);
            double baseOffset = baseOffsetParam.AsDouble();

            Parameter topLevelParam = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
            ElementId topLevelId = topLevelParam.AsElementId();

            if (topLevelId != ElementId.InvalidElementId)
            {
                Parameter topOffsetParam = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET);
                double topOffset = topOffsetParam.AsDouble();
                Level topLevel = doc.GetElement(topLevelId) as Level;
                if (topLevel != null)
                    return topLevel.Elevation + topOffset;
            }

            Parameter heightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
            double height = heightParam.AsDouble();
            return baseElev + baseOffset + height;
        }

        private Level FindClosestLevel(Document doc, double elevation)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => Math.Abs(l.Elevation - elevation))
                .FirstOrDefault();
        }

        private FamilySymbol SelectBeamType(Document doc)
        {
            FamilySymbol preselectedType = null;

            if (_lastBeamTypeId != null)
            {
                Element elem = doc.GetElement(_lastBeamTypeId);
                FamilySymbol fs = elem as FamilySymbol;
                if (fs != null)
                    preselectedType = fs;
            }

            BeamTypeSelector dialog = new BeamTypeSelector(doc, preselectedType);
            dialog.ShowDialog();

            if (dialog.SelectedBeamType != null)
                _lastBeamTypeId = dialog.SelectedBeamType.Id;

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
