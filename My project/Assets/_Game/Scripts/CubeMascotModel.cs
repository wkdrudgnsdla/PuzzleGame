using System;
using UnityEngine;

namespace PuzzleGame
{
    [DefaultExecutionOrder(20)]
    [RequireComponent(typeof(Animator))]
    public sealed class CubeMascotModel : MonoBehaviour
    {
        private static readonly int[] StateHashes = Array.ConvertAll(
            (PlayerAnimationState[])Enum.GetValues(typeof(PlayerAnimationState)),
            state => Animator.StringToHash("Base Layer." + state));

        [SerializeField, Min(0.001f)] private float quarterTurnSeconds = 0.085f;
        private Animator animator;
        private PrototypePlayer3D player;
        private PlayerAnimationState? playedState;
        private Quaternion initialLocalRotation;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            player = GetComponentInParent<PrototypePlayer3D>();
            initialLocalRotation = transform.localRotation;
        }

        private void OnEnable() => playedState = null;

        private void Update()
        {
            if (player == null) return;
            if (player.VisualFacing == Vector3.zero)
            {
                transform.localRotation = initialLocalRotation;
            }
            else
            {
                Quaternion heading = Quaternion.LookRotation(player.VisualFacing);
                transform.rotation = playedState.HasValue
                    ? Quaternion.RotateTowards(transform.rotation, heading, 90f * Time.deltaTime / quarterTurnSeconds)
                    : heading;
            }

            PlayerAnimationState state = player.Animation.State;
            if (state == playedState) return;
            animator.Play(StateHashes[(int)state], 0, 0f);
            playedState = state;
        }
    }
}
