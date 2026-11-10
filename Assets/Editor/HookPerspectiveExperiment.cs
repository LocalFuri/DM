// Standalone, non-invasive perspective experiment for Dungeon Master ornaments.
// Install: Assets/Editor/HookPerspectiveExperiment.cs
// Open: Tools > Hook Perspective Experiment
#if UNITY_EDITOR
using System;
using System.IO;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class HookPerspectiveExperiment : EditorWindow
{
    private const string HookPath = "Assets/Art/Ornaments/Hook_Side_16x19.png";
    private const int FrameHeight = 200;
    private readonly int[] widths = {16, 6, 3};
    private readonly int[] heights = {19, 12, 5};
    private readonly int[] existingBottomY = {107, 117, 122};
    private readonly string[] slots = {"S1", "S2", "S3"};

    private float horizonY = 72f;
    private float verticalFov = 65f;
    private float mountAboveEye = 0f;
    private float firstDepth = 1.5f;
    private float tileDepth = 1f;
    private bool showExisting = true;
    private Texture2D hook;
    private Vector2 scroll;

    [MenuItem("Tools/Hook Perspective Experiment")]
    private static void Open()
    {
        GetWindow<HookPerspectiveExperiment>("Hook S1-S3 Projection").minSize = new Vector2(650, 520);
    }

    private void OnEnable()
    {
        hook = AssetDatabase.LoadAssetAtPath<Texture2D>(HookPath);
    }

    // Pinhole camera projection, on the project's 200-pixel-high framebuffer.
    // With zero camera pitch, y = horizon - focalPixels * (worldHeight / depth).
    // This projects the Hook's mounting point; 2D sprite height remains unchanged.
    private float ProjectTop(int i)
    {
        float focalPixels = FrameHeight / (2f * Mathf.Tan(verticalFov * Mathf.Deg2Rad * 0.5f));
        float depth = firstDepth + tileDepth * i;
        return horizonY - focalPixels * mountAboveEye / depth;
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Hook S1 → S2 → S3: positioning only", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Non-invasive experiment. Uses existing 16×19 / 6×12 / 3×5 artwork sizes. " +
            "No dungeon geometry, renderer, saved calibration, mirroring or texture scaling is changed. " +
            "Framebuffer Y is bottom-origin in the existing code.", MessageType.Info);

        horizonY = EditorGUILayout.Slider("Projected horizon Y (top-origin)", horizonY, 40f, 110f);
        verticalFov = EditorGUILayout.Slider("Virtual vertical FOV (degrees)", verticalFov, 35f, 100f);
        mountAboveEye = EditorGUILayout.Slider("Mount height above eye (tiles)", mountAboveEye, -0.5f, 0.5f);
        firstDepth = EditorGUILayout.Slider("S1 distance (tiles)", firstDepth, 0.5f, 4f);
        tileDepth = EditorGUILayout.Slider("Distance per step (tiles)", tileDepth, 0.5f, 2f);
        showExisting = EditorGUILayout.Toggle("Show reference outline", showExisting);
        if (GUILayout.Button("Reset to common mounting anchor"))
        {
            horizonY = 72; verticalFov = 65; mountAboveEye = 0;
            firstDepth = 1.5f; tileDepth = 1;
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Existing vs 3D-projected placement (whole 320×200 Game View)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Slot", GUILayout.Width(50));
        EditorGUILayout.LabelField("Size", GUILayout.Width(80));
        EditorGUILayout.LabelField("Existing top", GUILayout.Width(110));
        EditorGUILayout.LabelField("Projected top", GUILayout.Width(110));
        EditorGUILayout.LabelField("Projected stored Y", GUILayout.Width(140));
        EditorGUILayout.LabelField("Delta", GUILayout.Width(70));
        EditorGUILayout.EndHorizontal();
        for (int i = 0; i < 3; i++)
        {
            int originalTop = FrameHeight - existingBottomY[i] - heights[i];
            float newTop = ProjectTop(i);
            int roundedTop = Mathf.RoundToInt(newTop);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(slots[i], GUILayout.Width(50));
            EditorGUILayout.LabelField(widths[i] + " × " + heights[i], GUILayout.Width(80));
            EditorGUILayout.LabelField(originalTop.ToString(), GUILayout.Width(110));
            EditorGUILayout.LabelField(newTop.ToString("F2", CultureInfo.InvariantCulture), GUILayout.Width(110));
            EditorGUILayout.LabelField((FrameHeight - roundedTop - heights[i]).ToString(), GUILayout.Width(140));
            EditorGUILayout.LabelField((newTop - originalTop).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture), GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Visual comparison (4× magnification; top-origin positions)", EditorStyles.boldLabel);
        if (hook == null)
        {
            EditorGUILayout.HelpBox("Missing Hook artwork: " + HookPath + ". Numeric experiment still works.", MessageType.Warning);
        }
        else
        {
            const float zoom = 4f;
            const float boxWidth = 188f;
            const float previewHeight = 205f;
            Rect area = GUILayoutUtility.GetRect(600f, previewHeight);
            for (int i = 0; i < 3; i++)
            {
                Rect pane = new Rect(area.x + i * (boxWidth + 8f), area.y + 4, boxWidth, previewHeight - 8);
                EditorGUI.DrawRect(pane, new Color(0.13f, 0.17f, 0.20f));
                GUI.Label(new Rect(pane.x + 8, pane.y + 4, 130, 18), slots[i]);
                float baseY = pane.y + 25f;
                float top = ProjectTop(i);
                // Shift the preview coordinates so screen Y=60 starts the box.
                float displayedY = baseY + (top - 60f) * zoom;
                Rect spriteRect = new Rect(pane.x + 56f, displayedY, widths[i] * zoom, heights[i] * zoom);
                // Do not mutate the imported asset's filter mode.
                GUI.DrawTexture(spriteRect, hook, ScaleMode.StretchToFill, true);
                if (showExisting)
                {
                    int referenceTop = FrameHeight - existingBottomY[i] - heights[i];
                    Rect reference = new Rect(pane.x + 55f, baseY + (referenceTop - 60f) * zoom - 1f,
                        widths[i] * zoom + 2f, heights[i] * zoom + 2f);
                    Handles.BeginGUI();
                    Handles.color = Color.yellow;
                    Handles.DrawSolidRectangleWithOutline(reference, new Color(0,0,0,0), Color.yellow);
                    Handles.EndGUI();
                }
            }
        }
        EditorGUILayout.HelpBox(
            "Yellow outlines = current source-code placement. Sprites = 3D-derived placement. " +
            "Preview sprite stretching is illustrative; actual texture sampling and artwork are untouched.", MessageType.None);
        if (GUILayout.Button("Export experiment CSV to Assets/Editor")) ExportCsv();
        EditorGUILayout.EndScrollView();
    }

    private void ExportCsv()
    {
        try
        {
            string path = Path.Combine(Application.dataPath, "Editor/HookPerspectiveExperiment.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            StringBuilder output = new StringBuilder();
            output.AppendLine("slot,width,height,existing_bottom_y,existing_top_y,projected_top_y,projected_bottom_y,delta_top_y,horizon_y,fov_degrees,mount_above_eye,depth_tiles");
            for (int i = 0; i < 3; i++)
            {
                int existingTop = FrameHeight - existingBottomY[i] - heights[i];
                float projected = ProjectTop(i);
                int newBottom = FrameHeight - Mathf.RoundToInt(projected) - heights[i];
                output.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5:F4},{6},{7:F4},{8:F2},{9:F2},{10:F3},{11:F3}\n",
                    slots[i], widths[i], heights[i], existingBottomY[i], existingTop,
                    projected, newBottom, projected - existingTop, horizonY,
                    verticalFov, mountAboveEye, firstDepth + tileDepth * i);
            }
            File.WriteAllText(path, output.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log("Hook perspective experiment CSV saved: " + path);
        }
        catch (Exception e) { Debug.LogError("Hook projection CSV export failed: " + e); }
    }
}
#endif
