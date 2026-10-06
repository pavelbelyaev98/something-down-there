using System;
using UnityEngine;

namespace SomethingDownThere
{
    [Serializable]
    public sealed class FpsTuning
    {
        [Min(0f)] public float WalkSpeed = 4f;
        [Range(1f, 1.5f)] public float SprintSpeedMultiplier = 1.35f;
        [Range(0.05f, 1f)] public float CrouchSpeedMultiplier = 0.35f;
        [Min(0.6f)] public float CrouchHeight = 1.1f;
        [Min(0.3f)] public float CrouchEyeHeight = 0.95f;
        [Min(0.01f)] public float CrouchTransitionSeconds = 0.2f;
        [Min(0f)] public float LookSensitivity = 0.12f;
        [Range(1f, 89f)] public float PitchLimit = 85f;
        public float Gravity = -20f;
        [Min(0f)] public float JumpHeight = 1.15f;
        [Min(0f)] public float JetpackHoldDelay = 0.22f;
        [Min(0.01f)] public float BatteryCapacity = 100f;
        [Min(0f)] public float DigEnergy = 1f;
        [Min(0.01f)] public float DigInterval = 0.35f;
        // A shovel stroke into the ground cuts at its scoop, a moment after the press (off: on the press, as the drill).
        public bool CutAtScoop = true;
        [Min(0.01f)] public float DigReach = 3f;
        [Min(0.01f)] public float InteractReach = 3f;
        [Min(0.01f)] public float LooseFindReach = 6f;
        [Min(1)] public int InventorySlots = 10;
    }

    public enum PlayerMenu { None, Pause, Inventory, Station, DeveloperAdmin, ConfirmTerrainReset, Persistence, CameraComfort, MainMenu, ConfirmNewGame, InputSettings, DeviceSettings }

    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
    public sealed class FpsPlayer : MonoBehaviour
    {
        [SerializeField] private FpsTuning tuning = new FpsTuning();
        [SerializeField] private Camera viewCamera;
        [Tooltip("Include all world blockers, not just interactive objects. Player colliders must be excluded.")]
        [SerializeField] private LayerMask worldMask = Physics.DefaultRaycastLayers;
        // Authored once in EquipmentProgression.ToolProfiles(). Never serialize the ladder onto this
        // component: the scene copy silently won every code edit until 048's follow-up.
        private readonly ShovelProfile[] shovelLevels = EquipmentProgression.ToolProfiles();
        [UnityEngine.Serialization.FormerlySerializedAs("practiceTerrain")]
        [SerializeField] private TerrainVolume excavationTerrain;
        [SerializeField] private Transform surfaceReturn;
        [SerializeField] private DiscoveryField discoveries;
        [SerializeField] private SurfaceRecharge surfaceRecharge;
        [SerializeField] private ReturnWarning returnWarning = new ReturnWarning();
        [SerializeField, Min(0)] private int maximumRescueFee = 10;

        private CharacterController motor;
        private FpsInput input;
        private PlayerCrouch crouch;
        public FindDetector Detector { get; private set; }
        [SerializeField] private SalvageCrane crane;
        [SerializeField] private WorksiteTools worksiteTools;
        public WorksiteTools WorksiteTools => worksiteTools;
        private HoldInteraction holdInteraction;
        public SalvageCrane Crane => crane;
        // How far the current hold of Interact has come (0 when none): marking a unique for the crane, forcing a chest's lock.
        public float HoldProgress => holdInteraction?.Progress ?? 0;
        // The unique the hold would mark for the crane now, for the mark preview.
        internal bool TryGetRecoveryMark(out BuriedFind find, out RaycastHit hit)
        {
            find = null; hit = default;
            if (holdInteraction == null || !holdInteraction.TryGetTarget(out var held, out hit)) return false;
            find = held as BuriedFind;
            return find != null;
        }
        private FindProximityCollection proximityCollection;
        private LoadRide loadRide;
        private FindPickupPresentation pickupPresentation;
        private float pitch, verticalSpeed, digCooldown, savedTimeScale, jetpackHoldTime;
        private float scheduledDigInterval;
        private CursorLockMode savedCursorLock;
        private bool savedCursorVisible, ownsPresentation, focused = true;
        private int transitionFrame = -1;
        private float feedbackUntil;
        private float rescueRetryDelay;
        private BuriedFind blockedPickup;
        private readonly RaycastHit[] fullBagDigHits = new RaycastHit[128];
        private int adminLevel;
        private ShovelProfile[] adminTuning;
        private bool unlimitedBattery;
        private bool adminXray;
        // Detector-off playtest (099): session admin switch, or -noDetector for the whole run.
        private bool adminDetectorOff;
        private bool adminHoverOnRelease;
        private bool adminGroundXray;
        // Break-in A/B (109): breaking into a chest's pocket caves part of it in (default) or only drops crumbs and dust.
        private bool adminBreakInDust;
        // Drill look comparison (playtest 001): the step picked on each DrillDial.
        private readonly int[] adminDrillDials = new int[DrillDialSteps.Length];
        // Ground contact shading (SSAO) strength set from the admin slider for the session.
        private bool adminContactShading;
        // A shovel stroke starts on the press and its cut waits for the scoop (pendingScoop counts down to it; below
        // zero nothing is pending), lifting the dirt wherever the player looks when it lands.
        private float pendingScoop = -1f;
        private static readonly bool DetectorOffAtLaunch = Array.IndexOf(Environment.GetCommandLineArgs(), "-noDetector") >= 0;
        public bool DetectorShown => !DetectorOffAtLaunch && !(AdminAvailable && adminDetectorOff);
        private bool jetpackReadyInAir;

