using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static ReviveAPI.ReviveAPI;
using static RoR2.Inventory.ItemTransformation;

namespace ReviveAPI
{
    public class Tests
    {
        public static void AddRevives()
        {
            ReviveAPI.AddCustomRevive(CanReviveTest1, OnReviveTest1, null, 2);
            ReviveAPI.AddCustomRevive(CanReviveTest2, OnReviveTest1, null, 1);
            ReviveAPI.AddCustomRevive(CanReviveTest3, OnReviveTest1, null, 0);
            /*
            ReviveAPI.AddCustomRevive(CanReviveSyringe, new ReviveAPI.PendingOnRevive[]
                {
                    new ReviveAPI.PendingOnRevive
                    {
                        onReviveDelegate = ReviveWithEffectsSyringe,
                        timer = 2f,
                    }
                },
                33);

            ReviveAPI.AddCustomRevive(CanReviveMedkit, new ReviveAPI.PendingOnRevive[]
                {
                    new ReviveAPI.PendingOnRevive
                    {
                        onReviveDelegate = ReviveWithEffectsMedkit,
                        timer = 2f,
                    }
                },
                44);
            ReviveAPI.AddCustomRevive(CanReviveGlasses, new ReviveAPI.PendingOnRevive[]
                {
                    new ReviveAPI.PendingOnRevive
                    {
                        onReviveDelegate = ReviveWithEffectsGlasses,
                        timer = 2f,
                    }
                },
                -44);
            ReviveAPI.AddCustomRevive(CanReviveHoof, new ReviveAPI.PendingOnRevive[]
                {
                    new ReviveAPI.PendingOnRevive
                    {
                        onReviveDelegate = ReviveWithEffectsHoof,
                        timer = 2f,
                    }
                },
                -10);*/
        }
        public static bool CanReviveTest1(CharacterMaster characterMaster, out CanReviveInfo canReviveInfo)
        {
            Inventory.ItemTransformation itemTransformation = default(Inventory.ItemTransformation);
            itemTransformation.originalItemIndex = RoR2Content.Items.Syringe.itemIndex;
            itemTransformation.newItemIndex = RoR2Content.Items.Medkit.itemIndex;
            itemTransformation.transformationType = (ItemTransformationTypeIndex)0;
            Inventory.ItemTransformation.CanTakeResult canTakeResult;
            if (itemTransformation.CanTake(characterMaster.inventory, out canTakeResult))
            {
                canReviveInfo = new CanReviveInfo
                {
                    canTakeResult = canTakeResult,
                    //itemTransformation = itemTransformation,
                    revive = true,
                };
                return true;
            }
            canReviveInfo = null;
            return false;
        }
        public static void OnReviveTest1(CharacterMaster characterMaster, CanReviveInfo canReviveInfo)
        {
            CharacterMaster.ExtraLifeServerBehavior extraLifeServerBehavior = characterMaster.gameObject.AddComponent<CharacterMaster.ExtraLifeServerBehavior>();
            extraLifeServerBehavior.pendingTransformation = canReviveInfo.canTakeResult.PerformTake();
            extraLifeServerBehavior.consumedItemIndex = RoR2Content.Items.Medkit.itemIndex;
            extraLifeServerBehavior.completionTime = Run.FixedTimeStamp.now + 2f;
            extraLifeServerBehavior.completionCallback = characterMaster.RespawnExtraLife;
            extraLifeServerBehavior.completionTime -= 1f;
            extraLifeServerBehavior.soundCallback = characterMaster.PlayExtraLifeSFX;
            //canReviveInfo.itemTransformation.TryTransform(characterMaster.inventory, out _);
        }
        public static bool CanReviveTest2(CharacterMaster characterMaster, out CanReviveInfo canReviveInfo)
        {
            Inventory.ItemTransformation itemTransformation = default(Inventory.ItemTransformation);
            itemTransformation.originalItemIndex = RoR2Content.Items.Mushroom.itemIndex;
            itemTransformation.newItemIndex = RoR2Content.Items.Medkit.itemIndex;
            itemTransformation.transformationType = (ItemTransformationTypeIndex)0;
            Inventory.ItemTransformation.CanTakeResult canTakeResult;
            if (itemTransformation.CanTake(characterMaster.inventory, out canTakeResult))
            {
                canReviveInfo = new CanReviveInfo
                {
                    canTakeResult = canTakeResult,
                    //itemTransformation = itemTransformation,
                    revive = true,
                };
                return true;
            }
            canReviveInfo = null;
            return false;
        }
        public static bool CanReviveTest3(CharacterMaster characterMaster, out CanReviveInfo canReviveInfo)
        {
            Inventory.ItemTransformation itemTransformation = default(Inventory.ItemTransformation);
            itemTransformation.originalItemIndex = RoR2Content.Items.IgniteOnKill.itemIndex;
            itemTransformation.newItemIndex = RoR2Content.Items.Medkit.itemIndex;
            itemTransformation.transformationType = (ItemTransformationTypeIndex)0;
            Inventory.ItemTransformation.CanTakeResult canTakeResult;
            if (itemTransformation.CanTake(characterMaster.inventory, out canTakeResult))
            {
                canReviveInfo = new CanReviveInfo
                {
                    canTakeResult = canTakeResult,
                    //itemTransformation = itemTransformation,
                    revive = true,
                };
                return true;
            }
            canReviveInfo = null;
            return false;
        }
        #region LowPriorityBeforeVanilla(Syringe)
        private static void ReviveWithEffectsSyringe(CharacterMaster master)
        {
            ReviveAPI.ManualLogger.LogInfo($"Revivng {master} via Syringe.");
            var vector = master.deathFootPosition;
            master.Respawn(master.deathFootPosition, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), true);
            var body = master.GetBody();
            body.AddTimedBuff(RoR2Content.Buffs.Immune, 3f);
            master.inventory.RemoveItem(RoR2Content.Items.Syringe, master.inventory.GetItemCount(RoR2Content.Items.Syringe));
            if (master.bodyInstanceObject)
            {
                EntityStateMachine[] components = master.bodyInstanceObject.GetComponents<EntityStateMachine>();
                foreach (EntityStateMachine obj in components)
                {
                    obj.initialStateType = obj.mainStateType;
                }
                if (master.gameObject)
                {
                    var effect = Addressables.LoadAssetAsync<GameObject>(RoR2BepInExPack.GameAssetPaths.RoR2_DLC1_VoidSurvivor.VoidSurvivorCorruptDeathMuzzleflash_prefab).WaitForCompletion();
                    if (effect)
                    {
                        EffectManager.SpawnEffect(effect, new EffectData
                        {
                            origin = vector,
                            rotation = master.bodyInstanceObject.transform.rotation
                        }, transmit: true);
                    }
                }
            }
        }

