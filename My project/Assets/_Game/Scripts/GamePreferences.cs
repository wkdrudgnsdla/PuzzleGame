using UnityEngine;

namespace PuzzleGame
{
    public static class GamePreferences
    {
        private const string TutorialCompletedKey = "Foldway.Campaign.TutorialCompleted";
        private const string CampaignResumeSceneKey = "Foldway.Campaign.ResumeScene";
        private const string VolumeKey = "Foldway.Audio.MasterVolume";
        private const string CameraRotationSensitivityKey = "Foldway.Camera.RotationSensitivity";

        internal const float MinCameraRotationSensitivity = 0.25f;
        internal const float MaxCameraRotationSensitivity = 2f;

        private static bool TutorialCompleted => PlayerPrefs.GetInt(TutorialCompletedKey) == 1;

        public static string StartScene => TutorialCompleted
            ? "Stage01_FirstKey" : "Tutorial_CoreMechanics";

        public static string CampaignResumeScene => PlayerPrefs.GetString(CampaignResumeSceneKey);

        public static float MasterVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            set
            {
                AudioListener.volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
            }
        }

        public static float CameraRotationSensitivity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(CameraRotationSensitivityKey, 1f),
                MinCameraRotationSensitivity, MaxCameraRotationSensitivity);
            set => PlayerPrefs.SetFloat(CameraRotationSensitivityKey,
                Mathf.Clamp(value, MinCameraRotationSensitivity, MaxCameraRotationSensitivity));
        }

        public static void RecordCampaignVisit(string sceneName)
        {
            PlayerPrefs.SetString(CampaignResumeSceneKey, sceneName);
            PlayerPrefs.Save();
        }

        internal static void RecordTutorialCompletion()
        {
            if (TutorialCompleted) return;
            PlayerPrefs.SetInt(TutorialCompletedKey, 1);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RestoreVolume() => AudioListener.volume = MasterVolume;
    }
}
