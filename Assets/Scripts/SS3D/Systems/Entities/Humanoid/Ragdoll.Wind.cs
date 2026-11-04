using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using SS3D.Systems.Atmospherics;
using UnityEngine;

namespace SS3D.Systems.Entities.Humanoid
{
    public partial class Ragdoll
    {
        internal static readonly HashSet<Ragdoll> WindBodies = new();
        private bool _windKnockdown;
        private bool _serverWindPhysics;

        public override void OnStopNetwork()
        {
            WindBodies.Remove(this);
            base.OnStopNetwork();
        }

        public Vector3 WindSamplePosition => _hips != null && IsKnockedDown ? _hips.position : transform.position;

        [Server]
        public void ServerApplyWind(Vector3 gradient, float seconds)
        {
            if (gradient.sqrMagnitude < AtmosWind.MinPressure * AtmosWind.MinPressure) return;
            _serverWindPhysics = !Owner.IsValid;
            // Do not convert an existing health-owned timeless collapse to a timed knockdown.
            if (!IsKnockedDown && gradient.magnitude >= AtmosWind.KnockdownPressure)
            {
                _windKnockdown = true;
                ServerKnockdown(3f);
            }
            else if (_isKnockdownTimed)
                _knockdownTimer = Mathf.Max(_knockdownTimer, 2f);

            Vector3 deltaVelocity = AtmosWind.Acceleration(gradient) * seconds;
            if (_deathRagdoll || !Owner.IsValid)
            {
                if (IsKnockedDown) ApplyPresentation(_presentation, false);
                ApplyWind(deltaVelocity, seconds, _presentation != BodyPresentationState.Locomotion);
            }
            else
                RpcWind(Owner, deltaVelocity, seconds, _presentation);
        }

        [TargetRpc]
        private void RpcWind(NetworkConnection connection, Vector3 deltaVelocity, float seconds, BodyPresentationState presentation)
        {
            // RPC ordering must not leave the first gust on a still-kinematic body.
            if (presentation != BodyPresentationState.Locomotion) ApplyPresentation(presentation, false);
            ApplyWind(deltaVelocity, seconds, presentation != BodyPresentationState.Locomotion);
        }

        private void ApplyWind(Vector3 deltaVelocity, float seconds, bool collapsed)
        {
            if (collapsed)
            {
                if (!IsRagdollPhysicsAuthority()) return;
                foreach (Transform part in _ragdollParts)
                {
                    Rigidbody body = part.GetComponent<Rigidbody>();
                    if (!body.isKinematic)
                        body.AddForce(deltaVelocity, ForceMode.VelocityChange);
                }
            }
            else if (IsOwner && _characterController != null && _characterController.enabled)
            {
                _characterController.Move(deltaVelocity * seconds);
            }
        }
    }
}
