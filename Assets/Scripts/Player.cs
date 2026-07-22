using UnityEngine;
using Fusion;

namespace AmongUsClone
{
    [DisallowMultipleComponent]
    public class Player : NetworkBehaviour
    {
        public const int KillButton = 0;
        public const int ReportButton = 1;
        public const int VoteButton = 2;
        public const int InteractButton = 3;
        public const int SabotageButton = 4;
        public const int VentButton = 5;
        public const int SkipVoteTarget = -1;

        private const string PlayerSortingLayerName = "Player";
        private const int DynamicSortingUnitsPerWorldUnit = 10;
        private const int ShadowSortingOrder = 7;
        private const int FootSortingOrder = 8;
        private const int BackpackSortingOrder = 9;
        private const int BodySortingOrder = 10;
        private const int HeadSortingOrder = 11;
        private const int WeaponSortingOrder = 12;

        private static readonly Color[] PlayerColors =
        {
            new Color(0.85f, 0.12f, 0.12f),
            new Color(0.1f, 0.34f, 0.88f),
            new Color(0.12f, 0.7f, 0.24f),
            new Color(0.95f, 0.78f, 0.12f),
            new Color(0.86f, 0.2f, 0.86f),
            new Color(0.1f, 0.78f, 0.78f),
            new Color(0.95f, 0.42f, 0.16f),
            new Color(0.88f, 0.88f, 0.88f),
            new Color(0.18f, 0.18f, 0.2f),
            new Color(0.55f, 0.3f, 0.95f),
        };

        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        [SerializeField] private float _moveSpeed = 4.5f;
        [SerializeField] private float _collisionRadius = 0.5f;
        [SerializeField] private float _killCooldownSeconds = 10f;
        [SerializeField] private float _proxyInterpolationSpeed = 18f;
        [SerializeField] private bool _followLocalPlayer = true;
        [SerializeField] private float _cameraFollowSpeed = 14f;
        [SerializeField] private float _lightsOutCameraSize = 3.2f;
        [SerializeField] private float _cameraSizeSpeed = 8f;
        [SerializeField] private float _walkAnimationSpeed = 10.5f;
        [SerializeField] private float _walkBobAmount = 0.06f;
        [SerializeField] private float _walkLeanDegrees = 5.5f;
        [SerializeField] private float _walkSquashAmount = 0.045f;
        [SerializeField] private Renderer _bodyRenderer;
        [SerializeField] private Sprite _characterBodySprite;
        [SerializeField] private Sprite _characterHeadSprite;
        [SerializeField] private Sprite _characterLeftFootSprite;
        [SerializeField] private Sprite _characterRightFootSprite;
        [SerializeField] private Sprite _characterWeaponSprite;
        [SerializeField] private Sprite _characterShadowSprite;

        [Networked] public Vector2 NetworkedPosition { get; private set; }
        [Networked] public int ColorIndex { get; private set; }
        [Networked] public int PlayerNumber { get; private set; }
        [Networked] public bool IsBot { get; private set; }
        [Networked] public PlayerRole Role { get; private set; }
        [Networked] public MatchState MatchState { get; private set; }
        [Networked] public WinningTeam WinningTeam { get; private set; }
        [Networked] public bool IsAlive { get; private set; }
        [Networked] public bool IsReportableBody { get; private set; }
        [Networked] public bool HasVoted { get; private set; }
        [Networked] public int VotedTargetNumber { get; private set; }
        [Networked] public int MeetingReporterNumber { get; private set; }
        [Networked] public int MeetingBodyNumber { get; private set; }
        [Networked] public int AssignedTaskCount { get; private set; }
        [Networked] public int AssignedTaskMask { get; private set; }
        [Networked] public int CompletedTaskMask { get; private set; }
        [Networked] public int LastAssignedTaskId { get; private set; }
        [Networked] public float TaskDeadlineEndsAt { get; private set; }
        [Networked] public bool TaskDeadlineFailed { get; private set; }
        [Networked] public float TaskFailureCutInEndsAt { get; private set; }
        [Networked] public int ActiveSabotageId { get; private set; }
        [Networked] public float SabotageEndsAt { get; private set; }
        [Networked] public int InteractionKindId { get; private set; }
        [Networked] public float InteractionProgress { get; private set; }
        [Networked] public float InteractionRequired { get; private set; }
        [Networked] public float KillFlashEndsAt { get; private set; }
        [Networked] public float MeetingEndsAt { get; private set; }
        [Networked] public int AnnouncementKindId { get; private set; }
        [Networked] public int AnnouncementPlayerNumber { get; private set; }
        [Networked] public int AnnouncementRoleId { get; private set; }
        [Networked] public float AnnouncementEndsAt { get; private set; }
        [Networked] private float LastKillTime { get; set; }
        [Networked] private NetworkButtons PreviousButtons { get; set; }

