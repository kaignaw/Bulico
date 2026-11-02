using System;
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
            if (!CommandGuard.TryGetDocument(commandData, "一键门窗类型标记", out UIDocument uidoc, out Document doc))
                return Result.Cancelled;

            ProgressWindow progress = null;
            try
            {
                List<FamilySymbol> doorAndWindowTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(fs =>
                    {
                        Category cat = fs.Category;
                        if (cat == null) return false;
                        BuiltInCategory bic = (BuiltInCategory)cat.Id.Value;
                        return bic == BuiltInCategory.OST_Doors || bic == BuiltInCategory.OST_Windows;
                    })
                    .ToList();

                if (doorAndWindowTypes.Count == 0)
                {
                    TaskDialog.Show("一键门窗类型标记", "项目中未找到门窗类型。");
                    return Result.Cancelled;
                }

                progress = new ProgressWindow("一键门窗类型标记");
                progress.Show();
                progress.SetText("正在修改门窗类型标记...");
                progress.SetRange(doorAndWindowTypes.Count);

                int modifiedCount = 0;
                int skippedCount = 0;

                using (Transaction trans = new Transaction(doc, "一键门窗类型标记"))
                {
                    TransactionUtil.Prepare(trans, false);
                    trans.Start();

                    for (int i = 0; i < doorAndWindowTypes.Count; i++)
                    {
                        FamilySymbol fs = doorAndWindowTypes[i];

                        Parameter markParam = fs.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK)
                            ?? fs.Parameters.Cast<Parameter>().FirstOrDefault(p =>
                                p.Definition != null && (p.Definition.Name == "类型标记" || p.Definition.Name == "Type Mark"));

                        if (markParam == null || markParam.IsReadOnly)
                        {
                            skippedCount++;
                        }
                        else
                        {
                            string typeName = fs.Name;
                            if (markParam.AsString() != typeName)
                            {
                                markParam.Set(typeName);
                                modifiedCount++;
                            }
                        }

                        progress.Update(i + 1, doorAndWindowTypes.Count);
                    }

                    trans.Commit();
                }

                progress.Finish();

                string summary;
                if (modifiedCount > 0)
                {
                    summary = string.Format("修改完成，共修改了 {0} 个门窗的类型标记。", modifiedCount);
                }
                else
                {
                    summary = "所有门窗的类型标记都已是最新，未做修改。";
                }

                if (skippedCount > 0)
                {
                    summary += string.Format(
                        "\n有 {0} 个类型没有可写的“类型标记”参数，已跳过。",
                        skippedCount);
                }

                TaskDialog.Show("一键门窗类型标记", summary);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                ErrorReporter.Show("一键门窗类型标记", "修改门窗类型标记失败，本次操作未保存。", ex);
                message = "修改门窗类型标记失败，请查看本地日志。";
                return Result.Failed;
            }
            finally
            {
                if (progress != null)
                    progress.Dispose();
            }
        }
    }
}
