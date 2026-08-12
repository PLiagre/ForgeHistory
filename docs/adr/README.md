# Architecture Decision Records

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [0001](0001-three-role-harness-and-mechanical-gate.md) | Three-role harness + mechanical gate as F0's core structural decision | accepted | 2026-07-29 |
| [0002](0002-pluggable-generator-backend.md) | Pluggable Générateur backend (Claude Code default, Cursor CLI as second backend) | accepted | 2026-07-29 |
| [0003](0003-single-spatial-primary-key.md) | The geographic cell as the single spatial primary key (Province as derived aggregation) | accepted | 2026-07-29 |
| [0004](0004-bulk-port-victoriaproject-unity-game.md) | Bulk-port VictoriaProject's Unity game into `unity/game_unity/`, automation layer excluded | accepted | 2026-07-31 |
| [0005](0005-cursor-as-independent-auditor.md) | Cursor Cloud as independent auditor (repositioned from Générateur backend) | accepted | 2026-08-05 |
| [0006](0006-full-auto-agent-pipeline.md) | Full-auto agent pipeline (derogation to ADR-0005's owner step) | accepted | 2026-08-05 |
| [0007](0007-full-auto-mode-split.md) | Split `mode: full_auto` into `full_auto_decision_only` and a reserved, fail-closed `full_auto` | accepted | 2026-08-10 |
| [0008](0008-codex-as-evaluateur-under-credit-cap.md) | Codex may hold the Évaluateur role, only in a third-party-triggered session, only when Claude is credit-capped | accepted | 2026-08-11 |
| [0009](0009-codex-as-official-generator-backend.md) | Codex as an official Générateur backend | accepted | 2026-08-11 |
| [0010](0010-hermes-chef-de-projet-workflow-quatre-acteurs.md) | Hermes chef de projet (point d'entrée, tient `ROADMAP.md`) et chaîne à quatre acteurs Hermes → Claude → Codex → Cursor | accepted | 2026-08-12 |
