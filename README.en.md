# Sam MultiNecklaces

A BepInEx plugin for **Cult of the Lamb** that lets one follower wear **several different necklaces**
at once. All equipped necklaces stay active; only one chosen necklace is shown on the model, but the
hidden ones keep working.

- GUID: `com.sam.cultofthelamb.multinecklaces`
- Built against: Unity 2022.3.62f2, build 22885603
- Dependencies: BepInEx 5.4.x (COTL pack), COTL_API 0.3.4 (soft)

## Install
```
<GameFolder>\BepInEx\plugins\SamMods\MultiNecklaces\
```
Config is created on first run at `BepInEx\config\com.sam.cultofthelamb.multinecklaces.cfg`.

## Usage
1. Stand next to a follower.
2. Press **F8** (configurable, `UI/PanelHotkey`) to open the "Necklaces" panel for the nearest follower.
3. **Equip from inventory**: press `+` next to a necklace (consumed from inventory; no duplicate types).
4. **Equipped** list: **Show** makes a necklace visible (others keep their effects); **Remove** returns
   it to the inventory (rare necklaces ask for confirmation).
5. **Prepare save for mod removal** returns all hidden necklaces to the inventory, leaving only the
   visible one on each follower — run this before uninstalling.

## Implemented
- Multiple different necklaces per follower; duplicate types rejected before the item is consumed.
- Full equipped list with visible-selection and removal; only one necklace rendered (vanilla
  `Necklace` field mirrors the visible one — the "bridge").
- Effect stacking for **hidden** necklaces for the cleanly-patchable cases: **demon level
  (Necklace_Demonic, +2)** and **immortality (Necklace_Gold_Skull)** via postfixes on
  `FollowerInfo.GetDemonLevel` / `HasTraitFromNecklace` (no double-apply: vanilla handles the visible one).
- Per-save-slot JSON store with atomic writes, schema version, `.bak` and pre-migration backup.
- Stable follower key = permanent `FollowerInfo.ID`.
- Death does not strip necklaces; resurrection keeps the set.
- Butchering drops every remaining necklace once (idempotent journal); record cleared.
- Russian/English localization.

## Known limitations (honest)
- Hidden-necklace effect stacking currently covers only types read through clean getters
  (Demonic, Gold_Skull). Other necklaces apply only while they are the **visible** one in v1, because
  their checks are inline `Necklace` field comparisons scattered across dozens of sites.
- Manage necklaces through the panel; do not change a multi-necklace follower's necklace via the
  vanilla appearance menu (it would return the old necklace to inventory and desync).
- Equip/remove/butcher/resurrect paths are verified to **compile and load**; full in-game click
  testing must be done manually (input automation is unavailable here).

## Uninstall
Run "Prepare save for mod removal" in the panel, then `uninstall_sammods.ps1`, or delete
`BepInEx\plugins\SamMods\MultiNecklaces\` and `saves\Sam.MultiNecklaces.slot*.json`.
