using Game.Core;
using UnityEngine;

namespace Game.SupermarketOrganize
{
    /// <summary>底部技能栏：四技能按钮 + 360° Image 径向冷却。</summary>
    [DisallowMultipleComponent]
    public sealed class SupermarketOrganizeSkillHud : MonoBehaviour
    {
        [SerializeField] private GameObject sectionRoot;
        [SerializeField] private SupermarketOrganizeSkillSlotUi[] skillSlots;

        SupermarketOrganizeSkillController _controller;

        void OnEnable()
        {
            ApplyProductModeVisibility();
            BindController();
            SupermarketOrganizeProgressionService.SkillUpgraded += OnSkillUpgraded;
        }

        void OnDisable()
        {
            UnbindController();
            SupermarketOrganizeProgressionService.SkillUpgraded -= OnSkillUpgraded;
        }

        void OnSkillUpgraded(SupermarketSkillId skillId, int level) => RefreshAll();

        void Update()
        {
            RefreshCooldowns();
            TryHandleHotkeys();
        }

        void TryHandleHotkeys()
        {
            if (_controller == null || skillSlots == null)
                return;

            if (SupermarketOrganizeCursorLock.IsSettingsVisible())
                return;

            if (!SupermarketOrganizeCursorLock.IsChallengeRunning())
                return;

            for (var i = 0; i < skillSlots.Length && i < 4; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha1 + i))
                    continue;

                if (skillSlots[i] != null)
                    skillSlots[i].UseFromHotkey();
            }
        }

        public void Bind(SupermarketOrganizeSkillController controller)
        {
            UnbindController();
            _controller = controller;
            if (_controller != null)
                _controller.Changed += OnControllerChanged;

            BindSlots();
            RefreshAll();
        }

        void BindController()
        {
            if (_controller != null)
                return;

            var director = SupermarketOrganizeDirector.Instance
                           ?? FindObjectOfType<SupermarketOrganizeDirector>();
            if (director?.SkillController != null)
                Bind(director.SkillController);
        }

        void UnbindController()
        {
            if (_controller != null)
                _controller.Changed -= OnControllerChanged;
            _controller = null;
        }

        void BindSlots()
        {
            if (skillSlots == null)
                return;

            for (var i = 0; i < skillSlots.Length; i++)
            {
                if (skillSlots[i] == null)
                    continue;

                skillSlots[i].SetHotkeyNumber(i + 1);
                skillSlots[i].Bind(_controller);
            }
        }

        void OnControllerChanged() => RefreshAll();

        public void RefreshAll()
        {
            ApplyProductModeVisibility();
            if (skillSlots == null)
                return;

            for (var i = 0; i < skillSlots.Length; i++)
            {
                if (skillSlots[i] == null)
                    continue;

                skillSlots[i].SetHotkeyNumber(i + 1);
                skillSlots[i].RefreshVisual();
            }
        }

        void RefreshCooldowns()
        {
            if (skillSlots == null)
                return;

            for (var i = 0; i < skillSlots.Length; i++)
                skillSlots[i]?.RefreshCooldownVisual();
        }

        void ApplyProductModeVisibility()
        {
            var active = GameplayModuleContext.IsModuleActive(GameplayModule.SupermarketOrganize);
            if (sectionRoot != null)
                sectionRoot.SetActive(active);
            else
                gameObject.SetActive(active);
        }
    }
}
