using System.Collections;
using System.Diagnostics;
using System.Resources;
using System.Runtime.Serialization;
using GameNetcodeStuff;
using LethalLib.Modules;
using Unity.Netcode;
using UnityEngine;

namespace TheKirby {
    class ExampleEnemyAI : EnemyAI {

#pragma warning disable 0649
        public Transform turnCompass = null!;
        public Transform attackArea = null!;
#pragma warning restore 0649
        public AudioClip attackSFX = null!;
        public AudioClip fullStateSFX = null!;

        public Collider SwallowCollider = null!;

        private float timeSinceNewRandPos;
        private Vector3 positionRandomness;
        private Vector3 StalkPos;
        private System.Random enemyRandom = null!;

        [SerializeField] private string startWalkTrigger = "startWalk";
        [SerializeField] private string stopWalkTrigger = "stopWalk";
        [SerializeField] private string startAttackTrigger = "startAttack";
        [SerializeField] private string fullStateTrigger = "full";
        [SerializeField] private string deathTrigger = "death";

        private int detectionRange = 20;
        private int maxSwallowedPlayers = 2;
        private bool useItem = false;

        private ulong[] swallowedPlayers = new ulong[99];
        private int swallowedPlayersIndex = 0;

        private bool isFullPlayed = false;

        private bool IsAttacking = false;

        private Item kirbyItem = null!;

        enum State {
            IdellingState,
            Patrolling,
            FollowingPlayer,
            StickingInFrontOfPlayer,
            SwallowingInProgress,
        }

        [Conditional("DEBUG")]
        void LogIfDebugBuild(string text) {
            Plugin.Logger.LogInfo("===========");
            Plugin.Logger.LogInfo(text);
            Plugin.Logger.LogInfo("===========");
        }

        public override void Start() {
            base.Start();
            DoAnimationClientRpc(startWalkTrigger);
            timeSinceNewRandPos = 0;
            positionRandomness = new Vector3(0, 0, 0);
            enemyRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
            detectionRange = Plugin.BoundConfig.DetectionRange.Value;
            maxSwallowedPlayers = Plugin.BoundConfig.MaxSwallowPlayers.Value;
            useItem = Plugin.BoundConfig.UseItem.Value;

            kirbyItem = StartOfRound.Instance.allItemsList.itemsList.Find(item => item.itemName.ToLower() == "kirby");

            LogIfDebugBuild(kirbyItem.itemName);

            agent.speed = 3f;

            currentBehaviourStateIndex = (int)State.Patrolling;
            StartSearch(transform.position);
        }


        public override void Update() {
            base.Update();
            if (isEnemyDead) {
                return;
            }

            timeSinceNewRandPos += Time.deltaTime;

            var state = currentBehaviourStateIndex;
            if (targetPlayer != null && state == (int)State.StickingInFrontOfPlayer) {
                turnCompass.LookAt(targetPlayer.gameplayCamera.transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation,
                    Quaternion.Euler(new Vector3(0f, turnCompass.eulerAngles.y, 0f)), 4f * Time.deltaTime);
            }

            if (stunNormalizedTimer > 0f) {
                agent.speed = 0f;
            }
        }

        public override void DoAIInterval() {
            base.DoAIInterval();
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead) {
                return;
            }

            if (swallowedPlayersIndex >= maxSwallowedPlayers && !isFullPlayed) {
                SwitchToBehaviourClientRpc((int)State.IdellingState);
                StartCoroutine(StopWalking(-1f));
                return;
            }

            switch (currentBehaviourStateIndex) {
                case (int)State.Patrolling:
                    if (FoundClosestPlayerInRange(detectionRange, 3f)) {
                        SwitchToBehaviourClientRpc((int)State.FollowingPlayer);
                    }

                    break;

                case (int)State.FollowingPlayer:
                    if (targetPlayer != null &&
                        Vector3.Distance(transform.position, targetPlayer.transform.position) > 2) {
                        SetDestinationToPosition(targetPlayer.transform.position);
                    } else if (targetPlayer != null) {
                        // Trigger swallowing attack when close to the player
                        StartCoroutine(SwallowingAttack());
                    }

                    break;

                case (int)State.StickingInFrontOfPlayer:
                    StickingInFrontOfPlayer();
                    break;

                case (int)State.SwallowingInProgress:
                    // We don't care about doing anything here
                    break;
                case (int)State.IdellingState:
                    // We don't care about doing anything here
                    break;
                default:
                    break;
            }
        }

        bool FoundClosestPlayerInRange(float range, float senseRange) {
            TargetClosestPlayer(bufferDistance: 1.5f, requireLineOfSight: true);
            if (targetPlayer == null) {
                TargetClosestPlayer(bufferDistance: 1.5f, requireLineOfSight: false);
                range = senseRange;
            }

            return targetPlayer != null &&
                   Vector3.Distance(transform.position, targetPlayer.transform.position) < range;
        }

        void StickingInFrontOfPlayer() {
            if (targetPlayer == null || !IsOwner) {
                return;
            }

            if (timeSinceNewRandPos > 0.7f) {
                timeSinceNewRandPos = 0;
                if (enemyRandom.Next(0, 5) == 0) { } else {
                    positionRandomness = new Vector3(enemyRandom.Next(-2, 2), 0, enemyRandom.Next(-2, 2));
                    StalkPos = targetPlayer.transform.position -
                        Vector3.Scale(new Vector3(-5, 0, -5), targetPlayer.transform.forward) + positionRandomness;
                }

                SetDestinationToPosition(StalkPos, checkForPath: false);
            }
        }

