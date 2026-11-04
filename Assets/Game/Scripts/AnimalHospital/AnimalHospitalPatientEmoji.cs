using Game.AnimalHospital.Config;
using UnityEngine;

namespace Game.AnimalHospital
{
    /// <summary>
    /// 控制病人头顶 EmojiCanvas 下的表情显示。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalHospitalPatientEmoji : MonoBehaviour
    {
        const string EmojiCanvasName = "EmojiCanvas";
        const string AttackedNameToken = "！！！";
        const string QuestionNameToken = "？？？";
        const string LovedNameToken = "❤";

        [Header("Emoji Objects（留空则自动从 EmojiCanvas 子物体解析）")]
        [SerializeField] private GameObject attackedEmoji;
        [SerializeField] private GameObject questionEmoji;
        [SerializeField] private GameObject lovedEmoji;

        [Header("Timing")]
        [Min(0.1f)]
        [SerializeField] private float attackedDisplaySeconds = 2f;
        [Min(0.1f)]
        [SerializeField] private float questionDisplaySeconds = 2.5f;
        [Min(1f)]
        [SerializeField] private float randomCheckIntervalSeconds = 15f;
        [Range(0f, 1f)]
        [SerializeField] private float randomTriggerChance = 0.25f;

        AnimalHospitalPatient _patient;
        float _hideTimer = -1f;
        float _randomTimer;
        bool _lovedLocked;
        bool _attackedLocked;

        void Awake()
        {
            _patient = GetComponent<AnimalHospitalPatient>();
            ResolveEmojiObjects();
            HideAll();
        }

        public void ApplyConfig(AnimalHospitalConfig config)
        {
            if (config == null)
                return;

            attackedDisplaySeconds = config.patientEmojiAttackedDuration;
            questionDisplaySeconds = config.patientEmojiQuestionDuration;
            randomCheckIntervalSeconds = config.patientEmojiRandomIntervalSeconds;
            randomTriggerChance = config.patientEmojiRandomChance;
        }

        void Update()
        {
            if (_patient == null)
                return;

            // 生命归零离院途中仍保持受伤表情，不跑随机问号逻辑。
            if (_attackedLocked)
            {
                EnsureAttackedVisible();
                return;
            }

            if (!_patient.IsAlive)
                return;

            TickHideTimer();
            TickRandomQuestion();
        }

        public void ShowAttacked()
        {
            if (_lovedLocked || _attackedLocked)
                return;

            if (_patient == null)
                _patient = GetComponent<AnimalHospitalPatient>();

            ResolveEmojiObjects();
            var canvas = FindEmojiCanvas();
            if (canvas != null && !canvas.gameObject.activeSelf)
                canvas.gameObject.SetActive(true);

            ShowExclusive(attackedEmoji, attackedDisplaySeconds);
        }

        /// <summary>生命归零离院：受伤表情锁定常亮，直到物体销毁。</summary>
        public void ShowAttackedPersistent()
        {
            _attackedLocked = true;
            _lovedLocked = false;
            _hideTimer = -1f;

            if (_patient == null)
                _patient = GetComponent<AnimalHospitalPatient>();

            ResolveEmojiObjects();
            var canvas = FindEmojiCanvas();
            if (canvas != null && !canvas.gameObject.activeSelf)
                canvas.gameObject.SetActive(true);

            ShowExclusive(attackedEmoji, -1f);
        }

        public void ShowTreatmentComplete()
        {
            if (_attackedLocked)
                return;

            _lovedLocked = true;
            _hideTimer = -1f;
            ShowExclusive(lovedEmoji, -1f);
        }

        void EnsureAttackedVisible()
        {
            ResolveEmojiObjects();
            if (attackedEmoji != null && !attackedEmoji.activeSelf)
                ShowExclusive(attackedEmoji, -1f);
        }

        void TickRandomQuestion()
        {
            if (_lovedLocked || _patient == null)
                return;

            if (!IsInWaitingOrReceptionArea())
                return;

            if (IsEmojiVisible(attackedEmoji) || IsEmojiVisible(questionEmoji))
                return;

            _randomTimer += Time.deltaTime;
            if (_randomTimer < randomCheckIntervalSeconds)
                return;

            _randomTimer = 0f;
            if (Random.value > randomTriggerChance)
                return;

            ShowExclusive(questionEmoji, questionDisplaySeconds);
        }

        bool IsInWaitingOrReceptionArea()
        {
            var phase = _patient.Phase;
            return phase == AnimalHospitalPatientPhase.Waiting
                   || phase == AnimalHospitalPatientPhase.AtReception;
        }

        void ShowExclusive(GameObject target, float durationSeconds)
        {
            if (target == null)
                return;

            HideAllExcept(target);
            target.SetActive(true);
            _hideTimer = durationSeconds > 0f ? durationSeconds : -1f;
        }

        void TickHideTimer()
        {
            if (_lovedLocked || _attackedLocked || _hideTimer < 0f)
                return;

            _hideTimer -= Time.deltaTime;
            if (_hideTimer > 0f)
                return;

            _hideTimer = -1f;
            HideAll();
        }

        void HideAll()
        {
            if (_attackedLocked)
            {
                HideIfPresent(questionEmoji);
                HideIfPresent(lovedEmoji);
                if (attackedEmoji != null)
                    attackedEmoji.SetActive(true);
                return;
            }

            if (_lovedLocked)
            {
                HideIfPresent(attackedEmoji);
                HideIfPresent(questionEmoji);
                if (lovedEmoji != null)
                    lovedEmoji.SetActive(true);
                return;
            }

            HideAllExcept(null);
        }

        void HideAllExcept(GameObject keep)
        {
            if (attackedEmoji != null && attackedEmoji != keep)
                attackedEmoji.SetActive(false);
            if (questionEmoji != null && questionEmoji != keep)
                questionEmoji.SetActive(false);
            if (lovedEmoji != null && lovedEmoji != keep)
                lovedEmoji.SetActive(false);
        }

        static void HideIfPresent(GameObject go)
        {
            if (go != null)
                go.SetActive(false);
        }

        static bool IsEmojiVisible(GameObject go) => go != null && go.activeSelf;

        void ResolveEmojiObjects()
        {
            if (attackedEmoji != null && questionEmoji != null && lovedEmoji != null)
                return;

            var canvas = FindEmojiCanvas();
            if (canvas == null)
                return;

            for (var i = 0; i < canvas.childCount; i++)
            {
                var child = canvas.GetChild(i);
                if (child == null)
                    continue;

                var name = child.name;
                if (name.Contains("(1)"))
                    continue;

                if (attackedEmoji == null && name.Contains(AttackedNameToken))
                    attackedEmoji = child.gameObject;
                else if (questionEmoji == null && name.Contains(QuestionNameToken))
                    questionEmoji = child.gameObject;
                else if (lovedEmoji == null && name.Contains(LovedNameToken))
                    lovedEmoji = child.gameObject;
            }
        }

        Transform FindEmojiCanvas()
        {
            var roots = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == EmojiCanvasName)
                    return roots[i];
            }

            return null;
        }
    }
}
