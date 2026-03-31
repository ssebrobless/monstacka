import { formatTime } from '../engine/state';
import { populateMonsterPreviewFigure } from './monsterDom';
import { getVisibleScoreRecords, getVisibleSprintRecords } from '../demoRecords';
import type { PieceType, StorageData } from '../types';

export type HomeLeaderboardMode = 'arcade' | 'sprint40';

export interface HomeMenuState {
  activeIndex: number;
  leaderboardMode: HomeLeaderboardMode;
  loreOpen: boolean;
  loreBubbleOpenedAt: number;
  loreTypingPiece: PieceType | null;
  loreVisibleText: string;
}

interface MonstosProfile {
  pieceType: PieceType;
  name: string;
  lore: string;
  previewRotation: number;
  voiceHint: string;
}

export interface HomeMenuRefs {
  monstosName: HTMLElement;
  monstosVoiceButton: HTMLButtonElement;
  monstosLoreButton: HTMLButtonElement;
  monstosLoreMask: HTMLElement;
  monstosLoreBubble: HTMLElement;
  monstosLoreText: HTMLElement;
  monstosLeft: HTMLElement;
  monstosCenter: HTMLElement;
  monstosRight: HTMLElement;
  leaderboardSprintButton: HTMLButtonElement;
  leaderboardArcadeButton: HTMLButtonElement;
  homeLeaderboard: HTMLElement;
}

const MONSTOS_ORDER: PieceType[] = ['S', 'I', 'Z', 'O', 'T', 'J', 'L'];

const MONSTOS_PROFILES: Record<PieceType, MonstosProfile> = {
  I: {
    pieceType: 'I',
    name: 'BLYNDOOLIE',
    lore: "it sees all! good luck sneaking up... wait...it doesn't even blink... is it drooling on it's own eyes?",
    previewRotation: 1,
    voiceHint: 'Wet little staring noises.',
  },
  O: {
    pieceType: 'O',
    name: 'MUWERDE',
    lore: 'unfortunately one of the smartest in the bunch, though measures of intelligence are inconsistent. provides solid data until it begins refusing to cooperate. The screams were annoying.',
    previewRotation: 0,
    voiceHint: 'A round hungry gasp.',
  },
  T: {
    pieceType: 'T',
    name: 'LYSERGICADA',
    lore: 'might as well be lobotomized. succesfully managed to have naturally occuring traces of lysergic acid diethylamide secreting from the saliva glands. the same fungus variant we developed to thrive inside its body has... gone above and beyond to say the least. it is safe to to say the the host is no longer in control.',
    previewRotation: 2,
    voiceHint: 'A sticky gargle.',
  },
  S: {
    pieceType: 'S',
    name: 'SORRISOL',
    lore: 'designed to clean any mess and have a constant insatiable hunger. in need of dental reconstruction, majority of mouth full of molars; too many waking up before it finishes cleaning.',
    previewRotation: 0,
    voiceHint: 'A scratchy zipper grin.',
  },
  Z: {
    pieceType: 'Z',
    name: 'AGGRASO',
    lore: "the first one that didn't melt into goop... ectodermal influx. a minor over correction on our part. Approach with caution.",
    previewRotation: 0,
    voiceHint: 'A mossy chomp.',
  },
  J: {
    pieceType: 'J',
    name: 'DOUSEMA',
    lore: 'surprisingly resilliant. all teeth and four of its eyes were redistrubted to more promising candidates. but... had i realized the potential sooner... what a waste.',
    previewRotation: 3,
    voiceHint: 'A tiny nasal sniff.',
  },
  L: {
    pieceType: 'L',
    name: 'GALIFFAMBOS',
    lore: 'thee who listens. not a step is taken without being a announced first.the oldest of the refined ones. we considered replacing the eye once it went blind. but the when the additional ears came in we were amazed at how innate its ability to use echolocation was. so the eye remained... i- ... we though it was... funny.',
    previewRotation: 1,
    voiceHint: 'A twitchy ear wiggle.',
  },
};

