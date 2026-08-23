---
author: hermes
kind: rapport
created_at: 2026-08-23T09:00:00Z
concerns: projet
status: REFLECTED_IN_ROADMAP
---
# Rapport — base saine de pilotage après #126

Lot documentaire. Aucune mécanique de monde. #126 était déjà dans
`origin/master` (`7901ce8`, head `3a4bae6`).

## Inventaire mesuré

| chemin ou famille | rôle aujourd'hui | garder / archiver git / supprimer du working set | pourquoi |
|---|---|---|---|
| `VISION.md` | philosophie produit | garder ; une note de statut en tête | prime sur la roadmap ; piliers inchangés |
| `ROADMAP.md` | où on en est, un prochain pas | garder ; corrigé | vérité après #126 |
| `HANDOFF.md` | reprise de session | garder ; 3 sessions | ADR-0014 |
| `hermes/DASHBOARD.md` | vue générée | garder ; régénérée courte | plus de file d'audits morts |
| `hermes/dashboard.py` | générateur de la vue | garder ; modifié | la vue trop bavarde était un défaut du générateur |
| `hermes/requests/` | décisions propriétaire | garder ; toutes `CLOSED` | déjà reflétées ou exécutées ; boot = OPEN seulement |
| `hermes/reports/` | mémoire | garder ; + ce rapport | pas une lecture de boot |
| `hermes/propositions/` | ce qui attend | garder ; 0 OPEN | proposition G6 caduque après #126 |
| `hermes/milestones/` | jalons d'audit | garder | ADR-0012 ; pas au boot |
| `architecture/inbox/` | audits bruts | garder sur place | append-only ; plus lu au boot |
| `architecture/archive/` | boucles closes | garder | preuve ; pas au boot |
| `architecture/decisions/` | verdicts humains | garder | un ADR par décision ailleurs ; ici l'audit |
| `harness/queue/queue.md` | table morte depuis 001 | garder | dit elle-même qu'elle n'est plus autoritaire |
| `harness/queue/cost-ledger.jsonl` | mesure backends | garder | pas un roman ; pas dans ROADMAP |
| `harness/queue/briefs/001`–`029` | archives d'exécution | garder tous | on n'efface pas un brief ; on cesse de les lire au boot |
| `docs/adr/**` | décisions | garder | pas de compactage |
| `docs/rules/**` | règles payées | garder | pas de paraphrase |
| `CLAUDE.md` / `AGENTS.md` | routage | garder ; table raccourcie | pas une seconde vision |
| `sim/` / `pipeline/geo/` / `viewer/` | produit | garder ; README déjà justes | hors périmètre de ce lot |
| `unity/` | référence gelée | garder, ne pas toucher | ADR-0016 |

Aucune suppression de brief, verdict, preuve, artefact geo, test ou ADR.

## Fermé, déplacé, inchangé

- Fermé : 17 `DEMANDE-*` (toutes `CLOSED`). 1 proposition G6 (`CLOSED`).
- Déplacé : rien. `architecture/inbox/` reste append-only.
- Supprimé du working set de boot : inbox d'audits, briefs 001–025,
  demandes closes, historique de sessions au-delà de trois.

## Compteurs reconstruits

| compteur | valeur |
|---|---|
| `fichiers_lus_au_boot_hermes` | 4 (`hermes/DASHBOARD.md`, `hermes/propositions/`, `ROADMAP.md`, `HANDOFF.md`) |
| `demandes_hermes_open` | 0 |
| `propositions_open` | 0 |
| `handoff_sessions` | 3 |
| `roadmap_mentions_pr_perimees` | 0 |
| `dashboard_audits_actionnables` | 0 |

Suites : `harness/tests` 407 passed / 16 skipped (Unity Linux) ;
`control-plane/tests` 81 OK ; `git diff --check` propre ;
`.venv/bin/python -m sim --ticks 0 --json` : 596 cellules, `sans_unity`.

CI initiale rouge pour deux raisons mécaniques, pas un défaut du ménage :
`risk-gate` exigeait `Forge-Risk: R2` (chemins Hermes / VISION / ROADMAP) ;
`cursor-scope` prenait toute branche `cursor/` pour un dépôt d'audit.
La garde ne s'applique plus qu'aux PRs qui touchent `architecture/inbox/`.

## Prochain pas produit (un seul)

Exécuter le brief 026 — gisements 1400.

Pourquoi pas G6 consommable : le cache Copernicus manque, la preuve
Europe est bloquée, le snapshot dit encore `not_consumed`. Pourquoi pas
un lot visuel : V0 première tranche est déjà dans #126.
