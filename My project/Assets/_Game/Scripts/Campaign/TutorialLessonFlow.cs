using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PuzzleGame
{
    internal sealed class TutorialLessonFlow
    {
        private enum Lesson { Walk, Jump, Camera, FindKey, CollectKey, FindRail, PushRail, ConnectWire, ChargeWire, Exit }

        private readonly ProjectionPrototype prototype;
        private readonly TMP_Text hint;
        private readonly ProjectionKeyPuzzle key;
        private readonly ProjectionRailSwitch rail;
        private readonly ProjectionPowerNetwork power;

        private Lesson lesson;
        private bool explanationPending = true;
        private bool explaining;
        private float previousTimeScale;
        private Vector3 startFoot;
        private float startYaw, startPitch;
        private bool jumped;
        private string move, jump, orbit, pan, dimension, horizontal;
        private (bool In2D, bool TopDown, bool KeyAligned, bool RailAligned, bool Charging) hintState;
        private bool hintNotCurrent = true;

        private Image backdrop;
        private TMP_Text title, explanation;
        private Button practice;
        private EventSystem events;

        internal TutorialLessonFlow(ProjectionPrototype prototype, TMP_Text hint, ProjectionPuzzleMechanic[] mechanics)
        {
            this.prototype = prototype;
            this.hint = hint;
            foreach (ProjectionPuzzleMechanic mechanic in mechanics)
            {
                if (mechanic is ProjectionKeyPuzzle keyPuzzle) key = keyPuzzle;
                if (mechanic is ProjectionRailSwitch railSwitch) rail = railSwitch;
                if (mechanic is ProjectionPowerNetwork network) power = network;
            }
        }

        internal void RefreshInputLabels()
        {
            move = GameInput.MoveKeys;
            jump = GameInput.KeyName("Jump");
            orbit = GameInput.KeyName("Orbit");
            pan = GameInput.KeyName("Pan");
            dimension = GameInput.KeyName("SwitchDimension");
            horizontal = $"{GameInput.KeyName("Move", 3)} / {GameInput.KeyName("Move", 4)}";
            hintNotCurrent = true;
        }

        internal void Reset()
        {
            DismissExplanation();
            lesson = Lesson.Walk;
            explanationPending = true;
            hint.gameObject.SetActive(false);
        }

        internal void Tick()
        {
            if (explaining || !prototype.isActiveAndEnabled || Time.timeScale <= 0f) return;
            if (prototype.HasWon)
            {
                hint.gameObject.SetActive(false);
                return;
            }

            if (!explanationPending && PracticeComplete())
            {
                lesson++;
                explanationPending = true;
            }
            if (explanationPending) ShowExplanation();
            else RefreshPracticeHint();
        }

        private bool PracticeComplete()
        {
            switch (lesson)
            {
                case Lesson.Walk:
                    Vector3 travel = prototype.PlayerWorldFoot - startFoot;
                    return !prototype.Is2D && new Vector2(travel.x, travel.z).sqrMagnitude >= 0.09f;
                case Lesson.Jump:
                    if (prototype.Is2D) return false;
                    if (!prototype.IsGrounded && GameInput.Action("Jump").WasPressedThisFrame()) jumped = true;
                    return jumped && prototype.IsGrounded;
                case Lesson.Camera:
                    return !prototype.Is2D && (Mathf.Abs(Mathf.DeltaAngle(startYaw, prototype.YawDegrees)) >= 6f ||
                        Mathf.Abs(startPitch - prototype.PitchDegrees) >= 6f);
                case Lesson.FindKey:
                    return key == null || key.IsComplete || key.IsAligned;
                case Lesson.CollectKey:
                    return key == null || key.IsComplete;
                case Lesson.FindRail:
                    return rail == null || rail.IsComplete || IsNearAlignedRail();
                case Lesson.PushRail:
                    return rail == null || rail.IsComplete;
                case Lesson.ConnectWire:
                    return power == null || power.IsComplete || HasChargingWire();
                case Lesson.ChargeWire:
                    return power == null || power.IsComplete;
                default:
                    return false;
            }
        }

        private bool HasChargingWire()
        {
            foreach (ProjectionWire wire in power.Wires)
                if (wire != null && wire.IsCharging) return true;
            return false;
        }

        private bool IsNearAlignedRail()
        {
            if (!prototype.IsTopDown2D || !rail.IsAligned) return false;
            var view = new ProjectionFrame(Vector3.zero, prototype.YawDegrees, prototype.PitchDegrees);
            Vector2 slider = view.Project(rail.SliderWorldPosition);
            return (slider - prototype.PlayerProjectedFoot).sqrMagnitude <= 4f;
        }

        private void ShowExplanation()
        {
            if (backdrop == null) BuildExplanation();
            (string caption, string instructions) = LessonExplanation();
            title.text = $"{(int)lesson + 1} / {(int)Lesson.Exit + 1}   {caption}";
            explanation.text = instructions;
            hint.gameObject.SetActive(false);
            explanationPending = false;
            explaining = true;
            previousTimeScale = Time.timeScale;
            prototype.SuspendGameplay();
            Time.timeScale = 0f;
            backdrop.transform.SetAsLastSibling();
            backdrop.gameObject.SetActive(true);
            events.SetSelectedGameObject(practice.gameObject);
            GameAudioEvents.Play(SoundCue.UiTutorialCue);
        }

        private void BeginPractice()
        {
            if (!explaining) return;
            GameAudioEvents.Play(SoundCue.UiConfirm);
            startFoot = prototype.PlayerWorldFoot;
            startYaw = prototype.YawDegrees;
            startPitch = prototype.PitchDegrees;
            jumped = false;
            DismissExplanation();
            hintNotCurrent = true;
            RefreshPracticeHint();
        }

        internal void DismissExplanation()
        {
            if (!explaining) return;
            explaining = false;
            Time.timeScale = previousTimeScale;
            if (events != null && events.currentSelectedGameObject == practice.gameObject)
                events.SetSelectedGameObject(null);
            backdrop.gameObject.SetActive(false);
            if (prototype != null && prototype.gameObject.activeInHierarchy) prototype.enabled = true;
        }

        private (string, string) LessonExplanation()
        {
            switch (lesson)
            {
                case Lesson.Walk:
                    return ("발판에서 이동하기", $"{move} 키로 발판 위에서 움직이세요.\n3D에서는 바라보는 방향을 기준으로 이동합니다.");
                case Lesson.Jump:
                    return ("점프하기", $"발판에 서서 {jump} 키를 누르고 다시 착지하세요.\n발판 밖으로 떨어지면 최근 복귀 지점으로 돌아옵니다.");
                case Lesson.Camera:
                    return ("시점 돌리기", $"{orbit}로 드래그하면 시점이 돌아갑니다. 앞뒤로 떨어진 물체도 한 화면에서 이어 보일 수 있습니다.\n{pan} 드래그로 화면을 옮기고, 마우스 휠로 확대·축소하세요.");
                case Lesson.FindKey:
                    return ("열쇠 조각 맞추기", $"{orbit}로 시점을 돌려 앞에 있는 열쇠의 고리와 이 부분을 맞추세요.\n두 조각이 유효하게 이어지면 초록색으로 바뀝니다.");
                case Lesson.CollectKey:
                    return ("2D에서 열쇠 획득하기", $"조각이 맞은 상태에서 {dimension} 키로 차원을 바꾸세요.\n횡스크롤 2D에서는 {horizontal} 키로 움직여 열쇠에 몸을 닿게 하세요.");
                case Lesson.FindRail:
                    return ("레일 찾기와 탑뷰 전환", $"{dimension} 키로 3D에 복귀하고 미니맵에서 레일을 찾으세요. 발판을 화면에서 이어 2D로 건너가세요.\n상하 각도를 약 55°로 맞춰 선로가 초록색이 되면 {dimension} 키로 탑뷰 2D에 들어가세요.");
                case Lesson.PushRail:
                    return ("레일 스위치 밀기", $"탑뷰에서는 {move} 키로 발판 위를 움직입니다.\n스위치 뒤에서 몸으로 밀고, 모퉁이에서는 새 방향의 뒤로 돌아 노란 끝점까지 미세요.");
                case Lesson.ConnectWire:
                    return ("전원에서 전선 잇기", $"레일 스위치가 전원을 켰습니다. {dimension} 키로 3D에 돌아가 전선의 두 절단면을 맞추세요.\n선이 초록색이면 현재 발판에서 {dimension} 키로 2D에 들어가세요.");
                case Lesson.ChargeWire:
                    return ("충전 지켜보기", "2D에서 전선이 이어지면 파란 전류가 차오릅니다.\n전류가 끝에 도달해 전구가 켜질 때까지 연결을 유지하세요.");
                default:
                    return ("초록 출구 찾기", $"미니맵에서 출구를 찾고, 3D에서 발판끼리 이어 보이는 방향을 맞추세요.\n{dimension} 키로 2D에 들어가 건넌 뒤, 발판 안쪽에서 3D로 돌아와 출구에 닿으세요.");
            }
        }

        private void RefreshPracticeHint()
        {
            var state = (prototype.Is2D, prototype.IsTopDown2D, key != null && key.IsAligned,
                rail != null && rail.IsAligned, lesson == Lesson.ChargeWire && power != null && HasChargingWire());
            if (hintNotCurrent || state != hintState)
            {
                hintState = state;
                hint.text = PracticeHint();
                hintNotCurrent = false;
            }
            if (!hint.gameObject.activeSelf) hint.gameObject.SetActive(true);
        }

        private string PracticeHint()
        {
            string returnTo3D = $"발판에 서서 {dimension} 키로 3D에 돌아오세요.";
            switch (lesson)
            {
                case Lesson.Walk:
                    return hintState.In2D ? returnTo3D : $"{move} 키로 발판 위를 움직이세요.";
                case Lesson.Jump:
                    return hintState.In2D ? returnTo3D : $"{jump} 키로 점프한 뒤 착지하세요.";
                case Lesson.Camera:
                case Lesson.FindKey:
                    return hintState.In2D ? returnTo3D : lesson == Lesson.Camera
                        ? $"{orbit} 드래그로 시점을 돌리세요."
                        : $"{orbit} 드래그로 열쇠의 두 조각을 맞추세요.";
                case Lesson.CollectKey:
                    if (hintState.In2D && hintState.KeyAligned)
                        return $"{(hintState.TopDown ? move : horizontal)} 키로 이동해 열쇠에 닿으세요.";
                    if (hintState.In2D) return returnTo3D;
                    return hintState.KeyAligned ? $"{dimension} 키로 2D에 들어가 열쇠를 획득하세요."
                        : $"{orbit} 드래그로 열쇠를 맞춘 뒤 {dimension} 키로 2D에 들어가세요.";
                case Lesson.FindRail:
                    if (hintState.TopDown && hintState.RailAligned)
                        return $"스위치 쪽으로 다가가세요. 길이 끊기면 {dimension} 키로 복귀해 먼저 발판을 이으세요.";
                    if (hintState.In2D) return $"레일이 있는 발판으로 건너고 {dimension} 키로 3D에 복귀하세요.";
                    return $"레일까지 갈 발판을 미니맵에서 찾으세요. 레일은 상하 약 55°에서 이어집니다.";
                case Lesson.PushRail:
                    if (!hintState.In2D) return $"레일을 맞춘 뒤 {dimension} 키로 탑뷰 2D에 들어가세요.";
                    if (!hintState.TopDown || !hintState.RailAligned) return returnTo3D;
                    return "스위치 진행 방향의 뒤에서 노란 끝점까지 밀어 주세요.";
                case Lesson.ConnectWire:
                    return hintState.In2D ? returnTo3D
                        : $"전선 두 끝이 초록색으로 연결되면 {dimension} 키로 2D에 들어가세요.";
                case Lesson.ChargeWire:
                    if (!hintState.In2D) return $"전선을 잇고 {dimension} 키로 2D에 들어가 충전하세요.";
                    return hintState.Charging ? "파란 전류가 전선 끝까지 차올라 전구가 켜질 때까지 기다리세요."
                        : $"{returnTo3D} 전선 절단면을 다시 맞춰 주세요.";
                default:
                    return hintState.In2D ? $"출구 발판 안쪽까지 이동하고 {dimension} 키로 3D에 돌아오세요."
                        : "미니맵에서 초록 출구를 찾고, 3D 상태로 출구에 닿으세요.";
            }
        }

        private void BuildExplanation()
        {
            ProjectionPrototypeHud hud = prototype.Hud;
            backdrop = OverlayUi.CreateBackdrop(hud, "Tutorial Lesson");
            backdrop.color = new Color(0f, 0f, 0f, 0.42f);
            Image panel = OverlayUi.CreateImage("Lesson Panel", backdrop.transform, OverlayUi.Card);
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.10f, 0.13f);
            panelRect.anchorMax = new Vector2(0.90f, 0.13f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(0f, 252f);

            title = OverlayUi.CreateLabel("Lesson Title", panel.transform, hud.KoreanFont,
                string.Empty, 28f, OverlayUi.Accent);
            PlaceText(title, 194f, 232f);
            explanation = OverlayUi.CreateLabel("Instructions", panel.transform, hud.KoreanFont,
                string.Empty, 23f, OverlayUi.Text);
            explanation.fontStyle = FontStyles.Normal;
            explanation.textWrappingMode = TextWrappingModes.Normal;
            explanation.enableAutoSizing = true;
            explanation.fontSizeMin = 17f;
            explanation.fontSizeMax = 23f;
            PlaceText(explanation, 68f, 185f);

            practice = OverlayUi.CreateButton("Begin Practice", panel.transform, hud.KoreanFont,
                "연습 시작", OverlayUi.Accent, OverlayUi.Card, 23f, out _);
            var buttonRect = (RectTransform)practice.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 36f);
            buttonRect.sizeDelta = new Vector2(180f, 44f);
            practice.navigation = new Navigation { mode = Navigation.Mode.None };
            practice.onClick.AddListener(BeginPractice);

            events = OverlayUi.GetOrCreateEventSystem(hud.transform);
        }

        private static void PlaceText(TMP_Text label, float bottom, float top)
        {
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.right;
            rect.offsetMin = new Vector2(24f, bottom);
            rect.offsetMax = new Vector2(-24f, top);
        }
    }
}
