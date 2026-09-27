using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KLTN.Game.Presentation
{
    /// <summary>
    /// Draws a moving cyan core and broken outer strands between UI card centers.
    /// It is visual-only and never intercepts selection raycasts.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class AbilityTargetLinkGraphic : MaskableGraphic
    {
        [SerializeField] private Color coreColor = new Color(0.65f, 1f, 1f, 1f);
        [SerializeField] private Color strandColor = new Color(0.18f, 0.84f, 1f, 0.72f);
        [Min(1f)] [SerializeField] private float coreWidth = 9f;
        [Min(1f)] [SerializeField] private float haloWidth = 20f;
        [Min(1f)] [SerializeField] private float strandWidth = 5f;
        [SerializeField] private float waveAmplitude = 9f;
        [SerializeField] private float waveFrequency = 17f;
        [SerializeField] private float travelSpeed = 6f;

        [Header("Prefab Preview (Editor Only)")]
        [SerializeField] private bool showPrefabPreview = true;
        [SerializeField] private Vector2 previewStartNormalized = new Vector2(0.2f, 0.35f);
        [SerializeField] private Vector2 previewEndNormalized = new Vector2(0.8f, 0.65f);

        private readonly List<RectTransform> selectedTargets = new List<RectTransform>();
        private readonly List<Rect> protectedCardBounds = new List<Rect>();
        private readonly Vector3[] cardCorners = new Vector3[4];
        private RectTransform source;
        private RectTransform hoverTarget;

#if UNITY_EDITOR
        private const double PreviewRefreshInterval = 1.0 / 30.0;
        private double nextPreviewRefresh;
#endif

        protected override void OnEnable()
        {
            base.OnEnable();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += RefreshPrefabPreview;
#endif
        }

        protected override void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= RefreshPrefabPreview;
#endif

            base.OnDisable();
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            maskable = false;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif

        public void SetLinks(
            RectTransform sourceRect,
            IReadOnlyList<RectTransform> selected,
            RectTransform hovered
        )
        {
            source = sourceRect;
            hoverTarget = hovered;
            selectedTargets.Clear();

            if (selected != null)
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    if (selected[i] != null)
                    {
                        selectedTargets.Add(selected[i]);
                    }
                }
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();

#if UNITY_EDITOR
            if (ShouldShowPrefabPreview())
            {
                Rect area = rectTransform.rect;
                DrawLink(mesh, PreviewPoint(area, previewStartNormalized),
                    PreviewPoint(area, previewEndNormalized));
                return;
            }
#endif

            if (source == null || !TryLocalCenter(source, out Vector2 start))
            {
                return;
            }

            protectedCardBounds.Clear();
            AddProtectedCardBounds(source);

            for (int i = 0; i < selectedTargets.Count; i++)
            {
                AddProtectedCardBounds(selectedTargets[i]);
            }

            if (hoverTarget != null && !selectedTargets.Contains(hoverTarget))
            {
                AddProtectedCardBounds(hoverTarget);
            }

            for (int i = 0; i < selectedTargets.Count; i++)
            {
                DrawLink(mesh, start, selectedTargets[i], protectedCardBounds);
            }

            if (hoverTarget != null && !selectedTargets.Contains(hoverTarget))
            {
                DrawLink(mesh, start, hoverTarget, protectedCardBounds);
            }
        }

        private void LateUpdate()
        {
            if (source != null && (hoverTarget != null || selectedTargets.Count > 0))
            {
                SetVerticesDirty();
            }
        }

        private void DrawLink(
            VertexHelper mesh,
            Vector2 start,
            RectTransform target,
            IReadOnlyList<Rect> protectedBounds
        )
        {
            if (target == null || !TryLocalCenter(target, out Vector2 end))
            {
                return;
            }

            DrawLink(mesh, start, end, protectedBounds);
        }

        private void DrawLink(
            VertexHelper mesh,
            Vector2 start,
            Vector2 end,
            IReadOnlyList<Rect> protectedBounds = null
        )
        {
            Vector2 control = (start + end) * 0.5f
                + Vector2.up * Mathf.Min(58f, Vector2.Distance(start, end) * 0.14f);

            const int segments = 28;
            // Negative phase moves highlights and broken strands from source to target.
            // Bound the phase so long matches never lose wave/dash precision.
            double time = Time.unscaledTime;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                time = UnityEditor.EditorApplication.timeSinceStartup;
            }
#endif

            float phase = -(float)((time * travelSpeed) % (Mathf.PI * 2.0));

            for (int strand = 0; strand < 3; strand++)
            {
                float width = strand == 0 ? coreWidth : strandWidth;
                Color tint = strand == 0 ? coreColor : strandColor;

                for (int segment = 0; segment < segments; segment++)
                {
                    int dashIndex = (
                        segment + Mathf.FloorToInt(phase * 2f) + strand * 2
                    ) % 7;

                    if (dashIndex < 0)
                    {
                        dashIndex += 7;
                    }

                    if (strand > 0 && dashIndex >= 4)
                    {
                        continue;
                    }

                    float from = segment / (float)segments;
                    float to = (segment + 1) / (float)segments;

                    Vector2 first = Sample(start, control, end, from, strand, phase);
                    Vector2 second = Sample(start, control, end, to, strand, phase);

                    if (strand == 0)
                    {
                        Color halo = coreColor;
                        halo.a *= 0.22f;
                        AddVisibleSegment(
                            mesh, first, second, haloWidth, halo, protectedBounds, 0
                        );
                    }

                    AddVisibleSegment(
                        mesh, first, second, width, tint, protectedBounds, 0
                    );
                }
            }
        }

        private void AddProtectedCardBounds(RectTransform card)
        {
            if (card == null || card.rect.width <= 0f || card.rect.height <= 0f)
            {
                return;
            }

            card.GetWorldCorners(cardCorners);
            Camera camera = ProjectionCamera();
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < cardCorners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, cardCorners[i]);

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screen, camera, out Vector2 local
                ))
                {
                    return;
                }

                minimum = Vector2.Min(minimum, local);
                maximum = Vector2.Max(maximum, local);
            }

            protectedCardBounds.Add(Rect.MinMaxRect(
                minimum.x, minimum.y, maximum.x, maximum.y
            ));
        }

