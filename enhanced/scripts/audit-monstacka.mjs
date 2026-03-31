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

async function textList(page, selector) {
  return page.locator(selector).evaluateAll((nodes) =>
    nodes.map((node) => (node.textContent || '').trim()).filter(Boolean));
}

async function getPreviewSnapshot(page) {
  return page.evaluate(() => {
    const stage = (id) => {
      const el = document.getElementById(id);
      return {
        childCount: el?.children.length ?? 0,
        htmlLength: el?.innerHTML.length ?? 0,
        text: (el?.textContent || '').trim(),
      };
    };

    return {
      name: (document.getElementById('monstosName')?.textContent || '').trim(),
      lore: (document.getElementById('monstosLoreText')?.textContent || '').trim(),
      loreBubbleClass: document.getElementById('monstosLoreBubble')?.className || '',
      left: stage('monstosLeft'),
      center: stage('monstosCenter'),
      right: stage('monstosRight'),
    };
  });
}

async function getGameSnapshot(page) {
  return page.evaluate(() => ({
    overlayHidden: document.getElementById('overlay')?.classList.contains('hidden') ?? false,
    overlayText: (document.getElementById('overlay')?.textContent || '').trim(),
    status: (document.getElementById('statusText')?.textContent || '').trim(),
    score: (document.getElementById('score')?.textContent || '').trim(),
    lines: (document.getElementById('lines')?.textContent || '').trim(),
    timer: (document.getElementById('timer')?.textContent || '').trim(),
    boardMonsterCells: document.querySelectorAll('#board .monster-cell').length,
    boardGhostCells: document.querySelectorAll('#board .ghost').length,
    retryVisible: !document.getElementById('retryButton')?.closest('.hidden'),
    resumeVisible: !(document.getElementById('resumeModal')?.classList.contains('hidden') ?? true),
  }));
}

async function main() {
  await ensureDir(AUDIT_DIR);
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1600, height: 900 } });
  const page = await context.newPage();

  const consoleMessages = [];
  const pageErrors = [];
  page.on('console', (message) => {
    consoleMessages.push(`${message.type()}: ${message.text()}`);
  });
  page.on('pageerror', (error) => {
    pageErrors.push(error.message);
  });

  const report = {
    generatedAt: new Date().toISOString(),
    screenshots: {},
    preview: {},
    leaderboards: {},
    gameModes: {},
    consoleMessages,
    pageErrors,
  };

  await page.goto(BASE_URL, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(700);
  report.screenshots.home = await screenshot(page, 'home-initial.png');
  report.preview.initial = await getPreviewSnapshot(page);

  await page.click('#monstosLoreButton');
  await page.waitForTimeout(250);
  report.preview.afterLoreToggle = await getPreviewSnapshot(page);
  report.screenshots.homeLoreCollapsed = await screenshot(page, 'home-lore-collapsed.png');

  await page.click('#monstosVoiceButton');
  await page.waitForTimeout(150);
  report.preview.afterVoiceButton = await getPreviewSnapshot(page);

  await page.click('#monstosNextButton');
  await page.waitForTimeout(250);
  report.preview.afterNext = await getPreviewSnapshot(page);
  report.screenshots.homeAfterNext = await screenshot(page, 'home-after-next.png');

  await page.click('#leaderboardSprintButton');
  await page.waitForTimeout(150);
  report.leaderboards.sprint = await textList(page, '#homeLeaderboard .home-score-value');
  report.leaderboards.sprintTags = await textList(page, '#homeLeaderboard .home-score-tag');

  await page.click('#leaderboardArcadeButton');
  await page.waitForTimeout(150);
  report.leaderboards.arcade = await textList(page, '#homeLeaderboard .home-score-value');
  report.leaderboards.arcadeTags = await textList(page, '#homeLeaderboard .home-score-tag');

  const modes = [
    ['arcade', '#startArcadeButton'],
    ['sprint40', '#startSprintButton'],
    ['training', '#startTrainingButton'],
  ];

  for (const [mode, selector] of modes) {
    await page.goto(BASE_URL, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(500);
    await page.click(selector);
    await page.waitForTimeout(1300);
    const gameStart = await getGameSnapshot(page);
    report.gameModes[mode] = { start: gameStart };
    report.screenshots[`${mode}Start`] = await screenshot(page, `${mode}-start.png`);

    if (mode !== 'training') {
      await page.keyboard.press('ArrowLeft');
      await page.waitForTimeout(80);
      await page.keyboard.press('Space');
      await page.waitForTimeout(140);
    } else {
      await page.keyboard.press('ArrowRight');
      await page.waitForTimeout(80);
      await page.keyboard.press('Space');
      await page.waitForTimeout(140);
    }

    report.gameModes[mode].afterInput = await getGameSnapshot(page);
    report.screenshots[`${mode}AfterInput`] = await screenshot(page, `${mode}-after-input.png`);

    await page.keyboard.press('KeyP');
    await page.waitForTimeout(120);
    report.gameModes[mode].paused = await getGameSnapshot(page);

    await page.keyboard.press('KeyP');
    await page.waitForTimeout(120);
    report.gameModes[mode].resumed = await getGameSnapshot(page);

    await page.keyboard.press('KeyP');
    await page.waitForTimeout(120);
    await page.keyboard.press('KeyO');
    await page.waitForTimeout(350);
    report.gameModes[mode].afterPausedRestart = await getGameSnapshot(page);

    await page.click('#homeButtonGame');
    await page.waitForTimeout(250);
    report.gameModes[mode].afterHome = {
      homeVisible: await page.locator('#homeScreen').isVisible(),
      resumeVisible: !(await page.locator('#resumeModal').evaluate((node) => node.classList.contains('hidden'))),
    };
    report.screenshots[`${mode}AfterHome`] = await screenshot(page, `${mode}-after-home.png`);

    await page.click(selector);
    await page.waitForTimeout(250);
    report.gameModes[mode].resumePrompt = {
      visible: !(await page.locator('#resumeModal').evaluate((node) => node.classList.contains('hidden'))),
      title: await page.locator('#resumeTitle').textContent(),
      summary: await page.locator('#resumeSummary').textContent(),
    };
    report.screenshots[`${mode}Resume`] = await screenshot(page, `${mode}-resume.png`);
  }

  await fs.writeFile(
    path.join(AUDIT_DIR, 'audit-report.json'),
    JSON.stringify(report, null, 2),
    'utf8',
  );

  await browser.close();
  console.log(JSON.stringify(report, null, 2));
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
