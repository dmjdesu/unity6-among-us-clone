using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AmongUsClone.Editor
{
    public static class ProductionRoomPrefabBuilder
    {
        public const string PrefabFolder = "Assets/Resources/RoomPrefabs/Production";

        private const string ArtFolder = "Assets/Art";
        private const string RoomArtFolder = "Assets/Art/ProductionRooms";
        private const string MaterialFolder = "Assets/Art/ProductionRooms/Materials";
        private const string MeshFolder = "Assets/Art/ProductionRooms/Meshes";

        private static readonly string[] RoomLayers =
        {
            "Background",
            "FloorDetails",
            "Walls",
            "PropsBack",
            "InteractiveObjects",
            "PropsFront",
            "Collision",
            "VisionOccluders",
            "TaskPoints",
            "DoorConnections"
        };

        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        private static Mesh _unitQuadMesh;
        private static int _meshIndex;

        [MenuItem("Build/Generate Production Room Prefabs")]
        public static void BuildThreeRoomPrefabs()
        {
            EnsureFolderPath("Assets/Resources");
            EnsureFolderPath("Assets/Resources/RoomPrefabs");
            EnsureFolderPath(PrefabFolder);
            EnsureFolderPath(ArtFolder);
            EnsureFolderPath(RoomArtFolder);
            EnsureFolderPath(MaterialFolder);

            if (AssetDatabase.IsValidFolder(MeshFolder))
            {
                AssetDatabase.DeleteAsset(MeshFolder);
            }

            EnsureFolderPath(MeshFolder);

            MaterialCache.Clear();
            _unitQuadMesh = null;
            _meshIndex = 0;

            SaveRoom(BuildCentralMeetingRoom(), "CentralMeetingRoom");
            SaveRoom(BuildReactorRoom(), "ReactorRoom");
            SaveRoom(BuildMedicalRoom(), "MedicalRoom");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated production room prefabs for CentralMeetingRoom, ReactorRoom, and MedicalRoom.");
        }

        [MenuItem("Build/Render Production Room Preview")]
        public static void RenderProductionRoomPreview()
        {
            const int width = 2400;
            const int height = 1400;

            BuildThreeRoomPrefabs();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            InstantiatePreviewRoom("CentralMeetingRoom", Vector2.zero);
            InstantiatePreviewRoom("ReactorRoom", new Vector2(-29f, 0f));
            InstantiatePreviewRoom("MedicalRoom", new Vector2(28f, 14f));

            var cameraObject = new GameObject("Production Room Preview Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 25f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.006f, 0.01f, 0.014f, 1f);
            camera.transform.position = new Vector3(0f, 3f, -10f);

            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previousActive = RenderTexture.active;
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();

            var outputPath = Path.GetFullPath("Logs/Screenshots/production-room-prefabs-render.png");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            File.WriteAllBytes(outputPath, texture.EncodeToPNG());

            camera.targetTexture = null;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(texture);
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);

            Debug.Log($"Rendered production room preview to {outputPath}");
        }

        private static RoomContext CreateRoom(string name)
        {
            var root = new GameObject(name);
            var layers = new Dictionary<string, Transform>(RoomLayers.Length);
            foreach (var layerName in RoomLayers)
            {
                var layer = new GameObject(layerName).transform;
                layer.SetParent(root.transform, false);
                layers[layerName] = layer;
            }

            return new RoomContext(root, layers, name);
        }

        private static void InstantiatePreviewRoom(string roomName, Vector2 position)
        {
            var prefabPath = $"{PrefabFolder}/{roomName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new FileNotFoundException($"Missing production room prefab: {prefabPath}", prefabPath);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = new Vector3(position.x, position.y, 0f);
        }

        private static GameObject BuildCentralMeetingRoom()
        {
            var ctx = CreateRoom("CentralMeetingRoom");
            var floor = new[]
            {
                new Vector2(-12.5f, -14f),
                new Vector2(12.5f, -14f),
                new Vector2(17f, -9.5f),
                new Vector2(17f, 9.5f),
                new Vector2(12.5f, 14f),
                new Vector2(-12.5f, 14f),
                new Vector2(-17f, 9.5f),
                new Vector2(-17f, -9.5f)
            };

            var floorMat = Mat("central_floor_deep_teal", new Color(0.065f, 0.12f, 0.14f, 1f));
            var floorPanelMat = Mat("central_floor_panel_blue", new Color(0.09f, 0.17f, 0.2f, 1f));
            var floorTrimMat = Mat("central_floor_trim_cyan", new Color(0.16f, 0.36f, 0.4f, 1f));
            var wallTopA = Mat("central_wall_top_a", new Color(0.32f, 0.43f, 0.46f, 1f));
            var wallTopB = Mat("central_wall_top_b", new Color(0.24f, 0.34f, 0.38f, 1f));
            var wallSideA = Mat("central_wall_side_a", new Color(0.045f, 0.07f, 0.085f, 1f));
            var wallSideB = Mat("central_wall_side_b", new Color(0.07f, 0.1f, 0.115f, 1f));
            var glowMat = Mat("central_cyan_light_wash", new Color(0.05f, 0.38f, 0.44f, 0.26f));
            var darkMat = Mat("ship_backdrop_black_green", new Color(0.006f, 0.012f, 0.016f, 1f));
            var tableMat = Mat("central_table_graphite", new Color(0.045f, 0.052f, 0.058f, 1f));
            var tableTopMat = Mat("central_table_top", new Color(0.12f, 0.16f, 0.17f, 1f));
            var yellowMat = Mat("shared_warning_yellow", new Color(1f, 0.72f, 0.16f, 1f));
            var screenMat = Mat("central_screen_cyan", new Color(0.1f, 0.9f, 0.88f, 0.9f));
            var frontMat = Mat("central_foreground_rail", new Color(0.018f, 0.025f, 0.03f, 1f));
            var occluderMat = Mat("vision_occluder_soft_black", new Color(0f, 0f, 0f, 0.22f));

            CreatePolygon(ctx["Background"], "Outer Hull Shadow", ScalePoints(floor, 1.08f), darkMat, -8);
            CreatePolygon(ctx["Background"], "Cyan Ambient Wash", ScalePoints(floor, 1.02f), glowMat, -7);
            CreatePolygon(ctx["Background"], "Octagonal Floor Base", floor, floorMat, -4);

            CreatePolygon(ctx["FloorDetails"], "Raised Inner Floor Plate", ScalePoints(floor, 0.73f), floorPanelMat, -2);
            CreateArcStrip(ctx["FloorDetails"], "Meeting Ring Warning Track", Vector2.zero, new Vector2(8.6f, 5.5f), new Vector2(8.0f, 4.9f), 200f, 340f, 28, yellowMat, -1);
            CreateArcStrip(ctx["FloorDetails"], "Meeting Ring Cyan Track", Vector2.zero, new Vector2(11.8f, 8.2f), new Vector2(11.45f, 7.85f), 25f, 155f, 26, floorTrimMat, -1);
            CreateRect(ctx["FloorDetails"], "West Cable Trench", new Rect(-14.8f, -0.45f, 7.8f, 0.38f), floorTrimMat, -1);
            CreateRect(ctx["FloorDetails"], "East Cable Trench", new Rect(7.0f, -0.45f, 7.8f, 0.38f), floorTrimMat, -1);
            CreateRect(ctx["FloorDetails"], "South Service Hatch", new Rect(-2.4f, -11.9f, 4.8f, 2.0f), Mat("central_hatch_dark", new Color(0.035f, 0.055f, 0.065f, 1f)), -1);
            CreateSegment(ctx["FloorDetails"], "Hatch Split Line", new Vector2(0f, -11.8f), new Vector2(0f, -10.0f), 0.12f, floorTrimMat, 0);
            CreatePolyline(ctx["FloorDetails"], "Crew Manifest Cable", new[]
            {
                new Vector2(-10.5f, 5.6f),
                new Vector2(-7.2f, 4.1f),
                new Vector2(-4.8f, 2.4f)
            }, 0.16f, Mat("central_cable_orange", new Color(0.8f, 0.37f, 0.12f, 1f)), 0);

            CreateWallLoop(ctx["Walls"], "Central", floor, 0.68f, wallSideA, wallSideB, 1);
            CreateWallLoop(ctx["Walls"], "Central Top", OffsetPolygon(floor, 0.42f), 0.46f, wallTopA, wallTopB, 3);
            CreateRect(ctx["Walls"], "North Observation Glass", new Rect(-7.2f, 12.9f, 14.4f, 0.72f), Mat("central_window_blue", new Color(0.1f, 0.32f, 0.42f, 0.75f)), 5);
            CreateRect(ctx["Walls"], "South Wall Service Panels", new Rect(-10.4f, -13.65f, 5.4f, 0.42f), wallTopB, 5);
            CreateRect(ctx["Walls"], "South Wall Storage Panels", new Rect(5.6f, -13.65f, 4.8f, 0.42f), wallTopB, 5);

            CreateArcStrip(ctx["PropsBack"], "North Crew Seating Arc", new Vector2(0f, 1.0f), new Vector2(10.8f, 7.2f), new Vector2(9.8f, 6.3f), 22f, 158f, 30, Mat("central_seat_back", new Color(0.12f, 0.18f, 0.2f, 1f)), 4);
            CreateArcStrip(ctx["PropsBack"], "South Crew Seating Arc", new Vector2(0f, -0.8f), new Vector2(10.8f, 6.5f), new Vector2(9.8f, 5.65f), 202f, 338f, 30, Mat("central_seat_shadow", new Color(0.055f, 0.075f, 0.085f, 1f)), 4);
            CreateRect(ctx["PropsBack"], "Left Wall Voting Console Bank", new Rect(-15.8f, 2.0f, 1.2f, 5.6f), Mat("central_console_bank", new Color(0.075f, 0.11f, 0.13f, 1f)), 5);
            CreateRect(ctx["PropsBack"], "Right Wall Status Console Bank", new Rect(14.6f, -7.4f, 1.2f, 5.4f), Mat("central_console_bank_alt", new Color(0.095f, 0.12f, 0.125f, 1f)), 5);

            CreateEllipse(ctx["InteractiveObjects"], "Large Oval Meeting Table", Vector2.zero, new Vector2(5.9f, 3.4f), 32, tableMat, 8);
            CreateEllipse(ctx["InteractiveObjects"], "Inset Meeting Table Top", new Vector2(0f, 0.18f), new Vector2(4.9f, 2.55f), 32, tableTopMat, 9);
            CreateRect(ctx["InteractiveObjects"], "Emergency Button Pedestal", new Rect(-0.75f, 2.52f, 1.5f, 1.0f), Mat("emergency_button_pedestal", new Color(0.23f, 0.22f, 0.18f, 1f)), 12);
            CreateEllipse(ctx["InteractiveObjects"], "Emergency Button Lens", new Vector2(0f, 3.18f), new Vector2(0.48f, 0.3f), 18, Mat("emergency_button_red", new Color(1f, 0.08f, 0.04f, 1f)), 13);
            CreateRect(ctx["InteractiveObjects"], "Crew Manifest Screen", new Rect(-11.2f, 4.6f, 1.9f, 1.15f), screenMat, 12);
            CreateRect(ctx["InteractiveObjects"], "Diagnostics Screen", new Rect(9.7f, 5.2f, 2.1f, 1.0f), screenMat, 12);
            CreateRect(ctx["InteractiveObjects"], "Navigation Briefing Console", new Rect(10.1f, -7.0f, 2.4f, 1.3f), Mat("central_screen_green", new Color(0.23f, 0.9f, 0.55f, 0.9f)), 12);

            CreateRect(ctx["PropsFront"], "Foreground Table Lip", new Rect(-5.7f, -2.95f, 11.4f, 0.52f), frontMat, 30);
            CreateRect(ctx["PropsFront"], "South Safety Rail", new Rect(-7.6f, -9.9f, 15.2f, 0.36f), frontMat, 31);
            CreateSegment(ctx["PropsFront"], "Left Foreground Rail Upright", new Vector2(-7.6f, -10.25f), new Vector2(-7.6f, -8.9f), 0.24f, frontMat, 31);
            CreateSegment(ctx["PropsFront"], "Right Foreground Rail Upright", new Vector2(7.6f, -10.25f), new Vector2(7.6f, -8.9f), 0.24f, frontMat, 31);

            CreateColliderBox(ctx["Collision"], "Meeting Table Collision", Vector2.zero, new Vector2(10.5f, 5.7f));
            CreateColliderBox(ctx["Collision"], "North Wall Collision", new Vector2(0f, 14.15f), new Vector2(25f, 1.0f));
            CreateColliderBox(ctx["Collision"], "South Wall Collision", new Vector2(0f, -14.15f), new Vector2(25f, 1.0f));
            CreateColliderBox(ctx["Collision"], "West Wall Collision", new Vector2(-17.2f, 0f), new Vector2(1.0f, 18.8f));
            CreateColliderBox(ctx["Collision"], "East Wall Collision", new Vector2(17.2f, 0f), new Vector2(1.0f, 18.8f));

            CreateRect(ctx["VisionOccluders"], "North Wall Vision Occluder", new Rect(-12f, 12.9f, 24f, 1.25f), occluderMat, 40);
            CreateRect(ctx["VisionOccluders"], "Foreground Rail Vision Occluder", new Rect(-8.3f, -10.35f, 16.6f, 1.2f), occluderMat, 40);

            CreateTaskPoint(ctx, "TaskPoint_CrewManifest", new Vector2(-10.1f, 4.2f));
            CreateTaskPoint(ctx, "TaskPoint_EmergencyDiagnostics", new Vector2(9.5f, 4.2f));
            CreateTaskPoint(ctx, "TaskPoint_NavigationBriefing", new Vector2(9.7f, -5.2f));
            CreateDoorConnection(ctx, "Door_West_Reactor", new Vector2(-17f, -1.0f), new Vector2(1.2f, 6.8f), 90f);
            CreateDoorConnection(ctx, "Door_East_Medical", new Vector2(17f, 8.0f), new Vector2(1.2f, 6.6f), 90f);

            return ctx.Root;
        }

        private static GameObject BuildReactorRoom()
        {
            var ctx = CreateRoom("ReactorRoom");
            var outline = new[]
            {
                new Vector2(-12f, -12f),
                new Vector2(12f, -12f),
                new Vector2(12f, 0f),
                new Vector2(3f, 0f),
                new Vector2(3f, 12f),
                new Vector2(-12f, 12f)
            };

            var floorA = Mat("reactor_floor_burnt_metal", new Color(0.105f, 0.085f, 0.07f, 1f));
            var floorB = Mat("reactor_floor_service_wing", new Color(0.13f, 0.105f, 0.08f, 1f));
            var wallTopA = Mat("reactor_wall_top_iron", new Color(0.45f, 0.33f, 0.23f, 1f));
            var wallTopB = Mat("reactor_wall_top_heat", new Color(0.35f, 0.24f, 0.18f, 1f));
            var wallSideA = Mat("reactor_wall_side_dark", new Color(0.055f, 0.04f, 0.035f, 1f));
            var wallSideB = Mat("reactor_wall_side_warm", new Color(0.095f, 0.06f, 0.045f, 1f));
            var glow = Mat("reactor_green_glow", new Color(0.16f, 0.9f, 0.42f, 0.33f));
            var amberGlow = Mat("reactor_amber_glow", new Color(1f, 0.42f, 0.08f, 0.22f));
            var hazard = Mat("reactor_hazard_yellow", new Color(1f, 0.68f, 0.08f, 1f));
            var pipe = Mat("reactor_pipe_copper", new Color(0.74f, 0.29f, 0.12f, 1f));
            var front = Mat("reactor_front_pipe", new Color(0.035f, 0.03f, 0.025f, 1f));
            var occluder = Mat("vision_occluder_warm_black", new Color(0f, 0f, 0f, 0.26f));

            CreateRect(ctx["Background"], "L Room Shadow Main", new Rect(-12.8f, -12.8f, 16.6f, 25.6f), Mat("reactor_backdrop_black", new Color(0.012f, 0.008f, 0.006f, 1f)), -8);
            CreateRect(ctx["Background"], "L Room Shadow Wing", new Rect(2.2f, -12.8f, 10.6f, 13.6f), Mat("reactor_backdrop_black", new Color(0.012f, 0.008f, 0.006f, 1f)), -8);
            CreateRect(ctx["Background"], "Reactor Heat Wash", new Rect(-12f, -12f, 24f, 24f), amberGlow, -7);
            CreateRect(ctx["Background"], "Main Reactor Floor", new Rect(-12f, -12f, 15f, 24f), floorA, -4);
            CreateRect(ctx["Background"], "Service Wing Floor", new Rect(3f, -12f, 9f, 12f), floorB, -4);

            CreateRect(ctx["FloorDetails"], "Main Grated Service Lane", new Rect(-10.8f, -1.0f, 7.5f, 1.15f), Mat("reactor_floor_grate", new Color(0.055f, 0.055f, 0.052f, 1f)), -1);
            CreateRect(ctx["FloorDetails"], "Lower Service Lane", new Rect(-1.0f, -10.4f, 10.4f, 1.0f), Mat("reactor_floor_grate_alt", new Color(0.075f, 0.065f, 0.055f, 1f)), -1);
            CreatePolyline(ctx["FloorDetails"], "Coolant Pipe Floor Route", new[]
            {
                new Vector2(-9.4f, -7.4f),
                new Vector2(-5.1f, -7.4f),
                new Vector2(-2.6f, -3.4f),
                new Vector2(8.7f, -3.4f)
            }, 0.32f, pipe, 0);
            CreatePolyline(ctx["FloorDetails"], "Control Cable Floor Route", new[]
            {
                new Vector2(-2.0f, 9.3f),
                new Vector2(-0.2f, 5.5f),
                new Vector2(-0.2f, 0.7f),
                new Vector2(8.2f, -8.1f)
            }, 0.18f, Mat("reactor_control_cable_red", new Color(0.85f, 0.18f, 0.08f, 1f)), 0);
            CreateSegmentPieces(ctx["FloorDetails"], "Lower Hazard Stripe", new Vector2(2.3f, -8.8f), new Vector2(10.8f, -8.8f), 0.2f, 9, 0.24f, new[] { hazard, floorB }, 0);
            CreateSegmentPieces(ctx["FloorDetails"], "Core Hazard Stripe", new Vector2(-9.4f, 7.3f), new Vector2(-3.0f, 7.3f), 0.22f, 7, 0.22f, new[] { hazard, floorA }, 0);

            CreateWallLoop(ctx["Walls"], "Reactor", outline, 0.72f, wallSideA, wallSideB, 1);
            CreateWallLoop(ctx["Walls"], "Reactor Top", OffsetPolygon(outline, 0.42f), 0.48f, wallTopA, wallTopB, 3);
            CreateRect(ctx["Walls"], "Upper Heat Sink Wall Panels", new Rect(-10.9f, 10.65f, 11.2f, 0.78f), wallTopB, 5);
            CreateRect(ctx["Walls"], "Service Wing South Panels", new Rect(3.8f, -11.45f, 7.2f, 0.58f), wallTopB, 5);

            CreateRect(ctx["PropsBack"], "Left Coolant Tank", new Rect(-10.5f, -5.5f, 2.2f, 8.8f), Mat("reactor_coolant_tank", new Color(0.08f, 0.18f, 0.16f, 1f)), 4);
            CreateRect(ctx["PropsBack"], "Right Heat Exchanger", new Rect(6.8f, -11.1f, 2.9f, 6.4f), Mat("reactor_heat_exchanger", new Color(0.19f, 0.13f, 0.095f, 1f)), 4);
            CreateRect(ctx["PropsBack"], "Control Rod Rack", new Rect(-2.4f, 7.2f, 3.2f, 3.0f), Mat("reactor_rod_rack", new Color(0.13f, 0.11f, 0.095f, 1f)), 4);

            CreateEllipse(ctx["InteractiveObjects"], "Reactor Core Glow", new Vector2(-6.0f, 0.0f), new Vector2(4.0f, 8.4f), 36, glow, 9);
            CreateEllipse(ctx["InteractiveObjects"], "Reactor Core Shell", new Vector2(-6.0f, 0.0f), new Vector2(2.45f, 6.8f), 32, Mat("reactor_core_shell", new Color(0.08f, 0.12f, 0.1f, 1f)), 10);
            CreateRect(ctx["InteractiveObjects"], "Reactor Core Glass Column", new Rect(-6.95f, -5.8f, 1.9f, 11.6f), Mat("reactor_core_glass", new Color(0.22f, 1f, 0.55f, 0.78f)), 11);
            CreateRect(ctx["InteractiveObjects"], "Coolant Flow Console", new Rect(-10.2f, -8.4f, 2.2f, 1.35f), Mat("reactor_console_blue", new Color(0.1f, 0.65f, 0.82f, 0.92f)), 12);
            CreateRect(ctx["InteractiveObjects"], "Turbine Governor Console", new Rect(6.2f, -8.6f, 2.5f, 1.2f), Mat("reactor_console_orange", new Color(1f, 0.42f, 0.12f, 0.9f)), 12);
            CreateRect(ctx["InteractiveObjects"], "Stabilizer Console", new Rect(-3.1f, 8.2f, 2.2f, 1.0f), Mat("reactor_console_green", new Color(0.25f, 0.95f, 0.45f, 0.88f)), 12);

            CreateSegment(ctx["PropsFront"], "Foreground Overhead Pipe A", new Vector2(-11.5f, -9.8f), new Vector2(9.4f, -9.8f), 0.7f, front, 31);
            CreateSegment(ctx["PropsFront"], "Foreground Overhead Pipe B", new Vector2(-9.8f, -11.1f), new Vector2(7.6f, -11.1f), 0.34f, Mat("reactor_front_pipe_highlight", new Color(0.09f, 0.075f, 0.055f, 1f)), 32);
            CreateRect(ctx["PropsFront"], "Core Front Guard Rail", new Rect(-9.3f, -5.95f, 6.6f, 0.42f), front, 32);

            CreateColliderBox(ctx["Collision"], "Missing Upper Right Collision", new Vector2(7.5f, 6.0f), new Vector2(9.0f, 12.0f));
            CreateColliderBox(ctx["Collision"], "Reactor Core Collision", new Vector2(-6.0f, 0.0f), new Vector2(4.9f, 12.8f));
            CreateColliderBox(ctx["Collision"], "Coolant Tank Collision", new Vector2(-10.5f, -1.1f), new Vector2(2.4f, 9.0f));
            CreateColliderBox(ctx["Collision"], "Heat Exchanger Collision", new Vector2(8.2f, -7.9f), new Vector2(3.4f, 6.6f));

            CreateRect(ctx["VisionOccluders"], "Core Vision Occluder", new Rect(-8.8f, -6.4f, 5.6f, 13.0f), occluder, 40);
            CreateRect(ctx["VisionOccluders"], "Foreground Pipe Vision Occluder", new Rect(-12f, -11.5f, 22f, 2.1f), occluder, 40);

            CreateTaskPoint(ctx, "TaskPoint_ReactorStabilize", new Vector2(-3.2f, 7.0f));
            CreateTaskPoint(ctx, "TaskPoint_CoolantFlow", new Vector2(-8.2f, -8.6f));
            CreateTaskPoint(ctx, "TaskPoint_TurbineGovernor", new Vector2(5.5f, -8.8f));
            CreateTaskPoint(ctx, "TaskPoint_ControlRods", new Vector2(-0.4f, 9.5f));
            CreateDoorConnection(ctx, "Door_East_Central", new Vector2(12f, -3.0f), new Vector2(1.2f, 7.0f), 90f);

            return ctx.Root;
        }

        private static GameObject BuildMedicalRoom()
        {
            var ctx = CreateRoom("MedicalRoom");
            var floor = new[]
            {
                new Vector2(-11f, -9f),
                new Vector2(10f, -9f),
                new Vector2(11f, 5.8f),
                new Vector2(7f, 9f),
                new Vector2(-8.8f, 9f),
                new Vector2(-11f, 5.0f)
            };

            var floorMat = Mat("medical_floor_warm_gray", new Color(0.16f, 0.18f, 0.18f, 1f));
            var floorPanel = Mat("medical_floor_clean_panel", new Color(0.2f, 0.25f, 0.245f, 1f));
            var wallTopA = Mat("medical_wall_top_white", new Color(0.72f, 0.78f, 0.76f, 1f));
            var wallTopB = Mat("medical_wall_top_mint", new Color(0.49f, 0.68f, 0.64f, 1f));
            var wallSideA = Mat("medical_wall_side_deep", new Color(0.08f, 0.11f, 0.115f, 1f));
            var wallSideB = Mat("medical_wall_side_green", new Color(0.07f, 0.14f, 0.13f, 1f));
            var lightWash = Mat("medical_soft_lighting", new Color(0.46f, 1f, 0.82f, 0.19f));
            var glass = Mat("medical_glass_cyan", new Color(0.32f, 0.92f, 1f, 0.48f));
            var bed = Mat("medical_bed_white", new Color(0.77f, 0.83f, 0.81f, 1f));
            var screen = Mat("medical_screen_bluegreen", new Color(0.08f, 0.84f, 0.78f, 0.9f));
            var front = Mat("medical_front_arm_dark", new Color(0.04f, 0.055f, 0.058f, 1f));
            var occluder = Mat("vision_occluder_med_black", new Color(0f, 0f, 0f, 0.2f));

            CreatePolygon(ctx["Background"], "Trapezoid Room Shadow", ScalePoints(floor, 1.08f), Mat("medical_backdrop_dark", new Color(0.012f, 0.016f, 0.016f, 1f)), -8);
            CreatePolygon(ctx["Background"], "Medical Light Wash", ScalePoints(floor, 1.03f), lightWash, -7);
            CreatePolygon(ctx["Background"], "Trapezoid Floor Base", floor, floorMat, -4);
            CreatePolygon(ctx["FloorDetails"], "Inset Sterile Floor Plate", ScalePoints(floor, 0.76f), floorPanel, -2);
            CreateRect(ctx["FloorDetails"], "Scanner Cable Channel", new Rect(-4.8f, -2.2f, 9.2f, 0.28f), Mat("medical_cable_teal", new Color(0.18f, 0.65f, 0.62f, 1f)), 0);
            CreateRect(ctx["FloorDetails"], "Bed Utility Channel", new Rect(5.9f, -7.4f, 0.34f, 11.8f), Mat("medical_floor_line_mint", new Color(0.34f, 0.78f, 0.68f, 1f)), 0);
            CreateRect(ctx["FloorDetails"], "Sterilizer Hatch", new Rect(6.4f, -7.2f, 3.4f, 2.1f), Mat("medical_hatch_dark", new Color(0.09f, 0.12f, 0.12f, 1f)), -1);
            CreateSegmentPieces(ctx["FloorDetails"], "Clean Zone Warning Dashes", new Vector2(-9.2f, -6.6f), new Vector2(2.7f, -6.6f), 0.16f, 8, 0.32f, new[] { Mat("medical_warning_mint", new Color(0.5f, 1f, 0.82f, 1f)), floorMat }, 0);

            CreateWallLoop(ctx["Walls"], "Medical", floor, 0.64f, wallSideA, wallSideB, 1);
            CreateWallLoop(ctx["Walls"], "Medical Top", OffsetPolygon(floor, 0.38f), 0.46f, wallTopA, wallTopB, 3);
            CreateRect(ctx["Walls"], "North Cabinet Row", new Rect(-7.4f, 7.9f, 10.8f, 0.82f), wallTopB, 5);
            CreateRect(ctx["Walls"], "East Glass Cabinet", new Rect(9.55f, -1.0f, 0.82f, 5.2f), glass, 6);

            CreateRect(ctx["PropsBack"], "Sample Fridge", new Rect(-9.6f, 4.0f, 2.4f, 4.2f), Mat("medical_fridge_body", new Color(0.62f, 0.7f, 0.68f, 1f)), 4);
            CreateRect(ctx["PropsBack"], "Records Cabinet", new Rect(5.5f, 5.5f, 3.6f, 2.1f), Mat("medical_records_cabinet", new Color(0.22f, 0.28f, 0.27f, 1f)), 4);
            CreateRect(ctx["PropsBack"], "Lower Recovery Pod", new Rect(6.4f, -5.9f, 3.6f, 2.0f), Mat("medical_recovery_pod", new Color(0.36f, 0.48f, 0.46f, 1f)), 4);

            CreateRect(ctx["InteractiveObjects"], "Med Scanner Bed Base", new Rect(-4.1f, -1.35f, 7.4f, 2.4f), bed, 10);
            CreateRect(ctx["InteractiveObjects"], "Med Scanner Mattress", new Rect(-3.75f, -1.0f, 6.65f, 1.55f), Mat("medical_mattress_pale", new Color(0.87f, 0.95f, 0.92f, 1f)), 11);
            CreateArcStrip(ctx["InteractiveObjects"], "Scanner Glass Hood", new Vector2(-0.5f, -0.6f), new Vector2(4.4f, 2.3f), new Vector2(3.75f, 1.75f), 8f, 172f, 24, glass, 12);
            CreateRect(ctx["InteractiveObjects"], "Vitals Screen", new Rect(6.7f, 3.5f, 2.4f, 1.25f), screen, 12);
            CreateRect(ctx["InteractiveObjects"], "Sample Analyzer Screen", new Rect(-9.2f, 6.7f, 1.9f, 0.95f), screen, 12);
            CreateRect(ctx["InteractiveObjects"], "Sterilizer Console", new Rect(7.2f, -6.9f, 2.2f, 1.0f), Mat("medical_screen_green", new Color(0.28f, 0.95f, 0.54f, 0.88f)), 12);

            CreateSegment(ctx["PropsFront"], "Scanner Foreground Arm", new Vector2(-5.4f, 0.15f), new Vector2(3.9f, 0.15f), 0.38f, front, 31);
            CreateSegment(ctx["PropsFront"], "Ceiling Surgical Rail", new Vector2(-8.0f, -7.8f), new Vector2(8.4f, -7.8f), 0.34f, front, 31);
            CreateRect(ctx["PropsFront"], "Lower Recovery Pod Foreground Lip", new Rect(5.9f, -6.9f, 4.5f, 0.48f), front, 32);

            CreateColliderBox(ctx["Collision"], "Scanner Bed Collision", new Vector2(-0.4f, -1.05f), new Vector2(7.8f, 2.55f));
            CreateColliderBox(ctx["Collision"], "Sample Fridge Collision", new Vector2(-9.6f, 4.0f), new Vector2(2.7f, 4.4f));
            CreateColliderBox(ctx["Collision"], "Recovery Pod Collision", new Vector2(7.4f, -5.7f), new Vector2(3.9f, 2.2f));
            CreateColliderBox(ctx["Collision"], "East Wall Collision", new Vector2(11.2f, -1.0f), new Vector2(0.9f, 12.5f));
            CreateColliderBox(ctx["Collision"], "North Wall Collision", new Vector2(-1.0f, 9.25f), new Vector2(15.8f, 0.9f));

            CreateRect(ctx["VisionOccluders"], "Scanner Hood Vision Occluder", new Rect(-5.3f, -0.05f, 9.2f, 1.0f), occluder, 40);
            CreateRect(ctx["VisionOccluders"], "Recovery Pod Vision Occluder", new Rect(5.5f, -7.15f, 5.0f, 1.2f), occluder, 40);

            CreateTaskPoint(ctx, "TaskPoint_MedScan", new Vector2(-2.1f, -3.1f));
            CreateTaskPoint(ctx, "TaskPoint_SortRecords", new Vector2(6.2f, 4.2f));
            CreateTaskPoint(ctx, "TaskPoint_AnalyzeSamples", new Vector2(-6.8f, 6.3f));
            CreateTaskPoint(ctx, "TaskPoint_SterilizeBay", new Vector2(4.1f, -7.2f));
            CreateDoorConnection(ctx, "Door_West_Central", new Vector2(-11f, -3.5f), new Vector2(1.2f, 7.0f), 90f);

            return ctx.Root;
        }

        private static void SaveRoom(GameObject room, string fileName)
        {
            var prefabPath = $"{PrefabFolder}/{fileName}.prefab";
            AssetDatabase.DeleteAsset(prefabPath);
            PrefabUtility.SaveAsPrefabAsset(room, prefabPath);
            UnityEngine.Object.DestroyImmediate(room);
        }

        private static void CreateTaskPoint(RoomContext ctx, string name, Vector2 localPosition)
        {
            var point = new GameObject(name);
            point.transform.SetParent(ctx["TaskPoints"], false);
            point.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            CreateRect(ctx["TaskPoints"], $"{name}_AnchorPlate", new Rect(localPosition.x - 0.32f, localPosition.y - 0.32f, 0.64f, 0.64f), Mat("task_point_anchor_blue", new Color(0.1f, 0.82f, 0.72f, 0.72f)), 20);
        }

        private static void CreateDoorConnection(RoomContext ctx, string name, Vector2 localPosition, Vector2 size, float rotationDegrees)
        {
            var door = new GameObject(name);
            door.transform.SetParent(ctx["DoorConnections"], false);
            door.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            door.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);

            CreateRect(ctx["DoorConnections"], $"{name}_Threshold", new Rect(localPosition.x - size.x * 0.5f, localPosition.y - size.y * 0.5f, size.x, size.y), Mat("door_threshold_floor", new Color(0.14f, 0.24f, 0.25f, 1f)), 6);
            CreateSegment(ctx["DoorConnections"], $"{name}_LeftFrame", localPosition + new Vector2(-size.x * 0.7f, -size.y * 0.5f), localPosition + new Vector2(-size.x * 0.7f, size.y * 0.5f), 0.28f, Mat("door_frame_dark", new Color(0.04f, 0.065f, 0.075f, 1f)), 16);
            CreateSegment(ctx["DoorConnections"], $"{name}_RightFrame", localPosition + new Vector2(size.x * 0.7f, -size.y * 0.5f), localPosition + new Vector2(size.x * 0.7f, size.y * 0.5f), 0.28f, Mat("door_frame_light", new Color(0.22f, 0.32f, 0.34f, 1f)), 17);
        }

        private static void CreateWallLoop(Transform parent, string prefix, Vector2[] points, float thickness, Material materialA, Material materialB, int sortingOrder)
        {
            for (var i = 0; i < points.Length; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Length];
                CreateSegmentPieces(parent, $"{prefix} Wall {i + 1}", a, b, thickness, 3, 0.34f, new[] { materialA, materialB }, sortingOrder);
            }
        }

        private static void CreatePolyline(Transform parent, string name, Vector2[] points, float thickness, Material material, int sortingOrder)
        {
            for (var i = 0; i < points.Length - 1; i++)
            {
                CreateSegment(parent, $"{name} {i + 1}", points[i], points[i + 1], thickness, material, sortingOrder);
            }
        }

        private static void CreateSegmentPieces(Transform parent, string name, Vector2 a, Vector2 b, float thickness, int pieces, float gap, Material[] materials, int sortingOrder)
        {
            var delta = b - a;
            var length = delta.magnitude;
            if (length <= 0.001f)
            {
                return;
            }

            var direction = delta / length;
            var pieceLength = length / Mathf.Max(1, pieces);
            var inset = Mathf.Min(gap * 0.5f, pieceLength * 0.28f);
            for (var i = 0; i < pieces; i++)
            {
                var start = a + direction * (i * pieceLength + inset);
                var end = a + direction * ((i + 1) * pieceLength - inset);
                var material = materials[Mathf.Abs(i) % materials.Length];
                CreateSegment(parent, $"{name} Segment {i + 1}", start, end, thickness, material, sortingOrder);
            }
        }

        private static void CreateSegment(Transform parent, string name, Vector2 a, Vector2 b, float thickness, Material material, int sortingOrder)
        {
            var delta = b - a;
            var length = delta.magnitude;
            if (length <= 0.001f)
            {
                return;
            }

            var center = (a + b) * 0.5f;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var go = CreateMeshObject(parent, name, UnitQuadMesh, material, sortingOrder);
            go.transform.localPosition = new Vector3(center.x, center.y, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(length, thickness, 1f);
        }

        private static GameObject CreateRect(Transform parent, string name, Rect rect, Material material, int sortingOrder)
        {
            var go = CreateMeshObject(parent, name, UnitQuadMesh, material, sortingOrder);
            go.transform.localPosition = new Vector3(rect.center.x, rect.center.y, 0f);
            go.transform.localScale = new Vector3(rect.width, rect.height, 1f);
            return go;
        }

        private static GameObject CreatePolygon(Transform parent, string name, Vector2[] points, Material material, int sortingOrder)
        {
            var mesh = SaveMesh(CreatePolygonMesh($"{parent.name}_{name}", points), $"{parent.name}_{name}");
            return CreateMeshObject(parent, name, mesh, material, sortingOrder);
        }

        private static GameObject CreateEllipse(Transform parent, string name, Vector2 center, Vector2 radii, int segments, Material material, int sortingOrder)
        {
            var points = new Vector2[Mathf.Max(8, segments)];
            for (var i = 0; i < points.Length; i++)
            {
                var angle = i / (float)points.Length * Mathf.PI * 2f;
                points[i] = center + new Vector2(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y);
            }

            return CreatePolygon(parent, name, points, material, sortingOrder);
        }

        private static GameObject CreateArcStrip(Transform parent, string name, Vector2 center, Vector2 outerRadii, Vector2 innerRadii, float startDegrees, float endDegrees, int segments, Material material, int sortingOrder)
        {
            var mesh = new Mesh { name = MakeAssetName(name) };
            var segmentCount = Mathf.Max(3, segments);
            var vertices = new Vector3[(segmentCount + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segmentCount * 6];

            for (var i = 0; i <= segmentCount; i++)
            {
                var t = i / (float)segmentCount;
                var angle = Mathf.Lerp(startDegrees, endDegrees, t) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices[i * 2] = center + new Vector2(direction.x * outerRadii.x, direction.y * outerRadii.y);
                vertices[i * 2 + 1] = center + new Vector2(direction.x * innerRadii.x, direction.y * innerRadii.y);
                uv[i * 2] = new Vector2(t, 1f);
                uv[i * 2 + 1] = new Vector2(t, 0f);
            }

            for (var i = 0; i < segmentCount; i++)
            {
                var v = i * 2;
                var tri = i * 6;
                triangles[tri] = v;
                triangles[tri + 1] = v + 2;
                triangles[tri + 2] = v + 1;
                triangles[tri + 3] = v + 1;
                triangles[tri + 4] = v + 2;
                triangles[tri + 5] = v + 3;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return CreateMeshObject(parent, name, SaveMesh(mesh, $"{parent.name}_{name}"), material, sortingOrder);
        }

        private static Mesh CreatePolygonMesh(string name, Vector2[] points)
        {
            var mesh = new Mesh { name = MakeAssetName(name) };
            var center = Vector2.zero;
            foreach (var point in points)
            {
                center += point;
            }

            center /= Mathf.Max(1, points.Length);

            var vertices = new Vector3[points.Length + 1];
            var uv = new Vector2[vertices.Length];
            vertices[0] = center;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (var i = 0; i < points.Length; i++)
            {
                vertices[i + 1] = points[i];
                uv[i + 1] = points[i] * 0.05f;
            }

            var triangles = new int[points.Length * 3];
            for (var i = 0; i < points.Length; i++)
            {
                var tri = i * 3;
                triangles[tri] = 0;
                triangles[tri + 1] = i + 1;
                triangles[tri + 2] = i == points.Length - 1 ? 1 : i + 2;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static GameObject CreateMeshObject(Transform parent, string name, Mesh mesh, Material material, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        private static void CreateColliderBox(Transform parent, string name, Vector2 center, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(center.x, center.y, 0f);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private static Material Mat(string key, Color color)
        {
            if (MaterialCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var path = $"{MaterialFolder}/{MakeAssetName(key)}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (color.a < 0.999f)
            {
                material.renderQueue = 3000;
            }

            EditorUtility.SetDirty(material);
            MaterialCache[key] = material;
            return material;
        }

        private static Mesh UnitQuadMesh
        {
            get
            {
                if (_unitQuadMesh != null)
                {
                    return _unitQuadMesh;
                }

                var path = $"{MeshFolder}/shared_unit_quad.asset";
                _unitQuadMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (_unitQuadMesh != null)
                {
                    return _unitQuadMesh;
                }

                var mesh = new Mesh { name = "shared_unit_quad" };
                mesh.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                };
                mesh.uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                AssetDatabase.CreateAsset(mesh, path);
                _unitQuadMesh = mesh;
                return _unitQuadMesh;
            }
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            var path = $"{MeshFolder}/{MakeAssetName(name)}_{_meshIndex:000}.asset";
            _meshIndex++;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Vector2[] ScalePoints(Vector2[] points, float scale)
        {
            var scaled = new Vector2[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                scaled[i] = points[i] * scale;
            }

            return scaled;
        }

        private static Vector2[] OffsetPolygon(Vector2[] points, float amount)
        {
            var scaled = new Vector2[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                var direction = points[i].sqrMagnitude <= 0.001f ? Vector2.zero : points[i].normalized;
                scaled[i] = points[i] + direction * amount;
            }

            return scaled;
        }

        private static string MakeAssetName(string value)
        {
            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static void EnsureFolderPath(string path)
        {
            var parts = path.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new InvalidOperationException($"Only Assets-relative folders are supported: {path}");
            }

            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private sealed class RoomContext
        {
            public readonly GameObject Root;
            private readonly Dictionary<string, Transform> _layers;

            public RoomContext(GameObject root, Dictionary<string, Transform> layers, string meshPrefix)
            {
                Root = root;
                _layers = layers;
            }

            public Transform this[string layerName] => _layers[layerName];
        }
    }
}
