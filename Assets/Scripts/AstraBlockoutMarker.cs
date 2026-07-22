using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AmongUsClone
{
    public enum AstraBlockoutMarkerKind
    {
        RoomArea,
        SpawnPoint,
        MeetingPoint,
        TaskPoint,
        SabotagePoint,
        VentPoint,
        CameraBounds,
        MapBoundary,
        Obstacle
    }

    [DisallowMultipleComponent]
    public sealed class AstraBlockoutMarker : MonoBehaviour
    {
        public string Id;
        public string RoomId;
        public string DisplayName;
        public string GroupId;
        public string[] ConnectedIds;
        public AstraBlockoutMarkerKind Kind;
        public Color Color = Color.white;
        public Vector2 Size = Vector2.zero;
        public float Radius = 0.35f;
        public bool DrawFilled;

        private void OnDrawGizmos()
        {
            var previousColor = Gizmos.color;
            Gizmos.color = Color;

            if (Size.sqrMagnitude > 0.0001f)
            {
                var size = new Vector3(Size.x, Size.y, 0.04f);
                if (DrawFilled)
                {
                    Gizmos.DrawCube(transform.position, size);
                }

                Gizmos.DrawWireCube(transform.position, size);
            }
            else
            {
                if (DrawFilled)
                {
                    Gizmos.DrawSphere(transform.position, Radius);
                }

                Gizmos.DrawWireSphere(transform.position, Radius);
            }

            Gizmos.color = previousColor;

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(DisplayName))
            {
                Handles.Label(transform.position + Vector3.up * 0.35f, DisplayName);
            }
#endif
        }
    }
}
