using System;
using System.Collections.Generic;

namespace DM.Rendering
{
  /// <summary>
  /// Named reusable viewport wall visibility pattern.
  /// Controls temporary draw visibility only — never written to ViewportLayout.
  /// </summary>
  [Serializable]
  public class ViewportPatternDefinition
  {
    public string PatternId;
    public string GeometryKey;
    public List<DungeonGraphicType> VisibleGraphics =
        new List<DungeonGraphicType>();

    public bool ContainsGraphic(DungeonGraphicType graphic)
    {
      if (VisibleGraphics == null)
        return false;

      for (int i = 0; i < VisibleGraphics.Count; i++)
      {
        if (VisibleGraphics[i] == graphic)
          return true;
      }

      return false;
    }
  }
}
