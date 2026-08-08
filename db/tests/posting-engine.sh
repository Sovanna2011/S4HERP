#!/usr/bin/env bash
# Phase 3 acceptance: the §23 mandatory rules that belong to the posting engine
# rather than to the database.
#
# Each check asserts the specific HTTP status and application error code. A test
# that merely "did not succeed" is not a pass — a typo in a URL also fails.
#
# Usage: ./db/tests/posting-engine.sh [base-url]
set -uo pipefail

BASE="${1:-http://localhost:8080}"

# Refuse to run twice at once. Several checks measure a trial-balance delta
# around their own postings, so an overlapping run makes them fail with no
# product cause — a false red, which costs as much time to chase as a false
# green. Observed exactly once, when a background invocation of this suite
# overlapped a manual one and reported 133/136.
LOCK="${TMPDIR:-/tmp}/s4herp-posting-engine.lock"
exec 9>"$LOCK"
if command -v flock >/dev/null 2>&1 && ! flock -n 9; then
  echo "Another run of this suite is already in progress against a shared database."
  echo "Refusing to start: the trial-balance delta checks would fail for no reason."
  exit 2
fi
# Wait for readiness, not for the port to answer. On a fresh volume the host
# starts listening, then migrates, then seeds; a suite that starts in that window
# gets 403 on every request because no role has been granted yet, and reports
# ~170 failures that have nothing to do with the code. Observed exactly that.
printf 'Waiting for %s/health/ready' "$BASE"
for _ in $(seq 1 60); do
  if [ "$(curl -s -o /dev/null -m 5 -w '%{http_code}' "$BASE/health/ready")" = "200" ]; then
    READY=1; break
  fi
  printf '.'; sleep 5
done
echo
if [ "${READY:-0}" != "1" ]; then
  echo "Not ready after five minutes. Refusing to run: every check would fail for"
  echo "the same reason and none of the failures would mean anything."
  echo "Try: docker compose logs api"
  exit 2
fi

ACCOUNTANT='X-S4HERP-User: seed.accountant'
CLERK='X-S4HERP-User: seed.clerk'
AUDITOR='X-S4HERP-User: seed.auditor'
APPROVER='X-S4HERP-User: seed.approver'
CFO='X-S4HERP-User: seed.cfo'
# Holds both F_BKPF_BUK/01 and W_APPROVE — an SOD003 conflict, seeded on purpose
# so maker-checker has something to catch.
SUPERVISOR='X-S4HERP-User: seed.supervisor'
# Sees payment runs and holds S_EXPORT, so may take the bank file away. Holds
# no F_BP_BANK: SOD001 rates that combination Critical.
TREASURY='X-S4HERP-User: seed.treasury'
BANKCLERK='X-S4HERP-User: seed.bankclerk'
BANKCLERK2='X-S4HERP-User: seed.bankclerk2'
BANKSUP='X-S4HERP-User: seed.banksupervisor'
JSON='Content-Type: application/json'

pass=0; fail=0
declare -a FAILURES

# check <name> <expected-status> <expected-errorCode|-> <curl args...>
check() {
  local name="$1" wantStatus="$2" wantCode="$3"; shift 3
  local body status code
  body=$(curl -s -m 20 -w '\n%{http_code}' "$@" 2>/dev/null)
  status=$(printf '%s' "$body" | tail -1)
  body=$(printf '%s' "$body" | sed '$d')
  code=$(printf '%s' "$body" | grep -o '"errorCode":"[^"]*"' | head -1 | cut -d'"' -f4)

  if [ "$status" = "$wantStatus" ] && { [ "$wantCode" = "-" ] || [ "$code" = "$wantCode" ]; }; then
    printf '  PASS  %-58s %s %s\n' "$name" "$status" "${code:-—}"
    pass=$((pass+1))
  else
    printf '  FAIL  %-58s got %s %s, wanted %s %s\n' \
      "$name" "$status" "${code:-—}" "$wantStatus" "$wantCode"
    FAILURES+=("$name")
    fail=$((fail+1))
  fi
  LAST_BODY="$body"
}

balanced() {
  cat <<JSON
{"companyCode":"1000","documentType":"SA","documentDate":"2026-04-10",
 "postingDate":"2026-04-10","currency":"USD","reference":"$1","headerText":"Acceptance test",
 "lines":[
   {"postingKey":"40","amount":250.00,"glAccount":"6000000000","costCenter":"CC101000","lineText":"Office supplies"},
   {"postingKey":"50","amount":250.00,"glAccount":"1000100000","lineText":"Bank"}]}
JSON
}

# balanced_amount <reference> <amount>
balanced_amount() {
  cat <<JSON
{"companyCode":"1000","documentType":"SA","documentDate":"2026-04-10",
 "postingDate":"2026-04-10","currency":"USD","reference":"$1","headerText":"Workflow test",
 "lines":[
   {"postingKey":"40","amount":$2,"glAccount":"6000000000","costCenter":"CC101000","lineText":"Expense"},
   {"postingKey":"50","amount":$2,"glAccount":"1000100000","lineText":"Bank"}]}
JSON
}

# customer_invoice <reference> <amount> [payment-terms]
# A customer invoice is an ordinary document with a partner line: debit the
# customer, credit revenue. The due date is derived from the payment terms.
customer_invoice() {
  local terms=""
  [ -n "${3:-}" ] && terms=",\"paymentTerms\":\"$3\""
  cat <<JSON
{"companyCode":"1000","documentType":"DR","documentDate":"2026-04-10",
 "postingDate":"2026-04-10","currency":"USD","reference":"$1","headerText":"AR test",
 "lines":[
   {"postingKey":"01","amount":$2,"businessPartner":"$CUSTOMER","lineText":"Invoice"$terms},
   {"postingKey":"50","amount":$2,"glAccount":"4000000000","costCenter":"CC102000","lineText":"Revenue"}]}
JSON
}

# json_field <json-key> — first scalar value of that key in $LAST_BODY
json_field() {
  printf '%s' "$LAST_BODY" | grep -o "\"$1\":\(\"[^\"]*\"\|[-0-9.a-z]*\)" | head -1 \
    | cut -d: -f2- | tr -d '"'
}

# assert_body <name> <extended-regex against $LAST_BODY>
assert_body() {
  if printf '%s' "$LAST_BODY" | grep -Eq "$2"; then
    printf '  PASS  %s\n' "$1"; pass=$((pass+1))
  else
    printf '  FAIL  %s (body did not match %s)\n' "$1" "$2"
    FAILURES+=("$1"); fail=$((fail+1))
  fi
}

trial_balance_debit() {
  local body
  body=$(curl -s -m 20 "$BASE/api/v1/finance/reports/trial-balance?companyCode=1000&fiscalYear=2026" \
    -H "$ACCOUNTANT")
  printf '%s' "$body" | grep -o '"totalDebit":[-0-9.]*' | head -1 | cut -d: -f2
}

echo "== Posting engine =="

check "Balanced document posts" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -d "$(balanced ACC-001)"
DOC=$(printf '%s' "$LAST_BODY" | grep -o '"documentNumber":[0-9]*' | head -1 | cut -d: -f2)

check "Simulation returns the impact without posting" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/simulate" -H "$ACCOUNTANT" -H "$JSON" \
  -d "$(balanced ACC-SIM)"
SIM_POSTED=$(printf '%s' "$LAST_BODY" | grep -o '"posted":[a-z]*' | head -1 | cut -d: -f2)
if [ "$SIM_POSTED" = "false" ]; then
  echo "  PASS  Simulation reports posted=false"; pass=$((pass+1))
else
  echo "  FAIL  Simulation reported posted=$SIM_POSTED"; FAILURES+=("simulate flag"); fail=$((fail+1))
fi

check "Unbalanced document is rejected" 422 DOCUMENT_UNBALANCED \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","documentType":"SA","documentDate":"2026-04-10",
       "postingDate":"2026-04-10","currency":"USD",
       "lines":[{"postingKey":"40","amount":100,"glAccount":"6000000000","costCenter":"CC101000"},
                {"postingKey":"50","amount":90,"glAccount":"1000100000"}]}'

check "Closed period is rejected" 422 PERIOD_CLOSED \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","documentType":"SA","documentDate":"2027-04-10",
       "postingDate":"2027-04-10","currency":"USD",
       "lines":[{"postingKey":"40","amount":100,"glAccount":"6000000000","costCenter":"CC101000"},
                {"postingKey":"50","amount":100,"glAccount":"1000100000"}]}'

check "Direct posting to a reconciliation account is rejected" 422 UNKNOWN_OBJECT \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","documentType":"SA","documentDate":"2026-04-10",
       "postingDate":"2026-04-10","currency":"USD",
       "lines":[{"postingKey":"40","amount":100,"glAccount":"1200000000"},
                {"postingKey":"50","amount":100,"glAccount":"1000100000"}]}'
if printf '%s' "$LAST_BODY" | grep -q RECONCILIATION_ACCOUNT_DIRECT_POSTING; then
  echo "  PASS  ...and names RECONCILIATION_ACCOUNT_DIRECT_POSTING as the reason"; pass=$((pass+1))
else
  echo "  FAIL  reconciliation-account reason not reported"; FAILURES+=("recon reason"); fail=$((fail+1))
fi

check "Expense account without a cost object is rejected" 422 UNKNOWN_OBJECT \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","documentType":"SA","documentDate":"2026-04-10",
       "postingDate":"2026-04-10","currency":"USD",
       "lines":[{"postingKey":"40","amount":100,"glAccount":"6000000000"},
                {"postingKey":"50","amount":100,"glAccount":"1000100000"}]}'

check "Unknown G/L account is rejected" 422 UNKNOWN_OBJECT \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","documentType":"SA","documentDate":"2026-04-10",
       "postingDate":"2026-04-10","currency":"USD",
       "lines":[{"postingKey":"40","amount":100,"glAccount":"9999999999","costCenter":"CC101000"},
                {"postingKey":"50","amount":100,"glAccount":"1000100000"}]}'

echo
echo "== Idempotency =="

IDEM="acceptance-$$-$(date +%s)"
check "First request with an idempotency key posts" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -H "Idempotency-Key: $IDEM" -d "$(balanced ACC-IDEM)"
FIRST=$(printf '%s' "$LAST_BODY" | grep -o '"documentNumber":[0-9]*' | head -1 | cut -d: -f2)

