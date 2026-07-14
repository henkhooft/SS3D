using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Systems.Tile.MapEditor
{
    /// <summary>
    /// Hides sibling UI under <see cref="PlayerCanvas"/> while the map editor is active.
    /// </summary>
    internal sealed class MapEditorGameplayHud
    {
        private readonly Transform _editorRoot;
        private readonly List<(GameObject gameObject, bool wasActive)> _suppressed = new();

        public MapEditorGameplayHud(Transform editorRoot)
        {
            _editorRoot = editorRoot;
        }

        public void SetVisible(bool visible)
        {
            if (visible)
                Restore();
            else
                Suppress();
        }

        private void Suppress()
        {
            Restore();

            Transform canvasRoot = FindPlayerCanvasRoot();
            if (canvasRoot == null)
                return;

            foreach (Transform child in canvasRoot)
            {
                if (IsEditorHierarchy(child))
                    continue;

                GameObject go = child.gameObject;
                _suppressed.Add((go, go.activeSelf));
                go.SetActive(false);
            }
        }

        private void Restore()
        {
            foreach ((GameObject gameObject, bool wasActive) in _suppressed)
            {
                if (gameObject != null)
                    gameObject.SetActive(wasActive);
            }

            _suppressed.Clear();
        }

        private Transform FindPlayerCanvasRoot()
        {
            Transform current = _editorRoot;
            while (current != null)
            {
                if (current.name == "PlayerCanvas")
                    return current;

                current = current.parent;
            }

            return null;
        }

        private bool IsEditorHierarchy(Transform candidate) =>
            candidate == _editorRoot || candidate.IsChildOf(_editorRoot);
    }
}