export function getHomeMenuRefs(): HomeMenuRefs {
  return {
    monstosName: document.getElementById('monstosName')!,
    monstosVoiceButton: document.getElementById('monstosVoiceButton') as HTMLButtonElement,
    monstosLoreButton: document.getElementById('monstosLoreButton') as HTMLButtonElement,
    monstosLoreMask: document.getElementById('monstosLoreMask')!,
    monstosLoreBubble: document.getElementById('monstosLoreBubble')!,
    monstosLoreText: document.getElementById('monstosLoreText')!,
    monstosLeft: document.getElementById('monstosLeft')!,
    monstosCenter: document.getElementById('monstosCenter')!,
    monstosRight: document.getElementById('monstosRight')!,
    leaderboardSprintButton: document.getElementById('leaderboardSprintButton') as HTMLButtonElement,
    leaderboardArcadeButton: document.getElementById('leaderboardArcadeButton') as HTMLButtonElement,
    homeLeaderboard: document.getElementById('homeLeaderboard')!,
  };
}

export function createHomeMenuState(): HomeMenuState {
  return {
    activeIndex: MONSTOS_ORDER.indexOf('I'),
    leaderboardMode: 'arcade',
    loreOpen: true,
    loreBubbleOpenedAt: performance.now(),
    loreTypingPiece: 'I',
    loreVisibleText: '',
  };
}

export function cycleHomeMonstos(state: HomeMenuState, direction: 1 | -1): void {
  state.activeIndex = (state.activeIndex + direction + MONSTOS_ORDER.length) % MONSTOS_ORDER.length;
}

export function getActiveMonstos(state: HomeMenuState): MonstosProfile {
  return MONSTOS_PROFILES[MONSTOS_ORDER[state.activeIndex]];
}

function getProfileAtOffset(state: HomeMenuState, offset: number): MonstosProfile {
  const index = (state.activeIndex + offset + MONSTOS_ORDER.length) % MONSTOS_ORDER.length;
  return MONSTOS_PROFILES[MONSTOS_ORDER[index]];
}

function renderScoreboard(refs: HomeMenuRefs, storage: StorageData, mode: HomeLeaderboardMode): void {
  refs.homeLeaderboard.innerHTML = '';
  const visibleArcade = getVisibleScoreRecords(storage.score);
  const visibleSprint = getVisibleSprintRecords(storage.sprint);

  for (let index = 0; index < 10; index += 1) {
    const item = document.createElement('li');
    item.className = 'home-score-row';
    const value = document.createElement('span');
    value.className = 'home-score-value';
    const tag = document.createElement('span');
    tag.className = 'home-score-tag';

    if (mode === 'arcade') {
      const record = visibleArcade[index];
      value.textContent = record ? `${record.score} pts` : '---';
      tag.textContent = record ? record.nickname : '-----';
    } else {
      const record = visibleSprint[index];
      value.textContent = record ? formatTime(record.timeMs) : '--:--.---';
      tag.textContent = record ? record.nickname : '-----';
    }

    item.appendChild(value);
    item.appendChild(tag);
    refs.homeLeaderboard.appendChild(item);
  }
}

function syncLoreBubbleState(state: HomeMenuState, active: MonstosProfile, now: number): void {
  const bubbleOpenDelayMs = 260;
  const charIntervalMs = 18;

  if (!state.loreOpen) {
    state.loreVisibleText = '';
    state.loreTypingPiece = active.pieceType;
    return;
  }

  if (state.loreTypingPiece !== active.pieceType) {
    state.loreTypingPiece = active.pieceType;
    state.loreBubbleOpenedAt = now;
    state.loreVisibleText = '';
  }

  const elapsedMs = Math.max(0, now - state.loreBubbleOpenedAt - bubbleOpenDelayMs);
  const visibleChars = Math.min(active.lore.length, Math.floor(elapsedMs / charIntervalMs));
  state.loreVisibleText = active.lore.slice(0, visibleChars);
}

