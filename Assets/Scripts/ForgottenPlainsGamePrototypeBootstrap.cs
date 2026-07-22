using UnityEngine;
using UnityEngine.SceneManagement;

namespace AmongUsClone
{
    [DisallowMultipleComponent]
    public sealed class ForgottenPlainsGamePrototypeBootstrap : MonoBehaviour
    {
        public const string SceneName = "ForgottenPlainsGamePrototype";

        private const string RootName = "ForgottenPlainsGamePrototype";
        private const float BoundaryThickness = 0.45f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrapForActiveScene()
        {
            if (SceneManager.GetActiveScene().name != SceneName ||
                Object.FindAnyObjectByType<ForgottenPlainsGamePrototypeBootstrap>() != null)
            {
                return;
            }

            var root = EnsureRoot(RootName);
            var debug = EnsureChild(root, "Debug");
            var bootstrap = new GameObject("Forgotten Plains Prototype Bootstrap");
            bootstrap.transform.SetParent(debug, false);
            bootstrap.AddComponent<ForgottenPlainsGamePrototypeBootstrap>();
        }

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name == SceneName)
            {
                EnsurePrototypeScene();
            }
        }

        public static void EnsurePrototypeScene()
        {
            if (SceneManager.GetActiveScene().name != SceneName)
            {
                return;
            }

            var root = EnsureRoot(RootName);
            var environment = EnsureChild(root, "Environment");
            var gameplay = EnsureChild(root, "Gameplay");
            var networking = EnsureChild(root, "Networking");
            var cameraGroup = EnsureChild(root, "Camera");
            var lighting = EnsureChild(root, "Lighting");
            var debug = EnsureChild(root, "Debug");

            EnsureChild(environment, "Ground");
            EnsureChild(environment, "Walls");
            var obstacles = EnsureChild(environment, "Obstacles");
            EnsureChild(environment, "DecorationsBack");
            EnsureChild(environment, "DecorationsFront");

            var mapBoundary = EnsureChild(gameplay, "MapBoundary");
            var roomAreas = EnsureChild(gameplay, "RoomAreas");
            var spawnPoints = EnsureChild(gameplay, "SpawnPoints");
            var taskPoints = EnsureChild(gameplay, "TaskPoints");
            var meetingPoint = EnsureChild(gameplay, "MeetingPoint");
            var reportTestArea = EnsureChild(gameplay, "ReportTestArea");
            var killTestArea = EnsureChild(gameplay, "KillTestArea");

            MoveIfExists("Level", environment);
            MoveIfExists("Main Camera", cameraGroup);
            MoveIfExists("Global Light 2D", lighting);
            MoveIfExists("Network", networking);
            MoveIfExists("EventSystem", debug);
            DisableIfExists("Canvas");
            DisableRiverAndWaterVisuals();
            DisableMinifantasyCameraController();
            DisableWalkableTilemapColliders();
            ConfigureCamera();
            ConfigureSorting();

            RebuildBoundary(mapBoundary);
            RebuildObstacleBounds(obstacles);
            RebuildRoomAreas(roomAreas);
            RebuildSpawnPoints(spawnPoints);
            RebuildTaskPoints(taskPoints);
            RebuildMeetingPoint(meetingPoint);
            RebuildTestAreas(reportTestArea, killTestArea);
        }

        private static Transform EnsureRoot(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                return existing.transform;
            }

            return new GameObject(name).transform;
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            var childObject = new GameObject(name);
            childObject.transform.SetParent(parent);
            childObject.transform.localPosition = Vector3.zero;
            childObject.transform.localRotation = Quaternion.identity;
            childObject.transform.localScale = Vector3.one;
            return childObject.transform;
        }

        private static void MoveIfExists(string objectName, Transform parent)
        {
            var candidate = GameObject.Find(objectName);
            if (candidate == null || candidate.transform == parent || candidate.transform.IsChildOf(parent))
            {
                return;
            }

            candidate.transform.SetParent(parent, true);
        }

        private static void DisableIfExists(string objectName)
        {
            foreach (var candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.name == objectName)
                {
                    candidate.gameObject.SetActive(false);
                }
            }
        }

        private static void DisableRiverAndWaterVisuals()
        {
            DisableIfExists("Water");
            DisableIfExists("Cattails");
        }

        private static void DisableMinifantasyCameraController()
        {
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour != null && behaviour.GetType().FullName == "Minifantasy.CameraController")
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static void DisableWalkableTilemapColliders()
        {
            foreach (var collider in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var objectName = collider.gameObject.name;
                if (objectName == "Ground" || objectName == "GroundDecoration" || objectName == "GroundShadow")
                {
                    collider.enabled = false;
                }
            }
        }

        private static void ConfigureCamera()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 6f;
            var target = new Vector3(ShipMap.MeetingPoint.x, ShipMap.MeetingPoint.y, mainCamera.transform.position.z);
            mainCamera.transform.position = ShipMap.ClampCameraPosition(target, mainCamera);
        }

        private static void ConfigureSorting()
        {
            SetSortingForNamedHierarchy("Ground", "Ground");
            SetSortingForNamedHierarchy("GroundShadow", "Ground");
            SetSortingForNamedHierarchy("Water", "Ground");
            SetSortingForNamedHierarchy("GroundDecoration", "FloorDetails");
            SetSortingForNamedHierarchy("WallsShadow", "PropsBack");
            SetSortingForNamedHierarchy("Walls - Vertical", "PropsBack");
            SetSortingForNamedHierarchy("Walls - Horizontal", "PropsBack");
            SetSortingForNamedHierarchy("Walls - Cliffs (1)", "PropsBack");
            SetSortingForNamedHierarchy("Walls - Cliffs (2)", "PropsBack");
            SetSortingForNamedHierarchy("Walls - Cliffs (3)", "PropsBack");
            SetSortingForNamedHierarchy("Walls - Above Cliffs", "PropsBack");
            SetSortingForNamedHierarchy("Cattails", "PropsBack");
            SetSortingForNamedHierarchy("GrassLong", "FloorDetails");
            SetSortingForNamedHierarchy("Creepers", "FloorDetails");
            SetSortingForNamedHierarchy("Flowers", "FloorDetails");
            SetSortingForNamedHierarchy("Trees", "PropsFront");
            SetSortingForNamedHierarchy("Pillars", "PropsFront");
        }

        private static void SetSortingForNamedHierarchy(string objectName, string sortingLayerName)
        {
            foreach (var root in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (root.name != objectName)
                {
                    continue;
                }

                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sortingLayerName = sortingLayerName;
                }
            }
        }

        private static void RebuildBoundary(Transform parent)
        {
            ClearChildren(parent);

            var bounds = ShipMap.PlayBounds;
            CreateBox(
                parent,
                "Boundary North",
                new Rect(bounds.xMin - BoundaryThickness, bounds.yMax, bounds.width + BoundaryThickness * 2f, BoundaryThickness),
                false,
                ForgottenPlainsPrototypeMarkerKind.MapBoundary,
                new Color(1f, 0.22f, 0.18f, 0.85f));
            CreateBox(
                parent,
                "Boundary South",
                new Rect(bounds.xMin - BoundaryThickness, bounds.yMin - BoundaryThickness, bounds.width + BoundaryThickness * 2f, BoundaryThickness),
                false,
                ForgottenPlainsPrototypeMarkerKind.MapBoundary,
                new Color(1f, 0.22f, 0.18f, 0.85f));
            CreateBox(
                parent,
                "Boundary West",
                new Rect(bounds.xMin - BoundaryThickness, bounds.yMin, BoundaryThickness, bounds.height),
                false,
                ForgottenPlainsPrototypeMarkerKind.MapBoundary,
                new Color(1f, 0.22f, 0.18f, 0.85f));
            CreateBox(
                parent,
                "Boundary East",
                new Rect(bounds.xMax, bounds.yMin, BoundaryThickness, bounds.height),
                false,
                ForgottenPlainsPrototypeMarkerKind.MapBoundary,
                new Color(1f, 0.22f, 0.18f, 0.85f));
        }

        private static void RebuildObstacleBounds(Transform parent)
        {
            ClearChildren(parent);

            foreach (var obstacle in ShipMap.Obstacles)
            {
                CreateBox(
                    parent,
                    "Obstacle Collider",
                    obstacle,
                    false,
                    ForgottenPlainsPrototypeMarkerKind.Collider,
                    new Color(1f, 0.62f, 0.12f, 0.75f));
            }
        }

        private static void RebuildRoomAreas(Transform parent)
        {
            ClearChildren(parent);

            foreach (var room in ShipMap.Rooms)
            {
                CreateBox(
                    parent,
                    room.Name,
                    room.Bounds,
                    true,
                    ForgottenPlainsPrototypeMarkerKind.RoomArea,
                    new Color(0.18f, 0.62f, 1f, 0.55f),
                    room.Name);
            }
        }

        private static void RebuildSpawnPoints(Transform parent)
        {
            ClearChildren(parent);

            var spawnPoints = ShipMap.SpawnPoints;
            for (var i = 0; i < spawnPoints.Length; i++)
            {
                CreateCircle(
                    parent,
                    $"SpawnPoint {i + 1:00}",
                    spawnPoints[i],
                    0.34f,
                    ForgottenPlainsPrototypeMarkerKind.SpawnPoint,
                    new Color(0.35f, 1f, 0.35f, 0.85f),
                    $"Spawn {i + 1:00}");
            }
        }

        private static void RebuildTaskPoints(Transform parent)
        {
            ClearChildren(parent);

            foreach (var station in ShipMap.TaskStations)
            {
                CreateCircle(
                    parent,
                    $"TaskPoint - {station.Name}",
                    station.Position,
                    0.38f,
                    ForgottenPlainsPrototypeMarkerKind.TaskPoint,
                    new Color(1f, 0.88f, 0.18f, 0.85f),
                    station.Name);
            }
        }

        private static void RebuildMeetingPoint(Transform parent)
        {
            ClearChildren(parent);

            CreateCircle(
                parent,
                "Emergency Meeting Point",
                ShipMap.MeetingPoint,
                0.55f,
                ForgottenPlainsPrototypeMarkerKind.MeetingPoint,
                new Color(1f, 0.2f, 0.95f, 0.9f),
                "Emergency Meeting");
        }

        private static void RebuildTestAreas(Transform reportParent, Transform killParent)
        {
            ClearChildren(reportParent);
            ClearChildren(killParent);

            CreateBox(
                reportParent,
                "Open Report Check",
                new Rect(-3.2f, -1.7f, 6.4f, 3.2f),
                true,
                ForgottenPlainsPrototypeMarkerKind.ReportTestArea,
                new Color(0.4f, 1f, 0.95f, 0.55f),
                "Report Check");
            CreateBox(
                killParent,
                "Obstacle Kill Check",
                new Rect(-1.85f, 3.05f, 3.7f, 3.4f),
                true,
                ForgottenPlainsPrototypeMarkerKind.KillTestArea,
                new Color(1f, 0.24f, 0.24f, 0.55f),
                "Kill LOS Check");
        }

        private static void CreateBox(
            Transform parent,
            string name,
            Rect rect,
            bool isTrigger,
            ForgottenPlainsPrototypeMarkerKind kind,
            Color color,
            string label = null)
        {
            var boxObject = new GameObject(name);
            boxObject.transform.SetParent(parent);
            boxObject.transform.position = new Vector3(rect.center.x, rect.center.y, 0f);

            var collider = boxObject.AddComponent<BoxCollider2D>();
            collider.size = rect.size;
            collider.isTrigger = isTrigger;

            var marker = boxObject.AddComponent<ForgottenPlainsPrototypeMarker>();
            marker.Kind = kind;
            marker.Label = label ?? name;
            marker.Color = color;
            marker.Size = rect.size;
            marker.DrawFilled = isTrigger;
        }

        private static void CreateCircle(
            Transform parent,
            string name,
            Vector2 position,
            float radius,
            ForgottenPlainsPrototypeMarkerKind kind,
            Color color,
            string label)
        {
            var pointObject = new GameObject(name);
            pointObject.transform.SetParent(parent);
            pointObject.transform.position = new Vector3(position.x, position.y, 0f);

            var collider = pointObject.AddComponent<CircleCollider2D>();
            collider.radius = radius;
            collider.isTrigger = true;

            var marker = pointObject.AddComponent<ForgottenPlainsPrototypeMarker>();
            marker.Kind = kind;
            marker.Label = label;
            marker.Color = color;
            marker.Radius = radius;
        }

        private static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Object.Destroy(child);
                }
                else
                {
                    Object.DestroyImmediate(child);
                }
            }
        }
    }
}