        public FpsTuning Tuning => tuning;
        public Camera ViewCamera => viewCamera;
        public CameraPreferences CameraSettings { get; private set; }
        public InputPreferences InputSettings { get; private set; }
        public GamePreferences GameSettings { get; private set; }
        public GraphicsAutoTuner GraphicsTuner { get; } = new GraphicsAutoTuner();
        // A new device measures itself once at the title; interrupted runs retry there.
        private bool pendingGraphicsTuning;
        public SettingsCategory SettingsCategory { get; private set; }
        public bool IsSettingsOpen => Menu == PlayerMenu.DeviceSettings || Menu == PlayerMenu.CameraComfort || Menu == PlayerMenu.InputSettings;
        public InputBindingCapture BindingCapture { get; private set; }
        public Battery Battery { get; private set; }
        public SessionInventory Inventory { get; private set; }
        public SessionWallet Wallet { get; private set; }
        public StationTrade Trade { get; private set; }
        public long StationRevision { get; private set; }
        public string StationNotice { get; private set; } = "";
        public RescueController Rescue { get; private set; }
        public bool RescueAvailable => Rescue != null && surfaceReturn != null && excavationTerrain != null;
        public PlayerMenu Menu { get; private set; }
        public StationTarget Station { get; private set; }
        public bool IsMenuOpen => Menu != PlayerMenu.None;
        public bool GameplayActive => isActiveAndEnabled && focused && !IsMenuOpen && (Persistence == null || !Persistence.BlocksPlay);
        internal bool HasGameplayFocus => focused;
        public WorldSaveController Persistence { get; internal set; }
        public TerrainVolume ExcavationTerrain => excavationTerrain;
        public SurfaceRecharge SurfaceRecharge => surfaceRecharge;
        public ReturnWarning ReturnWarning => returnWarning;
        public Vector3 FeetPosition => motor == null ? transform.position
            : transform.TransformPoint(motor.center - Vector3.up * (motor.height * 0.5f));
        public string TargetPrompt { get; private set; } = "";
        public string Feedback { get; private set; } = "";
        public float Pitch => pitch;
        public float VerticalSpeed => verticalSpeed;
        public float CrouchAmount => crouch?.Amount ?? 0f;
        public bool StandBlocked => crouch != null && crouch.StandBlocked;
        public bool IsJetpackActive { get; private set; }
        // Hover hold (jetpack level 2+): powered, but holding height rather than climbing.
        public bool IsHovering { get; private set; }
        public ShovelState Shovel { get; private set; }
        public JetpackState Jetpack { get; private set; }
        public LampKit LampKit { get; private set; }
        public ChargeKit Charges { get; private set; }
        // Unity 6.6 uses managed code variants; DEVELOPMENT_BUILD is deprecated.
        // This engine-owned build flag is true in the Editor/development players.
        public static bool AdminBuild => Debug.isDebugBuild;
        public bool ExcavationAvailable => excavationTerrain != null;
        public bool AdminAvailable => AdminBuild && ExcavationAvailable && surfaceReturn != null;
        public bool HasAdminOverrides => AdminAvailable && (adminLevel > 0 || unlimitedBattery || adminXray || adminDetectorOff
            || adminHoverOnRelease || adminGroundXray || adminContactShading || adminBreakInDust);
        // Hover A/B (022): hold height while digging (default) or whenever Space is released.
        public bool HoverOnRelease => AdminAvailable && adminHoverOnRelease;
        public string AdminHoverLabel => HoverOnRelease ? "on release" : "while digging";
        public bool BreakInCollapses => !(AdminAvailable && adminBreakInDust);
        public string AdminBreakInLabel => BreakInCollapses ? "collapse" : "dust only";
        // The first-person drill's look (ToolRigPresenter), dialled per session in developer admin; each dial's first
        // step is the default. Size scales the drill (1 = the purchased model); Position moves it along the tool (metres,
        // + away from the eye).
        public enum DrillDial { Size, Position }
        private static readonly float[][] DrillDialSteps =
        {
            new[] { 1f, 1.05f, 1.1f, 1.15f, 1.2f, 1.3f, 1.4f, 1.5f, 1.75f, 2f, .9f, .95f },
            new[] { -.06f, -.07f, -.08f, -.09f, -.1f, -.11f, -.12f, -.05f, -.04f, -.02f, 0f },
        };
        public float DrillLook(DrillDial dial) => DrillDialSteps[(int)dial][AdminAvailable ? adminDrillDials[(int)dial] : 0];
        public string AdminDrillLabel(DrillDial dial)
        {
            float value = DrillLook(dial);
            return dial == DrillDial.Position ? (value * 100f).ToString("+0;-0;0") + " cm" : $"{value * 100f:0}%";
        }
        public bool ShavingEnabled => ExcavationAvailable && EquipmentProgression.UsesDrill(EffectiveShovelLevel);
        public DiscoveryField Discoveries => discoveries;
        public bool AdminXray => AdminAvailable && adminXray && discoveries != null && discoveries.isActiveAndEnabled;
        public bool AdminGroundXray => AdminAvailable && adminGroundXray && excavationTerrain.GroundXrayEnabled;
        public bool UnlimitedBattery => AdminAvailable && unlimitedBattery;
        public int EffectiveShovelLevel => AdminAvailable && adminLevel > 0 ? adminLevel : Shovel.Level;
        // Developer calibration: a session-only ladder copy the dev menu edits live.
        // Null means the authored ladder in EquipmentProgression.ToolProfiles() is in charge.
        public bool HasAdminTuning => AdminAvailable && adminTuning != null;
        public ShovelProfile ProfileAt(int level) => HasAdminTuning
            ? adminTuning[Mathf.Clamp(level, 1, Shovel.LevelCount) - 1] : Shovel.GetProfile(level);
        public ShovelProfile EffectiveShovel => ProfileAt(EffectiveShovelLevel);
        public const float MaximumDigReach = 8f;
        public float DigReachAtLevel(int level) => Mathf.Min(MaximumDigReach, tuning.DigReach + ProfileAt(level).ReachBonus);
        public float EffectiveDigReach => DigReachAtLevel(EffectiveShovelLevel);
        public float MaximumPickupReach => Mathf.Max(tuning.InteractReach, tuning.LooseFindReach);
        public float PickupReach(BuriedFind find) => find != null && find.IsReleased ? MaximumPickupReach : tuning.InteractReach;
        public float ScoopDigInterval => Mathf.Max(0.01f, tuning.DigInterval * EffectiveShovel.CadenceMultiplier);
        public float DigIntervalAtLevel(int level) => Mathf.Max(0.01f, tuning.DigInterval * ProfileAt(level).CadenceMultiplier
            * (EquipmentProgression.UsesDrill(level) ? EquipmentProgression.ShavingIntervalScale : 1f));
        public float EffectiveDigInterval => Mathf.Max(.01f, ScoopDigInterval
            * (ShavingEnabled ? EquipmentProgression.ShavingIntervalScale : 1f));
        public float EffectiveDigEnergy => Mathf.Max(0f, tuning.DigEnergy) * EffectiveDigInterval / ScoopDigInterval;
        public TerrainMaterialId LastDigMaterial { get; private set; }
        public float LastDigInterval { get; private set; }
        public float DigPulse { get; private set; }
        public int SuccessfulStrokes { get; private set; }
        // Tool strokes as they start (the tool rig animates each): on the press for the shovel, with every cut for the drill.
        public int StrokesStarted { get; private set; }
        public float LastScoopVolume { get; private set; }
        public float ExcavatedVolume => excavationTerrain != null ? excavationTerrain.RemovedVolume : 0;
        public float Depth => excavationTerrain == null ? 0 : Mathf.Max(0, excavationTerrain.SurfaceHeight - transform.position.y);
        // Standing on the site floor reads as its full depth (150 m, not 149.9).
        public float DisplayDepth
        {
            get
            {
                if (excavationTerrain == null) return 0;
                float floor = excavationTerrain.Dimensions.y * excavationTerrain.CellSize;
                return Depth >= floor - .3f ? floor : Depth;
            }
        }
        public event Action MenuChanged;

        private void Awake()
        {
            motor = GetComponent<CharacterController>();
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>(true);
            if (viewCamera == null)
            {
                Debug.LogError("FPS player requires a child camera.", this);
                enabled = false;
                return;
            }
            Battery = new Battery(Mathf.Max(0.01f, tuning.BatteryCapacity));
            Inventory = new SessionInventory(Mathf.Max(1, tuning.InventorySlots));
            Wallet = new SessionWallet();
            Rescue = new RescueController(Inventory, Wallet, Mathf.Max(0, maximumRescueFee));
            Shovel = new ShovelState(shovelLevels);
            Jetpack = new JetpackState();
            LampKit = new LampKit();
            Charges = new ChargeKit();
            Trade = new StationTrade(Inventory, Wallet, Shovel, Battery, Jetpack, LampKit, Charges);
            pitch = Mathf.DeltaAngle(0f, viewCamera.transform.localEulerAngles.x);
            if (InputSettings == null)
                ConfigureInputPreferences(new DevicePreferencesFile(System.IO.Path.Combine(Application.persistentDataPath,
                    Application.isEditor ? "EditorPreferences" : "Preferences", "input-v1.ini")));
            input = new FpsInput(InputSettings);
            Detector = new FindDetector(this);
            holdInteraction = new HoldInteraction(this);
            proximityCollection = new FindProximityCollection(this, motor, worldMask);
            loadRide = new LoadRide(motor, worldMask);
            pickupPresentation = new FindPickupPresentation(transform, viewCamera);
            crouch = new PlayerCrouch(motor, viewCamera, tuning, worldMask);
            if (CameraSettings == null)
                ConfigureCameraPreferences(new CameraPreferencesFile(System.IO.Path.Combine(Application.persistentDataPath,
                    Application.isEditor ? "EditorPreferences" : "Preferences", "camera-v1.ini")));
            else ApplyCameraPreferences();
            if (GameSettings == null)
                ConfigureGamePreferences(new DevicePreferencesFile(System.IO.Path.Combine(Application.persistentDataPath,
                    Application.isEditor ? "EditorPreferences" : "Preferences", "game-v1.json")),
                    new UnityGameSettingsPlatform(!Application.isEditor || gameObject.scene.name == "MainGame", viewCamera));
            pendingGraphicsTuning = !Application.isEditor && !GameSettings.Values.GraphicsTuned;
            GraphicsTuner.Changed += () => MenuChanged?.Invoke();
        }

        // Settings button: measure this PC again from anywhere a menu is open.
        public void AutoConfigureGraphics()
        {
            if (!GraphicsTuner.Running && focused && excavationTerrain != null && GameSettings.RenderingAvailable)
                StartCoroutine(TuneGraphics(() => focused, false));
        }

        private System.Collections.IEnumerator TuneGraphics(Func<bool> stillValid, bool firstLaunch)
        {
            yield return GraphicsTuner.Run(GameSettings, viewCamera, excavationTerrain, stillValid);
            if (firstLaunch && !GameSettings.Values.GraphicsTuned) pendingGraphicsTuning = true;
        }

        public void ConfigureGamePreferences(IDevicePreferencesStore store, IGameSettingsPlatform platform = null)
        {
            GameSettings?.Dispose();
            GameSettings = new GamePreferences(store, platform ?? new UnityGameSettingsPlatform(false));
        }

        // Injectable storage keeps integration fixtures independent of the user's device preferences.
        public void ConfigureInputPreferences(IDevicePreferencesStore store)
        {
            InputSettings = new InputPreferences(store);
            BindingCapture = new InputBindingCapture(InputSettings);
            input?.ConfigurePreferences(InputSettings);
        }

        public void ConfigureCameraPreferences(ICameraPreferencesStore store)
        {
            if (CameraSettings != null) CameraSettings.Changed -= ApplyCameraPreferences;
            CameraSettings = new CameraPreferences(store);
            CameraSettings.Changed += ApplyCameraPreferences;
            ApplyCameraPreferences();
        }

