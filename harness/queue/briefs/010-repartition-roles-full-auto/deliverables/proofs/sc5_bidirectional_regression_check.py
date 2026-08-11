"""
SC5, iteration 2. The iteration-1 checker (sc5_regression_check.py) only
looked for PASS->FAIL flips. That is the direction non-goal 7 forbids, but
it is not the direction the Evaluateur's D1 finding came from: D1 was a
FAIL->PASS flip (a case the control used to refuse started passing). A
checker that only watches one direction would have said ACCEPT on the very
regression this iteration exists to fix. This script watches both.

Usage:
    py sc5_bidirectional_regression_check.py <before.txt> <after.txt>

<before.txt> / <after.txt> are the full-gate reports produced by running
verdict_audit.py over every harness/queue/briefs/*/ directory, each brief's
block introduced by a line "=== <brief-name> ===" (see
sc5-gate-before-d1fix-all-briefs.txt / sc5-gate-after-d1fix-all-briefs.txt).
"""
import re
import sys
import pathlib


def parse(path):
    text = pathlib.Path(path).read_text(encoding="utf-8")
    blocks = re.split(r"^=== (.+?) ===$", text, flags=re.MULTILINE)[1:]
    out = {}
    for i in range(0, len(blocks), 2):
        name = blocks[i].strip()
        body = blocks[i + 1]
        m = re.search(r"\[(PASS|FAIL|N/A)\] verdict_is_not_self_authored", body)
        out[name] = m.group(1) if m else None
    return out


def main():
    before = parse(sys.argv[1])
    after = parse(sys.argv[2])
    assert set(before) == set(after), (set(before) - set(after), set(after) - set(before))

    pass_to_fail = [n for n in before if before[n] == "PASS" and after[n] == "FAIL"]
    fail_to_pass = [n for n in before if before[n] == "FAIL" and after[n] == "PASS"]

    print(f"briefs compared: {len(before)}")
    print(f"PASS->FAIL regressions (non-goal 7): {len(pass_to_fail)} {pass_to_fail}")
    print(f"FAIL->PASS regressions (D1 direction): {len(fail_to_pass)} {fail_to_pass}")
    for n in sorted(before):
        print(f"  {n}: {before[n]} -> {after[n]}")

    if pass_to_fail or fail_to_pass:
        sys.exit(1)


if __name__ == "__main__":
    main()
