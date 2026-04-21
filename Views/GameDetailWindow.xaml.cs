using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameLauncher.Models;
using GameLauncher.Services;
using Microsoft.Win32;

namespace GameLauncher.Views;

public partial class GameDetailWindow : Window
{
    private readonly GameManager _gameManager;
    private readonly GameInfo _game;

    public GameDetailWindow(GameManager gameManager, GameInfo game)
    {
        InitializeComponent();
        _gameManager = gameManager;
        _game = game;

        Title = game.Name;
        GameName.Text = game.Name;

        // 加载图标
        LoadIcon();

        // 加载视频背景
        LoadBackgroundVideo();

        // 设置按钮文字
        UpdateVideoBtnText();
    }

    private void LoadIcon()
    {
        try
        {
            if (!string.IsNullOrEmpty(_game.IconPath) && File.Exists(_game.IconPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_game.IconPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                GameIcon.Source = bitmap;
            }
        }
        catch { }
    }

    private void LoadBackgroundVideo()
    {
        if (string.IsNullOrEmpty(_game.BackgroundVideo) || !File.Exists(_game.BackgroundVideo))
        {
            VideoLabel.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            VideoBg.Source = new Uri(_game.BackgroundVideo, UriKind.Absolute);
            VideoBg.ScrubbingEnabled = true;
            VideoBg.Play();
            VideoBg.Visibility = Visibility.Visible;
            VideoOverlay.Visibility = Visibility.Visible;
            VideoLabel.Text = $"🎬 {Path.GetFileName(_game.BackgroundVideo)}";
            VideoLabel.Visibility = Visibility.Visible;
        }
        catch { }
    }

    private void UpdateVideoBtnText()
    {
        if (string.IsNullOrEmpty(_game.BackgroundVideo))
            VideoBtn.Content = "🎬 添加专属背景";
        else
            VideoBtn.Content = "🎬 更换专属背景";
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _gameManager.LaunchGame(_game.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"启动失败：{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddGameDialog(_gameManager, _game);
        dialog.Owner = this.Owner; // 保持主窗口为Owner
        if (dialog.ShowDialog() == true)
        {
            // 刷新数据
            var updated = _gameManager.Games.FirstOrDefault(g => g.Id == _game.Id);
            if (updated != null)
            {
                Title = updated.Name;
                GameName.Text = updated.Name;
                LoadIcon();
            }
        }
    }

    private void SetVideo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择专属背景视频",
            Filter = "视频文件|*.mp4;*.avi;*.mkv;*.mov;*.webm;*.wmv|所有文件|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };

        if (dialog.ShowDialog() == true)
        {
            _gameManager.UpdateGame(_game.Id, null, null, dialog.FileName);

            // 重新加载视频
            var updated = _gameManager.Games.FirstOrDefault(g => g.Id == _game.Id);
            if (updated != null)
            {
                _game.BackgroundVideo = updated.BackgroundVideo;
                LoadBackgroundVideo();
                UpdateVideoBtnText();
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        VideoBg.Stop();
        VideoBg.Source = null;
    }
}
