using System;
using System.Collections.Generic;
using DM.Dungeon;
using DM.Rendering;
using UnityEngine;

/// <summary>
/// Per-pose Enabled + MirrorHorizontally flags. Keyed by exact X + Y + Facing.
/// Shared by Edit Mode and Play/Build. Does not store Graphic / X / Y / order.
/// Captures and applies every layout piece — no name/type exclusions.
/// </summary>
[CreateAssetMenu(
    fileName = "ViewportPoseVisibility",
    menuName = "Dungeon Master/Viewport Pose Visibility")]
public sealed class ViewportPoseVisibilityStore : ScriptableObject
{
  public const string DefaultAssetPath =
      "Assets/EditorData/ViewportPoseVisibility.asset";

  public string MapId = "HallOfChampions";

  public List<ViewportPoseVisibilityEntry> Entries = new();

  public static string FormatPoseKey(int x, int y, DungeonFacing facing)
  {
    return x + "," + y + "," + facing;
  }

  public bool TryFindEntry(
      int x,
      int y,
      DungeonFacing facing,
      out ViewportPoseVisibilityEntry entry)
  {
    for (int i = 0; i < Entries.Count; i++)
    {
      ViewportPoseVisibilityEntry candidate = Entries[i];
      if (candidate == null)
        continue;

      if (candidate.X == x
          && candidate.Y == y
          && candidate.Facing == facing)
      {
        entry = candidate;
        return true;
      }
    }

    entry = null;
    return false;
  }

  public ViewportPoseVisibilityEntry GetOrCreateEntry(
      int x,
      int y,
      DungeonFacing facing)
  {
    if (TryFindEntry(x, y, facing, out ViewportPoseVisibilityEntry existing))
      return existing;

    ViewportPoseVisibilityEntry created = new ViewportPoseVisibilityEntry
    {
      X = x,
      Y = y,
      Facing = facing
    };
    Entries.Add(created);
    return created;
  }

  /// <summary>
  /// Replaces obsolete per-pose "Front Wall F1 A/B" names with "Front Wall F1".
  /// Keeps existing Front Wall F1 flags when already present.
  /// </summary>
  public bool MigrateObsoleteFrontWallF1ABEntries()
  {
    bool changed = false;
    if (Entries == null)
      return false;

    for (int i = 0; i < Entries.Count; i++)
    {
      if (MigrateObsoleteFrontWallF1ABEntry(Entries[i]))
        changed = true;
    }

    return changed;
  }

  private static bool MigrateObsoleteFrontWallF1ABEntry(
      ViewportPoseVisibilityEntry entry)
  {
    if (entry == null || entry.PieceNames == null)
      return false;

    int indexA = IndexOfName(entry.PieceNames, "Front Wall F1 A");
    int indexB = IndexOfName(entry.PieceNames, "Front Wall F1 B");
    if (indexA < 0 && indexB < 0)
      return false;

    int indexF1 = IndexOfName(entry.PieceNames, "Front Wall F1");
    if (indexF1 < 0)
    {
      bool enabledA = GetFlag(entry.EnabledFlags, indexA);
      bool enabledB = GetFlag(entry.EnabledFlags, indexB);
      bool mirrorA = GetFlag(entry.MirrorHorizontallyFlags, indexA);
      bool mirrorB = GetFlag(entry.MirrorHorizontallyFlags, indexB);

      bool enabled = enabledA || enabledB;
      bool mirror = false;
      if (enabledA)
        mirror = mirrorA;
      else if (enabledB)
        mirror = mirrorB;

      int insertAt = indexA >= 0 ? indexA : indexB;
      InsertFlag(entry.EnabledFlags, insertAt, enabled);
      InsertFlag(entry.MirrorHorizontallyFlags, insertAt, mirror);
      entry.PieceNames.Insert(insertAt, "Front Wall F1");

      if (indexA >= 0 && indexA >= insertAt)
        indexA++;
      if (indexB >= 0 && indexB >= insertAt)
        indexB++;
    }

    if (indexA >= 0 && indexB >= 0)
    {
      int first = Math.Min(indexA, indexB);
      int second = Math.Max(indexA, indexB);
      RemoveAt(entry, second);
      RemoveAt(entry, first);
    }
    else if (indexA >= 0)
    {
      RemoveAt(entry, indexA);
    }
    else
    {
      RemoveAt(entry, indexB);
    }

    return true;
  }

