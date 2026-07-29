#!/usr/bin/env py
"""
F0's done-criterion, made concrete: a deliberately fake brief must be
rejected by the mechanical gate, and the rejection must be PROVEN, not
narrated.

Runs verdict_audit.py against this directory's forged brief as a real
subprocess, logs the full stdout/stderr + exit code to run_demo.log
(timestamped, command included verbatim -- hard-won rule 9: an impossibility
is tested before being invoked, command + error, never a bare claim), and
exits 0 only if the gate actually rejected the forgery. Exits 1 if the gate
ever wrongly accepts it -- that would be a demo failure, not a success.
"""
import datetime
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO_ROOT = HERE.parent.parent.parent
VERDICT_AUDIT = REPO_ROOT / "harness" / "verdict_audit.py"
LOG_FILE = HERE / "run_demo.log"


def main() -> int:
    cmd = [sys.executable, str(VERDICT_AUDIT), str(HERE)]
    result = subprocess.run(cmd, capture_output=True, text=True, cwd=REPO_ROOT)

    log_lines = [
        f"# run_demo.py log -- {datetime.datetime.now().isoformat()}",
        f"# command: {' '.join(cmd)}",
        f"# cwd: {REPO_ROOT}",
        f"# exit_code: {result.returncode}",
        "",
        "## stdout",
        result.stdout,
        "## stderr",
        result.stderr,
    ]
    LOG_FILE.write_text("\n".join(log_lines), encoding="utf-8")

    rejected = result.returncode == 1 and "VERDICT: REJECT" in result.stdout
    if rejected:
        print(f"PROVEN: fake brief was REJECTED by verdict_audit.py. See {LOG_FILE}")
        return 0

    print(
        "DEMO FAILURE: the gate did not reject the forged brief as expected "
        f"(exit_code={result.returncode}). See {LOG_FILE}",
        file=sys.stderr,
    )
    return 1


if __name__ == "__main__":
    sys.exit(main())
