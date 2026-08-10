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

/// The mirror of textAppears, and not the same as `!textAppears`: after a
/// filter change the old rows are still on screen for as long as the reload
/// takes, so "is it absent right now" is true of a list that is about to become
/// correct and false of one that already is. Only "does it go away" is stable.
async function textDisappears(selector, needle, timeout = 15000) {
  try {
    await page.waitForFunction(
      ([sel, text]) => !(document.querySelector(sel)?.innerText?.includes(text) ?? false),
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

console.log('\n== The inbox is not journal entries only ==');

// Only one change may be open per partner, so a run that died halfway would
// otherwise poison every run after it. Clearing first makes the suite
// re-runnable against a live instance, which is how it is actually used.
const stale = await (await fetch(`${BASE}/api/v1/approvals?objectType=PartnerBank`,
  { headers: { 'X-S4HERP-User': 'seed.approver' } })).json();
for (const item of stale.filter((i) => i.title.includes('1000000004'))) {
  await fetch(`${BASE}/api/v1/business-partners/bank-details/changes/${item.objectId}/reject`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-S4HERP-User': 'seed.approver' },
    body: JSON.stringify({ comment: 'Cleared by the acceptance suite before re-running.' })
  });
}

// A bank change raised over the API, then found and decided entirely through
// the UI. The point of the increment is that an approver does not need to know
// the request id to discover it, so the test does not use one to navigate.
// The account number varies per run so a completed earlier run does not make
// this one a duplicate.
const accountNumber = `777-2-${String(Date.now()).slice(-5)}-4`;
const bankChange = await fetch(`${BASE}/api/v1/business-partners/1000000004/bank-details/changes`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', 'X-S4HERP-User': 'seed.bankclerk' },
  body: JSON.stringify({
    operation: 'Create',
    newCountryCode: 'TH',
    newBankKey: 'BKKBTHBK',
    newBankName: 'Bangkok Bank',
    newAccountNumber: accountNumber,
    newAccountHolder: 'KSS Thailand',
    newIsDefault: true,
    reason: 'Intercompany settlement account opened in Bangkok'
  })
});
const bankChangeBody = await bankChange.json();
check('A bank change can be raised for the inbox to carry', bankChange.status === 202,
  `${bankChange.status} ${JSON.stringify(bankChangeBody).slice(0, 120)}`);

await setUser('seed.approver');
await open('#/approvals');
await page.waitForSelector(ID('approvals', 'approvalsTable'), { timeout: 30000 });
check('The inbox shows the bank change alongside financial documents',
  await textAppears(ID('approvals', 'approvalsTable'), 'KSS Thailand'));
check('...described by what it would do, not by its request id',
  await textAppears(ID('approvals', 'approvalsTable'), accountNumber));
check('...and labelled as a client-level object, having no company code',
  await textAppears(ID('approvals', 'approvalsTable'), 'All company codes'));

// Filtering is the convenience; showing everything is the default. Selected by
// label rather than by position: a SegmentedButton renders its items as <li>,
// and an index would silently follow whatever order the view happens to declare.
const filterButton = (label) => page
  .locator(ID('approvals', 'approvalsFilter') + ' .sapMSegBBtn')
  .filter({ hasText: new RegExp(`^${label}$`) });

await filterButton('Bank details').click();
check('Filtering to bank details keeps the bank change',
  await textAppears(ID('approvals', 'approvalsTable'), 'KSS Thailand'));
await filterButton('Journal entries').click();
check('...and filtering to journal entries drops it',
  await textDisappears(ID('approvals', 'approvalsTable'), 'KSS Thailand'));
await filterButton('All').click();
await page.waitForTimeout(1500);

const bankRow = page.locator(ID('approvals', 'approvalsTable') + ' tbody tr')
  .filter({ hasText: 'KSS Thailand' });
check('The bank change opens from the inbox', (await bankRow.count()) === 1);
await bankRow.first().click();
await page.waitForURL(/#\/approvals\/bank-change\//, { timeout: 30000 });
await page.waitForSelector(ID('bankChange', 'bankChangeFields'), { timeout: 30000 });

const FIELDS_TABLE = ID('bankChange', 'bankChangeFields');
check('...showing every field, not only the ones that moved',
  (await textAppears(FIELDS_TABLE, 'Account number'))
  && (await textAppears(FIELDS_TABLE, 'SWIFT/BIC'))
  && (await textAppears(FIELDS_TABLE, 'Account holder')));
check('...with the proposed account number',
  await textAppears(FIELDS_TABLE, accountNumber));
check('...and an explicit blank where there is no previous value',
  await textAppears(FIELDS_TABLE, '\u2014'));

await page.locator(ID('bankChange', 'bankChangeApproveButton')).click();
await page.waitForSelector('.sapMDialog', { state: 'visible', timeout: 15000 });
await page.locator('.sapMDialog textarea').fill('Account confirmed against the bank letter.');
await page.locator('.sapMDialog .sapMDialogFooter button, .sapMDialog footer button').first().click();
check('Approving from the screen applies the change',
  await textAppears(ID('bankChange', 'bankChangeStatus'), 'Applied'));
check('...and records who decided it',
  await textAppears(ID('bankChange', 'bankChangeSteps'), 'seed.approver'));

await open('#/approvals');
await page.waitForSelector(ID('approvals', 'approvalsTable'), { timeout: 30000 });
check('...so the inbox no longer carries it',
  await textDisappears(ID('approvals', 'approvalsTable'), 'KSS Thailand'));

console.log('\n== The payment run, end to end in the browser ==');

// The run is the transaction that moves the most money, and until this
// increment it was the only approval path with no screen. Everything here is
// done by clicking, including the parts that post to the ledger.
await setUser('seed.accountant');
await open('#/payment-runs');
await page.waitForSelector(ID('paymentRuns', 'paymentRunsTable'), { timeout: 30000 });
check('The payment runs are reachable without knowing an id',
  await textAppears(ID('paymentRuns', 'paymentRunsTable'), '1000-'));

// An invoice to pay, posted over the API — the journal screen is covered above
// and re-covering it here would only make this slower. 2,500 clears the 1,000
// USD release threshold on its own, so the run needs approval regardless of
// what else happens to be open. At 300 it depended on leftovers from earlier
// blocks, and the second consecutive run took the other branch.
const invoice = await fetch(`${BASE}/api/v1/finance/journal-entries`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', 'X-S4HERP-User': 'seed.accountant' },
  body: JSON.stringify({
    companyCode: '1000', documentType: 'KR',
    documentDate: '2026-05-02', postingDate: '2026-05-02',
    currency: 'USD', reference: 'AP-UI',
    lines: [
      { postingKey: '40', amount: 2500.00, glAccount: '6000000000', costCenter: 'CC101000' },
      { postingKey: '31', amount: 2500.00, businessPartner: '1000000002', paymentTerms: 'N014' }
    ]
  })
});
check('An invoice exists for the run to find', invoice.status === 201, String(invoice.status));

await page.locator(ID('paymentRuns', 'paymentRunsCreateButton')).click();
await page.waitForSelector('.sapMDialog', { state: 'visible', timeout: 15000 });
const dialog = page.locator('.sapMDialog');
await dialog.locator('input').nth(1).fill('2026-05-10');   // run date
await dialog.locator('input').nth(2).fill('2026-05-31');   // items due by
await dialog.locator('input').nth(6).fill('1000000002');   // one partner, to keep it small
await page.locator(ID('paymentRuns', 'createRunConfirm')).click();

await page.waitForURL(/#\/payment-runs\/1000-/, { timeout: 30000 });
const uiRunId = decodeURIComponent(page.url().split('/payment-runs/')[1]);
await page.waitForSelector(ID('paymentRun', 'paymentRunPayments'), { timeout: 30000 });
check('Proposing opens the proposal itself, not a list',
  await textAppears(ID('paymentRun', 'paymentRunPayments'), '1000000002'));
check('...showing the run as proposed, nothing posted',
  await textAppears(ID('paymentRun', 'paymentRunStatus'), 'Proposed'));

// This partner has other open items falling due, so the run clears the release
// threshold and the screen must offer Submit rather than Execute. Which button
// appears is read from the run, not assumed: the amount depends on what else is
// open, and a test that hard-codes it breaks whenever an earlier block changes.
const submitVisible = await page.locator(ID('paymentRun', 'paymentRunSubmitButton')).isVisible();
const executeVisible = await page.locator(ID('paymentRun', 'paymentRunExecuteButton')).isVisible();
check('A run over the release threshold offers Submit, not Execute',
  submitVisible && !executeVisible, `submit=${submitVisible} execute=${executeVisible}`);

await page.locator(ID('paymentRun', 'paymentRunSubmitButton')).click();
check('Submitting puts it in front of an approver',
  await textAppears(ID('paymentRun', 'paymentRunStatus'), 'PendingApproval'));
check('...and the approval trail appears on the same page',
  await textAppears(ID('paymentRun', 'paymentRunSteps'), 'FI_APPROVER'));

// The approve button is on screen for the accountant who raised the run, and
// the server refuses. The refusal here is authority rather than maker-checker —
// an accountant holds no W_APPROVE at all, and the run's amount is checked
// against the approver's limit before the workflow is consulted. Maker-checker
// on a payment run is proven in the API suite, by the one seeded role that
// could otherwise self-release.
await page.locator(ID('paymentRun', 'paymentRunApproveButton')).click();
await page.waitForSelector('.sapMDialog', { state: 'visible', timeout: 15000 });
await page.locator('.sapMDialog textarea').fill('Releasing my own run.');
await page.locator('.sapMDialog .sapMDialogFooter button, .sapMDialog footer button').first().click();
await page.waitForSelector('.sapMMessageBox', { state: 'visible', timeout: 30000 });
check('The person who raised the run is refused, on screen, with a reason',
  /authoris|authoriz|not permitted|W_APPROVE/i.test(
    await page.locator('.sapMMessageBox').innerText()));
await page.locator('.sapMMessageBox button').first().click();

await setUser('seed.approver');
await page.reload({ waitUntil: 'networkidle' });
await page.waitForSelector(ID('paymentRun', 'paymentRunApproveButton'), { timeout: 30000 });
await page.locator(ID('paymentRun', 'paymentRunApproveButton')).click();
await page.waitForSelector('.sapMDialog', { state: 'visible', timeout: 15000 });
await page.locator('.sapMDialog textarea').fill('Checked the payees and the exclusions.');
await page.locator('.sapMDialog .sapMDialogFooter button, .sapMDialog footer button').first().click();
check('An approver releases it',
  await textAppears(ID('paymentRun', 'paymentRunStatus'), 'Approved'));
check('...which is release, not payment',
  !(await textAppears(ID('paymentRun', 'paymentRunStatus'), 'Executed', 3000)));

await setUser('seed.accountant');
await page.reload({ waitUntil: 'networkidle' });
await page.waitForSelector(ID('paymentRun', 'paymentRunExecuteButton'), { timeout: 30000 });
await page.locator(ID('paymentRun', 'paymentRunExecuteButton')).click();
await page.waitForSelector('.sapMMessageBox', { state: 'visible', timeout: 15000 });
const confirmText = await page.locator('.sapMMessageBox').innerText();
check('...and executing says plainly that there is no undo',
  /no undo/i.test(confirmText), confirmText.slice(0, 140));
await page.locator('.sapMMessageBox button').first().click();

check('Executing posts the payments',
  await textAppears(ID('paymentRun', 'paymentRunStatus'), 'Executed'));

// The bank file, from the same screen, by the two people entitled to each half.
check('The executed run offers to generate its bank file',
  await becomesVisible(ID('paymentRun', 'paymentRunGenerateFileButton')));
await page.locator(ID('paymentRun', 'paymentRunGenerateFileButton')).click();
check('...and generating it shows the file, hash and all',
  await textAppears(ID('paymentRun', 'paymentRunFileMessageId'), '1000'));
check('...with no copies taken yet',
  await textAppears(ID('paymentRun', 'paymentRunFileCopies'), '0'));

// The accountant who generated it may not take it away — S_EXPORT, held only by
// treasury. The button is on screen for them; the server is what refuses.
await page.locator(ID('paymentRun', 'paymentRunDownloadFileButton')).click();
await page.waitForSelector('.sapMMessageBox', { state: 'visible', timeout: 15000 });
check('The accountant is refused the file itself, on screen',
  /not authorised|NOT_AUTHORIZED|authoris/i.test(
    await page.locator('.sapMMessageBox').innerText()));
await page.locator('.sapMMessageBox button').first().click();

await setUser('seed.treasury');
await page.reload({ waitUntil: 'networkidle' });
await page.waitForSelector(ID('paymentRun', 'paymentRunDownloadFileButton'), { timeout: 30000 });
await page.locator(ID('paymentRun', 'paymentRunDownloadFileButton')).click();
check('Treasury may, and the copy is counted',
  await textAppears(ID('paymentRun', 'paymentRunFileCopies'), '1'));

console.log('\n== The bank answers back ==');

// The run above was executed and its file generated. A pain.002 refusing that
// payment is what closes the loop: until this increment the ledger said paid and
// nothing could contradict it.
const fileMeta = await (await fetch(
  `${BASE}/api/v1/finance/payment-runs/${uiRunId}/payment-file`,
  { headers: { 'X-S4HERP-User': 'seed.accountant' } })).json();

const uiPaymentDoc = await (await fetch(
  `${BASE}/api/v1/finance/payment-runs/${uiRunId}`,
  { headers: { 'X-S4HERP-User': 'seed.accountant' } })).json();
const uiE2E = `1000-2026-${uiPaymentDoc.payments[0].paymentDocumentNumber}`;

const rejection = await fetch(`${BASE}/api/v1/finance/payment-status-reports`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/xml', 'X-S4HERP-User': 'seed.accountant' },
  body: `<?xml version="1.0" encoding="UTF-8"?>
<Document xmlns="urn:iso:std:iso:20022:tech:xsd:pain.002.001.10">
  <CstmrPmtStsRpt>
    <GrpHdr><MsgId>UI-STS-${Date.now()}</MsgId><CreDtTm>2026-05-12T08:00:00Z</CreDtTm></GrpHdr>
    <OrgnlGrpInfAndSts><OrgnlMsgId>${fileMeta.messageId}</OrgnlMsgId></OrgnlGrpInfAndSts>
    <OrgnlPmtInfAndSts>
      <TxInfAndSts>
        <OrgnlEndToEndId>${uiE2E}</OrgnlEndToEndId>
        <TxSts>RJCT</TxSts>
        <StsRsnInf><Rsn><Cd>AC04</Cd></Rsn><AddtlInf>Creditor account closed</AddtlInf></StsRsnInf>
      </TxInfAndSts>
    </OrgnlPmtInfAndSts>
  </CstmrPmtStsRpt>
</Document>`
});
check('A pain.002 refusing the payment imports', rejection.status === 201,
  String(rejection.status));

await setUser('seed.accountant');
await open(`#/payment-runs/${uiRunId}`);
await page.waitForSelector(ID('paymentRun', 'paymentRunBankStatus'), { timeout: 30000 });
check('The run shows what the bank said',
  await textAppears(ID('paymentRun', 'paymentRunBankStatus'), 'Rejected'));
check('...in the bank\'s own words',
  await textAppears(ID('paymentRun', 'paymentRunBankStatus'), 'Creditor account closed'));
check('...and warns that the ledger still disagrees',
  await becomesVisible(ID('paymentRun', 'paymentRunRejectionStrip')));

// Reversing is the second irreversible action on this screen, and the reason is
// mandatory — the dialog will not send an empty one.
await page.locator(ID('paymentRun', 'paymentRunBankStatus') + ' button').first().click();
await page.waitForSelector('.sapMDialog', { state: 'visible', timeout: 15000 });
await page.locator('.sapMDialog textarea').fill('Bank confirmed the account is closed.');
await page.locator('.sapMDialog .sapMDialogFooter button, .sapMDialog footer button').first().click();

check('Reversing it from the screen clears the warning',
  await textDisappears(ID('paymentRun', 'paymentRunBankStatusPanel'), 'Reverse the payment'));

const openItems = await (await fetch(
  `${BASE}/api/v1/finance/open-items?companyCode=1000&businessPartner=1000000002`,
  { headers: { 'X-S4HERP-User': 'seed.accountant' } })).json();
// The open-item view carries amounts, not the invoice reference, so the proof
// is the 2,500 coming back as open rather than the reference string. Matched on
// the row rather than a formatted literal: JSON trims trailing zeros, and
// "2500.0000" is not what decimal(19,4) serialises to.
check('...and the invoice is open again, so the ledger agrees with the bank',
  openItems.rows.some((r) => Number(r.openAmount) === 2500 && r.clearingStatus === 'Open'),
  JSON.stringify(openItems.rows.map((r) => r.openAmount)));

console.log('\n== The bank rejections inbox ==');

// Increment 12 could answer "what has the bank refused"; nobody could ask it
// without already knowing which run to open. This is that page.
await setUser('seed.accountant');
await open('#/bank-rejections');
await page.waitForSelector(ID('bankRejections', 'bankRejectionsTable'), { timeout: 30000 });

// The API suite leaves one rejection the bank named for a payment this system
// cannot place, and it is permanently outstanding: reversing a guess is refused,
// so there is no action to offer. The page has to say that rather than show a
// button that always fails.
check('A rejection with no matching payment is listed',
  await textAppears(ID('bankRejections', 'bankRejectionsTable'), '1000-2026-999999999'));
check('...as needing investigation, not as a reversal waiting to happen',
  await textAppears(ID('bankRejections', 'bankRejectionsTable'), 'Needs investigation'));

const secondRejection = await fetch(`${BASE}/api/v1/finance/payment-status-reports`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/xml', 'X-S4HERP-User': 'seed.accountant' },
  body: `<?xml version="1.0" encoding="UTF-8"?>
<Document xmlns="urn:iso:std:iso:20022:tech:xsd:pain.002.001.10">
  <CstmrPmtStsRpt>
    <GrpHdr><MsgId>UI-STS-B-${Date.now()}</MsgId><CreDtTm>2026-05-13T08:00:00Z</CreDtTm></GrpHdr>
    <OrgnlGrpInfAndSts><OrgnlMsgId>${fileMeta.messageId}</OrgnlMsgId></OrgnlGrpInfAndSts>
    <OrgnlPmtInfAndSts>
      <TxInfAndSts>
        <OrgnlEndToEndId>${uiE2E}</OrgnlEndToEndId>
        <TxSts>RJCT</TxSts>
        <StsRsnInf><Rsn><Cd>AM04</Cd></Rsn><AddtlInf>Insufficient funds</AddtlInf></StsRsnInf>
      </TxInfAndSts>
    </OrgnlPmtInfAndSts>
  </CstmrPmtStsRpt>
</Document>`
});
check('A second refusal of the same payment imports', secondRejection.status === 201,
  String(secondRejection.status));

await open('#/bank-rejections');
await page.waitForSelector(ID('bankRejections', 'bankRejectionsTable'), { timeout: 30000 });
check('...and now the page says there is work',
  await textAppears(ID('bankRejections', 'bankRejectionsSummary'), 'Outstanding'));
check('...naming who was not paid',
  await textAppears(ID('bankRejections', 'bankRejectionsTable'), 'Mekong Logistics Ltd'));
check('...with the bank\'s reason',
  await textAppears(ID('bankRejections', 'bankRejectionsTable'), 'Insufficient funds'));

// Resolved ones are hidden by default: the page is a work queue, not a log.
check('Reversed ones are out of the way until asked for',
  !(await textAppears(ID('bankRejections', 'bankRejectionsTable'), 'Creditor account closed', 3000)));
await page.locator(ID('bankRejections', 'bankRejectionsIncludeResolved')).click();
check('...and there when they are',
  await textAppears(ID('bankRejections', 'bankRejectionsTable'), 'Creditor account closed'));

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