        private Camera _mainCamera;
        private MaterialPropertyBlock _visualPropertyBlock;
        private int _appliedColorIndex = -1;
        private bool _appliedAliveState = true;
        private bool _appliedFlashState;
        private Transform _bodyTransform;
        private Vector3 _aliveBodyScale;
        private Quaternion _aliveBodyRotation;
        private float _baseCameraOrthographicSize;
        private SpriteRenderer _bodySpriteRenderer;
        private SpriteRenderer _visorSpriteRenderer;
        private SpriteRenderer _backpackSpriteRenderer;
        private SpriteRenderer _shadowSpriteRenderer;
        private SpriteRenderer _headSpriteRenderer;
        private SpriteRenderer _leftFootSpriteRenderer;
        private SpriteRenderer _rightFootSpriteRenderer;
        private SpriteRenderer _weaponSpriteRenderer;
        private Vector3 _headRestPosition;
        private Vector3 _leftFootRestPosition;
        private Vector3 _rightFootRestPosition;
        private Vector3 _weaponRestPosition;
        private bool _usesImportedCharacterVisual;
        private Vector3 _lastRenderPosition;
        private Vector2 _smoothedVisualVelocity;
        private int _facingSign = 1;
        private float _walkCycle;
        private bool _hasRenderPosition;

        public bool IsLocalPlayer => Object != null && Object.HasInputAuthority;
        public bool IsImpostor => Role == PlayerRole.Impostor;
        public bool CanVote => MatchState == MatchState.Meeting && IsAlive && Role != PlayerRole.Spectator && !TaskDeadlineFailed;
        public string DisplayName => IsBot ? $"CPU {PlayerNumber}" : $"Player {PlayerNumber}";
        public float KillCooldownRemaining => Mathf.Max(0f, _killCooldownSeconds - (Time.time - LastKillTime));
        public bool IsKillReady => KillCooldownRemaining <= 0f;
        public bool CanDoTasks => MatchState == MatchState.Playing &&
            IsAlive &&
            (Role == PlayerRole.Crewmate || Role == PlayerRole.Impostor) &&
            AssignedTaskCount > 0 &&
            !TaskDeadlineFailed;
        public int CompletedTaskCount => CountTasks(CompletedTaskMask & AssignedTaskMask);
        public bool AllTasksComplete => AssignedTaskCount > 0 && CompletedTaskCount >= AssignedTaskCount;
        public SabotageType ActiveSabotage => (SabotageType)ActiveSabotageId;
        public InteractionKind ActiveInteraction => (InteractionKind)InteractionKindId;
        public float InteractionNormalized => InteractionRequired <= 0f ? 0f : Mathf.Clamp01(InteractionProgress / InteractionRequired);
        public AnnouncementType Announcement => (AnnouncementType)AnnouncementKindId;
        public PlayerRole AnnouncementRole => (PlayerRole)AnnouncementRoleId;

        private void Awake()
        {
            EnsureRuntimeVisuals();

            if (_bodyRenderer != null)
            {
                _bodyTransform ??= _bodyRenderer.transform;
                _aliveBodyScale = _bodyTransform.localScale;
                _aliveBodyRotation = _bodyTransform.localRotation;
            }
        }

        private void EnsureRuntimeVisuals()
        {
            if (_bodySpriteRenderer != null)
            {
                return;
            }

            var legacyRenderer = _bodyRenderer != null ? _bodyRenderer : GetComponentInChildren<Renderer>();
            if (legacyRenderer != null)
            {
                legacyRenderer.enabled = false;
            }

            if (_characterBodySprite != null && _characterHeadSprite != null)
            {
                CreateImportedCharacterVisuals();
                return;
            }

            CreateCrewmateVisuals();
        }

        private void CreateCrewmateVisuals()
        {
            var visualRoot = new GameObject("Crewmate Visual").transform;
            visualRoot.SetParent(transform, false);
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localScale = new Vector3(1.05f, 1.2f, 1f);

            _bodyTransform = visualRoot;
            _shadowSpriteRenderer = CreateSpritePart(
                transform,
                "Shadow",
                CrewmateSprites.Shadow,
                new Vector3(0f, -0.58f, 0f),
                new Vector3(0.8f, 0.3f, 1f),
                ShadowSortingOrder);
            _shadowSpriteRenderer.color = new Color(0f, 0f, 0f, 0.28f);
            _backpackSpriteRenderer = CreateSpritePart(
                visualRoot,
                "Backpack",
                CrewmateSprites.Backpack,
                new Vector3(-0.32f, -0.04f, 0f),
                new Vector3(0.75f, 0.95f, 1f),
                BackpackSortingOrder);
            _bodySpriteRenderer = CreateSpritePart(
                visualRoot,
                "Body",
                CrewmateSprites.Body,
                Vector3.zero,
                Vector3.one,
                BodySortingOrder);
            _visorSpriteRenderer = CreateSpritePart(
                visualRoot,
                "Visor",
                CrewmateSprites.Visor,
                new Vector3(0.13f, 0.14f, 0f),
                new Vector3(0.68f, 0.58f, 1f),
                HeadSortingOrder);
            _bodyRenderer = _bodySpriteRenderer;
        }

