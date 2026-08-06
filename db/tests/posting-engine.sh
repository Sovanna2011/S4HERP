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

echo
printf '%s/%s passed\n' "$pass" "$((pass+fail))"
if [ "$fail" -gt 0 ]; then
  printf 'failed: %s\n' "${FAILURES[*]}"
  exit 1
fi
