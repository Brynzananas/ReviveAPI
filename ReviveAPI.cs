using BepInEx;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System.Collections.Generic;
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
        public const string ModVer = "1.1.0";
        public delegate bool CanReviveDelegate(CharacterMaster characterMaster);
        public delegate void OnReviveDelegate(CharacterMaster characterMaster);
        public class CustomRevive
        {
            public CanReviveDelegate canRevive;
            public OnReviveDelegate onRevive;
            public PendingOnRevive[] pendingOnRevives;
        }
        public class PendingOnRevive
        {
            internal CharacterMaster characterMaster;
            public float timer;
            public OnReviveDelegate onReviveDelegate;
        }
        private static List<PendingOnRevive> pendingRevives = [];
        private static List<CustomRevive> customRevives = [];
        public void Awake()
        {
            SetHooks();
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
                return [pendingSimpleRespawn, pendingSimpleRespawnSound];
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
            IL.RoR2.Artifacts.TeamDeathArtifactManager.OnServerCharacterDeathGlobal += ILHook1;
            IL.RoR2.Artifacts.DoppelgangerInvasionManager.OnCharacterDeathGlobal += ILHook3;
            IL.RoR2.CharacterMaster.IsDeadAndOutOfLivesServer += ILHook2;
            IL.RoR2.CharacterMaster.OnBodyDeath += ILHook4;
            hooksSet = true;
        }
        private void UnsetHooks()
        {
            if (!hooksSet) return;
            IL.RoR2.Artifacts.TeamDeathArtifactManager.OnServerCharacterDeathGlobal -= ILHook1;
            IL.RoR2.Artifacts.DoppelgangerInvasionManager.OnCharacterDeathGlobal -= ILHook3;
            IL.RoR2.CharacterMaster.IsDeadAndOutOfLivesServer -= ILHook2;
            IL.RoR2.CharacterMaster.OnBodyDeath -= ILHook4;
            hooksSet = false;
        }
        private void ILHook1(ILContext iLContext)
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
                c.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(DamageReport), nameof(DamageReport.victimMaster)));
                c.Emit(OpCodes.Ldc_I4_0);
                c.Emit(OpCodes.Ldc_I4_0);
                c.EmitDelegate(CanRevive);
                c.Emit(OpCodes.Brtrue_S, iLLabel);
            }
            else
            {
                Logger.LogError(iLContext.Method.Name + " IL Hook failed!");
            }
        }
        private void ILHook2(ILContext iLContext)
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
                c.Emit(OpCodes.Ldc_I4_1);
                c.Emit(OpCodes.Ldc_I4_0);
                c.EmitDelegate(CanRevive);
                c.Emit(OpCodes.Brtrue_S, iLLabel);
            }
            else
            {
                Logger.LogError(iLContext.Method.Name + " IL Hook failed!");
            }
        }
        private void ILHook3(ILContext iLContext)
        {
            ILCursor c = new ILCursor(iLContext);
            ILLabel iLLabel = null;
            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.ExtraLife)),
                    x => x.MatchCallvirt<Inventory>(nameof(Inventory.GetItemCount)),
                    x => x.MatchBrtrue(out iLLabel)
                ))
            {
                c.Emit(OpCodes.Ldloc_0);
                c.Emit(OpCodes.Ldc_I4_1);
                c.Emit(OpCodes.Ldc_I4_0);
                c.EmitDelegate(CanRevive);
                c.Emit(OpCodes.Brtrue_S, iLLabel);
            }
            else
            {
                Logger.LogError(iLContext.Method.Name + " IL Hook failed!");
            }
        }
        private void ILHook4(ILContext iLContext)
        {
            ILCursor c = new ILCursor(iLContext);
            ILLabel iLLabel = null;
            ILLabel iLLabel2 = null;
            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.ExtraLifeVoid)),
                    x => x.MatchCallvirt<Inventory>(nameof(Inventory.GetItemCount)),
                    x => x.MatchLdcI4(0),
                    x => x.MatchBle(out iLLabel)
                ))
            {
                if (c.TryGotoNext(MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdstr(out _),
                    x => x.MatchLdcR4(1f),
                    x => x.MatchCall<MonoBehaviour>(nameof(MonoBehaviour.Invoke)),
                    x => x.MatchBr(out iLLabel2)
                ))
                {
                    c.GotoLabel(iLLabel, MoveType.Before);
                    Instruction instruction = c.Emit(OpCodes.Ldarg_0).Prev;
                    iLLabel.Target = instruction;
                    c.Emit(OpCodes.Ldc_I4_0);
                    c.Emit(OpCodes.Ldc_I4_1);
                    c.EmitDelegate(CanRevive);
                    c.Emit(OpCodes.Brtrue_S, iLLabel2);
                }
                else
                {
                    Logger.LogError(iLContext.Method.Name + " IL Hook 2 failed!");
                }
            }
            else
            {
                Logger.LogError(iLContext.Method.Name + " IL Hook 1 failed!");
            }
        }
        private static bool CanRevive(CharacterMaster characterMaster, bool reverse, bool onRevive)
        {
            bool canRevive = reverse;
            foreach (CustomRevive customRevive in customRevives)
            {
                if (customRevive.canRevive == null) continue;
                if (customRevive.canRevive.Invoke(characterMaster))
                {
                    canRevive = !canRevive;
                    if (onRevive) customRevive.onRevive?.Invoke(characterMaster);
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
        public static void AddCustomRevive(CanReviveDelegate canReviveDelegate)
        {
            CustomRevive customRevive = new CustomRevive
            {
                canRevive = canReviveDelegate,
                pendingOnRevives = defaultPendingOnRevives
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
                pendingOnRevives = defaultPendingOnRevives
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
                pendingOnRevives = pendingOnRevives
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
                pendingOnRevives = pendingOnRevives
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
        }
        
    }
}