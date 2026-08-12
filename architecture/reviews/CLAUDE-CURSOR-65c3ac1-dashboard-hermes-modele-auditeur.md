---
review_of: CURSOR-65c3ac1-dashboard-hermes-modele-auditeur
reviewer: claude-code
target_commit: 65c3ac1c85c24cc61265c7f9ec4989cc67a0b4f9
reviewed_at: 2026-08-12T00:00:00Z
---

# Contre-audit de CURSOR-65c3ac1-dashboard-hermes-modele-auditeur

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : 65c3ac1c85c24cc61265c7f9ec4989cc67a0b4f9
- Le commit existe-t-il dans l'historique de la branche cible ? **Oui.**
  `git cat-file -t 65c3ac1c85c24cc61265c7f9ec4989cc67a0b4f9` → `commit` ;
  `git log --oneline -1 65c3ac1` → `Merge pull request #27 from
  PLiagre/forge/hermes-dashboard-modele-auditeur-977d` ; parents
  `9ee112d3581472867ed41388da3e3c3e057c0a7c` et
  `73022bdab6d2fff7c4d08812c281bcc56172dcc8`, identiques à ceux annoncés en
  §2.1 de l'audit. `git merge-base --is-ancestor 65c3ac1... HEAD` confirme
  qu'il est bien ancêtre de la tête actuelle. Note honnête : au moment de
  cette contre-critique, `master` a avancé (`ba6abc3`, qui inclut d'ailleurs
  déjà cet audit lui-même, mergé via PR #29) — la fraîcheur « CURRENT »
  annoncée par l'audit était vraie **au moment de sa rédaction**
  (2026-08-12T11:55:00Z), pas une prétention de fraîcheur perpétuelle. Aucun
  fichier cité par l'audit (`pipeline-audit.yml`, `hermes/dashboard.py`,
  `hermes-dashboard.yml`, `ci_budget_guard.py`) n'a changé entre 65c3ac1 et
  HEAD (`git log 65c3ac1..HEAD -- <ces fichiers>` → vide), donc toutes les
  reproductions ci-dessous, faites sur l'arbre de travail actuel, portent
  bien sur l'état exact audité.
- Mesures de l'audit rejouées ? **Oui, quasi intégralement** (voir §2). Seule
  exception : §5.2 (liste des 7 runs CI) et §5.3 (log du run `31593029671`)
  n'ont pas pu être rejoués — pas d'authentification `gh` dans cet
  environnement de revue (`gh auth status` → non connecté). Signalé
  explicitement, pas passé sous silence.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | §3.1 (P0) — `pipeline-audit.yml` n'appelle ni `ci_budget_guard.py precheck` ni `record`, contrairement aux deux autres workflows ; le ledger fait 1 octet (vide) ; le tableau de bord affiche « 0.0 USD sur 0 invocation(s) ». | **CONFIRMED** | `git show 65c3ac1:.github/workflows/pipeline-forge-run.yml \| grep ci_budget_guard` et `.../pipeline-challenge.yml` → 2 occurrences chacun (precheck + record) ; `git show 65c3ac1:.github/workflows/pipeline-audit.yml \| grep budget` → aucune. `wc -c harness/pipeline/ci-budget-ledger.jsonl` → `1`, `od -c` → `\n` seul. `python3 -c "...dashboard.generer(Path('.'))..."` (module inchangé depuis 65c3ac1) → ligne reproduite au caractère près : « 0.0 USD mesurés sur 0 invocation(s), plafond 200 USD. » |
