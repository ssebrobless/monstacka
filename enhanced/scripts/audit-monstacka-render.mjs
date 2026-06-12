import fs from 'node:fs/promises';
import path from 'node:path';
import { chromium } from 'playwright';

const AUDIT_DIR = path.resolve('audit-artifacts');
const BASE_URL = 'http://127.0.0.1:4173?debug=1';
const CELL_SIZE = 112;

const PROFILES = [
  { name: 'SORRISOL', pieceType: 'S', rotation: 0, cells: [{ x: 0, y: 1 }, { x: 1, y: 1 }, { x: 1, y: 0 }, { x: 2, y: 0 }] },
  { name: 'BLYNDOOLIE', pieceType: 'I', rotation: 1, cells: [{ x: 2, y: 0 }, { x: 2, y: 1 }, { x: 2, y: 2 }, { x: 2, y: 3 }] },
  { name: 'AGGRASO', pieceType: 'Z', rotation: 0, cells: [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }] },
  { name: 'MUWERDE', pieceType: 'O', rotation: 0, cells: [{ x: 1, y: 0 }, { x: 2, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 1 }] },
  { name: 'LYSERGICADA', pieceType: 'T', rotation: 2, cells: [{ x: 0, y: 1 }, { x: 1, y: 1 }, { x: 2, y: 1 }, { x: 1, y: 2 }] },
  { name: 'DOUSEMA', pieceType: 'J', rotation: 3, cells: [{ x: 1, y: 0 }, { x: 1, y: 1 }, { x: 1, y: 2 }, { x: 0, y: 2 }] },
  { name: 'GALIFFAMBOS', pieceType: 'L', rotation: 1, cells: [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }, { x: 1, y: 2 }] },
];

async function ensureDir(dir) {
  await fs.mkdir(dir, { recursive: true });
}

async function screenshot(page, name) {
  const file = path.join(AUDIT_DIR, name);
  await page.screenshot({ path: file, type: 'png' });
  return file;
}

async function waitForMonsterPreviewReady(page) {
  for (let index = 0; index < 40; index += 1) {
    const ready = await page.evaluate(() => {
      const center = document.getElementById('monstosCenter');
      return !!center && !center.classList.contains('preview-loading') && center.querySelector('.monster-art');
    });
    if (ready) {
      return;
    }
    await page.waitForTimeout(150);
  }
  throw new Error('Monster preview did not finish loading in time.');
}

async function activateMonstos(page, targetName) {
  for (let index = 0; index < 12; index += 1) {
    const current = (await page.locator('#monstosName').textContent())?.trim();
    if (current === targetName) {
      return;
    }
    await page.click('#monstosNextButton');
    await page.waitForTimeout(240);
  }
  throw new Error(`Could not activate Monstos ${targetName}`);
}

function normalizeCells(cells) {
  const minX = Math.min(...cells.map((cell) => cell.x));
  const minY = Math.min(...cells.map((cell) => cell.y));
  return cells.map((cell) => ({
    x: cell.x - minX,
    y: cell.y - minY,
  }));
}

