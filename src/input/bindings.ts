import { CONTROL_ORDER, CONTROL_LABELS } from '../constants';
import type { ControlAction, ControlBindings } from '../types';

export function keyboardToken(code: string): string {
  return `Key:${code}`;
}

export function mouseToken(button: number): string {
  return `Mouse:${button}`;
}

export function gamepadButtonToken(button: number): string {
  return `Pad:Button${button}`;
}

export function gamepadAxisToken(axis: number, direction: -1 | 1): string {
  return `Pad:Axis${axis}:${direction}`;
}

function findActionForToken(bindings: ControlBindings, token: string): ControlAction | null {
  for (const action of CONTROL_ORDER) {
    if (bindings[action] === token) {
      return action;
    }
  }
  return null;
}

export function findActionForKeyboard(bindings: ControlBindings, code: string): ControlAction | null {
  return findActionForToken(bindings, keyboardToken(code));
}

export function findActionForMouse(bindings: ControlBindings, button: number): ControlAction | null {
  return findActionForToken(bindings, mouseToken(button));
}

export function findActionForGamepadButton(bindings: ControlBindings, button: number): ControlAction | null {
  return findActionForToken(bindings, gamepadButtonToken(button));
}

export function findActionForGamepadAxis(
  bindings: ControlBindings,
  axis: number,
  direction: -1 | 1,
): ControlAction | null {
  return findActionForToken(bindings, gamepadAxisToken(axis, direction));
}

export function formatBindingLabel(binding: string): string {
  if (!binding) {
    return 'Unbound';
  }

  if (binding.startsWith('Mouse:')) {
    const button = Number(binding.slice('Mouse:'.length));
    switch (button) {
      case 0:
        return 'Mouse 1';
      case 1:
        return 'Mouse 2';
      case 2:
        return 'Mouse 3';
      case 3:
        return 'Mouse 4';
      case 4:
        return 'Mouse 5';
      default:
        return `Mouse ${button}`;
    }
  }

  if (binding.startsWith('Pad:Button')) {
    const button = Number(binding.slice('Pad:Button'.length));
    const buttonLabels: Record<number, string> = {
      0: 'A / Cross',
      1: 'B / Circle',
      2: 'X / Square',
      3: 'Y / Triangle',
      4: 'L1 / LB',
      5: 'R1 / RB',
      6: 'L2 / LT',
      7: 'R2 / RT',
      8: 'Back / Select',
      9: 'Start',
      10: 'L3',
      11: 'R3',
      12: 'D-pad Up',
      13: 'D-pad Down',
      14: 'D-pad Left',
      15: 'D-pad Right',
      16: 'Guide',
    };
    return buttonLabels[button] ?? `Pad Button ${button}`;
  }

  if (binding.startsWith('Pad:Axis')) {
    const match = binding.match(/^Pad:Axis(\d+):(-?1)$/);
    if (match) {
      const axis = Number(match[1]);
      const direction = Number(match[2]) as -1 | 1;
      const axisLabels: Record<string, string> = {
        '0:-1': 'Left Stick Left',
        '0:1': 'Left Stick Right',
        '1:-1': 'Left Stick Up',
        '1:1': 'Left Stick Down',
        '2:-1': 'Right Stick Left',
        '2:1': 'Right Stick Right',
        '3:-1': 'Right Stick Up',
        '3:1': 'Right Stick Down',
      };
      return axisLabels[`${axis}:${direction}`] ?? `Axis ${axis} ${direction < 0 ? '-' : '+'}`;
    }
  }

  const code = binding.startsWith('Key:') ? binding.slice('Key:'.length) : binding;
  if (code.startsWith('Key')) {
    return code.slice(3).toUpperCase();
  }
  if (code.startsWith('Digit')) {
    return code.slice(5);
  }

  const keyLabels: Record<string, string> = {
    ArrowLeft: 'Left Arrow',
    ArrowRight: 'Right Arrow',
    ArrowUp: 'Up Arrow',
    ArrowDown: 'Down Arrow',
    Space: 'Space',
    ShiftLeft: 'Left Shift',
    ShiftRight: 'Right Shift',
    ControlLeft: 'Left Ctrl',
    ControlRight: 'Right Ctrl',
    AltLeft: 'Left Alt',
    AltRight: 'Right Alt',
    Escape: 'Esc',
    Enter: 'Enter',
    Backspace: 'Backspace',
    Tab: 'Tab',
  };

  return keyLabels[code] ?? code;
}

export function assignBinding(bindings: ControlBindings, action: ControlAction, binding: string): ControlBindings {
  const next = { ...bindings };

  if (binding) {
    for (const otherAction of CONTROL_ORDER) {
      if (otherAction !== action && next[otherAction] === binding) {
        next[otherAction] = '';
      }
    }
  }

  next[action] = binding;
  return next;
}

export function getControlHelpRows(bindings: ControlBindings): Array<{ action: ControlAction; label: string; binding: string }> {
  return CONTROL_ORDER.map((action) => ({
    action,
    label: CONTROL_LABELS[action],
    binding: bindings[action],
  }));
}
