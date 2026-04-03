import monstackaBgmUrl from './assets/audio/monstacka-bgm.wav';
import { MONSTER_AUDIO_URLS } from './assets/audio/monstos';
import type { PieceType, Settings } from './types';

export type SoundCue =
  | 'move'
  | 'softDrop'
  | 'rotate'
  | 'hold'
  | 'hardDrop'
  | 'lock'
  | 'lineClear'
  | 'topOut'
  | 'countdown'
  | 'go'
  | 'previewBeep';

export class AudioManager {
  private context: AudioContext | null = null;
  private masterGain: GainNode | null = null;
  private sfxGain: GainNode | null = null;
  private musicElement: HTMLAudioElement | null = null;
  private monsterBuffers = new Map<string, AudioBuffer>();
  private monsterBuffersLoaded = false;
  private activeNeutralSource: AudioBufferSourceNode | null = null;
  private activeNeutralGain: GainNode | null = null;
  private impactIndex: Record<PieceType, number> = { I: 0, O: 0, T: 0, S: 0, Z: 0, J: 0, L: 0 };
  private neutralIndex: Record<PieceType, number> = { I: 0, O: 0, T: 0, S: 0, Z: 0, J: 0, L: 0 };

  boot(settings: Settings): void {
    this.ensureMusicElement();
    this.syncSettings(settings);
    this.tryStartMusic();
  }

  ensureReady(settings: Settings): void {
    if (!this.context) {
      this.context = new AudioContext();
      this.masterGain = this.context.createGain();
      this.sfxGain = this.context.createGain();

      this.sfxGain.connect(this.masterGain);
      this.masterGain.connect(this.context.destination);
    }

    if (this.context.state === 'suspended') {
      void this.context.resume();
    }

    this.syncSettings(settings);

    if (!this.monsterBuffersLoaded && !this.monsterSoundsLoading) {
      void this.loadMonsterSounds();
    }
  }

  syncSettings(settings: Settings): void {
    this.ensureMusicElement();
    if (!this.masterGain || !this.sfxGain) {
      this.applyMusicSettings(settings);
      return;
    }

    this.masterGain.gain.value = 1;
    this.sfxGain.gain.value = settings.sfxEnabled ? settings.sfxVolume / 100 : 0;
    this.applyMusicSettings(settings);

    if (settings.musicEnabled) {
      this.tryStartMusic();
    }
  }

  play(cue: SoundCue, settings: Settings): void {
    this.ensureReady(settings);
    if (!this.context || !this.sfxGain || !settings.sfxEnabled) return;

    const now = this.context.currentTime;

    switch (cue) {
      case 'move':
        this.playTone('square', 220, 0.018, 0.028, now);
        break;
      case 'softDrop':
        this.playTone('sine', 170, 0.012, 0.02, now);
        break;
      case 'rotate':
        this.playTone('triangle', 330, 0.022, 0.03, now);
        break;
      case 'hold':
        this.playTone('triangle', 260, 0.05, 0.04, now);
        this.playTone('sine', 390, 0.05, 0.04, now + 0.01);
        break;
      case 'hardDrop':
        this.playTone('sawtooth', 95, 0.07, 0.05, now);
        break;
      case 'lock':
        this.playTone('square', 140, 0.05, 0.05, now);
        break;
      case 'lineClear':
        this.playTone('triangle', 420, 0.07, 0.05, now);
        this.playTone('triangle', 620, 0.06, 0.05, now + 0.035);
        break;
      case 'topOut':
        this.playTone('sawtooth', 130, 0.12, 0.08, now);
        this.playTone('sawtooth', 98, 0.12, 0.08, now + 0.08);
        break;
      case 'countdown':
        this.playTone('sine', 520, 0.05, 0.03, now);
        break;
      case 'go':
        this.playTone('triangle', 660, 0.08, 0.05, now);
        this.playTone('triangle', 880, 0.08, 0.05, now + 0.05);
        break;
      case 'previewBeep':
        this.playTone('sine', 640, 0.07, 0.035, now);
        break;
      default:
        break;
    }
  }

  private monsterSoundsLoading = false;

