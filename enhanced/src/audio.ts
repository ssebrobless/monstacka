import type { Settings } from './types';

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
  | 'go';

export class AudioManager {
  private context: AudioContext | null = null;
  private masterGain: GainNode | null = null;
  private sfxGain: GainNode | null = null;
  private musicGain: GainNode | null = null;
  private musicStarted = false;

  ensureReady(settings: Settings): void {
    if (!this.context) {
      this.context = new AudioContext();
      this.masterGain = this.context.createGain();
      this.sfxGain = this.context.createGain();
      this.musicGain = this.context.createGain();

      this.sfxGain.connect(this.masterGain);
      this.musicGain.connect(this.masterGain);
      this.masterGain.connect(this.context.destination);
    }

    if (this.context.state === 'suspended') {
      void this.context.resume();
    }

    this.syncSettings(settings);

    if (!this.musicStarted) {
      this.startAmbient();
    }
  }

  syncSettings(settings: Settings): void {
    if (!this.masterGain || !this.sfxGain || !this.musicGain) return;

    const muted = settings.muted;
    this.masterGain.gain.value = muted ? 0 : 1;
    this.sfxGain.gain.value = muted ? 0 : settings.sfxVolume / 100;
    this.musicGain.gain.value = muted ? 0 : (settings.musicVolume / 100) * 0.18;
  }

  play(cue: SoundCue, settings: Settings): void {
    this.ensureReady(settings);
    if (!this.context || !this.sfxGain || settings.muted) return;

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
      default:
        break;
    }
  }

  private startAmbient(): void {
    if (!this.context || !this.musicGain || this.musicStarted) return;

    const oscA = this.context.createOscillator();
    oscA.type = 'sine';
    oscA.frequency.value = 55;

    const oscB = this.context.createOscillator();
    oscB.type = 'triangle';
    oscB.frequency.value = 82.5;

    const gainA = this.context.createGain();
    gainA.gain.value = 0.12;

    const gainB = this.context.createGain();
    gainB.gain.value = 0.07;

    const lfo = this.context.createOscillator();
    lfo.type = 'sine';
    lfo.frequency.value = 0.18;
    const lfoGain = this.context.createGain();
    lfoGain.gain.value = 6;

    lfo.connect(lfoGain);
    lfoGain.connect(oscB.detune);

    oscA.connect(gainA);
    oscB.connect(gainB);
    gainA.connect(this.musicGain);
    gainB.connect(this.musicGain);

    oscA.start();
    oscB.start();
    lfo.start();

    this.musicStarted = true;
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
