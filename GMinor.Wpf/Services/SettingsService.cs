using System.IO;
using System.Text.Json;

namespace GMinor.Wpf.Services;

public class SettingsService
{
    private static readonly string SettingsPath =
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public AppSettings Load()
    {
        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json)
                   ?? new AppSettings(new FileRouterSettings("", ""));
        }
        catch
        {
            return new AppSettings(new FileRouterSettings("", ""));
        }
    }

    public void Save(string sourcePath, string destinationPath)
    {
        var settings = new AppSettings(new FileRouterSettings(sourcePath, destinationPath));
        var json = JsonSerializer.Serialize(settings, WriteOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
