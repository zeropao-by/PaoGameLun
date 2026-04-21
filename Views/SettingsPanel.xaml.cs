using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GameLauncher.Models;
using GameLauncher.Services;
using Microsoft.Win32;

namespace GameLauncher.Views;

public partial class SettingsPanel : Window
{
    private readonly GameManager _gameManager;
    private readonly MainWindow _mainWindow;
    private GameInfo? _selectedGame;

    public SettingsPanel(MainWindow mainWindow, GameManager gameManager, GameInfo? selectedGame)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        _gameManager = gameManager;
        _selectedGame = selectedGame;

        // 初始化状态
        GlassToggle.IsChecked = _gameManager.Settings.EnableAcrylicGlass;
        TrayToggle.IsChecked = _gameManager.Settings.MinimizeToTray;
        AutoStartToggle.IsChecked = _gameManager.Settings.AutoStart;

        // 显示当前游戏信息
        if (_selectedGame != null)
        {
            GameSettingsPanel.Visibility = Visibility.Visible;
            FavoriteToggle.IsChecked = _selectedGame.IsFavorite;
            FavoriteStatus.Text = _selectedGame.IsFavorite ? "已收藏" : "未收藏";
        }
        else
        {
            GameSettingsPanel.Visibility = Visibility.Collapsed;
        }

    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // 双击不做什么
        }
        else
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void GlassToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_mainWindow == null || GlassToggle == null) return;

        // 检查开关当前状态（而非 settings 中的旧状态）
        if (GlassToggle.IsChecked == true)
        {
            _mainWindow.EnableAcrylicGlass();
        }
        else
        {
            _mainWindow.DisableAcrylicGlass();
        }
    }

    private void TrayToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_mainWindow == null || TrayToggle == null) return;

        _mainWindow.SetMinimizeToTray(TrayToggle.IsChecked == true);
    }

    private void AutoStartToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_mainWindow == null || AutoStartToggle == null) return;

        var enabled = AutoStartToggle.IsChecked == true;
        _gameManager.Settings.AutoStart = enabled;
        _gameManager.SaveSettings();

        // 修改注册表
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                if (enabled)
                {
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue("PaoGameLun", $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue("PaoGameLun", false);
                }
            }
        }
        catch
        {
            // 权限不足时静默失败
        }
    }

    private void EditName_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedGame == null) return;

        var dialog = new AddGameDialog(_gameManager, _selectedGame);
        dialog.Owner = _mainWindow;
        if (dialog.ShowDialog() == true)
        {
            var updated = _gameManager.Games.FirstOrDefault(g => g.Id == _selectedGame.Id);
            if (updated != null)
            {
                _selectedGame = updated;
                _mainWindow.RefreshGameSelection(updated);
            }
        }
    }

    private void SelectImage_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedGame == null) return;
        e.Handled = true;

        var dialog = new OpenFileDialog
        {
            Title = $"为「{_selectedGame.Name}」选择背景图片",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif|所有文件|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };

        if (dialog.ShowDialog() == true)
        {
            _gameManager.UpdateGame(_selectedGame.Id, backgroundImage: dialog.FileName);
            var updated = _gameManager.Games.FirstOrDefault(g => g.Id == _selectedGame.Id);
            if (updated != null)
            {
                _selectedGame = updated;
                _mainWindow.LoadBackground(updated.BackgroundVideo, updated.BackgroundImage);
            }
        }
    }

    private void SelectVideo_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedGame == null) return;
        e.Handled = true;

        var dialog = new OpenFileDialog
        {
            Title = $"为「{_selectedGame.Name}」选择背景视频",
            Filter = "视频文件|*.mp4;*.avi;*.mkv;*.mov;*.webm;*.wmv|所有文件|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };

        if (dialog.ShowDialog() == true)
        {
            _gameManager.UpdateGame(_selectedGame.Id, backgroundVideo: dialog.FileName);
            var updated = _gameManager.Games.FirstOrDefault(g => g.Id == _selectedGame.Id);
            if (updated != null)
            {
                _selectedGame = updated;
                _mainWindow.LoadBackground(updated.BackgroundVideo, updated.BackgroundImage);
            }
        }
    }

    private void SetOnlineImage_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedGame == null) return;
        e.Handled = true;

        var inputDialog = CreateUrlInputDialog(
            "🌐 输入在线图片URL",
            "输入图片的网络URL地址",
            "支持 .jpg .png .gif .webp 等格式",
            (url) =>
            {
                // 保存为 url: 前缀
                _gameManager.UpdateGame(_selectedGame.Id, backgroundImage: "url:" + url);
                var updated = _gameManager.Games.FirstOrDefault(g => g.Id == _selectedGame.Id);
                if (updated != null)
                {
                    _selectedGame = updated;
                    _mainWindow.LoadBackground(updated.BackgroundVideo, updated.BackgroundImage);
                }
            });

        inputDialog.ShowDialog();
    }

    private void SetOnlineVideo_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedGame == null) return;
        e.Handled = true;

        var inputDialog = CreateUrlInputDialog(
            "📹 输入在线视频URL",
            "输入视频的网络URL地址",
            "支持 .mp4 .webm .m3u8 等格式",
            (url) =>
            {
                // 保存为 url: 前缀
                _gameManager.UpdateGame(_selectedGame.Id, backgroundVideo: "url:" + url);
                var updated = _gameManager.Games.FirstOrDefault(g => g.Id == _selectedGame.Id);
                if (updated != null)
                {
                    _selectedGame = updated;
                    _mainWindow.LoadBackground(updated.BackgroundVideo, updated.BackgroundImage);
                }
            });

        inputDialog.ShowDialog();
    }

    /// <summary>
    /// 创建统一的URL输入对话框，使用与主窗口一致的风格
    /// </summary>
    private Window CreateUrlInputDialog(string title, string description, string hint, Action<string> onConfirm)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 520,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            ResizeMode = ResizeMode.NoResize
        };

        // 外层圆角边框
        var mainBorder = new Border
        {
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromRgb(36, 36, 40)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 55)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(8)
        };
        mainBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            ShadowDepth = 0,
            BlurRadius = 30,
            Opacity = 0.6,
            Color = Colors.Black
        };

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.Margin = new Thickness(24, 20, 24, 20);

        // 标题栏
        var titlePanel = new Grid();
        titlePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titlePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };

        var closeBtn = new Button
        {
            Content = "✕",
            Width = 28,
            Height = 28,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 135)),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            FontSize = 14
        };
        closeBtn.Template = CreateButtonTemplate("#FF3B30");
        Grid.SetColumn(closeBtn, 1);

        titlePanel.Children.Add(titleText);
        titlePanel.Children.Add(closeBtn);

        // 内容区
        var descLabel = new TextBlock
        {
            Text = description,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 165)),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var urlInput = new TextBox
        {
            Height = 38,
            FontSize = 14,
            Padding = new Thickness(12, 0, 12, 0),
            Background = new SolidColorBrush(Color.FromRgb(50, 50, 55)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
            BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
            CaretBrush = Brushes.White
        };
        urlInput.GotFocus += (s, e) =>
        {
            urlInput.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 255));
        };
        urlInput.LostFocus += (s, e) =>
        {
            urlInput.BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75));
        };

        // 提示文字
        var hintLabel = new TextBlock
        {
            Text = hint,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 105)),
            Margin = new Thickness(0, 6, 0, 0)
        };

        var contentPanel = new StackPanel();
        contentPanel.Children.Add(descLabel);
        contentPanel.Children.Add(urlInput);
        contentPanel.Children.Add(hintLabel);

        // 按钮区
        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var btnCancel = new Button
        {
            Content = "取消",
            Width = 90,
            Height = 36,
            Background = new SolidColorBrush(Color.FromRgb(60, 60, 65)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            FontSize = 13,
            Margin = new Thickness(0, 0, 10, 0)
        };
        btnCancel.Template = CreateButtonTemplate("#3A3A3E");

        var btnOK = new Button
        {
            Content = "确定",
            Width = 90,
            Height = 36,
            Background = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            FontSize = 13
        };
        btnOK.Template = CreateButtonTemplate("#007AFF");

        btnPanel.Children.Add(btnCancel);
        btnPanel.Children.Add(btnOK);

        Grid.SetRow(titlePanel, 0);
        Grid.SetRow(contentPanel, 1);
        Grid.SetRow(btnPanel, 2);

        content.Children.Add(titlePanel);
        content.Children.Add(contentPanel);
        content.Children.Add(btnPanel);

        mainBorder.Child = content;
        dialog.Content = mainBorder;

        // 事件绑定
        closeBtn.Click += (s, args) => dialog.Close();
        btnCancel.Click += (s, args) => dialog.Close();
        btnOK.Click += (s, args) =>
        {
            var url = urlInput.Text.Trim();
            if (!string.IsNullOrEmpty(url))
            {
                dialog.DialogResult = true;
                dialog.Close();
                onConfirm(url);
            }
            else
            {
                urlInput.Focus();
            }
        };

        urlInput.KeyDown += (s, args) =>
        {
            if (args.Key == Key.Enter)
            {
                btnOK.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            else if (args.Key == Key.Escape)
            {
                dialog.Close();
            }
        };

        // 支持拖拽移动
        mainBorder.MouseLeftButtonDown += (s, args) =>
        {
            if (args.ClickCount == 1)
            {
                dialog.DragMove();
            }
        };

        // 显示后自动聚焦输入框
        dialog.Loaded += (s, args) =>
        {
            urlInput.Focus();
        };

        return dialog;
    }

    /// <summary>
    /// 创建按钮模板
    /// </summary>
    private ControlTemplate CreateButtonTemplate(string hoverColor)
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "border";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));

        var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentPresenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);

        border.AppendChild(contentPresenter);
        template.VisualTree = border;

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter { TargetName = "border", Property = Border.BackgroundProperty, Value = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hoverColor)) });
        template.Triggers.Add(hoverTrigger);

        return template;
    }

    private void ClearBackground_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedGame == null) return;
        e.Handled = true;

        var result = MessageBox.Show(this,
            $"确定清除「{_selectedGame.Name}」的背景图片和视频？",
            "确认清除",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _gameManager.UpdateGame(_selectedGame.Id, backgroundVideo: "", backgroundImage: "");
            var updated = _gameManager.Games.FirstOrDefault(g => g.Id == _selectedGame.Id);
            if (updated != null)
            {
                _selectedGame = updated;
                _mainWindow.LoadBackground(null, null);
            }
        }
    }

    private void DeleteGame_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedGame == null) return;

        var result = MessageBox.Show(this,
            $"确定删除「{_selectedGame.Name}」？",
            "确认删除",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _gameManager.RemoveGame(_selectedGame.Id);
            _mainWindow.RefreshGameList();
            Close();
        }
    }

    private void FavoriteToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_selectedGame == null || FavoriteToggle == null) return;

        var isFavorite = FavoriteToggle.IsChecked == true;
        _selectedGame.IsFavorite = isFavorite;
        _gameManager.SaveGames();
        FavoriteStatus.Text = isFavorite ? "已收藏" : "未收藏";
        _mainWindow.RefreshGameList();
    }

}