  private static int IndexOfName(List<string> names, string name)
  {
    if (names == null)
      return -1;

    for (int i = 0; i < names.Count; i++)
    {
      if (names[i] == name)
        return i;
    }

    return -1;
  }

  private static bool GetFlag(List<bool> flags, int index)
  {
    if (flags == null || index < 0 || index >= flags.Count)
      return false;

    return flags[index];
  }

  private static void InsertFlag(List<bool> flags, int index, bool value)
  {
    if (flags == null)
      return;

    if (index < 0)
      index = 0;
    if (index > flags.Count)
      index = flags.Count;

    flags.Insert(index, value);
  }

  private static void RemoveAt(ViewportPoseVisibilityEntry entry, int index)
  {
    if (entry.PieceNames != null
        && index >= 0
        && index < entry.PieceNames.Count)
    {
      entry.PieceNames.RemoveAt(index);
    }

    if (entry.EnabledFlags != null
        && index >= 0
        && index < entry.EnabledFlags.Count)
    {
      entry.EnabledFlags.RemoveAt(index);
    }

    if (entry.MirrorHorizontallyFlags != null
        && index >= 0
        && index < entry.MirrorHorizontallyFlags.Count)
    {
      entry.MirrorHorizontallyFlags.RemoveAt(index);
    }
  }

  /// <summary>
  /// Stores Enabled + MirrorHorizontally for every non-null layout piece.
  /// </summary>
  public void CaptureFromLayout(
      ViewportPoseVisibilityEntry entry,
      ViewportLayout layout)
  {
    if (entry == null || layout == null || layout.Pieces == null)
      return;

    entry.PieceNames.Clear();
    entry.EnabledFlags.Clear();
    entry.MirrorHorizontallyFlags.Clear();

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      entry.PieceNames.Add(piece.Name ?? string.Empty);
      entry.EnabledFlags.Add(piece.Enabled);
      entry.MirrorHorizontallyFlags.Add(piece.MirrorHorizontally);
    }
  }

  /// <summary>
  /// Applies stored Enabled + MirrorHorizontally to every layout piece.
  /// Pieces missing from the entry get safe defaults (Enabled=false,
  /// MirrorHorizontally=false) so prior-pose state cannot leak.
  /// </summary>
  public void ApplyToLayout(
      ViewportPoseVisibilityEntry entry,
      ViewportLayout layout)
  {
    if (entry == null || layout == null || layout.Pieces == null)
      return;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (TryGetEnabledByName(entry, piece.Name, out bool enabled))
        piece.Enabled = enabled;
      else
        piece.Enabled = false;

      if (TryGetMirrorByName(entry, piece.Name, out bool mirror))
        piece.MirrorHorizontally = mirror;
      else
        piece.MirrorHorizontally = false;
    }
  }

  private static bool TryGetEnabledByName(
      ViewportPoseVisibilityEntry entry,
      string pieceName,
      out bool enabled)
  {
    enabled = false;
    if (entry == null || entry.PieceNames == null || entry.EnabledFlags == null)
      return false;

    string key = pieceName ?? string.Empty;
    int count = Math.Min(entry.PieceNames.Count, entry.EnabledFlags.Count);
    for (int i = 0; i < count; i++)
    {
      if (entry.PieceNames[i] == key)
      {
        enabled = entry.EnabledFlags[i];
        return true;
      }
    }

    return false;
  }

  private static bool TryGetMirrorByName(
      ViewportPoseVisibilityEntry entry,
      string pieceName,
      out bool mirror)
  {
    mirror = false;
    if (entry == null
        || entry.PieceNames == null
        || entry.MirrorHorizontallyFlags == null
        || entry.MirrorHorizontallyFlags.Count == 0)
    {
      return false;
    }

    string key = pieceName ?? string.Empty;
    int count = Math.Min(
        entry.PieceNames.Count,
        entry.MirrorHorizontallyFlags.Count);
    for (int i = 0; i < count; i++)
    {
      if (entry.PieceNames[i] == key)
      {
        mirror = entry.MirrorHorizontallyFlags[i];
        return true;
      }
    }

    return false;
  }
}

[Serializable]
public sealed class ViewportPoseVisibilityEntry
{
  public int X;
  public int Y;
  public DungeonFacing Facing;

  public List<string> PieceNames = new();
  public List<bool> EnabledFlags = new();
  public List<bool> MirrorHorizontallyFlags = new();
}
