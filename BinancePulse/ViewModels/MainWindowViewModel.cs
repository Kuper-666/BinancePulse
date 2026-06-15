using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using BinancePulse.Models;
using BinancePulse.Services;

namespace BinancePulse.ViewModels
{
    public class PairAnalysisItem : INotifyPropertyChanged
    {
        private string _price;
        private string _analysis;
        public string Pair { get; set; }
        public string Price { get => _price; set { _price = value; OnPropertyChanged (); } }
        public string Analysis { get => _analysis; set { _analysis = value; OnPropertyChanged (); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (name));
    }

    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly TradingService _tradingService;
        private readonly UpdateManager _updateManager;
        private string _status = "Остановлен";
        private string _balance = "0.00";
        private string _version = "1.0.0";
        private string _updateStatus = "";
        private bool _isUpdating = false;
        private DispatcherTimer _balanceTimer;

        public ObservableCollection<PairAnalysisItem> PairsList { get; set; } = new ();
        public ObservableCollection<TradeLog> TradesHistory { get; set; } = new ();

        public string Status { get => _status; set { _status = value; OnPropertyChanged (); } }
        public string Balance { get => _balance; set { _balance = value; OnPropertyChanged (); } }
        public string Version { get => _version; set { _version = value; OnPropertyChanged (); } }
        public string UpdateStatus { get => _updateStatus; set { _updateStatus = value; OnPropertyChanged (); } }
        public bool IsUpdating { get => _isUpdating; set { _isUpdating = value; OnPropertyChanged (); } }

        public Action<string>? AddLog { get; set; }

        public MainWindowViewModel(TradingService tradingService, UpdateManager updateManager)
        {
            _tradingService = tradingService;
            _updateManager = updateManager;
            _tradingService.OnLogGenerated += msg => AddLog?.Invoke (msg);
            _tradingService.OnTradeClosed += OnTradeClosed;
            _version = _updateManager.GetCurrentVersion ().ToString ();

            _balanceTimer = new DispatcherTimer ();
            _balanceTimer.Interval = TimeSpan.FromSeconds (30);
            _balanceTimer.Tick += async (s, e) => await UpdateBalanceAsync ();
            _balanceTimer.Start ();

            // Подписка на обновление пар из TradingService (можно через событие)
            _tradingService.OnMarketUpdate += UpdateMarketTable;
        }

        private async Task UpdateBalanceAsync()
        {
            try
            {
                var balance = await _tradingService.GetUsdcBalanceAsync ();
                Balance = balance.ToString ("F2");
            }
            catch { }
        }

        private void OnTradeClosed(TradeLog trade)
        {
            Application.Current.Dispatcher.Invoke (() =>
            {
                TradesHistory.Insert (0, trade);
                if (TradesHistory.Count > 100) TradesHistory.RemoveAt (TradesHistory.Count - 1);
            });
        }

        private void UpdateMarketTable(string symbol, decimal price, TradeAction action)
        {
            Application.Current.Dispatcher.Invoke (() =>
            {
                var existing = PairsList.FirstOrDefault (p => p.Pair == symbol);
                if (existing != null)
                {
                    existing.Price = price.ToString ("F4");
                    existing.Analysis = action.ToString ();
                }
                else
                {
                    PairsList.Add (new PairAnalysisItem { Pair = symbol, Price = price.ToString ("F4"), Analysis = action.ToString () });
                }
            });
        }

        public async Task Start()
        {
            Status = "Запуск...";
            _tradingService.Start ();
            Status = "Работает";
            await UpdateBalanceAsync ();
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
            UpdateStatus = "Проверка обновлений...";
            AddLog?.Invoke ("🔍 Проверка обновлений...");

            bool updated = await _updateManager.CheckAndUpdateAsync (silent: false);
            if (!updated)
            {
                UpdateStatus = "У вас последняя версия";
                AddLog?.Invoke ("✅ Установлена актуальная версия.");
                await Task.Delay (3000);
                UpdateStatus = "";
            }
            IsUpdating = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (name));
    }
}