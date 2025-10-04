using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ReviveAPI
{
    public class Tests
    {
        public static void AddRevives()
        {
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
                -10);
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
