using UnityEditor;
using UnityEngine;

namespace TexasHoldem
{
    [CustomEditor(typeof(TableLayoutManager))]
    public class TableLayoutManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty prop = serializedObject.GetIterator();
            prop.NextVisible(true); // skip m_Script

            while (prop.NextVisible(false))
            {
                EditorGUILayout.PropertyField(prop, true);

                if (prop.name == "_cardWidth")
                {
                    float cardHeight = prop.floatValue * (95f / 65f);
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.FloatField("Card Height (auto)", cardHeight);
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "HudPanel width/X come from Card_1. Per-seat HudGlow → Panel Right/Bottom Border Px (default 14) " +
                "extend the dark fill past card edges. BetAnchor / DealerButtonAnchor positions " +
                "place chip stacks and dealer token per seat — Apply Layout to refresh.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(0.45f, 0.95f, 0.55f);
            if (GUILayout.Button("Apply Layout", GUILayout.Height(32)))
            {
                var mgr = (TableLayoutManager)target;
                mgr.ApplyLayout();
                EditorUtility.SetDirty(mgr.gameObject);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
        }

        private void OnSceneGUI()
        {
            try
            {
                var mgr = (TableLayoutManager)target;
                if (mgr == null) return;

                PlayerView[] views = mgr.GetPlayerViews();
                if (views == null || views.Length == 0) return;

                var so = serializedObject;
                if (so == null) return;
                so.Update();

                var canvasProp = so.FindProperty("_canvasRect");
                if (canvasProp == null || canvasProp.objectReferenceValue == null) return;

                var canvasRect = canvasProp.objectReferenceValue as RectTransform;
                if (canvasRect == null) return;

                var seatsProp = so.FindProperty("_seats");
                if (seatsProp == null || !seatsProp.isArray) return;
                int seatCount = seatsProp.arraySize;

                float scale = canvasRect.localScale.x;

                for (int i = 0; i < views.Length && i < seatCount && i < TableLayoutManager.SeatCount; i++)
                {
                    if (views[i] == null) continue;
                    var rt = views[i].transform as RectTransform;
                    if (rt == null) continue;

                    var seatProp = seatsProp.GetArrayElementAtIndex(i);
                    if (seatProp == null) continue;
                    var sizeProp = seatProp.FindPropertyRelative("size");
                    if (sizeProp == null) continue;

                    var   size = sizeProp.vector2Value;
                    float r    = Mathf.Min(size.x, size.y) * 0.5f * scale;
                    Vector3 wPos = canvasRect.TransformPoint(
                        new Vector3(rt.anchoredPosition.x, rt.anchoredPosition.y, 0f));

                    Handles.color = i == 0
                        ? new Color(0.2f, 1f, 0.4f, 0.85f)
                        : new Color(0.3f, 0.8f, 1f, 0.85f);

                    Handles.DrawWireDisc(wPos, Vector3.forward, r);
                    string seatLabel = views[i].ResolveDisplayName();
                    if (string.IsNullOrWhiteSpace(seatLabel))
                        seatLabel = i == 0 ? "You" : $"Bot {i}";
                    Handles.Label(wPos + Vector3.up * (r + 4f * scale), seatLabel);
                }
            }
            catch (System.Exception) { /* suppress stale-assembly errors during recompile */ }
        }
    }
}
