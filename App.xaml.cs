using System;
using System.Windows;
using System.Text;

namespace GameLauncher;

public partial class App : Application
{
    private static string GetFullException(Exception? ex, int depth = 0)
    {
        if (ex == null) return "(null)";
        var sb = new StringBuilder();
        sb.Append(' ', depth * 2).AppendLine(ex.GetType().FullName);
        sb.Append(' ', depth * 2).Append("Message: ").AppendLine(ex.Message);
        if (!string.IsNullOrEmpty(ex.StackTrace))
            sb.Append(' ', depth * 2).AppendLine(ex.StackTrace?.Split('\n')?[0]?.Trim());
        if (ex.InnerException != null && depth < 5)
        {
            sb.AppendLine("--- Inner Exception ---");
            sb.Append(GetFullException(ex.InnerException, depth + 1));
        }
        return sb.ToString();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 全局异常处理
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show(GetFullException(ex), "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show(GetFullException(args.Exception), "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
