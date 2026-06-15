using System.Windows;
using BinancePulse.ViewModels;

namespace BinancePulse
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel? _vm;

        public MainWindow()
        {
            InitializeComponent ();
            Loaded += (s, e) =>
            {
                _vm = DataContext as MainWindowViewModel;
                if (_vm != null)
                    _vm.AddLog = (msg) => Dispatcher.Invoke (() => LogTextBox.AppendText (msg + "\n"));
            };
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_vm != null) await _vm.Start ();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _vm?.Stop ();
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_vm != null) await _vm.CheckForUpdates ();
        }
    }
}