        private void ApplyCameraPreferences()
        {
            if (viewCamera != null && viewCamera.fieldOfView != CameraSettings.VerticalFov)
                viewCamera.fieldOfView = CameraSettings.VerticalFov;
            crouch?.UpdateProjection();
        }

        public void Capture(WorldSnapshot snapshot)
        {
            snapshot.InventoryCapacity = Inventory.Capacity;
            snapshot.InventoryLevel = Inventory.Level;
            snapshot.FuelLevel = Battery.Level;
            snapshot.JetpackLevel = Jetpack.Level;
            snapshot.LampsOwned = LampKit.Owned;
            snapshot.C4Level = Charges.Level;
            snapshot.ChargesOwned = Charges.Owned;
            snapshot.Inventory = new ItemSnapshot[Inventory.Count];
            for (int i = 0; i < Inventory.Count; i++) snapshot.Inventory[i] = ItemSnapshot.Capture(Inventory.Items[i]);
            snapshot.Credits = Wallet.WholeCredits;
            snapshot.ShovelLevel = Shovel.Level;
            snapshot.BatteryCapacity = Battery.Capacity;
            snapshot.BatteryCharge = Battery.Charge;
            snapshot.PlayerPosition = transform.position;
            snapshot.PlayerRotation = transform.rotation;
            snapshot.Pitch = pitch;
            snapshot.VerticalSpeed = verticalSpeed;
            snapshot.CrouchAmount = CrouchAmount;
            snapshot.SuccessfulStrokes = SuccessfulStrokes;
            snapshot.Worksite = worksiteTools != null ? worksiteTools.Capture() : new WorksiteSnapshot();
        }

        public void Restore(WorldSnapshot snapshot)
        {
            Detector?.Reset();
            worksiteTools?.Cancel();
            pickupPresentation?.Clear();
            proximityCollection?.Clear();
            loadRide?.Clear();
            Physics.SyncTransforms();
            if (!crouch.CanRestore(snapshot.CrouchAmount, snapshot.PlayerPosition, snapshot.PlayerRotation, excavationTerrain))
                throw new System.IO.InvalidDataException("The saved player stance has no safe clearance. The checkpoint has been kept.");
            var inventory = new SessionInventory(snapshot.InventoryCapacity, snapshot.InventoryLevel);
            foreach (var item in snapshot.Inventory)
                if (!inventory.TryAdd(item.Restore())) throw new System.IO.InvalidDataException("The carried finds could not be restored.");
            var shovel = new ShovelState(shovelLevels);
            for (int level = 2; level <= snapshot.ShovelLevel; level++)
                if (!shovel.TryUpgradeTo(level)) throw new System.IO.InvalidDataException("The owned shovel could not be restored.");
            var jetpack = new JetpackState();
            for (int level = 2; level <= snapshot.JetpackLevel; level++)
                if (!jetpack.TryUpgradeTo(level)) throw new System.IO.InvalidDataException("The owned jetpack could not be restored.");
            var battery = new Battery(snapshot.BatteryCapacity, snapshot.FuelLevel);
            battery.RestoreCharge(snapshot.BatteryCharge);
            Inventory = inventory;
            Wallet = new SessionWallet(snapshot.Credits);
            Shovel = shovel;
            Battery = battery;
            Jetpack = jetpack;
            LampKit = new LampKit(snapshot.LampsOwned);
            Charges = new ChargeKit(snapshot.C4Level, snapshot.ChargesOwned);
            Trade = new StationTrade(Inventory, Wallet, Shovel, Battery, Jetpack, LampKit, Charges);
            Rescue = new RescueController(Inventory, Wallet, maximumRescueFee);
            adminLevel = 0;
            unlimitedBattery = adminXray = jetpackReadyInAir = adminHoverOnRelease = adminGroundXray = adminBreakInDust = false;
            excavationTerrain?.SetGroundXray(false, null);
            discoveries?.SetXray(false, null);
            motor.enabled = false;
            transform.SetPositionAndRotation(snapshot.PlayerPosition, snapshot.PlayerRotation);
            crouch.Restore(snapshot.CrouchAmount);
            pitch = snapshot.Pitch;
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            verticalSpeed = snapshot.VerticalSpeed;
            SuccessfulStrokes = snapshot.SuccessfulStrokes;
            ResetJetpackHold();
            digCooldown = DigPulse = LastScoopVolume = 0;
            blockedPickup = null;
            motor.enabled = true;
            Physics.SyncTransforms();
            input?.SuppressHeldActions();
            holdInteraction?.Reset();
            worksiteTools?.Restore(snapshot.Worksite);
        }

        public void ShowPersistenceMenu() => ShowSessionMenu(PlayerMenu.Persistence);

        internal void ShowSessionMenu(PlayerMenu menu)
        {
            GameSettings?.RevertDisplay();
            BindingCapture?.Cancel();
            if (!IsMenuOpen) OpenMenu(menu);
            else
            {
                Menu = menu;
                input?.SuppressHeldActions();
            holdInteraction?.Reset();
                transitionFrame = Time.frameCount;
                MenuChanged?.Invoke();
            }
        }

        private void OnEnable()
        {
            discoveries?.SetXray(AdminXray, viewCamera);
            if (input == null) return;
            input.Enable();
            savedCursorLock = Cursor.lockState;
            savedCursorVisible = Cursor.visible;
            ownsPresentation = true;
            SetGameplayCursor();
        }

        private void Update()
        {
            if (input == null) return;
            if (pendingGraphicsTuning && focused && Menu == PlayerMenu.MainMenu && !GraphicsTuner.Running && excavationTerrain != null
                && excavationTerrain.CanDig && !excavationTerrain.IsRestoring)
            {
                pendingGraphicsTuning = false;
                StartCoroutine(TuneGraphics(() => focused && Menu == PlayerMenu.MainMenu, true));
            }
            BindingCapture.Tick();
            Tick(input.Read(GameplayActive && !BindingCapture.BlocksInput), Time.deltaTime);
            crouch.UpdateProjection();
            Detector?.Tick();
            if (Time.unscaledTime >= feedbackUntil) Feedback = "";
            DigPulse = Mathf.MoveTowards(DigPulse, 0, Time.deltaTime * 5f);
        }

        private void LateUpdate()
        {
            if (GameplayActive) pickupPresentation?.Tick(Time.deltaTime);
        }

        internal void AnimateCollection(MeshRenderer source, MeshFilter mesh) => pickupPresentation?.Play(source, mesh);
        internal bool CanCollectNearby(BuriedFind find) => proximityCollection != null && proximityCollection.CanCollect(find);

        // Exposed for deterministic simulation checks; device bindings remain in FpsInput.
        public void Tick(FpsInputFrame frame, float deltaTime)
        {
            if (!GameplayActive) worksiteTools?.Cancel();
            if (!GameplayActive) holdInteraction?.Reset();
            if (BindingCapture != null && BindingCapture.BlocksInput) { holdInteraction?.Reset(); return; }
            if (Persistence != null && Persistence.BlocksPlay)
            {
                if (focused && frame.BackPressed && transitionFrame != Time.frameCount)
                {
                    if (Menu == PlayerMenu.DeviceSettings) BackFromSettings();
                    else if (Menu == PlayerMenu.InputSettings) BackFromInputSettings();
                    else if (Menu == PlayerMenu.CameraComfort) BackFromCameraComfort();
                    else if (Menu == PlayerMenu.ConfirmNewGame) Persistence.CancelNewGame();
                }
                return;
            }
            if (!focused || input == null || transitionFrame == Time.frameCount) return;
            if (frame.AdminMenuPressed && AdminAvailable
                && (Menu == PlayerMenu.None || Menu == PlayerMenu.Pause || Menu == PlayerMenu.DeveloperAdmin))
            {
                if (Menu == PlayerMenu.DeveloperAdmin) CloseMenu(); else ShowAdminMenu();
                return;
            }
            if (frame.BackPressed)
            {
                if (GameplayActive && worksiteTools != null && worksiteTools.IsPlacing)
                { worksiteTools.Cancel(); SuppressWorldActions(); return; }
                if (Menu == PlayerMenu.DeviceSettings) BackFromSettings();
                else if (Menu == PlayerMenu.InputSettings) BackFromInputSettings();
                else if (Menu == PlayerMenu.CameraComfort) BackFromCameraComfort();
                else if (IsMenuOpen) CloseMenu(); else OpenMenu(PlayerMenu.Pause);
                return;
            }
            if (frame.InventoryPressed)
            {
                if (Menu == PlayerMenu.Inventory) CloseMenu();
                else if (!IsMenuOpen) OpenMenu(PlayerMenu.Inventory);
                return;
            }
            if (IsMenuOpen)
            {
                pendingScoop = -1f;
                // Admin actions are available in their panel; other menus remain barriers.
                if (Menu == PlayerMenu.DeveloperAdmin) HandleAdminShortcuts(frame);
                return;
            }
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            if (HandleAdminShortcuts(frame)) return;
            rescueRetryDelay = Mathf.Max(0f, rescueRetryDelay - deltaTime);
            if (TryAutomaticRescue()) return;

            ApplyLook(frame.Look);
            Vector3 previousFeet = FeetPosition;
            Move(frame.Move, frame.JumpPressed, frame.JetpackHeld, frame.CrouchHeld, frame.SprintHeld, frame.DigHeld, deltaTime);
            if (TryAutomaticRescue()) return;
            // Preserve the fractional frame remainder while holding, but never bank
            // more than one cut or run a burst of terrain rebuilds after a hitch.
            digCooldown = Mathf.Max(-EffectiveDigInterval, digCooldown - deltaTime);
            if (!frame.DigHeld || frame.DigPressed) digCooldown = Mathf.Max(0f, digCooldown);
            RefreshTargetPrompt();
            if (worksiteTools != null && worksiteTools.HandleInput(frame)) { RefreshTargetPrompt(); return; }
            if (holdInteraction.Tick(frame, deltaTime)) { RefreshTargetPrompt(); return; }
            // Interaction wins a simultaneous press so opening a station cannot also dig.
            if (frame.InteractPressed)
            {
                TryInteract();
                return;
            }
            if (!frame.DigHeld) blockedPickup = null;
            if (proximityCollection.Tick(previousFeet,
                frame.Move.sqrMagnitude > .0001f, frame.DigHeld || frame.DigPressed))
            {
                RefreshTargetPrompt();
            }
            if (pendingScoop >= 0f && (pendingScoop -= deltaTime) <= 0f) CompletePendingScoop();
            if (frame.DigPressed || frame.DigHeld) TryPrimaryAction(frame.DigHeld);
        }

