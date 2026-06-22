# Sam MultiNecklaces

A BepInEx plugin for **Cult of the Lamb**: one follower can wear **several different necklaces** at
once. All equipped necklaces stay active; only one chosen ("visible") necklace is shown on the model,
the others keep working while hidden.

- Version: **1.5.0**
- GUID: `com.sam.cultofthelamb.multinecklaces`
- Built against: Unity 2022.3.62f2, build 22885603
- Dependencies: BepInEx 5.4.x (COTL pack), COTL_API 0.3.4 (soft)

## Install (players)
```
<GameFolder>\BepInEx\plugins\SamMods\MultiNecklaces\
```
Config is created on first run at `BepInEx\config\com.sam.cultofthelamb.multinecklaces.cfg`.

## How to use — in the follower's radial wheel
Management is built into the **follower interaction wheel** (same wheel as "Give work" etc.):

1. Open the follower's interaction wheel.
2. Pick **"Necklaces"** — its icon is the currently visible necklace.
3. The sub-wheel opens:
   - **"Gift a necklace"** is pinned first — equip another from your inventory;
   - then one slice per equipped necklace, each marked with an **eye** (shown on the model) or a
     **crossed-out eye** (hidden).
4. Click a necklace → its action wheel: **Make visible** / **Hide**, and **Remove**.
5. With many necklaces the wheel **paginates automatically** (like the vanilla wheel).

Hovering any entry shows the necklace **name and effect**.

> Fallback interface: an **F8** panel (configurable via `UI/PanelHotkey`). The wheel is the main way.

## Where necklaces are shown
The **"Read thoughts"** screen lists **all** equipped necklaces (visible and hidden) with icons,
names and descriptions.

## Loyalty
Like vanilla: equipping a necklace grants the follower adoration/loyalty (+ a positive thought);
removing one adds a negative thought.

## Hidden-necklace effect stacking
The **visible** necklace's effect always works (vanilla). For **hidden** ones, the effects that the
game exposes through clean members are stacked safely:

| Necklace | Effect | Stacks while hidden |
|---|---|---|
| Necklace_1 | +devotion | ✅ |
| Necklace_4 (nature) | +resource gathering | ✅ |
| Necklace_Frozen | doesn't freeze | ✅ |
| Necklace_Demonic | +2 demon level | ✅ |
| Necklace_Gold_Skull | immortality | ✅ |
| others (speed, no-sleep, death save, loyalty, etc.) | various | ⚠️ only while visible |

The remaining effects are inline in large game methods and will be added one by one.

## Save & safety
- Per-save-slot JSON store with atomic writes, schema version, `.bak` and pre-migration backup.
- Follower key = permanent `FollowerInfo.ID`.
- Death keeps the necklaces; resurrection restores the set.
- Butchering drops every remaining necklace once (idempotent).

## Build from source
.NET SDK 8 and the game installed with BepInEx + COTL_API:
```
dotnet build src/Sam.MultiNecklaces/Sam.MultiNecklaces.csproj -c Release -p:GamePath="X:\path\to\Cult of the Lamb"
```
Or set the `COTL_GAMEPATH` environment variable. Game DLLs are referenced by `HintPath` and are not
committed to the repo.

## License
MIT. This mod contains no game files. "Cult of the Lamb" is a trademark of Massive Monster / Devolver Digital.