  async loadMonsterSounds(): Promise<void> {
    if (this.monsterBuffersLoaded || this.monsterSoundsLoading) return;
    this.monsterSoundsLoading = true;

    // Ensure AudioContext exists — caller should have triggered ensureReady() first
    if (!this.context) {
      this.context = new AudioContext();
      this.masterGain = this.context.createGain();
      this.sfxGain = this.context.createGain();
      this.sfxGain.connect(this.masterGain);
      this.masterGain.connect(this.context.destination);
    }

    if (this.context.state === 'suspended') {
      await this.context.resume().catch(() => {});
    }

    const ctx = this.context;
    const entries: Array<{ key: string; url: string }> = [];
    for (const [piece, urls] of Object.entries(MONSTER_AUDIO_URLS)) {
      urls.impact.forEach((url, i) => entries.push({ key: `${piece}:impact:${i}`, url }));
      urls.neutral.forEach((url, i) => entries.push({ key: `${piece}:neutral:${i}`, url }));
    }

    await Promise.all(
      entries.map(async ({ key, url }) => {
        try {
          const response = await fetch(url);
          const arrayBuffer = await response.arrayBuffer();
          const audioBuffer = await ctx.decodeAudioData(arrayBuffer);
          this.monsterBuffers.set(key, audioBuffer);
        } catch {
          // Silently skip failed loads — game still works with oscillator fallback
        }
      }),
    );

    this.monsterBuffersLoaded = true;
    this.monsterSoundsLoading = false;
  }

  playMonsterNeutral(pieceType: PieceType, settings: Settings, maxDurationSec = 5): void {
    this.ensureReady(settings);
    if (!this.context || !this.sfxGain || !settings.sfxEnabled) return;

    this.stopMonsterNeutral();

    const urls = MONSTER_AUDIO_URLS[pieceType].neutral;
    const idx = this.neutralIndex[pieceType] % urls.length;
    this.neutralIndex[pieceType] = idx + 1;

    const buffer = this.monsterBuffers.get(`${pieceType}:neutral:${idx}`);
    if (!buffer) return;

    const source = this.context.createBufferSource();
    source.buffer = buffer;
    const gain = this.context.createGain();
    gain.gain.value = 1;
    source.connect(gain);
    gain.connect(this.sfxGain);
    source.start(0);
    if (maxDurationSec < Infinity) {
      source.stop(this.context.currentTime + maxDurationSec);
    }
    source.onended = () => {
      if (this.activeNeutralSource === source) {
        this.activeNeutralSource = null;
        this.activeNeutralGain = null;
      }
    };
    this.activeNeutralSource = source;
    this.activeNeutralGain = gain;
  }

  get isNeutralPlaying(): boolean {
    return this.activeNeutralSource !== null;
  }

  stopMonsterNeutral(): void {
    if (this.activeNeutralSource) {
      try {
        this.activeNeutralSource.stop();
      } catch {
        // Already stopped
      }
      this.activeNeutralSource = null;
      this.activeNeutralGain = null;
    }
  }

  playMonsterImpact(pieceType: PieceType, settings: Settings): void {
    this.ensureReady(settings);
    if (!this.context || !this.sfxGain || !settings.sfxEnabled) return;

    this.stopMonsterNeutral();

    const urls = MONSTER_AUDIO_URLS[pieceType].impact;
    const idx = this.impactIndex[pieceType] % urls.length;
    this.impactIndex[pieceType] = idx + 1;

    const buffer = this.monsterBuffers.get(`${pieceType}:impact:${idx}`);
    if (!buffer) return;

    const source = this.context.createBufferSource();
    source.buffer = buffer;
    source.connect(this.sfxGain);
    source.start(0);
  }

