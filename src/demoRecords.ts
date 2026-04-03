import type { ScoreRecord, SprintRecord } from './types';
import { MAX_LEADERBOARD_ENTRIES } from './constants';

function sortScoreRecords(records: ScoreRecord[]): ScoreRecord[] {
  return [...records].sort((a, b) => b.score - a.score || a.timestamp.localeCompare(b.timestamp));
}

function sortSprintRecords(records: SprintRecord[]): SprintRecord[] {
  return [...records].sort((a, b) => a.timeMs - b.timeMs || a.timestamp.localeCompare(b.timestamp));
}

export function getVisibleScoreRecords(records: ScoreRecord[]): ScoreRecord[] {
  return sortScoreRecords(records).slice(0, MAX_LEADERBOARD_ENTRIES);
}

export function getVisibleSprintRecords(records: SprintRecord[]): SprintRecord[] {
  return sortSprintRecords(records).slice(0, MAX_LEADERBOARD_ENTRIES);
}
