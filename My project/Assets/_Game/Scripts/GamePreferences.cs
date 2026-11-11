using UnityEngine;

namespace PuzzleGame
{
    public static class GamePreferences
    {
        private const string HasPlayedKey = "Foldway.Campaign.HasPlayed";
        private const string VolumeKey = "Foldway.Audio.MasterVolume";

        public static string StartScene => PlayerPrefs.HasKey(HasPlayedKey)
            ? "Stage01_FirstKey" : "Tutorial_CoreMechanics";

        public static float MasterVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            set
            {
                AudioListener.volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
            }
        }

        public static void RecordCampaignVisit()
        {
            if (PlayerPrefs.HasKey(HasPlayedKey)) return;
            PlayerPrefs.SetInt(HasPlayedKey, 1);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RestoreVolume() => AudioListener.volume = MasterVolume;
    }
}