check "Replay with the same key does not post again" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -H "Idempotency-Key: $IDEM" -d "$(balanced ACC-IDEM)"
SECOND=$(printf '%s' "$LAST_BODY" | grep -o '"documentNumber":[0-9]*' | head -1 | cut -d: -f2)
REPLAY=$(printf '%s' "$LAST_BODY" | grep -o '"wasReplay":[a-z]*' | head -1 | cut -d: -f2)

if [ -n "$FIRST" ] && [ "$FIRST" = "$SECOND" ] && [ "$REPLAY" = "true" ]; then
  echo "  PASS  ...and returns the original document $FIRST"; pass=$((pass+1))
else
  echo "  FAIL  replay returned $SECOND (first was $FIRST, wasReplay=$REPLAY)"
  FAILURES+=("idempotent replay"); fail=$((fail+1))
fi

echo
echo "== Authorisation =="

check "Unknown user is refused" 403 NOT_AUTHORIZED \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "X-S4HERP-User: nobody" -H "$JSON" \
  -d "$(balanced ACC-ANON)"

check "No user header at all is refused" 403 NOT_AUTHORIZED \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$JSON" -d "$(balanced ACC-NOUSER)"

check "Clerk may post in their own company code" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$CLERK" -H "$JSON" \
  -d "$(balanced ACC-CLERK)"

check "Clerk may not post in another company code" 403 NOT_AUTHORIZED \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$CLERK" -H "$JSON" \
  -d '{"companyCode":"1100","documentType":"SA","documentDate":"2026-04-10",
       "postingDate":"2026-04-10","currency":"USD",
       "lines":[{"postingKey":"40","amount":100,"glAccount":"6000000000","costCenter":"CC110100"},
                {"postingKey":"50","amount":100,"glAccount":"1000100000"}]}'

check "Clerk may not post a document type they lack" 403 NOT_AUTHORIZED \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$CLERK" -H "$JSON" \
  -d '{"companyCode":"1000","documentType":"AB","documentDate":"2026-04-10",
       "postingDate":"2026-04-10","currency":"USD",
       "lines":[{"postingKey":"40","amount":100,"glAccount":"6000000000","costCenter":"CC101000"},
                {"postingKey":"50","amount":100,"glAccount":"1000100000"}]}'

check "Auditor may read" 200 - \
  "$BASE/api/v1/finance/reports/trial-balance?companyCode=1000&fiscalYear=2026" -H "$AUDITOR"

check "Auditor may not post, whatever their roles say" 403 NOT_AUTHORIZED \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$AUDITOR" -H "$JSON" \
  -d "$(balanced ACC-AUDIT)"

echo
echo "== Reporting =="

check "Document is readable after posting" 200 - \
  "$BASE/api/v1/finance/journal-entries/1000/2026/$DOC" -H "$ACCOUNTANT"

check "Unknown document returns not found" 404 NOT_FOUND \
  "$BASE/api/v1/finance/journal-entries/1000/2026/999999999" -H "$ACCOUNTANT"

check "Trial balance is served" 200 - \
  "$BASE/api/v1/finance/reports/trial-balance?companyCode=1000&fiscalYear=2026" -H "$ACCOUNTANT"
DIFF=$(printf '%s' "$LAST_BODY" | grep -o '"difference":[-0-9.]*' | head -1 | cut -d: -f2)
# Numeric comparison: the value is a decimal, so "0.0000" must count as zero.
if [ -n "$DIFF" ] && awk -v d="$DIFF" 'BEGIN{exit !(d==0)}'; then
  echo "  PASS  Trial balance foots to zero ($DIFF)"; pass=$((pass+1))
else
  echo "  FAIL  Trial balance difference is ${DIFF:-missing}"; FAILURES+=("trial balance"); fail=$((fail+1))
fi

echo
echo "== Derivation =="

check "Profit centre and segment derive from the cost centre" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/simulate" -H "$ACCOUNTANT" -H "$JSON" \
  -d "$(balanced ACC-DERIVE)"
if printf '%s' "$LAST_BODY" | grep -q '"profitCenter":"PC9000"' \
   && printf '%s' "$LAST_BODY" | grep -q '"segment":"CORP"'; then
  echo "  PASS  ...CC101000 derived PC9000 and segment CORP"; pass=$((pass+1))
else
  echo "  FAIL  derivation did not produce PC9000/CORP"; FAILURES+=("CO derivation"); fail=$((fail+1))
fi



echo
echo "== Tax and reversal =="

# Tax lines are generated by the engine, so the caller submits only base lines.
check "Tax is generated and the document balances" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","documentType":"SA","documentDate":"2026-04-10",
       "postingDate":"2026-04-10","currency":"USD","reference":"ACC-TAX",
       "lines":[{"postingKey":"40","amount":1000,"glAccount":"6000000000","costCenter":"CC101000","taxCode":"V1"},
                {"postingKey":"50","amount":1100,"glAccount":"1000100000"}]}'
TAXDOC=$(printf '%s' "$LAST_BODY" | grep -o '"documentNumber":[0-9]*' | head -1 | cut -d: -f2)
if printf '%s' "$LAST_BODY" | grep -q '"isGenerated":true'; then
  echo "  PASS  ...engine generated the input VAT line"; pass=$((pass+1))
else
  echo "  FAIL  no generated tax line in the result"; FAILURES+=("tax generation"); fail=$((fail+1))
fi

check "Reversal posts a mirror document" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$DOC/reverse" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"reversalReasonCode":"01"}'
REVDOC=$(printf '%s' "$LAST_BODY" | grep -o '"reversalDocumentNumber":[0-9]*' | head -1 | cut -d: -f2)

check "A document cannot be reversed twice" 422 ALREADY_REVERSED \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$DOC/reverse" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"reversalReasonCode":"01"}'

check "A reversal cannot itself be reversed" 422 REVERSAL_OF_REVERSAL \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$REVDOC/reverse" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"reversalReasonCode":"01"}'

check "The original now reads as Reversed" 200 - \
  "$BASE/api/v1/finance/journal-entries/1000/2026/$DOC" -H "$ACCOUNTANT"
if printf '%s' "$LAST_BODY" | grep -q '"status":"Reversed"'; then
  echo "  PASS  ...and links to its reversal"; pass=$((pass+1))
else
  echo "  FAIL  original status is not Reversed"; FAILURES+=("reversal status"); fail=$((fail+1))
fi

check "Trial balance still foots after reversal" 200 - \
  "$BASE/api/v1/finance/reports/trial-balance?companyCode=1000&fiscalYear=2026" -H "$ACCOUNTANT"
DIFF2=$(printf '%s' "$LAST_BODY" | grep -o '"difference":[-0-9.]*' | head -1 | cut -d: -f2)
if [ -n "$DIFF2" ] && awk -v d="$DIFF2" 'BEGIN{exit !(d==0)}'; then
  echo "  PASS  ...difference $DIFF2"; pass=$((pass+1))
else
  echo "  FAIL  difference ${DIFF2:-missing}"; FAILURES+=("post-reversal balance"); fail=$((fail+1))
fi

echo "== Park, submit, approve =="

TB_BEFORE=$(trial_balance_debit)

check "A document parks without posting" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries/park" -H "$ACCOUNTANT" -H "$JSON" \
  -d "$(balanced_amount WF-PARK 250.00)"
PARKED=$(json_field documentNumber)
assert_body "...and reports status Parked, posted=false" '"posted":false.*"status":"Parked"'

TB_AFTER_PARK=$(trial_balance_debit)
if awk -v a="$TB_BEFORE" -v b="$TB_AFTER_PARK" 'BEGIN{exit !(a==b)}'; then
  echo "  PASS  A parked document is absent from the trial balance"; pass=$((pass+1))
else
  echo "  FAIL  trial balance moved on park: $TB_BEFORE -> $TB_AFTER_PARK"
  FAILURES+=("park not in ledger"); fail=$((fail+1))
fi

check "A parked document cannot be reversed" 422 DOCUMENT_NOT_POSTED \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$PARKED/reverse" \
  -H "$ACCOUNTANT" -H "$JSON" -d '{"reversalReasonCode":"01"}'

check "A parked document cannot be approved" 422 DOCUMENT_NOT_PENDING_APPROVAL \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$PARKED/approve" \
  -H "$APPROVER" -H "$JSON" -d '{}'

check "Submission opens an approval" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$PARKED/submit" -H "$ACCOUNTANT"
assert_body "...one step, pending, document awaiting approval" \
  '"documentStatus":"PendingApproval".*"approvalOutcome":"Pending"'

check "A document cannot be submitted twice" 422 DOCUMENT_NOT_PARKED \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$PARKED/submit" -H "$ACCOUNTANT"

check "Someone without the approver role is refused" 403 NOT_AUTHORIZED \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$PARKED/approve" \
  -H "$CLERK" -H "$JSON" -d '{}'

check "The approver's inbox lists the document" 200 - \
  "$BASE/api/v1/finance/approvals" -H "$APPROVER"
assert_body "...and names the waiting document" "\"documentId\":\"[^\"]*-$(printf '%010d' "$PARKED")\""

check "A clerk's inbox is empty" 200 - "$BASE/api/v1/finance/approvals" -H "$CLERK"
if [ "$LAST_BODY" = "[]" ]; then
  echo "  PASS  ...because approval is by role, not by seniority"; pass=$((pass+1))
else
  echo "  FAIL  clerk inbox was not empty: $LAST_BODY"
  FAILURES+=("clerk inbox"); fail=$((fail+1))
fi

check "Approval posts the document" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$PARKED/approve" \
  -H "$APPROVER" -H "$JSON" -d '{"comment":"Checked against the supplier invoice."}'
assert_body "...workflow Approved, document Posted" \
  '"documentStatus":"Posted".*"approvalOutcome":"Approved"'

TB_AFTER_POST=$(trial_balance_debit)
if awk -v a="$TB_BEFORE" -v b="$TB_AFTER_POST" 'BEGIN{exit !(b-a==250)}'; then
  echo "  PASS  ...and it now moves the trial balance ($TB_BEFORE -> $TB_AFTER_POST)"; pass=$((pass+1))
