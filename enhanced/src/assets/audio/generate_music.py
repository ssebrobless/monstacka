"""
Generates creepy/whimsical background music for Monstacka.
Pure Python - no external dependencies. Outputs a seamlessly looping WAV file.

Style: haunted music box meets monster carnival
"""

import wave
import struct
import math
import random
import os

SAMPLE_RATE = 44100
DURATION = 120  # 2 minutes
NUM_SAMPLES = SAMPLE_RATE * DURATION
CHANNELS = 1
SAMPLE_WIDTH = 2  # 16-bit
MAX_AMP = 32767

random.seed(42)  # reproducible

def sine(freq, t, phase=0.0):
    return math.sin(2 * math.pi * freq * t + phase)

def music_box_env(t_local, duration):
    """Sharp attack, slow decay - like a struck bell/music box tine."""
    if t_local < 0 or t_local > duration:
        return 0.0
    attack = min(t_local / 0.005, 1.0)
    decay = math.exp(-t_local * 4.0 / duration)
    return attack * decay

def pad_env(t_local, duration):
    """Slow swell pad envelope."""
    if t_local < 0 or t_local > duration:
        return 0.0
    attack = min(t_local / (duration * 0.3), 1.0)
    release_start = duration * 0.7
    if t_local > release_start:
        release = 1.0 - (t_local - release_start) / (duration * 0.3)
    else:
        release = 1.0
    return attack * release

def drone_env(t_local, duration):
    """Very slow fade in/out for bass drone."""
    if t_local < 0 or t_local > duration:
        return 0.0
    attack = min(t_local / (duration * 0.4), 1.0)
    release_start = duration * 0.6
    if t_local > release_start:
        return 1.0 - (t_local - release_start) / (duration * 0.4)
    return attack

# Note frequencies (Hz) - using A=440 tuning
def note_freq(midi_note):
    return 440.0 * (2.0 ** ((midi_note - 69) / 12.0))

# Scale: C minor pentatonic with chromatic passing tones for creepiness
# C=60, Eb=63, F=65, G=67, Bb=70
CREEPY_SCALE = [60, 63, 65, 67, 70, 72, 75, 77, 79, 82]
CHROMATIC_SPICE = [61, 64, 66, 68, 71, 73, 76, 78, 80, 83]

# Bass notes - low register drones
BASS_NOTES = [36, 39, 41, 43]  # C2, Eb2, F2, G2

# Build the composition as a list of note events
events = []

# === LAYER 1: Deep bass drone ===
# Shifts between notes every 8 beats (4 seconds each at ~120bpm)
drone_dur = 8.0
t = 0.0
bass_seq = [36, 43, 39, 41, 36, 39, 43, 36, 41, 39, 36, 43, 39, 41, 36]
while t < DURATION:
    idx = int(t / drone_dur) % len(bass_seq)
    events.append(('drone', t, drone_dur, bass_seq[idx], 0.12))
    # Add a fifth above for richness
    events.append(('drone', t, drone_dur, bass_seq[idx] + 7, 0.06))
    t += drone_dur