  playMonstosPreview(pieceType: PieceType, settings: Settings): void {
    this.ensureReady(settings);
    if (!this.context || !this.sfxGain || !settings.sfxEnabled) return;

    // Use full neutral sound for preview (no time limit)
    const buffer = this.monsterBuffers.get(`${pieceType}:neutral:${this.neutralIndex[pieceType] % MONSTER_AUDIO_URLS[pieceType].neutral.length}`);
    if (buffer) {
      this.stopMonsterNeutral();
      const source = this.context.createBufferSource();
      source.buffer = buffer;
      const gain = this.context.createGain();
      gain.gain.value = 1;
      source.connect(gain);
      gain.connect(this.sfxGain);
      source.start(0);
      source.onended = () => {
        if (this.activeNeutralSource === source) {
          this.activeNeutralSource = null;
          this.activeNeutralGain = null;
        }
      };
      this.activeNeutralSource = source;
      this.activeNeutralGain = gain;
      this.neutralIndex[pieceType] += 1;
      return;
    }

    // Fallback to oscillator tones if buffers not loaded
    const now = this.context.currentTime;
    const patterns: Record<PieceType, Array<{ type: OscillatorType; frequency: number; delay: number; duration: number; amplitude: number }>> = {
      I: [
        { type: 'sine', frequency: 260, delay: 0, duration: 0.08, amplitude: 0.05 },
        { type: 'triangle', frequency: 340, delay: 0.06, duration: 0.08, amplitude: 0.045 },
        { type: 'sine', frequency: 220, delay: 0.12, duration: 0.12, amplitude: 0.04 },
      ],
      O: [
        { type: 'triangle', frequency: 280, delay: 0, duration: 0.06, amplitude: 0.05 },
        { type: 'triangle', frequency: 280, delay: 0.05, duration: 0.06, amplitude: 0.05 },
      ],
      T: [
        { type: 'sawtooth', frequency: 210, delay: 0, duration: 0.12, amplitude: 0.045 },
        { type: 'sine', frequency: 160, delay: 0.08, duration: 0.18, amplitude: 0.038 },
      ],
      S: [
        { type: 'square', frequency: 200, delay: 0, duration: 0.07, amplitude: 0.04 },
        { type: 'square', frequency: 250, delay: 0.04, duration: 0.07, amplitude: 0.04 },
        { type: 'sawtooth', frequency: 180, delay: 0.1, duration: 0.1, amplitude: 0.03 },
      ],
      Z: [
        { type: 'triangle', frequency: 180, delay: 0, duration: 0.08, amplitude: 0.04 },
        { type: 'triangle', frequency: 150, delay: 0.05, duration: 0.08, amplitude: 0.04 },
      ],
      J: [
        { type: 'sine', frequency: 300, delay: 0, duration: 0.05, amplitude: 0.04 },
        { type: 'sine', frequency: 360, delay: 0.045, duration: 0.05, amplitude: 0.04 },
        { type: 'triangle', frequency: 220, delay: 0.1, duration: 0.08, amplitude: 0.035 },
      ],
      L: [
        { type: 'sawtooth', frequency: 230, delay: 0, duration: 0.06, amplitude: 0.04 },
        { type: 'triangle', frequency: 190, delay: 0.06, duration: 0.08, amplitude: 0.038 },
        { type: 'sawtooth', frequency: 260, delay: 0.12, duration: 0.07, amplitude: 0.04 },
      ],
    };

    for (const note of patterns[pieceType]) {
      this.playTone(note.type, note.frequency, note.duration, note.amplitude, now + note.delay);
    }
  }

  private ensureMusicElement(): void {
    if (this.musicElement) {
      return;
    }

    const element = new Audio(monstackaBgmUrl);
    element.loop = true;
    element.preload = 'auto';
    element.setAttribute('playsinline', '');
    this.musicElement = element;
  }

  private applyMusicSettings(settings: Settings): void {
    if (!this.musicElement) {
      return;
    }

    this.musicElement.volume = settings.musicEnabled ? Math.max(0, Math.min(1, settings.musicVolume / 100)) : 0;
    this.musicElement.muted = !settings.musicEnabled;
    if (!settings.musicEnabled) {
      this.musicElement.pause();
    }
  }

  private tryStartMusic(): void {
    if (!this.musicElement) {
      return;
    }

    if (!this.musicElement.paused) {
      return;
    }

    const playPromise = this.musicElement.play();
    if (playPromise) {
      void playPromise.catch(() => {
        // Autoplay can be blocked in some webviews until the first user gesture.
      });
    }
  }

  private playTone(
    type: OscillatorType,
    frequency: number,
    duration: number,
    amplitude: number,
    startTime: number,
  ): void {
    if (!this.context || !this.sfxGain) return;

    const oscillator = this.context.createOscillator();
    const gain = this.context.createGain();

    oscillator.type = type;
    oscillator.frequency.setValueAtTime(frequency, startTime);
    gain.gain.setValueAtTime(0.0001, startTime);
    gain.gain.exponentialRampToValueAtTime(Math.max(0.001, amplitude), startTime + 0.01);
    gain.gain.exponentialRampToValueAtTime(0.0001, startTime + duration);

    oscillator.connect(gain);
    gain.connect(this.sfxGain);

    oscillator.start(startTime);
    oscillator.stop(startTime + duration + 0.02);
  }
}
