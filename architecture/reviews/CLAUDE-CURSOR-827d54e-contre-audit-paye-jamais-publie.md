---
review_of: CURSOR-827d54e-contre-audit-paye-jamais-publie
reviewer: claude-code
target_commit: 827d54ec2b0ee3b49d1b1a1992d64137759f32a6
reviewed_at: 2026-08-13T11:08:04Z
---

# Contre-audit de CURSOR-827d54e-contre-audit-paye-jamais-publie

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- Le commit `827d54ec2b0ee3b49d1b1a1992d64137759f32a6` existe bien dans
  l'historique : `git log --oneline -1 827d54e` le trouve, parent 1
  `e034f07`, parent 2 `4c45718`, message « Merge pull request #65 »
  (confirme le § 0 de l'audit).
- PR #65 re-vérifiée via l'API publique GitHub non authentifiée (pas de
  `GH_TOKEN` dans cet environnement de revue, mais `api.github.com` est
  joignable en lecture anonyme) : `additions=843, changed_files=11,
  merged_at=2026-08-13T10:47:51Z, merged_by=PLiagre` — identique au § 6.2
  de l'audit.
- Mesures rejouées dans un git-worktree posé sur `827d54e`
  (`git worktree add /tmp/wt-827d54e 827d54ec2...`) pour garantir l'état
  exact du commit audité, séparément de `master` qui a continué d'avancer
  pendant cette revue (confirmé : `git fetch origin master` a fait passer
  `HEAD` local de `ae76a15` à `da53650` en cours de revue — la boucle
  tourne en continu, exactement le phénomène que l'audit décrit).
- `pytest` n'était pas installé dans cet environnement (`.venv/bin/python`
  du dépôt n'existe pas ici) ; installé localement (`pip install pytest`)
  pour rejouer les suites sans dépendre du chiffre annoncé.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1a | P0 — le contre-audit de `4c45718` est écrit à 09:03:43 UTC, `gh pr create` est refusé avec `Resource not accessible by personal access token (createPullRequest)`, l'étape émet `::warning::` et le job reste vert, sept branches `forge-bot/review-*` sont poussées, zéro PR ouverte au moment de la mesure | **CONFIRMED** | Rejoué dans le worktree : `.github/workflows/pipeline-challenge.yml:197-201` cite `gh pr create ... || echo "::warning::gh pr create refused (repository setting or permissions) -- branch $branch is pushed; open the PR manually."` — texte identique à celui cité par l'audit. `gh run view 31684301091 --json jobs` (API publique, sans auth requise pour la liste des jobs) : job `invoke-claude-challenger` = `success`, ses 12 étapes = `success`, y compris l'étape 12 « Publish the review as a pull request » — confirme que l'échec ne fait pas tomber le job. `git ls-remote origin 'refs/heads/forge-bot/review-*'` retrouve encore aujourd'hui 5 des 7 branches citées (les 2 manquantes ont été fusionnées entretemps, voir 1c). PR #65 confirmée mergée à 10:47:51Z par l'API (§1). |
| 1b | P0 — l'échec est un échec de droits du PAT `FORGE_BOT_PAT`, « pas un réglage de dépôt » | **PARTIAL** | Le message d'erreur cité (« personal access token ») et le fait que `GH_TOKEN: ${{ secrets.FORGE_BOT_PAT \|\| secrets.GITHUB_TOKEN }}` (ligne 174) alimente l'étape sont cohérents avec l'hypothèse PAT. Mais `HANDOFF.md` — le document de statut faisant autorité — attribue **la même panne récurrente** au réglage de dépôt « Allow GitHub Actions to create and approve pull requests », et ce à trois endroits distincts (lignes 93-94, 289-292, 438), avec la recommandation explicite d'aller l'activer. L'audit lui-même liste cette ambiguïté dans ses Limites (§5 : « une restriction équivalente au niveau du dépôt produirait le même message ») — donc **il ne pouvait pas trancher**, et présenter « pas un réglage de dépôt » comme un fait établi en constat 1a surinterprète sa propre incertitude déclarée. Les deux causes ne sont pas mutuellement exclusives et je n'ai pas accès aux réglages du dépôt pour trancher non plus. |
| 1c | P0 — implication : « aucune [relecture] n'est arrivée dans le tronc » / la contradiction prévue par ADR-0010 « n'existe pas en pratique » | **PARTIAL** | Vrai au moment mesuré par l'audit (avant 11:05:00Z). Faux comme généralisation : au moment de cette revue, les deux relectures les plus pertinentes pour cet audit — celle de `16ff5ac` et celle de `4c45718` — ont bien atteint `master` via des PR ouvertes **à la main** (#71 fusionnée 11:00:01Z, #73 fusionnée 11:01:37Z, toutes deux **avant** que cet audit `827d54e` ne soit lui-même proposé à 11:05:00Z) : `git show origin/master:architecture/audit-ledger.jsonl` porte bien `AUDIT_CHALLENGED` puis `AUDIT_APPROVED` pour les deux. Ce patron (branche bloquée → PR ouverte à la main par l'orchestrateur → fusion) est déjà documenté trois fois dans `HANDOFF.md`, donc ce n'est pas une découverte : c'est un défaut connu, suivi, avec une compensation manuelle qui fonctionne à chaque fois observée dans cette session. Le vrai problème mécanique — l'étape ne fait jamais échouer le job — reste réel et non corrigé, mais « personne ne lit » / « la chaîne... a produit et payé sa relecture, puis l'a perdue » est une formulation plus forte que ce qui est mesurable : elle a été retrouvée, pas perdue. |
| 2a | P1 — comptage figé au registre pour `a4de4bb` : `{CONFIRMED:14, REFUTED:4, PARTIAL:6, NEEDS_OWNER:4}` contre 9 CONFIRMED + 1 PARTIAL réels (10 points) | **CONFIRMED** | Rejoué avec le code du dépôt dans le worktree : `audit_review.parse_verdicts()` sur `architecture/reviews/CLAUDE-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois.md` rend exactement `{'CONFIRMED': 14, 'REFUTED': 4, 'PARTIAL': 6, 'NEEDS_OWNER': 4}` — identique à la ligne `AUDIT_CHALLENGED` du ledger. Le tableau réel du fichier (`sed -n` sur les lignes `\| # \|`) compte 10 lignes, 9 `**CONFIRMED**`, 1 `**PARTIAL**`. Cause confirmée : `parse_verdicts` compte tout le texte, y compris la ligne de légende « Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER. » présente en tête de chaque revue — et ce même bug est déjà décrit, pour un autre audit (`3b47ffe`), au point 9 de la revue de `a4de4bb` elle-même (`CONFIRMED`, avec citation de ligne de code). Root cause cohérente sur les deux occurrences. |
| 2b | P1 — 8 événements `AUDIT_IMPLEMENTED`/`AUDIT_VERIFIED` sans aucun champ de preuve, sur 47 lignes de registre | **CONFIRMED** | Script rejoué sur `architecture/audit-ledger.jsonl` au commit audité : 47 lignes totales, exactement 8 événements `AUDIT_IMPLEMENTED`/`AUDIT_VERIFIED` ne portent que `{timestamp, audit_id, event, actor}` (aucun `sha`/`run`/`verdict_path`/`evidence`), sur `CURSOR-FIXTURE-full-auto-demo`, `CURSOR-5633ee7-...`, `CURSOR-e9a6f4c-...`, `CURSOR-3b47ffe-...` (×2 chacun) — identique à l'audit. |
| 3 | P1 — briefs 013 et 014 sont des graines vides (marqueur « TODO (planificateur) » entre chevrons doubles, 6 occurrences chacun ; `eval-rubric.md` réduit à 1 ligne), `verdict_audit.py` sur 014 rend `REJECT` | **CONFIRMED** | Rejoué au commit exact : `git show 827d54e:.../013.../brief.md \| grep -c "TODO (planificateur)"` → 6 ; idem 014 → 6 ; `eval-rubric.md` des deux → 1 occurrence de TODO chacun. `python3 harness/verdict_audit.py harness/queue/briefs/014-pipeline-contre-audit-porte` dans le worktree → `VERDICT: REJECT`. Déclaration de non-doublon du § 9 (008/013/014 seuls briefs ouverts) également vérifiée : `for d in harness/queue/briefs/*/; do [ -f verdict.md ] ...` ne rend que ces trois répertoires sans `verdict.md`. |
| 4 | P2 — `ci-budget-ledger.jsonl` fait 1 octet au commit fusionné ; mécanisme : `git add architecture/reviews` seul en publication, la ligne de coût reste dans le workspace du runner ; total mesuré 7,2771804 $ | **PARTIAL** | Fait fichier confirmé à l'octet près : `git show 827d54e:harness/pipeline/ci-budget-ledger.jsonl \| wc -c` → 1. Mécanisme confirmé par lecture directe : `pipeline-challenge.yml:194` ne stage que `architecture/reviews`. Le montant total (7,2771804 $) repose sur `total_cost_usd` extrait des logs de run CI ; ces logs (`gh run view --log`) rendent 403 sans authentification GitHub dans cet environnement de revue — je n'ai pas pu re-télécharger les transcripts pour re-sommer le montant. Le fait qualitatif (dépense mesurée puis non committée) est solide ; le chiffre en dollars n'est vérifiable que par quelqu'un disposant d'un `GH_TOKEN`. |
| 5 | P2 — arriéré au commit fusionné : 31 audits en inbox, 16 avec ≥1 ligne de registre, 15 sans, répartition `[PROPOSED:15, CHALLENGED:3, APPROVED:3, CONVERTED:2, ARCHIVED:8]`, 13 revues ; croissance depuis 12/25 (cité de `a600532`, P2-1) | **CONFIRMED** | Tous les chiffres reproduits exactement dans le worktree : `ls architecture/inbox/*.md \| wc -l` → 31 ; script de comparaison inbox↔ledger → 16 avec ligne / 15 sans ; `python3 harness/audits.py list` → `[AUDIT_PROPOSED] (15) [AUDIT_CHALLENGED] (3) [AUDIT_APPROVED] (3) [AUDIT_CONVERTED] (2) [AUDIT_ARCHIVED] (8)` ; `ls architecture/reviews/*.md \| wc -l` → 13. Citation de provenance vérifiée : `architecture/inbox/CURSOR-a600532-fusion-sans-contre-audit.md:291` porte bien « 12 audits sur 25 n'y ont aucune ligne, et l'événement `AUDIT_PROPOSED` n'y apparaît jamais ». |
| 6 | P3 — 54 runs `hermes-observer` en `queued` sur les 100 derniers runs du dépôt, le plus ancien depuis 08:56:46 UTC | **PARTIAL** | L'état des runs GitHub Actions n'est pas versionné avec le commit : impossible de le rejouer rétroactivement à l'instant exact de l'audit. Le mécanisme cité est confirmé par lecture statique : `.github/workflows/hermes-observer.yml` cible bien un runner auto-hébergé (`runs-on: [self-hosted, Windows, X64, hermes-observer]`), ce qui rend plausible une file qui ne se vide jamais si la machine du propriétaire est hors ligne. Mesuré en direct pendant cette revue (bien après le commit audité) : le nombre de runs `hermes-observer` en `queued` est passé à **485** (`GET /actions/workflows/331350437/runs?status=queued` → `total_count: 485`) — la tendance à la croissance que l'audit annonce est donc confirmée et même aggravée, mais je ne peux pas confirmer le chiffre précis « 54 » ni l'horodatage exact « 08:56:46 » propres au commit audité. |
| 7 | P3 — `harness_audit.py` rend 20/24 (2 FAIL) sur un clone propre au commit audité, alors qu'`AGENTS.md:50` annonce 23/24 (1 FAIL connu) ; cause : `run_demo.log` git-ignoré et jamais commité | **CONFIRMED** | Reproduit à l'identique dans le worktree posé sur `827d54e` : `SCORE: 20/24`, `[FAIL] (3 pt) fake_honest_demo_pair: missing: ['run_demo.log ...']` et `[FAIL] (1 pt) no_premature_stub_content: ...`. `AGENTS.md:48-50` cité mot pour mot confirmé (« currently scores 23/24 »). `git check-ignore -v harness/demo/fake_brief_001/run_demo.log` → `.gitignore:7:*.log`. `git log --all --oneline -- .../run_demo.log` → vide, confirme qu'il n'a jamais été commité. |
| 4b (§4 « ce qui tient ») | Fusion propre, aucun conflit résolu à la main | **CONFIRMED** | `git diff --stat e034f07..827d54e` → 11 fichiers, 843 insertions, 0 suppression. `git diff --stat 4c45718..827d54e` → exactement les 5 fichiers cités (4 audits + `hermes/DASHBOARD.md`), 2278 insertions / 17 suppressions. |
| 4c | Portes mécaniques vertes : `314 passed, 16 skipped` (harnais), `25 passed` (`sim/`), `31 audits valides` | **CONFIRMED** | Reproduit à l'identique dans le worktree sur `827d54e` (après installation locale de `pytest`, absente de cet environnement de revue) : `pytest harness/tests/ -q` → `314 passed, 16 skipped` ; `pytest sim/tests/ -q` → `25 passed` ; `python3 harness/audit_schema.py` → `All 31 audit(s) valid.` |
| 4d | Un constat antérieur (P2-2 de `a600532`, « `sim/tests/` hors CI ») est levé : job `sim-tests` existe et est vert | **CONFIRMED** | `.github/workflows/harness-ci.yml` porte bien un job `sim-tests` (ligne 38) distinct de `f0-demo` (ligne 50). Citation P2-2 vérifiée dans `architecture/inbox/CURSOR-a600532-...md:323`. |
| 4e | Le gate refuse bien les graines (014 → `REJECT`) | **CONFIRMED** | Doublon exact du point 3 ci-dessus. |
| 4f | `pipeline-orchestrate` a eu raison de ne pas se déclencher (filtre `architecture/reviews/*.md`, la fusion n'apporte qu'une copie d'archive de revue) | **CONFIRMED** | `.github/workflows/pipeline-orchestrate.yml:28-29` : `paths: - 'architecture/reviews/*.md'`. Le diff de fusion (point 4b) ne touche aucun fichier sous `architecture/reviews/` — cohérent. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **Constat 1, tel que reposé une quatrième fois en P0** : le mécanisme
  sous-jacent (l'étape de publication ne fait jamais échouer le job) est
  réel et non corrigé — mais le point précis « la relecture est perdue et
  la fusion se fait sans contradiction » s'est en fait résolu tout seul
  pour les deux revues les plus proches de cette PR, via récupération
  manuelle, dans l'heure qui a suivi le dépôt de cet audit. Le
  propriétaire sait déjà (`HANDOFF.md` × 3) qu'activer « Allow GitHub
  Actions to create and approve pull requests » réglerait la cause
  probable. Question business, pas technique : ce constat mérite-t-il un
  brief dédié en plus du 014 (comme l'audit le propose lui-même en
  alternative au § 8), ou l'activation du réglage suffit-elle à clore la
  chaîne des quatre P0 identiques ?
- **Les 3 propositions de briefs du § 8** sont explicitement des
  propositions non instruites — c'est au propriétaire de décider s'il les
  ouvre, les fond dans 013/014, ou les écarte, comme l'audit le dit
  lui-même.

## 4. Synthèse

Ce qui tient techniquement, point par point : tous les chiffres
directement rejouables depuis le dépôt (comptages de TODO, ledger,
`parse_verdicts`, scores de gate, suites de tests, diffs de fusion) sont
**reproduits à l'identique** — je n'ai trouvé aucune mesure locale fausse
dans cet audit. C'est un niveau de rigueur inhabituel : chaque chiffre
cité au § 6 a survécu à une reproduction indépendante dans un worktree
séparé posé exactement sur le commit audité.

Ce qui tombe ou se nuance : les points qui dépendent de **l'API GitHub
en direct** (constat 1, constat 6) sont, par construction, des snapshots
non rejouables après coup — l'état des runs et des PR n'est pas
versionné. Pour le constat 1 spécifiquement, l'évolution *pendant cette
revue elle-même* a changé la donne : les deux relectures que l'audit
décrit comme perdues (`16ff5ac`, `4c45718`) sont arrivées sur `master`
quelques minutes avant que cet audit ne soit proposé, via le même
mécanisme de récupération manuelle que `HANDOFF.md` documente déjà trois
fois. Le défaut mécanique (l'étape ne remonte jamais un échec) reste
entier et vaut d'être corrigé — mais le classer une quatrième fois en P0
« la chaîne... l'a perdue » mérite d'être nuancé : elle est retrouvée à
chaque fois observée, avec un délai, pas silencieusement absorbée pour
toujours.

Sur l'attribution causale (PAT vs réglage de dépôt, constat 1b) : l'audit
avait déjà noté cette incertitude dans ses propres Limites, et je n'ai pas
pu la trancher non plus faute d'accès aux réglages GitHub — mais présenter
« pas un réglage de dépôt » comme acquis dans le corps du constat, alors
que le document de statut du dépôt affirme l'inverse depuis plusieurs
sessions, va au-delà de ce que l'audit peut établir seul.

Recommandation de traitement : les constats 2, 3, 5, 7 (et les 5 points de
« ce qui tient ») peuvent être retenus tels quels — véracité technique
solide. Le constat 1 mérite d'être retenu pour son défaut mécanique réel
(l'étape ne peut pas échouer), mais sa formulation en « perte définitive »
devrait être corrigée avant toute conversion en brief, avec le racontage
complet (récupération manuelle intervenue le jour même). Le constat 4 est
retenable pour le fait et le mécanisme, pas pour le chiffre en dollars
(non vérifiable depuis cet environnement). Le constat 6 est retenable pour
la tendance, pas pour le chiffre précis au commit audité.
