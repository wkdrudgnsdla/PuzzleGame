using UnityEngine;
using UnityEngine.Events;

namespace PuzzleGame
{
    /// <summary>Coordinates independent directed relay links. Only the final lamp gates completion.</summary>
    public sealed class ProjectionPowerNetwork : ProjectionPuzzleMechanic
    {
        public ProjectionPowerNode[] Nodes = new ProjectionPowerNode[0];
        public ProjectionWire[] Wires = new ProjectionWire[0];
        public UnityEvent OnCompleted = new UnityEvent();
        private bool completionRaised;
        private ProjectionWire chargingWire;

        public override bool IsComplete
        {
            get
            {
                bool foundFinal = false;
                foreach (ProjectionPowerNode node in Nodes)
                {
                    if (node == null || !node.IsFinal) continue;
                    foundFinal = true;
                    if (!node.IsPowered) return false;
                }
                return foundFinal;
            }
        }

        public override string StatusHint
        {
            get
            {
                if (IsComplete) return "최종 전구 점등 · 전력 퍼즐 완료";
                if (chargingWire != null)
                    return chargingWire.Source.Label + " → " + chargingWire.Target.Label + " 충전 " +
                        Mathf.RoundToInt(chargingWire.FillProgress * 100f) + "%";
                foreach (ProjectionWire wire in Wires)
                {
                    if (wire == null || !wire.IsValid || !wire.Source.IsPowered || wire.Target.IsPowered) continue;
                    return wire.Source.Label + " → " + wire.Target.Label +
                        " · 어느 발판에서든 전선을 맞추고 R로 충전하세요";
                }
                int powered = 0;
                foreach (ProjectionPowerNode node in Nodes) if (node != null && node.IsPowered) powered++;
                return powered == 0 ? "전원이 꺼져 있습니다 · 레일 스위치로 첫 전원을 켜세요"
                    : "전원 " + powered + "/" + Nodes.Length + " · 전선을 맞추고 R · 충전 후 3D에서 다음 연결";
            }
        }

        public override void Initialize(ProjectionPrototype prototype)
        {
            base.Initialize(prototype);
            if (Nodes == null || Nodes.Length == 0) Nodes = GetComponentsInChildren<ProjectionPowerNode>();
            if (Wires == null || Wires.Length == 0) Wires = GetComponentsInChildren<ProjectionWire>();
            foreach (ProjectionPowerNode node in Nodes) if (node != null) node.InitializeVisuals();
            foreach (ProjectionWire wire in Wires) if (wire != null) wire.InitializeVisuals();
            ResetPuzzle();
        }

        public override void Tick(ProjectionFrame frame, bool is2D, Vector2 moveInput, float deltaTime)
        {
            chargingWire = null;
            foreach (ProjectionWire wire in Wires)
            {
                if (wire == null) continue;
                // Electrical state and the visible join determine charging, independently
                // of the player's support, checkpoint, distance, or grounded state.
                wire.Tick(frame, is2D, deltaTime);
                if (wire.IsCharging) chargingWire = wire;
            }
            foreach (ProjectionPowerNode node in Nodes)
                if (node != null) node.TickVisuals(frame, is2D, true);
            if (!completionRaised && is2D && IsComplete)
            {
                completionRaised = true;
                OnCompleted.Invoke();
            }
        }

        public override void ResetPuzzle()
        {
            completionRaised = false;
            chargingWire = null;
            foreach (ProjectionPowerNode node in Nodes) if (node != null) node.ResetNode();
            foreach (ProjectionWire wire in Wires) if (wire != null) wire.ResetWire();
        }
    }
}
