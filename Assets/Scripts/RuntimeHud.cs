using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AmongUsClone
{
    [DisallowMultipleComponent]
    public class RuntimeHud : MonoBehaviour
    {
        private const int MaxRosterRows = 10;
        private const int MaxVoteButtons = 12;
        private const int MaxControlRows = 7;

        private static readonly Color PanelColor = new Color(0.035f, 0.05f, 0.07f, 0.9f);
        private static readonly Color PanelMutedColor = new Color(0.06f, 0.08f, 0.1f, 0.84f);
        private static readonly Color TextColor = new Color(0.9f, 0.95f, 0.98f, 1f);
        private static readonly Color MutedTextColor = new Color(0.62f, 0.72f, 0.78f, 1f);
        private static readonly Color CrewColor = new Color(0.13f, 0.9f, 0.78f, 1f);
        private static readonly Color AlertColor = new Color(1f, 0.27f, 0.18f, 1f);
        private static readonly Color GoldColor = new Color(1f, 0.78f, 0.2f, 1f);

        private BasicSpawner _spawner;
        private Font _font;
        private GameObject _canvasRoot;
        private GameObject _lobbyPanel;
        private GameObject _sessionPanel;
        private GameObject _rosterPanel;
        private GameObject _actionPanel;
        private GameObject _controlsPanel;
        private GameObject _meetingPanel;
        private GameObject _taskPanel;
        private GameObject _taskFailurePanel;
        private InputField _roomInput;
        private Text _statusText;
        private Text _sessionText;
        private Text _stateText;
        private Text _roleText;
        private Text _taskProgressText;
        private Text _actionTitleText;
        private Text _actionHintText;
        private Text _alertText;
        private Text _meetingTitleText;
        private Text _meetingSubtitleText;
        private Text _meetingVoteText;
        private Text _taskTitleText;
        private Text _taskSubtitleText;
        private Text _taskPercentText;
        private Toggle _preferHostToggle;
        private Button _startRoundButton;
        private Button _hostButton;
        private Button _joinButton;
        private Button _autoButton;
        private Button _leaveButton;
        private Image _crewTaskFill;
        private Image _meetingFill;
        private Image _taskFill;
        private RectTransform _taskScanner;
        private RectTransform _taskFailureBanner;
        private Image _taskFailureBannerImage;
        private Text _taskFailureTitleText;
        private Text _taskFailureSubtitleText;
        private readonly List<Text> _rosterRows = new List<Text>();
        private readonly List<Button> _voteButtons = new List<Button>();
        private readonly List<Text> _voteButtonLabels = new List<Text>();
        private readonly List<Text> _controlRows = new List<Text>();
        private readonly List<Image> _taskWireFills = new List<Image>();
        private string _lastRoomName = string.Empty;
        private AudioSource _taskAlertAudioSource;
        private AudioClip _taskAlertClip;
        private AudioClip _taskFailureClip;
        private int _observedPlayerNumber = -1;
        private int _observedTimedTaskId = -1;
        private float _taskAlertUntil;
        private string _taskAlertMessage = string.Empty;
        private bool _observedTaskFailure;

        private void Awake()
        {
            _spawner = GetComponent<BasicSpawner>();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                Resources.GetBuiltinResource<Font>("Arial.ttf");
            EnsureEventSystem();
            BuildHud();
            EnsureTaskAlertAudio();
        }

        private void Update()
        {
            if (_spawner == null)
            {
                _spawner = BasicSpawner.Active;
                if (_spawner == null)
                {
                    return;
                }
            }

            UpdateLobby();
            UpdateTimedTaskAlert();
            UpdateSession();
            UpdateRoster();
            UpdateActionPanel();
            UpdateControlsPanel();
            UpdateMeetingPanel();
            UpdateTaskPanel();
            UpdateTaskFailureCutIn();
        }

        private void BuildHud()
        {
            _canvasRoot = new GameObject("Runtime Canvas HUD");
            _canvasRoot.transform.SetParent(transform, false);

            var canvas = _canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = _canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRoot.AddComponent<GraphicRaycaster>();

            _lobbyPanel = CreatePanel("Lobby", _canvasRoot.transform, new Vector2(440f, 314f), new Vector2(0.5f, 0.5f), Vector2.zero, PanelColor);
            AddVerticalLayout(_lobbyPanel, 16, 12);
            CreateText(_lobbyPanel.transform, "Among Us Style", 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            CreateText(_lobbyPanel.transform, "Fusion multiplayer prototype", 15, FontStyle.Normal, TextAnchor.MiddleCenter, MutedTextColor);
            _roomInput = CreateInput(_lobbyPanel.transform, "Room name");
            _hostButton = CreateButton(_lobbyPanel.transform, "Host", () => _spawner.RequestHost(), CrewColor);
            _joinButton = CreateButton(_lobbyPanel.transform, "Join", () => _spawner.RequestJoin(), new Color(0.35f, 0.62f, 1f, 1f));
            _autoButton = CreateButton(_lobbyPanel.transform, "Auto Host Or Join", () => _spawner.RequestAutoHostOrJoin(), GoldColor);
            _statusText = CreateText(_lobbyPanel.transform, string.Empty, 14, FontStyle.Normal, TextAnchor.MiddleCenter, MutedTextColor);

            _sessionPanel = CreatePanel("Session", _canvasRoot.transform, new Vector2(360f, 246f), new Vector2(0f, 1f), new Vector2(18f, -18f), PanelColor);
            AddVerticalLayout(_sessionPanel, 12, 8);
            _sessionText = CreateText(_sessionPanel.transform, string.Empty, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            _stateText = CreateText(_sessionPanel.transform, string.Empty, 14, FontStyle.Normal, TextAnchor.MiddleLeft, MutedTextColor);
            _roleText = CreateText(_sessionPanel.transform, string.Empty, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
            _taskProgressText = CreateText(_sessionPanel.transform, string.Empty, 13, FontStyle.Normal, TextAnchor.MiddleLeft, MutedTextColor);
            _crewTaskFill = CreateBar(_sessionPanel.transform, CrewColor, 10f);
            _preferHostToggle = CreateToggle(_sessionPanel.transform, "Make host Impostor for tests");
            _startRoundButton = CreateButton(_sessionPanel.transform, "Start Game", () => _spawner.RequestStartOrRestartRound(), GoldColor);
            _leaveButton = CreateButton(_sessionPanel.transform, "Leave", () => _spawner.RequestLeave(), new Color(0.9f, 0.24f, 0.22f, 1f));

            _rosterPanel = CreatePanel("Roster", _canvasRoot.transform, new Vector2(282f, 392f), new Vector2(1f, 1f), new Vector2(-18f, -18f), PanelMutedColor);
            AddVerticalLayout(_rosterPanel, 12, 5);
            CreateText(_rosterPanel.transform, "Players", 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            for (var i = 0; i < MaxRosterRows; i++)
            {
                _rosterRows.Add(CreateText(_rosterPanel.transform, string.Empty, 13, FontStyle.Normal, TextAnchor.MiddleLeft, MutedTextColor));
            }

            _actionPanel = CreatePanel("Action Bar", _canvasRoot.transform, new Vector2(600f, 122f), new Vector2(0.5f, 0f), new Vector2(-54f, 18f), PanelColor);
            AddVerticalLayout(_actionPanel, 12, 6);
            _actionTitleText = CreateText(_actionPanel.transform, string.Empty, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            _actionHintText = CreateText(_actionPanel.transform, string.Empty, 13, FontStyle.Normal, TextAnchor.MiddleCenter, MutedTextColor);
            _alertText = CreateText(_actionPanel.transform, string.Empty, 14, FontStyle.Bold, TextAnchor.MiddleCenter, AlertColor);

            _controlsPanel = CreatePanel("Controls", _canvasRoot.transform, new Vector2(292f, 218f), new Vector2(1f, 0f), new Vector2(-18f, 18f), PanelMutedColor);
            AddVerticalLayout(_controlsPanel, 12, 4);
            CreateText(_controlsPanel.transform, "Controls", 16, FontStyle.Bold, TextAnchor.MiddleLeft);
            for (var i = 0; i < MaxControlRows; i++)
            {
                _controlRows.Add(CreateText(_controlsPanel.transform, string.Empty, 12, FontStyle.Normal, TextAnchor.MiddleLeft, MutedTextColor));
            }

            _meetingPanel = CreatePanel("Meeting", _canvasRoot.transform, new Vector2(560f, 612f), new Vector2(0.5f, 0.5f), Vector2.zero, PanelColor);
            AddVerticalLayout(_meetingPanel, 14, 8);
            _meetingTitleText = CreateText(_meetingPanel.transform, "Meeting", 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            _meetingSubtitleText = CreateText(_meetingPanel.transform, string.Empty, 15, FontStyle.Normal, TextAnchor.MiddleCenter, MutedTextColor);
            _meetingFill = CreateBar(_meetingPanel.transform, GoldColor, 14f);
            _meetingVoteText = CreateText(_meetingPanel.transform, string.Empty, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
            for (var i = 0; i < MaxVoteButtons; i++)
            {
                var index = i;
                var button = CreateButton(_meetingPanel.transform, string.Empty, () => SubmitVoteFromButton(index), new Color(0.2f, 0.31f, 0.42f, 1f));
                _voteButtons.Add(button);
                _voteButtonLabels.Add(button.GetComponentInChildren<Text>());
            }

            _taskPanel = CreatePanel("Task Panel", _canvasRoot.transform, new Vector2(540f, 190f), new Vector2(0.5f, 0f), new Vector2(0f, 154f), PanelColor);
            AddVerticalLayout(_taskPanel, 14, 8);
            _taskTitleText = CreateText(_taskPanel.transform, string.Empty, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            _taskSubtitleText = CreateText(_taskPanel.transform, string.Empty, 13, FontStyle.Normal, TextAnchor.MiddleCenter, MutedTextColor);
            _taskFill = CreateBar(_taskPanel.transform, CrewColor, 16f);
            _taskPercentText = CreateText(_taskPanel.transform, string.Empty, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
            CreateTaskVisualizer(_taskPanel.transform);
            CreateTaskFailureCutIn();
        }

        private void UpdateLobby()
        {
            var showLobby = !_spawner.IsConnected || _spawner.IsStarting;
            _lobbyPanel.SetActive(showLobby);
            if (!showLobby)
            {
                return;
            }

            if (!_roomInput.isFocused && _lastRoomName != _spawner.RoomName)
            {
                _roomInput.text = _spawner.RoomName;
                _lastRoomName = _spawner.RoomName;
            }

            var canStartConnection = !_spawner.IsStarting;
            _hostButton.interactable = canStartConnection;
            _joinButton.interactable = canStartConnection;
            _autoButton.interactable = canStartConnection;
            _statusText.text = _spawner.StatusText;
        }

        private void UpdateSession()
        {
            var connected = _spawner.IsConnected && !_spawner.IsStarting;
            _sessionPanel.SetActive(connected);
            if (!connected)
            {
                return;
            }

            var localPlayer = _spawner.LocalPlayer;
            _sessionText.text = $"{_spawner.SessionName}  /  {_spawner.GameModeText}";
            var location = localPlayer == null ? "Spawning" : _spawner.GetRoomNameForHud(localPlayer);
            _stateText.text = $"Participants: {_spawner.VisiblePlayerCount} ({_spawner.VisibleCpuCount} CPU)   Location: {location}";
            _roleText.text = localPlayer == null
                ? "State: Spawning"
                : $"State: {localPlayer.MatchState}   You: {localPlayer.Role}   {(localPlayer.IsAlive ? "Alive" : "Out")}";

            _spawner.GetTaskProgressForHud(out var completedTasks, out var totalTasks);
            var taskSummary = localPlayer != null && localPlayer.Role == PlayerRole.Crewmate && localPlayer.AssignedTaskCount > 0
                ? $"Your tasks: {localPlayer.CompletedTaskCount}/{localPlayer.AssignedTaskCount}   Crew total: {completedTasks}/{totalTasks}"
                : totalTasks > 0 ? $"Crew tasks: {completedTasks}/{totalTasks}" : "Crew tasks: waiting";
            var deadlineRemaining = localPlayer == null || localPlayer.TaskDeadlineEndsAt <= 0f
                ? 0f
                : Mathf.Max(0f, localPlayer.TaskDeadlineEndsAt - Time.time);
            var showDeadline = localPlayer != null &&
                (localPlayer.MatchState == MatchState.Playing || localPlayer.MatchState == MatchState.Meeting) &&
                localPlayer.TaskDeadlineEndsAt > 0f;
            _taskProgressText.text = showDeadline ? $"{taskSummary}   Deadline: {deadlineRemaining:0}s" : taskSummary;
            _taskProgressText.color = showDeadline && deadlineRemaining <= 30f ? AlertColor : MutedTextColor;
            SetBar(_crewTaskFill, totalTasks <= 0 ? 0f : completedTasks / (float)totalTasks);

            _preferHostToggle.gameObject.SetActive(_spawner.IsServer && !_spawner.RoundStarted);
            _preferHostToggle.interactable = !_spawner.RoundStarted;
            _preferHostToggle.isOn = _spawner.PreferHostAsImpostorForTesting;
            _startRoundButton.gameObject.SetActive(_spawner.IsServer);
            _startRoundButton.GetComponentInChildren<Text>().text = _spawner.RoundStarted ? "Restart Game" : "Start Game";
            _startRoundButton.interactable = _spawner.CanStartRound;
        }

        private void UpdateRoster()
        {
            var connected = _spawner.IsConnected && !_spawner.IsStarting;
            _rosterPanel.SetActive(connected);
            if (!connected)
            {
                return;
            }

            var localPlayer = _spawner.LocalPlayer;
            var players = _spawner.KnownPlayers;
            for (var i = 0; i < _rosterRows.Count; i++)
            {
                var row = _rosterRows[i];
                var active = i < players.Length;
                row.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                var player = players[i];
                var state = player.MatchState == MatchState.Meeting && player.CanVote
                    ? (player.HasVoted ? "voted" : "voting")
                    : _spawner.GetRosterStateForHud(player);
                var role = _spawner.ShouldRevealRoleForHud(localPlayer, player)
                    ? $" / {_spawner.FormatRoleForHud(player.Role)}"
                    : string.Empty;
                row.text = $"{player.DisplayName}: {state}{role}";
                row.color = player.IsAlive ? TextColor : MutedTextColor;
            }
        }

        private void UpdateActionPanel()
        {
            var localPlayer = _spawner.LocalPlayer;
            var visible = _spawner.IsConnected && !_spawner.IsStarting && localPlayer != null && localPlayer.MatchState != MatchState.Meeting;
            _actionPanel.SetActive(visible);
            if (!visible)
            {
                return;
            }

            if (localPlayer.MatchState == MatchState.Ended)
            {
                _actionTitleText.text = localPlayer.WinningTeam == WinningTeam.None
                    ? "Round Ended"
                    : $"{localPlayer.WinningTeam} Win";
                _actionHintText.text = _spawner.IsServer ? "Use Restart Game to play another round." : "Waiting for host.";
                _alertText.text = GetAnnouncementText(localPlayer);
                return;
            }

            _actionTitleText.text = localPlayer.IsImpostor ? "Impostor" : "Crewmate";
            _actionHintText.text = GetPrimaryHint(localPlayer);
            _alertText.text = GetAlertText(localPlayer);
            _alertText.color = Time.time < _taskAlertUntil
                ? Color.Lerp(GoldColor, AlertColor, Mathf.PingPong(Time.time * 5f, 1f))
                : AlertColor;
        }

        private void UpdateTimedTaskAlert()
        {
            var localPlayer = _spawner.LocalPlayer;
            if (localPlayer == null)
            {
                _observedPlayerNumber = -1;
                _observedTimedTaskId = -1;
                return;
            }

            if (_observedPlayerNumber != localPlayer.PlayerNumber)
            {
                _observedPlayerNumber = localPlayer.PlayerNumber;
                _observedTimedTaskId = localPlayer.LastAssignedTaskId;
                return;
            }

            if (localPlayer.LastAssignedTaskId < 0)
            {
                _observedTimedTaskId = -1;
                return;
            }

            if (localPlayer.LastAssignedTaskId == _observedTimedTaskId ||
                localPlayer.MatchState != MatchState.Playing ||
                localPlayer.Role != PlayerRole.Crewmate)
            {
                return;
            }

            _observedTimedTaskId = localPlayer.LastAssignedTaskId;
            var taskName = BasicSpawner.TryGetTaskInfo(localPlayer.LastAssignedTaskId, out var name, out _)
                ? name
                : "New assignment";
            _taskAlertMessage = $"NEW TASK: {taskName}";
            _taskAlertUntil = Time.time + 6f;
            if (_taskAlertAudioSource != null && _taskAlertClip != null)
            {
                _taskAlertAudioSource.PlayOneShot(_taskAlertClip, 0.65f);
            }
        }

        private void UpdateTaskFailureCutIn()
        {
            var localPlayer = _spawner.LocalPlayer;
            var active = localPlayer != null &&
                localPlayer.TaskDeadlineFailed &&
                localPlayer.MatchState != MatchState.Ended &&
                Time.time < localPlayer.TaskFailureCutInEndsAt;
            _taskFailurePanel.SetActive(active);

            if (localPlayer == null || !localPlayer.TaskDeadlineFailed)
            {
                _observedTaskFailure = false;
            }

            if (!active)
            {
                return;
            }

            if (!_observedTaskFailure)
            {
                _observedTaskFailure = true;
                if (_taskAlertAudioSource != null && _taskFailureClip != null)
                {
                    _taskAlertAudioSource.PlayOneShot(_taskFailureClip, 0.8f);
                }
            }

            var duration = Mathf.Max(1f, _spawner.TaskFailureCutInDurationSeconds);
            var remaining = Mathf.Max(0f, localPlayer.TaskFailureCutInEndsAt - Time.time);
            var progress = 1f - Mathf.Clamp01(remaining / duration);
            var entrance = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.16f));
            var exit = remaining < 0.35f ? Mathf.Clamp01(remaining / 0.35f) : 1f;

            _taskFailureBanner.anchoredPosition = new Vector2(Mathf.Lerp(-1500f, 0f, entrance), 0f);
            _taskFailureBanner.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, entrance) * exit;
            _taskFailureBannerImage.color = Color.Lerp(
                new Color(0.18f, 0.01f, 0.015f, 0.98f),
                new Color(0.48f, 0.015f, 0.02f, 0.98f),
                Mathf.PingPong(Time.time * 3.5f, 1f));
            _taskFailureTitleText.color = Color.Lerp(TextColor, AlertColor, Mathf.PingPong(Time.time * 5f, 1f));
            _taskFailureSubtitleText.text = $"OBJECTIVES INCOMPLETE\nIMPOSTORS WIN IN {remaining:0.0}s";
        }

        private void UpdateControlsPanel()
        {
            var visible = _spawner.IsConnected && !_spawner.IsStarting;
            _controlsPanel.SetActive(visible);
            if (!visible)
            {
                return;
            }

            var localPlayer = _spawner.LocalPlayer;
            if (localPlayer != null && localPlayer.MatchState == MatchState.Meeting)
            {
                SetControlRows(
                    "Vote: Click a player",
                    "Skip: Click Skip Vote",
                    "Timer: Vote before it ends");
                return;
            }

            SetControlRows(
                "Move: WASD / Arrow",
                "Task / Emergency: E",
                "Kill: Q",
                "Report: R",
                "Sabotage: F",
                "Vent: V",
                "Vote: Meeting buttons");
        }

        private void UpdateMeetingPanel()
        {
            var localPlayer = _spawner.LocalPlayer;
            var inMeeting = _spawner.IsConnected && localPlayer != null && localPlayer.MatchState == MatchState.Meeting;
            _meetingPanel.SetActive(inMeeting);
            if (!inMeeting)
            {
                return;
            }

            _meetingTitleText.text = localPlayer.MeetingBodyNumber > 0 ? "Body Reported" : "Emergency Meeting";
            _meetingSubtitleText.text = localPlayer.MeetingBodyNumber > 0
                ? $"{_spawner.FormatPlayerNameForHud(localPlayer.MeetingReporterNumber)} reported {_spawner.FormatPlayerNameForHud(localPlayer.MeetingBodyNumber)}"
                : $"{_spawner.FormatPlayerNameForHud(localPlayer.MeetingReporterNumber)} called a meeting";

            var remaining = Mathf.Max(0f, localPlayer.MeetingEndsAt - Time.time);
            SetBar(_meetingFill, _spawner.MeetingDurationSeconds <= 0f ? 0f : remaining / _spawner.MeetingDurationSeconds);
            _meetingVoteText.text = _spawner.IsServer
                ? $"Votes: {_spawner.VoteCount}/{_spawner.EligibleVoterCount}   Time: {remaining:0.0}s"
                : $"Time: {remaining:0.0}s";

            var players = _spawner.KnownPlayers;
            var buttonIndex = 0;
            for (var i = 0; i < players.Length && buttonIndex < _voteButtons.Count - 1; i++)
            {
                var player = players[i];
                if (!player.IsAlive || player.Role == PlayerRole.Spectator)
                {
                    continue;
                }

                SetVoteButton(buttonIndex, player.PlayerNumber, _spawner.FormatPlayerNameForHud(player.PlayerNumber), localPlayer);
                buttonIndex++;
            }

            if (buttonIndex < _voteButtons.Count)
            {
                SetVoteButton(buttonIndex, Player.SkipVoteTarget, "Skip Vote", localPlayer);
                buttonIndex++;
            }

            for (var i = buttonIndex; i < _voteButtons.Count; i++)
            {
                _voteButtons[i].gameObject.SetActive(false);
            }
        }

        private void UpdateTaskPanel()
        {
            var localPlayer = _spawner.LocalPlayer;
            var visible = _spawner.IsConnected &&
                localPlayer != null &&
                localPlayer.MatchState == MatchState.Playing &&
                localPlayer.ActiveInteraction != InteractionKind.None;
            _taskPanel.SetActive(visible);
            if (!visible)
            {
                return;
            }

            var isRepair = localPlayer.ActiveInteraction == InteractionKind.Repair;
            var progress = localPlayer.InteractionNormalized;
            var taskKind = TaskKind.DataTransfer;
            var taskColor = CrewColor;
            if (isRepair)
            {
                _spawner.TryGetActiveSabotageInfo(localPlayer, out var sabotageName, out _, out _, out _);
                _taskTitleText.text = $"Repair: {sabotageName}";
                _taskSubtitleText.text = "Hold E to stabilize the system.";
                _taskFill.color = AlertColor;
            }
            else
            {
                _spawner.TryGetNearestTaskInfo(localPlayer, out var taskName, out taskKind, out _, out _);
                _taskTitleText.text = string.IsNullOrEmpty(taskName) ? "Task" : $"{taskName} Task";
                _taskSubtitleText.text = BasicSpawner.GetTaskInstruction(taskKind) + ".";
                taskColor = taskKind == TaskKind.CircuitPulse
                    ? GoldColor
                    : taskKind == TaskKind.Calibration && progress >= 0.7f && progress <= 0.82f
                        ? CrewColor
                        : taskKind == TaskKind.Calibration ? GoldColor : CrewColor;
                _taskFill.color = taskColor;
            }

            SetBar(_taskFill, progress);
            _taskPercentText.text = $"{progress * 100f:0}%";
            for (var i = 0; i < _taskWireFills.Count; i++)
            {
                var rowProgress = Mathf.Clamp01(progress * 1.35f - i * 0.15f);
                SetBar(_taskWireFills[i], rowProgress);
                _taskWireFills[i].color = isRepair ? AlertColor : taskColor;
            }

            if (_taskScanner != null)
            {
                var scannerX = Mathf.Lerp(-222f, 222f, Mathf.PingPong(Time.time * 0.65f + progress, 1f));
                _taskScanner.anchoredPosition = new Vector2(scannerX, _taskScanner.anchoredPosition.y);
            }
        }

        private string GetPrimaryHint(Player localPlayer)
        {
            if (localPlayer == null)
            {
                return "Spawning...";
            }

            if (!localPlayer.IsAlive)
            {
                return "You are out. Watch the remaining players.";
            }

            if (_spawner.TryGetActiveSabotageInfo(localPlayer, out var sabotageName, out var sabotageDistance, out var sabotageInRange, out _))
            {
                if (localPlayer.Role == PlayerRole.Crewmate)
                {
                    return sabotageInRange
                        ? $"Hold E to repair {sabotageName}."
                        : $"Repair {sabotageName}: {sabotageDistance:0.0}m away.";
                }

                return $"Sabotage active: {sabotageName}.";
            }

            if (localPlayer.Role == PlayerRole.Crewmate)
            {
                if (localPlayer.AllTasksComplete)
                {
                    return "All tasks complete. Stay alive and report bodies.";
                }

                if (_spawner.TryGetNearestTaskInfo(localPlayer, out var taskName, out var taskKind, out var distance, out var inRange))
                {
                    return inRange
                        ? $"{BasicSpawner.GetTaskInstruction(taskKind)}: {taskName}."
                        : $"Next task: {taskName}, {distance:0.0}m away.";
                }
            }

            if (localPlayer.Role == PlayerRole.Impostor)
            {
                var kill = localPlayer.KillCooldownRemaining <= 0f
                    ? "Q kill ready"
                    : $"Q kill in {localPlayer.KillCooldownRemaining:0.0}s";
                var sabotage = _spawner.TryGetNearestSabotageInfo(localPlayer, out var sabotageConsole, out var nearestSabotageDistance, out var sabotageReady)
                    ? (sabotageReady ? $"F sabotage {sabotageConsole}" : $"Sabotage panel: {sabotageConsole} {nearestSabotageDistance:0.0}m")
                    : "No sabotage panel";
                var vent = _spawner.TryGetNearestVentInfo(localPlayer, out var ventName, out var ventDistance, out var ventReady)
                    ? (ventReady ? $"V vent at {ventName}" : $"Vent: {ventName} {ventDistance:0.0}m")
                    : "No vent";
                return $"{kill}   {sabotage}   {vent}";
            }

            return "Move with WASD or arrow keys.";
        }

        private string GetAlertText(Player localPlayer)
        {
            var announcement = GetAnnouncementText(localPlayer);
            if (!string.IsNullOrEmpty(announcement))
            {
                return announcement;
            }

            if (Time.time < _taskAlertUntil)
            {
                return _taskAlertMessage;
            }

            if (localPlayer.TaskDeadlineEndsAt > 0f &&
                (localPlayer.MatchState == MatchState.Playing || localPlayer.MatchState == MatchState.Meeting))
            {
                var deadlineRemaining = Mathf.Max(0f, localPlayer.TaskDeadlineEndsAt - Time.time);
                if (deadlineRemaining <= 30f)
                {
                    return $"TASK DEADLINE: {deadlineRemaining:0.0}s";
                }
            }

            if (_spawner.TryGetActiveSabotageInfo(localPlayer, out var sabotageName, out _, out _, out var remaining) &&
                localPlayer.ActiveSabotage == SabotageType.Reactor)
            {
                return $"Reactor meltdown: {remaining:0.0}s";
            }

            if (_spawner.IsEmergencyButtonInRange(localPlayer))
            {
                var cooldown = _spawner.GetEmergencyCooldownRemaining();
                return cooldown <= 0f ? "E emergency meeting ready" : $"Emergency cooldown: {cooldown:0.0}s";
            }

            if (localPlayer.Role == PlayerRole.Impostor)
            {
                var cooldown = _spawner.GetSabotageCooldownRemaining();
                if (cooldown > 0f)
                {
                    return $"Sabotage cooldown: {cooldown:0.0}s";
                }
            }

            return string.Empty;
        }

        private void EnsureTaskAlertAudio()
        {
            _taskAlertAudioSource = gameObject.AddComponent<AudioSource>();
            _taskAlertAudioSource.playOnAwake = false;
            _taskAlertAudioSource.spatialBlend = 0f;

            const int sampleRate = 44100;
            const float duration = 0.42f;
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var time = i / (float)sampleRate;
                var firstPulse = time < 0.12f;
                var secondPulse = time >= 0.2f && time < 0.36f;
                if (!firstPulse && !secondPulse)
                {
                    continue;
                }

                var pulseTime = firstPulse ? time : time - 0.2f;
                var pulseDuration = firstPulse ? 0.12f : 0.16f;
                var frequency = firstPulse ? 880f : 1040f;
                var envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(pulseTime / pulseDuration));
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * pulseTime) * envelope * 0.35f;
            }

            _taskAlertClip = AudioClip.Create("Timed Task Alert", sampleCount, 1, sampleRate, false);
            _taskAlertClip.SetData(samples, 0);

            const float failureDuration = 1.3f;
            var failureSampleCount = Mathf.CeilToInt(sampleRate * failureDuration);
            var failureSamples = new float[failureSampleCount];
            for (var i = 0; i < failureSampleCount; i++)
            {
                var time = i / (float)sampleRate;
                var pulse = Mathf.Repeat(time, 0.28f);
                if (pulse >= 0.2f)
                {
                    continue;
                }

                var frequency = Mathf.FloorToInt(time / 0.28f) % 2 == 0 ? 220f : 165f;
                var envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(pulse / 0.2f));
                failureSamples[i] = Mathf.Sin(2f * Mathf.PI * frequency * pulse) * envelope * 0.42f;
            }

            _taskFailureClip = AudioClip.Create("Task Deadline Failure", failureSampleCount, 1, sampleRate, false);
            _taskFailureClip.SetData(failureSamples, 0);
        }

        private string GetAnnouncementText(Player localPlayer)
        {
            if (localPlayer == null ||
                localPlayer.Announcement == AnnouncementType.None ||
                Time.time >= localPlayer.AnnouncementEndsAt)
            {
                return string.Empty;
            }

            if (localPlayer.Announcement == AnnouncementType.MeetingResult)
            {
                return localPlayer.AnnouncementPlayerNumber <= 0
                    ? "Meeting result: no one was ejected."
                    : $"Meeting result: {_spawner.FormatPlayerNameForHud(localPlayer.AnnouncementPlayerNumber)} was ejected ({_spawner.FormatRoleForHud(localPlayer.AnnouncementRole)}).";
            }

            return localPlayer.WinningTeam == WinningTeam.None ? "Round ended." : $"{localPlayer.WinningTeam} win.";
        }

        private void SetVoteButton(int index, int targetNumber, string label, Player localPlayer)
        {
            var button = _voteButtons[index];
            button.gameObject.SetActive(true);
            button.interactable = localPlayer.CanVote && !localPlayer.HasVoted;
            button.name = $"Vote {targetNumber}";
            _voteButtonLabels[index].text = localPlayer.HasVoted && localPlayer.VotedTargetNumber == targetNumber
                ? $"{label}  selected"
                : label;
        }

        private void SubmitVoteFromButton(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex >= _voteButtons.Count)
            {
                return;
            }

            var name = _voteButtons[buttonIndex].name;
            if (!name.StartsWith("Vote ") || !int.TryParse(name.Substring(5), out var targetNumber))
            {
                return;
            }

            _spawner.RequestVote(targetNumber);
        }

        private void SetControlRows(params string[] rows)
        {
            for (var i = 0; i < _controlRows.Count; i++)
            {
                var active = i < rows.Length;
                _controlRows[i].gameObject.SetActive(active);
                if (active)
                {
                    _controlRows[i].text = rows[i];
                }
            }
        }

        private void CreateTaskVisualizer(Transform parent)
        {
            var visual = CreatePanel("Task Visualizer", parent, new Vector2(480f, 56f), new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.02f, 0.028f, 0.035f, 0.72f));
            var layout = visual.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 6f;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;

            for (var i = 0; i < 3; i++)
            {
                _taskWireFills.Add(CreateBar(visual.transform, CrewColor, 8f));
            }

            var scanner = new GameObject("Scanner");
            scanner.transform.SetParent(visual.transform, false);
            _taskScanner = scanner.AddComponent<RectTransform>();
            _taskScanner.anchorMin = new Vector2(0.5f, 0.5f);
            _taskScanner.anchorMax = new Vector2(0.5f, 0.5f);
            _taskScanner.sizeDelta = new Vector2(8f, 46f);
            scanner.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.55f);
        }

        private void CreateTaskFailureCutIn()
        {
            _taskFailurePanel = CreatePanel(
                "Task Deadline Cut-In",
                _canvasRoot.transform,
                new Vector2(1600f, 900f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Color(0.01f, 0.005f, 0.008f, 0.86f));

            var banner = CreatePanel(
                "Failure Banner",
                _taskFailurePanel.transform,
                new Vector2(960f, 300f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Color(0.32f, 0.01f, 0.02f, 0.98f));
            _taskFailureBanner = banner.GetComponent<RectTransform>();
            _taskFailureBannerImage = banner.GetComponent<Image>();
            AddVerticalLayout(banner, 32, 14f);
            _taskFailureTitleText = CreateText(banner.transform, "TASK DEADLINE EXPIRED", 42, FontStyle.Bold, TextAnchor.MiddleCenter, AlertColor);
            _taskFailureSubtitleText = CreateText(banner.transform, string.Empty, 24, FontStyle.Bold, TextAnchor.MiddleCenter, TextColor);
            _taskFailureSubtitleText.rectTransform.sizeDelta = new Vector2(0f, 92f);
            _taskFailurePanel.SetActive(false);
        }

        private GameObject CreatePanel(string panelName, Transform parent, Vector2 size, Vector2 anchor, Vector2 anchoredPosition, Color color)
        {
            var panel = new GameObject(panelName);
            panel.transform.SetParent(parent, false);
            var rectTransform = panel.AddComponent<RectTransform>();
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = anchor;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
            panel.AddComponent<Image>().color = color;
            return panel;
        }

        private static void AddVerticalLayout(GameObject target, int padding, float spacing)
        {
            var layout = target.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private Text CreateText(Transform parent, string text, int size, FontStyle style, TextAnchor alignment, Color? color = null)
        {
            var textObject = new GameObject("Text");
            textObject.transform.SetParent(parent, false);
            var rectTransform = textObject.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0f, Mathf.Max(22f, size + 8f));
            var label = textObject.AddComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color ?? TextColor;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private InputField CreateInput(Transform parent, string placeholder)
        {
            var inputObject = new GameObject("Input");
            inputObject.transform.SetParent(parent, false);
            var rectTransform = inputObject.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0f, 42f);
            inputObject.AddComponent<Image>().color = new Color(0.9f, 0.95f, 1f, 0.12f);

            var input = inputObject.AddComponent<InputField>();
            var text = CreateText(inputObject.transform, string.Empty, 16, FontStyle.Normal, TextAnchor.MiddleLeft, TextColor);
            var placeholderText = CreateText(inputObject.transform, placeholder, 16, FontStyle.Normal, TextAnchor.MiddleLeft, MutedTextColor);
            FitToParent(text.rectTransform, new Vector2(12f, 0f), new Vector2(-12f, 0f));
            FitToParent(placeholderText.rectTransform, new Vector2(12f, 0f), new Vector2(-12f, 0f));
            input.textComponent = text;
            input.placeholder = placeholderText;
            input.characterLimit = 32;
            input.onValueChanged.AddListener(value =>
            {
                _lastRoomName = value;
                if (_spawner != null)
                {
                    _spawner.RoomName = value;
                }
            });
            return input;
        }

        private Toggle CreateToggle(Transform parent, string label)
        {
            var toggleObject = new GameObject("Toggle");
            toggleObject.transform.SetParent(parent, false);
            var rectTransform = toggleObject.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0f, 28f);
            var toggle = toggleObject.AddComponent<Toggle>();

            var box = new GameObject("Checkmark Box");
            box.transform.SetParent(toggleObject.transform, false);
            var boxRect = box.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0f, 0.5f);
            boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.anchoredPosition = Vector2.zero;
            boxRect.sizeDelta = new Vector2(22f, 22f);
            box.AddComponent<Image>().color = new Color(0.9f, 0.95f, 1f, 0.16f);

            var check = new GameObject("Checkmark");
            check.transform.SetParent(box.transform, false);
            FitToParent(check.AddComponent<RectTransform>(), new Vector2(5f, 5f), new Vector2(-5f, -5f));
            check.AddComponent<Image>().color = CrewColor;
            toggle.graphic = check.GetComponent<Image>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.onValueChanged.AddListener(value =>
            {
                if (_spawner != null)
                {
                    _spawner.PreferHostAsImpostorForTesting = value;
                }
            });

            var labelText = CreateText(toggleObject.transform, label, 13, FontStyle.Normal, TextAnchor.MiddleLeft, MutedTextColor);
            FitToParent(labelText.rectTransform, new Vector2(32f, 0f), Vector2.zero);
            return toggle;
        }

        private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action, Color color)
        {
            var buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent, false);
            var rectTransform = buttonObject.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0f, 38f);
            var image = buttonObject.AddComponent<Image>();
            image.color = color;
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var text = CreateText(buttonObject.transform, label, 15, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            FitToParent(text.rectTransform, Vector2.zero, Vector2.zero);

            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.14f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.disabledColor = new Color(0.26f, 0.3f, 0.33f, 0.55f);
            button.colors = colors;
            return button;
        }

        private Image CreateBar(Transform parent, Color fillColor, float height)
        {
            var frame = new GameObject("Bar");
            frame.transform.SetParent(parent, false);
            var frameRect = frame.AddComponent<RectTransform>();
            frameRect.sizeDelta = new Vector2(0f, height);
            frame.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(frame.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = fillColor;
            return fillImage;
        }

        private static void SetBar(Image fill, float normalized)
        {
            if (fill == null)
            {
                return;
            }

            var rectTransform = fill.rectTransform;
            rectTransform.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
        }

        private static void FitToParent(RectTransform rectTransform, Vector2 minOffset, Vector2 maxOffset)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = minOffset;
            rectTransform.offsetMax = maxOffset;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }
}
