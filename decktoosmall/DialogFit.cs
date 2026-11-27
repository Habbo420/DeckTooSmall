using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

namespace DeckTooSmall;

// Lines windows up from the top left at their chosen sizes, shrinking any that
// would not fit.
//
// The game still builds each window at your GUI Scale. We only change how it is
// shown ("zoom out"), in three places:
//   1. Drawing:  the window is drawn smaller.
//   2. Clipping: the cut-off edges of scrolling lists are shrunk to match.
//   3. Mouse:    the pointer position is converted back, so clicks land correctly.
//
// All windows are drawn and sent input by one part of the game, GuiManager.
// We redirect its calls to the "...Fitted" methods below.
internal static class DialogFit
{
    private static ICoreClientAPI capi = null!;

    // The settings. Nothing is changed while this is missing or the mod is off.
    public static UiSize? Ui;
    private static bool On => Ui is { Enabled: true };

    // Which of the settings window's rows a window belongs to.
    private static string? GroupOf(GuiDialog dialog)
    {
        if (dialog is SettingsWindow) return null; // always at the main size
        if (dialog is GuiDialogCharacterBase) return "character";
        string name = dialog.GetType().Name;
        if (name is "HudHotbar" or "HudStatbar") return "hotbar";
        if (name == "GuiDialogInventory") return "inventory";
        if (name.Contains("Handbook")) return "handbook";
        if (name == "GuiDialogWorldMap") return "map";
        if (name.Contains("BlockEntity") || name.Contains("CreatureContents")) return "chests";
        return "other";
    }

    // The extra size chosen for this window, 1 = same as the main size.
    private static double ZoomOf(GuiDialog dialog) =>
        GroupOf(dialog) is { } group ? Ui!.Zoom(group) : 1;

    // The shrink being applied right now. None while no shrunk window is busy.
    private static FitTransform active = FitTransform.None;

    private static AccessTools.FieldRef<GuiComposer, Dictionary<string, GuiElement>> elements = null!;
    private static AccessTools.FieldRef<GuiComposer, Dictionary<string, GuiElement>> fixedElements = null!;
    private static readonly Dictionary<string, double> logged = new();
    private static readonly List<GuiDialog> openOrder = new();
    private static int redirected;

    public static void Apply(Harmony harmony, ICoreClientAPI api)
    {
        capi = api;
        active = FitTransform.None;
        logged.Clear();
        openOrder.Clear();
        elements = AccessTools.FieldRefAccess<GuiComposer, Dictionary<string, GuiElement>>("interactiveElements");
        fixedElements = AccessTools.FieldRefAccess<GuiComposer, Dictionary<string, GuiElement>>("staticElements");

        Type manager = Find(typeof(ClientMain).Assembly.GetType("Vintagestory.Client.NoObf.GuiManager"), "GuiManager");
        foreach (string name in new[] { "OnRenderFrameGUI", "OnFinalizeFrame", "OnKeyDown", "OnKeyUp",
                     "OnKeyPress", "OnMouseDown", "OnMouseUp", "OnMouseMove", "OnMouseWheel" })
        {
            redirected = 0;
            harmony.Patch(Find(AccessTools.DeclaredMethod(manager, name), name),
                transpiler: new HarmonyMethod(typeof(DialogFit), nameof(Redirect)));
            if (redirected == 0) throw new MissingMethodException("Nothing to redirect in GuiManager." + name);
        }

        Type platform = Find(typeof(ClientMain).Assembly.GetType("Vintagestory.Client.NoObf.ClientPlatformWindows"), "platform");
        harmony.Patch(Find(AccessTools.DeclaredMethod(platform, "GlScissor"), "GlScissor"),
            prefix: new HarmonyMethod(typeof(DialogFit), nameof(ShrinkClipping)));
    }

    private static T Find<T>(T? thing, string name) where T : class
    {
        return thing ?? throw new MissingMemberException("Not found in the game: " + name);
    }