        private void CreateImportedCharacterVisuals()
        {
            var visualRoot = new GameObject("Jovial Soldier Visual").transform;
            visualRoot.SetParent(transform, false);
            visualRoot.localPosition = new Vector3(-0.1f, -0.03f, 0f);
            visualRoot.localScale = Vector3.one;

            _usesImportedCharacterVisual = true;
            _bodyTransform = visualRoot;
            _shadowSpriteRenderer = CreateSpritePart(
                transform,
                "Shadow",
                _characterShadowSprite != null ? _characterShadowSprite : CrewmateSprites.Shadow,
                new Vector3(0.05f, -0.56f, 0f),
                Vector3.one,
                ShadowSortingOrder);
            _shadowSpriteRenderer.color = new Color(0f, 0f, 0f, 0.28f);
            _leftFootSpriteRenderer = CreateSpritePart(
                visualRoot,
                "Left Foot",
                _characterLeftFootSprite,
                new Vector3(-0.073f, -0.386f, 0f),
                Vector3.one,
                FootSortingOrder);
            _rightFootSpriteRenderer = CreateSpritePart(
                visualRoot,
                "Right Foot",
                _characterRightFootSprite,
                new Vector3(0.36f, -0.386f, 0f),
                Vector3.one,
                FootSortingOrder);
            _bodySpriteRenderer = CreateSpritePart(
                visualRoot,
                "Body",
                _characterBodySprite,
                new Vector3(0.103f, -0.018f, 0f),
                Vector3.one,
                BodySortingOrder);
            _headSpriteRenderer = CreateSpritePart(
                visualRoot,
                "Head",
                _characterHeadSprite,
                new Vector3(0.119f, 0.577f, 0f),
                Vector3.one,
                HeadSortingOrder);

            if (_characterWeaponSprite != null)
            {
                _weaponSpriteRenderer = CreateSpritePart(
                    visualRoot,
                    "Weapon",
                    _characterWeaponSprite,
                    new Vector3(0.708f, 0.029f, 0f),
                    Vector3.one,
                    WeaponSortingOrder);
            }

            _headRestPosition = _headSpriteRenderer.transform.localPosition;
            _leftFootRestPosition = _leftFootSpriteRenderer.transform.localPosition;
            _rightFootRestPosition = _rightFootSpriteRenderer.transform.localPosition;
            _weaponRestPosition = _weaponSpriteRenderer != null
                ? _weaponSpriteRenderer.transform.localPosition
                : Vector3.zero;
            _bodyRenderer = _bodySpriteRenderer;
        }

        private static SpriteRenderer CreateSpritePart(
            Transform parent,
            string partName,
            Sprite sprite,
            Vector3 localPosition,
            Vector3 localScale,
            int sortingOrder)
        {
            var part = new GameObject(partName);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            var spriteRenderer = part.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingLayerName = PlayerSortingLayerName;
            spriteRenderer.sortingOrder = sortingOrder;
            return spriteRenderer;
        }

        public void Initialize(Vector2 spawnPosition, int colorIndex, bool isBot, int playerNumber)
        {
            NetworkedPosition = spawnPosition;
            ColorIndex = colorIndex;
            IsBot = isBot;
            PlayerNumber = playerNumber;
            ReturnToLobby(spawnPosition);
        }

        public void ReturnToLobby(Vector2 spawnPosition)
        {
            NetworkedPosition = spawnPosition;
            Role = PlayerRole.Unassigned;
            MatchState = MatchState.Lobby;
            WinningTeam = WinningTeam.None;
            IsAlive = true;
            IsReportableBody = false;
            HasVoted = false;
            VotedTargetNumber = SkipVoteTarget;
            MeetingReporterNumber = 0;
            MeetingBodyNumber = 0;
            AssignedTaskCount = 0;
            AssignedTaskMask = 0;
            CompletedTaskMask = 0;
            LastAssignedTaskId = -1;
            TaskDeadlineEndsAt = 0f;
            TaskDeadlineFailed = false;
            TaskFailureCutInEndsAt = 0f;
            ClearSabotage();
            ClearInteraction();
            ClearAnnouncement();
            MeetingEndsAt = 0f;
            KillFlashEndsAt = 0f;
            LastKillTime = -_killCooldownSeconds;
            ResetMovementAnimation();
        }

