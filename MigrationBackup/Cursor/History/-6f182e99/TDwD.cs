using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace TexasHoldem
{
    /// <summary>SDF action pill above the player name when a player Checks, Folds, Raises, Calls, or All-In.</summary>
    public class ActionBadge : MonoBehaviour
    {
        private const string BadgeFontFallbackResourcesPath = "Fonts & Materials/LiberationSans SDF - Fallback";
        private const string BadgeFontPrimaryResourcesPath  = "Fonts & Materials/LiberationSans SDF";
        private const float  DisplayDurationSecs            = 3f;
        private const float  LabelFontSize                  = 21f;
        private static readonly Color WinColor = new Color(1f, 0.88f, 0.15f, 1f);

        private static readonly NumberFormatInfo GermanNFI = new NumberFormatInfo
        {
            NumberGroupSeparator   = ".",
            NumberDecimalSeparator = ",",
            NumberDecimalDigits    = 0,
            NumberGroupSizes       = new[] { 3 }
        };

        private static TMP_FontAsset _badgeFont;

        [SerializeField] private ActionBadgeSdfGraphic _pillGraphic;
        [SerializeField] private TMP_Text              _label;

        private Coroutine _layoutRefreshCoroutine;

        private void Awake()
        {
            ActionBadgeUtility.Repair(gameObject, this);
            ResolveReferences();
            Hide();
        }

        private void ResolveReferences()
        {
            _pillGraphic ??= GetComponent<ActionBadgeSdfGraphic>();

            if (_label == null)
            {
                Transform t = transform.Find("Label");
                if (t != null)
                    _label = t.GetComponent<TMP_Text>();
            }
        }

        /// <summary>Shows the badge for the given betting action.</summary>
        public void Show(BettingAction action, int amount = 0)
        {
            PresentBadge(ActionColors.For(action), FormatLabel(action));
        }

        /// <summary>Hides the badge immediately.</summary>
        public void Hide()
        {
            CancelInvoke(nameof(Hide));

            if (_layoutRefreshCoroutine != null)
            {
                StopCoroutine(_layoutRefreshCoroutine);
                _layoutRefreshCoroutine = null;
            }

            gameObject.SetActive(false);
        }

        /// <summary>Shows a gold WIN badge that stays visible for <paramref name="duration"/> seconds.</summary>
        public void ShowWin(int potAmount, float duration)
        {
            string text = potAmount > 0
                ? "WIN \u20AC " + potAmount.ToString("N0", GermanNFI)
                : "WIN!";
            PresentBadge(WinColor, text, duration);
        }

        private void PresentBadge(Color accent, string text, float duration = DisplayDurationSecs)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            enabled = true;
            ActionBadgeUtility.Repair(gameObject, this);
            ResolveReferences();

            ApplyAccent(accent);
            ApplyLabel(accent, text);
            BringToFrontOfSeat();
            RefreshVisuals();
            ScheduleLayoutRefresh();

            CancelInvoke(nameof(Hide));
            Invoke(nameof(Hide), duration);
        }

        private void ScheduleLayoutRefresh()
        {
            if (!isActiveAndEnabled)
                return;

            if (_layoutRefreshCoroutine != null)
                StopCoroutine(_layoutRefreshCoroutine);

            _layoutRefreshCoroutine = StartCoroutine(RefreshAfterLayout());
        }

        private IEnumerator RefreshAfterLayout()
        {
            yield return null;
            if (this != null && gameObject.activeInHierarchy)
                RefreshVisuals();
            _layoutRefreshCoroutine = null;
        }

        private void ApplyAccent(Color accent)
        {
            if (_pillGraphic != null)
                _pillGraphic.BorderColor = accent;
        }

        private void ApplyLabel(Color accent, string text)
        {
            if (_label == null)
                return;

            TMP_FontAsset font = ResolveBadgeFont();
            if (font != null && font.material != null)
            {
                _label.font               = font;
                _label.fontSharedMaterial = font.material;
                ButtonLabelStyle.Apply(_label, accent, LabelFontSize);
            }
            else
            {
                _label.color             = accent;
                _label.fontSize          = LabelFontSize;
                _label.fontStyle         = FontStyles.Bold;
                _label.enableAutoSizing  = false;
                _label.alignment         = TextAlignmentOptions.Center;
            }

            _label.text = text;
            _label.gameObject.SetActive(true);
        }

        private void RefreshVisuals()
        {
            if (_pillGraphic != null)
            {
                _pillGraphic.enabled = true;
                _pillGraphic.color   = Color.white;
                _pillGraphic.ForceRefresh();
            }

            if (_label != null)
                _label.ForceMeshUpdate(true);

            Canvas.ForceUpdateCanvases();
        }

        private static TMP_FontAsset ResolveBadgeFont()
        {
            if (_badgeFont != null)
                return _badgeFont;

            _badgeFont = Resources.Load<TMP_FontAsset>(BadgeFontFallbackResourcesPath)
                      ?? Resources.Load<TMP_FontAsset>(BadgeFontPrimaryResourcesPath);

#if UNITY_EDITOR
            if (_badgeFont == null)
            {
                _badgeFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset");
            }
#endif
            return _badgeFont;
        }

        /// <summary>Draws above cards, name, and bet chip display on the seat.</summary>
        public void BringToFront()
        {
            Transform parent = transform.parent;
            if (parent == null)
                return;

            transform.SetAsLastSibling();
        }

        private void BringToFrontOfSeat() => BringToFront();

        private static string FormatLabel(BettingAction action) => action switch
        {
            BettingAction.Fold  => "Fold",
            BettingAction.Check => "Check",
            BettingAction.Call  => "Call",
            BettingAction.Raise => "Raise",
            BettingAction.AllIn => "All In",
            _                   => action.ToString()
        };
    }
}
