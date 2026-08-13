---
review_of: CURSOR-70380c6-pr83-porte-desarmee-par-la-derive-de-sha
reviewer: claude-code
target_commit: 70380c6faf08d1c45fc654cca1acfbe39b5c8507
reviewed_at: 2026-08-13T14:20:00Z
---

# Contre-audit de CURSOR-70380c6-pr83-porte-desarmee-par-la-derive-de-sha

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `70380c6faf08d1c45fc654cca1acfbe39b5c8507`.
- Le commit existe-t-il dans l'historique de la branche cible ? **Oui.**
  `git fetch origin 'refs/pull/83/head:pr83head'` puis `git log --oneline
  pr83head` place `70380c6` en tête, avec `150fd14` et `bd34ded` juste
  en-dessous, dans le même ordre que celui narré par l'audit. Le
  merge-base avec `master` est `da536505c804e3ecc937bab16e3747e09c81968f`
  — identique à la valeur « base » déclarée dans le tableau de provenance
  de l'audit.
- Mesures de l'audit rejouées ? **Oui**, dans un worktree dédié
  (`pr83head`) et un second sur le merge-ref (`pr83merge`), avec
  `python3` (pas de `.venv` dans cet environnement ; `python3` est
  l'échappatoire documentée par `harness/bare_python.py`, la regex
  exclut explicitement `python3`). Détail des commandes rejouées dans le
  tableau ci-dessous.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | § 1 — CI du commit `70380c6` classée entièrement verte, avec le détail des jobs cité (`actionlint success`, `audit-check success`/`skipped`, etc.) | **CONFIRMED** | `curl -s https://api.github.com/repos/PLiagre/ForgeHistory/commits/70380c6.../check-runs?per_page=50` (API publique, sans authentification) reproduit exactement le même ensemble de `(nom, conclusion/status)` que la liste citée par l'audit, y compris le doublon `audit-check` (`success` + `skipped`) et `Reconcile local Hermes state: queued`. |
