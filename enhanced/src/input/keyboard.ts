import type { AppPhase, GameState, Settings } from '../types';
import {
  move, rotate, hardDrop, hold, dropOnce,
} from '../engine/state';
import type { SoundCue } from '../audio';
import { findActionForKeyboard, findActionForMouse } from './bindings';

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
  getAppPhase: () => AppPhase,
  onPause: () => void,
  onResume: () => void,
  onRestartPaused: () => void,
  isInputBlocked: () => boolean,
  onSound: (cue: SoundCue) => void,
): () => void {
  function countTrainingInput(): void {
    if (state.mode === 'training') {
      state.currentPieceInputs += 1;
    }
  }

  function isInteractiveTarget(target: EventTarget | null): boolean {
    if (!(target instanceof HTMLElement)) {
      return false;
    }
    return Boolean(target.closest('button,input,select,textarea,label,a'));
  }

  function handleAction(action: string, event: Event, isRepeat = false) {
    if (isInputBlocked()) {
      return;
    }

    event.preventDefault();
    if ('stopPropagation' in event) {
      event.stopPropagation();
    }

    if (action === 'retry') {
      if (getAppPhase() === 'menu') {
        return;
      }
      onReset();
      return;
    }

    if (action === 'pause') {
      const phase = getAppPhase();
      if (phase === 'playing') {
        onPause();
      } else if (phase === 'paused') {
        onResume();
      }
      return;
    }

    if (action === 'restartPaused') {
      if (getAppPhase() === 'paused') {
        onRestartPaused();
      }
      return;
    }

    if (!state.startTime || state.gameOver) return;
    if (isRepeat && action !== 'soft') return;

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

  function handleKeydown(event: KeyboardEvent) {
    if (isInteractiveTarget(event.target)) {
      return;
    }
    const action = findActionForKeyboard(settings.controls, event.code);
    if (!action) return;
    handleAction(action, event, event.repeat);
  }

  function handleKeyup(event: KeyboardEvent) {
    const action = findActionForKeyboard(settings.controls, event.code);
    if (!action) return;
    if ((action === 'left' && input.horizontal < 0) || (action === 'right' && input.horizontal > 0)) {
      input.horizontal = 0;
      clearHorizontalRepeat(input);
    }
  }

  function handleMousedown(event: MouseEvent) {
    const action = findActionForMouse(settings.controls, event.button);
    if (!action) return;
    if (isInteractiveTarget(event.target)) {
      return;
    }
    handleAction(action, event, false);
  }

  function handleContextMenu(event: MouseEvent) {
    const action = findActionForMouse(settings.controls, 2);
    if (!action || isInputBlocked()) return;
    if (isInteractiveTarget(event.target)) return;
    event.preventDefault();
  }

  document.addEventListener('keydown', handleKeydown);
  document.addEventListener('keyup', handleKeyup);
  document.addEventListener('mousedown', handleMousedown);
  document.addEventListener('contextmenu', handleContextMenu);

  return () => {
    document.removeEventListener('keydown', handleKeydown);
    document.removeEventListener('keyup', handleKeyup);
    document.removeEventListener('mousedown', handleMousedown);
    document.removeEventListener('contextmenu', handleContextMenu);
  };
}

export { clearHorizontalRepeat };
