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

      return Name + " MIRRORED";
    }
  }

  /// <summary>
  /// Wall debug listing helpers (F0→F3). Console formatting for Play/Build.
  /// </summary>
  public static class ViewportWallDebugText
  {
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
      "Wall F3Right"
    };

    public static string FormatConsoleLine(
        int posX,
        int posY,
        DungeonFacing facing,
        IReadOnlyList<ViewportWallDebugEntry> walls)
    {
      StringBuilder builder = new StringBuilder(128);
      builder.Append("POS (");
      builder.Append(posX);
      builder.Append(',');
      builder.Append(posY);
      builder.Append(") ");
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