function renderMonstosStage(
  container: HTMLElement,
  profile: MonstosProfile,
  now: number,
  animate: boolean,
): void {
  container.classList.toggle('is-active', animate);
  const lookX = animate ? Math.sin(now / 520) * 0.16 : 0.02;
  const lookY = animate ? Math.cos(now / 760) * 0.1 : 0.02;
  populateMonsterPreviewFigure(container, profile.pieceType, {
    rotation: profile.previewRotation,
    now,
    lookX,
    lookY,
    animate,
    fillRatio: animate ? 0.84 : 0.62,
  });
}

function applyLoreFitClasses(refs: HomeMenuRefs): void {
  refs.monstosLoreBubble.classList.remove('is-long', 'is-huge', 'is-overflowing');
  refs.monstosLoreText.style.fontSize = '';
  refs.monstosLoreText.style.lineHeight = '';
  refs.monstosLoreText.style.letterSpacing = '';

  const contentLength = refs.monstosLoreText.textContent?.trim().length ?? 0;
  let fontSize = contentLength < 90 ? 1.16 : contentLength < 145 ? 1.02 : contentLength < 215 ? 0.88 : 0.76;
  let lineHeight = contentLength < 145 ? 1.08 : 1.03;

  while (fontSize > 0.54) {
    refs.monstosLoreText.style.fontSize = `${fontSize.toFixed(2)}rem`;
    refs.monstosLoreText.style.lineHeight = `${lineHeight.toFixed(2)}`;

    if (refs.monstosLoreText.scrollHeight <= refs.monstosLoreText.clientHeight + 2) {
      break;
    }

    fontSize -= 0.04;
    lineHeight = Math.max(0.96, lineHeight - 0.01);
  }

  if (fontSize <= 0.88) {
    refs.monstosLoreBubble.classList.add('is-long');
  }
  if (fontSize <= 0.72) {
    refs.monstosLoreBubble.classList.add('is-huge');
  }
  if (refs.monstosLoreText.scrollHeight > refs.monstosLoreText.clientHeight + 2) {
    refs.monstosLoreBubble.classList.add('is-overflowing');
    refs.monstosLoreText.style.letterSpacing = '0.01em';
  }
}

export function renderActiveHomeMonstosPreview(
  refs: HomeMenuRefs,
  state: HomeMenuState,
  now: number,
): void {
  renderMonstosStage(refs.monstosCenter, getProfileAtOffset(state, 0), now, true);
}

export function renderHomeMenu(
  refs: HomeMenuRefs,
  storage: StorageData,
  state: HomeMenuState,
  now: number,
): void {
  const active = getProfileAtOffset(state, 0);
  const left = getProfileAtOffset(state, -1);
  const right = getProfileAtOffset(state, 1);
  syncLoreBubbleState(state, active, now);

  refs.monstosName.textContent = active.name;
  refs.monstosLoreText.textContent = state.loreVisibleText;
  refs.monstosLoreMask.classList.toggle('is-open', state.loreOpen);
  refs.monstosLoreBubble.classList.toggle('is-collapsed', !state.loreOpen);
  applyLoreFitClasses(refs);
  refs.monstosLoreButton.setAttribute('aria-pressed', String(state.loreOpen));
  refs.monstosVoiceButton.title = `${active.name}: voice preview coming later`;

  refs.leaderboardArcadeButton.classList.toggle('is-selected', state.leaderboardMode === 'arcade');
  refs.leaderboardSprintButton.classList.toggle('is-selected', state.leaderboardMode === 'sprint40');

  renderMonstosStage(refs.monstosLeft, left, now, false);
  renderMonstosStage(refs.monstosCenter, active, now, true);
  renderMonstosStage(refs.monstosRight, right, now, false);
  renderScoreboard(refs, storage, state.leaderboardMode);
}
