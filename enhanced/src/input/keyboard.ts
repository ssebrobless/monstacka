import { CONTROL_ORDER } from '../constants';
import type { AppPhase, ControlAction, ControlBindingSource, GameState, Settings } from '../types';
import {
  move, rotate, hardDrop, hold, dropOnce,
} from '../engine/state';
import type { SoundCue } from '../audio';
import {
  findActionForGamepadAxis,
  findActionForGamepadButton,
  findActionForKeyboard,
  findActionForMouse,
  gamepadAxisToken,
  gamepadButtonToken,
} from './bindings';

export interface InputState {
  horizontal: number;
  horizontalTimer: number;
}

interface InputEventLike {
  preventDefault?: () => void;
  stopPropagation?: () => void;
}

interface GamepadAxisState {
  negative: boolean;
  positive: boolean;
}

interface GamepadRawState {
  buttons: boolean[];
  axes: GamepadAxisState[];
}

export interface BindingCaptureTarget {
  action: ControlAction;
  source: ControlBindingSource;
}

export interface GameplayInputContext {
  state: GameState;
  input: InputState;
  settings: Settings;
  onRender: () => void;
  onReset: () => void;
  getAppPhase: () => AppPhase;
  onPause: () => void;
  onResume: () => void;
  onRestartPaused: () => void;
  isInputBlocked: () => boolean;
  onSound: (cue: SoundCue) => void;
}

export function createInputState(): InputState {
  return { horizontal: 0, horizontalTimer: 0 };
}

export function clearHorizontalRepeat(input: InputState): void {
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

function createActionDispatcher(context: GameplayInputContext) {
  const {
    state,
    input,
    settings,
    onRender,
    onReset,
    getAppPhase,
    onPause,
    onResume,
    onRestartPaused,
    isInputBlocked,
    onSound,
  } = context;

  function countTrainingInput(): void {
    if (state.mode === 'training') {
      state.currentPieceInputs += 1;
    }
  }

  function handleAction(action: ControlAction, event?: InputEventLike, isRepeat = false): void {
    if (isInputBlocked()) {
      return;
    }

    event?.preventDefault?.();
    event?.stopPropagation?.();

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

    if (getAppPhase() !== 'playing' || !state.startTime || state.gameOver) return;
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

  function releaseHorizontal(action: 'left' | 'right'): void {
    if ((action === 'left' && input.horizontal < 0) || (action === 'right' && input.horizontal > 0)) {
      input.horizontal = 0;
      clearHorizontalRepeat(input);
    }
  }

  return {
    handleAction,
    releaseHorizontal,
  };
}

function isInteractiveTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) {
    return false;
  }
  return Boolean(target.closest('button,input,select,textarea,label,a'));
}

function readGamepadRawState(gamepad: Gamepad | null): GamepadRawState {
  if (!gamepad) {
    return { buttons: [], axes: [] };
  }

  return {
    buttons: gamepad.buttons.map((button) => button.pressed || button.value >= 0.5),
    axes: gamepad.axes.map((axis) => ({
      negative: axis <= -0.55,
      positive: axis >= 0.55,
    })),
  };
}

function findFreshGamepadBinding(current: GamepadRawState, previous: GamepadRawState): string | null {
  for (let buttonIndex = 0; buttonIndex < current.buttons.length; buttonIndex += 1) {
    if (current.buttons[buttonIndex] && !previous.buttons[buttonIndex]) {
      return gamepadButtonToken(buttonIndex);
    }
  }

  for (let axisIndex = 0; axisIndex < current.axes.length; axisIndex += 1) {
    const currentAxis = current.axes[axisIndex];
    const previousAxis = previous.axes[axisIndex] ?? { negative: false, positive: false };

    if (currentAxis.negative && !previousAxis.negative) {
      return gamepadAxisToken(axisIndex, -1);
    }

    if (currentAxis.positive && !previousAxis.positive) {
      return gamepadAxisToken(axisIndex, 1);
    }
  }

  return null;
}

function getConnectedGamepad(): Gamepad | null {
  const pads = navigator.getGamepads?.() ?? [];
  for (const pad of pads) {
    if (pad && pad.connected) {
      return pad;
    }
  }
  return null;
}

