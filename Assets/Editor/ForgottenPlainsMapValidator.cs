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
    public static class ForgottenPlainsMapValidator
    {
        private const string AssetRoot = "Assets/KrishnaPalacio/MINIFANTASY - Forgotten Plains";
        private const string LargeScenePath = "Assets/Game/Maps/ForgottenPlainsLargePrototype.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerPrefab.prefab";

        [MenuItem("Tools/Forgotten Plains/Validate Large Prototype")]
        public static void ValidateLargePrototypeMenu()
        {
            ForgottenPlainsMapGenerator.ValidateLargePrototypeBatch();
        }

        public static ValidationResult Validate(ForgottenPlainsMapGenerator.LargeLayoutDefinition layout, string scenePath)
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
            checks.Add(Check("All areas reachable", IsConnected(graph), $"{CountReachable(graph)}/{layout.areas.Length} areas reachable"));
            checks.Add(Check("Loop routes >= 3", layout.connections.Length - layout.areas.Length + 1 >= 3, $"cycles={layout.connections.Length - layout.areas.Length + 1}"));
            checks.Add(Check("Dead-end areas <= 2", graph.Count(pair => pair.Value.Count <= 1) <= 2, $"deadEnds={graph.Count(pair => pair.Value.Count <= 1)}"));
            checks.Add(Check("Single blockage does not split map", FindBridges(layout, graph).Length == 0, $"bridgeEdges={string.Join(", ", FindBridges(layout, graph))}"));

            checks.Add(CheckPoints("SpawnPoint", layout.spawnPoints, layout, 15));
            checks.Add(CheckPoints("TaskPoint", layout.taskPoints, layout, 18));
            checks.Add(Check("MeetingPoint count", layout.meetingPoint != null && !IsBlocked(layout, layout.meetingPoint.position.ToVector2(), 0.5f), layout.meetingPoint?.displayName ?? "missing"));
            checks.Add(Check("RoomArea count", layout.areas.Length == 9, $"rooms={layout.areas.Length}"));
            checks.Add(Check("Kill test areas", layout.killTestAreas != null && layout.killTestAreas.Length == 3, $"killAreas={layout.killTestAreas?.Length ?? 0}"));
            checks.Add(Check("Report test areas", layout.reportTestAreas != null && layout.reportTestAreas.Length == 3, $"reportAreas={layout.reportTestAreas?.Length ?? 0}"));

            checks.Add(Check("Every spawn reaches meeting", PointsReachMeeting(layout.spawnPoints, layout), "layout graph connected and spawn points are open"));
            checks.Add(Check("Every task reachable", PointsReachMeeting(layout.taskPoints, layout), "layout graph connected and task points are open"));
            checks.Add(Check("Water and cliffs blocked", WaterAndCliffsAreObstacles(layout), "water/river/cliff rects are in obstacle collision data"));
            checks.Add(Check("Map boundary present", GameObject.Find("MapBoundary") != null, "Collision/MapBoundary"));
            checks.Add(Check("Terrain collision present", GameObject.Find("TerrainCollision") != null, "Collision/TerrainCollision"));
            checks.Add(Check("Obstacle collision present", GameObject.Find("ObstacleCollision") != null, "Collision/ObstacleCollision"));

            var networkRunnerCount = UnityEngine.Object.FindObjectsByType<NetworkRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            checks.Add(Check("NetworkRunner not duplicated", networkRunnerCount == 0, $"scene NetworkRunner components={networkRunnerCount}; runner is runtime-created by BasicSpawner"));
            var spawnerCount = UnityEngine.Object.FindObjectsByType<BasicSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            checks.Add(Check("BasicSpawner exists", spawnerCount == 1, $"BasicSpawner components={spawnerCount}"));
            checks.Add(Check("BasicSpawner PlayerPrefab assigned", PlayerPrefabIsAssigned(), PlayerPrefabPath));
            var networkedTilemaps = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(tilemap => tilemap.GetComponent<NetworkObject>() != null);
            checks.Add(Check("No NetworkObject on static Tilemaps", networkedTilemaps == 0, $"networkedTilemaps={networkedTilemaps}"));

            var missingScripts = CountMissingScripts();
            checks.Add(Check("Missing Script", missingScripts == 0, $"missingScripts={missingScripts}"));
            var missingSprites = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(renderer => renderer.sprite == null);
            checks.Add(Check("Missing Sprite", missingSprites == 0, $"missingSprites={missingSprites}"));
            var missingMaterials = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(renderer => renderer.sharedMaterial == null);
            checks.Add(Check("Missing Material", missingMaterials == 0, $"missingMaterials={missingMaterials}"));
            checks.Add(Check("Forgotten Plains originals unchanged", CountDirtyForgottenPlainsAssets() == 0, $"dirtyOriginalAssets={CountDirtyForgottenPlainsAssets()}"));

            return new ValidationResult(checks);
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

        private static ValidationCheck CheckPoints(string label, PointFeatureDefinition[] points, ForgottenPlainsMapGenerator.LargeLayoutDefinition layout, int expectedCount)
        {
            var blocked = points == null
                ? expectedCount
                : points.Count(point => IsBlocked(layout, point.position.ToVector2(), 0.5f));
            var inCorridorOnly = points == null
                ? 0
                : points.Count(point => IsInsideCorridorOnly(layout, point.position.ToVector2()));
            var count = points?.Length ?? 0;
            return Check($"{label} count/open placement", count == expectedCount && blocked == 0 && inCorridorOnly == 0, $"count={count}, blocked={blocked}, corridorOnly={inCorridorOnly}");
        }

        private static bool PointsReachMeeting(PointFeatureDefinition[] points, ForgottenPlainsMapGenerator.LargeLayoutDefinition layout)
        {
            if (points == null)
            {
                return false;
            }

            return points.All(point => !IsBlocked(layout, point.position.ToVector2(), 0.5f));
        }

        private static bool WaterAndCliffsAreObstacles(ForgottenPlainsMapGenerator.LargeLayoutDefinition layout)
        {
            return layout.obstacles.Any(obstacle => obstacle.kind == "water") &&
                layout.obstacles.Any(obstacle => obstacle.kind == "river") &&
                layout.obstacles.Any(obstacle => obstacle.kind == "cliff");
        }

        private static Dictionary<string, List<string>> BuildGraph(ForgottenPlainsMapGenerator.LargeLayoutDefinition layout)
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

        private static bool IsConnected(Dictionary<string, List<string>> graph)
        {
            return CountReachable(graph) == graph.Count;
        }

        private static string[] FindBridges(ForgottenPlainsMapGenerator.LargeLayoutDefinition layout, Dictionary<string, List<string>> graph)
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

        private static bool IsBlocked(ForgottenPlainsMapGenerator.LargeLayoutDefinition layout, Vector2 point, float radius)
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

        private static bool IsInsideWalkable(ForgottenPlainsMapGenerator.LargeLayoutDefinition layout, Vector2 point)
        {
            return layout.areas.Any(area => area.bounds.ToRect().Contains(point)) ||
                layout.corridors.Any(corridor => corridor.bounds.ToRect().Contains(point));
        }

        private static bool IsInsideCorridorOnly(ForgottenPlainsMapGenerator.LargeLayoutDefinition layout, Vector2 point)
        {
            var inRoom = layout.areas.Any(area => area.bounds.ToRect().Contains(point));
            var inCorridor = layout.corridors.Any(corridor => corridor.bounds.ToRect().Contains(point));
            return inCorridor && !inRoom;
        }

        private static Rect Inflate(Rect rect, float amount)
        {
            return new Rect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);
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

        private static int CountDirtyForgottenPlainsAssets()
        {
            var count = 0;
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { AssetRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null && EditorUtility.IsDirty(asset))
                {
                    count++;
                }
            }

            return count;
        }

        private static ValidationCheck Check(string name, bool passed, string detail)
        {
            return new ValidationCheck(name, passed, detail);
        }

        private static ValidationCheck Pass(string name, string detail)
        {
            return new ValidationCheck(name, true, detail);
        }

        private static ValidationCheck Fail(string name, string detail)
        {
            return new ValidationCheck(name, false, detail);
        }

        public sealed class ValidationResult
        {
            public readonly List<ValidationCheck> Checks;
            public bool Passed => Checks.All(check => check.Passed);

            public ValidationResult(List<ValidationCheck> checks)
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
    }
}
