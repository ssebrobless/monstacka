import { formatTime } from '../engine/state';
import { populateMonsterPreviewFigure } from './monsterDom';
import type { PieceType, StorageData } from '../types';

export type HomeLeaderboardMode = 'arcade' | 'sprint40';

export interface HomeMenuState {
  activeIndex: number;
  leaderboardMode: HomeLeaderboardMode;
  loreOpen: boolean;
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

  for (let index = 0; index < 10; index += 1) {
    const item = document.createElement('li');
    item.className = 'home-score-row';
    const entry = document.createElement('span');
    entry.className = 'home-score-entry';

    if (mode === 'arcade') {
      const record = storage.score[index];
      entry.textContent = record ? `${record.nickname}  ${record.score} pts` : '-----  ---';
    } else {
      const record = storage.sprint[index];
      entry.textContent = record ? `${record.nickname}  ${formatTime(record.timeMs)}` : '-----  --:--.---';
    }

    item.appendChild(entry);
    refs.homeLeaderboard.appendChild(item);
  }
}

function renderMonstosStage(
  container: HTMLElement,
  profile: MonstosProfile,
  now: number,
  animate: boolean,
): void {
  container.classList.toggle('is-active', animate);
  const lookX = animate ? Math.sin(now / 680) * 0.1 : 0;
  const lookY = animate ? Math.cos(now / 940) * 0.05 : 0.015;
  populateMonsterPreviewFigure(container, profile.pieceType, {
    rotation: profile.previewRotation,
    now,
    lookX,
    lookY,
    animate,
  });
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

  refs.monstosName.textContent = active.name;
  refs.monstosLoreText.textContent = active.lore;
  refs.monstosLoreBubble.classList.toggle('is-long', active.lore.length > 160);
  refs.monstosLoreBubble.classList.toggle('is-huge', active.lore.length > 260);
  refs.monstosLoreBubble.classList.toggle('is-collapsed', !state.loreOpen);
  refs.monstosLoreButton.setAttribute('aria-pressed', String(state.loreOpen));
  refs.monstosVoiceButton.title = `${active.name}: voice preview coming later`;

  refs.leaderboardArcadeButton.classList.toggle('is-selected', state.leaderboardMode === 'arcade');
  refs.leaderboardSprintButton.classList.toggle('is-selected', state.leaderboardMode === 'sprint40');

  renderMonstosStage(refs.monstosLeft, left, now, false);
  renderMonstosStage(refs.monstosCenter, active, now, true);
  renderMonstosStage(refs.monstosRight, right, now, false);
  renderScoreboard(refs, storage, state.leaderboardMode);
}
