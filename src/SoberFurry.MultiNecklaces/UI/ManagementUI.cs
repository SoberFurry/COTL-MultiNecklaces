using System;
using System.Collections.Generic;
using System.Linq;
using SoberFurry.MultiNecklaces.Core;
using UnityEngine;

namespace SoberFurry.MultiNecklaces.UI;

/// <summary>
/// Periodic reconciliation + the IMGUI necklace-management panel for the follower nearest the player.
///
/// NOTE (documented design choice / deviation): full integration into the native follower menu was
/// out of safe-verifiable scope for v1, so this self-contained panel is the management surface. It
/// never touches vanilla UI, so it cannot soft-lock the game. See README "Known limitations".
/// </summary>
internal sealed class ManagementUI : MonoBehaviour
{
    private const float SyncIntervalSeconds = 2f;
    private float _nextSync;
    private bool _open;
    private FollowerInfo? _target;
    private Vector3 _targetPos;
    private string _toast = "";
    private float _toastUntil;
    private string? _pendingUnequip; // necklaceId awaiting confirmation
    private Vector2 _scroll;

    private GUIStyle? _box, _title, _row, _btn, _btnFocus, _label;

    public static ManagementUI Create()
    {
        var go = new GameObject("SoberFurry.MultiNecklaces.UI");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        return go.AddComponent<ManagementUI>();
    }

    private void Update()
    {
        try
        {
            if (!Plugin.Cfg.Enabled.Value) return;

            if (Time.unscaledTime >= _nextSync)
            {
                _nextSync = Time.unscaledTime + SyncIntervalSeconds;
                SyncTick();
            }

            if (Input.GetKeyDown(Plugin.Cfg.PanelHotkey.Value))
                Toggle();
        }
        catch (Exception e) { Plugin.Log.LogError($"ManagementUI.Update failed: {e}"); }
    }

    private void SyncTick()
    {
        var dm = DataManager.Instance;
        if (dm?.Followers == null) return;
        foreach (var info in dm.Followers)
        {
            if (info == null) continue;
            NecklaceService.Instance.EnsureImported(info);
            // Keep the model authoritatively in sync with our data (prevents desync / dup-on-remove).
            NecklaceService.Instance.ApplyVisibleToVanilla(info);
        }
    }

    private void Toggle()
    {
        _open = !_open;
        _pendingUnequip = null;
        if (_open) AcquireTarget();
    }

    private void AcquireTarget()
    {
        _target = null;
        try
        {
            var player = PlayerFarming.Instance;
            if (player == null) return;
            Vector3 p = player.transform.position;
            Follower? nearest = null;
            float best = float.MaxValue;
            foreach (var f in Follower.Followers)
            {
                if (f == null || f.Brain == null || f.Brain._directInfoAccess == null) continue;
                float d = Vector3.Distance(p, f.transform.position);
                if (d < best) { best = d; nearest = f; }
            }
            if (nearest != null)
            {
                _target = nearest.Brain._directInfoAccess;
                _targetPos = nearest.transform.position;
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"AcquireTarget failed: {e.Message}"); }
    }

    private void Toast(string msgKey)
    {
        _toast = Localizer.Get(msgKey);
        _toastUntil = Time.unscaledTime + 3f;
    }

