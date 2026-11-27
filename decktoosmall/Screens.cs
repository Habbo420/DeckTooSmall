using System;

namespace DeckTooSmall;

// Picks a UI size for a screen. Sizes here are in game slider units (8 = normal).
internal static class Screens
{
    public const float Smallest = 4, Largest = 24;

    // Ready-made choices, as a share of the recommended size.
    public static readonly (string Key, string Label, float Share)[] Presets =
    {
        ("compact", "Compact", 0.8f),
        ("recommended", "Recommended", 1f),
        ("large", "Large", 1.2f),
    };

    // The Steam Deck (1280x800) gets 10. Other screens get the same look: the
    // size grows or shrinks with the screen, going by its tighter direction.
    public static float Recommended(int width, int height, float share = 1)
    {
        float units = 10 * Math.Min(width / 1280f, height / 800f) * share;
        float toNearestHalf = (float)Math.Round(units * 2) / 2;
        return Math.Clamp(toNearestHalf, Smallest, Largest);
    }
}
