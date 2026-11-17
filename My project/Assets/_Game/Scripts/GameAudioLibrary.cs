using System;
using UnityEngine;

namespace PuzzleGame
{
    public enum SoundCue
    {
        CampaignClear = 0,
        CheckpointActivated = 1,
        DimensionBlocked = 2,
        DimensionTo2D = 3,
        DimensionTo3D = 4,
        ExitOpened = 5,
        KeyAligned = 6,
        KeyCollected = 7,
        PlayerFall = 8,
        PlayerJump = 9,
        PlayerLand = 10,
        PlayerRespawn = 11,
        PlayerRestart = 12,
        PlayerStepA = 13,
        PlayerStepB = 14,
        PlayerStepC = 15,
        RailAligned = 16,
        RailLocked = 17,
        RailSlideLoop = 18,
        StageClear = 19,
        UiBack = 20,
        UiConfirm = 21,
        UiMove = 22,
        UiPause = 23,
        UiResume = 24,
        UiTutorialCue = 25,
        WireAligned = 26,
        WireChargeLoop = 27,
        WireCharged = 28,
        WireChargeStart = 29,
    }

    [Serializable]
    public struct SoundEntry
    {
        public SoundCue Cue;
        public AudioClip Clip;
    }

    public sealed class GameAudioLibrary : ScriptableObject
    {
        public AudioClip[] Music;
        public SoundEntry[] Sounds;
    }
}
