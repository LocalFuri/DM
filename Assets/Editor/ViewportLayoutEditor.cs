using DM.Rendering;
using UnityEditor;
using UnityEngine;

public class ViewportLayoutEditor : EditorWindow
{
  private static readonly int[] SnapValues = { 1, 2, 4, 8 };

  private ViewportLayout layout;
  private Vector2 scroll;
  private bool[] rememberedEnabledStates;
  private int snap = 1;

  [MenuItem("Tools/Dungeon Master/Viewport Layout Editor")]
  public static void Open()
  {
    GetWindow<ViewportLayoutEditor>("Viewport Layout");
  }

  private void OnGUI()
  {
    EditorGUI.BeginChangeCheck();

    ViewportLayout previousLayout = layout;
    layout = (ViewportLayout)EditorGUILayout.ObjectField(
        "Viewport Layout",
        layout,
        typeof(ViewportLayout),
        false);

    if (layout != previousLayout)
      rememberedEnabledStates = null;

    if (layout == null)
    {
      EditorGUI.EndChangeCheck();
      EditorGUILayout.HelpBox("Select a ViewportLayout asset.", MessageType.Info);
      return;
    }

    DrawSnapToolbar();

    using (new EditorGUI.DisabledScope(rememberedEnabledStates == null))
    {
      if (GUILayout.Button("Restore Enabled States"))
      {
        RestoreEnabledStates();
        PersistChanges();
      }
    }

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Pieces (render order)", EditorStyles.boldLabel);

    scroll = EditorGUILayout.BeginScrollView(scroll);

    bool changed = false;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      EditorGUILayout.BeginVertical(EditorStyles.helpBox);

      piece.Name = EditorGUILayout.TextField("Name", piece.Name);
      piece.Enabled = EditorGUILayout.Toggle("Enabled", piece.Enabled);
      piece.Graphic = (DungeonGraphicType)EditorGUILayout.EnumPopup("Graphic", piece.Graphic);

      if (DrawIntStepper("X", ref piece.X, snap))
        changed = true;

      if (DrawIntStepper("Y", ref piece.Y, snap))
        changed = true;

      EditorGUILayout.BeginHorizontal();

      using (new EditorGUI.DisabledScope(i <= 0))
      {
        if (GUILayout.Button("Move Up"))
        {
          SwapPieces(i, i - 1);
          changed = true;
        }
      }

      using (new EditorGUI.DisabledScope(i >= layout.Pieces.Count - 1))
      {
        if (GUILayout.Button("Move Down"))
        {
          SwapPieces(i, i + 1);
          changed = true;
        }
      }

      if (GUILayout.Button("Solo"))
      {
        SoloPiece(i);
        PersistChanges();
      }

      EditorGUILayout.EndHorizontal();

      EditorGUILayout.EndVertical();
    }

    EditorGUILayout.EndScrollView();

    if (EditorGUI.EndChangeCheck() || changed)
      PersistChanges();
  }

  private void DrawSnapToolbar()
  {
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField("Snap", GUILayout.Width(36));

    foreach (int value in SnapValues)
    {
      bool selected = snap == value;
      GUIStyle style = selected
          ? EditorStyles.miniButtonMid
          : EditorStyles.miniButton;

      Color previousColor = GUI.backgroundColor;
      if (selected)
        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);

      if (GUILayout.Toggle(selected, value.ToString(), style, GUILayout.Width(28))
          && !selected)
      {
        snap = value;
      }

      GUI.backgroundColor = previousColor;
    }

    EditorGUILayout.EndHorizontal();
  }

  private void SoloPiece(int soloIndex)
  {
    rememberedEnabledStates = new bool[layout.Pieces.Count];

    for (int i = 0; i < layout.Pieces.Count; i++)
      rememberedEnabledStates[i] = layout.Pieces[i].Enabled;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      piece.Enabled =
          i == soloIndex || IsFloorOrCeiling(piece);
    }
  }

  private void RestoreEnabledStates()
  {
    if (rememberedEnabledStates == null)
      return;

    int count = Mathf.Min(
        rememberedEnabledStates.Length,
        layout.Pieces.Count);

    for (int i = 0; i < count; i++)
      layout.Pieces[i].Enabled = rememberedEnabledStates[i];
  }

  private static bool IsFloorOrCeiling(ViewportPiece piece)
  {
    return piece.Graphic == DungeonGraphicType.Floor
        || piece.Graphic == DungeonGraphicType.Ceiling
        || piece.Name == "Floor"
        || piece.Name == "Ceiling";
  }

  private void PersistChanges()
  {
    EditorUtility.SetDirty(layout);
    AssetDatabase.SaveAssets();
    RefreshDungeonRenderer();
  }

  private void SwapPieces(int indexA, int indexB)
  {
    ViewportPiece temp = layout.Pieces[indexA];
    layout.Pieces[indexA] = layout.Pieces[indexB];
    layout.Pieces[indexB] = temp;
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

  private static bool DrawIntStepper(string label, ref int value, int step)
  {
    EditorGUILayout.BeginHorizontal();
    value = EditorGUILayout.IntField(label, value);

    bool changed = false;

    if (GUILayout.Button($"-{step}", GUILayout.Width(36)))
    {
      value -= step;
      changed = true;
    }

    if (GUILayout.Button($"+{step}", GUILayout.Width(36)))
    {
      value += step;
      changed = true;
    }

    EditorGUILayout.EndHorizontal();
    return changed;
  }
}
