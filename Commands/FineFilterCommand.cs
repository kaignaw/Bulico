using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Bulico
{
    [Transaction(TransactionMode.Manual)]
    public class FineFilterCommand : IExternalCommand
    {
        private const int PseudoTypeIdOffset = -1000000;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds.Count == 0)
            {
                TaskDialog.Show("精细过滤", "请先选择构件再启动命令。");
                return Result.Cancelled;
            }

            List<CategoryNode> categories = BuildHierarchy(doc, selectedIds);
            if (categories.Count == 0)
            {
                TaskDialog.Show("精细过滤", "未识别到有效的构件。");
                return Result.Cancelled;
            }

            var dialog = new FilterDialog(categories);
            bool? dialogResult = dialog.ShowDialog();
            if (dialogResult != true)
                return Result.Cancelled;

            HashSet<ElementId> selectedTypeIds = dialog.GetSelectedTypeIds();
            if (selectedTypeIds.Count == 0)
            {
                TaskDialog.Show("精细过滤", "请至少选择一种类型。");
                return Result.Cancelled;
            }

            List<ElementId> filteredIds = new List<ElementId>();
            foreach (ElementId selId in selectedIds)
            {
                Element e = doc.GetElement(selId);
                if (e != null && MatchesSelectedTypes(e, selectedTypeIds))
                    filteredIds.Add(selId);
            }

            if (filteredIds.Count == 0)
            {
                TaskDialog.Show("精细过滤", "选中的构件中未找到匹配的类型。");
                return Result.Cancelled;
            }

            uidoc.Selection.SetElementIds(filteredIds);
            return Result.Succeeded;
        }

        private static ElementId GetPseudoTypeId(Element e)
        {
            return new ElementId(PseudoTypeIdOffset - e.Category.Id.IntegerValue);
        }

        private List<CategoryNode> BuildHierarchy(Document doc, ICollection<ElementId> ids)
        {
            var categoryMap = new Dictionary<string, CategoryNode>(StringComparer.OrdinalIgnoreCase);
            var seenTypes = new HashSet<ElementId>();

            foreach (ElementId id in ids)
            {
                Element e = doc.GetElement(id);
                if (e == null || e.Category == null)
                    continue;

                string catName = e.Category.Name;
                if (!categoryMap.TryGetValue(catName, out var catNode))
                {
                    catNode = new CategoryNode(catName);
                    categoryMap[catName] = catNode;
                }

                string familyName;
                ElementId typeId;
                string typeName;

                if (e is ElementType elemType)
                {
                    typeId = elemType.Id;
                    typeName = elemType.Name;
                }
                else
                {
                    typeId = e.GetTypeId();
                    if (typeId == ElementId.InvalidElementId)
                    {
                        typeId = GetPseudoTypeId(e);
                        typeName = "默认 (" + catName + ")";
                        familyName = e.Category.Name;
                        if (!seenTypes.Contains(typeId))
                        {
                            seenTypes.Add(typeId);
                            catNode.AddType(familyName, typeName, typeId);
                        }
                        continue;
                    }

                    ElementType type = doc.GetElement(typeId) as ElementType;
                    if (type == null)
                        continue;
                    typeName = type.Name;
                }

                if (seenTypes.Contains(typeId))
                    continue;
                seenTypes.Add(typeId);

                if (e is FamilyInstance fi && fi.Symbol?.Family != null)
                {
                    familyName = fi.Symbol.Family.Name;
                }
                else if (doc.GetElement(typeId) is FamilySymbol fs && fs.Family != null)
                {
                    familyName = fs.Family.Name;
                }
                else
                {
                    familyName = "系统族: " + catName;
                }

                catNode.AddType(familyName, typeName, typeId);
            }

            return categoryMap.Values.ToList();
        }

        private static bool MatchesSelectedTypes(Element e, HashSet<ElementId> selectedTypeIds)
        {
            if (e is ElementType et)
                return selectedTypeIds.Contains(et.Id);
            ElementId typeId = e.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
                return selectedTypeIds.Contains(typeId);
            return e.Category != null && selectedTypeIds.Contains(GetPseudoTypeId(e));
        }
    }
}
