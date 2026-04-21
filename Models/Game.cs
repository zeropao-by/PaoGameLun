using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GameLauncher.Models;

public class GameInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("exePath")]
    public string ExePath { get; set; } = "";

    [JsonPropertyName("iconPath")]
    public string IconPath { get; set; } = "";

    [JsonPropertyName("accentColor")]
    public string AccentColor { get; set; } = "#5B7FFF";

    [JsonPropertyName("lastPlayed")]
    public string? LastPlayed { get; set; }

    [JsonPropertyName("totalPlayTime")]
    public long TotalPlayTimeSeconds { get; set; } = 0;  // 累计游玩时长（秒）

    [JsonPropertyName("isFavorite")]
    public bool IsFavorite { get; set; } = false;  // 是否收藏

    /// <summary>累计游玩时长（内存中使用）</summary>
    [JsonIgnore]
    public TimeSpan TotalPlayTime
    {
        get => TimeSpan.FromSeconds(TotalPlayTimeSeconds);
        set => TotalPlayTimeSeconds = (long)value.TotalSeconds;
    }

    /// <summary>当前会话开始时间（内存中，不持久化）</summary>
    [JsonIgnore]
    public DateTime? PlayStartTime { get; set; }

    [JsonPropertyName("backgroundVideo")]
    public string BackgroundVideo { get; set; } = "";

    [JsonPropertyName("backgroundImage")]
    public string BackgroundImage { get; set; } = "";

    /// <summary>游戏实际进程名（用于检测和终止进程，不带扩展名，多个用逗号分隔）</summary>
    [JsonPropertyName("processName")]
    public string ProcessName { get; set; } = "";
}

public class AppSettings
{
    [JsonPropertyName("backgroundVideo")]
    public string BackgroundVideo { get; set; } = "";

    [JsonPropertyName("volume")]
    public double Volume { get; set; } = 0;

    [JsonPropertyName("windowX")]
    public int WindowX { get; set; } = 100;

    [JsonPropertyName("windowY")]
    public int WindowY { get; set; } = 100;

    [JsonPropertyName("windowW")]
    public int WindowW { get; set; } = 1200;

    [JsonPropertyName("windowH")]
    public int WindowH { get; set; } = 800;

    [JsonPropertyName("accentColor")]
    public string AccentColor { get; set; } = "#007AFF";

    [JsonPropertyName("enableAcrylicGlass")]
    public bool EnableAcrylicGlass { get; set; } = false;

    [JsonPropertyName("acrylicOpacity")]
    public double AcrylicOpacity { get; set; } = 50;  // 模糊度 0-100

    [JsonPropertyName("autoStart")]
    public bool AutoStart { get; set; } = false;  // 开机自启动

    [JsonPropertyName("minimizeToTray")]
    public bool MinimizeToTray { get; set; } = false;  // 最小化到托盘
}

public class GameDataStore
{
    [JsonPropertyName("games")]
    public List<GameInfo> Games { get; set; } = [];
}
