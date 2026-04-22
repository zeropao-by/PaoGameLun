using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GameLauncher.Models;
using GameLauncher.Services;
using Microsoft.Win32;

namespace GameLauncher.Views;

public partial class MainWindow : Window
{
    private readonly GameManager _gameManager;
    private readonly DispatcherTimer _videoTimer;
    private readonly DispatcherTimer _processCheckTimer; // 检测游戏进程
    private GameInfo? _selectedGame;
    private bool _isGameRunning = false; // 游戏是否正在运行
    private bool _isMuted = false;
    private bool _isPaused = false;
    private double _volume = 1.0; // 音量 0.0-1.0
    private bool _isAddGameDialogOpen = false; // 防止重复打开对话框

    // 系统托盘
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private bool _minimizeToTray = false;

    // 搜索和筛选
    private string _searchText = "";
    private bool _showFavoritesOnly = false;
    private List<GameInfo>? _filteredGames; // 筛选后的游戏列表（排序后）

    // 主题颜色预设
    private static readonly List<(string Name, string Hex)> ThemeColors = new()
    {
        ("蓝色 Blue",   "#007AFF"),
        ("紫色 Purple", "#AF52DE"),
        ("粉色 Pink",   "#FF2D55"),
        ("橙色 Orange", "#FF9500"),
        ("绿色 Green",  "#34C759"),
        ("青色 Teal",   "#5AC8FA"),
        ("红色 Red",    "#FF3B30"),
        ("金色 Gold",   "#FFD60A"),
    };

    public MainWindow()
    {
        InitializeComponent();
        _gameManager = new GameManager();

        // 窗口位置
        var s = _gameManager.Settings;
        Left = s.WindowX;
        Top = s.WindowY;
        Width = s.WindowW;
        Height = s.WindowH;
        _minimizeToTray = s.MinimizeToTray;

        // 视频循环定时器
        _videoTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _videoTimer.Tick += VideoTimer_Tick;

        // 游戏进程检测定时器（每秒检测一次）
        _processCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _processCheckTimer.Tick += ProcessCheckTimer_Tick;
        _processCheckTimer.Start();

        // 初始化托盘
        InitNotifyIcon();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        SizeChanged += MainWindow_SizeChanged;

        _gameManager.Games.CollectionChanged += (_, _) => RefreshGameList();
        RefreshGameList();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 加载窗口图标（使用 Assets 中的 PNG）
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string[] pngPaths = {
                Path.Combine(baseDir, "Assets", "app.png"),
                Path.Combine(baseDir, "app.png"),
            };

            System.Drawing.Image? srcImg = null;
            foreach (var path in pngPaths)
            {
                if (File.Exists(path))
                {
                    srcImg = System.Drawing.Image.FromFile(path);
                    break;
                }
            }

            if (srcImg != null)
            {
                using var bmp512 = new System.Drawing.Bitmap(512, 512);
                using var g = System.Drawing.Graphics.FromImage(bmp512);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(srcImg, 0, 0, 512, 512);
                srcImg.Dispose();

                IntPtr hIcon = bmp512.GetHicon();
                this.Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                DestroyIcon(hIcon);
            }
        }
        catch { }

        // 恢复保存的主题颜色
        if (!string.IsNullOrEmpty(_gameManager.Settings.AccentColor))
            SetAccentColor(_gameManager.Settings.AccentColor);

        // 恢复保存的液态玻璃设置
        if (_gameManager.Settings.EnableAcrylicGlass)
        {
            BlurSlider.Value = _gameManager.Settings.AcrylicOpacity;
            EnableAcrylicGlass();
        }

        // 如果有游戏，默认选中第一个
        if (_gameManager.Games.Count > 0)
            SelectGame(_gameManager.Games[0]);
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    // ==================== 公开方法 ====================

    /// <summary>启用窗口液态玻璃效果</summary>
    public void EnableAcrylicGlass()
    {
        try
        {
            double opacity = _gameManager.Settings.AcrylicOpacity;
            byte alpha = (byte)(20 + opacity * 2.35); // 滑条 0→最低可见度(~8%), 100→最强(100%)

            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
            var accent = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                GradientColor = (alpha << 24) | 0x000000 // ARGB: alpha + 黑色背景
            };

            var accentSize = Marshal.SizeOf(accent);
            var accentPtr = Marshal.AllocHGlobal(accentSize);
            Marshal.StructureToPtr(accent, accentPtr, true);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentSize,
                Data = accentPtr
            };

            SetWindowCompositionAttribute(hwnd, ref data);
            Marshal.FreeHGlobal(accentPtr);

            _gameManager.Settings.EnableAcrylicGlass = true;
            _gameManager.SaveSettings();

