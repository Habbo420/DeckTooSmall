using System;
using Vintagestory.API.Client;

namespace DeckTooSmall;

// The mod's own settings window, opened with F9 or the ".dts" chat command.
internal sealed class SettingsWindow : GuiDialog
{
    private const double RowHeight = 38;

    private readonly UiSize ui;

    public SettingsWindow(ICoreClientAPI capi, UiSize ui) : base(capi)
    {
        this.ui = ui;
        ui.Changed += Refresh;
    }

    // The hotkey is registered by the mod itself.
    public override string ToggleKeyCombinationCode => null!;

    public override bool PrefersUngrabbedMouse => true;

    public override void OnGuiOpened()
    {
        Build();
        base.OnGuiOpened();
    }

    private void Build()
    {
        ElementBounds background = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        background.BothSizing = ElementSizing.FitToChildren;
        ElementBounds window = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        CairoFont font = CairoFont.WhiteSmallText();
        const double Right = 450; // where the second column starts
        double x = 0, y = 30;

        // Every element goes inside "background", which grows to hold them.
        ElementBounds At(double left, double top, double width, double height = 28)
        {
            ElementBounds bounds = ElementBounds.Fixed(x + left, top, width, height);
            background.WithChild(bounds);
            return bounds;
        }

        GuiComposer composer = capi.Gui.CreateCompo("decktoosmall-settings", window)
            .AddShadedDialogBG(background)
            .AddDialogTitleBar("DeckTooSmall", () => TryClose())
            .BeginChildElements(background);

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
            composer.AddStaticText(label, font, At(0, y + 4, 190))
                .AddSmallButton("-", () => { change(-1); return true; }, At(195, y, 32))
                .AddDynamicText("", font.Clone().WithOrientation(EnumTextOrientation.Center), At(240, y + 4, 55), key)
                .AddSmallButton("+", () => { change(1); return true; }, At(308, y, 32));
            if (keep != null) composer.AddSwitch(keep, At(365, y, 30), key + "-keep");
            y += RowHeight;
        }

        // First column: general settings.
        composer.AddStaticText("Mod enabled", font, At(0, y + 4, 220))
            .AddSwitch(on => ui.SetEnabled(on), At(240, y, 30), "enabled");
        y += RowHeight;

        composer.AddDynamicText("", font, At(0, y + 4, 400), "screen");
        y += RowHeight - 6;
        for (int i = 0; i < Screens.Presets.Length; i++)
        {
            var preset = Screens.Presets[i];
            composer.AddSmallButton(preset.Label, () => { ui.UsePreset(preset.Key); return true; }, At(i * 135, y, 110));
        }
        y += RowHeight + 8;

        Heading("");
        Row("main", "All menus", d => ui.SetMain(UiSize.StepMain(ui.Config.UiScale, d)), null);
        Row("text", "Text",
            d => ui.SetText(UiSize.StepFine(ui.Config.Text.Actual(ui.Config.UiScale), d)),
            on => ui.SetTextKeep(on));

        y += 8;
        composer.AddSmallButton("Reset sizes", () => { ui.Reset(); return true; }, At(0, y, 130))
            .AddSmallButton("Close", () => TryClose(), At(270, y, 100));

        // Second column: windows, then parts of windows.
        x = Right;
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

        float main = ui.Config.UiScale;
        void Show(string key, float scale) =>
            SingleComposer.GetDynamicText(key).SetNewText((scale / UiSize.Unit).ToString("0.#"));

        SingleComposer.GetSwitch("enabled").SetValue(ui.Enabled);
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
