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
            ("IsSeated", AnimatorControllerParameterType.Bool),
            ("CombatMode", AnimatorControllerParameterType.Bool),
            ("CombatStance", AnimatorControllerParameterType.Int),
            ("AimYaw", AnimatorControllerParameterType.Float),
            ("AimPitch", AnimatorControllerParameterType.Float),
            ("AttackSwing", AnimatorControllerParameterType.Trigger),
            ("AttackStab", AnimatorControllerParameterType.Trigger),
            ("AttackVariant", AnimatorControllerParameterType.Int),
            ("Throw", AnimatorControllerParameterType.Trigger),
            ("Emote", AnimatorControllerParameterType.Trigger),
            ("Flinch", AnimatorControllerParameterType.Trigger),
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
                    "remaps Flinch / injured-arm additive.\n\n" +
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

        // Keep old menu entry as alias.
        [MenuItem("SS3D/Animation/Rebuild Locomotion Pack Blend Tree")]
        public static void RebuildLocomotionBlendTreeMenu() => RebuildCombatStanceBlendTreesMenu();

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
            BlendTree injuredTree = BuildBlendTree(controller, "Injured Locomotion 2D", InjuredPack, InjuredClips);
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

            AnimationClip jumpClip = LoadPackClip($"{LocomotionPack}/jump.fbx", "Mix_Jump");
            if (jumpClip != null)
            {
                AnimatorState jumpState = FindOrCreateState(baseMachine, "Jump", new Vector3(750, 120, 0));
                jumpState.motion = jumpClip;
                EnsureAnyStateTrigger(baseMachine, jumpState, "Jump", canTransitionToSelf: false);
                EnsureExitToState(jumpState, peaceful, hasExitTime: true, exitTime: 0.85f, duration: 0.1f);
            }

            RemapStateMotion(baseMachine, "Flinch", $"{MeleePack}/standing react large gut.fbx", "Mix_StandingReactLargeGut");

            // Base layer must not consume AttackSwing — upper body owns the swing trigger.
            MuteAnyStateTrigger(baseMachine, "AttackSwing");

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

                    EnsureAnyStateTriggerWithInt(
                        upper, upperAttack, "AttackSwing", "AttackVariant", swing.Variant, canTransitionToSelf: true);
                    if (holdDefault != null)
                    {
                        EnsureExitToState(upperAttack, holdDefault, hasExitTime: true, exitTime: 0.85f, duration: 0.15f);
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
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return "OK: Rebuilt Peaceful / Melee / Ranged / Injured locomotion blends; " +
                   "AttackSwing variants (horizontal/downward/backhand) on Upper Body; Flinch / injured-arm additive remapped.";
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

        private static void EnsureAnyStateTrigger(
            AnimatorStateMachine machine,
            AnimatorState destination,
            string triggerName,
            bool canTransitionToSelf)
        {
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            {
                if (transition.destinationState == destination
                    && transition.conditions.Any(c => c.parameter == triggerName)
                    && transition.conditions.Length == 1)
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
