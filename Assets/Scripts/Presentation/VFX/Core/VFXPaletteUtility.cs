using UnityEngine;

public static class VFXPaletteUtility
{
    public static Color ParseColor(string hex, Color fallback)
    {
        if (!string.IsNullOrWhiteSpace(hex) && ColorUtility.TryParseHtmlString(hex, out var color))
        {
            return color;
        }

        return fallback;
    }

    public static Color Soften(Color color, float alpha = 0.5f)
    {
        color.a = alpha;
        return color;
    }
}
