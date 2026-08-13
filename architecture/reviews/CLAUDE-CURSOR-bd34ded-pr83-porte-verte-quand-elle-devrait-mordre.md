---
review_of: CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre
reviewer: claude-code
target_commit: bd34dedbb713863d7f9bfa8f9341975aa01291d6
reviewed_at: 2026-08-13T13:30:00Z
---

# Contre-audit de CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : bd34dedbb713863d7f9bfa8f9341975aa01291d6
- Le commit existe-t-il dans l'historique de la branche cible ? **Oui.**
  `git fetch origin forge/014-pipeline-contre-audit-porte-e180` puis
  `git log -1 --format="%H %s" origin/forge/014-pipeline-contre-audit-porte-e180`
  →  `bd34dedbb713863d7f9bfa8f9341975aa01291d6 evaluateur: verdict PASS du lot 014
  (itération 3) + ligne rectificative au registre de coût (N8)`. C'est bien le
  SHA de tête de la PR #83 au moment de l'audit.
- Mesures de l'audit rejouées ? Oui, sur un worktree isolé
  (`git worktree add /tmp/pr83 bd34ded…`) + l'API GitHub REST non
  authentifiée (`gh` n'a pas de token dans cet environnement, mais
  `curl https://api.github.com/repos/...` fonctionne pour un dépôt public) :
  check-runs du commit audité, contenu des workflows, tests pytest (34 nouveaux
  tests, après `pip install pytest` — absent de l'environnement), démonstrations
  git en bac à sable. Détail par point ci-dessous.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | P0-1 — Injection shell via `${{ github.head_ref }}` dans le job `audit-check` de `audit-guard.yml` ; `actionlint` rouge sur la PR, vert sur `master` | **CONFIRMED** | Code relu au SHA audité : `run: \| … --head-branch "${{ github.head_ref }}"` (audit-guard.yml, job `audit-check`), motif non filtré par `env:`, contrairement au job `cursor-scope` du même fichier qui passe `BASE_REF` par `env:`. Vérifié via l'API GitHub : `GET /repos/PLiagre/ForgeHistory/commits/bd34ded/check-runs` → `actionlint … failure` sur les deux runs (push et pull_request) de ce commit ; `audit-check … success`. `GET /repos/.../actions/workflows/security.yml/runs?branch=master` → tous les runs récents `security` sur `master` sont `success`. La régression est bien introduite par cette PR, comme annoncé. |
| 2 | P0-2a — La règle « branche » est morte pour une PR (aucun `head_ref` ne vaut `master`) | **CONFIRMED**, avec une divergence de comptage mineure | Au SHA audité, `architecture/inbox/` contient 34 fichiers dont 25 `target_branch: master` (l'audit dit 18/20 — probablement compté à un autre instant ou sur un sous-ensemble ; l'inbox croît en continu). Le ratio dominant (~74 %) et la conclusion qualitative (« la règle branche ne peut pas se déclencher sur un `head_ref` de PR ») sont corrects indépendamment du compte exact. |
| 3 | P0-2b — La règle « commit » s'efface au commit suivant | **CONFIRMED** | Démo rejouée en bac à sable isolé (`--inbox /tmp/demo83/inbox --ledger /tmp/demo83/ledger.jsonl`, audit `PROPOSED` non adjugé) : `--head-commit abcdef0…` → `exit=1` (rouge) ; même audit, `--head-commit 999…` (un « commit suivant » simulé) → `exit=0` (vert). Sortie identique à celle citée par l'audit. |
| 4 | P0-2c — La PR #83 elle-même passe sa propre porte en vert pendant l'écriture de la critique | **CONFIRMED** | Rejoué dans le worktree au SHA bd34ded : `pr_audit_guard.py check --head-branch forge/014-pipeline-contre-audit-porte-e180 --head-commit bd34ded…` → `Aucun audit ne cible cette PR — contrôle vert. exit=0`. Confirmé aussi côté CI réelle : `audit-check … success` sur le commit bd34ded (check-runs API). |
| 5 | P1-1 — Le repli Codex ne peut aboutir : le CLI n'est installé nulle part dans `pipeline-challenge.yml` | **CONFIRMED** | `grep -n "npm install" .github/workflows/pipeline-challenge.yml` → une seule ligne, `@anthropic-ai/claude-code` uniquement. `grep -i codex` → aucune installation, aucun `~/.codex/auth.json`. `pipeline-forge-run.yml` installe bien `@openai/codex` **et** amorce `auth.json` via `codex login status`. La garde d'identifiants du repli teste `secrets.CODEX_AUTH_JSON != ''` (présence du secret), pas la présence du binaire ni de `auth.json` — le message « identifiants absents » ne sera donc jamais émis alors que `codex exec` échouera par `command not found`. |
| 6 | P1-2 — L'état du refus fournisseur ne rejoint jamais `master` ; le `git add` de l'étape de publication est un no-op après `git checkout -` | **CONFIRMED** | Code relu : l'étape « Commit état du refus fournisseur » fait `git checkout -b … && git add … && git commit … && git push … ; git checkout -`. Reproduit en bac à sable (`git init`, fichier modifié, `checkout -b`, `add`, `commit`, `checkout -`) : après le `checkout -`, le fichier sur la branche d'origine revient à son contenu committé (la modification n'existe que sur la branche jetable) — `git status --porcelain` vide, contenu du fichier inchangé. L'étape « Publish the review », plus bas, contient bien un second `git add harness/pipeline/vendor-refusal-state.jsonl` (ligne confirmée), qui est donc un no-op puisqu'il n'y a rien à ajouter. |
| 7 | P1-3a — Le test qui valide le volet B est un simulateur d'Actions écrit par le même acteur, sans run réel citant les nouveaux chemins | **CONFIRMED** | `test_pipeline_challenge_paths.py` contient bien `simulate_job`, `_eval_condition`, `_has_status_func` — une réimplémentation des règles de conditions GitHub Actions, pas une exécution. Les trois runs cités par le corps de la PR (31694643198, 31694909507, 31694993448) ont pour horodatage 2026-08-13T11:14–11:19Z (API `GET .../actions/runs/{id}`), soit plus d'une heure **avant** le commit bd34ded (2026-08-13T12:46:31Z) qui contient le correctif final — ils ne peuvent donc pas valider les chemins de l'itération 3. |
| 8 | P1-3b — Un des sept chemins (`codex_succeeds=True`) est irréalisable en CI | **CONFIRMED** | `grep -n codex_succeeds test_pipeline_challenge_paths.py` → le simulateur pilote bien la conclusion du chemin « repli réussi » par un booléen de scénario (`ctx.get("codex_succeeds", False)`), jamais par une exécution réelle de `codex exec`. Combiné à P1-1 (CLI absent), ce chemin ne peut pas se produire tel quel en CI. |
| 9 | § 2 — Ni `verdict.md`, ni `generator-log.md`, ni les feedbacks ne mentionnent `actionlint`/`injection`/la rougeur de `security` ; `verdict.md:121` compte la présence de `head_ref` comme un PASS (SC2) | **CONFIRMED** | `grep -rniE "actionlint\|injection\|head_ref" harness/queue/briefs/014-pipeline-contre-audit-porte/` → seules 5 occurrences de `head_ref`/`github.sha`, toutes dans le sens « la garde appelle bien la commande avec ces arguments », aucune mention d'`actionlint` ni d'`injection`. `verdict.md:121` dit explicitement PASS sur la présence littérale de `${{ github.head_ref }}` dans le job. |
| 10 | P2-1 — 22 fichiers, +4691/−26, deux volets (A et B) sans dépendance technique dans un seul lot | **CONFIRMED** | `git diff --stat origin/master...bd34ded` → `22 files changed, 4691 insertions(+), 26 deletions(-)`, exactement le chiffre cité. Les fichiers touchés se répartissent bien entre `pr_audit_guard.py`/`audit-guard.yml` (volet A) et `vendor_refusal.py`/`pipeline-challenge.yml` (volet B), sans import croisé entre les deux modules Python. |
| 11 | P2-2 — Le lecteur de frontmatter casse sur le format documenté par `architecture/README.md` (commentaires en ligne) | **CONFIRMED** | Rejoué avec l'exemple exact de `README.md:66-79` (avec commentaires `# …` en fin de ligne) passé à `_parse_frontmatter` : `target_branch` retourné vaut `'master                              # branche auditée'` (chaîne brute, commentaire inclus), donc `== 'master'` est `False`. Un audit rédigé exactement comme le README le prescrit échapperait à la règle « branche ». |
| 12 | P2-3 — `audit_ledger` importé par `sys.path.insert` au niveau module | **CONFIRMED** | `pr_audit_guard.py` : `sys.path.insert(0, str(HARNESS))` en ligne 40, `import audit_ledger` en ligne 41 (l'audit dit « lignes 40-42 » — décalage d'une ligne, sans conséquence sur le constat). Le mécanisme fonctionne mais pollue `sys.path` du processus appelant, comme décrit. |
| 13 | P3 — 34 tests nouveaux passent ; boucle à trois rôles réellement mordante (2 REJECT documentés puis PASS) ; câblage événementiel juste (`skipping` sur push, `pass` sur pull_request) ; aucune régression sur `tests`/`sim-tests`/`f0-demo`/`schema`/`gitleaks` | **CONFIRMED** | `python3 -m pytest harness/tests/test_pr_audit_guard.py harness/tests/test_vendor_refusal.py harness/tests/test_pipeline_challenge_paths.py -q` → `34 passed in 0.17s` (pytest absent de l'environnement de revue, installé via `pip install pytest` pour rejouer). Check-runs API sur bd34ded : `audit-check` `skipping` sur l'événement push, `success` sur pull_request ; `tests`, `sim-tests`, `f0-demo`, `schema`, `gitleaks` tous `success`. Conforme point pour point. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **La rubrique d'évaluation a-t-elle validé un motif dangereux ?** L'audit
  observe (à raison, techniquement) que `eval-rubric.md:93` exigeait
  littéralement `${{ github.head_ref }}` dans un `run:`, et que l'Évaluateur a
  suivi la rubrique à la lettre. La question de savoir si les rubriques qui
  prescrivent du YAML de CI doivent désormais être passées au linter de
  sécurité avant d'être adjugées est un arbitrage de process, pas un fait
  technique — au propriétaire de trancher.
- **Aucun rôle du harnais ne lit la CI du commit qu'il juge.** Fait technique
  confirmé (le workflow `security` était rouge aux trois pushes du Générateur,
  aucun rôle ne le mentionne). La décision d'ajouter cette lecture au
  protocole des rôles (Générateur/Évaluateur) est un choix de gouvernance, pas
  une correction de code — au propriétaire de décider si/quand.
- **Les trois briefs atomiques proposés (B-1, B-2, B-3)** sont des
  propositions de correction techniquement cohérentes avec les défauts
  confirmés ci-dessus, mais leur conversion en briefs réels, leur priorité et
  leur découpage exact restent une décision du propriétaire (l'audit le dit
  lui-même : « proposition, pas instruction »).

## 4. Synthèse

Tous les points majeurs de l'audit — les deux P0, les trois P1, les trois P2,
et le paragraphe P3 (« ce qui tient ») — ont été rejoués indépendamment et
**tiennent** : le code cité correspond au SHA audité, les commandes citées
reproduisent les sorties citées (à un écart de comptage mineur près sur le
ratio `target_branch` en § P0-2a, et un écart d'une ligne sur un numéro de
ligne en P2-3, ni l'un ni l'autre n'affectant la conclusion). Rien n'a été
réfuté.

Le point le plus grave (P0-1) est vérifiable en une commande côté CI réelle
(`actionlint` rouge sur la PR, vert sur `master`) et n'est pas contestable.
Le point P0-2 est le plus intéressant techniquement : la porte annoncée
« un audit non adjugé rend le contrôle rouge » est vérifiée vraie sur le
papier (les tests unitaires du lot passent) et fausse sur le cas d'usage
réel (un audit vivant sur une PR vivante), ce qui est exactement le genre
d'écart qu'une porte mécanique doit fermer avant fusion, pas après.

Aucun des trois griefs n'est un désaccord de valeur — ce sont des faits
reproductibles (code, sorties de commande, API GitHub). La seule zone
d'appréciation relève du propriétaire (§ 3) : la gouvernance des rubriques et
la conversion des briefs proposés, pas la véracité des constats.
