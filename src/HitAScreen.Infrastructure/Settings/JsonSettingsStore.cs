using System.Text.Json;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Infrastructure;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    public JsonSettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new UserSettings();
        }

        await using var stream = File.OpenRead(_filePath);
        var settings = await JsonSerializer.DeserializeAsync<UserSettings>(stream, JsonOptions, cancellationToken);
        return settings ?? new UserSettings();
    }

    public async Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
    }
}
