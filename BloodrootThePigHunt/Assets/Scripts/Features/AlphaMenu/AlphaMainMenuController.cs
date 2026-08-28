using System;
using Bloodroot.Campaign;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bloodroot.Features.AlphaMenu
{
    public enum AlphaMenuScreenId
    {
        Main = 0,
        Options = 1,
        Credits = 2
    }

    [Serializable]
    public sealed class AlphaMenuScreenBinding
    {
        [SerializeField] private AlphaMenuScreenId screenId;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Selectable firstSelection;

        public AlphaMenuScreenId ScreenId => screenId;

        public CanvasGroup CanvasGroup => canvasGroup;

        public Selectable FirstSelection => firstSelection;

        public void Configure(
            AlphaMenuScreenId id,
            CanvasGroup group,
            Selectable initialSelection)
        {
            screenId = id;
            canvasGroup = group;
            firstSelection = initialSelection;
        }
    }

    /// <summary>
    /// Runtime contract for the authored alpha main-menu scene. It controls
    /// existing CanvasGroups, Selectables, art slots, and services only; it
    /// never creates a GameObject, component, or UI element at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AlphaMainMenuController : MonoBehaviour
    {
        [Header("Authored Services")]
        [SerializeField] private CampaignStateService campaignState;
        [SerializeField] private AlphaMenuAudio menuAudio;
        [SerializeField] private EventSystem menuEventSystem;
        [SerializeField] private AlphaMenuTransition menuTransition;
        [SerializeField] public GameObject exitMenu;

        [Header("Authored Screens")]
        [SerializeField] private AlphaMenuScreenBinding[] screenBindings;
        [SerializeField] private Button continueButton;

        [Header("Replaceable Placeholder Art Slots")]
        [SerializeField] private Image backgroundArtSlot;
        [SerializeField] private Image logoArtSlot;

        [Header("Scene Contract")]
        [SerializeField] private string newGameSceneName =
            CampaignSceneNames.FarmPrologueHub;
        [SerializeField] private string continueFallbackSceneName =
            CampaignSceneNames.FarmPrologueHub;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float sceneTransitionDelay = 0.15f;

        private AlphaMenuScreenBinding currentBinding;
        private bool actionInProgress;
        private bool navigationSettingWasCaptured;
        private bool authoredSendNavigationEvents;

        public AlphaMenuScreenId CurrentScreen => currentBinding != null
            ? currentBinding.ScreenId
            : AlphaMenuScreenId.Main;

        public Image BackgroundArtSlot => backgroundArtSlot;

        public Image LogoArtSlot => logoArtSlot;

        public AlphaMenuTransition MenuTransition => menuTransition;

        private void Awake()
        {
            // A menu reached from paused gameplay must always run in realtime.
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            ResolveAuthoredServices();
        }

        private void OnEnable()
        {
            ResolveAuthoredServices();
            CaptureAndDisableBuiltInNavigation();
        }

        private void Start()
        {
#if UNITY_WEBGL
            disableExit(true);
#else
            // Do Nothing
#endif

            if (!ValidateAuthoredContract(out string error))
            {

                enabled = false;
                return;
            }

            ShowScreenImmediate(AlphaMenuScreenId.Main);
            RefreshContinueAvailability();
        }

        private void Update()
        {
            if (actionInProgress ||
                (menuTransition != null && menuTransition.IsTransitioning))
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetKeyDown(KeyCode.Backspace))
            {
                HandleCancelInput();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.W))
            {
                MoveSelection(NavigationDirection.Up);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) ||
                Input.GetKeyDown(KeyCode.S))
            {
                MoveSelection(NavigationDirection.Down);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) ||
                Input.GetKeyDown(KeyCode.A))
            {
                MoveSelection(NavigationDirection.Left);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) ||
                Input.GetKeyDown(KeyCode.D))
            {
                MoveSelection(NavigationDirection.Right);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space))
            {
                SubmitCurrentSelection();
            }
        }

        private void OnDisable()
        {
            RestoreBuiltInNavigation();
        }

        /// <summary>
        /// Editor-authoring hook used to serialize all required scene
        /// references. No runtime object is created by this method.
        /// </summary>
        public void Configure(
            CampaignStateService state,
            AlphaMenuAudio audio,
            EventSystem eventSystem,
            AlphaMenuTransition authoredTransition,
            Button authoredContinueButton,
            AlphaMenuScreenBinding[] authoredScreens,
            Image authoredBackgroundSlot,
            Image authoredLogoSlot,
            string authoredNewGameScene,
            string authoredContinueScene)
        {
            campaignState = state;
            menuAudio = audio;
            menuEventSystem = eventSystem;
            menuTransition = authoredTransition;
            continueButton = authoredContinueButton;
            screenBindings = authoredScreens;
            backgroundArtSlot = authoredBackgroundSlot;
            logoArtSlot = authoredLogoSlot;
            newGameSceneName = authoredNewGameScene;
            continueFallbackSceneName = authoredContinueScene;
        }

        public void StartNewGame()
        {
            if (!CanBeginAction())
            {
                return;
            }

            CampaignStateService state = ResolveCampaignState();
            if (state == null)
            {
                FailAction(
                    "New Game requires the authored CampaignStateService.");
                return;
            }

            if (!IsSceneLoadable(newGameSceneName))
            {
                FailAction(
                    $"Scene '{newGameSceneName}' is not enabled in Build " +
                    "Settings. The existing campaign save was not changed.");
                return;
            }

            if (!CampaignSafetySaveIntegration.TryResetForNewGame(
                    state.StartNewGame,
                    out string resetError))
            {
                FailAction(
                    "New Game could not atomically reset the campaign and " +
                    $"Safety saves: {resetError}");
                return;
            }
            PersistOutsideWorld.instance.SaveData(false);
            BeginSceneLoad(newGameSceneName);
        }

        public void ContinueGame()
        {
            if (!CanBeginAction())
            {
                return;
            }

            CampaignStateService state = ResolveCampaignState();
            if (state == null)
            {
                FailAction(
                    "Continue requires the authored CampaignStateService.");
                return;
            }

            state.LoadFromDisk();
            if (!HasContinueProgress(state.Current))
            {
                RefreshContinueAvailability();
                FailAction("No resumable campaign progress was found.");
                return;
            }
            PersistOutsideWorld.instance.SaveData(false);
            string targetScene = GetContinueTarget(state.Current);
            BeginSceneLoad(targetScene);
        }

        public void ShowOptions()
        {
            RequestScreen(AlphaMenuScreenId.Options);
        }

        public void ShowCredits()
        {
            RequestScreen(AlphaMenuScreenId.Credits);
        }

        public void BackToMain()
        {
            RequestScreen(AlphaMenuScreenId.Main);
        }

        public void ExitGame()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return;
