using SS3D.Core;
using SS3D.Data.Generated;
using SS3D.Systems.Audio;
using UnityEngine;
using AudioType = SS3D.Systems.Audio.AudioType;

namespace SS3D.Systems.Furniture
{
    /// <summary>
    /// State machine behaviour to update colors upon change of state in the Airlock animator.
    /// This behaviour should go on state Open and Enter of the airlock state machine.
    /// </summary>
    public class AirlockStateMachine : StateMachineBehaviour
    {
        private const string Opening = "Opening";
        private const string Closing = "Closing";

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            AirLockOpener opener = animator.GetComponent<AirLockOpener>();
            if (opener != null)
            {
                opener.SetDoorLightColor(AirLockOpener.DoorLightIdleColor);
            }
        }

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // Play at the door's world position with no parent — parenting to the NetworkObject
            // tied the pooled source to the sliding mesh and (with Doppler) warbled the open/close
            // attack. Positional one-shot is enough for a stationary door cycle.
            Vector3 position = animator.transform.position;
            AudioSubSystem audio = SubSystems.Get<AudioSubSystem>();
            AirLockOpener opener = animator.GetComponent<AirLockOpener>();

            if (stateInfo.IsName(Opening))
            {
                if (opener != null)
                {
                    opener.SetDoorLightColor(AirLockOpener.DoorLightOpeningColor);
                }

                audio.PlayAudioSource(AudioType.Sfx, Sounds.AirlockOpen, position, null);
            }

            if (stateInfo.IsName(Closing))
            {
                if (opener != null)
                {
                    opener.SetDoorLightColor(AirLockOpener.DoorLightClosingColor);
                }

                audio.PlayAudioSource(AudioType.Sfx, Sounds.AirlockClose, position, null);
            }
        }
    }
}
