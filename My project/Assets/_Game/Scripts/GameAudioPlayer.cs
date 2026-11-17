using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleGame
{
    internal sealed class GameAudioPlayer : MonoBehaviour
    {
        private const float MusicVolume = 0.68f;
        private const float LoopVolume = 0.53f;
        private const double MusicLookahead = 1.5;

        private readonly Dictionary<SoundCue, AudioClip> clips = new();
        private readonly Dictionary<SoundCue, float> lastPlayed = new();
        private readonly Dictionary<SoundCue, LoopPlayback> loops = new();
        private readonly AudioSource[] musicSources = new AudioSource[2];
        private AudioClip[] music;
        private AudioSource oneShots;
        private AudioListener listener;
        private bool gameScene;
        private int lastMusicIndex = -1;
        private int nextSource;
        private double nextTrackStart;
        private float currentMusicVolume, duckMusicUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var library = Resources.Load<GameAudioLibrary>("GameAudioLibrary");
            if (library == null) return;
            var root = new GameObject("Game Audio");
            DontDestroyOnLoad(root);
            root.AddComponent<GameAudioPlayer>().Initialize(library);
        }

        private void Initialize(GameAudioLibrary library)
        {
            var uniqueTracks = new HashSet<AudioClip>();
            music = Array.FindAll(library.Music, clip => clip != null && uniqueTracks.Add(clip));
            foreach (var sound in library.Sounds)
                if (sound.Clip != null) clips[sound.Cue] = sound.Clip;
            oneShots = NewSource(64);
            for (int i = 0; i < musicSources.Length; i++) musicSources[i] = NewSource(0);
            GameAudioEvents.Played += Play;
            GameAudioEvents.LoopChanged += SetLoop;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void Start() => RefreshScene(SceneManager.GetActiveScene());

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) RefreshScene(scene);
        }

        private void OnActiveSceneChanged(Scene previous, Scene current) => RefreshScene(current);

        private void RefreshScene(Scene scene)
        {
            if (!scene.IsValid()) return;
            bool play = scene.path.StartsWith("Assets/_Game/Scenes/", StringComparison.Ordinal);
            if (gameScene == play) return;
            gameScene = play;
            if (play)
            {
                if (listener == null) listener = gameObject.AddComponent<AudioListener>();
                listener.enabled = true;
                nextTrackStart = AudioSettings.dspTime + 0.2;
                return;
            }

            listener.enabled = false;
            oneShots.Stop();
            foreach (LoopPlayback loop in loops.Values)
            {
                loop.Owners.Clear();
                loop.Source.Stop();
                loop.Source.volume = 0f;
                loop.Paused = false;
            }
            foreach (AudioSource source in musicSources) source.Stop();
            currentMusicVolume = 0f;
        }

        private AudioSource NewSource(int priority)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = priority;
            return source;
        }

        private void Play(SoundCue cue)
        {
            if (!gameScene || !clips.TryGetValue(cue, out AudioClip clip)) return;
            float now = Time.unscaledTime;
            float interval = cue == SoundCue.KeyAligned || cue == SoundCue.RailAligned || cue == SoundCue.WireAligned
                ? 0.35f : 0.055f;
            if (lastPlayed.TryGetValue(cue, out float previous) && now - previous < interval) return;
            lastPlayed[cue] = now;
            if (cue == SoundCue.StageClear || cue == SoundCue.CampaignClear)
                duckMusicUntil = now + clip.length;
            oneShots.PlayOneShot(clip, 0.85f);
        }

        private void SetLoop(SoundCue cue, UnityEngine.Object owner, bool playing)
        {
            if (!gameScene || owner == null) return;
            if (!loops.TryGetValue(cue, out LoopPlayback loop))
            {
                if (!playing || !clips.TryGetValue(cue, out AudioClip clip)) return;
                var source = NewSource(96);
                source.volume = 0f;
                source.loop = true;
                source.clip = clip;
                loop = new LoopPlayback(source);
                loops.Add(cue, loop);
            }
            if (playing) loop.Owners.Add(owner);
            else loop.Owners.Remove(owner);
        }

        private void Update()
        {
            if (!gameScene) return;
            bool paused = Time.timeScale <= 0f;
            UpdateMusic(paused);
            foreach (LoopPlayback loop in loops.Values) UpdateLoop(loop, paused);
        }

        private void UpdateMusic(bool paused)
        {
            if (music.Length == 0) return;
            float targetVolume = MusicVolume * (Time.unscaledTime < duckMusicUntil ? 0.42f : paused ? 0.72f : 1f);
            currentMusicVolume = Mathf.MoveTowards(currentMusicVolume, targetVolume, 1.8f * Time.unscaledDeltaTime);
            foreach (AudioSource source in musicSources) source.volume = currentMusicVolume;

            double dspTime = AudioSettings.dspTime;
            if (nextTrackStart - dspTime > MusicLookahead) return;
            if (nextTrackStart <= dspTime) nextTrackStart = dspTime + 0.2;
            AudioClip track = PickNextTrack();
            AudioSource next = musicSources[nextSource];
            next.clip = track;
            next.PlayScheduled(nextTrackStart);
            double end = nextTrackStart + track.samples / (double)track.frequency;
            next.SetScheduledEndTime(end);
            nextTrackStart = end;
            nextSource = 1 - nextSource;
        }

        private AudioClip PickNextTrack()
        {
            if (music.Length == 1) return music[0];

            int index = UnityEngine.Random.Range(0, lastMusicIndex < 0 ? music.Length : music.Length - 1);
            if (lastMusicIndex >= 0 && index >= lastMusicIndex) index++;
            lastMusicIndex = index;
            return music[index];
        }

        private static void UpdateLoop(LoopPlayback loop, bool paused)
        {
            loop.Owners.RemoveWhere(owner => owner == null);
            bool needed = loop.Owners.Count > 0;
            AudioSource source = loop.Source;
            if (paused && needed)
            {
                if (source.isPlaying)
                {
                    source.Pause();
                    loop.Paused = true;
                }
                return;
            }

            if (needed)
            {
                if (loop.Paused) source.UnPause();
                else if (!source.isPlaying) source.Play();
                loop.Paused = false;
            }
            source.volume = Mathf.MoveTowards(source.volume, needed ? LoopVolume : 0f, 6f * Time.unscaledDeltaTime);
            if (!needed && source.volume <= 0f)
            {
                if (source.isPlaying || loop.Paused) source.Stop();
                loop.Paused = false;
            }
        }

        private void OnAudioConfigurationChanged(bool deviceChanged)
        {
            if (!gameScene) return;
            foreach (AudioSource source in musicSources) source.Stop();
            foreach (LoopPlayback loop in loops.Values) loop.Paused = false;
            nextTrackStart = AudioSettings.dspTime + 0.2;
        }

        private void OnDestroy()
        {
            GameAudioEvents.Played -= Play;
            GameAudioEvents.LoopChanged -= SetLoop;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        private sealed class LoopPlayback
        {
            internal readonly AudioSource Source;
            internal readonly HashSet<UnityEngine.Object> Owners = new();
            internal bool Paused;

            internal LoopPlayback(AudioSource source) => Source = source;
        }
    }
}
