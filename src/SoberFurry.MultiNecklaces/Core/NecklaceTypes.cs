using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoberFurry.MultiNecklaces.Core;

/// <summary>Helpers around the <c>InventoryItem.ITEM_TYPE</c> necklace values.</summary>
internal static class NecklaceTypes
{
    /// <summary>All ITEM_TYPE values whose name starts with "Necklace_".</summary>
    public static readonly List<InventoryItem.ITEM_TYPE> All = BuildAll();

    private static List<InventoryItem.ITEM_TYPE> BuildAll()
    {
        var list = new List<InventoryItem.ITEM_TYPE>();
        foreach (InventoryItem.ITEM_TYPE t in Enum.GetValues(typeof(InventoryItem.ITEM_TYPE)))
        {
            if (IsNecklace(t)) list.Add(t);
        }
        return list;
    }

    public static bool IsNecklace(InventoryItem.ITEM_TYPE type)
    {
        if (type == InventoryItem.ITEM_TYPE.NONE) return false;
        string name = type.ToString();
        return name.StartsWith("Necklace_", StringComparison.Ordinal);
    }

    public static string Name(InventoryItem.ITEM_TYPE type) => type.ToString();

    public static bool TryParse(string? name, out InventoryItem.ITEM_TYPE type)
    {
        type = InventoryItem.ITEM_TYPE.NONE;
        if (string.IsNullOrEmpty(name)) return false;
        try
        {
            type = (InventoryItem.ITEM_TYPE)Enum.Parse(typeof(InventoryItem.ITEM_TYPE), name);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>True if the enum name is known in this build (an unknown id from a newer save is "stored but no effect promised").</summary>
    public static bool IsKnown(string name) => Enum.IsDefined(typeof(InventoryItem.ITEM_TYPE), name);
}