        private IEnumerator SwallowingAttack() {
            SwitchToBehaviourClientRpc((int)State.SwallowingInProgress);
            StalkPos = targetPlayer.transform.position;
            SetDestinationToPosition(StalkPos);
            yield return new WaitForSeconds(.5f);
            if (isEnemyDead) {
                yield break;
            }

            if (swallowedPlayersIndex >= maxSwallowedPlayers) {
                SwitchToBehaviourClientRpc((int)State.IdellingState);
                yield break;
            }

            SwallowingAttackHitClientRpc();
            if (currentBehaviourStateIndex != (int)State.SwallowingInProgress) {
                yield break;
            }

            if (swallowedPlayersIndex >= maxSwallowedPlayers && !isFullPlayed) {
                SwitchToBehaviourClientRpc((int)State.IdellingState);
                StartCoroutine(StopWalking(-1f));
            } else {
                SwitchToBehaviourClientRpc((int)State.FollowingPlayer);
            }
        }

        [ClientRpc]
        public void SwallowingAttackHitClientRpc() {
            int playerLayer = 1 << 3;
            Collider[] hitColliders = Physics.OverlapBox(attackArea.position, attackArea.localScale,
                Quaternion.identity, playerLayer);

            if (hitColliders.Length <= 0) {
                LogIfDebugBuild("No player hit!");
                return;
            }

            if (IsAttacking) {
                return;
            }

            PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(hitColliders[0]);
            if (playerControllerB != null) {
                LogIfDebugBuild("Swallowing attack hit player!");
                IsAttacking = true;
                DoAnimationClientRpc(startAttackTrigger);
                StartCoroutine(WaitToPlaySound(0.5f, attackSFX));
                StartCoroutine(WaitToDamage(2f));
                StartCoroutine(StopWalking(10f));
                SwitchToBehaviourClientRpc((int)State.FollowingPlayer);
            }
        }

        IEnumerator WaitToPlaySound(float time, AudioClip clip) {
            yield return new WaitForSeconds(time);
            creatureVoice.PlayOneShot(clip);
        }

        IEnumerator WaitToDamage(float time) {
            yield return new WaitForSeconds(time);

            if (SwallowCollider != null && SwallowCollider.enabled) {
                Collider[] colliders = Physics.OverlapBox(SwallowCollider.bounds.center, SwallowCollider.bounds.extents, SwallowCollider.transform.rotation, 1 << 3);
                if (colliders.Length > 0) {
                    PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(colliders[0]);
                    swallowedPlayers[swallowedPlayersIndex] = playerControllerB.playerClientId;
                    swallowedPlayersIndex++;
                    playerControllerB.KillPlayer(Vector3.zero, false, CauseOfDeath.Suffocation);
                }
            }
        }

        public override void HitEnemy(int force = 1, PlayerControllerB? playerWhoHit = null, bool playHitSFX = false) {
            base.HitEnemy(force, playerWhoHit, playHitSFX);
            if (isEnemyDead) {
                return;
            }

            enemyHP -= force;

            if (IsOwner) {
                if (enemyHP <= 0 && !isEnemyDead) {
                    StopCoroutine(SwallowingAttack());
                    StopCoroutine(searchCoroutine);
                    creatureVoice.Stop();
                    DoAnimationClientRpc(deathTrigger);
                    KillEnemy();

                    if (useItem) {
                        StartCoroutine(SpawnItem());
                    }

                    StartCoroutine(DespawnAfter(3f));
                }
            }

        }

        IEnumerator SpawnItem() {
            yield return new WaitForSeconds(3f);
            Vector3 position = transform.position + new Vector3(0, 1, 0);
            Quaternion rotation = transform.rotation;

            GameObject kirbyGameObject = Instantiate<GameObject>(this.kirbyItem.spawnPrefab, position, rotation, RoundManager.Instance.spawnedScrapContainer);

            GrabbableObject component = kirbyGameObject.GetComponent<GrabbableObject>();
            component.startFallingPosition = position;
            component.targetFloorPosition = component.GetItemFloorPosition(transform.position);
            component.SetScrapValue(Random.Range(Plugin.BoundConfig.MinValue.Value, Plugin.BoundConfig.MaxValue.Value));

            KirbyItem kirbyItem = kirbyGameObject.GetComponent<KirbyItem>();
            kirbyItem.swallowedPlayersByEnemy = swallowedPlayers;
            kirbyItem.swallowedPlayersByEnemyIndex = swallowedPlayersIndex;

            component.NetworkObject.Spawn();

            LogIfDebugBuild("Swallowed players: " + swallowedPlayersIndex);

        }

        [ClientRpc]
        public void DoAnimationClientRpc(string animationName) {
            LogIfDebugBuild("Playing animation: " + animationName);
            creatureAnimator.SetTrigger(animationName);
        }

        IEnumerator StopWalking(float time) {
            if (time == -1 && isFullPlayed == false) {
                agent.isStopped = true;
                DoAnimationClientRpc(fullStateTrigger);
                creatureVoice.PlayOneShot(fullStateSFX);
                StopSearch(currentSearch);
                isFullPlayed = true;
                yield break;
            }

            SwitchToBehaviourClientRpc((int)State.IdellingState);
            agent.speed = 0;
            DoAnimationClientRpc(stopWalkTrigger);
            yield return new WaitForSeconds(time);
            if (currentBehaviourStateIndex == (int)State.IdellingState && !isFullPlayed) {
                StartCoroutine(StopWalking(-1));
                yield break;
            }

            DoAnimationClientRpc(startWalkTrigger);
            agent.speed = 3f;
            SwitchToBehaviourClientRpc((int)State.Patrolling);
            IsAttacking = false;
        }

        IEnumerator DespawnAfter(float time) {
            yield return new WaitForSeconds(time);
            thisNetworkObject.Despawn();
        }

    }
}