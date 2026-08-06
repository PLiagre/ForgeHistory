"""harness/pipeline -- full-auto agent pipeline (Brief 006).

Lot 006a delivers only the governance/config surface consumed by
`audit_decision.py --policy auto`: `auto_policy.yaml`, `config.yaml`, and
the tiny vendored loader in `policy_loader.py` (PyYAML is not installed in
this environment -- see harness/queue/briefs/006-full-auto-agent-pipeline/
deliverables/generator-log.md for the check that established that).

`orchestrator.py`, the GitHub workflows, and the demo are explicitly Lot
006b/006c -- not created here.
"""
