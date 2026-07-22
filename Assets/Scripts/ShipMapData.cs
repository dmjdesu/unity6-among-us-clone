using System;
using UnityEngine;

namespace AmongUsClone
{
    [Serializable]
    public sealed class ShipMapDefinition
    {
        public string mapId;
        public float gridSize = 1f;
        public RectDefinition playBounds;
        public RoomDefinition[] rooms;
        public RectFeatureDefinition[] corridors;
        public RectFeatureDefinition[] doorways;
        public RectFeatureDefinition[] obstacles;
        public PointFeatureDefinition[] navigationWaypoints;
        public PointFeatureDefinition[] spawnPoints;
        public PointFeatureDefinition[] taskPoints;
        public SabotagePointDefinition[] sabotagePoints;
        public VentNodeDefinition[] ventNodes;
        public PointFeatureDefinition meetingPoint;
    }

    [Serializable]
    public struct RectDefinition
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public Rect ToRect()
        {
            return new Rect(x, y, width, height);
        }
    }

    [Serializable]
    public struct Vector2Definition
    {
        public float x;
        public float y;

        public Vector2 ToVector2()
        {
            return new Vector2(x, y);
        }
    }

    [Serializable]
    public sealed class RoomDefinition
    {
        public string id;
        public string displayName;
        public RectDefinition bounds;
        public Vector2Definition label;
    }

    [Serializable]
    public sealed class RectFeatureDefinition
    {
        public string id;
        public string displayName;
        public RectDefinition bounds;
    }

    [Serializable]
    public class PointFeatureDefinition
    {
        public string id;
        public string displayName;
        public string roomId;
        public string taskKind;
        public Vector2Definition position;
    }

    [Serializable]
    public sealed class SabotagePointDefinition : PointFeatureDefinition
    {
        public string kind;
        public bool hasCountdown;
    }

    [Serializable]
    public sealed class VentNodeDefinition : PointFeatureDefinition
    {
        public string groupId;
    }

    public readonly struct RoomArea
    {
        public readonly string Id;
        public readonly string Name;
        public readonly Rect Bounds;
        public readonly Vector2 LabelPosition;

        public RoomArea(string id, string name, Rect bounds, Vector2 labelPosition)
        {
            Id = id;
            Name = name;
            Bounds = bounds;
            LabelPosition = labelPosition;
        }
    }

    public readonly struct TaskStation
    {
        public readonly int Id;
        public readonly string Name;
        public readonly TaskKind Kind;
        public readonly Vector2 Position;

        public TaskStation(int id, string name, TaskKind kind, Vector2 position)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Position = position;
        }
    }

    public readonly struct SabotageStation
    {
        public readonly SabotageType Type;
        public readonly string Name;
        public readonly Vector2 Position;
        public readonly bool HasCountdown;

        public SabotageStation(SabotageType type, string name, Vector2 position, bool hasCountdown)
        {
            Type = type;
            Name = name;
            Position = position;
            HasCountdown = hasCountdown;
        }
    }

    public readonly struct VentStation
    {
        public readonly string Name;
        public readonly string GroupId;
        public readonly Vector2 Position;

        public VentStation(string name, string groupId, Vector2 position)
        {
            Name = name;
            GroupId = groupId;
            Position = position;
        }
    }
}
