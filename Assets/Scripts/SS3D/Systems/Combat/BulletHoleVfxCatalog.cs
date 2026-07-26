using UnityEngine;

namespace SS3D.Systems.Combat
{
    /// <summary>
    /// Shared bullet-hole decal assets loaded from Resources.
    /// </summary>
    public sealed class BulletHoleVfxCatalog : ScriptableObject
    {
        [SerializeField] private Texture2D[] _holes;
        [SerializeField] private Material _decalMaterial;
        [SerializeField] private GameObject _floorDecalPrefab;

        private static BulletHoleVfxCatalog _instance;

        public static BulletHoleVfxCatalog Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<BulletHoleVfxCatalog>(nameof(BulletHoleVfxCatalog));
                }

                return _instance;
            }
        }

        public Material DecalMaterial => _decalMaterial;

        public GameObject FloorDecalPrefab => _floorDecalPrefab;

        public Texture2D PickRandomHole()
        {
            if (_holes == null || _holes.Length == 0)
            {
                return null;
            }

            return _holes[Random.Range(0, _holes.Length)];
        }

        public Material CreateDecalMaterial()
        {
            if (_decalMaterial == null)
            {
                return null;
            }

            Material instance = new Material(_decalMaterial);
            Texture2D hole = PickRandomHole();
            if (hole == null)
            {
                return instance;
            }

            if (instance.HasProperty("Base_Map"))
            {
                instance.SetTexture("Base_Map", hole);
            }

            if (instance.HasProperty("_BaseMap"))
            {
                instance.SetTexture("_BaseMap", hole);
            }

            if (instance.HasProperty("_MainTex"))
            {
                instance.SetTexture("_MainTex", hole);
            }

            return instance;
        }
    }
}
