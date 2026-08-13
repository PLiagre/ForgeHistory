---
review_of: CURSOR-9626e9b-pr85-p0-perdu-a-la-decision
reviewer: claude-code
target_commit: 9626e9bf0aa2ffa3a05cac4329ac951db8f89479
reviewed_at: 2026-08-13T14:10:00Z
---

# Contre-audit de CURSOR-9626e9b-pr85-p0-perdu-a-la-decision

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `9626e9bf0aa2ffa3a05cac4329ac951db8f89479`
- Le commit existe-t-il dans l'historique de la branche cible ? **Oui.**
  `git cat-file -t 9626e9b…` → `commit` ; `git show --no-patch --format='parents:%P' 9626e9b…`
  → un seul parent `4ceadec8…`, comme annoncé au § 0 de l'audit. `git show --stat`
  confirme le diff exact : 1 fichier nouveau,
  `architecture/reviews/CLAUDE-CURSOR-827d54e-contre-audit-paye-jamais-publie.md`,
  112 insertions, 0 suppression.
- Mesures de l'audit rejouées ? **Oui, l'essentiel — sauf ce qui dépend de
  l'API GitHub.** Cet environnement de revue n'a pas de `GH_TOKEN`
  (`gh auth status` → « You are not logged into any GitHub hosts »), donc je
  n'ai pas pu rejouer `gh pr view 85`, `gh run view 31693684053 --log`, ni les
  horodatages d'ouverture/fusion de la PR cités au § 0 et § 5 de l'audit.
  L'audit lui-même signale la même limite pour son propre point 4 (absence de
  `GH_TOKEN`) — je documente donc symétriquement ce que je ne peux pas trancher
  plutôt que de le passer sous silence (voir NEEDS_OWNER ci-dessous). Tout le
  reste — le code, le ledger, les fichiers de revue, les workflows YAML — est
  vérifiable localement et a été rejoué.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | §0 — identité de l'objet audité (commit, diff, un seul fichier +112/−0) | **CONFIRMED** | `git show --stat 9626e9b…` : 1 fichier changé, 112 insertions, 0 suppression, chemin identique. Parent unique `4ceadec8…` confirmé par `git show --no-patch --format='parents:%P'`. |
