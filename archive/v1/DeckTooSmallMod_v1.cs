// DeckTooSmall - a small client-side Vintage Story mod.
//
// This is a "source mod": the game compiles this file itself when it loads the
// mod, so you don't need any build tools. The game's built-in compiler only
// understands older C# (version 6), so keep the code simple if you edit it.

using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace DeckTooSmall
{
    // Settings saved to VintagestoryData/ModConfig/decktoosmall.json
    public class DeckTooSmallConfig
    {
        // 1.0 = normal size. 1.3 = 30% bigger text.
        public double TextScale = 1.3;

        // 1.0 = normal size. Bigger item slots can make some dialogs overlap,
        // so this starts switched off. Try 1.15 - 1.25 if you want it.
        public double SlotScale = 1.0;

        // Where windows go:
        //   "topleft" = every window is pinned to the top left corner of the screen
        //   "fit"     = windows stay where the game puts them, but are pushed back
        //               inside the screen if they stick out (top left wins if too big)
        //   "off"     = the game's normal behaviour
        public string WindowMode = "topleft";
    }

    public class DeckTooSmallMod : ModSystem
    {
        const string ConfigFileName = "decktoosmall.json";

        // Key used to remember the game's original sizes for the whole game session,
        // so the scale is never applied twice when you leave and rejoin a world.
        const string OriginalsKey = "decktoosmall.originalsizes";

        DeckTooSmallConfig config;
        ICoreAPI api;

        // Only run on the client (your Deck), never on a server.
        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Client;
        }

        // Load before the game's own mods so their menus are built with the new sizes.
        public override double ExecuteOrder()
        {
            return 0.0;
        }

        // StartPre runs very early, before any menus are built.
        public override void StartPre(ICoreAPI api)
        {
            this.api = api;
            LoadConfig();
            Apply();
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            // Type ".textscale 1.4" in chat to change text size in-game.
            capi.ChatCommands.Create("textscale")
                .WithDescription("Set menu text size. Example: .textscale 1.3 (1 = normal)")
                .WithArgs(capi.ChatCommands.Parsers.OptionalDouble("factor", 0))
                .HandleWith(OnTextScaleCommand);

            // Type ".slotscale 1.2" in chat to change item slot size in-game.
            capi.ChatCommands.Create("slotscale")
                .WithDescription("Set item slot size. Example: .slotscale 1.2 (1 = normal)")
                .WithArgs(capi.ChatCommands.Parsers.OptionalDouble("factor", 0))
                .HandleWith(OnSlotScaleCommand);

            // Type ".windowpos topleft", ".windowpos fit" or ".windowpos off" in chat.
            capi.ChatCommands.Create("windowpos")
                .WithDescription("Where windows go: topleft, fit or off")
                .WithArgs(capi.ChatCommands.Parsers.OptionalWord("mode"))
                .HandleWith(OnWindowPosCommand);

            // Runs every frame and moves open windows into place.
            pinner = new WindowPinner(capi, config);
            capi.Event.RegisterRenderer(pinner, EnumRenderStage.Ortho, "decktoosmall-windows");
        }

        WindowPinner pinner;

        TextCommandResult OnWindowPosCommand(TextCommandCallingArgs args)
        {
            string mode = args[0] as string;
            if (string.IsNullOrEmpty(mode))
            {
                return TextCommandResult.Success("Window mode is " + config.WindowMode + ". Options: topleft, fit, off");
            }

            mode = mode.ToLowerInvariant();
            if (mode != "topleft" && mode != "fit" && mode != "off")
            {
                return TextCommandResult.Error("Unknown mode '" + mode + "'. Options: topleft, fit, off");
            }

            config.WindowMode = mode;
            api.StoreModConfig(config, ConfigFileName);
            return TextCommandResult.Success("Window mode set to " + mode + ". For 'off', close and reopen windows to reset them.");
        }

        TextCommandResult OnTextScaleCommand(TextCommandCallingArgs args)
        {
            double factor = (double)args[0];
            if (factor <= 0)
            {
                return TextCommandResult.Success("Text scale is " + config.TextScale + ". Use e.g. .textscale 1.3");
            }

            config.TextScale = Clamp(factor);
            SaveAndApply();
            return TextCommandResult.Success("Text scale set to " + config.TextScale + ". Close and reopen menus to see it (rejoin the world if a menu doesn't change).");
        }

        TextCommandResult OnSlotScaleCommand(TextCommandCallingArgs args)
        {
            double factor = (double)args[0];
            if (factor <= 0)
            {
                return TextCommandResult.Success("Slot scale is " + config.SlotScale + ". Use e.g. .slotscale 1.2");
            }

            config.SlotScale = Clamp(factor);
            SaveAndApply();
            return TextCommandResult.Success("Slot scale set to " + config.SlotScale + ". Rejoin the world to see it everywhere.");
        }

        void LoadConfig()
        {
            try
            {
                config = api.LoadModConfig<DeckTooSmallConfig>(ConfigFileName);
            }
            catch (Exception e)
            {
                api.Logger.Error("[DeckTooSmall] Could not read " + ConfigFileName + ", using defaults. " + e.Message);
                config = null;
            }

            if (config == null) config = new DeckTooSmallConfig();

            config.TextScale = Clamp(config.TextScale);
            config.SlotScale = Clamp(config.SlotScale);
            if (string.IsNullOrEmpty(config.WindowMode)) config.WindowMode = "topleft";

            // Writes the file so you can find and edit it.
            api.StoreModConfig(config, ConfigFileName);
        }

        void SaveAndApply()
        {
            api.StoreModConfig(config, ConfigFileName);
            Apply();
        }

        // Keeps values sensible so a typo can't make the game unusable.
        static double Clamp(double value)
        {
            if (value < 0.5) return 0.5;
            if (value > 3.0) return 3.0;
            return value;
        }

        // Remembers the game's original sizes the first time this runs.
        static double[] GetOriginalSizes()
        {
            double[] sizes = AppDomain.CurrentDomain.GetData(OriginalsKey) as double[];
            if (sizes == null)
            {
                sizes = new double[]
                {
                    GuiStyle.DetailFontSize,                       // 0
                    GuiStyle.SmallFontSize,                        // 1
                    GuiStyle.SmallishFontSize,                     // 2
                    GuiStyle.SubNormalFontSize,                    // 3
                    GuiStyle.NormalFontSize,                       // 4
                    GuiStyle.LargeFontSize,                        // 5
                    GuiElementPassiveItemSlot.unscaledSlotSize,    // 6
                    GuiElementPassiveItemSlot.unscaledItemSize     // 7
                };
                AppDomain.CurrentDomain.SetData(OriginalsKey, sizes);
            }
            return sizes;
        }

        void Apply()
        {
            double[] o = GetOriginalSizes();
            double t = config.TextScale;
            double s = config.SlotScale;

            GuiStyle.DetailFontSize = o[0] * t;
            GuiStyle.SmallFontSize = o[1] * t;
            GuiStyle.SmallishFontSize = o[2] * t;
            GuiStyle.SubNormalFontSize = o[3] * t;
            GuiStyle.NormalFontSize = o[4] * t;
            GuiStyle.LargeFontSize = o[5] * t;

            GuiElementPassiveItemSlot.unscaledSlotSize = o[6] * s;
            GuiElementPassiveItemSlot.unscaledItemSize = o[7] * s;

            api.Logger.Notification("[DeckTooSmall] Text scale " + t + ", slot scale " + s);
        }

        // Put everything back when you leave a world, so the main menu looks normal.
        public override void Dispose()
        {
            if (pinner != null)
            {
                pinner.Capi.Event.UnregisterRenderer(pinner, EnumRenderStage.Ortho);
                pinner = null;
            }

            double[] o = AppDomain.CurrentDomain.GetData(OriginalsKey) as double[];
            if (o == null) return;

            GuiStyle.DetailFontSize = o[0];
            GuiStyle.SmallFontSize = o[1];
            GuiStyle.SmallishFontSize = o[2];
            GuiStyle.SubNormalFontSize = o[3];
            GuiStyle.NormalFontSize = o[4];
            GuiStyle.LargeFontSize = o[5];
            GuiElementPassiveItemSlot.unscaledSlotSize = o[6];
            GuiElementPassiveItemSlot.unscaledItemSize = o[7];
        }
    }

    // Moves open windows every frame so they sit where WindowMode says.
    // HUD elements (hotbar, minimap, health bars) are left alone.
    public class WindowPinner : IRenderer
    {
        public ICoreClientAPI Capi;
        DeckTooSmallConfig config;
        bool loggedError;

        public WindowPinner(ICoreClientAPI capi, DeckTooSmallConfig config)
        {
            Capi = capi;
            this.config = config;
        }

        // Run early in the 2D stage, before the windows are drawn.
        public double RenderOrder { get { return 0.0; } }
        public int RenderRange { get { return 0; } }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (config.WindowMode == "off") return;

            // Copy the list, because windows can open or close while we work.
            GuiDialog[] dialogs = Capi.Gui.OpenedGuis.ToArray();
            for (int i = 0; i < dialogs.Length; i++)
            {
                try
                {
                    PlaceDialog(dialogs[i]);
                }
                catch (Exception e)
                {
                    // Some windows are built in unusual ways. Skip them rather than crash,
                    // but write the first problem to the log so it can be fixed.
                    if (!loggedError)
                    {
                        loggedError = true;
                        Capi.Logger.Warning("[DeckTooSmall] Could not move a window: " + e);
                    }
                }
            }
        }

        void PlaceDialog(GuiDialog dialog)
        {
            if (dialog == null || dialog.DialogType != EnumDialogType.Dialog) return;

            // A window can be made of several parts (the character screen has three).
            // Find the box around all of them so they move together.
            List<ElementBounds> parts = new List<ElementBounds>();
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (GuiComposer composer in dialog.Composers.Values)
            {
                if (composer == null) continue;
                ElementBounds b = composer.Bounds;
                if (b == null || !b.Initialized) continue;

                parts.Add(b);
                minX = Math.Min(minX, b.absX);
                minY = Math.Min(minY, b.absY);
                maxX = Math.Max(maxX, b.absX + b.OuterWidth);
                maxY = Math.Max(maxY, b.absY + b.OuterHeight);
            }
            if (parts.Count == 0) return;

            double screenW = Capi.Render.FrameWidth;
            double screenH = Capi.Render.FrameHeight;
            double width = maxX - minX;
            double height = maxY - minY;

            double targetX, targetY;
            if (config.WindowMode == "fit")
            {
                // Keep the position, but pull it back inside the screen.
                targetX = minX;
                targetY = minY;
                if (targetX + width > screenW) targetX = screenW - width;
                if (targetY + height > screenH) targetY = screenH - height;
                if (targetX < 0) targetX = 0;
                if (targetY < 0) targetY = 0;
            }
            else
            {
                // "topleft": pin to the corner.
                targetX = 0;
                targetY = 0;
            }

            double dx = targetX - minX;
            double dy = targetY - minY;
            if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5) return; // already in place

            // Offsets are stored before GUI scaling, so divide by the scale.
            double scale = RuntimeEnv.GUIScale;
            if (scale <= 0) scale = 1;

            for (int i = 0; i < parts.Count; i++)
            {
                ElementBounds b = parts[i];
                b.fixedOffsetX += dx / scale;
                b.fixedOffsetY += dy / scale;
                b.MarkDirtyRecursive();
                b.CalcWorldBounds();
            }
        }

        public void Dispose()
        {
        }
    }
}
