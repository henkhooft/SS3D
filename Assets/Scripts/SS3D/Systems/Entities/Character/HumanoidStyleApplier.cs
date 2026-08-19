using System.Collections.Generic;
using Coimbra;
using UnityEngine;

namespace SS3D.Systems.Entities.Character
{
    /// <summary>
    /// Applies hair / beard prefabs and hair colour tint to a humanoid preview dummy.
    ///
    /// Hair prefabs are authored in model space under the Human "03 Hair" group. Sockets are
    /// parented to the animated head bone using bind-pose offsets sampled from the baked meshes.
    /// </summary>
    public static class HumanoidStyleApplier
    {
        private const string HairGroupName = "03 Hair";
        private const string HairSocketName = "HairSocket";
        private const string FacialHairSocketName = "FacialHairSocket";
        private const string BakedHairName = "Hair";
        private const string BakedFacialHairName = "Facial Hair";
        private static readonly int ShaderColorId = Shader.PropertyToID("_BaseColor");

        private static readonly Dictionary<int, StyleSocketOffsets> OffsetCache = new();

        private struct StyleSocketOffsets
        {
            public bool HasHair;
            public Vector3 HairLocalPosition;
            public Quaternion HairLocalRotation;
            public bool HasFacial;
            public Vector3 FacialLocalPosition;
            public Quaternion FacialLocalRotation;
        }

        /// <summary>
        /// Samples baked hair / facial-hair transforms relative to the head bone (bind pose).
        /// Call while the animator is at Speed=0 before enabling walk locomotion.
        /// </summary>
        public static void WarmOffsetCache(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            int id = root.GetInstanceID();
            if (OffsetCache.ContainsKey(id))
            {
                return;
            }

            Transform head = FindHead(root.transform);
            if (head == null)
            {
                return;
            }

            Transform hairGroup = FindDescendant(root.transform, HairGroupName);
            Transform bakedHair = FindDirectChild(hairGroup, BakedHairName);
            Transform bakedFacial = FindDirectChild(hairGroup, BakedFacialHairName);

            StyleSocketOffsets offsets = default;
            if (bakedHair != null)
            {
                offsets.HasHair = true;
                offsets.HairLocalPosition = head.InverseTransformPoint(bakedHair.position);
                offsets.HairLocalRotation = Quaternion.Inverse(head.rotation) * bakedHair.rotation;
            }

            if (bakedFacial != null)
            {
                offsets.HasFacial = true;
                offsets.FacialLocalPosition = head.InverseTransformPoint(bakedFacial.position);
                offsets.FacialLocalRotation = Quaternion.Inverse(head.rotation) * bakedFacial.rotation;
            }

            OffsetCache[id] = offsets;
        }

        public static void ClearOffsetCache(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            OffsetCache.Remove(root.GetInstanceID());
        }

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

            WarmOffsetCache(root);

            Transform hairSocket = EnsureSocket(root, HairSocketName, facial: false);
            Transform facialSocket = EnsureSocket(root, FacialHairSocketName, facial: true);

            DisableBakedStyleMeshes(root, hairSocket, facialSocket);

            ClearChildren(hairSocket);
            ClearChildren(facialSocket);

            SpawnStyle(hairPrefab, hairSocket);
            SpawnStyle(beardPrefab, facialSocket);

            ApplyHairTint(hairSocket, hairColor);
            ApplyHairTint(facialSocket, hairColor);
        }

        private static Transform EnsureSocket(GameObject root, string socketName, bool facial)
        {
            Transform existing = FindDescendant(root.transform, socketName);
            if (existing != null)
            {
                ApplySocketOffset(existing, root, facial);
                return existing;
            }

            Transform head = FindHead(root.transform);
            Transform parent = head != null ? head : root.transform;

            GameObject socket = new(socketName);
            socket.transform.SetParent(parent, false);
            ApplySocketOffset(socket.transform, root, facial);
            socket.transform.localScale = Vector3.one;
            return socket.transform;
        }

        private static void ApplySocketOffset(Transform socket, GameObject root, bool facial)
        {
            StyleSocketOffsets offsets = GetOffsets(root);
            if (facial && offsets.HasFacial)
            {
                socket.localPosition = offsets.FacialLocalPosition;
                socket.localRotation = offsets.FacialLocalRotation;
                return;
            }

            if (!facial && offsets.HasHair)
            {
                socket.localPosition = offsets.HairLocalPosition;
                socket.localRotation = offsets.HairLocalRotation;
                return;
            }

            socket.localPosition = Vector3.zero;
            socket.localRotation = Quaternion.identity;
        }

        private static StyleSocketOffsets GetOffsets(GameObject root)
        {
            int id = root.GetInstanceID();
            if (OffsetCache.TryGetValue(id, out StyleSocketOffsets offsets))
            {
                return offsets;
            }

            WarmOffsetCache(root);
            OffsetCache.TryGetValue(id, out offsets);
            return offsets;
        }

        private static void DisableBakedStyleMeshes(
            GameObject root,
            Transform hairSocket,
            Transform facialSocket)
        {
            Transform hairGroup = FindDescendant(root.transform, HairGroupName);
            if (hairGroup == null)
            {
                return;
            }

            Transform bakedHair = FindDirectChild(hairGroup, BakedHairName);
            Transform bakedFacial = FindDirectChild(hairGroup, BakedFacialHairName);

            DisableRendererOn(bakedHair, hairSocket, facialSocket);
            DisableRendererOn(bakedFacial, hairSocket, facialSocket);

            Transform bakedBrows = FindDirectChild(hairGroup, "Eyebrows");
            DisableRendererOn(bakedBrows, hairSocket, facialSocket);
        }

        private static void DisableRendererOn(
            Transform target,
            Transform hairSocket,
            Transform facialSocket)
        {
            if (target == null || IsUnder(target, hairSocket) || IsUnder(target, facialSocket))
            {
                return;
            }

            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
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
                socket.GetChild(i).gameObject.Dispose(true);
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

        private static Transform FindHead(Transform root)
        {
            Transform head = FindDescendant(root, "head");
            if (head != null)
            {
                return head;
            }

            return FindDescendant(root, "Head");
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
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
