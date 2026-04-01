import { describe, expect, it } from 'vitest';
import { DEFAULT_CONTROLS } from '../../constants';
import {
  assignBinding,
  findActionForKeyboard,
  findActionForMouse,
  formatBindingLabel,
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

  it('keeps bindings unique when assigning a new input', () => {
    const bindings = assignBinding(DEFAULT_CONTROLS, 'hold', keyboardToken('ArrowLeft'));

    expect(bindings.hold).toBe('Key:ArrowLeft');
    expect(bindings.left).toBe('');
  });

  it('formats friendly labels for the controls table', () => {
    expect(formatBindingLabel('Key:ArrowLeft')).toBe('Left Arrow');
    expect(formatBindingLabel('Key:KeyZ')).toBe('Z');
    expect(formatBindingLabel('Mouse:4')).toBe('Mouse 5');
    expect(formatBindingLabel('')).toBe('Unbound');
  });
});
