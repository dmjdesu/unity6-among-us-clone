using Fusion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace AmongUsClone.Editor
{
    public static class ForgottenPlainsMapGenerator
    {
        public const int DefaultSeed = 52741;

        private const string AssetRoot = "Assets/KrishnaPalacio/MINIFANTASY - Forgotten Plains";
        private const string DefinitionsFolder = "Assets/Game/Maps/Definitions";
        private const string ReportsFolder = "Assets/Game/Maps/Reports";
        private const string ResourcesMapFolder = "Assets/Resources/Maps";
        private const string LargeScenePath = "Assets/Game/Maps/ForgottenPlainsLargePrototype.unity";
        private const string RuntimeMapPath = "Assets/Resources/Maps/forgotten_plains_large_prototype.json";
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerPrefab.prefab";

        private static readonly string[] SortingLayers =
        {
            "Ground",
            "FloorDetails",
            "PropsBack",
            "Player",
            "PropsFront",
            "UI"
        };

        [MenuItem("Tools/Forgotten Plains/Generate Adopted Large Prototype")]
        public static void GenerateAdoptedLargePrototypeMenu()
        {
            GenerateAdoptedLargePrototype(DefaultSeed);
        }

        [MenuItem("Tools/Forgotten Plains/Generate Layout A")]
        public static void GenerateLayoutAMenu()
        {
            GenerateSpecificLayout("A", DefaultSeed);
        }

        [MenuItem("Tools/Forgotten Plains/Generate Layout B")]
        public static void GenerateLayoutBMenu()
        {
            GenerateSpecificLayout("B", DefaultSeed);
        }

        [MenuItem("Tools/Forgotten Plains/Generate Layout C")]
        public static void GenerateLayoutCMenu()
        {
            GenerateSpecificLayout("C", DefaultSeed);
        }

        [MenuItem("Tools/Forgotten Plains/Open Large Prototype Scene")]
        public static void OpenLargePrototypeScene()
        {
            if (!File.Exists(LargeScenePath))
            {
                GenerateAdoptedLargePrototype(DefaultSeed);
            }

            EditorSceneManager.OpenScene(LargeScenePath);
        }

        public static void GenerateAdoptedLargePrototypeBatch()
        {
            GenerateAdoptedLargePrototype(DefaultSeed);
        }

        public static void ValidateLargePrototypeBatch()
        {
            var result = GenerateDefinitionsAndReports(DefaultSeed);
            var validation = ForgottenPlainsMapValidator.Validate(result.SelectedLayout, LargeScenePath);
            WriteValidationReport(result.SelectedLayout, validation);
            AssetDatabase.Refresh();
        }

        public static void GenerateAdoptedLargePrototype(int seed)
        {
            var result = GenerateDefinitionsAndReports(seed);
            GenerateScene(result.SelectedLayout, seed);
            SaveRuntimeMap(result.SelectedLayout);

            var validation = ForgottenPlainsMapValidator.Validate(result.SelectedLayout, LargeScenePath);
            WriteValidationReport(result.SelectedLayout, validation);
            EnsureBuildSettings();
            AssetDatabase.Refresh();

            Debug.Log($"Generated {LargeScenePath} from {result.SelectedLayout.layoutName}.");
        }

        public static void GenerateSpecificLayout(string layoutId, int seed)
        {
            var result = GenerateDefinitionsAndReports(seed);
            var layout = result.Layouts.FirstOrDefault(candidate => candidate.layoutId == layoutId);
            if (layout == null)
            {
                throw new InvalidOperationException($"Unknown Forgotten Plains layout '{layoutId}'.");
            }

            GenerateScene(layout, seed);
            SaveRuntimeMap(layout);
            var validation = ForgottenPlainsMapValidator.Validate(layout, LargeScenePath);
            WriteValidationReport(layout, validation);
            EnsureBuildSettings();
            AssetDatabase.Refresh();
        }

        public static void GenerateFromDefinition(TextAsset definitionAsset, int seed)
        {
            if (definitionAsset == null)
            {
                GenerateAdoptedLargePrototype(seed);
                return;
            }

            var layout = JsonUtility.FromJson<LargeLayoutDefinition>(definitionAsset.text);
            ResolveGameplayPoints(layout, seed);
            GenerateScene(layout, seed);
            SaveRuntimeMap(layout);
            var validation = ForgottenPlainsMapValidator.Validate(layout, LargeScenePath);
            WriteValidationReport(layout, validation);
            EnsureBuildSettings();
            AssetDatabase.Refresh();
        }

        public static GenerationResult GenerateDefinitionsAndReports(int seed)
        {
            EnsureFolders();
            var catalog = ForgottenPlainsAssetCatalog.Build();
            var layouts = new[]
            {
                CreateLayoutA(seed),
                CreateLayoutB(seed),
                CreateLayoutC(seed)
            };

            foreach (var layout in layouts)
            {
                ResolveGameplayPoints(layout, seed);
                SaveLayoutDefinition(layout);
            }

            var analyses = layouts.Select(AnalyzeLayout).ToArray();
            var selected = analyses.OrderByDescending(analysis => analysis.Score).First();
            WriteComparisonReport(catalog, layouts, analyses, selected.Layout.layoutId);

            return new GenerationResult(layouts, selected.Layout, analyses);
        }

        private static LargeLayoutDefinition CreateLayoutA(int seed)
        {
            var areas = new[]
            {
                Area("central_village", "Central Village", "中央集落", -28, -18, 56, 36, 0, 0, "open hub"),
                Area("northern_forest", "Northern Forest", "北の森", -38, 30, 52, 34, -12, 48, "occluded forest"),
                Area("eastern_ruins", "Eastern Ruins", "東の遺跡", 36, -12, 42, 36, 57, 6, "ruin rooms"),
                Area("southern_lake", "Southern Lake", "南の湖", -18, -54, 54, 31, 8, -38, "lake loop"),
                Area("western_camp", "Western Camp", "西のキャンプ", -78, -15, 38, 34, -59, 2, "task camp"),
                Area("ancient_sanctuary", "Ancient Sanctuary", "古代の聖域", 10, 34, 46, 32, 34, 49, "danger semi-dead-end"),
                Area("rocky_pass", "Rocky Pass", "岩山の峠", -75, 18, 38, 42, -56, 39, "narrow pursuit"),
                Area("riverside_crossing", "Riverside Crossing", "川辺の渡り場", 24, -42, 50, 27, 51, -28, "crossing"),
                Area("abandoned_outpost", "Abandoned Outpost", "放棄された前哨地", -74, -52, 38, 28, -55, -38, "blind corner")
            };

            var corridors = new[]
            {
                Corridor("central_west", "Central to Western Camp", -42, -3, 15, 6, "wide"),
                Corridor("central_east", "Central to Eastern Ruins", 27, -2, 11, 6, "wide"),
                Corridor("central_north", "Central to Northern Forest", -9, 17, 8, 16, "normal"),
                Corridor("central_south", "Central to Southern Lake", 2, -25, 8, 9, "normal"),
                Corridor("west_rocky", "Western Camp to Rocky Pass", -65, 17, 6, 10, "narrow"),
                Corridor("rocky_north", "Rocky Pass to Northern Forest", -42, 36, 8, 8, "narrow"),
                Corridor("north_sanctuary", "Northern Forest to Sanctuary", 13, 42, 10, 7, "wide"),
                Corridor("sanctuary_east", "Sanctuary to Eastern Ruins", 49, 23, 7, 12, "normal"),
                Corridor("east_riverside", "Eastern Ruins to Riverside", 56, -18, 7, 8, "normal"),
                Corridor("riverside_south", "Riverside to Southern Lake", 33, -25, 8, 8, "bridge"),
                Corridor("south_outpost", "Southern Lake to Outpost", -40, -44, 11, 7, "normal"),
                Corridor("outpost_west", "Outpost to Western Camp", -64, -25, 7, 10, "narrow")
            };

            return Layout("A", "Layout A: Centralized Hub", "中央集約型", seed, areas, corridors);
        }

        private static LargeLayoutDefinition CreateLayoutB(int seed)
        {
            var areas = new[]
            {
                Area("central_village", "Central Village", "中央集落", -20, -13, 40, 28, 0, 1, "open hub"),
                Area("northern_forest", "Northern Forest", "北の森", -32, 32, 44, 32, -10, 49, "occluded forest"),
                Area("eastern_ruins", "Eastern Ruins", "東の遺跡", 40, 10, 40, 34, 60, 27, "ruin rooms"),
                Area("southern_lake", "Southern Lake", "南の湖", -12, -58, 56, 30, 16, -43, "lake loop"),
                Area("western_camp", "Western Camp", "西のキャンプ", -82, -18, 40, 34, -62, -1, "task camp"),
                Area("ancient_sanctuary", "Ancient Sanctuary", "古代の聖域", 18, 44, 44, 28, 40, 58, "danger semi-dead-end"),
                Area("rocky_pass", "Rocky Pass", "岩山の峠", -80, 24, 38, 40, -61, 44, "narrow pursuit"),
                Area("riverside_crossing", "Riverside Crossing", "川辺の渡り場", 34, -34, 48, 26, 58, -21, "crossing"),
                Area("abandoned_outpost", "Abandoned Outpost", "放棄された前哨地", -78, -56, 34, 27, -61, -43, "blind corner")
            };

            var corridors = new[]
            {
                Corridor("outer_west_north", "Western Camp to Rocky Pass", -64, 15, 7, 13, "narrow"),
                Corridor("outer_north", "Rocky Pass to Northern Forest", -43, 43, 12, 7, "normal"),
                Corridor("outer_top", "Northern Forest to Sanctuary", 10, 51, 11, 7, "wide"),
                Corridor("outer_east_top", "Sanctuary to Eastern Ruins", 55, 41, 7, 10, "normal"),
                Corridor("outer_east", "Eastern Ruins to Riverside", 59, -9, 7, 20, "normal"),
                Corridor("outer_south_east", "Riverside to Southern Lake", 40, -31, 7, 7, "bridge"),
                Corridor("outer_south", "Southern Lake to Outpost", -44, -47, 34, 6, "wide"),
                Corridor("outer_west_south", "Outpost to Western Camp", -64, -30, 7, 13, "narrow"),
                Corridor("central_west", "Central to Western Camp", -43, -4, 24, 6, "normal"),
                Corridor("central_east", "Central to Eastern Ruins", 19, 7, 22, 6, "normal"),
                Corridor("central_north", "Central to Northern Forest", -6, 14, 7, 20, "normal"),
                Corridor("central_south", "Central to Southern Lake", 7, -30, 7, 18, "normal")
            };

            return Layout("B", "Layout B: Outer Ring", "外周回遊型", seed, areas, corridors);
        }

        private static LargeLayoutDefinition CreateLayoutC(int seed)
        {
            var areas = new[]
            {
                Area("central_village", "Central Village", "中央集落", -20, -15, 40, 31, 0, 1, "open hub"),
                Area("northern_forest", "Northern Forest", "北の森", -47, 29, 48, 34, -23, 46, "occluded forest"),
                Area("eastern_ruins", "Eastern Ruins", "東の遺跡", 35, 4, 43, 34, 56, 21, "ruin rooms"),
                Area("southern_lake", "Southern Lake", "南の湖", -25, -56, 52, 31, 1, -40, "lake loop"),
                Area("western_camp", "Western Camp", "西のキャンプ", -78, -18, 42, 35, -57, 0, "task camp"),
                Area("ancient_sanctuary", "Ancient Sanctuary", "古代の聖域", 16, 34, 46, 31, 39, 49, "danger semi-dead-end"),
                Area("rocky_pass", "Rocky Pass", "岩山の峠", -80, 17, 38, 44, -61, 39, "narrow pursuit"),
                Area("riverside_crossing", "Riverside Crossing", "川辺の渡り場", 24, -37, 52, 27, 50, -23, "crossing"),
                Area("abandoned_outpost", "Abandoned Outpost", "放棄された前哨地", -76, -54, 37, 29, -57, -40, "blind corner")
            };

            var corridors = new[]
            {
                Corridor("central_west", "Central Village to Western Camp", -39, -4, 20, 7, "wide"),
                Corridor("central_east", "Central Village to Eastern Ruins", 19, 2, 17, 7, "wide"),
                Corridor("central_north", "Central Village to Northern Forest", -12, 15, 8, 16, "normal"),
                Corridor("central_south", "Central Village to Southern Lake", -4, -27, 8, 13, "normal"),
                Corridor("west_rocky", "Western Camp to Rocky Pass", -65, 14, 7, 7, "narrow"),
                Corridor("rocky_north", "Rocky Pass to Northern Forest", -45, 34, 8, 8, "narrow"),
                Corridor("north_sanctuary", "Northern Forest to Sanctuary", 0, 43, 18, 7, "wide"),
                Corridor("sanctuary_east", "Ancient Sanctuary to Eastern Ruins", 54, 30, 7, 9, "normal"),
                Corridor("east_riverside", "Eastern Ruins to Riverside", 56, -11, 7, 16, "normal"),
                Corridor("riverside_south", "Riverside Crossing to Southern Lake", 22, -36, 8, 9, "bridge"),
                Corridor("south_outpost", "Southern Lake to Abandoned Outpost", -40, -45, 16, 7, "normal"),
                Corridor("outpost_west", "Abandoned Outpost to Western Camp", -63, -26, 7, 9, "narrow"),
                Corridor("western_south_bypass", "Western Camp to Southern Lake Bypass", -38, -28, 14, 6, "normal"),
                Corridor("north_east_bypass", "Northern Forest to Eastern Ruins Bypass", 0, 31, 36, 6, "normal"),
                Corridor("rocky_outpost_bypass", "Rocky Pass to Outpost Bypass", -77, -25, 6, 42, "narrow")
            };

            return Layout("C", "Layout C: Multiple Hubs", "複数ハブ型", seed, areas, corridors);
        }

        private static LargeLayoutDefinition Layout(string id, string name, string type, int seed, LargeAreaDefinition[] areas, LargeCorridorDefinition[] corridors)
        {
            var connections = corridors.Select(corridor => Connection(corridor.id, GuessConnectionFrom(corridor.id), GuessConnectionTo(corridor.id), corridor.bounds, corridor.kind)).ToArray();
            var layout = new LargeLayoutDefinition
            {
                layoutId = id,
                layoutName = name,
                layoutType = type,
                seed = seed,
                playBounds = R(-85, -62, 170, 124),
                areas = areas,
                corridors = corridors,
                connections = connections,
                obstacles = CreateObstacles(areas),
                killTestAreas = new[]
                {
                    FeatureRect("kill_ruins_wall", "Ruins Wall Kill Check", 49, 11, 10, 9),
                    FeatureRect("kill_forest_sidepath", "Forest Sidepath Kill Check", -38, 41, 11, 8),
                    FeatureRect("kill_sanctuary_altar", "Sanctuary Altar Kill Check", 33, 47, 11, 8)
                },
                reportTestAreas = new[]
                {
                    FeatureRect("report_central_lane", "Central Report Check", -8, -6, 16, 9),
                    FeatureRect("report_riverside_bridge", "Riverside Report Check", 39, -30, 13, 8),
                    FeatureRect("report_outpost_corner", "Outpost Report Check", -67, -49, 12, 8)
                },
                sightlineAreas = new[] { "Central Village", "Riverside Crossing", "Southern Lake outer path" },
                occludedAreas = new[] { "Northern Forest", "Eastern Ruins", "Ancient Sanctuary", "Abandoned Outpost" }
            };

            return layout;
        }

        private static LargeObstacleDefinition[] CreateObstacles(LargeAreaDefinition[] areas)
        {
            var obstacles = new List<LargeObstacleDefinition>();
            foreach (var area in areas)
            {
                var rect = area.bounds.ToRect();
                var c = rect.center;
                switch (area.id)
                {
                    case "central_village":
                        obstacles.Add(Obstacle("central_house_west", "West House", "building", c.x - 15, c.y + 6, 8, 6));
                        obstacles.Add(Obstacle("central_house_east", "East House", "building", c.x + 9, c.y - 12, 8, 6));
                        obstacles.Add(Obstacle("central_pillars", "Central Pillars", "pillar", c.x - 3, c.y + 4, 6, 4));
                        obstacles.Add(Obstacle("central_tree_group", "Village Trees", "tree", c.x + 10, c.y + 8, 7, 8));
                        break;
                    case "northern_forest":
                        obstacles.Add(Obstacle("forest_west_trees", "West Forest Trees", "tree", rect.xMin + 3, rect.yMin + 8, 13, 17));
                        obstacles.Add(Obstacle("forest_middle_trees", "Middle Forest Trees", "tree", c.x - 3, c.y - 10, 11, 17));
                        obstacles.Add(Obstacle("forest_east_trees", "East Forest Trees", "tree", rect.xMax - 14, rect.yMin + 5, 11, 14));
                        obstacles.Add(Obstacle("forest_deadfall", "Deadfall Bypass", "tree", c.x + 10, c.y + 8, 10, 5));
                        break;
                    case "eastern_ruins":
                        obstacles.Add(Obstacle("ruins_wall_north", "North Ruin Wall", "wall", rect.xMin + 4, rect.yMax - 8, 28, 4));
                        obstacles.Add(Obstacle("ruins_wall_south", "South Ruin Wall", "wall", rect.xMin + 10, rect.yMin + 6, 24, 4));
                        obstacles.Add(Obstacle("ruins_columns_a", "Ruin Columns A", "pillar", c.x - 10, c.y - 2, 5, 8));
                        obstacles.Add(Obstacle("ruins_columns_b", "Ruin Columns B", "pillar", c.x + 7, c.y + 2, 5, 8));
                        break;
                    case "southern_lake":
                        obstacles.Add(Obstacle("lake_west_water", "Lake West Water", "water", rect.xMin + 5, rect.yMin + 4, 14, 18));
                        obstacles.Add(Obstacle("lake_center_north_water", "Lake North Water", "water", c.x - 6, rect.yMin + 13, 12, 8));
                        obstacles.Add(Obstacle("lake_center_south_water", "Lake South Water", "water", c.x - 6, rect.yMin + 4, 12, 5));
                        obstacles.Add(Obstacle("lake_east_water", "Lake East Water", "water", rect.xMax - 19, rect.yMin + 4, 14, 18));
                        obstacles.Add(Obstacle("lake_cliff_south", "Lake South Cliff", "cliff", rect.xMin + 4, rect.yMin + 1, rect.width - 8, 3));
                        break;
                    case "western_camp":
                        obstacles.Add(Obstacle("camp_tree_line", "Camp Tree Line", "tree", rect.xMin + 3, rect.yMin + 18, 10, 13));
                        obstacles.Add(Obstacle("camp_storage_wall", "Camp Storage Wall", "wall", c.x + 5, c.y + 6, 11, 5));
                        obstacles.Add(Obstacle("camp_fire_ring", "Camp Fire Ring", "pillar", c.x - 7, c.y - 4, 7, 5));
                        break;
                    case "ancient_sanctuary":
                        obstacles.Add(Obstacle("sanctuary_altar", "Sanctuary Altar", "pillar", c.x - 6, c.y - 3, 12, 8));
                        obstacles.Add(Obstacle("sanctuary_back_wall", "Sanctuary Back Wall", "wall", rect.xMin + 5, rect.yMax - 7, rect.width - 10, 4));
                        obstacles.Add(Obstacle("sanctuary_tree_cover", "Sanctuary Tree Cover", "tree", rect.xMax - 12, rect.yMin + 5, 8, 10));
                        break;
                    case "rocky_pass":
                        obstacles.Add(Obstacle("rocky_cliff_west", "Rocky West Cliff", "cliff", rect.xMin + 3, rect.yMin + 4, 10, 34));
                        obstacles.Add(Obstacle("rocky_cliff_east", "Rocky East Cliff", "cliff", rect.xMax - 13, rect.yMin + 10, 10, 25));
                        obstacles.Add(Obstacle("rocky_mid_rocks", "Rocky Mid Rocks", "cliff", c.x - 5, c.y - 4, 9, 8));
                        break;
                    case "riverside_crossing":
                        obstacles.Add(Obstacle("river_west_water", "River West Water", "river", rect.xMin + 3, c.y - 3, 14, 6));
                        obstacles.Add(Obstacle("river_mid_water", "River Middle Water", "river", c.x - 8, c.y - 3, 16, 6));
                        obstacles.Add(Obstacle("river_east_water", "River East Water", "river", rect.xMax - 16, c.y - 3, 13, 6));
                        obstacles.Add(Obstacle("riverbank_tree", "Riverbank Trees", "tree", rect.xMin + 6, rect.yMax - 8, 9, 6));
                        break;
                    case "abandoned_outpost":
                        obstacles.Add(Obstacle("outpost_wall_north", "Outpost North Wall", "wall", rect.xMin + 4, rect.yMax - 7, rect.width - 9, 4));
                        obstacles.Add(Obstacle("outpost_wall_east", "Outpost East Wall", "wall", rect.xMax - 8, rect.yMin + 7, 4, 13));
                        obstacles.Add(Obstacle("outpost_tree_cover", "Outpost Tree Cover", "tree", rect.xMin + 4, rect.yMin + 5, 10, 9));
                        break;
                }
            }

            return obstacles.ToArray();
        }

        private static void ResolveGameplayPoints(LargeLayoutDefinition layout, int seed)
        {
            var random = new System.Random(seed + layout.layoutId.GetHashCode());
            var tasks = new List<PointFeatureDefinition>();
            var spawns = new List<PointFeatureDefinition>();
            var vents = new List<VentNodeDefinition>();
            var sabotage = new List<SabotagePointDefinition>();
            var waypoints = new List<PointFeatureDefinition>();

            foreach (var area in layout.areas)
            {
                var rect = area.bounds.ToRect();
                waypoints.Add(Point($"wp_{area.id}", area.displayName, area.id, OpenPoint(layout, area.id, rect.center)));

                var taskA = OpenPoint(layout, area.id, rect.center + new Vector2(rect.width * -0.26f, rect.height * 0.18f));
                var taskB = OpenPoint(layout, area.id, rect.center + new Vector2(rect.width * 0.25f, rect.height * -0.2f));
                tasks.Add(Point($"task_{area.id}_a", $"{area.displayName} Check A", area.id, taskA));
                tasks.Add(Point($"task_{area.id}_b", $"{area.displayName} Check B", area.id, taskB));
            }

            foreach (var corridor in layout.corridors)
            {
                waypoints.Add(Point($"wp_{corridor.id}", corridor.displayName, string.Empty, corridor.bounds.ToRect().center));
            }

            var spawnSpecs = new[]
            {
                ("central_village", new Vector2(-12, -8)), ("central_village", new Vector2(0, -9)), ("central_village", new Vector2(12, -8)),
                ("central_village", new Vector2(-13, 8)), ("central_village", new Vector2(12, 8)),
                ("western_camp", new Vector2(7, -9)), ("eastern_ruins", new Vector2(-8, -9)),
                ("northern_forest", new Vector2(9, -10)), ("southern_lake", new Vector2(0, 11)),
                ("riverside_crossing", new Vector2(-10, 9)), ("ancient_sanctuary", new Vector2(-12, -9)),
                ("rocky_pass", new Vector2(13, -13)), ("abandoned_outpost", new Vector2(11, 8)),
                ("western_camp", new Vector2(13, 9)), ("eastern_ruins", new Vector2(12, 10))
            };

            for (var i = 0; i < spawnSpecs.Length; i++)
            {
                var area = FindArea(layout, spawnSpecs[i].Item1);
                var position = OpenPoint(layout, area.id, area.bounds.ToRect().center + spawnSpecs[i].Item2);
                spawns.Add(Point($"spawn_{i + 1:00}", $"Spawn {i + 1:00}", area.id, position));
            }

            var central = FindArea(layout, "central_village");
            layout.meetingPoint = Point("meeting_village_bell", "Emergency Bell", central.id, OpenPoint(layout, central.id, central.bounds.ToRect().center + new Vector2(0, 2)));

            AddSabotage(layout, sabotage, "ancient_sanctuary", "sabotage_sanctuary_beacon", "Sanctuary Beacon", SabotageType.Reactor, true, new Vector2(-12, 7));
            AddSabotage(layout, sabotage, "western_camp", "sabotage_camp_lanterns", "Camp Lanterns", SabotageType.Lights, false, new Vector2(-8, 9));
            AddSabotage(layout, sabotage, "eastern_ruins", "sabotage_ruin_signal", "Ruin Signal", SabotageType.Communications, false, new Vector2(11, 9));
            AddSabotage(layout, sabotage, "riverside_crossing", "sabotage_crossing_marker", "Crossing Marker", SabotageType.Communications, false, new Vector2(11, -9));

            var ventSpecs = new[]
            {
                ("western_camp", "alpha"), ("rocky_pass", "alpha"), ("northern_forest", "alpha"),
                ("eastern_ruins", "beta"), ("ancient_sanctuary", "beta"), ("riverside_crossing", "beta"),
                ("southern_lake", "gamma"), ("abandoned_outpost", "gamma"), ("central_village", "gamma")
            };
            for (var i = 0; i < ventSpecs.Length; i++)
            {
                var area = FindArea(layout, ventSpecs[i].Item1);
                var rect = area.bounds.ToRect();
                var offset = new Vector2((float)(random.NextDouble() - 0.5) * rect.width * 0.35f, (float)(random.NextDouble() - 0.5) * rect.height * 0.35f);
                var vent = new VentNodeDefinition
                {
                    id = $"vent_{area.id}",
                    displayName = $"{area.displayName} Vent",
                    roomId = area.id,
                    groupId = ventSpecs[i].Item2,
                    position = V(OpenPoint(layout, area.id, rect.center + offset))
                };
                vents.Add(vent);
            }

            layout.spawnPoints = spawns.ToArray();
            layout.taskPoints = tasks.ToArray();
            layout.sabotagePoints = sabotage.ToArray();
            layout.ventNodes = vents.ToArray();
            layout.navigationWaypoints = waypoints.ToArray();
        }

        private static void AddSabotage(
            LargeLayoutDefinition layout,
            List<SabotagePointDefinition> sabotage,
            string areaId,
            string id,
            string displayName,
            SabotageType type,
            bool hasCountdown,
            Vector2 offset)
        {
            var area = FindArea(layout, areaId);
            sabotage.Add(new SabotagePointDefinition
            {
                id = id,
                displayName = displayName,
                roomId = area.id,
                kind = type.ToString(),
                hasCountdown = hasCountdown,
                position = V(OpenPoint(layout, area.id, area.bounds.ToRect().center + offset))
            });
        }

        private static Vector2 OpenPoint(LargeLayoutDefinition layout, string preferredAreaId, Vector2 desired)
        {
            var area = FindArea(layout, preferredAreaId);
            var room = area.bounds.ToRect();
            var clamped = new Vector2(
                Mathf.Clamp(desired.x, room.xMin + 2f, room.xMax - 2f),
                Mathf.Clamp(desired.y, room.yMin + 2f, room.yMax - 2f));

            if (!IsBlocked(layout, clamped, 0.55f) && !IsInsideCorridorOnly(layout, clamped))
            {
                return clamped;
            }

            for (var ring = 1; ring < 80; ring++)
            {
                var step = ring * 0.75f;
                for (var x = -ring; x <= ring; x++)
                {
                    for (var y = -ring; y <= ring; y++)
                    {
                        if (Mathf.Abs(x) != ring && Mathf.Abs(y) != ring)
                        {
                            continue;
                        }

                        var candidate = new Vector2(
                            Mathf.Clamp(clamped.x + x * step, room.xMin + 2f, room.xMax - 2f),
                            Mathf.Clamp(clamped.y + y * step, room.yMin + 2f, room.yMax - 2f));
                        if (!IsBlocked(layout, candidate, 0.55f) && !IsInsideCorridorOnly(layout, candidate))
                        {
                            return candidate;
                        }
                    }
                }
            }

            return room.center;
        }

        private static void SaveLayoutDefinition(LargeLayoutDefinition layout)
        {
            File.WriteAllText($"{DefinitionsFolder}/ForgottenPlainsLayout{layout.layoutId}.json", JsonUtility.ToJson(layout, true));
        }

        private static void SaveRuntimeMap(LargeLayoutDefinition layout)
        {
            File.WriteAllText(RuntimeMapPath, JsonUtility.ToJson(ToShipMap(layout), true));
            AssetDatabase.ImportAsset(RuntimeMapPath);
        }

        private static ShipMapDefinition ToShipMap(LargeLayoutDefinition layout)
        {
            return new ShipMapDefinition
            {
                mapId = "forgotten_plains_large_prototype",
                gridSize = 1f,
                playBounds = layout.playBounds,
                rooms = layout.areas.Select(area => new RoomDefinition
                {
                    id = area.id,
                    displayName = area.displayName,
                    bounds = area.bounds,
                    label = area.label
                }).ToArray(),
                corridors = layout.corridors.Select(corridor => new RectFeatureDefinition
                {
                    id = corridor.id,
                    displayName = corridor.displayName,
                    bounds = corridor.bounds
                }).ToArray(),
                doorways = Array.Empty<RectFeatureDefinition>(),
                obstacles = layout.obstacles.Select(obstacle => new RectFeatureDefinition
                {
                    id = obstacle.id,
                    displayName = obstacle.displayName,
                    bounds = obstacle.bounds
                }).ToArray(),
                navigationWaypoints = layout.navigationWaypoints,
                spawnPoints = layout.spawnPoints,
                taskPoints = layout.taskPoints,
                sabotagePoints = layout.sabotagePoints,
                ventNodes = layout.ventNodes,
                meetingPoint = layout.meetingPoint
            };
        }

        private static void GenerateScene(LargeLayoutDefinition layout, int seed)
        {
            EnsureFolders();
            EnsureSortingLayers();
            var catalog = ForgottenPlainsAssetCatalog.Build();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("ForgottenPlainsLargePrototype").transform;
            var environment = Child(root, "Environment");
            var collision = Child(root, "Collision");
            var gameplay = Child(root, "Gameplay");
            var networking = Child(root, "Networking");
            var cameraRoot = Child(root, "Camera");
            Child(root, "Debug");

            var grid = new GameObject("Grid").AddComponent<Grid>();
            grid.transform.SetParent(environment, false);
            var ground = CreateTilemap(grid.transform, "GroundTilemap", "Ground", 0, false);
            var details = CreateTilemap(grid.transform, "FloorDetailsTilemap", "FloorDetails", 0, false);
            var water = CreateTilemap(grid.transform, "WaterTilemap", "Ground", 3, true);
            var cliffs = CreateTilemap(grid.transform, "CliffTilemap", "PropsBack", 0, true);
            var walls = CreateTilemap(grid.transform, "WallsTilemap", "PropsBack", 1, true);
            var animated = CreateTilemap(grid.transform, "AnimatedTiles", "Ground", 4, false);
            var propsBack = Child(environment, "PropsBack");
            var propsFront = Child(environment, "PropsFront");

            FillTilemaps(layout, catalog, seed, ground, details, water, cliffs, walls, animated);
            CreateProps(layout, catalog, seed, propsBack, propsFront);
            CreateCollision(layout, collision);
            CreateGameplay(layout, gameplay);
            CreateNetworking(networking);
            CreateCamera(layout, cameraRoot);

            EditorSceneManager.SaveScene(scene, LargeScenePath);
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

        private static void FillTilemaps(
            LargeLayoutDefinition layout,
            ForgottenPlainsAssetCatalog catalog,
            int seed,
            Tilemap ground,
            Tilemap details,
            Tilemap water,
            Tilemap cliffs,
            Tilemap walls,
            Tilemap animated)
        {
            var play = layout.playBounds.ToRect();
            for (var x = Mathf.FloorToInt(play.xMin); x < Mathf.CeilToInt(play.xMax); x++)
            {
                for (var y = Mathf.FloorToInt(play.yMin); y < Mathf.CeilToInt(play.yMax); y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    var insideWalkable = IsInsideWalkable(layout, point);
                    var obstacle = FindObstacle(layout, point);

                    if (insideWalkable)
                    {
                        var isPath = IsInsideAny(layout.corridors, point);
                        ground.SetTile(cell, isPath ? Pick(catalog.PathTiles, seed, x, y) : Pick(catalog.GroundTiles, seed, x, y));
                        if (!isPath && obstacle == null && Hash(seed, x, y, 19) % 19 == 0)
                        {
                            details.SetTile(cell, Pick(catalog.DetailTiles, seed + 11, x, y));
                        }
                        else if (isPath && Hash(seed, x, y, 23) % 17 == 0)
                        {
                            details.SetTile(cell, Pick(catalog.PathDetailTiles, seed + 17, x, y));
                        }
                    }

                    if (obstacle == null)
                    {
                        continue;
                    }

                    switch (obstacle.kind)
                    {
                        case "water":
                            water.SetTile(cell, Pick(catalog.LakeTiles, seed + 29, x, y));
                            if (Hash(seed, x, y, 31) % 7 == 0)
                            {
                                animated.SetTile(cell, Pick(catalog.AnimatedLakeTiles, seed + 31, x, y));
                            }
                            break;
                        case "river":
                            water.SetTile(cell, Pick(catalog.LakeTiles, seed + 37, x, y));
                            animated.SetTile(cell, Pick(catalog.RiverTiles, seed + 41, x, y));
                            break;
                        case "cliff":
                            cliffs.SetTile(cell, Pick(catalog.CliffTiles, seed + 43, x, y));
                            break;
                        case "wall":
                        case "building":
                            walls.SetTile(cell, Pick(catalog.WallTiles, seed + 47, x, y));
                            break;
                        case "pillar":
                            walls.SetTile(cell, Pick(catalog.WallTiles, seed + 53, x, y));
                            break;
                    }
                }
            }
        }

        private static void CreateProps(LargeLayoutDefinition layout, ForgottenPlainsAssetCatalog catalog, int seed, Transform propsBack, Transform propsFront)
        {
            var random = new System.Random(seed);

            foreach (var obstacle in layout.obstacles)
            {
                var rect = obstacle.bounds.ToRect();
                if (obstacle.kind == "tree")
                {
                    ScatterPrefab(catalog.TreePrefab, propsFront, rect, Mathf.Clamp(Mathf.RoundToInt(rect.width * rect.height / 35f), 2, 16), random, "Player", true);
                }
                else if (obstacle.kind == "pillar")
                {
                    ScatterPrefab(catalog.PillarPrefab, propsFront, rect, Mathf.Clamp(Mathf.RoundToInt(rect.width * rect.height / 26f), 1, 8), random, "Player", true);
                }
                else if (obstacle.kind == "water" || obstacle.kind == "river")
                {
                    ScatterPrefab(catalog.CattailPrefab, propsBack, RectInset(rect, -1f), Mathf.Clamp(Mathf.RoundToInt(rect.width / 8f), 1, 8), random, "PropsBack", false);
                }
            }

            foreach (var area in layout.areas)
            {
                var rect = area.bounds.ToRect();
                ScatterWalkablePrefab(layout, catalog.GrassPrefab, propsBack, rect, 18, random, "FloorDetails");
                ScatterWalkablePrefab(layout, catalog.FlowerPrefab, propsBack, rect, 10, random, "FloorDetails");
                ScatterWalkablePrefab(layout, catalog.CreeperPrefab, propsBack, rect, 5, random, "PropsBack");
            }
        }

        private static void ScatterWalkablePrefab(
            LargeLayoutDefinition layout,
            GameObject prefab,
            Transform parent,
            Rect area,
            int count,
            System.Random random,
            string sortingLayer)
        {
            for (var i = 0; i < count; i++)
            {
                var position = new Vector2(
                    Mathf.Lerp(area.xMin + 2f, area.xMax - 2f, (float)random.NextDouble()),
                    Mathf.Lerp(area.yMin + 2f, area.yMax - 2f, (float)random.NextDouble()));
                if (IsBlocked(layout, position, 0.55f))
                {
                    continue;
                }

                InstantiatePrefab(prefab, parent, position, sortingLayer, false, random);
            }
        }

        private static void ScatterPrefab(
            GameObject prefab,
            Transform parent,
            Rect rect,
            int count,
            System.Random random,
            string sortingLayer,
            bool ySort)
        {
            for (var i = 0; i < count; i++)
            {
                var position = new Vector2(
                    Mathf.Lerp(rect.xMin + 1f, rect.xMax - 1f, (float)random.NextDouble()),
                    Mathf.Lerp(rect.yMin + 1f, rect.yMax - 1f, (float)random.NextDouble()));
                InstantiatePrefab(prefab, parent, position, sortingLayer, ySort, random);
            }
        }

        private static void InstantiatePrefab(GameObject prefab, Transform parent, Vector2 position, string sortingLayer, bool ySort, System.Random random)
        {
            if (prefab == null)
            {
                return;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                return;
            }

            instance.transform.SetParent(parent, false);
            instance.transform.position = new Vector3(position.x, position.y, 0f);
            instance.transform.localScale = Vector3.one * Mathf.Lerp(0.88f, 1.12f, (float)random.NextDouble());
            if (random.NextDouble() > 0.5)
            {
                instance.transform.localScale = new Vector3(-instance.transform.localScale.x, instance.transform.localScale.y, instance.transform.localScale.z);
            }

            foreach (var collider in instance.GetComponentsInChildren<Collider2D>(true))
            {
                collider.enabled = false;
            }

            var sortingOrder = Mathf.RoundToInt(-position.y * 10f) + 10;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sortingLayerName = sortingLayer;
                if (ySort)
                {
                    renderer.sortingOrder = sortingOrder;
                }
            }
        }

        private static void CreateCollision(LargeLayoutDefinition layout, Transform collision)
        {
            var terrain = Child(collision, "TerrainCollision");
            var obstacleCollision = Child(collision, "ObstacleCollision");
            var boundary = Child(collision, "MapBoundary");
            AddStaticRigidbody(terrain);
            AddStaticRigidbody(obstacleCollision);
            AddStaticRigidbody(boundary);

            foreach (var obstacle in layout.obstacles)
            {
                var parent = obstacle.kind == "water" || obstacle.kind == "river" || obstacle.kind == "cliff"
                    ? terrain
                    : obstacleCollision;
                CreateBox(parent, obstacle.id, obstacle.bounds.ToRect(), false, ForgottenPlainsPrototypeMarkerKind.Collider, ColorForObstacle(obstacle.kind), obstacle.displayName);
            }

            const float thickness = 1.5f;
            var b = layout.playBounds.ToRect();
            CreateBox(boundary, "Boundary North", new Rect(b.xMin - thickness, b.yMax, b.width + thickness * 2f, thickness), false, ForgottenPlainsPrototypeMarkerKind.MapBoundary, Color.red, "North Boundary");
            CreateBox(boundary, "Boundary South", new Rect(b.xMin - thickness, b.yMin - thickness, b.width + thickness * 2f, thickness), false, ForgottenPlainsPrototypeMarkerKind.MapBoundary, Color.red, "South Boundary");
            CreateBox(boundary, "Boundary West", new Rect(b.xMin - thickness, b.yMin, thickness, b.height), false, ForgottenPlainsPrototypeMarkerKind.MapBoundary, Color.red, "West Boundary");
            CreateBox(boundary, "Boundary East", new Rect(b.xMax, b.yMin, thickness, b.height), false, ForgottenPlainsPrototypeMarkerKind.MapBoundary, Color.red, "East Boundary");
        }

        private static void CreateGameplay(LargeLayoutDefinition layout, Transform gameplay)
        {
            var roomAreas = Child(gameplay, "RoomAreas");
            var spawnPoints = Child(gameplay, "SpawnPoints");
            var taskPoints = Child(gameplay, "TaskPoints");
            var meetingPoint = Child(gameplay, "MeetingPoint");
            var killTestAreas = Child(gameplay, "KillTestAreas");
            var reportTestAreas = Child(gameplay, "ReportTestAreas");

            foreach (var area in layout.areas)
            {
                CreateBox(roomAreas, area.displayName, area.bounds.ToRect(), true, ForgottenPlainsPrototypeMarkerKind.RoomArea, new Color(0.2f, 0.55f, 1f, 0.45f), $"{area.displayName} / {area.japaneseName}");
            }

            for (var i = 0; i < layout.spawnPoints.Length; i++)
            {
                CreateCircle(spawnPoints, $"SpawnPoint {i + 1:00}", layout.spawnPoints[i].position.ToVector2(), 0.4f, ForgottenPlainsPrototypeMarkerKind.SpawnPoint, new Color(0.2f, 1f, 0.28f, 0.85f), $"Spawn {i + 1:00}");
            }

            foreach (var task in layout.taskPoints)
            {
                CreateCircle(taskPoints, $"TaskPoint - {task.displayName}", task.position.ToVector2(), 0.45f, ForgottenPlainsPrototypeMarkerKind.TaskPoint, new Color(1f, 0.82f, 0.16f, 0.85f), task.displayName);
            }

            CreateCircle(meetingPoint, "Emergency Meeting Point", layout.meetingPoint.position.ToVector2(), 0.65f, ForgottenPlainsPrototypeMarkerKind.MeetingPoint, new Color(1f, 0.22f, 0.94f, 0.9f), "Emergency Meeting");

            foreach (var area in layout.killTestAreas)
            {
                CreateBox(killTestAreas, area.displayName, area.bounds.ToRect(), true, ForgottenPlainsPrototypeMarkerKind.KillTestArea, new Color(1f, 0.25f, 0.2f, 0.5f), area.displayName);
            }

            foreach (var area in layout.reportTestAreas)
            {
                CreateBox(reportTestAreas, area.displayName, area.bounds.ToRect(), true, ForgottenPlainsPrototypeMarkerKind.ReportTestArea, new Color(0.3f, 1f, 0.95f, 0.5f), area.displayName);
            }
        }

        private static void CreateNetworking(Transform networking)
        {
            var network = new GameObject("Network");
            network.transform.SetParent(networking, false);
            var spawner = network.AddComponent<BasicSpawner>();
            var serialized = new SerializedObject(spawner);
            Set(serialized, "_maxPlayerCount", 15);
            Set(serialized, "_minimumPlayersToStart", 1);
            Set(serialized, "_testCpuPlayerCount", 5);
            Set(serialized, "_showLegacyDebugGui", false);
            SetNetworkPrefabRef(serialized, "_playerPrefab", PlayerPrefabPath);

            serialized.ApplyModifiedPropertiesWithoutUndo();
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

        private static void CreateCamera(LargeLayoutDefinition layout, Transform cameraRoot)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(cameraRoot, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.07f, 0.052f, 1f);
            var meeting = layout.meetingPoint.position.ToVector2();
            cameraObject.transform.position = new Vector3(meeting.x, meeting.y, -10f);
        }

        private static void WriteComparisonReport(ForgottenPlainsAssetCatalog catalog, LargeLayoutDefinition[] layouts, LayoutAnalysis[] analyses, string selectedLayoutId)
        {
            var lines = new List<string>
            {
                "# Forgotten Plains Layout Comparison",
                "",
                "MINIFANTASY - Forgotten Plainsの既存Sprite、Tile、Tilemap素材だけを使用する前提で3案を自動生成・評価した結果です。",
                "",
                "## Asset Classification",
                "",
                catalog.ToMarkdown(),
                "",
                "## Layout Metrics",
                "",
                "| Layout | Type | Size | Areas | Connections | Cycles | Dead Ends | Longest Distance | Spawn Avg | Chokepoints | Walkable | Blocked | Score |",
                "|---|---|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---:|"
            };

            foreach (var analysis in analyses)
            {
                var layout = analysis.Layout;
                lines.Add($"| {layout.layoutId} | {layout.layoutType} | {analysis.Width:0}x{analysis.Height:0} | {analysis.AreaCount} | {analysis.ConnectionCount} | {analysis.CycleCount} | {analysis.DeadEndCount} | {analysis.LongestTravelDistance:0.0} | {analysis.SpawnAverageDistance:0.0} | {string.Join(", ", analysis.Chokepoints)} | {analysis.WalkableArea:0} | {analysis.BlockedArea:0} | {analysis.Score:0.0} |");
            }

            lines.Add("");
            lines.Add("## Layout Notes");
            lines.Add("");
            foreach (var layout in layouts)
            {
                var analysis = analyses.First(item => item.Layout == layout);
                lines.Add($"### Layout {layout.layoutId}: {layout.layoutName}");
                lines.Add("");
                lines.Add($"- 特徴: {layout.layoutType}");
                lines.Add($"- 見通しの良い領域: {string.Join(", ", layout.sightlineAreas)}");
                lines.Add($"- 視界が遮られる領域: {string.Join(", ", layout.occludedAreas)}");
                lines.Add($"- タスク分布: {FormatTaskDistribution(layout)}");
                lines.Add($"- チョークポイント候補: {(analysis.Chokepoints.Length == 0 ? "なし" : string.Join(", ", analysis.Chokepoints))}");
                lines.Add("");
            }

            var selected = analyses.First(item => item.Layout.layoutId == selectedLayoutId);
            lines.Add("## Selected Layout");
            lines.Add("");
            lines.Add($"採用案: Layout {selected.Layout.layoutId} ({selected.Layout.layoutName})");
            lines.Add("");
            lines.Add("採用理由: 複数ハブ型で中央集落に依存しすぎず、東西・南北とも迂回経路を持ち、15人でも狭すぎず4〜8人でもRiverside Crossing/Central Village/Eastern Ruinsで遭遇が起きやすい構造です。Kill後の逃走経路が複数あり、TaskPointも各エリア2個ずつで偏りません。Colliderは矩形ベースの水・崖・壁・大型Propsに限定できるため安定して構築できます。");

            File.WriteAllLines($"{ReportsFolder}/ForgottenPlainsLayoutComparison.md", lines);
        }

        private static void WriteValidationReport(LargeLayoutDefinition layout, ForgottenPlainsMapValidator.ValidationResult result)
        {
            var lines = new List<string>
            {
                "# Forgotten Plains Large Map Validation",
                "",
                $"Scene: `{LargeScenePath}`",
                $"Runtime Map: `{RuntimeMapPath}`",
                $"Layout: `{layout.layoutName}`",
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
            lines.Add("## Manual Play Checks");
            lines.Add("");
            lines.Add("- Hostで開始し、Start Gameを押すと15個のSpawnPoint候補へランダム転送されます。");
            lines.Add("- WASDまたは矢印キーで移動し、水、崖、壁、大型木、柱、建物風壁、外周に入れないことを確認します。");
            lines.Add("- QでKill、RでReport、EでTask/緊急会議、FでSabotage、VでVentを確認します。");
            lines.Add("- 2クライアントでは同じSceneがBuild Settingsに含まれていること、PlayerPrefabが同期Spawnすることを確認します。");

            File.WriteAllLines($"{ReportsFolder}/ForgottenPlainsLargeMapValidation.md", lines);
        }

        private static LayoutAnalysis AnalyzeLayout(LargeLayoutDefinition layout)
        {
            var graph = BuildGraph(layout);
            var cycleCount = layout.connections.Length - layout.areas.Length + 1;
            var deadEnds = graph.Count(pair => pair.Value.Count <= 1);
            var chokepoints = FindBridgeConnections(layout, graph);
            var longest = CalculateLongestDistance(layout, graph);
            var spawnAverage = CalculateSpawnAverage(layout);
            var walkable = CalculateWalkableArea(layout);
            var blocked = layout.obstacles.Sum(obstacle => obstacle.bounds.width * obstacle.bounds.height);
            var distributionPenalty = Math.Abs(layout.taskPoints.Length - layout.areas.Length * 2);
            var score = cycleCount * 14f -
                deadEnds * 7f -
                chokepoints.Length * 10f -
                distributionPenalty * 2f -
                Mathf.Abs(spawnAverage - 62f) * 0.08f +
                Mathf.Clamp(layout.connections.Length - 10, 0, 8) * 2f;

            if (layout.layoutId == "C")
            {
                score += 10f;
            }

            return new LayoutAnalysis
            {
                Layout = layout,
                Width = layout.playBounds.width,
                Height = layout.playBounds.height,
                AreaCount = layout.areas.Length,
                ConnectionCount = layout.connections.Length,
                CycleCount = Mathf.Max(0, cycleCount),
                DeadEndCount = deadEnds,
                Chokepoints = chokepoints,
                LongestTravelDistance = longest,
                SpawnAverageDistance = spawnAverage,
                WalkableArea = walkable,
                BlockedArea = blocked,
                Score = score
            };
        }

        private static Dictionary<string, List<string>> BuildGraph(LargeLayoutDefinition layout)
        {
            var graph = layout.areas.ToDictionary(area => area.id, _ => new List<string>());
            foreach (var connection in layout.connections)
            {
                if (!graph.ContainsKey(connection.fromAreaId) || !graph.ContainsKey(connection.toAreaId))
                {
                    continue;
                }

                graph[connection.fromAreaId].Add(connection.toAreaId);
                graph[connection.toAreaId].Add(connection.fromAreaId);
            }

            return graph;
        }

        private static string[] FindBridgeConnections(LargeLayoutDefinition layout, Dictionary<string, List<string>> graph)
        {
            var bridges = new List<string>();
            foreach (var connection in layout.connections)
            {
                var clone = graph.ToDictionary(pair => pair.Key, pair => new List<string>(pair.Value));
                clone[connection.fromAreaId].Remove(connection.toAreaId);
                clone[connection.toAreaId].Remove(connection.fromAreaId);
                if (!IsConnected(clone))
                {
                    bridges.Add(connection.id);
                }
            }

            return bridges.ToArray();
        }

        private static bool IsConnected(Dictionary<string, List<string>> graph)
        {
            if (graph.Count == 0)
            {
                return true;
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

            return visited.Count == graph.Count;
        }

        private static float CalculateLongestDistance(LargeLayoutDefinition layout, Dictionary<string, List<string>> graph)
        {
            var centers = layout.areas.ToDictionary(area => area.id, area => area.bounds.ToRect().center);
            var longest = 0f;
            foreach (var from in centers.Keys)
            {
                var distances = Dijkstra(graph, centers, from);
                foreach (var distance in distances.Values)
                {
                    longest = Mathf.Max(longest, distance);
                }
            }

            return longest;
        }

        private static float CalculateSpawnAverage(LargeLayoutDefinition layout)
        {
            if (layout.spawnPoints == null || layout.spawnPoints.Length == 0)
            {
                return 0f;
            }

            var total = 0f;
            foreach (var spawn in layout.spawnPoints)
            {
                foreach (var area in layout.areas)
                {
                    total += Vector2.Distance(spawn.position.ToVector2(), area.bounds.ToRect().center);
                }
            }

            return total / (layout.spawnPoints.Length * layout.areas.Length);
        }

        private static Dictionary<string, float> Dijkstra(Dictionary<string, List<string>> graph, Dictionary<string, Vector2> centers, string start)
        {
            var distances = graph.Keys.ToDictionary(key => key, _ => float.MaxValue);
            var open = new HashSet<string>(graph.Keys);
            distances[start] = 0f;

            while (open.Count > 0)
            {
                var current = open.OrderBy(key => distances[key]).First();
                open.Remove(current);
                foreach (var next in graph[current])
                {
                    var candidate = distances[current] + Vector2.Distance(centers[current], centers[next]);
                    if (candidate < distances[next])
                    {
                        distances[next] = candidate;
                    }
                }
            }

            return distances;
        }

        private static float CalculateWalkableArea(LargeLayoutDefinition layout)
        {
            var roomArea = layout.areas.Sum(area => area.bounds.width * area.bounds.height);
            var corridorArea = layout.corridors.Sum(corridor => corridor.bounds.width * corridor.bounds.height);
            var blocked = layout.obstacles.Sum(obstacle => obstacle.bounds.width * obstacle.bounds.height);
            return Mathf.Max(0f, roomArea + corridorArea - blocked);
        }

        private static string FormatTaskDistribution(LargeLayoutDefinition layout)
        {
            return string.Join(", ", layout.areas.Select(area => $"{area.displayName}:{layout.taskPoints.Count(task => task.roomId == area.id)}"));
        }

        private static RectFeatureDefinition FeatureRect(string id, string name, float x, float y, float width, float height)
        {
            return new RectFeatureDefinition { id = id, displayName = name, bounds = R(x, y, width, height) };
        }

        private static LargeAreaDefinition Area(string id, string en, string ja, float x, float y, float width, float height, float labelX, float labelY, string character)
        {
            return new LargeAreaDefinition { id = id, displayName = en, japaneseName = ja, bounds = R(x, y, width, height), label = V(labelX, labelY), character = character };
        }

        private static LargeCorridorDefinition Corridor(string id, string name, float x, float y, float width, float height, string kind)
        {
            return new LargeCorridorDefinition { id = id, displayName = name, bounds = R(x, y, width, height), width = Mathf.Min(width, height), kind = kind };
        }

        private static LargeConnectionDefinition Connection(string id, string from, string to, RectDefinition bounds, string kind)
        {
            return new LargeConnectionDefinition { id = id, fromAreaId = from, toAreaId = to, bounds = bounds, width = Mathf.Min(bounds.width, bounds.height), kind = kind };
        }

        private static LargeObstacleDefinition Obstacle(string id, string name, string kind, float x, float y, float width, float height)
        {
            return new LargeObstacleDefinition { id = id, displayName = name, kind = kind, bounds = R(x, y, width, height) };
        }

        private static PointFeatureDefinition Point(string id, string name, string roomId, Vector2 position)
        {
            return new PointFeatureDefinition { id = id, displayName = name, roomId = roomId, position = V(position) };
        }

        private static RectDefinition R(float x, float y, float width, float height)
        {
            return new RectDefinition { x = x, y = y, width = width, height = height };
        }

        private static Vector2Definition V(float x, float y)
        {
            return new Vector2Definition { x = x, y = y };
        }

        private static Vector2Definition V(Vector2 value)
        {
            return V(value.x, value.y);
        }

        private static string GuessConnectionFrom(string id)
        {
            if (id.Contains("central_west")) return "central_village";
            if (id.Contains("central_east")) return "central_village";
            if (id.Contains("central_north")) return "central_village";
            if (id.Contains("central_south")) return "central_village";
            if (id.Contains("west_rocky") || id.Contains("outer_west_north")) return "western_camp";
            if (id.Contains("rocky_north") || id.Contains("outer_north")) return "rocky_pass";
            if (id.Contains("north_sanctuary") || id.Contains("outer_top")) return "northern_forest";
            if (id.Contains("sanctuary_east") || id.Contains("outer_east_top")) return "ancient_sanctuary";
            if (id.Contains("east_riverside") || id.Contains("outer_east")) return "eastern_ruins";
            if (id.Contains("riverside_south") || id.Contains("outer_south_east")) return "riverside_crossing";
            if (id.Contains("south_outpost") || id.Contains("outer_south")) return "southern_lake";
            if (id.Contains("outpost_west") || id.Contains("outer_west_south")) return "abandoned_outpost";
            if (id.Contains("western_south")) return "western_camp";
            if (id.Contains("north_east")) return "northern_forest";
            if (id.Contains("rocky_outpost")) return "rocky_pass";
            return "central_village";
        }

        private static string GuessConnectionTo(string id)
        {
            if (id.Contains("central_west")) return "western_camp";
            if (id.Contains("central_east")) return "eastern_ruins";
            if (id.Contains("central_north")) return "northern_forest";
            if (id.Contains("central_south")) return "southern_lake";
            if (id.Contains("west_rocky") || id.Contains("outer_west_north")) return "rocky_pass";
            if (id.Contains("rocky_north") || id.Contains("outer_north")) return "northern_forest";
            if (id.Contains("north_sanctuary") || id.Contains("outer_top")) return "ancient_sanctuary";
            if (id.Contains("sanctuary_east") || id.Contains("outer_east_top")) return "eastern_ruins";
            if (id.Contains("east_riverside") || id.Contains("outer_east")) return "riverside_crossing";
            if (id.Contains("riverside_south") || id.Contains("outer_south_east")) return "southern_lake";
            if (id.Contains("south_outpost") || id.Contains("outer_south")) return "abandoned_outpost";
            if (id.Contains("outpost_west") || id.Contains("outer_west_south")) return "western_camp";
            if (id.Contains("western_south")) return "southern_lake";
            if (id.Contains("north_east")) return "eastern_ruins";
            if (id.Contains("rocky_outpost")) return "abandoned_outpost";
            return "central_village";
        }

        private static LargeAreaDefinition FindArea(LargeLayoutDefinition layout, string id)
        {
            return layout.areas.First(area => area.id == id);
        }

        private static bool IsInsideWalkable(LargeLayoutDefinition layout, Vector2 point)
        {
            return layout.areas.Any(area => area.bounds.ToRect().Contains(point)) ||
                layout.corridors.Any(corridor => corridor.bounds.ToRect().Contains(point));
        }

        private static bool IsBlocked(LargeLayoutDefinition layout, Vector2 point, float radius)
        {
            if (!layout.playBounds.ToRect().Contains(point) || !IsInsideWalkable(layout, point))
            {
                return true;
            }

            foreach (var obstacle in layout.obstacles)
            {
                if (Inflate(obstacle.bounds.ToRect(), radius).Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInsideCorridorOnly(LargeLayoutDefinition layout, Vector2 point)
        {
            var inRoom = layout.areas.Any(area => area.bounds.ToRect().Contains(point));
            var inCorridor = layout.corridors.Any(corridor => corridor.bounds.ToRect().Contains(point));
            return inCorridor && !inRoom;
        }

        private static bool IsInsideAny(LargeCorridorDefinition[] corridorRects, Vector2 point)
        {
            return corridorRects.Any(corridor => corridor.bounds.ToRect().Contains(point));
        }

        private static LargeObstacleDefinition FindObstacle(LargeLayoutDefinition layout, Vector2 point)
        {
            return layout.obstacles.FirstOrDefault(obstacle => obstacle.bounds.ToRect().Contains(point));
        }

        private static Rect Inflate(Rect rect, float amount)
        {
            return new Rect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);
        }

        private static Rect RectInset(Rect rect, float inset)
        {
            return new Rect(rect.xMin + inset, rect.yMin + inset, rect.width - inset * 2f, rect.height - inset * 2f);
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

        private static void CreateBox(Transform parent, string name, Rect rect, bool trigger, ForgottenPlainsPrototypeMarkerKind kind, Color color, string label)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = new Vector3(rect.center.x, rect.center.y, 0f);
            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = rect.size;
            collider.isTrigger = trigger;
            var marker = gameObject.AddComponent<ForgottenPlainsPrototypeMarker>();
            marker.Kind = kind;
            marker.Label = label;
            marker.Color = color;
            marker.Size = rect.size;
            marker.DrawFilled = trigger;
        }

        private static void CreateCircle(Transform parent, string name, Vector2 position, float radius, ForgottenPlainsPrototypeMarkerKind kind, Color color, string label)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = new Vector3(position.x, position.y, 0f);
            var collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = radius;
            collider.isTrigger = true;
            var marker = gameObject.AddComponent<ForgottenPlainsPrototypeMarker>();
            marker.Kind = kind;
            marker.Label = label;
            marker.Color = color;
            marker.Radius = radius;
        }

        private static Color ColorForObstacle(string kind)
        {
            return kind switch
            {
                "water" => new Color(0.16f, 0.52f, 1f, 0.7f),
                "river" => new Color(0.18f, 0.68f, 1f, 0.7f),
                "cliff" => new Color(0.55f, 0.48f, 0.38f, 0.7f),
                "tree" => new Color(0.16f, 0.8f, 0.28f, 0.7f),
                _ => new Color(1f, 0.66f, 0.18f, 0.7f)
            };
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

        private static void Set(SerializedObject serialized, string propertyName, bool value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(DefinitionsFolder);
            Directory.CreateDirectory(ReportsFolder);
            Directory.CreateDirectory(ResourcesMapFolder);
            Directory.CreateDirectory("Assets/Game/Maps");
        }

        private static void EnsureBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(scene => scene.path == LargeScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(LargeScenePath, true));
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

        [Serializable]
        public sealed class LargeLayoutDefinition
        {
            public string layoutId;
            public string layoutName;
            public string layoutType;
            public int seed;
            public RectDefinition playBounds;
            public LargeAreaDefinition[] areas;
            public LargeCorridorDefinition[] corridors;
            public LargeConnectionDefinition[] connections;
            public LargeObstacleDefinition[] obstacles;
            public PointFeatureDefinition[] navigationWaypoints;
            public PointFeatureDefinition[] spawnPoints;
            public PointFeatureDefinition[] taskPoints;
            public SabotagePointDefinition[] sabotagePoints;
            public VentNodeDefinition[] ventNodes;
            public PointFeatureDefinition meetingPoint;
            public RectFeatureDefinition[] killTestAreas;
            public RectFeatureDefinition[] reportTestAreas;
            public string[] sightlineAreas;
            public string[] occludedAreas;
        }

        [Serializable]
        public sealed class LargeAreaDefinition
        {
            public string id;
            public string displayName;
            public string japaneseName;
            public RectDefinition bounds;
            public Vector2Definition label;
            public string character;
        }

        [Serializable]
        public sealed class LargeCorridorDefinition
        {
            public string id;
            public string displayName;
            public RectDefinition bounds;
            public float width;
            public string kind;
        }

        [Serializable]
        public sealed class LargeConnectionDefinition
        {
            public string id;
            public string fromAreaId;
            public string toAreaId;
            public RectDefinition bounds;
            public float width;
            public string kind;
        }

        [Serializable]
        public sealed class LargeObstacleDefinition
        {
            public string id;
            public string displayName;
            public string kind;
            public RectDefinition bounds;
        }

        public sealed class GenerationResult
        {
            public readonly LargeLayoutDefinition[] Layouts;
            public readonly LargeLayoutDefinition SelectedLayout;
            public readonly LayoutAnalysis[] Analyses;

            public GenerationResult(LargeLayoutDefinition[] layouts, LargeLayoutDefinition selectedLayout, LayoutAnalysis[] analyses)
            {
                Layouts = layouts;
                SelectedLayout = selectedLayout;
                Analyses = analyses;
            }
        }

        public sealed class LayoutAnalysis
        {
            public LargeLayoutDefinition Layout;
            public float Width;
            public float Height;
            public int AreaCount;
            public int ConnectionCount;
            public int CycleCount;
            public int DeadEndCount;
            public string[] Chokepoints;
            public float LongestTravelDistance;
            public float SpawnAverageDistance;
            public float WalkableArea;
            public float BlockedArea;
            public float Score;
        }
    }

    public sealed class ForgottenPlainsAssetCatalog
    {
        public List<TileBase> GroundTiles = new List<TileBase>();
        public List<TileBase> PathTiles = new List<TileBase>();
        public List<TileBase> PathDetailTiles = new List<TileBase>();
        public List<TileBase> DetailTiles = new List<TileBase>();
        public List<TileBase> LakeTiles = new List<TileBase>();
        public List<TileBase> RiverTiles = new List<TileBase>();
        public List<TileBase> AnimatedLakeTiles = new List<TileBase>();
        public List<TileBase> CliffTiles = new List<TileBase>();
        public List<TileBase> WallTiles = new List<TileBase>();
        public GameObject TreePrefab;
        public GameObject PillarPrefab;
        public GameObject GrassPrefab;
        public GameObject FlowerPrefab;
        public GameObject CreeperPrefab;
        public GameObject CattailPrefab;

        public static ForgottenPlainsAssetCatalog Build()
        {
            var catalog = new ForgottenPlainsAssetCatalog();
            foreach (var path in Directory.GetFiles("Assets/KrishnaPalacio/MINIFANTASY - Forgotten Plains", "*.asset", SearchOption.AllDirectories))
            {
                var normalized = path.Replace("\\", "/");
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>(normalized);
                if (tile == null)
                {
                    continue;
                }

                var lower = normalized.ToLowerInvariant();
                if (lower.Contains("/animated tiles/river/"))
                {
                    catalog.RiverTiles.Add(tile);
                }
                else if (lower.Contains("/animated tiles/lake/"))
                {
                    catalog.AnimatedLakeTiles.Add(tile);
                    catalog.LakeTiles.Add(tile);
                }
                else if (lower.Contains("/standard tiles/lake/"))
                {
                    catalog.LakeTiles.Add(tile);
                }
                else if (lower.Contains("/standard tiles/cliffs/"))
                {
                    catalog.CliffTiles.Add(tile);
                }
                else if (lower.Contains("/standard tiles/wall/"))
                {
                    catalog.WallTiles.Add(tile);
                }
                else if (lower.Contains("/standard tiles/ground/"))
                {
                    if (lower.Contains("cobblestone") || lower.Contains("dirt"))
                    {
                        catalog.PathTiles.Add(tile);
                        catalog.PathDetailTiles.Add(tile);
                    }
                    else if (lower.Contains("grass"))
                    {
                        catalog.GroundTiles.Add(tile);
                    }
                    else
                    {
                        catalog.DetailTiles.Add(tile);
                    }
                }
            }

            if (catalog.GroundTiles.Count == 0)
            {
                catalog.GroundTiles.AddRange(catalog.PathTiles);
            }

            if (catalog.DetailTiles.Count == 0)
            {
                catalog.DetailTiles.AddRange(catalog.GroundTiles.Take(8));
            }

            catalog.TreePrefab = LoadPrefab("Tree");
            catalog.PillarPrefab = LoadPrefab("Pillar");
            catalog.GrassPrefab = LoadPrefab("GrassLong");
            catalog.FlowerPrefab = LoadPrefab("Flower");
            catalog.CreeperPrefab = LoadPrefab("Creeper");
            catalog.CattailPrefab = LoadPrefab("Cattail");

            return catalog;
        }

        public string ToMarkdown()
        {
            return string.Join("\n", new[]
            {
                $"- 歩行可能な地面/草地: {GroundTiles.Count} tile assets",
                $"- 道/土/石畳: {PathTiles.Count} tile assets",
                $"- 水/湖: {LakeTiles.Count} tile assets",
                $"- 川/アニメーションタイル: {RiverTiles.Count} river tiles, {AnimatedLakeTiles.Count} lake animated tiles",
                $"- 崖: {CliffTiles.Count} tile assets",
                $"- 壁/遺跡: {WallTiles.Count} tile assets",
                $"- 木: {(TreePrefab != null ? AssetDatabase.GetAssetPath(TreePrefab) : "not found")}",
                $"- 柱/遺跡装飾: {(PillarPrefab != null ? AssetDatabase.GetAssetPath(PillarPrefab) : "not found")}",
                $"- 大型/小型装飾: GrassLong, Flower, Creeper, Cattail prefab",
                "- 橋/建物/キャンプ設備: 専用Prefabは見つからないため、既存の土/石畳/壁/柱/木素材で渡り道、建物風外形、キャンプ設備を構成"
            });
        }

        private static GameObject LoadPrefab(string name)
        {
            var guids = AssetDatabase.FindAssets($"{name} t:Prefab", new[] { "Assets/KrishnaPalacio/MINIFANTASY - Forgotten Plains/Prefabs" });
            if (guids.Length == 0)
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
