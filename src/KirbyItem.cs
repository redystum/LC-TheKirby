using System.Collections;
using System.Diagnostics;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace TheKirby {
    class SwallowedItens {
        public string name;
        public int scrapValue;
        public float weight;
    }

    class KirbyItem : PhysicsProp {
        public AudioClip MusicSFX = null!;
        public AudioClip NavEnterSFX = null!;
        public AudioClip SwallowSFX = null!;
        public AudioClip PukeSFX = null!;
        public Animator KirbyAnimator = null!;
        public AudioClip FullSFX = null!;

        public AudioSource AudioSourceComponent = null!;

        public Collider SwallowCollider = null!;

        [SerializeField] private string startAttackTrigger = "startAttack";
        [SerializeField] private string fullStaterigger = "full";
        [SerializeField] private string stopWalkingTrigger = "stopWalking";

        private bool IsPlayingMusic = false;
        private bool IsSwallowing = false;

        private SwallowedItens[] SwallowedItems = new SwallowedItens[99];
        private int SwallowedItemsIndex = 0;
        private int MaxWeight = 100;

        private float currentWeight = 0f;
        private int currentValue = 0;
        private PlayerControllerB lastOwner = null!;

        public int defaultValue = 0;

        public ulong[] swallowedPlayersByEnemy = new ulong[99];
        public int swallowedPlayersByEnemyIndex = 0;
        public override void Start() {
            base.Start();
            if (AudioSourceComponent == null) {
                AudioSourceComponent = gameObject.GetComponent<AudioSource>();
            }

            LogIfDebugBuild(swallowedPlayersByEnemyIndex.ToString());

            AudioSourceComponent.outputAudioMixerGroup = SoundManager.Instance.diageticMixer.FindMatchingGroups("SFX")[0];

            MaxWeight = Plugin.BoundConfig.MaxWeight.Value;

            if (defaultValue == 0)
                defaultValue = Random.Range(Plugin.BoundConfig.MinValue.Value, Plugin.BoundConfig.MaxValue.Value);
            currentValue = defaultValue;
            SetScrapValue(defaultValue);
        }

        [Conditional("DEBUG")]
        void LogIfDebugBuild(string text, bool soround = false) {
            if (soround) {
                text = $"\n========\n========\n{text}\n========\n========\n";
            }

            Plugin.Logger.LogInfo(text);
        }

        public override void ItemActivate(bool used, bool buttonDown = true) {
            base.ItemActivate(used, buttonDown);
            if (buttonDown) {
                IsSwallowing = true;
                LogIfDebugBuild("KirbyItem: ItemActivate", true);
                StartCoroutine(Swallow());
            }
        }

        [ClientRpc]
        public void DoAnimationClientRpc(string animationName) {
            KirbyAnimator.SetTrigger(animationName);
        }

        public void PlaySound(AudioClip clip) {
            AudioSourceComponent.PlayOneShot(clip);
        }

        private IEnumerator Swallow() {
            DoAnimationClientRpc(startAttackTrigger);
            PlaySound(SwallowSFX);
            yield return new WaitForSeconds(1.5f);
            IsSwallowing = false;
        }

        public override void OnBroughtToShip() {
            base.OnBroughtToShip();
            PlaySound(NavEnterSFX);
        }

        public override void ItemInteractLeftRight(bool right) {
            base.ItemInteractLeftRight(right);

            if (playerHeldBy == null)
                return;

            if (!right) {
                PlaySound(PukeSFX);

                Vector3 inFrontOfPlayer = playerHeldBy.transform.forward;
                Vector3 newPosition = transform.position + inFrontOfPlayer;

                for (int i = 0; i < swallowedPlayersByEnemyIndex; i++) {
                    ulong playerId = swallowedPlayersByEnemy[i];
                    PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerId];

                    player.SpawnDeadBody((int)playerId, newPosition, 5, player);
                }

                swallowedPlayersByEnemyIndex = 0;
                swallowedPlayersByEnemy = new ulong[99];

                LogIfDebugBuild(SwallowedItemsIndex.ToString());

                if (SwallowedItemsIndex > 0) {
                    for (int i = 0; i < SwallowedItemsIndex; i++) {
                        LogIfDebugBuild(SwallowedItems[i].name, true);
                        Item item = StartOfRound.Instance.allItemsList.itemsList.Find(item => item.itemName == SwallowedItems[i].name);

                        if (item != null) {

                            GameObject kirbyGameObject = Instantiate<GameObject>(item.spawnPrefab, newPosition, Quaternion.identity, RoundManager.Instance.spawnedScrapContainer);

                            GrabbableObject component = kirbyGameObject.GetComponent<GrabbableObject>();
                            component.startFallingPosition = newPosition;
                            component.targetFloorPosition = component.GetItemFloorPosition(transform.position);
                            component.SetScrapValue(SwallowedItems[i].scrapValue);
                            component.itemProperties.weight = SwallowedItems[i].weight;

                            component.NetworkObject.Spawn();

                        } else {
                            LogIfDebugBuild("GrabbableObject is null", true);
                        }
                    }

                    playerHeldBy.carryWeight -= currentWeight;
                    SwallowedItemsIndex = 0;
                    currentWeight = 0;
                    currentValue = defaultValue;
                    SetScrapValue(defaultValue);
                    DoAnimationClientRpc(stopWalkingTrigger);
                }

                playerHeldBy.carryWeight -= currentWeight;
                SwallowedItemsIndex = 0;
                currentWeight = 0;

            } else {
                // TODO: if an interaction exist (like open door) dont play

                if (IsPlayingMusic) {
                    AudioSourceComponent.Stop();
                } else {
                    PlaySound(MusicSFX);
                }

                IsPlayingMusic = !IsPlayingMusic;
                string changeTo = !IsPlayingMusic ? "Play: [E]" : "Stop: [E]";
                if (IsOwner)
                    HUDManager.Instance.ChangeControlTip(2, changeTo);
            }
        }

        public override void EquipItem() {
            base.EquipItem();
            playerHeldBy.equippedUsableItemQE = true;
            if (!IsOwner)
                return;
            HUDManager.Instance.DisplayTip("To use the Kirby:", "Press LMB to swallow scrap (max. 3), and Q to puke them on the ground.", useSave: true, prefsKey: "LCTip_UseManual");
            HUDManager.Instance.ChangeControlTip(3, "Puke: [Q]");
        }

        public override void GrabItem() {
            base.GrabItem();

            string changeTo = !IsPlayingMusic ? "Play: [E]" : "Stop: [E]";
            if (IsOwner)
                HUDManager.Instance.ChangeControlTip(2, changeTo);

            // BUG: when item is grabed this propriety are changed to all instances so the kirbys not in hand start fly to this position

            itemProperties.positionOffset = new Vector3(-.2f, -.1f, -.2f);
            itemProperties.rotationOffset = new Vector3(-90, 180, 100);

            lastOwner = playerHeldBy;
            playerHeldBy.carryWeight += currentWeight;
        }

        public override void PlayDropSFX() {
            base.PlayDropSFX();
            itemProperties.positionOffset = new Vector3(0, 0, 0);
            itemProperties.rotationOffset = new Vector3(0, 0, 0);
        }

        public override void DiscardItem() {
            base.DiscardItem();
            lastOwner.carryWeight -= currentWeight;
        }

        public override void Update() {
            base.Update();

            if (SwallowCollider != null && SwallowCollider.enabled && IsSwallowing) {
                Collider[] colliders = Physics.OverlapBox(SwallowCollider.bounds.center, SwallowCollider.bounds.extents, SwallowCollider.transform.rotation, LayerMask.GetMask("Props"));
                if (colliders.Length > 0) {
                    foreach (Collider collider in colliders) {
                        if (collider == SwallowCollider)
                            continue;

                        if (currentWeight > MaxWeight) {
                            PlaySound(FullSFX);
                            DoAnimationClientRpc(fullStaterigger);
                            break;
                        }


                        GrabbableObject beeingSwallowed = collider.GetComponent<GrabbableObject>();
                        if (beeingSwallowed != null) {

                            SwallowedItems[SwallowedItemsIndex] = new SwallowedItens {
                                name = beeingSwallowed.itemProperties.itemName,
                                scrapValue = beeingSwallowed.scrapValue,
                                weight = beeingSwallowed.itemProperties.weight
                            };

                            SwallowedItemsIndex++;

                            var carryWeight = Mathf.Clamp(beeingSwallowed.itemProperties.weight - 1f, 0f, 10f);
                            currentWeight += carryWeight;
                            playerHeldBy.carryWeight += carryWeight;

                            currentValue += beeingSwallowed.scrapValue;

                            SetScrapValue(currentValue);

                            DespawnItemServerRpc((NetworkBehaviourReference)(NetworkBehaviour)beeingSwallowed);

                        }
                    }
                }
            }

        }

        [ServerRpc(RequireOwnership = false)]
        public void DespawnItemServerRpc(NetworkBehaviourReference grabbableRef) {
            GrabbableObject grabbableObject;

            if (grabbableRef.TryGet<GrabbableObject>(out grabbableObject)) {
                DespawnItem(grabbableObject.gameObject);
            } else {
                LogIfDebugBuild("Failed to retrieve GrabbableObject from NetworkBehaviourReference.", true);
            }
        }

        private void DespawnItem(GameObject item) {
            NetworkObject networkObject = item.GetComponent<NetworkObject>();
            if (networkObject != null) {
                networkObject.Despawn();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SpawnItemServerRpc(NetworkBehaviourReference grabbableRef) {
            GrabbableObject grabbableObject;

            if (grabbableRef.TryGet<GrabbableObject>(out grabbableObject)) {
                SpawnItem(grabbableObject);
            } else {
                LogIfDebugBuild("Failed to retrieve GrabbableObject from NetworkBehaviourReference.", true);
            }
        }

        private void SpawnItem(GrabbableObject item) {
            NetworkObject networkObject = item.GetComponent<NetworkObject>();
            if (networkObject != null) {
                networkObject.Spawn();
            }
        }
    }
}