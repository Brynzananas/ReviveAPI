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
        public const string ModVer = "1.3.1";
        public const bool ENABLE_TEST = false;
        public static ManualLogSource ManualLogger;

        public delegate bool CanReviveDelegate(CharacterMaster characterMaster);
        public delegate bool CanReviveNewDelegate(CharacterMaster characterMaster, out CanReviveInfo canReviveInfo);
        public delegate void OnReviveDelegate(CharacterMaster characterMaster);
        public delegate void OnReviveNewDelegate(CharacterMaster characterMaster, CanReviveInfo canReviveInfo);
        public class CanReviveInfo
        {
            public Inventory.ItemTransformation.CanTakeResult canTakeResult;
        }
        public class CustomRevive
        {
            [Obsolete("Use canReviveNew instead")]
            public CanReviveDelegate canRevive;
            public CanReviveNewDelegate canReviveNew;
            public OnReviveDelegate onRevive;
            public OnReviveNewDelegate onReviveNew;
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
            //if (ENABLE_TEST)
            //{
            //    Logger.LogWarning("ReviveAPI TESTS ARE ENABLED!");
            //    Tests.AddRevives();
            //}
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
            //IL.RoR2.CharacterMaster.OnBodyDeath += CharacterMaster_OnBodyDeath;
            On.RoR2.CharacterMaster.IsExtraLifePendingServer += CharacterMaster_IsExtraLifePendingServer;
            On.RoR2.CharacterMaster.TryReviveOnBodyDeath += CharacterMaster_TryReviveOnBodyDeath;
            RoR2Application.onLoadFinished += OnGameLoaded;
            hooksSet = true;
        }

        private bool CharacterMaster_TryReviveOnBodyDeath(On.RoR2.CharacterMaster.orig_TryReviveOnBodyDeath orig, CharacterMaster self, CharacterBody body)
        {
            if (CanReviveBeforeVanilla(self)) return ReviveBeforeVanilla(self);
            bool flag = orig(self, body);
            if (!flag && CanReviveAfterVanilla(self)) flag = ReviveAfterVanilla(self);
            return flag;
        }

        private void OnGameLoaded()
        {
            if (ENABLE_TEST)
            {
                Logger.LogWarning("ReviveAPI TESTS ARE ENABLED!");
                Tests.AddRevives();
            }
        }

        private void UnsetHooks()
        {
            if (!hooksSet) return;
            IL.RoR2.Artifacts.TeamDeathArtifactManager.OnServerCharacterDeathGlobal -= TeamDeathArtifactManager_OnServerCharacterDeathGlobal;
            IL.RoR2.Artifacts.DoppelgangerInvasionManager.OnCharacterDeathGlobal -= DoppelgangerInvasionManager_OnCharacterDeathGlobal;
            IL.RoR2.CharacterMaster.IsDeadAndOutOfLivesServer -= CharacterMaster_IsDeadAndOutOfLivesServer;
            //IL.RoR2.CharacterMaster.OnBodyDeath -= CharacterMaster_OnBodyDeath;
            On.RoR2.CharacterMaster.IsExtraLifePendingServer -= CharacterMaster_IsExtraLifePendingServer;
            On.RoR2.CharacterMaster.TryReviveOnBodyDeath -= CharacterMaster_TryReviveOnBodyDeath;
            RoR2Application.onLoadFinished -= OnGameLoaded;
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
                    x => x.MatchCallvirt<Inventory>(nameof(Inventory.GetItemCountEffective)),
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
                x => x.MatchCallvirt<Inventory>(nameof(Inventory.GetItemCountEffective)),
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
        }

        private void CharacterMaster_OnBodyDeath(ILContext iLContext)
        {
            ILCursor c = new ILCursor(iLContext);
            Instruction instruction = null;
            Instruction instruction2 = null;
            ILLabel ilLabel = null;
            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdarg(0),
                x => x.MatchLdloca(out _),
                x => x.MatchCall<UnityEngine.Component>(nameof(UnityEngine.Component.TryGetComponent)),
                x => x.MatchBrfalse(out _)))
            {
                instruction = c.Next;
            } else
            {
                Logger.LogError(iLContext.Method.Name + " IL hook 1 failed!");
            }
            c = new ILCursor(iLContext);
            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdarg(1),
                x => x.MatchLdsfld(typeof(DLC2Content.Buffs), nameof(DLC2Content.Buffs.ExtraLifeBuff)),
                x => x.MatchCallvirt<CharacterBody>(nameof(CharacterBody.HasBuff)),
                x => x.MatchBrfalse(out _)))
            {
                instruction2 = c.Next;
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate(CanReviveBeforeVanilla);
                c.Emit(OpCodes.Brfalse_S, instruction2);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate(ReviveBeforeVanilla);    
                c.Emit(OpCodes.Brtrue_S, instruction);
            }
            else
            {
                Logger.LogError(iLContext.Method.Name + " IL hook 2 failed!");
            }
            c = new ILCursor(iLContext);
            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdloca(5),
                x => x.MatchLdarg(0),
                x => x.MatchCall<CharacterMaster>("get_inventory"),
                x => x.MatchLdloca(1),
                x => x.MatchCall<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTake)),
                x => x.MatchBrfalse(out _))
                &&
                c.TryGotoNext(MoveType.Before,
                x => x.MatchLdloca(5),
                x => x.MatchLdarg(0),
                x => x.MatchCall<CharacterMaster>("get_inventory"),
                x => x.MatchLdloca(1),
                x => x.MatchCall<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTake)),
                x => x.MatchBrfalse(out ilLabel)))
            {
                Instruction instruction1 = ilLabel.Target;
                c.GotoLabel(ilLabel, MoveType.Before);
                ilLabel.Target = c.Emit(OpCodes.Ldarg_0).Prev;
                c.EmitDelegate(CanReviveAfterVanilla);
                c.Emit(OpCodes.Brfalse_S, instruction1);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate(ReviveAfterVanilla);
                c.Emit(OpCodes.Brtrue_S, instruction);
            }
            else
            {
                Logger.LogError(iLContext.Method.Name + " IL hook 3 failed!");
            }
        }
        private static bool CanReviveBeforeVanilla(CharacterMaster characterMaster) => CanRevive(characterMaster, customRevives.Where(item => item.priority > 0).ToArray());
        private static bool CanReviveAfterVanilla(CharacterMaster characterMaster) => CanRevive(characterMaster, customRevives.Where(item => item.priority <= 0).ToArray());
        private static CanReviveInfo canReviveInfoGlobal;
        private static CustomRevive customReviveGlobal;
        private static bool CanRevive(CharacterMaster characterMaster, CustomRevive[] customRevives)
        {
            foreach (CustomRevive customRevive in customRevives)
            {
                if (customRevive.canRevive != null && customRevive.canRevive.Invoke(characterMaster)) return true;
                if (customRevive.canReviveNew != null && customRevive.canReviveNew.Invoke(characterMaster, out CanReviveInfo canReviveInfo))
                {
                    canReviveInfoGlobal = canReviveInfo;
                    return true;
                }
            }
            return false;
        }
        private static bool ReviveBeforeVanilla(CharacterMaster characterMaster) => Revive(characterMaster, customRevives.Where(item => item.priority > 0).ToArray());
        private static bool ReviveAfterVanilla(CharacterMaster characterMaster) => Revive(characterMaster, customRevives.Where(item => item.priority <= 0).ToArray());
        private static bool Revive(CharacterMaster characterMaster, CustomRevive[] customRevives)
        {
            foreach (CustomRevive customRevive in customRevives)
            {
                if (customRevive.canRevive != null && customRevive.canRevive.Invoke(characterMaster))
                {
                    customRevive.onRevive?.Invoke(characterMaster);
                    if (customRevive.pendingOnRevives != null) HandlePendingOnRevives(customRevive, characterMaster);
                    return true;
                }
                if (customRevive.canReviveNew != null && customRevive.canReviveNew.Invoke(characterMaster, out canReviveInfoGlobal))
                {
                    customRevive.onReviveNew?.Invoke(characterMaster, canReviveInfoGlobal);
                    if (customRevive.pendingOnRevives != null) HandlePendingOnRevives(customRevive, characterMaster);
                    return true;
                }
            }
            return false;
        }
        private static void HandlePendingOnRevives(CustomRevive customRevive, CharacterMaster characterMaster)
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
        /// <param name="canReviveNewDelegate">Revive condition.</param>
        /// <param name="priority">Priority, values above zero run before vanilla, values below and equal to zero run after vanilla.</param>
        public static void AddCustomRevive(CanReviveNewDelegate canReviveNewDelegate, int priority)
        {
            CustomRevive customRevive = new CustomRevive
            {
                canReviveNew = canReviveNewDelegate,
                pendingOnRevives = defaultPendingOnRevives,
                priority = priority
            };
            AddCustomRevive(customRevive);
        }

        /// <summary>
        /// Add custom revive
        /// </summary>
        /// <param name="canReviveNewDelegate">Revive condition.</param>
        /// <param name="onReviveNewDelegate">On revive action.</param>
        /// <param name="priority">Priority, values above zero run before vanilla, values below and equal to zero run after vanilla.</param>
        public static void AddCustomRevive(CanReviveNewDelegate canReviveNewDelegate, OnReviveNewDelegate onReviveNewDelegate, int priority)
        {
            CustomRevive customRevive = new CustomRevive
            {
                canReviveNew = canReviveNewDelegate,
                onReviveNew = onReviveNewDelegate,
                pendingOnRevives = null,
                priority = priority
            };
            AddCustomRevive(customRevive);
        }
        /*
        /// <summary>
        /// Add custom revive
        /// </summary>
        /// <param name="canReviveDelegate">Revive condition.</param>
        /// <param name="pendingOnRevives">On revive actions that will invoke on timer.</param>
        /// <param name="priority">Priority, values above zero run before vanilla, values below and equal to zero run after vanilla.</param>
        public static void AddCustomRevive(CanReviveDelegate canReviveDelegate, PendingOnRevive[] pendingOnRevives, int priority)
        {
            CustomRevive customRevive = new CustomRevive
            {
                canRevive = canReviveDelegate,
                pendingOnRevives = pendingOnRevives,
                priority = priority
            };
            AddCustomRevive(customRevive);
        }
        */

        /// <summary>
        /// Add custom revive
        /// </summary>
        /// <param name="canReviveNewDelegate">Revive condition.</param>
        /// <param name="onReviveNewDelegate">On revive action.</param>
        /// <param name="pendingOnRevives">On revive actions that will invoke on timer.</param>
        /// <param name="priority">Priority, values above zero run before vanilla, values below and equal to zero run after vanilla.</param>
        public static void AddCustomRevive(CanReviveNewDelegate canReviveNewDelegate, OnReviveNewDelegate onReviveNewDelegate, PendingOnRevive[] pendingOnRevives, int priority)
        {
            CustomRevive customRevive = new CustomRevive
            {
                canReviveNew = canReviveNewDelegate,
                onReviveNew = onReviveNewDelegate,
                pendingOnRevives = pendingOnRevives,
                priority = priority
            };
            AddCustomRevive(customRevive);
        }

        /// <summary>
        /// Add custom revive
        /// </summary>
        /// <param name="canReviveNewDelegate">Revive condition.</param>
        public static void AddCustomRevive(CanReviveNewDelegate canReviveNewDelegate)
        {
            CustomRevive customRevive = new CustomRevive
            {
                canReviveNew = canReviveNewDelegate,
                pendingOnRevives = defaultPendingOnRevives,
                priority = -1
            };
            AddCustomRevive(customRevive);
        }

        /// <summary>
        /// Add custom revive
        /// </summary>
        /// <param name="canReviveNewDelegate">Revive condition.</param>
        /// <param name="onReviveNewDelegate">On revive action.</param>
        public static void AddCustomRevive(CanReviveNewDelegate canReviveNewDelegate, OnReviveNewDelegate onReviveNewDelegate)
        {
            CustomRevive customRevive = new CustomRevive
            {
                canReviveNew = canReviveNewDelegate,
                onReviveNew = onReviveNewDelegate,
                pendingOnRevives = null,
                priority = -1
            };
            AddCustomRevive(customRevive);
        }

        /// <summary>
        /// Add custom revive
        /// </summary>
        /// <param name="canReviveNewDelegate">Revive condition.</param>
        /// <param name="pendingOnRevives">On revive actions that will invoke on timer.</param>
        public static void AddCustomRevive(CanReviveNewDelegate canReviveNewDelegate, PendingOnRevive[] pendingOnRevives)
        {
            CustomRevive customRevive = new CustomRevive
            {
                canReviveNew = canReviveNewDelegate,
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
        public static void AddCustomRevive(CanReviveNewDelegate canReviveNewDelegate, OnReviveNewDelegate onReviveNewDelegate, PendingOnRevive[] pendingOnRevives)
        {
            CustomRevive customRevive = new CustomRevive
            {
                canReviveNew = canReviveNewDelegate,
                onReviveNew = onReviveNewDelegate,
                pendingOnRevives = pendingOnRevives,
                priority = -1
            };
            AddCustomRevive(customRevive);
        }
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