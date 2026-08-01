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

      string upperText = text.ToUpperInvariant();
      int drawX = destinationX;

      foreach (char character in upperText)
      {
        if (character == ' ')
        {
          drawX += GlyphAdvance;
          continue;
        }

        if (character < 'A' || character > 'Z')
          continue;

        DrawCharacter(
            destination,
            character,
            drawX,
            destinationY
        );

        drawX += GlyphAdvance;
      }

      destination.Apply(false);
    }

    private void DrawCharacter(
        Texture2D destination,
        char character,
        int destinationX,
        int destinationY)
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

      for (int y = 0; y < GlyphSize; y++)
      {
        for (int x = 0; x < GlyphSize; x++)
        {
          int targetX = destinationX + x;
          int targetY = destinationY + y;

          if (
              targetX < 0 ||
              targetY < 0 ||
              targetX >= destination.width ||
              targetY >= destination.height
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

          destination.SetPixel(
              targetX,
              targetY,
              pixel
          );
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