# BinancePulse

Автоматический торговый бот для Binance (Spot) на базе SMA, RSI и ATR с адаптивным риск-менеджментом.

## 📦 Возможности
- Адаптивный ATR и трейлинг-стоп
- Бэктест и оптимизатор параметров
- Telegram-уведомления и команды
- График баланса, история сделок, позиции в реальном времени
- Шифрование API-ключей
- Автообновление через GitHub Releases
- Поддержка нескольких стратегий

## 🚀 Установка
1. Скачайте последний релиз с [GitHub Releases](https://github.com/Kuper-666/BinancePulse/releases).
2. Запустите установщик (`.exe`) или распакуйте `.zip`.
3. Настройте `appsettings.json` (API-ключи, Telegram, параметры).
4. Запустите `BinancePulse.exe`.

## ⚙️ Настройка
- `ApiKey` / `ApiSecret` – получите на Binance.
- `IsEncrypted` – после первого запуска ключи зашифруются.
- `Telegram.BotToken` и `Telegram.ChatId` – для уведомлений.
- `Trading` – параметры стратегии, размер позиции, стоп-лосс, тейк-профит.
- `SelectedStrategy` – выбор стратегии (Sma или RsiBollinger).

## 🧪 Тестирование
Используйте тестовую сеть Binance (`UseTestnet: true`), чтобы проверить работу без реальных денег.

## 🔧 Сборка из исходников
```bash
git clone https://github.com/Kuper-666/BinancePulse.git
cd BinancePulse
dotnet restore
dotnet build -c Release