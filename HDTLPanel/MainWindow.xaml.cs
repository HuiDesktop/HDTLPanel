using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace HDTLPanel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string settingsPath = "settings.json";
        readonly MainWindowDataContext context = new();
        ProcessManager? manager;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = context;

            foreach (var i in MainStackPanel.Children)
            {
                if (i is INotifyPropertyChanged n)
                {
                    n.PropertyChanged += (_, _) =>
                    {
                        context.IsChanged = true;
                    };
                }
            }
        }

        async private void SwitchSubprogramRunningStatus(object sender, RoutedEventArgs e)
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
                context.IsRunning = true;
                manager = new ProcessManager("app/luajit.exe", System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app"), "main.lua", () => Dispatcher.Invoke(ReadIpc));
                manager.Exited += (_, _) =>
                {
                    System.Diagnostics.Debug.WriteLine(manager.process.StandardError.ReadToEnd());
                    context.IsRunning = false;
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
        }

        private void SaveConfig(object sender, RoutedEventArgs e)
        {
            if (manager is null) throw new NullReferenceException();
            using var w = manager.txIpc.BeginWrite();
            w.Write(2);
            foreach (var i in MainStackPanel.Children)
            {
                (i as ISaveableControl)?.Save(w);
            }
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
            if (reader is not null)
            {
                while (reader.Next())
                {
                    switch (reader.ReadInt())
                    {
                        case 0:
                            MainStackPanel.Children.Clear();
                            context.IsChanged = false;
                            break;
                        case 1:
                            {
                                SingleLineTextControl c = new(MainStackPanel.Children.Count + 1);
                                c.PromptText = reader.ReadString();
                                c.HintText = reader.ReadString();
                                if (reader.ReadInt() == 1)
                                {
                                    c.Type = SingleLineTextControl.SingleLineTextType.Integer;
                                    c.InputContent = reader.ReadInt().ToString();
                                }
                                c.PropertyChanged += (_, _) => context.IsChanged = true;
                                MainStackPanel.Children.Add(c);
                            }
                            break;
                        case 2:
                            {
                                BoolControl c = new(MainStackPanel.Children.Count + 1);
                                c.PromptText = reader.ReadString();
                                c.HintText = reader.ReadString();
                                c.Choice = reader.ReadInt() != 0;
                                c.PropertyChanged += (_, _) => context.IsChanged = true;
                                MainStackPanel.Children.Add(c);
                            }
                            break;
                    }
                }
            }
        }
    }

    class MainWindowDataContext : INotifyPropertyChanged
    {
        private bool isRunning = false;
        private bool isBusyClosing = false;
        private bool isChanged = false;

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
