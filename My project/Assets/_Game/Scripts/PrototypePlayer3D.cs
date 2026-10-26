using UnityEngine;

namespace PuzzleGame
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypePlayer3D : MonoBehaviour
    {
        public const float Height = 1.8f;
        public const float Radius = 0.3f;
        public const float Speed = 3f;
        public const float Gravity = 20f;
        public const float JumpHeight = 0.8f;
        private CharacterController controller;
        private float verticalVelocity;

        public Vector3 Foot => transform.position;
        public bool IsGrounded => controller != null && controller.enabled && verticalVelocity <= 0f &&
            Physics.SphereCast(Foot + Vector3.up * 0.35f, Radius * 0.92f,
                Vector3.down, out RaycastHit hit, 0.16f, 1 << ProjectionPrototype.World3DLayer,
                QueryTriggerInteraction.Ignore) && hit.normal.y > 0.6f;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = Height;
            controller.radius = Radius;
            controller.center = Vector3.up * (Height * 0.5f);
            controller.skinWidth = 0.015f;
            controller.stepOffset = 0.1f;
            controller.minMoveDistance = 0f;
        }

        public void Tick(Vector2 movement, ProjectionFrame frame, bool jump, float deltaTime)
        {
            bool grounded = IsGrounded;
            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (jump && grounded) verticalVelocity = Mathf.Sqrt(2f * Gravity * JumpHeight);
            verticalVelocity -= Gravity * deltaTime;
            // Yaw remains a valid movement heading even when the camera looks straight down.
            Vector3 groundForward = Vector3.Cross(frame.Right, Vector3.up).normalized;
            Vector3 horizontal = frame.Right * movement.x + groundForward * movement.y;
            controller.Move((horizontal * Speed + Vector3.up * verticalVelocity) * deltaTime);
        }

        public void Teleport(Vector3 foot)
        {
            controller.enabled = false;
            transform.position = foot;
            verticalVelocity = 0f;
            controller.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
