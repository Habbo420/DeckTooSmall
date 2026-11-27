# DeckTooSmall

A client-side Vintage Story mod that makes the menus usable on the Steam Deck's
1280x800 screen. Built for game version 1.22.

## Using it

- **F7 / F8**: smaller / bigger UI
- **F9**: settings window (turn the mod on or off, sizes per window and per part)
- `.dts help` in chat: the same settings as commands

## Building

Needs the .NET 10 SDK, and `VINTAGE_STORY` set to the game's install folder.
Run `./build.sh`; the mod zip appears in `Releases/`.

## Old versions

| Version | What is kept | Notes |
|---|---|---|
| 1.1.0 | source file only, `archive/v1/` | First attempt: bigger text only |
| 2.0.5 | built zip | Fits windows to the screen, top left, own UI size |
| 2.1.0 | built zip | Settings window, per-window sizes, text size, chat commands |
| 2.2.1 | built zip and the source in this repository | Title bar, button and slot sizes |

Versions 2.0.0 to 2.0.4 and 2.2.0 were not kept.
