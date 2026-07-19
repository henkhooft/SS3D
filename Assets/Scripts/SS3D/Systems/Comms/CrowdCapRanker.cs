using SS3D.Systems.Entities;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SS3D.Systems.Comms
{
    /// <summary>
    /// Ranks currently-active speakers by proximity to a viewer and splits them into the
    /// cap-sized "shown" set and the compressed overflow, per comms.md §4. Proximity-only for
    /// this slice - shouts bumping lower-priority speakers ahead of proximity (comms.md §8) is a
    /// later slice's concern; see the TODO below for where that plugs in.
    /// </summary>
    public class CrowdCapRanker
    {
        public readonly struct Result
        {
            public readonly IReadOnlyList<Entity> Shown;
            public readonly int OverflowCount;

            public Result(IReadOnlyList<Entity> shown, int overflowCount)
            {
                Shown = shown;
                OverflowCount = overflowCount;
            }
        }

        public Result Rank(Entity viewer, IReadOnlyCollection<Entity> activeSpeakers, int maxVisible)
        {
            if (viewer == null || activeSpeakers == null || activeSpeakers.Count == 0)
            {
                return new Result(System.Array.Empty<Entity>(), 0);
            }

            Vector3 viewerPosition = viewer.ViewPoint != null ? viewer.ViewPoint.transform.position : viewer.Position;

            // TODO(comms-shout-slice): shouts should bump a lower-priority speaker ahead of pure
            // proximity ordering here (comms.md §8) once SpeechMode.Shout is actually emitted.
            List<Entity> ranked = activeSpeakers
                .OrderBy(speaker => SqrDistanceTo(speaker, viewerPosition))
                .ToList();

            List<Entity> shown = ranked.Take(maxVisible).ToList();
            int overflowCount = ranked.Count - shown.Count;

            return new Result(shown, overflowCount);
        }

        private static float SqrDistanceTo(Entity speaker, Vector3 viewerPosition)
        {
            Vector3 speakerPosition = speaker.ViewPoint != null ? speaker.ViewPoint.transform.position : speaker.Position;
            return (speakerPosition - viewerPosition).sqrMagnitude;
        }
    }
}
