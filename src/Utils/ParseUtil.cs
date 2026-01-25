using System;
using System.Globalization;

using SwiftlyS2.Shared.Natives;

namespace BlockPasses;

public static class ParseUtil
{
    public static Vector ParseVector(string raw)
    {
        if (TryParse3(raw, out var x, out var y, out var z))
            return new Vector(x, y, z);

        return Vector.Zero;
    }

    public static QAngle ParseQAngle(string raw)
    {
        if (TryParse3(raw, out var x, out var y, out var z))
            return new QAngle(x, y, z);

        return new QAngle(0, 0, 0);
    }

    private static bool TryParse3(string raw, out float x, out float y, out float z)
    {
        x = y = z = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var split = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (split.Length < 3) return false;

        return float.TryParse(split[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
               && float.TryParse(split[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
               && float.TryParse(split[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z);
    }
}
