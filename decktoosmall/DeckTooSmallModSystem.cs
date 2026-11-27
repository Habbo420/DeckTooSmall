using System;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace DeckTooSmall;

public class DeckTooSmallModSystem : ModSystem
{
    private Harmony? harmony;
    private UiSize? uiSize;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        harmony = new Harmony(Mod.Info.ModID);
        try
        {
            var ui = new UiSize(api);
            DialogFit.Apply(harmony, api);
            TextSize.Apply(harmony);
            Parts.Apply(harmony);
            DialogFit.Ui = ui;

            // Only change the size once the windows are sure to fit the screen.
            uiSize = ui;
            ui.Start();
            AddControls(api, ui);
        }
        catch (Exception e)
        {
            // A game update can rename things. Undo everything rather than half-work.
            uiSize?.Restore();
            Parts.Reset();
            DialogFit.Ui = null;
            harmony.UnpatchAll(harmony.Id);
            api.Logger.Error("[DeckTooSmall] Could not start, so the menus are left unchanged: " + e);
        }
    }

    // Hotkeys, the settings window and the chat command.
    private static void AddControls(ICoreClientAPI api, UiSize ui)
    {
        var window = new SettingsWindow(api, ui);
        api.Gui.RegisterDialog(window);
        Commands.Register(api, ui, window);

        // Single keys, easy to put on controller buttons. Change them in Settings > Controls.
        void Hotkey(string code, string name, GlKeys key, Action action)
        {
            api.Input.RegisterHotKey(code, name, key, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler(code, _ =>
            {
                action();
                return true;
            });
        }

        Hotkey("decktoosmall-bigger", "DeckTooSmall: bigger UI", GlKeys.F8, () =>
        {
            if (ui.Enabled) ui.SetMain(UiSize.StepMain(ui.Config.UiScale, 1));
        });
        Hotkey("decktoosmall-smaller", "DeckTooSmall: smaller UI", GlKeys.F7, () =>
        {
            if (ui.Enabled) ui.SetMain(UiSize.StepMain(ui.Config.UiScale, -1));
        });
        Hotkey("decktoosmall-settings", "DeckTooSmall: settings", GlKeys.F9, () =>
        {
            if (window.IsOpened()) window.TryClose();
            else window.TryOpen();
        });
    }

    public override void Dispose()
    {
        uiSize?.Restore();
        uiSize = null;
        DialogFit.Ui = null;
        harmony?.UnpatchAll(harmony.Id);
        harmony = null;
    }
}
