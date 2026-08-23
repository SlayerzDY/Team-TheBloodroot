using Bloodroot.Campaign;
using UnityEngine;

namespace Bloodroot.Features.WorldMissions
{
    /// <summary>
    /// Legacy migration shape retained for local scene-authoring tools.
    /// Current campaign scenes must not contain this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldMissionCompletionTravel : MonoBehaviour
    {
        [SerializeField] private WorldMissionDirector missionDirector;
        [SerializeField] private CampaignSceneTravel sceneTravel;

        public WorldMissionDirector MissionDirector => missionDirector;
        public CampaignSceneTravel SceneTravel => sceneTravel;
    }
}