        private static bool CanReviveSyringe(CharacterMaster master)
        {
            return master.inventory.GetItemCount(RoR2Content.Items.Syringe) > 0;
        }
        #endregion

        #region HighPriorityBeforeVanilla(Medkit)
        private static void ReviveWithEffectsMedkit(CharacterMaster master)
        {
            ReviveAPI.ManualLogger.LogInfo($"Revivng {master} via MedKit.");
            var vector = master.deathFootPosition;
            master.Respawn(master.deathFootPosition, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), true);
            var body = master.GetBody();
            body.AddTimedBuff(RoR2Content.Buffs.Immune, 3f);
            master.inventory.RemoveItem(RoR2Content.Items.Medkit, master.inventory.GetItemCount(RoR2Content.Items.Medkit));
            if (master.bodyInstanceObject)
            {
                EntityStateMachine[] components = master.bodyInstanceObject.GetComponents<EntityStateMachine>();
                foreach (EntityStateMachine obj in components)
                {
                    obj.initialStateType = obj.mainStateType;
                }
                if (master.gameObject)
                {
                    var effect = Addressables.LoadAssetAsync<GameObject>(RoR2BepInExPack.GameAssetPaths.RoR2_Base_Common_VFX.BrittleDeath_prefab).WaitForCompletion();
                    if (effect)
                    {
                        EffectManager.SpawnEffect(effect, new EffectData
                        {
                            origin = vector,
                            rotation = master.bodyInstanceObject.transform.rotation
                        }, transmit: true);
                    }
                }
            }
        }

        private static bool CanReviveMedkit(CharacterMaster master)
        {
            return master.inventory.GetItemCount(RoR2Content.Items.Medkit) > 0;
        }
        #endregion

        #region LowPriorityAfterVanilla(Glasses)
        private static void ReviveWithEffectsGlasses(CharacterMaster master)
        {
            ReviveAPI.ManualLogger.LogInfo($"Revivng {master} via CritGlasses.");
            var vector = master.deathFootPosition;
            master.Respawn(master.deathFootPosition, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), true);
            var body = master.GetBody();
            body.AddTimedBuff(RoR2Content.Buffs.Immune, 3f);
            master.inventory.RemoveItem(RoR2Content.Items.CritGlasses, master.inventory.GetItemCount(RoR2Content.Items.CritGlasses));
            if (master.bodyInstanceObject)
            {
                EntityStateMachine[] components = master.bodyInstanceObject.GetComponents<EntityStateMachine>();
                foreach (EntityStateMachine obj in components)
                {
                    obj.initialStateType = obj.mainStateType;
                }
                if (master.gameObject)
                {
                    var effect = Addressables.LoadAssetAsync<GameObject>(RoR2BepInExPack.GameAssetPaths.RoR2_Base_Bandit2.Bandit2SmokeBomb_prefab).WaitForCompletion();
                    if (effect)
                    {
                        EffectManager.SpawnEffect(effect, new EffectData
                        {
                            origin = vector,
                            rotation = master.bodyInstanceObject.transform.rotation
                        }, transmit: true);
                    }
                }
            }
        }

        private static bool CanReviveGlasses(CharacterMaster master)
        {
            return master.inventory.GetItemCount(RoR2Content.Items.CritGlasses) > 0;
        }
        #endregion

        #region HighPriorityAfterVanilla(Hoof)
        private static void ReviveWithEffectsHoof(CharacterMaster master)
        {
            ReviveAPI.ManualLogger.LogInfo($"Revivng {master} via Hoof.");
            var vector = master.deathFootPosition;
            master.Respawn(master.deathFootPosition, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), true);
            var body = master.GetBody();
            body.AddTimedBuff(RoR2Content.Buffs.Immune, 3f);
            master.inventory.RemoveItem(RoR2Content.Items.Hoof, master.inventory.GetItemCount(RoR2Content.Items.Hoof));
            if (master.bodyInstanceObject)
            {
                EntityStateMachine[] components = master.bodyInstanceObject.GetComponents<EntityStateMachine>();
                foreach (EntityStateMachine obj in components)
                {
                    obj.initialStateType = obj.mainStateType;
                }
                if (master.gameObject)
                {
                    var effect = Addressables.LoadAssetAsync<GameObject>(RoR2BepInExPack.GameAssetPaths.RoR2_Base_Captain.CaptainTazerNova_prefab).WaitForCompletion();
                    if (effect)
                    {
                        EffectManager.SpawnEffect(effect, new EffectData
                        {
                            origin = vector,
                            rotation = master.bodyInstanceObject.transform.rotation
                        }, transmit: true);
                    }
                }
            }
        }

        private static bool CanReviveHoof(CharacterMaster master)
        {
            return master.inventory.GetItemCount(RoR2Content.Items.Hoof) > 0;
        }
        #endregion

    }
}