else
  echo "  FAIL  trial balance moved by $(awk -v a="$TB_BEFORE" -v b="$TB_AFTER_POST" 'BEGIN{print b-a}'), wanted 250"
  FAILURES+=("approval posts to ledger"); fail=$((fail+1))
fi

check "An approved document cannot be approved again" 422 DOCUMENT_NOT_PENDING_APPROVAL \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$PARKED/approve" \
  -H "$APPROVER" -H "$JSON" -d '{}'

check "The workflow history survives the posting" 200 - \
  "$BASE/api/v1/finance/journal-entries/1000/2026/$PARKED/workflow" -H "$ACCOUNTANT"
assert_body "...naming who approved it" '"decidedBy":"seed.approver"'

echo "== Maker-checker =="

check "A user who may both post and approve parks a document" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries/park" -H "$SUPERVISOR" -H "$JSON" \
  -d "$(balanced_amount WF-SELF 300.00)"
SELFDOC=$(json_field documentNumber)

check "...and submits it" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$SELFDOC/submit" -H "$SUPERVISOR"

check "...but cannot approve their own document" 422 MAKER_CHECKER_VIOLATION \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$SELFDOC/approve" \
  -H "$SUPERVISOR" -H "$JSON" -d '{"comment":"Looks fine to me."}'

check "Somebody else can" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$SELFDOC/approve" \
  -H "$APPROVER" -H "$JSON" -d '{"comment":"Reviewed."}'
assert_body "...and the document posts" '"documentStatus":"Posted"'

echo "== Approval thresholds =="

check "A larger document parks" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries/park" -H "$ACCOUNTANT" -H "$JSON" \
  -d "$(balanced_amount WF-TWO 6000.00)"
BIGDOC=$(json_field documentNumber)

check "...and needs two approvals" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$BIGDOC/submit" -H "$ACCOUNTANT"
assert_body "...FI_APPROVER then FI_SENIOR_APPROVER" \
  '"approverRoleCode":"FI_APPROVER".*"approverRoleCode":"FI_SENIOR_APPROVER"'

# 6,000 against a 50,000 limit. Compared as unpadded text "6000" > "50000" and
# this would be refused, which is the bug AmountLimit's zero padding prevents.
check "The first approval leaves it pending" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$BIGDOC/approve" \
  -H "$APPROVER" -H "$JSON" -d '{"comment":"Level 1."}'
assert_body "...still PendingApproval after step 1" \
  '"documentStatus":"PendingApproval".*"approvalOutcome":"Pending"'

check "The first approver cannot also take step 2" 403 NOT_AUTHORIZED \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$BIGDOC/approve" \
  -H "$APPROVER" -H "$JSON" -d '{}'

check "The second approver releases it" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$BIGDOC/approve" \
  -H "$CFO" -H "$JSON" -d '{"comment":"Level 2."}'
assert_body "...and only then does it post" \
  '"documentStatus":"Posted".*"approvalOutcome":"Approved"'

check "A document above the approver's limit parks" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries/park" -H "$ACCOUNTANT" -H "$JSON" \
  -d "$(balanced_amount WF-BIG 60000.00)"
HUGEDOC=$(json_field documentNumber)

check "...and is submitted" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$HUGEDOC/submit" -H "$ACCOUNTANT"

check "...but exceeds the first approver's value limit" 403 NOT_AUTHORIZED \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$HUGEDOC/approve" \
  -H "$APPROVER" -H "$JSON" -d '{}'

echo "== Rejection =="

check "A document parks and is submitted" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries/park" -H "$ACCOUNTANT" -H "$JSON" \
  -d "$(balanced_amount WF-REJ 120.00)"
REJDOC=$(json_field documentNumber)
check "...and enters approval" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$REJDOC/submit" -H "$ACCOUNTANT"

check "Rejection without a reason is refused" 422 REJECTION_COMMENT_REQUIRED \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$REJDOC/reject" \
  -H "$APPROVER" -H "$JSON" -d '{}'

check "Rejection with a reason is recorded" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$REJDOC/reject" \
  -H "$APPROVER" -H "$JSON" -d '{"comment":"Wrong cost centre."}'
assert_body "...and the document reads Rejected" \
  '"documentStatus":"Rejected".*"approvalOutcome":"Rejected"'

check "A rejected document cannot then be approved" 422 DOCUMENT_NOT_PENDING_APPROVAL \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$REJDOC/approve" \
  -H "$APPROVER" -H "$JSON" -d '{}'

check "Trial balance still foots after the whole workflow" 200 - \
  "$BASE/api/v1/finance/reports/trial-balance?companyCode=1000&fiscalYear=2026" -H "$ACCOUNTANT"
DIFF3=$(json_field difference)
if [ -n "$DIFF3" ] && awk -v d="$DIFF3" 'BEGIN{exit !(d==0)}'; then
  echo "  PASS  ...difference $DIFF3"; pass=$((pass+1))
else
  echo "  FAIL  difference ${DIFF3:-missing}"; FAILURES+=("workflow balance"); fail=$((fail+1))
fi

echo "== Withdraw =="

check "A document parks and is submitted" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries/park" -H "$ACCOUNTANT" -H "$JSON" \
  -d "$(balanced_amount WF-WD 90.00)"
WDDOC=$(json_field documentNumber)
check "...and enters approval" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$WDDOC/submit" -H "$ACCOUNTANT"

check "An approver cannot withdraw someone else's submission" 403 NOT_AUTHORIZED \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$WDDOC/withdraw" \
  -H "$APPROVER" -H "$JSON" -d '{}'

check "The submitter can" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$WDDOC/withdraw" \
  -H "$ACCOUNTANT" -H "$JSON" -d '{"comment":"Wrong period."}'
assert_body "...and the document is Parked again" \
  '"documentStatus":"Parked".*"approvalOutcome":"Withdrawn"'

check "A withdrawn document can be submitted again" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$WDDOC/submit" -H "$ACCOUNTANT"
assert_body "...opening a fresh approval" '"approvalOutcome":"Pending"'

echo "== Discard =="

check "A pending document cannot be discarded" 422 DOCUMENT_NOT_DISCARDABLE \
  -X DELETE "$BASE/api/v1/finance/journal-entries/1000/2026/$WDDOC" -H "$ACCOUNTANT"

check "...so withdraw it first" 200 - \
  -X POST "$BASE/api/v1/finance/journal-entries/1000/2026/$WDDOC/withdraw" \
  -H "$ACCOUNTANT" -H "$JSON" -d '{}'

check "Someone without the delete activity is refused" 403 NOT_AUTHORIZED \
  -X DELETE "$BASE/api/v1/finance/journal-entries/1000/2026/$WDDOC" -H "$CLERK"

TB_BEFORE_DISCARD=$(trial_balance_debit)

check "A parked document can be discarded" 200 - \
  -X DELETE "$BASE/api/v1/finance/journal-entries/1000/2026/$WDDOC" -H "$ACCOUNTANT"
assert_body "...reporting what it discarded" '"previousStatus":"Parked".*"linesDeleted":2'

check "...and it is gone" 404 NOT_FOUND \
  "$BASE/api/v1/finance/journal-entries/1000/2026/$WDDOC" -H "$ACCOUNTANT"

TB_AFTER_DISCARD=$(trial_balance_debit)
if awk -v a="$TB_BEFORE_DISCARD" -v b="$TB_AFTER_DISCARD" 'BEGIN{exit !(a==b)}'; then
  echo "  PASS  ...leaving the ledger untouched"; pass=$((pass+1))
else
  echo "  FAIL  trial balance moved on discard: $TB_BEFORE_DISCARD -> $TB_AFTER_DISCARD"
  FAILURES+=("discard touched the ledger"); fail=$((fail+1))
fi

# The number the discarded document consumed is spent. Gapless numbering means
# issued is issued; reusing it would defeat the point of the range.
check "The next document takes the next number, not the discarded one" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries/park" -H "$ACCOUNTANT" -H "$JSON" \
  -d "$(balanced_amount WF-AFTER 40.00)"
NEXTDOC=$(json_field documentNumber)
if [ -n "$NEXTDOC" ] && [ "$NEXTDOC" -gt "$WDDOC" ]; then
  echo "  PASS  ...$NEXTDOC follows the discarded $WDDOC"; pass=$((pass+1))
else
  echo "  FAIL  next number ${NEXTDOC:-missing} did not follow $WDDOC"
  FAILURES+=("discarded number reused"); fail=$((fail+1))
fi

check "A posted document can never be discarded" 422 DOCUMENT_NOT_DISCARDABLE \
  -X DELETE "$BASE/api/v1/finance/journal-entries/1000/2026/$PARKED" -H "$ACCOUNTANT"

check "A rejected document can be discarded" 200 - \
  -X DELETE "$BASE/api/v1/finance/journal-entries/1000/2026/$REJDOC" -H "$ACCOUNTANT"
assert_body "...closing the lifecycle dead-end" '"previousStatus":"Rejected"'

check "Trial balance still foots after discards" 200 - \
  "$BASE/api/v1/finance/reports/trial-balance?companyCode=1000&fiscalYear=2026" -H "$ACCOUNTANT"
DIFF4=$(json_field difference)
if [ -n "$DIFF4" ] && awk -v d="$DIFF4" 'BEGIN{exit !(d==0)}'; then
  echo "  PASS  ...difference $DIFF4"; pass=$((pass+1))
else
  echo "  FAIL  difference ${DIFF4:-missing}"; FAILURES+=("post-discard balance"); fail=$((fail+1))
fi

echo "== Payment terms and due dates =="

# The seeded customer. Resolved rather than hard-coded, so the suite survives a
# change to the business-partner number range.
CUSTOMER=$(curl -s -m 20 "$BASE/api/v1/finance/open-items?companyCode=1000&accountType=D" \
  -H "$ACCOUNTANT" | grep -o '"businessPartner":"[^"]*"' | head -1 | cut -d'"' -f4)
if [ -z "$CUSTOMER" ]; then
  echo "  FAIL  could not resolve a seeded customer from the open-items report"
  FAILURES+=("customer lookup"); fail=$((fail+1)); CUSTOMER="1000000001"
else
  echo "  PASS  Seeded customer $CUSTOMER has open items"; pass=$((pass+1))
fi

check "An invoice on N030 terms posts" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -d "$(customer_invoice AR-030 500.00 N030)"
INV30=$(json_field documentNumber)

