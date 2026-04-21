using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameLauncher.Models;
using GameLauncher.Services;
using Microsoft.Win32;

namespace GameLauncher.Views;

public partial class AddGameDialog : Window
{
    private readonly GameManager _gameManager;
    private readonly GameInfo? _editGame;

    // ==================== Win32 拖拽支持 ====================
    // 使用 HwndSource.AddHook 替代 SetWindowLongPtr 子类化，
    // 避免 x64 下 SetWindowLongPtr 签名不匹配导致窗口过程替换失败的问题。

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool fAccept);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, int cch);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(IntPtr hDrop);

    private const int WM_DROPFILES = 0x0233;

    private HwndSource? _hwndSource;

    public GameInfo? ResultGame { get; private set; }

    public AddGameDialog(GameManager gameManager, GameInfo? editGame = null)
    {
        InitializeComponent();
        _gameManager = gameManager;
        _editGame = editGame;

        if (_editGame != null)
        {
            DialogTitle.Text = "编辑游戏";
            ConfirmBtn.IsEnabled = true;
            ExePathBox.Text = _editGame.ExePath;
            NameBox.Text = _editGame.Name;
            UpdateDropZoneState();
        }

        // 注册 Win32 拖拽（解决 AllowsTransparency 窗口拖拽不生效的问题）
        Loaded += OnLoadedForDrag;
    }
    
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void BrowseExe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择游戏文件",
            Filter = "可执行文件|*.exe|快捷方式|*.lnk|所有文件|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };

        if (dialog.ShowDialog() == true)
        {
            SetGamePath(dialog.FileName);
        }
    }

    // ==================== Win32 拖拽支持 ====================

    private void OnLoadedForDrag(object sender, RoutedEventArgs e)
    {
        // 使用 WindowInteropHelper 获取真实窗口句柄（对 AllowsTransparency 窗口更可靠）
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        // 告诉系统这个窗口接受文件拖放
        DragAcceptFiles(hwnd, true);

        // 使用 HwndSource.AddHook 挂钩消息（替代 SetWindowLongPtr 子类化）
        _hwndSource = HwndSource.FromHwnd(hwnd);
        if (_hwndSource != null)
        {
            _hwndSource.AddHook(WndProcHook);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProcHook);
            DragAcceptFiles(_hwndSource.Handle, false);
            _hwndSource = null;
        }
    }

    /// <summary>
    /// HwndSource 消息钩子，拦截 WM_DROPFILES
    /// </summary>
    private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DROPFILES)
        {
            handled = true;

            // ★ 关键：hDrop 是非托管句柄，必须在当前消息处理中同步提取路径，
            // 不能用 Dispatcher.BeginInvoke（异步调用时句柄可能已失效）
            string? filePath = null;
            try
            {
                uint fileCount = DragQueryFile(wParam, 0xFFFFFFFF, null, 0);
                if (fileCount > 0)
                {
                    var sb = new StringBuilder(260);
                    DragQueryFile(wParam, 0, sb, sb.Capacity);
                    filePath = sb.ToString();
                }
            }
            finally
            {
                DragFinish(wParam);
            }

            // 路径提取完毕后，异步更新 UI
            if (!string.IsNullOrEmpty(filePath))
            {
                string capturedPath = filePath;
                Dispatcher.BeginInvoke(() =>
                {
                    var ext = Path.GetExtension(capturedPath).ToLower();
                    if (ext == ".exe" || ext == ".lnk")
                    {
                        SetGamePath(capturedPath);
                    }
                });
            }

            return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    private void DropZone_Click(object sender, MouseButtonEventArgs e)
    {
        BrowseExe_Click(sender, e);
    }

    /// <summary>
    /// 设置游戏路径，自动处理 lnk 快捷方式
    /// </summary>
    private void SetGamePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        if (!File.Exists(filePath)) return;

        // 对 lnk 文件，尝试解析目标路径
        if (ShortcutResolver.IsShortcut(filePath))
        {
            var target = ShortcutResolver.ResolveShortcut(filePath);
            ExePathBox.Text = filePath;
            if (target != null)
            {
                ExePathBox.Tag = target; // 保存真实 exe 路径
            }
            else
            {
                ExePathBox.Tag = null;
                // 解析失败不阻止添加，只是提醒
                // 后续启动时 lnk 会直接用 ShellExecute 打开
            }
        }
        else
        {
            ExePathBox.Text = filePath;
            ExePathBox.Tag = null;
        }

        // 自动填名称（如果为空）
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            // 对 lnk 文件，尝试从 lnk 文件名猜测
            string nameGuess = filePath;
            if (ExePathBox.Tag is string targetPath)
            {
                nameGuess = targetPath;
            }
            NameBox.Text = IconExtractor.GuessName(nameGuess);
        }

        ConfirmBtn.IsEnabled = true;
        UpdateDropZoneState();
    }

    /// <summary>
    /// 更新拖拽区域状态，显示已选文件信息
    /// </summary>
    private void UpdateDropZoneState()
    {
        var path = ExePathBox.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            DropZoneIcon.Text = "📎";
            DropZoneText.Text = "拖入 exe 或快捷方式到此处";
            DropZoneHint.Text = "或点击浏览";
            return;
        }

        var fileName = Path.GetFileName(path);
        bool isLnk = ShortcutResolver.IsShortcut(path);

        if (isLnk && ExePathBox.Tag is string targetPath)
        {
            DropZoneIcon.Text = "🔗";
            DropZoneText.Text = fileName;
            DropZoneHint.Text = $"→ {Path.GetFileName(targetPath)}";
        }
        else
        {
            DropZoneIcon.Text = "🎮";
            DropZoneText.Text = fileName;
            DropZoneHint.Text = "已选择";
        }
    }

    private void ExePathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ConfirmBtn.IsEnabled = !string.IsNullOrWhiteSpace(ExePathBox.Text);
        UpdateDropZoneState();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var exePath = ExePathBox.Text.Trim();
        if (string.IsNullOrEmpty(exePath))
        {
            MessageBox.Show(this, "请选择游戏文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!File.Exists(exePath))
        {
            MessageBox.Show(this, "游戏文件不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            if (_editGame != null)
            {
                // 编辑模式
                _gameManager.UpdateGame(_editGame.Id, exePath,
                    string.IsNullOrWhiteSpace(NameBox.Text) ? null : NameBox.Text.Trim());
                ResultGame = _gameManager.Games.FirstOrDefault(g => g.Id == _editGame.Id);
            }
            else
            {
                // 添加模式
                var name = string.IsNullOrWhiteSpace(NameBox.Text)
                    ? null
                    : NameBox.Text.Trim();
                ResultGame = _gameManager.AddGame(exePath, name);
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"操作失败：{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
