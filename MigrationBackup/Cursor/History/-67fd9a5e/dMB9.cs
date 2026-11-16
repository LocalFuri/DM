using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Blackjack
{
    /// <summary>
    /// Tiny dealer poke/check on the hole card — lift and slight Y squash, no visible rotation.
    /// </summary>
    [DisallowMultipleComponent]
    public class DealerPeekAnimation : MonoBehaviour
    {
        private const string LegacyPeekCornerName = "PeekCorner";

        [Header("Motion")]
        [SerializeField] private float liftPixels = 2f;
        [SerializeField] private float pressScaleY = 0.985f;

        [Header("Timing")]
        [SerializeField] private float liftDuration = 0.06f;
        [SerializeField] private float holdDuration = 0.08f;
        [SerializeField] private float returnDuration = 0.08f;

        private RectTransform _rect;
        private LayoutElement _layoutElement;
        private bool _savedIgnoreLayout;
        private bool _isPeeking;

        private void Awake()
        {
            _rect = transform as RectTransform;
            RemoveLegacyPeekCorner();
        }

        public IEnumerator PlayPeekRoutine()
        {
            yield return DealerPeekRoutine();
        }

        public IEnumerator DealerPeekRoutine()
        {
            if (_isPeeking || _rect == null)
                yield break;

            if (_rect.parent is RectTransform layoutRoot)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
                Canvas.ForceUpdateCanvases();
            }

            yield return null;

            _isPeeking = true;

            Vector3 restPos = _rect.localPosition;
            Quaternion restRot = _rect.localRotation;
            Vector3 restScale = _rect.localScale;

            Vector3 peakPos = restPos + new Vector3(0f, liftPixels, 0f);
            Vector3 peakScale = new Vector3(restScale.x, restScale.y * pressScaleY, restScale.z);

            _layoutElement = GetComponent<LayoutElement>();
            if (_layoutElement == null)
                _layoutElement = gameObject.AddComponent<LayoutElement>();

            _savedIgnoreLayout = _layoutElement.ignoreLayout;
            _layoutElement.ignoreLayout = true;

            _rect.localPosition = restPos;
            _rect.localRotation = restRot;
            _rect.localScale = restScale;

            yield return LerpPoke(restPos, restScale, peakPos, peakScale, liftDuration);
            yield return new WaitForSeconds(holdDuration);
            yield return LerpPoke(peakPos, peakScale, restPos, restScale, returnDuration);

            _rect.localPosition = restPos;
            _rect.localRotation = restRot;
            _rect.localScale = restScale;

            _layoutElement.ignoreLayout = _savedIgnoreLayout;
            _isPeeking = false;
        }

        private IEnumerator LerpPoke(
            Vector3 fromPos, Vector3 fromScale,
            Vector3 toPos, Vector3 toScale,
            float duration)
        {
            if (duration <= 0f)
            {
                _rect.localPosition = toPos;
                _rect.localScale = toScale;
                yield break;
            }

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float p = SmoothStep(Mathf.Clamp01(t / duration));
                _rect.localPosition = Vector3.Lerp(fromPos, toPos, p);
                _rect.localScale = Vector3.Lerp(fromScale, toScale, p);
                yield return null;
            }

            _rect.localPosition = toPos;
            _rect.localScale = toScale;
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);

        private void RemoveLegacyPeekCorner()
        {
            Transform legacy = transform.Find(LegacyPeekCornerName);
            if (legacy != null)
                Destroy(legacy.gameObject);
        }
    }
}