#else
            if (!CanBeginAction())
            {
                return;
            }

            actionInProgress = true;
            SetCurrentScreenInteractable(false);
            float minimumDelay = Mathf.Max(
                sceneTransitionDelay,
                menuAudio != null ? menuAudio.ConfirmClipDuration : 0f);
            if (!menuTransition.TryRunTerminalAction(
                    minimumDelay,
                    ExitApplication,
                    FailAction))
            {
                actionInProgress = false;
            }
#endif
        }

        public void RefreshContinueAvailability()
        {
            CampaignStateService state = ResolveCampaignState();
            bool canContinue = state != null &&
                               HasContinueProgress(state.Current);

            if (continueButton != null)
            {
                continueButton.interactable = canContinue;
            }
        }

        public bool ValidateAuthoredContract(out string error)
        {
            if (ResolveCampaignState() == null)
            {
                error = "CampaignStateService is not assigned.";
                return false;
            }

            if (menuAudio == null)
            {
                error = "AlphaMenuAudio is not assigned.";
                return false;
            }

            if (!menuAudio.ValidateAuthoredContract(out error))
            {
                return false;
            }

            if (menuEventSystem == null)
            {
                error = "EventSystem is not assigned.";
                return false;
            }

            if (menuTransition == null ||
                !menuTransition.ValidateAuthoredContract(out error))
            {
                return false;
            }

            if (continueButton == null)
            {
                error = "Continue button is not assigned.";
                return false;
            }

            if (backgroundArtSlot == null || logoArtSlot == null)
            {
                error = "Both replaceable placeholder art slots are required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(newGameSceneName) ||
                string.IsNullOrWhiteSpace(continueFallbackSceneName))
            {
                error = "New Game and Continue scene names are required.";
                return false;
            }

            if (screenBindings == null || screenBindings.Length != 3)
            {
                error =
                    "Exactly Main, Options, and Credits screens are required.";
                return false;
            }

            for (int i = 0; i < screenBindings.Length; i++)
            {
                AlphaMenuScreenBinding binding = screenBindings[i];
                if (binding == null ||
                    binding.CanvasGroup == null ||
                    binding.FirstSelection == null)
                {
                    error = $"Screen binding {i} is incomplete.";
                    return false;
                }

                if (!binding.FirstSelection.transform.IsChildOf(
                        binding.CanvasGroup.transform))
                {
                    error =
                        $"The first selection for {binding.ScreenId} must be " +
                        "inside that authored screen.";
                    return false;
                }

                for (int j = i + 1; j < screenBindings.Length; j++)
                {
                    if (screenBindings[j] != null &&
                        binding.ScreenId == screenBindings[j].ScreenId)
                    {
                        error =
                            $"Screen {binding.ScreenId} is assigned twice.";
                        return false;
                    }
                }
            }

            if (GetBinding(AlphaMenuScreenId.Main) == null ||
                GetBinding(AlphaMenuScreenId.Options) == null ||
                GetBinding(AlphaMenuScreenId.Credits) == null)
            {
                error = "Main, Options, and Credits must each be assigned.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void HandleCancelInput()
        {
            if (currentBinding == null ||
                currentBinding.ScreenId == AlphaMenuScreenId.Main)
            {
                return;
            }

            if (menuAudio != null)
            {
                menuAudio.PlayCancel();
            }

            RequestScreen(AlphaMenuScreenId.Main);
        }

        private void RequestScreen(AlphaMenuScreenId screenId)
        {
            if (!CanBeginAction())
            {
                return;
            }

            AlphaMenuScreenBinding target = GetBinding(screenId);
            if (target == null || target == currentBinding)
            {
                return;
            }

            actionInProgress = true;
            SetCurrentScreenInteractable(false);
            if (!menuTransition.TrySwapScreen(
                    () => ShowScreenImmediate(target.ScreenId),
                    () => actionInProgress = false,
                    FailAction))
            {
                actionInProgress = false;
            }
        }

        private void ShowScreenImmediate(AlphaMenuScreenId screenId)
        {
            AlphaMenuScreenBinding target = GetBinding(screenId);
            if (target == null)
            {
                return;
            }

            for (int i = 0; i < screenBindings.Length; i++)
            {
                AlphaMenuScreenBinding binding = screenBindings[i];
                if (binding == null || binding.CanvasGroup == null)
                {
                    continue;
                }

                bool isTarget = binding == target;
                CanvasGroup group = binding.CanvasGroup;
                group.gameObject.SetActive(isTarget);
                group.alpha = isTarget ? 1f : 0f;
                group.interactable = isTarget;
                group.blocksRaycasts = isTarget;
            }

            currentBinding = target;
            SelectInitialControl(target);
        }

        private void MoveSelection(NavigationDirection direction)
        {
            if (menuEventSystem == null || currentBinding == null)
            {
                return;
            }

            GameObject selectedObject =
                menuEventSystem.currentSelectedGameObject;
            Selectable selected = selectedObject != null
                ? selectedObject.GetComponent<Selectable>()
                : null;

            if (selected == null ||
                !selected.IsActive() ||
                !selected.IsInteractable())
            {
                SelectInitialControl(currentBinding);
                return;
            }

            Selectable next = FindNextInteractable(selected, direction);

            if (next != null && next.IsActive() && next.IsInteractable())
            {
                menuEventSystem.SetSelectedGameObject(next.gameObject);
            }
        }

        private void SubmitCurrentSelection()
        {
            if (menuEventSystem == null)
            {
                return;
            }

            GameObject selected = menuEventSystem.currentSelectedGameObject;
            if (selected == null)
            {
                SelectInitialControl(currentBinding);
                selected = menuEventSystem.currentSelectedGameObject;
            }

            if (selected != null)
            {
                ExecuteEvents.Execute(
                    selected,
                    new BaseEventData(menuEventSystem),
                    ExecuteEvents.submitHandler);
            }
        }

        private void SelectInitialControl(AlphaMenuScreenBinding binding)
        {
            if (menuEventSystem == null ||
                binding == null ||
                binding.FirstSelection == null)
            {
                return;
            }

            menuEventSystem.SetSelectedGameObject(null);
            menuEventSystem.SetSelectedGameObject(
                binding.FirstSelection.gameObject);
        }

        private void BeginSceneLoad(string sceneName)
        {
            if (!IsSceneLoadable(sceneName))
            {
                FailAction(
                    $"Scene '{sceneName}' is not enabled in Build Settings.");
                return;
            }

            actionInProgress = true;
            SetCurrentScreenInteractable(false);
            if (!menuTransition.TryLoadScene(sceneName, FailAction))
            {
                actionInProgress = false;
            }
        }

        private static void ExitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private string GetContinueTarget(CampaignProgressSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.PendingSceneName) &&
                CampaignSafetySaveIntegration
                    .TryValidatePendingArrivalContext(
                        snapshot.PendingSceneName) &&
                Application.CanStreamedLevelBeLoaded(
                    snapshot.PendingSceneName))
            {
                return snapshot.PendingSceneName;
            }

            if (CampaignSafetySaveIntegration.TryGetContinueScene(
                    out string savedSceneName) &&
                Application.CanStreamedLevelBeLoaded(savedSceneName))
            {
                return savedSceneName;
            }

            return GetCampaignOnlyContinueTarget(
                snapshot,
                continueFallbackSceneName);
        }

        private static string GetCampaignOnlyContinueTarget(
            CampaignProgressSnapshot snapshot,
            string configuredFallback)
        {
            // A campaign-first finale transition can survive a process exit
            // before the paired Safety context is written. Preserve the
            // truck as the only return authority: every crossed/unburned
            // Hollow state resumes OpenWorld, including recovered cargo.
            if (snapshot.HeartrootBurned || snapshot.CampaignCompleted)
                return CampaignSceneNames.FarmPrologueHub;

            if (snapshot.HollowVeilCrossed)
                return CampaignSceneNames.OpenWorld;

            return configuredFallback;
        }

        private static bool HasContinueProgress(
            CampaignProgressSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.PendingSceneName))
            {
                return CampaignSafetySaveIntegration
                    .TryValidatePendingArrivalContext(
                        snapshot.PendingSceneName);
            }

            return snapshot.PrologueCompleted ||
                   snapshot.PrologueCursedObjectRevealed ||
                   snapshot.PrologueCursedObjectOffered ||
                   !string.IsNullOrWhiteSpace(snapshot.PendingRootOfferingId) ||
                   snapshot.HasUnresolvedFarmEmergence ||
                   snapshot.HollowVeilCrossed ||
                   snapshot.HeartrootBurned ||
                   snapshot.CampaignCompleted ||
                   CampaignSafetySaveIntegration.TryGetContinueScene(out _);
        }

        private static bool IsSceneLoadable(string sceneName)
        {
            return !string.IsNullOrWhiteSpace(sceneName) &&
                   Application.CanStreamedLevelBeLoaded(sceneName);
        }

        private static Selectable FindNextInteractable(
            Selectable origin,
            NavigationDirection direction)
        {
            Selectable candidate = FindNeighbour(origin, direction);
            int remainingSearches = 32;

            while (candidate != null &&
                   candidate != origin &&
                   remainingSearches-- > 0)
            {
                if (candidate.IsActive() && candidate.IsInteractable())
                {
                    return candidate;
                }

                candidate = FindNeighbour(candidate, direction);
            }

            return null;
        }

        private static Selectable FindNeighbour(
            Selectable selectable,
            NavigationDirection direction)
        {
            return direction switch
            {
                NavigationDirection.Up => selectable.FindSelectableOnUp(),
                NavigationDirection.Down => selectable.FindSelectableOnDown(),
                NavigationDirection.Left => selectable.FindSelectableOnLeft(),
                NavigationDirection.Right => selectable.FindSelectableOnRight(),
                _ => null
            };
        }

        private bool CanBeginAction()
        {
            return enabled &&
                   !actionInProgress &&
                   menuTransition != null &&
                   !menuTransition.IsTransitioning;
        }

        private void FailAction(string message)
        {
            actionInProgress = false;
            SetCurrentScreenInteractable(true);

            if (menuAudio != null)
            {
                menuAudio.PlayCancel();
            }


        }

        private void SetCurrentScreenInteractable(bool interactable)
        {
            if (currentBinding == null || currentBinding.CanvasGroup == null)
            {
                return;
            }

            currentBinding.CanvasGroup.interactable = interactable;
            currentBinding.CanvasGroup.blocksRaycasts = interactable;
        }

        private AlphaMenuScreenBinding GetBinding(AlphaMenuScreenId screenId)
        {
            if (screenBindings == null)
            {
                return null;
            }

            for (int i = 0; i < screenBindings.Length; i++)
            {
                AlphaMenuScreenBinding binding = screenBindings[i];
                if (binding != null && binding.ScreenId == screenId)
                {
                    return binding;
                }
            }

            return null;
        }

        private void ResolveAuthoredServices()
        {
            CampaignStateService activeCampaignState =
                CampaignStateService.Instance;
            if (activeCampaignState != null)
            {
                campaignState = activeCampaignState;
            }

            AlphaMenuAudio activeMenuAudio = AlphaMenuAudio.Instance;
            if (activeMenuAudio != null)
            {
                menuAudio = activeMenuAudio;
            }

            if (menuEventSystem == null)
            {
                menuEventSystem = EventSystem.current;
            }
        }

        private CampaignStateService ResolveCampaignState()
        {
            CampaignStateService activeCampaignState =
                CampaignStateService.Instance;
            if (activeCampaignState != null)
            {
                campaignState = activeCampaignState;
            }

            return campaignState;
        }

        private void CaptureAndDisableBuiltInNavigation()
        {
            if (menuEventSystem == null || navigationSettingWasCaptured)
            {
                return;
            }

            authoredSendNavigationEvents =
                menuEventSystem.sendNavigationEvents;
            navigationSettingWasCaptured = true;

            // Pointer processing remains active. Keyboard navigation is owned
            // here so arrows/Enter are deterministic and never double-submit.
            menuEventSystem.sendNavigationEvents = false;
        }

        private void RestoreBuiltInNavigation()
        {
            if (!navigationSettingWasCaptured || menuEventSystem == null)
            {
                return;
            }

            menuEventSystem.sendNavigationEvents =
                authoredSendNavigationEvents;
            navigationSettingWasCaptured = false;
        }

        private enum NavigationDirection
        {
            Up,
            Down,
            Left,
            Right
        }

        private void disableExit(bool isOn = true) {
            if (isOn) {
                exitMenu.SetActive(false);
            } else {
                exitMenu.SetActive(true);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            sceneTransitionDelay = Mathf.Max(0f, sceneTransitionDelay);
        }
#endif
    }
}
