using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Bulico
{
    public class ProgressWindow : Window
    {
        private ProgressBar _bar;
        private Label _label;
        private string _title;

        public ProgressWindow(string title)
        {
            _title = title;
            Title = title;
            Width = 380;
            Height = 105;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;
            ShowInTaskbar = false;

            Grid grid = new Grid();
            grid.Margin = new Thickness(12, 10, 12, 10);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _label = new Label
            {
                Content = "",
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(0)
            };
            Grid.SetRow(_label, 0);

            _bar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Height = 24
            };
            Grid.SetRow(_bar, 2);

            grid.Children.Add(_label);
            grid.Children.Add(_bar);
            Content = grid;
        }

        public void SetRange(int maximum)
        {
            Dispatcher.Invoke(new System.Action(() =>
            {
                _bar.Maximum = maximum;
                _bar.Value = 0;
            }), DispatcherPriority.Background);
        }

        public void Update(int current, int total)
        {
            Dispatcher.Invoke(new System.Action(() =>
            {
                _bar.Value = current;
                _label.Content = string.Format("{0}... {1}/{2}", _title, current, total);
            }), DispatcherPriority.Background);
        }

        public void SetText(string text)
        {
            Dispatcher.Invoke(new System.Action(() =>
            {
                _label.Content = text;
            }), DispatcherPriority.Background);
        }

        public void Pump()
        {
            Dispatcher.Invoke(new System.Action(() => { }), DispatcherPriority.Background);
        }
    }
}
