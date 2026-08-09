using DM.Dungeon;
using UnityEngine;

namespace DM.Rendering
{
  public class DungeonBitmapFont : MonoBehaviour
  {
    private const int CellSize = 10;
    private const int GlyphInset = 1;
    private const int GlyphSize = 8;
    private const int GlyphAdvance = 8;

    /// <summary>
    /// Tight debug spacing: ~1px gap between typical ink widths.
    /// Does not affect HALK / player-name draws (those pass their own advance).
    /// </summary>
    public const int DebugGlyphAdvance = 5;

    /// <summary>8px glyph height for top-down → framebuffer Y conversion.</summary>
    public const int DebugGlyphHeight = GlyphSize;

    [Header("Dungeon Master Bitmap Font")]
    [SerializeField]
    private Texture2D alphabetGrid;

    public Texture2D AlphabetGrid => alphabetGrid;

    public void DrawText(
        Texture2D destination,
        string text,
        int destinationX,
        int destinationY)
    {
      if (destination == null || alphabetGrid == null)
        return;

      if (string.IsNullOrEmpty(text))
        return;

      Color32[] pixels = destination.GetPixels32();
      DrawText(
          pixels,
          destination.width,
          destination.height,
          text,
          destinationX,
          destinationY
      );
      destination.SetPixels32(pixels);
      destination.Apply(false);
    }

    public void DrawText(
        Color32[] destination,
        int destinationWidth,
        int destinationHeight,
        string text,
        int destinationX,
        int destinationY)
    {
      DrawText(
          destination,
          destinationWidth,
          destinationHeight,
          text,
          destinationX,
          destinationY,
          colour: default,
          useColour: false,
          clipX: 0,
          clipY: 0,
          clipWidth: destinationWidth,
          clipHeight: destinationHeight
      );
    }

    public void DrawText(
        Color32[] destination,
        int destinationWidth,
        int destinationHeight,
        string text,
        int destinationX,
        int destinationY,
        Color32 colour,
        int clipX,
        int clipY,
        int clipWidth,
        int clipHeight)
    {
      DrawText(
          destination,
          destinationWidth,
          destinationHeight,
          text,
          destinationX,
          destinationY,
          colour,
          useColour: true,
          clipX,
          clipY,
          clipWidth,
          clipHeight,
          GlyphAdvance
      );
    }

    public void DrawText(
        Color32[] destination,
        int destinationWidth,
        int destinationHeight,
        string text,
        int destinationX,
        int destinationY,
        Color32 colour,
        int clipX,
        int clipY,
        int clipWidth,
        int clipHeight,
        int characterAdvance)
    {
      DrawText(
          destination,
          destinationWidth,
          destinationHeight,
          text,
          destinationX,
          destinationY,
          colour,
          useColour: true,
          clipX,
          clipY,
          clipWidth,
          clipHeight,
          characterAdvance
      );
    }

    /// <summary>
    /// Compact debug line (POS / facing). Uses DebugGlyphAdvance, not name spacing.
    /// </summary>
    public void DrawDebugText(
        Color32[] destination,
        int destinationWidth,
        int destinationHeight,
        string text,
        int destinationX,
        int destinationY,
        Color32 colour)
    {
      DrawText(
          destination,
          destinationWidth,
          destinationHeight,
          text,
          destinationX,
          destinationY,
          colour,
          useColour: true,
          clipX: 0,
          clipY: 0,
          clipWidth: destinationWidth,
          clipHeight: destinationHeight,
          characterAdvance: DebugGlyphAdvance
      );
    }

    /// <summary>
    /// Shared comparison/debug pose line for Edit Mode and Play/Build.
    /// Top-down placement X=4, Y=174; black; DebugGlyphAdvance spacing.
    /// </summary>
    public void DrawPoseDebugText(
        Color32[] destination,
        int destinationWidth,
        int destinationHeight,
        int posX,
        int posY,
        DungeonFacing facing)
    {
      if (destination == null)
        return;

      const int topDownX = 4;
      const int topDownY = 174;

      int framebufferY =
          destinationHeight
          - topDownY
          - DebugGlyphHeight;

      string text =
          "POS "
          + posX
          + ","
          + posY
          + " / "
          + facing;

      DrawDebugText(
          destination,
          destinationWidth,
          destinationHeight,
          text,
          topDownX,
          framebufferY,
          new Color32(0, 0, 0, 255)
      );
    }

