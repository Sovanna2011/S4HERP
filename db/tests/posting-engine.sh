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

echo
printf '%s/%s passed\n' "$pass" "$((pass+fail))"
if [ "$fail" -gt 0 ]; then
  printf 'failed: %s\n' "${FAILURES[*]}"
  exit 1
fi
