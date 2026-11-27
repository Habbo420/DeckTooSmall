using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace DeckTooSmall;

// The mod's own settings window, opened with F9 or the ".dts" chat command.
// When set to full screen, everything in it is sized from the screen, not from
// "All menus", so it always fits and stays easy to press. Otherwise it is an
// ordinary window.
internal sealed class SettingsWindow : GuiDialog
{
    private const double RowHeight = 40;
    // The space the layout needs before it is enlarged to fill the screen.
    private const double LayoutWidth = 850, LayoutHeight = 570;
    private string builtFor = ""; // screen and scale the window was built for

    private readonly UiSize ui;

    public SettingsWindow(ICoreClientAPI capi, UiSize ui) : base(capi)
    {
        this.ui = ui;
        ui.Changed += Refresh;
    }

    // The hotkey is registered by the mod itself.
    public override string ToggleKeyCombinationCode => null!;

    public override bool PrefersUngrabbedMouse => true;

    // Drawn above other windows.
    public override double DrawOrder => 0.9;

    public bool FullScreen => ui.Config.FullScreenSettings;

    // What the layout depends on. A change means the window must be built again.
    private string LayoutKey() => ui.ScreenName() + "@" + RuntimeEnv.GUIScale + " " + FullScreen;

    public override void OnGuiOpened()
    {
        Build();
        base.OnGuiOpened();
    }

    private void Build()
    {
        builtFor = LayoutKey();

        // The screen, measured in the units the game lays windows out with.
        double padding = GuiStyle.ElementToDialogPadding;
        double innerWidth = capi.Render.FrameWidth / RuntimeEnv.GUIScale - 2 * padding;
        double innerHeight = capi.Render.FrameHeight / RuntimeEnv.GUIScale - 2 * padding;
        // How much to enlarge the layout.
        double k = FullScreen ? Math.Min(innerWidth / LayoutWidth, innerHeight / LayoutHeight) : 1;

        ElementBounds background;
        if (FullScreen)
        {
            background = ElementBounds.Fixed(0, 0, innerWidth, innerHeight).WithFixedPadding(padding);
        }
        else
        {
            background = ElementBounds.Fill.WithFixedPadding(padding);
            background.BothSizing = ElementSizing.FitToChildren; // just big enough
        }
        ElementBounds window = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        CairoFont font = CairoFont.WhiteSmallText();
        font.UnscaledFontsize *= k;
        // Where the second column starts: halfway across.
        double secondColumn = FullScreen ? Math.Max(450, innerWidth / k / 2) : 450;
        double x = 0, y = 30;

        // Every element goes inside "background", which grows to hold them.
        ElementBounds At(double left, double top, double width, double height = 28)
        {
            ElementBounds bounds = ElementBounds.Fixed((x + left) * k, top * k, width * k, height * k);
            background.WithChild(bounds);
            return bounds;
        }

        GuiComposer composer = capi.Gui.CreateCompo("decktoosmall-settings", window)
            .AddShadedDialogBG(background)
            .AddDialogTitleBar("DeckTooSmall", () => TryClose())
            .BeginChildElements(background);

        void Button(string label, Action press, ElementBounds bounds) =>
            composer.AddButton(label, () => { press(); return true; }, bounds, font.Clone().WithOrientation(EnumTextOrientation.Center), EnumButtonStyle.Small);

        void Switch(Action<bool> toggled, double left, double top, string key) =>
            composer.AddSwitch(toggled, At(left, top, 30), key, 30 * k);

        void Heading(string title)
        {
            composer.AddStaticText(title, font, At(0, y, 220))
                .AddStaticText("Size", font, At(235, y, 60))
                .AddStaticText("Keep", font, At(360, y, 60));
            y += 26;
        }

        // "change" is given -1 or +1.
        void Row(string key, string label, Action<int> change, Action<bool>? keep)
        {
            composer.AddStaticText(label, font, At(0, y + 4, 190));
            Button("-", () => change(-1), At(195, y, 36));
            composer.AddDynamicText("", font.Clone().WithOrientation(EnumTextOrientation.Center), At(240, y + 4, 55), key);
            Button("+", () => change(1), At(303, y, 36));
            if (keep != null) Switch(keep, 365, y, key + "-keep");
            y += RowHeight;
        }

        // First column: general settings.
        composer.AddStaticText("Mod enabled", font, At(0, y + 4, 230));
        Switch(on => ui.SetEnabled(on), 240, y, "enabled");
        y += RowHeight;
        composer.AddStaticText("Full screen:", font, At(0, y + 4, 230));
        y += 26;
        void FullRow(string label, Action<bool> toggled, string key)
        {
            composer.AddStaticText(label, font, At(20, y + 4, 210));
            Switch(toggled, 240, y, key);
            y += RowHeight;
        }
        FullRow("World map", on => ui.SetFullMap(on), "fullmap");
        FullRow("Handbook", on => ui.SetFullHandbook(on), "fullhandbook");
        FullRow("This window", on => ui.SetFullSettings(on), "fullsettings");

        composer.AddDynamicText("", font, At(0, y + 4, 400), "screen");
        y += RowHeight - 6;
        // One under the other: a button's width depends on its text and your size
        // settings, so side by side they could run into each other.
        foreach (var preset in Screens.Presets)
        {
            Button(preset.Label, () => ui.UsePreset(preset.Key), At(0, y, 180));
            y += RowHeight;
        }
        y += 8;

        Heading("");
        Row("main", "All menus", d => ui.SetMain(UiSize.StepMain(ui.Config.UiScale, d)), null);
        Row("text", "Text",
            d => ui.SetText(UiSize.StepFine(ui.Config.Text.Actual(ui.Config.UiScale), d)),
            on => ui.SetTextKeep(on));

        y += 8;
        Button("Reset sizes", () => ui.Reset(), At(0, y, 150));
        Button("Close", () => TryClose(), At(240, y, 120));

        // Second column: windows, then parts of windows.
        x = secondColumn;
        y = 30;
        Heading("Windows");
        foreach ((string key, string label) in Group.All)
        {
            Row(key, label,
                d => ui.SetWindow(key, UiSize.StepFine(ui.Config.Window(key).Actual(ui.Config.UiScale), d)),
                on => ui.SetWindowKeep(key, on));
        }

        y += 4;
        composer.AddStaticText("Parts (reopen windows to see changes)", font, At(0, y, 350));
        y += 26;
        foreach (var part in Parts.All)
        {
            string key = part.Key;
            Row(key, part.Label,
                d => ui.SetPart(key, UiSize.StepFine(ui.PartSize(key), d)),
                on => ui.SetPartKeep(key, on));
        }

        SingleComposer = composer.EndChildElements().Compose();
        Refresh();
    }

