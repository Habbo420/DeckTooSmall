using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace DeckTooSmall;

// Makes the world map fill the whole screen. Instead of zooming the window, we
// give the game a bigger map size before it builds it, so you see more map.
internal static class FullMap
{
    private static ICoreClientAPI capi = null!;

    // Room kept on the left for the layer tabs, in screen pixels.
    public static double TabRoom { get; private set; }

    public static void Apply(Harmony harmony, ICoreClientAPI api)
    {
        capi = api;
        var compose = AccessTools.DeclaredMethod(typeof(GuiDialogWorldMap), "ComposeDialog")
            ?? throw new MissingMethodException("World map ComposeDialog not found");
        harmony.Patch(compose,
            prefix: new HarmonyMethod(typeof(FullMap), nameof(Enlarge)),
            finalizer: new HarmonyMethod(typeof(FullMap), nameof(PutBack)));
    }

    public static bool Wanted => DialogFit.Ui is { Enabled: true } ui && ui.Config.FullScreenMap;

    private static void Enlarge(EnumDialogType dlgType, ref int ___mapWidth, ref int ___mapHeight,
        List<string> ___tabnames, out (int Width, int Height)? __state)
    {
        __state = null;
        double scale = RuntimeEnv.GUIScale;
        int screenWidth = capi.Render.FrameWidth, screenHeight = capi.Render.FrameHeight;
        if (dlgType != EnumDialogType.Dialog || !Wanted || screenWidth <= 0 || screenHeight <= 0) return;

        // The tabs are as wide as their longest name.
        CairoFont font = CairoFont.WhiteDetailText().WithFontSize(17f);
        double widest = 0;
        foreach (string name in ___tabnames)
        {
            widest = Math.Max(widest, font.GetTextExtents(Lang.Get("maplayer-" + name)).Width);
        }
        TabRoom = Math.Ceiling(widest + 8 * scale);

        __state = (___mapWidth, ___mapHeight);
        // The window adds a border (and a thin right column) around the map, and
        // a title bar above it.
        ___mapWidth = (int)((screenWidth - TabRoom) / scale) - 7;
        ___mapHeight = (int)(screenHeight / scale) - 34;
    }

    // Leave the game's own numbers as they were.
    private static void PutBack((int Width, int Height)? __state, ref int ___mapWidth, ref int ___mapHeight)
    {
        if (__state is not { } before) return;
        ___mapWidth = before.Width;
        ___mapHeight = before.Height;
    }
}
