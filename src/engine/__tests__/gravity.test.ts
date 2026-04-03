import { describe, expect, it } from 'vitest';
import { getGravityMs } from '../gravity';

describe('getGravityMs', () => {
  it('keeps sprint40 at flat gravity', () => {
    expect(getGravityMs('sprint40', 0)).toBe(650);
    expect(getGravityMs('sprint40', 80)).toBe(650);
    expect(getGravityMs('sprint40', 160)).toBe(650);
  });

  it('returns correct arcade gravity at each threshold', () => {
    expect(getGravityMs('arcade', 0)).toBe(650);
    expect(getGravityMs('arcade', 9)).toBe(650);
    expect(getGravityMs('arcade', 10)).toBe(500);
    expect(getGravityMs('arcade', 19)).toBe(500);
    expect(getGravityMs('arcade', 20)).toBe(400);
    expect(getGravityMs('arcade', 29)).toBe(400);
    expect(getGravityMs('arcade', 30)).toBe(300);
    expect(getGravityMs('arcade', 39)).toBe(300);
    expect(getGravityMs('arcade', 40)).toBe(220);
    expect(getGravityMs('arcade', 59)).toBe(220);
    expect(getGravityMs('arcade', 60)).toBe(150);
    expect(getGravityMs('arcade', 79)).toBe(150);
    expect(getGravityMs('arcade', 80)).toBe(100);
    expect(getGravityMs('arcade', 99)).toBe(100);
    expect(getGravityMs('arcade', 100)).toBe(70);
    expect(getGravityMs('arcade', 119)).toBe(70);
    expect(getGravityMs('arcade', 120)).toBe(50);
    expect(getGravityMs('arcade', 139)).toBe(50);
    expect(getGravityMs('arcade', 140)).toBe(33);
    expect(getGravityMs('arcade', 999)).toBe(33);
  });
});