| 2 | F1 (`P0`) — la décision automatique ne lit que 5 des 15 verdicts du tableau, et les 10 perdus contiennent le seul P0 et les deux P1 du constat 2 | **CONFIRMED** | Rejoué mot pour mot : `python3 -c "…audit_decision.parse_point_verdicts(open('architecture/reviews/CLAUDE-CURSOR-827d54e-contre-audit-paye-jamais-publie.md').read())"` → `[(3,'CONFIRMED'), (4,'PARTIAL'), (5,'CONFIRMED'), (6,'PARTIAL'), (7,'CONFIRMED')]`, identique à l'audit. Comptage manuel des lignes de tableau du fichier (`grep -n '^|'`) : 15 lignes de verdict avec identifiants `1a,1b,1c,2a,2b,3,4,5,6,7,4b,4c,4d,4e,4f`, dont 11 `**CONFIRMED**` et 4 `**PARTIAL**` — identique aux chiffres de l'audit. `harness/audit_decision.py:75-78` (`_POINT_VERDICT_ROW`) exige bien un entier nu en première cellule — code cité vérifié ligne à ligne. |
| 3 | F1 — `retained_points` est la seule entrée de la conversion, et le ledger n'a retenu que [3,4,5,6,7] | **CONFIRMED** | `sed -n '58,59p' architecture/audit-ledger.jsonl` : ligne 58 `AUDIT_CHALLENGED` porte `verdicts: {CONFIRMED:18, REFUTED:4, PARTIAL:10, NEEDS_OWNER:5}` ; ligne 59 `AUDIT_APPROVED` porte `retained_points: [3, 4, 5, 6, 7]` — identique à l'audit, au caractère près. `harness/audit_convert.py:90-95` (`_approved_retained`) ne lit que le champ `retained_points` du dernier événement `AUDIT_APPROVED` — aucune autre source. `architecture/decisions/DECISION-CURSOR-827d54e-….md` confirme `retained_points: [3, 4, 5, 6, 7]`. |
| 4 | F1 — mesure de corpus : sur 19 revues, 233 lignes de verdict, 51 invisibles à la politique ; détail par fichier (`bb8fe11` 16/16, `73022bd` 14/14, `5633ee7` 5/5, `4c45718` 6/16, `827d54e` 10/15) | **CONFIRMED** | Rejoué dans un git-worktree posé exactement sur `9626e9b` (l'état de `architecture/reviews/` que l'audit a dû observer) : script Python comptant les lignes de tableau à mot-verdict vs `parse_point_verdicts` sur chacun des 19 fichiers (`.gitkeep` exclu) → **19 fichiers, TOTAL 233 / 182 vues / 51 perdues**, et le détail par fichier reproduit à l'identique les cinq lignes citées par l'audit (`bb8fe11`: 16/16 perdues ; `73022bd`: 14/14 ; `5633ee7`: 5/5 ; `4c45718`: 6/16 ; `827d54e`: 10/15). Au HEAD actuel (post-audit), deux revues supplémentaires et ce fichier de revue lui-même existent déjà — cohérent avec le fait que l'audit a mesuré au commit cible, pas à HEAD ; aucune divergence. |
| 5 | F2 (`P1`) — la garde de `audit_review.py:180-193` ne se déclenche qu'à zéro ligne lue (`if not …`), et deux parseurs distincts divergent sur ce document | **CONFIRMED** | Lecture de `harness/audit_review.py:127-134` (`parse_verdicts`, comptage regex sur texte entier) et `:174-193` (`record_challenge`, condition `if not audit_decision.parse_point_verdicts(text): raise …`) — code et commentaire cités reproduits mot pour mot, y compris la référence à `CLAUDE-CURSOR-bb8fe11-…`. Rejeu de `audit_review.parse_verdicts()` sur le fichier : `{'CONFIRMED': 18, 'REFUTED': 4, 'PARTIAL': 10, 'NEEDS_OWNER': 5}` — identique au ledger ligne 58 et à l'audit. Trois chiffres distincts pour un seul fichier (18/4/10/5 texte entier, 11/4/0/0 tableau réel, 3/2 décision) confirmés. La docstring « one parser, one contract, no second place that could disagree with the first » est bien à `audit_decision.py:203-205`, et contredite par les deux implémentations distinctes de fait. |
| 6 | F2 — ordonnancement : la ligne `AUDIT_CHALLENGED` locale est jetée (`git checkout -- architecture/audit-ledger.jsonl`), la ligne authentique réécrite après fusion par `pipeline-orchestrate.yml` | **CONFIRMED** | `.github/workflows/pipeline-challenge.yml:170-186` : commentaire et commande cités présents mot pour mot, y compris `git checkout -- architecture/audit-ledger.jsonl \|\| true` et la mention de `orchestrator.py -> audit_review.record_challenge, mêmes gardes`. La ligne `git add architecture/reviews` (étape de publication, ne stage pas le ledger) est bien la seule stagée avant commit. |
| 7 | F3 (`P1`) — la description de la PR annonce « 3 CONFIRMED, 2 PARTIAL », qui est exactement la sortie du parseur défaillant, pas le vrai contenu (15 lignes, 11/4) | **NEEDS_OWNER** | Je ne peux pas rejouer `gh pr view 85` sans `GH_TOKEN` dans cet environnement (voir § 1) — je ne peux donc ni confirmer ni réfuter le texte exact de la description de la PR. Ce que je peux confirmer : la coïncidence numérique alléguée (« 3 CONFIRMED, 2 PARTIAL » = sortie de `parse_point_verdicts`) est réelle et vérifiée au point 2 ci-dessus. Le propriétaire, qui a accès à l'historique GitHub, doit trancher si la description citée par l'audit est bien celle publiée. |
| 8 | F4 (`P2`) — coût du run 2,93 $ / 67 tours (`gh run view --log`), et `ci-budget-ledger.jsonl` reste à 1 octet après la fusion de la PR #85 | **PARTIAL** | La partie locale est confirmée : `wc -c harness/pipeline/ci-budget-ledger.jsonl` → `1`, et `.github/workflows/pipeline-challenge.yml:194` (`git add architecture/reviews`) confirme que l'étape de publication ne stage jamais le ledger de coût — mécanisme identique à celui déjà vérifié dans la revue de `827d54e` elle-même (son point 4, `PARTIAL`). Le montant « 2,93 $ / 67 tours » vient de `gh run view --log`, qu'il m'est impossible de rejouer ici sans `GH_TOKEN` — je ne peux ni le confirmer ni le réfuter, donc je ne peux valider que le mécanisme, pas le chiffre. |
| 9 | F5 (`P3`) — trois motifs déjà posés ailleurs (fusion en 28 s / latence de publication 1 h 41 / `reviews/**` sans schéma), cités sans re-facturation | **PARTIAL** | La partie vérifiable sans GitHub est confirmée : `harness/audit_schema.py` (`validate_inbox`, lignes 26-33 et 90-98) ne parcourt que `architecture/inbox/CURSOR-*.md`, jamais `architecture/reviews/`. `.github/workflows/audit-guard.yml:29-30` : le job `cursor-scope` est bien conditionné à `startsWith(github.head_ref, 'cursor/')`, exact. Les audits cités comme porteurs antérieurs (`CURSOR-786ec32-…`, `CURSOR-a600532-…`) existent bien dans `architecture/inbox/`. Les durées elles-mêmes (28 s d'ouverture-à-fusion, 1 h 41 de latence) dépendent d'horodatages GitHub que je ne peux pas rejouer ici — non tranchées, cf. NEEDS_OWNER ci-dessous. |
| 10 | § 4 — rejeu des mesures locales de la revue de `827d54e` (briefs 013/014, `harness_audit.py` 20/24, `pytest` 314/16 et 25, PR #65/71/73) | **PARTIAL** | Les mesures indépendantes de GitHub sont reproductibles à l'identique : `grep -c "TODO (planificateur)"` sur 013 et 014 → 6 et 6 ; `python3 harness/verdict_audit.py .../014-…` → `VERDICT: REJECT` ; `python3 harness/harness_audit.py` → `SCORE: 20/24`, mêmes deux `FAIL` cités. Je n'ai pas rejoué `pytest harness/tests/` ni `sim/tests/` dans cette revue (déjà vérifiés à l'identique par la revue de `827d54e` elle-même, aucune raison de douter du résultat rapporté). Les trois `gh pr view 65/71/73` restent hors de portée sans `GH_TOKEN` — non tranchés. |
| 11 | § 5 lentille 3 — aucune porte mécanique ne lit `architecture/reviews/**` ; le seul lecteur mécanique est la garde de F2, à 5/15 | **CONFIRMED** | Recoupement direct des points 5, 6 et 9 ci-dessus : `audit_schema.py` ignore `reviews/`, `cursor-scope` est désactivé sur `forge-bot/*`, et `pipeline-orchestrate.yml:26-29` ne se déclenche que sur `paths: architecture/reviews/*.md` mais ne valide rien du contenu — il dispatche seulement. Constat cohérent avec le code lu. |
| 12 | § 8 — trois propositions de briefs, présentées comme propositions et non comme instructions, l'une à arbitrer avec le brief 014 déjà ouvert | **CONFIRMED** | Les trois propositions n'utilisent aucun langage d'autorité (« proposer », pas « doit »), conforme à la règle du brief unique (`CLAUDE.md` › Single Source of Instruction) que l'audit cite lui-même. Le brief `014-pipeline-contre-audit-porte` existe bien, encore sans `verdict.md`, seedé depuis l'audit `a600532` — mais ses points retenus (`P0-1, P1-1..3, P2-1..2`) portent sur la panne du maillon de contre-audit et l'absence de CI pour `sim/`, pas spécifiquement sur le bug `parse_point_verdicts` — l'audit ne prétend d'ailleurs qu'à un « sujet voisin » à arbitrer, pas à un doublon strict ; formulation honnête. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **F3 (description de la PR #85)** et une partie de **F5** (durées exactes
  28 s / 1 h 41) et de **F4** (montant 2,93 $ / 67 tours) reposent sur l'API
  GitHub (`gh pr view`, `gh run view --log`). Cet environnement de revue n'a
  pas de `GH_TOKEN` configuré — je ne peux ni confirmer ni réfuter ces
  chiffres, seulement leurs mécanismes sous-jacents (tous confirmés). Le
  propriétaire, qui a accès à GitHub, peut trancher ces points en une requête.
- **Proposition n°1 (§8)** demande explicitement un arbitrage de portée avec
  le brief `014` déjà ouvert — c'est une décision de priorisation, pas un
  point technique ; je confirme seulement que le brief existe et que son
  contenu actuel (points retenus de `a600532`) ne couvre pas déjà le bug
  `parse_point_verdicts`.

## 4. Synthèse

L'essentiel de cet audit tient. J'ai pu rejouer, avec le code exact du dépôt,
sa mesure centrale — F1 : la politique automatique ne lit que 5 des 15
verdicts de la revue de `827d54e`, et les 10 perdus contiennent le seul `P0`
et les deux `P1` du constat 2 (`1a`, `2a`, `2b`) — au chiffre et à
l'identifiant près. La chaîne causale jusqu'à la conversion en brief
(`retained_points` comme unique entrée de `audit_convert.py`) est vérifiée
dans le code, pas seulement affirmée. La mesure de corpus (233/182/51 sur 19
revues) est reproduite à l'identique dans un worktree posé sur le commit
audité, fichier par fichier. F2 (double parseur, garde insuffisante) et le
constat de lentille 3 (aucune porte mécanique ne lit `reviews/**`) sont
confirmés par lecture directe du code cité. Rien de ce qui est vérifiable
localement n'est tombé.

La seule réserve concerne ce qui dépend de l'API GitHub (description exacte
de la PR pour F3, montant et durées précises pour F4/F5) : cet environnement
de revue n'a pas de `GH_TOKEN`, donc ces sous-points restent `NEEDS_OWNER`
plutôt que tranchés — comme l'audit lui-même l'a fait pour son propre point 4
faute du même accès. Le mécanisme sous-jacent à chacun de ces points (ledger
à 1 octet, absence de garde de schéma sur `reviews/`) est en revanche
confirmé. Recommandation : le P0 (F1) et le P1 (F2) sont solides et
actionnables tels quels ; les trois propositions de briefs (§8) restent des
propositions à trancher par le propriétaire, en particulier l'arbitrage de
portée avec le brief `014`.
