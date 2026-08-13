# RepoCheque — Taxman's Cheque

Replaces the **surplus money bag** that R.E.P.O. drops at the extraction point when you beat
the haul goal with a **paper cheque** that has the amount printed on it, live.

![Taxman's Cheque](1-main.jpg)

## What it does

### 🧾 Looks like a cheque
A flat, low-poly card using your own artwork, with torn-paper edges.

### 💵 Prints the amount
The surplus value is drawn on the cheque in the game's own Teko font, formatted exactly like
every other money readout.

![The cheque riding in a cart](2-in%20cart.jpg)

### 👶 Rugrat-proof
The baby that steals and throws valuables completely ignores the cheque. Your surplus is safe.

![The Rugrat ignoring the cheque](3-rugrat%20ignored.gif)

### 🛒 Spawns in your cart
Instead of falling from the ceiling and bouncing away, it appears gently inside a nearby cart,
lying flat.

### 🛡️ Indestructible
Drops, explosions, monsters, the green cauldron — the cheque never loses a cent. Paper doesn't
shatter.

![The cheque surviving everything](4-indestructible.gif)

---

Everything about the value, the physics, the grabbing and the multiplayer syncing stays
**completely vanilla**. Only the appearance, the collider shape, the Rugrat's targeting, the
spawn position and the damage immunity are modified — which is why it's low risk.

## Requirements

- **BepInEx 5.x** (tested against 5.4.23.2)
- R.E.P.O. — see *Game version* below

**REPOLib is not required.** Most mods need it because it registers *new content* — custom
valuables, items, enemies, levels. This mod adds no new content: it re-skins an object the game
already spawns. The compiled DLL references only Harmony, BepInEx, Unity and the game's own
`Assembly-CSharp`. It is listed as a *soft* dependency purely so load order stays sensible if
you happen to have it installed.

## ⚠️ Everyone needs this mod — it is not server-side

This is a **client-side** mod, so **every player in the lobby should install it**.

- The cheque's appearance is built independently on each machine. **A player without the mod
  will still see the vanilla money bag** — the value, weight and physics stay identical for
  them, so nothing desyncs or breaks; it just looks different on their screen.
- The **host's** copy is what drives the behaviour: spawning inside the cart, the Rugrat
  ignoring it, and the indestructibility are all host-authoritative, exactly as the game
  already handles them. If the host doesn't have the mod, those three won't apply to anyone.

Short version: **everyone installs it, and make sure the host has it.**

## Install (manual)

1. Make sure BepInEx is installed and has been run at least once.
2. Create a folder called `RepoCheque` in:
   ```
   <your R.E.P.O. folder>\BepInEx\plugins\
   ```
3. Put these three files inside it:
   - `RepoCheque.dll` (from `release/`, or from the Releases page)
   - `Taxman's Cheque - Front.png`
   - `Taxman's Cheque - Back.png`
4. Launch the game once to generate the config file.

You should end up with `BepInEx\plugins\RepoCheque\RepoCheque.dll` plus the two images beside
it. The images live outside the DLL on purpose, so you can redraw the cheque without
rebuilding anything.

## Building from source

Requires the **.NET SDK** (9 or 10) — nothing else; the game references are restored from NuGet.

```bash
cp Directory.Repo.props.example Directory.Repo.props
```

Edit that copy so `GameDirectory` points at your own R.E.P.O. install, then:

```bash
dotnet build
```

The build drops the DLL and both textures straight into your game's
`BepInEx\plugins\RepoCheque\` folder, ready to play.

## Your own artwork

The cheque texture is a plain PNG next to the DLL, so you can redraw it any time without
rebuilding anything.

| | |
|---|---|
| **Front** | `cheque.png`, or any PNG with `front` in the filename |
| **Back** | `cheque_back.png`, or any PNG with `back` in the filename (optional) |
| **Size** | Any, but **2.2 : 1** looks like a real cheque. The shipped art is 1408 × 640. |
| **Shape** | The 3D card automatically matches your image's proportions, so nothing gets stretched. |
| **Transparency** | Supported. Transparent pixels are cut away, so ragged/torn paper edges work. |

Leave a blank area where the amount should print, then point `TextOffsetU` / `TextOffsetV`
at its centre (0–1, measured from the left and from the bottom).

If no image is found, the mod draws a plain placeholder cheque so it still works.

## Config

`BepInEx\config\RepoCheque.cfg` — edit with Notepad, then restart the game.

### 1. General
| Setting | Default | What it does |
|---|---|---|
| `Enabled` | `true` | Master switch. Off = the vanilla money bag comes back. |
| `DebugLogging` | `false` | Extra detail in the log. Turn on if something looks wrong. |
| `CartProbeLogging` | `false` | Very chatty cart diagnostic. Floods the log — leave off. |

### 2. Appearance
| Setting | Default | What it does |
|---|---|---|
| `ScaleMultiplier` | `1.0` | Overall size of the cheque. |
| `Thickness` | `0.06` | How thick the paper **looks**, as a fraction of its height. |
| `ColliderThickness` | `0.075` | How thick the **invisible physics box** is, in metres. See note below. |
| `AspectRatioOverride` | `0` | `0` = match your PNG automatically. Set `2.2` to force cheque proportions. |
| `FitToCart` | `true` | Shrink the cheque if it's too big to lie flat in a cart. |
| `Mass` | `0.6` | Weight. Every cheque weighs the same regardless of the amount — it's paper. |

> **About `ColliderThickness`** — this is deliberately thicker than the paper looks, and it
> matters more than it sounds. If the physics box matches the visible paper, the cheque comes
> to rest *exactly level* with the floor, which makes it flicker against the ground, and it
> sinks out of sight inside a cart (the cart's collision floor sits below its visible one).
> Making the box a little fatter lifts the paper just clear of whatever it lands on.
> `0.075` was tuned in-game: `0.15` visibly hovers, values near `0.05` start to sink.

### 3. Printed Amount
| Setting | Default | What it does |
|---|---|---|
| `ShowPrintedAmount` | `true` | Draw the value on the cheque. |
| `TextAutoFit` | `true` | Scale the number to fill the box, so `$5,000` and `$1,250,000` both fit. |
| `TextSize` | `4.0` | Font size — the maximum when auto-fit is on. |
| `TextColor` | `#1A1A2E` | Hex colour. |
| `TextOffsetU` | `0.35` | Position across the cheque. 0 = left edge, 1 = right edge. |
| `TextOffsetV` | `0.33` | Position up the cheque. 0 = bottom, 1 = top. |
| `TextBoxWidth` | `0.56` | Width of the blank amount box in your artwork, as a fraction. |
| `TextBoxHeight` | `0.34` | Height of the blank amount box, as a fraction. |
| `TextSide` | `Front` | Which face the number is printed on: `Front`, `Back` or `Both`. |
| `TextDepthOffset` | `0.004` | How far the number floats above the paper, in metres. |

### 4. Cart Spawning
| Setting | Default | What it does |
|---|---|---|
| `SpawnInCart` | `true` | Spawn inside a nearby cart instead of dropping from above. |
| `CartSearchRadius` | `25` | How far to look for a cart, in metres. |

### 5. Durability
| Setting | Default | What it does |
|---|---|---|
| `Indestructible` | `true` | The cheque never loses value. |

## If something goes wrong

The mod is built to **fail safely**. If a future game update renames something, the patches
won't apply — the mod logs a clear warning, disables itself, and the vanilla money bag comes
back. It will not break your game or your run.

To check, open:
```
<your R.E.P.O. folder>\BepInEx\LogOutput.log
```
Every line from this mod starts with `RepoCheque`. Set `DebugLogging = true` first for detail.

## Game version

Built and tested against:

| | |
|---|---|
| Steam build ID | `23363152` |
| `REPO.exe` build date | 18 May 2026 |
| Unity engine | 2022.3.67f2 |

R.E.P.O. is patched often and internal names drift between versions. If a future update breaks
the mod, this is the build it was written against.

## Credits

Artwork by Sinan. Uses the game's own Teko font asset, so the printed amount matches the rest
of the UI exactly.
