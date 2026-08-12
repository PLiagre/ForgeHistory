---
review_of: CURSOR-cdc683f-hermes-workflow-quatre-acteurs
reviewer: claude-code
target_commit: cdc683f1d1fb581a9bcb50b1bfa816134c12b82c
reviewed_at: 2026-08-12T10:15:00Z
---

# Contre-audit de CURSOR-cdc683f-hermes-workflow-quatre-acteurs

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `cdc683f1d1fb581a9bcb50b1bfa816134c12b82c` — existe,
  confirmé :
  ```
  $ git log --oneline -1 cdc683f1d1fb581a9bcb50b1bfa816134c12b82c
  cdc683f Merge pull request #24 from PLiagre/forge/workflow-quatre-acteurs-977d
  ```
- Ancêtre de `master` actuel : oui (`git merge-base --is-ancestor cdc683f HEAD`
  → exit 0).
- Nuance sur « Fraîcheur : CURRENT » — vrai **au moment de la création de
  l'audit** (2026-08-12T09:37:46Z). Depuis, l'audit lui-même a été committé
  (`4921f1d`) et fusionné (PR #25, `beb57b5`), donc `master` a avancé
  d'un commit. `git diff cdc683f..HEAD -- .` ne montre qu'un seul fichier
  ajouté : l'audit lui-même. Aucune dérive de fond entre le commit audité et
  l'état actuel du dépôt.
- Mesures rejouées :
  - `git branch -a --contains cdc683f` → confirme `master` et
    `origin/master`. CONFIRMED.
  - `grep -n "TODO(operator" .github/workflows/pipeline-{audit,challenge,forge-run}.yml`
    → aucune sortie, comme annoncé. CONFIRMED.
  - `grep "^mode:" harness/pipeline/config.yaml harness/pipeline/auto_policy.yaml`
    → `mode: full_auto` dans les deux. CONFIRMED.
  - `gh run list --commit cdc683f ...` → effectivement inaccessible depuis ce
    runner (erreur d'auth `GH_TOKEN`), cohérent avec le « 403 » annoncé par
    l'audit — l'audit ne prétend pas avoir vu la sortie réelle, il le dit
    explicitement. CONFIRMED (honnêteté de la limitation).
  - `python3 -m pytest harness/tests/ -q` → 305 passed, 16 skipped, 0 échec.
    Cohérent avec « CI verte » (mesuré localement, pas via `gh`).

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | P0 — Auth abonnement (`CODEX_AUTH_JSON`) non testée en CI, plantera si secret mal formé | CONFIRMED | Le step `Bootstrap Codex subscription auth (auth.json)` cité (lignes 126-142 de l'audit) est reproduit à l'identique dans `.github/workflows/pipeline-forge-run.yml` au commit `cdc683f` (`git show cdc683f:.github/workflows/pipeline-forge-run.yml`). Aucun job CI ne valide le format du secret avant `codex login status`. Le constat et sa recommandation tiennent. |
| 2 | P1 — Hermes cumule « propose » et « relit la roadmap qu'il écrit », aucun contre-pouvoir | PARTIAL | Le tableau ADR-0010 (lignes 30-36) confirme qu'Hermes écrit seul `ROADMAP.md`/`hermes/**` et qu'aucun acteur du harnais (Claude/Codex/Cursor) n'a mandat de relecture automatisée — ce fragment est vrai. Mais `hermes/README.md` (dernier paragraphe, non cité par l'audit) dit explicitement : « une PR Hermes est toujours relue par le propriétaire (ou son délégué) avant fusion. » Il existe donc un contre-pouvoir documenté (revue humaine obligatoire), même s'il n'est pas un acteur agent distinct au sens strict de la séparation producteur/juge du harnais. L'audit a raison sur l'absence d'un *acteur agent* réviseur, mais surstate en implicite « aucun autre acteur ne le signalera » — faux, le propriétaire le signale, par construction du contrat. |
| 3 | P1 — Guide de critique (`review-guidelines.md`) non synchronisé, `CURSOR-6231186-execution-budgets.md` cité comme exemple sans « sévérités P0-P3 explicites » | REFUTED (sur la preuve citée) | `grep -n "P0\|P1\|P2\|P3" architecture/inbox/CURSOR-6231186-execution-budgets.md` montre au contraire des sévérités P1/P2 explicites tout du long (constats 1-5, tableau de risques lignes 146-150). La preuve citée par l'audit à l'appui de ce point est factuellement fausse. Le souci général de version du guide (un futur audit rouvert doit-il appliquer le guide rétroactivement ?) reste concevable, mais aucune preuve locale ne le soutient — sans nouvelle preuve, ce point ne tient pas. |
| 4 | P2 — `ROADMAP.md`/`hermes/**` hors `auto_merge_allowlist`, « n'est écrit nulle part dans le contrat `hermes/README.md` » | REFUTED | `harness/pipeline/config.yaml` lignes 52-55 confirme l'absence de ces chemins dans l'allowlist (exact, l'audit cite bien le fichier réel). Mais l'affirmation « n'est écrit nulle part » est fausse : `hermes/README.md`, dernier paragraphe, dit mot pour mot « Ces chemins ne figurent pas dans l'allowlist du merge-bot : une PR Hermes est toujours relue par le propriétaire (ou son délégué) avant fusion. » C'est exactement la clarification que l'audit recommande d'ajouter — elle existe déjà dans le commit audité. Le brief 2 proposé par l'audit (section 7) est donc en grande partie déjà satisfait par le texte existant. |
| 5 | P2 — Pas de smoke-test réel de la CLI Codex après install (`claude --version`/`codex --version` insuffisant) | PARTIAL | Le constat de fond est vrai : `git show cdc683f:.github/workflows/pipeline-forge-run.yml` confirme que le step « Install Claude Code and Codex CLIs » ne fait que `--version`, pas d'appel fonctionnel. Mais la preuve citée par l'audit (bloc YAML avec `npm install -g @anthropics/claude-cli` et `npm install -g codex-cli`) ne correspond pas au fichier réel, qui contient `npm install -g @anthropic-ai/claude-code @openai/codex` (vérifié à la fois sur `HEAD` et directement sur le commit `cdc683f`). Les noms de paquets cités sont incorrects/inventés ; le point de fond survit, la preuve citée ne survit pas telle quelle. |
| 6 | P3 — Sources externes du guide de critique toutes antérieures à mars 2026, risque de péremption | NEEDS_OWNER | Vérifiable localement uniquement pour les dates citées dans `architecture/review-guidelines.md` (lignes 65-71) : exact, S1-S5 sont bien datées 2026-08-12/2026-03-03. Impossible de vérifier ici le contenu réel des URLs (pas d'accès web autorisé dans cette session). La question « faut-il planifier un re-sourçage T4 2026 » est un arbitrage de calendrier/priorité, pas un fait technique — à trancher par le propriétaire. |
| 7 | Sources externes S1-S3 (état de l'art 2026, section 4) | NEEDS_OWNER | Non vérifiables depuis cet environnement (permission WebFetch refusée dans cette session). Ni confirmées ni réfutées — à ne pas invoquer comme preuve engageante tant qu'elles n'ont pas été rejouées par quelqu'un ayant accès web. |
| 8 | « Deux forces » — direction unique (ROADMAP.md) + fin des stubs d'invocation | CONFIRMED | `grep TODO(operator` vide sur les trois workflows (voir §1). `ROADMAP.md` existe, frontmatter de propriété Hermes présent. |
| 9 | Section 8 — aucun brief ouvert ne fait doublon avec les 3 briefs proposés | CONFIRMED (avec réserve sur le brief 2) | `ls harness/queue/briefs/` reproduit exactement la liste citée (10 briefs hors fixtures). Aucun brief existant ne couvre la validation de secrets (brief 1) ni le versionnement du guide (brief 3). Le brief 2 (relecture ROADMAP.md) reste utile pour formaliser une relecture par un *acteur agent*, mais son urgence est amoindrie par le point 4 ci-dessus : une relecture humaine est déjà contractuellement obligatoire. |
| 10 | Commit `0b4ac9f` — tests `test_mode_guard.py` « inversés consciemment » | CONFIRMED | `git show 0b4ac9f -- harness/tests/test_mode_guard.py` montre l'inversion documentée en toutes lettres dans le diff et le message de commit. `python3 -m pytest harness/tests/test_mode_guard.py -q` → 17 passed. |
| 11 | `harness/pipeline/config.yaml` — `cursor_review_on_pr: true` ajouté par ce commit | CONFIRMED | `git diff 0a8b022 cdc683f -- harness/pipeline/config.yaml` montre la ligne ajoutée. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **Sources externes (constats P3 et section 4)** : cette relecture n'a pas pu
  rejouer les URLs citées (pas d'accès web autorisé dans cette session). Si
  le propriétaire veut s'appuyer sur ces sources pour trancher une priorité,
  elles doivent être revérifiées par un acteur ayant accès web avant d'être
  citées comme preuve engageante.
- **Brief 2 proposé (relecture ROADMAP.md par un acteur distinct)** : sa
  prémisse — « aucun contre-pouvoir n'existe » — est partiellement fausse
  (revue humaine déjà obligatoire, documentée dans `hermes/README.md`). Le
  propriétaire doit trancher si une relecture *agent* (Claude ou Cursor)
  ajoute une valeur au-delà de la revue humaine déjà en place, ou si le
  brief doit être réduit à une simple clarification documentaire (ADR-0010
  amendé pour citer explicitement `hermes/README.md`).

## 4. Synthèse

Ce qui tient : le constat P0 (auth abonnement Codex non testée en CI) est
solide et bien prouvé — c'est le point le plus actionnable de l'audit. Les
« deux forces », la provenance du commit, et les commandes rejouées sont
fidèles à l'état réel du dépôt. La vérification de non-doublon des briefs
(section 8) est correcte.

Ce qui tombe : deux des quatre constats majeurs (P1 « Hermes cumule
propose+relit » et P2 « ROADMAP.md hors allowlist non documenté ») citent
des lacunes documentaires qui, en réalité, existent déjà dans
`hermes/README.md` — un fichier introduit par le même commit audité, non
cité par l'audit. Le troisième constat P1 (guide de critique non
synchronisé) s'appuie sur une preuve fausse (absence de sévérités P0-P3 dans
`CURSOR-6231186`, alors qu'elles y sont). Le constat P2 sur le smoke-test
Codex cite un extrait YAML avec des noms de paquets qui ne correspondent pas
au fichier réel, bien que le point de fond (pas de smoke-test fonctionnel)
survive à la vérification.

Recommandation de traitement : retenir le constat P0 (brief 1, validation
des secrets en CI) tel quel. Réduire le brief 2 à une clarification légère
d'ADR-0010 plutôt qu'un nouveau mécanisme de relecture agent (la revue
humaine existe déjà). Le brief 3 (versionnement du guide de critique) reste
un choix de confort documentaire, pas une urgence — sa preuve d'appui
principale ne tient pas. Le point P2 « smoke-test Codex CLI » (non converti
en brief par l'audit lui-même) reste valable si corrigé avec les vrais noms
de paquets.
