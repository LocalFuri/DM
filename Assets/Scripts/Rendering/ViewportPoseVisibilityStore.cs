using System;
using System.Collections.Generic;
using DM.Dungeon;
using DM.Rendering;
using UnityEngine;

/// <summary>
/// Per-pose Enabled + MirrorHorizontally flags. Keyed by exact X + Y + Facing.
/// Shared by Edit Mode and Play/Build. Does not store Graphic / X / Y / order.
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

      // Fixed HoC entrance overlay — not authored per pose.
      if (IsBlackDoorPiece(piece))
        continue;

      entry.PieceNames.Add(piece.Name ?? string.Empty);
      // Ceiling Strip 84/85 stay muted until we decide their role.
      entry.EnabledFlags.Add(
          IsTemporarilyMutedPiece(piece.Name) ? false : piece.Enabled);
      entry.MirrorHorizontallyFlags.Add(piece.MirrorHorizontally);
    }
  }

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

      if (TryGetMirrorByName(entry, piece.Name, out bool mirror))
        piece.MirrorHorizontally = mirror;

      // Ceiling Strip 84/85 stay muted until we decide their role.
      if (IsTemporarilyMutedPiece(piece.Name))
        piece.Enabled = false;
    }
  }

  /// <summary>
  /// Investigation pieces kept non-drawing without deleting layout entries.
  /// </summary>
  private static bool IsTemporarilyMutedPiece(string pieceName)
  {
    return pieceName == "Ceiling Strip 84"
        || pieceName == "Ceiling Strip 85";
  }

  private static bool IsBlackDoorPiece(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    return piece.Graphic == DungeonGraphicType.BlackDoor
        || piece.Name == "Black Door";
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
