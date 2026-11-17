using UnityEngine;

namespace PuzzleGame
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CubeMascotSprite : MonoBehaviour
    {
        private const int Tile = 96;
        // Rows from the top: front/profile idle, profile walk/jump/fall/land,
        // overhead idle/walk, front jump/fall/land.
        private static readonly int[] FrameCounts = { 4, 4, 6, 2, 2, 3, 4, 6, 2, 2, 3 };
        [SerializeField] private Texture2D atlas;
        [SerializeField, Min(0.001f)] private float topDownQuarterTurnSeconds = 0.09f;
        private readonly Sprite[][] frames = new Sprite[FrameCounts.Length][];
        private PrototypePlayer2D player;
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            player = GetComponentInParent<PrototypePlayer2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (atlas == null || atlas.width != 6 * Tile || atlas.height != FrameCounts.Length * Tile)
            {
                Debug.LogError("The Cube Mascot sprite atlas is missing or has an unexpected size.", this);
                enabled = false;
                return;
            }

            for (int row = 0; row < frames.Length; row++)
            {
                frames[row] = new Sprite[FrameCounts[row]];
                bool overhead = row is 6 or 7;
                Vector2 pivot = overhead ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 10f / Tile);
                float pixelsPerUnit = overhead ? 108f : 96f;
                for (int column = 0; column < frames[row].Length; column++)
                {
                    var rect = new Rect(column * Tile, atlas.height - (row + 1) * Tile, Tile, Tile);
                    frames[row][column] = Sprite.Create(atlas, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                }
            }
            spriteRenderer.sprite = frames[0][0];
        }

        private void OnEnable() => transform.localRotation = Quaternion.identity;

        private void LateUpdate()
        {
            PlayerAnimation animation = player.Animation;
            bool overhead = player.IsTopDown;
            Vector2 facing = player.VisualFacing;
            int row = overhead ? (animation.State == PlayerAnimationState.Walk ? 7 : 6) : animation.State switch
            {
                PlayerAnimationState.Walk => 2,
                PlayerAnimationState.Jump => facing.x == 0f ? 8 : 3,
                PlayerAnimationState.Fall => facing.x == 0f ? 9 : 4,
                PlayerAnimationState.Land => facing.x == 0f ? 10 : 5,
                _ => facing.x == 0f ? 0 : 1
            };

            Sprite[] sequence = frames[row];
            float elapsed = animation.Elapsed;
            int frame = animation.State switch
            {
                PlayerAnimationState.Walk => Mathf.FloorToInt(elapsed * 12f) % sequence.Length,
                PlayerAnimationState.Jump or PlayerAnimationState.Fall =>
                    Mathf.Min(Mathf.FloorToInt(elapsed * 14f), sequence.Length - 1),
                PlayerAnimationState.Land => Mathf.Min(
                    Mathf.FloorToInt(elapsed / PlayerAnimation.LandDuration * sequence.Length), sequence.Length - 1),
                _ => IdleFrame(elapsed)
            };
            spriteRenderer.sprite = sequence[frame];
            spriteRenderer.flipX = !overhead && facing.x < 0f;
            transform.localPosition = new Vector3(0f, overhead ? 0f : -PrototypePlayer3D.Height * 0.5f, -0.15f);
            if (overhead && facing != Vector2.zero)
            {
                Quaternion heading = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, facing));
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, heading,
                    90f * Time.deltaTime / topDownQuarterTurnSeconds);
            }
            else
            {
                transform.localRotation = Quaternion.identity;
            }
        }

        private static int IdleFrame(float elapsed)
        {
            float phase = elapsed % 2.6f;
            if (phase >= 2.42f && phase < 2.53f) return 3;
            if (phase >= 1.3f) return 2;
            return phase >= 0.65f ? 1 : 0;
        }

        private void OnDestroy()
        {
            foreach (Sprite[] sequence in frames)
                if (sequence != null)
                    foreach (Sprite frame in sequence)
                        Destroy(frame);
        }
    }
}