        private void ApplyLook(Vector2 delta)
        {
            var preferences = GameSettings.Values;
            float sensitivity = tuning.LookSensitivity * preferences.Sensitivity / 100f;
            transform.Rotate(0f, delta.x * sensitivity * (preferences.InvertX ? -1 : 1), 0f, Space.World);
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity * (preferences.InvertY ? -1 : 1), -tuning.PitchLimit, tuning.PitchLimit);
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private bool HandleAdminShortcuts(FpsInputFrame frame)
        {
            if (!AdminAvailable) return false;
            if (frame.AdminLevel > 0) SelectAdminLevel(frame.AdminLevel);
            if (frame.RefillPressed) RefillAdminBattery();
            if (frame.XrayPressed) ToggleAdminXray();
            if (!frame.ReturnPressed) return false;
            AdminReturnToSurface();
            return true;
        }

        private void Move(Vector2 direction, bool jumpPressed, bool spaceHeld, bool crouchHeld, bool sprintHeld, bool digHeld, float deltaTime)
        {
            direction = Vector2.ClampMagnitude(direction, 1f);
            // Resizing a CharacterController refreshes its native shape. Retain
            // the preceding Move's contact state for this frame's jump/flight logic.
            // A load the player stands on carries them (LoadRide) and counts as ground.
            bool riding = loadRide.Standing && verticalSpeed <= 0f;
            bool grounded = motor.isGrounded || riding;
            if (crouch.Tick(crouchHeld, deltaTime)) ShowFeedback("Low ceiling");
            if (grounded && verticalSpeed <= 0f) jetpackReadyInAir = false;
            if (grounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (jumpPressed && grounded)
                verticalSpeed = Mathf.Sqrt(2f * Mathf.Max(0f, tuning.JumpHeight) * Mathf.Max(0f, -tuning.Gravity));
            float unpoweredSpeed = verticalSpeed;
            verticalSpeed += tuning.Gravity * deltaTime;
            var jet = Jetpack.Current;

            // Charge only for the part of this frame after the hold threshold. A tap
            // is a free jump, including when the battery is exhausted.
            // Once powered flight has begun, another press can immediately arrest
            // a fall. Landing starts a new grounded jump/hold cycle.
            float delay = jetpackReadyInAir ? 0f : Mathf.Max(0f, tuning.JetpackHoldDelay);
            float thrustTime = spaceHeld ? Mathf.Max(0f, deltaTime - Mathf.Max(0f, delay - jetpackHoldTime)) : 0f;
            jetpackHoldTime = spaceHeld ? Mathf.Min(delay, jetpackHoldTime + deltaTime) : 0f;
            float energyRate = jet.EnergyPerSecond;
            float cost = energyRate * thrustTime;
            if (!UnlimitedBattery && energyRate > 0f && cost > Battery.Charge)
            {
                // Burn the final fraction instead of stranding fuel smaller than
                // this frame's cost, which could restart thrust on a shorter frame.
                cost = Battery.Charge;
                thrustTime = cost / energyRate;
            }
            IsJetpackActive = thrustTime > 0f && SpendEnergy(cost);
            IsHovering = false;
            if (IsJetpackActive)
            {
                jetpackReadyInAir = true;
                verticalSpeed = Mathf.Min(jet.MaxAscentSpeed, Mathf.Max(0f, verticalSpeed)
                    + jet.Acceleration * thrustTime);
            }
            else if (jet.HoverHold && jetpackReadyInAir && !grounded && !spaceHeld
                && (HoverOnRelease ? !crouchHeld : digHeld) && (UnlimitedBattery || Battery.Charge > 0f) && !NearGroundBelow()
                && SpendEnergy(Mathf.Min(UnlimitedBattery ? float.MaxValue : Battery.Charge,
                    jet.EnergyPerSecond * EquipmentProgression.HoverEnergyScale * deltaTime)))
            {
                // Brake to a standstill and hold; never within a stride of the floor, so arriving lands.
                IsHovering = true;
                verticalSpeed = Mathf.MoveTowards(unpoweredSpeed, 0f, EquipmentProgression.HoverBrake * deltaTime);
            }
            float speedMultiplier = crouch.IsPrecision ? crouch.SpeedMultiplier
                : sprintHeld ? Mathf.Clamp(tuning.SprintSpeedMultiplier, 1f, 1.5f) : 1f;
            var planar = (transform.right * direction.x + transform.forward * direction.y) * (tuning.WalkSpeed * speedMultiplier);
            loadRide.Carry(deltaTime);
            // No push down onto a carrying load: its collider runs up to a physics step ahead of the drawn load,
            // so the rider would drop in steps after it.
            float fall = riding && verticalSpeed < 0f ? 0f : verticalSpeed;
            var collisions = motor.Move((planar + Vector3.up * fall) * deltaTime);
            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            if ((collisions & CollisionFlags.Below) != 0 && verticalSpeed < 0f) verticalSpeed = -2f;
            loadRide.Track();
        }

        private bool NearGroundBelow()
        {
            float radius = motor.radius * .9f;
            return Physics.SphereCast(FeetPosition + Vector3.up * (radius + .05f), radius, Vector3.down, out _,
                EquipmentProgression.HoverGroundClearance + .05f, worldMask, QueryTriggerInteraction.Ignore);
        }

        public bool TryGetTarget(float reach, out RaycastHit hit)
        {
            var aim = AimRay;
            return Physics.Raycast(aim.origin, aim.direction, out hit, reach, worldMask, QueryTriggerInteraction.Ignore);
        }

        private Ray AimRay => new Ray(viewCamera.transform.position, viewCamera.transform.forward);

        internal static T Contract<T>(Collider collider) where T : class
        {
            foreach (var component in collider.GetComponentsInParent<MonoBehaviour>())
                if (component.isActiveAndEnabled && component is T target) return target;
            return null;
        }

        public void RefreshTargetPrompt()
        {
            TargetPrompt = "";
            if (!IsMenuOpen && worksiteTools != null && worksiteTools.IsPlacing)
            { TargetPrompt = worksiteTools.PlacementPrompt; return; }
            if (IsMenuOpen || !TryGetTarget(Mathf.Max(EffectiveDigReach, MaximumPickupReach), out var hit)) return;
            var find = Contract<BuriedFind>(hit.collider);
            var interactable = Contract<IInteractionTarget>(hit.collider);
            if (find != null && hit.distance <= PickupReach(find))
                TargetPrompt = find.GetPrompt(this);
            else if (hit.distance <= tuning.InteractReach && interactable != null)
                TargetPrompt = interactable.GetPrompt(this);
            if (HoldProgress > 0 && !string.IsNullOrEmpty(TargetPrompt)) TargetPrompt += $"  {Mathf.CeilToInt(HoldProgress * 100)}%";
            else if (hit.distance <= EffectiveDigReach && Contract<IDigTarget>(hit.collider) is IDigTarget target && !target.CanDig)
                TargetPrompt = target.DigPrompt;
            if (find == null && interactable == null && worksiteTools != null)
            {
                string markPrompt = worksiteTools.MarkPrompt();
                if (!string.IsNullOrEmpty(markPrompt)) TargetPrompt = markPrompt;
            }
            // Ground Lab: name the bay or crane scene and the ground under the crosshair from anywhere on the plot.
            if (string.IsNullOrEmpty(TargetPrompt) && Persistence != null && Persistence.State == WorldSaveState.Lab
                && TryGetTarget(40f, out var far) && Contract<TerrainVolume>(far.collider) == excavationTerrain)
                TargetPrompt = GroundLab.Describe(far.point, excavationTerrain.MaterialAt(far.point - far.normal * .05f));
        }

        internal void SuppressWorldActions()
        {
            input?.SuppressHeldActions(); holdInteraction?.Reset();
            blockedPickup = null; digCooldown = 0; pendingScoop = -1f;
        }

        // Aimed pickup uses the centre ray, independently of shovel radius. A terrain
        // stroke aimed at a visible find can finish that same find's collection.
        // Held and fresh input collect eligible aimed finds without the shovel timer.
        // Soil-only strokes never collect off-aim finds.
        public bool TryPrimaryAction() => TryPrimaryAction(false);

        private bool TryPrimaryAction(bool continueDiggingAfterPickup)
        {
            if (IsMenuOpen || !focused || (Persistence != null && Persistence.BlocksPlay)) return false;
            if (TryGetTarget(Mathf.Max(EffectiveDigReach, MaximumPickupReach), out var hit)
                && Contract<BuriedFind>(hit.collider) is BuriedFind find)
            {
                if (Inventory.IsFull && find.Kind == DiscoveryKind.Common)
                {
                    if (blockedPickup != find) ShowFeedback("Inventory full - keep digging; find left in place");
                    blockedPickup = find;
                    if (digCooldown > 0f) return false;
                    bool cut = TryDig();
                    ScheduleNextDig();
                    return cut;
                }
                if (!find.Collectible)
                {
                    if (digCooldown > 0f) return false;
                    bool uncovered = TryDig();
                    ScheduleNextDig();
                    if (uncovered && find.Collectible)
                    {
                        bool finishedPickup = find.TryCollect(this);
                        blockedPickup = !finishedPickup && Inventory.IsFull ? find : null;
                    }
                    RefreshTargetPrompt();
                    return uncovered;
                }
                if (hit.distance > PickupReach(find)) return false;
                bool collected = find.TryCollect(this);
                blockedPickup = !collected && Inventory.IsFull ? find : null;
                if (collected && continueDiggingAfterPickup && digCooldown <= 0f)
                {
                    // Re-resolve the world ray after collection removes the collider.
                    // Pickup never consumes or restarts the normal cutting cadence.
                    if (TryDig()) ScheduleNextDig();
                }
                RefreshTargetPrompt();
                return collected;
            }
            blockedPickup = null;
            if (digCooldown > 0 || pendingScoop >= 0f) return false;
            if (!ShavingEnabled && tuning.CutAtScoop)
            {
                // A shovel stroke into the ground starts now; its cut waits for the scoop, when the blade lifts and the
                // dirt goes (CompletePendingScoop). Other dig targets, which show no dirt, are struck at once below.
                bool ready = PrepareDig(out _, out var aimed, out var ground, out _);
                if (!ready || aimed is TerrainVolume)
                {
                    if (ready)
                    {
                        LastDigMaterial = ground;
                        LastDigInterval = scheduledDigInterval;
                        StrokesStarted++;
                        pendingScoop = ToolRigPresenter.ScoopDelay(scheduledDigInterval, ground);
                    }
                    ScheduleNextDig();
                    return ready;
                }
            }
            bool dug = TryDig();
            ScheduleNextDig();
            // The cut can reveal a different find on the same ray. Resolve it now,
            // then keep checking each held-input frame while the shovel recovers.
            if (dug && TryGetTarget(MaximumPickupReach, out var newlyExposed)
                && Contract<BuriedFind>(newlyExposed.collider) is BuriedFind revealed)
                revealed.TryCollect(this);
            return dug;
        }

        private void ScheduleNextDig() => digCooldown = Mathf.Max(0.001f, digCooldown + scheduledDigInterval);

        private bool TryGetDigTarget(out RaycastHit hit)
        {
            if (ShavingEnabled && NearerLip(out hit)) return true;
            if (!TryGetTarget(EffectiveDigReach, out hit)) return false;
            var find = Contract<BuriedFind>(hit.collider);
            if (find == null) return true;
            if (!Inventory.IsFull || find.Kind != DiscoveryKind.Common)
                return find.TryGetCoveringSoil(this, worldMask, out hit);

            // Bag capacity must never make common finds a barrier to excavation.
            // Only this cutting ray ignores them; physical bodies and pickup stay intact.
            var aim = AimRay;
            int count = Physics.RaycastNonAlloc(aim.origin, aim.direction, fullBagDigHits, EffectiveDigReach, worldMask,
                QueryTriggerInteraction.Ignore);
            hit = default;
            if (count == fullBagDigHits.Length) return false;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var candidate = fullBagDigHits[i];
                if (candidate.distance >= nearest) continue;
                var common = candidate.collider.GetComponentInParent<BuriedFind>();
                if (common != null && common.isActiveAndEnabled && common.Kind == DiscoveryKind.Common
                    && common.State == FindState.World) continue;
                hit = candidate; nearest = candidate.distance;
            }
            return hit.collider != null;
        }

