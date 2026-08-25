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
| [0011](0011-hermes-console-du-proprietaire.md) | Hermes console du propriétaire — un droit d'agir borné, sur ordre explicite | accepted | 2026-08-12 |
| [0012](0012-audit-contre-audit-par-grandes-etapes.md) | Audit et contre-audit par grandes étapes (jalons `hermes/milestones/`) — plus jamais par PR | accepted | 2026-08-13 |
| [0013](0013-forgepilot-hermes-claude-cursor.md) | Pilote Hermes léger, Claude Code en lecture et Cursor comme unique exécutant | accepted | 2026-08-14 |
| [0014](0014-hermes-declenche-claude-juge.md) | Hermes déclenche et rend compte, Claude juge, Cursor exécute — amende 0010 et 0013 | accepted | 2026-08-15 |
| [0015](0015-capacites-hermes-sous-agents-crons-issues.md) | Les trois capacités d'Hermes : sous-agents qui lisent sans juger, crons de lecture, issues qui pointent sans instruire | accepted | 2026-08-19 |
| [0016](0016-sim-sans-unity-hermes-pilote-et-propose.md) | `sim/` sans Unity est le produit vivant ; Hermes pilote et propose | accepted | 2026-08-20 |
| [0017](0017-grok-juge-claude-temoin-fusion-mecanique.md) | Grok juge la PR, Claude Opus 5 témoin rare, fusion mécanique | accepted | 2026-08-23 |
| [0018](0018-degraissage-trois-acteurs-et-carte-figee.md) | Le dégraissage — trois acteurs, carte figée, vraisemblable plutôt que véridique | accepted | 2026-08-25 |

## Lecture

**ADR-0018 est le point d'entrée.** Il amende ADR-0001, ADR-0002 et
ADR-0005 à ADR-0017 : là où l'un d'eux contredit ADR-0018, c'est ADR-0018
qui vaut.

Les ADR périmés par le dégraissage — parce qu'ils décrivent un code ou une
organisation qui n'existent plus — sont conservés comme mémoire du projet,
mais ne sont plus à lire au démarrage :

| ADR | pourquoi il ne décrit plus le dépôt |
|---|---|
| 0001 | les trois rôles ne sont plus trois agents ; seule la règle « qui produit ne juge pas » subsiste |
| 0002 | le backend Générateur enfichable est supprimé |
| 0005, 0012 | la boucle audit / contre-audit et sa machine d'états sont supprimées |
| 0006, 0007 | le pipeline full-auto est supprimé |
| 0008, 0009 | le backend Codex est supprimé |
| 0010, 0011, 0013, 0014, 0015 | la chaîne à quatre acteurs est remplacée par les trois acteurs d'ADR-0018 |
| 0016 | reste vrai sauf sur un point : Hermes écrit désormais les briefs |
| 0017 | reste vrai sur la répartition des modèles ; la fusion mécanique automatique disparaît |

Restent pleinement en vigueur : **ADR-0003** (la cellule est la clé spatiale
unique, la province est dérivée), **ADR-0004** (l'origine du code Unity, en
veille) et **ADR-0018**.
