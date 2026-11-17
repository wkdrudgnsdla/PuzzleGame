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
        private static readonly SoundCue[] Footsteps = { SoundCue.PlayerStepA, SoundCue.PlayerStepB, SoundCue.PlayerStepC };

        private int footstepIndex;
        private float nextStep;

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

            if (State == next)
            {
                if (State == PlayerAnimationState.Walk && Elapsed >= nextStep)
                {
                    GameAudioEvents.Play(Footsteps[footstepIndex]);
                    footstepIndex = (footstepIndex + 1) % Footsteps.Length;
                    nextStep += 0.29f;
                }
                if (State == PlayerAnimationState.Fall && Elapsed >= 0.32f && Elapsed - deltaTime < 0.32f)
                    GameAudioEvents.Play(SoundCue.PlayerFall);
                return;
            }
            if (next == PlayerAnimationState.Walk) nextStep = 0.09f;
            if (next == PlayerAnimationState.Jump) GameAudioEvents.Play(SoundCue.PlayerJump);
            if (next == PlayerAnimationState.Land && Elapsed >= 0.075f) GameAudioEvents.Play(SoundCue.PlayerLand);
            State = next;
            Elapsed = 0f;
        }
    }
}
