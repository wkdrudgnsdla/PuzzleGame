using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleGame.Tests
{
    internal static class ProjectionTestWorld
    {
        private const string TutorialScene = "Assets/_Game/Scenes/Campaign/Tutorial_CoreMechanics.unity";

        internal static IEnumerator AssignPlayerPrefabs(ProjectionPrototype prototype)
        {
            Scene tutorial = default;
            void ReadPrefabReferences(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != TutorialScene)
                    return;

                tutorial = scene;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    // Keep the tutorial from starting a second gameplay world in this fixture.
                    root.SetActive(false);
                    var source = root.GetComponent<ProjectionPrototype>();
                    if (source == null)
                        continue;

                    prototype.Player3DPrefab = source.Player3DPrefab;
                    prototype.Player2DPrefab = source.Player2DPrefab;
                }
            }

            SceneManager.sceneLoaded += ReadPrefabReferences;
            try
            {
                yield return SceneManager.LoadSceneAsync(TutorialScene, LoadSceneMode.Additive);
            }
            finally
            {
                SceneManager.sceneLoaded -= ReadPrefabReferences;
            }

            Assert.That(tutorial.IsValid(), Is.True);
            yield return SceneManager.UnloadSceneAsync(tutorial);
            Assert.That(prototype.Player3DPrefab, Is.Not.Null);
            Assert.That(prototype.Player2DPrefab, Is.Not.Null);
        }

        internal static void SetInitialFrontView(ProjectionPrototype prototype)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ProjectionPrototype).GetField("initialYawDegrees", flags).SetValue(prototype, 0f);
            typeof(ProjectionPrototype).GetField("initialPitchDegrees", flags).SetValue(prototype, 0f);
        }
    }
}
