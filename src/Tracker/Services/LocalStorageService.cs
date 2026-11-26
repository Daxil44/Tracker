using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.JSInterop;

namespace Tracker.Services;

/// <summary>Thin typed wrapper over window.localStorage (via wwwroot/js/tracker.js).</summary>
public sealed class LocalStorageService(IJSRuntime js, ILogger<LocalStorageService> logger)
{
    public async ValueTask<T?> GetAsync<T>(string key, JsonTypeInfo<T> typeInfo) where T : class
    {
        var raw = await js.InvokeAsync<string?>("tracker.storage.get", key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(raw, typeInfo);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Keep a copy of unreadable data instead of silently overwriting it on the next save.
            logger.LogWarning(ex, "Stored value for {Key} could not be read; a backup was kept.", key);
            await js.InvokeAsync<bool>("tracker.storage.set", $"{key}.corrupt", raw);
            return null;
        }
    }

    /// <returns><c>false</c> when the browser refused the write (quota exceeded, storage disabled).</returns>
    public async ValueTask<bool> SetAsync<T>(string key, T value, JsonTypeInfo<T> typeInfo)
    {
        var json = JsonSerializer.Serialize(value, typeInfo);
        return await js.InvokeAsync<bool>("tracker.storage.set", key, json);
    }

    public ValueTask RemoveAsync(string key) => js.InvokeVoidAsync("tracker.storage.remove", key);
}
