import { describe, expect, it } from 'vitest';
import { DEFAULT_CONTROLS, DEFAULT_GAMEPAD_CONTROLS } from '../../constants';
import {
  assignBinding,
  findActionForGamepadAxis,
  findActionForGamepadButton,
  findActionForKeyboard,
  findActionForMouse,
  formatBindingLabel,
  gamepadAxisToken,
  gamepadButtonToken,
  keyboardToken,
  mouseToken,
} from '../../input/bindings';

describe('control bindings', () => {
  it('finds keyboard and mouse actions from saved bindings', () => {
    const bindings = {
      ...DEFAULT_CONTROLS,
      hold: mouseToken(1),
    };

    expect(findActionForKeyboard(bindings, 'ArrowLeft')).toBe('left');
    expect(findActionForMouse(bindings, 1)).toBe('hold');
    expect(findActionForMouse(bindings, 4)).toBeNull();
  });

  it('finds controller button and stick actions from saved bindings', () => {
    const bindings = {
      ...DEFAULT_GAMEPAD_CONTROLS,
      left: gamepadAxisToken(0, -1),
      hold: gamepadButtonToken(5),
    };

    expect(findActionForGamepadAxis(bindings, 0, -1)).toBe('left');
    expect(findActionForGamepadButton(bindings, 5)).toBe('hold');
    expect(findActionForGamepadButton(bindings, 1)).toBe('cw');
    expect(findActionForGamepadAxis(bindings, 0, 1)).toBeNull();
  });

  it('keeps bindings unique when assigning a new input', () => {
    const bindings = assignBinding(DEFAULT_CONTROLS, 'hold', keyboardToken('ArrowLeft'));

    expect(bindings.hold).toBe('Key:ArrowLeft');
    expect(bindings.left).toBe('');
  });

  it('formats friendly labels for the controls table', () => {
    expect(formatBindingLabel('Key:ArrowLeft')).toBe('Left Arrow');
    expect(formatBindingLabel('Key:KeyZ')).toBe('Z');
    expect(formatBindingLabel('Mouse:4')).toBe('Mouse 5');
    expect(formatBindingLabel('Pad:Button14')).toBe('D-pad Left');
    expect(formatBindingLabel('Pad:Axis0:-1')).toBe('Left Stick Left');
    expect(formatBindingLabel('')).toBe('Unbound');
  });
});
