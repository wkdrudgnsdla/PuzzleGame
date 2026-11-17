using UnityEngine;

namespace PuzzleGame
{
    /// <summary>A world-unit orthographic frame with yaw and pitch, without roll.</summary>
    public readonly struct ProjectionFrame
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Right;
        public readonly Vector3 Up;
        public readonly Vector3 Forward;
        public readonly float Yaw;
        public readonly float Pitch;

        public ProjectionFrame(Vector3 origin, float yawDegrees, float pitchDegrees = 0f)
        {
            Origin = origin;
            Yaw = yawDegrees;
            Pitch = pitchDegrees;
            Quaternion rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            Right = rotation * Vector3.right;
            Up = rotation * Vector3.up;
            Forward = rotation * Vector3.forward;
        }

        public Vector2 Project(Vector3 world)
        {
            Vector3 offset = world - Origin;
            return new Vector2(Vector3.Dot(offset, Right), Vector3.Dot(offset, Up));
        }

        public float Depth(Vector3 world)
        {
            return Vector3.Dot(world - Origin, Forward);
        }

        public Vector3 Unproject(Vector2 projected, float depth)
        {
            return Origin + projected.x * Right + projected.y * Up + depth * Forward;
        }
    }
}
