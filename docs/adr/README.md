# Décisions d'architecture — index historique

Les ADR conservent les décisions prises pendant l'évolution du projet. Depuis
la réinitialisation de gouvernance du 2026-08-30, aucune clause ancienne de
rôle, d'identité, de fournisseur, de relecture, de verdict, de porte,
d'orchestration ou de fusion n'est active.

Les règles courantes sont uniquement celles d'[AGENTS.md](../../AGENTS.md).
Chaque ADR porte un avertissement en tête ; son statut original reste visible
comme fait historique.

## Décisions techniques encore pertinentes

| ADR | portée actuelle |
|---|---|
| [0003](0003-single-spatial-primary-key.md) | `cell_id` reste la clé spatiale unique |
| [0016](0016-sim-sans-unity-hermes-pilote-et-propose.md) | `sim/` reste le produit vivant sans Unity ; les rôles sont obsolètes |
| [0018](0018-degraissage-trois-acteurs-et-carte-figee.md) | carte figée, niveaux de vraisemblance et archives ; les rôles sont obsolètes |
| [0020](0020-pc-worker-opportuniste.md) | runner Windows facultatif ; toute distribution de rôles est obsolète |

## ADR historiques

| ADR | sujet historique |
|---|---|
| [0001](0001-three-role-harness-and-mechanical-gate.md) | séparation en trois rôles et porte mécanique |
| [0002](0002-pluggable-generator-backend.md) | backends du générateur |
| [0004](0004-bulk-port-victoriaproject-unity-game.md) | port Unity désormais archivé |
| [0005](0005-cursor-as-independent-auditor.md) | auditeur Cursor |
| [0006](0006-full-auto-agent-pipeline.md) | pipeline automatique |
| [0007](0007-full-auto-mode-split.md) | modes automatiques |
| [0008](0008-codex-as-evaluateur-under-credit-cap.md) | attribution du rôle d'évaluation |
| [0009](0009-codex-as-official-generator-backend.md) | backend Codex |
| [0010](0010-hermes-chef-de-projet-workflow-quatre-acteurs.md) | workflow à quatre acteurs |
| [0011](0011-hermes-console-du-proprietaire.md) | console Hermes |
| [0012](0012-audit-contre-audit-par-grandes-etapes.md) | audits par jalons |
| [0013](0013-forgepilot-hermes-claude-cursor.md) | ancien pilote ForgePilot |
| [0014](0014-hermes-declenche-claude-juge.md) | ancien déclenchement et jugement |
| [0015](0015-capacites-hermes-sous-agents-crons-issues.md) | anciennes capacités Hermes |
| [0017](0017-grok-juge-claude-temoin-fusion-mecanique.md) | juge et fusion mécanique |
| [0019](0019-claude-ecrit-les-briefs-hermes-pilote.md) | propriété des briefs |
| [0021](0021-claude-manuel-jamais-invoque-par-hermes.md) | interdiction d'un fournisseur |

Les briefs, rapports, demandes et jalons liés à ces ADR restent eux aussi des
archives. Leur conservation ne réactive pas leur procédure.
