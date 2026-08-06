#!/usr/bin/env bash
# harness/pipeline/demo/run_full_auto_demo.sh -- SC18/SC19, brief 006 Lot 006c.
#
# Reproducible, < 5 min, exit 0 on success, portable to Linux CI (bash) as
# well as this dev machine (Windows Git Bash). Runs the WHOLE full-auto
# pipeline chain against the REAL repo files (architecture/audit-ledger.jsonl,
# architecture/inbox/CURSOR-FIXTURE-full-auto-demo.md,
# harness/queue/cost-ledger.jsonl) -- not a tmp_path sandbox, because SC19
# requires the real ledger to carry the fixture's chain after the demo runs.
# The one thing it never touches for real is harness/queue/briefs/ -- the
# brief seed audit_convert.py writes goes to a demo-local, gitignored
# output directory (harness/pipeline/demo/output/), never the real queue.
#
# Every ledger write goes through the FSM-legal path (bootstrap
# AUDIT_CHALLENGED as the first real event for this audit_id -- per
# audit_ledger.py's own TRANSITIONS[None], AUDIT_PROPOSED is documented
# optional) so harness/audit_ledger.py's own FSM never refuses a step here.
#
# Idempotent by construction: step 0 removes any PRIOR ledger lines,
# review/decision files, demo brief output, and cost-ledger demo entries for
# THIS audit_id before doing anything else, so re-running never duplicates
# or corrupts the chain, and never trips the FSM on a second run.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
cd "$REPO_ROOT"

# `py`, never bare `python`, on this dev machine (hard-won rule 1) -- but the
# `py` launcher is Windows-only and does not exist on Linux CI runners, so
# fall back to `python3` there (never bare `python`, on either platform).
if command -v py >/dev/null 2>&1; then
  PY=py
elif command -v python3 >/dev/null 2>&1; then
  PY=python3
else
  echo "error: neither 'py' nor 'python3' found on PATH" >&2
  exit 1
fi

AUDIT_ID="CURSOR-FIXTURE-full-auto-demo"
LEDGER="architecture/audit-ledger.jsonl"
INBOX_DIR="architecture/inbox"
REVIEWS_DIR="architecture/reviews"
DECISIONS_DIR="architecture/decisions"
COST_LEDGER="harness/queue/cost-ledger.jsonl"
DEMO_OUTPUT="harness/pipeline/demo/output"
DEMO_BRIEFS="$DEMO_OUTPUT/briefs"
FIXTURE_AUDIT="$INBOX_DIR/$AUDIT_ID.md"
REVIEW_PATH="$REVIEWS_DIR/CLAUDE-$AUDIT_ID.md"
DECISION_PATH="$DECISIONS_DIR/DECISION-$AUDIT_ID.md"
LOG_FILE="harness/queue/briefs/006-full-auto-agent-pipeline/deliverables/full-auto-demo.log"

mkdir -p "$(dirname "$LOG_FILE")"
: > "$LOG_FILE"

log() {
  echo "$1" | tee -a "$LOG_FILE"
}

log "=== run_full_auto_demo.sh -- $(date -u +%Y-%m-%dT%H:%M:%SZ) ==="
log "audit_id: $AUDIT_ID"

# --- Step 0: idempotent cleanup ------------------------------------------
"$PY" - "$AUDIT_ID" "$LEDGER" <<'PYEOF'
import json, sys
from pathlib import Path
audit_id, ledger_path = sys.argv[1], sys.argv[2]
p = Path(ledger_path)
if p.exists():
    kept = []
    for line in p.read_text(encoding="utf-8").splitlines():
        raw = line.strip()
        if not raw:
            continue
        try:
            rec = json.loads(raw)
        except json.JSONDecodeError:
            kept.append(raw)
            continue
        if rec.get("audit_id") == audit_id:
            continue
        kept.append(raw)
    p.write_text(("\n".join(kept) + "\n") if kept else "", encoding="utf-8")
PYEOF

"$PY" - "$AUDIT_ID" "$COST_LEDGER" <<'PYEOF'
import json, sys
from pathlib import Path
audit_id, cost_ledger_path = sys.argv[1], sys.argv[2]
p = Path(cost_ledger_path)
if p.exists():
    kept = []
    for line in p.read_text(encoding="utf-8").splitlines():
        raw = line.strip()
        if not raw:
            continue
        try:
            rec = json.loads(raw)
        except json.JSONDecodeError:
            kept.append(raw)
            continue
        if rec.get("audit_id") == audit_id and rec.get("event") == "demo-full-auto-fixture":
            continue
        kept.append(raw)
    p.write_text(("\n".join(kept) + "\n") if kept else "", encoding="utf-8")
PYEOF

rm -f "$REVIEW_PATH" "$DECISION_PATH"
rm -rf "$DEMO_OUTPUT"
mkdir -p "$DEMO_BRIEFS"

log "STEP OK: 0 idempotent cleanup (stale ledger/review/decision/demo-brief/cost-ledger lines for $AUDIT_ID removed)"

# --- Step 1: fixture audit present with >= 3 dated web sources -----------
if [ ! -f "$FIXTURE_AUDIT" ]; then
  log "STEP FAIL: fixture audit missing at $FIXTURE_AUDIT"
  exit 1
