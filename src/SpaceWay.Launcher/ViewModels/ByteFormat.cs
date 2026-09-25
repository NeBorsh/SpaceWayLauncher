using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// Human-readable sizes.
/// </summary>
public static class ByteFormat
{
    private static readonly string[] UnitKeys = ["unit-bytes", "unit-kib", "unit-mib", "unit-gib"];

    public static string Format(long bytes)
    {
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < UnitKeys.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var name = Loc.T(UnitKeys[unit]);

        return unit == 0 ? $"{bytes} {name}" : $"{value:0.#} {name}";
    }
}
