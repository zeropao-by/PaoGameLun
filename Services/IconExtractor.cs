using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameLauncher.Services;

public static class IconExtractor
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO shfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private const uint SHGFI_ICON = 0x100;
    private const uint SHIL_LARGE = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    /// <summary>
    /// 从 exe 提取图标，返回 BitmapSource
    /// </summary>
    public static BitmapSource? ExtractIcon(string exePath, int size = 64)
    {
        if (!File.Exists(exePath)) return null;

        try
        {
            // 方法1：Shell API
            var bmp = ExtractViaShellAPI(exePath, size);
            if (bmp != null) return bmp;
        }
        catch { }

        return null;
    }

    private static BitmapSource? ExtractViaShellAPI(string exePath, int size)
    {
        SHFILEINFO shinfo = new();
        IntPtr hImg = SHGetFileInfo(exePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHIL_LARGE);
        if (hImg == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero) return null;

        try
        {
            if (GetIconInfo(shinfo.hIcon, out ICONINFO iconInfo))
            {
                IntPtr hBmp = iconInfo.hbmColor != IntPtr.Zero ? iconInfo.hbmColor : iconInfo.hbmMask;
                if (hBmp != IntPtr.Zero)
                {
                    try
                    {
                        var source = Imaging.CreateBitmapSourceFromHBitmap(
                            hBmp,
                            IntPtr.Zero,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        source.Freeze();

                        // 缩放
                        var scaled = new TransformedBitmap(source,
                            new ScaleTransform(size / (double)source.PixelWidth, size / (double)source.PixelHeight));
                        scaled.Freeze();
                        return scaled;
                    }
                    finally
                    {
                        if (iconInfo.hbmMask != IntPtr.Zero) DeleteObject(iconInfo.hbmMask);
                        if (iconInfo.hbmColor != IntPtr.Zero) DeleteObject(iconInfo.hbmColor);
                    }
                }
            }
        }
        finally
        {
            DestroyIcon(shinfo.hIcon);
        }

        return null;
    }

    /// <summary>
    /// 生成默认图标（纯 WPF）
    /// </summary>
    public static BitmapSource CreateDefaultIcon(int size, string hexColor)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var color = ParseColor(hexColor);
            var brush = new SolidColorBrush(color);
            dc.DrawEllipse(brush, null, new System.Windows.Point(size / 2.0, size / 2.0), size / 2.0 - 1, size / 2.0 - 1);

            // 绘制字母 G
            var tf = new Typeface("Segoe UI");
            var ft = new FormattedText("G",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                tf, size / 2.0, Brushes.White,
                VisualTreeHelper.GetDpi(dv).PixelsPerDip);
            ft.TextAlignment = TextAlignment.Center;
            dc.DrawText(ft, new System.Windows.Point(size / 2.0, size / 2.0 - ft.Height / 2.0));
        }

        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    public static string GuessName(string exePath)
    {
        var name = Path.GetFileNameWithoutExtension(exePath);
        // 清理快捷方式后缀（.lnk 去掉后可能还有 .exe 残留）
        if (exePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            // 对 lnk 文件，GetFileNameWithoutExtension 已去掉 .lnk
            // 如果名称本身是 "xxx.exe"，再去掉 .exe
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = Path.GetFileNameWithoutExtension(name);
        }
        name = Regex.Replace(name, @"(_win64|_win32|_x64|_x86|-win|-x64|_launcher|-launcher)$", "", RegexOptions.IgnoreCase);
        name = name.Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(name) ? "未知游戏" : name;
    }

    public static string GuessColor(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("原神") || n.Contains("genshin") || n.Contains("gi")) return "#FF6B6B";
        if (n.Contains("崩坏") || n.Contains("honkai") || n.Contains("himi")) return "#FFD93D";
        if (n.Contains("绝区零") || n.Contains("zzz") || n.Contains("zzmi")) return "#A855F7";
        if (n.Contains("鸣潮") || n.Contains("wuthering") || n.Contains("wwmi")) return "#FF9F43";
        if (n.Contains("塞尔达") || n.Contains("zelda") || n.Contains("srmi")) return "#6BCB77";
        if (n.Contains("mc") || n.Contains("minecraft")) return "#6BCB77";
        if (n.Contains("csgo") || n.Contains("cs2")) return "#FFA500";
        return "#5B7FFF";
    }

    private static Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            return Color.FromRgb(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16)
            );
        }
        return Color.FromRgb(91, 127, 255);
    }
}
