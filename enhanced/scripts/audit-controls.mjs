import fs from 'node:fs/promises';
import path from 'node:path';
import { chromium } from 'playwright';

const AUDIT_DIR = path.resolve('audit-artifacts');
const BASE_URL = 'http://127.0.0.1:4173?debug=1';

async function ensureDir(dir) {
  await fs.mkdir(dir, { recursive: true });
}

async function screenshot(page, name) {
  const file = path.join(AUDIT_DIR, name);
  await page.screenshot({ path: file, type: 'png' });
  return file;
}

async function getBindingLabel(page, action) {
  return page.locator(`.controls-binding[data-action="${action}"]`).textContent();
}

async function getSnapshot(page) {
  return page.evaluate(() => window.monstackaDebug?.snapshot());
}

async function openControls(page) {
  await page.click('#openSettingsButtonHome');
  await page.click('#openControlsButton');
}

async function closeSettings(page) {
  await page.click('#closeSettingsButton');
}

async function waitForCountdown(page) {
  await page.waitForTimeout(1300);
}

async function startFreshArcade(page) {
  await page.click('#startArcadeButton');
  await page.waitForTimeout(250);
  const resumeVisible = await page.locator('#resumeModal').evaluate((node) => !node.classList.contains('hidden'));
  if (resumeVisible) {
    await page.click('#startFreshButton');
  }
  await waitForCountdown(page);
}

async function main() {
  await ensureDir(AUDIT_DIR);

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1600, height: 900 } });
  const page = await context.newPage();

  const report = {
    generatedAt: new Date().toISOString(),
    before: {},
    remapped: {},
    gameplay: {},
    reset: {},
    screenshots: {},
  };

  await page.goto(BASE_URL, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(500);

  await openControls(page);
  report.before.left = (await getBindingLabel(page, 'left'))?.trim();
  report.before.hold = (await getBindingLabel(page, 'hold'))?.trim();

  await page.click('.controls-binding[data-action="left"]');
  await page.keyboard.press('KeyJ');
  await page.waitForTimeout(100);

  await page.click('.controls-binding[data-action="hold"]');
  await page.mouse.click(10, 10, { button: 'middle' });
  await page.waitForTimeout(100);

  report.remapped.left = (await getBindingLabel(page, 'left'))?.trim();
  report.remapped.hold = (await getBindingLabel(page, 'hold'))?.trim();
  report.screenshots.controlsRemapped = await screenshot(page, 'controls-remapped.png');
  await closeSettings(page);

  await startFreshArcade(page);
  report.gameplay.initial = await getSnapshot(page);

  await page.keyboard.press('ArrowLeft');
  await page.waitForTimeout(100);
  report.gameplay.afterDefaultArrowLeft = await getSnapshot(page);

  await page.keyboard.press('KeyJ');
  await page.waitForTimeout(100);
  report.gameplay.afterRemappedJ = await getSnapshot(page);

  await page.keyboard.press('KeyC');
  await page.waitForTimeout(100);
  report.gameplay.afterDefaultHoldKey = await getSnapshot(page);

  const boardBox = await page.locator('#gameBoardZone').boundingBox();
  if (!boardBox) {
    throw new Error('Could not locate game board zone for mouse remap audit.');
  }
  await page.mouse.click(
    Math.round(boardBox.x + boardBox.width / 2),
    Math.round(boardBox.y + boardBox.height / 2),
    { button: 'middle' },
  );
  await page.waitForTimeout(100);
  report.gameplay.afterRemappedMouseHold = await getSnapshot(page);
  report.screenshots.gameplayRemapped = await screenshot(page, 'controls-gameplay-remapped.png');

  await page.click('#homeButtonGame');
  await page.waitForTimeout(250);

  await openControls(page);
  await page.click('#controlsDefaultsButton');
  await page.waitForTimeout(100);
  report.reset.left = (await getBindingLabel(page, 'left'))?.trim();
  report.reset.hold = (await getBindingLabel(page, 'hold'))?.trim();
  report.screenshots.controlsReset = await screenshot(page, 'controls-reset.png');
  await closeSettings(page);

  await startFreshArcade(page);
  report.reset.initial = await getSnapshot(page);

  await page.keyboard.press('KeyJ');
  await page.waitForTimeout(100);
  report.reset.afterRemappedJ = await getSnapshot(page);

  await page.keyboard.press('ArrowLeft');
  await page.waitForTimeout(100);
  report.reset.afterDefaultArrowLeft = await getSnapshot(page);

  await page.keyboard.press('KeyC');
  await page.waitForTimeout(100);
  report.reset.afterDefaultHoldKey = await getSnapshot(page);
  report.screenshots.gameplayReset = await screenshot(page, 'controls-gameplay-reset.png');

  await fs.writeFile(
    path.join(AUDIT_DIR, 'controls-audit-report.json'),
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