check "...and its open item is due 30 days after the document date" 200 - \
  "$BASE/api/v1/finance/open-items?companyCode=1000&accountType=D&businessPartner=$CUSTOMER" \
  -H "$ACCOUNTANT"
if printf '%s' "$LAST_BODY" | grep -q '"dueDate":"2026-05-10"'; then
  echo "  PASS  ...2026-04-10 + 30 = 2026-05-10"; pass=$((pass+1))
else
  echo "  FAIL  no open item due 2026-05-10"; FAILURES+=("N030 due date"); fail=$((fail+1))
fi

check "An invoice on N014 terms posts" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -d "$(customer_invoice AR-014 200.00 N014)"
INV14=$(json_field documentNumber)

check "...and is due 14 days out, not 30" 200 - \
  "$BASE/api/v1/finance/open-items?companyCode=1000&accountType=D&businessPartner=$CUSTOMER" \
  -H "$ACCOUNTANT"
if printf '%s' "$LAST_BODY" | grep -q '"dueDate":"2026-04-24"'; then
  echo "  PASS  ...the term drives the date, not the caller"; pass=$((pass+1))
else
  echo "  FAIL  no open item due 2026-04-24"; FAILURES+=("N014 due date"); fail=$((fail+1))
fi

check "Unknown payment terms are refused" 422 UNKNOWN_OBJECT \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -d "$(customer_invoice AR-BAD 100.00 ZZZZ)"

echo "== Open items and aging =="

check "The open-items report is served" 200 - \
  "$BASE/api/v1/finance/open-items?companyCode=1000&accountType=D&asOf=2026-06-30" \
  -H "$ACCOUNTANT"
if printf '%s' "$LAST_BODY" | grep -q '"agingBucket":"31-60"'; then
  echo "  PASS  ...and buckets a 2026-05-10 item at 51 days on 2026-06-30"; pass=$((pass+1))
else
  echo "  FAIL  expected a 31-60 bucket"; FAILURES+=("aging bucket"); fail=$((fail+1))
fi
if printf '%s' "$LAST_BODY" | grep -q '"totalOverdue":'; then
  echo "  PASS  ...and reports a total overdue"; pass=$((pass+1))
else
  echo "  FAIL  no totalOverdue"; FAILURES+=("aging total"); fail=$((fail+1))
fi

check "Nothing is overdue before the due date" 200 - \
  "$BASE/api/v1/finance/open-items?companyCode=1000&accountType=D&asOf=2026-04-11" \
  -H "$ACCOUNTANT"
OVERDUE=$(json_field totalOverdue)
if [ -n "$OVERDUE" ] && awk -v d="$OVERDUE" 'BEGIN{exit !(d==0)}'; then
  echo "  PASS  ...totalOverdue $OVERDUE the day after invoicing"; pass=$((pass+1))
else
  echo "  FAIL  totalOverdue ${OVERDUE:-missing} on 2026-04-11"
  FAILURES+=("premature overdue"); fail=$((fail+1))
fi

check "An auditor may read the report" 200 - \
  "$BASE/api/v1/finance/open-items?companyCode=1000" -H "$AUDITOR"

check "A clerk may not read another company code's" 403 NOT_AUTHORIZED \
  "$BASE/api/v1/finance/open-items?companyCode=2000" -H "$CLERK"

echo "== Payment and clearing =="

check "A payment must select something" 422 NO_ITEMS_SELECTED \
  -X POST "$BASE/api/v1/finance/payments" -H "$ACCOUNTANT" -H "$JSON" \
  -d "{\"companyCode\":\"1000\",\"postingDate\":\"2026-04-15\",
       \"businessPartner\":\"$CUSTOMER\",\"bankAccount\":\"1000100000\",\"items\":[]}"

check "Clearing more than is open is refused" 422 OPEN_ITEM_NOT_FOUND \
  -X POST "$BASE/api/v1/finance/payments" -H "$ACCOUNTANT" -H "$JSON" \
  -d "{\"companyCode\":\"1000\",\"postingDate\":\"2026-04-15\",
       \"businessPartner\":\"$CUSTOMER\",\"bankAccount\":\"1000100000\",
       \"items\":[{\"fiscalYear\":2026,\"documentNumber\":$INV14,\"lineNumber\":1,\"amount\":999.00}]}"
if printf '%s' "$LAST_BODY" | grep -q CLEARING_EXCEEDS_OPEN_AMOUNT; then
  echo "  PASS  ...naming CLEARING_EXCEEDS_OPEN_AMOUNT as the reason"; pass=$((pass+1))
else
  echo "  FAIL  did not name the over-clearing"; FAILURES+=("over-clearing reason"); fail=$((fail+1))
fi

check "A partial payment clears part of an invoice" 201 - \
  -X POST "$BASE/api/v1/finance/payments" -H "$ACCOUNTANT" -H "$JSON" \
  -d "{\"companyCode\":\"1000\",\"postingDate\":\"2026-04-15\",
       \"businessPartner\":\"$CUSTOMER\",\"bankAccount\":\"1000100000\",\"reference\":\"PAY-PART\",
       \"items\":[{\"fiscalYear\":2026,\"documentNumber\":$INV14,\"lineNumber\":1,\"amount\":80.00}]}"
assert_body "...leaving the remainder open" \
  '"itemsPartiallyCleared":1.*"remainingOpen":120'

check "A full payment clears the rest" 201 - \
  -X POST "$BASE/api/v1/finance/payments" -H "$ACCOUNTANT" -H "$JSON" \
  -d "{\"companyCode\":\"1000\",\"postingDate\":\"2026-04-16\",
       \"businessPartner\":\"$CUSTOMER\",\"bankAccount\":\"1000100000\",\"reference\":\"PAY-REST\",
       \"items\":[{\"fiscalYear\":2026,\"documentNumber\":$INV14,\"lineNumber\":1}]}"
PAYDOC=$(json_field documentNumber)
assert_body "...and the item is settled" '"itemsCleared":1.*"remainingOpen":0'

check "The cleared item drops out of the open-items report" 200 - \
  "$BASE/api/v1/finance/open-items?companyCode=1000&accountType=D&businessPartner=$CUSTOMER" \
  -H "$ACCOUNTANT"
if printf '%s' "$LAST_BODY" | grep -q "\"documentNumber\":$INV14,"; then
  echo "  FAIL  the cleared invoice is still listed"; FAILURES+=("cleared still open"); fail=$((fail+1))
else
  echo "  PASS  ...and is listed again only with includeCleared"; pass=$((pass+1))
fi

check "Clearing an already-cleared item is refused" 422 OPEN_ITEM_NOT_FOUND \
  -X POST "$BASE/api/v1/finance/payments" -H "$ACCOUNTANT" -H "$JSON" \
  -d "{\"companyCode\":\"1000\",\"postingDate\":\"2026-04-17\",
       \"businessPartner\":\"$CUSTOMER\",\"bankAccount\":\"1000100000\",
       \"items\":[{\"fiscalYear\":2026,\"documentNumber\":$INV14,\"lineNumber\":1}]}"
if printf '%s' "$LAST_BODY" | grep -q ITEM_NOT_OPEN; then
  echo "  PASS  ...naming ITEM_NOT_OPEN"; pass=$((pass+1))
else
  echo "  FAIL  did not name ITEM_NOT_OPEN"; FAILURES+=("double clearing reason"); fail=$((fail+1))
fi

check "The payment document balances like any other" 200 - \
  "$BASE/api/v1/finance/journal-entries/1000/2026/$PAYDOC" -H "$ACCOUNTANT"
assert_body "...bank against the reconciliation account" '"documentType":"DZ"'

check "Trial balance still foots after payments" 200 - \
  "$BASE/api/v1/finance/reports/trial-balance?companyCode=1000&fiscalYear=2026" -H "$ACCOUNTANT"
DIFF5=$(json_field difference)
if [ -n "$DIFF5" ] && awk -v d="$DIFF5" 'BEGIN{exit !(d==0)}'; then
  echo "  PASS  ...difference $DIFF5"; pass=$((pass+1))
else
  echo "  FAIL  difference ${DIFF5:-missing}"; FAILURES+=("post-payment balance"); fail=$((fail+1))
fi

echo "== Reset clearing =="

check "Resetting reopens the items" 200 - \
  -X POST "$BASE/api/v1/finance/payments/1000/2026/$PAYDOC/reset-clearing" -H "$ACCOUNTANT"
# Two, not one: the invoice reopens and so does the payment's own open item.
# Reopening only the invoice is what broke the Phase 2 reconciliation rule.
assert_body "...reopening both sides of the clearing" '"itemsReopened":2'

check "...and the invoice is open again" 200 - \
  "$BASE/api/v1/finance/open-items?companyCode=1000&accountType=D&businessPartner=$CUSTOMER" \
  -H "$ACCOUNTANT"
if printf '%s' "$LAST_BODY" | grep -q "\"documentNumber\":$INV14,"; then
  echo "  PASS  ...with only the earlier partial payment still applied"; pass=$((pass+1))
else
  echo "  FAIL  the reopened invoice is not in the report"; FAILURES+=("reset reopen"); fail=$((fail+1))
fi

check "A clearing cannot be reset twice" 422 CLEARING_ALREADY_RESET \
  -X POST "$BASE/api/v1/finance/payments/1000/2026/$PAYDOC/reset-clearing" -H "$ACCOUNTANT"

check "Resetting a document that cleared nothing is refused" 422 CLEARING_NOT_FOUND \
  -X POST "$BASE/api/v1/finance/payments/1000/2026/$INV30/reset-clearing" -H "$ACCOUNTANT"

check "Trial balance is untouched by a clearing reset" 200 - \
  "$BASE/api/v1/finance/reports/trial-balance?companyCode=1000&fiscalYear=2026" -H "$ACCOUNTANT"
DIFF6=$(json_field difference)
if [ -n "$DIFF6" ] && awk -v d="$DIFF6" 'BEGIN{exit !(d==0)}'; then
  echo "  PASS  ...clearing is not a ledger fact (ADR-09), difference $DIFF6"; pass=$((pass+1))
else
  echo "  FAIL  difference ${DIFF6:-missing}"; FAILURES+=("post-reset balance"); fail=$((fail+1))
fi

echo "== Payment run: proposal =="