# === LAYER 2: Music box melody - the main creepy tune ===
# Compose a melody that loops seamlessly
melody_phrases = [
    # Phrase 1: descending minor with chromatic neighbor (eerie music box)
    [(72, 0.4), (71, 0.2), (72, 0.3), (70, 0.5), (67, 0.4), (65, 0.6), (63, 0.8), (None, 0.4)],
    # Phrase 2: ascending with tritone
    [(60, 0.3), (63, 0.3), (66, 0.5), (67, 0.3), (70, 0.4), (72, 0.6), (None, 0.6)],
    # Phrase 3: playful but unsettling jumps
    [(75, 0.2), (67, 0.2), (75, 0.2), (63, 0.4), (72, 0.3), (60, 0.5), (None, 0.6)],
    # Phrase 4: slow creepy descent
    [(79, 0.6), (78, 0.6), (77, 0.6), (76, 0.6), (75, 0.8), (None, 0.4)],
    # Phrase 5: quick scurrying
    [(60, 0.15), (63, 0.15), (65, 0.15), (67, 0.15), (70, 0.15), (72, 0.15), (75, 0.15), (77, 0.25), (None, 0.5)],
    # Phrase 6: haunting repeat
    [(72, 0.5), (None, 0.3), (72, 0.5), (None, 0.3), (70, 0.4), (67, 0.8), (None, 0.6)],
    # Phrase 7: wide intervals (monster stomping)
    [(48, 0.3), (72, 0.3), (48, 0.3), (75, 0.3), (48, 0.3), (70, 0.5), (None, 0.5)],
    # Phrase 8: twinkling high register
    [(84, 0.2), (82, 0.2), (84, 0.15), (79, 0.3), (82, 0.2), (77, 0.4), (75, 0.5), (None, 0.4)],
    # Phrase 9: chromatic creep
    [(60, 0.35), (61, 0.35), (62, 0.35), (63, 0.35), (64, 0.35), (65, 0.5), (None, 0.5)],
    # Phrase 10: resolution to beginning (seamless loop)
    [(67, 0.4), (70, 0.3), (72, 0.5), (75, 0.3), (72, 0.6), (None, 0.8)],
]

t = 0.5  # slight offset from drone start
phrase_idx = 0
while t < DURATION - 2:
    phrase = melody_phrases[phrase_idx % len(melody_phrases)]
    for note, dur in phrase:
        if t >= DURATION - 1:
            break
        if note is not None:
            events.append(('musicbox', t, dur * 1.8, note, 0.18))
            # Add a quiet octave-up ghost for shimmer
            if random.random() < 0.3:
                events.append(('musicbox', t + 0.05, dur * 1.2, note + 12, 0.04))
        t += dur
    t += random.uniform(0.3, 1.5)  # pause between phrases
    phrase_idx += 1

# === LAYER 3: Eerie pad chords ===
pad_chords = [
    [60, 63, 67],       # Cm
    [60, 66, 70],       # dim-ish
    [63, 67, 72],       # Eb
    [65, 68, 72],       # Fm(b5) cluster
    [60, 63, 67],       # Cm
    [58, 63, 67],       # Bb/Eb
    [60, 65, 70],       # suspended feel
    [60, 63, 67],       # Cm return
]
pad_dur = 15.0
t = 2.0
chord_idx = 0
while t < DURATION - 4:
    chord = pad_chords[chord_idx % len(pad_chords)]
    for note in chord:
        events.append(('pad', t, pad_dur, note + 12, 0.04))
    t += pad_dur
    chord_idx += 1

# === LAYER 4: Random creepy accents - whispers, clicks, high pings ===
t = 5.0
while t < DURATION - 1:
    if random.random() < 0.4:
        # High ping
        note = random.choice([84, 87, 89, 91, 94, 96])
        events.append(('musicbox', t, 0.8, note, random.uniform(0.02, 0.06)))
    if random.random() < 0.15:
        # Low rumble hit
        note = random.choice([30, 31, 33, 35])
        events.append(('drone', t, 2.0, note, 0.05))
    t += random.uniform(2.0, 6.0)

# === LAYER 5: Ticking clock (whimsical metronome) ===
tick_freq = 4200  # high click
t = 0.0
beat = 0
while t < DURATION:
    # Irregular rhythm: 3+3+2 pattern for unease
    pattern = [0.45, 0.45, 0.35, 0.45, 0.45, 0.35, 0.3, 0.3]
    dt = pattern[beat % len(pattern)]
    vol = 0.025 if beat % 4 == 0 else 0.012
    events.append(('tick', t, 0.02, 80, vol))
    t += dt
    beat += 1

print(f"Total events: {len(events)}")
print("Rendering audio...")

