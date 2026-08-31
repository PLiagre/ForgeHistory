#!/usr/bin/env py
"""Prouve que le vérificateur détecte un dossier incohérent.

Cette démonstration teste un format historique. Son résultat est informatif et
n'accorde aucune autorité de livraison.
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

    detected = result.returncode == 1 and "RESULT: INCOHERENT" in result.stdout
    if detected:
        print(f"PROUVÉ : dossier incohérent détecté. Voir {LOG_FILE}")
        return 0

    print(
        "ÉCHEC DE DÉMONSTRATION : l'incohérence attendue n'a pas été détectée "
        f"(exit_code={result.returncode}). See {LOG_FILE}",
        file=sys.stderr,
    )
    return 1


if __name__ == "__main__":
    sys.exit(main())
