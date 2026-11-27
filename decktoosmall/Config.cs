using System.Collections.Generic;

namespace DeckTooSmall;

// Saved in VintagestoryData/ModConfig/decktoosmall.json
// Sizes are stored like the game does: 1.0 is 8 on the GUI Scale slider.
public class DeckTooSmallConfig
{
    public bool Enabled { get; set; } = true;

    public bool FullScreenMap { get; set; } = true;
    public bool FullScreenHandbook { get; set; } = true;
    public bool FullScreenSettings { get; set; } = true;

    // The main size, used by all menus unless they have their own.
    public float UiScale { get; set; } = 1.25f;

    // The main size you chose on each screen, for example "1280x800".
    public Dictionary<string, float> ScreenSizes { get; set; } = new();

    // Your own GUI Scale, remembered while the mod's size is in use.
    public float? GameScale { get; set; }

    public SizeRule Text { get; set; } = new();

    // One rule per kind of window, by the keys in Group.All.
    public Dictionary<string, SizeRule> Windows { get; set; } = new();

    // Title bar icons, text buttons and item slots, by the keys in DeckTooSmall.Parts.All.
    public Dictionary<string, SizeRule> PartSizes { get; set; } = new();

    public SizeRule Part(string key)
    {
        if (!PartSizes.TryGetValue(key, out SizeRule? rule)) PartSizes[key] = rule = new SizeRule();
        return rule;
    }

    public SizeRule Window(string key)
    {
        if (!Windows.TryGetValue(key, out SizeRule? rule)) Windows[key] = rule = new SizeRule();
        return rule;
    }
}

// The size of one thing (a kind of window, or text).
public class SizeRule
{
    // Keep ticked: stays at Size, whatever the main size does.
    public bool Keep { get; set; }
    public float Size { get; set; } = 1;

    // Keep not ticked: follows the main size, multiplied by Ratio.
    public float Ratio { get; set; } = 1;

    public float Actual(float main) => Keep ? Size : main * Ratio;

    public void Set(float main, float value)
    {
        if (Keep) Size = value;
        else Ratio = value / main;
    }

    // Ticking or unticking never changes how big the thing is right now.
    public void SetKeep(float main, bool keep)
    {
        float now = Actual(main);
        Keep = keep;
        Set(main, now);
    }
}

// The kinds of window that can have their own size.
internal static class Group
{
    public static readonly (string Key, string Label)[] All =
    {
        ("hotbar", "Hotbar and health"),
        ("inventory", "Inventory"),
        ("character", "Character screen"),
        ("handbook", "Handbook (windowed)"),
        ("chests", "Chests and machines"),
        ("map", "World map (windowed)"),
        ("minimap", "Minimap"),
        ("other", "Other windows"),
    };
}