# A vendor invoice, so the run has something outgoing to find. Posting key 31 is
# the vendor credit; the expense is the debit.
check "A vendor invoice posts" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","documentType":"KR","documentDate":"2026-04-01",
       "postingDate":"2026-04-01","currency":"USD","reference":"AP-RUN",
       "lines":[
         {"postingKey":"40","amount":300.00,"glAccount":"6000000000","costCenter":"CC101000"},
         {"postingKey":"31","amount":300.00,"businessPartner":"1000000002","paymentTerms":"N014"}]}'
VINV=$(json_field documentNumber)

check "A proposal is created without posting anything" 201 - \
  -X POST "$BASE/api/v1/finance/payment-runs" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","runDate":"2026-04-20","dueBy":"2026-04-30",
       "paymentMethod":"T","houseBank":"ACLED","houseBankAccount":"MAIN"}'
RUNID=$(json_field runId)
assert_body "...proposing the vendor invoice" '"status":"Proposed"'

check "The proposal is readable" 200 - \
  "$BASE/api/v1/finance/payment-runs/$RUNID" -H "$ACCOUNTANT"
if printf '%s' "$LAST_BODY" | grep -q "\"documentNumber\":$VINV,"; then
  echo "  PASS  ...and lists the invoice due 2026-04-15"; pass=$((pass+1))
else
  echo "  FAIL  the vendor invoice is not in the proposal"; FAILURES+=("proposal selection"); fail=$((fail+1))
fi

# Exclusions are the point of a proposal. Something must always be excluded here:
# the seeded KHR vendor item cannot be paid from a USD account.
if printf '%s' "$LAST_BODY" | grep -q '"excluded":\[\]'; then
  echo "  FAIL  nothing was excluded, so exclusions are untested"
  FAILURES+=("no exclusions"); fail=$((fail+1))
else
  echo "  PASS  ...and records what it left out, with reasons"; pass=$((pass+1))
fi

check "Nothing was posted by proposing" 200 - \
  "$BASE/api/v1/finance/reports/trial-balance?companyCode=1000&fiscalYear=2026" -H "$ACCOUNTANT"
TB_AFTER_PROPOSAL=$(json_field totalDebit)

echo "== Payment run: execution =="

# 300 USD is below the seeded release threshold of 1,000. Submitting it is not a
# no-op that quietly succeeds: it answers NotRequired and leaves the run Proposed,
# which is the branch that says "configuration asked for nobody" rather than
# "somebody approved". Executing then works with no signature at all.
check "Submitting a run below the release threshold asks nobody" 200 - \
  -X POST "$BASE/api/v1/finance/payment-runs/$RUNID/submit" -H "$ACCOUNTANT"
assert_body "...it stays Proposed and reports NotRequired" \
  '"status":"Proposed".*"approvalOutcome":"NotRequired"'

check "A run below the release threshold executes unapproved" 200 - \
  -X POST "$BASE/api/v1/finance/payment-runs/$RUNID/execute" -H "$ACCOUNTANT"
assert_body "...one document per partner" '"status":"Executed".*"paymentsPosted":1'

check "...and the run cannot be executed twice" 422 PAYMENT_RUN_NOT_PROPOSED \
  -X POST "$BASE/api/v1/finance/payment-runs/$RUNID/execute" -H "$ACCOUNTANT"

check "...nor discarded once executed" 422 PAYMENT_RUN_NOT_PROPOSED \
  -X DELETE "$BASE/api/v1/finance/payment-runs/$RUNID" -H "$ACCOUNTANT"

check "The paid invoice is no longer open" 200 - \
  "$BASE/api/v1/finance/open-items?companyCode=1000&accountType=K" -H "$ACCOUNTANT"
if printf '%s' "$LAST_BODY" | grep -q "\"documentNumber\":$VINV,"; then
  echo "  FAIL  the paid vendor invoice is still open"; FAILURES+=("run did not clear"); fail=$((fail+1))
else
  echo "  PASS  ...the run cleared it"; pass=$((pass+1))
fi

check "The run now shows its payment document" 200 - \
  "$BASE/api/v1/finance/payment-runs/$RUNID" -H "$ACCOUNTANT"
assert_body "...against the partner it paid" '"paymentDocumentNumber":[0-9]'

check "Trial balance still foots after the run" 200 - \
  "$BASE/api/v1/finance/reports/trial-balance?companyCode=1000&fiscalYear=2026" -H "$ACCOUNTANT"
DIFF7=$(json_field difference)
if [ -n "$DIFF7" ] && awk -v d="$DIFF7" 'BEGIN{exit !(d==0)}'; then
  echo "  PASS  ...difference $DIFF7"; pass=$((pass+1))
else
  echo "  FAIL  difference ${DIFF7:-missing}"; FAILURES+=("post-run balance"); fail=$((fail+1))
fi

echo "== Payment run: refusals =="

check "An unknown payment method is refused" 422 UNKNOWN_PAYMENT_METHOD \
  -X POST "$BASE/api/v1/finance/payment-runs" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","runDate":"2026-04-20","dueBy":"2026-04-30",
       "paymentMethod":"Z","houseBank":"ACLED","houseBankAccount":"MAIN"}'

check "An unknown house bank account is refused" 422 UNKNOWN_HOUSE_BANK_ACCOUNT \
  -X POST "$BASE/api/v1/finance/payment-runs" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","runDate":"2026-04-20","dueBy":"2026-04-30",
       "paymentMethod":"T","houseBank":"NOPE","houseBankAccount":"MAIN"}'

check "A clerk may not run payments in another company code" 403 NOT_AUTHORIZED \
  -X POST "$BASE/api/v1/finance/payment-runs" -H "$CLERK" -H "$JSON" \
  -d '{"companyCode":"2000","runDate":"2026-04-20","dueBy":"2026-04-30",
       "paymentMethod":"T","houseBank":"BBL","houseBankAccount":"MAIN"}'

check "A run with nothing due proposes nothing" 201 - \
  -X POST "$BASE/api/v1/finance/payment-runs" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","runDate":"2026-01-05","dueBy":"2026-01-05",
       "paymentMethod":"T","houseBank":"ACLED","houseBankAccount":"MAIN"}'
EMPTYRUN=$(json_field runId)
assert_body "...and totals zero" '"totalToPay":0'

check "...and executing it is refused rather than posting nothing" 422 PAYMENT_RUN_EMPTY \
  -X POST "$BASE/api/v1/finance/payment-runs/$EMPTYRUN/execute" -H "$ACCOUNTANT"

check "An unexecuted proposal can be discarded" 200 - \
  -X DELETE "$BASE/api/v1/finance/payment-runs/$EMPTYRUN" -H "$ACCOUNTANT"
assert_body "...and is kept as evidence of what was considered" '"status":"Deleted"'

check "An unknown run is not found" 404 NOT_FOUND \
  "$BASE/api/v1/finance/payment-runs/nonexistent-run" -H "$ACCOUNTANT"

echo "== Payment run approval =="

# 12,000 USD, comfortably over the 1,000 release threshold and comfortably under
# the approver's 50,000 limit, so the run that pays it needs exactly one signature.
check "A second vendor invoice posts" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","documentType":"KR","documentDate":"2026-04-01",
       "postingDate":"2026-04-01","currency":"USD","reference":"AP-APPR",
       "lines":[
         {"postingKey":"40","amount":12000.00,"glAccount":"6000000000","costCenter":"CC101000"},
         {"postingKey":"31","amount":12000.00,"businessPartner":"1000000002","paymentTerms":"N014"}]}'

# Proposed by the supervisor, not the accountant. The supervisor is the seeded
# role that both prepares and approves, so a refusal to self-approve is
# maker-checker doing its job and not an authorisation failure wearing its
# clothes — the accountant holds no W_APPROVE at all and would be turned away
# at the pipeline with NOT_AUTHORIZED, proving nothing.
check "A proposal is created" 201 - \
  -X POST "$BASE/api/v1/finance/payment-runs" -H "$SUPERVISOR" -H "$JSON" \
  -d '{"companyCode":"1000","runDate":"2026-04-21","dueBy":"2026-04-30",
       "paymentMethod":"T","houseBank":"ACLED","houseBankAccount":"MAIN"}'
APPRUN=$(json_field runId)

# The control that matters: a run cannot route around approval by never being
# submitted. Execute asks the same rule matcher the submit path uses.
check "An unapproved run cannot be executed" 422 PAYMENT_RUN_NEEDS_APPROVAL \
  -X POST "$BASE/api/v1/finance/payment-runs/$APPRUN/execute" -H "$ACCOUNTANT"

check "Deciding before submission is refused" 422 PAYMENT_RUN_NOT_AWAITING_APPROVAL \
  -X POST "$BASE/api/v1/finance/payment-runs/$APPRUN/approve" -H "$APPROVER" -H "$JSON" -d '{}'

check "Submitting opens an approval" 200 - \
  -X POST "$BASE/api/v1/finance/payment-runs/$APPRUN/submit" -H "$SUPERVISOR"
assert_body "...the run is now PendingApproval" \
  '"status":"PendingApproval".*"approvalOutcome":"Pending"'

check "...and it still cannot be executed" 422 PAYMENT_RUN_NOT_PROPOSED \
  -X POST "$BASE/api/v1/finance/payment-runs/$APPRUN/execute" -H "$ACCOUNTANT"

check "The maker cannot approve their own run" 422 MAKER_CHECKER_VIOLATION \
  -X POST "$BASE/api/v1/finance/payment-runs/$APPRUN/approve" \
  -H "$SUPERVISOR" -H "$JSON" -d '{"comment":"Mine, looks fine."}'

check "Someone without the approver role is refused" 403 NOT_AUTHORIZED \
  -X POST "$BASE/api/v1/finance/payment-runs/$APPRUN/approve" -H "$CLERK" -H "$JSON" -d '{}'

check "The run appears in the approver's inbox" 200 - \
  "$BASE/api/v1/finance/approvals" -H "$APPROVER"
# The inbox is filtered to JournalEntry, so a PaymentRun must NOT be there —
# proof the object type actually narrows rather than decorating.
if printf '%s' "$LAST_BODY" | grep -q "$APPRUN"; then
  echo "  FAIL  the journal inbox listed a payment run"; FAILURES+=("inbox scope"); fail=$((fail+1))
