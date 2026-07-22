using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AmongUsClone
{
    public static class ShipMap
    {
        private const string DefaultMapResourcePath = "Maps/production_room_preview_01";
        private const string ForgottenPlainsSceneName = "ForgottenPlainsGamePrototype";
        private const string ForgottenPlainsLargeSceneName = "ForgottenPlainsLargePrototype";
        private const string AstraResearchComplexSceneName = "AstraResearchComplex_Blockout";
        private const string ForgottenPlainsMapResourcePath = "Maps/forgotten_plains_game_prototype";
        private const string ForgottenPlainsLargeMapResourcePath = "Maps/forgotten_plains_large_prototype";
        private const string AstraResearchComplexMapResourcePath = "Maps/astra_research_complex_blockout_01";
        private const string RootName = "Ship Map";
        private const string AstraRuntimeBlockoutRootName = "Astra Runtime Visible Blockout";
        private const float WallThickness = 0.12f;
        private const float PreviewCameraSize = 6f;

        private static readonly RoomPrefabPlacement[] ProductionRoomPlacements =
        {
            new RoomPrefabPlacement("RoomPrefabs/Production/CentralMeetingRoom", "Central Meeting Room", Vector2.zero),
            new RoomPrefabPlacement("RoomPrefabs/Production/ReactorRoom", "Reactor Room", new Vector2(-29f, 0f)),
            new RoomPrefabPlacement("RoomPrefabs/Production/MedicalRoom", "Medical Room", new Vector2(28f, 14f))
        };

        private static RuntimeMap _map;
        private static string _loadedMapResourcePath;

        public static string ActiveMapId => Map.MapId;
        public static bool UsesSceneProvidedEnvironment =>
            ActiveMapResourcePath == ForgottenPlainsMapResourcePath ||
            ActiveMapResourcePath == ForgottenPlainsLargeMapResourcePath ||
            ActiveMapResourcePath == AstraResearchComplexMapResourcePath;
        public static Rect PlayBounds => Map.PlayBounds;
        public static RoomArea[] Rooms => Map.Rooms;
        public static Rect[] Obstacles => Map.Obstacles;
        public static Vector2[] SpawnPoints => Map.SpawnPoints;
        public static TaskStation[] TaskStations => Map.TaskStations;
        public static SabotageStation[] SabotageStations => Map.SabotageStations;
        public static VentStation[] VentStations => Map.VentStations;
        public static Vector2 MeetingPoint => Map.MeetingPoint;

        private static string ActiveMapResourcePath
        {
            get
            {
                var activeScene = SceneManager.GetActiveScene();
                if (string.Equals(activeScene.name, ForgottenPlainsSceneName, StringComparison.Ordinal))
                {
                    return ForgottenPlainsMapResourcePath;
                }

                if (string.Equals(activeScene.name, ForgottenPlainsLargeSceneName, StringComparison.Ordinal))
                {
                    return ForgottenPlainsLargeMapResourcePath;
                }

                if (string.Equals(activeScene.name, AstraResearchComplexSceneName, StringComparison.Ordinal))
                {
                    return AstraResearchComplexMapResourcePath;
                }

                return DefaultMapResourcePath;
            }
        }

        private static RuntimeMap Map
        {
            get
            {
                var resourcePath = ActiveMapResourcePath;
                if (_map == null || _loadedMapResourcePath != resourcePath)
                {
                    _loadedMapResourcePath = resourcePath;
                    _map = LoadMap(resourcePath);
                }

                return _map;
            }
        }

        private sealed class RuntimeMap
        {
            public string MapId;
            public Rect PlayBounds;
            public RoomArea[] Rooms = Array.Empty<RoomArea>();
            public Rect[] RoomBounds = Array.Empty<Rect>();
            public Rect[] Corridors = Array.Empty<Rect>();
            public Rect[] Doorways = Array.Empty<Rect>();
            public Rect[] Obstacles = Array.Empty<Rect>();
            public Vector2[] NavigationWaypoints = Array.Empty<Vector2>();
            public Vector2[] SpawnPoints = Array.Empty<Vector2>();
            public TaskStation[] TaskStations = Array.Empty<TaskStation>();
            public SabotageStation[] SabotageStations = Array.Empty<SabotageStation>();
            public VentStation[] VentStations = Array.Empty<VentStation>();
            public Vector2 MeetingPoint;
        }

        private readonly struct RoomPrefabPlacement
        {
            public readonly string ResourcePath;
            public readonly string InstanceName;
            public readonly Vector2 Position;

            public RoomPrefabPlacement(string resourcePath, string instanceName, Vector2 position)
            {
                ResourcePath = resourcePath;
                InstanceName = instanceName;
                Position = position;
            }
        }

        public static Vector2 ResolveMove(Vector2 current, Vector2 delta, float radius)
        {
            var currentOpen = ClampToWalkable(current, radius);
            var afterX = ClampToBounds(currentOpen + new Vector2(delta.x, 0f), radius);
            if (IsBlocked(afterX, radius))
            {
                afterX = currentOpen;
            }

            var afterY = ClampToBounds(afterX + new Vector2(0f, delta.y), radius);
            if (IsBlocked(afterY, radius))
            {
                afterY = afterX;
            }

            return afterY;
        }

        public static Vector2 ClampToWalkable(Vector2 position, float radius)
        {
            var clamped = ClampToBounds(position, radius);
            return IsBlocked(clamped, radius) ? FindNearestOpenPosition(clamped, radius) : clamped;
        }

        public static Vector2 GetRandomWalkablePosition()
        {
            var rooms = Map.Rooms;
            for (var attempts = 0; attempts < 32 && rooms.Length > 0; attempts++)
            {
                var area = rooms[UnityEngine.Random.Range(0, rooms.Length)].Bounds;
                var candidate = new Vector2(
                    UnityEngine.Random.Range(area.xMin + 0.4f, area.xMax - 0.4f),
                    UnityEngine.Random.Range(area.yMin + 0.4f, area.yMax - 0.4f));

                if (!IsBlocked(candidate, 0.28f))
                {
                    return candidate;
                }
            }

            return MeetingPoint;
        }

        public static Vector2 GetSpawnPoint(int playerIndex, Vector2 fallback)
        {
            var spawnPoints = Map.SpawnPoints;
            if (spawnPoints.Length == 0)
            {
                return ClampToWalkable(fallback, 0.32f);
            }

            return ClampToWalkable(spawnPoints[Mathf.Abs(playerIndex) % spawnPoints.Length], 0.32f);
        }

        public static Vector2 GetNavigationTarget(Vector2 current, Vector2 destination)
        {
            var currentOpen = ClampToWalkable(current, 0.22f);
            var destinationOpen = ClampToWalkable(destination, 0.22f);
            if (HasClearWalkableSegment(currentOpen, destinationOpen, 0.22f))
            {
                return destinationOpen;
            }

            var bestWaypoint = destinationOpen;
            var bestScore = float.MaxValue;
            var foundWaypoint = false;

            foreach (var waypoint in Map.NavigationWaypoints)
            {
                if (IsBlocked(waypoint, 0.22f) || !HasClearWalkableSegment(currentOpen, waypoint, 0.22f))
                {
                    continue;
                }

                var canSeeDestination = HasClearWalkableSegment(waypoint, destinationOpen, 0.22f);
                var score = Vector2.Distance(currentOpen, waypoint) +
                    Vector2.Distance(waypoint, destinationOpen) +
                    (canSeeDestination ? 0f : 2.5f);

                if (score < bestScore)
                {
                    bestWaypoint = waypoint;
                    bestScore = score;
                    foundWaypoint = true;
                }
            }

            return foundWaypoint ? bestWaypoint : destinationOpen;
        }

        public static string GetRoomName(Vector2 position)
        {
            foreach (var room in Map.Rooms)
            {
                if (room.Bounds.Contains(position))
                {
                    return room.Name;
                }
            }

            foreach (var corridor in Map.Corridors)
            {
                if (corridor.Contains(position))
                {
                    return "Hallway";
                }
            }

            foreach (var doorway in Map.Doorways)
            {
                if (doorway.Contains(position))
                {
                    return "Doorway";
                }
            }

            return "Ship";
        }

        public static bool HasClearWalkableSegment(Vector2 start, Vector2 end, float radius)
        {
            var distance = Vector2.Distance(start, end);
            var steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.18f));
            for (var i = 1; i <= steps; i++)
            {
                var sample = Vector2.Lerp(start, end, i / (float)steps);
                if (IsBlocked(sample, radius))
                {
                    return false;
                }
            }

            return true;
        }

        public static Vector3 ClampCameraPosition(Vector3 position, Camera camera)
        {
            if (camera == null || !camera.orthographic)
            {
                return position;
            }

            var bounds = Map.PlayBounds;
            var halfHeight = camera.orthographicSize;
            var halfWidth = halfHeight * camera.aspect;

            var x = bounds.width <= halfWidth * 2f
                ? bounds.center.x
                : Mathf.Clamp(position.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
            var y = bounds.height <= halfHeight * 2f
                ? bounds.center.y
                : Mathf.Clamp(position.y, bounds.yMin + halfHeight, bounds.yMax - halfHeight);

            return new Vector3(x, y, position.z);
        }

        public static void EnsureVisuals()
        {
            EnsureCameraSettings();

            if (ActiveMapResourcePath == AstraResearchComplexMapResourcePath)
            {
                EnsureAstraRuntimeBlockout();
                return;
            }

            if (UsesSceneProvidedEnvironment)
            {
                return;
            }

            if (GameObject.Find(RootName) != null)
            {
                return;
            }

            var root = new GameObject(RootName);

            var roomMaterial = CreateMaterial(new Color(0.085f, 0.13f, 0.17f, 1f));
            var corridorMaterial = CreateMaterial(new Color(0.115f, 0.17f, 0.22f, 1f));
            var doorMaterial = CreateMaterial(new Color(0.16f, 0.26f, 0.29f, 1f));
            var wallMaterial = CreateMaterial(new Color(0.018f, 0.024f, 0.03f, 1f));
            var trimMaterial = CreateMaterial(new Color(0.32f, 0.39f, 0.42f, 1f));
            var obstacleMaterial = CreateMaterial(new Color(0.035f, 0.04f, 0.045f, 1f));

            CreateRect(root.transform, "Production Preview Backdrop", Map.PlayBounds, wallMaterial, 0.85f);

            if (TryInstantiateProductionRoomPrefabs(root.transform))
            {
                return;
            }

            Debug.LogWarning("Production room prefabs are missing. Falling back to generated blockout visuals.");

            foreach (var room in Map.Rooms)
            {
                CreateRect(root.transform, $"Room Floor - {room.Name}", room.Bounds, roomMaterial, 0.62f);
                CreateOutline(root.transform, $"Room Wall - {room.Name}", room.Bounds, trimMaterial, 0.34f);
            }

            foreach (var corridor in Map.Corridors)
            {
                CreateRect(root.transform, "Corridor Floor", corridor, corridorMaterial, 0.58f);
                CreateOutline(root.transform, "Corridor Wall", corridor, trimMaterial, 0.38f);
            }

            foreach (var doorway in Map.Doorways)
            {
                CreateRect(root.transform, "Doorway", doorway, doorMaterial, 0.22f);
            }

            foreach (var obstacle in Map.Obstacles)
            {
                CreateRect(root.transform, "Room Prop", obstacle, obstacleMaterial, 0.2f);
                CreateOutline(root.transform, "Prop Trim", obstacle, trimMaterial, 0.18f);
            }

            foreach (var room in Map.Rooms)
            {
                CreateLabel(root.transform, room.Name, room.LabelPosition);
            }
        }

        private static void EnsureAstraRuntimeBlockout()
        {
            if (GameObject.Find(AstraRuntimeBlockoutRootName) != null)
            {
                return;
            }

            var root = new GameObject(AstraRuntimeBlockoutRootName);
            var roomMaterial = CreateMaterial(new Color(0.42f, 0.56f, 0.50f, 1f));
            var corridorMaterial = CreateMaterial(new Color(0.48f, 0.45f, 0.37f, 1f));
            var obstacleMaterial = CreateMaterial(new Color(0.30f, 0.24f, 0.22f, 1f));

            foreach (var room in Map.Rooms)
            {
                CreateSortedRect(root.transform, $"Visible Room - {room.Name}", room.Bounds, roomMaterial, 0.04f, "Ground", -20);
            }

            foreach (var corridor in Map.Corridors)
            {
                CreateSortedRect(root.transform, "Visible Corridor", corridor, corridorMaterial, 0.05f, "Ground", -19);
            }

            foreach (var obstacle in Map.Obstacles)
            {
                CreateSortedRect(root.transform, "Visible Obstacle", obstacle, obstacleMaterial, 0.06f, "PropsFront", -20);
            }
        }

        private static bool TryInstantiateProductionRoomPrefabs(Transform root)
        {
            var prefabs = new GameObject[ProductionRoomPlacements.Length];
            for (var i = 0; i < ProductionRoomPlacements.Length; i++)
            {
                var prefab = Resources.Load<GameObject>(ProductionRoomPlacements[i].ResourcePath);
                if (prefab == null)
                {
                    return false;
                }

                prefabs[i] = prefab;
            }

            for (var i = 0; i < ProductionRoomPlacements.Length; i++)
            {
                var placement = ProductionRoomPlacements[i];
                var instance = UnityEngine.Object.Instantiate(
                    prefabs[i],
                    new Vector3(placement.Position.x, placement.Position.y, 0f),
                    Quaternion.identity,
                    root);
                instance.name = placement.InstanceName;
            }

            return true;
        }

        private static void EnsureCameraSettings()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null || !mainCamera.orthographic)
            {
                return;
            }

            mainCamera.orthographicSize = PreviewCameraSize;
        }

        private static RuntimeMap LoadMap(string mapResourcePath)
        {
            var asset = Resources.Load<TextAsset>(mapResourcePath);
            if (asset != null)
            {
                try
                {
                    var definition = JsonUtility.FromJson<ShipMapDefinition>(asset.text);
                    var runtime = BuildRuntimeMap(definition);
                    if (runtime.Rooms.Length > 0 && runtime.TaskStations.Length > 0)
                    {
                        return runtime;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Failed to load ship map '{mapResourcePath}': {exception.Message}");
                }
            }

            Debug.LogWarning($"Using fallback ship map because '{mapResourcePath}' could not be loaded.");
            return BuildRuntimeMap(CreateFallbackDefinition());
        }

        private static RuntimeMap BuildRuntimeMap(ShipMapDefinition definition)
        {
            if (definition == null)
            {
                definition = CreateFallbackDefinition();
            }

            var runtime = new RuntimeMap
            {
                MapId = string.IsNullOrWhiteSpace(definition.mapId) ? "fallback_ship" : definition.mapId,
                PlayBounds = definition.playBounds.ToRect(),
                Rooms = BuildRooms(definition.rooms),
                RoomBounds = BuildRoomBounds(definition.rooms),
                Corridors = BuildRects(definition.corridors),
                Doorways = BuildRects(definition.doorways),
                Obstacles = BuildRects(definition.obstacles),
                NavigationWaypoints = BuildPoints(definition.navigationWaypoints),
                SpawnPoints = BuildPoints(definition.spawnPoints),
                TaskStations = BuildTaskStations(definition.taskPoints),
                SabotageStations = BuildSabotageStations(definition.sabotagePoints),
                VentStations = BuildVentStations(definition.ventNodes),
                MeetingPoint = definition.meetingPoint != null
                    ? definition.meetingPoint.position.ToVector2()
                    : Vector2.zero
            };

            if (runtime.PlayBounds.width <= 0f || runtime.PlayBounds.height <= 0f)
            {
                runtime.PlayBounds = new Rect(-8f, -5f, 16f, 10f);
            }

            return runtime;
        }

        private static RoomArea[] BuildRooms(RoomDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
            {
                return Array.Empty<RoomArea>();
            }

            var rooms = new RoomArea[definitions.Length];
            for (var i = 0; i < definitions.Length; i++)
            {
                var room = definitions[i];
                var name = !string.IsNullOrWhiteSpace(room.displayName) ? room.displayName : room.id;
                rooms[i] = new RoomArea(room.id, name, room.bounds.ToRect(), room.label.ToVector2());
            }

            return rooms;
        }

        private static Rect[] BuildRoomBounds(RoomDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
            {
                return Array.Empty<Rect>();
            }

            var bounds = new Rect[definitions.Length];
            for (var i = 0; i < definitions.Length; i++)
            {
                bounds[i] = definitions[i].bounds.ToRect();
            }

            return bounds;
        }

        private static Rect[] BuildRects(RectFeatureDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
            {
                return Array.Empty<Rect>();
            }

            var rects = new Rect[definitions.Length];
            for (var i = 0; i < definitions.Length; i++)
            {
                rects[i] = definitions[i].bounds.ToRect();
            }

            return rects;
        }

        private static Vector2[] BuildPoints(PointFeatureDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
            {
                return Array.Empty<Vector2>();
            }

            var points = new Vector2[definitions.Length];
            for (var i = 0; i < definitions.Length; i++)
            {
                points[i] = definitions[i].position.ToVector2();
            }

            return points;
        }

        private static TaskStation[] BuildTaskStations(PointFeatureDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
            {
                return Array.Empty<TaskStation>();
            }

            var stations = new TaskStation[definitions.Length];
            for (var i = 0; i < definitions.Length; i++)
            {
                var point = definitions[i];
                var name = !string.IsNullOrWhiteSpace(point.displayName) ? point.displayName : point.id;
                var fallbackKind = (TaskKind)(i % Enum.GetValues(typeof(TaskKind)).Length);
                var kind = Enum.TryParse(point.taskKind, true, out TaskKind parsedKind)
                    ? parsedKind
                    : fallbackKind;
                stations[i] = new TaskStation(i, name, kind, point.position.ToVector2());
            }

            return stations;
        }

        private static SabotageStation[] BuildSabotageStations(SabotagePointDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
            {
                return Array.Empty<SabotageStation>();
            }

            var stations = new SabotageStation[definitions.Length];
            for (var i = 0; i < definitions.Length; i++)
            {
                var point = definitions[i];
                var name = !string.IsNullOrWhiteSpace(point.displayName) ? point.displayName : point.id;
                if (!Enum.TryParse(point.kind, true, out SabotageType type))
                {
                    type = SabotageType.Lights;
                }

                stations[i] = new SabotageStation(type, name, point.position.ToVector2(), point.hasCountdown);
            }

            return stations;
        }

        private static VentStation[] BuildVentStations(VentNodeDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
            {
                return Array.Empty<VentStation>();
            }

            var vents = new VentStation[definitions.Length];
            for (var i = 0; i < definitions.Length; i++)
            {
                var point = definitions[i];
                var name = !string.IsNullOrWhiteSpace(point.displayName) ? point.displayName : point.id;
                vents[i] = new VentStation(name, point.groupId, point.position.ToVector2());
            }

            return vents;
        }

        private static bool IsBlocked(Vector2 position, float radius)
        {
            return ShipMapGeometry.IsBlocked(
                position,
                radius,
                Map.RoomBounds,
                Map.Corridors,
                Map.Doorways,
                Map.Obstacles);
        }

        private static Vector2 FindNearestOpenPosition(Vector2 position, float radius)
        {
            const float step = 0.2f;
            for (var ring = 1; ring <= 48; ring++)
            {
                for (var x = -ring; x <= ring; x++)
                {
                    for (var y = -ring; y <= ring; y++)
                    {
                        if (Mathf.Abs(x) != ring && Mathf.Abs(y) != ring)
                        {
                            continue;
                        }

                        var candidate = ClampToBounds(position + new Vector2(x * step, y * step), radius);
                        if (!IsBlocked(candidate, radius))
                        {
                            return candidate;
                        }
                    }
                }
            }

            return MeetingPoint;
        }

        private static Vector2 ClampToBounds(Vector2 position, float radius)
        {
            return new Vector2(
                Mathf.Clamp(position.x, Map.PlayBounds.xMin + radius, Map.PlayBounds.xMax - radius),
                Mathf.Clamp(position.y, Map.PlayBounds.yMin + radius, Map.PlayBounds.yMax - radius));
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var material = shader != null ? new Material(shader) : null;
            if (material != null)
            {
                material.color = color;
            }

            return material;
        }

        private static void CreateOutline(Transform root, string name, Rect rect, Material material, float z)
        {
            CreateRect(root, $"{name} Top", new Rect(rect.xMin, rect.yMax - WallThickness, rect.width, WallThickness), material, z);
            CreateRect(root, $"{name} Bottom", new Rect(rect.xMin, rect.yMin, rect.width, WallThickness), material, z);
            CreateRect(root, $"{name} Left", new Rect(rect.xMin, rect.yMin, WallThickness, rect.height), material, z);
            CreateRect(root, $"{name} Right", new Rect(rect.xMax - WallThickness, rect.yMin, WallThickness, rect.height), material, z);
        }

        private static void CreateRect(Transform root, string name, Rect rect, Material material, float z)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(root);
            quad.transform.position = new Vector3(rect.center.x, rect.center.y, z);
            quad.transform.localScale = new Vector3(rect.width, rect.height, 1f);

            var collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            var renderer = quad.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.material = material;
            }
        }

        private static void CreateSortedRect(
            Transform root,
            string name,
            Rect rect,
            Material material,
            float z,
            string sortingLayer,
            int sortingOrder)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(root);
            quad.transform.position = new Vector3(rect.center.x, rect.center.y, z);
            quad.transform.localScale = new Vector3(rect.width, rect.height, 1f);

            var collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            var renderer = quad.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            if (material != null)
            {
                renderer.material = material;
            }

            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = sortingOrder;
        }

        private static void CreateLabel(Transform root, string label, Vector2 position)
        {
            var labelObject = new GameObject($"Room Label - {label}");
            labelObject.transform.SetParent(root);
            labelObject.transform.position = new Vector3(position.x, position.y, 0.08f);

            var text = labelObject.AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.22f;
            text.fontSize = 48;
            text.color = new Color(0.62f, 0.75f, 0.82f, 1f);
        }

        private static ShipMapDefinition CreateFallbackDefinition()
        {
            return new ShipMapDefinition
            {
                mapId = "fallback_ship",
                playBounds = new RectDefinition { x = -7.6f, y = -4.35f, width = 15.2f, height = 8.75f },
                rooms = new[]
                {
                    new RoomDefinition
                    {
                        id = "cafeteria",
                        displayName = "Cafeteria",
                        bounds = new RectDefinition { x = -3.25f, y = -0.9f, width = 6.5f, height = 1.95f },
                        label = new Vector2Definition { x = 0f, y = 0.58f }
                    },
                    new RoomDefinition
                    {
                        id = "power",
                        displayName = "Power",
                        bounds = new RectDefinition { x = -7.25f, y = 1.45f, width = 4.05f, height = 2.75f },
                        label = new Vector2Definition { x = -5.15f, y = 3.65f }
                    },
                    new RoomDefinition
                    {
                        id = "comms",
                        displayName = "Comms",
                        bounds = new RectDefinition { x = 2.1f, y = -4.05f, width = 5.05f, height = 2.8f },
                        label = new Vector2Definition { x = 4.55f, y = -1.15f }
                    }
                },
                corridors = new[]
                {
                    new RectFeatureDefinition { id = "central", bounds = new RectDefinition { x = -4.2f, y = -0.35f, width = 6.6f, height = 0.75f } }
                },
                doorways = new[]
                {
                    new RectFeatureDefinition { id = "door_power", bounds = new RectDefinition { x = -3.82f, y = 1.2f, width = 0.75f, height = 0.62f } },
                    new RectFeatureDefinition { id = "door_comms", bounds = new RectDefinition { x = 1.8f, y = -1.58f, width = 0.88f, height = 0.62f } }
                },
                obstacles = Array.Empty<RectFeatureDefinition>(),
                navigationWaypoints = new[]
                {
                    new PointFeatureDefinition { id = "cafeteria", position = new Vector2Definition { x = 0f, y = 0f } },
                    new PointFeatureDefinition { id = "power", position = new Vector2Definition { x = -5.4f, y = 2.75f } },
                    new PointFeatureDefinition { id = "comms", position = new Vector2Definition { x = 4.72f, y = -2.68f } }
                },
                spawnPoints = new[]
                {
                    new PointFeatureDefinition { id = "spawn_01", position = new Vector2Definition { x = -1f, y = 0.4f } },
                    new PointFeatureDefinition { id = "spawn_02", position = new Vector2Definition { x = 0f, y = 0.4f } },
                    new PointFeatureDefinition { id = "spawn_03", position = new Vector2Definition { x = 1f, y = 0.4f } }
                },
                taskPoints = new[]
                {
                    new PointFeatureDefinition { id = "task_power", displayName = "Power Wires", position = new Vector2Definition { x = -6f, y = 3f } },
                    new PointFeatureDefinition { id = "task_comms", displayName = "Upload Data", position = new Vector2Definition { x = 4.8f, y = -3.1f } }
                },
                sabotagePoints = new[]
                {
                    new SabotagePointDefinition { id = "sabotage_lights", displayName = "Lights", kind = "Lights", position = new Vector2Definition { x = -6.8f, y = 2.2f } },
                    new SabotagePointDefinition { id = "sabotage_comms", displayName = "Comms", kind = "Communications", position = new Vector2Definition { x = 5.7f, y = -3.3f } }
                },
                ventNodes = new[]
                {
                    new VentNodeDefinition { id = "vent_power", displayName = "Power", groupId = "fallback", position = new Vector2Definition { x = -4.6f, y = 2.4f } },
                    new VentNodeDefinition { id = "vent_comms", displayName = "Comms", groupId = "fallback", position = new Vector2Definition { x = 4.2f, y = -2.4f } }
                },
                meetingPoint = new PointFeatureDefinition { id = "meeting", position = new Vector2Definition { x = 0f, y = 0.25f } }
            };
        }
    }
}
