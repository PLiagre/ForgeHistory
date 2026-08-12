# ADR-0010: Hermes chef de projet et chaîne à quatre acteurs

**Date**: 2026-08-12
**Status**: accepted
**Deciders**: propriétaire du projet (demande explicite, session Cursor Cloud
du 2026-08-12) ; rédaction déléguée à Cursor.

## Context

La répartition enregistrée le 2026-08-11 (fin de
`architecture/decisions/DECISION-CURSOR-e9a6f4c-codex-passation-full-auto.md`,
reprise dans `HANDOFF.md`) faisait d'Hermes un **observateur en lecture
seule**, sans aucun droit d'écriture dans le dépôt, et laissait à Claude la
totalité du pilotage. Le propriétaire constate que ce montage lui impose de
re-découvrir l'état du projet à chaque session : il manque un point d'entrée
unique qui tienne le contexte global et la feuille de route.

Par ailleurs, les trois workflows d'invocation d'agents
(`pipeline-audit.yml`, `pipeline-challenge.yml`, `pipeline-forge-run.yml`)
étaient restés des stubs documentés `TODO(operator...)` : la boucle
décisionnelle tournait sans humain, mais aucun agent n'était réellement
appelé par la CI.

## Decision

Le propriétaire arrête la chaîne suivante — quatre acteurs, chacun un
maillon, jamais deux maillons adjacents tenus par le même acteur sur le même
lot :

| acteur | rôle | écrit | n'écrit jamais |
|---|---|---|---|
| **Hermes** | **Chef de projet** — point d'entrée du propriétaire, suivi global, contexte ; tient la feuille de route | `ROADMAP.md`, `hermes/**` (rapports, demandes d'évolution) | code, CI, briefs, verdicts, audits |
| **Claude Code** | **CTO** — lit la roadmap, écrit les briefs (Planificateur), orchestre `/forge-run`, évalue (Évaluateur), intègre et ouvre les PR | briefs, rubriques, verdicts, merges/PR | le verdict d'un lot qu'il a produit |
| **Codex** (modèle GPT-5.6 Sol) | **Exécutant** — Générateur par défaut des lots (`--backend codex`) ; Évaluateur de substitution sous l'exception ADR-0008 | code, tests, `deliverables/` du lot | le verdict d'un lot qu'il a produit ; ne committe jamais |
| **Cursor** | **Critique** — relit chaque PR contre les bonnes pratiques d'ingénierie IA sourcées (`architecture/review-guidelines.md`) et audite les merges | `architecture/inbox/**` | code, CI, briefs |

Trois conséquences opérationnelles, décidées en même temps :

1. **Hermes passe d'observateur à chef de projet.** Il obtient un droit
   d'écriture strictement borné : `ROADMAP.md` et `hermes/**`. Un rapport ou
   une demande Hermes reste une **entrée** pour le CTO, jamais une
   instruction pour un Générateur (la source d'instruction reste le brief —
   `CLAUDE.md` › Single Source of Instruction). Contrat d'écriture complet :
   `hermes/README.md`. Ceci met en œuvre — et étend — l'arbitrage n°4 du
   2026-08-11 (« Hermes → contrat d'écriture dans le dépôt »).
2. **Les trois stubs d'invocation sont câblés pour de vrai** : Claude
   headless (`claude -p` avec `--max-budget-usd`, arbitrage n°2 du
   2026-08-11) pour forge-run et challenge, l'API Cursor Cloud Agents pour
   l'audit/critique — qui se déclenche désormais aussi sur chaque
   `pull_request`, pas seulement après merge. Sans secret configuré, chaque
   workflow consigne une dérogation et ne fait rien : jamais d'échec ni de
   succès silencieux.
3. **La critique Cursor est adossée à des sources externes datées** :
   `architecture/review-guidelines.md` consolide les bonnes pratiques
   d'ingénierie IA (revue par intention, portes mécaniques, preuve
   d'exécution, revue adverse, découpage des diffs), chacune avec URL et
   date de consultation, et sera re-sourcée à chaque trimestre.

La décision du 2026-08-11 n'est remplacée **que sur la ligne Hermes** ; les
lignes Codex, Cursor et Claude sont reconduites telles quelles, ainsi que la
substitution « option B » (session d'évaluation distincte, déclenchée par un
tiers) et les arbitrages n°1 (porte conditionnelle de fusion) et n°3
(`cursor-auditor` câblé en premier).

## Alternatives Considered

### Alternative 1 : Cursor comme point d'entrée (audit `CURSOR-32640da-entry-point-force-proposition`, PR #12)
- **Pros** : Cursor a déjà un canal d'écriture (`architecture/inbox/`) et
  une discipline de preuve ; la proposition était rédigée et argumentée.
- **Cons** : cumulerait « propose les évolutions » et « critique les PR qui
  en résultent » — le même acteur en amont et en aval de la chaîne.
- **Why not** : le propriétaire a tranché pour Hermes comme point d'entrée ;
  Cursor garde le maillon où son indépendance a le plus de valeur : la
  critique. La PR #12 est fermée comme remplacée par la présente décision.

### Alternative 2 : garder Hermes en lecture seule et loger la roadmap chez Claude
- **Pros** : aucun changement au contrat du 2026-08-11.
- **Cons** : le suivi global resterait éclaté entre `HANDOFF.md` (état de
  session) et la tête du propriétaire ; c'est le problème constaté.
- **Why not** : un chef de projet sans document qu'il possède n'est pas un
  point d'entrée.

## Consequences

### Positive
- Un seul endroit où lire l'état et la direction du projet : `ROADMAP.md`,
  tenu par un acteur dont c'est l'unique mandat.
- La boucle complète (roadmap → brief → génération Codex → gate → verdict →
  PR → critique Cursor) peut tourner sans intervention humaine autre que la
  fourniture des secrets et la fusion finale.
- La séparation producteur/juge du harnais est inchangée et reste vérifiée
  mécaniquement (`verdict_audit.check_verdict_not_self_authored`).

### Negative
- Un acteur de plus avec droit d'écriture = une surface de dérive de plus ;
  bornée par le périmètre `ROADMAP.md` + `hermes/**` et par le fait qu'aucun
  workflow n'exécute ce que Hermes écrit.
- Les appels headless en CI coûtent de l'argent réel ; plafonnés par
  `--max-budget-usd` (natif, coupe avant la dépense) **et** par le plafond
  mensuel `harness/pipeline/ci_budget_guard.py` (post-hoc, trace) — les deux,
  conformément à l'arbitrage n°2.

### Risks
- **Hermes déborde de son périmètre** → le périmètre est écrit dans
  `hermes/README.md` ; toute PR Hermes touchant autre chose que
  `ROADMAP.md`/`hermes/**` est refusée en revue (et jamais auto-fusionnée :
  ces chemins ne sont pas dans l'allowlist du merge-bot).
- **La critique Cursor devient du bruit** → `architecture/review-guidelines.md`
  impose sévérité + preuve citée par constat ; un constat sans preuve
  citable est à ignorer, par contrat.
- **Un secret manquant fait croire que la boucle tourne** → chaque workflow
  écrit une dérogation visible (`::warning::`) quand un secret manque ;
  le mode `full_auto` reste soumis au garde-fou
  `harness/pipeline/full_auto_mode_guard.py`.
