using System.Collections.Generic;
using System.Linq;
using SS3D.Core.Behaviours;
using SS3D.UI.Buttons;
using UnityEngine;

namespace SS3D.UI
{
    public class GenericTabControllerView : Actor
    {
        [SerializeField] private List<GenericTabView> _tabs;

        protected override void OnStart()
        {
            base.OnStart();

            SetupTabs();
            GenericTabView first = _tabs?.FirstOrDefault(tab => tab != null);
            if (first != null)
            {
                HandleTabButtonPressed(first);
            }
        }

        private void SetupTabs()
        {
            if (_tabs == null)
            {
                return;
            }

            foreach (GenericTabView tab in _tabs)
            {
                if (tab == null || tab.Button == null)
                {
                    continue;
                }

                tab.Button.onClick.AddListener(() => HandleTabButtonPressed(tab));
            }
        }

        private void HandleTabButtonPressed(GenericTabView tab)
        {
            if (tab == null)
            {
                return;
            }

            foreach (GenericTabView tabView in _tabs.Where(tabView => tabView != null))
            {
                tabView.SetTabActive(tab == tabView);
            }
        }
    }
}
