using Game.Core;
using Game.AI;
using Game.EscapeSnake;
using Game.Player;
using Game.WormDodge;
using Game.WormDodge.Pickups;
using UnityEngine;
using UnityEngine.AI;

namespace Game.WormDodge
{
    /// <summary>
    /// 躲避虫子 AI 装配：与钓鱼场景 <c>AI (N)</c> 根节点结构一致。
    /// </summary>
    public static class WormDodgeAiActor
    {
        public static void ConfigureRoot(
            GameObject root,
            GameObject characterRoot,
            WormDodgeAiPath aiPath,
            string displayName,
            Transform respawnPoint = null)
        {
            if (root == null || characterRoot == null || !GameProductSettings.IsWormDodgeContentEnabled)
                return;

            var rig = root.GetComponent<PlayerRig>();
            if (rig == null)
                rig = root.AddComponent<PlayerRig>();

            var behavior = root.GetComponent<AIFakeWormDodgeBehavior>();
            if (behavior == null)
                behavior = root.AddComponent<AIFakeWormDodgeBehavior>();

            var agent = characterRoot.GetComponent<NavMeshAgent>();
            if (agent == null)
                agent = characterRoot.AddComponent<NavMeshAgent>();

            agent.height = 1.8f;
            agent.radius = 0.35f;
            agent.speed = 3.8f + Random.Range(-0.4f, 0.6f);
            agent.angularSpeed = 420f;
            agent.acceleration = 10f;
            agent.stoppingDistance = 0.35f;
            agent.autoBraking = true;

            var patrol = characterRoot.GetComponent<NavMeshAIPatrolAgent>();
            if (patrol == null)
                patrol = characterRoot.AddComponent<NavMeshAIPatrolAgent>();

            var capsule = characterRoot.GetComponent<CapsuleCollider>();
            if (capsule == null)
                capsule = characterRoot.AddComponent<CapsuleCollider>();

            capsule.height = 2f;
            capsule.radius = 0.5f;
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.isTrigger = false;

            var body = characterRoot.GetComponent<Rigidbody>();
            if (body == null)
                body = characterRoot.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            var animator = characterRoot.GetComponentInChildren<Animator>();

            var participant = characterRoot.GetComponent<WormDodgeParticipant>();
            if (participant == null)
                participant = characterRoot.AddComponent<WormDodgeParticipant>();

            if (characterRoot.GetComponent<WormDodgeParticipantPickupEffects>() == null)
                characterRoot.AddComponent<WormDodgeParticipantPickupEffects>();

            var target = characterRoot.GetComponent<WormChaseTarget>();
            if (target == null)
                target = characterRoot.AddComponent<WormChaseTarget>();

            var spawn = respawnPoint != null ? respawnPoint : characterRoot.transform;
            participant.ConfigureForPlayer(false, spawn);
            participant.SetLeaderboardDisplayName(displayName);
            target.SetHideOnDeath(true);

            if (characterRoot.transform.Find("SafeZoneIndicator") != null)
                WormDodgePresenceSetup.EnsureSafeZoneIndicator(characterRoot);

            behavior.Configure(aiPath, agent, patrol, animator, rig);
            behavior.SetDisplayName(displayName);

            var cc = characterRoot.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

            var rootCc = root.GetComponent<CharacterController>();
            if (rootCc != null)
                rootCc.enabled = false;
        }
    }
}
