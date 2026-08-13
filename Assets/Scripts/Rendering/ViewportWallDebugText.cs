using System.Collections.Generic;
using System.Text;
using DM.Dungeon;
using UnityEngine;

namespace DM.Rendering
{
  public readonly struct ViewportWallDebugEntry
  {
    public readonly string Name;
    public readonly bool MirrorHorizontally;

    public ViewportWallDebugEntry(string name, bool mirrorHorizontally)
    {
      Name = name;
      MirrorHorizontally = mirrorHorizontally;
    }

    public string Format()
    {
      if (!MirrorHorizontally)
        return Name;

      return Name + " (M)";
    }
  }

  /// <summary>
  /// Console listing helpers. Lists every Enabled pose-controlled layout piece
  /// in layout order (not a walls-only subset).
  /// </summary>
  public static class ViewportWallDebugText
  {
    /// <summary>
    /// Preferred ordering when present; remaining Enabled pieces follow in
    /// layout order after these.
    /// </summary>
    public static readonly string[] OrderedWallNames =
    {
      "Wall F0Left",
      "Wall F0Right",
      "Wall F1Left",
      "Front Wall F1 A",
      "Front Wall F1 B",
      "Wall F1Right",
      "Wall F2Left",
      "Front Wall F2",
      "Wall F2Right",
      "Wall F3Left",
      "Front Wall F3",
      "Wall F3Right",
      "Ceiling",
      "Floor",
      "Black Door Frame Left",
      "Black Door Frame Right",
      "Black Door"
    };

    public static string FormatConsoleLine(
        int posX,
        int posY,
        DungeonFacing facing,
        IReadOnlyList<ViewportWallDebugEntry> walls)
    {
      StringBuilder builder = new StringBuilder(128);
      builder.Append(posX);
      builder.Append(',');
      builder.Append(posY);
      builder.Append(' ');
      builder.Append(facing.ToString().ToUpperInvariant());

      if (walls != null)
      {
        for (int i = 0; i < walls.Count; i++)
        {
          builder.Append(" | ");
          builder.Append(walls[i].Format());
        }
      }

      return builder.ToString();
    }

    public static void CollectEnabledFromLayout(
        ViewportLayout layout,
        List<ViewportWallDebugEntry> results)
    {
      results.Clear();
      if (layout == null || layout.Pieces == null)
        return;

      HashSet<string> added = new HashSet<string>();

      for (int i = 0; i < OrderedWallNames.Length; i++)
      {
        string name = OrderedWallNames[i];
        ViewportPiece piece = FindPieceByName(layout, name);
        if (piece == null || !piece.Enabled)
          continue;

        results.Add(
            new ViewportWallDebugEntry(
                piece.Name,
                piece.MirrorHorizontally
            )
        );
        added.Add(piece.Name ?? string.Empty);
      }

      // Any other Enabled pose-controlled pieces (not in the preferred list).
      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece == null || !piece.Enabled)
          continue;

        string name = piece.Name ?? string.Empty;
        if (added.Contains(name))
          continue;

        // Live UI chrome is positioned, not a viewport blit piece.
        if (piece.Graphic == DungeonGraphicType.MovementArrows)
          continue;

        results.Add(
            new ViewportWallDebugEntry(
                name,
                piece.MirrorHorizontally
            )
        );
        added.Add(name);
      }
    }

    public static ViewportPiece FindPieceByName(
        ViewportLayout layout,
        string name)
    {
      if (layout == null || layout.Pieces == null || string.IsNullOrEmpty(name))
        return null;

      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece != null && piece.Name == name)
          return piece;
      }

      return null;
    }

    public static bool IsOrderedWallName(string name)
    {
      if (string.IsNullOrEmpty(name))
        return false;

      for (int i = 0; i < OrderedWallNames.Length; i++)
      {
        if (OrderedWallNames[i] == name)
          return true;
      }

      return false;
    }
  }
}
