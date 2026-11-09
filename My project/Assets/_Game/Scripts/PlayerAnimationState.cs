namespace PuzzleGame
{
    public enum PlayerAnimationState
    {
        Idle,
        Walk,
        Jump,
        Fall,
        Land
    }

    internal sealed class PlayerAnimation
    {
        public const float LandDuration = 0.12f;

        public PlayerAnimationState State { get; private set; }
        public float Elapsed { get; private set; }

        public void Reset()
        {
            State = PlayerAnimationState.Idle;
            Elapsed = 0f;
        }

        public void Tick(bool grounded, bool moving, float verticalSpeed, float deltaTime)
        {
            Elapsed += deltaTime;
            PlayerAnimationState next;
            if (!grounded)
                next = verticalSpeed > 0f ? PlayerAnimationState.Jump : PlayerAnimationState.Fall;
            else if (State == PlayerAnimationState.Jump || State == PlayerAnimationState.Fall ||
                     (State == PlayerAnimationState.Land && Elapsed < LandDuration))
                next = PlayerAnimationState.Land;
            else
                next = moving ? PlayerAnimationState.Walk : PlayerAnimationState.Idle;

            if (State == next) return;
            State = next;
            Elapsed = 0f;
        }
    }
}