        public void BeginRound(PlayerRole role, Vector2 spawnPosition, int taskMask, float taskDeadlineEndsAt)
        {
            NetworkedPosition = spawnPosition;
            Role = role;
            MatchState = MatchState.Playing;
            WinningTeam = WinningTeam.None;
            IsAlive = role != PlayerRole.Spectator;
            IsReportableBody = false;
            HasVoted = false;
            VotedTargetNumber = SkipVoteTarget;
            MeetingReporterNumber = 0;
            MeetingBodyNumber = 0;
            AssignedTaskMask = role == PlayerRole.Crewmate || role == PlayerRole.Impostor ? taskMask & 0x3fffffff : 0;
            AssignedTaskCount = CountTasks(AssignedTaskMask);
            CompletedTaskMask = 0;
            LastAssignedTaskId = -1;
            TaskDeadlineEndsAt = taskDeadlineEndsAt;
            TaskDeadlineFailed = false;
            TaskFailureCutInEndsAt = 0f;
            ClearSabotage();
            ClearInteraction();
            ClearAnnouncement();
            MeetingEndsAt = 0f;
            KillFlashEndsAt = 0f;
            LastKillTime = -_killCooldownSeconds;
            ResetMovementAnimation();
        }

        public void EnterMeeting(int reporterNumber, int bodyNumber, float meetingEndsAt)
        {
            MatchState = MatchState.Meeting;
            HasVoted = false;
            VotedTargetNumber = SkipVoteTarget;
            MeetingReporterNumber = reporterNumber;
            MeetingBodyNumber = bodyNumber;
            MeetingEndsAt = meetingEndsAt;
            ClearAnnouncement();
            ClearInteraction();
        }

        public void ResumeAfterMeeting()
        {
            if (MatchState != MatchState.Ended)
            {
                MatchState = MatchState.Playing;
            }

            HasVoted = false;
            VotedTargetNumber = SkipVoteTarget;
            MeetingReporterNumber = 0;
            MeetingBodyNumber = 0;
            MeetingEndsAt = 0f;
            ClearInteraction();
        }

        public void EndRound(WinningTeam winningTeam)
        {
            MatchState = MatchState.Ended;
            WinningTeam = winningTeam;
            HasVoted = false;
            MeetingEndsAt = 0f;
            ClearSabotage();
            ClearInteraction();
        }

        public void Kill()
        {
            IsAlive = false;
            IsReportableBody = true;
            ClearInteraction();
            KillFlashEndsAt = Time.time + 0.65f;
        }

        public void Exile()
        {
            IsAlive = false;
            IsReportableBody = false;
            ClearInteraction();
        }

        public void MarkBodyReported()
        {
            IsReportableBody = false;
        }

        public void MarkVoted(int targetNumber)
        {
            HasVoted = true;
            VotedTargetNumber = targetNumber;
        }

        public void MarkKillUsed()
        {
            LastKillTime = Time.time;
        }

        public bool HasAssignedTask(int taskId)
        {
            return taskId >= 0 && taskId < 30 && (AssignedTaskMask & (1 << taskId)) != 0;
        }

        public bool HasCompletedTask(int taskId)
        {
            return HasAssignedTask(taskId) && (CompletedTaskMask & (1 << taskId)) != 0;
        }

        public bool CompleteTask(int taskId)
        {
            if (!Object.HasStateAuthority || !CanDoTasks || !HasAssignedTask(taskId) || HasCompletedTask(taskId))
            {
                return false;
            }

            CompletedTaskMask |= 1 << taskId;
            return true;
        }

        public bool AssignTask(int taskId)
        {
            if (!Object.HasStateAuthority || Role != PlayerRole.Crewmate || taskId < 0 || taskId >= 30 || HasAssignedTask(taskId))
            {
                return false;
            }

            AssignedTaskMask |= 1 << taskId;
            AssignedTaskCount = CountTasks(AssignedTaskMask);
            LastAssignedTaskId = taskId;
            return true;
        }

        public void SetTaskFailureCutIn(float cutInEndsAt)
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            TaskDeadlineFailed = true;
            TaskFailureCutInEndsAt = cutInEndsAt;
            if (MatchState == MatchState.Meeting)
            {
                ResumeAfterMeeting();
            }

            ClearInteraction();
        }