    // Rewrites GuiManager's compiled code: "ask the window to do X" becomes
    // "call our XFitted method", which shrinks and then asks the window.
    private static IEnumerable<CodeInstruction> Redirect(IEnumerable<CodeInstruction> code)
    {
        foreach (CodeInstruction line in code)
        {
            if ((line.opcode == OpCodes.Callvirt || line.opcode == OpCodes.Call) && line.operand is MethodInfo called)
            {
                string? replacement = null;
                if (called.DeclaringType == typeof(GuiDialog))
                {
                    replacement = called.Name + "Fitted";
                }
                else if (called.DeclaringType == typeof(ElementBounds) && called.Name == "PointInside")
                {
                    replacement = nameof(PointInsideFitted);
                }

                MethodInfo? ours = replacement == null ? null : AccessTools.DeclaredMethod(typeof(DialogFit), replacement);
                if (ours != null)
                {
                    line.opcode = OpCodes.Call;
                    line.operand = ours;
                    redirected++;
                }
            }
            yield return line;
        }
    }

    // The area a window covers on screen before we change anything.
    private static (double Left, double Top, double Width, double Height)? Area(GuiDialog dialog)
    {
        double left = double.MaxValue, top = double.MaxValue;
        double right = double.MinValue, bottom = double.MinValue;

        void Include(ElementBounds? b)
        {
            if (b == null || b.OuterWidth <= 0 || b.OuterHeight <= 0) return;
            left = Math.Min(left, b.renderX);
            top = Math.Min(top, b.renderY);
            right = Math.Max(right, b.renderX + b.OuterWidth);
            bottom = Math.Max(bottom, b.renderY + b.OuterHeight);
        }

        // A window can be made of several parts (the character screen has several).
        foreach (GuiComposer composer in dialog.Composers.Values)
        {
            if (composer == null || !composer.Enabled) continue;
            Include(composer.Bounds);

            // Side tabs (as in the Handbook) stick out beyond the window itself.
            foreach (GuiElement element in elements(composer).Values)
            {
                if (element is GuiElementVerticalTabs) Include(element.Bounds);
            }
        }
        if (right <= left) return null;
        return (left, top, right - left, bottom - top);
    }

    // Other HUD parts (minimap, chat and so on) are left alone.
    private static bool IsWindow(GuiDialog dialog) =>
        dialog.DialogType == EnumDialogType.Dialog && dialog.IsOpened();

    // The tool picker pops up around the crosshair, so it should stay there.
    private static bool StaysPut(GuiDialog dialog) => dialog.GetType().Name == "GuiDialogToolMode";

    // On the character screen the game can place the Environment panel on top of
    // the bottom of the character panel. Move it below everything in that panel.
    private static void KeepEnvironmentBelow(GuiDialog dialog)
    {
        if (!On || dialog is not GuiDialogCharacterBase) return;
        GuiComposer? main = dialog.Composers["playercharacter"];
        GuiComposer? environment = dialog.Composers["environment"];
        if (main == null || environment == null) return;

        double bottom = main.Bounds.renderY + main.Bounds.OuterHeight;
        foreach (var parts in new[] { elements(main), fixedElements(main) })
        {
            foreach (GuiElement element in parts.Values)
            {
                bottom = Math.Max(bottom, element.Bounds.renderY + element.Bounds.OuterHeight);
            }
        }

        double overlap = bottom + 5 * RuntimeEnv.GUIScale - environment.Bounds.renderY;
        if (overlap < 1) return;

        environment.Bounds.fixedY += overlap / RuntimeEnv.GUIScale;
        environment.Bounds.MarkDirtyRecursive();
        environment.Bounds.CalcWorldBounds();
        capi.Logger.Notification("[DeckTooSmall] Moved the Environment panel down {0:0} pixels (character panel is {1:0} high, contents reach {2:0})",
            overlap, main.Bounds.OuterHeight, bottom - main.Bounds.renderY);
    }

    // The hotbar and the health/food bars above it.
    private static bool IsBottomBar(GuiDialog dialog) =>
        dialog.GetType().Name is "HudHotbar" or "HudStatbar";

    // The bottom bars are a fixed width. If that is wider than the screen they
    // shrink towards the bottom middle, so they stay lined up with each other.
    private static FitTransform BottomBars(double screenWidth, double screenHeight)
    {
        double width = 0;
        double zoom = Ui!.Zoom("hotbar");
        foreach (GuiDialog dialog in capi.Gui.OpenedGuis)
        {
            if (dialog.GetType().Name == "HudHotbar" && dialog.Composers["hotbar"] is { } bar)
            {
                width = bar.Bounds.OuterWidth;
            }
        }
        return FitTransform.BottomCentre(width, zoom, screenWidth, screenHeight);
    }

