using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace GameLauncher.Services;

/// <summary>
/// 解析 Windows 快捷方式 (.lnk) 文件，获取目标可执行文件路径
/// </summary>
public static class ShortcutResolver
{
    /// <summary>
    /// 解析 .lnk 快捷方式，返回目标可执行文件路径。失败返回 null。
    /// </summary>
    public static string? ResolveShortcut(string lnkPath)
    {
        if (!File.Exists(lnkPath)) return null;
        if (!Path.GetExtension(lnkPath).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) return null;

        try
        {
            // 方法1：使用 WScript.Shell（最简单可靠）
            var target = ResolveViaWshShell(lnkPath);
            if (target != null) return target;
        }
        catch { }

        try
        {
            // 方法2：通过 COM IShellLinkW + IPersistFile
            var target = ResolveViaCom(lnkPath);
            if (target != null) return target;
        }
        catch { }

        return null;
    }

    /// <summary>
    /// 方法1：使用 WScript.Shell 的 CreateShortcut 读取 TargetPath
    /// </summary>
    private static string? ResolveViaWshShell(string lnkPath)
    {
        // 通过 COM ProgID 创建 WScript.Shell
        var type = Type.GetTypeFromProgID("WScript.Shell");
        if (type == null) return null;

        dynamic? shell = null;
        try
        {
            shell = Activator.CreateInstance(type);
            if (shell == null) return null;

            dynamic shortcut = shell.CreateShortcut(lnkPath);
            string? targetPath = shortcut.TargetPath as string;

            if (string.IsNullOrEmpty(targetPath)) return null;
            if (!File.Exists(targetPath)) return null;

            return Path.GetFullPath(targetPath);
        }
        finally
        {
            if (shell != null)
            {
                try { Marshal.ReleaseComObject(shell); } catch { }
            }
        }
    }

    /// <summary>
    /// 方法2：通过 IShellLinkW COM 接口解析
    /// </summary>
    private static string? ResolveViaCom(string lnkPath)
    {
        var shellLinkType = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"));
        if (shellLinkType == null) return null;

        object? shellLink = null;
        try
        {
            shellLink = Activator.CreateInstance(shellLinkType);
            if (shellLink == null) return null;

            var persistFile = (IPersistFile)shellLink;
            persistFile.Load(lnkPath, 0);

            // 先调用 Resolve 确保快捷方式被正确解析
            // (IntPtr.Zero = 无父窗口, SLR_NO_UI = 0x1, SLR_ANY_MATCH = 0x2)
            var sl = (IShellLinkW)shellLink;
            sl.Resolve(IntPtr.Zero, 0x1);

            var sb = new StringBuilder(260);
            var fd = new WIN32_FIND_DATAW();
            sl.GetPath(sb, sb.Capacity, ref fd, 0);

            string? targetPath = sb.ToString().Trim();
            if (string.IsNullOrEmpty(targetPath))
            {
                sb.Clear();
                sl.GetPath(sb, sb.Capacity, ref fd, 1); // SLGP_SHORTPATH
                targetPath = sb.ToString().Trim();
            }

            if (string.IsNullOrEmpty(targetPath)) return null;
            // 不强制检查 File.Exists，因为某些目标可能是网络路径或环境变量路径
            return Path.GetFullPath(targetPath);
        }
        finally
        {
            if (shellLink != null)
            {
                try { Marshal.ReleaseComObject(shellLink); } catch { }
            }
        }
    }

    #region COM 接口定义

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, ref WIN32_FIND_DATAW pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchMaxPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFileName, int cchMaxPath);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    #endregion

    /// <summary>
    /// 判断文件是否为 .lnk 快捷方式
    /// </summary>
    public static bool IsShortcut(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取 .lnk 文件的目标路径。如果文件不是 lnk，返回原路径。
    /// </summary>
    public static string GetTargetPath(string filePath)
    {
        if (IsShortcut(filePath))
        {
            var resolved = ResolveShortcut(filePath);
            if (resolved != null) return resolved;
        }
        return filePath;
    }
}