function isActionActiveFromGamepad(action: ControlAction, settings: Settings, gamepad: Gamepad | null): boolean {
  if (!gamepad) {
    return false;
  }

  const binding = settings.gamepadControls[action];
  if (!binding) {
    return false;
  }

  if (binding.startsWith('Pad:Button')) {
    const buttonIndex = Number(binding.slice('Pad:Button'.length));
    return findActionForGamepadButton(settings.gamepadControls, buttonIndex) === action
      && Boolean(gamepad.buttons[buttonIndex]?.pressed || gamepad.buttons[buttonIndex]?.value >= 0.5);
  }

  const axisMatch = binding.match(/^Pad:Axis(\d+):(-?1)$/);
  if (axisMatch) {
    const axisIndex = Number(axisMatch[1]);
    const direction = Number(axisMatch[2]) as -1 | 1;
    const axisValue = gamepad.axes[axisIndex] ?? 0;
    const isActive = direction < 0 ? axisValue <= -0.55 : axisValue >= 0.55;
    return findActionForGamepadAxis(settings.gamepadControls, axisIndex, direction) === action && isActive;
  }

  return false;
}

export function setupKeyboard(context: GameplayInputContext): () => void {
  const { settings, input } = context;
  const { handleAction, releaseHorizontal } = createActionDispatcher(context);

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
    if (action === 'left' || action === 'right') {
      releaseHorizontal(action);
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
    if (!action || context.isInputBlocked()) return;
    if (isInteractiveTarget(event.target)) return;
    event.preventDefault();
  }

  document.addEventListener('keydown', handleKeydown);
  document.addEventListener('keyup', handleKeyup);
  document.addEventListener('mousedown', handleMousedown);
  document.addEventListener('contextmenu', handleContextMenu);

  return () => {
    clearHorizontalRepeat(input);
    document.removeEventListener('keydown', handleKeydown);
    document.removeEventListener('keyup', handleKeyup);
    document.removeEventListener('mousedown', handleMousedown);
    document.removeEventListener('contextmenu', handleContextMenu);
  };
}

export function setupGamepad(
  context: GameplayInputContext,
  getCaptureTarget: () => BindingCaptureTarget | null,
  onCaptureBinding: (binding: string) => void,
): () => void {
  const { handleAction, releaseHorizontal } = createActionDispatcher(context);
  const previousActions: Record<ControlAction, boolean> = {
    left: false,
    right: false,
    soft: false,
    hard: false,
    ccw: false,
    cw: false,
    flip: false,
    hold: false,
    retry: false,
    pause: false,
    restartPaused: false,
  };
  let previousRaw: GamepadRawState = { buttons: [], axes: [] };
  let nextSoftRepeatAt = 0;
  let rafId = 0;

  const syntheticEvent: InputEventLike = {
    preventDefault() {},
    stopPropagation() {},
  };

  const loop = () => {
    const gamepad = getConnectedGamepad();
    const rawState = readGamepadRawState(gamepad);
    const captureTarget = getCaptureTarget();

    if (captureTarget?.source === 'gamepadControls') {
      const binding = findFreshGamepadBinding(rawState, previousRaw);
      if (binding) {
        onCaptureBinding(binding);
      }
    } else if (!context.isInputBlocked()) {
      for (const action of CONTROL_ORDER) {
        const active = isActionActiveFromGamepad(action, context.settings, gamepad);
        const wasActive = previousActions[action];

        if (action === 'left' || action === 'right') {
          if (active && !wasActive) {
            handleAction(action, syntheticEvent, false);
          } else if (!active && wasActive) {
            releaseHorizontal(action);
          }
        } else if (action === 'soft') {
          if (!active) {
            nextSoftRepeatAt = 0;
          } else {
            const now = performance.now();
            if (!wasActive || now >= nextSoftRepeatAt) {
              handleAction(action, syntheticEvent, wasActive);
              nextSoftRepeatAt = now + 42;
            }
          }
        } else if (active && !wasActive) {
          handleAction(action, syntheticEvent, false);
        }

        previousActions[action] = active;
      }
    } else {
      for (const action of CONTROL_ORDER) {
        if ((action === 'left' || action === 'right') && previousActions[action]) {
          releaseHorizontal(action);
        }
        previousActions[action] = false;
      }
      nextSoftRepeatAt = 0;
    }

    previousRaw = rawState;
    rafId = window.requestAnimationFrame(loop);
  };

  rafId = window.requestAnimationFrame(loop);

  return () => {
    window.cancelAnimationFrame(rafId);
    releaseHorizontal('left');
    releaseHorizontal('right');
  };
}
