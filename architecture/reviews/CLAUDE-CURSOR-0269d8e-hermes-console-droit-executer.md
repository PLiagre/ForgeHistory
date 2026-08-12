---
review_of: CURSOR-0269d8e-hermes-console-droit-executer
reviewer: claude-code
target_commit: 0269d8e90231e554db356cbc57aea1f70bc3f507
reviewed_at: 2026-08-12T15:10:00Z
---

# Contre-audit de CURSOR-0269d8e-hermes-console-droit-executer

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : 0269d8e90231e554db356cbc57aea1f70bc3f507
- Le commit existe-t-il dans l'historique de la branche cible ? **Oui.**
  `git cat-file -t 0269d8e90231e554db356cbc57aea1f70bc3f507` → `commit` ;
  `git log --oneline` sur `master` le montre en ancêtre direct (fusionné par
  PR #34, `27aaf2a4..bb8fe11b`). `git show --stat --format='%H%n%ci%n%s%n%P'`
  reproduit exactement les 4 fichiers / +168/-8 annoncés.
- Mesures de l'audit rejouées ? Oui, sur ce checkout (`master` à `f505bc9`,
  qui contient `0269d8e` en ancêtre) :
  - `python3 -m pytest harness/tests/ -q` → `309 passed, 16 skipped in
    7.53s`. Identique au chiffre de l'audit (durée différente, normal).
  - `python3 harness/audit_schema.py` → aujourd'hui `All 13 audit(s)
    valid.` contre `11` dans l'audit : **écart attendu**, pas une erreur —
    deux audits supplémentaires (dont celui-ci même) sont entrés dans
    `inbox/` depuis. Rejoué à l'état du SHA audité
    (`git ls-tree -r --name-only 0269d8e -- architecture/inbox/`) → 11
    fichiers, conforme.
  - `python3 harness/harness_audit.py` → `SCORE: 20/24`, mêmes deux `[FAIL]`
    (`fake_honest_demo_pair`, `no_premature_stub_content`) que l'audit,
    chiffre et libellés identiques.
  - `gh pr view 34 ...`, `gh api .../branches/master/protection`,
    `gh repo view` : **non rejouables dans ce sandbox** (pas de `GH_TOKEN`,
    `gh auth status` → non authentifié). Corroboré indirectement par
    l'historique git local : le commit de tête de PR (`bb8fe11`, auteur-date
    `2026-08-12 12:23:51 +0000`) et le commit de merge
    (`2026-08-12 14:25:23 +0200` = `12:25:23Z`) sont distants d'environ 90
    secondes, cohérent avec la fusion rapide décrite ; je ne peux pas
    confirmer indépendamment `createdAt`, `reviews: []` ni les libellés de
    checks (`SKIPPED`/`SUCCESS`) faute d'accès à l'API GitHub ici.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | Résumé (bilan documentaire, 0 P0, 2 P1, 4 P2, 3 P3) | CONFIRMED | Diff rejoué (`git show --stat`) : 4 fichiers, +168/-8, aucun fichier de code ni de test. Décompte de sévérité conforme au corps du rapport. |
| 2 | P1-1 — porte des 4 preuves inactive ; `merge-bot.yml` ne couvre pas `forge/*` ; aucun audit ne ciblait le SHA de tête au moment de la fusion | CONFIRMED | `docs/rules/conditional-merge-gate.md:1-4` = « spécifiée, non câblée » (texte identique). `.github/workflows/merge-bot.yml:27` = `if: startsWith(github.head_ref, 'cursor/') \|\| startsWith(github.head_ref, 'forge-bot/')` — ne matche pas `forge/hermes-decision-adr-0011-c2dd`. Recherche directe : aucun fichier de `architecture/inbox/` au SHA `0269d8e` ne porte `target_commit: bb8fe11...` (script de vérification exécuté fichier par fichier sur l'arbre de ce commit, 0 correspondance). Le timing exact de la PR (56s, `reviews: []`) n'a pas pu être revérifié faute de jeton GitHub — voir §1. |
| 3 | P1-2 — `invoke-cursor-auditor` vert = déclenchement, pas résultat | CONFIRMED | `.github/workflows/pipeline-audit.yml:184-194` : `curl --fail-with-body` vers l'API Cursor, le job réussit dès que la requête HTTP aboutit ; le message affiché est littéralement « cursor-auditor launched -- its audit will arrive as a cursor/* PR ». Rien dans ce job ne vérifie qu'un audit est ensuite déposé. |
| 4 | P2-1 — Hermes obtient capacités par ADR, hors circuit brief/gate ; asymétrie avec `architecture/inbox/` | CONFIRMED | `rg -l "hermes" harness/queue/briefs/*/brief.md` → vide. Brief 010 Non-Goal 4 (ligne 216-217) : « Donner un droit d'écriture à Hermes... Son contrat d'écriture fera l'objet d'un brief distinct » — ce brief n'existe pas. `audit-guard.yml:28` a bien un job `cursor-scope` qui garde `architecture/inbox/`, sans équivalent pour le jeton d'Hermes. |
| 5 | P2-2 — ADR-0011 paraphrase les 4 conditions sans citer `conditional-merge-gate.md` ; 3 éléments manquants (Forge-Brief unique, relecture avant tentative, statut inactif) | CONFIRMED | `grep -n "conditional-merge-gate" docs/adr/0011-*.md` → aucun résultat. `docs/rules/conditional-merge-gate.md:24` exige `Forge-Brief: harness/queue/briefs/<id>/`, absent de l'ADR. `hermes/README.md:41` affirme encore « Aucun workflow n'exécute ce que Hermes écrit » juste avant d'introduire la section ADR-0011 qui décrit une exécution directe. |
| 6 | P2-3 — ADR-0010 reste `accepted`, ne mentionne pas 0011 ; table de routage `CLAUDE.md` pointe encore vers ADR-0010 seul | CONFIRMED | `docs/adr/0010-*.md` : `**Status**: accepted`, aucune occurrence de « 0011 » dans le fichier. `docs/adr/template.md:4` prévoit bien le champ `superseded by ADR-NNNN`, inutilisé ici. `CLAUDE.md:127` : `\| ROADMAP.md, hermes/** \| hermes/README.md (contrat d'écriture d'Hermes) + ADR-0010 \|` — ADR-0011 absent. |
| 7 | P2-4 — le tableau de bord ne lit ni `hermes/requests/**` ni `hermes/reports/**`, seulement PR ouvertes + `AUDIT_APPROVED` | CONFIRMED | `hermes/dashboard.py:225-239` (« Ce qui attend le propriétaire ») itère uniquement sur `prs` et `audits_en_cours` filtrés `AUDIT_APPROVED`. `rg -n "requests\|reports\|DEMANDE" hermes/dashboard.py` → une seule occurrence, ligne 325, dans une phrase d'explication, pas une lecture de données. |
| 8 | P3-1 — `hermes-observer.yml` cumule `pull_request_target` + runner self-hosted persistant + PAT à venir sur la même machine ; mais atténué (repo privé, permissions read-only, pas de checkout, interpolations non contrôlables) | CONFIRMED | Fichier lu en entier : `on: pull_request_target`, `runs-on: [self-hosted, Windows, X64, hermes-observer]`, `permissions:` toutes en `read`, aucune étape `actions/checkout`, seules interpolations `github.event_name` / `github.event_path`. Le classement en P3 (et non P1) plutôt qu'un `REFUTED` pur repose sur des atténuants externes (visibilité du repo) non re-vérifiables ici faute de `gh auth` — voir §1 ; je fais confiance à la commande citée (`gh repo view` → `PRIVATE`) sans avoir pu la rejouer. |
| 9 | P3-2 — dépense Codex CLI déléguée par Hermes hors de tous les compteurs existants | CONFIRMED | `harness/backends/ledger.py append` exige `--brief <dir>` (ligne 32) : scope par lot du harnais, pas par usage conversationnel d'Hermes. ADR-0011 lignes 60-63 : délégation Codex CLI local, « câblage... hors dépôt ». Aucun point d'intégration dans `hermes/dashboard.py` (la ligne « Dépense CI ce mois-ci », ligne 217, lit `harness/pipeline/ci-budget-ledger.jsonl`, un fichier différent et sans rapport avec des appels locaux d'Hermes). |
| 10 | P3-3 — auto-audit du harnais mesure 20/24 sur checkout propre, `AGENTS.md` annonce 23/24 | CONFIRMED | `python3 harness/harness_audit.py` → `SCORE: 20/24`, `[FAIL] fake_honest_demo_pair` (`run_demo.log` manquant) + `[FAIL] no_premature_stub_content`. `AGENTS.md:50` : « scores 23/24 : the single FAIL... ». Cause confirmée : `.gitignore:7` = `*.log`, et `git ls-files harness/demo/fake_brief_001/` ne liste pas `run_demo.log` ; `git check-ignore -v` le confirme ignoré. |
| 11 | Veille externe — 10 sources citées, doublons vérifiés contre les 11 briefs existants | PARTIAL | Le contenu technique du dépôt cité (deny-list, `conditional-merge-gate.md`, `harness/budget.py`, brief 010 Non-Goal 2/3) est vérifié et exact. Les 10 sources externes (S1-S10) elles-mêmes n'ont pas été revisitées — hors périmètre d'un contre-audit technique du dépôt, et je n'ai pas d'accès web dans ce passage. Je ne conteste pas leur existence ni leur teneur, je ne l'ai simplement pas vérifiée. |
| 12 | 3 briefs proposés, présentés comme non-doublons des briefs existants | CONFIRMED | Vérification indépendante : `rg -l "hermes" harness/queue/briefs/*/brief.md` vide (aucun brief existant ne couvre P2-1/P2-2/P2-4/P3-2) ; brief 010 SC15/Non-Goal 2 spécifie la porte sans l'activer, cohérent avec « lot ultérieur » cité pour la Proposition 1. Aucune des 3 propositions ne duplique un brief déjà ouvert. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- Le périmètre des quatre actions déléguées à Hermes (dont fusionner une PR)
  reste un choix légitime du propriétaire ; l'audit ne le conteste pas et je
  ne le conteste pas non plus. Ce qui doit revenir au propriétaire est
  l'ordre de traitement des 2 P1 et 4 P2 : notamment si la Proposition 1
  (porte mécanique des 4 preuves) doit précéder ou suivre l'usage réel de la
  capacité de fusion par Hermes, et si ADR-0010 doit être amendé
  immédiatement (correction d'une ligne, faible coût) indépendamment du sort
  des propositions de brief.
- Je n'ai pas pu revérifier moi-même les métadonnées GitHub de la PR #34
  (durée exacte, `reviews: []`, statuts des checks) faute de jeton dans ce
  sandbox de revue. Ces éléments sont corroborés indirectement (timing des
  commits) mais pas reproduits à l'identique — à garder en tête si la
  décision du propriétaire s'appuie sur le chiffre précis des 56 secondes.

## 4. Synthèse

Les 12 points vérifiables techniquement tiennent : chaque commande citée par
l'audit a été rejouée avec un résultat identique ou explicable (l'écart
11→13 audits dans `inbox/` est un effet du temps qui passe, pas une erreur
de l'audit — revérifié à l'état exact du SHA audité). Le seul point non
reproductible ici est l'accès à l'API GitHub (PR #34, protection de
branche) : le sandbox de cette revue n'a pas de `gh auth`, donc ces éléments
sont corroborés indirectement (git local) plutôt que rejoués à l'identique —
ce n'est pas une réfutation, c'est une limite de l'environnement de contre-
audit. Aucun `REFUTED`. Un `PARTIAL` sur les seules sources externes
(S1-S10), hors du périmètre technique du dépôt.

Recommandation de traitement : les 2 P1 sont réels et pointent un même
trou — la porte de fusion documentée n'est mécaniquement vérifiée par
personne, et le signal « audit Cursor » que la porte doit lire est
aujourd'hui un déclenchement, pas une preuve. Les 3 propositions de brief
sont non redondantes entre elles et avec l'existant ; leur conversion reste
une décision du propriétaire, hors du rôle de ce contre-audit.