async function samplePreviewFigure(page, profile, samples = 10, waitMs = 240) {
  const normalizedCells = normalizeCells(profile.cells);
  const series = [];

  for (let index = 0; index < samples; index += 1) {
    const sample = await page.evaluate(({ normalizedCells, cellSize }) => {
      const canvas = document.querySelector('#monstosCenter .monster-art');
      const ripple = document.querySelector('#monstosCenter .monster-ripple-art');
      const stage = document.getElementById('monstosCenter');

      if (!(canvas instanceof HTMLCanvasElement) || !(stage instanceof HTMLElement)) {
        return null;
      }

      const ctx = canvas.getContext('2d', { willReadFrequently: true });
      if (!ctx) {
        return null;
      }

      const widthCells = Math.round(canvas.width / cellSize);
      const heightCells = Math.round(canvas.height / cellSize);
      const occupied = new Set(normalizedCells.map((cell) => `${cell.x}:${cell.y}`));
      const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
      const alpha = imageData.data;

      const emptyCells = [];
      for (let y = 0; y < heightCells; y += 1) {
        for (let x = 0; x < widthCells; x += 1) {
          if (!occupied.has(`${x}:${y}`)) {
            emptyCells.push({ x, y });
          }
        }
      }

      const emptyLeaks = emptyCells.map((cell) => {
        let opaquePixels = 0;
        const startX = cell.x * cellSize;
        const startY = cell.y * cellSize;
        for (let y = startY; y < startY + cellSize; y += 1) {
          for (let x = startX; x < startX + cellSize; x += 1) {
            if (alpha[(y * canvas.width + x) * 4 + 3] >= 16) {
              opaquePixels += 1;
            }
          }
        }
        return {
          cell: `${cell.x}:${cell.y}`,
          opaquePixels,
        };
      });

      const seamHits = [];
      for (const cell of normalizedCells) {
        const rightKey = `${cell.x + 1}:${cell.y}`;
        const downKey = `${cell.x}:${cell.y + 1}`;

        if (occupied.has(rightKey)) {
          const seamX = (cell.x + 1) * cellSize;
          let hits = 0;
          const startY = cell.y * cellSize;
          for (let y = startY; y < startY + cellSize; y += 1) {
            for (let dx = -2; dx <= 1; dx += 1) {
              const x = seamX + dx;
              if (x >= 0 && x < canvas.width && alpha[(y * canvas.width + x) * 4 + 3] >= 16) {
                hits += 1;
              }
            }
          }
          seamHits.push({ seam: `${cell.x}:${cell.y}-right`, hits });
        }

        if (occupied.has(downKey)) {
          const seamY = (cell.y + 1) * cellSize;
          let hits = 0;
          const startX = cell.x * cellSize;
          for (let x = startX; x < startX + cellSize; x += 1) {
            for (let dy = -2; dy <= 1; dy += 1) {
              const y = seamY + dy;
              if (y >= 0 && y < canvas.height && alpha[(y * canvas.width + x) * 4 + 3] >= 16) {
                hits += 1;
              }
            }
          }
          seamHits.push({ seam: `${cell.x}:${cell.y}-down`, hits });
        }
      }

      return {
        size: { width: canvas.width, height: canvas.height },
        hasRipple: ripple instanceof HTMLCanvasElement,
        loadingTextVisible: (stage.textContent || '').toLowerCase().includes('loading'),
        eyeOverlayCount: stage.querySelectorAll('.monster-eye-overlay').length,
        emptyLeaks,
        seamHits,
      };
    }, { normalizedCells, cellSize: CELL_SIZE });

    if (!sample) {
      throw new Error(`Could not sample preview figure for ${profile.name}.`);
    }

    series.push(sample);
    if (index < samples - 1) {
      await page.waitForTimeout(waitMs);
    }
  }

  const maxEmptyLeak = Math.max(
    ...series.flatMap((sample) => sample.emptyLeaks.map((leak) => leak.opaquePixels)),
    0,
  );
  const minSeamHit = Math.min(
    ...series.flatMap((sample) => sample.seamHits.map((seam) => seam.hits)),
  );

  return {
    size: series[0].size,
    hasRipple: series.every((sample) => sample.hasRipple),
    loadingTextVisible: series.some((sample) => sample.loadingTextVisible),
    eyeOverlayCount: Math.max(...series.map((sample) => sample.eyeOverlayCount), 0),
    maxEmptyLeak,
    minSeamHit,
    seamHits: series[0].seamHits.map((seam) => ({
      seam: seam.seam,
      minHits: Math.min(...series.map((sample) => sample.seamHits.find((entry) => entry.seam === seam.seam)?.hits ?? 0)),
      maxHits: Math.max(...series.map((sample) => sample.seamHits.find((entry) => entry.seam === seam.seam)?.hits ?? 0)),
    })),
    emptyLeakCells: series[0].emptyLeaks.map((leak) => ({
      cell: leak.cell,
      maxOpaquePixels: Math.max(...series.map((sample) => sample.emptyLeaks.find((entry) => entry.cell === leak.cell)?.opaquePixels ?? 0)),
    })),
  };
}

async function main() {
  await ensureDir(AUDIT_DIR);

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1600, height: 900 } });
  const page = await context.newPage();

  const report = {
    generatedAt: new Date().toISOString(),
    preview: {},
    gameplay: {},
    screenshots: {},
  };

  await page.goto(BASE_URL, { waitUntil: 'domcontentloaded' });
  await waitForMonsterPreviewReady(page);

  for (const profile of PROFILES) {
    await activateMonstos(page, profile.name);
    await page.waitForTimeout(240);
    report.preview[profile.name] = await samplePreviewFigure(page, profile);
    report.screenshots[profile.name] = await screenshot(
      page,
      `render-preview-${profile.pieceType.toLowerCase()}.png`,
    );
  }

  await page.goto(BASE_URL, { waitUntil: 'domcontentloaded' });
  await waitForMonsterPreviewReady(page);
  await page.click('#startArcadeButton');
  await page.waitForTimeout(1400);

  for (let index = 0; index < 8; index += 1) {
    await page.keyboard.press('Space');
    await page.waitForTimeout(160);
  }

  report.gameplay = await page.evaluate(() => ({
    boardFigures: document.querySelectorAll('#boardMonsterLayer .board-piece-figure').length,
    boardArtCanvases: document.querySelectorAll('#boardMonsterLayer .monster-art').length,
    boardRippleCanvases: document.querySelectorAll('#boardMonsterLayer .monster-ripple-art').length,
    holdArtCanvases: document.querySelectorAll('#hold .monster-art').length,
    nextArtCanvases: document.querySelectorAll('#nextQueue .monster-art').length,
    nextRippleCanvases: document.querySelectorAll('#nextQueue .monster-ripple-art').length,
  }));
  report.screenshots.gameplay = await screenshot(page, 'render-gameplay-board.png');

  await fs.writeFile(
    path.join(AUDIT_DIR, 'render-audit.json'),
    JSON.stringify(report, null, 2),
    'utf8',
  );

  console.log(JSON.stringify(report, null, 2));
  await browser.close();
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
