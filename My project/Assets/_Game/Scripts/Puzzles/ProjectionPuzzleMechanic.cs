using UnityEngine;

namespace PuzzleGame
{
    /// <summary>A projected connection can drive any puzzle without owning camera or player controls.</summary>
    public abstract class ProjectionPuzzleMechanic : MonoBehaviour
    {
        public bool RequiredForExit = true;
        [Tooltip("Completed mechanics that supply or unlock this device. Checkpoints are not prerequisites.")]
        public ProjectionPuzzleMechanic[] Prerequisites = new ProjectionPuzzleMechanic[0];
        protected ProjectionPrototype Prototype;

        public abstract bool IsComplete { get; }
        public abstract string StatusHint { get; }
        public bool PrerequisitesComplete
        {
            get
            {
                if (Prerequisites == null) return true;
                foreach (ProjectionPuzzleMechanic prerequisite in Prerequisites)
                    if (prerequisite != null && !prerequisite.IsComplete) return false;
                return true;
            }
        }

        public virtual void Initialize(ProjectionPrototype prototype) { Prototype = prototype; }
        public abstract void Tick(ProjectionFrame frame, bool is2D, Vector2 moveInput, float deltaTime);
        public abstract void ResetPuzzle();
    }
}