    // How far down windows may reach without covering the bottom bars.
    private static double SpaceAboveBars(double screenWidth, double screenHeight)
    {
        double top = screenHeight;
        foreach (GuiDialog dialog in capi.Gui.OpenedGuis)
        {
            if (!IsBottomBar(dialog)) continue;
            string part = dialog.GetType().Name == "HudHotbar" ? "hotbar" : "statbar";
            if (dialog.Composers[part] is not { } composer) continue;

            // The air bar sits a little above the other health bars.
            double extra = part == "statbar" ? 15 * RuntimeEnv.GUIScale : 0;
            top = Math.Min(top, composer.Bounds.renderY - extra);
        }
        FitTransform bars = BottomBars(screenWidth, screenHeight);
        return Math.Clamp(bars.Y + top * bars.Scale, screenHeight / 2, screenHeight);
    }

    // Works out how one window should be shrunk and moved.
    private static FitTransform Measure(GuiDialog dialog)
    {
        if (!On) return FitTransform.None;
        double screenWidth = capi.Render.FrameWidth;
        double screenHeight = capi.Render.FrameHeight;
        if (screenWidth <= 0 || screenHeight <= 0) return FitTransform.None;

        if (IsBottomBar(dialog)) return BottomBars(screenWidth, screenHeight);
        if (!IsWindow(dialog) || Area(dialog) is not { } own) return FitTransform.None;

        FitTransform fit;
        if (StaysPut(dialog))
        {
            fit = FitTransform.For(own.Left, own.Top, own.Width, own.Height, screenWidth, screenHeight);
        }
        else
        {
            // Keep our own list in the order windows were opened. The game's list
            // is reordered by clicking, which would make windows swap places.
            openOrder.RemoveAll(d => !IsWindow(d));
            foreach (GuiDialog open in capi.Gui.OpenedGuis)
            {
                if (IsWindow(open) && !StaysPut(open) && !openOrder.Contains(open)) openOrder.Add(open);
            }

            var areas = new List<(double, double, double, double, double)>();
            int mine = -1;
            foreach (GuiDialog open in openOrder)
            {
                if (Area(open) is not { } area) continue;
                if (open == dialog) mine = areas.Count;
                areas.Add((area.Left, area.Top, area.Width, area.Height, ZoomOf(open)));
            }
            if (mine < 0) return FitTransform.None;
            fit = FitTransform.Row(areas, screenWidth, SpaceAboveBars(screenWidth, screenHeight))[mine];
        }

        string name = dialog.GetType().Name;
        if (!logged.TryGetValue(name, out double last) || Math.Abs(last - fit.Scale) > 0.01)
        {
            logged[name] = fit.Scale;
            capi.Logger.Notification("[DeckTooSmall] {0}: {1:0}x{2:0} on a {3:0}x{4:0} screen, shown at {5:0}%",
                name, own.Width, own.Height, screenWidth, screenHeight, fit.Scale * 100);
        }
        return fit;
    }

    // Runs one window action with the mouse position converted to match its shrink.
    private static void WithFit(FitTransform fit, MouseEvent? mouse, Action action)
    {
        FitTransform previous = active;
        active = fit;
        if (fit == FitTransform.None)
        {
            try { action(); }
            finally { active = previous; }
            return;
        }

        ClientMain game = (ClientMain)capi.World;
        int realX = game.MouseCurrentX, realY = game.MouseCurrentY;
        int fakeX = fit.UnmapX(realX), fakeY = fit.UnmapY(realY);
        game.MouseCurrentX = fakeX;
        game.MouseCurrentY = fakeY;

        int eventX = 0, eventY = 0;
        if (mouse != null)
        {
            eventX = mouse.X;
            eventY = mouse.Y;
            mouse.X = fit.UnmapX(eventX);
            mouse.Y = fit.UnmapY(eventY);
        }

        try
        {
            action();
        }
        finally
        {
            // Put the real positions back, unless the game moved the mouse itself.
            if (game.MouseCurrentX == fakeX) game.MouseCurrentX = realX;
            if (game.MouseCurrentY == fakeY) game.MouseCurrentY = realY;
            if (mouse != null)
            {
                mouse.X = eventX;
                mouse.Y = eventY;
            }
            active = previous;
        }
    }