    private void DrawText(
        Color32[] destination,
        int destinationWidth,
        int destinationHeight,
        string text,
        int destinationX,
        int destinationY,
        Color32 colour,
        bool useColour,
        int clipX,
        int clipY,
        int clipWidth,
        int clipHeight)
    {
      DrawText(
          destination,
          destinationWidth,
          destinationHeight,
          text,
          destinationX,
          destinationY,
          colour,
          useColour,
          clipX,
          clipY,
          clipWidth,
          clipHeight,
          GlyphAdvance
      );
    }

    private void DrawText(
        Color32[] destination,
        int destinationWidth,
        int destinationHeight,
        string text,
        int destinationX,
        int destinationY,
        Color32 colour,
        bool useColour,
        int clipX,
        int clipY,
        int clipWidth,
        int clipHeight,
        int characterAdvance)
    {
      if (destination == null || alphabetGrid == null)
        return;

      if (string.IsNullOrEmpty(text))
        return;

      if (destinationWidth <= 0 || destinationHeight <= 0)
        return;

      if (destination.Length < destinationWidth * destinationHeight)
        return;

      if (clipWidth <= 0 || clipHeight <= 0)
        return;

      if (characterAdvance <= 0)
        characterAdvance = GlyphAdvance;

      string upperText = text.ToUpperInvariant();
      int drawX = destinationX;

      foreach (char character in upperText)
      {
        if (character == ' ')
        {
          drawX += characterAdvance;
          continue;
        }

        if (TryGetAtlasCharacterCell(character, out int column, out int rowFromTop))
        {
          DrawAtlasCharacter(
              destination,
              destinationWidth,
              destinationHeight,
              column,
              rowFromTop,
              drawX,
              destinationY,
              colour,
              useColour,
              clipX,
              clipY,
              clipWidth,
              clipHeight
          );
          drawX += characterAdvance;
          continue;
        }

        if (TryGetExtraGlyph(character, out byte[] rows))
        {
          DrawExtraGlyph(
              destination,
              destinationWidth,
              destinationHeight,
              rows,
              drawX,
              destinationY,
              colour,
              useColour,
              clipX,
              clipY,
              clipWidth,
              clipHeight
          );
          drawX += characterAdvance;
        }
      }
    }

    private void DrawAtlasCharacter(
        Color32[] destination,
        int destinationWidth,
        int destinationHeight,
        int column,
        int rowFromTop,
        int destinationX,
        int destinationY,
        Color32 colour,
        bool useColour,
        int clipX,
        int clipY,
        int clipWidth,
        int clipHeight)
    {
      int sourceX =
          column * CellSize + GlyphInset;

      int sourceY =
          alphabetGrid.height -
          ((rowFromTop + 1) * CellSize) +
          GlyphInset;

      Color32 background =
          alphabetGrid.GetPixel(
              sourceX,
              sourceY
          );

      int clipMaxX = clipX + clipWidth;
      int clipMaxY = clipY + clipHeight;

      for (int y = 0; y < GlyphSize; y++)
      {
        for (int x = 0; x < GlyphSize; x++)
        {
          int targetX = destinationX + x;
          int targetY = destinationY + y;

          if (
              targetX < 0 ||
              targetY < 0 ||
              targetX >= destinationWidth ||
              targetY >= destinationHeight
          )
          {
            continue;
          }

          if (
              targetX < clipX ||
              targetY < clipY ||
              targetX >= clipMaxX ||
              targetY >= clipMaxY
          )
          {
            continue;
          }

          Color32 pixel =
              alphabetGrid.GetPixel(
                  sourceX + x,
                  sourceY + y
              );

          if (IsBackground(pixel, background))
            continue;

          destination[targetY * destinationWidth + targetX] =
              useColour ? colour : pixel;
        }
      }
    }

    private static void DrawExtraGlyph(
        Color32[] destination,
        int destinationWidth,
        int destinationHeight,
        byte[] rows,
        int destinationX,
        int destinationY,
        Color32 colour,
        bool useColour,
        int clipX,
        int clipY,
        int clipWidth,
        int clipHeight)
    {
      int clipMaxX = clipX + clipWidth;
      int clipMaxY = clipY + clipHeight;
      Color32 ink = useColour
          ? colour
          : new Color32(255, 255, 255, 255);

      for (int y = 0; y < GlyphSize; y++)
      {
        // rows[0] is top of glyph; framebuffer Y increases upward.
        int rowIndex = GlyphSize - 1 - y;
        byte bits = rows[rowIndex];

        for (int x = 0; x < GlyphSize; x++)
        {
          if ((bits & (1 << (GlyphSize - 1 - x))) == 0)
            continue;

          int targetX = destinationX + x;
          int targetY = destinationY + y;

          if (
              targetX < 0 ||
              targetY < 0 ||
              targetX >= destinationWidth ||
              targetY >= destinationHeight
          )
          {
            continue;
          }

          if (
              targetX < clipX ||
              targetY < clipY ||
              targetX >= clipMaxX ||
              targetY >= clipMaxY
          )
          {
            continue;
          }

          destination[targetY * destinationWidth + targetX] = ink;
        }
      }
    }

