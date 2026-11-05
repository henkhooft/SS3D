using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SS3D.Systems.Audio
{
    /// <summary>Every <see cref="AmbienceProfile"/>, loaded from Resources.</summary>
    public sealed class AmbienceProfileCatalog : ScriptableObject
    {
        /// <summary>Plays for a room with no department, or one whose department has no profile yet.</summary>
        public const string GenericProfileId = "station";

        [SerializeField] private AmbienceProfile[] _profiles = Array.Empty<AmbienceProfile>();

        private static AmbienceProfileCatalog _instance;

        public static AmbienceProfileCatalog Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<AmbienceProfileCatalog>(nameof(AmbienceProfileCatalog));
                }

                return _instance;
            }
        }

        public IEnumerable<string> ProfileIds => _profiles.Select(profile => profile.Id);

        public bool TryGet(string id, out AmbienceProfile profile)
        {
            profile = _profiles.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
            return profile != null;
        }

        /// <summary>The profile named <paramref name="id"/>, or the generic one when there's no such profile.</summary>
        public AmbienceProfile Resolve(string id)
        {
            if (TryGet(id, out AmbienceProfile profile) || TryGet(GenericProfileId, out profile))
            {
                return profile;
            }

            return null;
        }
    }
}
