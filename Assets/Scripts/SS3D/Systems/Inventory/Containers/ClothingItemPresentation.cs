using UnityEngine;

namespace SS3D.Systems.Inventory.Containers
{
    /// <summary>
    /// World/hand visual form for clothing Items: folded mesh child on, worn-shaped child off.
    /// Worn look is <see cref="ClothesDisplayer"/> + <see cref="Cloth"/> body mesh while the Item
    /// is hidden in a cloth slot — same NetworkObject, no dual-prefab swap.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Cloth))]
    public class ClothingItemPresentation : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Folded / piled mesh shown in the world and when held.")]
        private GameObject _worldRoot;

        [SerializeField]
        [Tooltip("Body-shaped mesh used only as a Cloth source historically; keep inactive for world/hand.")]
        private GameObject _wornShapedRoot;

        private void Awake()
        {
            ApplyWorldForm();
        }

        /// <summary>
        /// Activate the folded world mesh and deactivate the worn-shaped child.
        /// Safe to call when becoming visible again after a hide container.
        /// </summary>
        public void ApplyWorldForm()
        {
            if (_worldRoot != null)
            {
                _worldRoot.SetActive(true);
            }

            if (_wornShapedRoot != null)
            {
                _wornShapedRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Worn slots hide via <see cref="Items.Item.SetVisibility"/>; body mesh is
        /// <see cref="ClothesDisplayer"/>. Child activity can stay on world form.
        /// </summary>
        public void ApplyHiddenForm()
        {
            // Intentionally empty — HideItems disables renderers; no child swap required.
        }
    }
}
