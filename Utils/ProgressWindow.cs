using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Bulico
{
    public class ProgressWindow : Window, IDisposable
    {
        private const int PumpIntervalMs = 100;

        private readonly ProgressBar _bar;
        private readonly Label _label;
        private readonly string _title;
        private bool _canClose;
        private DateTime _lastPumpUtc = DateTime.MinValue;

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

            // 让进度窗跟随 Revit 主窗口，避免出现在主窗口后面被误认为卡死。
            try
            {
                IntPtr owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                if (owner != IntPtr.Zero)
                    new WindowInteropHelper(this).Owner = owner;
            }
            catch
            {
                // 取不到主窗口句柄时保持默认行为。
            }
        }

        /// <summary>
        /// 命令执行期间禁止用户关闭进度窗，避免后续 Pump 变成空操作。
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_canClose)
                e.Cancel = true;

            base.OnClosing(e);
        }

        /// <summary>
        /// 关闭进度窗并允许后续窗口关闭事件通过。重复调用是安全的：
        /// 命令会先 Finish() 再弹结果对话框，最后在 finally 中 Dispose() 兜底。
        /// </summary>
        public void Finish()
        {
            if (_canClose)
                return;

            _canClose = true;
            try
            {
                base.Close();
            }
            catch
            {
                // 窗口已关闭或已脱离可视化树。
            }
        }

        /// <summary>
        /// 与 using 配合使用；等价于 Finish()。不要直接调用 base Close()，
        /// 命令执行期间窗口会被 OnClosing 拒绝关闭。
        /// </summary>
        public void Dispose()
        {
            Finish();
        }

        public void SetRange(int maximum)
        {
            RunOnUi(() =>
            {
                _bar.Maximum = maximum > 0 ? maximum : 1;
                _bar.Value = 0;
            });
            _lastPumpUtc = DateTime.MinValue;
            Pump();
        }

        public void Update(int current, int total)
        {
            RunOnUi(() =>
            {
                _bar.Value = current;
                _label.Content = string.Format("{0}... {1}/{2}", _title, current, total);
            });
            PumpIfDue();
        }

        public void SetText(string text)
        {
            RunOnUi(() => { _label.Content = text; });
        }

        public void Pump()
        {
            if (Dispatcher == null || Dispatcher.HasShutdownStarted)
                return;

            try
            {
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Background);
                _lastPumpUtc = DateTime.UtcNow;
            }
            catch
            {
                // 泵消息失败不应影响命令本身。
            }
        }

        private void PumpIfDue()
        {
            if ((DateTime.UtcNow - _lastPumpUtc).TotalMilliseconds < PumpIntervalMs)
                return;

            Pump();
        }

        private void RunOnUi(Action action)
        {
            if (action == null)
                return;

            try
            {
                if (Dispatcher == null || Dispatcher.HasShutdownStarted)
                    return;

                if (Dispatcher.CheckAccess())
                    action();
                else
                    Dispatcher.Invoke(action, DispatcherPriority.Background);
            }
            catch
            {
                // 窗口已关闭时忽略界面更新。
            }
        }
    }
}