| 2 | §3.1 — citation du brief 009, lignes 375-378 (Non-Goal excluant `pipeline-audit.yml`) et lignes 210-212 (raison d'être du plafond). | **CONFIRMED** | `grep -n "Lot 009c wires" harness/queue/briefs/009-full-auto-agent-invocation/brief.md` → ligne 375, texte identique au mot près jusqu'à la ligne 378. `grep -n "every merge touching"` → ligne 210, texte identique. |
| 3 | §3.2 (P1) — le motif anti-boucle exclut `hermes/` sans distinction document/code, uniquement sur le déclencheur `push` (pas `pull_request`). | **CONFIRMED** | `git show 65c3ac1:.github/workflows/pipeline-audit.yml` — le motif `grep -vE '^(architecture/(inbox\|reviews\|decisions\|archive)/\|architecture/audit-ledger\.jsonl$\|hermes/)'` est cité mot pour mot (ligne 67) sous une étape portant `if: github.event_name == 'push'` (ligne 59). Motif rejoué sur les 5 cas types de l'audit (§5.4) → sortie identique caractère près : SKIP pour `hermes/dashboard.py`, `hermes/DASHBOARD.md`, `architecture/inbox/CURSOR-x.md` ; AUDIT pour `sim/engine.py`, `architecture/agents/cursor-auditor.md`. |
| 4 | §3.3 (P1) — l'en-tête horodaté à la minute empêche le garde « rien à pousser » de jamais se déclencher ; cron `17 */6 * * *` → ≥4 commits bot/jour directement sur `master`. | **CONFIRMED** | `grep -n "strftime" hermes/dashboard.py` → ligne 208, motif identique. `grep -n "git diff --quiet" .github/workflows/hermes-dashboard.yml` → ligne 94. Rejeu Python (`generer(now=12h00)` vs `generer(now=18h00)`) → `identiques ? False`, 1 seule ligne diffère, exactement la ligne d'horodatage — reproduction bit-à-bit du §5.7 de l'audit. `grep cron` → `17 */6 * * *`, soit 4 exécutions/jour, cohérent avec « quatre fois par jour au minimum ». |
| 5 | §3.4a (P2) — le filtre `grep -i opus \| grep -i thinking` est inerte car la liste API n'a jamais de variante « thinking » dans `id`/`aliases`. | **PARTIAL** | Le code est confirmé identique (`git show 65c3ac1:.github/workflows/pipeline-audit.yml` ligne 141). Mais je n'ai **pas pu rejouer l'appel réel à `GET /v1/models`** ni le log du run `31593029671` (pas d'accès `gh`/réseau Cursor dans cet environnement) — je m'appuie donc sur l'extrait de log reproduit dans l'audit (§5.3) et sur sa lecture de la doc Cursor (S8), non re-vérifiée indépendamment ici. Le raisonnement (le workflow n'envoie jamais `model.params`, donc `id`/`aliases` ne peuvent contenir « thinking ») est logiquement cohérent avec le code lu, mais reste une inférence, pas une observation directe de ma part. |
| 6 | §3.4b — sélection dépendante de l'ordre de la réponse API ; `ids` agrège `id` et `aliases[]` à plat (`jq -r '[.items[] \| .id, (.aliases[]? // empty)] \| .[]'`). | **CONFIRMED** (pour la lecture du code) / **NEEDS_OWNER** (pour le risque futur) | `git show 65c3ac1:.github/workflows/pipeline-audit.yml` ligne 128 — le `jq` cité aplatit bien `id` et `aliases` en une seule liste, confirmant que `grep -i opus \| head -1` peut retenir un alias nu. Le scénario « Cursor réordonne sa réponse demain » est une projection non observable aujourd'hui — techniquement plausible vu le code, mais un jugement sur un risque futur, pas un fait vérifiable maintenant. |
| 7 | §3.4c — le modèle retenu n'est conservé nulle part (ni frontmatter d'audit, ni `audit-ledger.jsonl`). | **CONFIRMED** | `grep -n model harness/audit_schema.py architecture/audit-ledger.jsonl` → aucune occurrence. Le frontmatter de l'audit lui-même (relu en §1 de cette revue) ne porte aucun champ modèle. |
| 8 | §3.5 (P2) — le garde anti-boucle et la résolution de modèle n'ont aucun test ; seul le tableau de bord en a (4). | **CONFIRMED** | `python3 -m pytest harness/tests/test_hermes_dashboard.py -q` → `4 passed`. `rg -l "Documentary push\|CURSOR_AUDITOR_MODEL\|hors_boucle" harness/tests/` → aucun résultat, reproduction exacte du §5.5 de l'audit. |
| 9 | §3.6 (P3) — trois sujets indépendants en un seul lot, 801 lignes sur 8 fichiers, au-delà du seuil ~400 lignes cité par `review-guidelines.md`. | **CONFIRMED** | `git show --stat 65c3ac1` → `8 files changed, 801 insertions(+), 13 deletions(-)`, décompte fichier par fichier identique à celui cité en §2.2 de l'audit. `grep -n "400" architecture/review-guidelines.md` → ligne 33, seuil confirmé. |
| 10 | §1/§5.2 — CI verte, 7/7 en `success` sur le commit audité (incluant le run `31593029671`). | **NEEDS_OWNER** (non vérifiable techniquement ici) | `gh auth status` → non authentifié dans cet environnement de revue ; je n'ai pas pu exécuter `gh run list --commit 65c3ac1...` ni `gh run view 31593029671 --log`. Je ne peux ni confirmer ni réfuter ce point par manque d'accès — ce n'est pas une incohérence détectée, seulement une limite d'outillage. Le propriétaire, qui a accès à GitHub Actions, peut trancher directement en un coup d'œil. |
| 11 | §5.5 — 309 tests passent, 16 skippés, tous `test_run_unity.py` faute de `powershell.exe`. | **CONFIRMED** | `python3 -m pytest harness/tests/ -q` → `309 passed, 16 skipped in 7.24s`. `pytest -q -rs \| grep SKIPPED` → 16 lignes, toutes `harness/tests/test_run_unity.py:*: powershell.exe not available on this platform`. Reproduction exacte. |
| 12 | §4.3 — aucun doublon avec un brief ouvert ; table de 11 briefs avec titres. | **CONFIRMED** | `ls harness/queue/briefs/` → exactement les 11 dossiers listés (001, 002, 003, 004, 005, 006, 007, 008-contexte, 008-gaps, 009, 010) ; titres extraits (`grep -m1 "^# "`) identiques mot pour mot à ceux de la table §4.3 de l'audit. |
| 13 | §4 — sources externes S1-S8 (blogs, docs Cursor) citées à l'appui des comparaisons état-de-l'art. | **NEEDS_OWNER** | Hors du périmètre technique vérifiable depuis ce dépôt (URLs externes, pas d'accès web sortant confirmé dans cette revue). Je n'ai pas cherché à les récupérer ; leur exactitude n'engage que la lecture qu'en fait l'audit, pas un fait du dépôt. Le fond de leur usage (visibilité ≠ contrôle, pré-appel vs post-hoc) est cohérent avec la structure déjà présente dans le dépôt (`ci_budget_guard.py precheck`/`record`), donc plausible, mais non ré-audité. |
| 14 | Frontmatter — l'audit ne s'auto-autorise aucune exécution (`implementation_authorized: false`, `ci_changes_authorized: false`, `code_changes_authorized: false`) et les 3 briefs proposés sont présentés comme des entrées, pas des instructions. | **CONFIRMED** | Frontmatter relu ligne par ligne (§1 de ce document) — les trois clés sont bien à `false`. Le corps de l'audit (§6, §7) répète explicitement « aucune n'est une instruction, et aucune n'autorise quoi que ce soit », cohérent avec le frontmatter. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **CI verte 7/7 et log du run `31593029671` (§5.2, §5.3 de l'audit).** Je
  n'ai pas d'accès `gh` authentifié dans cet environnement de revue et n'ai
  pas pu rejouer ces deux commandes. Le propriétaire (ou tout accès
  authentifié à `gh`) peut vérifier en une commande :
  `gh run list --commit 65c3ac1c85c24cc61265c7f9ec4989cc67a0b4f9`.
- **Traitement des 3 briefs proposés.** Comme le rappelle l'audit lui-même,
  ce sont des entrées pour la boucle (`claude-challenger` puis le
  propriétaire), pas des instructions. Cette revue confirme leur exactitude
  technique (proposition 1 ↔ constat §3.1 CONFIRMED ; proposition 2 ↔ §3.2
  CONFIRMED ; proposition 3 ↔ §3.4 CONFIRMED/PARTIAL) mais ne juge pas leur
  priorité ni leur valeur métier — c'est au propriétaire de décider lesquels
  convertir en brief via `/forge-audit-accept` puis `/forge-audit-convert`.
- **Constats laissés volontairement sans brief (§3.3 churn du tableau de
  bord, §3.6 lot de trois sujets).** L'audit les qualifie de « décision de
  conception qui appartient au propriétaire ». Cette revue confirme
  techniquement les deux constats (voir §2, lignes 4 et 9) ; reste au
  propriétaire de dire s'il veut les refermer maintenant ou les laisser tels
  quels.

## 4. Synthèse

**Ce qui tient.** Sur les 14 points vérifiés, 10 sont **CONFIRMED** sans
réserve : la mécanique budgétaire manquante sur `pipeline-audit.yml`
(preuve reproduite au caractère près, y compris le ledger vide et le « 0
USD » affiché), le trou du garde anti-boucle sur `hermes/**` (motif rejoué
sur les 5 cas cités, sortie identique), le churn du tableau de bord dû à
l'horodatage (reproduction bit-à-bit de la diff à 6h d'écart), l'absence de
tests sur le shell des workflows, le volume du lot (801 lignes/8 fichiers)
au-delà du seuil documenté, l'absence de tout champ « modèle » dans le
schéma d'audit ou le ledger, la non-duplication avec les 11 briefs ouverts,
la suite de tests (309 passés/16 skippés, tous `test_run_unity.py` faute de
PowerShell), et la citation exacte du brief 009 (Non-Goal + justification du
plafond). Le calcul de l'agrégation `id`+`aliases` par `jq` est également
confirmé à la lecture du code.

