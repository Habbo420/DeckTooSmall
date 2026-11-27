using System;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

namespace DeckTooSmall;

// Holds the settings and puts them into effect. The settings window, the chat
// commands and the hotkeys all make their changes through here.
//
// The mod's size is only used inside a world. Your own GUI Scale comes back when
// you leave (or switch the mod off), so the main menu is never affected.
internal sealed class UiSize
{
    private const string ConfigFile = "decktoosmall.json";
    private const string Setting = "guiScale";

    public const float Unit = 0.125f; // 1 on the game's slider
    private const float MainSmallest = Screens.Smallest * Unit, MainLargest = Screens.Largest * Unit;
    private const float WindowSmallest = 4 * Unit, WindowLargest = 36 * Unit;
    private const double TextSmallest = 0.6, TextLargest = 2; // compared with the main size

    // How far one press of - or + goes, in slider units.
    private const float MainStep = 0.5f, FineStep = 0.1f;

    public static float StepMain(float size, int direction) => Stepped(size, direction * MainStep);
    public static float StepFine(float size, int direction) => Stepped(size, direction * FineStep);

    // Rounds first, so tiny calculation errors never build up.
    private static float Stepped(float size, float units) => ((float)Math.Round(size / Unit, 1) + units) * Unit;

    // Rebuilding every menu takes a moment, so wait until presses stop.
    private const int WaitMilliseconds = 350;

    private readonly ICoreClientAPI capi;
    private bool active;
    private bool applying;
    private long pending; // the waiting "rebuild" timer, 0 if none
    private long screenCheck;
    private string screen = "";     // the screen size the current main size is for
    private string lastSeen = "";
    private readonly bool hadSettings;

    public DeckTooSmallConfig Config { get; }
    public bool Enabled => Config.Enabled;

    // Tells the settings window to update its numbers.
    public event Action? Changed;

    public UiSize(ICoreClientAPI capi)
    {
        this.capi = capi;
        try
        {
            DeckTooSmallConfig? saved = capi.LoadModConfig<DeckTooSmallConfig>(ConfigFile);
            hadSettings = saved != null;
            Config = saved ?? new DeckTooSmallConfig();
        }
        catch (Exception e)
        {
            capi.Logger.Error("[DeckTooSmall] Could not read " + ConfigFile + ", using defaults: " + e.Message);
            Config = new DeckTooSmallConfig();
        }
        Config.UiScale = Math.Clamp(Config.UiScale, MainSmallest, MainLargest);
    }

    public void Start()
    {
        // If the game crashed last time, the game's setting may still hold the mod's
        // size. In that case the value remembered then is the real one.
        float current = capi.Settings.Float[Setting];
        bool leftOver = Config.GameScale != null && Math.Abs(current - Config.UiScale) < 0.001f;
        if (!leftOver) Config.GameScale = Enabled ? current : null;

        // Settings from an earlier version: keep your size for this screen.
        screen = lastSeen = ScreenName();
        if (hadSettings && Config.ScreenSizes.Count == 0) Config.ScreenSizes[screen] = Config.UiScale;
        UseSizeForScreen();

        active = true;
        // Notices when the screen changes, for example when docking.
        screenCheck = capi.Event.RegisterGameTickListener(_ => CheckScreen(), 1000);
        capi.Settings.AddWatcher<float>(Setting, OnSliderMoved);
        capi.Event.LeaveWorld += Restore;
        ApplyNow();
    }

    // ---- Screens ----

    public string ScreenName() => capi.Render.FrameWidth + "x" + capi.Render.FrameHeight;

    // Your saved size for this screen, or else the recommended one.
    private void UseSizeForScreen()
    {
        if (capi.Render.FrameWidth <= 0 || capi.Render.FrameHeight <= 0) return;
        Config.UiScale = Config.ScreenSizes.TryGetValue(screen, out float saved)
            ? Math.Clamp(saved, MainSmallest, MainLargest)
            : Screens.Recommended(capi.Render.FrameWidth, capi.Render.FrameHeight) * Unit;
    }

    private void CheckScreen()
    {
        if (capi.Render.FrameWidth <= 0 || capi.Render.FrameHeight <= 0) return; // minimised

        // Wait until the size has stopped changing (a window being dragged).
        string now = ScreenName();
        bool settled = now == lastSeen;
        lastSeen = now;
        if (!settled || now == screen) return;

        screen = now;
        UseSizeForScreen();
        Schedule();
    }

    public void UsePreset(string key)
    {
        var preset = Screens.Presets.First(p => p.Key == key);
        // "Recommended" forgets your own choice, so the mod picks again.
        if (key == "recommended")
        {
            Config.ScreenSizes.Remove(screen);
            UseSizeForScreen();
            Schedule();
        }
        else
        {
            SetMain(Screens.Recommended(capi.Render.FrameWidth, capi.Render.FrameHeight, preset.Share) * Unit);
        }
    }

    // ---- Changes ----

    public void SetEnabled(bool on)
    {
        Config.Enabled = on;
        Schedule();
    }

    public void SetMain(float scale)
    {
        Config.UiScale = Math.Clamp(scale, MainSmallest, MainLargest);
        Config.ScreenSizes[screen] = Config.UiScale;
        Schedule();
    }

