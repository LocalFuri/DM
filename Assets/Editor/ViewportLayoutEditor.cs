using DM.Rendering;
using UnityEditor;
using UnityEngine;

public class ViewportLayoutEditor : EditorWindow
{
  private ViewportLayout layout;
  private Vector2 scroll;

  [MenuItem("Tools/Dungeon Master/Viewport Layout Editor")]
  public static void Open()
  {
    GetWindow<ViewportLayoutEditor>("Viewport Layout");
  }

  private void OnGUI()
  {
    EditorGUI.BeginChangeCheck();
    layout = (ViewportLayout)EditorGUILayout.ObjectField(
        "Viewport Layout",
        layout,
        typeof(ViewportLayout),
        false);

    if (layout == null)
    {
      EditorGUI.EndChangeCheck();
      EditorGUILayout.HelpBox("Select a ViewportLayout asset.", MessageType.Info);
      return;
    }

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Pieces (render order)", EditorStyles.boldLabel);

    scroll = EditorGUILayout.BeginScrollView(scroll);

    bool stepped = false;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      EditorGUILayout.BeginVertical(EditorStyles.helpBox);

      piece.Name = EditorGUILayout.TextField("Name", piece.Name);
      piece.Enabled = EditorGUILayout.Toggle("Enabled", piece.Enabled);
      piece.Graphic = (DungeonGraphicType)EditorGUILayout.EnumPopup("Graphic", piece.Graphic);

      if (DrawIntStepper("X", ref piece.X))
        stepped = true;

      if (DrawIntStepper("Y", ref piece.Y))
        stepped = true;

      EditorGUILayout.EndVertical();
    }

    EditorGUILayout.EndScrollView();

    if (EditorGUI.EndChangeCheck() || stepped)
    {
      EditorUtility.SetDirty(layout);
      AssetDatabase.SaveAssets();
      RefreshDungeonRenderer();
    }
  }

  private static void RefreshDungeonRenderer()
  {
    if (!Application.isPlaying)
      return;

    DungeonRenderer renderer =
        Object.FindAnyObjectByType<DungeonRenderer>();

    if (renderer == null)
      return;

    renderer.RequestRedraw();
  }

  private static bool DrawIntStepper(string label, ref int value)
  {
    EditorGUILayout.BeginHorizontal();
    value = EditorGUILayout.IntField(label, value);

    bool changed = false;

    if (GUILayout.Button("-1", GUILayout.Width(32)))
    {
      value--;
      changed = true;
    }

    if (GUILayout.Button("+1", GUILayout.Width(32)))
    {
      value++;
      changed = true;
    }

    EditorGUILayout.EndHorizontal();
    return changed;
  }
}