**Ce qui est nuancé.** Le filtre « thinking » inerte (§3.4a) est confirmé
*au niveau du code lu*, mais je n'ai pas pu rejouer l'appel réel à
`GET /v1/models` ni le log du run cité — verdict PARTIAL, pas parce que
l'audit se trompe, mais parce que je ne peux pas re-vérifier cette portion
moi-même depuis cet environnement. Le risque « Cursor réordonne sa réponse
demain » (§3.4b) est un jugement sur un futur possible, pas un fait présent
— NEEDS_OWNER par nature, même si le code qui le rend possible est
confirmé.

**Ce qui n'a pas pu être vérifié ici.** L'état CI (§5.2) et le log du run
`31593029671` (§5.3) n'ont pas pu être rejoués faute d'authentification
`gh` dans cet environnement — signalé explicitement, non extrapolé. Les
sources externes S1-S8 n'ont pas été re-consultées ; leur usage dans
l'argumentaire est cohérent avec l'infrastructure déjà présente dans le
dépôt, mais leur exactitude propre reste NEEDS_OWNER / hors périmètre de ce
contre-audit technique.

**Recommandation de traitement.** Aucune fausse affirmation technique
détectée sur les points reproductibles depuis ce dépôt. L'audit est solide :
chaque preuve citée (commandes, extraits de code, lignes de brief) rejoue à
l'identique. Les trois briefs proposés (branchement budgétaire de
`pipeline-audit.yml`, restriction de l'exemption anti-boucle, déterminisme
et traçabilité du modèle) sont des candidats valides pour la suite de la
boucle — sous réserve de la vérification CI que seul le propriétaire (ou un
accès `gh` authentifié) peut clore.
