using System;
using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB;

namespace Bulico
{
    /// <summary>
    /// 楼板创建参数。房间建板与墙梁建板共用同一套输入项，
    /// 集中在一处避免两个命令各自维护一份界面与校验。
    /// </summary>
    public class FloorSettings
    {
        public FloorType FloorType { get; set; }
        public Level Level { get; set; }
        public double OffsetMeters { get; set; }
        public bool UseCenterline { get; set; } = true;
    }

    public partial class FloorSettingsDialog : Window
    {
        private readonly bool _showModeOption;
        private FloorSettings _result;

        public FloorSettingsDialog(
            string title,
            List<FloorType> floorTypes,
            List<Level> levels,
            ElementId initialTypeId,
            ElementId initialLevelId,
            double initialOffsetMeters,
            bool initialUseCenterline,
            bool showModeOption)
        {
            InitializeComponent();

            Title = title;
            _showModeOption = showModeOption;

            TypeCombo.ItemsSource = floorTypes;
            LevelCombo.ItemsSource = levels;

            TypeCombo.SelectedIndex = IndexOf(floorTypes, type => type.Id, initialTypeId);
            LevelCombo.SelectedIndex = IndexOf(levels, level => level.Id, initialLevelId);
            OffsetBox.Text = initialOffsetMeters.ToString("F3");

            CenterlineRadio.IsChecked = showModeOption ? initialUseCenterline : true;
            InnerRadio.IsChecked = !CenterlineRadio.IsChecked;

            if (!showModeOption)
            {
                // 房间建板不需要“内边线”模式，整行隐藏而不是留一个无意义的选项。
                ModeLabel.Visibility = System.Windows.Visibility.Collapsed;
                ModePanel.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        public FloorSettings Result
        {
            get { return _result; }
        }

        /// <summary>
        /// 弹出对话框并记录本次选择。取消时返回 null。
        /// </summary>
        public static FloorSettings Show(
            Document doc,
            string commandKey,
            List<FloorType> floorTypes,
            List<Level> levels,
            bool showModeOption)
        {
            ElementId typeId;
            ElementId levelId;
            double offsetMeters;
            bool useCenterline;
            FloorSettingsCache.TryGetInitial(
                commandKey, doc,
                out typeId, out levelId, out offsetMeters, out useCenterline);

            FloorSettingsDialog dialog = new FloorSettingsDialog(
                commandKey, floorTypes, levels,
                typeId, levelId, offsetMeters, useCenterline, showModeOption);

            if (dialog.ShowDialog() != true || dialog.Result == null)
                return null;

            FloorSettingsCache.Store(commandKey, doc, dialog.Result);
            return dialog.Result;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            FloorType floorType = TypeCombo.SelectedItem as FloorType;
            Level level = LevelCombo.SelectedItem as Level;
            if (floorType == null || level == null)
            {
                MessageBox.Show("请选择楼板类型和标高。", "提示");
                return;
            }

            double offsetMeters;
            if (!double.TryParse(OffsetBox.Text, out offsetMeters) ||
                !InputLimits.IsValidFloorOffset(offsetMeters))
            {
                MessageBox.Show(
                    string.Format(
                        "标高偏移请输入 -{0:0.###} 到 {0:0.###} 之间的有效数值。",
                        InputLimits.MaxFloorOffsetMeters),
                    "提示");
                return;
            }

            _result = new FloorSettings
            {
                FloorType = floorType,
                Level = level,
                OffsetMeters = offsetMeters,
                UseCenterline = !_showModeOption || CenterlineRadio.IsChecked == true
            };

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private static int IndexOf<T>(IList<T> items, Func<T, ElementId> idOf, ElementId target)
        {
            if (target != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (idOf(items[i]) == target)
                        return i;
                }
            }

            return items.Count > 0 ? 0 : -1;
        }
    }
}
