using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SS3D.Core.Behaviours;
using UnityEngine.UI;
using UnityEngine.Experimental.Rendering;
using System;
using SS3D.Systems.Selection;
using SS3D.Core;

namespace SS3D.Systems.Examine
{
    /// <summary>
    /// The Examine System allows additional detail of items to be displayed when
    /// the cursor hovers over them. The particular information displayed is item
    /// and requirement dependant, and may take different formats.
    /// </summary>
    public class ExamineSubSystem : NetworkSubSystem
    {
        public event ExaminableChangedHandler OnExaminableChanged;
        public event ExaminableChangedHandler OnDetailedExamineRequested;

        public delegate void ExaminableChangedHandler(IExaminable examinable);
        
        private SelectionSubSystem _selectionSystem;
        
        protected override void OnAwake()
        {
            base.OnAwake();
            // Sibling on NetworkSystemsHub — Awake order may run before Selection registers.
            SubSystems.TryGet(out _selectionSystem);
        }

        protected override void OnEnabled()
        {
            base.OnEnabled();

            if (_selectionSystem == null)
            {
                SubSystems.TryGet(out _selectionSystem);
            }

            if (_selectionSystem != null)
            {
                _selectionSystem.OnSelectableChanged += UpdateExaminable;
            }
        }

        protected override void OnDisabled()
        {
            base.OnDisabled();

            if (_selectionSystem != null)
            {
                _selectionSystem.OnSelectableChanged -= UpdateExaminable;
            }
        }

        private void UpdateExaminable()
        {
            // Get the examinable under the cursor
            IExaminable current = _selectionSystem.GetCurrentSelectable<IExaminable>();
            OnExaminableChanged?.Invoke(current);
        }

        /// <summary>
        /// Opens the detailed examine panel for a target chosen from the radial menu.
        /// </summary>
        public void ShowDetailedExamine(IExaminable examinable)
        {
            OnDetailedExamineRequested?.Invoke(examinable);
        }
    }
}