| 2 | § 2 — Le défaut d'injection `github.head_ref` est fermé par un bloc `env:` entre `bd34ded` et `150fd14` | **CONFIRMED** | `git diff bd34ded..150fd14 -- .github/workflows/audit-guard.yml` reproduit le diff cité au caractère près (ajout de `PR_HEAD_BRANCH`/`PR_HEAD_COMMIT` en `env:`, remplacement des deux interpolations `${{ }}` par des variables shell citées). |
| 3 | § 2 — Le gate mécanique rejoue `ACCEPT` et 34 tests passent au SHA de tête | **CONFIRMED** | Worktree sur `pr83head` : `python3 harness/verdict_audit.py harness/queue/briefs/014-pipeline-contre-audit-porte` → `VERDICT: ACCEPT` (tous les contrôles cités en `PASS`, y compris `verdict_is_not_self_authored`). `python3 -m pytest harness/tests/test_pr_audit_guard.py harness/tests/test_vendor_refusal.py harness/tests/test_pipeline_challenge_paths.py -q` → `34 passed` (0,21 s ici contre 0,13 s cité — écart de machine, non significatif). |
| 4 | § 3, P0-1 — Au SHA de tête, l'audit `CURSOR-bd34ded-pr83-…` est présent dans l'arbre du merge-ref, absent du registre (`current_state_for` → `None`), et la garde répond `exit=0` (vert) alors qu'elle répond `exit=1` (rouge) sur le seul SHA visé par cet audit non adjugé | **CONFIRMED** | Worktree sur `refs/pull/83/merge` : `ls architecture/inbox/ | grep pr83` → le fichier `CURSOR-bd34ded-…` y est bien. `audit_ledger.current_state_for(...)` → `None`. La démonstration A/B (§ 3) est reproduite **au caractère près**, message et code de sortie identiques dans les deux sens : `--head-commit 70380c6…` → « Aucun audit ne cible cette PR — contrôle vert. » exit=0 ; `--head-commit bd34ded…` → « ERREUR : … 1 audit(s) non adjugé(s) … » exit=1. C'est le constat central de l'audit et il tient intégralement. |
| 5 | § 3 — 30 des 41 audits de l'inbox portent `target_branch: master`, rendant la règle « branche » structurellement inapplicable pour la majorité du corpus | **CONFIRMED, avec une dérive de dénominateur bénigne** | Sur le merge-ref fetché aujourd'hui, l'inbox compte 42 fichiers (pas 41) et 30 portent `target_branch: master` (18 avec un alignement large + 12 avec un alignement simple — même total, `grep -c` le confirme). L'écart de dénominateur (41 → 42) s'explique : le merge-ref de GitHub se recalcule contre `master` courant, qui a avancé de plusieurs commits depuis la rédaction de l'audit (13:10) — dont un qui a fusionné cet audit lui-même dans l'inbox (`843cc90`, PR #95). Le numérateur (30) est identique ; la conclusion (majorité écrasante en `target_branch: master`, règle « branche » inopérante) n'est pas affectée par cette dérive naturelle du corpus. |
| 6 | § 4, P1-1 — La garde répond le même message/code de sortie (« contrôle vert », exit 0) pour une inbox absente/mal orthographiée que pour une inbox lue sans correspondance ; aucun test ne couvre le cas | **CONFIRMED** | Reproduit à l'identique : `--inbox architecture/Inbox` (majuscule) → vert, exit 0 ; `--inbox /tmp/vide83` (répertoire vide créé pour l'occasion) → vert, exit 0. `grep -nE "inbox_absente\|not exists\|missing\|inexistant" harness/tests/test_pr_audit_guard.py` → aucun résultat, confirmé. |
| 7 | § 5, P1-2 — Les 4 compteurs du manifeste n'exercent que la règle « branche » (fixture `target_branch=head_branch`, `head_commit` non appariable) ; le seul test de la règle « commit » utilise un SHA de tête identique au `target_commit` | **CONFIRMED** | `test_counters_code_sortie_avec_audit_non_adjuge` (ligne 224 dans mon exemplaire) : `_make_audit(..., target_branch="target-branch")`, appel avec `head_commit="zzzzzzz"` — non appariable par commit, ne peut passer que par la règle branche. `test_exits_1_when_matched_by_commit` (ligne ~187) : `target_commit="abc1234e714a9ff…"`, `head_commit="abc1234feedbeef"` — les 7 premiers caractères sont identiques, exactement le cas dégénéré décrit par l'audit. |
| 8 | § 6 — Trois reports P1 du volet B vérifiés persistants (repli Codex inerte : `npm install` ne pose que `@anthropic-ai/claude-code`, `codex exec` appelé sans installation ; état du refus sur une branche jamais fusionnée, `git checkout -` suivi d'un `git add` no-op) | **CONFIRMED** | `grep -nE "npm install\|codex\|CODEX"` → une seule ligne `npm install`, à la ligne 141, `@anthropic-ai/claude-code` seul ; `codex exec` appelé ligne 241. `grep -nE "checkout -\|git add\|git push\|git commit"` → `git checkout -` toujours présent (ligne 298 dans mon exemplaire), suivi plus loin (ligne 329) d'un `git add harness/pipeline/vendor-refusal-state.jsonl` sur une autre branche fraîchement checkoutée — le `git add` cité s'applique bien après le retour arrière, comme décrit. |
| 9 | § 6 — Le lecteur de frontmatter casse sur le format documenté par `architecture/README.md` (commentaires en ligne non retirés) | **CONFIRMED, et reproduit en direct** (l'audit ne montrait que le code, pas un run) | `architecture/README.md:70-71` documente `target_branch: master    # branche auditée`. En rejouant `_parse_frontmatter` sur cet exemple exact : `{'target_branch': 'master                              # branche auditée', ...}` — la valeur retenue inclut le commentaire, donc ne peut jamais être `== "master"`. C'est un défaut réel et démontrable, pas seulement plausible sur lecture de code. |
| 10 | § 6 — `sys.path.insert` au niveau module pour importer `audit_ledger` | **CONFIRMED** | `pr_audit_guard.py:39-41` (léger décalage de numéro de ligne par rapport à l'audit, contenu identique) : `sys.path.insert(0, str(HARNESS)); import audit_ledger  # noqa: E402`. |
| 11 | § 6 — Simulateur du même auteur pour la preuve du volet B, `test_pipeline_challenge_paths.py` inchangé depuis `bd34ded` | **CONFIRMED** | `git diff --stat bd34ded..70380c6 -- harness/tests/test_pipeline_challenge_paths.py` → aucune sortie (fichier identique à l'octet). |
| 12 | § 0/§ 6 — « Toutes les mesures ci-dessous ont été rejouées au SHA final » ; diff cumulé annoncé à « 22 fichiers, +4696 / −26 », y compris dans la ligne § 6 marquée « vérifié au SHA audité » | **REFUTED sur ce point précis** (ne change aucune sévérité) | `git diff --stat da536505c804e3ecc937bab16e3747e09c81968f...70380c6` donne ici **`22 files changed, 4710 insertions(+), 26 deletions(-)`**, confirmé indépendamment par l'API GitHub (`additions: 4710`). L'écart (4710 − 4696 = 14) correspond exactement aux 14 lignes ajoutées à `verdict.md` entre `150fd14` et `70380c6` (la note de re-vérification que l'audit crédite lui-même en § 2). Donc cette ligne précise du tableau § 6 n'a **pas** été rejouée au SHA final malgré l'affirmation générale de la section — un chiffre stale de deux commits, sur un constat P2 dont la substance (gros lot, deux volets sans dépendance) reste par ailleurs correcte au nombre de fichiers (22, exact) près. |
| 13 | § 1 — `mergeStateStatus: UNSTABLE` (via `gh pr view --json mergeStateStatus`) | **NEEDS_OWNER (non vérifiable ici)** | Cet environnement n'a pas de session `gh auth`, donc pas d'accès GraphQL. L'API REST publique (`GET /pulls/83`) donne `mergeable_state: "unknown"` — un champ distinct (REST, pas GraphQL) et calculé de façon asynchrone par GitHub ; il ne confirme ni n'infirme `UNSTABLE`. Ce point n'appuie aucune sévérité (P0/P1) de l'audit, donc son statut non vérifié ne change pas la lecture technique. |
| 14 | § 9 — Briefs B-1/B-2/B-3 proposés | **NEEDS_OWNER** (propositions, pas des faits) | Chaque brief cible un point confirmé ci-dessus (B-1 → P0-1 confirmé ; B-2 → P1-1 confirmé ; B-3 → reports § 6 confirmés). Leur contenu technique est cohérent avec les constats vérifiés ; leur adoption reste un choix de priorisation du propriétaire, hors compétence de cette revue. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **§ 10, gouvernance — correctif avant décision.** L'audit `bd34ded`
  (P0) a été corrigé au commit `150fd14` alors qu'il est toujours
  `PROPOSED`, sans ligne au registre — confirmé ci-dessus (`current_state_for`
  → `None`). Question ouverte, hors compétence technique : qui inscrit la
  ligne au registre quand un audit est traité « au vol » par l'acteur
  audité, et faut-il l'exiger avant de fusionner un correctif qui répond à
  un audit encore `PROPOSED` ?
- **§ 10, gouvernance — convention `target_branch: master`.** L'audit
  observe que la majorité du corpus (30/42 ici, contre 30/41 à sa
  rédaction — item 5 ci-dessus) documente `target_branch: master` pour
  des critiques de PR, ce qui est précisément ce qui prive la règle
  « branche » de prise. Ce n'est pas un bug de code isolé : c'est une
  convention de rédaction du corpus qui interagit avec le mécanisme de la
  porte. Le propriétaire doit trancher si la convention change (brief
  B-1) ou si l'appariement doit s'adapter à la convention actuelle.
- **mergeStateStatus (item 13 ci-dessus)** : si ce chiffre a une
  importance pour la décision de fusion, il doit être revérifié avec un
  accès `gh auth` valide — je ne peux pas le confirmer ni le réfuter ici.

## 4. Synthèse

**Ce qui tient, intégralement.** Le constat central de l'audit — P0-1,
« la porte est verte alors qu'un audit non adjugé de cette PR est dans
l'arbre qu'elle lit » — se reproduit **au caractère près**, in situ, sur
le merge-ref réel de la PR #83 : fichier présent, registre vide (`None`),
garde à `exit=0` sur le SHA de tête et `exit=1` sur le seul SHA visé par
l'audit non adjugé. Ce n'est pas une déduction de code, c'est une mesure
directe que j'ai reproduite indépendamment. Les deux constats P1
(fail-open sur entrée illisible, compteurs qui n'exercent que la règle
inopérante) se reproduisent également à l'identique, commande pour
commande. Les six reports de `bd34ded` (trois P1, trois P2) tiennent
tous, y compris le bug du lecteur de frontmatter que j'ai démontré en
direct plutôt que de me fier au seul examen du code cité par l'audit. La
classification CI, le rejeu du gate (`ACCEPT`) et des 34 tests sont
également confirmés à l'identique.

**Ce qui tombe, et ce que ça ne change pas.** Un seul point précis est
faux : le tableau § 6 annonce avoir « rejoué au SHA final » le chiffre
« 22 fichiers, +4696/−26 », alors que ce chiffre est en réalité stale de
deux commits (la valeur réelle au SHA audité est +4710, confirmée à la
fois par `git diff --stat` et par l'API GitHub). L'écart de 14 lignes est
exactement la note de re-vérification que l'audit crédite par ailleurs en
§ 2 — ironie mineure, pas contradiction. Le nombre de fichiers (22) et la
conclusion qualitative (gros lot, deux volets sans dépendance technique,
P2) restent corrects. `mergeStateStatus: UNSTABLE` n'a pas pu être
revérifié faute d'accès `gh auth` dans cet environnement ; il n'appuie
aucune sévérité de l'audit.

**Recommandation de traitement.** Aucun des deux écarts trouvés (le
chiffre stale, le champ non vérifiable) n'affaiblit P0-1, P1-1 ou P1-2 :
ce sont des points périphériques de provenance, pas des points sur
lesquels repose une sévérité. La porte du volet A est bien désarmée par
la dérive de SHA, mesurée in situ et reproduite indépendamment ici — ce
constat peut être transmis au propriétaire tel quel pour décision
(`/forge-audit-accept` ou `-reject`).
