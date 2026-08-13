---
review_of: CURSOR-4c45718-pr65-ledger-recupere-a-la-main
reviewer: claude-code
target_commit: 4c4571892476603e41740f3d3ef52ca527ba5358
reviewed_at: 2026-08-13T09:01:51Z
---

# Contre-audit de CURSOR-4c45718-pr65-ledger-recupere-a-la-main

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `4c4571892476603e41740f3d3ef52ca527ba5358`. Le
  commit existe : `git rev-parse origin/forge/boucle-audits-post-pr60-ddda`
  renvoie exactement ce SHA — c'est bien la tête de la branche cible
  (`target_branch: forge/boucle-audits-post-pr60-ddda`). Confirmé après
  `git fetch origin`.
- Ce commit n'est **pas** fusionné dans `master` (ni local ni
  `origin/master` après fetch) : `git merge-base --is-ancestor 4c45718
  origin/master` échoue. C'est cohérent avec `audit_type:
  pull-request-review` d'une PR encore ouverte — pas une contradiction.
- Mesures rejouées dans un `git worktree add /tmp/pr65check 4c45718`
  (checkout direct du commit cible, pas d'un `git apply` sur `master`) :
  `harness/audit_schema.py`, `pytest harness/tests/`, `harness/audits.py
  list`, `harness/verdict_audit.py` sur les briefs 012 et 013, comparaisons
  `diff -q` des trois fichiers archivés, comptage des lignes de ledger.
  Environnement sans accès à l'API GitHub (`gh auth status` : pas de
  token) — je n'ai donc pas pu rejouer les commandes `gh api
  repos/.../actions/runs/...` ni `gh pr checks 65` du § 7 de l'audit. Cette
  limite est documentée point par point ci-dessous plutôt que passée sous
  silence.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | La cause de l'incident (deux orchestrations concurrentes) est fausse ; la vraie cause est `actions/checkout` sans `ref:` sur un évènement `push` | **PARTIAL** | Le mécanisme causal est vérifié dans le code : `.github/workflows/pipeline-orchestrate.yml:51-53` a bien `concurrency: {group: pipeline-orchestrate-master, cancel-in-progress: false}` ; `actions/checkout@...` (ligne 65, bloc `with:` jusqu'à la ligne ~67) n'a **aucun `ref:`** — sur un `push`, ceci épingle l'arbre à `github.sha`, pas à la tête de `master`. Le commentaire lignes 133-138 dit littéralement « les seuls autres écrivains du ledger sont les runs de CE workflow […] donc le rebase ne peut pas conflicter sur le ledger » — texte identique à celui cité par l'audit. Tout cela confirme le **mécanisme** proposé. Ce que je n'ai **pas** pu revérifier : les horodatages `started_at`/`completed_at` des deux runs GitHub Actions (`31682696284`, `31682710982`) qui établissent l'absence de recouvrement — aucun accès `gh`/API dans cet environnement. Rien ne contredit le chiffre cité ; je ne peux simplement pas le rejouer moi-même. L'audit signale lui-même cette limite en § 5 (« je n'ai pas d'accès à l'ordonnanceur »), donc rien n'est caché. |
| 2 | La PR réécrit une ligne de ledger avec des comptages faux (`REFUTED: 4` alors que 0 point n'est réfuté) | **CONFIRMED** | Rejeu exact de la commande du § 7 : `parse_verdicts` renvoie `{'CONFIRMED': 14, 'REFUTED': 4, 'PARTIAL': 6, 'NEEDS_OWNER': 4}` alors que le tableau réel du document ne contient que `{'CONFIRMED': 9, 'PARTIAL': 1}` sur 10 points. Cause confirmée dans le code : `harness/audit_review.py:127-134` (`parse_verdicts`) compte les occurrences du mot sur **tout le texte**, légende et prose comprises, pas seulement la colonne verdict du tableau. Nuance sur la citation : l'audit attribue le caractère append-only du ledger à « `architecture/README.md`, règle d'intégrité 3 » — cette règle (ligne 102 du README) dit explicitement que c'est **`inbox/`** qui est append-only, pas `audit-ledger.jsonl`. Le caractère append-only du ledger est réel mais est documenté ailleurs, dans `harness/audit_ledger.py:3` (« append-only historisation of the audit loop ») et dans le fait qu'aucune commande de suppression/réécriture n'existe. La conclusion du constat tient ; la référence au numéro de règle du README est inexacte. |
| 3 | `AUDIT_VERIFIED` est écrit sans jamais consulter la CI ; le SHA final `16ff5ac` a un run rouge | **PARTIAL** | Partie (a) intégralement confirmée sur le code : `harness/pipeline/orchestrator.py:224-229` (`handle_evaluateur_pass`) enchaîne `append_event(..., "AUDIT_IMPLEMENTED", ...)` puis `append_event(..., "AUDIT_VERIFIED", ...)` sans aucun paramètre de SHA, de run ni d'appel réseau — inconditionnel, confirmé. Partie (c) : sur le ledger au commit cible, `AUDIT_IMPLEMENTED`/`AUDIT_VERIFIED` sont bien les deux seuls évènements sans champ de preuve annexe (`review`, `decision`, `briefs`, `archive` absents des deux). Chiffre à corriger : l'audit dit « 6 occurrences dans tout le ledger » ; le compte réel à `4c45718` est **8** (4 paires : `CURSOR-FIXTURE-full-auto-demo`, `CURSOR-5633ee7-...`, `CURSOR-e9a6f4c-...`, `CURSOR-3b47ffe-...`). Le chiffre de 6 ne colle que si l'on exclut la paire `FIXTURE` (données de démo) — hypothèse plausible mais non explicitée dans l'audit. Ce que je n'ai pas pu revérifier : l'existence d'un run rouge sur `16ff5ac` (`31682196140`) — pas d'accès `gh`/API ici ; rien ne le contredit. |
| 4 | Les deux gardes mécaniques de portée (`cursor-scope`, `check-and-automerge`) dépendent du préfixe de branche, pas des chemins ; la branche de cette PR (`forge/...`) n'a ni préfixe | **CONFIRMED** | `.github/workflows/audit-guard.yml:30` : `if: github.event_name == 'pull_request' && startsWith(github.head_ref, 'cursor/')`. `.github/workflows/merge-bot.yml:27` : `if: startsWith(github.head_ref, 'cursor/') || startsWith(github.head_ref, 'forge-bot/')`. La branche cible est `forge/boucle-audits-post-pr60-ddda`, qui ne matche aucun des deux préfixes — les deux gardes se seraient bien exécutées en `skipping`. La liste blanche de chemins de `merge-bot.yml:50` (`architecture/inbox/\|architecture/reviews/\|harness/queue/briefs/.../feedback/`) n'aurait donc jamais été confrontée à ce diff, qui la violerait entièrement (écriture dans `architecture/decisions/`, `architecture/archive/`, `audit-ledger.jsonl`). La citation de `cursor-auditor.md` § Interdits (« tout chemin en dehors de `architecture/inbox/**` ») est exacte (ligne 32). Je n'ai pas pu revérifier la sortie littérale `gh pr checks 65` (`skipping`/`skipping`) faute d'accès API, mais elle découle mécaniquement de la condition `if:` lue dans le fichier — pas une extrapolation risquée. |
| 5 | La section « Validation » de la PR affiche `AUDIT_APPROVED` pour les deux audits alors que le contenu final de la même PR les met en `AUDIT_CONVERTED` | **CONFIRMED** | `harness/audits.py list` rejoué sur `4c45718` : `[AUDIT_CONVERTED] (2)` liste exactement `CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois` et `CURSOR-a600532-fusion-sans-contre-audit` — pas `AUDIT_APPROVED`. Les deux lignes `AUDIT_CONVERTED` sont bien ajoutées par ce même diff (`git show 4c45718 -- architecture/audit-ledger.jsonl`, dernières lignes). Je n'ai pas pu lire le texte exact de la description de PR sur GitHub (pas d'accès API), donc je ne confirme pas le libellé mot pour mot de la section « Validation », mais l'incohérence structurelle (état affiché vs état final du même diff) est vérifiable et réelle indépendamment du libellé exact. |
| 6 | Les lignes de ledger récupérées portent l'heure du rejeu (`08:40:11Z`) et aucun pointeur vers le run d'origine | **CONFIRMED** | Les lignes ajoutées par `4c45718` portent `"timestamp": "2026-08-13T08:40:11Z"` / `08:40:26Z` / `08:40:34Z` (rejeu manuel), et aucun champ ne référence `31682710982` (le run d'origine) — vérifié en lisant chaque ligne ajoutée du diff : les clés présentes sont `timestamp, audit_id, event, actor` (+ `review`/`verdicts`/`decision`/`retained_points`/`briefs`/`archive`/`bundled` selon l'évènement), jamais de `source_run` ou équivalent. Le run d'origine `31682710982` et son horodatage `08:35:36Z` viennent du § 7 de l'audit, que je n'ai pas pu réinterroger via l'API — mais l'absence de pointeur, elle, est vérifiable directement sur le diff et confirmée. |
| 7 | Trois objets dans une PR qui plaide pour « un objet par PR » ; 704/843 lignes sont des copies identiques, ~139 lignes réellement neuves dont ~96 de gabarits | **PARTIAL** | Le nombre total (11 fichiers, 843 insertions) est exact (`git show --stat 4c45718`). Les trois fichiers archivés sont bien byte-identiques à leurs sources (`diff -q`, trois fois `IDENTIQUE`). Mais l'arithmétique détaillée est fausse : les trois fichiers archivés totalisent **722** lignes (616+88+18 via `wc -l`), pas 704 ; le contenu réellement neuf est donc **121** lignes (843−722), pas ~139. L'écart est de 18 dans les deux sens — cohérent avec le fait que le fichier `DECISION-CURSOR-3b47ffe-...md` (18 lignes, lui aussi une copie byte-identique confirmée) semble avoir été compté côté « neuf » plutôt que côté « copie ». Le calcul explicite de l'audit lui-même est d'ailleurs interne incohérent : il dit « ~139 dont ~96 de gabarits » (donc ~43 lignes de substance) puis conclut « la mesure honnête est donc : 7 lignes de ledger + 18 lignes de décision » (= 25, pas 43). Les 96 lignes de gabarits sont exactes (41+7+41+7 = 96, `wc -l` sur les 4 fichiers de graine 013/014). La conclusion qualitative (le gros du diff est mécanique ; le couplage de trois objets dans un seul verdict de fusion est le vrai problème) reste correcte et bien étayée par ailleurs ; les chiffres cités à l'appui sont à corriger. |
| 8 | Acteurs de ledger codés en dur (`"actor": "owner"`) sur des lignes émises par une machine | **CONFIRMED** | Les lignes ajoutées par `4c45718` contiennent exactement trois occurrences de `"actor": "owner"` (une `AUDIT_ARCHIVED`, deux `AUDIT_CONVERTED`) alors que la description du commit dit elle-même que l'orchestrateur a été rejoué (aucune action humaine synchrone). Correctement rattaché au point 3 (CONFIRMED, retenu) de l'audit `a4de4bb` — je n'ai pas revérifié cet audit amont mais la citation de filiation est cohérente avec le ledger (`retained_points` de `a4de4bb` inclut 1..10). |
| 9 | Le ledger déclare 8 audits `AUDIT_ARCHIVED`, mais `architecture/archive/` n'en contient que 3 après cette PR | **CONFIRMED** | `git show 4c45718:architecture/audit-ledger.jsonl \| grep -c AUDIT_ARCHIVED` → 8 (`FIXTURE`, `6231186`, `bbe6da5`, `POSTMERGE-42cb054`, `198cfd9`, `5633ee7`, `e9a6f4c`, `3b47ffe`). `git ls-tree 4c45718:architecture/archive/` → exactement 3 sous-répertoires (`3b47ffe`, `5633ee7`, `e9a6f4c`). Chiffres identiques à ceux de l'audit. |
| 10 | `harness/pipeline/ci-budget-ledger.jsonl` est vide ; aucun coût de la boucle n'est enregistré | **CONFIRMED** | `git show 4c45718:harness/pipeline/ci-budget-ledger.jsonl \| wc -c` → 1 (une ligne blanche). Identique sur le disque de travail actuel. |
| §4.1 | « Points retenus identiques au log CI » | **PARTIAL** | La partie rejouable sans API (`parse_verdicts` sur le document de revue → `{'CONFIRMED': 14, 'REFUTED': 4, 'PARTIAL': 6, 'NEEDS_OWNER': 4}`, identique aux lignes ajoutées au ledger) est confirmée. La comparaison avec le log CI du run `31682710982` lui-même n'a pas pu être rejouée (pas d'accès `gh run view --log`). |
| §4.2 | « Aucun fichier hors `architecture/` et `harness/queue/briefs/` » | **CONFIRMED** | Les 11 chemins du diff (`git show --stat 4c45718`) sont tous sous `architecture/` ou `harness/queue/briefs/`. |
| §4.3 | Les copies d'archive sont exactes, pas une duplication accidentelle | **CONFIRMED** | Trois `diff -q` identiques (voir constat 7). `harness/audit_archive.py` — non relu ligne à ligne ici, mais le comportement observé (copie, pas déplacement) est cohérent avec le nom du module. |
| §4.4 | Les graines de brief pleines de marqueurs à remplir ne sont pas consommables comme instruction ; le gate rejette le brief 013 pour la bonne raison | **CONFIRMED** | `verdict_audit.py` sur `harness/queue/briefs/013-sim-tick-nourrit-une-fois` → `[FAIL] verdict_numbers_traceable: verdict.md missing`, `VERDICT: REJECT`. Sur `012` → `VERDICT: ACCEPT`, avec `verdict.md` signé `forge-evaluateur`. Identique au § 7 de l'audit. |
| §2 | Classification CI sur `4c45718` (15 vertes, 2 ignorées, 1 en attente) | **NEEDS_OWNER** (techniquement non vérifiable ici) | Aucun accès `gh`/API GitHub dans cet environnement pour rejouer `gh pr checks 65`. Rien dans le dépôt local ne contredit ce compte ; je ne peux ni le confirmer ni le réfuter par moi-même. |
| §7 environnement | 28 audits valides après application du diff sur `master` avancé d'un commit | **CONFIRMED** | `master` à la base de la PR (`97e8e7c`) contient 27 fichiers dans `inbox/` ; le commit suivant `72a69e7` (« audit post-fusion du commit 16ff5ac (PR #60) ») en ajoute un, portant le total à 28 — exactement la méthodologie que l'audit décrit en § 5 pour expliquer l'écart avec le commit `4c45718` seul (27 audits en checkout direct, vérifié dans mon propre worktree). Pas une incohérence : l'audit documente honnêtement sa propre divergence de méthode. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- Le constat 1 (mauvais diagnostic légué à un futur brief) et le constat 4
  (gardes de portée contournables en renommant une branche) sont, à mon
  sens, les deux points où le propriétaire doit trancher une priorité —
  pas une vérité technique : les deux sont confirmés/partiels
  techniquement, la question est de savoir s'ils passent devant le
  prochain lot déjà en file plutôt que d'entrer en concurrence avec lui.
- Aucune partie de cet audit ne s'auto-attribue une autorisation
  d'implémentation (`implementation_authorized: false` etc., vérifié dans
  le frontmatter) — rien à arbitrer sur ce plan.

## 4. Synthèse

Ce qui tient sans réserve : les constats 2, 4, 5, 6, 8, 9, 10 et les
quatre points « ce qui tient » du § 4 de l'audit — tous rejoués localement
avec des résultats identiques aux chiffres cités.

Ce qui tient avec une réserve mineure : le constat 3 (le mécanisme et
l'absence de preuve sont confirmés dans le code ; le compte « 6
occurrences » devrait être 8 sauf exclusion implicite des lignes de
fixture) ; le constat 7 (la conclusion qualitative — diff dominé par des
copies mécaniques, trois objets couplés dans un seul verdict — tient, mais
l'arithmétique détaillée est fausse de 18 lignes dans les deux sens, et
l'audit se contredit lui-même entre « ~139 dont 96 de gabarits » et « la
mesure honnête est 7+18=25 »).

Ce que je n'ai pas pu vérifier moi-même : tout ce qui dépend de l'API
GitHub (horodatages de jobs, `gh pr checks`, logs de run) — cet
environnement n'a pas de jeton `gh`. Rien de ce que j'ai pu vérifier par
ailleurs (configuration des workflows, code de l'orchestrateur, contenu du
ledger, sorties des gates) ne contredit ces éléments ; l'audit lui-même
documente cette même limite en § 5 pour d'autres points, ce qui est
cohérent avec le reste de sa rigueur méthodologique.

Recommandation de traitement : aucun point ne justifie un rejet en bloc.
Les trois briefs proposés en § 6 de l'audit (checkout sur la tête de
`master`, pointeur de preuve sur chaque transition, gardes de portée
indexées sur les chemins) couvrent fidèlement les constats P1 confirmés
(1, 3, 4) et méritent d'être retenus. Avant conversion, je signalerais au
propriétaire les deux imprécisions chiffrées relevées ci-dessus (constat 3
et constat 7) — elles n'invalident aucune conclusion mais ne doivent pas
être recopiées telles quelles dans un brief si le brief cite ces chiffres
comme preuve.