        public bool TryDig() => TryDig(true);

        // The drill's bit is wider than the crosshair (user, 2026-10-06: "as if it has a tiny tip, I have to be really
        // precise"): aimed just past the edge of a near lip, it bores the lip rather than whatever the aim's ray reaches
        // beyond it, however far (looking down through a hole into a geode, the ray passed the rim to a floor out of reach,
        // so nothing was dug). Rays in two rings up to LipCone degrees round the aim look for ground within reach and
        // nearer than the ray's own hit, LipDrop or more off that hit's face (past an edge, not further along the same floor
        // or wall at a slant); the nearest is the lip. A cone, not a ball: only ground beside the crosshair counts. A ball
        // as wide as the bite caught a wall right beside the player, far off the crosshair (user: "it dug next to me").
        private const float LipCone = 3.5f, LipDrop = .2f, LipSight = 50f, LipNearer = .15f;
        private const int LipRays = 12;
        private bool NearerLip(out RaycastHit lip)
        {
            var aim = AimRay;
            lip = default;
            if (!Physics.Raycast(aim.origin, aim.direction, out var beyond, LipSight, worldMask, QueryTriggerInteraction.Ignore)) return false;
            float reach = Mathf.Min(EffectiveDigReach, beyond.distance - LipNearer);
            if (reach <= 0) return false;
            var side = Vector3.Cross(aim.direction, Mathf.Abs(aim.direction.y) < .99f ? Vector3.up : Vector3.right).normalized;
            float nearest = float.MaxValue;
            for (int ring = 1; ring <= 2; ring++)
            for (int i = 0; i < LipRays; i++)
            {
                var direction = Quaternion.AngleAxis(i * 360f / LipRays, aim.direction) * Quaternion.AngleAxis(LipCone * ring * .5f, side) * aim.direction;
                if (!Physics.Raycast(aim.origin, direction, out var hit, reach, worldMask, QueryTriggerInteraction.Ignore) || hit.distance >= nearest
                    || Mathf.Abs(Vector3.Dot(beyond.point - hit.point, hit.normal)) < LipDrop || Contract<TerrainVolume>(hit.collider) == null) continue;
                nearest = hit.distance;
                lip = hit;
            }
            return nearest < float.MaxValue;
        }

        // The cut a shovel stroke started earlier, at its scoop, where the player looks by then (user, 2026-10-05: not
        // where the stroke began); a find it reveals is collected as on a press.
        private void CompletePendingScoop()
        {
            pendingScoop = -1f;
            if (!TryDig(false)) return;
            if (TryGetTarget(MaximumPickupReach, out var exposed) && Contract<BuriedFind>(exposed.collider) is BuriedFind revealed)
                revealed.TryCollect(this);
        }

        // What a cut here would hit, in what ground, and its cost; false (with feedback) when nothing can be dug.
        private bool PrepareDig(out RaycastHit hit, out IDigTarget target, out TerrainMaterialId material, out float cost)
        {
            scheduledDigInterval = EffectiveDigInterval;
            hit = default; target = null; material = TerrainMaterialId.Soil; cost = 0f;
            if (IsMenuOpen || !focused || (Persistence != null && Persistence.BlocksPlay)
                || !TryGetDigTarget(out hit)) return false;
            target = Contract<IDigTarget>(hit.collider);
            if (target == null || !target.CanDig)
            {
                var find = hit.collider.GetComponent<BuriedFind>();
                if (find == null) ShowFeedback(target?.DigPrompt ?? "Cannot dig here");
                return false;
            }
            var terrain = target as TerrainVolume;
            material = terrain != null ? terrain.ToolMaterialAt(hit) : TerrainMaterialId.Soil;
            float intervalScale = EquipmentProgression.MaterialResponse(material).Interval;
            scheduledDigInterval *= intervalScale;
            cost = EffectiveDigEnergy * intervalScale;
            if (!UnlimitedBattery && !Battery.CanSpend(cost)) { ShowFeedback("Not enough charge to dig - return to recharge"); return false; }
            return true;
        }

