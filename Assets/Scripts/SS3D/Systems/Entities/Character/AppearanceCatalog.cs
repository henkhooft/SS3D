using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Entities.Character
{
    /// <summary>
    /// Ordered appearance options for the character creator and spawn apply.
    /// Beard index 0 is reserved for None.
    /// </summary>
    [CreateAssetMenu(fileName = "AppearanceCatalog", menuName = "SS3D/Character/Appearance Catalog")]
    public class AppearanceCatalog : ScriptableObject
    {
        [SerializeField] private List<GameObject> _hairStyles = new();
        [SerializeField] private List<GameObject> _beardStyles = new();
        [SerializeField] private List<Color> _skinTones = new();
        [SerializeField] private List<Color> _hairColors = new();
        [SerializeField] private string _skinMaterialNameContains = "HumanSkin";

        public IReadOnlyList<GameObject> HairStyles => _hairStyles;
        public IReadOnlyList<GameObject> BeardStyles => _beardStyles;
        public IReadOnlyList<Color> SkinTones => _skinTones;
        public IReadOnlyList<Color> HairColors => _hairColors;
        public string SkinMaterialNameContains => _skinMaterialNameContains;

        public int HairStyleCount => _hairStyles?.Count ?? 0;
        public int BeardStyleCount => _beardStyles?.Count ?? 0;
        public int SkinToneCount => _skinTones?.Count ?? 0;
        public int HairColorCount => _hairColors?.Count ?? 0;

        public GameObject GetHairStyle(int id)
        {
            if (_hairStyles == null || id < 0 || id >= _hairStyles.Count)
            {
                return null;
            }

            return _hairStyles[id];
        }

        /// <summary>
        /// Index 0 is None (no beard). Positive indices map to _beardStyles[id - 1] conceptually
        /// if the first entry is null, or directly when catalog includes a null slot at 0.
        /// </summary>
        public GameObject GetBeardStyle(int id)
        {
            if (_beardStyles == null || id < 0 || id >= _beardStyles.Count)
            {
                return null;
            }

            return _beardStyles[id];
        }

        public Color GetSkinTone(int index)
        {
            if (_skinTones == null || _skinTones.Count == 0)
            {
                return new Color(1f, 0.74f, 0.6f, 1f);
            }

            return _skinTones[ClampSkinToneIndex(index)];
        }

        public Color GetHairColor(int index)
        {
            if (_hairColors == null || _hairColors.Count == 0)
            {
                return new Color(0.5f, 0.28f, 0.19f, 1f);
            }

            return _hairColors[ClampHairColorIndex(index)];
        }

        public int ClampHairStyleId(int id) => Clamp(id, HairStyleCount);
        public int ClampBeardStyleId(int id) => Clamp(id, BeardStyleCount);
        public int ClampSkinToneIndex(int index) => Clamp(index, SkinToneCount);
        public int ClampHairColorIndex(int index) => Clamp(index, HairColorCount);

        private static int Clamp(int value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            return Mathf.Clamp(value, 0, count - 1);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            if (_skinTones == null || _skinTones.Count == 0)
            {
                _skinTones = new List<Color>
                {
                    new Color(1f, 0.85f, 0.72f),
                    new Color(0.96f, 0.76f, 0.58f),
                    new Color(0.80f, 0.58f, 0.40f),
                    new Color(0.55f, 0.36f, 0.24f),
                    new Color(0.32f, 0.20f, 0.14f),
                };
            }

            if (_hairColors == null || _hairColors.Count == 0)
            {
                _hairColors = new List<Color>
                {
                    new Color(0.12f, 0.10f, 0.09f),
                    new Color(0.35f, 0.22f, 0.12f),
                    new Color(0.55f, 0.30f, 0.15f),
                    new Color(0.72f, 0.55f, 0.28f),
                    new Color(0.75f, 0.20f, 0.15f),
                    new Color(0.85f, 0.85f, 0.88f),
                };
            }
        }
#endif
    }
}
