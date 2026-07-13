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
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

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
