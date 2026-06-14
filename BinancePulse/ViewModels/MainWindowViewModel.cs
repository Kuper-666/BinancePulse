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
        private string _status = "Остановлен";
        private string _balance = "0.00";

        public string Status { get => _status; set { _status = value; OnPropertyChanged (); } }
        public string Balance { get => _balance; set { _balance = value; OnPropertyChanged (); } }

        public Action<string>? AddLog { get; set; }

        public MainWindowViewModel(TradingService tradingService)
        {
            _tradingService = tradingService;
            _tradingService.OnLogGenerated += msg => AddLog?.Invoke (msg);
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (name));
    }
}