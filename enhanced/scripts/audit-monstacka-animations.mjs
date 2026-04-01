import fs from 'node:fs/promises';
import path from 'node:path';
import { chromium } from 'playwright';

const AUDIT_DIR = path.resolve('audit-artifacts');
const BASE_URL = 'http://127.0.0.1:4173';

async function ensureDir(dir) {
  await fs.mkdir(dir, { recursive: true });
}

async function screenshot(page, name) {
  const file = path.join(AUDIT_DIR, name);
  await page.screenshot({ path: file, type: 'png' });
  return file;
}

async function getNumberSeries(page, selector, cssVar, samples = 8, waitMs = 180) {
  const series = [];
  for (let index = 0; index < samples; index += 1) {
    const values = await page.$$eval(
      selector,
      (nodes, prop) => nodes.map((node) => Number.parseFloat(node.style.getPropertyValue(prop)) || 0),
      cssVar,
    );
    series.push(values);
    if (index < samples - 1) {
      await page.waitForTimeout(waitMs);
    }
  }
  return series;
}

function maxDelta(series) {
  const flattened = series.flat();
  if (!flattened.length) {
    return 0;
  }
  return Math.max(...flattened) - Math.min(...flattened);
}

async function activateMonstos(page, targetName) {
  for (let index = 0; index < 10; index += 1) {
    const current = (await page.locator('#monstosName').textContent())?.trim();
    if (current === targetName) {
      return;
    }
    await page.click('#monstosNextButton');
    await page.waitForTimeout(220);
  }
  throw new Error(`Could not activate Monstos ${targetName}`);
}

async function waitForMonsterPreviewReady(page) {
  for (let index = 0; index < 40; index += 1) {
    const ready = await page.evaluate(() => {
      const center = document.getElementById('monstosCenter');
      return !!center && !center.classList.contains('preview-loading') && center.querySelector('.monster-body');
    });
    if (ready) {
      return;
    }
    await page.waitForTimeout(150);
  }
  throw new Error('Monster preview did not finish loading in time.');
}

async function sampleBlink(page) {
  const series = await getNumberSeries(page, '#monstosCenter .monster-eye', '--blink', 30, 120);
  return {
    maxBlink: Math.max(...series.flat(), 0),
    samples: series.length,
  };
}

async function sampleLook(page, selector) {
  const lookXSeries = await getNumberSeries(page, selector, '--look-x', 8, 180);
  const lookYSeries = await getNumberSeries(page, selector, '--look-y', 8, 180);
  return {
    xDelta: maxDelta(lookXSeries),
    yDelta: maxDelta(lookYSeries),
  };
}

async function sampleTongue(page, selector) {
  const series = await getNumberSeries(page, selector, '--tongue-sway', 12, 140);
  return {
    maxDelta: maxDelta(series),
    samples: series.length,
  };
}

async function ensureBoardDetails(page) {
  for (let index = 0; index < 12; index += 1) {
    const eyeCount = await page.locator('#board .monster-eye').count();
    const tongueCount = await page.locator('#board .monster-tongue').count();
    if (eyeCount > 0 && tongueCount > 0) {
      return;
    }
    await page.keyboard.press('Space');
    await page.waitForTimeout(180);
  }
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

  report.preview.initialLook = await sampleLook(page, '#monstosCenter .monster-eye');
  report.screenshots.previewInitial = await screenshot(page, 'animation-preview-initial.png');

  await activateMonstos(page, 'SORRISOL');
  report.preview.redBlink = await sampleBlink(page);

  await activateMonstos(page, 'DOUSEMA');
  report.preview.pinkBlink = await sampleBlink(page);

  await activateMonstos(page, 'GALIFFAMBOS');
  report.preview.orangeBlink = await sampleBlink(page);

  await activateMonstos(page, 'LYSERGICADA');
  report.preview.tongue = await sampleTongue(page, '#monstosCenter .monster-tongue');
  report.screenshots.previewTongue = await screenshot(page, 'animation-preview-tongue.png');

  await page.goto(BASE_URL, { waitUntil: 'domcontentloaded' });
  await waitForMonsterPreviewReady(page);
  await page.click('#startArcadeButton');
  await page.waitForTimeout(1300);
  await ensureBoardDetails(page);
  await page.waitForTimeout(240);

  report.gameplay.boardLook = await sampleLook(page, '#board .monster-eye');
  report.gameplay.boardBlink = {
    maxBlink: Math.max(...(await getNumberSeries(page, '#board .monster-eye', '--blink', 24, 120)).flat(), 0),
  };
  report.gameplay.boardTongue = await sampleTongue(page, '#board .monster-tongue');
  report.gameplay.motionTransforms = await page.$$eval(
    '#board .monster-motion',
    (nodes) => nodes.slice(0, 8).map((node) => getComputedStyle(node).transform),
  );
  report.screenshots.gameplayBoard = await screenshot(page, 'animation-gameplay-board.png');

  await fs.writeFile(
    path.join(AUDIT_DIR, 'animation-audit.json'),
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
