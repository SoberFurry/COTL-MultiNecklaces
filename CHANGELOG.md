# Changelog — Sam MultiNecklaces

## 1.2.0
- Necklace management is now in the follower's radial command wheel: a "Necklaces" entry whose icon
  is the currently visible necklace opens a sub-wheel with Show / Remove per necklace + a "Gift a
  necklace" entry (reuses the vanilla gift wheel, routed through the service).
- The vanilla single-necklace wheel items are replaced by this unified group when the follower is managed.
- Necklace names now use the game's localized item names everywhere (no more "1/2/3").
- F8 panel kept as a secondary interface.

## 1.1.0
- Giving a 2nd+ necklace through the normal follower wheel now works (was blocked by the vanilla
  "AlreadyHaveNecklace" check). Remove-necklace through the wheel now syncs with the mod data.

## 1.0.0
- First release.
- Multiple necklaces per follower, visible-selection, per-slot atomic JSON save.
- Hidden-necklace effect stacking for Necklace_Demonic and Necklace_Gold_Skull.
- Butchering drops all remaining necklaces once (idempotent).
- Death/resurrection preserve the loadout.
- RU/EN localization, management panel (hotkey F8), prepare-for-removal command.
