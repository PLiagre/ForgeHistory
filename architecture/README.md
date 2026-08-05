# architecture/ — la boucle d'audit multi-agents

Ce dossier héberge la **boucle d'audit indépendant** de Forge : Cursor Cloud
audite un commit, Claude challenge l'audit, le propriétaire tranche, et un
audit accepté redevient un **brief normal** sous
`harness/queue/briefs/`.

> **Étape 1 de la migration** — ce commit ne pose que le *squelette* : ce
> README (contrat + schéma), les dossiers, et rien d'autre. Les commandes
> `/forge-audit-*`, le `audit-ledger.jsonl` et la CI arrivent aux étapes
> suivantes. Tant qu'aucun audit n'est traité, **rien ici n'affecte le
> workflow harness existant** (`/forge-run`, le gate, les briefs).

Conception complète : voir le document d'architecture *« Cursor comme
auditeur indépendant »* (à figer en `docs/adr/00NN-cursor-as-auditor.md`).

## Principe : Cursor audite, il ne développe jamais

Le développeur canonique reste **Claude Code**. Cursor est un **auditeur en
lecture seule**. Un audit n'est **jamais** une instruction exécutable : c'est
une *entrée*. Seul le propriétaire peut, par conversion explicite, la
transformer en brief — et le brief reste alors la **source unique
d'instruction** (voir `CLAUDE.md` › « Single Source of Instruction »).

## Un seul rôle écrit dans chaque dossier

| Dossier | Écrit par | Rôle |
|---|---|---|
| `inbox/` | **Cursor seul** | Audits bruts, `status: PROPOSED`. **Immuable** : un fichier neuf par nouveau commit audité, jamais édité ni supprimé. |
| `reviews/` | **Claude seul** | Le contre-audit (« challenge ») : chaque point marqué `CONFIRMED` / `REFUTED` / `PARTIAL` / `NEEDS_OWNER`, avec preuve. |
| `decisions/` | **Propriétaire seul** | Un verdict humain `APPROVED` / `REJECTED` + justification + points retenus. |
| `archive/` | Machine (commande) | Audits en état terminal, regroupés et gelés. |
| `audit-ledger.jsonl` | Machine (commande) | *(étape 2)* Une ligne par transition d'état. Referme la boucle audit ↔ brief. |

Cette séparation reproduit au niveau des dossiers la règle Forge *« trois
rôles, jamais un seul agent »* : on peut prouver mécaniquement **qui** a écrit
**quoi**.

## Cycle de vie d'un audit

```
[Cursor]           [Claude]            [Propriétaire]
PROPOSED  ──review──▶ CHALLENGED ──humain──▶ APPROVED ─┬─▶ CONVERTED ─▶ IMPLEMENTED ─▶ VERIFIED ─▶ ARCHIVED
                                             REJECTED ─┴─▶ ARCHIVED
(target_commit obsolète avant acceptation ─▶ STALE)
```

| Statut | Signification |
|---|---|
| `AUDIT_PROPOSED` | Posé par Cursor dans `inbox/`. Rien n'est engagé. |
| `AUDIT_CHALLENGED` | Claude a produit son contre-audit ; prêt pour arbitrage humain. |
| `AUDIT_APPROVED` | Propriétaire a retenu au moins un point. |
| `AUDIT_REJECTED` | Propriétaire a écarté ; motif consigné. |
| `AUDIT_CONVERTED` | Transformé en brief(s) ; ID(s) référencé(s). |
| `AUDIT_IMPLEMENTED` | Le(s) brief(s) issu(s) ont passé le gate + l'Évaluateur. |
| `AUDIT_VERIFIED` | Mergé, CI verte sur le SHA final. |
| `AUDIT_STALE` | `target_commit` obsolète avant acceptation → à re-soumettre. |
| `AUDIT_ARCHIVED` | État terminal, gelé. |

## Schéma du frontmatter d'un audit (`inbox/CURSOR-<sha>-<sujet>.md`)

Chaque audit commence par un frontmatter YAML. Ce schéma **décrit le format
déjà produit par Cursor** ; il sera imposé mécaniquement par la CI à
l'étape 8.

```yaml
---
audit_id:                CURSOR-6231186-execution-budgets   # unique ; = nom de fichier sans .md
auditor:                 cursor-cloud                        # identité de l'auditeur
target_branch:           master                              # branche auditée
target_commit:           623118671dd98543a197b06415a240b9912999af   # SHA complet audité
created_at:              2026-08-03T18:44:03Z                # ISO 8601 UTC
audit_type:              architecture-and-qa                 # nature de l'audit
status:                  PROPOSED                            # état initial ; seul PROPOSED est valide à l'entrée
implementation_authorized: false                            # DOIT être false
ci_changes_authorized:   false                               # DOIT être false
code_changes_authorized: false                               # DOIT être false
---
```

### Champs

| Champ | Requis | Contrainte |
|---|---|---|
| `audit_id` | oui | Identifiant unique ; doit correspondre au nom de fichier (sans `.md`). |
| `auditor` | oui | Identité de l'agent auditeur (ex. `cursor-cloud`). |
| `target_branch` | oui | Branche auditée. |
| `target_commit` | oui | SHA **complet** (40 hex) de l'état audité — sert au calcul de fraîcheur. |
| `created_at` | oui | Horodatage ISO 8601 UTC. |
| `audit_type` | oui | Catégorie libre mais stable (ex. `architecture-and-qa`). |
| `status` | oui | `PROPOSED` à l'entrée. Les transitions ultérieures sont journalisées par le ledger, pas ré-écrites ici. |
| `implementation_authorized` | oui | **DOIT** valoir `false`. |
| `ci_changes_authorized` | oui | **DOIT** valoir `false`. |
| `code_changes_authorized` | oui | **DOIT** valoir `false`. |

### Règles d'intégrité (futures gardes CI)

1. Une PR d'auditeur ne touche **que** `architecture/inbox/**` — jamais du
   code, des tests, des workflows, ni un brief.
2. Les trois flags `*_authorized` **doivent** être `false` ; sinon l'audit
   s'auto-attribue une autorisation qu'il n'a pas.
3. `inbox/` est **append-only** : un nouvel audit = un nouveau fichier ; on
   ne modifie ni ne supprime un audit existant.
4. Le `target_commit` doit exister dans l'historique de `target_branch`.

## Compatibilité

Tout ici est **additif**. Aucun chemin existant n'est modifié
(`harness/queue/briefs/**`, `verdict_audit.py`, `/forge-run`, agents
`forge-*`). Si Cursor n'est jamais utilisé, ce dossier reste inerte et le
harness fonctionne exactement comme avant.
