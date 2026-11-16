using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Sprite-free seat action badge: neon capsule ring, outer bloom, black fill.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasRenderer))]
    public class ActionBadgeSdfGraphic : RawImage
    {
        private static readonly int RectSizeId           = Shader.PropertyToID("_RectSize");
        private static readonly int PillSizeId           = Shader.PropertyToID("_PillSize");
        private static readonly int CornerRadiusPxId     = Shader.PropertyToID("_CornerRadiusPx");
        private static readonly int BorderWidthPxId      = Shader.PropertyToID("_BorderWidthPx");
        private static readonly int GlowSpreadPxId       = Shader.PropertyToID("_GlowSpreadPx");
        private static readonly int GlowStrengthId       = Shader.PropertyToID("_GlowStrength");
        private static readonly int GlowFalloffId        = Shader.PropertyToID("_GlowFalloff");
        private static readonly int BorderColorId        = Shader.PropertyToID("_BorderColor");
        private static readonly int FillColorTopId       = Shader.PropertyToID("_FillColorTop");
        private static readonly int FillColorBotId       = Shader.PropertyToID("_FillColorBot");
        private static readonly int HighlightColorId     = Shader.PropertyToID("_HighlightColor");
        private static readonly int HighlightStrengthId  = Shader.PropertyToID("_HighlightStrength");

        private Material _pillMaterial;

        [SerializeField] private Shader _shader;
        [SerializeField] private float _pillWidthPx  = 120f;
        [SerializeField] private float _pillHeightPx = 40f;
        [SerializeField, Range(4f, 24f)]  private float _cornerRadiusPx = 20f;
        [SerializeField, Range(1f, 8f)]   private float _borderWidthPx  = 5f;
        [SerializeField, Range(4f, 80f)]  private float _glowSpreadPx   = 32f;
        [SerializeField, Range(0f, 4f)]   private float _glowStrength    = 2.5f;
        [SerializeField, Range(0.8f, 3f)] private float _glowFalloff     = 1.5f;
        [SerializeField] private Color _borderColor = ButtonLabelStyle.RaiseText;
        [SerializeField] private Color _fillColorTop    = Color.black;
        [SerializeField] private Color _fillColorBot    = Color.black;
        [SerializeField] private Color _highlightColor  = new Color(0.92f, 0.94f, 1f, 1f);
        [SerializeField, Range(0f, 0.6f)] private float _highlightStrength = 0f;

        public Color FillColorTop
        {
            get => _fillColorTop;
            set { _fillColorTop = value; MarkRenderDirty(); }
        }

        public Color FillColorBot
        {
            get => _fillColorBot;
            set { _fillColorBot = value; MarkRenderDirty(); }
        }

        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; MarkRenderDirty(); }
        }

        public float PillWidthPx
        {
            get => _pillWidthPx;
            set { _pillWidthPx = value; MarkRenderDirty(); }
        }

        public float PillHeightPx
        {
            get => _pillHeightPx;
            set { _pillHeightPx = value; MarkRenderDirty(); }
        }

        public float CornerRadiusPx
        {
            get => _cornerRadiusPx;
            set { _cornerRadiusPx = value; MarkRenderDirty(); }
        }

        public float GlowSpreadPx
        {
            get => _glowSpreadPx;
            set { _glowSpreadPx = value; MarkRenderDirty(); }
        }

        public float GlowStrength
        {
            get => _glowStrength;
            set { _glowStrength = value; MarkRenderDirty(); }
        }

        /// <summary>Neon stadium capsule: hot tube core, dual halo, pure black fill.</summary>
        public void ApplyNeonCapsulePreset(Color accent, float rectWidthPx, float rectHeightPx)
        {
            const float glowOutset = 10f;
            const float pillHeight = 32f;
            const float minPillW   = 68f;
            _pillHeightPx      = Mathf.Max(rectHeightPx - glowOutset * 2f, pillHeight);
            _pillWidthPx       = Mathf.Max(rectWidthPx  - glowOutset * 2f, minPillW);
            _cornerRadiusPx    = _pillHeightPx * 0.5f;
            _borderWidthPx     = 3f;
            _glowSpreadPx      = 24f;
            _glowStrength      = 1.5f;
            _glowFalloff       = 1.7f;
            _borderColor       = accent;
            _fillColorTop      = Color.black;
            _fillColorBot      = Color.black;
            _highlightColor    = Color.Lerp(accent, Color.white, 0.72f);
            _highlightStrength = 0.42f;
            MarkRenderDirty();
        }

        public void ForceRefresh()
        {
            if (this == null || rectTransform == null)
                return;

            EnsureRendererSettings();
            MarkRenderDirty();
            if (!IsActive())
                return;

            UpdateGeometry();
            UpdateMaterial();
            Canvas.ForceUpdateCanvases();
        }

        protected override void UpdateMaterial()
        {
            if (!IsActive() || canvasRenderer == null)
                return;

            if (!EnsurePillMaterial())
            {
                Debug.LogWarning("[ActionBadgeSdfGraphic] UI/ActionBadgeSDF shader missing — pill will not render.", this);
                base.UpdateMaterial();
                return;
            }

            // Same direct CanvasRenderer path as avatar rings — avoids materialForRendering NRE on first enable.
            ApplyUniforms(_pillMaterial);

            canvasRenderer.materialCount = 1;
            canvasRenderer.SetMaterial(_pillMaterial, 0);
            canvasRenderer.SetTexture(mainTexture);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f)
                r = rectTransform.rect;

            Color32 c = color;
            vh.Clear();
            vh.AddVert(new Vector3(r.x,           r.y),            c, new Vector2(0, 0));
            vh.AddVert(new Vector3(r.x,           r.y + r.height), c, new Vector2(0, 1));
            vh.AddVert(new Vector3(r.x + r.width, r.y + r.height), c, new Vector2(1, 1));
            vh.AddVert(new Vector3(r.x + r.width, r.y),            c, new Vector2(1, 0));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        protected override void Awake()
        {
            base.Awake();
            texture  = null;
            maskable = false;
            EnsureRendererSettings();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureRendererSettings();
            MarkRenderDirty();
            if (Application.isPlaying && IsActive())
                ForceRefresh();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += ForceCanvasRebuildInEditor;
#endif
        }

