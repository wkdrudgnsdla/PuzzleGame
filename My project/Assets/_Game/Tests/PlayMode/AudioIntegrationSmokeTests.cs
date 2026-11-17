using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PuzzleGame.Tests
{
    public sealed class AudioIntegrationSmokeTests
    {
        private const string FirstStage = "Assets/_Game/Scenes/Campaign/Stage01_FirstKey.unity";
        private Scene previousScene;

        [UnityTest]
        public IEnumerator AudioLibraryImportsAndMusicSurvivesMenuToStage()
        {
            previousScene = SceneManager.GetActiveScene();
            var library = Resources.Load<GameAudioLibrary>("GameAudioLibrary");
            Assert.That(library, Is.Not.Null);
            Assert.That(library.Music, Has.Length.EqualTo(6));
            Assert.That(library.Music.All(clip => clip != null && clip.samples == 2116800), Is.True);
            Assert.That(library.Sounds.Select(sound => sound.Cue), Is.EquivalentTo(Enum.GetValues(typeof(SoundCue))));
            Assert.That(library.Sounds.All(sound => sound.Clip != null), Is.True);

            GameObject audioRoot = GameObject.Find("Game Audio");
            Assert.That(audioRoot, Is.Not.Null);
            if (!SceneManager.GetSceneByPath(MainMenuController.ScenePath).isLoaded)
                yield return SceneManager.LoadSceneAsync(MainMenuController.ScenePath, LoadSceneMode.Additive);
            Assert.That(SceneManager.SetActiveScene(SceneManager.GetSceneByPath(MainMenuController.ScenePath)), Is.True);
            yield return null;
            yield return null;

            AudioSource source = audioRoot.GetComponents<AudioSource>().FirstOrDefault(candidate => candidate.clip != null);
            Assert.That(source, Is.Not.Null, "Music must be scheduled when the menu becomes active.");
            AudioClip scheduledIntro = source.clip;
            Assert.That(audioRoot.GetComponent<AudioListener>().enabled, Is.True);

            if (!SceneManager.GetSceneByPath(FirstStage).isLoaded)
                yield return SceneManager.LoadSceneAsync(FirstStage, LoadSceneMode.Additive);
            yield return null;
            Assert.That(SceneManager.SetActiveScene(SceneManager.GetSceneByPath(FirstStage)), Is.True);
            yield return null;

            Assert.That(GameObject.Find("Game Audio"), Is.SameAs(audioRoot));
            Assert.That(source.clip, Is.SameAs(scheduledIntro));
            Assert.That(audioRoot.GetComponent<AudioListener>().enabled, Is.True);
        }

        [UnityTearDown]
        public IEnumerator UnloadSmokeScenes()
        {
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            foreach (string path in new[] { FirstStage, MainMenuController.ScenePath })
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded && scene != previousScene)
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