#if UNITY_EDITOR
        private void RefreshPrefabPreview()
        {
            if (!ShouldShowPrefabPreview())
            {
                return;
            }

            double now = UnityEditor.EditorApplication.timeSinceStartup;

            if (now < nextPreviewRefresh)
            {
                return;
            }

            nextPreviewRefresh = now + PreviewRefreshInterval;
            SetVerticesDirty();
            UnityEditor.SceneView.RepaintAll();
        }

        private bool ShouldShowPrefabPreview()
        {
            if (Application.isPlaying || !showPrefabPreview)
            {
                return false;
            }

            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null && transform.IsChildOf(stage.prefabContentsRoot.transform);
        }

        private static Vector2 PreviewPoint(Rect area, Vector2 normalized)
        {
            return new Vector2(
                Mathf.LerpUnclamped(area.xMin, area.xMax, normalized.x),
                Mathf.LerpUnclamped(area.yMin, area.yMax, normalized.y)
            );
        }
#endif

        private Vector2 Sample(
            Vector2 start,
            Vector2 control,
            Vector2 end,
            float t,
            int strand,
            float phase
        )
        {
            float inverse = 1f - t;
            Vector2 point = inverse * inverse * start
                + 2f * inverse * t * control
                + t * t * end;

            Vector2 tangent = 2f * inverse * (control - start)
                + 2f * t * (end - control);
            Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized;
            float taper = Mathf.Sin(t * Mathf.PI);
            float wave = Mathf.Sin(t * waveFrequency + phase + strand * 2.2f);

            return point + normal * wave * waveAmplitude * taper * (strand == 0 ? 0.28f : 1f);
        }

        private bool TryLocalCenter(RectTransform target, out Vector2 local)
        {
            Camera camera = ProjectionCamera();
            Vector3 world = target.TransformPoint(target.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, world);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                screen,
                camera,
                out local
            );
        }

        private Camera ProjectionCamera()
        {
            Canvas root = GetComponentInParent<Canvas>()?.rootCanvas;
            return root != null && root.renderMode != RenderMode.ScreenSpaceOverlay
                ? root.worldCamera
                : null;
        }

        /// <summary>
        /// Removes only the portions covered by the source and chosen/hovered cards.
        /// Other cards stay beneath the beam without changing their Canvas or raycasts.
        /// </summary>
        private static void AddVisibleSegment(
            VertexHelper mesh,
            Vector2 start,
            Vector2 end,
            float width,
            Color color,
            IReadOnlyList<Rect> protectedBounds,
            int nextBound
        )
        {
            if (protectedBounds == null || nextBound >= protectedBounds.Count)
            {
                AddSegment(mesh, start, end, width, color);
                return;
            }

            if (!TryClipSegmentToRect(
                start, end, protectedBounds[nextBound], width * 0.5f + 1f,
                out float entry, out float exit
            ))
            {
                AddVisibleSegment(
                    mesh, start, end, width, color, protectedBounds, nextBound + 1
                );
                return;
            }

            Vector2 delta = end - start;

            if (entry > 0.0001f)
            {
                AddVisibleSegment(
                    mesh, start, start + delta * entry, width, color,
                    protectedBounds, nextBound + 1
                );
            }

            if (exit < 0.9999f)
            {
                AddVisibleSegment(
                    mesh, start + delta * exit, end, width, color,
                    protectedBounds, nextBound + 1
                );
            }
        }

        private static bool TryClipSegmentToRect(
            Vector2 start,
            Vector2 end,
            Rect bounds,
            float padding,
            out float entry,
            out float exit
        )
        {
            entry = 0f;
            exit = 1f;
            Vector2 delta = end - start;

            return ClipEdge(-delta.x, start.x - bounds.xMin + padding, ref entry, ref exit)
                && ClipEdge(delta.x, bounds.xMax + padding - start.x, ref entry, ref exit)
                && ClipEdge(-delta.y, start.y - bounds.yMin + padding, ref entry, ref exit)
                && ClipEdge(delta.y, bounds.yMax + padding - start.y, ref entry, ref exit);
        }

        private static bool ClipEdge(float direction, float distance, ref float entry, ref float exit)
        {
            if (Mathf.Abs(direction) < 0.000001f)
            {
                return distance >= 0f;
            }

            float crossing = distance / direction;

            if (direction < 0f)
            {
                if (crossing > exit)
                {
                    return false;
                }

                entry = Mathf.Max(entry, crossing);
            }
            else
            {
                if (crossing < entry)
                {
                    return false;
                }

                exit = Mathf.Min(exit, crossing);
            }

            return true;
        }

        private static void AddSegment(
            VertexHelper mesh,
            Vector2 start,
            Vector2 end,
            float width,
            Color color
        )
        {
            if ((end - start).sqrMagnitude < 0.000001f)
            {
                return;
            }

            Vector2 direction = (end - start).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (width * 0.5f);
            int index = mesh.currentVertCount;

            AddVertex(mesh, start - normal, color);
            AddVertex(mesh, start + normal, color);
            AddVertex(mesh, end + normal, color);
            AddVertex(mesh, end - normal, color);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }

        private static void AddVertex(VertexHelper mesh, Vector2 position, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            mesh.AddVert(vertex);
        }
    }
}