        // `startsStroke`: this cut also starts a tool stroke (false for a shovel's scoop, whose stroke started earlier).
        private bool TryDig(bool startsStroke)
        {
            if (!PrepareDig(out var hit, out var target, out var material, out float cost)) return false;
            var terrain = target as TerrainVolume;
            bool accepted = terrain != null
                ? terrain.TryToolCut(hit, EffectiveShovel.Radius, ShavingEnabled, AimRay.direction)
                : target.TryDig(hit);
            if (!accepted) return false;
            LastDigMaterial = material;
            LastDigInterval = scheduledDigInterval;
            SpendEnergy(cost);
            SuccessfulStrokes++;
            if (startsStroke) StrokesStarted++;
            LastScoopVolume = target is TerrainVolume volume ? volume.LastRemovedVolume : 0;
            DigPulse = 1;
            RefreshTargetPrompt();
            TryAutomaticRescue();
            return true;
        }

        private bool SpendEnergy(float cost) => UnlimitedBattery || Battery.TrySpend(cost);

        public void RestoreAdminOverrides()
        {
            if (!focused || !AdminAvailable) return;
            adminLevel = 0;
            unlimitedBattery = false;
            adminXray = false;
            adminDetectorOff = false;
            adminHoverOnRelease = false;
            adminGroundXray = false;
            adminBreakInDust = false;
            if (adminContactShading) { adminContactShading = false; ContactShading.Restore(); }
            pendingScoop = -1f;
            excavationTerrain?.SetGroundXray(false, null);
            discoveries?.SetXray(false, null);
            ResetDigComparisonInput();
            ShowFeedback("Normal rules restored");
            MenuChanged?.Invoke();
        }

        public void ToggleAdminUnlimitedBattery()
        {
            if (!focused || !AdminAvailable) return;
            unlimitedBattery = !unlimitedBattery;
            ShowFeedback(unlimitedBattery ? "Unlimited battery enabled" : "Normal battery use restored");
            MenuChanged?.Invoke();
        }

        private void ResetDigComparisonInput()
        {
            digCooldown = DigPulse = 0; pendingScoop = -1f;
            blockedPickup = null;
            input?.SuppressDig();
            holdInteraction?.Reset();
        }

        public void ToggleAdminHover()
        {
            if (!focused || !AdminAvailable || (IsMenuOpen && Menu != PlayerMenu.DeveloperAdmin)) return;
            adminHoverOnRelease = !adminHoverOnRelease;
            ShowFeedback("Hover " + AdminHoverLabel + (HoverOnRelease ? "; hold crouch to drop" : ""));
            MenuChanged?.Invoke();
        }

        public void ToggleAdminBreakIn()
        {
            if (!focused || !AdminAvailable || (IsMenuOpen && Menu != PlayerMenu.DeveloperAdmin)) return;
            adminBreakInDust = !adminBreakInDust;
            ShowFeedback("Breaking into a chest's pocket: " + AdminBreakInLabel);
            MenuChanged?.Invoke();
        }

        public void CycleAdminDrill(DrillDial dial)
        {
            if (!focused || !AdminAvailable || (IsMenuOpen && Menu != PlayerMenu.DeveloperAdmin)) return;
            int i = (int)dial;
            adminDrillDials[i] = (adminDrillDials[i] + 1) % DrillDialSteps[i].Length;
            ShowFeedback("Drill " + dial.ToString().ToLowerInvariant() + " " + AdminDrillLabel(dial));
            MenuChanged?.Invoke();
        }

        public void ToggleAdminDetector()
        {
            if (!focused || !AdminAvailable || (IsMenuOpen && Menu != PlayerMenu.DeveloperAdmin)) return;
            adminDetectorOff = !adminDetectorOff;
            Detector?.Reset();
            ShowFeedback(DetectorShown ? "Detector shown" : "Detector off for this session");
            MenuChanged?.Invoke();
        }

        public float AdminContactShading => ContactShading.Current;

        public void SetAdminContactShading(float value)
        {
            if (!AdminAvailable) return;
            adminContactShading = true;
            ContactShading.Set(Mathf.Clamp(value, 0, ContactShading.Maximum));
        }

        public void ToggleAdminXray()
        {
            if (!focused || !AdminAvailable || discoveries == null
                || (IsMenuOpen && Menu != PlayerMenu.DeveloperAdmin)) return;
            adminXray = !adminXray;
            discoveries.SetXray(AdminXray, viewCamera);
            MenuChanged?.Invoke();
        }

        // Finds ground tells on purpose: transparent ground with coloured markers for every tell ground (backfill).
        // Ground Lab sessions start with unlimited battery; everything else stays as in the game.
        public void BeginGroundLab()
        {
            if (!AdminAvailable) return;
            unlimitedBattery = true;
            ShowFeedback("Ground Lab: unlimited battery on. Ctrl+Shift+1-9/0 picks a tool level; the holes around the bays hold computers for the crane. Nothing here is saved.");
            MenuChanged?.Invoke();
        }

        public void ToggleAdminGroundXray()
        {
            if (!focused || !AdminAvailable || (IsMenuOpen && Menu != PlayerMenu.DeveloperAdmin)) return;
            adminGroundXray = !adminGroundXray;
            excavationTerrain.SetGroundXray(adminGroundXray, viewCamera);
            ShowFeedback(adminGroundXray ? "Ground X-ray: " + TerrainVolume.XrayLegend : "Ground X-ray off");
            MenuChanged?.Invoke();
        }

        // Testing convenience: hand the session enough money to buy through a track
        // without digging a full run first.
        public void GrantAdminMoney()
        {
            if (!focused || !AdminAvailable) return;
            Wallet.TryCredit(500);
            ShowFeedback("+$500 test money");
            MenuChanged?.Invoke();
        }

        // Testing convenience: a full C4 pack at the owned track level.
        public void FillAdminCharges()
        {
            if (!focused || !AdminAvailable) return;
            Charges.Fill();
            ShowFeedback($"C4 pack filled: {Charges.Owned} charges");
            MenuChanged?.Invoke();
        }

        public bool SelectAdminLevel(int level)
        {
            if (!focused || !AdminAvailable || level < 1 || level > Shovel.LevelCount) return false;
            adminLevel = level;
            ResetDigComparisonInput();
            ShowFeedback($"{EquipmentProgression.ToolName(level)} {level}  |  Reach {EffectiveDigReach:F1} m  |  Cut width {EffectiveShovel.Radius * 2:F2} m");
            MenuChanged?.Invoke();
            return true;
        }

        public void RefillAdminBattery()
        {
            if (!focused || !AdminAvailable) return;
            Battery.Recharge();
            ShowFeedback("Battery refilled");
        }

        // Developer calibration: the dev menu edits a session copy of the tool ladder and
        // prints it, so feel tuning never needs a scene patch or a rebuild to iterate.
        public enum TuningDial { Bite, Cadence, Reach }

        // Sliders call this while dragging: the value lands on the selected level and is
        // used by the very next stroke. MenuChanged is deliberately not raised - a
        // rebuild mid-drag would tear the slider out from under the pointer.
        public void SetAdminTuning(TuningDial dial, float value)
        {
            if (!focused || !AdminAvailable) return;
            EnsureAdminTuning();
            var profile = adminTuning[EffectiveShovelLevel - 1];
            // Ranges match the dev sliders: the bite floor is one voxel, the ceiling and
            // cadence span deliberately exceed anything the authored ladder ships with.
            if (dial == TuningDial.Bite) profile.Radius = Mathf.Clamp(value, .125f, 2f);
            else if (dial == TuningDial.Cadence) profile.CadenceMultiplier = Mathf.Clamp(value, .1f, 4f);
            else profile.ReachBonus = Mathf.Clamp(value, 0f, MaximumDigReach);
        }

        public float AdminTuningValue(int level, TuningDial dial)
        {
            var profile = ProfileAt(level);
            return dial == TuningDial.Bite ? profile.Radius
                : dial == TuningDial.Cadence ? profile.CadenceMultiplier : profile.ReachBonus;
        }

        private void EnsureAdminTuning()
        {
            if (adminTuning != null) return;
            adminTuning = new ShovelProfile[Shovel.LevelCount];
            for (int i = 0; i < adminTuning.Length; i++) adminTuning[i] = shovelLevels[i].Clone();
        }

