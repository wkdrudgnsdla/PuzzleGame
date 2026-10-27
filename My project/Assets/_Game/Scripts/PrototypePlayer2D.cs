using UnityEngine;

namespace PuzzleGame
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class PrototypePlayer2D : MonoBehaviour
    {
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];
        private Rigidbody2D body;
        private CapsuleCollider2D capsule;
        private ContactFilter2D groundFilter;
        private Vector2 movement;
        private bool jumpQueued;
        private bool topDown;
        private System.Func<Vector2, Vector2, Vector2> constrainMove;

        public Vector2 Foot => topDown ? body.position :
            body.position - Vector2.up * (PrototypePlayer3D.Height * 0.5f);
        public bool IsGrounded => body.simulated &&
            (topDown || (body.linearVelocity.y <= 0.2f && GroundHitCount() > 0));

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            capsule = GetComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(PrototypePlayer3D.Radius * 2f, PrototypePlayer3D.Height);
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.gravityScale = PrototypePlayer3D.Gravity / Mathf.Abs(Physics2D.gravity.y);
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.None;
            groundFilter = new ContactFilter2D();
            groundFilter.SetLayerMask(1 << ProjectionPrototype.World2DLayer);
            groundFilter.useTriggers = false;
        }

        private int GroundHitCount()
        {
            int count = capsule.Cast(Vector2.down, groundFilter, groundHits, 0.09f);
            int valid = 0;
            for (int i = 0; i < count; i++) if (groundHits[i].normal.y > 0.6f) valid++;
            return valid;
        }

        public bool IsSupportedBy(Collider2D surface)
        {
            if (topDown || !body.simulated || body.linearVelocity.y > 0.2f) return false;
            int count = capsule.Cast(Vector2.down, groundFilter, groundHits, 0.09f);
            for (int i = 0; i < count; i++)
                if (groundHits[i].collider == surface && groundHits[i].normal.y > 0.6f) return true;
            return false;
        }

        public void SetInput(float horizontal, bool jump)
        {
            SetInput(new Vector2(horizontal, 0f), jump);
        }

        public void SetInput(Vector2 input, bool jump)
        {
            movement = Vector2.ClampMagnitude(input, 1f);
            jumpQueued |= !topDown && jump;
        }

        public void SetTopDown(bool topDown, System.Func<Vector2, Vector2, Vector2> constrainMove = null)
        {
            this.topDown = topDown;
            this.constrainMove = topDown ? constrainMove : null;
            body.gravityScale = topDown ? 0f : PrototypePlayer3D.Gravity / Mathf.Abs(Physics2D.gravity.y);
            capsule.size = topDown
                ? Vector2.one * (PrototypePlayer3D.Radius * 2f)
                : new Vector2(PrototypePlayer3D.Radius * 2f, PrototypePlayer3D.Height);
            body.linearVelocity = Vector2.zero;
            movement = Vector2.zero;
            jumpQueued = false;
        }

        private void FixedUpdate()
        {
            if (!body.simulated) return;
            if (topDown)
            {
                Vector2 current = body.position;
                Vector2 desired = current + movement * (PrototypePlayer3D.Speed * Time.fixedDeltaTime);
                Vector2 allowed = constrainMove != null ? constrainMove(current, desired) : current;
                body.linearVelocity = Vector2.zero;
                body.MovePosition(allowed);
                jumpQueued = false;
                return;
            }

            float vertical = body.linearVelocity.y;
            if (jumpQueued && IsGrounded)
                vertical = Mathf.Sqrt(2f * PrototypePlayer3D.Gravity * PrototypePlayer3D.JumpHeight);
            jumpQueued = false;
            body.linearVelocity = new Vector2(movement.x * PrototypePlayer3D.Speed, vertical);
        }

        public void Teleport(Vector2 foot)
        {
            Vector2 center = topDown ? foot :
                foot + Vector2.up * (PrototypePlayer3D.Height * 0.5f + 0.012f);
            // An unsimulated body is reinserted using its Transform when enabled.
            // Update both representations so the first physics step cannot spawn inside the floor.
            transform.position = new Vector3(center.x, center.y, 0f);
            body.position = center;
            body.linearVelocity = Vector2.zero;
            movement = Vector2.zero;
            jumpQueued = false;
            Physics2D.SyncTransforms();
        }

        public void SetSimulated(bool simulated)
        {
            body.simulated = simulated;
            body.linearVelocity = Vector2.zero;
            movement = Vector2.zero;
            jumpQueued = false;
        }

        public bool HasBlockingOverlap()
        {
            if (topDown) return false;
            // A tiny shrink leaves room for the supporting floor's contact tolerance.
            return Physics2D.OverlapCapsule(body.position,
                capsule.size - Vector2.one * 0.04f, CapsuleDirection2D.Vertical, 0f,
                1 << ProjectionPrototype.World2DLayer) != null;
        }
    }
}
