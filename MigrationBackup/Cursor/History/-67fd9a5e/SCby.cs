using System.Collections;
using UnityEngine;

namespace Blackjack
{
    /// <summary>
    /// Simulates a dealer peeking under the hole card without revealing its value.
    /// Rotates around the left edge (Y-axis hinge); the card back stays visible throughout.
    /// </summary>
    [DisallowMultipleComponent]
    public class DealerPeekAnimation : MonoBehaviour
    {
        private static readonly int PeekTrigger = Animator.StringToHash("Peek");

        [Header("References")]
        [Tooltip("RectTransform that rotates. Defaults to this object's RectTransform.")]
        [SerializeField] private RectTransform peekPivot;

        [Tooltip("Optional CardView on the same card — keeps the back sprite visible during peek.")]
        [SerializeField] private CardView cardView;

        [Header("Animator (optional)")]
        [Tooltip("When assigned and Use Animator is enabled, drives the Peek state instead of the coroutine.")]
        [SerializeField] private Animator animator;

        [SerializeField] private bool useAnimator;

        [SerializeField] private string peekStateName = "Peek";

        [Header("Timing")]
        [SerializeField] [Range(0.3f, 0.5f)] private float duration = 0.4f;

        [Header("Rotation")]
        [Tooltip("Peak Y rotation in degrees. ~80–85° shows mostly the thin card edge.")]
        [SerializeField] [Range(60f, 89f)] private float maxYRotation = 82f;

        private Coroutine _peekCoroutine;
        private Vector2 _savedPivot;
        private Vector2 _savedAnchoredPosition;
        private Quaternion _savedLocalRotation;
        private bool _pivotAdjusted;

        private void Awake()
        {
            if (peekPivot == null)
                peekPivot = transform as RectTransform;
        }

        private void Reset()
        {
            cardView = GetComponent<CardView>();
            peekPivot = transform as RectTransform;
        }

        /// <summary>
        /// Plays the dealer peek animation once. Call when the dealer upcard is Ace or 10-value.
        /// </summary>
        public void PlayPeekAnimation()
        {
            if (_peekCoroutine != null)
                StopCoroutine(_peekCoroutine);

            _peekCoroutine = StartCoroutine(PlayPeekAnimationRoutine());
        }

        /// <summary>Coroutine-friendly wrapper — yields until the peek finishes.</summary>
        public IEnumerator PlayPeekAnimationRoutine()
        {
            if (peekPivot == null)
                yield break;

            EnsureBackVisible();
            BeginLeftEdgePivot();

            if (useAnimator && animator != null)
            {
                animator.ResetTrigger(PeekTrigger);
                animator.SetTrigger(PeekTrigger);
                yield return WaitForAnimatorClip();
            }
            else
            {
                yield return PeekMotionRoutine();
            }

            RestorePivotAndRotation();
            _peekCoroutine = null;
        }

        private IEnumerator WaitForAnimatorClip()
        {
            yield return null;

            if (animator == null)
                yield break;

            int stateHash = Animator.StringToHash(peekStateName);
            float timeout = duration + 0.5f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash == stateHash && info.normalizedTime >= 1f)
                    break;

                EnsureBackVisible();
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator PeekMotionRoutine()
        {
            float half = duration * 0.5f;

            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                float angle = maxYRotation * SmoothStep(Mathf.Clamp01(t / half));
                peekPivot.localRotation = _savedLocalRotation * Quaternion.Euler(0f, angle, 0f);
                EnsureBackVisible();
                yield return null;
            }

            peekPivot.localRotation = _savedLocalRotation * Quaternion.Euler(0f, maxYRotation, 0f);
            EnsureBackVisible();
            yield return null;

            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                float angle = maxYRotation * (1f - SmoothStep(Mathf.Clamp01(t / half)));
                peekPivot.localRotation = _savedLocalRotation * Quaternion.Euler(0f, angle, 0f);
                EnsureBackVisible();
                yield return null;
            }
        }

        private void EnsureBackVisible()
        {
            cardView?.ShowBackOnly();
        }

        private void BeginLeftEdgePivot()
        {
            _savedPivot            = peekPivot.pivot;
            _savedAnchoredPosition = peekPivot.anchoredPosition;
            _savedLocalRotation    = peekPivot.localRotation;

            Vector2 leftPivot = new Vector2(0f, 0.5f);
            if (_savedPivot != leftPivot)
            {
                Vector2 delta = new Vector2(
                    peekPivot.rect.width * (leftPivot.x - _savedPivot.x),
                    peekPivot.rect.height * (leftPivot.y - _savedPivot.y));

                peekPivot.pivot = leftPivot;
                peekPivot.anchoredPosition = _savedAnchoredPosition + delta;
                _pivotAdjusted = true;
            }
            else
            {
                _pivotAdjusted = false;
            }
        }

        private void RestorePivotAndRotation()
        {
            if (peekPivot == null)
                return;

            peekPivot.localRotation = _savedLocalRotation;

            if (_pivotAdjusted)
            {
                peekPivot.pivot = _savedPivot;
                peekPivot.anchoredPosition = _savedAnchoredPosition;
            }
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);

        /*
         * Trigger this from BlackjackGame.DealRound() immediately after the initial four cards are dealt
         * and _dealerHoleCardView is assigned, BEFORE natural-blackjack checks:
         *
         *   if (ShouldDealerPeek())
         *   {
         *       DealerPeekAnimation peek = _dealerHoleCardView.GetComponent<DealerPeekAnimation>();
         *       if (peek != null)
         *           yield return peek.PlayPeekAnimationRoutine();
         *   }
         *
         * ShouldDealerPeek() returns true when the dealer upcard is Ace or 10-value (10/J/Q/K).
         * Do not call CardView.Flip() on the hole card during the peek.
         */
    }
}