        public void ResetAdminTuning()
        {
            if (!focused || !AdminAvailable) return;
            adminTuning = null;
            ShowFeedback("Tool tuning reset to the authored ladder");
            MenuChanged?.Invoke();
        }

        // One row per level, in the source's own fields and precision, so a screenshot
        // of this table is enough to bake the numbers back into EquipmentProgression.ToolProfiles().
        public string AdminTuningSummary()
        {
            var text = new System.Text.StringBuilder();
            for (int level = 1; level <= Shovel.LevelCount; level++)
            {
                var profile = ProfileAt(level);
                text.Append(level).Append(":  radius ").Append(Number(profile.Radius))
                    .Append("  cadence ").Append(Number(profile.CadenceMultiplier))
                    .Append("  reach +").Append(Number(profile.ReachBonus));
                if (level < Shovel.LevelCount) text.Append('\n');
            }
            return text.ToString();
        }

        public enum GroundDial { Width, Length, Depth, Speed }
        // Developer ground tuning: dials one ground's bite for the session (EquipmentProgression overrides).
        public TerrainMaterialId AdminGround { get; private set; } = TerrainMaterialId.Soil;
        public bool HasAdminGroundTuning => AdminAvailable && EquipmentProgression.HasResponseOverrides;
        public static readonly TerrainMaterialId[] TunableGrounds = { TerrainMaterialId.Soil, TerrainMaterialId.Backfill, TerrainMaterialId.GeodeShell };

        public void CycleAdminGround()
        {
            if (!focused || !AdminAvailable) return;
            int index = System.Array.IndexOf(TunableGrounds, AdminGround);
            AdminGround = TunableGrounds[(index + 1) % TunableGrounds.Length];
            MenuChanged?.Invoke();
        }

        public float AdminGroundValue(GroundDial dial)
        {
            var response = EquipmentProgression.MaterialResponse(AdminGround);
            return dial == GroundDial.Width ? response.Width : dial == GroundDial.Length ? response.Length
                : dial == GroundDial.Depth ? response.Penetration : response.Interval;
        }

        public void SetAdminGround(GroundDial dial, float value)
        {
            if (!AdminAvailable || !(value > 0)) return;
            var r = EquipmentProgression.MaterialResponse(AdminGround);
            EquipmentProgression.OverrideResponse(AdminGround, new MaterialToolResponse(
                dial == GroundDial.Width ? value : r.Width, dial == GroundDial.Length ? value : r.Length,
                dial == GroundDial.Depth ? value : r.Penetration, dial == GroundDial.Speed ? value : r.Interval));
        }

        public void ResetAdminGroundTuning()
        {
            if (!focused || !AdminAvailable) return;
            EquipmentProgression.ClearResponseOverrides();
            ShowFeedback("Ground tuning back to the authored values");
            MenuChanged?.Invoke();
        }

        public static string GroundTable()
        {
            var table = new System.Text.StringBuilder("Ground: width / length / depth x the tool's bite, speed x its stroke time\n");
            foreach (var ground in TunableGrounds)
            {
                var r = EquipmentProgression.MaterialResponse(ground);
                table.Append($"{ground}: {r.Width:0.00} / {r.Length:0.00} / {r.Penetration:0.00}, speed {r.Interval:0.00}x - {EquipmentProgression.GroundEffect(ground)}\n");
            }
            return table.ToString().TrimEnd();
        }

        public void PrintAdminGroundTuning()
        {
            if (!focused || !AdminAvailable) return;
            var source = new System.Text.StringBuilder();
            foreach (var ground in TunableGrounds)
            {
                var r = EquipmentProgression.MaterialResponse(ground);
                source.Append($"        {ground} = new MaterialToolResponse({r.Width:0.###}f, {r.Length:0.###}f, {r.Penetration:0.###}f, {r.Interval:0.###}f);\n");
            }
            string printed = source.ToString();
            Debug.Log(printed);
            try
            {
                string path = System.IO.Path.Combine(Application.persistentDataPath, "ground-tuning.txt");
                System.IO.File.WriteAllText(path, printed);
                ShowFeedback("Ground tuning saved beside your saves as ground-tuning.txt");
            }
            catch (System.Exception error) { ShowFeedback("Could not write ground-tuning.txt: " + error.Message); }
            MenuChanged?.Invoke();
        }

        public void PrintAdminTuning()
        {
            if (!focused || !AdminAvailable) return;
            if (adminTuning == null)
            {
                ShowFeedback("Tool tuning still matches the authored ladder - nothing to print");
                return;
            }
            var source = new System.Text.StringBuilder();
            for (int level = 1; level <= adminTuning.Length; level++)
            {
                source.Append("            ").Append(Source(adminTuning[level - 1]));
                source.Append(level < adminTuning.Length ? ",\n" : "\n");
            }
            for (int level = 2; level <= adminTuning.Length; level++)
            {
                var previous = adminTuning[level - 2];
                var profile = adminTuning[level - 1];
                if (profile.Radius > previous.Radius && profile.ReachBonus > previous.ReachBonus) continue;
                source.Append("// WARNING: level ").Append(level)
                    .Append(" must beat level ").Append(level - 1)
                    .Append(" in both bite and reach before pasting.\n");
            }
            string printed = source.ToString();
            Debug.Log(printed);
            try
            {
                // Saved with the player's other files, never beside the game: a released
                // build folder has to stay clean of development leftovers.
                string path = System.IO.Path.Combine(Application.persistentDataPath, "tuning.txt");
                System.IO.File.WriteAllText(path, printed);
                Debug.Log("Tool tuning written to " + path);
                ShowFeedback("Tool tuning logged; saved beside your saves as tuning.txt");
            }
            catch (System.Exception error) { ShowFeedback("Could not write tuning.txt: " + error.Message); }
            MenuChanged?.Invoke();
        }

        // Invariant culture: a Bulgarian decimal comma would not compile as C# source.
        private static string Source(ShovelProfile profile) =>
            "new ShovelProfile(" + Number(profile.Radius) + "f, " + Number(profile.CadenceMultiplier)
            + "f, " + Number(profile.ReachBonus) + "f)";
        private static string Number(float value) =>
            value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

        public void AdminReturnToSurface()
        {
            if (!focused || !AdminAvailable) return;
            ReturnToSurface();
            ShowFeedback("Returned to the surface. Your excavation is preserved.");
        }

        private void ReturnToSurface()
        {
            worksiteTools?.Cancel();
            loadRide?.Clear();
            pickupPresentation?.Clear();
            motor.enabled = false;
            transform.SetPositionAndRotation(surfaceReturn.position, surfaceReturn.rotation);
            crouch.Restore(0f);
            pitch = 48;
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            verticalSpeed = 0;
            jetpackReadyInAir = false;
            ResetJetpackHold();
            digCooldown = DigPulse = 0; pendingScoop = -1f;
            blockedPickup = null;
            motor.enabled = true;
            Physics.SyncTransforms();
            input?.SuppressHeldActions();
            holdInteraction?.Reset();
        }

        private bool TryAutomaticRescue()
        {
            if (!GameplayActive || !RescueAvailable || UnlimitedBattery || Battery.Charge > 0f || rescueRetryDelay > 0f) return false;
            // Never charge or discard loot if the authored landing area is unavailable.
            Physics.SyncTransforms();
            if (!crouch.CanRestore(0f, surfaceReturn.position, surfaceReturn.rotation, excavationTerrain)
                || !Physics.Raycast(surfaceReturn.position + Vector3.up * 0.2f, Vector3.down,
                    out var ground, 0.6f, worldMask, QueryTriggerInteraction.Ignore) || ground.normal.y < 0.7f)
            {
                rescueRetryDelay = 1f;
                ShowFeedback("Rescue waiting for a clear landing area. Your finds and money are safe.");
                return false;
            }
            Rescue.Prepare();
            if (!Rescue.TryConfirm(out var receipt))
            {
                Rescue.Cancel();
                return false;
            }
            ReturnToSurface();
            Battery.Recharge();
            transitionFrame = Time.frameCount;
            TargetPrompt = "";
            int count = receipt.LostItems.Count;
            ShowFeedback($"Fuel empty — rescued  |  {count} {(count == 1 ? "find" : "finds")} lost  |  -${receipt.Fee:0}");
            Persistence?.RequestCheckpoint();
            return true;
        }

        public void ShowAdminMenu()
        {
            if (!focused || !AdminAvailable || (IsMenuOpen && Menu != PlayerMenu.Pause)) return;
            if (!IsMenuOpen) { OpenMenu(PlayerMenu.DeveloperAdmin); return; }
            Menu = PlayerMenu.DeveloperAdmin;
            MenuChanged?.Invoke();
        }

        public void RequestTerrainReset()
        {
            if (!focused || !AdminAvailable || Menu != PlayerMenu.DeveloperAdmin) return;
            Menu = PlayerMenu.ConfirmTerrainReset;
            MenuChanged?.Invoke();
        }