    private void EnsureStyles()
    {
        if (_box != null) return;
        _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 16, 16) };
        _title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        _label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = false };
        _row = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 6, 6) };
        _btn = new GUIStyle(GUI.skin.button) { fontSize = 14, padding = new RectOffset(10, 10, 6, 6) };
        _btnFocus = new GUIStyle(_btn);
        _btnFocus.normal.textColor = Color.yellow;
    }

    private void OnGUI()
    {
        if (!_open || !Plugin.Cfg.Enabled.Value) return;
        EnsureStyles();

        float w = Mathf.Min(640f, Screen.width * 0.6f);
        float h = Mathf.Min(560f, Screen.height * 0.8f);
        var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(rect, _box);

        GUILayout.BeginHorizontal();
        GUILayout.Label(Localizer.Get("panel.title"), _title);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(Localizer.Get("panel.close"), _btn, GUILayout.Width(110))) { _open = false; }
        GUILayout.EndHorizontal();

        if (_target == null)
        {
            GUILayout.Space(12);
            GUILayout.Label(Localizer.Get("panel.noFollower"), _label);
            GUILayout.EndArea();
            return;
        }

        string fname = string.IsNullOrEmpty(_target.Name) ? $"#{_target.ID}" : _target.Name;
        GUILayout.Label($"{Localizer.Get("panel.follower")}: {fname}  (ID {_target.ID})", _label);
        GUILayout.Space(6);

        _scroll = GUILayout.BeginScrollView(_scroll);

        // Equipped section
        GUILayout.Label(Localizer.Get("panel.equipped"), _title);
        var entries = NecklaceService.Instance.GetEntries(_target.ID);
        var visible = NecklaceService.Instance.GetVisible(_target.ID);
        if (entries.Count == 0)
            GUILayout.Label(Localizer.Get("panel.empty"), _label);

        foreach (var entry in entries)
        {
            NecklaceTypes.TryParse(entry.NecklaceId, out var type);
            bool known = NecklaceTypes.IsKnown(entry.NecklaceId);
            bool isVisible = visible.HasValue && NecklaceTypes.Name(visible.Value) == entry.NecklaceId;

            GUILayout.BeginHorizontal(_row);
            string label = known ? PrettyName(entry.NecklaceId) : $"{Localizer.Get("panel.unknown")}: {entry.NecklaceId}";
            string status = isVisible ? Localizer.Get("panel.visible") : Localizer.Get("panel.hidden");
            GUILayout.Label($"{label}  [{status}]", _label, GUILayout.Width(w * 0.5f));
            GUILayout.FlexibleSpace();

            GUI.enabled = !isVisible && known;
            if (GUILayout.Button(Localizer.Get("panel.makeVisible"), _btn, GUILayout.Width(90)))
            {
                var r = NecklaceService.Instance.SetVisible(_target, type);
                Toast(r.MessageKey);
            }
            GUI.enabled = true;

            string uneqLabel = _pendingUnequip == entry.NecklaceId ? Localizer.Get("panel.confirm") : Localizer.Get("panel.unequip");
            if (GUILayout.Button(uneqLabel, _pendingUnequip == entry.NecklaceId ? _btnFocus : _btn, GUILayout.Width(110)))
            {
                bool needConfirm = Plugin.Cfg.ConfirmRareUnequip.Value;
                if (needConfirm && _pendingUnequip != entry.NecklaceId)
                {
                    _pendingUnequip = entry.NecklaceId;
                }
                else
                {
                    _pendingUnequip = null;
                    var r = NecklaceService.Instance.Unequip(_target, type, toInventory: true);
                    Toast(r.MessageKey);
                }
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(12);

        // Available-to-equip section (owned necklaces not yet equipped on this follower)
        GUILayout.Label(Localizer.Get("panel.available"), _title);
        foreach (var type in NecklaceTypes.All)
        {
            int owned = SafeOwned(type);
            if (owned <= 0) continue;
            if (NecklaceService.Instance.Has(_target.ID, type)) continue;

            GUILayout.BeginHorizontal(_row);
            GUILayout.Label($"{PrettyName(NecklaceTypes.Name(type))}  x{owned}", _label, GUILayout.Width(w * 0.6f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", _btn, GUILayout.Width(60)))
            {
                var r = NecklaceService.Instance.Equip(_target, type, fromInventory: true);
                Toast(r.MessageKey);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        GUILayout.Space(8);
        if (GUILayout.Button(Localizer.Get("panel.prepareRemoval"), _btn))
        {
            int n = NecklaceService.Instance.PrepareForRemoval();
            Toast("op.ok");
            Plugin.Log.LogInfo($"PrepareForRemoval returned {n} necklaces.");
        }

        if (!string.IsNullOrEmpty(_toast) && Time.unscaledTime < _toastUntil)
        {
            GUILayout.Space(6);
            GUILayout.Label(_toast, _title);
        }

        GUILayout.EndArea();
    }

    private static int SafeOwned(InventoryItem.ITEM_TYPE type)
    {
        try { return Inventory.GetItemQuantity(type); } catch { return 0; }
    }

    private static string PrettyName(string id)
    {
        // Prefer the game's own localized necklace name (e.g. "Ожерелье с перьями").
        if (NecklaceTypes.TryParse(id, out var t))
        {
            try
            {
                string n = InventoryItem.LocalizedName(t);
                if (!string.IsNullOrWhiteSpace(n)) return n;
            }
            catch { }
        }
        return id.Replace("Necklace_", "").Replace("_", " ");
    }
}
