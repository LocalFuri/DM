using System;
using System.Collections.Generic;
using DM.Dungeon;
using DM.Rendering;
using UnityEngine;

/// <summary>
/// Per-pose wall visibility (Enabled flags only). Keyed by exact X + Y + Facing.
/// Shared by Edit Mode and Play/Build. Does not store Graphic / X / Y / Mirror / order.
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

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      entry.PieceNames.Add(piece.Name ?? string.Empty);
      entry.EnabledFlags.Add(piece.Enabled);
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
}

[Serializable]
public sealed class ViewportPoseVisibilityEntry
{
  public int X;
  public int Y;
  public DungeonFacing Facing;

  public List<string> PieceNames = new();
  public List<bool> EnabledFlags = new();
}
