using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace SafeAudioGuard.Infrastructure;

internal sealed class AppLog
{
    private readonly object _sync = new();
    internal string FilePath { get; }

    internal AppLog()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SafeAudioGuard");
        Directory.CreateDirectory(folder);
        FilePath = Path.Combine(folder, "app.log");
    }

    internal void Write(string message)
    {
        try
        {
            lock (_sync)
            {
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 1024 * 1024)
                    File.Move(FilePath, FilePath + ".old", overwrite: true);

                File.AppendAllText(FilePath,
                    $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex) { Debug.WriteLine($"SafeAudioGuard log failure: {ex.Message}"); }
    }

    // 不把驱动返回的完整设备 ID（可能含设备标识）写入日志。
    internal static string Fingerprint(string deviceId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(deviceId));
        return Convert.ToHexString(hash.AsSpan(0, 4));
    }
}
