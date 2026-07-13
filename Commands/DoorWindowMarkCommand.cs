using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Bulico
{
    [Transaction(TransactionMode.Manual)]
    public class DoorWindowMarkCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            List<FamilySymbol> doorAndWindowTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(fs =>
                {
                    Category cat = fs.Category;
                    if (cat == null) return false;
                    BuiltInCategory bic = (BuiltInCategory)cat.Id.IntegerValue;
                    return bic == BuiltInCategory.OST_Doors || bic == BuiltInCategory.OST_Windows;
                })
                .ToList();

            if (doorAndWindowTypes.Count == 0)
            {
                TaskDialog.Show("一键门窗类型标记", "项目中未找到门窗类型。");
                return Result.Succeeded;
            }

            ProgressWindow progress = new ProgressWindow("一键门窗类型标记");
            progress.Show();
            progress.SetText("正在修改门窗类型标记...");
            progress.Pump();
            progress.SetRange(doorAndWindowTypes.Count);

            int modifiedCount = 0;

            using (Transaction trans = new Transaction(doc, "一键门窗类型标记"))
            {
                trans.Start();

                for (int i = 0; i < doorAndWindowTypes.Count; i++)
                {
                    FamilySymbol fs = doorAndWindowTypes[i];

                    Parameter markParam = fs.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK)
                        ?? fs.Parameters.Cast<Parameter>().FirstOrDefault(p =>
                            p.Definition != null && (p.Definition.Name == "类型标记" || p.Definition.Name == "Type Mark"));
                    if (markParam != null && !markParam.IsReadOnly)
                    {
                        string typeName = fs.Name;
                        if (markParam.AsString() != typeName)
                        {
                            markParam.Set(typeName);
                            modifiedCount++;
                        }
                    }

                    progress.Update(i + 1, doorAndWindowTypes.Count);
                    progress.Pump();
                }

                trans.Commit();
            }

            progress.Close();

            TaskDialog.Show("一键门窗类型标记",
                string.Format("修改成功，共修改了 {0} 个门窗的类型标记。", modifiedCount));

            return Result.Succeeded;
        }
    }
}
