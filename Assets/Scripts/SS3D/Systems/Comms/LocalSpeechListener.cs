using SS3D.Systems.Entities;
using UnityEngine;

namespace SS3D.Systems.Comms
{
    /// <summary>
    /// Computes a viewer's audibility tier for a speaker's local speech, per comms.md §3: distance
    /// thresholds plus a grace-distance rule so a thin obstruction at close range degrades to
    /// Muffled instead of cutting straight to Inaudible.
    /// </summary>
    public class LocalSpeechListener
    {
        private readonly LocalSpeechConfig _config;

        public LocalSpeechListener(LocalSpeechConfig config)
        {
            _config = config;
        }

        public AudibilityTier ComputeTier(Entity viewer, Entity speaker)
        {
            if (viewer == null || speaker == null || viewer.ViewPoint == null || speaker.ViewPoint == null)
            {
                return AudibilityTier.Inaudible;
            }

            Vector3 origin = viewer.ViewPoint.transform.position;
            Vector3 target = speaker.ViewPoint.transform.position;
            float distance = Vector3.Distance(origin, target);

            if (distance > _config.InaudibleDistance)
            {
                return AudibilityTier.Inaudible;
            }

            bool blocked = IsBlocked(origin, target, distance);

            if (!blocked)
            {
                return distance <= _config.ClearDistance ? AudibilityTier.Clear : AudibilityTier.Muffled;
            }

            return distance <= _config.GraceDistance ? AudibilityTier.Muffled : AudibilityTier.Inaudible;
        }

        /// <summary>
        /// Raycasts from the viewer's ViewPoint toward the speaker's, following the same
        /// line-of-sight pattern DropInteraction uses. v1 treats all solid geometry the same
        /// (comms.md §13) - a single occlusion mask, not material-aware.
        /// </summary>
        private bool IsBlocked(Vector3 origin, Vector3 target, float distance)
        {
            if (distance <= 0f)
            {
                return false;
            }

            Vector3 direction = (target - origin) / distance;
            return Physics.Raycast(origin, direction, distance, _config.OcclusionMask);
        }
    }
}
