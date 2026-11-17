using UnityEngine;

namespace PuzzleGame
{
    /// <summary>Coordinates scene-authored mechanics; charged nodes and rail progress survive mode changes.</summary>
    public sealed class ProjectionPuzzleStage : MonoBehaviour
    {
        public string StageTitle;
        [TextArea(2, 5)] public string Instructions;
        public ProjectionPuzzleMechanic[] Mechanics = new ProjectionPuzzleMechanic[0];
        public ProjectionPuzzleReward[] Rewards = new ProjectionPuzzleReward[0];
        private ProjectionPrototype owner;

        public int RequiredObjectiveCount
        {
            get
            {
                int count = 0;
                foreach (ProjectionPuzzleMechanic mechanic in Mechanics)
                    if (mechanic != null && mechanic.RequiredForExit) count++;
                return count;
            }
        }

        public int CompletedObjectiveCount
        {
            get
            {
                int count = 0;
                foreach (ProjectionPuzzleMechanic mechanic in Mechanics)
                    if (mechanic != null && mechanic.RequiredForExit && mechanic.IsComplete) count++;
                return count;
            }
        }

        public bool ObjectivesComplete
        {
            get
            {
                foreach (ProjectionPuzzleMechanic mechanic in Mechanics)
                    if (mechanic != null && mechanic.RequiredForExit && !mechanic.IsComplete) return false;
                return true;
            }
        }

        public string ProgressHint => ObjectivesComplete
            ? owner != null && owner.Route != null && owner.Route.RequireCompletionForExit && !owner.Route.IsComplete
                ? "기믹 완료 · 다음 착지대에서 3D 체크포인트를 확인하세요"
                : "출구 활성화 · 3D에서 출구로 이동하세요"
            : "기믹을 완료하면 출구가 열립니다";

        public void Initialize(ProjectionPrototype prototype)
        {
            owner = prototype;
            if (Mechanics.Length == 0) Mechanics = GetComponentsInChildren<ProjectionPuzzleMechanic>(true);
            foreach (ProjectionPuzzleMechanic mechanic in Mechanics)
                if (mechanic != null) mechanic.Initialize(prototype);
            foreach (ProjectionPuzzleReward reward in Rewards)
                if (reward != null) reward.Initialize(prototype);
        }

        public void Tick(ProjectionFrame frame, bool is2D, Vector2 moveInput, float deltaTime, bool won)
        {
            foreach (ProjectionPuzzleMechanic mechanic in Mechanics)
            {
                if (mechanic == null || !mechanic.isActiveAndEnabled) continue;
                mechanic.Tick(frame, is2D, moveInput, deltaTime);
            }
        }

        public void ResetProgress()
        {
            foreach (ProjectionPuzzleMechanic mechanic in Mechanics)
                if (mechanic != null) mechanic.ResetPuzzle();
            foreach (ProjectionPuzzleReward reward in Rewards)
                if (reward != null) reward.ResetReward();
        }

    }
}
