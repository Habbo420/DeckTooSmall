using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Client;

namespace DeckTooSmall;

// Makes parts of windows bigger: title bar icons, text buttons and item slots.
// Each "factor" is how big the part is compared with normal: 1 = unchanged.
internal static class Parts
{
    public static readonly (string Key, string Label, double Smallest, double Largest)[] All =
    {
        ("titlebars", "Title bar icons", 0.8, 1.5), // bigger would not fit in the bar
        ("buttons", "Text buttons", 0.8, 1.5),
        ("slots", "Item slots", 0.8, 1.4),          // bigger overlaps on the character screen
    };

    private static double titleFactor = 1;
    private static double buttonFactor = 1;

    // The game's own slot and item sizes.
    private static double slotSize, itemSize;

    // The padding each button had before we touched it.
    private static ConditionalWeakTable<ElementBounds, Tuple<double, double>> padding = new();

    public static void Apply(Harmony harmony)
    {
        slotSize = GuiElementPassiveItemSlot.unscaledSlotSize;
        itemSize = GuiElementPassiveItemSlot.unscaledItemSize;
        titleFactor = buttonFactor = 1;
        padding = new();

        harmony.Patch(AccessTools.DeclaredMethod(typeof(GuiElementDialogTitleBar), "ComposeTextElements"),
            prefix: new HarmonyMethod(typeof(Parts), nameof(BiggerIcons)),
            finalizer: new HarmonyMethod(typeof(Parts), nameof(NormalIcons)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(GuiElementTextButton), "BeforeCalcBounds"),
            postfix: new HarmonyMethod(typeof(Parts), nameof(ResizeButton)));

        // The hotbar places its slot rows at fixed spots, so it keeps normal slots.
        Type hotbar = typeof(Vintagestory.Client.NoObf.ClientMain).Assembly.GetType("Vintagestory.Client.NoObf.HudHotbar")
            ?? throw new MissingMemberException("HudHotbar not found");
        harmony.Patch(AccessTools.DeclaredMethod(hotbar, "ComposeGuis"),
            prefix: new HarmonyMethod(typeof(Parts), nameof(HotbarStart)),
            finalizer: new HarmonyMethod(typeof(Parts), nameof(HotbarEnd)));
    }

    public static void Set(double titles, double buttons, double slots)
    {
        titleFactor = titles;
        buttonFactor = buttons;
        GuiElementPassiveItemSlot.unscaledSlotSize = slotSize * slots;
        GuiElementPassiveItemSlot.unscaledItemSize = itemSize * slots;
    }

    public static void Reset()
    {
        if (slotSize > 0) Set(1, 1, 1);
    }

    // Use "using" with this: normal slot sizes until the block ends.
    public static SlotScope NormalSlots(bool wanted)
    {
        var scope = new SlotScope(wanted && slotSize > 0,
            GuiElementPassiveItemSlot.unscaledSlotSize, GuiElementPassiveItemSlot.unscaledItemSize);
        if (scope.Changed)
        {
            GuiElementPassiveItemSlot.unscaledSlotSize = slotSize;
            GuiElementPassiveItemSlot.unscaledItemSize = itemSize;
        }
        return scope;
    }

    public readonly record struct SlotScope(bool Changed, double Slot, double Item) : IDisposable
    {
        public void Dispose()
        {
            if (!Changed) return;
            GuiElementPassiveItemSlot.unscaledSlotSize = Slot;
            GuiElementPassiveItemSlot.unscaledItemSize = Item;
        }
    }

    private static void HotbarStart(out SlotScope __state) => __state = NormalSlots(true);

    private static void HotbarEnd(SlotScope __state) => __state.Dispose();

    private static void BiggerIcons(out int __state)
    {
        __state = GuiElementDialogTitleBar.unscaledCloseIconSize;
        GuiElementDialogTitleBar.unscaledCloseIconSize = (int)Math.Round(__state * titleFactor);
    }

    private static void NormalIcons(int __state)
    {
        if (__state > 0) GuiElementDialogTitleBar.unscaledCloseIconSize = __state;
    }

    // Buttons are sized from their text plus padding, so we change the padding.
    private static void ResizeButton(GuiElementTextButton __instance, GuiElementStaticText ___normalText,
        ref GuiElementStaticText ___pressedText)
    {
        ElementBounds bounds = __instance.Bounds;
        var (x, y) = padding.GetValue(bounds, b => Tuple.Create(b.fixedPaddingX, b.fixedPaddingY));

        double extra = (buttonFactor - 1) * (bounds.fixedHeight + 2 * y) / 2;
        bounds.fixedPaddingX = ___normalText.Bounds.fixedPaddingX = Math.Max(0, x + extra);
        bounds.fixedPaddingY = ___normalText.Bounds.fixedPaddingY = Math.Max(0, y + extra);
        ___pressedText.Bounds = ___normalText.Bounds.CopyOnlySize();
    }
}
