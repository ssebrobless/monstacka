import type { GameMode, SavedRun, SavedRunsByMode, Settings, SprintRecord, ScoreRecord, StorageData } from './types';
import {
  STORAGE_KEY,
  LEGACY_STORAGE_KEYS,
  SETTINGS_DEFAULTS,
  MAX_NICKNAME_LENGTH,
  MAX_LEADERBOARD_ENTRIES,
} from './constants';

function createDefaultSettings(): Settings {
  return {
    ...SETTINGS_DEFAULTS,
    controls: { ...SETTINGS_DEFAULTS.controls },
    gamepadControls: { ...SETTINGS_DEFAULTS.gamepadControls },
  };
}

function normalizeGamepadControls(bindings: Partial<Settings['gamepadControls']> | undefined): Partial<Settings['gamepadControls']> {
  if (!bindings) {
    return {};
  }

  const normalized = { ...bindings };
  if (
    normalized.pause === 'Pad:Button9'
    && normalized.retry === 'Pad:Button8'
  ) {
    normalized.pause = 'Pad:Button8';
    normalized.retry = 'Pad:Button9';
  }

  if (
    normalized.left === 'Pad:Button14'
    && normalized.right === 'Pad:Button15'
    && normalized.soft === 'Pad:Button13'
    && normalized.hard === 'Pad:Button0'
    && normalized.ccw === 'Pad:Button2'
    && normalized.cw === 'Pad:Button1'
    && normalized.flip === 'Pad:Button3'
    && normalized.hold === 'Pad:Button4'
  ) {
    normalized.hard = 'Pad:Button12';
    normalized.ccw = 'Pad:Button0';
  }

  return normalized;
}

function createEmptySavedRuns(): SavedRunsByMode {
  return {
    arcade: null,
    sprint40: null,
    training: null,
  };
}

function parseSavedRun(value: unknown, mode: GameMode): SavedRun | null {
  if (!value || typeof value !== 'object') {
    return null;
  }

  const parsed = value as Partial<SavedRun>;
  if (parsed.mode !== mode) {
    return null;
  }

  if (parsed.phase !== 'countdown' && parsed.phase !== 'playing' && parsed.phase !== 'paused') {
    return null;
  }

  if (!parsed.state || typeof parsed.state !== 'object') {
    return null;
  }

  return parsed as SavedRun;
}

function parseSavedRuns(value: unknown): SavedRunsByMode {
  if (!value || typeof value !== 'object') {
    return createEmptySavedRuns();
  }

  const parsed = value as Partial<Record<GameMode, unknown>>;
  return {
    arcade: parseSavedRun(parsed.arcade, 'arcade'),
    sprint40: parseSavedRun(parsed.sprint40, 'sprint40'),
    training: parseSavedRun(parsed.training, 'training'),
  };
}

function parseStorage(raw: string | null): StorageData | null {
  if (!raw) return null;

  const parsed = JSON.parse(raw || '{}');
  const parsedSettings = parsed.settings || {};
  const legacyMuted = parsedSettings.muted === true;
  const defaultSettings = createDefaultSettings();
  return {
    sprint: Array.isArray(parsed.sprint) ? parsed.sprint : [],
    score: Array.isArray(parsed.score) ? parsed.score : [],
    settings: {
      ...defaultSettings,
      ...parsedSettings,
      sfxEnabled: parsedSettings.sfxEnabled ?? !legacyMuted,
      musicEnabled: parsedSettings.musicEnabled ?? !legacyMuted,
      controls: {
        ...defaultSettings.controls,
        ...(parsedSettings.controls || {}),
      },
      gamepadControls: {
        ...defaultSettings.gamepadControls,
        ...normalizeGamepadControls(parsedSettings.gamepadControls || {}),
      },
    },
    savedRuns: parseSavedRuns(parsed.savedRuns),
  };
}

export function loadStorage(): StorageData {
  try {
    const current = parseStorage(localStorage.getItem(STORAGE_KEY));
    if (current) {
      return current;
    }

    for (const legacyKey of LEGACY_STORAGE_KEYS) {
      const legacy = parseStorage(localStorage.getItem(legacyKey));
      if (legacy) {
        saveStorage(legacy);
        return legacy;
      }
    }

    return { sprint: [], score: [], settings: createDefaultSettings(), savedRuns: createEmptySavedRuns() };
  } catch {
    return { sprint: [], score: [], settings: createDefaultSettings(), savedRuns: createEmptySavedRuns() };
  }
}

export function saveStorage(data: StorageData): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify({
    sprint: data.sprint,
    score: data.score,
    settings: data.settings,
    savedRuns: data.savedRuns,
  }));
}

export function normalizeNickname(value: string): string {
  return value
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, '')
    .slice(0, MAX_NICKNAME_LENGTH);
}

export function qualifiesSprintRecord(data: StorageData, timeMs: number): boolean {
  if (timeMs <= 0) return false;
  if (data.sprint.length < MAX_LEADERBOARD_ENTRIES) return true;
  return timeMs < data.sprint[data.sprint.length - 1].timeMs;
}

export function qualifiesScoreRecord(data: StorageData, score: number): boolean {
  if (score <= 0) return false;
  if (data.score.length < MAX_LEADERBOARD_ENTRIES) return true;
  return score > data.score[data.score.length - 1].score;
}

export function saveSprintRecord(
  data: StorageData,
  nickname: string,
  timeMs: number,
  lines: number,
  pieces: number,
): void {
  const entry: SprintRecord = {
    nickname: normalizeNickname(nickname) || 'STACK',
    timeMs,
    lines,
    pieces,
    timestamp: new Date().toISOString(),
  };
  data.sprint.push(entry);
  data.sprint.sort((a, b) => a.timeMs - b.timeMs || a.timestamp.localeCompare(b.timestamp));
  data.sprint = data.sprint.slice(0, MAX_LEADERBOARD_ENTRIES);
  saveStorage(data);
}

export function saveScoreRecord(
  data: StorageData,
  nickname: string,
  score: number,
  lines: number,
  timeMs: number,
): void {
  const entry: ScoreRecord = {
    nickname: normalizeNickname(nickname) || 'STACK',
    score,
    lines,
    timeMs,
    timestamp: new Date().toISOString(),
  };
  data.score.push(entry);
  data.score.sort((a, b) => b.score - a.score || a.timestamp.localeCompare(b.timestamp));
  data.score = data.score.slice(0, MAX_LEADERBOARD_ENTRIES);
  saveStorage(data);
}

export function getSavedRun(data: StorageData, mode: GameMode): SavedRun | null {
  return data.savedRuns[mode];
}

export function setSavedRun(data: StorageData, run: SavedRun): void {
  data.savedRuns[run.mode] = run;
  saveStorage(data);
}

export function clearSavedRun(data: StorageData, mode: GameMode): void {
  data.savedRuns[mode] = null;
  saveStorage(data);
}
