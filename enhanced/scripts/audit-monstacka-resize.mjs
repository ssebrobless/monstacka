import fs from 'node:fs/promises';
import path from 'node:path';
import { chromium } from 'playwright';

const AUDIT_DIR = path.resolve('audit-artifacts');
const BASE_URL = 'http://127.0.0.1:4173?debug=1';
const VIEWPORTS = [
  { name: 'fullscreen-ish', width: 1600, height: 900 },
  { name: 'medium', width: 1280, height: 720 },
  { name: 'small', width: 900, height: 560 },
  { name: 'tiny', width: 760, height: 460 },
];

async function ensureDir(dir) {
  await fs.mkdir(dir, { recursive: true });
}

async function screenshot(page, name) {
  const file = path.join(AUDIT_DIR, name);
  await page.screenshot({ path: file, type: 'png' });
  return file;
}

async function collectStageMetrics(page, screenId) {
  return page.evaluate((targetId) => {
    const artboard = document.getElementById(targetId);
    const root = document.documentElement;
    const body = document.body;
    const rootStyles = getComputedStyle(root);
    const scaleValue = Number.parseFloat(rootStyles.getPropertyValue('--artboard-scale')) || 0;

    const rect = artboard?.getBoundingClientRect();
    return {
      viewport: {
        width: window.innerWidth,
        height: window.innerHeight,
      },
      scale: scaleValue,
      scroll: {
        width: root.scrollWidth,
        height: root.scrollHeight,
      },
      bodyScroll: {
        width: body.scrollWidth,
        height: body.scrollHeight,
      },
      artboardRect: rect ? {
        left: rect.left,
        top: rect.top,
        right: rect.right,
        bottom: rect.bottom,
        width: rect.width,
        height: rect.height,
      } : null,
    };
  }, screenId);
}

async function collectModalMetrics(page) {
  return page.evaluate(() => {
    const modal = document.getElementById('settingsModal');
    const card = modal?.querySelector('.modal-card');
    const modalRect = modal?.getBoundingClientRect();
    const cardRect = card?.getBoundingClientRect();
    return {
      modalHidden: modal?.classList.contains('hidden') ?? true,
      modalRect: modalRect ? {
        left: modalRect.left,
        top: modalRect.top,
        right: modalRect.right,
        bottom: modalRect.bottom,
        width: modalRect.width,
        height: modalRect.height,
      } : null,
      cardRect: cardRect ? {
        left: cardRect.left,
        top: cardRect.top,
        right: cardRect.right,
        bottom: cardRect.bottom,
        width: cardRect.width,
        height: cardRect.height,
      } : null,
    };
  });
}

function fitsWithinViewport(rect, viewport) {
  if (!rect) {
    return false;
  }
  return rect.left >= -1
    && rect.top >= -1
    && rect.right <= viewport.width + 1
    && rect.bottom <= viewport.height + 1;
}

async function main() {
  await ensureDir(AUDIT_DIR);

  const browser = await chromium.launch({ headless: true });
  const report = {
    generatedAt: new Date().toISOString(),
    home: {},
    homeSettings: {},
    game: {},
    screenshots: {},
  };

  for (const viewport of VIEWPORTS) {
    const context = await browser.newContext({ viewport: { width: viewport.width, height: viewport.height } });
    const page = await context.newPage();

    await page.goto(BASE_URL, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(900);

    const homeMetrics = await collectStageMetrics(page, 'homeArtboard');
    report.home[viewport.name] = {
      ...homeMetrics,
      artboardFitsViewport: fitsWithinViewport(homeMetrics.artboardRect, homeMetrics.viewport),
    };
    report.screenshots[`home-${viewport.name}`] = await screenshot(page, `resize-home-${viewport.name}.png`);

    await page.click('#openSettingsButtonHome');
    await page.waitForTimeout(160);
    const modalMetrics = await collectModalMetrics(page);
    report.homeSettings[viewport.name] = {
      ...modalMetrics,
      cardFitsViewport: fitsWithinViewport(modalMetrics.cardRect, homeMetrics.viewport),
    };
    report.screenshots[`settings-${viewport.name}`] = await screenshot(page, `resize-settings-${viewport.name}.png`);
    await page.click('#closeSettingsButton');
    await page.waitForTimeout(120);

    await page.click('#startArcadeButton');
    await page.waitForTimeout(1400);
    const gameMetrics = await collectStageMetrics(page, 'gameArtboard');
    report.game[viewport.name] = {
      ...gameMetrics,
      artboardFitsViewport: fitsWithinViewport(gameMetrics.artboardRect, gameMetrics.viewport),
    };
    report.screenshots[`game-${viewport.name}`] = await screenshot(page, `resize-game-${viewport.name}.png`);

    await context.close();
  }

  await fs.writeFile(
    path.join(AUDIT_DIR, 'resize-audit.json'),
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
