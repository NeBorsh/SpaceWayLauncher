using Dapper;

namespace SpaceWay.Core.Data;

/// <summary>
/// Launcher settings as key-value pairs.
/// </summary>
public sealed class SettingsStore(LauncherDatabase db)
{
    public string? Get(string key) => db.Connection.QuerySingleOrDefault<string>(
        "SELECT Value FROM Config WHERE Key = @key", new { key });

    public T Get<T>(string key, T fallback) where T : IParsable<T>
    {
        var raw = Get(key);
        if (raw == null)
            return fallback;

        return T.TryParse(raw, null, out var value) ? value : fallback;
    }

    public bool GetBool(string key, bool fallback) => Get(key, fallback);

    public void Set(string key, string value) => db.Connection.Execute(
        """
        INSERT INTO Config (Key, Value) VALUES (@key, @value)
        ON CONFLICT (Key) DO UPDATE SET Value = excluded.Value
        """,
        new { key, value });

    public void Set<T>(string key, T value) where T : IFormattable =>
        Set(key, value.ToString(null, System.Globalization.CultureInfo.InvariantCulture));

    public void SetBool(string key, bool value) => Set(key, value ? "True" : "False");

    public void Remove(string key) => db.Connection.Execute(
        "DELETE FROM Config WHERE Key = @key", new { key });
}

/// <summary>Known setting keys, to avoid scattering strings across the code.</summary>
public static class SettingKeys
{
    public const string Language = "language";
    public const string SelectedAccount = "selected-account";

    /// <summary>
    /// Engine compatibility mode (<c>display.compat</c>). The official launcher
    /// always passes it; here it is opt-in, since it degrades graphics on modern
    /// hardware but is required on some old GPUs.
    /// </summary>
    public const string DisplayCompat = "game.compat";

    public const string FilterHideFull = "filter.hide-full";
    public const string FilterHideEmpty = "filter.hide-empty";
    public const string FilterHideAdult = "filter.hide-adult";
    public const string FilterSort = "filter.sort";
}
