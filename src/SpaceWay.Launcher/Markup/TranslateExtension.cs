using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace SpaceWay.Launcher.Markup;

/// <summary>
/// Markup like <c>{loc:T nav-servers}</c>.
/// Returns a binding rather than a string so text updates when the language changes.
/// </summary>
public sealed class TranslateExtension : MarkupExtension
{
    public TranslateExtension()
    {
    }

    public TranslateExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding($"[{Key}]")
        {
            Mode = BindingMode.OneWay,
            Source = Localizer.Instance,
        };
    }
}
