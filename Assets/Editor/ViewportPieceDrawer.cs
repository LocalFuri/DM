using DM.Rendering;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ViewportPiece))]
public sealed class ViewportPieceDrawer : PropertyDrawer
{
  private const string FrontWallF1Name = "Front Wall F1";

  public override float GetPropertyHeight(
      SerializedProperty property,
      GUIContent label)
  {
    int lines = 5;
    if (!IsAuthoredSideWall(property))
      lines++;
    if (IsFrontWallF1(property))
      lines++;

    return (lines * EditorGUIUtility.singleLineHeight)
        + ((lines - 1) * EditorGUIUtility.standardVerticalSpacing);
  }

  public override void OnGUI(
      Rect position,
      SerializedProperty property,
      GUIContent label)
  {
    EditorGUI.BeginProperty(position, label, property);

    float y = position.y;
    float line = EditorGUIUtility.singleLineHeight;
    float gap = EditorGUIUtility.standardVerticalSpacing;

    y = DrawLine(position, y, line, gap, property, "Name");
    y = DrawLine(position, y, line, gap, property, "Enabled");
    y = DrawLine(position, y, line, gap, property, "Graphic");

    if (!IsAuthoredSideWall(property))
      y = DrawLine(position, y, line, gap, property, "MirrorHorizontally");

    if (IsFrontWallF1(property))
      y = DrawLine(position, y, line, gap, property, "FrontWallF1Width");

    y = DrawLine(position, y, line, gap, property, "X");
    DrawLine(position, y, line, gap, property, "Y");

    EditorGUI.EndProperty();
  }

  private static bool IsFrontWallF1(SerializedProperty property)
  {
    SerializedProperty nameProp = property.FindPropertyRelative("Name");
    return nameProp != null && nameProp.stringValue == FrontWallF1Name;
  }

  private static bool IsAuthoredSideWall(SerializedProperty property)
  {
    SerializedProperty nameProp = property.FindPropertyRelative("Name");
    if (nameProp != null)
    {
      string name = nameProp.stringValue;
      if (name == "Wall F0Left"
          || name == "Wall F0Right"
          || name == "Wall F2Left"
          || name == "Wall F2Right"
          || name == "Wall F3Left"
          || name == "Wall F3Right")
      {
        return true;
      }
    }

    SerializedProperty graphicProp = property.FindPropertyRelative("Graphic");
    if (graphicProp == null)
      return false;

    return StraightF1WallLogic.IsAuthoredSideWallGraphic(
        (DungeonGraphicType)graphicProp.intValue);
  }

  private static float DrawLine(
      Rect position,
      float y,
      float line,
      float gap,
      SerializedProperty property,
      string fieldName)
  {
    SerializedProperty field = property.FindPropertyRelative(fieldName);
    if (field != null)
    {
      EditorGUI.PropertyField(
          new Rect(position.x, y, position.width, line),
          field);
    }

    return y + line + gap;
  }
}