    public void SetText(float scale)
    {
        float main = Config.UiScale;
        Config.Text.Set(main, Math.Clamp(scale, main * (float)TextSmallest, main * (float)TextLargest));
        Schedule();
    }

    public void SetTextKeep(bool keep)
    {
        Config.Text.SetKeep(Config.UiScale, keep);
        Save();
        Changed?.Invoke();
    }

    // Window sizes only change how windows are shown, so they apply at once.
    public void SetWindow(string key, float scale)
    {
        Config.Window(key).Set(Config.UiScale, Math.Clamp(scale, WindowSmallest, WindowLargest));
        Save();
        Changed?.Invoke();
    }

    public void SetWindowKeep(string key, bool keep)
    {
        Config.Window(key).SetKeep(Config.UiScale, keep);
        Save();
        Changed?.Invoke();
    }

    // Parts are stored like other sizes, but can only differ so far from the main size.
    public void SetPart(string key, float scale)
    {
        var part = Parts.All.First(p => p.Key == key);
        float main = Config.UiScale;
        Config.Part(key).Set(main, Math.Clamp(scale, main * (float)part.Smallest, main * (float)part.Largest));
        Schedule();
    }

    public void SetPartKeep(string key, bool keep)
    {
        Config.Part(key).SetKeep(Config.UiScale, keep);
        Save();
        Changed?.Invoke();
    }

    // The size a part really has now, in the same units as the main size.
    public float PartSize(string key) => Config.UiScale * (float)PartFactor(key);

    private double PartFactor(string key)
    {
        var part = Parts.All.First(p => p.Key == key);
        return Math.Clamp(Config.Part(key).Actual(Config.UiScale) / Config.UiScale, part.Smallest, part.Largest);
    }

    // Puts all window and text sizes back to "follow the main size".
    public void Reset()
    {
        Config.Windows.Clear();
        Config.PartSizes.Clear();
        Config.Text = new SizeRule();
        Schedule();
    }

    // How much a window must be enlarged or shrunk on screen to have its chosen size.
    // This compares with the size the menus are built at right now, which lags a
    // moment behind Config.UiScale after F7/F8. Using Config.UiScale here made
    // kept windows jump for that moment.
    public double Zoom(string key) => Config.Window(key).Actual(Config.UiScale) / RuntimeEnv.GUIScale;

    // ---- Putting changes into effect ----

    private void Schedule()
    {
        Changed?.Invoke();
        CancelPending();
        // "true": also run while the game is paused.
        pending = capi.Event.RegisterCallback(_ =>
        {
            pending = 0;
            if (active) ApplyNow();
        }, WaitMilliseconds, true);
    }

    private void CancelPending()
    {
        if (pending != 0) capi.Event.UnregisterCallback(pending);
        pending = 0;
    }

    private void ApplyNow()
    {
        TextSize.Factor = Enabled
            ? Math.Clamp(Config.Text.Actual(Config.UiScale) / Config.UiScale, TextSmallest, TextLargest)
            : 1;
        if (Enabled) Parts.Set(PartFactor("titlebars"), PartFactor("buttons"), PartFactor("slots"));
        else Parts.Reset();

        if (Enabled)
        {
            Config.GameScale ??= capi.Settings.Float[Setting];
            UseScale(Config.UiScale);
        }
        else if (Config.GameScale is float own)
        {
            UseScale(own);
            Config.GameScale = null;
        }

        Save();
        // Redraw every menu, so a new text size shows too.
        ((ClientMain)capi.World).GuiComposers.MarkAllDialogsForRecompose();
        RebuildCharacterScreen();
        Changed?.Invoke();
    }

    // Changing the setting this way makes the game rebuild its menus right away.
    private void UseScale(float scale)
    {
        applying = true;
        try
        {
            capi.Settings.Float[Setting] = scale;
            RuntimeEnv.GUIScale = scale;
        }
        finally
        {
            applying = false;
        }
    }

    // The character screen places its side panels next to the main one only when
    // it is first built, so build it afresh after a size change.
    private void RebuildCharacterScreen()
    {
        foreach (GuiDialog dialog in capi.Gui.OpenedGuis.ToArray())
        {
            if (dialog is GuiDialogCharacterBase)
            {
                AccessTools.Method(dialog.GetType(), "ComposeGuis")?.Invoke(dialog, null);
            }
        }
    }

    // While the mod is on, moving the game's slider changes the mod's size instead.
    private void OnSliderMoved(float value)
    {
        if (!active || applying || !Enabled) return;
        Config.UiScale = Math.Clamp(value, MainSmallest, MainLargest);
        Config.ScreenSizes[screen] = Config.UiScale;
        Save();
        Changed?.Invoke();
    }

    public void Restore()
    {
        if (!active) return;
        active = false;
        CancelPending();
        capi.Event.LeaveWorld -= Restore;
        capi.Event.UnregisterGameTickListener(screenCheck);
        TextSize.Factor = 1;
        Parts.Reset();

        if (Config.GameScale is float own)
        {
            // "false": don't rebuild menus, the world is closing.
            capi.Settings.Float.Set(Setting, own, false);
            RuntimeEnv.GUIScale = own;
            Config.GameScale = null;
        }
        Save();
    }

    private void Save() => capi.StoreModConfig(Config, ConfigFile);
}
