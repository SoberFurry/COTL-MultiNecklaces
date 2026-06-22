# Reverse Engineering Map — Cult of the Lamb

Game build verified against the **installed** assembly.

| Item | Value |
|---|---|
| Game path | `H:\SteamLibrary\steamapps\common\Cult of the Lamb` |
| Unity | 2022.3.62f2 (Mono, x64) |
| Steam build id | 22885603 |
| Assembly-CSharp.dll | 15.9 MB, 2026-06-16 |
| BepInEx | 5.4.19 (pack BepInExPack_CultOfTheLamb 5.4.21) |
| COTL_API | 0.3.4 (io.github.xhayper.COTL_API) |

All signatures below were taken from `ilspycmd` decompilation of the installed `Assembly-CSharp.dll`
(see `docs/decomp/`). They must be re-verified after any game update.

---

## 1. Necklaces (for Sam.MultiNecklaces)

### Necklace storage
- Necklaces are values of the enum **`InventoryItem.ITEM_TYPE`**.
- A follower's *single* equipped necklace is stored in **`FollowerInfo.Necklace`** (`public InventoryItem.ITEM_TYPE Necklace;`).
- **`FollowerInfo.ShowingNecklace`** (`public bool`) — whether the necklace is rendered.
- **`FollowerInfo.ID`** (`public int ID;`) — the stable, permanent follower id.
  Assigned via `followerInfo.ID = ++DataManager.Instance.FollowerID;` and de-duplicated against
  `FollowerManager.UniqueFollowerIDs`. **This is the FollowerKey** required by the TZ.
- Static lookup: **`FollowerInfo.GetInfoByID(int id, bool includeDead)`**.

### Necklace types present in this build (17)
`Necklace_1 Necklace_2 Necklace_3 Necklace_4 Necklace_5`
`Necklace_Loyalty Necklace_Demonic Necklace_Dark Necklace_Light Necklace_Missionary`
`Necklace_Gold_Skull Necklace_Bell Necklace_Deaths_Door Necklace_Winter Necklace_Frozen`
`Necklace_Weird Necklace_Targeted`

### Effect read sites (the "scattered checks" warned about in the TZ)
`.Necklace` is read in **133 places across 55 files**. The ones that are *clean, stable instance
methods on `FollowerInfo`* (and therefore safe to patch for effect-stacking) are:

| Effect | Method (instance on FollowerInfo) | Vanilla logic |
|---|---|---|
| Demon level (+2) | `int GetDemonLevel()` | `Clamp(XPLevel,1,10) + (Necklace==Necklace_Demonic ? 2 : 0)` |
| Immortal trait | `bool HasTraitFromNecklace(FollowerTrait.TraitType trait)` | `Immortal` if `ID!=666 && ID!=10009 && Necklace==Necklace_Gold_Skull` |

These two are patched (postfix) so a **hidden** `Necklace_Demonic` / `Necklace_Gold_Skull` still
applies. Other necklace effects are inline field comparisons scattered through `FollowerBrain`,
`Follower`, `RitualSacrifice`, etc.; in v1 those apply only while the necklace is the **visible**
one (documented limitation — see README "Effect stacking").

### Visible necklace rendering
- `FollowerBrain.SetFollowerCostume(... InventoryItem.ITEM_TYPE Necklace ...)` renders the necklace
  from `FollowerInfo.Necklace`. Keeping `FollowerInfo.Necklace = visible` makes the vanilla visual
  + the visible necklace's native effect work with **zero** extra patching → satisfies the
  TZ "double-apply protection" requirement automatically.

### Inventory / spawning
- `InventoryItem.Spawn(ITEM_TYPE type, int quantity, Vector3 position, float startSpeed = 4f, Action<PickUp> result = null)`
- `Inventory.ChangeItemQuantity(InventoryItem.ITEM_TYPE type, int quantity, int reserved = 0)`
- `Inventory.GetItemQuantity(InventoryItem.ITEM_TYPE itemType) : int`

### Butchering (drop all necklaces after completion)
- File: `Interaction_HarvestMeat.cs`.
- The butchering coroutine `HarvestMeatIE()` at the **completion point** does:
  ```
  if (DeadWorshipper.followerInfo.Necklace != 0) {
      InventoryItem.Spawn(DeadWorshipper.followerInfo.Necklace, 1, transform.position + back*0.5f);
      RemoveTraitGivenByItem();          // L803
  }
  DeadWorshipper.followerInfo.Necklace = NONE;   // L805
  ```
- A coroutine body cannot be cleanly postfixed, but **`private void RemoveTraitGivenByItem()`** is a
  normal method called exactly at the butcher/loot completion point. We postfix it: it gives us the
  `Interaction_HarvestMeat` instance (→ `DeadWorshipper.followerInfo.ID`), the visible necklace was
  already dropped by vanilla, so we drop every **remaining (hidden)** necklace once and clear our
  record, guarded by an idempotency journal keyed by follower id.

### Save slot
- `SaveAndLoad.SAVE_SLOT` (`public static int`) — current slot index. Used to name our per-slot
  mod save file. Save directory = `Application.persistentDataPath/saves` (confirmed: COTL_API writes
  `io.github.xhayper.COTL_API.mp` there).

---

## 2. Indoctrination (for Sam.ConfirmIndoctrination)

### First-recruit flow
- `FollowerRecruit : Interaction` is the interaction on a **new waiting recruit** at the base.
- The interact button triggers **`FollowerRecruit.OnInteract(StateMachine state)`** (primary, customise=true)
  and **`FollowerRecruit.OnSecondaryInteract(StateMachine state)`** (secondary, customise=false).
- These call `DoRecruit(state, customise)` → eventually `SimpleNewRecruitRoutine` →
  `UIManager.ShowIndoctrinationMenu(Follower, OriginalFollowerLookData)` which opens the
  name/form screen (`UIFollowerIndoctrinationMenuController.Show`).
- `FollowerRecruit.Follower` field → `Follower.Brain.Info.ID` is the follower key.

### Why we hook `FollowerRecruit.OnInteract`, not `ShowIndoctrinationMenu`
- `UIManager.ShowIndoctrinationMenu` is **shared**: it is also called by
  `Interaction_Reindoctrinate` (re-indoctrination of an existing cultist, L236).
- Hooking `FollowerRecruit.OnInteract`/`OnSecondaryInteract` therefore cleanly targets **only the
  first acceptance of a new waiting follower** and never re-indoctrination — exactly the TZ scope.

### Cancel inside the native screen
- `UIFollowerIndoctrinationMenuController` has **no vanilla cancel** — `OnCancelButtonInput()` just
  re-focuses the Accept button. Accept path: `_acceptButton.onClick` → `SwapOutfit()` → `Hide()` →
  `OnHideCompleted` → `OnIndoctrinationCompleted` (FollowerRecruit finalises the recruit).
- Adding a safe in-screen cancel requires injecting a separate UI element and aborting before the
  finalise callback fires, **without** `RemoveAllListeners` on vanilla buttons. This is implemented
  as an experimental, fail-open option (`Mode=Both/InsideScreen`); the default `Mode=BeforeScreen`
  is the guaranteed-safe mechanism (see README).
