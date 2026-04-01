import { CONTROL_ORDER, CONTROL_LABELS } from '../constants';
import type { ControlAction, ControlBindings } from '../types';

export function keyboardToken(code: string): string {
  return `Key:${code}`;
}

export function mouseToken(button: number): string {
  return `Mouse:${button}`;
}

export function findActionForKeyboard(bindings: ControlBindings, code: string): ControlAction | null {
  const token = keyboardToken(code);
  for (const action of CONTROL_ORDER) {
    if (bindings[action] === token) {
      return action;
    }
  }
  return null;
}

export function findActionForMouse(bindings: ControlBindings, button: number): ControlAction | null {
  const token = mouseToken(button);
  for (const action of CONTROL_ORDER) {
    if (bindings[action] === token) {
      return action;
    }
  }
  return null;
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