fi
SRC_COUNT="$("$PY" -c "
import re, pathlib
t = pathlib.Path('$FIXTURE_AUDIT').read_text(encoding='utf-8')
print(len(re.findall(r'https?://\S+.*?consult\w+ le \d{4}-\d{2}-\d{2}', t)))
")"
if [ "$SRC_COUNT" -lt 3 ]; then
  log "STEP FAIL: fixture audit has only $SRC_COUNT dated web sources (need >= 3)"
  exit 1
fi
log "STEP OK: 1 fixture audit present ($FIXTURE_AUDIT) with $SRC_COUNT dated web sources (>= 3)"

# --- Step 2: claude-challenger scaffolds + fills + records a real review -
"$PY" harness/audit_review.py scaffold --audit-id "$AUDIT_ID" --inbox "$INBOX_DIR" --reviews "$REVIEWS_DIR" >/dev/null

cat > "$REVIEW_PATH" <<EOF
---
review_of: $AUDIT_ID
reviewer: claude-code
target_commit: 000000000000000000000000000000000000000f
reviewed_at: $(date -u +%Y-%m-%dT%H:%M:%SZ)
---

# Contre-audit de $AUDIT_ID (fixture, brief 006 Lot 006c demo)

| # | Point de l'audit | Verdict | Preuve / delimitation |
|---|---|---|---|
| 1 | pipeline full-auto manque une demo E2E rejouable sans humain | CONFIRMED | harness/pipeline/demo/run_full_auto_demo.sh (ce script), harness/tests/test_full_auto_pipeline.py |
EOF

"$PY" harness/audit_review.py record --audit-id "$AUDIT_ID" --inbox "$INBOX_DIR" --reviews "$REVIEWS_DIR" --ledger "$LEDGER" >/dev/null
log "STEP OK: 2 review scaffolded, filled, and recorded -> AUDIT_CHALLENGED"

# --- Step 3: policy auto decides (no owner accept/reject call) -----------
"$PY" harness/audit_decision.py auto --audit-id "$AUDIT_ID" --inbox "$INBOX_DIR" --decisions "$DECISIONS_DIR" --ledger "$LEDGER" >/dev/null
log "STEP OK: 3 policy auto decided -> AUDIT_APPROVED (auto_policy.yaml rule review_has_confirmed_or_partial, no owner call)"

# --- Step 4: convert APPROVED -> CONVERTED, brief seed written -----------
"$PY" harness/audit_convert.py convert --audit-id "$AUDIT_ID" --inbox "$INBOX_DIR" --briefs "$DEMO_BRIEFS" --ledger "$LEDGER" >/dev/null
BRIEF_SLUG_DIR="$(find "$DEMO_BRIEFS" -mindepth 1 -maxdepth 1 -type d | head -n1)"
if [ -z "$BRIEF_SLUG_DIR" ]; then
  log "STEP FAIL: audit_convert did not write a brief seed under $DEMO_BRIEFS"
  exit 1
fi
log "STEP OK: 4 audit converted -> AUDIT_CONVERTED (brief seed at $BRIEF_SLUG_DIR, demo-local, never the real queue)"

# --- Step 5: forge-run MOCK (no Claude/Cursor process spawned) -----------
# Represents claude-planificateur filling the seed, claude-developer
# producing deliverables, the mechanical gate ACCEPTing, and
# claude-evaluator PASSing -- all real LLM/agent invocations in production,
# explicitly simulated here per the brief's own "forge-run mock" wording.
# What IS real: the orchestrator event this outcome triggers next.
"$PY" harness/pipeline/orchestrator.py run --event evaluateur_pass \
  --payload "{\"audit_id\": \"$AUDIT_ID\"}" --ledger "$LEDGER" >/dev/null
log "STEP OK: 5 forge-run mock (Generateur+gate ACCEPT+Evaluateur PASS simulated) -> AUDIT_IMPLEMENTED, AUDIT_VERIFIED"

# --- Step 6: cost-ledger entry, linked back to this audit_id (SC16) ------
"$PY" harness/backends/ledger.py append --backend claude --brief "$BRIEF_SLUG_DIR" \
  --event demo-full-auto-fixture --audit-id "$AUDIT_ID" >/dev/null
log "STEP OK: 6 cost ledger entry recorded with audit_id=$AUDIT_ID ($COST_LEDGER)"

# --- Step 7: archive -- terminal state of the happy path -----------------
"$PY" harness/audit_ledger.py append --audit-id "$AUDIT_ID" --event AUDIT_ARCHIVED \
  --ledger "$LEDGER" --set actor=policy:auto >/dev/null
log "STEP OK: 7 audit archived -> AUDIT_ARCHIVED"

# --- Step 8: verify the ledger chain is complete for this audit_id -------
FINAL_STATE="$("$PY" - "$AUDIT_ID" "$LEDGER" <<'PYEOF'
import sys
sys.path.insert(0, "harness")
import audits, audit_ledger
audit_id, ledger_path = sys.argv[1], sys.argv[2]
events = audit_ledger.read_events(ledger_path)
print(audits.current_state(audit_id, events))
PYEOF
)"
if [ "$FINAL_STATE" != "AUDIT_ARCHIVED" ]; then
  log "STEP FAIL: expected final state AUDIT_ARCHIVED, got $FINAL_STATE"
  exit 1
fi
log "STEP OK: 8 ledger chain complete for $AUDIT_ID -- final state AUDIT_ARCHIVED"

log "=== demo complete -- exit 0 ==="
exit 0
