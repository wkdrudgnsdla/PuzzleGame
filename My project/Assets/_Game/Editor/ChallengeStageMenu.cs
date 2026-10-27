using UnityEditor;
using UnityEditor.SceneManagement;

namespace PuzzleGame.Editor
{
    public static class ChallengeStageMenu
    {
        [MenuItem("Puzzle Game/Stages/01 Relay Bend")]
        private static void OpenRelayBend() => Open("Stage01_RelayBend");

        [MenuItem("Puzzle Game/Stages/02 Spiral Ascent")]
        private static void OpenSpiralAscent() => Open("Stage02_SpiralAscent");

        [MenuItem("Puzzle Game/Stages/03 Serpentine")]
        private static void OpenSerpentine() => Open("Stage03_Serpentine");

        [MenuItem("Puzzle Game/Stages/04 Crossing Heights")]
        private static void OpenCrossingHeights() => Open("Stage04_CrossingHeights");

        [MenuItem("Puzzle Game/Stages/05 Long Traverse")]
        private static void OpenLongTraverse() => Open("Stage05_LongTraverse");

        private static void Open(string sceneName)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Stages/" + sceneName + ".unity", OpenSceneMode.Single);
        }
    }
}
