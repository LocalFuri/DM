using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Blackjack
{
    /// <summary>
    /// Controls a single card UI element: face/back display, flip animation, and bloom glow effect.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class CardView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image cardImage;
        [SerializeField] private Image glowImage;

        [Header("Flip Animation")]
        [SerializeField] private float flipDuration = 0.35f;

        [Header("Bloom Pulse")]
        [SerializeField] private float breatheSpeed  = 0.7f;
        [SerializeField] private float flickerSpeed  = 4.5f;
        [SerializeField] private float flickerWeight = 0.35f;
        [SerializeField] private float pulseMinAlpha = 0.15f;
        [SerializeField] private float pulseMaxAlpha = 0.88f;

        private Sprite _faceSprite;
        private Sprite _backSprite;
        private bool   _isFaceUp;

        private Coroutine _flipCoroutine;
        private Coroutine _glowCoroutine;

        private float _glowNoiseOffset;

        private void Awake()
        {
            if (cardImage == null) cardImage = GetComponent<Image>();
            _glowNoiseOffset = Random.Range(0f, 100f);
        }

        public void Setup(Sprite faceSprite, Sprite backSprite, bool faceUp = true)
        {
            _faceSprite      = faceSprite;
            _backSprite      = backSprite;
            _isFaceUp        = faceUp;
            cardImage.sprite = faceUp ? _faceSprite : _backSprite;
            SetGlow(false);
        }

        public void Flip(bool toFaceUp, System.Action onComplete = null)
        {
            if (_flipCoroutine != null) StopCoroutine(_flipCoroutine);
            _flipCoroutine = StartCoroutine(FlipRoutine(toFaceUp, onComplete));
        }

        private IEnumerator FlipRoutine(bool toFaceUp, System.Action onComplete)
        {
            RectTransform rt        = GetComponent<RectTransform>();
            Vector3       origScale = rt.localScale;
            float         half      = flipDuration * 0.5f;
            float         elapsed   = 0f;

            while (elapsed < half)
            {
                elapsed       += Time.deltaTime;
                float t        = Mathf.Clamp01(elapsed / half);
                rt.localScale  = new Vector3(origScale.x * (1f - t), origScale.y, 1f);
                yield return null;
            }
            rt.localScale = new Vector3(0f, origScale.y, 1f);

            _isFaceUp        = toFaceUp;
            cardImage.sprite = toFaceUp ? _faceSprite : _backSprite;

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed       += Time.deltaTime;
                float t        = Mathf.Clamp01(elapsed / half);
                rt.localScale  = new Vector3(origScale.x * t, origScale.y, 1f);
                yield return null;
            }
            rt.localScale = origScale;

            onComplete?.Invoke();
        }

        public void SetGlow(bool enabled)
        {
            StopGlowPulse();
            if (glowImage == null) return;
            glowImage.enabled = enabled;
            glowImage.color   = new Color(3f, 2.7f, 0.5f, enabled ? pulseMaxAlpha : 0f);
        }

        public void StartGlowPulse()
        {
            if (glowImage == null) return;
            StopGlowPulse();
            glowImage.enabled = true;
            _glowCoroutine    = StartCoroutine(GlowPulseRoutine());
        }

        public void StopGlowPulse()
        {
            if (_glowCoroutine != null)
            {
                StopCoroutine(_glowCoroutine);
                _glowCoroutine = null;
            }
            if (glowImage != null)
                glowImage.enabled = false;
        }

        private IEnumerator GlowPulseRoutine()
        {
            float time = 0f;
            while (true)
            {
                time += Time.deltaTime;

                float breathe = (Mathf.Sin(time * breatheSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                float flicker = Mathf.PerlinNoise(time * flickerSpeed + _glowNoiseOffset, 0f);
                float t       = Mathf.Lerp(breathe, flicker, flickerWeight);
                float alpha   = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, t);

                Color color = Color.Lerp(
                    new Color(1.5f, 0.65f, 0.05f, alpha),
                    new Color(4.0f, 3.5f,  0.6f,  alpha),
                    t);

                glowImage.color = color;
                yield return null;
            }
        }

        public bool IsFaceUp => _isFaceUp;
    }
}
