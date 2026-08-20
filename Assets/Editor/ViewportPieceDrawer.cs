using DM.Rendering;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ViewportPiece))]
public sealed class ViewportPieceDrawer : PropertyDrawer
{
  private const string FrontWallF1Name = "Front Wall F1";
  private const string FrontF1Name = "FrontF1";
  private const string FrontWallF2Name = "Front Wall F2";
  private const string FrontF2Name = "FrontF2";

  public override float GetPropertyHeight(
      SerializedProperty property,
      GUIContent label)
  {
    int lines = 6;
    if (IsFrontWallF1(property))
      lines++;
    if (IsFrontWallF2(property))
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
    y = DrawLine(position, y, line, gap, property, "MirrorHorizontally");

    if (IsFrontWallF1(property))
      y = DrawLine(position, y, line, gap, property, "FrontWallF1Width");

    if (IsFrontWallF2(property))
      y = DrawLine(position, y, line, gap, property, "FrontWallF2Width");

    y = DrawLine(position, y, line, gap, property, "X");
    DrawLine(position, y, line, gap, property, "Y");

    EditorGUI.EndProperty();
  }

  private static bool IsFrontWallF1(SerializedProperty property)
  {
    SerializedProperty nameProp = property.FindPropertyRelative("Name");
    if (nameProp == null)
      return false;

    string name = nameProp.stringValue;
    return name == FrontF1Name || name == FrontWallF1Name;
  }

  private static bool IsFrontWallF2(SerializedProperty property)
  {
    SerializedProperty nameProp = property.FindPropertyRelative("Name");
    if (nameProp == null)
      return false;

    string name = nameProp.stringValue;
    return name == FrontF2Name || name == FrontWallF2Name;
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
