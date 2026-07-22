using UnityEngine;

namespace SS3D.Systems.Electricity
{
    /// <summary>
    /// Disables legacy scene/prefab Unity lights that are not owned by a <see cref="LightPower"/> fixture.
    /// Station illumination is fixture-driven only; orphan lights are editor/lobby leftovers.
    /// </summary>
    public static class GameplayLightGuard
    {
        public static void DisableOrphanSceneLights()
        {
#if UNITY_SERVER
            if (!UnityEngine.Application.isEditor)
            {
                return;
            }
#endif

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light == null || light.GetComponentInParent<LightPower>(true) != null)
                {
                    continue;
                }

                light.enabled = false;
                light.gameObject.SetActive(false);
            }
        }
    }
}
