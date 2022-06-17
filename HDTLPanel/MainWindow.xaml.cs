using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HDTLPanel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly MainWindowDataContext context = new();
        readonly TempConfigDataContext tempConfigData = new("settings.json");

        ProcessManager? manager;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = context;
            ConfigZoneGrid.DataContext = tempConfigData;
        }

        async private void SwitchSubprogramRunningStatus(object sender, RoutedEventArgs e)
        {
            if (context.IsRunning)
            {
                if (manager is not null)
                {
                    context.IsBusyClosing = true;
                    manager.TryCloseWindow();
                    await Task.Delay(1000);
                    if (context.IsRunning)
                    {
                        manager.ForceCloseWindow();
                    }
                    manager = null;
                    context.IsBusyClosing = false;
                }
            }
            else
            {
                context.IsRunning = true;
                manager = new ProcessManager("app/luajit.exe", System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app"), "main.lua");
                manager.Exited += (_, _) =>
                {
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
            }
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!int.TryParse(e.Text, out _))
            {
                e.Handled = true;
            }
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }
    }

    class TempConfigDataContext : INotifyPropertyChanged
    {
        record class TempConfigFile(int Fps);
        private string fps = "0";
        private bool changed = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public TempConfigDataContext(string path)
        {
            if (File.Exists(path))
            {
                var r = System.Text.Json.JsonSerializer.Deserialize<TempConfigFile>(File.ReadAllText(path));
                if (r is not null)
                {
                    fps = r.Fps.ToString();
                }
            }
        }

        public string Fps { get => fps; set { Changed = true; fps=value; } }
        public bool Changed { get => changed; set { changed=value; OnPropertyChanged(); } }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    class MainWindowDataContext : INotifyPropertyChanged
    {
        private bool isRunning = false;
        private bool isBusyClosing = false;

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
