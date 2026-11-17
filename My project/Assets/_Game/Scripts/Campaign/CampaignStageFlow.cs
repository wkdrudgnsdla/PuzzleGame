using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

namespace PuzzleGame
{
    /// <summary>Advances completed campaign scenes and runs the tutorial in its own scene.</summary>
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
        private CampaignClearDialog clearDialog;
        private InputAction continueInput;
        private TutorialLessonFlow tutorial;

        internal bool IsFinalStage => string.IsNullOrWhiteSpace(NextSceneName);

        private void Start()
        {
            if (Prototype == null) Prototype = GetComponent<ProjectionPrototype>();
            if (gameObject.scene == SceneManager.GetActiveScene())
                GamePreferences.RecordCampaignVisit(gameObject.scene.name);
            if (Prototype != null && Prototype.Hud != null)
            {
                if (ClearText == null) ClearText = Prototype.Hud.ClearText;
                Prototype.Hud.ConfigureCampaignStage(StageNumber);
                if (IsTutorial && TutorialText != null)
                {
                    Prototype.Hud.GetComponent<ProjectionHudLayout>()?.SetTutorialHint(TutorialText);
                    tutorial = new TutorialLessonFlow(Prototype, TutorialText, TutorialMechanics);
                }
            }
            RefreshInputLabels();
            if (TutorialText != null) TutorialText.gameObject.SetActive(false);
        }

        internal void RefreshInputLabels() => tutorial?.RefreshInputLabels();

        internal void ResetTutorial() => tutorial?.Reset();

        private void OnDisable()
        {
            tutorial?.DismissExplanation();
            continueInput?.Disable();
        }

        private void LateUpdate()
        {
            if (gameObject.scene != SceneManager.GetActiveScene() || loadingNext) return;
            if (clearDialog != null)
            {
                clearDialog.HandleContinue(continueInput.WasPressedThisFrame());
                return;
            }

            tutorial?.Tick();
            if (Prototype == null || !Prototype.isActiveAndEnabled || !Prototype.HasWon || Prototype.Hud == null) return;
            Prototype.SuspendGameplay();
            if (ClearText != null) ClearText.gameObject.SetActive(false);
            clearDialog = new CampaignClearDialog(Prototype.Hud, IsFinalStage ? null : Continue, Replay, SelectStage);
            continueInput = GameInput.Action("Continue");
            continueInput.Enable();
        }

        /// <summary>Can also be bound to an authored button. Incomplete stages cannot be skipped.</summary>
        public void Continue()
        {
            if (clearDialog == null || IsFinalStage) return;
            LoadScene(NextSceneName, SoundCue.UiConfirm);
        }

        private void Replay() => LoadScene(gameObject.scene.path, SoundCue.PlayerRestart);

        private void SelectStage() => LoadScene(MainMenuController.ScenePath, SoundCue.UiBack, openStageSelect: true);

        private void LoadScene(string scene, SoundCue cue, bool openStageSelect = false)
        {
            if (loadingNext) return;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError("Campaign destination is not registered in Build Settings: " + scene, this);
                return;
            }
            loadingNext = true;
            if (openStageSelect) MainMenuController.SelectStageOnNextMenuOpen(StageNumber);
            GameAudioEvents.Play(cue);
            SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
        }
    }
}
