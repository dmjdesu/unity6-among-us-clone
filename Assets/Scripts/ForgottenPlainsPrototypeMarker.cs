using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AmongUsClone
{
    public enum ForgottenPlainsPrototypeMarkerKind
    {
        Collider,
        MapBoundary,
        RoomArea,
        SpawnPoint,
        TaskPoint,
        MeetingPoint,
        ReportTestArea,
        KillTestArea
    }

    [DisallowMultipleComponent]
    public sealed class ForgottenPlainsPrototypeMarker : MonoBehaviour
    {
        public ForgottenPlainsPrototypeMarkerKind Kind;
        public string Label;
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
                if (DrawFilled)
                {
                    Gizmos.DrawCube(transform.position, new Vector3(Size.x, Size.y, 0.02f));
                }

                Gizmos.DrawWireCube(transform.position, new Vector3(Size.x, Size.y, 0.02f));
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
            if (!string.IsNullOrWhiteSpace(Label))
            {
                Handles.Label(transform.position + Vector3.up * 0.35f, Label);
            }
#endif
        }
    }
}
