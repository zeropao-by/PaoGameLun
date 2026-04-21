using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GameLauncher.Services;

public static class AcrylicBlurService
{
    // Windows API for blur effect
    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

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
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,      // 经典模糊
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4, // Win10 1903+ 液态玻璃
        ACCENT_INVALID_STATE = 5
    }

    /// <summary>
    /// 启用/禁用窗口液态玻璃模糊效果
    /// </summary>
    public static void SetBlur(Window window, bool enabled, uint tintColor = 0x40000000)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            var accent = new AccentPolicy();

            if (enabled)
            {
                // 尝试 Win11/Win10 1903+ 的液态玻璃效果
                accent.AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND;
            }
            else
            {
                accent.AccentState = AccentState.ACCENT_DISABLED;
            }

            accent.GradientColor = (int)(tintColor | 0xFF000000);

            var accentSize = Marshal.SizeOf(accent);
            var accentPtr = Marshal.AllocHGlobal(accentSize);

            try
            {
                Marshal.StructureToPtr(accent, accentPtr, true);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    SizeOfData = accentSize,
                    Data = accentPtr
                };

                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
        }
        catch
        {
            // 忽略不支持的系统
        }
    }

    /// <summary>
    /// 启用经典毛玻璃效果（Win10 1809+）
    /// </summary>
    public static void SetClassicBlur(Window window, bool enabled)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            var accent = new AccentPolicy();

            if (enabled)
            {
                accent.AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND;
            }
            else
            {
                accent.AccentState = AccentState.ACCENT_DISABLED;
            }

            accent.GradientColor = 0x40000000;

            var accentSize = Marshal.SizeOf(accent);
            var accentPtr = Marshal.AllocHGlobal(accentSize);

            try
            {
                Marshal.StructureToPtr(accent, accentPtr, true);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    SizeOfData = accentSize,
                    Data = accentPtr
                };

                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
        }
        catch
        {
            // 忽略不支持的系统
        }
    }
}
