using System;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace DeckTooSmall;

// Everything in the settings window can also be typed in chat as ".dts ...".
internal static class Commands
{
    private const string Help =
        "DeckTooSmall commands (sizes are like the GUI Scale slider, 8 = normal):\n" +
        ".dts  (open the settings window)\n" +
        ".dts on / off\n" +
        ".dts size 10  (all menus)\n" +
        ".dts preset compact / recommended / large\n" +
        ".dts hotbar 12\n" +
        ".dts hotbar keep  (stay at that size)\n" +
        ".dts hotbar follow  (change with the main size again)\n" +
        ".dts status / reset / help\n" +
        "Windows: text, hotbar, inventory, character, handbook, chests, map, other\n" +
        "Parts: titlebars, buttons, slots";

    public static void Register(ICoreClientAPI capi, UiSize ui, SettingsWindow window)
    {
        capi.ChatCommands.Create("dts")
            .WithDescription("DeckTooSmall settings. Type .dts help")
            .WithArgs(capi.ChatCommands.Parsers.OptionalWord("what"), capi.ChatCommands.Parsers.OptionalWord("value"))
            .HandleWith(args => Run(ui, window,
                (args[0] as string ?? "").ToLowerInvariant(),
                (args[1] as string ?? "").ToLowerInvariant()));
    }

    private static TextCommandResult Run(UiSize ui, SettingsWindow window, string what, string value)
    {
        bool isNumber = float.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
            && number > 0 && float.IsFinite(number);
        float scale = number * UiSize.Unit;

        switch (what)
        {
            case "":
            case "menu":
                window.TryOpen();
                return TextCommandResult.Success();
            case "help":
                return TextCommandResult.Success(Help);
            case "status":
                return TextCommandResult.Success(Status(ui));
            case "on":
            case "off":
                ui.SetEnabled(what == "on");
                return TextCommandResult.Success("DeckTooSmall is " + what);
            case "reset":
                ui.Reset();
                return TextCommandResult.Success("Window and text sizes follow the main size again");
            case "preset":
                if (!Screens.Presets.Any(p => p.Key == value)) return TextCommandResult.Error("Use compact, recommended or large");
                ui.UsePreset(value);
                return TextCommandResult.Success(Status(ui));
            case "size":
                if (!isNumber) return TextCommandResult.Error("Type a number, for example: .dts size 10");
                ui.SetMain(scale);
                return TextCommandResult.Success(Status(ui));
        }

        bool isText = what == "text";
        bool isPart = Parts.All.Any(p => p.Key == what);
        if (!isText && !isPart && !Group.All.Any(g => g.Key == what)) return TextCommandResult.Error("Unknown name. Type .dts help");

        if (value is "keep" or "follow")
        {
            if (isText) ui.SetTextKeep(value == "keep");
            else if (isPart) ui.SetPartKeep(what, value == "keep");
            else ui.SetWindowKeep(what, value == "keep");
        }
        else if (isNumber)
        {
            if (isText) ui.SetText(scale);
            else if (isPart) ui.SetPart(what, scale);
            else ui.SetWindow(what, scale);
        }
        else
        {
            return TextCommandResult.Error("Add a number, keep or follow. For example: .dts " + what + " 12");
        }
        return TextCommandResult.Success(Status(ui));
    }

    private static string Status(UiSize ui)
    {
        float main = ui.Config.UiScale;
        string Line(string name, SizeRule rule) =>
            $"\n{name}: {rule.Actual(main) / UiSize.Unit:0.#}{(rule.Keep ? " (kept)" : "")}";

        return $"Mod {(ui.Enabled ? "on" : "off")}\nscreen: {ui.ScreenName()}\nsize: {main / UiSize.Unit:0.#}"
            + Line("text", ui.Config.Text)
            + string.Concat(Group.All.Select(g => Line(g.Key, ui.Config.Window(g.Key))))
            + string.Concat(Parts.All.Select(p =>
                $"\n{p.Key}: {ui.PartSize(p.Key) / UiSize.Unit:0.#}{(ui.Config.Part(p.Key).Keep ? " (kept)" : "")}"));
    }
}
