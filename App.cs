using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace Bulico
{
    public class App : IExternalApplication
    {
        private const string TabName = "Bulico";

        public Result OnStartup(UIControlledApplication application)
        {
            EnsureRibbonTab(application);

            RibbonPanel modelingPanel = GetOrCreatePanel(application, "建模");
            RibbonPanel annotationPanel = GetOrCreatePanel(application, "注释");
            RibbonPanel efficiencyPanel = GetOrCreatePanel(application, "效率");
            RibbonPanel otherPanel = GetOrCreatePanel(application, "其他");

            AddPushButton(modelingPanel, "GridWallGenerator", "框轴生墙",
                typeof(GridWallCommand).FullName,
                "从框选的轴网交点生成墙体",
                Icon("grid_wall", 32),
                Icon("grid_wall", 16));

            AddPushButton(modelingPanel, "WallBeamGenerator", "框墙生梁",
                typeof(BeamCommand).FullName,
                "在选中的墙体顶部生成结构梁",
                Icon("wall_beam", 32),
                Icon("wall_beam", 16));

            AddPushButton(modelingPanel, "WallBeamFloor", "墙梁建板",
                typeof(WallBeamFloorCommand).FullName,
                "在围合区域内批量创建楼板",
                Icon("wall_beam_floor", 32),
                Icon("wall_beam_floor", 16));

            AddPushButton(modelingPanel, "RoomFloor", "房间建板",
                typeof(RoomFloorCommand).FullName,
                "根据房间轮廓批量创建楼板",
                Icon("room_floor", 32),
                Icon("room_floor", 16));

            AddPushButton(modelingPanel, "OneKeyHorizontalMullion", "一键横挺",
                typeof(MullionCommand).FullName,
                "在幕墙上批量生成横向网格线",
                Icon("mullion", 32),
                Icon("mullion", 16));

            AddPushButton(annotationPanel, "ElevationGridMark", "立面轴网标注",
                typeof(ElevationGridMarkCommand).FullName,
                "在立面/剖面视图中为框选轴网批量添加尺寸标注",
                Icon("elevation_grid", 32),
                Icon("elevation_grid", 16));

            AddPushButton(annotationPanel, "DoorWindowMark", "一键门窗类型标记",
                typeof(DoorWindowMarkCommand).FullName,
                "将项目中所有门窗的类型名称写入类型标记",
                Icon("door_window_mark", 32),
                Icon("door_window_mark", 16));

            AddPushButton(efficiencyPanel, "SameTypeFilter", "同型过滤",
                typeof(SameTypeCommand).FullName,
                "框选范围内仅保留与预选构件同类型的构件",
                Icon("same_type", 32),
                Icon("same_type", 16));

            AddPushButton(efficiencyPanel, "CategoryFilter", "类别过滤",
                typeof(CategoryFilterCommand).FullName,
                "框选范围内仅保留与预选构件同类别的构件",
                Icon("category_filter", 32),
                Icon("category_filter", 16));

            AddPushButton(efficiencyPanel, "FineFilter", "精细过滤",
                typeof(FineFilterCommand).FullName,
                "通过类型树形结构精确筛选所需构件",
                Icon("fine_filter", 32),
                Icon("fine_filter", 16));

            AddPushButton(otherPanel, "About", "关于",
                typeof(AboutCommand).FullName,
                "查看插件版本与更新信息",
                Icon("about", 32),
                Icon("about", 16));

            return Result.Succeeded;
        }

        private static void EnsureRibbonTab(UIControlledApplication application)
        {
            try
            {
                application.CreateRibbonTab(TabName);
            }
            catch
            {
                // 选项卡已存在（例如插件被重复加载）时复用，不中断启动。
            }
        }

        private static RibbonPanel GetOrCreatePanel(UIControlledApplication application, string panelName)
        {
            try
            {
                foreach (RibbonPanel panel in application.GetRibbonPanels(TabName))
                {
                    if (panel.Name == panelName)
                        return panel;
                }
            }
            catch
            {
                // 选项卡尚不存在时忽略查询失败，直接创建面板。
            }

            return application.CreateRibbonPanel(TabName, panelName);
        }

        private static string Icon(string name, int size)
        {
            return $"Bulico.Brand.Icons.{name}_{size}.png";
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
            SetButtonImage(buttonData, largeIcon, smallIcon);
            panel.AddItem(buttonData);
        }

        private void SetButtonImage(PushButtonData buttonData,
            string largeIcon, string smallIcon)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using (var stream = assembly.GetManifestResourceStream(largeIcon))
            {
                if (stream != null)
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
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
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.DecodePixelWidth = 16;
                    bitmap.DecodePixelHeight = 16;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    buttonData.Image = bitmap;
                }
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
