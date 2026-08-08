/**
 * Browser acceptance for the OpenUI5 front end.
 *
 * Drives the real UI against the real API — no mocks — because the questions
 * worth answering here are whether the views render, whether posting reaches the
 * server, and whether a server refusal is shown to the user rather than
 * swallowed.
 *
 * Usage: node test/ui-acceptance.mjs [base-url]
 */
import { chromium } from 'playwright';

const BASE = process.argv[2] || 'http://localhost:8080';
const CHROME = process.env.CHROME_PATH || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';

// UI5 prefixes control ids with the component container and view id.
const ID = (view, control) => `#container-s4herp---${view}--${control}`;

let pass = 0;
const failures = [];

function check(name, condition, detail) {
  if (condition) {
    console.log(`  PASS  ${name}`);
    pass++;
  } else {
    console.log(`  FAIL  ${name}${detail ? ` — ${detail}` : ''}`);
    failures.push(name);
  }
}

const browser = await chromium.launch({ executablePath: CHROME });
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });

const consoleErrors = [];
const notFound = [];
page.on('console', (m) => {
  // "Failed to load resource" carries no URL, so 404s are tracked from responses
  // instead — otherwise a missing favicon reads as a JavaScript error.
  if (m.type() === 'error' && !/Failed to load resource/i.test(m.text())) {
    consoleErrors.push(m.text());
  }
});
page.on('pageerror', (e) => consoleErrors.push(String(e)));
page.on('response', (r) => {
  if (r.status() === 404 && !/favicon/i.test(r.url())) notFound.push(r.url());
});

// Navigating to an identical URL does not reload the document, so the component
// keeps the previous user. A cache-busting parameter forces a real load.
let navCount = 0;
async function open(hash) {
  navCount += 1;
  await page.goto(`${BASE}/index.html?run=${navCount}${hash}`, { waitUntil: 'networkidle' });
}

/// The approval panel loads on its own request, so a control appearing is not
/// proof the panel is rendered or its text has arrived. Polling instead of
/// asserting once removes a race that failed roughly one run in three and looked
/// like a bug in the approval history rather than in the test. Both helpers
/// return false on timeout rather than throwing, so a genuine failure still
/// reports as one failed check and the rest of the suite keeps running.
async function becomesVisible(selector, timeout = 15000) {
  try {
    await page.waitForSelector(selector, { state: 'visible', timeout });
    return true;
  } catch {
    return false;
  }
}

async function textAppears(selector, needle, timeout = 15000) {
  try {
    await page.waitForFunction(
      ([sel, text]) => document.querySelector(sel)?.innerText?.includes(text) ?? false,
      [selector, needle],
      { timeout });
    return true;
  } catch {
    return false;
  }
}

async function setUser(userName) {
  await page.evaluate((u) => {
    const raw = window.localStorage.getItem('s4herp.context');
    const ctx = raw ? JSON.parse(raw) : {};
    ctx.userName = u;
    ctx.companyCode = ctx.companyCode || '1000';
    ctx.fiscalYear = ctx.fiscalYear || 2026;
    window.localStorage.setItem('s4herp.context', JSON.stringify(ctx));
  }, userName);
}

console.log('== Shell ==');
await page.goto(`${BASE}/index.html`, { waitUntil: 'networkidle' });
await page.waitForSelector('.sapMGT', { timeout: 60000 });

check('Launchpad renders application tiles',
  (await page.locator('.sapMGT').count()) >= 2);
check('Journal Entry tile is present',
  (await page.getByText('Journal Entry', { exact: false }).count()) > 0);
check('Context selectors are in the shell',
  (await page.locator('.sapMSlt').count()) >= 3);

// UI5 bootstraps a lot of modules; only genuine failures matter.
check('No JavaScript errors during bootstrap',
  consoleErrors.length === 0, consoleErrors.slice(0, 2).join(' | '));
check('No missing resources',
  notFound.length === 0, notFound.slice(0, 2).join(' | '));

console.log('\n== Journal Entry ==');
await setUser('seed.accountant');
await open('#/journal/create');
await page.waitForSelector(ID('journalEntry', 'lineTable'), { timeout: 30000 });

check('Journal entry screen renders the line table',
  await page.locator(ID('journalEntry', 'lineTable')).isVisible());

// Selected by placeholder rather than by position: a MultiSelect table row
// carries hidden inputs before the visible ones, and column order may change.
async function fillLine(row, account, amount, costCenter) {
  const cells = page.locator(ID('journalEntry', 'lineTable') + ' tbody tr').nth(row);
  await cells.locator('input[placeholder="6000000000"]').fill(account);
  if (costCenter) await cells.locator('input[placeholder="CC101000"]').fill(costCenter);
  await cells.locator('input[placeholder="0.00"]').fill(amount);
}

