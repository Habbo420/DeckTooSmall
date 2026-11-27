# DeckTooSmall

A client-side Vintage Story mod that makes the menus usable on the Steam Deck's
1280x800 screen. Built for game version 1.22.

## Using it

- **F7 / F8**: smaller / bigger UI
- Picks a starting size from your screen resolution and remembers your choice per screen
- **F9**: settings window (turn the mod on or off, sizes per window and per part)
- `.dts help` in chat: the same settings as commands

## Building

Needs the .NET 10 SDK, and `VINTAGE_STORY` set to the game's install folder.
Run `./build.sh`; the mod zip appears in `Releases/`.

## Old versions

| Version | What is kept | Notes |
|---|---|---|
| 1.1.0 | source file only, `archive/v1/` | First attempt: bigger text only |
| 2.0.0 | built zip | Shrinks any window that does not fit the screen |
| 2.0.1 | built zip | Own UI size, windows pinned top left |
| 2.0.2 | built zip | F7 / F8 to change the size |
| 2.0.5 | built zip | Fits windows to the screen, top left, own UI size |
| 2.1.0 | built zip | Settings window, per-window sizes, text size, chat commands |
| 2.2.1 | built zip, source at tag `v2.2.1` | Title bar, button and slot sizes |
| 2.2.2 | built zip | Screen presets, per-screen size, 0.5 / 0.1 steps |
| 2.2.3 | built zip and the source in this repository | Character screen panels stay snug (not yet tested) |

Versions 2.0.3, 2.0.4 and 2.2.0 were not kept.
