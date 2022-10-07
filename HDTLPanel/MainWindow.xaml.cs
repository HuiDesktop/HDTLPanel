using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using MouseButtons = System.Windows.Forms.MouseButtons;
using ToolTipIcon = System.Windows.Forms.ToolTipIcon;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace HDTLPanel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainWindowDataContext context = new();
        private readonly NotifyIcon notifyIcon = new();
        private ProcessManager? manager;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = context;

            var args = new List<string>(Environment.GetCommandLineArgs()[1..]);
            context.AutoRunEnabled = !args.Exists((s) => s == "--dist");

            notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
            notifyIcon.ContextMenuStrip = new();
            notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("退出"));
            notifyIcon.ContextMenuStrip.Items[0].Click += (_, _) => Close();
            notifyIcon.MouseClick += (o, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    Visibility = Visibility.Visible;
                    ShowInTaskbar = true;
                    WindowState = WindowState.Normal;
                    Activate();
                }
            };
            SwitchSubprogramRunningStatus(null, new());
            WindowState = WindowState.Minimized;
            Window_StateChanged(null, EventArgs.Empty);
        }

        private async void SwitchSubprogramRunningStatus(object? sender, RoutedEventArgs e)
        {
            if (context.IsRunning)
            {
                if (manager is not null)
                {
                    context.IsBusyClosing = true;
                    context.IsChanged = false;
                    MainStackPanel.Children.Clear();
                    manager.TryCloseWindow();
                    await Task.Delay(1000);
                    if (context.IsRunning)
                    {
                        manager.ForceCloseWindow();
                    }
                    manager.Dispose();
                    manager = null;
                    context.IsBusyClosing = false;
                }
            }
            else
            {
                var args = new List<string>(Environment.GetCommandLineArgs()[1..]);
                if (args.FirstOrDefault() == "--autorun")
                {
                    args.RemoveAt(0);
                }

                string? logPath = null;
                foreach (var arg in args)
                {
                    if (arg.StartsWith("config=")) logPath = arg[7..];
                }

                context.IsRunning = true;
                manager = new ProcessManager(
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app/luajit.exe"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app"),
                    args,
                    () => Dispatcher.Invoke(ReadIpc),
                    logPath);
                manager.Exited += (_, _) =>
                {
                    context.IsRunning = false;
                    context.IsChanged = false;
                    Dispatcher.Invoke(() =>
                    {
                        MainStackPanel.Children.Clear();
                        if (WindowState == WindowState.Minimized)
                        {
                            WindowState = WindowState.Normal;
                        }
                    });
                };
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (manager is not null)
            {
                manager.TryCloseWindow();
                Thread.Sleep(2000);
                if (context.IsRunning)
                {
                    manager.ForceCloseWindow();
                }
                manager.Dispose();
            }

            notifyIcon.Visible = false;
        }

        private void SaveConfigSub(System.Collections.ICollection ss, ManagedIpc.IpcWriter w)
        {
            foreach (var i in ss)
            {
                if (i is TabControl t)
                {
                    foreach (var ii in t.Items)
                    {
                        if (((ii as TabItem)?.Content as StackPanel)?.Children is not null and var x)
                        {
                            SaveConfigSub(x, w);
                        }
                    }
                }
                else
                {
                    (i as ISaveableControl)?.Save(w);
                }
            }
        }

        private void SaveConfig(object sender, RoutedEventArgs e)
        {
            if (manager is null) throw new NullReferenceException();
            using var w = manager.txIpc.BeginWrite();
            w.Write(2);
            SaveConfigSub(MainStackPanel.Children, w);
            w.Write(0);
            context.IsChanged = false;
        }

        private void DiscardConfigChange(object sender, RoutedEventArgs e)
        {
            if (manager is null) throw new NullReferenceException();
            using var w = manager.txIpc.BeginWrite();
            w.Write(3);
        }

        private void FlipModel(object sender, RoutedEventArgs e)
        {
            if (manager is not null)
            {
                using var writer = manager.txIpc.BeginWrite();
                writer.Write(1);
            }
        }

        private void ReadIpc()
        {
            var reader = manager?.rxIpc.GetReader();
            var tabStack = new List<TabControl>();
            var tabItems = new List<StackPanel>();
            var index = 0;
            if (reader is not null)
            {
                while (reader.Next())
                {
                    index++;
                    object? c = null;
                    switch (reader.ReadInt())
                    {
                        case 0:
                            index--;
                            MainStackPanel.Children.Clear();
                            context.IsChanged = false;
                            break;
                        case 1:
                            c = new SingleLineTextControl(index, reader);
                            break;
                        case 2:
                            c = new BoolControl(index, reader);
                            break;
                        case 3:
                            c = new ReadonlyTextControl(reader.ReadString());
                            break;
                        case 4:
                            c = new ButtonControl(index, manager!.txIpc, reader);
                            break;
                        case 5:
                            tabStack.Add(new());
                            c = tabStack.Last();
                            break;
                        case 6:
                            if (tabItems.Count == tabStack.Count)
                            {
                                tabItems.RemoveAt(tabItems.Count - 1);
                            }
                            StackPanel grid = new();
                            tabItems.Add(grid);
                            tabStack.Last().Items.Add(new TabItem() { Header = reader.ReadString(), Content = grid });
                            break;
                        case 7:
                            if (tabItems.Count == tabStack.Count)
                            {
                                tabItems.RemoveAt(tabItems.Count - 1);
                            }
                            tabStack.RemoveAt(tabStack.Count - 1);
                            break;
                    }
                    if (c is ISaveableControl sc)
                    {
                        sc.PropertyChanged += (_, _) => context.IsChanged = true;
                    }
                    if (c is UIElement ec)
                    {
                        if (tabItems.Count > 0)
                        {
                            tabItems.Last().Children.Add(ec);
                        }
                        else
                        {
                            MainStackPanel.Children.Add(ec);
                        }
                    }
                }
            }
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
                ShowInTaskbar = false;
                Hide();
                notifyIcon.Visible = true;
                var tag = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".notified");
                if (!File.Exists(tag))
                {
                    File.Create(tag).Close();
                    notifyIcon.ShowBalloonTip(1000, "HuiDesktop Light", "单击还原喵", ToolTipIcon.Info);
                }
            }
        }

        private void ChangeAutoRun(object sender, RoutedEventArgs e)
        {
            context.IsAutoRun = !context.IsAutoRun;
        }
    }

    class MainWindowDataContext : INotifyPropertyChanged
    {
        private bool isRunning;
        private bool isBusyClosing;
        private bool isChanged;
        private bool autoRunEnabled;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsRunning
        {
            get => isRunning;
            set
            {
                isRunning = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusyClosing
        {
            get => isBusyClosing;
            set
            {
                isBusyClosing = value;
                OnPropertyChanged();
            }
        }

        public bool IsChanged
        {
            get => isChanged;
            set
            {
                isChanged = value;
                OnPropertyChanged();
            }
        }

        public bool IsAutoRun
        {
            get => AutoRun.IsAutoRun(AppDomain.CurrentDomain.BaseDirectory + "HDTLPanel.exe", "HuiDesktop启动器与控制面板");
            set
            {
                AutoRun.SetAutoRun(AppDomain.CurrentDomain.BaseDirectory + "HDTLPanel.exe", "HuiDesktop启动器与控制面板", value);
                OnPropertyChanged();
            }
        }

        public bool AutoRunEnabled
        {
            get => autoRunEnabled;
            set
            {
                autoRunEnabled = value;
                OnPropertyChanged();
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class BoolToStringValueConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool v && parameter is string s) return s.Split('/')[v ? 1 : 0];
            return null;
        }

        public object? ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class ReverseBooleanValueConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool v) return !v;
            return null;
        }

        public object? ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
