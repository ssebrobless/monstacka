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
  homeArtboard: HTMLElement;
  monstosName: HTMLElement;
  monstosVoiceButton: HTMLButtonElement;
  monstosLoreButton: HTMLButtonElement;
  monstosLoreBubble: HTMLElement;
  monstosLoreSurface: HTMLElement;
  monstosLoreTailOutline: SVGPathElement;
  monstosLoreTailFill: SVGPathElement;
  monstosLoreText: HTMLElement;
  monstosLoreMeasure: HTMLElement;
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
    homeArtboard: document.getElementById('homeArtboard')!,
    monstosName: document.getElementById('monstosName')!,
    monstosVoiceButton: document.getElementById('monstosVoiceButton') as HTMLButtonElement,
    monstosLoreButton: document.getElementById('monstosLoreButton') as HTMLButtonElement,
    monstosLoreBubble: document.getElementById('monstosLoreBubble')!,
    monstosLoreSurface: document.getElementById('monstosLoreSurface')!,
    monstosLoreTailOutline: document.getElementById('monstosLoreTailOutline') as unknown as SVGPathElement,
    monstosLoreTailFill: document.getElementById('monstosLoreTailFill') as unknown as SVGPathElement,
    monstosLoreText: document.getElementById('monstosLoreText')!,
    monstosLoreMeasure: document.getElementById('monstosLoreMeasure')!,
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
    loreOpen: false,
    loreBubbleOpenedAt: performance.now(),
    loreTypingPiece: null,
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
  const maxVisibleRows = 8;

  for (let index = 0; index < maxVisibleRows; index += 1) {
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
    fillRatio: getPreviewFillRatio(profile.pieceType, animate),
  });
}

function getPreviewFillRatio(pieceType: PieceType, animate: boolean): number {
  const activeFill: Record<PieceType, number> = {
    I: 0.5,
    O: 0.54,
    T: 0.54,
    S: 0.48,
    Z: 0.48,
    J: 0.5,
    L: 0.5,
  };

  const sideFill: Record<PieceType, number> = {
    I: 0.42,
    O: 0.45,
    T: 0.44,
    S: 0.4,
    Z: 0.4,
    J: 0.42,
    L: 0.42,
  };

  return animate ? activeFill[pieceType] : sideFill[pieceType];
}

function layoutLoreBubble(refs: HomeMenuRefs, content: string): void {
  const measure = refs.monstosLoreMeasure;
  const candidateWidths = content.length < 120
    ? [260, 300, 340, 380]
    : content.length < 190
      ? [320, 360, 400, 440]
      : [360, 410, 460, 500];
  const maxHeight = 132;
  const minHeight = 66;
  let chosen = {
    width: 380,
    height: 96,
    fontSize: 0.98,
    lineHeight: 1.08,
  };

  measure.textContent = content;

  outer: for (const width of candidateWidths) {
    let fontSize = content.length < 110 ? 1.12 : content.length < 180 ? 0.98 : 0.9;
    let lineHeight = content.length < 160 ? 1.1 : 1.04;

    while (fontSize >= 0.7) {
      measure.style.width = `${width}px`;
      measure.style.fontSize = `${fontSize.toFixed(2)}rem`;
      measure.style.lineHeight = `${lineHeight.toFixed(2)}`;
      const measuredHeight = Math.ceil(measure.scrollHeight);

      if (measuredHeight <= maxHeight) {
        chosen = {
          width,
          height: Math.max(minHeight, Math.min(152, measuredHeight + 18)),
          fontSize,
          lineHeight,
        };
        break outer;
      }

      fontSize -= 0.04;
      lineHeight = Math.max(0.98, lineHeight - 0.02);
    }
  }

  refs.monstosLoreBubble.style.setProperty('--lore-bubble-width', `${chosen.width}px`);
  refs.monstosLoreBubble.style.setProperty('--lore-bubble-height', `${chosen.height}px`);
  refs.monstosLoreText.style.fontSize = `${chosen.fontSize.toFixed(2)}rem`;
  refs.monstosLoreText.style.lineHeight = `${chosen.lineHeight.toFixed(2)}`;

  const surfaceLeft = refs.monstosLoreSurface.offsetLeft;
  const surfaceTop = refs.monstosLoreSurface.offsetTop;
  const surfaceHeight = refs.monstosLoreSurface.offsetHeight;
  const buttonRight = refs.monstosLoreButton.offsetLeft - refs.monstosLoreBubble.offsetLeft + refs.monstosLoreButton.offsetWidth;
  const buttonTop = refs.monstosLoreButton.offsetTop - refs.monstosLoreBubble.offsetTop;
  const buttonHeight = refs.monstosLoreButton.offsetHeight;

  const buttonTipX = buttonRight + 6;
  const buttonTipY = buttonTop + buttonHeight * 0.92;

  const bubbleAnchorOuterX = surfaceLeft + 18;
  const bubbleAnchorOuterY = surfaceTop + surfaceHeight - 54;
  const bubbleAnchorInnerX = surfaceLeft + 48;
  const bubbleAnchorInnerY = surfaceTop + surfaceHeight - 16;

  const topCtrl1X = bubbleAnchorOuterX - 12;
  const topCtrl1Y = bubbleAnchorOuterY + 10;
  const topCtrl2X = buttonTipX + (bubbleAnchorOuterX - buttonTipX) * 0.34;
  const topCtrl2Y = buttonTipY - 8;

  const bottomCtrl1X = buttonTipX + (bubbleAnchorInnerX - buttonTipX) * 0.3;
  const bottomCtrl1Y = buttonTipY + 10;
  const bottomCtrl2X = bubbleAnchorInnerX - 10;
  const bottomCtrl2Y = bubbleAnchorInnerY + 6;

  refs.monstosLoreTailOutline.setAttribute(
    'd',
    `M ${bubbleAnchorOuterX} ${bubbleAnchorOuterY}
     C ${topCtrl1X} ${topCtrl1Y} ${topCtrl2X} ${topCtrl2Y} ${buttonTipX} ${buttonTipY}
     C ${bottomCtrl1X} ${bottomCtrl1Y} ${bottomCtrl2X} ${bottomCtrl2Y} ${bubbleAnchorInnerX} ${bubbleAnchorInnerY}
     Z`,
  );

  refs.monstosLoreTailFill.setAttribute(
    'd',
    `M ${bubbleAnchorOuterX + 5} ${bubbleAnchorOuterY + 3}
     C ${topCtrl1X + 5} ${topCtrl1Y + 3} ${topCtrl2X + 4} ${topCtrl2Y - 2} ${buttonTipX + 3} ${buttonTipY + 1}
     C ${bottomCtrl1X + 2} ${bottomCtrl1Y + 3} ${bottomCtrl2X + 3} ${bottomCtrl2Y + 2} ${bubbleAnchorInnerX - 4} ${bubbleAnchorInnerY}
     Z`,
  );
}

export function renderActiveHomeMonstosPreview(
  refs: HomeMenuRefs,
  state: HomeMenuState,
  now: number,
): void {
  const active = getProfileAtOffset(state, 0);
  renderMonstosStage(refs.monstosCenter, active, now, true);

  if (state.loreOpen) {
    syncLoreBubbleState(state, active, now);
    refs.monstosLoreText.textContent = state.loreVisibleText;
  }
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
  layoutLoreBubble(refs, active.lore);
  refs.monstosLoreBubble.setAttribute('aria-hidden', String(!state.loreOpen));
  refs.monstosLoreBubble.classList.toggle('is-open', state.loreOpen);
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
