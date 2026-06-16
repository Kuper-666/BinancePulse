using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BinancePulse.Models;
using BinancePulse.Services;

namespace BinancePulse.ViewModels
{
    public class OptimizeResult
    {
        public Dictionary<string, object> Parameters { get; set; }
        public BacktestResult Result { get; set; }
    }

    public class BacktestViewModel : INotifyPropertyChanged
    {
        private readonly BacktestEngine _backtestEngine;

        // Параметры бэктеста (те же, что были)
        private string _symbol = "BTCUSDC";
        private DateTime _startDate = DateTime.Now.AddDays (-30);
        private DateTime _endDate = DateTime.Now;
        private int _fastSma = 9;
        private int _slowSma = 21;
        private int _rsiPeriod = 14;
        private int _rsiBuy = 30;
        private int _rsiSell = 70;
        private decimal _stopLoss = 0.02m;
        private decimal _takeProfit = 0.04m;
        private decimal _initialCapital = 1000m;
        private bool _isRunning;
        private string _status = "Готов";
        private BacktestResult _result;
        private ObservableCollection<BacktestTrade> _trades = new ();

        // Параметры оптимизации
        private bool _optimizeFastSma = true;
        private int _fastSmaMin = 5;
        private int _fastSmaMax = 15;
        private int _fastSmaStep = 2;

        private bool _optimizeSlowSma = true;
        private int _slowSmaMin = 15;
        private int _slowSmaMax = 50;
        private int _slowSmaStep = 5;

        private bool _optimizeRsiBuy = true;
        private int _rsiBuyMin = 20;
        private int _rsiBuyMax = 40;
        private int _rsiBuyStep = 5;

        private bool _optimizeRsiSell = true;
        private int _rsiSellMin = 60;
        private int _rsiSellMax = 80;
        private int _rsiSellStep = 5;

        private bool _optimizeStopLoss = true;
        private decimal _stopLossMin = 0.01m;
        private decimal _stopLossMax = 0.03m;
        private decimal _stopLossStep = 0.005m;

        private bool _optimizeTakeProfit = true;
        private decimal _takeProfitMin = 0.02m;
        private decimal _takeProfitMax = 0.06m;
        private decimal _takeProfitStep = 0.005m;

        private int _optimizeTopCount = 5;
        private ObservableCollection<OptimizeResult> _optimizationResults = new ();

        // Свойства для бэктеста (оставляем)
        public string Symbol { get => _symbol; set { _symbol = value; OnPropertyChanged (); } }
        public DateTime StartDate { get => _startDate; set { _startDate = value; OnPropertyChanged (); } }
        public DateTime EndDate { get => _endDate; set { _endDate = value; OnPropertyChanged (); } }
        public int FastSma { get => _fastSma; set { _fastSma = value; OnPropertyChanged (); } }
        public int SlowSma { get => _slowSma; set { _slowSma = value; OnPropertyChanged (); } }
        public int RsiPeriod { get => _rsiPeriod; set { _rsiPeriod = value; OnPropertyChanged (); } }
        public int RsiBuy { get => _rsiBuy; set { _rsiBuy = value; OnPropertyChanged (); } }
        public int RsiSell { get => _rsiSell; set { _rsiSell = value; OnPropertyChanged (); } }
        public decimal StopLoss { get => _stopLoss; set { _stopLoss = value; OnPropertyChanged (); } }
        public decimal TakeProfit { get => _takeProfit; set { _takeProfit = value; OnPropertyChanged (); } }
        public decimal InitialCapital { get => _initialCapital; set { _initialCapital = value; OnPropertyChanged (); } }
        public bool IsRunning { get => _isRunning; set { _isRunning = value; OnPropertyChanged (); } }
        public string Status { get => _status; set { _status = value; OnPropertyChanged (); } }
        public BacktestResult Result { get => _result; set { _result = value; OnPropertyChanged (); } }
        public ObservableCollection<BacktestTrade> Trades { get => _trades; set { _trades = value; OnPropertyChanged (); } }

