using System.Collections;
using Game.Audio;
using Game.Economy;
using Game.Fishing;
using Game.Fishing.Config;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Fishing
{
    /// <summary>
    /// 钓获奖励提示：鱼图标、名称、价值；首次发现显示未解锁标记。
    /// </summary>
    public sealed class FishingCatchRewardToastView : MonoBehaviour
    {
        [SerializeField] private Image fishIcon;
        [SerializeField] private Text fishNameText;
        [SerializeField] private Text coinValueText;
        [SerializeField] private GameObject undiscoveredBadge;
        [SerializeField] private float lifetimeSeconds = 3f;
        [SerializeField] private float fadeOutSeconds = 0.45f;

        CanvasGroup _canvasGroup;
        Coroutine _routine;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (fishIcon == null)
                fishIcon = transform.Find("FishIcon")?.GetComponent<Image>();
            if (fishNameText == null)
                fishNameText = transform.Find("FishName")?.GetComponent<Text>();
            if (coinValueText == null)
                coinValueText = transform.Find("CoinValue")?.GetComponent<Text>();
            if (undiscoveredBadge == null)
            {
                var badge = transform.Find("UndiscoveredBadge");
                if (badge != null)
                    undiscoveredBadge = badge.gameObject;
            }
        }

        public void Show(
            FishingService.CatchResult result,
            SfxId settleSfxId = SfxId.FishingCatchSettle,
            Vector3? settleSoundWorldPosition = null)
        {
            if (!result.success || result.fish == null)
            {
                Destroy(gameObject);
                return;
            }

            gameObject.SetActive(true);
            AudioGameplayFeedback.PlayCatchSettleSound(settleSfxId, settleSoundWorldPosition);
            ApplyContent(result.fish, result.coinsAwarded, result.wasFirstDiscovery);

            if (_routine != null)
                StopCoroutine(_routine);
            _routine = StartCoroutine(CoLifetime());
        }

        void ApplyContent(FishDef fish, long coinsAwarded, bool showUndiscoveredBadge)
        {
            if (fishNameText != null)
                fishNameText.text = string.IsNullOrEmpty(fish.displayName) ? fish.fishId : fish.displayName;

            if (coinValueText != null)
                coinValueText.text = EconomyDisplayFormat.Format(coinsAwarded < 0 ? 0 : coinsAwarded);

            if (fishIcon != null)
            {
                fishIcon.sprite = fish.icon;
                fishIcon.enabled = fish.icon != null;
            }

            if (undiscoveredBadge != null)
                undiscoveredBadge.SetActive(showUndiscoveredBadge);
        }

        IEnumerator CoLifetime()
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;

            var hold = Mathf.Max(0.2f, lifetimeSeconds - fadeOutSeconds);
            yield return new WaitForSeconds(hold);

            var fade = Mathf.Max(0.05f, fadeOutSeconds);
            var elapsed = 0f;
            while (elapsed < fade)
            {
                elapsed += Time.deltaTime;
                if (_canvasGroup != null)
                    _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fade);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