    private static bool TryGetAtlasCharacterCell(
        char character,
        out int column,
        out int rowFromTop)
    {
      column = 0;
      rowFromTop = 0;

      if (character >= 'A' && character <= 'Z')
      {
        int index = character - 'A';

        if (index <= 10)
        {
          rowFromTop = 0;
          column = index;
          return true;
        }

        if (index <= 20)
        {
          rowFromTop = 1;
          column = index - 11;
          return true;
        }

        rowFromTop = 2;
        column = index - 21;
        return true;
      }

      // Row 2 after Z: comma, period, semicolon, colon.
      switch (character)
      {
        case ',':
          rowFromTop = 2;
          column = 5;
          return true;
        case '.':
          rowFromTop = 2;
          column = 6;
          return true;
        case ';':
          rowFromTop = 2;
          column = 7;
          return true;
        case ':':
          rowFromTop = 2;
          column = 8;
          return true;
        default:
          return false;
      }
    }

    // Digits and slash are not in alphabet_grid; 8×8 patterns match DM letter weight.
    private static bool TryGetExtraGlyph(char character, out byte[] rows)
    {
      switch (character)
      {
        case '0':
          rows = Glyph0;
          return true;
        case '1':
          rows = Glyph1;
          return true;
        case '2':
          rows = Glyph2;
          return true;
        case '3':
          rows = Glyph3;
          return true;
        case '4':
          rows = Glyph4;
          return true;
        case '5':
          rows = Glyph5;
          return true;
        case '6':
          rows = Glyph6;
          return true;
        case '7':
          rows = Glyph7;
          return true;
        case '8':
          rows = Glyph8;
          return true;
        case '9':
          rows = Glyph9;
          return true;
        case '/':
          rows = GlyphSlash;
          return true;
        default:
          rows = null;
          return false;
      }
    }

    private static bool IsBackground(
        Color32 pixel,
        Color32 background)
    {
      const int tolerance = 3;

      return
          Mathf.Abs(pixel.r - background.r) <= tolerance &&
          Mathf.Abs(pixel.g - background.g) <= tolerance &&
          Mathf.Abs(pixel.b - background.b) <= tolerance;
    }

    // Top-down bit rows; bit7 = left. Matches ~DM letter ink placement.
    private static readonly byte[] Glyph0 =
        { 0x00, 0x3C, 0x66, 0x6E, 0x76, 0x66, 0x3C, 0x00 };
    private static readonly byte[] Glyph1 =
        { 0x00, 0x18, 0x38, 0x18, 0x18, 0x18, 0x3C, 0x00 };
    private static readonly byte[] Glyph2 =
        { 0x00, 0x3C, 0x66, 0x0C, 0x18, 0x30, 0x7E, 0x00 };
    private static readonly byte[] Glyph3 =
        { 0x00, 0x3C, 0x66, 0x0C, 0x06, 0x66, 0x3C, 0x00 };
    private static readonly byte[] Glyph4 =
        { 0x00, 0x0C, 0x1C, 0x2C, 0x4C, 0x7E, 0x0C, 0x00 };
    private static readonly byte[] Glyph5 =
        { 0x00, 0x7E, 0x60, 0x7C, 0x06, 0x66, 0x3C, 0x00 };
    private static readonly byte[] Glyph6 =
        { 0x00, 0x1C, 0x30, 0x7C, 0x66, 0x66, 0x3C, 0x00 };
    private static readonly byte[] Glyph7 =
        { 0x00, 0x7E, 0x06, 0x0C, 0x18, 0x18, 0x18, 0x00 };
    private static readonly byte[] Glyph8 =
        { 0x00, 0x3C, 0x66, 0x3C, 0x66, 0x66, 0x3C, 0x00 };
    private static readonly byte[] Glyph9 =
        { 0x00, 0x3C, 0x66, 0x66, 0x3E, 0x0C, 0x38, 0x00 };
    private static readonly byte[] GlyphSlash =
        { 0x00, 0x06, 0x0C, 0x18, 0x30, 0x60, 0x00, 0x00 };
  }
}
