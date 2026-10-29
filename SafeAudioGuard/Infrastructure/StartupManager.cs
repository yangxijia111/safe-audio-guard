using Microsoft.Win32;

namespace SafeAudioGuard.Infrastructure;

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SafeAudioGuard";

    internal static void Configure(bool enabled)
    {
#if DEBUG
        // 调试模式不能把临时 bin/Debug 目录注册为开机项。
        return;
#else
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("无法打开用户开机启动注册表项");
        if (enabled)
        {
            var exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("无法定位程序 EXE 文件");
            if (!Path.GetFileName(exePath).Equals("SafeAudioGuard.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请先发布为 SafeAudioGuard.exe，再启用开机启动");
            key.SetValue(ValueName, $"\"{exePath}\"", RegistryValueKind.String);
        }
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
#endif
    }
}