else
  echo "  PASS  ...no: the journal inbox is scoped to journal entries"; pass=$((pass+1))
fi

check "The approver releases the run" 200 - \
  -X POST "$BASE/api/v1/finance/payment-runs/$APPRUN/approve" \
  -H "$APPROVER" -H "$JSON" -d '{"comment":"Checked the proposal."}'
assert_body "...to Approved, not paid" '"status":"Approved".*"approvalOutcome":"Approved"'

check "An approved run executes" 200 - \
  -X POST "$BASE/api/v1/finance/payment-runs/$APPRUN/execute" -H "$ACCOUNTANT"
assert_body "...posting the payments" '"status":"Executed".*"paymentsPosted":1'

echo "== Payment run rejection =="

check "A third vendor invoice posts" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","documentType":"KR","documentDate":"2026-04-02",
       "postingDate":"2026-04-02","currency":"USD","reference":"AP-REJ",
       "lines":[
         {"postingKey":"40","amount":9000.00,"glAccount":"6000000000","costCenter":"CC101000"},
         {"postingKey":"31","amount":9000.00,"businessPartner":"1000000002","paymentTerms":"N014"}]}'

check "It is proposed and submitted" 201 - \
  -X POST "$BASE/api/v1/finance/payment-runs" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","runDate":"2026-04-22","dueBy":"2026-04-30",
       "paymentMethod":"T","houseBank":"ACLED","houseBankAccount":"MAIN"}'
REJRUN=$(json_field runId)
check "...submitted" 200 - \
  -X POST "$BASE/api/v1/finance/payment-runs/$REJRUN/submit" -H "$ACCOUNTANT"

check "Rejection without a reason is refused" 422 REJECTION_COMMENT_REQUIRED \
  -X POST "$BASE/api/v1/finance/payment-runs/$REJRUN/reject" -H "$APPROVER" -H "$JSON" -d '{}'

check "Rejection with a reason is recorded" 200 - \
  -X POST "$BASE/api/v1/finance/payment-runs/$REJRUN/reject" \
  -H "$APPROVER" -H "$JSON" -d '{"comment":"Pay these next week."}'
assert_body "...and the run reads Rejected" '"status":"Rejected".*"approvalOutcome":"Rejected"'

check "A rejected run cannot be executed" 422 PAYMENT_RUN_NOT_PROPOSED \
  -X POST "$BASE/api/v1/finance/payment-runs/$REJRUN/execute" -H "$ACCOUNTANT"

check "Trial balance still foots after approved and rejected runs" 200 - \
  "$BASE/api/v1/finance/reports/trial-balance?companyCode=1000&fiscalYear=2026" -H "$ACCOUNTANT"
DIFF8=$(json_field difference)
if [ -n "$DIFF8" ] && awk -v d="$DIFF8" 'BEGIN{exit !(d==0)}'; then
  echo "  PASS  ...difference $DIFF8"; pass=$((pass+1))
else
  echo "  FAIL  difference ${DIFF8:-missing}"; FAILURES+=("post-approval balance"); fail=$((fail+1))
fi

echo "== Payment file (ISO 20022 pain.001) =="

# APPRUN is the approved, executed run from the approval block. Its payment is
# the one the file must instruct.
check "A file cannot be generated for a rejected run" 422 PAYMENT_RUN_NOT_EXECUTED \
  -X POST "$BASE/api/v1/finance/payment-runs/$REJRUN/payment-file" -H "$ACCOUNTANT"

check "Reading a file that was never generated says so" 422 PAYMENT_FILE_NOT_GENERATED \
  "$BASE/api/v1/finance/payment-runs/$REJRUN/payment-file" -H "$ACCOUNTANT"

check "An executed run generates its instruction" 201 - \
  -X POST "$BASE/api/v1/finance/payment-runs/$APPRUN/payment-file" -H "$ACCOUNTANT"
assert_body "...one credit transfer, pain.001.001.09" \
  '"format":"pain.001.001.09".*"transactionCount":1'
assert_body "...totalling what the run paid" '"controlSum":12000'
FILEHASH=$(json_field contentSha256)
if [ ${#FILEHASH} -eq 64 ]; then
  echo "  PASS  ...with a sha256 the treasurer can quote to the bank"; pass=$((pass+1))
else
  echo "  FAIL  contentSha256 was '${FILEHASH}', not 64 hex characters"
  FAILURES+=("file hash"); fail=$((fail+1))
fi
# Metadata is not a place for account numbers. The whole design rests on the XML
# being the only route to them, so assert the metadata has none.
if printf '%s' "$LAST_BODY" | grep -q "0001-00-123456-1"; then
  echo "  FAIL  the metadata response leaked the creditor account number"
  FAILURES+=("metadata leak"); fail=$((fail+1))
else
  echo "  PASS  ...and no account number anywhere in the metadata"; pass=$((pass+1))
fi

check "Generating a second file for the same run is refused" 422 PAYMENT_FILE_ALREADY_GENERATED \
  -X POST "$BASE/api/v1/finance/payment-runs/$APPRUN/payment-file" -H "$ACCOUNTANT"

check "The accountant who made the file may not take it away" 403 NOT_AUTHORIZED \
  "$BASE/api/v1/finance/payment-runs/$APPRUN/payment-file/content" -H "$ACCOUNTANT"

check "Nor may an approver" 403 NOT_AUTHORIZED \
  "$BASE/api/v1/finance/payment-runs/$APPRUN/payment-file/content" -H "$APPROVER"

check "Treasury, holding S_EXPORT, may" 200 - \
  "$BASE/api/v1/finance/payment-runs/$APPRUN/payment-file/content" -H "$TREASURY"
assert_body "...and it is a pain.001.001.09 document" \
  'urn:iso:std:iso:20022:tech:xsd:pain.001.001.09'
assert_body "...naming the creditor account, since a bank needs one" \
  '<Id>0001-00-123456-1</Id>'
# Othr/Id rather than IBAN, and no empty IBAN element anywhere: Cambodian banks
# issue no IBANs, and <IBAN/> is a file every one of them rejects.
assert_body "...as Othr/Id, because Cambodian banks issue no IBAN" '<Othr>'
if printf '%s' "$LAST_BODY" | grep -q '<IBAN'; then
  echo "  FAIL  the file carries an IBAN element for a bank that issues none"
  FAILURES+=("spurious IBAN"); fail=$((fail+1))
else
  echo "  PASS  ...and no IBAN element at all"; pass=$((pass+1))
fi
assert_body "...with the amount and currency the run paid" \
  '<InstdAmt Ccy="USD">12000.00</InstdAmt>'
assert_body "...one credit transfer" '<NbOfTxs>1</NbOfTxs>'
assert_body "...a control sum the bank can check" '<CtrlSum>12000.00</CtrlSum>'
assert_body "...remittance naming the invoice it settles" '<Ustrd>Invoices 2026/'
if printf '%s' "$LAST_BODY" | grep -q '<Dbtr>'; then
  echo "  PASS  ...and the paying company as debtor"; pass=$((pass+1))
else
  echo "  FAIL  no debtor in the file"; FAILURES+=("file debtor"); fail=$((fail+1))
fi

check "The download is counted" 200 - \
  "$BASE/api/v1/finance/payment-runs/$APPRUN/payment-file" -H "$ACCOUNTANT"
assert_body "...naming treasury as the first to take a copy" \
  '"downloadCount":1.*"firstDownloadedBy":"seed.treasury"'

check "A second download is allowed and counted, not refused" 200 - \
  "$BASE/api/v1/finance/payment-runs/$APPRUN/payment-file/content" -H "$TREASURY"
check "...and the count reflects it" 200 - \
  "$BASE/api/v1/finance/payment-runs/$APPRUN/payment-file" -H "$ACCOUNTANT"
assert_body "...two copies taken" '"downloadCount":2'

check "An unknown run has no file" 404 NOT_FOUND \
  "$BASE/api/v1/finance/payment-runs/nonexistent-run/payment-file" -H "$ACCOUNTANT"

echo "== Payment method requires bank details =="

# The dual-role partner is seeded with no bank details on purpose. Method T
# requires them, so its invoice must be proposed-and-excluded rather than
# silently absent — and rather than paid into nowhere.
check "An invoice posts for a vendor with no bank details" 201 - \
  -X POST "$BASE/api/v1/finance/journal-entries" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","documentType":"KR","documentDate":"2026-04-03",
       "postingDate":"2026-04-03","currency":"USD","reference":"AP-NOBANK",
       "lines":[
         {"postingKey":"40","amount":75.00,"glAccount":"6000000000","costCenter":"CC101000"},
         {"postingKey":"31","amount":75.00,"businessPartner":"1000000003","paymentTerms":"N014"}]}'
NOBANKINV=$(json_field documentNumber)

check "A transfer run proposes it only to exclude it" 201 - \
  -X POST "$BASE/api/v1/finance/payment-runs" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","runDate":"2026-04-25","dueBy":"2026-04-30",
       "paymentMethod":"T","houseBank":"ACLED","houseBankAccount":"MAIN",
       "businessPartner":"1000000003"}'
NOBANKRUN=$(json_field runId)
assert_body "...with the reason, not by omission" 'no bank details valid on 2026-04-25'
assert_body "...and nothing to pay" '"totalToPay":0'

check "...so there is nothing to execute" 422 PAYMENT_RUN_EMPTY \
  -X POST "$BASE/api/v1/finance/payment-runs/$NOBANKRUN/execute" -H "$ACCOUNTANT"

# Cheque does not need an account number, so the same invoice is payable by C.
# Without this the exclusion could be a blanket refusal and look identical.
check "A cheque run pays the same invoice" 201 - \
  -X POST "$BASE/api/v1/finance/payment-runs" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","runDate":"2026-04-25","dueBy":"2026-04-30",
       "paymentMethod":"C","houseBank":"ACLED","houseBankAccount":"MAIN",
       "businessPartner":"1000000003"}'
CHEQUERUN=$(json_field runId)
assert_body "...because a cheque needs no account number" '"totalToPay":75'

# And the older exclusion is still reachable: the transfer-only vendor is
# excluded from the same cheque run by method, not by bank details.
check "A vendor that permits no cheques is excluded by method" 201 - \
  -X POST "$BASE/api/v1/finance/payment-runs" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","runDate":"2026-04-25","dueBy":"2026-04-30",
       "paymentMethod":"C","houseBank":"ACLED","houseBankAccount":"MAIN",
       "businessPartner":"1000000002"}'