        public void SetSabotage(SabotageType sabotageType, float endsAt)
        {
            ActiveSabotageId = (int)sabotageType;
            SabotageEndsAt = endsAt;
        }

        public void ClearSabotage()
        {
            ActiveSabotageId = (int)SabotageType.None;
            SabotageEndsAt = 0f;
        }

        public void SetInteraction(InteractionKind kind, float progress, float required)
        {
            InteractionKindId = (int)kind;
            InteractionProgress = progress;
            InteractionRequired = required;
        }

        public void ClearInteraction()
        {
            InteractionKindId = (int)InteractionKind.None;
            InteractionProgress = 0f;
            InteractionRequired = 0f;
        }

        public void SetAnnouncement(AnnouncementType type, int playerNumber, PlayerRole role, float endsAt)
        {
            AnnouncementKindId = (int)type;
            AnnouncementPlayerNumber = playerNumber;
            AnnouncementRoleId = (int)role;
            AnnouncementEndsAt = endsAt;
        }

        public void ClearAnnouncement()
        {
            AnnouncementKindId = (int)AnnouncementType.None;
            AnnouncementPlayerNumber = 0;
            AnnouncementRoleId = (int)PlayerRole.Unassigned;
            AnnouncementEndsAt = 0f;
        }

        public void Teleport(Vector2 position)
        {
            if (Object.HasStateAuthority)
            {
                NetworkedPosition = ShipMap.ClampToWalkable(position, _collisionRadius);
                ResetMovementAnimation();
            }
        }

        public override void Spawned()
        {
            if (Object.HasStateAuthority && NetworkedPosition == default)
            {
                NetworkedPosition = new Vector2(transform.position.x, transform.position.y);
            }

            if (Object.HasInputAuthority && _followLocalPlayer)
            {
                _mainCamera = Camera.main;
                if (_mainCamera != null && _mainCamera.orthographic)
                {
                    _baseCameraOrthographicSize = _mainCamera.orthographicSize;
                }
            }

            ApplyNetworkedPosition(true);
            ApplyVisuals();
            ResetMovementAnimation();
            ApplyMovementAnimation();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            if (GetInput(out NetworkInputData data))
            {
                if (CanMove)
                {
                    var direction = data.Direction;
                    if (direction.sqrMagnitude > 1f)
                    {
                        direction.Normalize();
                    }

                    NetworkedPosition = ShipMap.ResolveMove(
                        NetworkedPosition,
                        direction * _moveSpeed * Runner.DeltaTime,
                        _collisionRadius);
                }

                if (TaskDeadlineFailed)
                {
                    BasicSpawner.Active?.UpdateHeldInteract(this, Runner.DeltaTime, false);
                    PreviousButtons = data.Buttons;
                    return;
                }

                if (data.Buttons.WasPressed(PreviousButtons, KillButton))
                {
                    TryUseKill();
                }

                if (data.Buttons.WasPressed(PreviousButtons, ReportButton))
                {
                    TryUseReport();
                }

                if (data.Buttons.WasPressed(PreviousButtons, VoteButton))
                {
                    TryUseVote(data.VoteTargetNumber);
                }

                if (data.Buttons.WasPressed(PreviousButtons, InteractButton))
                {
                    TryUseInteract();
                }

                if (data.Buttons.WasPressed(PreviousButtons, SabotageButton))
                {
                    TryUseSabotage();
                }

                if (data.Buttons.WasPressed(PreviousButtons, VentButton))
                {
                    TryUseVent();
                }

                BasicSpawner.Active?.UpdateHeldInteract(
                    this,
                    Runner.DeltaTime,
                    data.Buttons.IsSet(InteractButton));

                PreviousButtons = data.Buttons;
            }
        }

        public override void Render()
        {
            ApplyNetworkedPosition(Object.HasStateAuthority);
            ApplyVisuals();
            ApplyMovementAnimation();
            ApplyDynamicSorting();
            FollowCamera();
        }

        public void MoveBy(Vector2 delta)
        {
            if (Object.HasStateAuthority && CanMove)
            {
                NetworkedPosition = ShipMap.ResolveMove(NetworkedPosition, delta, _collisionRadius);
            }
        }

        private bool CanMove => MatchState == MatchState.Lobby || (MatchState == MatchState.Playing && IsAlive && !TaskDeadlineFailed);

        private void TryUseKill()
        {
            if (MatchState != MatchState.Playing || !IsAlive || !IsImpostor || !IsKillReady)
            {
                return;
            }

            var spawner = BasicSpawner.Active;
            if (spawner != null && spawner.TryKill(this))
            {
                MarkKillUsed();
            }
        }

