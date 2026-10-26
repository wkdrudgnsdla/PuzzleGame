using UnityEngine;

namespace PuzzleGame
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ProjectableBox : MonoBehaviour
    {
        [SerializeField, Min(0)] private int id;
        [SerializeField] private Color bodyColor = Color.white;
        private BoxCollider sourceCollider;

        public int Id => id;
        public Color BodyColor => bodyColor;
        public BoxCollider SourceCollider
        {
            get
            {
                if (sourceCollider == null) sourceCollider = GetComponent<BoxCollider>();
                return sourceCollider;
            }
        }
        public PolygonCollider2D ProjectedCollider { get; internal set; }

        private void Awake()
        {
            sourceCollider = GetComponent<BoxCollider>();
        }

        public void Initialize(int id, Color color)
        {
            this.id = id;
            bodyColor = color;
            sourceCollider = GetComponent<BoxCollider>();
        }
    }
}
