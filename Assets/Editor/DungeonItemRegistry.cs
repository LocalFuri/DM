#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Persistent editor-side identity store. Item identities belong to instances,
// not to their current map coordinates. The caller owns actual gameplay moves.
internal static class DungeonItemRegistry
{
    [Serializable]
    internal class Item
    {
        public long id;
        public string sourceKey;
        public string type;
        public int charges;
        public string locationType;
        public int level;
        public int x;
        public int y;
        public string slot;
    }

    [Serializable]
    private class Database
    {
        public long nextId = 1;
        public List<Item> items = new List<Item>();
    }

    private static Database data;
    private static Dictionary<string, Item> bySource;
    private static Dictionary<long, Item> byId;
    private static string StorePath => Path.Combine(Directory.GetCurrentDirectory(),
        "Library/DungeonMasterItemRegistry.json");

    private static void Load()
    {
        if (data != null) return;
        data = new Database();
        string path = StorePath;
        if (File.Exists(path))
        {
            try
            {
                data = JsonUtility.FromJson<Database>(File.ReadAllText(path));
                if (data == null || data.items == null)
                    throw new InvalidDataException("Registry JSON lacks an items array.");
            }
            catch (Exception ex)
            {
                data = null;
                Debug.LogError("ITEM REGISTRY: could not read " + path + ": " + ex.Message
                    + ". Existing records have NOT been replaced.");
                throw;
            }
        }
        bySource = new Dictionary<string, Item>(StringComparer.Ordinal);
        byId = new Dictionary<long, Item>();
        long maxId = 0;
        foreach (var item in data.items)
        {
            if (item == null || item.id <= 0 || string.IsNullOrEmpty(item.sourceKey)
                || byId.ContainsKey(item.id) || bySource.ContainsKey(item.sourceKey))
            {
                data = null;
                throw new InvalidDataException("ITEM REGISTRY: invalid or duplicated persistent ID/source key.");
            }
            byId.Add(item.id, item);
            bySource.Add(item.sourceKey, item);
            if (item.id > maxId) maxId = item.id;
        }
        if (data.nextId <= maxId) data.nextId = maxId + 1;
    }

    private static void Save()
    {
        string path = StorePath;
        string tempPath = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
        if (File.Exists(path)) File.Replace(tempPath, path, null);
        else File.Move(tempPath, path);
    }

    // Source keys are immutable identifiers from original dungeon data,
    // NOT generated from an item's present position after it moves.
    internal static long RegisterExisting(string sourceKey, string type, int charges,
        string locationType, int level, int x, int y, string slot)
    {
        Load();
        if (string.IsNullOrEmpty(sourceKey) || string.IsNullOrEmpty(type))
            throw new ArgumentException("Item source key and type are required.");
        if (bySource.TryGetValue(sourceKey, out var existing))
        {
            if (existing.type != type)
                throw new InvalidDataException("ITEM REGISTRY: source key reused for another item type: " + sourceKey);
            return existing.id;
        }
        if (data.nextId == long.MaxValue)
            throw new OverflowException("Item ID sequence exhausted.");
        var item = new Item
        {
            id = data.nextId++, sourceKey = sourceKey, type = type, charges = charges,
            locationType = locationType, level = level, x = x, y = y, slot = slot
        };
        data.items.Add(item);
        bySource.Add(sourceKey, item);
        byId.Add(item.id, item);
        Save();
        return item.id;
    }

    internal static Item Find(long id)
    {
        Load();
        if (!byId.TryGetValue(id, out var item))
            throw new KeyNotFoundException("ITEM REGISTRY: unknown item ID " + id);
        return item;
    }

    internal static void Move(long id, string locationType, int level, int x, int y, string slot)
    {
        var item = Find(id);
        if (string.IsNullOrEmpty(locationType))
            throw new ArgumentException("Location type is required.");
        // Preserve the original instance ID and properties across level changes.
        string oldType = item.locationType;
        int oldLevel = item.level, oldX = item.x, oldY = item.y;
        string oldSlot = item.slot;
        item.locationType = locationType;
        item.level = level;
        item.x = x;
        item.y = y;
        item.slot = slot;
        try { Save(); }
        catch
        {
            item.locationType = oldType;
            item.level = oldLevel;
            item.x = oldX;
            item.y = oldY;
            item.slot = oldSlot;
            throw;
        }
    }

    internal static int Count
    {
        get { Load(); return data.items.Count; }
    }
}
#endif
