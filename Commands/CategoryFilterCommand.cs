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
    public class CategoryFilterCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!CommandGuard.TryGetDocument(commandData, "类别过滤", out UIDocument uidoc, out Document doc))
                return Result.Cancelled;

            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds.Count == 0)
            {
                TaskDialog.Show("类别过滤", "请先选择构件再启动命令。");
                return Result.Cancelled;
            }

            HashSet<ElementId> targetCategoryIds = new HashSet<ElementId>();
            foreach (ElementId id in selectedIds)
            {
                Element elem = doc.GetElement(id);
                if (elem?.Category == null) continue;

                targetCategoryIds.Add(elem.Category.Id);
            }

            if (targetCategoryIds.Count == 0)
            {
                TaskDialog.Show("类别过滤", "未识别到有效的类别。");
                return Result.Cancelled;
            }

            ICollection<ElementId> originalSelection = new List<ElementId>(uidoc.Selection.GetElementIds());
            uidoc.Selection.SetElementIds(new List<ElementId>());

            try
            {
                ICollection<Element> picked = uidoc.Selection.PickElementsByRectangle(
                    new CategorySelectionFilter(targetCategoryIds),
                    "框选范围以过滤同类构件");

                List<ElementId> filteredIds = picked
                    .Where(e => e.Category != null && targetCategoryIds.Contains(e.Category.Id))
                    .Select(e => e.Id)
                    .ToList();

                if (filteredIds.Count > 0)
                {
                    uidoc.Selection.SetElementIds(filteredIds);
                }
                else
                {
                    uidoc.Selection.SetElementIds(originalSelection);
                    TaskDialog.Show("类别过滤", "框选范围内没有匹配的构件，已恢复原选择。");
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
                ErrorReporter.Show("类别过滤", "过滤构件失败，已恢复原选择。", ex);
                message = "过滤构件失败，请查看本地日志。";
                return Result.Failed;
            }

            return Result.Succeeded;
        }

    }

    public class CategorySelectionFilter : ISelectionFilter
    {
        private readonly HashSet<ElementId> _targetCategoryIds;

        public CategorySelectionFilter(HashSet<ElementId> targetCategoryIds)
        {
            _targetCategoryIds = targetCategoryIds;
        }

        public bool AllowElement(Element elem)
        {
            return elem.Category != null && _targetCategoryIds.Contains(elem.Category.Id);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
