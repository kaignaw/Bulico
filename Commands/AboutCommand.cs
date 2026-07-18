using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Bulico
{
    [Transaction(TransactionMode.Manual)]
    public class AboutCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            TaskDialog dialog = new TaskDialog("About Bulico")
            {
                Title = "关于Bulico",
                MainIcon = TaskDialogIcon.TaskDialogIconInformation,
                MainInstruction = "关于Bulico",
                MainContent = "Bulico for Revit 2020\nV1.2.00\n\nThe MIT License (MIT)\nCopyright © 2026 Kaignaw\n\n功能列表:\n• 框轴生墙 - 框选轴网交点生成墙体\n• 框墙生梁 - 墙体顶部生成结构梁\n• 墙梁建板 - 围合区域批量创建楼板\n• 房间建板 - 根据房间轮廓创建楼板（支持内环开洞）\n• 立面轴网标注 - 在立面/剖面视图中为框选轴网添加尺寸标注\n• 一键横挺 - 幕墙横向网格线生成\n• 一键门窗类型标记 - 批量标记门窗类型\n• 同型过滤 - 框选过滤同类型构件\n• 类别过滤 - 框选过滤同类别构件\n• 精细过滤 - 类型树结构精确筛选",
                CommonButtons = TaskDialogCommonButtons.Close
            };

            dialog.Show();

            return Result.Succeeded;
        }
    }
}
