using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media.Imaging;
using GameLauncher.Models;

namespace GameLauncher.Services;

public class GameManager : INotifyPropertyChanged
{
    private readonly string _dataDir;
    private readonly string _gamesFile;
    private readonly string _settingsFile;
    private readonly string _iconsDir;

    public ObservableCollection<GameInfo> Games { get; } = [];
    public AppSettings Settings { get; private set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public GameManager()
    {
        _dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GameLauncher"
        );
        Directory.CreateDirectory(_dataDir);
        _gamesFile = Path.Combine(_dataDir, "games.json");
        _settingsFile = Path.Combine(_dataDir, "settings.json");
        _iconsDir = Path.Combine(_dataDir, "icons");
        Directory.CreateDirectory(_iconsDir);

        Load();
    }

    private void Load()
    {
        if (File.Exists(_gamesFile))
        {
            try
            {
                var json = File.ReadAllText(_gamesFile);
                var data = JsonSerializer.Deserialize<GameDataStore>(json);
                if (data?.Games != null)
                    foreach (var g in data.Games)
                        Games.Add(g);
            }
            catch { }
        }

        if (File.Exists(_settingsFile))
        {
            try
            {
                var json = File.ReadAllText(_settingsFile);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch { }
        }
    }

    public void SaveGames()
    {
        var data = new GameDataStore { Games = [.. Games] };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_gamesFile, json);
    }

    public void SaveSettings()
    {
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsFile, json);
    }

    private readonly object _addLock = new();
    
    public GameInfo AddGame(string exePath, string? name = null, string? backgroundVideo = null, string? backgroundImage = null)
    {
        lock (_addLock)
        {
            // 检查是否已存在（对 lnk，同时检查解析后的目标路径）
            foreach (var g in Games)
            {
                if (string.Equals(Path.GetFullPath(g.ExePath), Path.GetFullPath(exePath), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("游戏已存在");
            }

            // 解析 lnk 快捷方式获取真实 exe 路径
            string targetPath = ShortcutResolver.GetTargetPath(exePath);

            string? iconPath = null;

            // 提取并保存图标（优先从真实 exe 提取）
            try
            {
                var icon = IconExtractor.ExtractIcon(targetPath, 96);
                if (icon == null && targetPath != exePath)
                    icon = IconExtractor.ExtractIcon(exePath, 96); // fallback 到 lnk 本身
                if (icon != null)
                {
                    var id = Guid.NewGuid().ToString("N")[..8];
                    iconPath = Path.Combine(_iconsDir, $"{id}.png");
                    SaveBitmapToPng(icon, iconPath);
                }
            }
            catch { }

            // 优先从真实 exe 推断名称
            var gameName = name ?? IconExtractor.GuessName(targetPath);
            var game = new GameInfo
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Name = gameName,
                ExePath = exePath,
                IconPath = iconPath ?? "",
                AccentColor = IconExtractor.GuessColor(gameName),
                BackgroundVideo = backgroundVideo ?? "",
                BackgroundImage = backgroundImage ?? "",
                ProcessName = ""
            };

            Games.Add(game);
            SaveGames();
            return game;
        }
    }

    public void RemoveGame(string id)
    {
        var game = Games.FirstOrDefault(g => g.Id == id);
        if (game != null)
        {
            Games.Remove(game);
            if (!string.IsNullOrEmpty(game.IconPath) && File.Exists(game.IconPath))
            {
                try { File.Delete(game.IconPath); } catch { }
            }
            SaveGames();
        }
    }

    public void UpdateGame(string id, string? exePath = null, string? name = null, string? backgroundVideo = null, string? backgroundImage = null)
    {
        var game = Games.FirstOrDefault(g => g.Id == id);
        if (game == null) return;

        // 只有当 exePath 有效时才更新 exePath 相关字段
        if (exePath != null && File.Exists(exePath))
        {
            game.ExePath = exePath;
            // 如果 name 为空，自动从 exePath 推断（对 lnk 解析真实路径）
            if (string.IsNullOrEmpty(name))
                name = IconExtractor.GuessName(ShortcutResolver.GetTargetPath(exePath));
            game.AccentColor = IconExtractor.GuessColor(name ?? game.Name);
        }

        // 单独更新 name（无论 exePath 是否变化）
        if (name != null)
            game.Name = name;

        // 更新背景视频
        if (backgroundVideo != null)
            game.BackgroundVideo = backgroundVideo;

        // 更新背景图片
        if (backgroundImage != null)
            game.BackgroundImage = backgroundImage;

        SaveGames();
    }

    public void LaunchGame(string id)
    {
        var game = Games.FirstOrDefault(g => g.Id == id);
        if (game == null) return;

        if (!File.Exists(game.ExePath))
            throw new FileNotFoundException($"游戏文件不存在: {game.ExePath}");

        game.LastPlayed = DateTime.Now.ToString("yyyy-MM-dd");
        SaveGames();

        var pathToLaunch = game.ExePath;

        // 对 lnk 文件，使用 ShellExecute 直接打开（Windows 会自动解析快捷方式）
        // 对 exe 文件，使用 Process.Start 直接启动
        if (ShortcutResolver.IsShortcut(pathToLaunch))
        {
            // lnk: 用 shell execute 打开，让 Windows 解析快捷方式
            var psi = new ProcessStartInfo
            {
                FileName = pathToLaunch,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(pathToLaunch) ?? ""
            };
            Process.Start(psi);
        }
        else
        {
            // exe: 直接启动
            var psi = new ProcessStartInfo
            {
                FileName = pathToLaunch,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(pathToLaunch) ?? ""
            };
            Process.Start(psi);
        }
    }

    public void SetBackgroundVideo(string path)
    {
        if (!string.IsNullOrEmpty(path) && !File.Exists(path))
            throw new FileNotFoundException($"视频文件不存在: {path}");
        Settings.BackgroundVideo = path ?? "";
        SaveSettings();
    }

    public void SetVolume(double volume)
    {
        Settings.Volume = Math.Clamp(volume, 0, 1);
        SaveSettings();
    }

    public void SaveWindowGeometry(int x, int y, int w, int h)
    {
        Settings.WindowX = x;
        Settings.WindowY = y;
        Settings.WindowW = w;
        Settings.WindowH = h;
        SaveSettings();
    }

    public BitmapSource? LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath) || !File.Exists(iconPath)) return null;
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.UriSource = new Uri(iconPath, UriKind.Absolute);
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch { return null; }
    }

    private static void SaveBitmapToPng(BitmapSource source, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
