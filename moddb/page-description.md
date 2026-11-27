# DeckTooSmall

Readable menus on small screens. Made for the Steam Deck, and works out a size
for other resolutions too.

Raising the game's GUI Scale on a small screen makes windows bigger than the
screen, so buttons such as "Close Handbook" end up out of reach. DeckTooSmall
gives you a bigger interface that stays on screen.

## What it does
- Its own UI size, picked from your screen resolution and remembered per screen
- Shrinks any window that would not fit
- Lines windows up from the top left, side by side, above the hotbar
- Optional full-screen world map, Handbook and settings window
- Separate sizes for the hotbar, inventory, character screen, chests, minimap and more.
  Tick "Keep" and that item stays put when you change the main size
- Sizes for text, title bar icons, text buttons and item slots
- Compact, Recommended and Large presets

## Controls
- F7 / F8: smaller / bigger
- F9: settings window
- Chat: type .dts help

Keys can be changed under Settings > Controls. On Steam Deck, put F7, F8 and F9
on back buttons in your controller layout.

## Good to know
- Client-side only. The server does not need it.
- Applies inside a world. The main menu is not changed, and your own GUI Scale
  comes back when you leave.
- The starting size is chosen for handhelds. On a desktop monitor it may be
  large: press F7 or choose Compact.
- Windows that are zoomed to a different size look a little softer.
- Windows cannot be dragged while the mod is on.
- Very large text can run into other things.
- It changes how the game draws its menus, so other interface mods may conflict.
  If something goes wrong, type .dts off, or disable the mod in the Mod Manager.

## Tested on
Vintage Story 1.22.7 on Steam Deck, singleplayer.

## Reporting problems
Please leave a comment with your screen resolution, game version, other mods, a
screenshot, and the client-main.log file if the game logged an error.
