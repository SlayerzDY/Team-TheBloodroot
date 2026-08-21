using System;
using System.Collections.Generic;
using Bloodroot.Features.FarmPrologue;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bloodroot.Campaign
{
    public enum CampaignEnvironmentMode
    {
        Farm = 0,
        OpenWorld = 1
    }

    [Serializable]
    public sealed class CampaignEnvironmentPreset
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private Material skybox;
        [SerializeField] private Volume postProcessVolume;
        [SerializeField] private Color fogColor = Color.gray;
        [SerializeField, Min(0f)] private float fogDensity = 0.01f;
        [SerializeField] private Color ambientSkyColor = Color.gray;
        [SerializeField] private Color ambientEquatorColor = Color.gray;
        [SerializeField] private Color ambientGroundColor = Color.gray;
        [SerializeField, Min(0f)] private float ambientIntensity = 1f;
        [SerializeField, Range(0f, 1f)] private float reflectionIntensity = 1f;
        [SerializeField] private Color sunColor = Color.white;
        [SerializeField, Min(0f)] private float sunIntensity = 1f;
        [SerializeField] private Vector3 sunEulerAngles = new(35f, -30f, 0f);

        public CampaignEnvironmentPreset(
            string presetId,
            Material authoredSkybox,
            Volume authoredPostProcessVolume,
            Color authoredFogColor,
            float authoredFogDensity,
            Color authoredAmbientSkyColor,
            Color authoredAmbientEquatorColor,
            Color authoredAmbientGroundColor,
            float authoredAmbientIntensity,
            float authoredReflectionIntensity,
            Color authoredSunColor,
            float authoredSunIntensity,
            Vector3 authoredSunEulerAngles)
        {
            id = presetId ?? string.Empty;
            skybox = authoredSkybox;
            postProcessVolume = authoredPostProcessVolume;
            fogColor = authoredFogColor;
            fogDensity = Mathf.Max(0f, authoredFogDensity);
            ambientSkyColor = authoredAmbientSkyColor;
            ambientEquatorColor = authoredAmbientEquatorColor;
            ambientGroundColor = authoredAmbientGroundColor;
            ambientIntensity = Mathf.Max(0f, authoredAmbientIntensity);
            reflectionIntensity = Mathf.Clamp01(authoredReflectionIntensity);
            sunColor = authoredSunColor;
            sunIntensity = Mathf.Max(0f, authoredSunIntensity);
            sunEulerAngles = authoredSunEulerAngles;
        }

        public string Id => id;
        public Material Skybox => skybox;
        public Volume PostProcessVolume => postProcessVolume;
        public Color FogColor => fogColor;
        public float FogDensity => fogDensity;
        public Color AmbientSkyColor => ambientSkyColor;
        public Color AmbientEquatorColor => ambientEquatorColor;
        public Color AmbientGroundColor => ambientGroundColor;
        public float AmbientIntensity => ambientIntensity;
        public float ReflectionIntensity => reflectionIntensity;
        public Color SunColor => sunColor;
        public float SunIntensity => sunIntensity;
        public Vector3 SunEulerAngles => sunEulerAngles;
    }

    [Serializable]
    public sealed class CampaignAreaEnvironmentBinding
    {
        [SerializeField] private CampaignAreaId area;
        [SerializeField] private Transform anchor;
        [SerializeField] private CampaignEnvironmentPreset preset;

        public CampaignAreaEnvironmentBinding(
            CampaignAreaId authoredArea,
            Transform authoredAnchor,
            CampaignEnvironmentPreset authoredPreset)
        {
            area = authoredArea;
            anchor = authoredAnchor;
            preset = authoredPreset;
        }

        public CampaignAreaId Area => area;
        public Transform Anchor => anchor;
        public CampaignEnvironmentPreset Preset => preset;
    }

    /// <summary>
    /// Owns campaign-only atmosphere transitions. Farm presentation follows
    /// the Prologue-to-Hub lifecycle; the continuous Open World follows the
    /// Player's nearest authored regional anchor so a tower unlock cannot
    /// change the sky while the Player is still standing in the prior area.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class CampaignEnvironmentTransitionController : MonoBehaviour
    {
        private static readonly CampaignAreaId[] RequiredAreaOrder =
        {
            CampaignAreaId.BlackPines,
            CampaignAreaId.StillwaterFeedMill,
            CampaignAreaId.HarrowEstate,
            CampaignAreaId.BloodrootHollow
        };

        [SerializeField] private CampaignEnvironmentMode mode;
        [SerializeField] private CampaignStateService campaignState;
        [SerializeField] private FarmPrologueDirector farmPrologueDirector;
        [SerializeField] private CampaignRegionalRespawn regionalRespawn;
        [SerializeField] private Light directionalLight;
        [SerializeField] private CampaignEnvironmentPreset farmProloguePreset;
        [SerializeField] private CampaignEnvironmentPreset farmHubPreset;
        [SerializeField] private CampaignAreaEnvironmentBinding[] areaBindings =
            Array.Empty<CampaignAreaEnvironmentBinding>();
        [SerializeField, Min(0.1f)] private float transitionSeconds = 8f;
        [SerializeField, Range(0.05f, 2f)] private float areaPollSeconds = 0.25f;
        [SerializeField, Min(0f)] private float areaSwitchHysteresis = 25f;

        private Material runtimeSkybox;
        private Material transitionStartSkybox;
        private CampaignEnvironmentPreset currentPreset;
        private CampaignEnvironmentPreset targetPreset;
        private CampaignAreaId? currentArea;
        private float transitionElapsed;
        private float nextAreaPollAt;
        private bool transitionActive;
        private bool subscribed;

        private Color startFogColor;
        private float startFogDensity;
        private Color startAmbientSky;
        private Color startAmbientEquator;
        private Color startAmbientGround;
        private float startAmbientIntensity;
        private float startReflectionIntensity;
        private Color startSunColor;
        private float startSunIntensity;
        private Quaternion startSunRotation;
        private Volume[] ownedVolumes = Array.Empty<Volume>();
        private float[] startVolumeWeights = Array.Empty<float>();

        public CampaignEnvironmentMode Mode => mode;
        public CampaignStateService CampaignState => campaignState;
        public FarmPrologueDirector FarmPrologueDirector => farmPrologueDirector;
        public CampaignRegionalRespawn RegionalRespawn => regionalRespawn;
        public Light DirectionalLight => directionalLight;
        public CampaignEnvironmentPreset FarmProloguePreset => farmProloguePreset;
        public CampaignEnvironmentPreset FarmHubPreset => farmHubPreset;
        public IReadOnlyList<CampaignAreaEnvironmentBinding> AreaBindings =>
            areaBindings ?? Array.Empty<CampaignAreaEnvironmentBinding>();
        public float TransitionSeconds => transitionSeconds;
        public float AreaPollSeconds => areaPollSeconds;
        public float AreaSwitchHysteresis => areaSwitchHysteresis;
        public string CurrentPresetId => currentPreset?.Id ?? string.Empty;
        public bool IsTransitioning => transitionActive;

        private void Awake()
        {
            ResolveCampaignState();
            BuildOwnedVolumeCache();
            ApplyInitialEnvironment();
        }

        private void OnEnable()
        {
            Bind();
            if (runtimeSkybox == null)
            {
                ResolveCampaignState();
                BuildOwnedVolumeCache();
                ApplyInitialEnvironment();
            }
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();
            if (runtimeSkybox != null && RenderSettings.skybox == runtimeSkybox)
            {
                RenderSettings.skybox = targetPreset?.Skybox;
            }

            DestroyRuntimeMaterial(runtimeSkybox);
            DestroyRuntimeMaterial(transitionStartSkybox);
        }

        private void OnValidate()
        {
            transitionSeconds = Mathf.Max(0.1f, transitionSeconds);
            areaPollSeconds = Mathf.Clamp(areaPollSeconds, 0.05f, 2f);
            areaSwitchHysteresis = Mathf.Max(0f, areaSwitchHysteresis);
        }

        private void Update()
        {
            if (mode == CampaignEnvironmentMode.OpenWorld &&
                Time.unscaledTime >= nextAreaPollAt)
            {
                nextAreaPollAt = Time.unscaledTime + areaPollSeconds;
                ReconcileOpenWorldEnvironment();
            }

            if (!transitionActive || targetPreset == null)
                return;

            transitionElapsed += Time.unscaledDeltaTime;
            float linear = Mathf.Clamp01(transitionElapsed / transitionSeconds);
            float blend = linear * linear * (3f - (2f * linear));
            ApplyBlendedEnvironment(targetPreset, blend);
            if (linear < 1f)
                return;

            transitionActive = false;
            currentPreset = targetPreset;
            DynamicGI.UpdateEnvironment();
        }

        public void ConfigureFarm(
            CampaignStateService state,
            FarmPrologueDirector director,
            Light authoredDirectionalLight,
            CampaignEnvironmentPreset prologuePreset,
            CampaignEnvironmentPreset hubPreset,
            float authoredTransitionSeconds = 8f)
        {
            Unbind();
            mode = CampaignEnvironmentMode.Farm;
            campaignState = state;
            farmPrologueDirector = director;
            regionalRespawn = null;
            directionalLight = authoredDirectionalLight;
            farmProloguePreset = prologuePreset;
            farmHubPreset = hubPreset;
            areaBindings = Array.Empty<CampaignAreaEnvironmentBinding>();
            transitionSeconds = Mathf.Max(0.1f, authoredTransitionSeconds);
            areaPollSeconds = 0.25f;
            areaSwitchHysteresis = 25f;
            BuildOwnedVolumeCache();
            if (isActiveAndEnabled)
            {
                Bind();
            }
        }

        public void ConfigureOpenWorld(
            CampaignStateService state,
            CampaignRegionalRespawn authoredRegionalRespawn,
            Light authoredDirectionalLight,
            CampaignAreaEnvironmentBinding[] authoredAreaBindings,
            float authoredTransitionSeconds = 8f,
            float authoredAreaPollSeconds = 0.25f,
            float authoredAreaSwitchHysteresis = 25f)
        {
            Unbind();
            mode = CampaignEnvironmentMode.OpenWorld;
            campaignState = state;
            farmPrologueDirector = null;
            regionalRespawn = authoredRegionalRespawn;
            directionalLight = authoredDirectionalLight;
            farmProloguePreset = null;
            farmHubPreset = null;
            areaBindings = authoredAreaBindings != null
                ? (CampaignAreaEnvironmentBinding[])authoredAreaBindings.Clone()
                : Array.Empty<CampaignAreaEnvironmentBinding>();
            transitionSeconds = Mathf.Max(0.1f, authoredTransitionSeconds);
            areaPollSeconds = Mathf.Clamp(authoredAreaPollSeconds, 0.05f, 2f);
            areaSwitchHysteresis = Mathf.Max(0f, authoredAreaSwitchHysteresis);
            BuildOwnedVolumeCache();
            if (isActiveAndEnabled)
            {
                Bind();
            }
        }

        public bool ValidateRuntimeContract(out string problem)
        {
            problem = string.Empty;
            if (directionalLight == null ||
                directionalLight.type != LightType.Directional ||
                transitionSeconds < 0.1f || transitionSeconds > 30f)
            {
                problem =
                    "Campaign environment requires one directional light and a 0.1-30 second transition.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var materials = new HashSet<Material>();
            var volumes = new HashSet<Volume>();
            Shader commonShader = null;

            if (mode == CampaignEnvironmentMode.Farm)
            {
                if (farmPrologueDirector == null || regionalRespawn != null ||
                    areaBindings == null || areaBindings.Length != 0 ||
                    !ValidatePreset(farmProloguePreset, ids, materials, volumes,
                        ref commonShader, out problem) ||
                    !ValidatePreset(farmHubPreset, ids, materials, volumes,
                        ref commonShader, out problem))
                {
                    if (string.IsNullOrEmpty(problem))
                    {
                        problem =
                            "Farm environment requires its director and exactly two unique presets.";
                    }

                    return false;
                }
            }
            else
            {
                if (regionalRespawn == null || farmPrologueDirector != null ||
                    areaBindings == null ||
                    areaBindings.Length != RequiredAreaOrder.Length)
                {
                    problem =
                        "Open World environment requires regional respawn and four ordered bindings.";
                    return false;
                }

                var anchors = new HashSet<Transform>();
                for (int index = 0; index < RequiredAreaOrder.Length; index++)
                {
                    CampaignAreaEnvironmentBinding binding = areaBindings[index];
                    if (binding == null || binding.Area != RequiredAreaOrder[index] ||
                        binding.Anchor == null || !anchors.Add(binding.Anchor) ||
                        !ValidatePreset(binding.Preset, ids, materials, volumes,
                            ref commonShader, out problem))
                    {
                        if (string.IsNullOrEmpty(problem))
                        {
                            problem =
                                $"Open World environment binding {index + 1} is incomplete, duplicated, or out of order.";
                        }

                        return false;
                    }
                }
            }

            problem = string.Empty;
            return true;
        }

        private static bool ValidatePreset(
            CampaignEnvironmentPreset preset,
            ISet<string> ids,
            ISet<Material> materials,
            ISet<Volume> volumes,
            ref Shader commonShader,
            out string problem)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.Id) ||
                preset.Skybox == null || preset.Skybox.shader == null ||
                preset.Skybox.shader.name != "Skybox/Procedural" ||
                preset.PostProcessVolume == null ||
                !preset.PostProcessVolume.isGlobal ||
                preset.PostProcessVolume.sharedProfile == null ||
                !ids.Add(preset.Id) || !materials.Add(preset.Skybox) ||
                !volumes.Add(preset.PostProcessVolume) ||
                preset.FogDensity < 0f || preset.AmbientIntensity < 0f ||
                preset.ReflectionIntensity < 0f ||
                preset.ReflectionIntensity > 1f || preset.SunIntensity < 0f)
            {
                problem =
                    "Each environment preset needs a unique ID, procedural skybox, unique global Volume/profile, and bounded lighting values.";
                return false;
            }

            commonShader ??= preset.Skybox.shader;
            if (preset.Skybox.shader != commonShader)
            {
                problem =
                    "All campaign skyboxes must share one shader for gradual blending.";
                return false;
            }

            problem = string.Empty;
            return true;
        }

        private void ResolveCampaignState()
        {
            if (CampaignStateService.Instance != null)
            {
                campaignState = CampaignStateService.Instance;
            }
        }

        private void Bind()
        {
            if (subscribed)
                return;

            ResolveCampaignState();
            if (farmPrologueDirector != null)
            {
                farmPrologueDirector.PhaseChanged += HandleFarmPhaseChanged;
            }

            if (campaignState != null)
            {
                campaignState.ProgressLoaded += HandleProgressLoaded;
            }

            if (regionalRespawn != null)
            {
                regionalRespawn.ActiveRegionChanged += HandleActiveRegionChanged;
            }

            subscribed = true;
        }

        private void Unbind()
        {
            if (!subscribed)
                return;

            if (farmPrologueDirector != null)
            {
                farmPrologueDirector.PhaseChanged -= HandleFarmPhaseChanged;
            }

            if (campaignState != null)
            {
                campaignState.ProgressLoaded -= HandleProgressLoaded;
            }

            if (regionalRespawn != null)
            {
                regionalRespawn.ActiveRegionChanged -= HandleActiveRegionChanged;
            }

            subscribed = false;
        }

        private void HandleFarmPhaseChanged(FarmProloguePhase phase)
        {
            SetTargetPreset(
                phase == FarmProloguePhase.Hub
                    ? farmHubPreset
                    : farmProloguePreset,
                false);
        }

        private void HandleProgressLoaded(CampaignProgressSnapshot snapshot)
        {
            if (mode != CampaignEnvironmentMode.Farm)
                return;

            bool hub = snapshot.PrologueCompleted &&
                       (farmPrologueDirector == null ||
                        farmPrologueDirector.CurrentPhase == FarmProloguePhase.Hub);
            SetTargetPreset(hub ? farmHubPreset : farmProloguePreset, false);
        }

        private void HandleActiveRegionChanged(
            CampaignAreaId area,
            Transform unusedSocket)
        {
            if (mode != CampaignEnvironmentMode.OpenWorld ||
                ResolveAuthoritativePlayer() != null)
            {
                return;
            }

            SetOpenWorldArea(area, false);
        }

        private void ApplyInitialEnvironment()
        {
            if (!ValidateRuntimeContract(out string problem))
            {
                Debug.LogError(
                    "Campaign environment configuration is invalid: " + problem,
                    this);
                enabled = false;
                return;
            }

            if (mode == CampaignEnvironmentMode.Farm)
            {
                bool hub = campaignState != null &&
                           campaignState.HasCompletedPrologue;
                SetTargetPreset(hub ? farmHubPreset : farmProloguePreset, true);
                return;
            }

            if (!TryFindNearestArea(out CampaignAreaId nearest))
            {
                nearest = regionalRespawn != null
                    ? regionalRespawn.ActiveArea
                    : CampaignAreaId.BlackPines;
            }

            SetOpenWorldArea(nearest, true);
        }

        private void ReconcileOpenWorldEnvironment()
        {
            if (!TryFindNearestArea(out CampaignAreaId nearest))
                return;

            if (currentArea.HasValue && currentArea.Value != nearest &&
                TryGetBinding(currentArea.Value, out CampaignAreaEnvironmentBinding current) &&
                TryGetBinding(nearest, out CampaignAreaEnvironmentBinding candidate))
            {
                GameObject player = ResolveAuthoritativePlayer();
                if (player != null)
                {
                    Vector2 point = ToXZ(player.transform.position);
                    float currentDistance = Vector2.Distance(
                        point,
                        ToXZ(current.Anchor.position));
                    float candidateDistance = Vector2.Distance(
                        point,
                        ToXZ(candidate.Anchor.position));
                    if (currentDistance <= candidateDistance + areaSwitchHysteresis)
                        return;
                }
            }

            SetOpenWorldArea(nearest, false);
        }

        private bool TryFindNearestArea(out CampaignAreaId area)
        {
            GameObject player = ResolveAuthoritativePlayer();
            if (player == null || areaBindings == null || areaBindings.Length == 0)
            {
                area = default;
                return false;
            }

            Vector2 point = ToXZ(player.transform.position);
            float bestDistance = float.PositiveInfinity;
            area = areaBindings[0].Area;
            foreach (CampaignAreaEnvironmentBinding binding in areaBindings)
            {
                if (binding?.Anchor == null)
                    continue;

                float distance = (point - ToXZ(binding.Anchor.position)).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    area = binding.Area;
                }
            }

            return !float.IsPositiveInfinity(bestDistance);
        }

        private void SetOpenWorldArea(CampaignAreaId area, bool immediate)
        {
            if (!TryGetBinding(area, out CampaignAreaEnvironmentBinding binding))
                return;

            currentArea = area;
            SetTargetPreset(binding.Preset, immediate);
        }

        private bool TryGetBinding(
            CampaignAreaId area,
            out CampaignAreaEnvironmentBinding binding)
        {
            foreach (CampaignAreaEnvironmentBinding candidate in
                     areaBindings ?? Array.Empty<CampaignAreaEnvironmentBinding>())
            {
                if (candidate != null && candidate.Area == area)
                {
                    binding = candidate;
                    return true;
                }
            }

            binding = null;
            return false;
        }

        private static GameObject ResolveAuthoritativePlayer()
        {
            return global::gameManager.instance != null
                ? global::gameManager.instance.player
                : null;
        }

        private static Vector2 ToXZ(Vector3 point)
        {
            return new Vector2(point.x, point.z);
        }

        private void SetTargetPreset(
            CampaignEnvironmentPreset preset,
            bool immediate)
        {
            if (preset == null ||
                (!immediate && targetPreset == preset && transitionActive) ||
                (!immediate && currentPreset == preset && !transitionActive))
            {
                return;
            }

            EnsureRuntimeMaterials(preset.Skybox);
            targetPreset = preset;
            if (immediate || runtimeSkybox == null)
            {
                ApplyImmediateEnvironment(preset);
                return;
            }

            transitionStartSkybox.CopyPropertiesFromMaterial(runtimeSkybox);
            CaptureTransitionStart();
            transitionElapsed = 0f;
            transitionActive = true;
        }

        private void EnsureRuntimeMaterials(Material source)
        {
            if (source == null)
                return;

            if (runtimeSkybox == null)
            {
                runtimeSkybox = new Material(source)
                {
                    name = "Campaign Environment Runtime Skybox",
                    hideFlags = HideFlags.DontSave
                };
                RenderSettings.skybox = runtimeSkybox;
            }

            if (transitionStartSkybox == null)
            {
                transitionStartSkybox = new Material(source)
                {
                    name = "Campaign Environment Transition Start",
                    hideFlags = HideFlags.DontSave
                };
            }
        }

        private void BuildOwnedVolumeCache()
        {
            var volumes = new List<Volume>();
            if (mode == CampaignEnvironmentMode.Farm)
            {
                AddVolume(farmProloguePreset, volumes);
                AddVolume(farmHubPreset, volumes);
            }
            else
            {
                foreach (CampaignAreaEnvironmentBinding binding in
                         areaBindings ?? Array.Empty<CampaignAreaEnvironmentBinding>())
                {
                    AddVolume(binding?.Preset, volumes);
                }
            }

            ownedVolumes = volumes.ToArray();
            startVolumeWeights = new float[ownedVolumes.Length];
        }

        private static void AddVolume(
            CampaignEnvironmentPreset preset,
            ICollection<Volume> volumes)
        {
            if (preset?.PostProcessVolume != null &&
                !volumes.Contains(preset.PostProcessVolume))
            {
                volumes.Add(preset.PostProcessVolume);
            }
        }

        private void CaptureTransitionStart()
        {
            startFogColor = RenderSettings.fogColor;
            startFogDensity = RenderSettings.fogDensity;
            startAmbientSky = RenderSettings.ambientSkyColor;
            startAmbientEquator = RenderSettings.ambientEquatorColor;
            startAmbientGround = RenderSettings.ambientGroundColor;
            startAmbientIntensity = RenderSettings.ambientIntensity;
            startReflectionIntensity = RenderSettings.reflectionIntensity;
            startSunColor = directionalLight.color;
            startSunIntensity = directionalLight.intensity;
            startSunRotation = directionalLight.transform.rotation;
            if (startVolumeWeights.Length != ownedVolumes.Length)
            {
                startVolumeWeights = new float[ownedVolumes.Length];
            }

            for (int index = 0; index < ownedVolumes.Length; index++)
            {
                startVolumeWeights[index] = ownedVolumes[index] != null
                    ? ownedVolumes[index].weight
                    : 0f;
            }
        }

        private void ApplyImmediateEnvironment(CampaignEnvironmentPreset preset)
        {
            runtimeSkybox.CopyPropertiesFromMaterial(preset.Skybox);
            RenderSettings.skybox = runtimeSkybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = preset.FogColor;
            RenderSettings.fogDensity = preset.FogDensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = preset.AmbientSkyColor;
            RenderSettings.ambientEquatorColor = preset.AmbientEquatorColor;
            RenderSettings.ambientGroundColor = preset.AmbientGroundColor;
            RenderSettings.ambientIntensity = preset.AmbientIntensity;
            RenderSettings.reflectionIntensity = preset.ReflectionIntensity;
            RenderSettings.sun = directionalLight;
            directionalLight.color = preset.SunColor;
            directionalLight.intensity = preset.SunIntensity;
            directionalLight.transform.rotation =
                Quaternion.Euler(preset.SunEulerAngles);
            ApplyVolumeWeights(preset, 1f);
            transitionActive = false;
            transitionElapsed = transitionSeconds;
            currentPreset = preset;
            targetPreset = preset;
            DynamicGI.UpdateEnvironment();
        }

        private void ApplyBlendedEnvironment(
            CampaignEnvironmentPreset preset,
            float blend)
        {
            runtimeSkybox.Lerp(transitionStartSkybox, preset.Skybox, blend);
            RenderSettings.skybox = runtimeSkybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(startFogColor, preset.FogColor, blend);
            RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, preset.FogDensity, blend);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(
                startAmbientSky,
                preset.AmbientSkyColor,
                blend);
            RenderSettings.ambientEquatorColor = Color.Lerp(
                startAmbientEquator,
                preset.AmbientEquatorColor,
                blend);
            RenderSettings.ambientGroundColor = Color.Lerp(
                startAmbientGround,
                preset.AmbientGroundColor,
                blend);
            RenderSettings.ambientIntensity = Mathf.Lerp(
                startAmbientIntensity,
                preset.AmbientIntensity,
                blend);
            RenderSettings.reflectionIntensity = Mathf.Lerp(
                startReflectionIntensity,
                preset.ReflectionIntensity,
                blend);
            directionalLight.color = Color.Lerp(startSunColor, preset.SunColor, blend);
            directionalLight.intensity = Mathf.Lerp(
                startSunIntensity,
                preset.SunIntensity,
                blend);
            directionalLight.transform.rotation = Quaternion.Slerp(
                startSunRotation,
                Quaternion.Euler(preset.SunEulerAngles),
                blend);
            ApplyVolumeWeights(preset, blend);
        }

        private void ApplyVolumeWeights(
            CampaignEnvironmentPreset preset,
            float blend)
        {
            Volume targetVolume = preset.PostProcessVolume;
            for (int index = 0; index < ownedVolumes.Length; index++)
            {
                Volume volume = ownedVolumes[index];
                if (volume == null)
                    continue;

                float targetWeight = volume == targetVolume ? 1f : 0f;
                volume.weight = transitionActive
                    ? Mathf.Lerp(startVolumeWeights[index], targetWeight, blend)
                    : targetWeight;
            }
        }

        private static void DestroyRuntimeMaterial(Material material)
        {
            if (material == null)
                return;

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }
    }
}
