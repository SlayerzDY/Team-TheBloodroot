using UnityEngine;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Authored IInteract adapter for a truck or other travel object. Travel
    /// success and rejection remain available on CampaignSceneTravel's events.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class CampaignTravelInteractable : MonoBehaviour, IInteract
    {
        [SerializeField]
        private CampaignSceneTravel sceneTravel;

        public CampaignSceneTravel SceneTravel => sceneTravel;

        public void SendInteract(Collider target)
        {
            if (sceneTravel == null)
            {

                return;
            }

            sceneTravel.TryTravel();
        }

        public void Configure(CampaignSceneTravel travel)
        {
            sceneTravel = travel;
        }

        private void Reset()
        {
            sceneTravel = GetComponent<CampaignSceneTravel>();
        }
    }
}