    // Shows the current values.
    private void Refresh()
    {
        if (SingleComposer == null || !IsOpened() && !SingleComposer.Composed) return;

        // After a change that affects the layout, build the window again. Not right
        // now: we may be in the middle of handling a click on this very window.
        if (IsOpened() && builtFor != LayoutKey())
        {
            capi.Event.RegisterCallback(_ =>
            {
                if (IsOpened() && builtFor != LayoutKey()) Build();
            }, 50, true);
        }

        float main = ui.Config.UiScale;
        void Show(string key, float scale) =>
            SingleComposer.GetDynamicText(key).SetNewText((scale / UiSize.Unit).ToString("0.#"));

        SingleComposer.GetSwitch("enabled").SetValue(ui.Enabled);
        SingleComposer.GetSwitch("fullmap").SetValue(ui.Config.FullScreenMap);
        SingleComposer.GetSwitch("fullhandbook").SetValue(ui.Config.FullScreenHandbook);
        SingleComposer.GetSwitch("fullsettings").SetValue(FullScreen);
        SingleComposer.GetDynamicText("screen").SetNewText("Your screen: " + ui.ScreenName() + ". Presets:");
        Show("main", main);
        Show("text", ui.Config.Text.Actual(main));
        SingleComposer.GetSwitch("text-keep").SetValue(ui.Config.Text.Keep);
        foreach ((string key, _) in Group.All)
        {
            Show(key, ui.Config.Window(key).Actual(main));
            SingleComposer.GetSwitch(key + "-keep").SetValue(ui.Config.Window(key).Keep);
        }
        foreach (var part in Parts.All)
        {
            Show(part.Key, ui.PartSize(part.Key));
            SingleComposer.GetSwitch(part.Key + "-keep").SetValue(ui.Config.Part(part.Key).Keep);
        }
    }
}
