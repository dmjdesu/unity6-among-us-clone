using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AmongUsClone
{
    public struct NetworkInputData : INetworkInput
    {
        public Vector2 Direction;
        public NetworkButtons Buttons;
        public int VoteTargetNumber;
    }

    public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static BasicSpawner Active { get; private set; }

        private const string RoomNamePrefsKey = "AmongUsClone.RoomName";
        private const string TaskMarkerRootName = "Task Stations";
        private const string SabotageMarkerRootName = "Sabotage Stations";
        private const string VentMarkerRootName = "Vents";
        private const string EmergencyMarkerRootName = "Emergency Station";
        private const int CircuitPulseCount = 4;
        private const float CalibrationTargetStart = 0.7f;
        private const float CalibrationTargetEnd = 0.82f;
        private static readonly Vector2 SafeSpawnOrigin = new Vector2(-1.8f, 0.35f);
        private static readonly Vector2 SafeSpawnSpacing = new Vector2(0.9f, 0.75f);

        private struct ChannelState
        {
            public InteractionKind Kind;
            public int TargetId;
            public float Progress;
            public bool WasHeld;

            public ChannelState(InteractionKind kind, int targetId)
            {
                Kind = kind;
                TargetId = targetId;
                Progress = 0f;
                WasHeld = false;
            }
        }

        private static TaskStation[] TaskStations => ShipMap.TaskStations;
        private static SabotageStation[] SabotageStations => ShipMap.SabotageStations;
        private static VentStation[] VentStations => ShipMap.VentStations;
        private static Vector2 EmergencyStationPosition => ShipMap.MeetingPoint;

        [Header("Session")]
        [SerializeField] private string _defaultRoomName = "among-us-style";
        [SerializeField] private int _maxPlayerCount = 10;

        [Header("Game")]
        [SerializeField] private int _minimumPlayersToStart = 1;
        [SerializeField] private int _impostorCount = 1;
        [SerializeField] private bool _preferHostAsImpostorForTesting = true;
        [SerializeField] private int _testCpuPlayerCount = 5;
        [SerializeField] private float _killRange = 1.45f;
        [SerializeField] private float _reportRange = 1.7f;
        [SerializeField] private float _meetingDurationSeconds = 30f;
        [SerializeField] private float _botMoveSpeed = 1.6f;
        [SerializeField] private float _botVoteDelay = 1.5f;
        [SerializeField] private float _botVoteInterval = 0.45f;
        [SerializeField] private float _taskUseRange = 1.1f;
        [SerializeField] private float _botTaskPauseSeconds = 3.5f;
        [SerializeField] private float _taskMarkerScale = 0.75f;
        [SerializeField] private float _sabotageUseRange = 1.2f;
        [SerializeField] private float _sabotageCooldownSeconds = 18f;
        [SerializeField] private float _reactorCountdownSeconds = 45f;
        [SerializeField] private float _ventUseRange = 0.9f;
        [SerializeField] private float _emergencyUseRange = 1.15f;
        [SerializeField] private float _emergencyCooldownSeconds = 25f;
        [SerializeField, Range(1, 10)] private int _tasksPerCrewmate = 5;
        [SerializeField, Min(1f)] private float _firstTimedTaskDelaySeconds = 30f;
        [SerializeField, Min(1f)] private float _timedTaskIntervalSeconds = 45f;
        [SerializeField, Range(0, 5)] private int _maxTimedTaskWaves = 2;
        [SerializeField, Min(30f)] private float _taskDeadlineSeconds = 180f;
        [SerializeField, Min(1f)] private float _taskFailureCutInSeconds = 4f;
        [SerializeField, Min(0.1f)] private float _taskChannelSeconds = 1.35f;
        [SerializeField, Min(0.5f)] private float _calibrationCycleSeconds = 2.2f;
        [SerializeField] private float _repairChannelSeconds = 2f;
        [SerializeField] private float _announcementSeconds = 4.5f;
        [SerializeField] private float _botImpostorThinkSeconds = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _botImpostorSabotageChance = 0.3f;
        [SerializeField] private bool _showLegacyDebugGui;

        [Header("Player")]
        [SerializeField] private NetworkPrefabRef _playerPrefab;
        [SerializeField] private Vector2 _spawnOrigin = new Vector2(-1.8f, 0.35f);
        [SerializeField] private Vector2 _spawnSpacing = new Vector2(0.9f, 0.75f);
        [SerializeField] private int _spawnColumns = 5;

        private NetworkRunner _runner;
        private string _roomName;
        private string _status = "Disconnected";
        private bool _isStarting;
        private bool _roundStarted;
        private readonly Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
        private readonly List<NetworkObject> _botCharacters = new List<NetworkObject>();
        private readonly Dictionary<int, Vector2> _botTargets = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, float> _botTaskCooldowns = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _botActionCooldowns = new Dictionary<int, float>();
        private readonly Dictionary<int, ChannelState> _channels = new Dictionary<int, ChannelState>();
        private readonly Dictionary<int, int> _votes = new Dictionary<int, int>();
        private readonly Dictionary<int, Renderer> _taskMarkerRenderers = new Dictionary<int, Renderer>();
        private int _nextPlayerNumber = 1;
        private int? _queuedVoteTargetNumber;
        private bool _meetingActive;
        private float _meetingEndsAt;
        private float _nextBotVoteAt;
        private SabotageType _activeSabotage = SabotageType.None;
        private float _activeSabotageEndsAt;
        private float _nextSabotageAllowedAt;
        private float _nextEmergencyAllowedAt;
        private float _nextTimedTaskAt;
        private int _timedTaskWavesIssued;
        private float _taskDeadlineEndsAt;
        private bool _taskFailurePending;
        private float _taskFailureCutInEndsAt;

        public string RoomName
        {
            get => _roomName;
            set => _roomName = value ?? string.Empty;
        }

        public string StatusText => _status;
        public bool IsConnected => _runner != null;
        public bool IsStarting => _isStarting;
        public bool IsServer => _runner != null && _runner.IsServer;
        public bool RoundStarted => _roundStarted;
        public string SessionName => _runner != null ? _runner.SessionInfo.Name : SanitizeRoomName(_roomName);
        public string GameModeText => _runner != null ? _runner.GameMode.ToString() : "Disconnected";
        public int VisiblePlayerCount => GetVisiblePlayerCount();
        public int VisibleCpuCount => GetVisibleCpuCount();
        public Player LocalPlayer => GetLocalPlayer();
        public Player[] KnownPlayers => GetKnownPlayers();
        public int VoteCount => _votes.Count;
        public int EligibleVoterCount => IsServer ? GetEligibleVoterCount() : CountVisibleEligibleVoters();
        public float MeetingDurationSeconds => _meetingDurationSeconds;
        public float TaskFailureCutInDurationSeconds => _taskFailureCutInSeconds;
        public bool PreferHostAsImpostorForTesting
        {
            get => _preferHostAsImpostorForTesting;
            set => _preferHostAsImpostorForTesting = value;
        }

        public bool CanStartRound => IsServer &&
            GetSpawnedPlayers().Count >= _minimumPlayersToStart &&
            GetSpawnedPlayers().Count > 0;

        private void Awake()
        {
            Active = this;
            _roomName = PlayerPrefs.GetString(RoomNamePrefsKey, _defaultRoomName);
        }

        private void Start()
        {
            EnsureRuntimeHud();
            ShipMap.EnsureVisuals();
            EnsureTaskStationMarkers();

            if (!ShipMap.UsesSceneProvidedEnvironment)
            {
                EnsureSabotageStationMarkers();
                EnsureVentMarkers();
                EnsureEmergencyMarker();
            }
        }

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        private void Update()
        {
            UpdateTaskStationMarkers();

            if (_runner == null || !_runner.IsServer || !_roundStarted)
            {
                return;
            }

            if (_taskFailurePending)
            {
                if (Time.time >= _taskFailureCutInEndsAt)
                {
                    EndRound(WinningTeam.Impostors);
                }

                return;
            }

            if (_taskDeadlineEndsAt > 0f && Time.time >= _taskDeadlineEndsAt)
            {
                BeginTaskFailureCutIn();
                return;
            }

            if (_meetingActive)
            {
                UpdateBotVoting();

                if (Time.time >= _meetingEndsAt)
                {
                    ResolveMeeting();
                }

                return;
            }

            if (_activeSabotage == SabotageType.Reactor && Time.time >= _activeSabotageEndsAt)
            {
                _status = "Reactor meltdown completed.";
                EndRound(WinningTeam.Impostors);
                return;
            }

            UpdateTimedTaskAssignments();
            UpdateBots();
        }

        async void StartGame(GameMode mode)
        {
            if (_isStarting || _runner != null)
            {
                return;
            }

            _isStarting = true;
            _status = $"Starting {mode}...";
            PlayerPrefs.SetString(RoomNamePrefsKey, _roomName);

            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
            _runner.AddCallbacks(this);
            Active = this;

            var activeScene = SceneManager.GetActiveScene();
            var sceneInfo = new NetworkSceneInfo();
            var hasScene = false;
            if (activeScene.buildIndex >= 0)
            {
                sceneInfo.AddSceneRef(SceneRef.FromIndex(activeScene.buildIndex), LoadSceneMode.Additive);
                hasScene = true;
            }

            var args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = SanitizeRoomName(_roomName),
                PlayerCount = _maxPlayerCount,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            };

            if (hasScene)
            {
                args.Scene = sceneInfo;
            }

            var result = await _runner.StartGame(args);
            _isStarting = false;

            if (result.Ok)
            {
                _status = $"Connected to {_runner.SessionInfo.Name} as {_runner.GameMode}";
                return;
            }

            _status = $"Start failed: {result.ShutdownReason}";
            CleanupRunner();
        }

        public void RequestHost()
        {
            StartGame(GameMode.Host);
        }

        public void RequestJoin()
        {
            StartGame(GameMode.Client);
        }

        public void RequestAutoHostOrJoin()
        {
            StartGame(GameMode.AutoHostOrClient);
        }

        public void RequestStartOrRestartRound()
        {
            StartRound();
        }

        public void RequestLeave()
        {
            Shutdown();
        }

        public void RequestVote(int targetNumber)
        {
            _queuedVoteTargetNumber = targetNumber;
        }

        private void OnGUI()
        {
            if (!_showLegacyDebugGui)
            {
                return;
            }

            const float width = 330f;
            var height = Mathf.Max(360f, Screen.height - 32f);
            GUILayout.BeginArea(new Rect(16f, 16f, width, height), GUI.skin.box);
            GUILayout.Label("Among Us Style - Fusion Test");

            if (_runner == null || _isStarting)
            {
                GUILayout.Label("Room");
                _roomName = GUILayout.TextField(_roomName, 32);

                GUI.enabled = !_isStarting;
                if (GUILayout.Button("Host"))
                {
                    StartGame(GameMode.Host);
                }

                if (GUILayout.Button("Join"))
                {
                    StartGame(GameMode.Client);
                }

                if (GUILayout.Button("Auto Host Or Join"))
                {
                    StartGame(GameMode.AutoHostOrClient);
                }

                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label($"Room: {_runner.SessionInfo.Name}");
                GUILayout.Label($"Mode: {_runner.GameMode}");
                GUILayout.Label($"Participants: {GetVisiblePlayerCount()} ({GetVisibleCpuCount()} CPU)");

                DrawMatchStatus();
                DrawRoster(GetLocalPlayer());

                if (_runner.IsServer)
                {
                    DrawHostControls();
                }

                if (GUILayout.Button("Leave"))
                {
                    Shutdown();
                }
            }

            GUILayout.Space(8f);
            GUILayout.Label(_status);
            GUILayout.Label("Move: WASD / Arrow Keys");
            GUILayout.Label("Kill: Q (Impostor only)");
            GUILayout.Label("Report: R near a body");
            GUILayout.Label("Interact/Task/Fix: E");
            GUILayout.Label("Emergency Meeting: E");
            GUILayout.Label("Sabotage: F (Impostor)");
            GUILayout.Label("Vent: V (Impostor)");
            GUILayout.EndArea();
        }

        private void DrawMatchStatus()
        {
            var localPlayer = GetLocalPlayer();
            if (localPlayer == null)
            {
                GUILayout.Label("State: Spawning");
                return;
            }

            GUILayout.Label($"State: {localPlayer.MatchState}");
            if (localPlayer.MatchState == MatchState.Playing)
            {
                GUILayout.Label($"You are: {localPlayer.Role}");
                GUILayout.Label(localPlayer.IsAlive ? "Status: Alive" : "Status: Dead");
                DrawAnnouncement(localPlayer);
                DrawSabotageStatus(localPlayer);
                DrawTaskProgress();
                DrawLocalTaskStatus(localPlayer);
                DrawLocalUtilityStatus(localPlayer);

                if (localPlayer.IsImpostor && localPlayer.IsAlive)
                {
                    var cooldown = localPlayer.KillCooldownRemaining;
                    var killLabel = cooldown <= 0f ? "Kill ready: press Q" : $"Kill cooldown: {cooldown:0.0}s";
                    GUILayout.Label(killLabel);
                }
            }
            else if (localPlayer.MatchState == MatchState.Meeting)
            {
                GUILayout.Label(localPlayer.MeetingBodyNumber > 0
                    ? $"Meeting: {FormatPlayerName(localPlayer.MeetingReporterNumber)} reported {FormatPlayerName(localPlayer.MeetingBodyNumber)}"
                    : $"Meeting: {FormatPlayerName(localPlayer.MeetingReporterNumber)} called emergency");
                var voteSecondsRemaining = Mathf.Max(0f, localPlayer.MeetingEndsAt - Time.time);
                DrawProgressBar(
                    _meetingDurationSeconds <= 0f ? 0f : voteSecondsRemaining / _meetingDurationSeconds,
                    $"Voting ends in {voteSecondsRemaining:0.0}s",
                    new Color(1f, 0.78f, 0.2f, 1f));
                DrawSabotageStatus(localPlayer);
                DrawTaskProgress();

                if (_runner.IsServer)
                {
                    GUILayout.Label($"Votes: {_votes.Count}/{GetEligibleVoterCount()}");
                }

                DrawVoteControls(localPlayer);
            }
            else if (localPlayer.MatchState == MatchState.Ended)
            {
                GUILayout.Label(localPlayer.WinningTeam == WinningTeam.None
                    ? "Result: No winner"
                    : $"Result: {localPlayer.WinningTeam} win");
                DrawAnnouncement(localPlayer);
                DrawSabotageStatus(localPlayer);
                DrawTaskProgress();
            }
        }

        private void DrawHostControls()
        {
            var playerCount = GetSpawnedPlayers().Count;
            var canStart = playerCount >= _minimumPlayersToStart && playerCount > 0;

            if (!_roundStarted)
            {
                _preferHostAsImpostorForTesting = GUILayout.Toggle(
                    _preferHostAsImpostorForTesting,
                    "Make host Impostor for tests");
            }

            GUI.enabled = canStart;
            if (GUILayout.Button(_roundStarted ? "Restart Game" : "Start Game"))
            {
                StartRound();
            }
            GUI.enabled = true;
        }

        private void DrawTaskProgress()
        {
            GetVisibleTaskProgress(out var completedTasks, out var totalTasks);
            if (totalTasks > 0)
            {
                GUILayout.Label($"Crew tasks: {completedTasks}/{totalTasks}");
                DrawProgressBar(
                    totalTasks <= 0 ? 0f : completedTasks / (float)totalTasks,
                    string.Empty,
                    new Color(0.1f, 0.9f, 0.75f, 1f));
            }
        }

        private void DrawSabotageStatus(Player localPlayer)
        {
            var sabotageType = localPlayer.ActiveSabotage;
            if (sabotageType == SabotageType.None)
            {
                return;
            }

            if (sabotageType == SabotageType.Reactor)
            {
                var remaining = Mathf.Max(0f, localPlayer.SabotageEndsAt - Time.time);
                GUILayout.Label($"Sabotage: Reactor meltdown ({remaining:0.0}s)");
            }
            else
            {
                GUILayout.Label($"Sabotage: {sabotageType}");
            }
        }

        private void DrawLocalTaskStatus(Player localPlayer)
        {
            if (localPlayer.ActiveSabotage == SabotageType.Communications)
            {
                GUILayout.Label("Tasks blocked: fix Communications");
                return;
            }

            if (!localPlayer.CanDoTasks)
            {
                return;
            }

            if (localPlayer.AllTasksComplete)
            {
                GUILayout.Label(localPlayer.Role == PlayerRole.Impostor ? "Fake tasks: complete" : "Your tasks: complete");
                return;
            }

            if (localPlayer.ActiveInteraction == InteractionKind.Task)
            {
                DrawProgressBar(
                    localPlayer.InteractionNormalized,
                    $"Task progress: {localPlayer.InteractionNormalized * 100f:0}%",
                    new Color(0.1f, 0.9f, 0.75f, 1f));
                return;
            }

            GUILayout.Label(localPlayer.Role == PlayerRole.Impostor
                ? $"Fake tasks: {localPlayer.CompletedTaskCount}/{localPlayer.AssignedTaskCount}"
                : $"Your tasks: {localPlayer.CompletedTaskCount}/{localPlayer.AssignedTaskCount}");
            if (TryGetNearestIncompleteTask(localPlayer, out var station, out var distance))
            {
                var label = distance <= _taskUseRange
                    ? $"Task ready: {GetTaskInstruction(station.Kind)} at {station.Name}"
                    : $"Nearest task: {station.Name} ({distance:0.0}m)";
                GUILayout.Label(label);
            }
        }

        private void DrawLocalUtilityStatus(Player localPlayer)
        {
            if (!localPlayer.IsAlive)
            {
                return;
            }

            if (localPlayer.ActiveSabotage != SabotageType.None &&
                TryGetSabotageStation(localPlayer.ActiveSabotage, out var sabotageStation))
            {
                var distance = Vector2.Distance(localPlayer.NetworkedPosition, sabotageStation.Position);
                if (localPlayer.ActiveInteraction == InteractionKind.Repair)
                {
                    DrawProgressBar(
                        localPlayer.InteractionNormalized,
                        $"Repair progress: {localPlayer.InteractionNormalized * 100f:0}%",
                        new Color(1f, 0.25f, 0.15f, 1f));
                    return;
                }

                GUILayout.Label(distance <= _sabotageUseRange
                    ? $"Fix ready: hold E at {sabotageStation.Name}"
                    : $"Fix sabotage: {sabotageStation.Name} ({distance:0.0}m)");
                return;
            }

            var emergencyDistance = Vector2.Distance(localPlayer.NetworkedPosition, EmergencyStationPosition);
            if (emergencyDistance <= _emergencyUseRange)
            {
                var cooldown = Mathf.Max(0f, _nextEmergencyAllowedAt - Time.time);
                GUILayout.Label(cooldown <= 0f ? "Emergency ready: press E" : $"Emergency cooldown: {cooldown:0.0}s");
            }

            if (localPlayer.IsImpostor)
            {
                var sabotageCooldown = Mathf.Max(0f, _nextSabotageAllowedAt - Time.time);
                if (TryGetNearestSabotageStation(localPlayer, out var nearestSabotageStation, out var sabotageDistance))
                {
                    var label = sabotageDistance <= _sabotageUseRange && sabotageCooldown <= 0f
                        ? $"Sabotage ready: press F for {nearestSabotageStation.Name}"
                        : $"Nearest sabotage: {nearestSabotageStation.Name} ({sabotageDistance:0.0}m)";
                    GUILayout.Label(label);
                }

                if (sabotageCooldown > 0f)
                {
                    GUILayout.Label($"Sabotage cooldown: {sabotageCooldown:0.0}s");
                }

                if (TryGetNearestVent(localPlayer, out var vent, out var ventDistance))
                {
                    GUILayout.Label(ventDistance <= _ventUseRange
                        ? $"Vent ready: press V at {vent.Name}"
                        : $"Nearest vent: {vent.Name} ({ventDistance:0.0}m)");
                }
            }
        }

        private void DrawVoteControls(Player localPlayer)
        {
            if (!localPlayer.CanVote)
            {
                GUILayout.Label("Waiting for living players to vote...");
                return;
            }

            if (localPlayer.HasVoted)
            {
                GUILayout.Label($"Voted: {FormatVoteTarget(localPlayer.VotedTargetNumber)}");
                return;
            }

            GUILayout.Label("Vote:");
            foreach (var player in GetKnownPlayers())
            {
                if (!player.IsAlive || player.Role == PlayerRole.Spectator)
                {
                    continue;
                }

                if (GUILayout.Button(FormatPlayerName(player.PlayerNumber)))
                {
                    _queuedVoteTargetNumber = player.PlayerNumber;
                }
            }

            if (GUILayout.Button("Skip"))
            {
                _queuedVoteTargetNumber = Player.SkipVoteTarget;
            }
        }

        private void DrawAnnouncement(Player localPlayer)
        {
            if (localPlayer.Announcement == AnnouncementType.None || Time.time >= localPlayer.AnnouncementEndsAt)
            {
                return;
            }

            GUILayout.Space(4f);
            if (localPlayer.Announcement == AnnouncementType.MeetingResult)
            {
                if (localPlayer.AnnouncementPlayerNumber <= 0)
                {
                    GUILayout.Label("Meeting result: no one was ejected");
                    return;
                }

                GUILayout.Label(
                    $"Meeting result: {FormatPlayerName(localPlayer.AnnouncementPlayerNumber)} was ejected ({FormatRole(localPlayer.AnnouncementRole)})");
            }
            else if (localPlayer.Announcement == AnnouncementType.RoundResult)
            {
                GUILayout.Label(localPlayer.WinningTeam == WinningTeam.None
                    ? "Round ended"
                    : $"{localPlayer.WinningTeam} win");
            }
        }

        private void DrawRoster(Player localPlayer)
        {
            var players = GetKnownPlayers();
            if (players.Length == 0)
            {
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label("Players");

            foreach (var player in players)
            {
                var state = player.MatchState == MatchState.Meeting && player.CanVote
                    ? (player.HasVoted ? "voted" : "voting")
                    : GetRosterState(player);
                var role = ShouldRevealRole(localPlayer, player) ? $" / {FormatRole(player.Role)}" : string.Empty;
                GUILayout.Label($"{player.DisplayName}: {state}{role}");
            }
        }

        private static void DrawProgressBar(float normalized, string label, Color fillColor)
        {
            var height = string.IsNullOrEmpty(label) ? 10f : 16f;
            var rect = GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none);

            var fillRect = new Rect(
                rect.x + 2f,
                rect.y + 2f,
                Mathf.Max(0f, (rect.width - 4f) * Mathf.Clamp01(normalized)),
                Mathf.Max(0f, rect.height - 4f));

            var previousColor = GUI.color;
            GUI.color = fillColor;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = previousColor;

            if (!string.IsNullOrEmpty(label))
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                GUI.Label(rect, label, style);
            }
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer)
            {
                var spawnPosition = GetSpawnPosition(GetSpawnedPlayers().Count);
                var networkPlayerObject = SpawnPlayerObject(player, spawnPosition, false);
                runner.SetPlayerObject(player, networkPlayerObject);
                _spawnedCharacters.Add(player, networkPlayerObject);
                _status = $"Player joined: {player}";

                EnsureTestBots();

                if (_roundStarted && networkPlayerObject.TryGetBehaviour<Player>(out var lateJoiner))
                {
                    lateJoiner.BeginRound(PlayerRole.Spectator, spawnPosition, 0, _taskDeadlineEndsAt);
                    if (_taskFailurePending)
                    {
                        lateJoiner.SetTaskFailureCutIn(_taskFailureCutInEndsAt);
                    }
                    if (_activeSabotage != SabotageType.None)
                    {
                        lateJoiner.SetSabotage(_activeSabotage, _activeSabotageEndsAt);
                    }
                }
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
            {
                runner.Despawn(networkObject);
                _spawnedCharacters.Remove(player);
            }

            _status = $"Player left: {player}";

            if (_roundStarted)
            {
                CheckWinCondition();
            }
            else if (runner.IsServer)
            {
                EnsureTestBots();
            }
        }
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new NetworkInputData();

            data.Direction = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                data.Direction += Vector2.up;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                data.Direction += Vector2.down;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                data.Direction += Vector2.left;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                data.Direction += Vector2.right;

            data.Buttons.Set(Player.KillButton, Input.GetKey(KeyCode.Q));
            data.Buttons.Set(Player.ReportButton, Input.GetKey(KeyCode.R));
            data.Buttons.Set(Player.InteractButton, Input.GetKey(KeyCode.E));
            data.Buttons.Set(Player.SabotageButton, Input.GetKey(KeyCode.F));
            data.Buttons.Set(Player.VentButton, Input.GetKey(KeyCode.V));

            if (_queuedVoteTargetNumber.HasValue)
            {
                data.Buttons.Set(Player.VoteButton, true);
                data.VoteTargetNumber = _queuedVoteTargetNumber.Value;
                _queuedVoteTargetNumber = null;
            }
            else
            {
                data.VoteTargetNumber = Player.SkipVoteTarget;
            }

            input.Set(data);
        }

        private NetworkObject SpawnPlayerObject(PlayerRef inputAuthority, Vector2 spawnPosition, bool isBot)
        {
            var playerNumber = _nextPlayerNumber++;
            var colorIndex = Mathf.Abs(playerNumber - 1) % _maxPlayerCount;

            return _runner.Spawn(
                _playerPrefab,
                new Vector3(spawnPosition.x, spawnPosition.y, 0f),
                Quaternion.identity,
                inputAuthority,
                (_, obj) =>
                {
                    obj.GetBehaviour<Player>()?.Initialize(spawnPosition, colorIndex, isBot, playerNumber);
                });
        }

        private NetworkObject SpawnBotObject(Vector2 spawnPosition)
        {
            var playerNumber = _nextPlayerNumber++;
            var colorIndex = Mathf.Abs(playerNumber - 1) % _maxPlayerCount;

            return _runner.Spawn(
                _playerPrefab,
                position: new Vector3(spawnPosition.x, spawnPosition.y, 0f),
                rotation: Quaternion.identity,
                onBeforeSpawned: (_, obj) =>
                {
                    obj.GetBehaviour<Player>()?.Initialize(spawnPosition, colorIndex, true, playerNumber);
                });
        }

        private void EnsureTestBots()
        {
            if (_runner == null || !_runner.IsServer || _roundStarted)
            {
                return;
            }

            var desiredBotCount = Mathf.Min(_testCpuPlayerCount, Mathf.Max(0, _maxPlayerCount - _spawnedCharacters.Count));
            while (_botCharacters.Count > desiredBotCount)
            {
                var lastIndex = _botCharacters.Count - 1;
                var bot = _botCharacters[lastIndex];
                _botCharacters.RemoveAt(lastIndex);
                if (bot == null)
                {
                    continue;
                }

                if (bot.TryGetBehaviour<Player>(out var botPlayer))
                {
                    _botTargets.Remove(botPlayer.PlayerNumber);
                    _botTaskCooldowns.Remove(botPlayer.PlayerNumber);
                    _botActionCooldowns.Remove(botPlayer.PlayerNumber);
                    _channels.Remove(botPlayer.PlayerNumber);
                }

                _runner.Despawn(bot);
            }

            while (_botCharacters.Count < desiredBotCount)
            {
                var spawnPosition = GetSpawnPosition(GetSpawnedPlayers().Count);
                var bot = SpawnBotObject(spawnPosition);
                _botCharacters.Add(bot);

                if (bot.TryGetBehaviour<Player>(out var player))
                {
                    _botTargets[player.PlayerNumber] = GetRandomBotTarget();
                    _botTaskCooldowns[player.PlayerNumber] = Time.time + UnityEngine.Random.Range(1f, _botTaskPauseSeconds);
                    _botActionCooldowns[player.PlayerNumber] = Time.time + UnityEngine.Random.Range(0.5f, 2f);
                }
            }
        }

        public bool TryKill(Player killer)
        {
            if (_runner == null || !_runner.IsServer || !_roundStarted || _meetingActive)
            {
                return false;
            }

            var target = FindKillTarget(killer);
            if (target == null)
            {
                _status = "No kill target in range";
                return false;
            }

            target.Kill();
            CancelHeldInteract(target);
            _status = $"{killer.DisplayName} killed {target.DisplayName}";
            CheckWinCondition();
            return true;
        }

        public bool TryReport(Player reporter)
        {
            if (_runner == null || !_runner.IsServer || !_roundStarted || _meetingActive)
            {
                return false;
            }

            var body = FindReportableBody(reporter);
            if (body == null)
            {
                _status = "No reportable body in range";
                return false;
            }

            StartMeeting(reporter, body);
            return true;
        }

        public bool TryInteract(Player player)
        {
            if (_runner == null || !_runner.IsServer || !_roundStarted || _meetingActive || _taskFailurePending || !player.IsAlive)
            {
                return false;
            }

            if (_activeSabotage != SabotageType.None && player.Role == PlayerRole.Crewmate &&
                TryGetSabotageStation(_activeSabotage, out var sabotageStation))
            {
                var distance = Vector2.Distance(player.NetworkedPosition, sabotageStation.Position);
                if (distance <= _sabotageUseRange)
                {
                    _status = $"Hold E to repair {sabotageStation.Name}";
                    return true;
                }
            }

            if (_activeSabotage == SabotageType.None && TryCallEmergencyMeeting(player))
            {
                return true;
            }

            if (player.CanDoTasks)
            {
                if (_activeSabotage == SabotageType.Communications)
                {
                    _status = "Tasks are blocked while Communications is sabotaged";
                    return false;
                }

                if (TryGetNearestIncompleteTask(player, out var taskStation, out var taskDistance) &&
                    taskDistance <= _taskUseRange)
                {
                    _status = $"{GetTaskInstruction(taskStation.Kind)}: {taskStation.Name}";
                    return true;
                }
            }

            return false;
        }

        public void UpdateHeldInteract(Player player, float deltaTime, bool isHeld)
        {
            if (_runner == null || !_runner.IsServer || !_roundStarted || _meetingActive || !player.IsAlive)
            {
                CancelHeldInteract(player);
                return;
            }

            if (_activeSabotage != SabotageType.None && player.Role == PlayerRole.Crewmate &&
                TryGetSabotageStation(_activeSabotage, out var sabotageStation))
            {
                var distance = Vector2.Distance(player.NetworkedPosition, sabotageStation.Position);
                if (distance <= _sabotageUseRange)
                {
                    if (!isHeld)
                    {
                        CancelHeldInteract(player);
                        return;
                    }

                    AdvanceChannel(
                        player,
                        InteractionKind.Repair,
                        (int)_activeSabotage,
                        _repairChannelSeconds,
                        deltaTime,
                        () => TryFixSabotage(player));
                    return;
                }
            }

            if (player.CanDoTasks && _activeSabotage != SabotageType.Communications &&
                TryGetNearestIncompleteTask(player, out var taskStation, out var taskDistance) &&
                taskDistance <= _taskUseRange)
            {
                UpdateTaskChannel(player, taskStation, deltaTime, isHeld);
                return;
            }

            CancelHeldInteract(player);
        }

        private void UpdateTaskChannel(Player player, TaskStation station, float deltaTime, bool isHeld)
        {
            switch (station.Kind)
            {
                case TaskKind.CircuitPulse:
                    UpdateCircuitTask(player, station, deltaTime, isHeld);
                    break;
                case TaskKind.Calibration:
                    UpdateCalibrationTask(player, station, deltaTime, isHeld);
                    break;
                default:
                    if (!isHeld)
                    {
                        CancelHeldInteract(player);
                        return;
                    }

                    AdvanceChannel(
                        player,
                        InteractionKind.Task,
                        station.Id,
                        _taskChannelSeconds,
                        deltaTime,
                        () => TryCompleteTask(player));
                    break;
            }
        }

        private void UpdateCircuitTask(Player player, TaskStation station, float deltaTime, bool isHeld)
        {
            if (!_channels.TryGetValue(player.PlayerNumber, out var channel) ||
                channel.Kind != InteractionKind.Task ||
                channel.TargetId != station.Id)
            {
                if (!isHeld)
                {
                    return;
                }

                channel = new ChannelState(InteractionKind.Task, station.Id);
            }

            if (player.IsBot)
            {
                channel.Progress += deltaTime / 0.35f;
            }
            else if (isHeld && !channel.WasHeld)
            {
                channel.Progress += 1f;
            }

            channel.WasHeld = isHeld;
            CommitTaskChannel(player, channel, CircuitPulseCount);
        }

        private void UpdateCalibrationTask(Player player, TaskStation station, float deltaTime, bool isHeld)
        {
            var cycleSeconds = Mathf.Max(0.5f, _calibrationCycleSeconds);
            if (!_channels.TryGetValue(player.PlayerNumber, out var channel) ||
                channel.Kind != InteractionKind.Task ||
                channel.TargetId != station.Id)
            {
                if (!isHeld)
                {
                    return;
                }

                channel = new ChannelState(InteractionKind.Task, station.Id);
            }

            if (player.IsBot)
            {
                channel.Progress += deltaTime;
                if (channel.Progress >= cycleSeconds * CalibrationTargetStart)
                {
                    CompleteTaskChannel(player);
                    return;
                }
            }
            else if (isHeld)
            {
                channel.Progress = Mathf.Repeat(channel.Progress + deltaTime, cycleSeconds);
            }
            else if (channel.WasHeld)
            {
                var normalized = channel.Progress / cycleSeconds;
                if (normalized >= CalibrationTargetStart && normalized <= CalibrationTargetEnd)
                {
                    CompleteTaskChannel(player);
                    return;
                }

                channel.Progress = 0f;
            }

            channel.WasHeld = isHeld;
            _channels[player.PlayerNumber] = channel;
            player.SetInteraction(InteractionKind.Task, channel.Progress, cycleSeconds);
        }

        private void CommitTaskChannel(Player player, ChannelState channel, float requiredProgress)
        {
            if (channel.Progress >= requiredProgress)
            {
                CompleteTaskChannel(player);
                return;
            }

            _channels[player.PlayerNumber] = channel;
            player.SetInteraction(InteractionKind.Task, channel.Progress, requiredProgress);
        }

        private void CompleteTaskChannel(Player player)
        {
            _channels.Remove(player.PlayerNumber);
            player.ClearInteraction();
            TryCompleteTask(player);
        }

        private void AdvanceChannel(
            Player player,
            InteractionKind kind,
            int targetId,
            float requiredSeconds,
            float deltaTime,
            Func<bool> onComplete)
        {
            if (!_channels.TryGetValue(player.PlayerNumber, out var channel) ||
                channel.Kind != kind ||
                channel.TargetId != targetId)
            {
                channel = new ChannelState(kind, targetId);
            }

            channel.Progress += deltaTime;
            _channels[player.PlayerNumber] = channel;
            player.SetInteraction(kind, channel.Progress, requiredSeconds);

            if (channel.Progress < requiredSeconds)
            {
                return;
            }

            _channels.Remove(player.PlayerNumber);
            player.ClearInteraction();
            onComplete();
        }

        private void CancelHeldInteract(Player player)
        {
            if (player == null)
            {
                return;
            }

            _channels.Remove(player.PlayerNumber);
            player.ClearInteraction();
        }

        public bool TryCompleteTask(Player crewmate)
        {
            if (_runner == null || !_runner.IsServer || !_roundStarted || _meetingActive || _taskFailurePending || !crewmate.CanDoTasks)
            {
                return false;
            }

            if (_activeSabotage == SabotageType.Communications)
            {
                _status = "Tasks are blocked while Communications is sabotaged";
                return false;
            }

            if (!TryGetNearestIncompleteTask(crewmate, out var station, out var distance) || distance > _taskUseRange)
            {
                _status = "No unfinished task in range";
                return false;
            }

            if (!crewmate.CompleteTask(station.Id))
            {
                return false;
            }

            _status = $"{crewmate.DisplayName} completed {station.Name}";
            CheckWinCondition();
            return true;
        }

        public bool TryStartSabotage(Player impostor)
        {
            if (_runner == null || !_runner.IsServer || !_roundStarted || _meetingActive ||
                !impostor.IsAlive || impostor.Role != PlayerRole.Impostor)
            {
                return false;
            }

            if (_activeSabotage != SabotageType.None)
            {
                _status = "A sabotage is already active";
                return false;
            }

            if (Time.time < _nextSabotageAllowedAt)
            {
                _status = "Sabotage is cooling down";
                return false;
            }

            if (!TryGetNearestSabotageStation(impostor, out var station, out var distance) || distance > _sabotageUseRange)
            {
                _status = "No sabotage console in range";
                return false;
            }

            StartSabotage(station);
            _nextSabotageAllowedAt = Time.time + _sabotageCooldownSeconds;
            _status = $"{impostor.DisplayName} sabotaged {station.Name}";
            return true;
        }

        public bool TryUseVent(Player impostor)
        {
            if (_runner == null || !_runner.IsServer || !_roundStarted || _meetingActive ||
                !impostor.IsAlive || impostor.Role != PlayerRole.Impostor)
            {
                return false;
            }

            if (!TryGetNearestVent(impostor, out var vent, out var distance) || distance > _ventUseRange)
            {
                _status = "No vent in range";
                return false;
            }

            var nextVent = GetNextVent(vent);
            impostor.Teleport(nextVent.Position);
            _status = $"{impostor.DisplayName} vented to {nextVent.Name}";
            return true;
        }

        public bool TrySubmitVote(Player voter, int targetNumber)
        {
            if (_runner == null || !_runner.IsServer || !_meetingActive || !voter.CanVote)
            {
                return false;
            }

            if (_votes.ContainsKey(voter.PlayerNumber))
            {
                return false;
            }

            if (targetNumber != Player.SkipVoteTarget && !IsValidVoteTarget(targetNumber))
            {
                targetNumber = Player.SkipVoteTarget;
            }

            _votes[voter.PlayerNumber] = targetNumber;
            voter.MarkVoted(targetNumber);
            _status = $"{voter.DisplayName} voted {FormatVoteTarget(targetNumber)}";

            if (_votes.Count >= GetEligibleVoterCount())
            {
                ResolveMeeting();
            }

            return true;
        }

        private void StartRound()
        {
            if (_runner == null || !_runner.IsServer)
            {
                return;
            }

            EnsureTestBots();

            var players = GetSpawnedPlayers();
            if (players.Count == 0)
            {
                return;
            }

            _roundStarted = true;
            _meetingActive = false;
            _activeSabotage = SabotageType.None;
            _activeSabotageEndsAt = 0f;
            _nextSabotageAllowedAt = Time.time + 4f;
            _nextEmergencyAllowedAt = 0f;
            _nextTimedTaskAt = Time.time + Mathf.Max(1f, _firstTimedTaskDelaySeconds);
            _timedTaskWavesIssued = 0;
            _taskDeadlineEndsAt = Time.time + Mathf.Max(30f, _taskDeadlineSeconds);
            _taskFailurePending = false;
            _taskFailureCutInEndsAt = 0f;
            _botTargets.Clear();
            _botTaskCooldowns.Clear();
            _botActionCooldowns.Clear();
            _channels.Clear();
            _votes.Clear();

            Shuffle(players);
            PreferHostAsFirstImpostor(players);

            var impostorsToAssign = Mathf.Clamp(_impostorCount, 1, players.Count);
            var roundSpawnPositions = CreateRoundSpawnPositions(players.Count);
            for (var i = 0; i < players.Count; i++)
            {
                var role = i < impostorsToAssign ? PlayerRole.Impostor : PlayerRole.Crewmate;
                var taskMask = CreateTaskAssignmentMask();
                players[i].BeginRound(role, roundSpawnPositions[i], taskMask, _taskDeadlineEndsAt);

                if (players[i].IsBot)
                {
                    _botTargets[players[i].PlayerNumber] = GetNextBotTarget(players[i]);
                    _botTaskCooldowns[players[i].PlayerNumber] = Time.time + UnityEngine.Random.Range(1f, _botTaskPauseSeconds);
                    _botActionCooldowns[players[i].PlayerNumber] = Time.time + UnityEngine.Random.Range(0.5f, 2f);
                }
            }

            _status = $"Round started: {players.Count - impostorsToAssign} Crewmate(s), {impostorsToAssign} Impostor(s)";
        }

        private int CreateTaskAssignmentMask()
        {
            var availableTaskCount = Mathf.Min(TaskStations.Length, 30);
            var assignedTaskCount = Mathf.Clamp(_tasksPerCrewmate, 0, availableTaskCount);
            var taskIds = new List<int>(availableTaskCount);
            for (var taskId = 0; taskId < availableTaskCount; taskId++)
            {
                taskIds.Add(taskId);
            }

            for (var i = 0; i < assignedTaskCount; i++)
            {
                var selectedIndex = UnityEngine.Random.Range(i, taskIds.Count);
                (taskIds[i], taskIds[selectedIndex]) = (taskIds[selectedIndex], taskIds[i]);
            }

            var mask = 0;
            for (var i = 0; i < assignedTaskCount; i++)
            {
                mask |= 1 << taskIds[i];
            }

            return mask;
        }

        private void UpdateTimedTaskAssignments()
        {
            if (_timedTaskWavesIssued >= _maxTimedTaskWaves || Time.time < _nextTimedTaskAt)
            {
                return;
            }

            var assignedCount = 0;
            foreach (var player in GetSpawnedPlayers())
            {
                if (player.MatchState != MatchState.Playing || !player.IsAlive || player.Role != PlayerRole.Crewmate)
                {
                    continue;
                }

                if (TryAssignRandomTask(player))
                {
                    assignedCount++;
                }
            }

            _timedTaskWavesIssued++;
            _nextTimedTaskAt = _timedTaskWavesIssued >= _maxTimedTaskWaves
                ? float.PositiveInfinity
                : Time.time + Mathf.Max(1f, _timedTaskIntervalSeconds);

            if (assignedCount > 0)
            {
                _status = $"Task alert: {assignedCount} Crewmate(s) received a new task";
            }
        }

        private static bool TryAssignRandomTask(Player player)
        {
            var availableTasks = new List<TaskStation>();
            foreach (var station in TaskStations)
            {
                if (station.Id < 30 && !player.HasAssignedTask(station.Id))
                {
                    availableTasks.Add(station);
                }
            }

            if (availableTasks.Count == 0)
            {
                return false;
            }

            var task = availableTasks[UnityEngine.Random.Range(0, availableTasks.Count)];
            return player.AssignTask(task.Id);
        }

        private void BeginTaskFailureCutIn()
        {
            if (_taskFailurePending)
            {
                return;
            }

            _taskFailurePending = true;
            _taskFailureCutInEndsAt = Time.time + Mathf.Max(1f, _taskFailureCutInSeconds);
            _meetingActive = false;
            _channels.Clear();
            _votes.Clear();
            ClearActiveSabotage();

            foreach (var player in GetSpawnedPlayers())
            {
                player.SetTaskFailureCutIn(_taskFailureCutInEndsAt);
            }

            _status = "Task deadline expired. Impostor victory cut-in started.";
        }

        private Player FindKillTarget(Player killer)
        {
            Player closest = null;
            var closestDistance = _killRange;

            foreach (var player in GetSpawnedPlayers())
            {
                if (player == killer || !player.IsAlive || player.Role != PlayerRole.Crewmate)
                {
                    continue;
                }

                var distance = Vector2.Distance(killer.NetworkedPosition, player.NetworkedPosition);
                if (distance <= closestDistance &&
                    ShipMap.HasClearWalkableSegment(killer.NetworkedPosition, player.NetworkedPosition, 0.18f))
                {
                    closest = player;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        private Player FindReportableBody(Player reporter)
        {
            Player closest = null;
            var closestDistance = _reportRange;

            foreach (var player in GetSpawnedPlayers())
            {
                if (player == reporter || !player.IsReportableBody)
                {
                    continue;
                }

                var distance = Vector2.Distance(reporter.NetworkedPosition, player.NetworkedPosition);
                if (distance <= closestDistance &&
                    ShipMap.HasClearWalkableSegment(reporter.NetworkedPosition, player.NetworkedPosition, 0.18f))
                {
                    closest = player;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        private bool CheckWinCondition()
        {
            if (_taskFailurePending)
            {
                return false;
            }

            if (GetSpawnedPlayers().Count < 2)
            {
                return false;
            }

            var aliveCrewmates = 0;
            var aliveImpostors = 0;

            foreach (var player in GetSpawnedPlayers())
            {
                if (!player.IsAlive)
                {
                    continue;
                }

                if (player.Role == PlayerRole.Crewmate)
                {
                    aliveCrewmates++;
                }
                else if (player.Role == PlayerRole.Impostor)
                {
                    aliveImpostors++;
                }
            }

            if (aliveImpostors == 0)
            {
                EndRound(WinningTeam.Crewmates);
                return true;
            }
            else if (aliveImpostors >= aliveCrewmates)
            {
                EndRound(WinningTeam.Impostors);
                return true;
            }

            if (_taskDeadlineEndsAt > 0f && Time.time >= _taskDeadlineEndsAt)
            {
                BeginTaskFailureCutIn();
                return true;
            }

            if (AreCrewTasksComplete())
            {
                EndRound(WinningTeam.Crewmates);
                return true;
            }

            return false;
        }

        private void EndRound(WinningTeam winningTeam)
        {
            _meetingActive = false;
            _nextTimedTaskAt = float.PositiveInfinity;
            _taskDeadlineEndsAt = 0f;
            _taskFailurePending = false;
            ClearActiveSabotage();
            _channels.Clear();
            _votes.Clear();

            foreach (var player in GetSpawnedPlayers())
            {
                player.EndRound(winningTeam);
                player.SetAnnouncement(AnnouncementType.RoundResult, 0, PlayerRole.Unassigned, Time.time + _announcementSeconds);
            }

            _status = $"{winningTeam} win. Host can restart the game.";
        }

        private void StartMeeting(Player reporter, Player body)
        {
            _meetingActive = true;
            _meetingEndsAt = Time.time + _meetingDurationSeconds;
            _nextBotVoteAt = Time.time + _botVoteDelay;
            _nextEmergencyAllowedAt = Time.time + _emergencyCooldownSeconds;
            _votes.Clear();
            _channels.Clear();
            ClearActiveSabotage();

            var bodyNumber = 0;
            if (body != null)
            {
                body.MarkBodyReported();
                bodyNumber = body.PlayerNumber;
            }

            foreach (var player in GetSpawnedPlayers())
            {
                player.EnterMeeting(reporter.PlayerNumber, bodyNumber, _meetingEndsAt);
            }

            _status = body != null
                ? $"{reporter.DisplayName} reported {body.DisplayName}. Vote now."
                : $"{reporter.DisplayName} called an emergency meeting. Vote now.";
        }

        private void ResolveMeeting()
        {
            if (!_meetingActive)
            {
                return;
            }

            _meetingActive = false;

            var ejectedPlayerNumber = GetEjectedPlayerNumber();
            var ejectedPlayer = ejectedPlayerNumber == Player.SkipVoteTarget ? null : FindPlayerByNumber(ejectedPlayerNumber);

            if (ejectedPlayer != null)
            {
                ejectedPlayer.Exile();
                _status = $"{ejectedPlayer.DisplayName} was ejected.";
            }
            else
            {
                _status = "No one was ejected.";
            }

            AnnounceMeetingResult(ejectedPlayer);

            _votes.Clear();

            if (CheckWinCondition())
            {
                return;
            }

            foreach (var player in GetSpawnedPlayers())
            {
                player.ResumeAfterMeeting();
            }
        }

        private void AnnounceMeetingResult(Player ejectedPlayer)
        {
            var ejectedPlayerNumber = ejectedPlayer != null ? ejectedPlayer.PlayerNumber : 0;
            var ejectedRole = ejectedPlayer != null ? ejectedPlayer.Role : PlayerRole.Unassigned;
            var endsAt = Time.time + _announcementSeconds;

            foreach (var player in GetSpawnedPlayers())
            {
                player.SetAnnouncement(AnnouncementType.MeetingResult, ejectedPlayerNumber, ejectedRole, endsAt);
            }
        }

        private int GetEjectedPlayerNumber()
        {
            if (_votes.Count == 0)
            {
                return Player.SkipVoteTarget;
            }

            var counts = new Dictionary<int, int>();
            foreach (var targetNumber in _votes.Values)
            {
                counts[targetNumber] = counts.TryGetValue(targetNumber, out var currentCount) ? currentCount + 1 : 1;
            }

            var topTargetNumber = Player.SkipVoteTarget;
            var topVotes = 0;
            var tied = false;

            foreach (var pair in counts)
            {
                if (pair.Value > topVotes)
                {
                    topTargetNumber = pair.Key;
                    topVotes = pair.Value;
                    tied = false;
                }
                else if (pair.Value == topVotes)
                {
                    tied = true;
                }
            }

            return tied ? Player.SkipVoteTarget : topTargetNumber;
        }

        private void StartSabotage(SabotageStation station)
        {
            _activeSabotage = station.Type;
            _activeSabotageEndsAt = station.HasCountdown ? Time.time + _reactorCountdownSeconds : 0f;
            BroadcastSabotage();
        }

        private bool TryFixSabotage(Player crewmate)
        {
            if (_activeSabotage == SabotageType.None || !TryGetSabotageStation(_activeSabotage, out var station))
            {
                return false;
            }

            var distance = Vector2.Distance(crewmate.NetworkedPosition, station.Position);
            if (distance > _sabotageUseRange)
            {
                _status = $"Fix {station.Name} sabotage";
                return false;
            }

            ClearActiveSabotage();
            _status = $"{crewmate.DisplayName} fixed {station.Name}";
            return true;
        }

        private bool TryCallEmergencyMeeting(Player reporter)
        {
            if (!reporter.IsAlive || reporter.Role == PlayerRole.Spectator)
            {
                return false;
            }

            if (Time.time < _nextEmergencyAllowedAt)
            {
                return false;
            }

            var distance = Vector2.Distance(reporter.NetworkedPosition, EmergencyStationPosition);
            if (distance > _emergencyUseRange)
            {
                return false;
            }

            StartMeeting(reporter, null);
            return true;
        }

        private void ClearActiveSabotage()
        {
            _activeSabotage = SabotageType.None;
            _activeSabotageEndsAt = 0f;
            BroadcastSabotage();
        }

        private void BroadcastSabotage()
        {
            foreach (var player in GetSpawnedPlayers())
            {
                if (_activeSabotage == SabotageType.None)
                {
                    player.ClearSabotage();
                }
                else
                {
                    player.SetSabotage(_activeSabotage, _activeSabotageEndsAt);
                }
            }
        }

        private void EnsureRuntimeHud()
        {
            if (GetComponent<RuntimeHud>() == null)
            {
                gameObject.AddComponent<RuntimeHud>();
            }
        }

        private void UpdateBots()
        {
            foreach (var botObject in _botCharacters)
            {
                if (botObject == null || !botObject.TryGetBehaviour<Player>(out var bot))
                {
                    continue;
                }

                if (!bot.IsAlive || bot.MatchState != MatchState.Playing)
                {
                    continue;
                }

                if (bot.Role == PlayerRole.Impostor)
                {
                    UpdateBotImpostor(bot);
                    continue;
                }

                if (bot.Role == PlayerRole.Crewmate && _activeSabotage != SabotageType.None &&
                    TryGetSabotageStation(_activeSabotage, out var sabotageStation))
                {
                    var sabotageDistance = Vector2.Distance(bot.NetworkedPosition, sabotageStation.Position);
                    if (sabotageDistance <= _sabotageUseRange)
                    {
                        if (!_botTaskCooldowns.TryGetValue(bot.PlayerNumber, out var nextTaskTime) ||
                            Time.time >= nextTaskTime)
                        {
                            var previousSabotage = _activeSabotage;
                            UpdateHeldInteract(bot, Time.deltaTime, true);
                            if (previousSabotage != _activeSabotage)
                            {
                                _botTaskCooldowns[bot.PlayerNumber] = Time.time + _botTaskPauseSeconds;
                            }
                        }
                        else
                        {
                            CancelHeldInteract(bot);
                        }

                        continue;
                    }

                    CancelHeldInteract(bot);
                    MoveBotToward(bot, sabotageStation.Position);
                    continue;
                }

                if (bot.Role == PlayerRole.Crewmate && !bot.AllTasksComplete &&
                    TryGetNearestIncompleteTask(bot, out var station, out var taskDistance))
                {
                    if (taskDistance <= _taskUseRange)
                    {
                        if (!_botTaskCooldowns.TryGetValue(bot.PlayerNumber, out var nextTaskTime) ||
                            Time.time >= nextTaskTime)
                        {
                            var previousCompletedTasks = bot.CompletedTaskCount;
                            UpdateHeldInteract(bot, Time.deltaTime, true);
                            if (bot.CompletedTaskCount > previousCompletedTasks)
                            {
                                _botTaskCooldowns[bot.PlayerNumber] = Time.time + _botTaskPauseSeconds;
                                _botTargets[bot.PlayerNumber] = GetNextBotTarget(bot);
                            }
                        }
                        else
                        {
                            CancelHeldInteract(bot);
                        }

                        continue;
                    }

                    if (!_botTargets.TryGetValue(bot.PlayerNumber, out var taskTarget) ||
                        bot.HasCompletedTask(GetTaskStationIdAt(taskTarget)) ||
                        Vector2.Distance(bot.NetworkedPosition, taskTarget) < 0.2f)
                    {
                        taskTarget = station.Position;
                        _botTargets[bot.PlayerNumber] = taskTarget;
                    }

                    CancelHeldInteract(bot);
                    MoveBotToward(bot, taskTarget);
                    continue;
                }

                CancelHeldInteract(bot);
                if (!_botTargets.TryGetValue(bot.PlayerNumber, out var target) ||
                    Vector2.Distance(bot.NetworkedPosition, target) < 0.2f)
                {
                    target = GetNextBotTarget(bot);
                    _botTargets[bot.PlayerNumber] = target;
                }

                MoveBotToward(bot, target);
            }
        }

        private void UpdateBotImpostor(Player bot)
        {
            CancelHeldInteract(bot);

            if (bot.IsKillReady && FindKillTarget(bot) != null && TryKill(bot))
            {
                bot.MarkKillUsed();
                _botActionCooldowns[bot.PlayerNumber] = Time.time + UnityEngine.Random.Range(0.8f, 1.8f);
                return;
            }

            if (!_botActionCooldowns.TryGetValue(bot.PlayerNumber, out var nextThinkTime) ||
                Time.time >= nextThinkTime)
            {
                _botActionCooldowns[bot.PlayerNumber] = Time.time + Mathf.Max(0.1f, _botImpostorThinkSeconds);

                if (_activeSabotage == SabotageType.None &&
                    Time.time >= _nextSabotageAllowedAt &&
                    UnityEngine.Random.value <= _botImpostorSabotageChance)
                {
                    _botTargets[bot.PlayerNumber] = GetRandomSabotageTarget();
                }
            }

            if (_activeSabotage == SabotageType.None &&
                Time.time >= _nextSabotageAllowedAt &&
                TryGetNearestSabotageStation(bot, out _, out var sabotageDistance) &&
                sabotageDistance <= _sabotageUseRange &&
                UnityEngine.Random.value <= 0.55f)
            {
                TryStartSabotage(bot);
                _botActionCooldowns[bot.PlayerNumber] = Time.time + UnityEngine.Random.Range(3f, 6f);
                return;
            }

            if (bot.KillCooldownRemaining > 2f &&
                TryGetNearestVent(bot, out _, out var ventDistance) &&
                ventDistance <= _ventUseRange &&
                UnityEngine.Random.value <= 0.01f)
            {
                TryUseVent(bot);
                return;
            }

            if (TryGetClosestAliveCrewmate(bot, out var targetCrewmate))
            {
                MoveBotToward(bot, targetCrewmate.NetworkedPosition);
                return;
            }

            if (!_botTargets.TryGetValue(bot.PlayerNumber, out var target) ||
                Vector2.Distance(bot.NetworkedPosition, target) < 0.2f)
            {
                target = GetRandomBotTarget();
                _botTargets[bot.PlayerNumber] = target;
            }

            MoveBotToward(bot, target);
        }

        private void UpdateBotVoting()
        {
            if (Time.time < _nextBotVoteAt)
            {
                return;
            }

            _nextBotVoteAt = Time.time + _botVoteInterval;

            foreach (var botObject in _botCharacters)
            {
                if (botObject == null || !botObject.TryGetBehaviour<Player>(out var bot))
                {
                    continue;
                }

                if (!bot.CanVote || _votes.ContainsKey(bot.PlayerNumber))
                {
                    continue;
                }

                TrySubmitVote(bot, PickBotVoteTarget(bot));
                break;
            }
        }

        private void MoveBotToward(Player bot, Vector2 target)
        {
            var beforeMove = bot.NetworkedPosition;
            var steeringTarget = ShipMap.GetNavigationTarget(bot.NetworkedPosition, target);
            var direction = steeringTarget - bot.NetworkedPosition;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            bot.MoveBy(direction * _botMoveSpeed * Time.deltaTime);

            if (direction.sqrMagnitude <= 0.01f ||
                Vector2.Distance(beforeMove, bot.NetworkedPosition) > 0.01f)
            {
                return;
            }

            var sidestep = new Vector2(-direction.y, direction.x);
            if (UnityEngine.Random.value < 0.5f)
            {
                sidestep = -sidestep;
            }

            bot.MoveBy(sidestep * _botMoveSpeed * Time.deltaTime);
        }

        private bool AreCrewTasksComplete()
        {
            if (_timedTaskWavesIssued < _maxTimedTaskWaves)
            {
                return false;
            }

            GetCrewTaskProgress(GetSpawnedPlayers(), out var completedTasks, out var totalTasks);
            return totalTasks > 0 && completedTasks >= totalTasks;
        }

        private void GetVisibleTaskProgress(out int completedTasks, out int totalTasks)
        {
            GetCrewTaskProgress(GetKnownPlayers(), out completedTasks, out totalTasks);
        }

        private static void GetCrewTaskProgress(IEnumerable<Player> players, out int completedTasks, out int totalTasks)
        {
            completedTasks = 0;
            totalTasks = 0;

            foreach (var player in players)
            {
                if (player.Role != PlayerRole.Crewmate)
                {
                    continue;
                }

                completedTasks += player.CompletedTaskCount;
                totalTasks += player.AssignedTaskCount;
            }
        }

        private bool TryGetNearestIncompleteTask(Player player, out TaskStation station, out float distance)
        {
            station = default;
            distance = float.MaxValue;
            var foundStation = false;

            foreach (var candidate in TaskStations)
            {
                if (!player.HasAssignedTask(candidate.Id) || player.HasCompletedTask(candidate.Id))
                {
                    continue;
                }

                var candidateDistance = Vector2.Distance(player.NetworkedPosition, candidate.Position);
                if (candidateDistance < distance)
                {
                    station = candidate;
                    distance = candidateDistance;
                    foundStation = true;
                }
            }

            return foundStation;
        }

        private bool TryGetRandomIncompleteTask(Player player, out TaskStation station)
        {
            var stations = new List<TaskStation>();
            foreach (var candidate in TaskStations)
            {
                if (player.HasAssignedTask(candidate.Id) && !player.HasCompletedTask(candidate.Id))
                {
                    stations.Add(candidate);
                }
            }

            if (stations.Count == 0)
            {
                station = default;
                return false;
            }

            station = stations[UnityEngine.Random.Range(0, stations.Count)];
            return true;
        }

        private int GetTaskStationIdAt(Vector2 position)
        {
            foreach (var station in TaskStations)
            {
                if (Vector2.Distance(station.Position, position) < 0.01f)
                {
                    return station.Id;
                }
            }

            return -1;
        }

        private int PickBotVoteTarget(Player voter)
        {
            var candidates = new List<Player>();
            foreach (var player in GetSpawnedPlayers())
            {
                if (player != voter && player.IsAlive && player.Role != PlayerRole.Spectator)
                {
                    candidates.Add(player);
                }
            }

            if (candidates.Count == 0 || UnityEngine.Random.value < 0.25f)
            {
                return Player.SkipVoteTarget;
            }

            return candidates[UnityEngine.Random.Range(0, candidates.Count)].PlayerNumber;
        }

        private bool IsValidVoteTarget(int targetNumber)
        {
            var target = FindPlayerByNumber(targetNumber);
            return target != null && target.IsAlive && target.Role != PlayerRole.Spectator;
        }

        private int GetEligibleVoterCount()
        {
            var count = 0;
            foreach (var player in GetSpawnedPlayers())
            {
                if (player.CanVote)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountVisibleEligibleVoters()
        {
            var count = 0;
            foreach (var player in GetKnownPlayers())
            {
                if (player.CanVote)
                {
                    count++;
                }
            }

            return count;
        }

        private List<Player> GetSpawnedPlayers()
        {
            var players = new List<Player>();
            foreach (var networkObject in _spawnedCharacters.Values)
            {
                if (networkObject != null && networkObject.TryGetBehaviour<Player>(out var player))
                {
                    players.Add(player);
                }
            }

            foreach (var networkObject in _botCharacters)
            {
                if (networkObject != null && networkObject.TryGetBehaviour<Player>(out var player))
                {
                    players.Add(player);
                }
            }

            return players;
        }

        private Player[] GetKnownPlayers()
        {
            var players = FindObjectsByType<Player>(FindObjectsSortMode.None);
            Array.Sort(players, (left, right) => left.PlayerNumber.CompareTo(right.PlayerNumber));
            return players;
        }

        private Player FindPlayerByNumber(int playerNumber)
        {
            foreach (var player in GetSpawnedPlayers())
            {
                if (player.PlayerNumber == playerNumber)
                {
                    return player;
                }
            }

            return null;
        }

        private Player GetLocalPlayer()
        {
            if (_runner == null)
            {
                return null;
            }

            var playerObject = _runner.GetPlayerObject(_runner.LocalPlayer);
            if (playerObject != null && playerObject.TryGetBehaviour<Player>(out var player))
            {
                return player;
            }

            return null;
        }

        private int GetVisiblePlayerCount()
        {
            if (_runner != null && _runner.IsServer)
            {
                return GetSpawnedPlayers().Count;
            }

            return FindObjectsByType<Player>(FindObjectsSortMode.None).Length;
        }

        private int GetVisibleCpuCount()
        {
            if (_runner != null && _runner.IsServer)
            {
                return _botCharacters.Count;
            }

            var count = 0;
            foreach (var player in GetKnownPlayers())
            {
                if (player.IsBot)
                {
                    count++;
                }
            }

            return count;
        }

        private bool TryGetSabotageStation(SabotageType type, out SabotageStation station)
        {
            foreach (var candidate in SabotageStations)
            {
                if (candidate.Type == type)
                {
                    station = candidate;
                    return true;
                }
            }

            station = default;
            return false;
        }

        private bool TryGetNearestSabotageStation(Player player, out SabotageStation station, out float distance)
        {
            station = default;
            distance = float.MaxValue;
            var foundStation = false;

            foreach (var candidate in SabotageStations)
            {
                var candidateDistance = Vector2.Distance(player.NetworkedPosition, candidate.Position);
                if (candidateDistance < distance)
                {
                    station = candidate;
                    distance = candidateDistance;
                    foundStation = true;
                }
            }

            return foundStation;
        }

        private bool TryGetNearestVent(Player player, out VentStation vent, out float distance)
        {
            vent = default;
            distance = float.MaxValue;
            var foundVent = false;

            foreach (var candidate in VentStations)
            {
                var candidateDistance = Vector2.Distance(player.NetworkedPosition, candidate.Position);
                if (candidateDistance < distance)
                {
                    vent = candidate;
                    distance = candidateDistance;
                    foundVent = true;
                }
            }

            return foundVent;
        }

        private VentStation GetNextVent(VentStation currentVent)
        {
            if (VentStations.Length == 0)
            {
                return currentVent;
            }

            var groupVents = new List<VentStation>();
            foreach (var vent in VentStations)
            {
                if (vent.GroupId == currentVent.GroupId)
                {
                    groupVents.Add(vent);
                }
            }

            if (groupVents.Count <= 1)
            {
                return currentVent;
            }

            var currentIndex = 0;
            for (var i = 0; i < groupVents.Count; i++)
            {
                if (Vector2.Distance(groupVents[i].Position, currentVent.Position) < 0.01f)
                {
                    currentIndex = i;
                    break;
                }
            }

            return groupVents[(currentIndex + 1) % groupVents.Count];
        }

        private void PreferHostAsFirstImpostor(List<Player> players)
        {
            if (!_preferHostAsImpostorForTesting || _runner == null)
            {
                return;
            }

            var hostPlayer = players.Find(player => !player.IsBot && player.Object.InputAuthority == _runner.LocalPlayer);
            if (hostPlayer == null)
            {
                return;
            }

            players.Remove(hostPlayer);
            players.Insert(0, hostPlayer);
        }

        private Vector2 GetNextBotTarget(Player bot)
        {
            if (bot.Role == PlayerRole.Crewmate && !bot.AllTasksComplete &&
                TryGetRandomIncompleteTask(bot, out var station))
            {
                return station.Position;
            }

            return GetRandomBotTarget();
        }

        private Vector2 GetRandomBotTarget()
        {
            return ShipMap.GetRandomWalkablePosition();
        }

        private Vector2 GetRandomSabotageTarget()
        {
            if (SabotageStations.Length == 0)
            {
                return GetRandomBotTarget();
            }

            return SabotageStations[UnityEngine.Random.Range(0, SabotageStations.Length)].Position;
        }

        private bool TryGetClosestAliveCrewmate(Player seeker, out Player target)
        {
            target = null;
            var closestDistance = float.MaxValue;

            foreach (var player in GetSpawnedPlayers())
            {
                if (player == seeker || !player.IsAlive || player.Role != PlayerRole.Crewmate)
                {
                    continue;
                }

                var distance = Vector2.Distance(seeker.NetworkedPosition, player.NetworkedPosition);
                if (distance < closestDistance)
                {
                    target = player;
                    closestDistance = distance;
                }
            }

            return target != null;
        }

        public void GetTaskProgressForHud(out int completedTasks, out int totalTasks)
        {
            GetVisibleTaskProgress(out completedTasks, out totalTasks);
        }

        public bool TryGetNearestTaskInfo(
            Player player,
            out string taskName,
            out TaskKind taskKind,
            out float distance,
            out bool inRange)
        {
            taskName = string.Empty;
            taskKind = TaskKind.DataTransfer;
            distance = 0f;
            inRange = false;

            if (player == null || !TryGetNearestIncompleteTask(player, out var station, out distance))
            {
                return false;
            }

            taskName = station.Name;
            taskKind = station.Kind;
            inRange = distance <= _taskUseRange;
            return true;
        }

        public static bool TryGetTaskInfo(int taskId, out string taskName, out TaskKind taskKind)
        {
            taskName = string.Empty;
            taskKind = TaskKind.DataTransfer;
            foreach (var station in TaskStations)
            {
                if (station.Id != taskId)
                {
                    continue;
                }

                taskName = station.Name;
                taskKind = station.Kind;
                return true;
            }

            return false;
        }

        public static string GetTaskInstruction(TaskKind taskKind)
        {
            return taskKind switch
            {
                TaskKind.CircuitPulse => "Press E four times",
                TaskKind.Calibration => "Hold E, then release in the 70-82% target band",
                _ => "Hold E to transfer data"
            };
        }

        public bool TryGetActiveSabotageInfo(
            Player player,
            out string sabotageName,
            out float distance,
            out bool inRange,
            out float countdownRemaining)
        {
            sabotageName = string.Empty;
            distance = 0f;
            inRange = false;
            countdownRemaining = 0f;

            if (player == null ||
                player.ActiveSabotage == SabotageType.None ||
                !TryGetSabotageStation(player.ActiveSabotage, out var station))
            {
                return false;
            }

            sabotageName = station.Name;
            distance = Vector2.Distance(player.NetworkedPosition, station.Position);
            inRange = distance <= _sabotageUseRange;
            countdownRemaining = player.ActiveSabotage == SabotageType.Reactor
                ? Mathf.Max(0f, player.SabotageEndsAt - Time.time)
                : 0f;
            return true;
        }

        public bool TryGetNearestSabotageInfo(Player player, out string sabotageName, out float distance, out bool ready)
        {
            sabotageName = string.Empty;
            distance = 0f;
            ready = false;

            if (player == null || !TryGetNearestSabotageStation(player, out var station, out distance))
            {
                return false;
            }

            sabotageName = station.Name;
            ready = _activeSabotage == SabotageType.None &&
                Time.time >= _nextSabotageAllowedAt &&
                distance <= _sabotageUseRange;
            return true;
        }

        public bool TryGetNearestVentInfo(Player player, out string ventName, out float distance, out bool inRange)
        {
            ventName = string.Empty;
            distance = 0f;
            inRange = false;

            if (player == null || !TryGetNearestVent(player, out var vent, out distance))
            {
                return false;
            }

            ventName = vent.Name;
            inRange = distance <= _ventUseRange;
            return true;
        }

        public float GetSabotageCooldownRemaining()
        {
            return Mathf.Max(0f, _nextSabotageAllowedAt - Time.time);
        }

        public float GetEmergencyCooldownRemaining()
        {
            return Mathf.Max(0f, _nextEmergencyAllowedAt - Time.time);
        }

        public bool IsEmergencyButtonInRange(Player player)
        {
            return player != null &&
                Vector2.Distance(player.NetworkedPosition, EmergencyStationPosition) <= _emergencyUseRange;
        }

        public string GetRoomNameForHud(Player player)
        {
            return player == null ? string.Empty : ShipMap.GetRoomName(player.NetworkedPosition);
        }

        private void EnsureTaskStationMarkers()
        {
            if (GameObject.Find(TaskMarkerRootName) != null)
            {
                return;
            }

            var root = new GameObject(TaskMarkerRootName);
            var markerShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var markerMaterial = markerShader != null ? new Material(markerShader) : null;
            if (markerMaterial != null)
            {
                markerMaterial.color = new Color(0.1f, 0.9f, 0.75f, 1f);
            }

            foreach (var station in TaskStations)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                marker.name = $"Task Station - {station.Name}";
                marker.transform.SetParent(root.transform);
                marker.transform.position = new Vector3(station.Position.x, station.Position.y, -0.25f);
                marker.transform.localScale = new Vector3(_taskMarkerScale, _taskMarkerScale, 1f);
                marker.transform.rotation = Quaternion.Euler(0f, 0f, 45f);

                var collider = marker.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                var renderer = marker.GetComponent<Renderer>();
                if (renderer != null && markerMaterial != null)
                {
                    renderer.material = new Material(markerMaterial);
                    renderer.sortingLayerName = "PropsFront";
                    renderer.sortingOrder = 80;
                    renderer.enabled = false;
                    _taskMarkerRenderers[station.Id] = renderer;
                }
            }
        }

        private void UpdateTaskStationMarkers()
        {
            if (_taskMarkerRenderers.Count == 0)
            {
                return;
            }

            var localPlayer = GetLocalPlayer();
            var showAssignments = localPlayer != null &&
                localPlayer.MatchState == MatchState.Playing &&
                localPlayer.IsAlive &&
                (localPlayer.Role == PlayerRole.Crewmate || localPlayer.Role == PlayerRole.Impostor) &&
                !localPlayer.TaskDeadlineFailed;
            var nearestTaskId = -1;
            var nearestDistance = float.MaxValue;
            if (showAssignments && TryGetNearestIncompleteTask(localPlayer, out var nearest, out nearestDistance))
            {
                nearestTaskId = nearest.Id;
            }

            foreach (var station in TaskStations)
            {
                if (!_taskMarkerRenderers.TryGetValue(station.Id, out var renderer) || renderer == null)
                {
                    continue;
                }

                var active = showAssignments &&
                    localPlayer.HasAssignedTask(station.Id) &&
                    !localPlayer.HasCompletedTask(station.Id);
                renderer.enabled = active;
                if (!active)
                {
                    continue;
                }

                var isNearest = station.Id == nearestTaskId;
                var inRange = isNearest && nearestDistance <= _taskUseRange;
                var pulse = isNearest ? 1f + Mathf.PingPong(Time.time * 0.45f, 0.28f) : 0.72f;
                renderer.transform.localScale = Vector3.one * (_taskMarkerScale * pulse);
                renderer.transform.Rotate(0f, 0f, isNearest ? 42f * Time.deltaTime : 12f * Time.deltaTime);
                var taskColor = localPlayer.Role == PlayerRole.Impostor
                    ? new Color(0.72f, 0.38f, 1f, 0.96f)
                    : new Color(0.13f, 0.9f, 0.78f, 0.96f);
                renderer.material.color = inRange
                    ? new Color(1f, 0.78f, 0.2f, 1f)
                    : isNearest
                        ? taskColor
                        : new Color(taskColor.r, taskColor.g, taskColor.b, 0.48f);
            }
        }

        private void EnsureSabotageStationMarkers()
        {
            if (GameObject.Find(SabotageMarkerRootName) != null)
            {
                return;
            }

            var root = new GameObject(SabotageMarkerRootName);
            var material = CreateMarkerMaterial(new Color(1f, 0.25f, 0.15f, 1f));

            foreach (var station in SabotageStations)
            {
                CreateMarker(root.transform, $"Sabotage - {station.Name}", station.Position, material, 0.65f);
            }
        }

        private void EnsureVentMarkers()
        {
            if (GameObject.Find(VentMarkerRootName) != null)
            {
                return;
            }

            var root = new GameObject(VentMarkerRootName);
            var material = CreateMarkerMaterial(new Color(0.45f, 0.45f, 0.5f, 1f));

            foreach (var vent in VentStations)
            {
                CreateMarker(root.transform, $"Vent - {vent.Name}", vent.Position, material, 0.55f);
            }
        }

        private void EnsureEmergencyMarker()
        {
            if (GameObject.Find(EmergencyMarkerRootName) != null)
            {
                return;
            }

            var root = new GameObject(EmergencyMarkerRootName);
            var material = CreateMarkerMaterial(new Color(1f, 0.9f, 0.18f, 1f));
            CreateMarker(root.transform, "Emergency Button", EmergencyStationPosition, material, 0.85f);
        }

        private static Material CreateMarkerMaterial(Color color)
        {
            var markerShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var markerMaterial = markerShader != null ? new Material(markerShader) : null;
            if (markerMaterial != null)
            {
                markerMaterial.color = color;
            }

            return markerMaterial;
        }

        private static void CreateMarker(Transform root, string markerName, Vector2 position, Material material, float scale)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = markerName;
            marker.transform.SetParent(root);
            marker.transform.position = new Vector3(position.x, position.y, 0.1f);
            marker.transform.localScale = new Vector3(scale, scale, 1f);

            var collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.material = material;
            }
        }

        private static string GetRosterState(Player player)
        {
            if (player.Role == PlayerRole.Spectator)
            {
                return "spectating";
            }

            if (player.IsAlive)
            {
                return player.MatchState == MatchState.Lobby ? "lobby" : "alive";
            }

            return player.IsReportableBody ? "body" : "out";
        }

        private static bool ShouldRevealRole(Player localPlayer, Player listedPlayer)
        {
            if (listedPlayer.Role == PlayerRole.Unassigned || listedPlayer.Role == PlayerRole.Spectator)
            {
                return true;
            }

            if (localPlayer == null)
            {
                return listedPlayer.MatchState == MatchState.Ended;
            }

            return listedPlayer == localPlayer ||
                localPlayer.MatchState == MatchState.Ended ||
                (localPlayer.Role == PlayerRole.Impostor && listedPlayer.Role == PlayerRole.Impostor);
        }

        private static string FormatRole(PlayerRole role)
        {
            return role switch
            {
                PlayerRole.Crewmate => "Crewmate",
                PlayerRole.Impostor => "Impostor",
                PlayerRole.Spectator => "Spectator",
                _ => "Unassigned"
            };
        }

        public string GetRosterStateForHud(Player player)
        {
            return player == null ? string.Empty : GetRosterState(player);
        }

        public bool ShouldRevealRoleForHud(Player localPlayer, Player listedPlayer)
        {
            return listedPlayer != null && ShouldRevealRole(localPlayer, listedPlayer);
        }

        public string FormatRoleForHud(PlayerRole role)
        {
            return FormatRole(role);
        }

        public string FormatPlayerNameForHud(int playerNumber)
        {
            return FormatPlayerName(playerNumber);
        }

        public string FormatVoteTargetForHud(int targetNumber)
        {
            return FormatVoteTarget(targetNumber);
        }

        private string FormatPlayerName(int playerNumber)
        {
            var player = _runner != null && _runner.IsServer ? FindPlayerByNumber(playerNumber) : null;
            if (player == null)
            {
                foreach (var knownPlayer in GetKnownPlayers())
                {
                    if (knownPlayer.PlayerNumber == playerNumber)
                    {
                        player = knownPlayer;
                        break;
                    }
                }
            }

            return player != null ? player.DisplayName : $"Player {playerNumber}";
        }

        private string FormatVoteTarget(int targetNumber)
        {
            return targetNumber == Player.SkipVoteTarget ? "Skip" : FormatPlayerName(targetNumber);
        }

        private static void Shuffle<T>(IList<T> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var swapIndex = UnityEngine.Random.Range(0, i + 1);
                (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
            }
        }

        private Vector2 GetSpawnPosition(int playerIndex)
        {
            var columnCount = Mathf.Max(1, _spawnColumns);
            var column = playerIndex % columnCount;
            var row = playerIndex / columnCount;
            var spawnOrigin = IsLegacySpawnGrid() ? SafeSpawnOrigin : _spawnOrigin;
            var spawnSpacing = IsLegacySpawnGrid() ? SafeSpawnSpacing : _spawnSpacing;
            var fallback = spawnOrigin + new Vector2(column * spawnSpacing.x, -row * spawnSpacing.y);
            return ShipMap.GetSpawnPoint(playerIndex, fallback);
        }

        private Vector2[] CreateRoundSpawnPositions(int playerCount)
        {
            var assigned = new List<Vector2>(playerCount);
            var positions = new Vector2[playerCount];
            for (var i = 0; i < playerCount; i++)
            {
                var position = GetSeparatedRandomWalkablePosition(assigned);
                positions[i] = position;
                assigned.Add(position);
            }

            return positions;
        }

        private static Vector2 GetSeparatedRandomWalkablePosition(IReadOnlyList<Vector2> existingPositions)
        {
            const float minimumDistance = 1.15f;

            for (var attempt = 0; attempt < 64; attempt++)
            {
                var candidate = ShipMap.GetRandomWalkablePosition();
                var hasEnoughSpace = true;
                for (var i = 0; i < existingPositions.Count; i++)
                {
                    if (Vector2.Distance(candidate, existingPositions[i]) < minimumDistance)
                    {
                        hasEnoughSpace = false;
                        break;
                    }
                }

                if (hasEnoughSpace)
                {
                    return candidate;
                }
            }

            return ShipMap.GetRandomWalkablePosition();
        }

        private bool IsLegacySpawnGrid()
        {
            return ShipMap.GetRoomName(_spawnOrigin) == "Ship" ||
                _spawnSpacing.x > 1.2f ||
                _spawnSpacing.y > 1f;
        }

        private static string SanitizeRoomName(string roomName)
        {
            roomName = string.IsNullOrWhiteSpace(roomName) ? "among-us-style" : roomName.Trim();
            return roomName.Replace(" ", "-");
        }

        private async void Shutdown()
        {
            if (_runner == null)
            {
                return;
            }

            _status = "Leaving...";
            await _runner.Shutdown();
            CleanupRunner();
        }

        private void CleanupRunner()
        {
            if (_runner != null)
            {
                _runner.RemoveCallbacks(this);
                Destroy(_runner);
                _runner = null;
            }

            foreach (var sceneManager in GetComponents<NetworkSceneManagerDefault>())
            {
                Destroy(sceneManager);
            }

            _spawnedCharacters.Clear();
            _botCharacters.Clear();
            _botTargets.Clear();
            _botTaskCooldowns.Clear();
            _botActionCooldowns.Clear();
            _channels.Clear();
            _votes.Clear();
            _queuedVoteTargetNumber = null;
            _meetingActive = false;
            _meetingEndsAt = 0f;
            _nextBotVoteAt = 0f;
            _nextPlayerNumber = 1;
            _roundStarted = false;
            _isStarting = false;
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            _status = $"Disconnected: {shutdownReason}";
            CleanupRunner();
        }
        public void OnConnectedToServer(NetworkRunner runner)
        {
            _status = "Connected to server";
        }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            _status = $"Disconnected from server: {reason}";
        }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            _status = $"Connect failed: {reason}";
        }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    }
}
