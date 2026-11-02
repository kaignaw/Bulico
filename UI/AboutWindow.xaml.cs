using System;
using System.Reflection;
using System.Windows;

namespace Bulico
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();

            // 版本号统一来自 AssemblyInfo，避免关于界面与程序集版本各写一份。
            string version = FormatVersion();
            VersionText.Text = version;
            ProductText.Text = "Bulico for Revit 2026.5+\n" + version;
        }

        private static string FormatVersion()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null)
                return "V1.3.00";

            return string.Format("V{0}.{1}.{2:00}", version.Major, version.Minor, version.Build);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
