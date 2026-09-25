namespace SpaceWay.Core.Localization;

/// <summary>
/// An error whose message is shown to the player.
/// </summary>
public abstract class LocalizedException : Exception
{
    private readonly (string Name, object Value)[] _args;

    protected LocalizedException(
        string key,
        Exception? inner = null,
        params (string Name, object Value)[] args)
        : base(key, inner)
    {
        Key = key;
        _args = args;
    }

    /// <summary>
    /// Translation key. Tests identify the error by it, since comparing translated
    /// text would break a test with every wording change.
    /// </summary>
    public string Key { get; }

    public override string Message => Loc.T(Key, _args);
}
