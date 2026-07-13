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
    public class SameTypeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds.Count == 0)
            {
                TaskDialog.Show("同型过滤", "请先选择构件再启动命令。");
                return Result.Cancelled;
            }

            HashSet<ElementId> targetTypeIds = new HashSet<ElementId>();
            bool filterCurtainGridLines = false;
            foreach (ElementId id in selectedIds)
            {
                Element elem = doc.GetElement(id);
                if (elem == null) continue;

                if (elem is CurtainGridLine)
                {
                    filterCurtainGridLines = true;
                    continue;
                }

                if (elem is ElementType elemType)
                {
                    targetTypeIds.Add(elemType.Id);
                }
                else
                {
                    ElementId typeId = elem.GetTypeId();
                    if (typeId != ElementId.InvalidElementId)
                        targetTypeIds.Add(typeId);
                }
            }

            if (targetTypeIds.Count == 0 && !filterCurtainGridLines)
            {
                TaskDialog.Show("同型过滤", "未识别到有效的构件类型。");
                return Result.Cancelled;
            }

            uidoc.Selection.SetElementIds(new List<ElementId>());

            try
            {
                ICollection<Element> picked = uidoc.Selection.PickElementsByRectangle(
                    new SameTypeSelectionFilter(targetTypeIds, filterCurtainGridLines),
                    "框选范围以过滤同类型构件");

                List<ElementId> filteredIds = picked
                    .Where(e => MatchesAnyType(e, targetTypeIds, filterCurtainGridLines))
                    .Select(e => e.Id)
                    .ToList();

                if (filteredIds.Count > 0)
                {
                    uidoc.Selection.SetElementIds(filteredIds);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        private bool MatchesAnyType(Element element, HashSet<ElementId> targetTypeIds, bool filterCurtainGridLines)
        {
            if (element is CurtainGridLine)
                return filterCurtainGridLines;
            if (element is ElementType et)
                return targetTypeIds.Contains(et.Id);
            ElementId typeId = element.GetTypeId();
            return typeId != ElementId.InvalidElementId && targetTypeIds.Contains(typeId);
        }
    }

    public class SameTypeSelectionFilter : ISelectionFilter
    {
        private readonly HashSet<ElementId> _targetTypeIds;
        private readonly bool _filterCurtainGridLines;

        public SameTypeSelectionFilter(HashSet<ElementId> targetTypeIds, bool filterCurtainGridLines)
        {
            _targetTypeIds = targetTypeIds;
            _filterCurtainGridLines = filterCurtainGridLines;
        }

        public bool AllowElement(Element elem)
        {
            if (elem is CurtainGridLine)
                return _filterCurtainGridLines;
            if (elem is ElementType et)
                return _targetTypeIds.Contains(et.Id);
            ElementId typeId = elem.GetTypeId();
            return typeId != ElementId.InvalidElementId && _targetTypeIds.Contains(typeId);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
