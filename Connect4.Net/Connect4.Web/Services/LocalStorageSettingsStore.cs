using System.Text.Json;
using Microsoft.JSInterop;
using Connect4.App.Models;
using Connect4.App.Services;

namespace Connect4.Web.Services;

/// <summary>Settings as JSON in the browser's local storage.</summary>
public sealed class LocalStorageSettingsStore(IJSInProcessRuntime js) : ISettingsStore
{
    private const string Key = "connect4.settings";

    public AppSettings Load()
    {
        try
        {
            string? json = js.Invoke<string?>("localStorage.getItem", Key);
            return json is null
                ? AppSettings.Default
                : JsonSerializer.Deserialize(json, AppSettingsJson.Default.AppSettings)?.Normalize() ?? AppSettings.Default;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or JSException)
        {
            return AppSettings.Default;
        }
    }

    public bool Save(AppSettings settings)
    {
        try
        {
            js.InvokeVoid("localStorage.setItem", Key, JsonSerializer.Serialize(settings, AppSettingsJson.Default.AppSettings));
            return true;
        }
        catch (JSException)
        {
            return false;
        }
    }
}
