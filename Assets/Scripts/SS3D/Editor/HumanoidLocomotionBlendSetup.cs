using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SS3D.Editor
{
    /// <summary>
    /// Rebuilds Base Layer locomotion into Peaceful / Melee / Ranged / Injured
    /// FreeformCartesian2D blend trees switched by CombatStance and LimpSide.
    /// </summary>
    public static class HumanoidLocomotionBlendSetup
    {
        private const string ControllerPath =
            "Assets/Content/WorldObjects/Entities/Humanoids/Human/HumanCharacterAnimator.controller";
        private const string LocomotionPack = "Assets/Art/Animations/Locomotion Pack";
        private const string MeleePack = "Assets/Art/Animations/Pro Melee Axe Pack";
        private const string ShooterPack = "Assets/Art/Animations/Basic Shooter Pack";
        private const string InjuredPack = "Assets/Art/Animations/Male Injured Pack";

        // Soft blend into/out of hit flinch (Any State entry + exit-time return to loco).
        private const float FlinchEntryDuration = 0.2f;
        private const float FlinchExitDuration = 0.28f;
        private const float FlinchExitTime = 0.7f;

        private static readonly (string File, string ClipName, Vector2 Pos)[] PeacefulClips =
        {
            ("idle.fbx", "Mix_Idle", new Vector2(0f, 0f)),
            ("walking.fbx", "Mix_Walking", new Vector2(0f, 0.3f)),
            ("running.fbx", "Mix_Running", new Vector2(0f, 1f)),
            ("left strafe walking.fbx", "Mix_LeftStrafeWalking", new Vector2(-1f, 0.3f)),
            ("right strafe walking.fbx", "Mix_RightStrafeWalking", new Vector2(1f, 0.3f)),
            ("left strafe.fbx", "Mix_LeftStrafe", new Vector2(-1f, 1f)),
            ("right strafe.fbx", "Mix_RightStrafe", new Vector2(1f, 1f)),
            ("left strafe walking.fbx", "Mix_LeftStrafeWalking", new Vector2(-1f, 0f)),
            ("right strafe walking.fbx", "Mix_RightStrafeWalking", new Vector2(1f, 0f)),
        };

        private static readonly (string File, string ClipName, Vector2 Pos)[] MeleeClips =
        {
            ("standing idle.fbx", "Mix_StandingIdle", new Vector2(0f, 0f)),
            ("standing walk forward.fbx", "Mix_StandingWalkForward", new Vector2(0f, 0.3f)),
            ("standing walk back.fbx", "Mix_StandingWalkBack", new Vector2(0f, -0.3f)),
            // Walk-magnitude strafes must sit at ±0.3 — VelX/VelZ use gait 0.3 for walk.
            ("standing walk left.fbx", "Mix_StandingWalkLeft", new Vector2(-0.3f, 0.3f)),
            ("standing walk right.fbx", "Mix_StandingWalkRight", new Vector2(0.3f, 0.3f)),
            ("standing walk left.fbx", "Mix_StandingWalkLeft", new Vector2(-0.3f, 0f)),
            ("standing walk right.fbx", "Mix_StandingWalkRight", new Vector2(0.3f, 0f)),
            ("standing run forward.fbx", "Mix_StandingRunForward", new Vector2(0f, 1f)),
            ("standing run back.fbx", "Mix_StandingRunBack", new Vector2(0f, -1f)),
            // Run-magnitude strafe samples (pack has no dedicated run-strafe clips).
            ("standing walk left.fbx", "Mix_StandingWalkLeft", new Vector2(-1f, 1f)),
            ("standing walk right.fbx", "Mix_StandingWalkRight", new Vector2(1f, 1f)),
            ("standing walk left.fbx", "Mix_StandingWalkLeft", new Vector2(-1f, 0f)),
            ("standing walk right.fbx", "Mix_StandingWalkRight", new Vector2(1f, 0f)),
        };

        private static readonly (string File, string ClipName, Vector2 Pos)[] RangedClips =
        {
            ("rifle aiming idle.fbx", "Mix_AimingIdle", new Vector2(0f, 0f)),
            ("walking.fbx", "Mix_RifleWalking", new Vector2(0f, 0.3f)),
            ("walking backwards.fbx", "Mix_RifleWalkingBackwards", new Vector2(0f, -0.3f)),
            ("strafe left.fbx", "Mix_RifleStrafeLeft", new Vector2(-0.3f, 0.3f)),
            ("strafe right.fbx", "Mix_RifleStrafeRight", new Vector2(0.3f, 0.3f)),
            ("strafe left.fbx", "Mix_RifleStrafeLeft", new Vector2(-0.3f, 0f)),
            ("strafe right.fbx", "Mix_RifleStrafeRight", new Vector2(0.3f, 0f)),
            ("strafe (2).fbx", "Mix_RifleStrafeLeftFast", new Vector2(-1f, 1f)),
            ("strafe.fbx", "Mix_RifleStrafeRightFast", new Vector2(1f, 1f)),
            ("strafe left.fbx", "Mix_RifleStrafeLeft", new Vector2(-1f, 0f)),
            ("strafe right.fbx", "Mix_RifleStrafeRight", new Vector2(1f, 0f)),
            ("rifle run.fbx", "Mix_RifleRun", new Vector2(0f, 1f)),
            ("run backwards.fbx", "Mix_RunBackwards", new Vector2(0f, -1f)),
        };

        /// <summary>
        /// Male Injured Pack has no dedicated strafes — sample walk/run forward/back and reuse
        /// walk at lateral positions so VelX still blends.
        /// </summary>
        private static readonly (string File, string ClipName, Vector2 Pos)[] InjuredClips =
        {
            ("injured idle.fbx", "Mix_InjuredIdle", new Vector2(0f, 0f)),
            ("injured walk.fbx", "Mix_InjuredWalk", new Vector2(0f, 0.3f)),
            ("injured walk backwards.fbx", "Mix_InjuredWalkBackwards", new Vector2(0f, -0.3f)),
            ("injured walk.fbx", "Mix_InjuredWalk", new Vector2(-0.3f, 0.3f)),
            ("injured walk.fbx", "Mix_InjuredWalk", new Vector2(0.3f, 0.3f)),
            ("injured walk.fbx", "Mix_InjuredWalk", new Vector2(-0.3f, 0f)),
            ("injured walk.fbx", "Mix_InjuredWalk", new Vector2(0.3f, 0f)),
            ("injured run.fbx", "Mix_InjuredRun", new Vector2(0f, 1f)),
            ("injured run backwards.fbx", "Mix_InjuredRunBackwards", new Vector2(0f, -1f)),
            ("injured walk.fbx", "Mix_InjuredWalk", new Vector2(-1f, 1f)),
            ("injured walk.fbx", "Mix_InjuredWalk", new Vector2(1f, 1f)),
            ("injured walk.fbx", "Mix_InjuredWalk", new Vector2(-1f, 0f)),
            ("injured walk.fbx", "Mix_InjuredWalk", new Vector2(1f, 0f)),
        };

        /// <summary>
        /// Parameters expected by <c>Animations.Humanoid</c> / AnimationOrchestrator.
        /// Order is preserved when force-rebinding so the Animator window stays readable.
        /// </summary>
        private static readonly (string Name, AnimatorControllerParameterType Type)[] RequiredHumanoidParameters =
        {
            ("Speed", AnimatorControllerParameterType.Float),
            ("Floating", AnimatorControllerParameterType.Bool),
            ("LimpSide", AnimatorControllerParameterType.Int),
            ("IsCrawling", AnimatorControllerParameterType.Bool),
            ("IsDragging", AnimatorControllerParameterType.Bool),
            ("ArmHold", AnimatorControllerParameterType.Int),
            ("InjuredArmLeft", AnimatorControllerParameterType.Float),
            ("InjuredArmRight", AnimatorControllerParameterType.Float),
            ("InjuredLeg", AnimatorControllerParameterType.Float),
            ("IsSeated", AnimatorControllerParameterType.Bool),
            ("CombatMode", AnimatorControllerParameterType.Bool),
            ("CombatStance", AnimatorControllerParameterType.Int),
            ("AimYaw", AnimatorControllerParameterType.Float),
            ("AimPitch", AnimatorControllerParameterType.Float),
            ("AttackSwing", AnimatorControllerParameterType.Trigger),
            ("AttackStab", AnimatorControllerParameterType.Trigger),
            ("AttackVariant", AnimatorControllerParameterType.Int),
            ("MirrorUpperBody", AnimatorControllerParameterType.Bool),
            ("Throw", AnimatorControllerParameterType.Trigger),
            ("Emote", AnimatorControllerParameterType.Trigger),
            ("Flinch", AnimatorControllerParameterType.Trigger),
            ("FireRifle", AnimatorControllerParameterType.Trigger),
            ("Reload", AnimatorControllerParameterType.Trigger),
            ("VelX", AnimatorControllerParameterType.Float),
            ("VelZ", AnimatorControllerParameterType.Float),
            ("Turn", AnimatorControllerParameterType.Float),
            ("Jump", AnimatorControllerParameterType.Trigger),
            ("TurnLeft90", AnimatorControllerParameterType.Trigger),
            ("TurnRight90", AnimatorControllerParameterType.Trigger),
        };

        [MenuItem("SS3D/Animation/Rebuild Combat Stance Blend Trees")]
        public static void RebuildCombatStanceBlendTreesMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "Rebuild Combat Stance Blend Trees",
                    "Rebuild Base Layer Peaceful / Melee / Ranged / Injured FreeformCartesian2D locomotion " +
                    "switched by CombatStance + LimpSide. Restores AttackSwing trigger on Upper Body; " +
                    "wires stance-aware Flinch (GettingHit / gut / HitReaction); Ranged FireRifle/Reload oneshots; " +
                    "remaps injured-arm additive.\n\n" +
                    "Modifies HumanCharacterAnimator.controller.",
                    "Rebuild",
                    "Cancel"))
            {
                return;
            }

            string result = RebuildCombatStanceBlendTrees();
            EditorUtility.DisplayDialog("Rebuild Combat Stance Blend Trees", result, "OK");
        }

        /// <summary>
        /// Drop <c>artifacts/force-rebuild-animator.flag</c> then wait for script reload —
        /// used when batchmode cannot open the project because the interactive Editor holds it.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void RebuildFromFlagIfPresent()
        {
            const string flagPath = "artifacts/force-rebuild-animator.flag";
            if (!System.IO.File.Exists(flagPath))
            {
                return;
            }

            try
            {
                System.IO.File.Delete(flagPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HumanoidLocomotionBlendSetup] Could not delete rebuild flag: {ex.Message}");
                return;
            }

            string result = RebuildCombatStanceBlendTrees();
            Debug.Log($"[HumanoidLocomotionBlendSetup] Flag-triggered rebuild: {result}");
            System.IO.File.WriteAllText("artifacts/force-rebuild-animator.result", result);
        }

        /// <summary>Batchmode: -executeMethod SS3D.Editor.HumanoidLocomotionBlendSetup.RebuildCombatStanceBlendTreesBatch</summary>
        public static void RebuildCombatStanceBlendTreesBatch()
        {
            string result = RebuildCombatStanceBlendTrees();
            Debug.Log($"[HumanoidLocomotionBlendSetup] {result}");
            if (result.StartsWith("ERROR"))
            {
                EditorApplication.Exit(1);
            }
        }

        public static void RebuildLocomotionBlendTreeBatch() => RebuildCombatStanceBlendTreesBatch();

        [MenuItem("SS3D/Animation/Rebind Humanoid Animator Parameters")]
        public static void RebindHumanoidAnimatorParametersMenu()
        {
            string result = RebindHumanoidAnimatorParameters();
            EditorUtility.DisplayDialog("Rebind Humanoid Animator Parameters", result, "OK");
        }

        /// <summary>Batchmode: -executeMethod SS3D.Editor.HumanoidLocomotionBlendSetup.RebindHumanoidAnimatorParametersBatch</summary>
        public static void RebindHumanoidAnimatorParametersBatch()
        {
            string result = RebindHumanoidAnimatorParameters();
            Debug.Log($"[HumanoidLocomotionBlendSetup] {result}");
            if (result.StartsWith("ERROR"))
            {
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Force-removes and re-adds every orchestrator parameter through the AnimatorController API.
        /// Hand-edited YAML parameter entries can appear in the asset yet fail Animator.Set* at runtime.
        /// </summary>
        public static string RebindHumanoidAnimatorParameters()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                return $"ERROR: Missing controller at {ControllerPath}";
            }

            var before = controller.parameters.Select(p => $"{p.name}:{p.type}:{p.nameHash}").ToArray();
            Debug.Log($"[HumanoidLocomotionBlendSetup] Parameters before rebind ({before.Length}):\n - "
                      + string.Join("\n - ", before));

            // Remove required params by name (keep any unexpected extras).
            for (int i = controller.parameters.Length - 1; i >= 0; i--)
            {
                string name = controller.parameters[i].name;
                if (RequiredHumanoidParameters.Any(p => p.Name == name))
                {
                    controller.RemoveParameter(i);
                }
            }

            foreach ((string Name, AnimatorControllerParameterType Type) required in RequiredHumanoidParameters)
            {
                controller.AddParameter(required.Name, required.Type);
            }

            var after = controller.parameters.Select(p => $"{p.name}:{p.type}:{p.nameHash}").ToArray();
            Debug.Log($"[HumanoidLocomotionBlendSetup] Parameters after rebind ({after.Length}):\n - "
                      + string.Join("\n - ", after));

            // Verify hashes match Animator.StringToHash (what AnimationOrchestrator uses).
            var mismatches = new List<string>();
            foreach ((string Name, AnimatorControllerParameterType Type) required in RequiredHumanoidParameters)
            {
                AnimatorControllerParameter param = controller.parameters.FirstOrDefault(p => p.name == required.Name);
                int expected = Animator.StringToHash(required.Name);
                if (param == null)
                {
                    mismatches.Add($"{required.Name}: missing after rebind");
                }
                else if (param.nameHash != expected)
                {
                    mismatches.Add($"{required.Name}: nameHash {param.nameHash} != StringToHash {expected}");
                }
                else if (param.type != required.Type)
                {
                    mismatches.Add($"{required.Name}: type {param.type} != {required.Type}");
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (mismatches.Count > 0)
            {
                return "ERROR: Rebind finished with mismatches:\n - " + string.Join("\n - ", mismatches);
            }

            return $"OK: Re-bound {RequiredHumanoidParameters.Length} humanoid animator parameters via AnimatorController API.";
        }

        public static string RebuildCombatStanceBlendTrees()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                return $"ERROR: Missing controller at {ControllerPath}";
            }

            // Prefer a full API rebind so hand-edited YAML stubs cannot leave SetBool/SetInteger broken.
            string rebind = RebindHumanoidAnimatorParameters();
            if (rebind.StartsWith("ERROR"))
            {
                return rebind;
            }

            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            AnimatorStateMachine baseMachine = controller.layers[0].stateMachine;

            BlendTree peacefulTree = BuildBlendTree(controller, "Peaceful Locomotion 2D", LocomotionPack, PeacefulClips);
            BlendTree meleeTree = BuildBlendTree(controller, "Melee Locomotion 2D", MeleePack, MeleeClips);
            BlendTree rangedTree = BuildBlendTree(controller, "Ranged Locomotion 2D", ShooterPack, RangedClips);
            BlendTree injuredTree = BuildInjuredBlendTree(controller);
            if (peacefulTree == null || meleeTree == null || rangedTree == null || injuredTree == null)
            {
                return "ERROR: Failed to build one or more stance blend trees (check Mix_* clip names after reimport).";
            }

            AnimatorState peaceful = FindOrCreateState(baseMachine, "Peaceful Locomotion", new Vector3(300, 0, 0));
            AnimatorState melee = FindOrCreateState(baseMachine, "Melee Locomotion", new Vector3(300, 120, 0));
            AnimatorState ranged = FindOrCreateState(baseMachine, "Ranged Locomotion", new Vector3(300, 240, 0));
            AnimatorState injured = FindOrCreateState(baseMachine, "Injured Locomotion", new Vector3(550, 120, 0));
            peaceful.motion = peacefulTree;
            melee.motion = meleeTree;
            ranged.motion = rangedTree;
            injured.motion = injuredTree;
            baseMachine.defaultState = peaceful;

            // Retarget legacy Movement state if present.
            AnimatorState legacyMovement = FindState(baseMachine, "Movement");
            if (legacyMovement != null)
            {
                legacyMovement.motion = peacefulTree;
            }

            // Drop placeholder Limp Left/Right states — Injured Locomotion replaces them.
            RemoveStateIfPresent(baseMachine, "Limp Left");
            RemoveStateIfPresent(baseMachine, "Limp Right");

            ClearTransitions(peaceful);
            ClearTransitions(melee);
            ClearTransitions(ranged);
            ClearTransitions(injured);

            WireStanceTransitions(peaceful, melee, ranged);
            WireStanceTransitions(melee, peaceful, ranged);
            WireStanceTransitions(ranged, peaceful, melee);

            WireLimpEntry(peaceful, injured);
            WireLimpEntry(melee, injured);
            WireLimpEntry(ranged, injured);
            WireLimpExit(injured, peaceful, 0);
            WireLimpExit(injured, melee, 1);
            WireLimpExit(injured, ranged, 2);

            WireJumpAndTurnOneshots(controller, baseMachine, peaceful, melee, ranged, injured);
            WireInjuredWaveEmote(baseMachine, injured);

            WireStanceAwareFlinch(baseMachine, peaceful, melee, ranged, injured);
            WireRangedFireAndReload(baseMachine, peaceful, melee, ranged, injured);
            RemapStateMotion(
                baseMachine,
                "Floating",
                "Assets/Art/Animations/Misc/X Bot@Floating.fbx",
                "Mix_Floating");

            // Base layer must not consume AttackSwing — upper body owns the swing trigger.
            MuteAnyStateTrigger(baseMachine, "AttackSwing");
            // Upper body must not steal ranged fire/reload — those oneshots live on Base (Ranged loco).
            if (controller.layers.Length > 1)
            {
                AnimatorStateMachine upperMute = controller.layers[1].stateMachine;
                MuteAnyStateTrigger(upperMute, "FireRifle");
                MuteAnyStateTrigger(upperMute, "Reload");
            }

            // Upper-body Attack Swing variants (cycle via AttackVariant 0/1/2); exit-time → Hold Default.
            if (controller.layers.Length > 1)
            {
                AnimatorStateMachine upper = controller.layers[1].stateMachine;
                MuteAnyStateTrigger(upper, "AttackSwing");

                (string StateName, string File, string ClipName, int Variant, Vector3 Pos)[] swings =
                {
                    ("Attack Swing", "standing melee attack horizontal.fbx",
                        "Mix_StandingMeleeAttackHorizontal", 0, new Vector3(600, 40, 0)),
                    ("Attack Swing Downward", "standing melee attack downward.fbx",
                        "Mix_StandingMeleeAttackDownward", 1, new Vector3(600, 100, 0)),
                    ("Attack Swing Backhand", "standing melee attack backhand.fbx",
                        "Mix_StandingMeleeAttackBackhand", 2, new Vector3(600, 160, 0)),
                };

                AnimatorState holdDefault = FindState(upper, "Hold Default");
                foreach ((string StateName, string File, string ClipName, int Variant, Vector3 Pos) swing in swings)
                {
                    AnimationClip attackClip = LoadPackClip($"{MeleePack}/{swing.File}", swing.ClipName);
                    AnimatorState upperAttack = FindOrCreateState(upper, swing.StateName, swing.Pos);
                    if (attackClip != null)
                    {
                        upperAttack.motion = attackClip;
                        upperAttack.writeDefaultValues = true;
                    }

                    EnableMirrorParameter(upperAttack, "MirrorUpperBody");
                    EnsureAnyStateTriggerWithInt(
                        upper, upperAttack, "AttackSwing", "AttackVariant", swing.Variant, canTransitionToSelf: true);
                    if (holdDefault != null)
                    {
                        EnsureExitToState(upperAttack, holdDefault, hasExitTime: true, exitTime: 0.85f, duration: 0.15f);
                    }
                }

                // Mixamo holds are authored for the right hand — mirror when active hand is left.
                foreach (string holdName in new[] { "Hold Default", "Hold Item", "Hold Weapon" })
                {
                    AnimatorState hold = FindState(upper, holdName);
                    if (hold != null)
                    {
                        EnableMirrorParameter(hold, "MirrorUpperBody");
                    }
                }
            }

            if (controller.layers.Length > 2)
            {
                AnimatorStateMachine additive = controller.layers[2].stateMachine;
                RemapStateMotion(additive, "Flinch",
                    $"{MeleePack}/standing react large gut.fbx", "Mix_StandingReactLargeGut");
                RemapStateMotion(additive, "Empty Additive",
                    $"{InjuredPack}/injured hurting idle.fbx", "Mix_InjuredHurtingIdle");
                // Additive Flinch is the soft upper-body hit overlay (orchestrator lerps layer weight).
                // Keep unmuted — muted Additive + stagger weight only showed Empty Additive hurting idle.
                WireAdditiveFlinch(additive);
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return "OK: Rebuilt stance blends; injured idle severity; limp Jump/Turn90; Injured Wave Emote; "
                   + "AttackSwing variants + MirrorUpperBody; stance-aware Flinch (GettingHit/gut/HitReaction); "
                   + "Ranged FireRifle/Reload oneshots; Floating → Mix_Floating.";
        }

        /// <summary>
        /// Base-layer oneshots for M4 fire/reload (Basic Shooter pack). Gated to CombatStance == Ranged
        /// so melee/peaceful never consume the triggers. Upper Body weight is 0 in Ranged, so these
        /// must live on Base alongside rifle locomotion.
        /// </summary>
        private static void WireRangedFireAndReload(
            AnimatorStateMachine baseMachine,
            AnimatorState peaceful,
            AnimatorState melee,
            AnimatorState ranged,
            AnimatorState injured)
        {
            AnimationClip fireClip = LoadPackClip($"{ShooterPack}/firing rifle.fbx", "Mix_FiringRifle");
            AnimationClip reloadClip = LoadPackClip($"{ShooterPack}/reloading.fbx", "Mix_Reloading");

            AnimatorState fireState = FindOrCreateState(baseMachine, "Fire Rifle", new Vector3(50, 460, 0));
            if (fireClip != null)
            {
                fireState.motion = fireClip;
                fireState.writeDefaultValues = true;
            }

            AnimatorState reloadState = FindOrCreateState(baseMachine, "Reload Rifle", new Vector3(50, 520, 0));
            if (reloadClip != null)
            {
                reloadState.motion = reloadClip;
                reloadState.writeDefaultValues = true;
            }

            EnsureAnyStateTriggerWithCombatStance(
                baseMachine, fireState, "FireRifle", combatStance: 2, canTransitionToSelf: true, entryDuration: 0.05f);
            EnsureAnyStateTriggerWithCombatStance(
                baseMachine, reloadState, "Reload", combatStance: 2, canTransitionToSelf: false, entryDuration: 0.1f);

            ClearTransitions(fireState);
            EnsureExitToStateWithLimpGate(fireState, peaceful, requireLimping: false, combatStance: 0);
            EnsureExitToStateWithLimpGate(fireState, melee, requireLimping: false, combatStance: 1);
            EnsureExitToStateWithLimpGate(fireState, ranged, requireLimping: false, combatStance: 2);
            EnsureExitToStateWithLimpGate(fireState, injured, requireLimping: true, combatStance: null);
            SoftenOneshotExits(fireState, exitTime: 0.75f, duration: 0.12f);

            ClearTransitions(reloadState);
            EnsureExitToStateWithLimpGate(reloadState, peaceful, requireLimping: false, combatStance: 0);
            EnsureExitToStateWithLimpGate(reloadState, melee, requireLimping: false, combatStance: 1);
            EnsureExitToStateWithLimpGate(reloadState, ranged, requireLimping: false, combatStance: 2);
            EnsureExitToStateWithLimpGate(reloadState, injured, requireLimping: true, combatStance: null);
            SoftenOneshotExits(reloadState, exitTime: 0.9f, duration: 0.2f);
        }

        private static void SoftenOneshotExits(AnimatorState state, float exitTime, float duration)
        {
            foreach (AnimatorStateTransition transition in state.transitions)
            {
                transition.hasExitTime = true;
                transition.exitTime = exitTime;
                transition.hasFixedDuration = true;
                transition.duration = duration;
            }
        }

        private static void EnsureAnyStateTriggerWithCombatStance(
            AnimatorStateMachine machine,
            AnimatorState destination,
            string triggerName,
            int combatStance,
            bool canTransitionToSelf,
            float entryDuration)
        {
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            {
                if (transition.destinationState != destination)
                {
                    continue;
                }

                if (!transition.conditions.Any(c => c.parameter == triggerName))
                {
                    continue;
                }

                bool stanceOk = transition.conditions.Any(
                    c => c.parameter == "CombatStance" && (int)c.threshold == combatStance);
                if (!stanceOk)
                {
                    continue;
                }

                transition.mute = false;
                transition.canTransitionToSelf = canTransitionToSelf;
                transition.hasExitTime = false;
                transition.hasFixedDuration = true;
                transition.duration = entryDuration;
                MuteDuplicateAnyStateTriggers(machine, destination, triggerName, transition);
                return;
            }

            AnimatorStateTransition created = machine.AddAnyStateTransition(destination);
            created.hasExitTime = false;
            created.hasFixedDuration = true;
            created.duration = entryDuration;
            created.canTransitionToSelf = canTransitionToSelf;
            created.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
            created.AddCondition(AnimatorConditionMode.Equals, combatStance, "CombatStance");
            MuteDuplicateAnyStateTriggers(machine, destination, triggerName, created);
        }

        /// <summary>
        /// Base Flinch oneshots: GettingHit (Peaceful or limp), StandingReactLargeGut (Melee),
        /// HitReaction (Ranged). Legacy single Flinch state is removed.
        /// </summary>
        private static void WireStanceAwareFlinch(
            AnimatorStateMachine baseMachine,
            AnimatorState peaceful,
            AnimatorState melee,
            AnimatorState ranged,
            AnimatorState injured)
        {
            MuteAnyStateTrigger(baseMachine, "Flinch");
            RemoveStateIfPresent(baseMachine, "Flinch");

            AnimationClip gettingHit = LoadPackClip(
                "Assets/Art/Animations/Misc/X Bot@Getting Hit.fbx", "Mix_GettingHit");
            AnimationClip gut = LoadPackClip(
                $"{MeleePack}/standing react large gut.fbx", "Mix_StandingReactLargeGut");
            AnimationClip hitReaction = LoadPackClip(
                $"{ShooterPack}/hit reaction.fbx", "Mix_HitReaction");

            AnimatorState flinchGettingHit = FindOrCreateState(
                baseMachine, "Flinch GettingHit", new Vector3(50, 280, 0));
            if (gettingHit != null)
            {
                flinchGettingHit.motion = gettingHit;
                flinchGettingHit.writeDefaultValues = true;
            }

            AnimatorState flinchMelee = FindOrCreateState(
                baseMachine, "Flinch Melee", new Vector3(50, 340, 0));
            if (gut != null)
            {
                flinchMelee.motion = gut;
                flinchMelee.writeDefaultValues = true;
            }

            AnimatorState flinchRanged = FindOrCreateState(
                baseMachine, "Flinch Ranged", new Vector3(50, 400, 0));
            if (hitReaction != null)
            {
                flinchRanged.motion = hitReaction;
                flinchRanged.writeDefaultValues = true;
            }

            // Limp → GettingHit; Peaceful (stance 0, not limping) → GettingHit.
            EnsureAnyStateFlinch(baseMachine, flinchGettingHit, limpGreaterThanZero: true, combatStance: null);
            EnsureAnyStateFlinch(baseMachine, flinchGettingHit, limpGreaterThanZero: false, combatStance: 0);
            EnsureAnyStateFlinch(baseMachine, flinchMelee, limpGreaterThanZero: false, combatStance: 1);
            EnsureAnyStateFlinch(baseMachine, flinchRanged, limpGreaterThanZero: false, combatStance: 2);

            ClearTransitions(flinchGettingHit);
            EnsureExitToStateWithLimpGate(flinchGettingHit, injured, requireLimping: true, combatStance: null);
            EnsureExitToStateWithLimpGate(flinchGettingHit, peaceful, requireLimping: false, combatStance: 0);
            EnsureExitToStateWithLimpGate(flinchGettingHit, melee, requireLimping: false, combatStance: 1);
            EnsureExitToStateWithLimpGate(flinchGettingHit, ranged, requireLimping: false, combatStance: 2);
            SoftenFlinchStateExits(flinchGettingHit);

            ClearTransitions(flinchMelee);
            EnsureExitToStateWithLimpGate(flinchMelee, peaceful, requireLimping: false, combatStance: 0);
            EnsureExitToStateWithLimpGate(flinchMelee, melee, requireLimping: false, combatStance: 1);
            EnsureExitToStateWithLimpGate(flinchMelee, ranged, requireLimping: false, combatStance: 2);
            SoftenFlinchStateExits(flinchMelee);

            ClearTransitions(flinchRanged);
            EnsureExitToStateWithLimpGate(flinchRanged, peaceful, requireLimping: false, combatStance: 0);
            EnsureExitToStateWithLimpGate(flinchRanged, melee, requireLimping: false, combatStance: 1);
            EnsureExitToStateWithLimpGate(flinchRanged, ranged, requireLimping: false, combatStance: 2);
            SoftenFlinchStateExits(flinchRanged);
        }

        private static void SoftenFlinchStateExits(AnimatorState flinchState)
        {
            foreach (AnimatorStateTransition transition in flinchState.transitions)
            {
                transition.hasExitTime = true;
                transition.exitTime = FlinchExitTime;
                transition.hasFixedDuration = true;
                transition.duration = FlinchExitDuration;
            }
        }

        /// <summary>
        /// Unmute Additive Any→Flinch and soften entry/exit so gut overlay matches Base flinch blends.
        /// </summary>
        private static void WireAdditiveFlinch(AnimatorStateMachine additive)
        {
            AnimatorState flinch = FindState(additive, "Flinch");
            if (flinch == null)
            {
                return;
            }

            foreach (AnimatorStateTransition transition in additive.anyStateTransitions)
            {
                if (transition.destinationState != flinch
                    || !transition.conditions.Any(c => c.parameter == "Flinch"))
                {
                    continue;
                }

                transition.mute = false;
                transition.canTransitionToSelf = false;
                transition.hasExitTime = false;
                transition.hasFixedDuration = true;
                transition.duration = FlinchEntryDuration;
            }

            SoftenFlinchStateExits(flinch);
        }

        private static void EnsureAnyStateFlinch(
            AnimatorStateMachine machine,
            AnimatorState destination,
            bool limpGreaterThanZero,
            int? combatStance)
        {
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            {
                if (transition.destinationState != destination)
                {
                    continue;
                }

                if (!transition.conditions.Any(c => c.parameter == "Flinch"))
                {
                    continue;
                }

                bool limpOk = limpGreaterThanZero
                    ? transition.conditions.Any(c => c.parameter == "LimpSide" && c.mode == AnimatorConditionMode.Greater)
                    : transition.conditions.Any(c => c.parameter == "LimpSide" && c.mode == AnimatorConditionMode.Equals);
                bool stanceOk = combatStance == null
                    ? !transition.conditions.Any(c => c.parameter == "CombatStance")
                    : transition.conditions.Any(
                        c => c.parameter == "CombatStance" && (int)c.threshold == combatStance.Value);

                if (!limpOk || !stanceOk)
                {
                    continue;
                }

                transition.mute = false;
                transition.canTransitionToSelf = false;
                transition.hasExitTime = false;
                transition.hasFixedDuration = true;
                transition.duration = FlinchEntryDuration;
                return;
            }

            AnimatorStateTransition created = machine.AddAnyStateTransition(destination);
            created.hasExitTime = false;
            created.hasFixedDuration = true;
            created.duration = FlinchEntryDuration;
            created.canTransitionToSelf = false;
            created.AddCondition(AnimatorConditionMode.If, 0f, "Flinch");
            if (limpGreaterThanZero)
            {
                created.AddCondition(AnimatorConditionMode.Greater, 0f, "LimpSide");
            }
            else
            {
                created.AddCondition(AnimatorConditionMode.Equals, 0f, "LimpSide");
            }

            if (combatStance.HasValue)
            {
                created.AddCondition(AnimatorConditionMode.Equals, combatStance.Value, "CombatStance");
            }
        }

        private static BlendTree BuildInjuredBlendTree(AnimatorController controller)
        {
            AnimationClip idle = LoadPackClip($"{InjuredPack}/injured idle.fbx", "Mix_InjuredIdle");
            AnimationClip stumble = LoadPackClip($"{InjuredPack}/injured stumble idle.fbx", "Mix_InjuredStumbleIdle");
            if (idle == null || stumble == null)
            {
                Debug.LogError("[HumanoidLocomotionBlendSetup] Missing injured idle/stumble clips.");
                return null;
            }

            BlendTree idleSeverity = new BlendTree
            {
                name = "Injured Idle Severity",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "InjuredLeg",
                useAutomaticThresholds = false,
            };
            // Moderate limp keeps injured idle; severe leg damage blends to stumble.
            idleSeverity.AddChild(idle, 0.3f);
            idleSeverity.AddChild(idle, 0.55f);
            idleSeverity.AddChild(stumble, 0.7f);
            AssetDatabase.AddObjectToAsset(idleSeverity, controller);

            BlendTree tree = new BlendTree
            {
                name = "Injured Locomotion 2D",
                blendType = BlendTreeType.FreeformCartesian2D,
                blendParameter = "VelX",
                blendParameterY = "VelZ",
                useAutomaticThresholds = false,
            };

            tree.AddChild(idleSeverity, new Vector2(0f, 0f));
            foreach ((string File, string ClipName, Vector2 Pos) entry in InjuredClips)
            {
                if (entry.Pos == Vector2.zero)
                {
                    continue; // idle replaced by severity blend above
                }

                AnimationClip clip = LoadPackClip($"{InjuredPack}/{entry.File}", entry.ClipName);
                if (clip == null)
                {
                    Debug.LogError($"[HumanoidLocomotionBlendSetup] Missing clip '{entry.ClipName}' in {InjuredPack}/{entry.File}");
                    return null;
                }

                tree.AddChild(clip, entry.Pos);
            }

            AssetDatabase.AddObjectToAsset(tree, controller);
            return tree;
        }

        private static void WireJumpAndTurnOneshots(
            AnimatorController controller,
            AnimatorStateMachine baseMachine,
            AnimatorState peaceful,
            AnimatorState melee,
            AnimatorState ranged,
            AnimatorState injured)
        {
            AnimationClip jumpClip = LoadPackClip($"{LocomotionPack}/jump.fbx", "Mix_Jump");
            if (jumpClip != null)
            {
                AnimatorState jumpState = FindOrCreateState(baseMachine, "Jump", new Vector3(750, 120, 0));
                jumpState.motion = jumpClip;
                EnsureAnyStateTriggerWithLimpGate(baseMachine, jumpState, "Jump", requireLimping: false, canTransitionToSelf: false);
                ClearTransitions(jumpState);
                EnsureExitToStateWithLimpGate(jumpState, peaceful, requireLimping: false, combatStance: 0);
                EnsureExitToStateWithLimpGate(jumpState, melee, requireLimping: false, combatStance: 1);
                EnsureExitToStateWithLimpGate(jumpState, ranged, requireLimping: false, combatStance: 2);
            }

            AnimationClip injuredStandJump = LoadPackClip(
                $"{InjuredPack}/injured standing jump.fbx", "Mix_InjuredStandingJump");
            AnimationClip injuredRunJump = LoadPackClip(
                $"{InjuredPack}/injured run jump.fbx", "Mix_InjuredRunJump");
            if (injuredStandJump != null)
            {
                BlendTree injuredJumpBlend = new BlendTree
                {
                    name = "Injured Jump Blend",
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = "Speed",
                    useAutomaticThresholds = false,
                };
                injuredJumpBlend.AddChild(injuredStandJump, 0f);
                injuredJumpBlend.AddChild(injuredStandJump, 0.4f);
                if (injuredRunJump != null)
                {
                    injuredJumpBlend.AddChild(injuredRunJump, 0.85f);
                }

                AssetDatabase.AddObjectToAsset(injuredJumpBlend, controller);

                AnimatorState injuredJump = FindOrCreateState(baseMachine, "Injured Jump", new Vector3(750, 220, 0));
                injuredJump.motion = injuredJumpBlend;
                EnsureAnyStateTriggerWithLimpGate(baseMachine, injuredJump, "Jump", requireLimping: true, canTransitionToSelf: false);
                ClearTransitions(injuredJump);
                EnsureExitToStateWithLimpGate(injuredJump, injured, requireLimping: true, combatStance: null);
            }

            WireTurn90(
                baseMachine,
                "Turn Left 90",
                "Injured Turn Left 90",
                "TurnLeft90",
                $"{LocomotionPack}/left turn 90.fbx",
                "Mix_LeftTurn90",
                $"{InjuredPack}/injured turn left.fbx",
                "Mix_InjuredTurnLeft",
                new Vector3(750, 0, 0),
                new Vector3(900, 0, 0),
                peaceful,
                melee,
                ranged,
                injured);

            WireTurn90(
                baseMachine,
                "Turn Right 90",
                "Injured Turn Right 90",
                "TurnRight90",
                $"{LocomotionPack}/right turn 90.fbx",
                "Mix_RightTurn90",
                $"{InjuredPack}/injured turn right.fbx",
                "Mix_InjuredTurnRight",
                new Vector3(750, 40, 0),
                new Vector3(900, 40, 0),
                peaceful,
                melee,
                ranged,
                injured);
        }

        private static void WireTurn90(
            AnimatorStateMachine baseMachine,
            string healthyName,
            string injuredName,
            string triggerName,
            string healthyPath,
            string healthyClip,
            string injuredPath,
            string injuredClip,
            Vector3 healthyPos,
            Vector3 injuredPos,
            AnimatorState peaceful,
            AnimatorState melee,
            AnimatorState ranged,
            AnimatorState injured)
        {
            AnimationClip healthyMotion = LoadPackClip(healthyPath, healthyClip);
            if (healthyMotion != null)
            {
                AnimatorState turn = FindOrCreateState(baseMachine, healthyName, healthyPos);
                turn.motion = healthyMotion;
                EnsureAnyStateTriggerWithLimpGate(baseMachine, turn, triggerName, requireLimping: false, canTransitionToSelf: false);
                ClearTransitions(turn);
                EnsureExitToStateWithLimpGate(turn, peaceful, requireLimping: false, combatStance: 0);
                EnsureExitToStateWithLimpGate(turn, melee, requireLimping: false, combatStance: 1);
                EnsureExitToStateWithLimpGate(turn, ranged, requireLimping: false, combatStance: 2);
            }

            AnimationClip injuredMotion = LoadPackClip(injuredPath, injuredClip);
            if (injuredMotion != null)
            {
                AnimatorState turnInjured = FindOrCreateState(baseMachine, injuredName, injuredPos);
                turnInjured.motion = injuredMotion;
                EnsureAnyStateTriggerWithLimpGate(
                    baseMachine, turnInjured, triggerName, requireLimping: true, canTransitionToSelf: false);
                ClearTransitions(turnInjured);
                EnsureExitToStateWithLimpGate(turnInjured, injured, requireLimping: true, combatStance: null);
            }
        }

        private static void WireInjuredWaveEmote(AnimatorStateMachine baseMachine, AnimatorState injured)
        {
            AnimationClip wave = LoadPackClip($"{InjuredPack}/injured wave idle.fbx", "Mix_InjuredWaveIdle");
            if (wave == null)
            {
                return;
            }

            // Base layer — Full Body Override weight is 0 during locomotion, so wave must live here.
            AnimatorState injuredWave = FindOrCreateState(baseMachine, "Injured Wave", new Vector3(900, 120, 0));
            injuredWave.motion = wave;
            injuredWave.writeDefaultValues = true;
            EnsureAnyStateTriggerWithLimpGate(
                baseMachine, injuredWave, "Emote", requireLimping: true, canTransitionToSelf: false);
            ClearTransitions(injuredWave);
            EnsureExitToStateWithLimpGate(injuredWave, injured, requireLimping: true, combatStance: null);
        }

        private static BlendTree BuildBlendTree(
            AnimatorController controller,
            string treeName,
            string packFolder,
            (string File, string ClipName, Vector2 Pos)[] entries)
        {
            BlendTree tree = new BlendTree
            {
                name = treeName,
                blendType = BlendTreeType.FreeformCartesian2D,
                blendParameter = "VelX",
                blendParameterY = "VelZ",
                useAutomaticThresholds = false,
            };

            foreach ((string File, string ClipName, Vector2 Pos) entry in entries)
            {
                AnimationClip clip = LoadPackClip($"{packFolder}/{entry.File}", entry.ClipName);
                if (clip == null)
                {
                    Debug.LogError($"[HumanoidLocomotionBlendSetup] Missing clip '{entry.ClipName}' in {packFolder}/{entry.File}");
                    return null;
                }

                tree.AddChild(clip, entry.Pos);
            }

            AssetDatabase.AddObjectToAsset(tree, controller);
            return tree;
        }

        private static void WireStanceTransitions(AnimatorState from, AnimatorState otherA, AnimatorState otherB)
        {
            EnsureStanceTransition(from, otherA, GetStanceForState(otherA));
            EnsureStanceTransition(from, otherB, GetStanceForState(otherB));
        }

        private static int GetStanceForState(AnimatorState state)
        {
            if (state.name.StartsWith("Melee"))
            {
                return 1;
            }

            if (state.name.StartsWith("Ranged"))
            {
                return 2;
            }

            return 0;
        }

        private static void EnsureStanceTransition(AnimatorState from, AnimatorState to, int stanceValue)
        {
            foreach (AnimatorStateTransition transition in from.transitions)
            {
                if (transition.destinationState == to
                    && transition.conditions.Any(c => c.parameter == "CombatStance" && (int)c.threshold == stanceValue))
                {
                    return;
                }
            }

            AnimatorStateTransition created = from.AddTransition(to);
            created.hasExitTime = false;
            created.hasFixedDuration = true;
            created.duration = 0.15f;
            created.AddCondition(AnimatorConditionMode.Equals, stanceValue, "CombatStance");
            // Stay on healthy stance locomotion while not limping.
            created.AddCondition(AnimatorConditionMode.Equals, 0f, "LimpSide");
        }

        private static void WireLimpEntry(AnimatorState from, AnimatorState injured)
        {
            foreach (AnimatorStateTransition transition in from.transitions)
            {
                if (transition.destinationState == injured
                    && transition.conditions.Any(c => c.parameter == "LimpSide"))
                {
                    return;
                }
            }

            AnimatorStateTransition created = from.AddTransition(injured);
            created.hasExitTime = false;
            created.hasFixedDuration = true;
            created.duration = 0.2f;
            // LimpSide: 0 None, 1 Left, 2 Right — any non-zero enters injured gait.
            created.AddCondition(AnimatorConditionMode.Greater, 0f, "LimpSide");
        }

        private static void WireLimpExit(AnimatorState injured, AnimatorState stanceState, int stanceValue)
        {
            foreach (AnimatorStateTransition transition in injured.transitions)
            {
                if (transition.destinationState == stanceState
                    && transition.conditions.Any(c => c.parameter == "CombatStance" && (int)c.threshold == stanceValue))
                {
                    return;
                }
            }

            AnimatorStateTransition created = injured.AddTransition(stanceState);
            created.hasExitTime = false;
            created.hasFixedDuration = true;
            created.duration = 0.2f;
            created.AddCondition(AnimatorConditionMode.Equals, 0f, "LimpSide");
            created.AddCondition(AnimatorConditionMode.Equals, stanceValue, "CombatStance");
        }

        private static void ClearTransitions(AnimatorState state)
        {
            AnimatorStateTransition[] existing = state.transitions;
            for (int i = existing.Length - 1; i >= 0; i--)
            {
                state.RemoveTransition(existing[i]);
            }
        }

        private static void RemoveStateIfPresent(AnimatorStateMachine machine, string stateName)
        {
            AnimatorState state = FindState(machine, stateName);
            if (state != null)
            {
                machine.RemoveState(state);
            }
        }

        private static void RemapStateMotion(AnimatorStateMachine machine, string stateName, string fbxPath, string clipName)
        {
            if (machine == null)
            {
                return;
            }

            AnimatorState state = FindState(machine, stateName);
            if (state == null)
            {
                return;
            }

            AnimationClip clip = LoadPackClip(fbxPath, clipName);
            if (clip != null)
            {
                state.motion = clip;
            }
        }

        private static AnimationClip LoadPackClip(string fbxPath, string clipName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && clip.name == clipName && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }

            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    Debug.LogWarning($"[HumanoidLocomotionBlendSetup] Using '{clip.name}' instead of '{clipName}' from {fbxPath}");
                    return clip;
                }
            }

            return null;
        }

        private static void EnsureFloatParam(AnimatorController controller, string name)
        {
            if (controller.parameters.Any(p => p.name == name))
            {
                return;
            }

            controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }

        private static void EnsureIntParam(AnimatorController controller, string name)
        {
            if (controller.parameters.Any(p => p.name == name))
            {
                return;
            }

            controller.AddParameter(name, AnimatorControllerParameterType.Int);
        }

        private static void EnsureBoolParam(AnimatorController controller, string name)
        {
            if (controller.parameters.Any(p => p.name == name))
            {
                return;
            }

            controller.AddParameter(name, AnimatorControllerParameterType.Bool);
        }

        private static void EnsureTriggerParam(AnimatorController controller, string name)
        {
            if (controller.parameters.Any(p => p.name == name))
            {
                return;
            }

            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state != null && child.state.name == name)
                {
                    return child.state;
                }
            }

            return null;
        }

        private static AnimatorState FindOrCreateState(AnimatorStateMachine machine, string name, Vector3 position)
        {
            AnimatorState existing = FindState(machine, name);
            if (existing != null)
            {
                return existing;
            }

            return machine.AddState(name, position);
        }

        private static void EnableMirrorParameter(AnimatorState state, string parameterName)
        {
            if (state == null)
            {
                return;
            }

            state.mirrorParameterActive = true;
            state.mirrorParameter = parameterName;
        }

        private static void EnsureAnyStateTrigger(
            AnimatorStateMachine machine,
            AnimatorState destination,
            string triggerName,
            bool canTransitionToSelf)
        {
            EnsureAnyStateTriggerWithLimpGate(machine, destination, triggerName, requireLimping: null, canTransitionToSelf);
        }

        /// <param name="requireLimping">
        /// null = no limp gate; false = LimpSide == 0; true = LimpSide &gt; 0.
        /// </param>
        private static void EnsureAnyStateTriggerWithLimpGate(
            AnimatorStateMachine machine,
            AnimatorState destination,
            string triggerName,
            bool? requireLimping,
            bool canTransitionToSelf)
        {
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            {
                if (transition.destinationState != destination)
                {
                    continue;
                }

                if (!transition.conditions.Any(c => c.parameter == triggerName))
                {
                    continue;
                }

                transition.mute = false;
                transition.canTransitionToSelf = canTransitionToSelf;
                ApplyLimpGateConditions(transition, triggerName, requireLimping);
                MuteDuplicateAnyStateTriggers(machine, destination, triggerName, transition);
                return;
            }

            AnimatorStateTransition created = machine.AddAnyStateTransition(destination);
            created.hasExitTime = false;
            created.hasFixedDuration = true;
            created.duration = 0.05f;
            created.canTransitionToSelf = canTransitionToSelf;
            created.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
            ApplyLimpGateConditions(created, triggerName, requireLimping);
            MuteDuplicateAnyStateTriggers(machine, destination, triggerName, created);
        }

        private static void MuteDuplicateAnyStateTriggers(
            AnimatorStateMachine machine,
            AnimatorState destination,
            string triggerName,
            AnimatorStateTransition keep)
        {
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            {
                if (transition == keep || transition.destinationState != destination)
                {
                    continue;
                }

                if (transition.conditions.Any(c => c.parameter == triggerName))
                {
                    transition.mute = true;
                }
            }
        }

        private static void ApplyLimpGateConditions(
            AnimatorStateTransition transition,
            string triggerName,
            bool? requireLimping)
        {
            List<AnimatorCondition> extras = transition.conditions
                .Where(c => c.parameter != triggerName && c.parameter != "LimpSide")
                .ToList();

            while (transition.conditions.Length > 0)
            {
                transition.RemoveCondition(transition.conditions[0]);
            }

            transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
            if (requireLimping == true)
            {
                transition.AddCondition(AnimatorConditionMode.Greater, 0f, "LimpSide");
            }
            else if (requireLimping == false)
            {
                transition.AddCondition(AnimatorConditionMode.Equals, 0f, "LimpSide");
            }

            foreach (AnimatorCondition c in extras)
            {
                transition.AddCondition(c.mode, c.threshold, c.parameter);
            }
        }

        private static void EnsureExitToStateWithLimpGate(
            AnimatorState from,
            AnimatorState destination,
            bool requireLimping,
            int? combatStance)
        {
            foreach (AnimatorStateTransition transition in from.transitions)
            {
                if (transition.destinationState != destination)
                {
                    continue;
                }

                bool limpOk = requireLimping
                    ? transition.conditions.Any(c => c.parameter == "LimpSide" && c.mode == AnimatorConditionMode.Greater)
                    : transition.conditions.Any(c => c.parameter == "LimpSide" && c.mode == AnimatorConditionMode.Equals);
                bool stanceOk = combatStance == null
                    || transition.conditions.Any(
                        c => c.parameter == "CombatStance" && (int)c.threshold == combatStance.Value);
                if (limpOk && stanceOk)
                {
                    return;
                }
            }

            AnimatorStateTransition created = from.AddTransition(destination);
            created.hasExitTime = true;
            created.exitTime = 0.85f;
            created.hasFixedDuration = true;
            created.duration = 0.1f;
            if (requireLimping)
            {
                created.AddCondition(AnimatorConditionMode.Greater, 0f, "LimpSide");
            }
            else
            {
                created.AddCondition(AnimatorConditionMode.Equals, 0f, "LimpSide");
            }

            if (combatStance.HasValue)
            {
                created.AddCondition(AnimatorConditionMode.Equals, combatStance.Value, "CombatStance");
            }
        }

        private static void EnsureAnyStateTriggerWithInt(
            AnimatorStateMachine machine,
            AnimatorState destination,
            string triggerName,
            string intParamName,
            int intValue,
            bool canTransitionToSelf)
        {
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            {
                if (transition.destinationState != destination)
                {
                    continue;
                }

                bool hasTrigger = transition.conditions.Any(c => c.parameter == triggerName);
                bool hasVariant = transition.conditions.Any(
                    c => c.parameter == intParamName && (int)c.threshold == intValue);
                if (hasTrigger && hasVariant)
                {
                    transition.mute = false;
                    transition.canTransitionToSelf = canTransitionToSelf;
                    return;
                }
            }

            AnimatorStateTransition created = machine.AddAnyStateTransition(destination);
            created.hasExitTime = false;
            created.hasFixedDuration = true;
            created.duration = 0.05f;
            created.canTransitionToSelf = canTransitionToSelf;
            created.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
            created.AddCondition(AnimatorConditionMode.Equals, intValue, intParamName);
        }

        private static void MuteAnyStateTrigger(AnimatorStateMachine machine, string triggerName)
        {
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            {
                if (transition.conditions.Any(c => c.parameter == triggerName))
                {
                    transition.mute = true;
                }
            }
        }

        private static void EnsureExitToState(
            AnimatorState from,
            AnimatorState destination,
            bool hasExitTime,
            float exitTime,
            float duration)
        {
            foreach (AnimatorStateTransition transition in from.transitions)
            {
                if (transition.destinationState == destination)
                {
                    return;
                }
            }

            // Prefer rewriting Exit-node transitions so the clip cannot dump out of the layer SM.
            foreach (AnimatorStateTransition transition in from.transitions)
            {
                if (transition.isExit)
                {
                    transition.isExit = false;
                    transition.destinationState = destination;
                    transition.hasExitTime = hasExitTime;
                    transition.exitTime = exitTime;
                    transition.hasFixedDuration = true;
                    transition.duration = duration;
                    return;
                }
            }

            AnimatorStateTransition created = from.AddTransition(destination);
            created.hasExitTime = hasExitTime;
            created.exitTime = exitTime;
            created.hasFixedDuration = true;
            created.duration = duration;
        }
    }
}
