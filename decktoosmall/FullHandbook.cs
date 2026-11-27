using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace DeckTooSmall;

// Makes the Handbook fill the screen: a wider and taller list and page, so you
// see more at once. The game has the width (500) written into the code that
// builds the Handbook, so we swap that number for one that suits the screen.
internal static class FullHandbook
{
    private const double GameSize = 500; // the game's own width and list height

    private static ICoreClientAPI capi = null!;
    private static double width = GameSize;
    private static bool rebuilding;

    // How much bigger the whole window is than its list, in layout units. Starts
    // as an estimate and is corrected after measuring. One entry per view.
    private static readonly Dictionary<string, (double Width, double Height)> border = new();

    // Room kept on the left for the category tabs, in screen pixels.
    public static double TabRoom { get; private set; }

    private static AccessTools.FieldRef<GuiDialogHandbook, double> listHeight = null!;
    private static AccessTools.FieldRef<GuiDialogHandbook, GuiComposer> listGui = null!;
    private static AccessTools.FieldRef<GuiDialogHandbook, GuiComposer> pageGui = null!;

    public static bool Wanted => DialogFit.Ui is { Enabled: true } ui && ui.Config.FullScreenHandbook;

    public static void Apply(Harmony harmony, ICoreClientAPI api)
    {
        capi = api;
        rebuilding = false;
        border.Clear();
        listHeight = AccessTools.FieldRefAccess<GuiDialogHandbook, double>("listHeight");
        listGui = AccessTools.FieldRefAccess<GuiDialogHandbook, GuiComposer>("overviewGui");
        pageGui = AccessTools.FieldRefAccess<GuiDialogHandbook, GuiComposer>("detailViewGui");

        foreach (var (method, after) in new[] { ("initOverviewGui", nameof(CheckList)), ("initDetailGui", nameof(CheckPage)) })
        {
            MethodInfo target = AccessTools.DeclaredMethod(typeof(GuiDialogHandbook), method)
                ?? throw new MissingMethodException("Handbook method not found: " + method);
            harmony.Patch(target,
                prefix: new HarmonyMethod(typeof(FullHandbook), after == nameof(CheckList) ? nameof(SizeList) : nameof(SizePage)),
                postfix: new HarmonyMethod(typeof(FullHandbook), after),
                transpiler: new HarmonyMethod(typeof(FullHandbook), nameof(SwapWidth)));
        }
    }

    public static double Width() => width;

    // Replaces the number 500 in the game's code with a call to Width().
    private static IEnumerable<CodeInstruction> SwapWidth(IEnumerable<CodeInstruction> code)
    {
        int swapped = 0;
        foreach (CodeInstruction line in code)
        {
            if (line.opcode == OpCodes.Ldc_R8 && line.operand is double number && number == GameSize)
            {
                line.opcode = OpCodes.Call;
                line.operand = AccessTools.Method(typeof(FullHandbook), nameof(Width));
                swapped++;
            }
            yield return line;
        }
        if (swapped != 1) throw new InvalidOperationException("Handbook width found " + swapped + " times, expected once");
    }

    private static void SizeList(GuiDialogHandbook __instance) => Size(__instance, "list");

    private static void SizePage(GuiDialogHandbook __instance) => Size(__instance, "page");

    private static void Size(GuiDialogHandbook handbook, string view)
    {
        double scale = RuntimeEnv.GUIScale;
        if (!Wanted || capi.Render.FrameWidth <= 0 || capi.Render.FrameHeight <= 0)
        {
            width = listHeight(handbook) = GameSize;
            return;
        }

        TabRoom = MeasureTabs(handbook, scale);
        if (!border.TryGetValue(view, out var extra)) extra = (75, 190);
        width = Math.Max(300, Math.Floor((capi.Render.FrameWidth - TabRoom) / scale - extra.Width));
        listHeight(handbook) = Math.Max(150, Math.Floor(capi.Render.FrameHeight / scale - extra.Height));
    }

    // The tabs are as wide as their longest name.
    private static double MeasureTabs(GuiDialogHandbook handbook, double scale)
    {
        var arguments = new object?[] { 0 };
        var tabs = (GuiTab[])AccessTools.Method(handbook.GetType(), "genTabs").Invoke(handbook, arguments)!;
        CairoFont font = CairoFont.WhiteDetailText().WithFontSize(17f);
        double widest = 0;
        foreach (GuiTab tab in tabs) widest = Math.Max(widest, font.GetTextExtents(tab.Name).Width);
        return tabs.Length == 0 ? 0 : Math.Ceiling(widest + 8 * scale);
    }

    private static void CheckList(GuiDialogHandbook __instance) => Check(__instance, "list", listGui(__instance), "initOverviewGui");

    private static void CheckPage(GuiDialogHandbook __instance) => Check(__instance, "page", pageGui(__instance), "initDetailGui");

    // Measures what was built. If the estimate was off, builds it once more.
    private static void Check(GuiDialogHandbook handbook, string view, GuiComposer? gui, string rebuild)
    {
        if (rebuilding || !Wanted || gui == null) return;

        double scale = RuntimeEnv.GUIScale;
        var measured = (Width: gui.Bounds.OuterWidth / scale - width,
            Height: gui.Bounds.OuterHeight / scale - listHeight(handbook));
        if (!border.TryGetValue(view, out var used)) used = (75, 190);
        if (Math.Abs(measured.Width - used.Width) < 2 && Math.Abs(measured.Height - used.Height) < 2) return;

        border[view] = measured;
        rebuilding = true;
        try
        {
            AccessTools.Method(typeof(GuiDialogHandbook), rebuild).Invoke(handbook, null);
        }
        finally
        {
            rebuilding = false;
        }
    }
}
