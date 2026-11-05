using UnityEngine;

namespace SS3D.Systems.Audio
{
    /// <summary>
    /// What one kind of place sounds like (audio.md §2). A department's profile has the department's
    /// name in lower case as its id, and a room can name any other profile as its override.
    /// </summary>
    [CreateAssetMenu(fileName = "AmbienceProfile", menuName = "SS3D/Audio/Ambience Profile")]
    public sealed class AmbienceProfile : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private AudioClip _bed;
        [SerializeField, Range(0f, 1f)] private float _bedVolume = 1f;

        public string Id => _id;
        public AudioClip Bed => _bed;
        public float BedVolume => _bedVolume;
    }
}
