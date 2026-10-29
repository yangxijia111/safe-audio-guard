using System.Text.Json;

namespace SafeAudioGuard.Infrastructure;

internal sealed class AppSettings
{
    public bool StartWithWindows { get; set; } = true;
}

internal sealed class SettingsStore
{
    private readonly string _file;

    internal SettingsStore()
    {
        _file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SafeAudioGuard", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
    }

    internal AppSettings Load()
    {
        try
        {
            if (File.Exists(_file))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_file)) ?? new AppSettings();
        }
        catch (Exception) { /* 损坏配置回退到安全默认值 */ }
        return new AppSettings();
    }

    internal void Save(AppSettings settings)
    {
        var temp = _file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        File.Move(temp, _file, overwrite: true);
    }
}
