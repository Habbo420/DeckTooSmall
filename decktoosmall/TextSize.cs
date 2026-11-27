using HarmonyLib;
using Vintagestory.API.Client;

namespace DeckTooSmall;

// Makes text bigger or smaller. The game sets up every piece of text through
// CairoFont.SetupContext, so we change the font size just for that moment.
internal static class TextSize
{
    public static double Factor = 1;

    public static void Apply(Harmony harmony)
    {
        Factor = 1;
        harmony.Patch(AccessTools.DeclaredMethod(typeof(CairoFont), "SetupContext"),
            prefix: new HarmonyMethod(typeof(TextSize), nameof(Enlarge)),
            finalizer: new HarmonyMethod(typeof(TextSize), nameof(PutBack)));
    }

    private static void Enlarge(CairoFont __instance, out double __state)
    {
        __state = __instance.UnscaledFontsize;
        __instance.UnscaledFontsize *= Factor;
    }

    private static void PutBack(CairoFont __instance, double __state)
    {
        if (__state > 0) __instance.UnscaledFontsize = __state;
    }
}
