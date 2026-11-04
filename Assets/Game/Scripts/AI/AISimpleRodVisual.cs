using Game.Core.Config;
using Game.Fishing;
using Game.Player;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// AI 专用钓竿：不依赖玩家存档，按 rodId 从 RodDatabase 实例化。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AISimpleRodVisual : MonoBehaviour
    {
        [SerializeField] private PlayerRig rig;
        [SerializeField] private string rodId = "rod_1";

        GameObject _rod;
        FishingLineVisual _lineVisual;
        bool _onHand;

        public GameObject RodInstance => _rod;
        public FishingLineVisual LineVisual => _lineVisual;
        public PlayerRig Rig => rig;

        void Awake()
        {
            if (rig == null)
                rig = GetComponent<PlayerRig>();
        }

        public void EnsureRodSpawned()
        {
            if (_rod != null)
                return;

            var db = ConfigRegistry.Rods;
            if (db == null || !db.TryGet(rodId, out var def) || def?.prefab == null)
                return;

            var socket = ResolveBackSocket();
            if (socket == null)
                return;

            _rod = Instantiate(def.prefab, socket);
            _rod.transform.localPosition = Vector3.zero;
            _rod.transform.localRotation = Quaternion.identity;
            _rod.transform.localScale = Vector3.one;
            _onHand = false;
            ResolveLineVisualFromRod();
        }

        void ResolveLineVisualFromRod()
        {
            _lineVisual = _rod != null
                ? _rod.GetComponentInChildren<FishingLineVisual>(true)
                : null;
        }

        public void MountOnHand()
        {
            EnsureRodSpawned();
            ApplyMount(hand: true);
        }

        public void MountOnBack()
        {
            EnsureRodSpawned();
            ApplyMount(hand: false);
        }

        void ApplyMount(bool hand)
        {
            if (_rod == null)
                return;

            var socket = hand ? ResolveHandSocket() : ResolveBackSocket();
            if (socket == null)
                return;

            _rod.transform.SetParent(socket, false);
            _rod.transform.localPosition = Vector3.zero;
            _rod.transform.localRotation = Quaternion.identity;
            _rod.transform.localScale = Vector3.one;
            _onHand = hand;
        }

        Transform ResolveBackSocket()
        {
            rig?.EnsureSceneSocketsResolved();
            if (rig?.rodBackSocket != null)
                return rig.rodBackSocket;

            return FindSocket("BackRodRoot") ?? transform;
        }

        Transform ResolveHandSocket()
        {
            rig?.EnsureSceneSocketsResolved();
            if (rig?.rodHandSocket != null)
                return rig.rodHandSocket;

            return FindSocket("HandRodRoot") ?? transform;
        }

        Transform FindSocket(string name)
        {
            var roots = new[] { rig?.characterSocket, transform };
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                    continue;

                var found = CharacterHierarchySockets.FindDeepChildByName(root, name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
