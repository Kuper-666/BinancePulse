using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using BinancePulse.Models;
using BinancePulse.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Linq;

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
        private string _pauseStatus = "";
        private DispatcherTimer _balanceTimer;
        private DispatcherTimer _positionsTimer;

        public ObservableCollection<PairAnalysisItem> PairsList { get; set; } = new ();
        public ObservableCollection<TradeLog> TradesHistory { get; set; } = new ();
        public ObservableCollection<PositionDisplayItem> Positions { get; set; } = new ();

        public string Status { get => _status; set { _status = value; OnPropertyChanged (); } }
        public string Balance { get => _balance; set { _balance = value; OnPropertyChanged (); } }
        public string Version { get => _version; set { _version = value; OnPropertyChanged (); } }
        public string UpdateStatus { get => _updateStatus; set { _updateStatus = value; OnPropertyChanged (); } }
        public bool IsUpdating { get => _isUpdating; set { _isUpdating = value; OnPropertyChanged (); } }
        public string PauseStatus { get => _pauseStatus; set { _pauseStatus = value; OnPropertyChanged (); } }
        public BacktestViewModel Backtest { get; set; }

        public Action<string>? AddLog { get; set; }

        private PlotModel _balancePlotModel;
        public PlotModel BalancePlotModel
        {
            get => _balancePlotModel;
            set { _balancePlotModel = value; OnPropertyChanged (); }
        }

        private readonly object _plotLock = new object ();
        private LineSeries _balanceSeries;
        private ScatterSeries _tradeMarkers;

        public MainWindowViewModel(TradingService tradingService, UpdateManager updateManager, BacktestViewModel backtest)
        {
            _tradingService = tradingService;
            _updateManager = updateManager;

            _tradingService.OnLogGenerated += msg => AddLog?.Invoke (msg);
            _tradingService.OnTradeClosed += OnTradeClosed;
            _tradingService.OnBalanceUpdate += UpdateBalancePoint;
            _tradingService.OnMarketUpdate += UpdateMarketTable;
            _tradingService.OnPositionChanged += RefreshPositions;
            _tradingService.OnPauseStatusChanged += isPaused =>
            {
                PauseStatus = isPaused ? "⏸ ПАУЗА" : "";
            };

            _version = _updateManager.GetCurrentVersion ().ToString ();

            _balanceTimer = new DispatcherTimer ();
            _balanceTimer.Interval = TimeSpan.FromSeconds (30);
            _balanceTimer.Tick += async (s, e) => await UpdateBalanceAsync ();
            _balanceTimer.Start ();

            _positionsTimer = new DispatcherTimer ();
            _positionsTimer.Interval = TimeSpan.FromSeconds (5);
            _positionsTimer.Tick += (s, e) => UpdatePositions ();
            _positionsTimer.Start ();

            InitializePlot ();
            Backtest = backtest;
        }

        private void InitializePlot()
        {
            BalancePlotModel = new PlotModel
            {
                Title = "Баланс USDC",
                Background = OxyColors.Transparent,
                TextColor = OxyColors.White,
                TitleColor = OxyColors.White,
                PlotAreaBorderColor = OxyColors.DimGray
            };

            BalancePlotModel.Axes.Add (new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "HH:mm",
                Title = "Время",
                TitleColor = OxyColors.White,
                AxislineColor = OxyColors.White,
                TicklineColor = OxyColors.White,
                TextColor = OxyColors.White,
                Angle = -45,
                IntervalType = DateTimeIntervalType.Minutes,
                Minimum = DateTimeAxis.ToDouble (DateTime.Now.AddHours (-6)),
                Maximum = DateTimeAxis.ToDouble (DateTime.Now)
            });

            BalancePlotModel.Axes.Add (new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "USDC",
                TitleColor = OxyColors.White,
                AxislineColor = OxyColors.White,
                TicklineColor = OxyColors.White,
                TextColor = OxyColors.White,
                Minimum = 0,
                MaximumPadding = 0.2
            });

            _balanceSeries = new LineSeries
            {
                Color = OxyColors.LimeGreen,
                MarkerType = MarkerType.Circle,
                MarkerSize = 3,
                MarkerFill = OxyColors.LimeGreen,
                StrokeThickness = 2
            };
            BalancePlotModel.Series.Add (_balanceSeries);

            _tradeMarkers = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerSize = 8,
                MarkerStroke = OxyColors.White,
                MarkerStrokeThickness = 1
            };
            BalancePlotModel.Series.Add (_tradeMarkers);

            BalancePlotModel.InvalidatePlot (true);
        }

        private async Task UpdateBalanceAsync()
        {
            try
            {
                var balance = await _tradingService.GetUsdcBalanceAsync ();
                Balance = balance.ToString ("F2");
                UpdateBalancePoint (DateTime.Now, balance);
            }
            catch { }
        }

        private void UpdateBalancePoint(DateTime time, decimal balance)
        {
            Application.Current.Dispatcher.Invoke (() =>
            {
                lock (_plotLock)
                {
                    var point = new DataPoint (DateTimeAxis.ToDouble (time), (double)balance);
                    _balanceSeries.Points.Add (point);
                    if (_balanceSeries.Points.Count > 200)
                        _balanceSeries.Points.RemoveAt (0);
                    BalancePlotModel.InvalidatePlot (true);
                }
            });
        }

        private void OnTradeClosed(TradeLog trade)
        {
            Application.Current.Dispatcher.Invoke (() =>
            {
                TradesHistory.Insert (0, trade);
                if (TradesHistory.Count > 100) TradesHistory.RemoveAt (TradesHistory.Count - 1);

                lock (_plotLock)
                {
                    var point = new ScatterPoint (
                        DateTimeAxis.ToDouble (trade.CloseTime),
                        (double)( trade.ExitPrice * trade.Quantity ),
                        1);
                    _tradeMarkers.Points.Add (point);
                    BalancePlotModel.InvalidatePlot (true);
                }
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

        private void RefreshPositions()
        {
            Application.Current.Dispatcher.Invoke (() =>
            {
                var updated = _tradingService.GetPositionsWithCurrentPrice ();
                Positions.Clear ();
                foreach (var item in updated)
                    Positions.Add (item);
            });
        }

        private void UpdatePositions()
        {
            if (Positions.Count == 0) return;
            var updated = _tradingService.GetPositionsWithCurrentPrice ();
            for (int i = 0; i < Positions.Count && i < updated.Count; i++)
            {
                Positions[i].CurrentPrice = updated[i].CurrentPrice;
            }
        }

        public async Task Start()
        {
            Status = "Запуск...";
            _tradingService.Start ();
            Status = "Работает";
            await UpdateBalanceAsync ();
            RefreshPositions ();
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

        public async Task SendDailyReport()
        {
            await _tradingService.SendManualReport ();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (name));
    }
}