        // Свойства оптимизации
        public bool OptimizeFastSma { get => _optimizeFastSma; set { _optimizeFastSma = value; OnPropertyChanged (); } }
        public int FastSmaMin { get => _fastSmaMin; set { _fastSmaMin = value; OnPropertyChanged (); } }
        public int FastSmaMax { get => _fastSmaMax; set { _fastSmaMax = value; OnPropertyChanged (); } }
        public int FastSmaStep { get => _fastSmaStep; set { _fastSmaStep = value; OnPropertyChanged (); } }
        public bool OptimizeSlowSma { get => _optimizeSlowSma; set { _optimizeSlowSma = value; OnPropertyChanged (); } }
        public int SlowSmaMin { get => _slowSmaMin; set { _slowSmaMin = value; OnPropertyChanged (); } }
        public int SlowSmaMax { get => _slowSmaMax; set { _slowSmaMax = value; OnPropertyChanged (); } }
        public int SlowSmaStep { get => _slowSmaStep; set { _slowSmaStep = value; OnPropertyChanged (); } }
        public bool OptimizeRsiBuy { get => _optimizeRsiBuy; set { _optimizeRsiBuy = value; OnPropertyChanged (); } }
        public int RsiBuyMin { get => _rsiBuyMin; set { _rsiBuyMin = value; OnPropertyChanged (); } }
        public int RsiBuyMax { get => _rsiBuyMax; set { _rsiBuyMax = value; OnPropertyChanged (); } }
        public int RsiBuyStep { get => _rsiBuyStep; set { _rsiBuyStep = value; OnPropertyChanged (); } }
        public bool OptimizeRsiSell { get => _optimizeRsiSell; set { _optimizeRsiSell = value; OnPropertyChanged (); } }
        public int RsiSellMin { get => _rsiSellMin; set { _rsiSellMin = value; OnPropertyChanged (); } }
        public int RsiSellMax { get => _rsiSellMax; set { _rsiSellMax = value; OnPropertyChanged (); } }
        public int RsiSellStep { get => _rsiSellStep; set { _rsiSellStep = value; OnPropertyChanged (); } }
        public bool OptimizeStopLoss { get => _optimizeStopLoss; set { _optimizeStopLoss = value; OnPropertyChanged (); } }
        public decimal StopLossMin { get => _stopLossMin; set { _stopLossMin = value; OnPropertyChanged (); } }
        public decimal StopLossMax { get => _stopLossMax; set { _stopLossMax = value; OnPropertyChanged (); } }
        public decimal StopLossStep { get => _stopLossStep; set { _stopLossStep = value; OnPropertyChanged (); } }
        public bool OptimizeTakeProfit { get => _optimizeTakeProfit; set { _optimizeTakeProfit = value; OnPropertyChanged (); } }
        public decimal TakeProfitMin { get => _takeProfitMin; set { _takeProfitMin = value; OnPropertyChanged (); } }
        public decimal TakeProfitMax { get => _takeProfitMax; set { _takeProfitMax = value; OnPropertyChanged (); } }
        public decimal TakeProfitStep { get => _takeProfitStep; set { _takeProfitStep = value; OnPropertyChanged (); } }
        public int OptimizeTopCount { get => _optimizeTopCount; set { _optimizeTopCount = value; OnPropertyChanged (); } }
        public ObservableCollection<OptimizeResult> OptimizationResults { get => _optimizationResults; set { _optimizationResults = value; OnPropertyChanged (); } }

        public ICommand RunBacktestCommand { get; }
        public ICommand OptimizeCommand { get; }

        public BacktestViewModel(BacktestEngine backtestEngine)
        {
            _backtestEngine = backtestEngine;
            RunBacktestCommand = new RelayCommand (async _ => await RunBacktestAsync (), _ => !IsRunning);
            OptimizeCommand = new RelayCommand (async _ => await RunOptimizationAsync (), _ => !IsRunning);
        }

        private async Task RunBacktestAsync()
        {
            IsRunning = true;
            Status = "Загрузка данных...";
            StatusColor = "Yellow"; // можно добавить свойство для цвета
            Trades.Clear ();

            try
            {
                var result = await _backtestEngine.RunAsync (
                    Symbol,
                    StartDate,
                    EndDate,
                    FastSma,
                    SlowSma,
                    RsiPeriod,
                    RsiBuy,
                    RsiSell,
                    StopLoss,
                    TakeProfit,
                    InitialCapital
                );

                Result = result;
                if (result.Trades != null)
                {
                    foreach (var trade in result.Trades)
                        Trades.Add (trade);
                }
                Status = $"✅ Завершено. Сделок: {result.TotalTrades}, Доходность: {result.TotalReturnPercent:F2}%";
                StatusColor = "LimeGreen";
            }
            catch (Exception ex)
            {
                Status = $"❌ Ошибка: {ex.Message}";
                StatusColor = "Red";
                // Дополнительно вывести в лог, если есть доступ
                System.Diagnostics.Debug.WriteLine ($"Backtest error: {ex}");
                // Если есть возможность, вызвать AddLog (но его нет в этом классе, можно передать через конструктор)
            }
            finally
            {
                IsRunning = false;
            }
        }

