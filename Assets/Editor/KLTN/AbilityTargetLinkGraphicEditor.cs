using KLTN.Game.Presentation;
using UnityEditor;
using UnityEngine;

namespace KLTN.Game.Editor
{
    /// <summary>Lets artists position the sample link directly in Prefab Mode.</summary>
    [CustomEditor(typeof(AbilityTargetLinkGraphic))]
    public sealed class AbilityTargetLinkGraphicEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox(
                "In Prefab Mode, the line below is a preview. Select Beams and drag "
                + "the Start/End handles in Scene view. Colors, widths and motion "
                + "settings above affect the in-game link; preview points do not.",
                MessageType.Info
            );
        }

        private void OnSceneGUI()
        {
            var graphic = (AbilityTargetLinkGraphic)target;
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();

            if (stage == null || !graphic.transform.IsChildOf(stage.prefabContentsRoot.transform))
            {
                return;
            }

            serializedObject.Update();
            DrawPointHandle(graphic, "Start", "previewStartNormalized", Color.cyan);
            DrawPointHandle(graphic, "End", "previewEndNormalized", Color.yellow);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPointHandle(
            AbilityTargetLinkGraphic graphic,
            string label,
            string propertyName,
            Color color
        )
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Rect area = graphic.rectTransform.rect;
            Vector2 normalized = property.vector2Value;
            Vector3 local = new Vector3(
                Mathf.LerpUnclamped(area.xMin, area.xMax, normalized.x),
                Mathf.LerpUnclamped(area.yMin, area.yMax, normalized.y)
            );
            Vector3 world = graphic.transform.TransformPoint(local);

            using (new Handles.DrawingScope(color))
            {
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(world, Quaternion.identity);

                if (EditorGUI.EndChangeCheck() && area.width != 0f && area.height != 0f)
                {
                    Vector3 next = graphic.transform.InverseTransformPoint(moved);
                    property.vector2Value = new Vector2(
                        (next.x - area.xMin) / area.width,
                        (next.y - area.yMin) / area.height
                    );
                    serializedObject.ApplyModifiedProperties();
                    graphic.SetVerticesDirty();
                }

                Handles.Label(world, label);
            }
        }
    }
}
