using Fusion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace AmongUsClone.Editor
{
    public static class AstraMapDefinitionLoader
    {
        public const string DefinitionAssetPath = "Assets/Game/Maps/Definitions/astra_research_complex_map_definition.json";

        private const string ParentDefinitionPath = "../Assets/Game/Maps/Definitions/astra_research_complex_map_definition.json";

        public static AstraMapDefinition Load()
        {
            EnsureDefinitionAsset();

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(DefinitionAssetPath);
            if (asset == null)
            {
                throw new FileNotFoundException($"MapDefinition is missing: {DefinitionAssetPath}");
            }

            var definition = JsonUtility.FromJson<AstraMapDefinition>(asset.text);
            if (definition == null || definition.rooms == null || definition.connections == null)
            {
                throw new InvalidOperationException($"MapDefinition is invalid: {DefinitionAssetPath}");
            }

            return definition;
        }

        private static void EnsureDefinitionAsset()
        {
            if (File.Exists(DefinitionAssetPath))
            {
                return;
            }

            if (!File.Exists(ParentDefinitionPath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(DefinitionAssetPath));
            File.Copy(ParentDefinitionPath, DefinitionAssetPath, false);
            AssetDatabase.ImportAsset(DefinitionAssetPath);
        }
    }

    public static class AstraResearchComplexBlockoutGenerator
    {
        private const int DefaultSeed = 73621;
        private const string ScenePath = "Assets/Game/Maps/AstraResearchComplex_Blockout.unity";
        private const string RuntimeMapPath = "Assets/Resources/Maps/astra_research_complex_blockout_01.json";
        private const string ReportsFolder = "Assets/Game/Maps/Reports";
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerPrefab.prefab";
        private const float RuntimeWalkableOverlap = 0.05f;

        private static readonly string[] SortingLayers =
        {
            "Ground",
            "FloorDetails",
            "PropsBack",
            "Player",
            "PropsFront",
            "UI"
        };

        [MenuItem("Tools/Astra Research Complex/Generate Blockout Scene")]
        public static void GenerateBlockoutMenu()
        {
            GenerateBlockout(DefaultSeed);
        }

        [MenuItem("Tools/Astra Research Complex/Validate Blockout Scene")]
        public static void ValidateBlockoutMenu()
        {
            ValidateAstraBlockoutBatch();
        }

        public static void GenerateAstraBlockoutBatch()
        {
            GenerateBlockout(DefaultSeed);
        }

        public static void ValidateAstraBlockoutBatch()
        {
            var definition = AstraMapDefinitionLoader.Load();
            var layout = BuildLayout(definition, DefaultSeed);
            var result = AstraResearchComplexMapValidator.Validate(layout, ScenePath);
            AstraResearchComplexMapValidator.WriteReport(layout, result);
            AssetDatabase.Refresh();

            if (!result.Passed)
            {
                throw new InvalidOperationException("Astra Research Complex validation failed. See the generated report for details.");
            }
        }

        public static void GenerateBlockout(int seed)
        {
            EnsureFolders();
            EnsureSortingLayers();

            var definition = AstraMapDefinitionLoader.Load();
            var layout = BuildLayout(definition, seed);

            GenerateScene(layout, seed);
            SaveRuntimeMap(layout);
            EnsureBuildSettings();

            var validation = AstraResearchComplexMapValidator.Validate(layout, ScenePath);
            AstraResearchComplexMapValidator.WriteReport(layout, validation);
            AssetDatabase.Refresh();

            if (!validation.Passed)
            {
                throw new InvalidOperationException("Generated Astra Research Complex scene, but validation failed. See the report for details.");
            }

            Debug.Log($"Generated {ScenePath} from {AstraMapDefinitionLoader.DefinitionAssetPath}.");
        }

        internal static AstraLayout BuildLayout(AstraMapDefinition definition, int seed)
        {
            var layout = new AstraLayout
            {
                Definition = definition,
                Seed = seed,
                PlayBounds = new Rect(
                    definition.bounds.minX,
                    definition.bounds.minY,
                    definition.bounds.maxX - definition.bounds.minX,
                    definition.bounds.maxY - definition.bounds.minY)
            };

            foreach (var room in definition.rooms)
            {
                var roomLayout = BuildRoomLayout(room);
                layout.Rooms.Add(roomLayout);
                foreach (var cell in roomLayout.FloorCells)
                {
                    layout.RoomFloorCells.Add(cell);
                    layout.FloorCells.Add(cell);
                }

                foreach (var cell in roomLayout.CutoutCells)
                {
                    layout.ShapeWallCells.Add(cell);
                }
            }

            foreach (var connection in definition.connections)
            {
                var connectionLayout = BuildConnectionLayout(connection);
                layout.Connections.Add(connectionLayout);
                AddCorridorCells(layout, connectionLayout);
            }

            foreach (var cell in layout.CorridorCells)
            {
                layout.FloorCells.Add(cell);
                layout.ShapeWallCells.Remove(cell);
            }

            AddShapeCutoutObstacles(layout);
            AddInteriorObstacles(layout);
            BuildGameplayPoints(layout);
            BuildWallCells(layout);
            BuildRuntimeDefinitions(layout);

            return layout;
        }

        private static AstraRoomLayout BuildRoomLayout(AstraRoomDefinition room)
        {
            var rect = RoomRect(room);
            var polygon = BuildRoomPolygon(room, rect);
            var roomLayout = new AstraRoomLayout
            {
                Definition = room,
                Bounds = rect,
                Polygon = polygon
            };

            var xMin = Mathf.FloorToInt(rect.xMin);
            var xMax = Mathf.CeilToInt(rect.xMax);
            var yMin = Mathf.FloorToInt(rect.yMin);
            var yMax = Mathf.CeilToInt(rect.yMax);
            for (var x = xMin; x < xMax; x++)
            {
                for (var y = yMin; y < yMax; y++)
                {
                    var cell = new Vector2Int(x, y);
                    var center = CellCenter(cell);
                    if (PointInPolygon(center, polygon))
                    {
                        roomLayout.FloorCells.Add(cell);
                    }
                    else if (rect.Contains(center))
                    {
                        roomLayout.CutoutCells.Add(cell);
                    }
                }
            }

            return roomLayout;
        }

        private static AstraConnectionLayout BuildConnectionLayout(AstraConnectionDefinition connection)
        {
            return new AstraConnectionLayout
            {
                Definition = connection,
                Points = connection.path.Select(point => point.ToVector2()).ToArray()
            };
        }

        private static void AddCorridorCells(AstraLayout layout, AstraConnectionLayout connection)
        {
            var points = connection.Points;
            if (points == null || points.Length < 2)
            {
                return;
            }

            var radius = Mathf.Max(0.5f, connection.Definition.width * 0.5f);
            for (var i = 0; i < points.Length - 1; i++)
            {
                var start = points[i];
                var end = points[i + 1];
                var minX = Mathf.FloorToInt(Mathf.Min(start.x, end.x) - radius - 1f);
                var maxX = Mathf.CeilToInt(Mathf.Max(start.x, end.x) + radius + 1f);
                var minY = Mathf.FloorToInt(Mathf.Min(start.y, end.y) - radius - 1f);
                var maxY = Mathf.CeilToInt(Mathf.Max(start.y, end.y) + radius + 1f);

                for (var x = minX; x <= maxX; x++)
                {
                    for (var y = minY; y <= maxY; y++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (!CellInsideBounds(layout, cell))
                        {
                            continue;
                        }

                        var center = CellCenter(cell);
                        if (DistanceToSegment(center, start, end) <= radius)
                        {
                            layout.CorridorCells.Add(cell);
                        }
                    }
                }
            }
        }

        private static void AddShapeCutoutObstacles(AstraLayout layout)
        {
            foreach (var rect in CompressCellsToRects(layout.ShapeWallCells))
            {
                layout.Obstacles.Add(new AstraObstacle
                {
                    Id = $"shape_wall_{layout.Obstacles.Count:000}",
                    DisplayName = "Room Shape Cutout",
                    Kind = "shape_wall",
                    Bounds = rect
                });
            }
        }

        private static void AddInteriorObstacles(AstraLayout layout)
        {
            AddObstacle(layout, "medical_scanner", "Medical Scanner Bed", "wall", -53.5f, 10.7f, 7.2f, 2.2f);
            AddObstacle(layout, "medical_sample_fridge", "Sample Fridge", "wall", -58.0f, 15.1f, 2.8f, 4.2f);
            AddObstacle(layout, "medical_recovery_pod", "Recovery Pod", "wall", -43.8f, 5.4f, 3.8f, 2.4f);

            AddObstacle(layout, "xeno_containment_tank", "Containment Tank", "pillar", -45.8f, 33.8f, 5.2f, 8.4f);
            AddObstacle(layout, "xeno_specimen_table", "Specimen Table", "wall", -35.0f, 40.7f, 6.2f, 2.0f);

            AddObstacle(layout, "observation_console", "Observation Console", "wall", -5.5f, 49.8f, 11.0f, 1.6f);

            AddObstacle(layout, "communications_antenna_console", "Antenna Control Console", "wall", 32.2f, 36.1f, 5.6f, 2.0f);
            AddObstacle(layout, "communications_server_rack", "Server Rack", "wall", 40.6f, 30.6f, 2.3f, 5.0f);

            AddObstacle(layout, "security_monitor_wall", "Monitor Wall", "wall", 50.0f, 16.2f, 8.4f, 1.8f);
            AddObstacle(layout, "security_console", "Security Console", "wall", 43.7f, 9.0f, 3.2f, 4.6f);

            AddObstacle(layout, "engine_primary_block", "Engine Block", "pillar", 45.0f, -20.7f, 9.5f, 7.2f);
            AddObstacle(layout, "engine_turbine_housing", "Turbine Housing", "pillar", 56.5f, -20.9f, 4.8f, 6.2f);
            AddObstacle(layout, "engine_tool_wall", "Tool Wall", "wall", 38.3f, -12.3f, 4.6f, 3.2f);

            AddObstacle(layout, "reactor_core_block", "Reactor Core", "pillar", 36.4f, -46.0f, 6.8f, 10.2f);
            AddObstacle(layout, "reactor_coolant_tanks", "Coolant Tanks", "pillar", 27.6f, -47.8f, 3.2f, 7.2f);
            AddObstacle(layout, "reactor_service_barrier", "Service Barrier", "wall", 44.2f, -38.5f, 4.4f, 2.4f);

            AddObstacle(layout, "power_west_breakers", "West Breaker Panels", "wall", -0.9f, -47.5f, 2.2f, 7.8f);
            AddObstacle(layout, "power_east_breakers", "East Breaker Panels", "wall", 13.7f, -46.5f, 2.0f, 7.2f);
            AddObstacle(layout, "power_transformer", "Transformer", "pillar", 6.1f, -48.6f, 4.2f, 2.8f);

            AddObstacle(layout, "cargo_crate_cluster_a", "Cargo Crates A", "pillar", -29.2f, -34.1f, 6.3f, 4.8f);
            AddObstacle(layout, "cargo_crate_cluster_b", "Cargo Crates B", "pillar", -17.0f, -26.8f, 7.2f, 4.0f);
            AddObstacle(layout, "cargo_crate_cluster_c", "Cargo Crates C", "pillar", -8.7f, -37.3f, 4.2f, 4.8f);
            AddObstacle(layout, "cargo_low_shelf", "Low Cargo Shelf", "wall", -25.8f, -21.2f, 5.2f, 2.0f);

            AddObstacle(layout, "life_support_oxygen_tanks", "Oxygen Tanks", "pillar", -56.4f, -20.5f, 3.2f, 7.2f);
            AddObstacle(layout, "life_support_filter_bank", "Filter Bank", "wall", -47.6f, -23.2f, 4.8f, 2.6f);
            AddObstacle(layout, "life_support_valves", "Valve Manifold", "wall", -59.5f, -13.1f, 3.8f, 2.0f);
        }

        private static void AddObstacle(AstraLayout layout, string id, string displayName, string kind, float x, float y, float width, float height)
        {
            var rect = new Rect(x, y, width, height);
            layout.Obstacles.Add(new AstraObstacle
            {
                Id = id,
                DisplayName = displayName,
                Kind = kind,
                Bounds = rect
            });

            foreach (var cell in CellsInRect(rect))
            {
                if (layout.FloorCells.Contains(cell))
                {
                    layout.InteriorObstacleCells.Add(cell);
                }
            }
        }

        private static void BuildGameplayPoints(AstraLayout layout)
        {
            layout.SpawnPoints = layout.Definition.spawnPoints
                .Select(spawn => Point(spawn.id, $"Spawn {spawn.id.Replace("spawn_", string.Empty)}", spawn.roomId, FindOpenPoint(layout, spawn.roomId, spawn.position.ToVector2(), 0.55f)))
                .ToArray();

            layout.MeetingPoint = Point(
                layout.Definition.meetingPoint.id,
                "Emergency Meeting",
                layout.Definition.meetingPoint.roomId,
                FindOpenPoint(layout, layout.Definition.meetingPoint.roomId, layout.Definition.meetingPoint.position.ToVector2(), 0.65f));

            var tasks = new List<PointFeatureDefinition>();
            foreach (var room in layout.Definition.rooms)
            {
                var count = GetTaskCount(layout.Definition.taskDistribution.perRoom, room.id);
                var candidates = GetTaskCandidates(room.id, RoomRect(room).center);
                for (var i = 0; i < count; i++)
                {
                    var desired = candidates[Mathf.Min(i, candidates.Length - 1)];
                    var position = FindOpenPoint(layout, room.id, desired, 0.55f);
                    tasks.Add(Point(
                        $"task_{room.id}_{i + 1:00}",
                        GetTaskName(room.id, i),
                        room.id,
                        position,
                        GetTaskKind(room.id, i).ToString()));
                }
            }

            layout.TaskPoints = tasks.ToArray();

            var sabotage = new List<SabotagePointDefinition>();
            foreach (var point in layout.Definition.sabotagePoints)
            {
                var desired = GetSabotageDesiredPosition(point.id, FindRoom(layout, point.roomId).Bounds.center);
                sabotage.Add(new SabotagePointDefinition
                {
                    id = point.id,
                    displayName = GetSabotageName(point.id),
                    roomId = point.roomId,
                    kind = GetSabotageKind(point.id),
                    hasCountdown = ContainsIgnoreCase(point.id, "reactor"),
                    position = V(FindOpenPoint(layout, point.roomId, desired, 0.65f))
                });
            }

            layout.SabotagePoints = sabotage.ToArray();

            var vents = new List<VentNodeDefinition>();
            foreach (var group in layout.Definition.ventGroups)
            {
                foreach (var roomId in group.rooms)
                {
                    var room = FindRoom(layout, roomId);
                    var desired = GetVentDesiredPosition(roomId, room.Bounds);
                    vents.Add(new VentNodeDefinition
                    {
                        id = $"vent_{group.id}_{roomId}",
                        displayName = $"{room.Definition.displayName} Vent",
                        roomId = roomId,
                        groupId = group.id,
                        position = V(FindOpenPoint(layout, roomId, desired, 0.55f))
                    });
                }
            }

            layout.VentNodes = vents.ToArray();
        }

        private static void BuildWallCells(AstraLayout layout)
        {
            foreach (var cell in layout.FloorCells)
            {
                for (var x = -2; x <= 2; x++)
                {
                    for (var y = -2; y <= 2; y++)
                    {
                        if (x == 0 && y == 0)
                        {
                            continue;
                        }

                        var candidate = new Vector2Int(cell.x + x, cell.y + y);
                        if (!CellInsideBounds(layout, candidate) || layout.FloorCells.Contains(candidate))
                        {
                            continue;
                        }

                        layout.WallCells.Add(candidate);
                    }
                }
            }

            foreach (var cell in layout.ShapeWallCells)
            {
                layout.WallCells.Add(cell);
            }

            foreach (var cell in layout.InteriorObstacleCells)
            {
                layout.WallCells.Remove(cell);
            }
        }

        private static void BuildRuntimeDefinitions(AstraLayout layout)
        {
            layout.RuntimeRooms = layout.Rooms.Select(room => new RoomDefinition
            {
                id = room.Definition.id,
                displayName = room.Definition.displayName,
                bounds = R(room.Bounds),
                label = V(room.Bounds.center)
            }).ToArray();

            // Compressed cell runs only touch at their edges. Keep a small overlap so
            // ShipMap's per-rectangle inset cannot split a visually continuous route.
            layout.RuntimeCorridors = CompressCellsToRects(layout.CorridorCells)
                .Select((rect, index) => new RectFeatureDefinition
                {
                    id = $"corridor_cell_run_{index:000}",
                    displayName = "Astra Corridor",
                    bounds = R(Inflate(rect, RuntimeWalkableOverlap))
                })
                .ToArray();

            layout.RuntimeObstacles = layout.Obstacles.Select(obstacle => new RectFeatureDefinition
            {
                id = obstacle.Id,
                displayName = obstacle.DisplayName,
                bounds = R(obstacle.Bounds)
            }).ToArray();

            var waypoints = new List<PointFeatureDefinition>();
            foreach (var room in layout.Rooms)
            {
                waypoints.Add(Point($"wp_{room.Definition.id}", room.Definition.displayName, room.Definition.id, FindOpenPoint(layout, room.Definition.id, room.Bounds.center, 0.45f)));
            }

            foreach (var connection in layout.Connections)
            {
                for (var i = 0; i < connection.Points.Length; i++)
                {
                    var point = connection.Points[i];
                    waypoints.Add(Point($"wp_{connection.Definition.id}_{i:00}", connection.Definition.id, string.Empty, FindNearestOpenPoint(layout, point, 0.45f)));
                }
            }

            layout.NavigationWaypoints = waypoints.ToArray();
        }

        private static void GenerateScene(AstraLayout layout, int seed)
        {
            var catalog = ForgottenPlainsAssetCatalog.Build();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("AstraResearchComplex_Blockout").transform;
            var environment = Child(root, "Environment");
            environment.gameObject.AddComponent<Grid>();
            var collision = Child(root, "Collision");
            var gameplay = Child(root, "Gameplay");
            var networking = Child(root, "Networking");
            var cameraRoot = Child(root, "Camera");
            var debug = Child(root, "Debug");

            var ground = CreateTilemap(environment, "Ground", "Ground", 0, false);
            var details = CreateTilemap(environment, "FloorDetails", "FloorDetails", 0, false);
            var walls = CreateTilemap(environment, "Walls", "PropsBack", 0, true);
            var obstaclesBack = CreateTilemap(environment, "ObstaclesBack", "PropsBack", 1, false);
            var obstaclesFront = CreateTilemap(environment, "ObstaclesFront", "PropsFront", 0, true);

            CreateBlockoutBacking(layout, ground.transform, walls.transform, obstaclesFront.transform);
            FillTilemaps(layout, catalog, seed, ground, details, walls, obstaclesBack, obstaclesFront);
            CreateCollision(layout, collision);
            CreateGameplay(layout, gameplay);
            CreateNetworking(networking);
            CreateCamera(layout, cameraRoot);
            CreateLighting(cameraRoot);
            CreateDebug(layout, debug);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static Tilemap CreateTilemap(Transform parent, string name, string sortingLayer, int sortingOrder, bool collider)
        {
            var tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(parent, false);
            var tilemap = tilemapObject.AddComponent<Tilemap>();
            var renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            renderer.mode = TilemapRenderer.Mode.Chunk;
            renderer.sharedMaterial = CreateUnlitMaterial(Color.white, "Astra Unlit Tilemap");

            if (collider)
            {
                var tilemapCollider = tilemapObject.AddComponent<TilemapCollider2D>();
                tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
                var rigidbody = tilemapObject.AddComponent<Rigidbody2D>();
                rigidbody.bodyType = RigidbodyType2D.Static;
                tilemapObject.AddComponent<CompositeCollider2D>();
            }

            return tilemap;
        }

        private static void CreateBlockoutBacking(AstraLayout layout, Transform ground, Transform walls, Transform obstacles)
        {
            var roomCells = new HashSet<Vector2Int>(layout.RoomFloorCells);
            roomCells.ExceptWith(layout.InteriorObstacleCells);

            var corridorCells = new HashSet<Vector2Int>(layout.CorridorCells);
            corridorCells.ExceptWith(layout.RoomFloorCells);
            corridorCells.ExceptWith(layout.InteriorObstacleCells);

            var obstacleCells = new HashSet<Vector2Int>(layout.InteriorObstacleCells);

            CreateCellMesh(
                ground,
                "Room Floor Backing",
                roomCells,
                new Color(0.42f, 0.56f, 0.50f, 1f),
                "Ground",
                -8);
            CreateCellMesh(
                ground,
                "Corridor Floor Backing",
                corridorCells,
                new Color(0.48f, 0.45f, 0.37f, 1f),
                "Ground",
                -7);
            CreateCellMesh(
                walls,
                "Wall Backing",
                layout.WallCells,
                new Color(0.25f, 0.30f, 0.32f, 1f),
                "PropsBack",
                -4);
            CreateCellMesh(
                obstacles,
                "Obstacle Backing",
                obstacleCells,
                new Color(0.30f, 0.24f, 0.22f, 1f),
                "PropsFront",
                -4);
        }

        private static void CreateCellMesh(
            Transform parent,
            string name,
            IEnumerable<Vector2Int> cells,
            Color color,
            string sortingLayer,
            int sortingOrder)
        {
            var rects = CompressCellsToRects(cells).Where(rect => rect.width > 0f && rect.height > 0f).ToArray();
            if (rects.Length == 0)
            {
                return;
            }

            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var meshFilter = gameObject.AddComponent<MeshFilter>();
            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = CreateUnlitMaterial(color, $"Astra {name}");
            meshRenderer.sortingLayerName = sortingLayer;
            meshRenderer.sortingOrder = sortingOrder;

            var vertices = new List<Vector3>(rects.Length * 4);
            var triangles = new List<int>(rects.Length * 6);
            var uvs = new List<Vector2>(rects.Length * 4);
            foreach (var rect in rects)
            {
                var start = vertices.Count;
                vertices.Add(new Vector3(rect.xMin, rect.yMin, 0.04f));
                vertices.Add(new Vector3(rect.xMax, rect.yMin, 0.04f));
                vertices.Add(new Vector3(rect.xMax, rect.yMax, 0.04f));
                vertices.Add(new Vector3(rect.xMin, rect.yMax, 0.04f));
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start);
                triangles.Add(start + 3);
                triangles.Add(start + 2);
                uvs.Add(Vector2.zero);
                uvs.Add(Vector2.right);
                uvs.Add(Vector2.one);
                uvs.Add(Vector2.up);
            }

            var mesh = new Mesh
            {
                name = name
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();
            meshFilter.sharedMesh = mesh;
        }

        private static void FillTilemaps(
            AstraLayout layout,
            ForgottenPlainsAssetCatalog catalog,
            int seed,
            Tilemap ground,
            Tilemap details,
            Tilemap walls,
            Tilemap obstaclesBack,
            Tilemap obstaclesFront)
        {
            foreach (var cell in layout.FloorCells)
            {
                var vector = new Vector3Int(cell.x, cell.y, 0);
                var isCorridor = layout.CorridorCells.Contains(cell) && !layout.RoomFloorCells.Contains(cell);
                ground.SetTile(vector, Pick(isCorridor ? catalog.PathTiles : catalog.GroundTiles, seed, cell.x, cell.y));

                if (!layout.InteriorObstacleCells.Contains(cell) && Hash(seed, cell.x, cell.y, 17) % 23 == 0)
                {
                    details.SetTile(vector, Pick(isCorridor ? catalog.PathDetailTiles : catalog.DetailTiles, seed + 13, cell.x, cell.y));
                }
            }

            foreach (var cell in layout.WallCells)
            {
                var vector = new Vector3Int(cell.x, cell.y, 0);
                var useCliff = Mathf.Abs(cell.y - Mathf.RoundToInt(layout.PlayBounds.yMin)) < 4 ||
                    Mathf.Abs(cell.y - Mathf.RoundToInt(layout.PlayBounds.yMax)) < 4 ||
                    Hash(seed, cell.x, cell.y, 29) % 5 == 0;
                walls.SetTile(vector, Pick(useCliff ? catalog.CliffTiles : catalog.WallTiles, seed + 23, cell.x, cell.y));
            }

            foreach (var obstacle in layout.Obstacles.Where(item => item.Kind != "shape_wall"))
            {
                foreach (var cell in CellsInRect(obstacle.Bounds))
                {
                    var vector = new Vector3Int(cell.x, cell.y, 0);
                    var tile = Pick(obstacle.Kind == "pillar" ? catalog.CliffTiles : catalog.WallTiles, seed + 31, cell.x, cell.y);
                    obstaclesFront.SetTile(vector, tile);

                    if (Hash(seed, cell.x, cell.y, 37) % 7 == 0)
                    {
                        obstaclesBack.SetTile(vector, Pick(catalog.DetailTiles, seed + 37, cell.x, cell.y));
                    }
                }
            }
        }

        private static void CreateCollision(AstraLayout layout, Transform collision)
        {
            var terrain = Child(collision, "TerrainCollision");
            AddStaticRigidbody(terrain);

            var boundary = Child(collision, "MapBoundary");
            AddStaticRigidbody(boundary);

            const float thickness = 2f;
            var b = layout.PlayBounds;
            CreateBoundaryBox(boundary, "Boundary North", new Rect(b.xMin - thickness, b.yMax, b.width + thickness * 2f, thickness));
            CreateBoundaryBox(boundary, "Boundary South", new Rect(b.xMin - thickness, b.yMin - thickness, b.width + thickness * 2f, thickness));
            CreateBoundaryBox(boundary, "Boundary West", new Rect(b.xMin - thickness, b.yMin, thickness, b.height));
            CreateBoundaryBox(boundary, "Boundary East", new Rect(b.xMax, b.yMin, thickness, b.height));
        }

        private static void CreateGameplay(AstraLayout layout, Transform gameplay)
        {
            var roomAreas = Child(gameplay, "RoomAreas");
            var spawnPoints = Child(gameplay, "SpawnPoints");
            var taskPoints = Child(gameplay, "TaskPoints");
            var meetingPoint = Child(gameplay, "MeetingPoint");
            var sabotagePoints = Child(gameplay, "SabotagePoints");
            var ventPoints = Child(gameplay, "VentPoints");

            foreach (var room in layout.Rooms)
            {
                CreateRoomArea(roomAreas, room);
            }

            foreach (var point in layout.SpawnPoints)
            {
                CreatePoint(spawnPoints, point.id, point.displayName, point.roomId, point.position.ToVector2(), 0.42f, AstraBlockoutMarkerKind.SpawnPoint, new Color(0.2f, 1f, 0.28f, 0.9f), null, null);
            }

            foreach (var point in layout.TaskPoints)
            {
                CreatePoint(taskPoints, point.id, point.displayName, point.roomId, point.position.ToVector2(), 0.46f, AstraBlockoutMarkerKind.TaskPoint, new Color(1f, 0.82f, 0.12f, 0.9f), point.taskKind, null);
            }

            CreatePoint(meetingPoint, layout.MeetingPoint.id, layout.MeetingPoint.displayName, layout.MeetingPoint.roomId, layout.MeetingPoint.position.ToVector2(), 0.72f, AstraBlockoutMarkerKind.MeetingPoint, new Color(1f, 0.2f, 0.9f, 0.92f), null, null);

            foreach (var point in layout.SabotagePoints)
            {
                CreatePoint(sabotagePoints, point.id, point.displayName, point.roomId, point.position.ToVector2(), 0.56f, AstraBlockoutMarkerKind.SabotagePoint, new Color(1f, 0.23f, 0.16f, 0.92f), point.kind, null);
            }

            foreach (var vent in layout.VentNodes)
            {
                var connected = layout.VentNodes
                    .Where(other => other.groupId == vent.groupId && other.id != vent.id)
                    .Select(other => other.id)
                    .ToArray();
                CreatePoint(ventPoints, vent.id, vent.displayName, vent.roomId, vent.position.ToVector2(), 0.5f, AstraBlockoutMarkerKind.VentPoint, new Color(0.32f, 0.85f, 1f, 0.92f), vent.groupId, connected);
            }
        }

        private static void CreateNetworking(Transform networking)
        {
            var network = new GameObject("Network");
            network.transform.SetParent(networking, false);
            var spawner = network.AddComponent<BasicSpawner>();
            var serialized = new SerializedObject(spawner);
            Set(serialized, "_maxPlayerCount", 10);
            Set(serialized, "_minimumPlayersToStart", 4);
            Set(serialized, "_testCpuPlayerCount", 5);
            Set(serialized, "_tasksPerCrewmate", 5);
            Set(serialized, "_firstTimedTaskDelaySeconds", 30f);
            Set(serialized, "_timedTaskIntervalSeconds", 45f);
            Set(serialized, "_maxTimedTaskWaves", 2);
            Set(serialized, "_taskDeadlineSeconds", 180f);
            Set(serialized, "_taskFailureCutInSeconds", 4f);
            Set(serialized, "_calibrationCycleSeconds", 2.2f);
            Set(serialized, "_showLegacyDebugGui", false);
            SetNetworkPrefabRef(serialized, "_playerPrefab", PlayerPrefabPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateCamera(AstraLayout layout, Transform cameraRoot)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(cameraRoot, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(1f, layout.Definition.camera.orthographicSize);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.052f, 0.064f, 1f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();

            var bounds = new GameObject("CameraBounds");
            bounds.transform.SetParent(cameraRoot, false);
            bounds.transform.position = new Vector3(layout.PlayBounds.center.x, layout.PlayBounds.center.y, 0f);
            var collider = bounds.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = layout.PlayBounds.size;
            var marker = bounds.AddComponent<AstraBlockoutMarker>();
            marker.Id = "camera_bounds";
            marker.DisplayName = "Camera Bounds";
            marker.Kind = AstraBlockoutMarkerKind.CameraBounds;
            marker.Color = new Color(0.2f, 0.5f, 1f, 0.45f);
            marker.Size = layout.PlayBounds.size;
        }

        private static void CreateLighting(Transform parent)
        {
            var lightObject = new GameObject("Global Light 2D");
            lightObject.transform.SetParent(parent, false);
            var light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = Color.white;
            light.intensity = 1.15f;
        }

        private static void CreateDebug(AstraLayout layout, Transform debug)
        {
            var connections = Child(debug, "Connections");
            var material = CreateLineMaterial();
            foreach (var connection in layout.Connections)
            {
                var lineObject = new GameObject(connection.Definition.id);
                lineObject.transform.SetParent(connections, false);
                var renderer = lineObject.AddComponent<LineRenderer>();
                renderer.useWorldSpace = true;
                renderer.material = material;
                renderer.startColor = new Color(0.8f, 1f, 1f, 0.85f);
                renderer.endColor = new Color(0.8f, 1f, 1f, 0.85f);
                renderer.startWidth = 0.2f;
                renderer.endWidth = 0.2f;
                renderer.positionCount = connection.Points.Length;
                for (var i = 0; i < connection.Points.Length; i++)
                {
                    renderer.SetPosition(i, new Vector3(connection.Points[i].x, connection.Points[i].y, -0.1f));
                }
            }
        }

        private static void CreateRoomArea(Transform parent, AstraRoomLayout room)
        {
            var gameObject = new GameObject(room.Definition.displayName);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = new Vector3(room.Bounds.center.x, room.Bounds.center.y, 0f);

            var collider = gameObject.AddComponent<PolygonCollider2D>();
            collider.isTrigger = true;
            collider.points = room.Polygon
                .Select(point => point - room.Bounds.center)
                .ToArray();

            var marker = gameObject.AddComponent<AstraBlockoutMarker>();
            marker.Id = room.Definition.id;
            marker.RoomId = room.Definition.id;
            marker.DisplayName = room.Definition.displayName;
            marker.Kind = AstraBlockoutMarkerKind.RoomArea;
            marker.Color = new Color(0.2f, 0.55f, 1f, 0.36f);
            marker.Size = room.Bounds.size;
            marker.DrawFilled = true;
        }

        private static void CreatePoint(
            Transform parent,
            string id,
            string displayName,
            string roomId,
            Vector2 position,
            float radius,
            AstraBlockoutMarkerKind kind,
            Color color,
            string groupOrKind,
            string[] connectedIds)
        {
            var gameObject = new GameObject(displayName);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = new Vector3(position.x, position.y, 0f);
            var collider = gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = radius;

            var marker = gameObject.AddComponent<AstraBlockoutMarker>();
            marker.Id = id;
            marker.RoomId = roomId;
            marker.DisplayName = displayName;
            marker.GroupId = groupOrKind;
            marker.ConnectedIds = connectedIds ?? Array.Empty<string>();
            marker.Kind = kind;
            marker.Color = color;
            marker.Radius = radius;
            marker.DrawFilled = true;
        }

        private static void CreateBoundaryBox(Transform parent, string name, Rect rect)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = new Vector3(rect.center.x, rect.center.y, 0f);
            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = rect.size;
            var marker = gameObject.AddComponent<AstraBlockoutMarker>();
            marker.Id = name.ToLowerInvariant().Replace(" ", "_");
            marker.DisplayName = name;
            marker.Kind = AstraBlockoutMarkerKind.MapBoundary;
            marker.Color = new Color(1f, 0.1f, 0.1f, 0.45f);
            marker.Size = rect.size;
        }

        private static void SaveRuntimeMap(AstraLayout layout)
        {
            var runtime = new ShipMapDefinition
            {
                mapId = layout.Definition.mapId,
                gridSize = 1f,
                playBounds = R(layout.PlayBounds),
                rooms = layout.RuntimeRooms,
                corridors = layout.RuntimeCorridors,
                doorways = layout.RuntimeDoorways,
                obstacles = layout.RuntimeObstacles,
                navigationWaypoints = layout.NavigationWaypoints,
                spawnPoints = layout.SpawnPoints,
                taskPoints = layout.TaskPoints,
                sabotagePoints = layout.SabotagePoints,
                ventNodes = layout.VentNodes,
                meetingPoint = layout.MeetingPoint
            };

            File.WriteAllText(RuntimeMapPath, JsonUtility.ToJson(runtime, true));
        }

        private static Vector2[] BuildRoomPolygon(AstraRoomDefinition room, Rect rect)
        {
            var xMin = rect.xMin;
            var xMax = rect.xMax;
            var yMin = rect.yMin;
            var yMax = rect.yMax;
            var cx = rect.center.x;
            var cy = rect.center.y;
            var cut = Mathf.Min(rect.width, rect.height) * 0.22f;

            switch (room.shape)
            {
                case "cut_corner_octagon":
                    return new[]
                    {
                        new Vector2(xMin + cut, yMin),
                        new Vector2(xMax - cut, yMin),
                        new Vector2(xMax, yMin + cut),
                        new Vector2(xMax, yMax - cut),
                        new Vector2(xMax - cut, yMax),
                        new Vector2(xMin + cut, yMax),
                        new Vector2(xMin, yMax - cut),
                        new Vector2(xMin, yMin + cut)
                    };
                case "cut_corner_rectangle":
                case "rounded_rectangle":
                    return new[]
                    {
                        new Vector2(xMin + cut, yMin),
                        new Vector2(xMax - cut, yMin),
                        new Vector2(xMax, yMin + cut),
                        new Vector2(xMax, yMax - cut),
                        new Vector2(xMax - cut, yMax),
                        new Vector2(xMin + cut, yMax),
                        new Vector2(xMin, yMax - cut),
                        new Vector2(xMin, yMin + cut)
                    };
                case "offset_rectangle":
                    return new[]
                    {
                        new Vector2(xMin + 2f, yMin),
                        new Vector2(xMax, yMin),
                        new Vector2(xMax - 2f, yMax),
                        new Vector2(xMin, yMax)
                    };
                case "l_shape":
                    if (room.id == "security_control")
                    {
                        return new[]
                        {
                            new Vector2(xMin, yMin),
                            new Vector2(cx + 4f, yMin),
                            new Vector2(cx + 4f, cy - 1f),
                            new Vector2(xMax, cy - 1f),
                            new Vector2(xMax, yMax),
                            new Vector2(xMin, yMax)
                        };
                    }

                    return new[]
                    {
                        new Vector2(xMin, yMin),
                        new Vector2(xMax, yMin),
                        new Vector2(xMax, yMax),
                        new Vector2(cx - 4f, yMax),
                        new Vector2(cx - 4f, cy + 1f),
                        new Vector2(xMin, cy + 1f)
                    };
                case "trapezoid":
                    return new[]
                    {
                        new Vector2(xMin, yMin),
                        new Vector2(xMax, yMin),
                        new Vector2(xMax - 3.5f, yMax),
                        new Vector2(xMin + 3.5f, yMax)
                    };
                case "stepped_rectangle":
                    return new[]
                    {
                        new Vector2(xMin, yMin + 4f),
                        new Vector2(xMin + 5f, yMin + 4f),
                        new Vector2(xMin + 5f, yMin),
                        new Vector2(xMax - 4f, yMin),
                        new Vector2(xMax - 4f, yMin + 3f),
                        new Vector2(xMax, yMin + 3f),
                        new Vector2(xMax, yMax - 4f),
                        new Vector2(xMax - 5f, yMax - 4f),
                        new Vector2(xMax - 5f, yMax),
                        new Vector2(xMin, yMax)
                    };
                case "irregular_rectangle":
                    return new[]
                    {
                        new Vector2(xMin, yMin + 2f),
                        new Vector2(xMax - 3f, yMin),
                        new Vector2(xMax, yMin + 4f),
                        new Vector2(xMax, yMax),
                        new Vector2(xMin + 4f, yMax),
                        new Vector2(xMin, yMax - 5f)
                    };
                default:
                    return new[]
                    {
                        new Vector2(xMin, yMin),
                        new Vector2(xMax, yMin),
                        new Vector2(xMax, yMax),
                        new Vector2(xMin, yMax)
                    };
            }
        }

        private static Vector2[] GetTaskCandidates(string roomId, Vector2 center)
        {
            switch (roomId)
            {
                case "central_lounge":
                    return Offsets(center, new Vector2(-10f, 7f));
                case "medical_lab":
                    return Offsets(center, new Vector2(-5.5f, -5f), new Vector2(6f, 5f));
                case "xenobiology_lab":
                    return Offsets(center, new Vector2(-8f, -5f), new Vector2(7f, 3f));
                case "observation_deck":
                    return Offsets(center, new Vector2(0f, -4f));
                case "communications":
                    return Offsets(center, new Vector2(-6f, 1f), new Vector2(6f, -4f));
                case "security_control":
                    return Offsets(center, new Vector2(-6f, -3f), new Vector2(4f, 4f));
                case "engine_maintenance":
                    return Offsets(center, new Vector2(-10f, 4f), new Vector2(1f, 7f), new Vector2(10f, -5f));
                case "reactor_core":
                    return Offsets(center, new Vector2(-9f, 7f), new Vector2(8f, -7f));
                case "power_distribution":
                    return Offsets(center, new Vector2(-5f, 6f), new Vector2(1f, 5f), new Vector2(6f, -1f));
                case "cargo_storage":
                    return Offsets(center, new Vector2(-12f, 7f), new Vector2(11f, -3f));
                case "life_support":
                    return Offsets(center, new Vector2(-7f, 5f), new Vector2(7f, -1f));
                default:
                    return Offsets(center, Vector2.zero);
            }
        }

        private static string GetTaskName(string roomId, int index)
        {
            var names = new Dictionary<string, string[]>
            {
                { "central_lounge", new[] { "Review Emergency Log" } },
                { "medical_lab", new[] { "Run Med Scanner", "Analyze Blood Sample" } },
                { "xenobiology_lab", new[] { "Calibrate Containment", "Catalog Specimen" } },
                { "observation_deck", new[] { "Align Telescope" } },
                { "communications", new[] { "Upload Research Data", "Tune Antenna Array" } },
                { "security_control", new[] { "Review Camera Feed", "Reset Door Logs" } },
                { "engine_maintenance", new[] { "Tune Engine", "Prime Coolant Pump", "Inspect Turbine" } },
                { "reactor_core", new[] { "Stabilize Core", "Balance Control Rods" } },
                { "power_distribution", new[] { "Divert Power", "Reset Breakers", "Trace Cable Fault" } },
                { "cargo_storage", new[] { "Scan Cargo", "Sort Supply Crates" } },
                { "life_support", new[] { "Clean Oxygen Filter", "Check Air Tanks" } }
            };

            return names.TryGetValue(roomId, out var roomNames)
                ? roomNames[Mathf.Clamp(index, 0, roomNames.Length - 1)]
                : $"Task {index + 1}";
        }

        private static TaskKind GetTaskKind(string roomId, int index)
        {
            switch (roomId)
            {
                case "central_lounge":
                    return TaskKind.DataTransfer;
                case "medical_lab":
                    return index == 0 ? TaskKind.DataTransfer : TaskKind.Calibration;
                case "xenobiology_lab":
                    return index == 0 ? TaskKind.Calibration : TaskKind.DataTransfer;
                case "observation_deck":
                    return TaskKind.Calibration;
                case "communications":
                    return index == 0 ? TaskKind.DataTransfer : TaskKind.Calibration;
                case "security_control":
                    return index == 0 ? TaskKind.DataTransfer : TaskKind.CircuitPulse;
                case "engine_maintenance":
                    return index == 0 ? TaskKind.Calibration : index == 1 ? TaskKind.CircuitPulse : TaskKind.DataTransfer;
                case "reactor_core":
                    return index == 0 ? TaskKind.Calibration : TaskKind.CircuitPulse;
                case "power_distribution":
                    return TaskKind.CircuitPulse;
                case "cargo_storage":
                    return index == 0 ? TaskKind.DataTransfer : TaskKind.CircuitPulse;
                case "life_support":
                    return index == 0 ? TaskKind.CircuitPulse : TaskKind.DataTransfer;
                default:
                    return (TaskKind)(Mathf.Abs(index) % Enum.GetValues(typeof(TaskKind)).Length);
            }
        }

        private static Vector2[] Offsets(Vector2 center, params Vector2[] offsets)
        {
            return offsets.Select(offset => center + offset).ToArray();
        }

        private static Vector2 GetSabotageDesiredPosition(string id, Vector2 center)
        {
            if (ContainsIgnoreCase(id, "reactor")) return center + new Vector2(-7f, 7f);
            if (ContainsIgnoreCase(id, "power")) return center + new Vector2(1f, 6f);
            if (ContainsIgnoreCase(id, "comms")) return center + new Vector2(0f, -2f);
            if (ContainsIgnoreCase(id, "life")) return center + new Vector2(3f, 5f);
            return center;
        }

        private static string GetSabotageName(string id)
        {
            if (ContainsIgnoreCase(id, "reactor")) return "Reactor Core Sabotage";
            if (ContainsIgnoreCase(id, "power")) return "Power Distribution Sabotage";
            if (ContainsIgnoreCase(id, "comms")) return "Communications Sabotage";
            if (ContainsIgnoreCase(id, "life")) return "Oxygen Processing Sabotage";
            return id;
        }

        private static string GetSabotageKind(string id)
        {
            if (ContainsIgnoreCase(id, "reactor")) return nameof(SabotageType.Reactor);
            if (ContainsIgnoreCase(id, "power")) return nameof(SabotageType.Lights);
            if (ContainsIgnoreCase(id, "comms")) return nameof(SabotageType.Communications);
            if (ContainsIgnoreCase(id, "life")) return nameof(SabotageType.Oxygen);
            return nameof(SabotageType.Lights);
        }

        private static Vector2 GetVentDesiredPosition(string roomId, Rect bounds)
        {
            switch (roomId)
            {
                case "medical_lab": return new Vector2(bounds.xMin + 3.5f, bounds.yMin + 3.5f);
                case "xenobiology_lab": return new Vector2(bounds.xMax - 4.5f, bounds.yMax - 4.5f);
                case "communications": return new Vector2(bounds.xMin + 4f, bounds.yMin + 3f);
                case "security_control": return new Vector2(bounds.xMax - 4f, bounds.yMax - 4f);
                case "engine_maintenance": return new Vector2(bounds.xMin + 4f, bounds.yMin + 5f);
                case "reactor_core": return new Vector2(bounds.xMax - 5f, bounds.yMin + 4f);
                case "life_support": return new Vector2(bounds.xMin + 4f, bounds.yMin + 4f);
                case "cargo_storage": return new Vector2(bounds.xMin + 7f, bounds.yMax - 4f);
                case "power_distribution": return new Vector2(bounds.xMax - 4f, bounds.yMax - 4f);
                default: return bounds.center;
            }
        }

        private static int GetTaskCount(TaskPerRoomDefinition perRoom, string roomId)
        {
            switch (roomId)
            {
                case "central_lounge": return perRoom.central_lounge;
                case "medical_lab": return perRoom.medical_lab;
                case "xenobiology_lab": return perRoom.xenobiology_lab;
                case "observation_deck": return perRoom.observation_deck;
                case "communications": return perRoom.communications;
                case "security_control": return perRoom.security_control;
                case "engine_maintenance": return perRoom.engine_maintenance;
                case "reactor_core": return perRoom.reactor_core;
                case "power_distribution": return perRoom.power_distribution;
                case "cargo_storage": return perRoom.cargo_storage;
                case "life_support": return perRoom.life_support;
                default: return 0;
            }
        }

        private static Vector2 FindOpenPoint(AstraLayout layout, string roomId, Vector2 desired, float radius)
        {
            if (IsOpen(layout, desired, radius) && (string.IsNullOrEmpty(roomId) || PointInRoom(layout, roomId, desired)))
            {
                return desired;
            }

            const float step = 0.75f;
            for (var ring = 1; ring <= 14; ring++)
            {
                for (var x = -ring; x <= ring; x++)
                {
                    for (var y = -ring; y <= ring; y++)
                    {
                        if (Mathf.Abs(x) != ring && Mathf.Abs(y) != ring)
                        {
                            continue;
                        }

                        var candidate = desired + new Vector2(x * step, y * step);
                        if (IsOpen(layout, candidate, radius) && (string.IsNullOrEmpty(roomId) || PointInRoom(layout, roomId, candidate)))
                        {
                            return candidate;
                        }
                    }
                }
            }

            var room = string.IsNullOrEmpty(roomId) ? null : FindRoom(layout, roomId);
            return room != null ? FindNearestOpenPoint(layout, room.Bounds.center, radius) : FindNearestOpenPoint(layout, desired, radius);
        }

        private static Vector2 FindNearestOpenPoint(AstraLayout layout, Vector2 desired, float radius)
        {
            if (IsOpen(layout, desired, radius))
            {
                return desired;
            }

            const float step = 0.75f;
            for (var ring = 1; ring <= 80; ring++)
            {
                for (var x = -ring; x <= ring; x++)
                {
                    for (var y = -ring; y <= ring; y++)
                    {
                        if (Mathf.Abs(x) != ring && Mathf.Abs(y) != ring)
                        {
                            continue;
                        }

                        var candidate = desired + new Vector2(x * step, y * step);
                        if (IsOpen(layout, candidate, radius))
                        {
                            return candidate;
                        }
                    }
                }
            }

            return layout.MeetingPoint != null ? layout.MeetingPoint.position.ToVector2() : Vector2.zero;
        }

        internal static bool IsOpen(AstraLayout layout, Vector2 point, float radius)
        {
            if (!layout.PlayBounds.Contains(point))
            {
                return false;
            }

            if (!layout.FloorCells.Contains(WorldToCell(point)))
            {
                return false;
            }

            foreach (var obstacle in layout.Obstacles)
            {
                if (Inflate(obstacle.Bounds, radius).Contains(point))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool PointInRoom(AstraLayout layout, string roomId, Vector2 point)
        {
            var room = FindRoom(layout, roomId);
            return room != null && PointInPolygon(point, room.Polygon);
        }

        private static AstraRoomLayout FindRoom(AstraLayout layout, string roomId)
        {
            return layout.Rooms.FirstOrDefault(room => room.Definition.id == roomId);
        }

        private static Rect RoomRect(AstraRoomDefinition room)
        {
            return new Rect(
                room.center.x - room.size.width * 0.5f,
                room.center.y - room.size.height * 0.5f,
                room.size.width,
                room.size.height);
        }

        private static IEnumerable<Vector2Int> CellsInRect(Rect rect)
        {
            var minX = Mathf.FloorToInt(rect.xMin);
            var maxX = Mathf.CeilToInt(rect.xMax);
            var minY = Mathf.FloorToInt(rect.yMin);
            var maxY = Mathf.CeilToInt(rect.yMax);
            for (var x = minX; x < maxX; x++)
            {
                for (var y = minY; y < maxY; y++)
                {
                    if (rect.Contains(new Vector2(x + 0.5f, y + 0.5f)))
                    {
                        yield return new Vector2Int(x, y);
                    }
                }
            }
        }

        private static List<Rect> CompressCellsToRects(IEnumerable<Vector2Int> cells)
        {
            var rows = cells
                .Distinct()
                .GroupBy(cell => cell.y)
                .OrderBy(group => group.Key)
                .ToArray();
            var active = new Dictionary<string, RectInt>();
            var completed = new List<RectInt>();

            foreach (var row in rows)
            {
                var keysInRow = new HashSet<string>();
                var xs = row.Select(cell => cell.x).Distinct().OrderBy(x => x).ToArray();
                var index = 0;
                while (index < xs.Length)
                {
                    var start = xs[index];
                    var end = start;
                    while (index + 1 < xs.Length && xs[index + 1] == end + 1)
                    {
                        index++;
                        end = xs[index];
                    }

                    var key = $"{start}:{end}";
                    keysInRow.Add(key);
                    if (active.TryGetValue(key, out var rect) && rect.yMax == row.Key)
                    {
                        active[key] = new RectInt(rect.xMin, rect.yMin, rect.width, rect.height + 1);
                    }
                    else
                    {
                        if (active.TryGetValue(key, out var staleRect))
                        {
                            completed.Add(staleRect);
                        }

                        active[key] = new RectInt(start, row.Key, end - start + 1, 1);
                    }

                    index++;
                }

                foreach (var key in active.Keys.ToArray())
                {
                    if (!keysInRow.Contains(key))
                    {
                        completed.Add(active[key]);
                        active.Remove(key);
                    }
                }
            }

            completed.AddRange(active.Values);
            return completed
                .Select(rect => new Rect(rect.xMin, rect.yMin, rect.width, rect.height))
                .ToList();
        }

        private static bool CellInsideBounds(AstraLayout layout, Vector2Int cell)
        {
            return layout.PlayBounds.Contains(CellCenter(cell));
        }

        private static Vector2 CellCenter(Vector2Int cell)
        {
            return new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        }

        internal static Vector2Int WorldToCell(Vector2 point)
        {
            return new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
        }

        private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
        {
            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if (DistanceToSegment(point, a, b) <= 0.001f)
                {
                    return true;
                }

                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, start);
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private static Rect Inflate(Rect rect, float amount)
        {
            return new Rect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);
        }

        private static TileBase Pick(IReadOnlyList<TileBase> tiles, int seed, int x, int y)
        {
            if (tiles == null || tiles.Count == 0)
            {
                return null;
            }

            return tiles[Mathf.Abs(Hash(seed, x, y, tiles.Count + 11)) % tiles.Count];
        }

        private static int Hash(int seed, int x, int y, int salt)
        {
            unchecked
            {
                var value = seed;
                value = value * 397 ^ x;
                value = value * 397 ^ y;
                value = value * 397 ^ salt;
                return value;
            }
        }

        private static bool ContainsIgnoreCase(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static PointFeatureDefinition Point(string id, string name, string roomId, Vector2 position, string taskKind = null)
        {
            return new PointFeatureDefinition
            {
                id = id,
                displayName = name,
                roomId = roomId,
                taskKind = taskKind,
                position = V(position)
            };
        }

        private static RectDefinition R(Rect rect)
        {
            return new RectDefinition { x = rect.x, y = rect.y, width = rect.width, height = rect.height };
        }

        private static Vector2Definition V(Vector2 value)
        {
            return new Vector2Definition { x = value.x, y = value.y };
        }

        private static Vector2Definition V(float x, float y)
        {
            return new Vector2Definition { x = x, y = y };
        }

        private static Transform Child(Transform parent, string name)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            return gameObject.transform;
        }

        private static void AddStaticRigidbody(Transform target)
        {
            var rigidbody = target.gameObject.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = RigidbodyType2D.Static;
        }

        private static void Set(SerializedObject serialized, string propertyName, int value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, float value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, bool value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static unsafe void SetNetworkPrefabRef(SerializedObject serialized, string propertyName, string prefabPath)
        {
            var prefabRef = serialized.FindProperty(propertyName);
            if (prefabRef == null)
            {
                throw new InvalidOperationException($"Missing serialized property: {propertyName}");
            }

            var rawGuid = prefabRef.FindPropertyRelative(nameof(NetworkObjectGuid.RawGuidValue));
            if (rawGuid == null)
            {
                throw new InvalidOperationException($"Missing NetworkPrefabRef RawGuidValue for {propertyName}");
            }

            var guid = NetworkObjectGuid.Parse(AssetDatabase.AssetPathToGUID(prefabPath));
            rawGuid.GetFixedBufferElementAtIndex(0).longValue = guid.RawGuidValue[0];
            rawGuid.GetFixedBufferElementAtIndex(1).longValue = guid.RawGuidValue[1];
        }

        private static Material CreateLineMaterial()
        {
            return CreateUnlitMaterial(new Color(0.8f, 1f, 1f, 1f), "Astra Debug Line");
        }

        private static Material CreateUnlitMaterial(Color color, string name)
        {
            var shader = Shader.Find("Sprites/Default") ??
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = name,
                color = color
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory("Assets/Game/Maps");
            Directory.CreateDirectory("Assets/Game/Maps/Definitions");
            Directory.CreateDirectory(ReportsFolder);
            Directory.CreateDirectory("Assets/Resources/Maps");
        }

        private static void EnsureBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(scene => scene.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureSortingLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");
            foreach (var layer in SortingLayers)
            {
                var exists = false;
                for (var i = 0; i < layers.arraySize; i++)
                {
                    if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == layer)
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists)
                {
                    continue;
                }

                layers.InsertArrayElementAtIndex(layers.arraySize);
                var element = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                element.FindPropertyRelative("name").stringValue = layer;
                element.FindPropertyRelative("uniqueID").intValue = Math.Abs(layer.GetHashCode());
                element.FindPropertyRelative("locked").boolValue = false;
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public static class AstraResearchComplexMapValidator
    {
        private const string ScenePath = "Assets/Game/Maps/AstraResearchComplex_Blockout.unity";
        private const string RuntimeMapPath = "Assets/Resources/Maps/astra_research_complex_blockout_01.json";
        private const string ReportPath = "Assets/Game/Maps/Reports/AstraResearchComplex_BlockoutValidation.md";
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerPrefab.prefab";
        private const float RuntimePlayerRadius = 0.5f;

        internal static ValidationResult Validate(AstraLayout layout, string scenePath)
        {
            var checks = new List<ValidationCheck>();
            if (File.Exists(scenePath))
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                checks.Add(Pass("Scene load", scenePath));
            }
            else
            {
                checks.Add(Fail("Scene load", $"{scenePath} is missing"));
            }

            var graph = BuildGraph(layout);
            var runtimeReachability = CheckRuntimeReachability(layout, RuntimePlayerRadius);
            checks.Add(Check("11 rooms exist", layout.Definition.rooms.Length == 11 && CountMarkers(AstraBlockoutMarkerKind.RoomArea) == 11, $"definition={layout.Definition.rooms.Length}, scene={CountMarkers(AstraBlockoutMarkerKind.RoomArea)}"));
            checks.Add(Check("16 connections exist", layout.Definition.connections.Length == 16, $"connections={layout.Definition.connections.Length}"));
            checks.Add(Check("All rooms reachable", IsConnected(graph), $"{CountReachable(graph)}/{layout.Definition.rooms.Length} rooms reachable"));
            checks.Add(Check("West-east routes >= 3", CountSideRoutes(layout, true) >= 3, $"routes={CountSideRoutes(layout, true)}"));
            checks.Add(Check("North-south routes >= 3", CountSideRoutes(layout, false) >= 3, $"routes={CountSideRoutes(layout, false)}"));
            checks.Add(Check("Dead-end rooms <= 1", graph.Count(pair => pair.Value.Count <= 1) <= 1, $"deadEnds={graph.Count(pair => pair.Value.Count <= 1)}"));
            checks.Add(Check("No graph bridge dependency", FindBridges(layout, graph).Length == 0, $"bridges={string.Join(", ", FindBridges(layout, graph))}"));
            checks.Add(Check(
                "Runtime player-radius reachability",
                runtimeReachability.ReachableRoomCount == layout.RuntimeRooms.Length,
                runtimeReachability.RoomDetail));
            checks.Add(Check(
                "Runtime task points reachable",
                runtimeReachability.ReachableTaskCount == layout.TaskPoints.Length,
                runtimeReachability.TaskDetail));

            checks.Add(CheckPointCount("SpawnPoint", AstraBlockoutMarkerKind.SpawnPoint, layout.SpawnPoints.Length, 15, true));
            checks.Add(CheckPointCount("MeetingPoint", AstraBlockoutMarkerKind.MeetingPoint, layout.MeetingPoint == null ? 0 : 1, 1, false));
            checks.Add(CheckPointCount("TaskPoint", AstraBlockoutMarkerKind.TaskPoint, layout.TaskPoints.Length, 22, true));
            var taskKinds = layout.TaskPoints
                .Select(point => point.taskKind)
                .Where(taskKind => !string.IsNullOrWhiteSpace(taskKind))
                .Distinct()
                .ToArray();
            checks.Add(Check("3 task mechanics configured", taskKinds.Length == 3, $"taskKinds={string.Join(", ", taskKinds)}"));
            checks.Add(CheckPointCount("SabotagePoint", AstraBlockoutMarkerKind.SabotagePoint, layout.SabotagePoints.Length, 4, false));
            checks.Add(CheckPointCount("VentPoint", AstraBlockoutMarkerKind.VentPoint, layout.VentNodes.Length, 9, false));

            checks.Add(Check("SpawnPoint placement", PointsOpen(layout, layout.SpawnPoints, 0.55f) && PointsOutsideBlockingColliders(layout.SpawnPoints, 0.2f), "open and not inside non-trigger colliders"));
            checks.Add(Check("TaskPoint placement", PointsOpen(layout, layout.TaskPoints, 0.55f) && PointsOutsideBlockingColliders(layout.TaskPoints, 0.2f), "open and not inside non-trigger colliders"));
            checks.Add(Check("MapBoundary sealed", BoundarySealed(layout), "four continuous boundary colliders around play bounds"));
            checks.Add(Check("Tilemap composite colliders", TilemapCompositeColliderCount() >= 2, $"tilemapCompositeColliders={TilemapCompositeColliderCount()}"));

            var networkRunnerCount = UnityEngine.Object.FindObjectsByType<NetworkRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            checks.Add(Check("NetworkRunner not duplicated", networkRunnerCount == 0, $"scene NetworkRunner components={networkRunnerCount}; BasicSpawner creates one at runtime"));
            var spawnerCount = UnityEngine.Object.FindObjectsByType<BasicSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            checks.Add(Check("BasicSpawner exists", spawnerCount == 1, $"BasicSpawner components={spawnerCount}"));
            checks.Add(Check("Player range is 4-10", SpawnerIntEquals("_minimumPlayersToStart", 4) && SpawnerIntEquals("_maxPlayerCount", 10), "minimum=4, maximum=10"));
            checks.Add(Check("5 tasks assigned per Crewmate", SpawnerIntEquals("_tasksPerCrewmate", 5), "22 stations remain available across the map"));
            checks.Add(Check(
                "Timed task waves configured",
                SpawnerFloatEquals("_firstTimedTaskDelaySeconds", 30f) &&
                SpawnerFloatEquals("_timedTaskIntervalSeconds", 45f) &&
                SpawnerIntEquals("_maxTimedTaskWaves", 2),
                "one new task at 30s and 75s"));
            checks.Add(Check(
                "Task deadline loss configured",
                SpawnerFloatEquals("_taskDeadlineSeconds", 180f) &&
                SpawnerFloatEquals("_taskFailureCutInSeconds", 4f),
                "180s deadline, then 4s cut-in before Impostor victory"));
            checks.Add(Check("BasicSpawner PlayerPrefab assigned", PlayerPrefabIsAssigned(), PlayerPrefabPath));
            checks.Add(Check("PlayerPrefab spawnable", PlayerPrefabSpawnable(), "NetworkObject + Player behaviour"));
            checks.Add(Check("No NetworkObject on static map", CountStaticMapNetworkObjects() == 0, $"staticNetworkObjects={CountStaticMapNetworkObjects()}"));

            var missingScripts = CountMissingScripts();
            checks.Add(Check("Missing Script", missingScripts == 0, $"missingScripts={missingScripts}"));
            var missingSprites = CountMissingSprites();
            checks.Add(Check("Missing Sprite", missingSprites == 0, $"missingSprites={missingSprites}"));
            checks.Add(Check("Runtime map saved", File.Exists(RuntimeMapPath), RuntimeMapPath));
            checks.Add(Check("Orthographic size remains 6", Camera.main != null && Mathf.Approximately(Camera.main.orthographicSize, 6f), Camera.main == null ? "no MainCamera" : $"orthographicSize={Camera.main.orthographicSize:0.0}"));
            checks.Add(Check("Global Light 2D present", HasGlobalLight2D(), "prevents Sprite-Lit tilemaps from rendering black"));
            checks.Add(Check("Visible blockout floor backing", HasVisibleBacking("Room Floor Backing") && HasVisibleBacking("Corridor Floor Backing"), "unlit floor meshes under Tilemaps"));

            return new ValidationResult(checks);
        }

        internal static void WriteReport(AstraLayout layout, ValidationResult result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            var graph = BuildGraph(layout);
            var lines = new List<string>
            {
                "# Astra Research Complex Blockout Validation",
                "",
                $"Scene: `{ScenePath}`",
                $"MapDefinition: `{AstraMapDefinitionLoader.DefinitionAssetPath}`",
                $"Runtime Map: `{RuntimeMapPath}`",
                $"Map Size: {layout.PlayBounds.width:0} x {layout.PlayBounds.height:0} Unity units",
                $"Rooms: {layout.Definition.rooms.Length}",
                $"Connections: {layout.Definition.connections.Length}",
                $"TaskPoints: {layout.TaskPoints.Length}",
                $"SabotagePoints: {layout.SabotagePoints.Length}",
                $"VentPoints: {layout.VentNodes.Length}",
                "",
                "## Summary",
                "",
                $"Overall: {(result.Passed ? "PASS" : "FAIL")}",
                "",
                "| Check | Result | Detail |",
                "|---|---|---|"
            };

            foreach (var check in result.Checks)
            {
                lines.Add($"| {check.Name} | {(check.Passed ? "PASS" : "FAIL")} | {check.Detail.Replace("|", "/")} |");
            }

            lines.Add("");
            lines.Add("## Route Metrics");
            lines.Add("");
            lines.Add($"- West-east independent routes: {CountSideRoutes(layout, true)}");
            lines.Add($"- North-south independent routes: {CountSideRoutes(layout, false)}");
            lines.Add($"- Dead-end rooms: {graph.Count(pair => pair.Value.Count <= 1)}");
            lines.Add($"- Bridge dependencies: {(FindBridges(layout, graph).Length == 0 ? "none" : string.Join(", ", FindBridges(layout, graph)))}");
            lines.Add("");
            lines.Add("## Manual Checks");
            lines.Add("");
            lines.Add("- Open `Assets/Game/Maps/AstraResearchComplex_Blockout.unity`.");
            lines.Add("- Enter Play Mode, Host, then Start Game; players should spawn from the 15 Central Lounge spawn points.");
            lines.Add("- Move with WASD or arrow keys and confirm walls, obstacles, cut corners, and the map boundary block traversal.");
            lines.Add("- Verify camera follow at Orthographic Size 6; the full 150 x 116 map should not fit on screen.");
            lines.Add("- Confirm the HUD shows `Your tasks: completed/assigned` and aggregate Crew progress.");
            lines.Add("- Confirm the task tracker shows each incomplete assignment's room, distance, and direction; the nearest task also has a screen-edge pointer and pulsing world marker.");
            lines.Add("- Confirm timed task alerts occur at 30 seconds and 75 seconds with a flashing message and notification tone.");
            lines.Add("- Confirm the task deadline counts down from 180 seconds in the HUD.");
            lines.Add("- Leave tasks incomplete and confirm a 4-second failure cut-in appears before the Impostor victory result.");
            lines.Add("- Use hold E for Data Transfer, four E presses for Circuit Pulse, and release E in the 70-82% band for Calibration.");
            lines.Add("- Use E for meetings/repairs, F for sabotage, V for vents, Q for kill, and R for report.");

            File.WriteAllLines(ReportPath, lines);
        }

        private static ValidationCheck CheckPointCount(string label, AstraBlockoutMarkerKind kind, int definitionCount, int expected, bool minimum)
        {
            var sceneCount = CountMarkers(kind);
            var passed = minimum
                ? definitionCount >= expected && sceneCount >= expected
                : definitionCount == expected && sceneCount == expected;
            var comparator = minimum ? ">=" : "==";
            return Check($"{label} count {comparator} {expected}", passed, $"definition={definitionCount}, scene={sceneCount}");
        }

        private static bool PointsOpen(AstraLayout layout, PointFeatureDefinition[] points, float radius)
        {
            return points.All(point => AstraResearchComplexBlockoutGenerator.IsOpen(layout, point.position.ToVector2(), radius));
        }

        private static bool PointsOutsideBlockingColliders(PointFeatureDefinition[] points, float radius)
        {
            foreach (var point in points)
            {
                var colliders = Physics2D.OverlapCircleAll(point.position.ToVector2(), radius);
                if (colliders.Any(collider => !collider.isTrigger))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool BoundarySealed(AstraLayout layout)
        {
            var boundary = GameObject.Find("MapBoundary");
            if (boundary == null)
            {
                return false;
            }

            var colliders = boundary.GetComponentsInChildren<BoxCollider2D>();
            if (colliders.Length < 4)
            {
                return false;
            }

            var bounds = layout.PlayBounds;
            var worldBounds = colliders.Select(collider =>
            {
                var center = collider.transform.TransformPoint(collider.offset);
                return new Rect(center.x - collider.size.x * 0.5f, center.y - collider.size.y * 0.5f, collider.size.x, collider.size.y);
            }).ToArray();

            return worldBounds.Any(rect => rect.yMin <= bounds.yMax && rect.yMax >= bounds.yMax && rect.xMin <= bounds.xMin && rect.xMax >= bounds.xMax) &&
                worldBounds.Any(rect => rect.yMin <= bounds.yMin && rect.yMax >= bounds.yMin && rect.xMin <= bounds.xMin && rect.xMax >= bounds.xMax) &&
                worldBounds.Any(rect => rect.xMin <= bounds.xMin && rect.xMax >= bounds.xMin && rect.yMin <= bounds.yMin && rect.yMax >= bounds.yMax) &&
                worldBounds.Any(rect => rect.xMin <= bounds.xMax && rect.xMax >= bounds.xMax && rect.yMin <= bounds.yMin && rect.yMax >= bounds.yMax);
        }

        private static int TilemapCompositeColliderCount()
        {
            return UnityEngine.Object.FindObjectsByType<TilemapCollider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(collider => collider.GetComponent<CompositeCollider2D>() != null);
        }

        private static int CountMarkers(AstraBlockoutMarkerKind kind)
        {
            return UnityEngine.Object.FindObjectsByType<AstraBlockoutMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(marker => marker.Kind == kind);
        }

        private static int CountStaticMapNetworkObjects()
        {
            var count = 0;
            foreach (var rootName in new[] { "Environment", "Collision" })
            {
                var root = GameObject.Find(rootName);
                if (root != null)
                {
                    count += root.GetComponentsInChildren<NetworkObject>(true).Length;
                }
            }

            return count;
        }

        private static unsafe bool PlayerPrefabIsAssigned()
        {
            var spawner = UnityEngine.Object.FindFirstObjectByType<BasicSpawner>(FindObjectsInactive.Include);
            if (spawner == null)
            {
                return false;
            }

            var prefabRef = new SerializedObject(spawner).FindProperty("_playerPrefab");
            var rawGuid = prefabRef?.FindPropertyRelative(nameof(NetworkObjectGuid.RawGuidValue));
            if (rawGuid == null)
            {
                return false;
            }

            var expected = NetworkObjectGuid.Parse(AssetDatabase.AssetPathToGUID(PlayerPrefabPath));
            return rawGuid.GetFixedBufferElementAtIndex(0).longValue == expected.RawGuidValue[0] &&
                rawGuid.GetFixedBufferElementAtIndex(1).longValue == expected.RawGuidValue[1];
        }

        private static bool SpawnerIntEquals(string propertyName, int expected)
        {
            var spawner = UnityEngine.Object.FindFirstObjectByType<BasicSpawner>(FindObjectsInactive.Include);
            var property = spawner == null ? null : new SerializedObject(spawner).FindProperty(propertyName);
            return property != null && property.intValue == expected;
        }

        private static bool SpawnerFloatEquals(string propertyName, float expected)
        {
            var spawner = UnityEngine.Object.FindFirstObjectByType<BasicSpawner>(FindObjectsInactive.Include);
            var property = spawner == null ? null : new SerializedObject(spawner).FindProperty(propertyName);
            return property != null && Mathf.Approximately(property.floatValue, expected);
        }

        private static bool PlayerPrefabSpawnable()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            return prefab != null &&
                prefab.GetComponent<NetworkObject>() != null &&
                prefab.GetComponent<Player>() != null;
        }

        private static int CountMissingScripts()
        {
            var count = 0;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                }
            }

            return count;
        }

        private static int CountMissingSprites()
        {
            var missing = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(renderer => renderer.sprite == null);

            foreach (var tilemap in UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var position in tilemap.cellBounds.allPositionsWithin)
                {
                    var tile = tilemap.GetTile(position);
                    if (tile == null)
                    {
                        continue;
                    }

                    var data = new UnityEngine.Tilemaps.TileData();
                    tile.GetTileData(position, tilemap, ref data);
                    if (data.sprite == null)
                    {
                        missing++;
                    }
                }
            }

            return missing;
        }

        private static bool HasGlobalLight2D()
        {
            return UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(light => light.lightType == Light2D.LightType.Global && light.intensity > 0f);
        }

        private static bool HasVisibleBacking(string name)
        {
            var gameObject = GameObject.Find(name);
            if (gameObject == null)
            {
                return false;
            }

            var renderer = gameObject.GetComponent<MeshRenderer>();
            return renderer != null && renderer.enabled && renderer.sharedMaterial != null;
        }

        private static Dictionary<string, List<string>> BuildGraph(AstraLayout layout)
        {
            var graph = layout.Definition.rooms.ToDictionary(room => room.id, _ => new List<string>());
            foreach (var connection in layout.Definition.connections)
            {
                if (!graph.ContainsKey(connection.from) || !graph.ContainsKey(connection.to))
                {
                    continue;
                }

                graph[connection.from].Add(connection.to);
                graph[connection.to].Add(connection.from);
            }

            return graph;
        }

        private static bool IsConnected(Dictionary<string, List<string>> graph)
        {
            return CountReachable(graph) == graph.Count;
        }

        private static int CountReachable(Dictionary<string, List<string>> graph)
        {
            if (graph.Count == 0)
            {
                return 0;
            }

            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(graph.Keys.First());
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current))
                {
                    continue;
                }

                foreach (var next in graph[current])
                {
                    queue.Enqueue(next);
                }
            }

            return visited.Count;
        }

        private static string[] FindBridges(AstraLayout layout, Dictionary<string, List<string>> graph)
        {
            var bridges = new List<string>();
            foreach (var connection in layout.Definition.connections)
            {
                var clone = graph.ToDictionary(pair => pair.Key, pair => new List<string>(pair.Value));
                clone[connection.from].Remove(connection.to);
                clone[connection.to].Remove(connection.from);
                if (!IsConnected(clone))
                {
                    bridges.Add(connection.id);
                }
            }

            return bridges.ToArray();
        }

        private static int CountSideRoutes(AstraLayout layout, bool westEast)
        {
            var sources = layout.Definition.rooms
                .Where(room => westEast ? room.center.x < 0f : room.center.y > 0f)
                .Select(room => room.id)
                .ToArray();
            var sinks = layout.Definition.rooms
                .Where(room => westEast ? room.center.x > 0f : room.center.y < 0f)
                .Select(room => room.id)
                .ToArray();
            return MaxEdgeDisjointPaths(layout, sources, sinks);
        }

        private static int MaxEdgeDisjointPaths(AstraLayout layout, string[] sources, string[] sinks)
        {
            var capacity = new Dictionary<string, Dictionary<string, int>>();
            void AddCapacity(string from, string to, int value)
            {
                if (!capacity.TryGetValue(from, out var edges))
                {
                    edges = new Dictionary<string, int>();
                    capacity[from] = edges;
                }

                edges[to] = edges.TryGetValue(to, out var current) ? current + value : value;
            }

            foreach (var connection in layout.Definition.connections)
            {
                AddCapacity(connection.from, connection.to, 1);
                AddCapacity(connection.to, connection.from, 1);
            }

            foreach (var source in sources)
            {
                AddCapacity("__source", source, 99);
            }

            foreach (var sink in sinks)
            {
                AddCapacity(sink, "__sink", 99);
            }

            var flow = 0;
            while (TryFindAugmentingPath(capacity, "__source", "__sink", out var path))
            {
                for (var i = 0; i < path.Count - 1; i++)
                {
                    var from = path[i];
                    var to = path[i + 1];
                    capacity[from][to]--;
                    AddCapacity(to, from, 1);
                }

                flow++;
            }

            return flow;
        }

        private static bool TryFindAugmentingPath(Dictionary<string, Dictionary<string, int>> capacity, string source, string sink, out List<string> path)
        {
            var parent = new Dictionary<string, string> { { source, null } };
            var queue = new Queue<string>();
            queue.Enqueue(source);

            while (queue.Count > 0 && !parent.ContainsKey(sink))
            {
                var current = queue.Dequeue();
                if (!capacity.TryGetValue(current, out var edges))
                {
                    continue;
                }

                foreach (var edge in edges)
                {
                    if (edge.Value <= 0 || parent.ContainsKey(edge.Key))
                    {
                        continue;
                    }

                    parent[edge.Key] = current;
                    queue.Enqueue(edge.Key);
                }
            }

            if (!parent.ContainsKey(sink))
            {
                path = null;
                return false;
            }

            path = new List<string>();
            var node = sink;
            while (node != null)
            {
                path.Add(node);
                node = parent[node];
            }

            path.Reverse();
            return true;
        }

        private static RuntimeReachability CheckRuntimeReachability(AstraLayout layout, float radius)
        {
            var roomBounds = layout.RuntimeRooms.Select(room => room.bounds.ToRect()).ToArray();
            var corridors = layout.RuntimeCorridors.Select(corridor => corridor.bounds.ToRect()).ToArray();
            var doorways = layout.RuntimeDoorways.Select(doorway => doorway.bounds.ToRect()).ToArray();
            var obstacles = layout.RuntimeObstacles.Select(obstacle => obstacle.bounds.ToRect()).ToArray();
            var meetingPoint = layout.MeetingPoint == null ? Vector2.zero : layout.MeetingPoint.position.ToVector2();
            var geometry = new RuntimeGeometry(roomBounds, corridors, doorways, obstacles);

            if (layout.MeetingPoint == null || RuntimeBlocked(layout, geometry, meetingPoint, radius))
            {
                return new RuntimeReachability(0, 0, "meeting point is blocked", "meeting point is blocked");
            }

            var xCoordinates = BuildNavigationCoordinates(layout.PlayBounds, geometry, radius, true);
            var yCoordinates = BuildNavigationCoordinates(layout.PlayBounds, geometry, radius, false);
            var width = xCoordinates.Length - 1;
            var height = yCoordinates.Length - 1;
            if (width <= 0 || height <= 0)
            {
                return new RuntimeReachability(0, 0, "runtime geometry has no navigable cells", "runtime geometry has no navigable cells");
            }

            var open = new bool[width * height];
            var visited = new bool[open.Length];
            for (var x = 0; x < width; x++)
            {
                var sampleX = (xCoordinates[x] + xCoordinates[x + 1]) * 0.5f;
                for (var y = 0; y < height; y++)
                {
                    var sample = new Vector2(sampleX, (yCoordinates[y] + yCoordinates[y + 1]) * 0.5f);
                    open[RuntimeCellIndex(x, y, height)] = !RuntimeBlocked(layout, geometry, sample, radius);
                }
            }

            var queue = new Queue<Vector2Int>();
            for (var x = 0; x < width; x++)
            {
                if (meetingPoint.x < xCoordinates[x] || meetingPoint.x > xCoordinates[x + 1])
                {
                    continue;
                }

                for (var y = 0; y < height; y++)
                {
                    if (meetingPoint.y < yCoordinates[y] || meetingPoint.y > yCoordinates[y + 1])
                    {
                        continue;
                    }

                    var index = RuntimeCellIndex(x, y, height);
                    if (open[index])
                    {
                        queue.Enqueue(new Vector2Int(x, y));
                    }
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentIndex = RuntimeCellIndex(current.x, current.y, height);
                if (visited[currentIndex])
                {
                    continue;
                }

                visited[currentIndex] = true;
                TryQueueRuntimeNeighbor(layout, geometry, radius, current.x - 1, current.y, current.x, current.y, xCoordinates, yCoordinates, width, height, open, visited, queue);
                TryQueueRuntimeNeighbor(layout, geometry, radius, current.x + 1, current.y, current.x, current.y, xCoordinates, yCoordinates, width, height, open, visited, queue);
                TryQueueRuntimeNeighbor(layout, geometry, radius, current.x, current.y - 1, current.x, current.y, xCoordinates, yCoordinates, width, height, open, visited, queue);
                TryQueueRuntimeNeighbor(layout, geometry, radius, current.x, current.y + 1, current.x, current.y, xCoordinates, yCoordinates, width, height, open, visited, queue);
            }

            var unreachableRooms = new List<string>();
            foreach (var room in layout.RuntimeRooms)
            {
                var bounds = ShipMapGeometry.Inset(room.bounds.ToRect(), ShipMapGeometry.WalkableEdgeInset);
                var reached = false;
                for (var x = 0; x < width && !reached; x++)
                {
                    var sampleX = (xCoordinates[x] + xCoordinates[x + 1]) * 0.5f;
                    for (var y = 0; y < height; y++)
                    {
                        var index = RuntimeCellIndex(x, y, height);
                        var sample = new Vector2(sampleX, (yCoordinates[y] + yCoordinates[y + 1]) * 0.5f);
                        if (visited[index] && bounds.Contains(sample))
                        {
                            reached = true;
                            break;
                        }
                    }
                }

                if (!reached)
                {
                    unreachableRooms.Add(room.id);
                }
            }

            var unreachableTasks = layout.TaskPoints
                .Where(task => !RuntimePointReached(task.position.ToVector2(), xCoordinates, yCoordinates, height, visited, layout, geometry, radius))
                .Select(task => task.id)
                .ToArray();

            var reachableRoomCount = layout.RuntimeRooms.Length - unreachableRooms.Count;
            var reachableTaskCount = layout.TaskPoints.Length - unreachableTasks.Length;
            var roomDetail = $"{reachableRoomCount}/{layout.RuntimeRooms.Length} rooms reachable at radius {radius:0.00}";
            if (unreachableRooms.Count > 0)
            {
                roomDetail += $"; blocked={string.Join(", ", unreachableRooms)}";
            }

            var taskDetail = $"{reachableTaskCount}/{layout.TaskPoints.Length} task points reachable at radius {radius:0.00}";
            if (unreachableTasks.Length > 0)
            {
                taskDetail += $"; blocked={string.Join(", ", unreachableTasks)}";
            }

            return new RuntimeReachability(reachableRoomCount, reachableTaskCount, roomDetail, taskDetail);
        }

        private static void TryQueueRuntimeNeighbor(
            AstraLayout layout,
            RuntimeGeometry geometry,
            float radius,
            int nextX,
            int nextY,
            int currentX,
            int currentY,
            float[] xCoordinates,
            float[] yCoordinates,
            int width,
            int height,
            bool[] open,
            bool[] visited,
            Queue<Vector2Int> queue)
        {
            if (nextX < 0 || nextX >= width || nextY < 0 || nextY >= height)
            {
                return;
            }

            var nextIndex = RuntimeCellIndex(nextX, nextY, height);
            if (!open[nextIndex] || visited[nextIndex])
            {
                return;
            }

            Vector2 boundarySample;
            if (nextX != currentX)
            {
                var boundaryX = xCoordinates[Mathf.Max(nextX, currentX)];
                boundarySample = new Vector2(boundaryX, (yCoordinates[currentY] + yCoordinates[currentY + 1]) * 0.5f);
            }
            else
            {
                var boundaryY = yCoordinates[Mathf.Max(nextY, currentY)];
                boundarySample = new Vector2((xCoordinates[currentX] + xCoordinates[currentX + 1]) * 0.5f, boundaryY);
            }

            if (!RuntimeBlocked(layout, geometry, boundarySample, radius))
            {
                queue.Enqueue(new Vector2Int(nextX, nextY));
            }
        }

        private static bool RuntimePointReached(
            Vector2 point,
            float[] xCoordinates,
            float[] yCoordinates,
            int height,
            bool[] visited,
            AstraLayout layout,
            RuntimeGeometry geometry,
            float radius)
        {
            if (RuntimeBlocked(layout, geometry, point, radius))
            {
                return false;
            }

            for (var x = 0; x < xCoordinates.Length - 1; x++)
            {
                if (point.x < xCoordinates[x] || point.x > xCoordinates[x + 1])
                {
                    continue;
                }

                for (var y = 0; y < yCoordinates.Length - 1; y++)
                {
                    if (point.y >= yCoordinates[y] && point.y <= yCoordinates[y + 1] && visited[RuntimeCellIndex(x, y, height)])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool RuntimeBlocked(AstraLayout layout, RuntimeGeometry geometry, Vector2 point, float radius)
        {
            var movementBounds = new Rect(
                layout.PlayBounds.xMin + radius,
                layout.PlayBounds.yMin + radius,
                layout.PlayBounds.width - radius * 2f,
                layout.PlayBounds.height - radius * 2f);
            return !movementBounds.Contains(point) || ShipMapGeometry.IsBlocked(
                point,
                radius,
                geometry.RoomBounds,
                geometry.Corridors,
                geometry.Doorways,
                geometry.Obstacles);
        }

        private static float[] BuildNavigationCoordinates(Rect playBounds, RuntimeGeometry geometry, float radius, bool xAxis)
        {
            var minimum = (xAxis ? playBounds.xMin : playBounds.yMin) + radius;
            var maximum = (xAxis ? playBounds.xMax : playBounds.yMax) - radius;
            var values = new List<float> { minimum, maximum };
            var walkableInset = Mathf.Min(ShipMapGeometry.WalkableEdgeInset, Mathf.Max(0f, radius) * 0.1f);

            AddRuntimeRectEdges(values, geometry.RoomBounds, walkableInset, minimum, maximum, xAxis, false);
            AddRuntimeRectEdges(values, geometry.Corridors, walkableInset, minimum, maximum, xAxis, false);
            AddRuntimeRectEdges(values, geometry.Doorways, walkableInset, minimum, maximum, xAxis, false);
            AddRuntimeRectEdges(values, geometry.Obstacles, radius, minimum, maximum, xAxis, true);

            var sorted = values.OrderBy(value => value).ToArray();
            var unique = new List<float>(sorted.Length);
            foreach (var value in sorted)
            {
                if (unique.Count == 0 || Mathf.Abs(unique[unique.Count - 1] - value) > 0.0001f)
                {
                    unique.Add(value);
                }
            }

            return unique.ToArray();
        }

        private static void AddRuntimeRectEdges(
            List<float> values,
            Rect[] rects,
            float amount,
            float minimum,
            float maximum,
            bool xAxis,
            bool inflate)
        {
            foreach (var source in rects)
            {
                var rect = inflate ? ShipMapGeometry.Inflate(source, amount) : ShipMapGeometry.Inset(source, amount);
                var first = xAxis ? rect.xMin : rect.yMin;
                var second = xAxis ? rect.xMax : rect.yMax;
                if (first > minimum && first < maximum)
                {
                    values.Add(first);
                }

                if (second > minimum && second < maximum)
                {
                    values.Add(second);
                }
            }
        }

        private static int RuntimeCellIndex(int x, int y, int height)
        {
            return x * height + y;
        }

        private static ValidationCheck Check(string name, bool passed, string detail)
        {
            return new ValidationCheck(name, passed, detail);
        }

        private static ValidationCheck Pass(string name, string detail)
        {
            return Check(name, true, detail);
        }

        private static ValidationCheck Fail(string name, string detail)
        {
            return Check(name, false, detail);
        }

        private sealed class RuntimeGeometry
        {
            public readonly Rect[] RoomBounds;
            public readonly Rect[] Corridors;
            public readonly Rect[] Doorways;
            public readonly Rect[] Obstacles;

            public RuntimeGeometry(Rect[] roomBounds, Rect[] corridors, Rect[] doorways, Rect[] obstacles)
            {
                RoomBounds = roomBounds;
                Corridors = corridors;
                Doorways = doorways;
                Obstacles = obstacles;
            }
        }

        private readonly struct RuntimeReachability
        {
            public readonly int ReachableRoomCount;
            public readonly int ReachableTaskCount;
            public readonly string RoomDetail;
            public readonly string TaskDetail;

            public RuntimeReachability(int reachableRoomCount, int reachableTaskCount, string roomDetail, string taskDetail)
            {
                ReachableRoomCount = reachableRoomCount;
                ReachableTaskCount = reachableTaskCount;
                RoomDetail = roomDetail;
                TaskDetail = taskDetail;
            }
        }
    }

    internal sealed class AstraLayout
    {
        public AstraMapDefinition Definition;
        public int Seed;
        public Rect PlayBounds;
        public readonly List<AstraRoomLayout> Rooms = new List<AstraRoomLayout>();
        public readonly List<AstraConnectionLayout> Connections = new List<AstraConnectionLayout>();
        public readonly HashSet<Vector2Int> RoomFloorCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> CorridorCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> FloorCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> ShapeWallCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> WallCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> InteriorObstacleCells = new HashSet<Vector2Int>();
        public readonly List<AstraObstacle> Obstacles = new List<AstraObstacle>();
        public RoomDefinition[] RuntimeRooms = Array.Empty<RoomDefinition>();
        public RectFeatureDefinition[] RuntimeCorridors = Array.Empty<RectFeatureDefinition>();
        public RectFeatureDefinition[] RuntimeDoorways = Array.Empty<RectFeatureDefinition>();
        public RectFeatureDefinition[] RuntimeObstacles = Array.Empty<RectFeatureDefinition>();
        public PointFeatureDefinition[] NavigationWaypoints = Array.Empty<PointFeatureDefinition>();
        public PointFeatureDefinition[] SpawnPoints = Array.Empty<PointFeatureDefinition>();
        public PointFeatureDefinition[] TaskPoints = Array.Empty<PointFeatureDefinition>();
        public SabotagePointDefinition[] SabotagePoints = Array.Empty<SabotagePointDefinition>();
        public VentNodeDefinition[] VentNodes = Array.Empty<VentNodeDefinition>();
        public PointFeatureDefinition MeetingPoint;
    }

    internal sealed class AstraRoomLayout
    {
        public AstraRoomDefinition Definition;
        public Rect Bounds;
        public Vector2[] Polygon;
        public readonly HashSet<Vector2Int> FloorCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> CutoutCells = new HashSet<Vector2Int>();
    }

    internal sealed class AstraConnectionLayout
    {
        public AstraConnectionDefinition Definition;
        public Vector2[] Points;
    }

    internal sealed class AstraObstacle
    {
        public string Id;
        public string DisplayName;
        public string Kind;
        public Rect Bounds;
    }

    public sealed class ValidationResult
    {
        public readonly IReadOnlyList<ValidationCheck> Checks;
        public bool Passed => Checks.All(check => check.Passed);

        public ValidationResult(IReadOnlyList<ValidationCheck> checks)
        {
            Checks = checks;
        }
    }

    public readonly struct ValidationCheck
    {
        public readonly string Name;
        public readonly bool Passed;
        public readonly string Detail;

        public ValidationCheck(string name, bool passed, string detail)
        {
            Name = name;
            Passed = passed;
            Detail = detail;
        }
    }

    [Serializable]
    public sealed class AstraMapDefinition
    {
        public string schemaVersion;
        public string mapId;
        public string displayName;
        public string displayNameJa;
        public AstraBoundsDefinition bounds;
        public AstraCameraDefinition camera;
        public AstraRoomDefinition[] rooms;
        public AstraConnectionDefinition[] connections;
        public AstraSpawnDefinition[] spawnPoints;
        public AstraMeetingPointDefinition meetingPoint;
        public AstraTaskDistributionDefinition taskDistribution;
        public AstraSabotageDefinition[] sabotagePoints;
        public AstraVentGroupDefinition[] ventGroups;
        public AstraValidationTargets validationTargets;
    }

    [Serializable]
    public sealed class AstraBoundsDefinition
    {
        public float minX;
        public float maxX;
        public float minY;
        public float maxY;
    }

    [Serializable]
    public sealed class AstraCameraDefinition
    {
        public string projection;
        public float orthographicSize = 6f;
    }

    [Serializable]
    public sealed class AstraRoomDefinition
    {
        public string id;
        public string displayName;
        public string displayNameJa;
        public Vector2Definition center;
        public AstraSizeDefinition size;
        public string shape;
        public string importance;
        public int entranceCountTarget;
        public string artAnchor;
        public string dangerProfile;
    }

    [Serializable]
    public sealed class AstraSizeDefinition
    {
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class AstraConnectionDefinition
    {
        public string id;
        public string from;
        public string to;
        public string type;
        public float width;
        public Vector2Definition[] path;
    }

    [Serializable]
    public sealed class AstraSpawnDefinition
    {
        public string id;
        public string roomId;
        public Vector2Definition position;
    }

    [Serializable]
    public sealed class AstraMeetingPointDefinition
    {
        public string id;
        public string roomId;
        public Vector2Definition position;
    }

    [Serializable]
    public sealed class AstraTaskDistributionDefinition
    {
        public int minimumTaskPoints;
        public TaskPerRoomDefinition perRoom;
    }

    [Serializable]
    public sealed class TaskPerRoomDefinition
    {
        public int central_lounge;
        public int medical_lab;
        public int xenobiology_lab;
        public int observation_deck;
        public int communications;
        public int security_control;
        public int engine_maintenance;
        public int reactor_core;
        public int power_distribution;
        public int cargo_storage;
        public int life_support;
    }

    [Serializable]
    public sealed class AstraSabotageDefinition
    {
        public string id;
        public string roomId;
    }

    [Serializable]
    public sealed class AstraVentGroupDefinition
    {
        public string id;
        public string[] rooms;
    }

    [Serializable]
    public sealed class AstraValidationTargets
    {
        public bool allRoomsReachable;
        public int minimumIndependentRoutesBetweenWestAndEast;
        public int minimumIndependentRoutesBetweenNorthAndSouth;
        public int maximumSinglePathDependency;
        public int maximumDeadEndRooms;
    }
}