        private string _statusColor = "Yellow";
        public string StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged (); }
        }

        private async Task RunOptimizationAsync()
        {
            IsRunning = true;
            Status = "Оптимизация...";
            OptimizationResults.Clear ();

            try
            {
                // Генерируем все комбинации параметров
                var combinations = GenerateParameterCombinations ();
                int total = combinations.Count;
                int processed = 0;
                var bestResults = new List<OptimizeResult> ();

                Status = $"Оптимизация: 0/{total}";

                foreach (var combo in combinations)
                {
                    processed++;
                    if (processed % 5 == 0)
                        Status = $"Оптимизация: {processed}/{total}";

                    var result = await _backtestEngine.RunAsync (
                        Symbol,
                        StartDate,
                        EndDate,
                        combo.FastSma,
                        combo.SlowSma,
                        RsiPeriod,
                        combo.RsiBuy,
                        combo.RsiSell,
                        combo.StopLoss,
                        combo.TakeProfit,
                        InitialCapital
                    );

                    if (result.TotalTrades >= 5) // Минимальное число сделок для стабильности
                    {
                        bestResults.Add (new OptimizeResult
                        {
                            Parameters = new Dictionary<string, object>
                            {
                                ["FastSma"] = combo.FastSma,
                                ["SlowSma"] = combo.SlowSma,
                                ["RsiBuy"] = combo.RsiBuy,
                                ["RsiSell"] = combo.RsiSell,
                                ["StopLoss"] = combo.StopLoss,
                                ["TakeProfit"] = combo.TakeProfit
                            },
                            Result = result
                        });
                    }
                }

                // Сортируем по доходности (можно изменить на Sharpe или ProfitFactor)
                var sorted = bestResults.OrderByDescending (r => r.Result.TotalReturnPercent).Take (OptimizeTopCount).ToList ();

                foreach (var item in sorted)
                    OptimizationResults.Add (item);

                Status = $"Оптимизация завершена. Найдено {sorted.Count} лучших комбинаций.";
            }
            catch (Exception ex)
            {
                Status = $"Ошибка оптимизации: {ex.Message}";
            }
            finally
            {
                IsRunning = false;
            }
        }

        private List<(int FastSma, int SlowSma, int RsiBuy, int RsiSell, decimal StopLoss, decimal TakeProfit)> GenerateParameterCombinations()
        {
            var combinations = new List<(int, int, int, int, decimal, decimal)> ();

            var fastRange = OptimizeFastSma ? Enumerable.Range (FastSmaMin, ( FastSmaMax - FastSmaMin ) / FastSmaStep + 1).Select (x => x * FastSmaStep) : new[] { FastSma };
            var slowRange = OptimizeSlowSma ? Enumerable.Range (SlowSmaMin, ( SlowSmaMax - SlowSmaMin ) / SlowSmaStep + 1).Select (x => x * SlowSmaStep) : new[] { SlowSma };
            var rsiBuyRange = OptimizeRsiBuy ? Enumerable.Range (RsiBuyMin, ( RsiBuyMax - RsiBuyMin ) / RsiBuyStep + 1).Select (x => x * RsiBuyStep) : new[] { RsiBuy };
            var rsiSellRange = OptimizeRsiSell ? Enumerable.Range (RsiSellMin, ( RsiSellMax - RsiSellMin ) / RsiSellStep + 1).Select (x => x * RsiSellStep) : new[] { RsiSell };
            var stopLossRange = OptimizeStopLoss ? GenerateDecimalRange (StopLossMin, StopLossMax, StopLossStep) : new[] { StopLoss };
            var takeProfitRange = OptimizeTakeProfit ? GenerateDecimalRange (TakeProfitMin, TakeProfitMax, TakeProfitStep) : new[] { TakeProfit };

            // Фильтруем: SlowSma > FastSma и TakeProfit > StopLoss
            foreach (var fast in fastRange.Where (x => x > 0))
            {
                foreach (var slow in slowRange.Where (x => x > fast))
                {
                    foreach (var rsiB in rsiBuyRange)
                    {
                        foreach (var rsiS in rsiSellRange.Where (x => x > rsiB))
                        {
                            foreach (var sl in stopLossRange)
                            {
                                foreach (var tp in takeProfitRange.Where (x => x > sl))
                                {
                                    combinations.Add ((fast, slow, rsiB, rsiS, sl, tp));
                                }
                            }
                        }
                    }
                }
            }

            return combinations;
        }

        private IEnumerable<decimal> GenerateDecimalRange(decimal min, decimal max, decimal step)
        {
            for (decimal d = min; d <= max + step / 2; d += step)
                yield return Math.Round (d, 4);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (name));
    }
}