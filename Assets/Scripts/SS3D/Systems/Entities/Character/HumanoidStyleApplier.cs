using UnityEngine;

namespace SS3D.Systems.Entities.Character
{
    /// <summary>
    /// Applies hair / beard prefabs and hair colour tint to a humanoid preview dummy.
    ///
    /// Attach / detach flow:
    ///   1. Disable baked "Hair" and "Facial Hair" sub-meshes from the base FBX.
    ///   2. Create (or reuse) socket GameObjects under the head bone.
    ///   3. Clear old style children, instantiate new prefab.
    ///   4. Tint all renderers inside each socket via MaterialPropertyBlock.
    /// </summary>
    public static class HumanoidStyleApplier
    {
        private const string HairSocketName = "HairSocket";
        private const string FacialHairSocketName = "FacialHairSocket";
        private const string BakedHairName = "Hair";
        private const string BakedFacialHairName = "Facial Hair";
        private static readonly int ShaderColorId = Shader.PropertyToID("_BaseColor");

        /// <param name="root">Root GameObject of the preview dummy.</param>
        /// <param name="hairPrefab">Head hair prefab, or null for none.</param>
        /// <param name="beardPrefab">Facial hair prefab, or null for none.</param>
        /// <param name="hairColor">Tint applied to both hair and beard renderers.</param>
        public static void Apply(
            GameObject root,
            GameObject hairPrefab,
            GameObject beardPrefab,
            Color hairColor)
        {
            if (root == null)
            {
                return;
            }

            Transform hairSocket = EnsureSocket(root, HairSocketName);
            Transform facialSocket = EnsureSocket(root, FacialHairSocketName);

            DisableBakedStyleMeshes(root, hairSocket, facialSocket);

            ClearChildren(hairSocket);
            ClearChildren(facialSocket);

            SpawnStyle(hairPrefab, hairSocket);
            SpawnStyle(beardPrefab, facialSocket);

            ApplyHairTint(hairSocket, hairColor);
            ApplyHairTint(facialSocket, hairColor);
        }

        private static Transform EnsureSocket(GameObject root, string socketName)
        {
            Transform existing = FindDescendant(root.transform, socketName);
            if (existing != null)
            {
                return existing;
            }

            // Attach to head bone if found, otherwise to root.
            Transform parent = FindDescendant(root.transform, "head") ?? root.transform;

            GameObject socket = new(socketName);
            socket.transform.SetParent(parent, false);
            socket.transform.localPosition = Vector3.zero;
            socket.transform.localRotation = Quaternion.identity;
            socket.transform.localScale = Vector3.one;
            return socket.transform;
        }

        private static void DisableBakedStyleMeshes(
            GameObject root,
            Transform hairSocket,
            Transform facialSocket)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (IsUnder(r.transform, hairSocket) || IsUnder(r.transform, facialSocket))
                {
                    continue;
                }

                string objectName = r.gameObject.name;
                if (objectName == BakedHairName
                    || objectName == BakedFacialHairName
                    || objectName == "Eyebrows")
                {
                    r.enabled = false;
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
                Object.Destroy(socket.GetChild(i).gameObject);
            }
        }

        private static void SpawnStyle(GameObject prefab, Transform socket)
        {
            if (prefab == null || socket == null)
            {
                return;
            }

            Object.Instantiate(prefab, socket, false);
        }

        private static void ApplyHairTint(Transform socket, Color color)
        {
            if (socket == null)
            {
                return;
            }

            MaterialPropertyBlock mpb = new();
            mpb.SetColor(ShaderColorId, color);

            foreach (Renderer r in socket.GetComponentsInChildren<Renderer>(true))
            {
                r.SetPropertyBlock(mpb);
            }
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

        private static bool IsUnder(Transform t, Transform ancestor)
        {
            if (ancestor == null)
            {
                return false;
            }

            Transform current = t.parent;
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
