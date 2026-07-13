using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;

namespace Bulico
{
    public partial class FilterDialog : Window, INotifyPropertyChanged
    {
        private readonly List<CategoryNode> _categories;
        private string _selectedCountText = "选定的项目总数：0";

        public string SelectedCountText
        {
            get => _selectedCountText;
            set { _selectedCountText = value; OnPropertyChanged(); }
        }

        public FilterDialog(List<CategoryNode> categories)
        {
            InitializeComponent();
            _categories = categories;
            FilterTree.ItemsSource = categories;
            RecalcCount();
        }

        public HashSet<ElementId> GetSelectedTypeIds()
        {
            var result = new HashSet<ElementId>();
            foreach (var cat in _categories)
                CollectSelected(cat, result);
            return result;
        }

        private static void CollectSelected(CategoryNode cat, HashSet<ElementId> result)
        {
            if (cat.IsChecked == true)
            {
                foreach (var fam in cat.Families)
                    foreach (var type in fam.Types)
                        result.Add(type.TypeId);
                return;
            }

            foreach (var fam in cat.Families)
            {
                if (fam.IsChecked == true)
                {
                    foreach (var type in fam.Types)
                        result.Add(type.TypeId);
                }
                else if (fam.IsChecked == null)
                {
                    foreach (var type in fam.Types)
                        if (type.IsChecked == true)
                            result.Add(type.TypeId);
                }
            }
        }

        private void RecalcCount()
        {
            int count = 0;
            foreach (var cat in _categories)
            {
                foreach (var fam in cat.Families)
                {
                    foreach (var type in fam.Types)
                    {
                        if (type.IsChecked == true)
                            count++;
                    }
                }
            }
            SelectedCountText = "选定的项目总数：" + count.ToString();
        }

        private void UpdateCategoryChildren(CategoryNode cat, bool isChecked)
        {
            foreach (var fam in cat.Families)
            {
                fam.IsChecked = isChecked;
                foreach (var type in fam.Types)
                    type.IsChecked = isChecked;
            }
        }

        private void UpdateFamilyChildren(FamilyNode fam, bool isChecked)
        {
            foreach (var type in fam.Types)
                type.IsChecked = isChecked;
        }

        private void OnParentCheckBoxClick(object sender, RoutedEventArgs e)
        {
            var cb = sender as CheckBox;
            if (cb?.DataContext == null) return;

            if (cb.DataContext is CategoryNode cat)
            {
                if (cat.IsChecked == null)
                {
                    cat.IsChecked = false;
                    UpdateCategoryChildren(cat, false);
                }
                else if (cat.IsChecked == true)
                {
                    UpdateCategoryChildren(cat, true);
                }
                else
                {
                    UpdateCategoryChildren(cat, false);
                }
            }
            else if (cb.DataContext is FamilyNode fam)
            {
                if (fam.IsChecked == null)
                {
                    fam.IsChecked = false;
                    UpdateFamilyChildren(fam, false);
                    if (fam.ParentCategory != null)
                        SyncCategoryState(fam.ParentCategory);
                }
                else if (fam.IsChecked == true)
                {
                    UpdateFamilyChildren(fam, true);
                    if (fam.ParentCategory != null)
                        SyncCategoryState(fam.ParentCategory);
                }
                else
                {
                    UpdateFamilyChildren(fam, false);
                    if (fam.ParentCategory != null)
                        SyncCategoryState(fam.ParentCategory);
                }
            }

            RecalcCount();
        }

        private void OnTypeCheckBoxClick(object sender, RoutedEventArgs e)
        {
            var cb = sender as CheckBox;
            if (cb?.DataContext is TypeNode typeNode && typeNode.ParentFamily != null)
                SyncFamilyState(typeNode.ParentFamily);

            RecalcCount();
        }

        private static void SyncFamilyState(FamilyNode family)
        {
            bool allChecked = family.Types.All(t => t.IsChecked == true);
            bool allUnchecked = family.Types.All(t => t.IsChecked == false);

            family.IsChecked = allChecked ? true : allUnchecked ? false : (bool?)null;

            if (family.ParentCategory != null)
                SyncCategoryState(family.ParentCategory);
        }

        private static void SyncCategoryState(CategoryNode cat)
        {
            bool allChecked = cat.Families.All(f => f.IsChecked == true);
            bool allUnchecked = cat.Families.All(f => f.IsChecked == false);

            cat.IsChecked = allChecked ? true : allUnchecked ? false : (bool?)null;
        }

        private void OnSelectAll(object sender, RoutedEventArgs e)
        {
            foreach (var cat in _categories)
            {
                cat.IsChecked = true;
                foreach (var fam in cat.Families)
                {
                    fam.IsChecked = true;
                    foreach (var type in fam.Types)
                        type.IsChecked = true;
                }
            }
            RecalcCount();
        }

        private void OnDeselectAll(object sender, RoutedEventArgs e)
        {
            foreach (var cat in _categories)
            {
                cat.IsChecked = false;
                foreach (var fam in cat.Families)
                {
                    fam.IsChecked = false;
                    foreach (var type in fam.Types)
                        type.IsChecked = false;
                }
            }
            RecalcCount();
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void ExpandAllRecursive(ItemsControl parent)
        {
            for (int i = 0; i < parent.Items.Count; i++)
            {
                var item = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (item != null)
                {
                    item.IsExpanded = true;
                    item.UpdateLayout();
                    ExpandAllRecursive(item);
                }
            }
        }

        private void CollapseToFirstLevel(ItemsControl parent)
        {
            for (int i = 0; i < parent.Items.Count; i++)
            {
                var item = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (item != null)
                {
                    item.IsExpanded = false;
                    item.UpdateLayout();
                }
            }
        }

        private void OnExpandAll(object sender, RoutedEventArgs e)
        {
            ExpandAllRecursive(FilterTree);
        }

        private void OnCollapseAll(object sender, RoutedEventArgs e)
        {
            CollapseToFirstLevel(FilterTree);
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class CategoryNode : INotifyPropertyChanged
    {
        private bool? _isChecked = false;
        public string Name { get; }
        public ObservableCollection<FamilyNode> Families { get; }
        public bool? IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; OnPropertyChanged(); }
        }

        public CategoryNode(string name)
        {
            Name = name;
            Families = new ObservableCollection<FamilyNode>();
        }

        public void AddType(string familyName, string typeName, ElementId typeId)
        {
            var family = Families.FirstOrDefault(f => f.Name == familyName);
            if (family == null)
            {
                family = new FamilyNode(familyName) { ParentCategory = this };
                Families.Add(family);
            }
            family.AddType(typeName, typeId);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class FamilyNode : INotifyPropertyChanged
    {
        private bool? _isChecked = false;
        public string Name { get; }
        public ObservableCollection<TypeNode> Types { get; }
        public CategoryNode ParentCategory { get; set; }
        public bool? IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; OnPropertyChanged(); }
        }

        public FamilyNode(string name)
        {
            Name = name;
            Types = new ObservableCollection<TypeNode>();
        }

        public void AddType(string typeName, ElementId typeId)
        {
            if (!Types.Any(t => t.Name == typeName))
                Types.Add(new TypeNode(typeName, typeId) { ParentFamily = this });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class TypeNode : INotifyPropertyChanged
    {
        private bool? _isChecked = false;
        public string Name { get; }
        public ElementId TypeId { get; }
        public FamilyNode ParentFamily { get; set; }
        public bool? IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; OnPropertyChanged(); }
        }

        public TypeNode(string name, ElementId typeId)
        {
            Name = name;
            TypeId = typeId;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
