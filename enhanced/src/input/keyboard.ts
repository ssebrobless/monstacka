import type { GameState, Settings } from '../types';
import { KEYMAP } from '../constants';
import {
  move, rotate, hardDrop, hold, dropOnce, reset,
} from '../engine/state';
import type { SoundCue } from '../audio';

export interface InputState {
  horizontal: number;
  horizontalTimer: number;
}

export function createInputState(): InputState {
  return { horizontal: 0, horizontalTimer: 0 };
}

function clearHorizontalRepeat(input: InputState): void {
  if (input.horizontalTimer) {
    window.clearTimeout(input.horizontalTimer);
    input.horizontalTimer = 0;
  }
}

function restartHorizontalRepeat(
  input: InputState,
  state: GameState,
  settings: Settings,
  onRender: () => void,
): void {
  clearHorizontalRepeat(input);
  if (!input.horizontal || !state.startTime || state.gameOver) return;

  const action = input.horizontal < 0
    ? () => move(state, -1, 0, settings.lockDelayMs)
    : () => move(state, 1, 0, settings.lockDelayMs);

  input.horizontalTimer = window.setTimeout(() => {
    if (settings.arrMs === 0) {
      while (action()) { /* instant wall */ }
      onRender();
      return;
    }
    const repeat = () => {
      if (!input.horizontal || state.gameOver) return;
      action();
      onRender();
      input.horizontalTimer = window.setTimeout(repeat, settings.arrMs);
    };
    repeat();
  }, settings.dasMs);
}

export function setupKeyboard(
  state: GameState,
  input: InputState,
  settings: Settings,
  onRender: () => void,
  onReset: () => void,
  onRecordCheck: () => void,
  onSound: (cue: SoundCue) => void,
): () => void {
  function countTrainingInput(): void {
    if (state.mode === 'training') {
      state.currentPieceInputs += 1;
    }
  }

  function handleKeydown(event: KeyboardEvent) {
    const action = KEYMAP[event.code];
    if (!action) return;
    event.preventDefault();

    if (action === 'retry') {
      onReset();
      return;
    }

    if (!state.startTime || state.gameOver) return;
    if (event.repeat && action !== 'soft') return;

    if (action === 'left' || action === 'right') {
      input.horizontal = action === 'left' ? -1 : 1;
      if (move(state, input.horizontal, 0, settings.lockDelayMs)) {
        countTrainingInput();
        onSound('move');
      }
      restartHorizontalRepeat(input, state, settings, onRender);
    } else if (action === 'soft') {
      if (dropOnce(state, settings.lockDelayMs, true)) {
        onSound('softDrop');
      }
    } else if (action === 'hard') {
      hardDrop(state, settings.lockDelayMs);
      onSound('hardDrop');
      onRecordCheck();
    } else if (action === 'ccw') {
      if (rotate(state, -1, true, settings.lockDelayMs)) {
        countTrainingInput();
        onSound('rotate');
      }
    } else if (action === 'cw') {
      if (rotate(state, 1, true, settings.lockDelayMs)) {
        countTrainingInput();
        onSound('rotate');
      }
    } else if (action === 'flip') {
      if (rotate(state, 2, false, settings.lockDelayMs)) {
        countTrainingInput();
        onSound('rotate');
      }
    } else if (action === 'hold') {
      if (state.mode !== 'training' && hold(state, settings.lockDelayMs)) {
        onSound('hold');
      }
    }
    onRender();
  }

  function handleKeyup(event: KeyboardEvent) {
    const action = KEYMAP[event.code];
    if (!action) return;
    if ((action === 'left' && input.horizontal < 0) || (action === 'right' && input.horizontal > 0)) {
      input.horizontal = 0;
      clearHorizontalRepeat(input);
    }
  }

  document.addEventListener('keydown', handleKeydown);
  document.addEventListener('keyup', handleKeyup);

  return () => {
    document.removeEventListener('keydown', handleKeydown);
    document.removeEventListener('keyup', handleKeyup);
  };
}

export { clearHorizontalRepeat };