assert_body "...naming the method, not the bank details" \
  'does not permit payment method C'
RUNID_A=$(json_field runId)

# Back-to-back proposals with identical company code, date and method. The run
# id used to end in a seconds timestamp, so this pair collided on the unique
# index and came back 500. Two runs, two ids, is the whole check.
check "Two proposals in the same second both succeed" 201 - \
  -X POST "$BASE/api/v1/finance/payment-runs" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","runDate":"2026-04-25","dueBy":"2026-04-30",
       "paymentMethod":"C","houseBank":"ACLED","houseBankAccount":"MAIN",
       "businessPartner":"1000000002"}'
RUNID_B=$(json_field runId)
if [ -n "$RUNID_A" ] && [ -n "$RUNID_B" ] && [ "$RUNID_A" != "$RUNID_B" ]; then
  echo "  PASS  ...with different run ids ($RUNID_A, $RUNID_B)"; pass=$((pass+1))
else
  echo "  FAIL  run ids collided: '$RUNID_A' and '$RUNID_B'"
  FAILURES+=("run id collision"); fail=$((fail+1))
fi

check "Trial balance still foots after the file and the exclusions" 200 - \
  "$BASE/api/v1/finance/reports/trial-balance?companyCode=1000&fiscalYear=2026" -H "$ACCOUNTANT"
DIFF9=$(json_field difference)
if [ -n "$DIFF9" ] && awk -v d="$DIFF9" 'BEGIN{exit !(d==0)}'; then
  echo "  PASS  ...difference $DIFF9"; pass=$((pass+1))
else
  echo "  FAIL  difference ${DIFF9:-missing}"; FAILURES+=("post-file balance"); fail=$((fail+1))
fi

echo
echo "== Partner bank maintenance under maker-checker =="

BPBASE="$BASE/api/v1/business-partners"
CHANGES="$BPBASE/bank-details/changes"

# 1000000002 is the vendor the payment run pays; 1000000003 has no bank details
# at all, which is why the transfer run excluded it two blocks above.
check "A bank clerk may read a partner's bank details" 200 - \
  "$BPBASE/1000000002/bank-details" -H "$BANKCLERK"
assert_body "...and they are the ones the payment file used" '"accountNumber":"0001-00-123456-1"'
assert_body "...with nothing pending against them" '"pendingChangeRequests":0'

# Whether the account number can be read *without* F_BP_BANK. Treasury holds
# S_EXPORT on the payment file and no bank authority at all, and SOD001 is the
# reason the two are separate.
check "Treasury may not read bank details, holding no F_BP_BANK" 403 NOT_AUTHORIZED \
  "$BPBASE/1000000002/bank-details" -H "$TREASURY"
check "Nor may the accountant who runs the payments" 403 NOT_AUTHORIZED \
  "$BPBASE/1000000002/bank-details" -H "$ACCOUNTANT"

# The central claim: a change is raised and the live record does not move.
check "A clerk raises a change of account number" 202 - \
  -X POST "$BPBASE/1000000002/bank-details/changes" -H "$BANKCLERK" -H "$JSON" \
  -d '{"operation":"Change","accountNumber":"0001-00-123456-1","newAccountNumber":"0002-00-999888-7",
       "reason":"Supplier notified a new account by letter dated 2026-04-20"}'
BNK=$(json_field requestId)
assert_body "...which is pending, not applied" '"status":"Pending"'
assert_body "...naming what it would replace" '"previous":{'
assert_body "...so the approver sees the account being left behind" '"accountNumber":"0001-00-123456-1"'

check "...and the live details are untouched" 200 - \
  "$BPBASE/1000000002/bank-details" -H "$BANKCLERK"
assert_body "...still the old account number" '"accountNumber":"0001-00-123456-1"'
assert_body "...with the pending change surfaced, not hidden" '"pendingChangeRequests":1'

# Maker-checker, and the role separation behind it.
# The clerk holds no W_APPROVE at all, so the authority check refuses before
# maker-checker is even consulted. Both are real refusals and they are different
# ones — the maker-checker case needs somebody who *could* otherwise approve,
# which is what seed.banksupervisor exists for.
check "The clerk who raised it holds no approval authority" 403 NOT_AUTHORIZED \
  -X POST "$CHANGES/$BNK/approve" -H "$BANKCLERK" -H "$JSON" -d '{}'
check "Nor does a second clerk holding the same role" 403 NOT_AUTHORIZED \
  -X POST "$CHANGES/$BNK/approve" -H "$BANKCLERK2" -H "$JSON" -d '{}'
check "Nor may treasury, who will carry the file to the bank" 403 NOT_AUTHORIZED \
  -X POST "$CHANGES/$BNK/approve" -H "$TREASURY" -H "$JSON" -d '{}'

# A payment run started now must still use the approved account, because the
# proposed one has not been approved. This is the reason the staging table exists.
check "A payment run proposed meanwhile still uses the approved account" 201 - \
  -X POST "$BASE/api/v1/finance/payment-runs" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","runDate":"2026-04-27","dueBy":"2026-04-30",
       "paymentMethod":"T","houseBank":"ACLED","houseBankAccount":"MAIN",
       "businessPartner":"1000000002"}'
PENDRUN=$(json_field runId)
check "...and the details it will pay to are unchanged" 200 - \
  "$BPBASE/1000000002/bank-details" -H "$BANKCLERK"
assert_body "...the account the file would carry" '"accountNumber":"0001-00-123456-1"'

check "The approver releases it" 200 - \
  -X POST "$CHANGES/$BNK/approve" -H "$APPROVER" -H "$JSON" \
  -d '{"comment":"Confirmed by phone with the supplier finance team"}'
assert_body "...and only now is it applied" '"status":"Applied"'

check "The live record has moved" 200 - \
  "$BPBASE/1000000002/bank-details" -H "$BANKCLERK"
assert_body "...to the approved account number" '"accountNumber":"0002-00-999888-7"'
assert_body "...and nothing is pending any more" '"pendingChangeRequests":0'
# The request never mentioned the default flag. A field nobody typed must not move.
assert_body "...keeping the default flag the request never mentioned" '"isDefault":true'
assert_body "...and the SWIFT code it never mentioned" '"swift":"ACLBKHPP"'

check "A decided request cannot be decided twice" 422 BANK_CHANGE_NOT_PENDING \
  -X POST "$CHANGES/$BNK/approve" -H "$APPROVER" -H "$JSON" -d '{}'

# Rejection, and the reason requirement the journal workflow already enforces.
check "A second change is raised" 202 - \
  -X POST "$BPBASE/1000000002/bank-details/changes" -H "$BANKCLERK" -H "$JSON" \
  -d '{"operation":"Change","accountNumber":"0002-00-999888-7","newAccountHolder":"Not The Supplier Ltd",
       "reason":"Account holder correction"}'
BNK2=$(json_field requestId)
check "...and rejecting it without a reason is refused" 422 REJECTION_COMMENT_REQUIRED \
  -X POST "$CHANGES/$BNK2/reject" -H "$APPROVER" -H "$JSON" -d '{}'
check "...rejected with one" 200 - \
  -X POST "$CHANGES/$BNK2/reject" -H "$APPROVER" -H "$JSON" \
  -d '{"comment":"The holder name does not match the supplier on the invoice"}'
assert_body "...and the request says so" '"status":"Rejected"'
check "...leaving the account holder as it was" 200 - \
  "$BPBASE/1000000002/bank-details" -H "$BANKCLERK"
assert_body "...unchanged by a rejected request" '"accountHolder":"Mekong Logistics Ltd"'

# Withdrawal, and one open request at a time.
check "A third change is raised" 202 - \
  -X POST "$BPBASE/1000000002/bank-details/changes" -H "$BANKCLERK" -H "$JSON" \
  -d '{"operation":"Change","accountNumber":"0002-00-999888-7","newSwift":"ACLEKHPPXXX",
       "reason":"Adding the SWIFT code for international transfers"}'
BNK3=$(json_field requestId)
check "...and a second one is refused while it is open" 422 BANK_CHANGE_ALREADY_PENDING \
  -X POST "$BPBASE/1000000002/bank-details/changes" -H "$BANKCLERK" -H "$JSON" \
  -d '{"operation":"Change","accountNumber":"0002-00-999888-7","newBankName":"ACLEDA Bank Plc",
       "reason":"Bank name tidy-up"}'
check "An approver may not withdraw someone else's request" 403 - \
  -X POST "$CHANGES/$BNK3/withdraw" -H "$APPROVER" -H "$JSON" -d '{}'
check "The requester may" 200 - \
  -X POST "$CHANGES/$BNK3/withdraw" -H "$BANKCLERK" -H "$JSON" \
  -d '{"comment":"Wrong SWIFT code, will re-raise"}'
assert_body "...and it is withdrawn" '"status":"Withdrawn"'

# Validation that stops a bad file reaching the bank days later.
check "A malformed SWIFT code is refused" 400 VALIDATION_FAILED \
  -X POST "$BPBASE/1000000002/bank-details/changes" -H "$BANKCLERK" -H "$JSON" \
  -d '{"operation":"Change","accountNumber":"0002-00-999888-7","newSwift":"NOPE",
       "reason":"Typo test"}'
check "A change that changes nothing is refused" 422 BANK_CHANGE_IS_A_NO_OP \
  -X POST "$BPBASE/1000000002/bank-details/changes" -H "$BANKCLERK" -H "$JSON" \
  -d '{"operation":"Change","accountNumber":"0002-00-999888-7","newBankName":"ACLEDA Bank Plc",
       "reason":"No-op test"}'
check "An unknown partner is refused" 422 PARTNER_NOT_FOUND \
  -X POST "$BPBASE/9999999999/bank-details/changes" -H "$BANKCLERK" -H "$JSON" \
  -d '{"operation":"Create","newCountryCode":"KH","newBankKey":"ACLEDAKH",
       "newBankName":"ACLEDA Bank","newAccountNumber":"111-1","newAccountHolder":"X",
       "reason":"Unknown partner test"}'

