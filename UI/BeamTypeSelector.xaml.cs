using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;

namespace Bulico
{
    public class BeamTypeItem
    {
        public string DisplayName { get; set; }
        public FamilySymbol Symbol { get; set; }
    }

    public partial class BeamTypeSelector : Window
    {
        public FamilySymbol SelectedBeamType { get; private set; }

        public BeamTypeSelector(Document doc, FamilySymbol preselectedType = null)
        {
            InitializeComponent();

            var beamSymbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(fs => fs.Category != null &&
                    fs.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralFraming)
                .OrderBy(fs => fs.Family.Name)
                .ThenBy(fs => fs.Name)
                .Select(fs => new BeamTypeItem
                {
                    DisplayName = fs.Family.Name + "-" + fs.Name,
                    Symbol = fs
                })
                .ToList();

            BeamTypeListBox.ItemsSource = beamSymbols;

            if (preselectedType != null)
            {
                for (int i = 0; i < beamSymbols.Count; i++)
                {
                    if (beamSymbols[i].Symbol.Id == preselectedType.Id)
                    {
                        BeamTypeListBox.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (BeamTypeListBox.SelectedIndex < 0 && beamSymbols.Count > 0)
                BeamTypeListBox.SelectedIndex = 0;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            BeamTypeItem selectedItem = BeamTypeListBox.SelectedItem as BeamTypeItem;
            if (selectedItem == null)
            {
                MessageBox.Show("请选择一个梁类型。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SelectedBeamType = selectedItem.Symbol;
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
