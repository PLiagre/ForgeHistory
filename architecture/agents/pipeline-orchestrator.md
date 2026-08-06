# pipeline-orchestrator

Lot 006b role contract (brief `006-full-auto-agent-pipeline`, "Rôles agents
(contrats obligatoires)" § 6).

# Identité

**Machine, pas un LLM juge métier.** Script Python déterministe
(`harness/pipeline/orchestrator.py`) + workflows GitHub Actions
(`.github/workflows/pipeline-*.yml`). Remplace le propriétaire pour
accept/reject **selon la politique figée** de
`harness/pipeline/auto_policy.yaml` — il n'improvise aucune décision, il
applique une table de règles versionnée.

# Entrées

- Événements du ledger d'audit (`architecture/audit-ledger.jsonl`).
- Statuts CI des workflows requis.
- Fichiers : `harness/pipeline/auto_policy.yaml`,
  `harness/pipeline/config.yaml`, payloads JSON passés en CLI (SHA de
  merge, `audit_id`, `brief_dir`).

# Sorties

- Appends au ledger d'audit, **exclusivement** via
  `harness.audit_ledger.append_event` (jamais de ligne JSON construite à la
  main — condition de non-contournement de la FSM établie au Lot 006a).
- Labels de PR, invocation des autres rôles via API/CLI (documentée dans
  `architecture/agents/README.md`), mise en queue de `/forge-run`.

# Interdits

- Faire un choix non couvert par une règle de `auto_policy.yaml` — un cas
  non prévu doit refuser (fail closed) et journaliser
  `escalate: no matching policy rule`, jamais deviner.
- Construire une ligne de ledger sans passer par `append_event` (voir
  `harness/audit_ledger.py` — un guard placé après l'effet qu'il devrait
  prévenir ne protège rien).
- Merger vers `master` une PR touchant un chemin de
  `auto_merge_denylist` (`.github/workflows/**`, `harness/verdict_audit.py`,
  `VISION.md`) sans exception explicitement listée dans ADR-0006.
- Modifier `auto_policy.yaml` lui-même en réaction à un événement qu'il
  traite (la politique est versionnée séparément, jamais auto-modifiée par
  la machine qui l'applique).

# Déclencheur

`.github/workflows/pipeline-orchestrate.yml` (merge de review, changement
du ledger, ou `workflow_dispatch`) ; également invoqué en CLI directe par
les trois autres workflows pipeline (`pipeline-audit.yml`,
`pipeline-challenge.yml`, `pipeline-forge-run.yml`) pour journaliser leurs
transitions respectives.

# Preuve de fin

`py harness/pipeline/orchestrator.py run --event <kind> --payload '<json>'`
sort 0 et le ledger contient le nouvel événement attendu pour l'`audit_id`
du payload (vérifiable par `py harness/audit_ledger.py show --audit-id
<ID>`) ; ou sort non-zéro avec un message d'erreur explicite si la règle de
policy refuse (fail closed, jamais un exit 0 silencieux sur un refus).

# Budget max appels

Sans objet au sens agent (c'est un script déterministe, pas un LLM) — le
budget qui s'applique est celui du **rôle qu'il invoque** ensuite
(`claude-developer`: `harness/budget.py` ; `cursor-auditor` /
`cursor-qa-scout`: § Budget max appels de leur propre fichier).
