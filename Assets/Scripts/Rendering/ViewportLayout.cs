using System;
using System.Collections.Generic;
using UnityEngine;

namespace DM.Rendering
{
  [CreateAssetMenu(
      fileName = "ViewportLayout",
      menuName = "Dungeon Master/Viewport Layout")]
  public class ViewportLayout : ScriptableObject
  {
    [Header("Viewport")]
    public int Width = 320;
    public int Height = 200;

    [Header("Render Order")]
    public List<ViewportPiece> Pieces = new();
  }

  [Serializable]
  public class ViewportPiece
  {
    public string Name;

    public DungeonGraphicType Graphic;

    public int X;
    public int Y;

    public bool Enabled = true;
  }
}