        private void TryUseReport()
        {
            if (MatchState != MatchState.Playing || !IsAlive)
            {
                return;
            }

            BasicSpawner.Active?.TryReport(this);
        }

        private void TryUseVote(int targetNumber)
        {
            if (!CanVote)
            {
                return;
            }

            BasicSpawner.Active?.TrySubmitVote(this, targetNumber);
        }

        private void TryUseInteract()
        {
            BasicSpawner.Active?.TryInteract(this);
        }

        private void TryUseSabotage()
        {
            if (MatchState != MatchState.Playing || !IsAlive || !IsImpostor)
            {
                return;
            }

            BasicSpawner.Active?.TryStartSabotage(this);
        }

        private void TryUseVent()
        {
            if (MatchState != MatchState.Playing || !IsAlive || !IsImpostor)
            {
                return;
            }

            BasicSpawner.Active?.TryUseVent(this);
        }

        private static int CountTasks(int taskMask)
        {
            var count = 0;
            for (var taskId = 0; taskId < 30; taskId++)
            {
                if ((taskMask & (1 << taskId)) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private void ApplyNetworkedPosition(bool snap)
        {
            var target = new Vector3(NetworkedPosition.x, NetworkedPosition.y, transform.position.z);

            transform.position = snap
                ? target
                : Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-_proxyInterpolationSpeed * Time.deltaTime));
        }

        private void ResetMovementAnimation()
        {
            _lastRenderPosition = transform.position;
            _smoothedVisualVelocity = Vector2.zero;
            _walkCycle = 0f;
            _hasRenderPosition = false;
        }

        private void ApplyMovementAnimation()
        {
            if (_bodyTransform == null)
            {
                return;
            }

            if (!_hasRenderPosition)
            {
                _lastRenderPosition = transform.position;
                _hasRenderPosition = true;
            }

            var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            var rawVelocity3 = (transform.position - _lastRenderPosition) / deltaTime;
            _lastRenderPosition = transform.position;

            var rawVelocity = new Vector2(rawVelocity3.x, rawVelocity3.y);
            _smoothedVisualVelocity = Vector2.Lerp(
                _smoothedVisualVelocity,
                rawVelocity,
                1f - Mathf.Exp(-18f * deltaTime));

            var speed = _smoothedVisualVelocity.magnitude;
            var isFlashing = Time.time < KillFlashEndsAt;
            var isWalking = IsAlive && !isFlashing && CanMove && speed > 0.08f;

            if (IsAlive && Mathf.Abs(_smoothedVisualVelocity.x) > 0.06f)
            {
                _facingSign = _smoothedVisualVelocity.x < 0f ? -1 : 1;
            }

            if (isWalking)
            {
                var normalizedSpeed = Mathf.Clamp01(speed / Mathf.Max(_moveSpeed, 0.1f));
                _walkCycle += deltaTime * Mathf.Lerp(_walkAnimationSpeed * 0.65f, _walkAnimationSpeed * 1.25f, normalizedSpeed);
            }
            else
            {
                _walkCycle = Mathf.Lerp(_walkCycle, 0f, 1f - Mathf.Exp(-8f * deltaTime));
                _smoothedVisualVelocity = Vector2.Lerp(_smoothedVisualVelocity, Vector2.zero, 1f - Mathf.Exp(-10f * deltaTime));
            }

            if (!IsAlive)
            {
                _bodyTransform.localPosition = Vector3.zero;
                _bodyTransform.localScale = new Vector3(_aliveBodyScale.x * 1.35f * _facingSign, _aliveBodyScale.y * 0.28f, _aliveBodyScale.z);
                _bodyTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                ResetCharacterPartAnimation();
                SetShadow(false, 0f, 1f);
                return;
            }

            if (isFlashing)
            {
                _bodyTransform.localPosition = Vector3.zero;
                _bodyTransform.localScale = new Vector3(_aliveBodyScale.x * 1.25f * _facingSign, _aliveBodyScale.y * 1.25f, _aliveBodyScale.z);
                _bodyTransform.localRotation = Quaternion.Euler(0f, 0f, 20f * _facingSign);
                ResetCharacterPartAnimation();
                SetShadow(true, 0.2f, 1.05f);
                return;
            }

            var wave = Mathf.Sin(_walkCycle * Mathf.PI * 2f);
            var step = Mathf.Abs(wave);
            var bob = isWalking ? step * _walkBobAmount : 0f;
            var squash = isWalking ? step * _walkSquashAmount : 0f;
            var lean = isWalking ? -wave * _walkLeanDegrees : 0f;
            var sideSway = isWalking ? wave * 0.025f * _facingSign : 0f;

            _bodyTransform.localPosition = new Vector3(sideSway, bob, 0f);
            _bodyTransform.localScale = new Vector3(
                _aliveBodyScale.x * _facingSign * (1f + squash * 0.45f),
                _aliveBodyScale.y * (1f - squash),
                _aliveBodyScale.z);
            _bodyTransform.localRotation = Quaternion.Euler(0f, 0f, lean);

            ApplyCharacterPartAnimation(isWalking, wave, bob);
            SetShadow(true, bob, 1f + squash);
        }

        private void ApplyCharacterPartAnimation(bool isWalking, float wave, float bob)
        {
            if (!_usesImportedCharacterVisual)
            {
                return;
            }

            var footStride = isWalking ? wave * 0.055f : 0f;
            var footLift = isWalking ? Mathf.Abs(wave) * 0.045f : 0f;
            _leftFootSpriteRenderer.transform.localPosition = _leftFootRestPosition + new Vector3(footStride, footLift, 0f);
            _rightFootSpriteRenderer.transform.localPosition = _rightFootRestPosition + new Vector3(-footStride, -footLift * 0.35f, 0f);
            _headSpriteRenderer.transform.localPosition = _headRestPosition + new Vector3(0f, bob * 0.45f, 0f);

            if (_weaponSpriteRenderer != null)
            {
                _weaponSpriteRenderer.transform.localPosition = _weaponRestPosition + new Vector3(0f, bob * 0.3f, 0f);
                _weaponSpriteRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, isWalking ? -wave * 2.5f : 0f);
            }
        }