    // Same, but also draws smaller.
    private static void DrawWithFit(GuiDialog dialog, Action draw)
    {
        if (dialog.IsOpened()) KeepEnvironmentBelow(dialog);
        FitTransform fit = Measure(dialog);
        if (fit == FitTransform.None)
        {
            WithFit(fit, null, draw);
            return;
        }

        capi.Render.GlPushMatrix();
        try
        {
            capi.Render.GlTranslate(fit.X, fit.Y, 0);
            capi.Render.GlScale((float)fit.Scale, (float)fit.Scale, 1f);
            WithFit(fit, null, draw);
        }
        finally
        {
            capi.Render.GlPopMatrix();
        }
    }

    public static void OnRenderGUIFitted(GuiDialog d, float a)
    {
        using var slots = Parts.NormalSlots(IsBottomBar(d));
        DrawWithFit(d, () => d.OnRenderGUI(a));
    }

    public static void OnFinalizeFrameFitted(GuiDialog d, float a)
    {
        using var slots = Parts.NormalSlots(IsBottomBar(d));
        DrawWithFit(d, () => d.OnFinalizeFrame(a));
    }

    public static void OnKeyDownFitted(GuiDialog d, KeyEvent a)
    {
        using var slots = Parts.NormalSlots(IsBottomBar(d));
        WithFit(Measure(d), null, () => d.OnKeyDown(a));
    }

    public static void OnKeyUpFitted(GuiDialog d, KeyEvent a)
    {
        using var slots = Parts.NormalSlots(IsBottomBar(d));
        WithFit(Measure(d), null, () => d.OnKeyUp(a));
    }

    public static void OnKeyPressFitted(GuiDialog d, KeyEvent a)
    {
        using var slots = Parts.NormalSlots(IsBottomBar(d));
        WithFit(Measure(d), null, () => d.OnKeyPress(a));
    }

    public static void OnMouseDownFitted(GuiDialog d, MouseEvent a)
    {
        using var slots = Parts.NormalSlots(IsBottomBar(d));
        WithFit(Measure(d), a, () => d.OnMouseDown(a));
    }

    public static void OnMouseUpFitted(GuiDialog d, MouseEvent a)
    {
        using var slots = Parts.NormalSlots(IsBottomBar(d));
        WithFit(Measure(d), a, () => d.OnMouseUp(a));
    }

    public static void OnMouseMoveFitted(GuiDialog d, MouseEvent a)
    {
        using var slots = Parts.NormalSlots(IsBottomBar(d));
        WithFit(Measure(d), a, () => d.OnMouseMove(a));
    }

    public static void OnMouseWheelFitted(GuiDialog d, MouseWheelEventArgs a)
    {
        using var slots = Parts.NormalSlots(IsBottomBar(d));
        WithFit(Measure(d), null, () => d.OnMouseWheel(a));
    }

    // The game checks "is the mouse over this window?" when you scroll.
    public static bool PointInsideFitted(ElementBounds bounds, int x, int y)
    {
        foreach (GuiDialog dialog in capi.Gui.OpenedGuis)
        {
            foreach (GuiComposer composer in dialog.Composers.Values)
            {
                if (composer?.Bounds != bounds) continue;
                FitTransform fit = Measure(dialog);
                return bounds.PointInside(fit.UnmapX(x), fit.UnmapY(y));
            }
        }
        return bounds.PointInside(x, y);
    }

    // Scrolling lists cut off their contents at an exact screen rectangle, so that
    // rectangle has to shrink too. Note: here y is counted from the screen bottom.
    private static void ShrinkClipping(ref int x, ref int y, ref int width, ref int height)
    {
        if (active == FitTransform.None) return;

        int screenHeight = capi.Render.FrameHeight;
        double left = active.X + x * active.Scale;
        double top = active.Y + (screenHeight - y - height) * active.Scale;
        double right = left + width * active.Scale;
        double bottom = top + height * active.Scale;

        x = (int)Math.Floor(left);
        width = Math.Max(0, (int)Math.Ceiling(right) - x);
        y = screenHeight - (int)Math.Ceiling(bottom);
        height = Math.Max(0, (int)Math.Ceiling(bottom) - (int)Math.Floor(top));
    }
}
