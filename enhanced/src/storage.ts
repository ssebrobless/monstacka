import type { Settings, SprintRecord, ScoreRecord, StorageData } from './types';
import { STORAGE_KEY, SETTINGS_DEFAULTS, MAX_NICKNAME_LENGTH } from './constants';

export function loadStorage(): StorageData {
  try {
    const parsed = JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}');
    return {
      sprint: Array.isArray(parsed.sprint) ? parsed.sprint : [],
      score: Array.isArray(parsed.score) ? parsed.score : [],
      settings: { ...SETTINGS_DEFAULTS, ...(parsed.settings || {}) },
    };
  } catch {
    return { sprint: [], score: [], settings: { ...SETTINGS_DEFAULTS } };
  }
}

export function saveStorage(data: StorageData): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify({
    sprint: data.sprint,
    score: data.score,
    settings: data.settings,
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
  if (data.sprint.length < 10) return true;
  return timeMs < data.sprint[data.sprint.length - 1].timeMs;
}

export function qualifiesScoreRecord(data: StorageData, score: number): boolean {
  if (score <= 0) return false;
  if (data.score.length < 10) return true;
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
    nickname: normalizeNickname(nickname) || 'ERIS',
    timeMs,
    lines,
    pieces,
    timestamp: new Date().toISOString(),
  };
  data.sprint.push(entry);
  data.sprint.sort((a, b) => a.timeMs - b.timeMs || a.timestamp.localeCompare(b.timestamp));
  data.sprint = data.sprint.slice(0, 10);
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
    nickname: normalizeNickname(nickname) || 'ERIS',
    score,
    lines,
    timeMs,
    timestamp: new Date().toISOString(),
  };
  data.score.push(entry);
  data.score.sort((a, b) => b.score - a.score || a.timestamp.localeCompare(b.timestamp));
  data.score = data.score.slice(0, 10);
  saveStorage(data);
}
