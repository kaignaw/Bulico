using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;

namespace Bulico
{
    public partial class DimTypeSelector : Window
    {
        public DimensionType SelectedDimType { get; private set; }

        public DimTypeSelector(Document doc, DimensionType preselectedType = null)
        {
            InitializeComponent();

            var dimTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .Where(d => d.StyleType == DimensionStyleType.Linear)
                .OrderBy(d => d.Name)
                .ToList();

            DimTypeListBox.ItemsSource = dimTypes;

            if (preselectedType != null)
            {
                for (int i = 0; i < dimTypes.Count; i++)
                {
                    if (dimTypes[i].Id == preselectedType.Id)
                    {
                        DimTypeListBox.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (DimTypeListBox.SelectedIndex < 0 && dimTypes.Count > 0)
                DimTypeListBox.SelectedIndex = 0;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedDimType = DimTypeListBox.SelectedItem as DimensionType;
            if (SelectedDimType == null)
            {
                MessageBox.Show("请选择一个尺寸标注类型。", "提示",
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
