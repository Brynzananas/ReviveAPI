using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using UnityEngine;
using static ReviveAPI.ReviveAPI;

[assembly: SecurityPermission(System.Security.Permissions.SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: HG.Reflection.SearchableAttribute.OptIn]
[assembly: HG.Reflection.SearchableAttribute.OptInAttribute]
[module: UnverifiableCode]
#pragma warning disable CS0618
#pragma warning restore CS0618
namespace ReviveAPI
{
    [BepInPlugin(ModGuid, ModName, ModVer)]
    [System.Serializable]
    public class ReviveAPI : BaseUnityPlugin
    {
        public const string ModGuid = "com.brynzananas.reviveapi";
        public const string ModName = "Revive API";
        public const string ModVer = "1.2.1";
        public const bool ENABLE_TEST = false;
        public static ManualLogSource ManualLogger;

        public delegate bool CanReviveDelegate(CharacterMaster characterMaster);
        public delegate void OnReviveDelegate(CharacterMaster characterMaster);
        public class CustomRevive
        {
            public CanReviveDelegate canRevive;
            public OnReviveDelegate onRevive;
            public PendingOnRevive[] pendingOnRevives;
            public int priority;
        }
        public class PendingOnRevive
        {
            internal CharacterMaster characterMaster;
            public float timer;
            public OnReviveDelegate onReviveDelegate;
        }
        private static List<PendingOnRevive> pendingRevives = new List<PendingOnRevive>();
        private static List<CustomRevive> customRevives = new List<CustomRevive>();
        public void Awake()
        {
            ManualLogger = Logger;
            SetHooks();
            if (ENABLE_TEST)
            {
                Logger.LogWarning("ReviveAPI TESTS ARE ENABLED!");
                Tests.AddRevives();
            }
        }

        public void FixedUpdate()
        {
            for (int i = 0; i < pendingRevives.Count; i++)
            {
                PendingOnRevive pendingRevive = pendingRevives[i];
                pendingRevive.timer -= Time.fixedDeltaTime;
                if (pendingRevive.timer <= 0f)
                {
                    pendingRevives.Remove(pendingRevive);
                    if (pendingRevive.characterMaster) pendingRevive.onReviveDelegate?.Invoke(pendingRevive.characterMaster);
                }
            }
        }
        public static PendingOnRevive[] defaultPendingOnRevives
        {
            get
            {
                PendingOnRevive pendingSimpleRespawn = new PendingOnRevive
                {
                    timer = 2f,
                    onReviveDelegate = SimpleRespawn
                };
                PendingOnRevive pendingSimpleRespawnSound = new PendingOnRevive
                {
                    timer = 2f,
                    onReviveDelegate = SimpleRespawnSound
                };
                return new PendingOnRevive[] { pendingSimpleRespawn, pendingSimpleRespawnSound };
            }
        }
        public static void SimpleRespawn(CharacterMaster characterMaster)
        {
            Vector3 vector = characterMaster.deathFootPosition;
            if (characterMaster.killedByUnsafeArea)
            {
                vector = TeleportHelper.FindSafeTeleportDestination(characterMaster.deathFootPosition, characterMaster.bodyPrefab.GetComponent<CharacterBody>(), RoR2Application.rng) ?? characterMaster.deathFootPosition;
            }
            characterMaster.Respawn(vector, Quaternion.Euler(0f, global::UnityEngine.Random.Range(0f, 360f), 0f), true);
        }
        public static void SimpleRespawnSound(CharacterMaster characterMaster) => characterMaster.PlayExtraLifeSFX();
        public void Destroy()
        {
            UnsetHooks();
        }

        #region Hooks

        private bool hooksSet;

        private void SetHooks()
        {
            if (hooksSet) return;
            IL.RoR2.Artifacts.TeamDeathArtifactManager.OnServerCharacterDeathGlobal += TeamDeathArtifactManager_OnServerCharacterDeathGlobal;
            IL.RoR2.Artifacts.DoppelgangerInvasionManager.OnCharacterDeathGlobal += DoppelgangerInvasionManager_OnCharacterDeathGlobal;
            IL.RoR2.CharacterMaster.IsDeadAndOutOfLivesServer += CharacterMaster_IsDeadAndOutOfLivesServer;
            IL.RoR2.CharacterMaster.OnBodyDeath += CharacterMaster_OnBodyDeath;
            On.RoR2.CharacterMaster.IsExtraLifePendingServer += CharacterMaster_IsExtraLifePendingServer;
            hooksSet = true;
        }


        private void UnsetHooks()
        {
            if (!hooksSet) return;
            IL.RoR2.Artifacts.TeamDeathArtifactManager.OnServerCharacterDeathGlobal -= TeamDeathArtifactManager_OnServerCharacterDeathGlobal;
            IL.RoR2.Artifacts.DoppelgangerInvasionManager.OnCharacterDeathGlobal -= DoppelgangerInvasionManager_OnCharacterDeathGlobal;
            IL.RoR2.CharacterMaster.IsDeadAndOutOfLivesServer -= CharacterMaster_IsDeadAndOutOfLivesServer;
            IL.RoR2.CharacterMaster.OnBodyDeath -= CharacterMaster_OnBodyDeath;
            On.RoR2.CharacterMaster.IsExtraLifePendingServer -= CharacterMaster_IsExtraLifePendingServer;
            hooksSet = false;
        }

        private bool CharacterMaster_IsExtraLifePendingServer(On.RoR2.CharacterMaster.orig_IsExtraLifePendingServer orig, CharacterMaster self)
        {
            var result = orig(self);
            if (!result)
            {
                result = pendingRevives.Where(x => x.characterMaster == self).ToArray().Length > 0;
            }
            return result;
        }

        private void TeamDeathArtifactManager_OnServerCharacterDeathGlobal(ILContext iLContext)
        {
            ILCursor c = new ILCursor(iLContext);
            ILLabel afterTarget = null;
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld(typeof(RoR2.Artifacts.TeamDeathArtifactManager), nameof(RoR2.Artifacts.TeamDeathArtifactManager.forceSpectatePrefab))))
            {
                afterTarget = c.DefineLabel();
                afterTarget.Target = c.Prev;
            }
            else
            {
                Logger.LogError(iLContext.Method.Name + " finding forceSpectatePrefab label IL Hook failed!");
            }

            c = new ILCursor(iLContext);
            ILLabel beforeCheck = null;
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdarg(0),
                x => x.MatchLdfld(typeof(RoR2.DamageReport), nameof(DamageReport.victimMaster)),
                x => x.MatchCallvirt<CharacterMaster>("get_playerCharacterMasterController"),
                x => x.MatchCall<UnityEngine.Object>("op_Implicit"),
                x => x.MatchBrfalse(out beforeCheck)))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(DamageReport), nameof(DamageReport.victimMaster)));
                c.EmitDelegate(CanReviveBeforeVanilla);
                c.Emit(OpCodes.Brtrue_S, beforeCheck);
            }
            else
            {
                Logger.LogError(iLContext.Method.Name + " before IL Hook failed!");
            }

            c = new ILCursor(iLContext);
            ILLabel afterCheck = null;
            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld(typeof(RoR2.DamageReport), nameof(DamageReport.victimMaster)),
                    x => x.MatchLdfld(typeof(RoR2.CharacterMaster), nameof(CharacterMaster.seekerSelfRevive)),
                    x => x.MatchBrfalse(out afterCheck),
                    x => x.MatchRet()))
            {
                var instruction = c.Emit(OpCodes.Ldarg_0).Prev;
                c.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(DamageReport), nameof(DamageReport.victimMaster)));
                c.EmitDelegate(CanReviveAfterVanilla);
                c.Emit(OpCodes.Brfalse_S, afterTarget);
                afterCheck.Target = instruction;
                c.Emit(OpCodes.Ret);
            }
            else
            {
                Logger.LogError(iLContext.Method.Name + " after IL Hook failed!");
            }
        }

        private void CharacterMaster_IsDeadAndOutOfLivesServer(ILContext iLContext)
        {
            ILCursor c = new ILCursor(iLContext);
            ILLabel iLLabel = null;
            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.ExtraLife)),
                    x => x.MatchCallvirt<Inventory>(nameof(Inventory.GetItemCount)),
                    x => x.MatchLdcI4(0),
                    x => x.MatchBgt(out iLLabel)
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldc_I4_0);
                c.Emit(OpCodes.Ldc_I4_0);
                c.EmitDelegate(CanReviveAndOrRevive);
                c.Emit(OpCodes.Brtrue_S, iLLabel);
            }
            else
            {
                Logger.LogError(iLContext.Method.Name + " IL Hook failed!");
            }
        }

        private void DoppelgangerInvasionManager_OnCharacterDeathGlobal(ILContext iLContext)
        {
            ILCursor c = new ILCursor(iLContext);
            ILLabel returnLabel = null;
            if(c.TryGotoNext(MoveType.After, 
                x => x.MatchLdloc(0),
                x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.InvadingDoppelganger)),
                x => x.MatchCallvirt<Inventory>(nameof(Inventory.GetItemCount)),
                x => x.MatchLdcI4(0),
                x => x.MatchBle(out returnLabel)
                ))
            {
                c.Emit(OpCodes.Ldarg_1);
                c.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(DamageReport), nameof(DamageReport.victimMaster)));
                c.EmitDelegate(CanReviveBeforeVanilla);
                c.Emit(OpCodes.Brtrue_S, returnLabel);
            } else
            {
                Logger.LogError(iLContext.Method.Name + " before IL Hook failed!");
            }

            c = new ILCursor(iLContext);
            ILLabel iLLabel = null;
            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchLdarg(1),
                    x => x.MatchLdfld(typeof(RoR2.DamageReport), nameof(RoR2.DamageReport.victimBody)),
                    x => x.MatchCallvirt<CharacterBody>("get_equipmentSlot"),
                    x => x.MatchCallvirt<EquipmentSlot>("get_equipmentIndex"),
                    x => x.MatchLdsfld(typeof(RoR2.DLC2Content.Equipment), nameof(RoR2.DLC2Content.Equipment.HealAndRevive)),
                    x => x.MatchCallvirt<RoR2.EquipmentDef>("get_equipmentIndex"),
                    x => x.MatchBeq(out iLLabel)
                    ))
            {
                c.Emit(OpCodes.Ldarg_1);
                c.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(DamageReport), nameof(DamageReport.victimMaster)));
                c.EmitDelegate(CanReviveAfterVanilla);
                c.Emit(OpCodes.Brtrue_S, iLLabel);
            }
            else
            {
                Logger.LogError(iLContext.Method.Name + " after IL Hook failed!");
            }

            iLContext.Method.RecalculateILOffsets();
            Logger.LogInfo(iLContext);
        }

        private void CharacterMaster_OnBodyDeath(ILContext iLContext)
        {
            // finding and marking whatever happens after vanilla revives are handled
            ILCursor c = new ILCursor(iLContext);
            ILLabel afterVanillaRevivesLabel = null;
            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdarg(0),
                x => x.MatchLdloca(0),
                x => x.MatchCall<UnityEngine.Component>(nameof(UnityEngine.Component.TryGetComponent)),
                x => x.MatchBrfalse(out afterVanillaRevivesLabel)))
            {
                afterVanillaRevivesLabel = c.DefineLabel();
                afterVanillaRevivesLabel.Target = c.Next;
            } else
            {
                Logger.LogError(iLContext.Method.Name + " finding label after vanilla revives IL Hook failed!");
            }

            // adding pre vanilla revive handling
            c = new ILCursor(iLContext);
            ILLabel reviveBeforeVanillaLabel = null;
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdarg(0),
                x => x.MatchCall<RoR2.CharacterMaster>("get_playerCharacterMasterController"),
                x => x.MatchCallvirt<RoR2.PlayerCharacterMasterController>(nameof(RoR2.PlayerCharacterMasterController.OnBodyDeath))))
            {
                reviveBeforeVanillaLabel = c.DefineLabel();
                reviveBeforeVanillaLabel.Target = c.Emit(OpCodes.Ldarg_0).Prev;
                c.EmitDelegate(ReviveBeforeVanilla);
                c.Emit(OpCodes.Brtrue, afterVanillaRevivesLabel);
            }
            else
            {
                Logger.LogError(iLContext.Method.Name + " before IL Hook failed!");
            }

            // redirecting playerCharacterMasterController to our pre vanilla revive handling
            c = new ILCursor(iLContext);
            ILLabel playerCharacterMasterControllerLabel = null;
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdarg(0),
                x => x.MatchCall<RoR2.CharacterMaster>("get_playerCharacterMasterController"),
                x => x.MatchCall<UnityEngine.Object>("op_Implicit"),
                x => x.MatchBrfalse(out playerCharacterMasterControllerLabel)))
            {
                playerCharacterMasterControllerLabel.Target = reviveBeforeVanillaLabel.Target;
            } else
            {
                Logger.LogError(iLContext.Method.Name + " redirecting playerCharacterMasterController IL Hook failed!");
            }

            // adding post vanilla revive handling
            c = new ILCursor(iLContext);
            ILLabel afterRevivesCheck = null;
            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdstr("PlayExtraLifeVoidSFX"),
                    x => x.MatchLdcR4(1f),
                    x => x.MatchCall<MonoBehaviour>(nameof(MonoBehaviour.Invoke)),
                    x => x.MatchBr(out _)
                ))
            {
                afterRevivesCheck = c.DefineLabel();
                afterRevivesCheck.Target = c.Emit(OpCodes.Ldarg_0).Prev;
                c.EmitDelegate(ReviveAfterVanilla);
                c.Emit(OpCodes.Brtrue_S, afterVanillaRevivesLabel);
            }
            else
            {
                Logger.LogError(iLContext.Method.Name + " after IL Hook failed!");
            }

            // replacing void extra life jump to our post vanilla revive handling
            c = new ILCursor(iLContext);
            ILLabel currentvoidbearTarget = null;
            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchCall<RoR2.CharacterMaster>("get_inventory"),
                    x => x.MatchLdsfld(typeof(RoR2.DLC1Content.Items), nameof(RoR2.DLC1Content.Items.ExtraLifeVoid)),
                    x => x.MatchCallvirt<RoR2.Inventory>(nameof(RoR2.Inventory.GetItemCount)),
                    x => x.MatchLdcI4(0),
                    x => x.MatchBle(out currentvoidbearTarget)
                    ))
            {
                currentvoidbearTarget.Target = afterRevivesCheck.Target;
            } else
            {
                Logger.LogError(iLContext.Method.Name + " replacing void bear target IL Hook failed!");
            }
        }

        private static bool CanReviveBeforeVanilla(CharacterMaster characterMaster)
        {
            return CanRevive(characterMaster, customRevives.Where(item => item.priority > 0).ToArray());
        }

        private static bool CanReviveAfterVanilla(CharacterMaster characterMaster)
        {
            return CanRevive(characterMaster, customRevives.Where(item => item.priority <= 0).ToArray());
        }

        private static bool CanRevive(CharacterMaster characterMaster, CustomRevive[] customRevives)
        {
            foreach (CustomRevive customRevive in customRevives)
            {
                if (customRevive.canRevive == null) continue;
                if (customRevive.canRevive.Invoke(characterMaster))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ReviveBeforeVanilla(CharacterMaster characterMaster)
        {
            return Revive(characterMaster, customRevives.Where(item => item.priority > 0).ToArray());
        }

        private static bool ReviveAfterVanilla(CharacterMaster characterMaster)
        {
            return Revive(characterMaster, customRevives.Where(item => item.priority <= 0).ToArray());
        }

        private static bool Revive(CharacterMaster characterMaster, CustomRevive[] customRevives)
        {
            foreach (CustomRevive customRevive in customRevives)
            {
                if (customRevive.canRevive == null) continue;
                if (customRevive.canRevive.Invoke(characterMaster))
                {
                    customRevive.onRevive?.Invoke(characterMaster);
                    if (customRevive.pendingOnRevives != null)
                    {
                        foreach (PendingOnRevive pendingOnRevive in customRevive.pendingOnRevives)
                        {
                            PendingOnRevive pendingOnRevive1 = new PendingOnRevive
                            {
                                timer = Mathf.Max(1f, pendingOnRevive.timer),
                                characterMaster = characterMaster,
                                onReviveDelegate = pendingOnRevive.onReviveDelegate
                            };
                            pendingRevives.Add(pendingOnRevive1);
                        }
                    }

                    return true;
                }
            }
            return false;
        }

        private static bool CanReviveAndOrRevive(CharacterMaster characterMaster, bool reverse, bool onRevive)
        {
            bool canRevive = reverse;
            foreach (CustomRevive customRevive in customRevives)
            {
                if (customRevive.canRevive == null) continue;
                if (customRevive.canRevive.Invoke(characterMaster))
                {
                    canRevive = !canRevive;
                    if (onRevive)
                    {
                        customRevive.onRevive?.Invoke(characterMaster);
                        if (customRevive.pendingOnRevives != null)
                        {
                            foreach (PendingOnRevive pendingOnRevive in customRevive.pendingOnRevives)
                            {
                                PendingOnRevive pendingOnRevive1 = new PendingOnRevive
                                {
                                    timer = Mathf.Max(1f, pendingOnRevive.timer),
                                    characterMaster = characterMaster,
                                    onReviveDelegate = pendingOnRevive.onReviveDelegate
                                };
                                pendingRevives.Add(pendingOnRevive1);
                            }
                        }
                    }
                    break;
                }
            }

            return canRevive;
        }
        #endregion

        /// <summary>
        /// Add custom revive
        /// </summary>
        /// <param name="canReviveDelegate">Revive condition.</param>
        /// <param name="priority">Priority, values above zero run before vanilla, values below and equal to zero run after vanilla.</param>
        public static void AddCustomRevive(CanReviveDelegate canReviveDelegate, int priority)
        {
            CustomRevive customRevive = new CustomRevive
            {
                canRevive = canReviveDelegate,
                pendingOnRevives = defaultPendingOnRevives,
                priority = priority
            };
            AddCustomRevive(customRevive);
        }

        /// <summary>
        /// Add custom revive
        /// </summary>
        /// <param name="canReviveDelegate">Revive condition.</param>
        /// <param name="onReviveDelegate">On revive action.</param>
        /// <param name="priority">Priority, values above zero run before vanilla, values below and equal to zero run after vanilla.</param>
        public static void AddCustomRevive(CanReviveDelegate canReviveDelegate, OnReviveDelegate onReviveDelegate, int priority)
        {
            CustomRevive customRevive = new CustomRevive
            {
                canRevive = canReviveDelegate,
                onRevive = onReviveDelegate,
                pendingOnRevives = defaultPendingOnRevives,
                priority = priority
            };
            AddCustomRevive(customRevive);
        }

        /// <summary>
        /// Add custom revive
        /// </summary>
        /// <param name="canReviveDelegate">Revive condition.</param>
        /// <param name="pendingOnRevives">On revive actions that will invoke on timer.</param>
        /// <param name="priority">Priority, values above zero run before vanilla, values below and equal to zero run after vanilla.</param>
        public static void AddCustomRevive(CanReviveDelegate canReviveDelegate, PendingOnRevive[] pendingOnRevives, int priority)
        {
            if (pendingOnRevives == null) pendingOnRevives = defaultPendingOnRevives;
            CustomRevive customRevive = new CustomRevive
            {
                canRevive = canReviveDelegate,
                pendingOnRevives = pendingOnRevives,
                priority = priority
            };
            AddCustomRevive(customRevive);
        }

        /// <summary>
        /// Add custom revive
        /// </summary>
        /// <param name="canReviveDelegate">Revive condition.</param>
        /// <param name="onReviveDelegate">On revive action.</param>
        /// <param name="pendingOnRevives">On revive actions that will invoke on timer.</param>
        /// <param name="priority">Priority, values above zero run before vanilla, values below and equal to zero run after vanilla.</param>
        public static void AddCustomRevive(CanReviveDelegate canReviveDelegate, OnReviveDelegate onReviveDelegate, PendingOnRevive[] pendingOnRevives, int priority)
        {
            if (pendingOnRevives == null) pendingOnRevives = defaultPendingOnRevives;
            CustomRevive customRevive = new CustomRevive
            {
                canRevive = canReviveDelegate,
                onRevive = onReviveDelegate,
                pendingOnRevives = pendingOnRevives,
                priority = priority
            };
            AddCustomRevive(customRevive);
        }

        /// <summary>
        /// Add custom revive
        /// </summary>
        /// <param name="canReviveDelegate">Revive condition.</param>
        public static void AddCustomRevive(CanReviveDelegate canReviveDelegate)
        {
            CustomRevive customRevive = new CustomRevive
            {
                canRevive = canReviveDelegate,
                pendingOnRevives = defaultPendingOnRevives,
                priority = -1
            };
            AddCustomRevive(customRevive);
        }

        /// <summary>
        /// Add custom revive
        /// </summary>
        /// <param name="canReviveDelegate">Revive condition.</param>
        /// <param name="onReviveDelegate">On revive action.</param>
        public static void AddCustomRevive(CanReviveDelegate canReviveDelegate, OnReviveDelegate onReviveDelegate)
        {
            CustomRevive customRevive = new CustomRevive
            {
                canRevive = canReviveDelegate,
                onRevive = onReviveDelegate,
                pendingOnRevives = defaultPendingOnRevives,
                priority = -1
            };
            AddCustomRevive(customRevive);
        }

        /// <summary>
        /// Add custom revive
        /// </summary>
        /// <param name="canReviveDelegate">Revive condition.</param>
        /// <param name="pendingOnRevives">On revive actions that will invoke on timer.</param>
        public static void AddCustomRevive(CanReviveDelegate canReviveDelegate, PendingOnRevive[] pendingOnRevives)
        {
            if (pendingOnRevives == null) pendingOnRevives = defaultPendingOnRevives;
            CustomRevive customRevive = new CustomRevive
            {
                canRevive = canReviveDelegate,
                pendingOnRevives = pendingOnRevives,
                priority = -1
            };
            AddCustomRevive(customRevive);
        }

        /// <summary>
        /// Add custom revive
        /// </summary>
        /// <param name="canReviveDelegate">Revive condition.</param>
        /// <param name="onReviveDelegate">On revive action.</param>
        /// <param name="pendingOnRevives">On revive actions that will invoke on timer.</param>
        public static void AddCustomRevive(CanReviveDelegate canReviveDelegate, OnReviveDelegate onReviveDelegate, PendingOnRevive[] pendingOnRevives)
        {
            if (pendingOnRevives == null) pendingOnRevives = defaultPendingOnRevives;
            CustomRevive customRevive = new CustomRevive
            {
                canRevive = canReviveDelegate,
                onRevive = onReviveDelegate,
                pendingOnRevives = pendingOnRevives,
                priority = -1
            };
            AddCustomRevive(customRevive);
        }

        /// <summary>
        /// Add custom revive
        /// </summary>
        /// <param name="customRevive">Custom Revive class.</param>
        public static void AddCustomRevive(CustomRevive customRevive)
        {
            customRevives.Add(customRevive);
            customRevives = customRevives.OrderByDescending(x => x.priority).ToList();
        }
        
    }
}