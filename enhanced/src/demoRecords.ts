import type { ScoreRecord, SprintRecord } from './types';
import { MAX_LEADERBOARD_ENTRIES } from './constants';

const DEMO_SCORE_RECORDS: ScoreRecord[] = [
  { nickname: 'GLOOP', score: 12800, lines: 31, timeMs: 122000, timestamp: 'demo-score-1' },
  { nickname: 'BLOBB', score: 11100, lines: 28, timeMs: 119000, timestamp: 'demo-score-2' },
  { nickname: 'MAWZ1', score: 9700, lines: 24, timeMs: 111000, timestamp: 'demo-score-3' },
  { nickname: 'SPLOT', score: 9100, lines: 23, timeMs: 107000, timestamp: 'demo-score-4' },
  { nickname: 'OOZER', score: 8600, lines: 21, timeMs: 101000, timestamp: 'demo-score-5' },
  { nickname: 'FANG5', score: 8000, lines: 20, timeMs: 98000, timestamp: 'demo-score-6' },
  { nickname: 'CREEP', score: 7600, lines: 18, timeMs: 94000, timestamp: 'demo-score-7' },
  { nickname: 'STARE', score: 7100, lines: 17, timeMs: 91000, timestamp: 'demo-score-8' },
  { nickname: 'SLURP', score: 6900, lines: 16, timeMs: 88000, timestamp: 'demo-score-9' },
  { nickname: 'DRIPY', score: 6400, lines: 15, timeMs: 86000, timestamp: 'demo-score-10' },
];

const DEMO_SPRINT_RECORDS: SprintRecord[] = [
  { nickname: 'FASTY', timeMs: 52890, lines: 40, pieces: 95, timestamp: 'demo-sprint-1' },
  { nickname: 'GLINT', timeMs: 55620, lines: 40, pieces: 97, timestamp: 'demo-sprint-2' },
  { nickname: 'CHOMP', timeMs: 60110, lines: 40, pieces: 102, timestamp: 'demo-sprint-3' },
  { nickname: 'SLICK', timeMs: 63240, lines: 40, pieces: 104, timestamp: 'demo-sprint-4' },
  { nickname: 'GNAWS', timeMs: 66510, lines: 40, pieces: 108, timestamp: 'demo-sprint-5' },
  { nickname: 'BLINK', timeMs: 70300, lines: 40, pieces: 112, timestamp: 'demo-sprint-6' },
  { nickname: 'RINSE', timeMs: 74220, lines: 40, pieces: 116, timestamp: 'demo-sprint-7' },
  { nickname: 'SPINE', timeMs: 78110, lines: 40, pieces: 119, timestamp: 'demo-sprint-8' },
  { nickname: 'FLESH', timeMs: 82630, lines: 40, pieces: 125, timestamp: 'demo-sprint-9' },
  { nickname: 'DRONE', timeMs: 87450, lines: 40, pieces: 130, timestamp: 'demo-sprint-10' },
];

function sortScoreRecords(records: ScoreRecord[]): ScoreRecord[] {
  return [...records].sort((a, b) => b.score - a.score || a.timestamp.localeCompare(b.timestamp));
}

function sortSprintRecords(records: SprintRecord[]): SprintRecord[] {
  return [...records].sort((a, b) => a.timeMs - b.timeMs || a.timestamp.localeCompare(b.timestamp));
}

export function getVisibleScoreRecords(records: ScoreRecord[]): ScoreRecord[] {
  return sortScoreRecords([...records, ...DEMO_SCORE_RECORDS]).slice(0, MAX_LEADERBOARD_ENTRIES);
}

export function getVisibleSprintRecords(records: SprintRecord[]): SprintRecord[] {
  return sortSprintRecords([...records, ...DEMO_SPRINT_RECORDS]).slice(0, MAX_LEADERBOARD_ENTRIES);
}
