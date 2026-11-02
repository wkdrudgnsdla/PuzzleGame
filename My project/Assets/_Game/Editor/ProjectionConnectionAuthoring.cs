using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleGame.Editor
{
    /// <summary>Places two connection anchors at different depths in a chosen answer view.</summary>
    public sealed class ProjectionConnectionAuthoring : EditorWindow
    {
        [SerializeField] private Transform firstAnchor;
        [SerializeField] private Transform secondAnchor;
        [SerializeField] private float answerYaw;
        [SerializeField] private float answerPitch;
        [SerializeField] private float depthGap = 3f;
        [SerializeField] private float positionTolerance = 0.4f;
        [SerializeField] private bool alignLocalRight;

        [MenuItem("Puzzle Game/Connection Layout Tool")]
        private static void Open()
        {
            GetWindow<ProjectionConnectionAuthoring>("접합점 배치").Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(390f, 430f);
            ReadSelection(false);
        }

        private void OnSelectionChange() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("정답 시점에서 접합점 맞추기", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("열쇠 접합점 · 레일 끝/시작 · 전선 끝/시작", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(6f);

            if (GUILayout.Button("선택한 접합점 2개 가져오기")) ReadSelection(true);
            firstAnchor = (Transform)EditorGUILayout.ObjectField("기준 접합점 A", firstAnchor, typeof(Transform), true);
            secondAnchor = (Transform)EditorGUILayout.ObjectField("이동할 접합점 B", secondAnchor, typeof(Transform), true);
            EditorGUILayout.LabelField("마지막으로 선택한 오브젝트가 B입니다.", EditorStyles.miniLabel);

            EditorGUILayout.Space(8f);
            answerYaw = EditorGUILayout.FloatField("정답 좌우 각도", answerYaw);
            answerPitch = EditorGUILayout.Slider("정답 상하 각도", answerPitch, -90f, 90f);
            depthGap = EditorGUILayout.FloatField("A → B 깊이 간격", depthGap);
            positionTolerance = Mathf.Max(0f, EditorGUILayout.FloatField("위치 오차 허용", positionTolerance));
            alignLocalRight = EditorGUILayout.ToggleLeft("두 접합점의 로컬 +X를 정답 시점의 오른쪽으로 정렬", alignLocalRight);

            bool valid = ValidateAnchors(out string problem);
            if (!valid)
                EditorGUILayout.HelpBox(problem, MessageType.Info);
            else
                DrawPreview();

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox("배치는 B 접합점의 위치만 옮깁니다. 부모 오브젝트와 다른 경로 점은 함께 이동하지 않습니다. "
                + "방향 정렬을 체크하면 A와 B의 회전도 변경합니다. 레일·전선의 나머지 경로는 따로 조정하세요.", MessageType.Info);

            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("정답 시점 기준으로 접합점 배치", GUILayout.Height(30f))) Arrange();
                if (GUILayout.Button("Scene 뷰에서 정답 시점 보기")) ShowAnswerView();
            }
        }

        private void ReadSelection(bool showMessage)
        {
            Transform[] selected = Selection.transforms;
            if (selected.Length != 2)
            {
                if (showMessage) ShowNotification(new GUIContent("Hierarchy에서 접합점 오브젝트 2개를 선택하세요."));
                return;
            }
            secondAnchor = Selection.activeTransform == selected[0] ? selected[0] : selected[1];
            firstAnchor = secondAnchor == selected[0] ? selected[1] : selected[0];
            Repaint();
        }

        private bool ValidateAnchors(out string problem)
        {
            if (firstAnchor == null || secondAnchor == null)
            {
                problem = "씬에 있는 서로 다른 접합점 두 개를 지정하세요.";
                return false;
            }
            if (firstAnchor == secondAnchor)
            {
                problem = "A와 B에는 서로 다른 접합점을 지정하세요.";
                return false;
            }
            if (EditorUtility.IsPersistent(firstAnchor) || EditorUtility.IsPersistent(secondAnchor))
            {
                problem = "프로젝트 에셋 대신 씬에 배치된 접합점을 지정하세요.";
                return false;
            }
            if (firstAnchor.IsChildOf(secondAnchor) || secondAnchor.IsChildOf(firstAnchor))
            {
                problem = "서로 상하위 관계가 없는 접합점을 지정하세요. 한 점을 옮길 때 다른 점도 움직이는 배치는 사용할 수 없습니다.";
                return false;
            }
            problem = null;
            return true;
        }

        private void DrawPreview()
        {
            var frame = new ProjectionFrame(Vector3.zero, answerYaw, answerPitch);
            float distance = Vector2.Distance(frame.Project(firstAnchor.position), frame.Project(secondAnchor.position));
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("현재 배치 미리보기", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("투영된 접합점 간격", distance.ToString("F3") + " 유닛");
            float depth = frame.Depth(secondAnchor.position) - frame.Depth(firstAnchor.position);
            EditorGUILayout.LabelField("현재 깊이 차이", depth.ToString("F3") + " 유닛");

            Vector2 a = ProjectDirection(frame, firstAnchor.right);
            Vector2 b = ProjectDirection(frame, secondAnchor.right);
            if (a.sqrMagnitude > 0.0025f && b.sqrMagnitude > 0.0025f)
            {
                float cosine = Mathf.Clamp01(Mathf.Abs(Vector2.Dot(a.normalized, b.normalized)));
                float angle = Mathf.Acos(cosine) * Mathf.Rad2Deg;
                EditorGUILayout.LabelField("로컬 +X 투영 방향 차이", angle.ToString("F1") + "° (양방향)");
            }
            else
                EditorGUILayout.LabelField("로컬 +X 투영 방향", "시선과 겹쳐 방향 판정이 어렵습니다.");

            EditorGUILayout.HelpBox(distance <= positionTolerance
                ? "현재 위치 간격은 허용 범위 안입니다. 실제 연결에는 각 기믹의 방향·형태 조건도 적용됩니다."
                : "현재 위치 간격이 허용 범위를 벗어납니다. 배치 버튼으로 정답 시점의 간격을 0으로 맞출 수 있습니다.",
                distance <= positionTolerance ? MessageType.Info : MessageType.Warning);
        }

        private void Arrange()
        {
            if (!ValidateAnchors(out _)) return;
            Quaternion answerRotation = Quaternion.Euler(answerPitch, answerYaw, 0f);
            Vector3 destination = firstAnchor.position + answerRotation * Vector3.forward * depthGap;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Arrange projection connection anchors");
            Undo.RecordObject(secondAnchor, "Move projection connection anchor");
            if (alignLocalRight)
            {
                Undo.RecordObject(firstAnchor, "Align projection connection anchor");
                firstAnchor.rotation = answerRotation;
                secondAnchor.rotation = answerRotation;
                RecordSceneChange(firstAnchor);
            }
            secondAnchor.position = destination;
            RecordSceneChange(secondAnchor);
            Undo.CollapseUndoOperations(undoGroup);
            SceneView.RepaintAll();
            Repaint();
        }

        private void ShowAnswerView()
        {
            if (!ValidateAnchors(out _)) return;
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null) view = GetWindow<SceneView>();
            view.rotation = Quaternion.Euler(answerPitch, answerYaw, 0f);
            view.orthographic = true;
            view.pivot = (firstAnchor.position + secondAnchor.position) * 0.5f;
            view.size = Mathf.Max(3f, Vector3.Distance(firstAnchor.position, secondAnchor.position) * 0.6f);
            view.Repaint();
        }

        private static Vector2 ProjectDirection(ProjectionFrame frame, Vector3 direction)
        {
            return new Vector2(Vector3.Dot(direction, frame.Right), Vector3.Dot(direction, frame.Up));
        }

        private static void RecordSceneChange(Transform anchor)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(anchor);
            EditorUtility.SetDirty(anchor);
            if (anchor.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
        }
    }
}
