#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Independent V17 wall/open footprint explorer. Does not change the dungeon map,
/// scene, or production V17 renderer. Bit order matches the sampled rows in
/// ViewportLayoutEditor.BuildViewport17Inspection: D0/D1 -1..+1,
/// D2/D3 -2..+2, then the rear [0,-1] probe.
/// </summary>
public sealed class V17CombinationExplorer : EditorWindow
{
    private const int CellCount = 17;
    private const int CombinationCount = 1 << CellCount;
    private const string MaskPref = "DungeonMaster.V17CombinationExplorer.Mask";
    private const float CellWidth = 84f;
    private const float CellHeight = 55f;
    private int mask;
    private int jumpTo;
    private Vector2 scroll;

    [MenuItem("Tools/Dungeon Master/V17 Combination Explorer")]
    private static void Open()
    {
        var window = GetWindow<V17CombinationExplorer>("V17 Combinations");
        window.minSize = new Vector2(580f, 410f);
        window.Show();
    }

    private void OnEnable()
    {
        mask = Mathf.Clamp(EditorPrefs.GetInt(MaskPref, 0), 0, CombinationCount - 1);
        jumpTo = mask;
    }

    private void SetMask(int value)
    {
        mask = ((value % CombinationCount) + CombinationCount) % CombinationCount;
        jumpTo = mask;
        EditorPrefs.SetInt(MaskPref, mask);
        Repaint();
    }

    private static int GetBitIndex(int depth, int lateral)
    {
        if (depth <= 1) return depth * 3 + lateral + 1;
        return 6 + (depth - 2) * 5 + lateral + 2;
    }

    private static string GetPlayerViewLabel(int depth, int lateral)
    {
        if (depth < 0)
            return "Rear";

        string distance = "F" + depth;
        string side;
        if (lateral == 0) side = "Center";
        else if (lateral < 0)
            side = lateral == -1 ? "Left" : "Far Left";
        else
            side = lateral == 1 ? "Right" : "Far Right";

        return side + " " + distance;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("V17 WALL / OPEN COMBINATION EXPLORER", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "The 16 sampled V17 cells plus the rear-wall probe form 17 binary inputs. " +
            "W = wall, O = open. Each click changes one input. This is a geometry explorer only; " +
            "it does not yet render a 320x200 preview or alter the production renderer.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Previous", GUILayout.Width(90))) SetMask(mask - 1);
            if (GUILayout.Button("Next", GUILayout.Width(90))) SetMask(mask + 1);
            GUILayout.Label("Combination " + (mask + 1) + " / " + CombinationCount,
                EditorStyles.boldLabel, GUILayout.Width(220));
            GUILayout.FlexibleSpace();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            jumpTo = EditorGUILayout.IntField("Jump to (1–131072)", jumpTo + 1) - 1;
            if (GUILayout.Button("Go", GUILayout.Width(55)))
                SetMask(Mathf.Clamp(jumpTo, 0, CombinationCount - 1));
            if (GUILayout.Button("All open", GUILayout.Width(75))) SetMask(0);
            if (GUILayout.Button("All walls", GUILayout.Width(75))) SetMask(CombinationCount - 1);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space(8f);
        // Rear probe is the seventeenth input used for the BACK face.
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("REAR", GUILayout.Width(75));
            DrawCell(16, -1, 0);
        }
        EditorGUILayout.Space(8f);
        // Draw far-to-near, matching the player's forward-facing perspective.
        for (int depth = 3; depth >= 0; depth--)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("D" + depth, GUILayout.Width(75));
                for (int lateral = -2; lateral <= 2; lateral++)
                {
                    if (depth < 2 && Mathf.Abs(lateral) == 2)
                    {
                        GUILayout.Space(CellWidth + 4f);
                        continue;
                    }
                    DrawCell(GetBitIndex(depth, lateral), depth, lateral);
                }
            }
        }
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("PLAYER VIEW: bottom = nearest, top = farthest, left/right = as seen by the player",
            EditorStyles.boldLabel);
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Bitmask (decimal): " + mask);
        EditorGUILayout.LabelField("Bitmask (hex): 0x" + mask.ToString("X5"));
        EditorGUILayout.LabelField("Bit 0–2: D0 left/center/right; 3–5: D1; " +
                                   "6–10: D2; 11–15: D3; 16: rear.", EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    private void DrawCell(int bit, int depth, int lateral)
    {
        bool isWall = (mask & (1 << bit)) != 0;
        string viewLabel = GetPlayerViewLabel(depth, lateral);
        string coord = depth < 0
            ? "[0,-1]"
            : "[" + lateral.ToString("+0;-0;0") + "," + depth + "]";

        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = isWall ? new Color(0.95f, 0.55f, 0.45f) :
            new Color(0.55f, 0.85f, 0.55f);
        if (GUILayout.Button((isWall ? "WALL" : "OPEN") + "\n" + viewLabel + "\n" + coord + "  #" + bit,
                GUILayout.Width(CellWidth), GUILayout.Height(CellHeight + 16f)))
            SetMask(mask ^ (1 << bit));
        GUI.backgroundColor = previous;
    }

}
#endif
