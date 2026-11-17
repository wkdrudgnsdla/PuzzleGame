using UnityEngine;

namespace PuzzleGame
{
    internal static class DimensionRipple
    {
        private const float Duration = 0.64f;
        private static Camera activeCamera;
        private static float startTime;
        private static float direction;

        internal static void Play(Camera camera, bool to2D)
        {
            activeCamera = camera;
            startTime = Time.unscaledTime;
            direction = to2D ? 1f : -1f;
        }

        internal static bool TryGet(Camera camera, out float progress, out float rippleDirection)
        {
            progress = 0f;
            rippleDirection = 0f;
            if (activeCamera == null || activeCamera != camera) return false;

            progress = (Time.unscaledTime - startTime) / Duration;
            if (progress >= 1f)
            {
                activeCamera = null;
                return false;
            }

            rippleDirection = direction;
            return true;
        }

        internal static void Clear(Camera camera)
        {
            if (activeCamera == camera) activeCamera = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => activeCamera = null;
    }
}
