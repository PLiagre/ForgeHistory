#!/usr/bin/env python
"""Preuve R1 / v1_081 : gisements extractifs déclarés — deux passes actives + passe coupée.

Usage, depuis `pipeline/geo/` :
  ../../.venv/bin/python tests/run_proof_r1.py
"""

from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from constants import R1_PIPELINE_VERSION  # noqa: E402
from io_util import read_json, sha256_file, write_json  # noqa: E402
from qa.checks_r1 import run_r1_green  # noqa: E402
from tests.test_qa_red_r1 import run_all_red_r1  # noqa: E402

LOGS = ROOT / "logs"
ARTIFACTS = ROOT / "artifacts"
REGISTRY = ROOT / "registry"


def _load_r1():
    path = ROOT / "steps" / "r1_resources_1400.py"
    spec = importlib.util.spec_from_file_location("resources_r1", path)
    if spec is None or spec.loader is None:
        raise ImportError(path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def main() -> int:
    LOGS.mkdir(parents=True, exist_ok=True)
    r1 = _load_r1()
    t_all = time.perf_counter()

    context = r1.load_context()
    declared_ids = [str(d["id"]) for d in context["declarations"].get("deposits") or []]
    step_source = (ROOT / "steps" / "r1_resources_1400.py").read_text(encoding="utf-8")
    checks_source = (ROOT / "qa" / "checks_r1.py").read_text(encoding="utf-8")

    run1 = r1.run_resources(context=context, apply_declarations=True, export=True, captures=True)
    shas1 = dict(run1["shas"])

    run2 = r1.run_resources(context=context, apply_declarations=True, export=True, captures=False)
    shas2 = dict(run2["shas"])

    sha_pairs = {
        key: [shas1.get(key, ""), shas2.get(key, "")]
        for key in sorted(set(shas1) | set(shas2))
    }
    determinism_match = all(
        len(pair) == 2 and pair[0] == pair[1] and bool(pair[0])
        for pair in sha_pairs.values()
    ) and len(sha_pairs) > 0

    with tempfile.TemporaryDirectory(prefix="r1_off_") as tmp:
        tmp_path = Path(tmp)
        off_art = tmp_path / "artifacts"
        off_art.mkdir(parents=True)
        run_off = r1.run_resources(
            context=context,
            apply_declarations=False,
            output_dir=off_art,
            export=True,
            captures=False,
        )
        sha_cells_on = sha256_file(ARTIFACTS / "cells_resources_r1.json")
        sha_cells_off = sha256_file(off_art / "cells_resources_r1.json")
        stats_off = run_off["stats"]
        cells_off_count = len(run_off["cells_out"])

    cells_g3 = context["cells"]
    published = run2["published"]
    cells_resources = run2["cells_out"]
    stats = run2["stats"]
    containment_recheck = run2["containment_checks"]

    artifact_docs = [
        read_json(ARTIFACTS / "resources_1400_r1.json"),
        read_json(ARTIFACTS / "cells_resources_r1.json"),
        read_json(ARTIFACTS / "stats_r1.json"),
        read_json(ARTIFACTS / "MANIFEST_r1.json"),
        read_json(REGISTRY / "resource_registry.json"),
        read_json(ROOT / "data" / "resources_1400.json"),
    ]

    green = run_r1_green(
        declarations=context["declarations"],
        published=published,
        cells_g3=cells_g3,
        cells_resources=cells_resources,
        stats=stats,
        artifact_docs=artifact_docs,
        sha_pairs=sha_pairs,
        sha_cells_on=sha_cells_on,
        sha_cells_off=sha_cells_off,
        stats_off=stats_off,
        cells_off_count=cells_off_count,
        step_source=step_source,
        checks_source=checks_source,
        declared_ids=declared_ids,
        containment_recheck=containment_recheck,
    )
    reds = run_all_red_r1(
        declarations=context["declarations"],
        published=published,
        cells_g3=cells_g3,
        cells_resources=cells_resources,
        stats=stats,
        artifact_docs=artifact_docs,
        sha_pairs=sha_pairs,
        sha_cells_on=sha_cells_on,
        sha_cells_off=sha_cells_off,
        stats_off=stats_off,
        cells_off_count=cells_off_count,
        step_source=step_source,
        checks_source=checks_source,
        declared_ids=declared_ids,
        containment_recheck=containment_recheck,
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

    reversibility_ok = sha_cells_on != sha_cells_off
    ok = all_green_ok and all_red_ok and determinism_match and reversibility_ok

    qa_report = {
        "pipeline_version": R1_PIPELINE_VERSION,
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
            "cells_on": sha_cells_on,
            "cells_off": sha_cells_off,
            "cells_differ": reversibility_ok,
        },
        "metrics": stats,
    }
    write_json(LOGS / "v1_081_qa.json", qa_report)

    on_line = r1.format_summary_line(run2["derived"], apply_declarations=True)
    off_line = r1.format_summary_line(run_off["derived"], apply_declarations=False)
    (LOGS / "v1_081_declarations_on.txt").write_text(on_line + "\n", encoding="utf-8")
    (LOGS / "v1_081_declarations_off.txt").write_text(off_line + "\n", encoding="utf-8")

    elapsed = time.perf_counter() - t_all
    log_lines = [
        f"R1 proof v1_081 | elapsed_s={elapsed:.2f}",
        f"determinism_match={determinism_match} pairs={len(sha_pairs)}",
        f"cells_on!=off={reversibility_ok}",
        f"all_green={all_green_ok} all_red={all_red_ok} exit={0 if ok else 1}",
        on_line,
        off_line,
    ]
    for c in checks_out:
        log_lines.append(
            f"  {c['id']}: green={c['green_ok']} red={c['red_ok']} | {c['detail']}"
        )
    (LOGS / "v1_081_resources.log").write_text("\n".join(log_lines) + "\n", encoding="utf-8")

    print(json.dumps({"ok": ok, "checks": len(checks_out), "determinism_pairs": len(sha_pairs)}))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
