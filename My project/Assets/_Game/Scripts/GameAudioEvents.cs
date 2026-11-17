using System;
using UnityEngine;

namespace PuzzleGame
{
    internal static class GameAudioEvents
    {
        internal static event Action<SoundCue> Played;
        internal static event Action<SoundCue, UnityEngine.Object, bool> LoopChanged;

        internal static void Play(SoundCue cue) => Played?.Invoke(cue);

        internal static void SetLoop(SoundCue cue, UnityEngine.Object owner, bool playing)
            => LoopChanged?.Invoke(cue, owner, playing);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Played = null;
            LoopChanged = null;
        }
    }
}
