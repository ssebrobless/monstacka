using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonStacka.Core
{
    public sealed class MonStackaAudioController : MonoBehaviour
    {
        [Serializable]
        public struct PieceAudioBank
        {
            public PieceType PieceType;
            public AudioClip[] ImpactClips;
            public AudioClip[] NeutralClips;
        }

        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField] private PieceAudioBank[] pieceBanks = Array.Empty<PieceAudioBank>();
        [SerializeField] private float musicVolume = 0.20f;
        [SerializeField] private float sfxVolume = 0.90f;
        [SerializeField] private bool playMusicOnAwake = true;

        private readonly Dictionary<PieceType, PieceAudioBank> banks = new();
        private readonly Dictionary<PieceType, int> impactIndexes = new();
        private readonly Dictionary<PieceType, int> neutralIndexes = new();
        private AudioSource musicSource;
        private AudioSource sfxSource;
        private AudioSource neutralSource;

        private void Awake()
        {
            banks.Clear();
            foreach (var bank in pieceBanks)
            {
                banks[bank.PieceType] = bank;
            }

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            musicSource.volume = musicVolume;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.volume = sfxVolume;

            neutralSource = gameObject.AddComponent<AudioSource>();
            neutralSource.playOnAwake = false;
            neutralSource.loop = false;
            neutralSource.spatialBlend = 0f;
            neutralSource.volume = sfxVolume;

            if (playMusicOnAwake)
            {
                PlayMusic();
            }
        }

        private void Update()
        {
            ApplySettings();
        }

        public void PlayMusic()
        {
            if (!backgroundMusic || !musicSource)
            {
                return;
            }

            if (musicSource.clip != backgroundMusic)
            {
                musicSource.clip = backgroundMusic;
            }

            if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }

            ApplySettings();
        }

        public void PlayMonsterImpact(PieceType pieceType)
        {
            if (!MonStackaAppState.SfxEnabled || !sfxSource || !banks.TryGetValue(pieceType, out var bank))
            {
                return;
            }

            var clip = NextClip(bank.ImpactClips, impactIndexes, pieceType);
            if (clip)
            {
                neutralSource?.Stop();
                sfxSource.PlayOneShot(clip, GetSfxVolume());
            }
        }

        public void PlayMonsterPreview(PieceType pieceType)
        {
            if (!MonStackaAppState.SfxEnabled || !neutralSource || !banks.TryGetValue(pieceType, out var bank))
            {
                return;
            }

            var clip = NextClip(bank.NeutralClips, neutralIndexes, pieceType);
            if (!clip)
            {
                return;
            }

            neutralSource.Stop();
            neutralSource.clip = clip;
            neutralSource.time = 0f;
            neutralSource.volume = GetSfxVolume();
            neutralSource.Play();
        }

        public void StopMonsterPreview()
        {
            if (!neutralSource)
            {
                return;
            }

            neutralSource.Stop();
            neutralSource.clip = null;
        }

        public void PlayUiClick()
        {
            if (!sfxSource)
            {
                return;
            }

            if (MonStackaAppState.SfxEnabled)
            {
                sfxSource.pitch = 1f;
                sfxSource.PlayOneShot(CreateClickClip(), 0.75f * GetSfxVolume());
            }
        }

        private void ApplySettings()
        {
            if (musicSource)
            {
                musicSource.volume = MonStackaAppState.MusicEnabled ? GetMusicVolume() : 0f;
            }

            if (sfxSource)
            {
                sfxSource.volume = GetSfxVolume();
            }

            if (neutralSource)
            {
                neutralSource.volume = GetSfxVolume();
                if (!MonStackaAppState.SfxEnabled && neutralSource.isPlaying)
                {
                    neutralSource.Stop();
                }
            }
        }

        private float GetMusicVolume() => Mathf.Clamp01(MonStackaAppState.MusicVolume / 100f);

        private float GetSfxVolume() => MonStackaAppState.SfxEnabled ? Mathf.Clamp01(MonStackaAppState.SfxVolume / 100f) : 0f;

        private static AudioClip NextClip(IReadOnlyList<AudioClip> clips, IDictionary<PieceType, int> indexes, PieceType pieceType)
        {
            if (clips == null || clips.Count == 0)
            {
                return null;
            }

            indexes.TryGetValue(pieceType, out var index);
            indexes[pieceType] = index + 1;
            return clips[Mathf.Abs(index) % clips.Count];
        }

        private static AudioClip clickClip;

        private static AudioClip CreateClickClip()
        {
            if (clickClip)
            {
                return clickClip;
            }

            const int sampleRate = 22050;
            const float durationSeconds = 0.04f;
            var samples = Mathf.CeilToInt(sampleRate * durationSeconds);
            var data = new float[samples];
            for (var index = 0; index < samples; index += 1)
            {
                var t = index / (float)sampleRate;
                var env = 1f - (index / (float)samples);
                data[index] = Mathf.Sin(t * Mathf.PI * 2f * 520f) * env * 0.18f;
            }

            clickClip = AudioClip.Create("MonStackaUiClick", samples, 1, sampleRate, false);
            clickClip.SetData(data, 0);
            return clickClip;
        }
    }
}