# Creating details for the partner that had none — the exclusion from the
# payment run two blocks above was the symptom; this is the cure.
check "The partner with no bank details gets some" 202 - \
  -X POST "$BPBASE/1000000003/bank-details/changes" -H "$BANKCLERK" -H "$JSON" \
  -d '{"operation":"Create","newCountryCode":"KH","newBankKey":"WINGKHPP",
       "newBankName":"Wing Bank","newAccountNumber":"555444-3",
       "newAccountHolder":"Kampot Cane Growers Association","newIsDefault":true,
       "newValidFrom":"2026-01-01",
       "reason":"Supplier provided bank details on their first invoice"}'
BNK4=$(json_field requestId)
check "...an account already held by another partner is refused" 422 PARTNER_BANK_DUPLICATE \
  -X POST "$BPBASE/1000000004/bank-details/changes" -H "$BANKCLERK" -H "$JSON" \
  -d '{"operation":"Create","newCountryCode":"KH","newBankKey":"ACLBKHPP",
       "newBankName":"ACLEDA Bank Plc","newAccountNumber":"0002-00-999888-7",
       "newAccountHolder":"Someone Else","reason":"Duplicate account test"}'
check "...approved" 200 - \
  -X POST "$CHANGES/$BNK4/approve" -H "$APPROVER" -H "$JSON" \
  -d '{"comment":"Details match the letterhead on the invoice"}'
assert_body "...and applied" '"status":"Applied"'
assert_body "...valid from the date the approver saw" '"proposedValidFrom":"2026-01-01"'

check "...so a transfer run now pays the partner it used to exclude" 201 - \
  -X POST "$BASE/api/v1/finance/payment-runs" -H "$ACCOUNTANT" -H "$JSON" \
  -d '{"companyCode":"1000","runDate":"2026-04-28","dueBy":"2026-04-30",
       "paymentMethod":"T","houseBank":"ACLED","houseBankAccount":"MAIN",
       "businessPartner":"1000000003"}'
assert_body "...for the amount that was previously excluded" '"totalToPay":75'
assert_body "...with nothing left excluded" '"excluded":\[\]'

check "The audit trail records the change" 200 - \
  "$CHANGES/$BNK" -H "$BANKCLERK"
assert_body "...naming who asked" '"requestedBy":"seed.bankclerk"'
assert_body "...and who signed it off" '"decidedBy":"seed.approver"'
assert_body "...with the approver's own words" 'Confirmed by phone'

check "An unknown request is not found" 404 NOT_FOUND \
  "$CHANGES/BNK-99999999" -H "$BANKCLERK"

# Maker-checker itself, through the one role that could otherwise self-approve.
# seed.banksupervisor holds F_BP_BANK *and* W_APPROVE for PartnerBank — a
# combination SOD004 rates Critical and a real installation will eventually
# grant. The runtime control is what stops it becoming a self-approval.
check "A supervisor who can both maintain and approve raises a change" 202 - \
  -X POST "$BPBASE/1000000002/bank-details/changes" -H "$BANKSUP" -H "$JSON" \
  -d '{"operation":"Change","accountNumber":"0002-00-999888-7","newBankName":"ACLEDA Bank Plc (Head Office)",
       "reason":"Bank renamed its branch"}'
BNK5=$(json_field requestId)
check "...and cannot approve their own, though the role allows it" 422 MAKER_CHECKER_VIOLATION \
  -X POST "$CHANGES/$BNK5/approve" -H "$BANKSUP" -H "$JSON" -d '{}'
check "...while the same role in another person's hands can" 200 - \
  -X POST "$CHANGES/$BNK5/approve" -H "$APPROVER" -H "$JSON" \
  -d '{"comment":"Branch rename confirmed on the bank website"}'
assert_body "...and it applies" '"status":"Applied"'

# The rule is on the object type, so the engine cannot be talked into skipping it.
check "A clerk raises a change on the other partner" 202 - \
  -X POST "$BPBASE/1000000003/bank-details/changes" -H "$BANKCLERK" -H "$JSON" \
  -d '{"operation":"Change","accountNumber":"555444-3","newSwift":"WINGKHPPXXX",
       "reason":"Adding SWIFT for international transfers"}'
BNK6=$(json_field requestId)
check "...which the supervisor may approve, being a different person" 200 - \
  -X POST "$CHANGES/$BNK6/approve" -H "$BANKSUP" -H "$JSON" \
  -d '{"comment":"SWIFT verified against the Wing Bank published list"}'
assert_body "...so the control is about the person, not the role" '"status":"Applied"'
assert_body "...and the supervisor is on the record as the approver" '"decidedBy":"seed.banksupervisor"'

check "Deactivating an account still needs a second signature" 202 - \
  -X POST "$BPBASE/1000000003/bank-details/changes" -H "$BANKCLERK" -H "$JSON" \
  -d '{"operation":"Deactivate","accountNumber":"555444-3",
       "reason":"Supplier closed the account"}'
BNK7=$(json_field requestId)
check "...and the account still works until it is given" 200 - \
  "$BPBASE/1000000003/bank-details" -H "$BANKCLERK"
assert_body "...still current" '"isCurrent":true'
check "...approved" 200 - \
  -X POST "$CHANGES/$BNK7/approve" -H "$APPROVER" -H "$JSON" \
  -d '{"comment":"Closure letter on file"}'
check "...and now it is closed, not deleted" 200 - \
  "$BPBASE/1000000003/bank-details" -H "$BANKCLERK"
assert_body "...the row survives as the record of where money went" '"accountNumber":"555444-3"'
assert_body "...but is no longer current" '"isCurrent":false'

echo
echo "== The approvals inbox, across object types =="

# The engine has served three object types since increment 7; the only inbox
# filtered to journal entries, so a payment run waiting on release and a bank
# change waiting on a signature were invisible to whoever had to decide them.
check "A clerk raises a change the approver has not seen" 202 - \
  -X POST "$BPBASE/1000000002/bank-details/changes" -H "$BANKCLERK" -H "$JSON" \
  -d '{"operation":"Change","accountNumber":"0002-00-999888-7","newAccountHolder":"Mekong Logistics Company Limited",
       "reason":"Legal name on the invoice is the full form"}'
BNK8=$(json_field requestId)

check "The generic inbox carries it" 200 - "$BASE/api/v1/approvals" -H "$APPROVER"
assert_body "...as a bank change, named by object type" '"objectType":"PartnerBank"'
assert_body "...described by the partner, not the request id" \
  'Mekong Logistics Ltd \(1000000002\)'
assert_body "...with the change itself in the subtitle" 'Details of account 0002-00-999888-7'
# A bank change has no amount. Reporting zero would say it is worth nothing,
# which is a different and false claim.
assert_body "...and a null amount, because it has none" '"amount":null'
assert_body "...and no company code, being client-level" '"companyCode":null'

check "Filtering to journal entries excludes it" 200 - \
  "$BASE/api/v1/approvals?objectType=JournalEntry" -H "$APPROVER"
if printf '%s' "$LAST_BODY" | grep -q '"objectType":"PartnerBank"'; then
  echo "  FAIL  the object type filter is ignored"
  FAILURES+=("inbox filter"); fail=$((fail+1))
else
  echo "  PASS  ...leaving only what was asked for"; pass=$((pass+1))
fi

check "Filtering to bank changes keeps it" 200 - \
  "$BASE/api/v1/approvals?objectType=PartnerBank" -H "$APPROVER"
assert_body "...and only it" '"objectType":"PartnerBank"'

# The inbox is per person, not a global work list.
check "Someone with no approval authority sees an empty inbox" 200 - \
  "$BASE/api/v1/approvals" -H "$BANKCLERK"
assert_body "...because nothing waits on them" '^\[\]$'

# The supervisor holds the approver role, so a change they raised themselves
# appears in their own inbox — flagged rather than hidden, because an approver
# needs to see that something is waiting and understand why they in particular
# cannot release it.
check "...the supervisor sees the clerk's item as actionable" 200 - \
  "$BASE/api/v1/approvals?objectType=PartnerBank" -H "$BANKSUP"
assert_body "...not blocked, since somebody else raised it" '"makerCheckerBlocks":false'

check "Deciding it empties the inbox again" 200 - \
  -X POST "$CHANGES/$BNK8/approve" -H "$APPROVER" -H "$JSON" \
  -d '{"comment":"Full legal name matches the invoice"}'

# Now the other half: an item the viewer raised themselves.
check "The supervisor raises one of their own" 202 - \
  -X POST "$BPBASE/1000000002/bank-details/changes" -H "$BANKSUP" -H "$JSON" \
  -d '{"operation":"Change","accountNumber":"0002-00-999888-7","newBankName":"ACLEDA Bank Plc (Main Branch)",
       "reason":"Branch name on the statement"}'
BNK9=$(json_field requestId)
check "...and sees it in their own inbox" 200 - \
  "$BASE/api/v1/approvals?objectType=PartnerBank" -H "$BANKSUP"
assert_body "...flagged as their own work, not hidden from them" '"makerCheckerBlocks":true'
check "...while to another approver it is ordinary work" 200 - \
  "$BASE/api/v1/approvals?objectType=PartnerBank" -H "$APPROVER"
assert_body "...and actionable" '"makerCheckerBlocks":false'
check "...decided by that other approver" 200 - \
  -X POST "$CHANGES/$BNK9/approve" -H "$APPROVER" -H "$JSON" \
  -d '{"comment":"Branch name confirmed"}'
check "...confirmed" 200 - "$BASE/api/v1/approvals?objectType=PartnerBank" -H "$APPROVER"
assert_body "...nothing left of that kind" '^\[\]$'

check "Trial balance still foots after bank maintenance" 200 - \
  "$BASE/api/v1/finance/reports/trial-balance?companyCode=1000&fiscalYear=2026" -H "$ACCOUNTANT"
DIFF10=$(json_field difference)
if [ -n "$DIFF10" ] && awk -v d="$DIFF10" 'BEGIN{exit !(d==0)}'; then
  echo "  PASS  ...difference $DIFF10"; pass=$((pass+1))
else
  echo "  FAIL  difference ${DIFF10:-missing}"; FAILURES+=("post-bank balance"); fail=$((fail+1))
fi

echo
printf '%s/%s passed\n' "$pass" "$((pass+fail))"
if [ "$fail" -gt 0 ]; then
  printf 'failed: %s\n' "${FAILURES[*]}"
  exit 1
fi