            // 显示模糊度滑条
            BlurControl.Visibility = Visibility.Visible;
            BlurSlider.Value = opacity;
        }
        catch { }
    }

    /// <summary>禁用窗口液态玻璃效果</summary>
    public void DisableAcrylicGlass()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
            var accent = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_DISABLED
            };

            var accentSize = Marshal.SizeOf(accent);
            var accentPtr = Marshal.AllocHGlobal(accentSize);
            Marshal.StructureToPtr(accent, accentPtr, true);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentSize,
                Data = accentPtr
            };

            SetWindowCompositionAttribute(hwnd, ref data);
            Marshal.FreeHGlobal(accentPtr);

            _gameManager.Settings.EnableAcrylicGlass = false;
            _gameManager.SaveSettings();

            // 隐藏模糊度滑条
            BlurControl.Visibility = Visibility.Collapsed;
        }
        catch { }
    }

    /// <summary>设置主题强调色</summary>
    public void SetAccentColor(string hexColor)
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
            var brush = new SolidColorBrush(color);
            brush.Freeze();

            Resources["AccentColor"] = brush;
            _gameManager.Settings.AccentColor = hexColor;
            _gameManager.SaveSettings();
        }
        catch { }
    }

    /// <summary>
    /// 刷新游戏列表（带搜索和收藏筛选）
    /// </summary>
    public void RefreshGameList()
    {
        // 构建筛选后的列表
        var filtered = _gameManager.Games
            .Where(g =>
            {
                // 搜索筛选
                if (!string.IsNullOrWhiteSpace(_searchText))
                {
                    if (!g.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                // 收藏筛选
                if (_showFavoritesOnly && !g.IsFavorite)
                    return false;
                return true;
            })
            .OrderByDescending(g => g.IsFavorite)  // 收藏优先
            .ThenBy(g => g.Name)  // 按名称排序
            .ToList();

        _filteredGames = filtered;
        GameIconList.ItemsSource = null;
        GameIconList.ItemsSource = filtered;

        bool hasGames = filtered.Count > 0;
        EmptyState.Visibility = hasGames ? Visibility.Collapsed : Visibility.Visible;

        if (hasGames && _selectedGame != null)
        {
            var still = filtered.FirstOrDefault(g => g.Id == _selectedGame.Id);
            if (still == null && filtered.Count > 0)
                SelectGame(filtered[0]);
        }
        else if (hasGames && _selectedGame == null)
        {
            SelectGame(filtered[0]);
        }
        else if (!hasGames)
        {
            ClearGameSelection();
        }

        // 延迟加载图标（等 UI 渲染完成）
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            LoadAllGameIcons();
        });
    }
    
    /// <summary>加载所有游戏图标</summary>
    private void LoadAllGameIcons()
    {
        foreach (var item in GameIconList.Items)
        {
            var container = GameIconList.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
            if (container == null) continue;

            var btn = FindVisualChild<Button>(container);
            if (btn == null) continue;

            // 确保模板已应用
            btn.ApplyTemplate();

            // Tag 是 GameInfo 对象
            if (btn.Tag is not GameInfo game) continue;

            // 找到 Grid 和 Image
            var grid = FindVisualChild<Grid>(btn);
            if (grid == null) continue;

            System.Windows.Controls.Image? image = null;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(grid); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(grid, i);
                if (child is System.Windows.Controls.Image img)
                {
                    image = img;
                    break;
                }
            }

            if (image == null) continue;

            // 加载图标
            if (!string.IsNullOrEmpty(game.IconPath) && File.Exists(game.IconPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(game.IconPath, UriKind.Absolute);
                    bitmap.DecodePixelWidth = 64;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    image.Source = bitmap;
                }
                catch { }
            }
        }
    }
    
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            var grandChild = FindVisualChild<T>(child);
            if (grandChild != null) return grandChild;
        }
        return null;
    }
    
    private static T? FindChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        if (parent is FrameworkElement fe && fe.Name == name && parent is T result)
            return result;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            var found = FindChild<T>(child, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>刷新选中的游戏信息</summary>
    public void RefreshGameSelection(GameInfo game)
    {
        _selectedGame = game;
        GameNameText.Text = game.Name;
        TitleText.Text = game.Name;
    }

    /// <summary>切换游戏收藏状态</summary>
    public void ToggleFavorite(GameInfo game)
    {
        game.IsFavorite = !game.IsFavorite;
        _gameManager.SaveGames();
        RefreshGameList();
        LoadAllGameIcons();
        UpdateFavoriteButton();
    }

    /// <summary>更新收藏按钮状态</summary>
    private void UpdateFavoriteButton()
    {
        if (FavBtn == null || _selectedGame == null) return;
        try
        {
            var star = (System.Windows.Controls.TextBlock)FavBtn.Template.FindName("star", FavBtn);
            if (star != null)
                star.Text = _selectedGame.IsFavorite ? "★" : "☆";
        }
        catch { }
    }

    /// <summary>收藏按钮点击</summary>
    private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGame == null) return;
        ToggleFavorite(_selectedGame);
    }

    /// <summary>设置托盘最小化</summary>
    public void SetMinimizeToTray(bool enabled)
    {
        _minimizeToTray = enabled;
        _gameManager.Settings.MinimizeToTray = enabled;
        _gameManager.SaveSettings();
    }

    /// <summary>加载背景（视频或图片），支持本地文件和网络URL</summary>
    public void LoadBackground(string? videoPath, string? imagePath)
    {
        StopVideo();
        StopImage();

        // 去除 url: 前缀获取实际路径
        string? GetActualPath(string? path) => path?.StartsWith("url:") == true ? path.Substring(4) : path;
        
        // 判断去掉前缀后是否为网络URL
        bool IsUrl(string? path)
        {
            var actual = GetActualPath(path);
            return !string.IsNullOrEmpty(actual) && (actual.StartsWith("http://") || actual.StartsWith("https://"));
        }

        // 优先加载视频
        var actualVideoPath = GetActualPath(videoPath);
        if (!string.IsNullOrEmpty(actualVideoPath))
        {
            try
            {
                if (IsUrl(videoPath))
                {
                    // 网络视频
                    VideoBg.Source = new Uri(actualVideoPath, UriKind.Absolute);
                }
                else if (File.Exists(actualVideoPath))
                {
                    // 本地视频
                    VideoBg.Source = new Uri(actualVideoPath, UriKind.Absolute);
                }
                else
                {
                    throw new FileNotFoundException();
                }
                
                VideoBg.ScrubbingEnabled = true;
                VideoBg.Volume = _volume;  // 使用滑块设置的音量
                VideoBg.Play();
                VideoBg.Visibility = Visibility.Visible;
                VideoOverlay.Visibility = Visibility.Visible;
                _videoTimer.Start();
                return;
            }
            catch { }
        }

        // 其次加载图片
        var actualImagePath = GetActualPath(imagePath);
        if (!string.IsNullOrEmpty(actualImagePath))
        {
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                
                if (IsUrl(imagePath))
                {
                    // 网络图片
                    img.UriSource = new Uri(actualImagePath, UriKind.Absolute);
                }
                else if (File.Exists(actualImagePath))
                {
                    // 本地图片
                    img.UriSource = new Uri(actualImagePath, UriKind.Absolute);
                }
                else
                {
                    throw new FileNotFoundException();
                }
                
                img.EndInit();
                img.Freeze();

                ImageBg.Source = img;
                ImageBg.Visibility = Visibility.Visible;
                VideoOverlay.Visibility = Visibility.Visible;
                return;
            }
            catch { }
        }

        // 无背景
        VideoBg.Visibility = Visibility.Collapsed;
        VideoOverlay.Visibility = Visibility.Collapsed;
    }

    // ==================== Windows API ====================

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    private const int WM_NCHITTEST = 0x0084;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;
    private const int HTCAPTION = 2;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var source = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_NCHITTEST)
        {
            var result = GetHitTestResult(lParam);
            if (result != 0)
            {
                handled = true;
                return (IntPtr)result;
            }
        }
        return IntPtr.Zero;
    }

    private int GetHitTestResult(IntPtr lParam)
    {
        int x = (short)(lParam.ToInt32() & 0xFFFF);
        int y = (short)(lParam.ToInt32() >> 16);
        var pt = PointFromScreen(new System.Windows.Point(x, y));

        const int border = 6;
        double w = ActualWidth;
        double h = ActualHeight;

        // 只在边缘区域响应resize
        if (pt.X >= 0 && pt.X <= w && pt.Y >= 0 && pt.Y <= h)
        {
            if (pt.Y < border && pt.X < border) return HTTOPLEFT;
            if (pt.Y < border && pt.X > w - border) return HTTOPRIGHT;
            if (pt.Y > h - border && pt.X < border) return HTBOTTOMLEFT;
            if (pt.Y > h - border && pt.X > w - border) return HTBOTTOMRIGHT;
            if (pt.Y < border) return HTTOP;
            if (pt.Y > h - border) return HTBOTTOM;
            if (pt.X < border) return HTLEFT;
            if (pt.X > w - border) return HTRIGHT;
        }
        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    private enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19
    }

    private enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
    }

    // ==================== 系统托盘 ====================

    private void InitNotifyIcon()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "PaoGameLun",
            Visible = false
        };

        // 创建图标
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string iconPath = Path.Combine(baseDir, "Assets", "app.png");
            if (File.Exists(iconPath))
            {
                using var bmp = new System.Drawing.Bitmap(iconPath);
                _notifyIcon.Icon = System.Drawing.Icon.FromHandle(bmp.GetHicon());
                GC.KeepAlive(bmp);
            }
            else
            {
                // 使用默认图标
                _notifyIcon.Icon = SystemIcons.Application;
            }
        }
        catch
        {
            _notifyIcon.Icon = SystemIcons.Application;
        }

        // 右键菜单
        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("打开", null, (_, _) => ShowFromTray());
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("退出", null, (_, _) =>
        {
            _notifyIcon!.Visible = false;
            _notifyIcon.Dispose();
            Application.Current.Shutdown();
        });
        _notifyIcon.ContextMenuStrip = contextMenu;

        // 双击打开
        _notifyIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        _notifyIcon!.Visible = false;
    }

    private void MinimizeToTray()
    {
        Hide();
        _notifyIcon!.Visible = true;
        _notifyIcon.ShowBalloonTip(1000, "PaoGameLun", "已最小化到托盘", System.Windows.Forms.ToolTipIcon.Info);
    }

    // ==================== 事件处理 ====================

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 保存当前正在计时的游戏时长
        SaveCurrentPlayTime();

        _gameManager.SaveWindowGeometry((int)Left, (int)Top, (int)Width, (int)Height);
        _videoTimer.Stop();
        VideoBg.Stop();
        VideoBg.Source = null;

        // 清理托盘图标
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        VideoBg.Width = ActualWidth;
        VideoBg.Height = ActualHeight;
    }

    private void SelectGame(GameInfo game)
    {
        // 保存上一个游戏的时长
        SaveCurrentPlayTime();

        _selectedGame = game;

        // 显示游戏名称
        GameNameText.Text = game.Name;
        TitleText.Text = game.Name;

        // 显示游戏图标
        try
        {
            if (!string.IsNullOrEmpty(game.IconPath) && System.IO.File.Exists(game.IconPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(game.IconPath, UriKind.Absolute);
                bitmap.DecodePixelWidth = 48;
                bitmap.EndInit();
                bitmap.Freeze();
                LaunchGameIcon.Source = bitmap;
                GameNameIcon.Source = bitmap;
                GameNameIcon.Visibility = Visibility.Visible;
            }
            else
            {
                LaunchGameIcon.Source = null;
                GameNameIcon.Source = null;
                GameNameIcon.Visibility = Visibility.Collapsed;
            }
        }
        catch { }

        // 显示上次启动和累计时长
        var playTimeInfo = game.TotalPlayTime.TotalHours >= 1
            ? $"累计 {game.TotalPlayTime.TotalHours:F1} 小时"
            : game.TotalPlayTime.TotalMinutes >= 1
                ? $"累计 {(int)game.TotalPlayTime.TotalMinutes} 分钟"
                : "";

        // 格式化上次启动时间：旧数据只有日期，新数据包含时间
        string lastPlayedDisplay;
        if (string.IsNullOrEmpty(game.LastPlayed))
        {
            lastPlayedDisplay = string.IsNullOrEmpty(playTimeInfo) ? "从未启动" : playTimeInfo;
        }
        else
        {
            // 如果包含空格说明有具体时间，否则只有日期
            var timeStr = game.LastPlayed.Contains(' ') ? game.LastPlayed : $"{game.LastPlayed} 00:00";
            lastPlayedDisplay = $"上次启动：{timeStr}" + (string.IsNullOrEmpty(playTimeInfo) ? "" : $" · {playTimeInfo}");
        }

        GameLastPlayedText.Text = lastPlayedDisplay;

        // 显示右下角毛玻璃按钮
        LaunchButtonPanel.Visibility = Visibility.Visible;
        GameNameBadge.Visibility = Visibility.Visible;
        SetVideoBtn.Visibility = Visibility.Visible;

        // 有背景（图片或视频）时显示音量和暂停控件
        bool hasBackground = !string.IsNullOrEmpty(game.BackgroundVideo) || !string.IsNullOrEmpty(game.BackgroundImage);
        VolumeControl.Visibility = hasBackground ? Visibility.Visible : Visibility.Collapsed;

        // 模糊度滑条位置：有背景时在音量面板上方，无背景时贴近启动按钮面板
        if (BlurControl != null)
            BlurControl.Margin = new Thickness(0, 0, 20, hasBackground ? 170 : 90);

        // 在音量面板显示游戏名称
        if (hasBackground && VolumePanelGameName != null)
        {
            VolumePanelGameName.Text = game.Name;
        }

        // 更新收藏按钮状态
        UpdateFavoriteButton();

        // 初始化时检测游戏是否在运行
        _isGameRunning = IsGameRunning(game.ExePath);
        UpdateLaunchButton();

        LoadBackground(game.BackgroundVideo, game.BackgroundImage);
    }

    // ==================== 进程检测与终止（基于 PID 精确匹配）====================

    /// <summary>获取游戏候选进程名列表，用于匹配新启动的进程</summary>
    private List<string> GetGameProcessNames()
    {
        var names = new List<string>();
        
        // 优先使用配置的进程名（支持逗号分隔）
        if (!string.IsNullOrEmpty(_selectedGame?.ProcessName))
        {
            foreach (var name in _selectedGame.ProcessName.Split(',', '，'))
            {
                var trimmed = name.Trim();
                // 跳过无效进程名（纯数字、太短、明显错误的值）
                if (!string.IsNullOrEmpty(trimmed) && 
                    !names.Contains(trimmed) &&
                    trimmed.Length > 1 &&
                    !System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+$"))
                    names.Add(trimmed);
            }
        }
        
        // 从真实 ExePath 推断进程名（对 lnk 先解析为实际 exe）
        if (!string.IsNullOrEmpty(_selectedGame?.ExePath))
        {
            var realExePath = ShortcutResolver.GetTargetPath(_selectedGame.ExePath);
            var exeName = Path.GetFileNameWithoutExtension(realExePath);
            if (!string.IsNullOrEmpty(exeName) && !names.Contains(exeName))
                names.Add(exeName);

            // UE 引擎自动识别
            var path = realExePath.Replace('/', '\\');
            var exeDir = Path.GetDirectoryName(path);
            
            // 方式1：直接匹配 exe 路径中的 UE 结构
            var ueMatch = System.Text.RegularExpressions.Regex.Match(
                path, @"\\([^\\]+)\\Binaries\\Win64\\", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (ueMatch.Success)
            {
                AddUEShippingNames(names, ueMatch.Groups[1].Value);
            }
            else if (!string.IsNullOrEmpty(exeDir))
            {
                // 方式2：扫描同级子目录查找 UE 结构
                try
                {
                    foreach (var subDir in System.IO.Directory.EnumerateDirectories(exeDir))
                    {
                        var binariesDir = Path.Combine(subDir, "Binaries", "Win64");
                        if (System.IO.Directory.Exists(binariesDir))
                        {
                            AddUEShippingNames(names, Path.GetFileName(subDir));
                            break;
                        }
                        try
                        {
                            foreach (var subSubDir in System.IO.Directory.EnumerateDirectories(subDir))
                            {
                                var bDir = Path.Combine(subSubDir, "Binaries", "Win64");
                                if (System.IO.Directory.Exists(bDir))
                                {
                                    AddUEShippingNames(names, Path.GetFileName(subSubDir));
                                    break;
                                }
                            }
                        }
                        catch { }
                        
                        if (names.Any(n => n.Contains("Shipping"))) break;
                    }
                }
                catch { }
            }
        }
        
        return names;
    }

    private void AddUEShippingNames(List<string> names, string projectDir)
    {
        foreach (var sn in new[] { $"{projectDir}-Win64-Shipping", "Client-Win64-Shipping" })
        {
            if (!names.Contains(sn))
                names.Add(sn);
        }
    }

    /// <summary>
    /// 获取用于路径匹配的真实 exe 路径（lnk 解析为实际 exe）。
    /// 这是核心：直接用用户填的 exe 路径去精确匹配 MainModule.FileName。
    /// </summary>
    private string? GetRealExePathForMatching()
    {
        if (_selectedGame == null || string.IsNullOrEmpty(_selectedGame.ExePath)) return null;
        
        // 对 lnk 快捷方式，解析出真实目标 exe 路径
        var realPath = ShortcutResolver.GetTargetPath(_selectedGame.ExePath);
        if (!File.Exists(realPath)) return null;
        
        return Path.GetFullPath(realPath);
    }

    /// <summary>
    /// 启动游戏后，延迟捕获实际运行的进程 PID。
    /// 先记录启动前的进程快照，启动后对比新增的匹配进程。
    /// </summary>
    private void CaptureGameProcessIds()
    {
        if (_selectedGame == null) return;
        
        var game = _selectedGame;
        var candidateNames = GetGameProcessNames();
        
        // 记录启动前的已有进程 PID
        var beforePids = new HashSet<int>();
        foreach (var name in candidateNames)
        {
            try
            {
                foreach (var p in System.Diagnostics.Process.GetProcessesByName(name))
                {
                    beforePids.Add(p.Id);
                    p.Dispose();
                }
            }
            catch { }
        }
        
        // 延迟 3 秒后捕获新增进程（给游戏启动时间）
        Task.Delay(3000).ContinueWith(_ =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_selectedGame != game) return; // 用户已切换游戏
                
                var newPids = new List<int>();
                foreach (var name in candidateNames)
                {
                    try
                    {
                        foreach (var p in System.Diagnostics.Process.GetProcessesByName(name))
                        {
                            if (!beforePids.Contains(p.Id))
                            {
                                newPids.Add(p.Id);
                            }
                            p.Dispose();
                        }
                    }
                    catch { }
                }
                
                if (newPids.Count > 0)
                {
                    game.RunningProcessIds = newPids;
                    _isGameRunning = true;
                    UpdateLaunchButton();
                }
            });
        });
    }

    /// <summary>检测游戏是否在运行：PID → 路径精确匹配(lnk支持) → 进程名+目录 → 进程名模糊</summary>
    private bool IsGameRunning(string? exePath)
    {
        if (_selectedGame == null) return false;
        
        // 优先：检查已记录的 PID 是否存活
        if (_selectedGame.RunningProcessIds.Count > 0)
        {
            foreach (var pid in _selectedGame.RunningProcessIds.ToList())
            {
                try
                {
                    var proc = System.Diagnostics.Process.GetProcessById(pid);
                    if (!proc.HasExited)
                    {
                        proc.Dispose();
                        return true;
                    }
                    // 进程已退出，清理
                    proc.Dispose();
                    _selectedGame.RunningProcessIds.Remove(pid);
                }
                catch (ArgumentException)
                {
                    // 进程不存在，清理
                    _selectedGame.RunningProcessIds.Remove(pid);
                }
            }
            return false;
        }

        // 第二优先：路径精确匹配 — 直接用用户填的 exe 路径（lnk 解析为真实 exe）匹配 MainModule.FileName
        var realExePath = GetRealExePathForMatching();
        if (realExePath != null)
        {
            try
            {
                // 获取所有运行中的进程，逐个对比 MainModule.FileName 与用户填的真实 exe 路径
                var allProcesses = System.Diagnostics.Process.GetProcesses();
                foreach (var p in allProcesses)
                {
                    try
                    {
                        if (p.MainModule?.FileName != null &&
                            string.Equals(
                                Path.GetFullPath(p.MainModule.FileName),
                                realExePath,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            // 匹配成功，记录 PID 供后续精确使用
                            if (!_selectedGame.RunningProcessIds.Contains(p.Id))
                                _selectedGame.RunningProcessIds.Add(p.Id);
                            p.Dispose();
                            return true;
                        }
                    }
                    catch { } // 某些系统进程无法访问 MainModule
                    finally { try { p.Dispose(); } catch { } }
                }
            }
            catch { }
        }

        // 回退：没有记录的 PID 时，用进程名 + 路径验证匹配
        if (string.IsNullOrEmpty(exePath)) return false;
        
        var processNames = GetGameProcessNames();
        if (processNames.Count == 0) return false;
        
        var gameDir = Path.GetDirectoryName(exePath);
        
        try
        {
            foreach (var processName in processNames)
            {
                bool needsPathCheck = processName.Contains("Shipping") || processName.Contains("Win64");
                
                var processes = System.Diagnostics.Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    if (needsPathCheck && !string.IsNullOrEmpty(gameDir))
                    {
                        foreach (var p in processes)
                        {
                            try
                            {
                                if (p.MainModule?.FileName != null)
                                {
                                    var procDir = Path.GetDirectoryName(p.MainModule.FileName);
                                    if (procDir != null && 
                                        (procDir.StartsWith(gameDir, StringComparison.OrdinalIgnoreCase) ||
                                         gameDir.StartsWith(procDir, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        // 回退匹配成功，记录 PID 供后续精确使用
                                        if (!_selectedGame.RunningProcessIds.Contains(p.Id))
                                            _selectedGame.RunningProcessIds.Add(p.Id);
                                        p.Dispose();
                                        return true;
                                    }
                                }
                            }
                            catch { }
                            p.Dispose();
                        }
                    }
                    else
                    {
                        foreach (var p in processes)
                        {
                            if (!_selectedGame.RunningProcessIds.Contains(p.Id))
                                _selectedGame.RunningProcessIds.Add(p.Id);
                            p.Dispose();
                        }
                        return true;
                    }
                }
                foreach (var p in processes) p.Dispose();
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    // ===================================================================

    private void ClearGameSelection()
    {
        _selectedGame = null;
        LaunchButtonPanel.Visibility = Visibility.Collapsed;
        GameNameBadge.Visibility = Visibility.Collapsed;
        SetVideoBtn.Visibility = Visibility.Collapsed;
        VolumeControl.Visibility = Visibility.Collapsed;
        TitleText.Text = "PaoGameLun";
        StopVideo();
        StopImage();
        VideoOverlay.Visibility = Visibility.Collapsed;
    }

    private void GameIcon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is GameInfo game)
        {
            SelectGame(game);
        }
    }

    // 背景处理
    private void StopVideo()
    {
        _videoTimer.Stop();
        try { VideoBg.Stop(); } catch { }
        VideoBg.Source = null;
        VideoBg.Visibility = Visibility.Collapsed;
    }

    private void StopImage()
    {
        ImageBg.Source = null;
        ImageBg.Visibility = Visibility.Collapsed;
    }

    // 检测游戏进程是否在运行
    private void ProcessCheckTimer_Tick(object? sender, EventArgs e)
    {
        if (_selectedGame == null) return;

        // 检测进程是否在运行
        bool isRunning = IsGameRunning(_selectedGame.ExePath);

        // 如果状态变了，更新按钮
        if (isRunning != _isGameRunning)
        {
            _isGameRunning = isRunning;
            UpdateLaunchButton();
        }
    }

    // 更新启动按钮的文字和图标
    private void UpdateLaunchButton()
    {
        if (LaunchBtn == null || _selectedGame == null) return;

        try
        {
            var icon = (System.Windows.Controls.TextBlock)LaunchBtn.Template.FindName("icon", LaunchBtn);
            var text = (System.Windows.Controls.TextBlock)LaunchBtn.Template.FindName("text", LaunchBtn);
            var b = (System.Windows.Controls.Border)LaunchBtn.Template.FindName("b", LaunchBtn);

            if (_isGameRunning)
            {
                // 游戏运行时 - 显示红色终止按钮
                if (icon != null) icon.Text = "⬛";
                if (text != null) text.Text = "终止运行";
                if (b != null) b.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(200, 255, 59, 48)); // 半透明红色
            }
            else
            {
                // 游戏未运行 - 显示毛玻璃开始按钮
                if (icon != null) icon.Text = "▶";
                if (text != null) text.Text = "开始游戏";
                if (b != null) b.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(80, 255, 255, 255)); // 半透明白色
            }
        }
        catch { }
    }

    private void VideoTimer_Tick(object? sender, EventArgs e)
    {
        if (VideoBg.NaturalDuration.HasTimeSpan &&
            VideoBg.Position >= VideoBg.NaturalDuration.TimeSpan - TimeSpan.FromMilliseconds(300))
        {
            VideoBg.Position = TimeSpan.Zero;
            VideoBg.Play();
        }
    }

    private void VideoBg_MediaEnded(object sender, RoutedEventArgs e)
    {
        VideoBg.Position = TimeSpan.Zero;
        VideoBg.Play();
    }

    // 启动游戏 / 终止运行
    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGame == null) return;
        
        // 如果游戏正在运行，则终止它
        if (_isGameRunning)
        {
            TerminateGame();
            return;
        }
        
        try
        {
            // 保存上一个游戏的时长
            SaveCurrentPlayTime();

            // 启动新游戏
            _gameManager.LaunchGame(_selectedGame.Id);
            _selectedGame.PlayStartTime = DateTime.Now;
            GameLastPlayedText.Text = $"上次启动：{DateTime.Now:yyyy-MM-dd HH:mm}";

            // 记录游戏启动后的实际进程 PID（延迟捕获，给游戏启动时间）
            CaptureGameProcessIds();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"启动失败：{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // 终止游戏进程（优先级从高到低）：PID → 路径精确匹配(lnk支持) → 进程名+目录 → 进程名模糊 → 名称模糊
    private void TerminateGame()
    {
        if (_selectedGame == null) return;
        
        try
        {
            bool anyKilled = false;
            
            // 优先：用记录的 PID 精确终止
            if (_selectedGame.RunningProcessIds.Count > 0)
            {
                foreach (var pid in _selectedGame.RunningProcessIds.ToList())
                {
                    try
                    {
                        var proc = System.Diagnostics.Process.GetProcessById(pid);
                        if (!proc.HasExited)
                        {
                            proc.Kill();
                            proc.WaitForExit(3000);
                            anyKilled = true;
                        }
                        proc.Dispose();
                    }
                    catch { }
                }
                _selectedGame.RunningProcessIds.Clear();
            }

            // 第二优先：路径精确匹配终止 — 用真实 exe 路径匹配 MainModule.FileName 来 Kill
            if (!anyKilled)
            {
                var realExePath = GetRealExePathForMatching();
                if (realExePath != null)
                {
                    try
                    {
                        var allProcesses = System.Diagnostics.Process.GetProcesses();
                        foreach (var p in allProcesses)
                        {
                            try
                            {
                                if (p.MainModule?.FileName != null &&
                                    string.Equals(
                                        Path.GetFullPath(p.MainModule.FileName),
                                        realExePath,
                                        StringComparison.OrdinalIgnoreCase))
                                {
                                    p.Kill();
                                    p.WaitForExit(3000);
                                    anyKilled = true;
                                    break; // 找到就停，一个 exe 只有一个主进程
                                }
                            }
                            catch { } // 某些系统进程无法访问 MainModule
                            finally { try { p.Dispose(); } catch { } }
                        }
                    }
                    catch { }
                }
            }

            // 第三优先：进程名 + 目录验证匹配
            if (!anyKilled)
            {
                var processNames = GetGameProcessNames();
                var gameDir = Path.GetDirectoryName(_selectedGame.ExePath) ?? "";
                
                foreach (var processName in processNames)
                {
                    bool needsPathCheck = processName.Contains("Shipping") || processName.Contains("Win64");
                    
                    var processes = System.Diagnostics.Process.GetProcessesByName(processName);
                    foreach (var p in processes)
                    {
                        try
                        {
                            if (needsPathCheck && !string.IsNullOrEmpty(gameDir))
                            {
                                try
                                {
                                    if (p.MainModule?.FileName != null)
                                    {
                                        var procDir = Path.GetDirectoryName(p.MainModule.FileName);
                                        if (procDir == null ||
                                            (!procDir.StartsWith(gameDir, StringComparison.OrdinalIgnoreCase) &&
                                             !gameDir.StartsWith(procDir, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            p.Dispose();
                                            continue;
                                        }
                                    }
                                }
                                catch { }
                            }
                            
                            p.Kill();
                            p.WaitForExit(3000);
                            anyKilled = true;
                        }
                        catch { }
                        finally
                        {
                            p.Dispose();
                        }
                    }
                }
            }
            
            // 如果精确匹配没找到，尝试模糊匹配（兼容旧逻辑）
            if (!anyKilled)
            {
                var processNames = GetGameProcessNames();
                foreach (var processName in processNames)
                {
                    var processes = System.Diagnostics.Process.GetProcessesByName(processName);
                    if (processes.Length > 0)
                    {
                        anyKilled = true;
                        foreach (var p in processes) p.Dispose();
                    }
                }
                
                if (!anyKilled)
                {
                    var allProcesses = System.Diagnostics.Process.GetProcesses();
                    foreach (var p in allProcesses)
                    {
                        try
                        {
                            if (p.Id == 0 || p.Id == 4) continue;
                            
                            string pName = p.ProcessName.ToLower();
                            string gameName = _selectedGame.Name.ToLower();
                            
                            if ((pName.Contains(gameName) || gameName.Contains(pName)) && pName.Length > 2)
                            {
                                if (!pName.Contains("explorer") && !pName.Contains("system") && !pName.Contains("service"))
                                {
                                    p.Kill();
                                    p.WaitForExit(3000);
                                    anyKilled = true;
                                }
                            }
                        }
                        catch { }
                        finally
                        {
                            p.Dispose();
                        }
                    }
                }
            }
            
            // 手动更新状态
            _isGameRunning = false;
            UpdateLaunchButton();
            
            // 保存游玩时长
            SaveCurrentPlayTime();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"终止失败：{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>终止游戏按钮点击</summary>
    private void KillProcess_Click(object sender, RoutedEventArgs e)
    {
        TerminateGame();
    }

    /// <summary>删除游戏按钮点击</summary>
    private void DeleteGame_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGame == null) return;

        var result = MessageBox.Show(this,
            $"确定删除「{_selectedGame.Name}」？",
            "确认删除",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _gameManager.RemoveGame(_selectedGame.Id);
            RefreshGameList();
        }
    }

    /// <summary>保存当前游戏的游玩时长</summary>
    private void SaveCurrentPlayTime()
    {
        if (_selectedGame?.PlayStartTime != null)
        {
            var elapsed = DateTime.Now - _selectedGame.PlayStartTime.Value;
            if (elapsed.TotalSeconds > 5)  // 至少玩5秒才计入
            {
                _selectedGame.TotalPlayTime += elapsed;
                _gameManager.SaveGames();
            }
            _selectedGame.PlayStartTime = null;
        }
    }

    // 音量滑块
    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VideoBg == null) return;  // InitializeComponent() 时 Value=100 会触发此事件
        
        _volume = e.NewValue / 100.0;
        
        _volume = e.NewValue / 100.0;
        VideoBg.Volume = _volume;
        _isMuted = _volume == 0;
        
        // 更新音量图标
        UpdateVolumeIcon();
    }
    
    private void UpdateVolumeIcon()
    {
        if (VolumeIcon == null) return;
        
        if (_isMuted || _volume == 0)
            VolumeIcon.Text = "🔇";
        else if (_volume < 0.3)
            VolumeIcon.Text = "🔈";
        else if (_volume < 0.7)
            VolumeIcon.Text = "🔉";
        else
            VolumeIcon.Text = "🔊";
    }

    // 模糊度滑块
    private void BlurSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_gameManager == null) return;  // InitializeComponent() 时 Value=50 会触发此事件，此时 _gameManager 尚未赋值

        double value = e.NewValue;
        
        // 更新百分比显示
        if (BlurValueText != null)
            BlurValueText.Text = $"{(int)value}%";
        
        // 保存设置
        _gameManager.Settings.AcrylicOpacity = value;
        _gameManager.SaveSettings();
        
        // 如果液态玻璃已开启，实时更新模糊度
        if (_gameManager.Settings.EnableAcrylicGlass)
        {
            try
            {
                byte alpha = (byte)(20 + value * 2.35); // 0→最低可见度, 100→最强
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
                var accent = new AccentPolicy
                {
                    AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                    GradientColor = (alpha << 24) | 0x000000
                };
                var accentSize = Marshal.SizeOf(accent);
                var accentPtr = Marshal.AllocHGlobal(accentSize);
                Marshal.StructureToPtr(accent, accentPtr, true);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    SizeOfData = accentSize,
                    Data = accentPtr
                };
                SetWindowCompositionAttribute(hwnd, ref data);
                Marshal.FreeHGlobal(accentPtr);
            }
            catch { }
        }
    }

    // 暂停/播放按钮
    void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_isPaused)
        {
            VideoBg.Play();
            _isPaused = false;
        }
        else
        {
            VideoBg.Pause();
            _isPaused = true;
        }
        
        // 更新图标
        var btn = (Button)sender;
        var icon = (TextBlock)btn.Template.FindName("PauseIcon", btn);
        if (icon != null)
        {
            icon.Text = _isPaused ? "▶" : "⏸";
        }
    }

    // 设置背景（本地文件）
    void SetVideo_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGame == null) return;
        
        var dialog = new OpenFileDialog
        {
            Title = $"为「{_selectedGame.Name}」选择本地背景",
            Filter = "图片|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif|视频|*.mp4;*.avi;*.mkv;*.mov;*.webm;*.wmv|所有文件|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };

        if (dialog.ShowDialog() == true)
        {
            var ext = Path.GetExtension(dialog.FileName).ToLower();
            var isVideo = new[] { ".mp4", ".avi", ".mkv", ".mov", ".webm", ".wmv" }.Contains(ext);

            if (isVideo)
            {
                _gameManager.UpdateGame(_selectedGame.Id, null, null, dialog.FileName, "");
            }
            else
            {
                _gameManager.UpdateGame(_selectedGame.Id, null, null, "", dialog.FileName);
            }

            var updated = _gameManager.Games.FirstOrDefault(g => g.Id == _selectedGame.Id);
            if (updated != null)
            {
                _selectedGame = updated;
                LoadBackground(updated.BackgroundVideo, updated.BackgroundImage);
            }
        }
    }

    // 添加游戏
    void AddGame_Click(object sender, RoutedEventArgs e)
    {
        // 防止重复打开对话框
        if (_isAddGameDialogOpen) return;
        _isAddGameDialogOpen = true;
        
        try
        {
            var dialog = new AddGameDialog(_gameManager);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && dialog.ResultGame != null)
            {
                // 确保没有重复添加 - 通过 exePath 检查
                var existingByPath = _gameManager.Games.FirstOrDefault(g => 
                    string.Equals(Path.GetFullPath(g.ExePath), Path.GetFullPath(dialog.ResultGame.ExePath), StringComparison.OrdinalIgnoreCase));
                    
                if (existingByPath == null)
                {
                    SelectGame(dialog.ResultGame);
                }
            }
        }
        finally
        {
            _isAddGameDialogOpen = false;
        }
    }

    // 设置面板
    void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsPanel = new SettingsPanel(this, _gameManager, _selectedGame);
        settingsPanel.Owner = this;
        settingsPanel.ShowDialog();
    }

    // 搜索框输入
    void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text;
        RefreshGameList();
    }

    // 收藏筛选
    void FavoritesFilter_Click(object sender, RoutedEventArgs e)
    {
        _showFavoritesOnly = !_showFavoritesOnly;

        // 更新按钮样式
        if (sender is Button btn)
        {
            btn.ApplyTemplate();
            var border = (Border)btn.Template.FindName("b", btn);
            if (border != null)
            {
                border.Background = _showFavoritesOnly
                    ? (SolidColorBrush)FindResource("AccentColor")
                    : new SolidColorBrush(System.Windows.Media.Color.FromArgb(21, 255, 255, 255));
            }
        }

        RefreshGameList();
    }

    // 标题栏拖拽
    void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeBtn_Click(sender, e);
            return;
        }
        DragMove();
    }

    void MinimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_minimizeToTray)
            MinimizeToTray();
        else
            WindowState = WindowState.Minimized;
    }

    void MaximizeBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
