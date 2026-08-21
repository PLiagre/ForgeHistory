#!/usr/bin/env python
"""Preuve C1 / v1_080 : insolation astronomique et continentalité — deux passes, sept contrôles.

Usage, depuis `pipeline/geo/` :
  ../../.venv/bin/python tests/run_proof_c1.py
"""

from __future__ import annotations

import importlib.util
import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from constants import C1_PIPELINE_VERSION  # noqa: E402
from io_util import read_json, sha256_file, write_json  # noqa: E402
from qa.checks_c1 import run_c1_green  # noqa: E402
from tests.test_qa_red_c1 import run_all_red_c1  # noqa: E402

LOGS = ROOT / "logs"
ARTIFACTS = ROOT / "artifacts"


def _load_c1():
    path = ROOT / "steps" / "c1_climate_drivers.py"
    spec = importlib.util.spec_from_file_location("climate_c1", path)
    if spec is None or spec.loader is None:
        raise ImportError(path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def main() -> int:
    LOGS.mkdir(parents=True, exist_ok=True)
    c1 = _load_c1()
    t_all = time.perf_counter()

    context = c1.load_context()
    run1 = c1.run_climate_drivers(context=context, export=True, captures=True)
    shas1 = dict(run1["shas"])

    run2 = c1.run_climate_drivers(context=context, export=True, captures=True)
    shas2 = dict(run2["shas"])

    sha_pairs = {
        key: [shas1.get(key, ""), shas2.get(key, "")]
        for key in sorted(set(shas1) | set(shas2))
    }
    determinism_match = all(
        len(pair) == 2 and pair[0] == pair[1] and bool(pair[0])
        for pair in sha_pairs.values()
    ) and len(sha_pairs) > 0

    cells_g3 = context["cells"]
    climate_cells = run2["cells_out"]
    coastal_ids = run2["derived"]["coastal_ids"]
    metrics = run2["metrics"]

    artifact_docs = [
        read_json(ARTIFACTS / "cells_climate_drivers_c1.json"),
        read_json(ARTIFACTS / "stats_c1.json"),
        read_json(ARTIFACTS / "MANIFEST_c1.json"),
        read_json(ROOT / "registry" / "climate_drivers_registry.json"),
    ]

    green = run_c1_green(
        cells_g3=cells_g3,
        climate_cells=climate_cells,
        coastal_ids=coastal_ids,
        artifact_docs=artifact_docs,
        sha_pairs=sha_pairs,
    )
    reds = run_all_red_c1(
        cells_g3=cells_g3,
        climate_cells=climate_cells,
        coastal_ids=coastal_ids,
        artifact_docs=artifact_docs,
        sha_pairs=sha_pairs,
    )

    checks_out = []
    all_green_ok = True
    all_red_ok = True
    for check in green:
        proof = reds.get(check.id, {})
        became_red = bool(proof.get("became_red"))
        red_proof = str(proof.get("case") or "") if became_red else ""
        if not became_red:
            all_red_ok = False
        if not check.passed:
            all_green_ok = False
        checks_out.append(
            {
                "id": check.id,
                "name": check.name,
                "passed": bool(check.passed and became_red),
                "detail": check.detail,
                "red_proof": red_proof,
                "green_ok": bool(check.passed),
                "red_ok": became_red,
            }
        )

    ok = all_green_ok and all_red_ok and determinism_match

    qa_report = {
        "pipeline_version": C1_PIPELINE_VERSION,
        "exit_code": 0 if ok else 1,
        "checks": [
            {
                "id": c["id"],
                "name": c["name"],
                "passed": c["passed"],
                "detail": c["detail"],
                "red_proof": c["red_proof"],
            }
            for c in checks_out
        ],
        "determinism": {
            "runs": 2,
            "match": determinism_match,
            "sha256": sha_pairs,
        },
        "metrics": metrics,
    }
    write_json(LOGS / "v1_080_qa.json", qa_report)

    elapsed = time.perf_counter() - t_all
    log_lines = [
        f"C1 proof v1_080 | elapsed_s={elapsed:.2f}",
        f"determinism_match={determinism_match} pairs={len(sha_pairs)}",
        f"all_green={all_green_ok} all_red={all_red_ok} exit={0 if ok else 1}",
    ]
    for c in checks_out:
        log_lines.append(
            f"  {c['id']}: green={c['green_ok']} red={c['red_ok']} | {c['detail']}"
        )
    (LOGS / "v1_080_climate_drivers.log").write_text("\n".join(log_lines) + "\n", encoding="utf-8")

    print(json.dumps({"ok": ok, "checks": len(checks_out), "determinism_pairs": len(sha_pairs)}))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
