using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace BinancePulse.Services
{
    public class UpdateManager
    {
        private const string GitHubOwner = "Kuper-666";
        private const string GitHubRepo = "BinancePulse";   // название вашего репозитория
        private static readonly Version CurrentVersion = Assembly.GetExecutingAssembly ().GetName ().Version ?? new Version ("1.0.0");
        private readonly HttpClient _httpClient;
        private readonly Action<string> _logger;

        public UpdateManager(Action<string> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient ();
            _httpClient.DefaultRequestHeaders.Add ("User-Agent", "BinancePulse/1.0");
            _httpClient.DefaultRequestHeaders.Add ("Accept", "application/vnd.github.v3+json");
        }

        public Version GetCurrentVersion() => CurrentVersion;

        public async Task<bool> CheckAndUpdateAsync(bool silent = false)
        {
            try
            {
                _logger?.Invoke ("🔍 Проверка обновлений...");
                string apiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases";
                var response = await _httpClient.GetAsync (apiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger?.Invoke ($"❌ GitHub API вернул ошибку: {response.StatusCode}");
                    return false;
                }

                string json = await response.Content.ReadAsStringAsync ();
                var releases = JArray.Parse (json);
                if (releases.Count == 0)
                {
                    _logger?.Invoke ("⚠️ Релизы не найдены.");
                    return false;
                }

                var latestRelease = releases
                    .OrderByDescending (r => r["published_at"]?.Value<DateTime> () ?? DateTime.MinValue)
                    .First ();
                string latestTag = latestRelease["tag_name"]?.ToString () ?? "v0.0.0";
                string latestVersionStr = latestTag.TrimStart ('v');
                if (!Version.TryParse (latestVersionStr, out var latestVersion))
                {
                    _logger?.Invoke ($"⚠️ Не удалось разобрать версию {latestVersionStr}");
                    return false;
                }

                if (latestVersion <= CurrentVersion)
                {
                    _logger?.Invoke ("✅ Установлена актуальная версия.");
                    return false;
                }

                _logger?.Invoke ($"✨ Новая версия: {latestVersion} (текущая: {CurrentVersion})");

                if (!silent && MessageBox.Show ($"Доступна версия {latestVersion}. Обновить сейчас?",
                                                "Обновление", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return false;
                }

                // Скачивание и установка
                var asset = latestRelease["assets"]?.FirstOrDefault (a => a["name"].ToString ().EndsWith (".zip"));
                if (asset == null)
                {
                    _logger?.Invoke ("⚠️ Не найден архив для скачивания.");
                    return false;
                }

                string downloadUrl = asset["browser_download_url"].ToString ();
                return await DownloadAndInstall (downloadUrl, latestTag);
            }
            catch (Exception ex)
            {
                _logger?.Invoke ($"❌ Ошибка проверки обновлений: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> DownloadAndInstall(string downloadUrl, string newVersion)
        {
            try
            {
                _logger?.Invoke ($"📥 Загрузка обновления {newVersion}...");
                string tempZip = Path.GetTempFileName ();
                using (var response = await _httpClient.GetAsync (downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                using (var fs = new FileStream (tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    await response.Content.CopyToAsync (fs);
                }

                string extractPath = Path.Combine (Path.GetTempPath (), "BinancePulseUpdate_" + Guid.NewGuid ());
                Directory.CreateDirectory (extractPath);
                ZipFile.ExtractToDirectory (tempZip, extractPath);
                _logger?.Invoke ("📦 Файлы распакованы.");

                string currentExe = Environment.ProcessPath ?? Assembly.GetExecutingAssembly ().Location;
                string appDir = Path.GetDirectoryName (currentExe) ?? AppContext.BaseDirectory;
                string backupDir = Path.Combine (appDir, "Backup_" + DateTime.Now.ToString ("yyyyMMdd_HHmmss"));
                string scriptPath = CreateUpdateScript (extractPath, appDir, backupDir, currentExe);

                _logger?.Invoke ("🔄 Запуск обновления... Приложение будет закрыто.");
                Process.Start (new ProcessStartInfo
                {
                    FileName = scriptPath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });
                await Task.Delay (2000);
                Environment.Exit (0);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Invoke ($"❌ Ошибка установки: {ex.Message}");
                return false;
            }
        }

        private string CreateUpdateScript(string sourceDir, string targetDir, string backupDir, string currentExe)
        {
            string batPath = Path.Combine (Path.GetTempPath (), "UpdateBinancePulse_" + Guid.NewGuid () + ".bat");
            string batContent = $@"
@echo off
timeout /t 2 /nobreak > nul
echo Создание резервной копии в {backupDir}
xcopy ""{targetDir}"" ""{backupDir}"" /E /I /Y /Q > nul
taskkill /f /im ""{Path.GetFileName (currentExe)}"" > nul 2>&1
echo Обновление файлов...
xcopy ""{sourceDir}\*"" ""{targetDir}"" /E /I /Y /Q > nul
echo Запуск обновлённого приложения...
start "" "" ""{currentExe}""
rmdir /S /Q ""{sourceDir}""
del ""{batPath}""
";
            File.WriteAllText (batPath, batContent);
            return batPath;
        }
    }
}