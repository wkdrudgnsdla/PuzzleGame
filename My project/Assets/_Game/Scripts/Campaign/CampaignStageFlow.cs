using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace PuzzleGame
{
    /// <summary>Advances completed campaign scenes and supplies brief prompts only in the tutorial.</summary>
    public sealed class CampaignStageFlow : MonoBehaviour
    {
        public ProjectionPrototype Prototype;
        public string NextSceneName;
        [Min(0)] public int StageNumber;
        public bool IsTutorial;
        public TMP_Text TutorialText;
        public ProjectionPuzzleMechanic[] TutorialMechanics = new ProjectionPuzzleMechanic[0];
        public TMP_Text ClearText;

        private bool loadingNext;
        private bool finalClearShown;
        private string clearPrompt, dimension, orbit, movementHint, horizontalKeys;

        private bool IsFinalStage => string.IsNullOrWhiteSpace(NextSceneName);

        private void Start()
        {
            if (Prototype == null) Prototype = GetComponent<ProjectionPrototype>();
            GamePreferences.RecordCampaignVisit();
            if (Prototype != null && Prototype.Hud != null)
            {
                if (ClearText == null) ClearText = Prototype.Hud.ClearText;
                Prototype.Hud.ConfigureCampaignStage(StageNumber, IsFinalStage);
                if (IsTutorial && TutorialText != null)
                    Prototype.Hud.GetComponent<ProjectionHudLayout>()?.SetTutorialHint(TutorialText);
            }
            RefreshInputLabels();
            if (TutorialText != null) TutorialText.gameObject.SetActive(IsTutorial);
        }

        internal void RefreshInputLabels()
        {
            if (!IsFinalStage) clearPrompt = $"{GameInput.KeyName("Continue")} 키로 다음 스테이지 이동";
            if (IsTutorial)
            {
                dimension = GameInput.KeyName("SwitchDimension");
                orbit = GameInput.KeyName("Orbit");
                movementHint = $"{GameInput.MoveKeys} 이동 · {GameInput.KeyName("Jump")} 점프 · " +
                    $"{GameInput.KeyName("Pan")} 드래그로 화면 이동 · 휠 줌 · {orbit} 드래그로 회전";
                horizontalKeys = $"{GameInput.KeyName("Move", 3)} / {GameInput.KeyName("Move", 4)}";
            }
        }

        private void Update()
        {
            if (Prototype == null || !Prototype.isActiveAndEnabled || !Prototype.HasWon || loadingNext || IsFinalStage) return;
            if (GameInput.Action("Continue").WasPressedThisFrame()) Continue();
        }

        private void LateUpdate()
        {
            if (Prototype == null || !Prototype.isActiveAndEnabled) return;
            if (Prototype.HasWon)
            {
                if (IsFinalStage)
                {
                    if (!finalClearShown && Prototype.Hud != null)
                    {
                        CampaignClearDialog.Show(Prototype.Hud, () => LoadScene(MainMenuController.ScenePath));
                        finalClearShown = true;
                        Prototype.enabled = false;
                        if (ClearText != null) ClearText.gameObject.SetActive(false);
                    }
                }
                else if (ClearText != null)
                {
                    SetText(ClearText, clearPrompt);
                    ClearText.gameObject.SetActive(true);
                }
            }
            if (IsTutorial && TutorialText != null)
            {
                TutorialText.gameObject.SetActive(!Prototype.HasWon);
                if (!Prototype.HasWon) SetText(TutorialText, TutorialHint());
            }
        }

        /// <summary>Can also be bound to an authored button. Incomplete stages cannot be skipped.</summary>
        public void Continue()
        {
            if (Prototype == null || !Prototype.isActiveAndEnabled || !Prototype.HasWon || IsFinalStage) return;
            LoadScene(NextSceneName);
        }

        private void LoadScene(string scene)
        {
            if (loadingNext) return;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError("Campaign destination is not registered in Build Settings: " + scene, this);
                return;
            }
            loadingNext = true;
            SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
        }

        private string TutorialHint()
        {
            ProjectionPuzzleMechanic next = NearbyTutorialMechanic();
            if (next is ProjectionKeyPuzzle key)
            {
                if (Prototype.Is2D)
                    return key.IsAligned ? $"완성된 열쇠에 몸을 닿게 하여 획득하세요.\n{dimension} 키로 3D 복귀"
                        : $"{dimension} 키로 3D에 돌아가 열쇠 조각을 먼저 맞추세요.\n{orbit} 드래그로 시점 조절";
                return $"{movementHint}\n열쇠 조각을 맞추고 {dimension} 키로 전환한 뒤 2D에서 열쇠에 닿으세요.";
            }
            if (next is ProjectionRailSwitch rail)
            {
                if (Prototype.Is2D && !rail.IsAligned)
                    return $"레일 끝이 아직 이어지지 않았습니다.\n{dimension} 키로 돌아가 레일 끝을 먼저 맞추세요.";
                return Prototype.Is2D
                    ? Prototype.IsTopDown2D
                        ? $"스위치 진행 방향의 뒤에서 이동 키로 미세요.\n모퉁이에서는 새 방향의 뒤로 돌아서 미세요 · {dimension} 키로 3D 복귀"
                        : $"초록 스위치를 몸으로 밀어 노란 끝점까지 보내세요.\n{horizontalKeys} 밀기 · {dimension} 키로 3D 복귀"
                    : rail.AllowCornerJoints
                        ? $"위를 내려다보며 레일 끝을 맞추고 {dimension} 키로 탑뷰에 들어가세요.\n상하 40° 이상에서 이동 키로 꺾인 선로를 따라 미세요."
                        : $"다음 발판의 레일 끝을 맞추고 {dimension} 키로 2D에 들어가세요.\n초록 스위치를 몸으로 밀어 노란 끝점까지 보내세요.";
            }
            if (next is ProjectionPowerNetwork power)
            {
                bool connected = false;
                var frame = new ProjectionFrame(Vector3.zero, Prototype.YawDegrees, Prototype.PitchDegrees);
                foreach (ProjectionWire wire in power.Wires)
                {
                    if (wire == null || !wire.IsValid || wire.Target.IsPowered) continue;
                    if (wire.Source.IsPowered && wire.CanConnect(frame)) connected = true;
                }
                bool hasPower = false;
                foreach (ProjectionWire wire in power.Wires)
                    if (wire != null && wire.IsValid && wire.Source.IsPowered) hasPower = true;
                if (!hasPower)
                    return "전원이 꺼져 있습니다.\n초록 레일 스위치를 노란 끝점까지 밀어 전원을 켜세요.";
                if (Prototype.Is2D && !connected)
                    return "전선 끝이 아직 떨어져 있습니다.\nR로 돌아가 좌우·상하 각도를 맞추세요.";
                return Prototype.Is2D
                    ? "파란 에너지가 전선 끝까지 차오르면 다음 전원이 켜집니다.\n발판을 옮기지 않아도 충전됩니다 · 미니맵으로 위치를 확인하세요."
                    : "어느 발판에서든 파란 전원의 전선 끝을 맞추고 R을 누르세요.\n충전 후 다른 각도를 맞춰 다음 전원으로 이어 보내세요.";
            }
            return $"모든 장치를 익혔습니다.\n{dimension} 키로 3D에 돌아가 초록 출구에 도착하세요.";
        }

        private ProjectionPuzzleMechanic NearbyTutorialMechanic()
        {
            ProjectionPuzzleMechanic nearest = null;
            float bestDistance = float.PositiveInfinity;
            foreach (ProjectionPuzzleMechanic mechanic in TutorialMechanics)
            {
                if (mechanic == null || mechanic.IsComplete) continue;
                float distance = float.PositiveInfinity;
                if (mechanic is ProjectionKeyPuzzle key && key.FirstJoin != null)
                    distance = TutorialDistance(key.FirstJoin.position);
                else if (mechanic is ProjectionRailSwitch rail)
                    distance = TutorialDistance(rail.SliderWorldPosition);
                else if (mechanic is ProjectionPowerNetwork power)
                    foreach (ProjectionWire wire in power.Wires)
                    {
                        if (wire == null || !wire.IsValid || wire.Target.IsPowered) continue;
                        float wireDistance = TutorialDistance(wire.Source.transform.position);
                        distance = Mathf.Min(distance, wireDistance);
                    }
                if (distance < bestDistance) { bestDistance = distance; nearest = mechanic; }
            }
            return nearest;
        }

        private float TutorialDistance(Vector3 position)
        {
            if (!Prototype.Is2D) return (position - Prototype.PlayerWorldFoot).sqrMagnitude;
            var frame = new ProjectionFrame(Vector3.zero, Prototype.YawDegrees, Prototype.PitchDegrees);
            return (frame.Project(position) - Prototype.PlayerProjectedFoot).sqrMagnitude;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target.text != value) target.text = value;
        }
    }
}
