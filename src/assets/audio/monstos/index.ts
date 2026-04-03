import type { PieceType } from '../../../types';

import sImpact1 from './s-impact-1.mp3';
import sImpact2 from './s-impact-2.mp3';
import sImpact3 from './s-impact-3.mp3';
import sNeutral1 from './s-neutral-1.mp3';
import sNeutral2 from './s-neutral-2.mp3';

import iImpact1 from './i-impact-1.mp3';
import iImpact2 from './i-impact-2.mp3';
import iImpact3 from './i-impact-3.mp3';
import iNeutral1 from './i-neutral-1.mp3';
import iNeutral2 from './i-neutral-2.mp3';

import zImpact1 from './z-impact-1.mp3';
import zImpact2 from './z-impact-2.mp3';
import zImpact3 from './z-impact-3.mp3';
import zNeutral1 from './z-neutral-1.mp3';
import zNeutral2 from './z-neutral-2.mp3';

import oImpact1 from './o-impact-1.mp3';
import oImpact2 from './o-impact-2.mp3';
import oImpact3 from './o-impact-3.mp3';
import oNeutral1 from './o-neutral-1.mp3';
import oNeutral2 from './o-neutral-2.mp3';

import tImpact1 from './t-impact-1.mp3';
import tImpact2 from './t-impact-2.mp3';
import tImpact3 from './t-impact-3.mp3';
import tNeutral1 from './t-neutral-1.mp3';
import tNeutral2 from './t-neutral-2.mp3';

import jImpact1 from './j-impact-1.mp3';
import jImpact2 from './j-impact-2.mp3';
import jImpact3 from './j-impact-3.mp3';
import jNeutral1 from './j-neutral-1.mp3';
import jNeutral2 from './j-neutral-2.mp3';

import lImpact1 from './l-impact-1.mp3';
import lImpact2 from './l-impact-2.mp3';
import lNeutral1 from './l-neutral-1.mp3';
import lNeutral2 from './l-neutral-2.mp3';

export const MONSTER_AUDIO_URLS: Record<PieceType, { impact: string[]; neutral: string[] }> = {
  S: { impact: [sImpact1, sImpact2, sImpact3], neutral: [sNeutral1, sNeutral2] },
  I: { impact: [iImpact1, iImpact2, iImpact3], neutral: [iNeutral1, iNeutral2] },
  Z: { impact: [zImpact1, zImpact2, zImpact3], neutral: [zNeutral1, zNeutral2] },
  O: { impact: [oImpact1, oImpact2, oImpact3], neutral: [oNeutral1, oNeutral2] },
  T: { impact: [tImpact1, tImpact2, tImpact3], neutral: [tNeutral1, tNeutral2] },
  J: { impact: [jImpact1, jImpact2, jImpact3], neutral: [jNeutral1, jNeutral2] },
  L: { impact: [lImpact1, lImpact2], neutral: [lNeutral1, lNeutral2] },
};