        private void ResetCharacterPartAnimation()
        {
            if (!_usesImportedCharacterVisual)
            {
                return;
            }

            _headSpriteRenderer.transform.localPosition = _headRestPosition;
            _leftFootSpriteRenderer.transform.localPosition = _leftFootRestPosition;
            _rightFootSpriteRenderer.transform.localPosition = _rightFootRestPosition;

            if (_weaponSpriteRenderer != null)
            {
                _weaponSpriteRenderer.transform.localPosition = _weaponRestPosition;
                _weaponSpriteRenderer.transform.localRotation = Quaternion.identity;
            }
        }

        private void ApplyDynamicSorting()
        {
            var offset = Mathf.RoundToInt(-transform.position.y * DynamicSortingUnitsPerWorldUnit);
            SetSpriteSorting(_shadowSpriteRenderer, offset + ShadowSortingOrder);
            SetSpriteSorting(_leftFootSpriteRenderer, offset + FootSortingOrder);
            SetSpriteSorting(_rightFootSpriteRenderer, offset + FootSortingOrder);
            SetSpriteSorting(_backpackSpriteRenderer, offset + BackpackSortingOrder);
            SetSpriteSorting(_bodySpriteRenderer, offset + BodySortingOrder);
            SetSpriteSorting(_visorSpriteRenderer, offset + HeadSortingOrder);
            SetSpriteSorting(_headSpriteRenderer, offset + HeadSortingOrder);
            SetSpriteSorting(_weaponSpriteRenderer, offset + WeaponSortingOrder);
        }

