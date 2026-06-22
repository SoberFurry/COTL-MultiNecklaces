using System.Collections.Generic;
using Newtonsoft.Json;

namespace SoberFurry.MultiNecklaces.Models;

internal enum EntryState
{
    Equipped,
    PendingConsumption,
    Unknown
}

/// <summary>One equipped necklace. NecklaceId is the <c>InventoryItem.ITEM_TYPE</c> enum NAME
/// (not its numeric value) so the data survives enum reordering between game versions.</summary>
internal sealed class NecklaceEntry
{
    [JsonProperty("necklaceId")] public string NecklaceId = "";
    [JsonProperty("equipOrder")] public long EquipOrder;
    [JsonProperty("state")] public EntryState State = EntryState.Equipped;
    [JsonProperty("metadata")] public Dictionary<string, string> Metadata = new();
}

internal sealed class NecklaceLoadout
{
    [JsonProperty("equipped")] public List<NecklaceEntry> Equipped = new();
    [JsonProperty("visibleNecklaceId")] public string? VisibleNecklaceId;
    /// <summary>Remembers the last shown necklace so the wheel icon stays meaningful when all are hidden.</summary>
    [JsonProperty("lastVisibleNecklaceId")] public string? LastVisibleNecklaceId;
    [JsonProperty("lastUpdatedUtc")] public string LastUpdatedUtc = "";
}

internal sealed class MultiNecklacesSave
{
    public const int CurrentSchemaVersion = 1;

    [JsonProperty("schemaVersion")] public int SchemaVersion = CurrentSchemaVersion;
    [JsonProperty("gameSaveIdentity")] public string GameSaveIdentity = "";
    [JsonProperty("revision")] public long Revision;
    /// <summary>Key = follower id (FollowerInfo.ID) as string.</summary>
    [JsonProperty("followers")] public Dictionary<string, NecklaceLoadout> Followers = new();
    /// <summary>Idempotency journal for butchering drops (keyed by follower id string).</summary>
    [JsonProperty("butcherJournal")] public Dictionary<string, OperationJournalEntry> ButcherJournal = new();
}

/// <summary>Idempotency record so a repeated butchering callback cannot double-drop.</summary>
internal sealed class OperationJournalEntry
{
    [JsonProperty("followerId")] public int FollowerId;
    [JsonProperty("revision")] public long Revision;
    [JsonProperty("necklaceIds")] public List<string> NecklaceIds = new();
    [JsonProperty("status")] public string Status = "Started"; // Started | Committed
}
