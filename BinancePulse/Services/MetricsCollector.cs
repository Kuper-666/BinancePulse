using System;
using System.Collections.Concurrent;
using System.Threading;

namespace BinancePulse.Services
{
    public static class MetricsCollector
    {
        private static long _apiCalls = 0;
        private static long _ordersPlaced = 0;
        private static DateTime _startTime = DateTime.UtcNow;
        private static ConcurrentDictionary<string, int> _errors = new ();

        public static void IncrementApiCall() => Interlocked.Increment (ref _apiCalls);
        public static void IncrementOrder() => Interlocked.Increment (ref _ordersPlaced);
        public static void RecordError(string context) => _errors.AddOrUpdate (context, 1, (k, v) => v + 1);

        public static string GetReport()
        {
            return $"📊 <b>Метрики BinancePulse</b>\n" +
                   $"⏱️ Время работы: {( DateTime.UtcNow - _startTime ):hh\\:mm\\:ss}\n" +
                   $"📡 API запросов: {_apiCalls}\n" +
                   $"📦 Ордеров: {_ordersPlaced}\n" +
                   $"⚠️ Ошибок: {_errors.Count}\n" +
                   $"📈 Детали ошибок:\n{string.Join ("\n", _errors)}";
        }
    }
}