        private static void SetSpriteSorting(SpriteRenderer spriteRenderer, int sortingOrder)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.sortingLayerName = PlayerSortingLayerName;
            spriteRenderer.sortingOrder = sortingOrder;
        }

        private void SetShadow(bool visible, float bob, float scale)
        {
            if (_shadowSpriteRenderer == null)
            {
                return;
            }

            _shadowSpriteRenderer.enabled = visible;
            if (!visible)
            {
                return;
            }

            _shadowSpriteRenderer.transform.localScale = new Vector3(0.8f * scale, 0.3f * Mathf.Max(0.75f, 1f - bob * 1.7f), 1f);
            ApplyRendererColor(_shadowSpriteRenderer, new Color(0f, 0f, 0f, Mathf.Clamp(0.26f - bob * 1.4f, 0.14f, 0.28f)));
        }

        private void FollowCamera()
        {
            if (_mainCamera == null)
            {
                return;
            }

            var cameraTransform = _mainCamera.transform;
            var target = new Vector3(transform.position.x, transform.position.y, cameraTransform.position.z);
            if (_mainCamera.orthographic)
            {
                target = ShipMap.ClampCameraPosition(target, _mainCamera);
            }

            cameraTransform.position = Vector3.Lerp(
                cameraTransform.position,
                target,
                1f - Mathf.Exp(-_cameraFollowSpeed * Time.deltaTime));

            if (_mainCamera.orthographic)
            {
                if (_baseCameraOrthographicSize <= 0f)
                {
                    _baseCameraOrthographicSize = _mainCamera.orthographicSize;
                }

                var targetSize = ActiveSabotage == SabotageType.Lights &&
                    MatchState == MatchState.Playing &&
                    Role == PlayerRole.Crewmate &&
                    IsAlive
                        ? _lightsOutCameraSize
                        : _baseCameraOrthographicSize;

                _mainCamera.orthographicSize = Mathf.Lerp(
                    _mainCamera.orthographicSize,
                    targetSize,
                    1f - Mathf.Exp(-_cameraSizeSpeed * Time.deltaTime));

                cameraTransform.position = ShipMap.ClampCameraPosition(cameraTransform.position, _mainCamera);
            }
        }

        private void ApplyVisuals()
        {
            var isFlashing = Time.time < KillFlashEndsAt;
            if (_bodyRenderer == null ||
                (_appliedColorIndex == ColorIndex && _appliedAliveState == IsAlive && _appliedFlashState == isFlashing))
            {
                return;
            }

            var color = PlayerColors[Mathf.Abs(ColorIndex) % PlayerColors.Length];

            if (isFlashing)
            {
                color = Color.Lerp(color, Color.white, 0.65f);
            }
            else if (!IsAlive)
            {
                color = Color.Lerp(color, Color.black, 0.65f);
            }

            if (_bodySpriteRenderer != null)
            {
                if (_usesImportedCharacterVisual)
                {
                    ApplyImportedCharacterColors(color, isFlashing);
                }
                else
                {
                    ApplyRendererColor(_bodySpriteRenderer, color);
                    if (_backpackSpriteRenderer != null)
                    {
                        ApplyRendererColor(_backpackSpriteRenderer, Color.Lerp(color, Color.black, 0.3f));
                    }

                    if (_visorSpriteRenderer != null)
                    {
                        _visorSpriteRenderer.enabled = IsAlive || isFlashing;
                        ApplyRendererColor(_visorSpriteRenderer, isFlashing
                            ? Color.white
                            : new Color(0.65f, 0.88f, 1f, 1f));
                    }
                }
            }
            else if (_bodyRenderer != null)
            {
                ApplyRendererColor(_bodyRenderer, color);
            }

            if (_bodyTransform != null)
            {
                if (isFlashing)
                {
                    _bodyTransform.localScale = _aliveBodyScale * 1.25f;
                    _bodyTransform.localRotation = Quaternion.Euler(0f, 0f, 20f);
                }
                else
                {
                    _bodyTransform.localScale = IsAlive
                        ? _aliveBodyScale
                        : new Vector3(_aliveBodyScale.x * 1.35f, _aliveBodyScale.y * 0.28f, _aliveBodyScale.z);
                    _bodyTransform.localRotation = IsAlive
                        ? _aliveBodyRotation
                        : Quaternion.Euler(0f, 0f, 90f);
                }
            }

            _appliedColorIndex = ColorIndex;
            _appliedAliveState = IsAlive;
            _appliedFlashState = isFlashing;
        }

        private void ApplyImportedCharacterColors(Color playerColor, bool isFlashing)
        {
            var neutralColor = Color.white;
            if (!IsAlive)
            {
                neutralColor = Color.Lerp(Color.white, Color.black, 0.65f);
            }
            else if (isFlashing)
            {
                neutralColor = Color.white;
            }

            ApplyRendererColor(_bodySpriteRenderer, playerColor);
            ApplyRendererColor(_leftFootSpriteRenderer, Color.Lerp(playerColor, Color.black, 0.12f));
            ApplyRendererColor(_rightFootSpriteRenderer, Color.Lerp(playerColor, Color.black, 0.12f));
            ApplyRendererColor(_headSpriteRenderer, neutralColor);

            if (_weaponSpriteRenderer != null)
            {
                ApplyRendererColor(_weaponSpriteRenderer, neutralColor);
            }
        }

        private void ApplyRendererColor(SpriteRenderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.color = color;
            ApplyRendererPropertyBlock(renderer, color);
        }

        private void ApplyRendererColor(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            if (renderer is SpriteRenderer spriteRenderer)
            {
                spriteRenderer.color = color;
            }

            ApplyRendererPropertyBlock(renderer, color);
        }

        private void ApplyRendererPropertyBlock(Renderer renderer, Color color)
        {
            _visualPropertyBlock ??= new MaterialPropertyBlock();
            _visualPropertyBlock.Clear();
            renderer.GetPropertyBlock(_visualPropertyBlock);
            _visualPropertyBlock.SetColor(BaseColorPropertyId, color);
            _visualPropertyBlock.SetColor(ColorPropertyId, color);
            renderer.SetPropertyBlock(_visualPropertyBlock);
        }
    }
}
