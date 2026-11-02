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
            if (!CommandGuard.TryGetDocument(commandData, "同型过滤", out UIDocument uidoc, out Document doc))
                return Result.Cancelled;

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

            ICollection<ElementId> originalSelection = new List<ElementId>(uidoc.Selection.GetElementIds());
            uidoc.Selection.SetElementIds(new List<ElementId>());

            try
            {
                ICollection<Element> picked = uidoc.Selection.PickElementsByRectangle(
                    new SameTypeSelectionFilter(targetTypeIds, filterCurtainGridLines),
                    "框选范围以过滤同类型构件");

                List<ElementId> filteredIds = picked
                    .Where(e => SameTypeSelectionFilter.Matches(
                        e, targetTypeIds, filterCurtainGridLines))
                    .Select(e => e.Id)
                    .ToList();

                if (filteredIds.Count > 0)
                {
                    uidoc.Selection.SetElementIds(filteredIds);
                }
                else
                {
                    uidoc.Selection.SetElementIds(originalSelection);
                    TaskDialog.Show("同型过滤", "框选范围内没有匹配的构件，已恢复原选择。");
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                uidoc.Selection.SetElementIds(originalSelection);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                uidoc.Selection.SetElementIds(originalSelection);
                ErrorReporter.Show("同型过滤", "过滤构件失败，已恢复原选择。", ex);
                message = "过滤构件失败，请查看本地日志。";
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }

    /// <summary>
    /// “同型”判定：幕墙网格线按开关放行，类型元素按自身 Id 匹配，其余按类型 Id 匹配。
    /// 选择过滤器与框选后的最终过滤共用这一份逻辑，避免“能选中但结果不符”。
    /// </summary>
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
            return Matches(elem, _targetTypeIds, _filterCurtainGridLines);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }

        public static bool Matches(
            Element element, HashSet<ElementId> targetTypeIds, bool filterCurtainGridLines)
        {
            if (element == null)
                return false;

            if (element is CurtainGridLine)
                return filterCurtainGridLines;

            if (element is ElementType type)
                return targetTypeIds.Contains(type.Id);

            ElementId typeId = element.GetTypeId();
            return typeId != ElementId.InvalidElementId && targetTypeIds.Contains(typeId);
        }
    }
}