        public bool ConfirmTerrainReset()
        {
            if (!focused || Menu != PlayerMenu.ConfirmTerrainReset || !AdminAvailable) return false;
            AdminReturnToSurface();
            excavationTerrain.ResetExcavation();
            SuccessfulStrokes = 0;
            LastScoopVolume = DigPulse = 0;
            Battery.Recharge();
            Menu = PlayerMenu.DeveloperAdmin;
            ShowFeedback("Fresh ground ready. Shovel level and inventory preserved.");
            MenuChanged?.Invoke();
            return true;
        }

        public void CancelTerrainReset()
        {
            if (!focused || Menu != PlayerMenu.ConfirmTerrainReset) return;
            Menu = PlayerMenu.DeveloperAdmin;
            MenuChanged?.Invoke();
        }

        public bool TryInteract()
        {
            if (IsMenuOpen || !focused || !TryGetTarget(tuning.InteractReach, out var hit)) return false;
            if (worksiteTools != null && worksiteTools.TryEraseMark()) { RefreshTargetPrompt(); return true; }
            var target = Contract<IInteractionTarget>(hit.collider);
            if (target == null) return false;
            bool accepted = target.TryInteract(this);
            RefreshTargetPrompt();
            return accepted;
        }

        public void ShowFeedback(string message)
        {
            Feedback = message;
            feedbackUntil = Time.unscaledTime + 2.5f;
        }

        public void OpenStation(StationTarget station)
        {
            if (!GameplayActive || station == null || !station.isActiveAndEnabled
                || !TryGetTarget(tuning.InteractReach, out var hit)
                || Contract<IInteractionTarget>(hit.collider) != (IInteractionTarget)station) return;
            Station = station;
            StationNotice = "";
            RefreshStationOffers();
            OpenMenu(PlayerMenu.Station);
        }

        private void RefreshStationOffers()
        {
            StationRevision++;
            if (Station != null) Station.RefreshOffers(this);
        }

        public bool CanUseStation(StationTarget station) => isActiveAndEnabled && focused
            && Menu == PlayerMenu.Station && station != null && Station == station && station.isActiveAndEnabled
            && TryGetTarget(tuning.InteractReach, out var hit)
            && Contract<IInteractionTarget>(hit.collider) == (IInteractionTarget)station;

        public void ShowStationFeedback(string message)
        {
            StationNotice = message;
            ShowFeedback(message);
        }

        public void OpenMenu(PlayerMenu menu)
        {
            if (menu == PlayerMenu.None || IsMenuOpen) return;
            if ((menu == PlayerMenu.DeveloperAdmin || menu == PlayerMenu.ConfirmTerrainReset) && !AdminAvailable) return;
            savedTimeScale = Time.timeScale;
            Menu = menu;
            Time.timeScale = 0f;
            ResetJetpackHold();
            input?.SuppressHeldActions();
            holdInteraction?.Reset();
            transitionFrame = Time.frameCount;
            TargetPrompt = "";
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            MenuChanged?.Invoke();
        }

        public void CloseMenu()
        {
            if (Menu == PlayerMenu.DeviceSettings) { BackFromSettings(); return; }
            if (Menu == PlayerMenu.InputSettings) { BackFromInputSettings(); return; }
            if (Menu == PlayerMenu.CameraComfort) { BackFromCameraComfort(); return; }
            if (Persistence != null && Persistence.BlocksPlay) return;
            if (!IsMenuOpen || !focused) return;
            Rescue?.Cancel();
            Menu = PlayerMenu.None;
            Station = null;
            StationRevision++;
            StationNotice = "";
            Time.timeScale = savedTimeScale;
            input?.SuppressHeldActions();
            holdInteraction?.Reset();
            transitionFrame = Time.frameCount;
            SetGameplayCursor();
            MenuChanged?.Invoke();
        }

        public void ShowSettings() => ShowSettingsCategory(SettingsCategory.Display);
        public void ShowCameraComfort() => ShowSettingsCategory(SettingsCategory.Accessibility);
        public void ShowInputSettings() => ShowSettingsCategory(SettingsCategory.Controls);

        public void ShowSettingsCategory(SettingsCategory category)
        {
            if (!focused || (!IsSettingsOpen && Menu != PlayerMenu.Pause && Menu != PlayerMenu.MainMenu)) return;
            if (BindingCapture.State != BindingCaptureState.Idle || GameSettings.PreviewingDisplay) return;
            CameraSettings.Flush(); InputSettings.Flush(); GameSettings.Flush();
            SettingsCategory = category;
            Menu = category == SettingsCategory.Accessibility ? PlayerMenu.CameraComfort
                : category == SettingsCategory.Controls ? PlayerMenu.InputSettings : PlayerMenu.DeviceSettings;
            input?.SuppressHeldActions();
            holdInteraction?.Reset();
            transitionFrame = Time.frameCount;
            MenuChanged?.Invoke();
        }

        public void BackFromCameraComfort() { if (Menu == PlayerMenu.CameraComfort) BackFromSettings(); }
        public void BackFromInputSettings() { if (Menu == PlayerMenu.InputSettings) BackFromSettings(); }

        public void BackFromSettings()
        {
            if (!focused || !IsSettingsOpen || BindingCapture.BlocksInput) return;
            if (GetComponent<FpsHud>()?.Menus?.DismissDropdown() == true) return;
            if (GameSettings.PreviewingDisplay) { GameSettings.RevertDisplay(); return; }
            if (BindingCapture.State != BindingCaptureState.Idle) { BindingCapture.Cancel(); return; }
            CameraSettings.Flush(); InputSettings.Flush(); GameSettings.Flush();
            Menu = Persistence != null && Persistence.AwaitingGameChoice ? PlayerMenu.MainMenu : PlayerMenu.Pause;
            input?.SuppressHeldActions();
            holdInteraction?.Reset();
            transitionFrame = Time.frameCount;
            MenuChanged?.Invoke();
        }

        public bool ExecuteStationCommand(int index) => ExecuteStationCommand(index, StationRevision);

        public bool ExecuteStationCommand(int index, long displayedRevision)
        {
            if (displayedRevision != StationRevision || index < 0 || Station == null || index >= Station.CommandCount) return false;
            if (!CanUseStation(Station))
            {
                StationNotice = "Station unavailable. Close and approach it again.";
                MenuChanged?.Invoke();
                return false;
            }
            bool result = Station.CanExecute(index, this) && Station.TryExecute(index, this);
            if (result) Persistence?.RequestCheckpoint();
            if (!result) StationNotice = "Offer changed. Review the current items and price.";
            RefreshStationOffers();
            MenuChanged?.Invoke();
            return result;
        }

        public void SetApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) { CameraSettings?.Flush(); BindingCapture?.Cancel(); InputSettings?.Flush(); }
            GameSettings?.SetFocus(hasFocus);
            focused = hasFocus;
            ResetJetpackHold();
            input?.SuppressHeldActions();
            holdInteraction?.Reset();
            if (!hasFocus && !IsMenuOpen) OpenMenu(PlayerMenu.Pause);
        }

        private void OnApplicationFocus(bool hasFocus) => SetApplicationFocus(hasFocus);
        private void OnApplicationPause(bool paused) => SetApplicationFocus(!paused);

        private static void SetGameplayCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void ResetJetpackHold()
        {
            jetpackHoldTime = 0f;
            IsJetpackActive = IsHovering = false;
        }

        private void OnDisable()
        {
            GraphicsTuner.Stop();
            discoveries?.SetXray(false, null);
            pickupPresentation?.Clear();
            proximityCollection?.Clear();
            loadRide?.Clear();
            GameSettings?.RevertDisplay();
            Rescue?.Cancel();
            BindingCapture?.Cancel();
            jetpackReadyInAir = false;
            ResetJetpackHold();
            input?.Disable();
            if (IsMenuOpen) Time.timeScale = savedTimeScale;
            Menu = PlayerMenu.None;
            Station = null;
            StationRevision++;
            if (ownsPresentation)
            {
                Cursor.lockState = savedCursorLock;
                Cursor.visible = savedCursorVisible;
                ownsPresentation = false;
            }
            MenuChanged?.Invoke();
        }

        private void OnApplicationQuit() { GameSettings?.RevertDisplay(); GameSettings?.Flush(); CameraSettings?.Flush(); InputSettings?.Flush(); }

        private void OnDestroy()
        {
            // The shading slider writes the shared renderer asset: never leave an admin value behind.
            ContactShading.Restore();
            pickupPresentation?.Dispose();
            input?.Dispose();
            if (CameraSettings != null) CameraSettings.Changed -= ApplyCameraPreferences;
            GraphicsTuner.Stop();
            GameSettings?.Dispose();
        }
    }
}
