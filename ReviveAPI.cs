using BepInEx;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System.Collections.Generic;
using System.Security;
using System.Security.Permissions;
using UnityEngine;

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
        public const string ModVer = "1.0.0";
        public delegate bool CanReviveDelegate(CharacterMaster characterMaster);
        public delegate void OnReviveDelegate(CharacterMaster characterMaster);
        public class CustomRevive
        {
            public CanReviveDelegate canRevive;
            public OnReviveDelegate onRevive;
        }
        private static List<CustomRevive> customRevives = [];
        public void Awake()
        {
            SetHooks();
        }
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
        /// <param name="onReviveDelegate">On revive action.</param>
        public static void AddCustomRevive(CanReviveDelegate canReviveDelegate, OnReviveDelegate onReviveDelegate)
        {
            CustomRevive customRevive = new CustomRevive
            {
                canRevive = canReviveDelegate,
                onRevive = onReviveDelegate
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