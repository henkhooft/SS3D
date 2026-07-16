using Coimbra;
using SS3D.Systems.Entities.Humanoid.Body;
using UnityEngine;

namespace SS3D.Systems.Entities.Character
{
    /// <summary>
    /// Applies a <see cref="CharacterSheet"/> to a humanoid hierarchy (preview or spawned).
    /// Creates HairSocket / FacialHairSocket under the head when missing, disables baked hair meshes,
    /// and tints skin/hair via MaterialPropertyBlock.
    /// </summary>
    public static class HumanoidAppearanceApplier
    {
        public const string HairSocketName = "HairSocket";
        public const string FacialHairSocketName = "FacialHairSocket";
        public const string BakedHairName = "Hair";
        public const string BakedFacialHairName = "Facial Hair";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly MaterialPropertyBlock PropertyBlock = new();

        public static void Apply(GameObject root, in CharacterSheet sheet, AppearanceCatalog catalog)
        {
            if (root == null)
            {
                return;
            }

            CharacterSheet validated = sheet.Validated(catalog);
            Transform hairSocket = EnsureSocket(root, HairSocketName);
            Transform facialSocket = EnsureSocket(root, FacialHairSocketName);

            DisableBakedStyleMeshes(root, hairSocket, facialSocket);
            ClearChildren(hairSocket);
            ClearChildren(facialSocket);

            if (catalog != null)
            {
                SpawnStyle(catalog.GetHairStyle(validated.HairStyleId), hairSocket);
                SpawnStyle(catalog.GetBeardStyle(validated.BeardStyleId), facialSocket);

                Color skin = catalog.GetSkinTone(validated.SkinToneIndex);
                Color hair = catalog.GetHairColor(validated.HairColorIndex);
                ApplySkinTint(root, skin, catalog.SkinMaterialNameContains);
                ApplyHairTint(hairSocket, hair);
                ApplyHairTint(facialSocket, hair);
            }
        }

        private static Transform EnsureSocket(GameObject root, string socketName)
        {
            Transform existing = FindDescendant(root.transform, socketName);
            if (existing != null)
            {
                return existing;
            }

            Transform head = FindHead(root.transform);
            Transform parent = head != null ? head : root.transform;

            GameObject socket = new(socketName);
            socket.transform.SetParent(parent, false);
            socket.transform.localPosition = Vector3.zero;
            socket.transform.localRotation = Quaternion.identity;
            socket.transform.localScale = Vector3.one;
            return socket.transform;
        }

        private static Transform FindHead(Transform root)
        {
            HumanoidRigReferences rig = root.GetComponentInChildren<HumanoidRigReferences>(true);
            if (rig != null && rig.Head != null)
            {
                return rig.Head;
            }

            Transform byName = FindDescendant(root, "head");
            if (byName != null)
            {
                return byName;
            }

            return FindDescendant(root, "Head");
        }

        private static void DisableBakedStyleMeshes(GameObject root, Transform hairSocket, Transform facialSocket)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                Transform t = renderer.transform;
                if (IsUnder(t, hairSocket) || IsUnder(t, facialSocket))
                {
                    continue;
                }

                string objectName = t.gameObject.name;
                if (objectName == BakedHairName || objectName == BakedFacialHairName || objectName == "Eyebrows")
                {
                    renderer.enabled = false;
                }
            }
        }

        private static void ClearChildren(Transform socket)
        {
            if (socket == null)
            {
                return;
            }

            for (int i = socket.childCount - 1; i >= 0; i--)
            {
                Transform child = socket.GetChild(i);
                if (UnityEngine.Application.isPlaying)
                {
                    child.gameObject.Dispose(true);
                }
                else
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void SpawnStyle(GameObject prefab, Transform socket)
        {
            if (prefab == null || socket == null)
            {
                return;
            }

            GameObject instance = Object.Instantiate(prefab, socket, false);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
        }

        private static void ApplySkinTint(GameObject root, Color color, string materialNameContains)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (!RendererUsesMaterialName(renderer, materialNameContains))
                {
                    continue;
                }

                SetColor(renderer, color);
            }
        }

        private static void ApplyHairTint(Transform socket, Color color)
        {
            if (socket == null)
            {
                return;
            }

            Renderer[] renderers = socket.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                SetColor(renderer, color);
            }
        }

        private static bool RendererUsesMaterialName(Renderer renderer, string materialNameContains)
        {
            if (renderer == null || string.IsNullOrEmpty(materialNameContains))
            {
                return false;
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials == null)
            {
                return false;
            }

            foreach (Material material in materials)
            {
                if (material != null && material.name.Contains(materialNameContains))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetColor(Renderer renderer, Color color)
        {
            renderer.GetPropertyBlock(PropertyBlock);
            PropertyBlock.SetColor(BaseColorId, color);
            PropertyBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(PropertyBlock);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static bool IsUnder(Transform transform, Transform ancestor)
        {
            if (transform == null || ancestor == null)
            {
                return false;
            }

            Transform current = transform;
            while (current != null)
            {
                if (current == ancestor)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
