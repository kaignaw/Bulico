using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;

namespace Bulico
{
    public partial class WallTypeSelector : Window
    {
        public WallType SelectedWallType { get; private set; }

        public WallTypeSelector(Document doc, WallType preselectedType = null)
        {
            InitializeComponent();

            var wallTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .OrderBy(wt => wt.Name)
                .ToList();

            WallTypeListBox.ItemsSource = wallTypes;

            if (preselectedType != null)
            {
                for (int i = 0; i < wallTypes.Count; i++)
                {
                    if (wallTypes[i].Id == preselectedType.Id)
                    {
                        WallTypeListBox.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (WallTypeListBox.SelectedIndex < 0 && wallTypes.Count > 0)
                WallTypeListBox.SelectedIndex = 0;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedWallType = WallTypeListBox.SelectedItem as WallType;
            if (SelectedWallType == null)
            {
                MessageBox.Show("请选择一个墙体类型。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
