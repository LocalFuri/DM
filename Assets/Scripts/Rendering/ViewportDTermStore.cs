using System;
using System.Collections.Generic;
using DM.Dungeon;
using UnityEngine;

namespace DM.Rendering
{
  /// <summary>
  /// Occupancy-keyed verified wall states. DTerm checked in ViewEdit captures
  /// the current piece into this store. Keyed by ViewportPatternKey compact
  /// string, never by map coordinates.
  /// </summary>
  [CreateAssetMenu(
      fileName = "ViewportDTerm",
      menuName = "Dungeon Master/Viewport DTerm Store")]
  public sealed class ViewportDTermStore : ScriptableObject
  {
    public const string DefaultAssetPath =
        "Assets/EditorData/ViewportDTerm.asset";

    public List<ViewportDTermEntry> Entries = new();

    public bool TryGet(
        string geometryKey,
        string pieceName,
        out ViewportDTermEntry entry)
    {
      entry = null;
      if (string.IsNullOrEmpty(geometryKey)
          || string.IsNullOrEmpty(pieceName)
          || Entries == null)
      {
        return false;
      }

      for (int i = 0; i < Entries.Count; i++)
      {
        ViewportDTermEntry candidate = Entries[i];
        if (candidate == null)
          continue;

        if (candidate.GeometryKey == geometryKey
            && candidate.PieceName == pieceName)
        {
          entry = candidate;
          return true;
        }
      }

      return false;
    }

    public bool HasEntry(string geometryKey, string pieceName)
    {
      return TryGet(geometryKey, pieceName, out _);
    }

    public void Upsert(ViewportDTermEntry entry)
    {
      if (entry == null
          || string.IsNullOrEmpty(entry.GeometryKey)
          || string.IsNullOrEmpty(entry.PieceName))
      {
        return;
      }

      if (Entries == null)
        Entries = new List<ViewportDTermEntry>();

      for (int i = 0; i < Entries.Count; i++)
      {
        ViewportDTermEntry existing = Entries[i];
        if (existing == null)
          continue;

        if (existing.GeometryKey == entry.GeometryKey
            && existing.PieceName == entry.PieceName)
        {
          Entries[i] = entry;
          return;
        }
      }

      Entries.Add(entry);
    }

    public bool Remove(string geometryKey, string pieceName)
    {
      if (Entries == null
          || string.IsNullOrEmpty(geometryKey)
          || string.IsNullOrEmpty(pieceName))
      {
        return false;
      }

      for (int i = 0; i < Entries.Count; i++)
      {
        ViewportDTermEntry existing = Entries[i];
        if (existing == null)
          continue;

        if (existing.GeometryKey == geometryKey
            && existing.PieceName == pieceName)
        {
          Entries.RemoveAt(i);
          return true;
        }
      }

      return false;
    }

    public static string BuildGeometryKey(
        DungeonMap map,
        int playerX,
        int playerY,
        DungeonFacing facing)
    {
      if (map == null)
        return string.Empty;

      return ViewportPatternKeyBuilder.Build(
          map,
          playerX,
          playerY,
          facing).ToCompactString();
    }

    public static ViewportDTermStore LoadDefault()
    {
#if UNITY_EDITOR
      ViewportDTermStore fromPath =
          UnityEditor.AssetDatabase.LoadAssetAtPath<ViewportDTermStore>(
              DefaultAssetPath);
      if (fromPath != null)
        return fromPath;
#endif
      ViewportDTermStore[] loaded =
          Resources.FindObjectsOfTypeAll<ViewportDTermStore>();
      if (loaded != null)
      {
        for (int i = 0; i < loaded.Length; i++)
        {
          if (loaded[i] != null)
            return loaded[i];
        }
      }

      return null;
    }

#if UNITY_EDITOR
    public void Persist()
    {
      UnityEditor.EditorUtility.SetDirty(this);
      UnityEditor.AssetDatabase.SaveAssets();
    }
#endif
  }

  [Serializable]
  public sealed class ViewportDTermEntry
  {
    public string GeometryKey;
    public string PieceName;
    public bool Enabled;
    public DungeonGraphicType Graphic;
    public int X;
    public int Y;
    public bool Mirror;
    public int FrontWallF1Width;
    public int FrontWallF2Width;
  }
}