#if UNITY_EDITOR
        private void ForceCanvasRebuildInEditor()
        {
            if (this == null || Application.isPlaying) return;
            Canvas.ForceUpdateCanvases();
        }
#endif

        protected override void OnDisable()
        {
            base.OnDisable();
            if (!Application.isPlaying)
                CleanupMaterial();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            MarkRenderDirty();
        }

        private void MarkRenderDirty()
        {
            SetVerticesDirty();
            SetMaterialDirty();
        }

        private void EnsureRendererSettings()
        {
            if (canvasRenderer != null)
                canvasRenderer.cullTransparentMesh = false;
        }

        private bool EnsurePillMaterial()
        {
            Shader shader = UiAvatarShaders.ResolveActionBadgeSdf(_shader);
            if (shader == null)
                return false;

            if (_shader == null)
                _shader = shader;

            if (_pillMaterial == null || _pillMaterial.shader != shader)
            {
                CleanupMaterial();
                _pillMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            return true;
        }

        private void CleanupMaterial()
        {
            if (_pillMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(_pillMaterial);
            else
                DestroyImmediate(_pillMaterial);

            _pillMaterial = null;
        }

        private void ApplyUniforms(Material mat)
        {
            if (mat == null)
                return;

            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                rect = rectTransform.rect;

            mat.SetVector(RectSizeId,         new Vector2(rect.width, rect.height));
            // Leave a glow margin so the outer bloom isn't hard-clipped at the rect edge.
            const float GlowMargin = 6f;
            float pw = _pillWidthPx  > 0f ? _pillWidthPx  : Mathf.Max(rect.width  - GlowMargin * 2f, 8f);
            float ph = _pillHeightPx > 0f ? _pillHeightPx : Mathf.Max(rect.height - GlowMargin * 2f, 8f);
            mat.SetVector(PillSizeId,         new Vector2(pw, ph));
            mat.SetFloat(CornerRadiusPxId,    _cornerRadiusPx);
            mat.SetFloat(BorderWidthPxId,     _borderWidthPx);
            mat.SetFloat(GlowSpreadPxId,      _glowSpreadPx);
            mat.SetFloat(GlowStrengthId,      _glowStrength);
            mat.SetFloat(GlowFalloffId,       _glowFalloff);
            mat.SetColor(BorderColorId,       _borderColor);
            mat.SetColor(FillColorTopId,      _fillColorTop);
            mat.SetColor(FillColorBotId,      _fillColorBot);
            mat.SetColor(HighlightColorId,    _highlightColor);
            mat.SetFloat(HighlightStrengthId, _highlightStrength);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureRendererSettings();
            MarkRenderDirty();
        }
#endif
    }
}