# === RENDER ===
samples = [0.0] * NUM_SAMPLES

for event_type, start, dur, midi_note, volume in events:
    freq = note_freq(midi_note)
    start_sample = int(start * SAMPLE_RATE)
    end_sample = min(int((start + dur) * SAMPLE_RATE), NUM_SAMPLES)

    if event_type == 'musicbox':
        for i in range(start_sample, end_sample):
            t_local = (i - start_sample) / SAMPLE_RATE
            env = music_box_env(t_local, dur)
            # Fundamental + slight harmonics for metallic timbre
            val = (sine(freq, t_local) * 0.6 +
                   sine(freq * 2, t_local) * 0.25 +
                   sine(freq * 3, t_local) * 0.1 +
                   sine(freq * 5.03, t_local) * 0.05)  # inharmonic partial for bell quality
            samples[i] += val * env * volume

    elif event_type == 'drone':
        for i in range(start_sample, end_sample):
            t_local = (i - start_sample) / SAMPLE_RATE
            env = drone_env(t_local, dur)
            # Rich drone with slow beating
            val = (sine(freq, t_local) * 0.5 +
                   sine(freq * 1.002, t_local) * 0.3 +  # slow beat
                   sine(freq * 2, t_local) * 0.15 +
                   sine(freq * 3, t_local) * 0.05)
            samples[i] += val * env * volume

    elif event_type == 'pad':
        for i in range(start_sample, end_sample):
            t_local = (i - start_sample) / SAMPLE_RATE
            env = pad_env(t_local, dur)
            # Soft sine pad with vibrato
            vibrato = sine(5.2, t_local) * 2.0  # vibrato in Hz
            val = sine(freq + vibrato, t_local)
            samples[i] += val * env * volume

    elif event_type == 'tick':
        for i in range(start_sample, end_sample):
            t_local = (i - start_sample) / SAMPLE_RATE
            env = music_box_env(t_local, dur)
            # Noise-like click
            val = sine(4200, t_local) * 0.5 + sine(6300, t_local) * 0.3 + sine(8400, t_local) * 0.2
            samples[i] += val * env * volume

print("Applying crossfade for seamless loop...")

# === CROSSFADE for seamless looping ===
# Fade last 2 seconds into first 2 seconds
crossfade_samples = SAMPLE_RATE * 2
for i in range(crossfade_samples):
    fade_out = 1.0 - (i / crossfade_samples)
    fade_in = i / crossfade_samples
    # Blend end into beginning
    blended = samples[NUM_SAMPLES - crossfade_samples + i] * fade_out
    samples[i] = samples[i] * fade_in + blended

# Fade out the very end to zero (it was copied to beginning via crossfade)
for i in range(crossfade_samples):
    fade = 1.0 - (i / crossfade_samples)
    samples[NUM_SAMPLES - crossfade_samples + i] *= fade

print("Normalizing...")

# === NORMALIZE ===
peak = max(abs(s) for s in samples)
if peak > 0:
    scale = 0.85 / peak  # leave some headroom
    samples = [s * scale for s in samples]

print("Writing WAV file...")

# === WRITE WAV ===
output_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "monstacka-bgm.wav")
with wave.open(output_path, 'w') as wav:
    wav.setnchannels(CHANNELS)
    wav.setsampwidth(SAMPLE_WIDTH)
    wav.setframerate(SAMPLE_RATE)
    for s in samples:
        clamped = max(-1.0, min(1.0, s))
        wav.writeframes(struct.pack('<h', int(clamped * MAX_AMP)))

file_size = os.path.getsize(output_path) / (1024 * 1024)
print(f"Done! Wrote {output_path}")
print(f"File size: {file_size:.1f} MB")
print(f"Duration: {DURATION}s, Sample rate: {SAMPLE_RATE}Hz, 16-bit mono")
print("The track crossfades seamlessly for perfect looping.")