await fillLine(0, '6000000000', '125.00', 'CC101000');
await fillLine(1, '1000100000', '125.00', '');

await page.locator(ID('journalEntry', 'simulateButton')).click();
await page.waitForSelector(ID('journalEntry', 'simulationPanel'), { state: 'visible', timeout: 30000 });

check('Simulation shows the accounting impact',
  await page.locator(ID('journalEntry', 'simulationPanel')).isVisible());
check('Simulation derived the profit centre',
  (await page.locator(ID('journalEntry', 'simulationPanel')).innerText()).includes('PC9000'));

await page.locator(ID('journalEntry', 'postButton')).click();
await page.waitForURL(/#\/journal\/display\//, { timeout: 30000 });
await page.waitForSelector(ID('documentDisplay', 'documentTitle'), { timeout: 30000 });

const documentNumber = await page.locator(ID('documentDisplay', 'documentTitle')).innerText();
check('Posting navigates to the posted document',
  /^KSS-1000-2026-SA-\d{10}$/.test(documentNumber.trim()), documentNumber);
check('Document shows its lines',
  (await page.locator(ID('documentDisplay', 'documentLines') + ' tbody tr').count()) >= 2);

console.log('\n== Server refusals reach the user ==');
await open('#/journal/create');
await page.waitForSelector(ID('journalEntry', 'lineTable'), { timeout: 30000 });
await fillLine(0, '6000000000', '100.00', 'CC101000');
await fillLine(1, '1000100000', '90.00', '');
await page.locator(ID('journalEntry', 'postButton')).click();
await page.waitForSelector(ID('journalEntry', 'messagesButton'), { state: 'visible', timeout: 30000 });

check('An unbalanced document surfaces the server error',
  await page.locator(ID('journalEntry', 'messagesButton')).isVisible());
await page.locator(ID('journalEntry', 'messagesButton')).click();
await page.waitForSelector('.sapMPopover', { state: 'visible', timeout: 15000 });
const popover = await page.locator('.sapMPopover').innerText();
check('...and names the difference',
  /debit|credit|difference/i.test(popover), popover.slice(0, 80));

console.log('\n== Authorisation is the server\'s answer, not the UI\'s ==');
await setUser('seed.auditor');
await open('#/journal/create');
await page.waitForSelector(ID('journalEntry', 'lineTable'), { timeout: 30000 });
await fillLine(0, '6000000000', '50.00', 'CC101000');
await fillLine(1, '1000100000', '50.00', '');
await page.locator(ID('journalEntry', 'postButton')).click();
await page.waitForSelector(ID('journalEntry', 'messagesButton'), { state: 'visible', timeout: 30000 });
await page.locator(ID('journalEntry', 'messagesButton')).click();
await page.waitForSelector('.sapMPopover', { state: 'visible', timeout: 15000 });
const authText = await page.locator('.sapMPopover').innerText();
check('An auditor posting is refused by the server',
  /authorisation|authorization|not hold/i.test(authText), authText.slice(0, 80));

console.log('\n== Trial balance ==');
await setUser('seed.accountant');
await open('#/reports/trial-balance');
await page.waitForSelector(ID('trialBalance', 'balanceTable'), { timeout: 30000 });

check('Trial balance lists accounts',
  (await page.locator(ID('trialBalance', 'balanceTable') + ' tbody tr').count()) > 0);
const difference = await page
  .locator(ID('trialBalance', 'tbDifference') + ' .sapMObjStatusText').innerText();
check('Trial balance foots to zero in the UI',
  Number(difference.replace(/[^0-9.-]/g, '')) === 0, difference);

console.log('\n== Park, submit, approve, through the UI ==');
// seed.supervisor holds both F_BKPF_BUK/01 and W_APPROVE — an SOD003 conflict
// seeded on purpose. Parking as this user is what makes the refusal below
// maker-checker rather than the weaker "you are not an approver at all".
await setUser('seed.supervisor');
await open('#/journal/create');
await page.waitForSelector(ID('journalEntry', 'lineTable'), { timeout: 30000 });
await fillLine(0, '6000000000', '75.00', 'CC101000');
await fillLine(1, '1000100000', '75.00', '');

await page.locator(ID('journalEntry', 'parkButton')).click();
await page.waitForURL(/#\/journal\/display\//, { timeout: 30000 });
await page.waitForSelector(ID('documentDisplay', 'submitButton'), { state: 'visible', timeout: 30000 });

const parkedNumber = (await page.locator(ID('documentDisplay', 'documentTitle')).innerText()).trim();
check('Parking opens the document', /^KSS-1000-2026-SA-\d{10}$/.test(parkedNumber), parkedNumber);
check('A parked document offers Submit, not Reverse',
  await page.locator(ID('documentDisplay', 'submitButton')).isVisible()
  && !(await page.locator(ID('documentDisplay', 'reverseButton')).isVisible()));

await page.locator(ID('documentDisplay', 'submitButton')).click();
await page.waitForSelector(ID('documentDisplay', 'approveButton'), { state: 'visible', timeout: 30000 });
check('Submitting shows the approval history',
  await becomesVisible(ID('documentDisplay', 'workflowPanel')));
check('...with the step still pending',
  await textAppears(ID('documentDisplay', 'workflowSteps'), 'FI_APPROVER'));

// The maker's own approve button is on screen. It is the server that refuses —
// hiding it would be the wrong lesson (§22.1).
await page.locator(ID('documentDisplay', 'approveButton')).click();
await page.waitForSelector('.sapMDialog', { state: 'visible', timeout: 15000 });
await page.locator('.sapMDialog textarea').fill('Approving my own work.');
await page.locator('.sapMDialog .sapMDialogFooter button, .sapMDialog footer button').first().click();
await page.waitForSelector('.sapMMessageBox', { state: 'visible', timeout: 30000 });
const makerCheckerText = await page.locator('.sapMMessageBox').innerText();
check('The maker approving their own document is refused by the server',
  /maker-checker|cannot decide|submitted or created/i.test(makerCheckerText),
  makerCheckerText.slice(0, 120));
await page.locator('.sapMMessageBox button').first().click();

console.log('\n== The approver\'s inbox ==');
await setUser('seed.approver');
await open('#/approvals');
await page.waitForSelector(ID('approvals', 'approvalsTable'), { timeout: 30000 });
const inbox = await page.locator(ID('approvals', 'approvalsTable')).innerText();
check('The inbox lists the submitted document', inbox.includes(parkedNumber), inbox.slice(0, 120));

const inboxRow = page.locator(ID('approvals', 'approvalsTable') + ' tbody tr')
  .filter({ hasText: parkedNumber });
check('...and can be opened from it', (await inboxRow.count()) === 1);
await inboxRow.first().click();
await page.waitForURL(/#\/journal\/display\//, { timeout: 30000 });
await page.waitForSelector(ID('documentDisplay', 'approveButton'), { state: 'visible', timeout: 30000 });
check('Opening from the inbox shows the document itself',
  (await page.locator(ID('documentDisplay', 'documentTitle')).innerText()).trim() === parkedNumber);
check('...with its lines, so the approver sees what they are releasing',
  (await page.locator(ID('documentDisplay', 'documentLines') + ' tbody tr').count()) >= 2);

await page.locator(ID('documentDisplay', 'approveButton')).click();
await page.waitForSelector('.sapMDialog', { state: 'visible', timeout: 15000 });
await page.locator('.sapMDialog textarea').fill('Checked against the invoice.');
await page.locator('.sapMDialog .sapMDialogFooter button, .sapMDialog footer button').first().click();
await page.waitForSelector(ID('documentDisplay', 'reverseButton'), { state: 'visible', timeout: 30000 });
check('Approving posts the document',
  (await page.locator(ID('documentDisplay', 'documentPage')).innerText()).includes('Posted'));
check('...and the approval history records who approved it',
  await textAppears(ID('documentDisplay', 'workflowSteps'), 'seed.approver'));

console.log('\n== Internationalisation ==');
await page.goto(`${BASE}/index.html?sap-language=km&run=km#/reports/trial-balance`, { waitUntil: 'networkidle' });
await page.waitForSelector(ID('trialBalance', 'balanceTable'), { timeout: 30000 });
const khmer = await page.locator('body').innerText();
check('Khmer resource bundle is applied',
  /[ក-៿]/.test(khmer), khmer.slice(0, 60));

await open('');
await page.waitForSelector('.sapMGT', { timeout: 30000 });
// Resolved against this file, not the working directory: the suite is run
// from the repository root as often as from ui5/, and a relative path
// quietly writes the screenshot into whichever directory that happens to be.
await page.screenshot({
  path: new URL('screenshot-launchpad.png', import.meta.url).pathname,
});

await browser.close();

const total = pass + failures.length;
console.log(`\n${pass}/${total} passed`);
if (failures.length) {
  console.log('failed: ' + failures.join(', '));
  process.exit(1);
}
