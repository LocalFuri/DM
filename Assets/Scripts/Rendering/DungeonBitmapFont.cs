using UnityEngine;

namespace DM.Rendering
{
  public class DungeonBitmapFont : MonoBehaviour
  {
    private const int CellSize = 10;
    private const int GlyphInset = 1;
    private const int GlyphSize = 8;
    private const int GlyphAdvance = 8;

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

        if (character < 'A' || character > 'Z')
          continue;

        DrawCharacter(
            destination,
            destinationWidth,
            destinationHeight,
            character,
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

    private void DrawCharacter(
        Color32[] destination,
        int destinationWidth,
        int destinationHeight,
        char character,
        int destinationX,
        int destinationY,
        Color32 colour,
        bool useColour,
        int clipX,
        int clipY,
        int clipWidth,
        int clipHeight)
    {
      GetCharacterCell(
          character,
          out int column,
          out int rowFromTop
      );

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

    private static void GetCharacterCell(
        char character,
        out int column,
        out int rowFromTop)
    {
      int index = character - 'A';

      if (index <= 10)
      {
        rowFromTop = 0;
        column = index;
        return;
      }

      if (index <= 20)
      {
        rowFromTop = 1;
        column = index - 11;
        return;
      }

      rowFromTop = 2;
      column = index - 21;
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
  }
}
