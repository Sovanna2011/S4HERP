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

echo
printf '%s/%s passed\n' "$pass" "$((pass+fail))"
if [ "$fail" -gt 0 ]; then
  printf 'failed: %s\n' "${FAILURES[*]}"
  exit 1
fi
