using UnityEngine;

namespace PuzzleGame
{
    /// <summary>Enables ordinary authored blocks and registers them in both physics worlds immediately.</summary>
    public sealed class ProjectionPuzzleReward : MonoBehaviour
    {
        public GameObject[] Blocks = new GameObject[0];
        private ProjectionPrototype prototype;
        public bool IsActivated { get; private set; }

        public void Initialize(ProjectionPrototype owner) { prototype = owner; }

        public void Activate()
        {
            if (IsActivated) return;
            IsActivated = true;
            SetBlocks(true);
        }

        public void ResetReward()
        {
            IsActivated = false;
            SetBlocks(false);
        }

        private void SetBlocks(bool active)
        {
            foreach (GameObject block in Blocks) if (block != null) block.SetActive(active);
            if (prototype != null) prototype.RefreshTerrain();
        }
    }
}
