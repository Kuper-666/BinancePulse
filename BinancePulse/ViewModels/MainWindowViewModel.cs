using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BinancePulse.Services;

namespace BinancePulse.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly TradingService _tradingService;
        private readonly UpdateManager _updateManager;
        private string _status = "Остановлен";
        private string _balance = "0.00";
        private string _version = "1.0.0";
        private bool _isUpdating = false;

        public string Status { get => _status; set { _status = value; OnPropertyChanged (); } }
        public string Balance { get => _balance; set { _balance = value; OnPropertyChanged (); } }
        public string Version { get => _version; set { _version = value; OnPropertyChanged (); } }
        public bool IsUpdating { get => _isUpdating; set { _isUpdating = value; OnPropertyChanged (); } }

        public Action<string>? AddLog { get; set; }

        public MainWindowViewModel(TradingService tradingService, UpdateManager updateManager)
        {
            _tradingService = tradingService;
            _updateManager = updateManager;
            _tradingService.OnLogGenerated += msg => AddLog?.Invoke (msg);
            _version = _updateManager.GetCurrentVersion ().ToString ();
        }

        public async Task Start()
        {
            Status = "Запуск...";
            _tradingService.Start ();
            Status = "Работает";
            await Task.Delay (500);
            var balance = await _tradingService.GetUsdcBalanceAsync ();
            Balance = balance.ToString ("F2");
        }

        public void Stop()
        {
            _tradingService.Stop ();
            Status = "Остановлен";
        }

        public async Task CheckForUpdates()
        {
            if (IsUpdating) return;
            IsUpdating = true;
            AddLog?.Invoke ("🔍 Проверка обновлений...");
            bool updated = await _updateManager.CheckAndUpdateAsync (silent: false);
            if (!updated)
            {
                AddLog?.Invoke ("✅ Установлена актуальная версия.");
            }
            IsUpdating = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (name));
    }
}