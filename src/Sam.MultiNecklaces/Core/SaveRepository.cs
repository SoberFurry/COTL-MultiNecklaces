using System;
using System.IO;
using Newtonsoft.Json;
using Sam.MultiNecklaces.Models;
using UnityEngine;

namespace Sam.MultiNecklaces.Core;

/// <summary>
/// Per-slot JSON persistence with atomic writes, one rolling backup, and a pre-migration backup.
/// Files live next to the vanilla saves: <c>{persistentDataPath}/saves/Sam.MultiNecklaces.slot{N}.json</c>.
/// </summary>
internal sealed class SaveRepository
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Include,
        DefaultValueHandling = DefaultValueHandling.Include
    };

    private string SavesDir => Path.Combine(Application.persistentDataPath, "saves");

    private string FileFor(int slot) => Path.Combine(SavesDir, $"Sam.MultiNecklaces.slot{slot}.json");
    private string TempFor(int slot) => FileFor(slot) + ".tmp";
    private string BakFor(int slot) => FileFor(slot) + ".bak";

    public int CurrentSlot
    {
        get
        {
            try { return SaveAndLoad.SAVE_SLOT; }
            catch { return 0; }
        }
    }

    public MultiNecklacesSave LoadOrCreate(int slot)
    {
        try
        {
            Directory.CreateDirectory(SavesDir);
            string path = FileFor(slot);
            if (!File.Exists(path))
            {
                // Try to recover from a previous backup before declaring "new".
                if (File.Exists(BakFor(slot)))
                {
                    var recovered = TryRead(BakFor(slot));
                    if (recovered != null)
                    {
                        Plugin.Log.LogWarning($"Main save missing; recovered slot {slot} from backup.");
                        return Migrate(recovered, slot);
                    }
                }
                var fresh = new MultiNecklacesSave { GameSaveIdentity = slot.ToString() };
                Plugin.Log.LogInfo($"No mod save for slot {slot}; starting fresh.");
                return fresh;
            }

            var data = TryRead(path);
            if (data == null)
            {
                Plugin.Log.LogError($"Mod save for slot {slot} is corrupt; attempting backup restore.");
                var recovered = File.Exists(BakFor(slot)) ? TryRead(BakFor(slot)) : null;
                data = recovered ?? new MultiNecklacesSave { GameSaveIdentity = slot.ToString() };
            }
            return Migrate(data, slot);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"LoadOrCreate failed for slot {slot}: {e}");
            return new MultiNecklacesSave { GameSaveIdentity = slot.ToString() };
        }
    }

    private MultiNecklacesSave? TryRead(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            var data = JsonConvert.DeserializeObject<MultiNecklacesSave>(json, JsonSettings);
            return data;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Failed to read '{path}': {e.Message}");
            return null;
        }
    }

    private MultiNecklacesSave Migrate(MultiNecklacesSave data, int slot)
    {
        if (data.SchemaVersion == MultiNecklacesSave.CurrentSchemaVersion)
            return data;

        // Pre-migration backup (separate from the rolling .bak).
        try
        {
            string pre = FileFor(slot) + $".v{data.SchemaVersion}.premigration.bak";
            File.WriteAllText(pre, JsonConvert.SerializeObject(data, JsonSettings));
            Plugin.Log.LogInfo($"Pre-migration backup written: {Path.GetFileName(pre)}");
        }
        catch (Exception e) { Plugin.Log.LogWarning($"Pre-migration backup failed: {e.Message}"); }

        // Step migrations N -> N+1 would go here. Currently only v1 exists.
        while (data.SchemaVersion < MultiNecklacesSave.CurrentSchemaVersion)
        {
            // example: if (data.SchemaVersion == 1) { ...; data.SchemaVersion = 2; }
            data.SchemaVersion = MultiNecklacesSave.CurrentSchemaVersion;
        }
        return data;
    }

    /// <summary>Atomic save: temp file -> validate -> roll backup -> replace.</summary>
    public bool Save(MultiNecklacesSave data, int slot)
    {
        try
        {
            Directory.CreateDirectory(SavesDir);
            data.Revision++;
            data.SchemaVersion = MultiNecklacesSave.CurrentSchemaVersion;

            string tmp = TempFor(slot);
            string json = JsonConvert.SerializeObject(data, JsonSettings);
            File.WriteAllText(tmp, json);

            // Validate the temp file parses and carries the expected revision.
            var check = TryRead(tmp);
            if (check == null || check.Revision != data.Revision)
            {
                Plugin.Log.LogError($"Atomic save validation failed for slot {slot}; main file left intact.");
                TryDelete(tmp);
                return false;
            }

            string path = FileFor(slot);
            if (File.Exists(path))
            {
                TryDelete(BakFor(slot));
                File.Copy(path, BakFor(slot));
            }

            // Replace atomically where supported.
            if (File.Exists(path))
            {
                try { File.Replace(tmp, path, null); }
                catch { TryDelete(path); File.Move(tmp, path); }
            }
            else
            {
                File.Move(tmp, path);
            }
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Save failed for slot {slot}: {e}");
            return false;
        }
    }

    /// <summary>One-time backup of the matching vanilla save files for this slot (before our first write).</summary>
    public void BackupVanillaSlotOnce(int slot)
    {
        try
        {
            string marker = FileFor(slot) + ".vanillabackupdone";
            if (File.Exists(marker)) return;
            string dir = SavesDir;
            string backupDir = Path.Combine(dir, $"Sam.MultiNecklaces.vanillabackup.slot{slot}");
            Directory.CreateDirectory(backupDir);
            foreach (string pattern in new[] { $"slot_{slot}.mp", $"meta_{slot}.mp" })
            {
                string src = Path.Combine(dir, pattern);
                if (File.Exists(src))
                    File.Copy(src, Path.Combine(backupDir, pattern), overwrite: true);
            }
            File.WriteAllText(marker, DateTime.UtcNow.ToString("o"));
            Plugin.Log.LogInfo($"Vanilla slot {slot} backed up to {Path.GetFileName(backupDir)}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Vanilla backup for slot {slot} failed (non-fatal): {e.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
