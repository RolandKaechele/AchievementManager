#if ACHIEVEMENTMANAGER_DOTWEEN
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace AchievementManager.Runtime
{
    /// <summary>
    /// Optional bridge that animates achievement unlock notifications and progress bar fills
    /// using DOTween Pro.
    /// Enable define <c>ACHIEVEMENTMANAGER_DOTWEEN</c> in Player Settings › Scripting Define Symbols.
    /// Requires <b>DOTween Pro</b>.
    /// <para>
    /// Assign <see cref="notificationRoot"/> to the root <see cref="RectTransform"/> of your
    /// achievement toast panel and optionally <see cref="progressBarFill"/> to an <see cref="Image"/>
    /// with <c>fillMethod = Horizontal</c> for animated progress fills.
    /// </para>
    /// </summary>
    [AddComponentMenu("AchievementManager/DOTween Bridge")]
    [DisallowMultipleComponent]
    public class DotweenAchievementBridge : MonoBehaviour
    {
        [Header("Unlock Notification")]
        [Tooltip("Root RectTransform of the achievement toast notification panel.")]
        [SerializeField] private RectTransform notificationRoot;

        [Tooltip("CanvasGroup on the notification panel root used for fade.")]
        [SerializeField] private CanvasGroup notificationGroup;

        [Tooltip("Pixel offset below the final position from which the notification slides up.")]
        [SerializeField] private float slideUpOffset = 60f;

        [Tooltip("Duration for the notification to slide and fade in.")]
        [SerializeField] private float notifyInDuration = 0.35f;

        [Tooltip("How long the notification stays visible before fading out.")]
        [SerializeField] private float notifyHoldDuration = 2.5f;

        [Tooltip("Duration for the notification to fade out.")]
        [SerializeField] private float notifyOutDuration = 0.25f;

        [Tooltip("DOTween ease for the notification slide-in.")]
        [SerializeField] private Ease notifyEase = Ease.OutBack;

        [Header("Progress Bar")]
        [Tooltip("Image component with fill method Horizontal, used to display achievement progress bar.")]
        [SerializeField] private Image progressBarFill;

        [Tooltip("Duration to animate progress bar fill changes.")]
        [SerializeField] private float progressFillDuration = 0.4f;

        [Tooltip("DOTween ease for progress bar fill animation.")]
        [SerializeField] private Ease progressFillEase = Ease.OutCubic;

        // -------------------------------------------------------------------------

        private AchievementManager _am;
        private Sequence           _notifySequence;

        private void Awake()
        {
            _am = GetComponent<AchievementManager>() ?? FindFirstObjectByType<AchievementManager>();
            if (_am == null) Debug.LogWarning("[AchievementManager/DotweenAchievementBridge] AchievementManager not found.");

            if (notificationRoot != null && notificationGroup != null)
                notificationGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            if (_am == null) return;
            _am.OnAchievementUnlocked += OnAchievementUnlocked;
            _am.OnProgressUpdated     += OnProgressUpdated;
        }

        private void OnDisable()
        {
            if (_am == null) return;
            _am.OnAchievementUnlocked -= OnAchievementUnlocked;
            _am.OnProgressUpdated     -= OnProgressUpdated;
        }

        // -------------------------------------------------------------------------

        private void OnAchievementUnlocked(string id)
        {
            if (notificationRoot == null) return;

            _notifySequence?.Kill();
            _notifySequence = DOTween.Sequence();

            Vector2 finalPos = notificationRoot.anchoredPosition;
            notificationRoot.anchoredPosition = finalPos + Vector2.down * slideUpOffset;

            if (notificationGroup != null) notificationGroup.alpha = 0f;

            _notifySequence
                .Join(notificationRoot.DOAnchorPos(finalPos, notifyInDuration).SetEase(notifyEase));

            if (notificationGroup != null)
                _notifySequence.Join(notificationGroup.DOFade(1f, notifyInDuration));

            _notifySequence
                .AppendInterval(notifyHoldDuration);

            if (notificationGroup != null)
                _notifySequence.Append(notificationGroup.DOFade(0f, notifyOutDuration));
        }

        private void OnProgressUpdated(string id, int current, int target)
        {
            if (progressBarFill == null || target <= 0) return;

            float fillAmount = Mathf.Clamp01((float)current / target);
            DOTween.Kill(progressBarFill);
            progressBarFill.DOFillAmount(fillAmount, progressFillDuration).SetEase(progressFillEase);
        }
    }
}
#else
namespace AchievementManager.Runtime
{
    /// <summary>No-op stub — enable define <c>ACHIEVEMENTMANAGER_DOTWEEN</c> to activate.</summary>
    [UnityEngine.AddComponentMenu("AchievementManager/DOTween Bridge")]
    [UnityEngine.DisallowMultipleComponent]
    public class DotweenAchievementBridge : UnityEngine.MonoBehaviour { }
}
#endif
