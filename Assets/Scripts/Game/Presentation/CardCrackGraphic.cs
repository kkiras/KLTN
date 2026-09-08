using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KLTN.Game.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CardCrackGraphic : MaskableGraphic
    {
        #region Serialized Fields

        [Min(0.5f)]
        [SerializeField] private float lineWidth = 3f;

        [Range(4, 12)]
        [SerializeField] private int primaryBranchCount = 7;

        [Range(2, 5)]
        [SerializeField] private int segmentsPerBranch = 4;

        #endregion

        #region Runtime State

        private readonly List<CrackSegment> segments = new List<CrackSegment>();

        private int currentSeed;
        private float revealProgress;
        private bool configured;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            if (!configured) { return; }

            GenerateSegments(currentSeed);
            SetVerticesDirty();
        }

        #endregion

        #region Public API

        public void Configure(int seed)
        {
            currentSeed = seed;
            configured = true;
            revealProgress = 0f;

            GenerateSegments(seed);
            SetVerticesDirty();
        }

        public void SetReveal(float progress)
        {
            revealProgress = Mathf.Clamp01(progress);
            SetVerticesDirty();
        }

        #endregion

        #region Mesh Generation

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            if (segments.Count == 0 || revealProgress <= 0f) { return; }

            float exactVisibleCount = segments.Count * revealProgress;

            int completeCount = Mathf.FloorToInt(exactVisibleCount);

            for (int i = 0; i < completeCount; i++)
            {
                AddLine(vertexHelper, segments[i].Start, segments[i].End);
            }

            if (completeCount >= segments.Count) { return; }

            float partialProgress = exactVisibleCount - completeCount;

            if (partialProgress <= 0f) { return; }

            CrackSegment segment = segments[completeCount];

            AddLine(vertexHelper,segment.Start, Vector2.Lerp(segment.Start, segment.End, partialProgress));
        }

        private void AddLine(VertexHelper vertexHelper, Vector2 start, Vector2 end)
        {
            Vector2 direction = end - start;

            if (direction.sqrMagnitude <= 0.001f) { return; }

            direction.Normalize();

            Vector2 normal =new Vector2(-direction.y, direction.x) * lineWidth * 0.5f;

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            var vertices = new UIVertex[4];

            vertex.position = start - normal;
            vertices[0] = vertex;

            vertex.position = start + normal;
            vertices[1] = vertex;

            vertex.position = end + normal;
            vertices[2] = vertex;

            vertex.position = end - normal;
            vertices[3] = vertex;

            vertexHelper.AddUIVertexQuad(vertices);
        }

        #endregion

        #region Crack Pattern

        private void GenerateSegments(int seed)
        {
            segments.Clear();

            Rect bounds = rectTransform.rect;

            if (bounds.width <= 0f || bounds.height <= 0f) { return; }

            var random = new System.Random(seed);

            Vector2 center = new Vector2(
                RandomRange(random, bounds.xMin * 0.15f, bounds.xMax * 0.15f),

                RandomRange(random, bounds.yMin * 0.15f, bounds.yMax * 0.15f));

            float radius = Mathf.Min(bounds.width, bounds.height) * 0.56f;

            for (int branch = 0; branch < primaryBranchCount; branch++)
            {
                float angle = Mathf.PI * 2f * branch / primaryBranchCount + RandomRange(random, -0.28f, 0.28f);

                Vector2 current = center;

                for (int segmentIndex = 0; segmentIndex < segmentsPerBranch; segmentIndex++)
                {
                    angle += RandomRange(random, -0.22f, 0.22f);

                    float length = radius / segmentsPerBranch * RandomRange(random, 0.75f, 1.2f);

                    Vector2 next = current + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;

                    next = ClampToRect(next, bounds);
                    segments.Add(new CrackSegment(current, next));

                    if (segmentIndex > 0 && random.NextDouble() < 0.7d)
                    {
                        AddSideBranch(random, current, angle, length, bounds);
                    }

                    current = next;
                }
            }
        }

        private void AddSideBranch(System.Random random, Vector2 origin, float mainAngle, float mainLength, Rect bounds)
        {
            float sideSign = random.NextDouble() < 0.5d ? -1f : 1f;

            float angle = mainAngle + sideSign * RandomRange(random, 0.55f, 1.05f);

            float length =mainLength * RandomRange(random, 0.4f, 0.75f);

            Vector2 end = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;

            segments.Add(new CrackSegment(origin, ClampToRect(end, bounds)));
        }

        #endregion

        #region Helpers

        private static Vector2 ClampToRect(Vector2 point, Rect bounds)
        {
            return new Vector2(
                Mathf.Clamp(point.x, bounds.xMin, bounds.xMax),
                Mathf.Clamp(point.y, bounds.yMin, bounds.yMax)
            );
        }

        private static float RandomRange(System.Random random, float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }

        #endregion

        private readonly struct CrackSegment
        {
            public Vector2 Start { get; }
            public Vector2 End { get; }

            public CrackSegment(Vector2 start, Vector2 end)
            {
                Start = start;
                End = end;
            }
        }
    }
}