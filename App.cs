using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace Bulico
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            RibbonPanel panel = application.CreateRibbonPanel("Bulico");

            AddPushButton(panel, "GridWallGenerator", "框轴生墙",
                typeof(GridWallCommand).FullName,
                "从框选的轴网交点生成墙体",
                "Bulico.Resources.icon_gridwall_32.png",
                "Bulico.Resources.icon_gridwall_16.png");

            AddPushButton(panel, "WallBeamGenerator", "框墙生梁",
                typeof(BeamCommand).FullName,
                "在选中的墙体顶部生成结构梁",
                "Bulico.Resources.icon_beam_32.png",
                "Bulico.Resources.icon_beam_16.png");

            AddPushButton(panel, "BatchFloor", "批量建板",
                typeof(BatchFloorCommand).FullName,
                "在围合区域内批量创建楼板",
                "Bulico.Resources.icon_floor_32.png",
                "Bulico.Resources.icon_floor_16.png");

            AddPushButton(panel, "OneKeyHorizontalMullion", "一键横挺",
                typeof(MullionCommand).FullName,
                "在幕墙上批量生成横向网格线",
                "Bulico.Resources.icon_mullion_32.png",
                "Bulico.Resources.icon_mullion_16.png");

            AddPushButton(panel, "DoorWindowMark", "一键门窗类型标记",
                typeof(DoorWindowMarkCommand).FullName,
                "将项目中所有门窗的类型名称写入类型标记",
                "Bulico.Resources.icon_doorwin_32.png",
                "Bulico.Resources.icon_doorwin_16.png");

            AddPushButton(panel, "SameTypeFilter", "同型过滤",
                typeof(SameTypeCommand).FullName,
                "框选范围内仅保留与预选构件同类型的构件",
                "Bulico.Resources.icon_filter_32.png",
                "Bulico.Resources.icon_filter_16.png");

            AddPushButton(panel, "CategoryFilter", "类别过滤",
                typeof(CategoryFilterCommand).FullName,
                "框选范围内仅保留与预选构件同类别的构件",
                "Bulico.Resources.icon_category_32.png",
                "Bulico.Resources.icon_category_16.png");

            AddPushButton(panel, "FineFilter", "精细过滤",
                typeof(FineFilterCommand).FullName,
                "通过类型树形结构精确筛选所需构件",
                "Bulico.Resources.icon_fine_32.png",
                "Bulico.Resources.icon_fine_16.png");

            AddPushButton(panel, "About", "关于",
                typeof(AboutCommand).FullName,
                "查看插件版本与更新信息",
                "Bulico.Resources.icon_about_32.png",
                "Bulico.Resources.icon_about_16.png");

            return Result.Succeeded;
        }

        private void AddPushButton(RibbonPanel panel, string internalName,
            string text, string commandFullName, string tooltip,
            string largeIcon, string smallIcon)
        {
            PushButtonData buttonData = new PushButtonData(
                internalName, text,
                typeof(App).Assembly.Location,
                commandFullName);

            buttonData.ToolTip = tooltip;

            var assembly = Assembly.GetExecutingAssembly();

            using (var stream = assembly.GetManifestResourceStream(largeIcon))
            {
                if (stream != null)
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.DecodePixelWidth = 32;
                    bitmap.DecodePixelHeight = 32;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    buttonData.LargeImage = bitmap;
                }
            }

            using (var stream = assembly.GetManifestResourceStream(smallIcon))
            {
                if (stream != null)
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.DecodePixelWidth = 16;
                    bitmap.DecodePixelHeight = 16;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    buttonData.Image = bitmap;
                }
            }

            panel.AddItem(buttonData);
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
