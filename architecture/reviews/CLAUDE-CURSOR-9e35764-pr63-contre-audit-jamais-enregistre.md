---
review_of: CURSOR-9e35764-pr63-contre-audit-jamais-enregistre
reviewer: claude-code
target_commit: 9e35764e4dc3ce0f88c20b22fa22633f85754d61
reviewed_at: 2026-08-13T08:56:47Z
---

# Contre-audit de CURSOR-9e35764-pr63-contre-audit-jamais-enregistre

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

Environnement de cette revue : `gh` non authentifié (`gh auth login` requis,
absent), mais l'API GitHub publique répond en HTTP anonyme
(`curl https://api.github.com/...`) — utilisée pour re-vérifier tout ce qui
touche PR #63, ses check-runs et les runs `pipeline-orchestrate`. Les logs
bruts de job (`.../actions/jobs/<id>/logs`) sont, eux, HTTP 403 sans jeton :
non rejouables ici, noté où ça compte.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `9e35764e4dc3ce0f88c20b22fa22633f85754d61`. Confirmé
  ancêtre de `master` : `curl .../compare/9e35764...master` → `"status":
  "ahead"` (master est en avance sur ce commit, donc ce commit est bien un
  ancêtre — même conclusion que l'audit, rejouée indépendamment plutôt que
  recopiée).
- `gh api pulls/63` (anonyme) confirme mot pour mot les métadonnées citées :
  `created_at: 2026-08-13T08:34:40Z`, `merged_at: 2026-08-13T08:35:09Z`
  (29 s), `merge_commit_sha: 9e35764e...`, `head.sha: 25b3185...`,
  `draft: false`, `changed_files: 1`, titre identique.
- Mesures rejouées de fond en fond, sans recopier les chiffres de l'audit :
  voir § 2 points P0-1, P1-1, § 8.1 (double comptage nourriture, ordre de
  transport) — toutes confirmées à l'identique.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | P0-1 — la revue livrée par la PR n'entre jamais au registre (`architecture/audit-ledger.jsonl` sur `master` ne contient aucune ligne `a4de4bb`) ; le run `pipeline-orchestrate` a échoué à l'étape de poussée sur un conflit de rebase | **CONFIRMED** | Vérifié en local sur ce dépôt : `grep -c a4de4bb architecture/audit-ledger.jsonl` → `0` (40 lignes au total, identique au compte de l'audit), et `architecture/reviews/CLAUDE-CURSOR-a4de4bb-*.md` existe bien sur `master` (124 lignes, compte identique). Via API : `gh api .../actions/workflows/pipeline-orchestrate.yml/runs` → run `31682710982` (push, sha `9e35764e`) `conclusion: failure`, tandis que le run jumeau `31682696284` (sha `96d15654`, PR #62) est `success` — deux runs à 10 s d'écart, un seul a survécu, exactement l'asymétrie décrite. `gh api .../actions/runs/31682710982/jobs` confirme l'étape précise qui a échoué : `"Commit ledger/decision/brief-seed update": failure` (toutes les étapes précédentes `success`). Limite : les logs bruts de cette étape (le texte `CONFLICT (content)` et le commit local `508ef8e`) sont en `HTTP 403` sans jeton — non rejoués ici. Le faisceau (étape de poussée en échec + registre réellement vide de l'entrée + run jumeau réussi + comparaison de code source pour le mécanisme de rebase) rend la cause "conflit de rebase sur le ledger" hautement probable et cohérente avec `.github/workflows/pipeline-orchestrate.yml` lignes 133-138 (citation vérifiée mot pour mot) et le raisonnement qui la contredit (le `concurrency group` sérialise les *exécutions*, pas les *bases* d'un `push`) : raisonnement correct sur la mécanique GitHub Actions (`actions/checkout` fixe l'arbre au SHA du push, pas à `HEAD` de `master` au moment du run). |
| 2 | P0-1 (suite) — conséquence : le P0 moteur confirmé par le contre-audit perdu (« la nourriture transférée nourrit deux fois ») ne devient jamais un brief par la boucle automatique, faute d'événement au registre | **PARTIAL** | Le mécanisme est confirmé : `harness/pipeline/auto_policy.yaml` règle `review_has_confirmed_or_partial` (ligne 37, vérifiée) ne peut se déclencher sans événement `AUDIT_CHALLENGED`, absent ici. Mais l'audit sous-entend (§0, « sauf intervention ») que la perte reste ouverte au moment de sa publication — or une PR de récupération existait déjà **avant** l'horodatage `created_at` de l'audit (09:05:00Z) : PR/issue #65 (`forge/boucle-audits-post-pr60-ddda`), ouverte à `08:41:29Z` par un agent Cursor sous le compte du propriétaire, dont le corps annonce explicitement rejouer localement la même transition (`AUDIT_CHALLENGED` + `AUDIT_APPROVED`, points 1 à 10) et semer les briefs `013-sim-tick-nourrit-une-fois` / `014-pipeline-contre-audit-porte`. Cette PR est **encore ouverte, non fusionnée**, au moment de cette revue (`gh api pulls/65` → `state: open`, `merged: false`) — donc le registre de `master` est toujours vide de `a4de4bb` aujourd'hui, ce qui confirme la portée immédiate du constat. Mais la phrase de l'audit § « Visibilité de la panne » (« Aucune trace ouverte de l'incident du 13/08 ») est vraie seulement au sens strict d'`gh issue list` (qui exclut les PR par construction) — il existe bien une trace ouverte, sous forme de PR, retrouvée 24 minutes avant l'écriture de l'audit. Ce n'est pas une erreur factuelle de l'audit (il a fait exactement la vérification qu'il décrit, avec l'outil qu'il décrit), mais l'implication « ça restera invisible sauf intervention » est déjà en grande partie caduque au moment de la publication : l'intervention avait commencé. |
| 3 | P0-2 — auto-fusion 29 s après ouverture, sans 2 des 4 preuves exigées par la porte conditionnelle du 2026-08-11 (CI verte absente, audit Cursor déposé absent) | **CONFIRMED** | Horodatages re-vérifiés via API (§ 1) : 29 s pile entre `created_at` et `merged_at`. `gh api commits/25b3185.../check-runs` (anonyme) reproduit exactement le tableau de l'audit : `Reconcile local Hermes state` `queued`, `cursor-scope` `skipped`, `invoke-cursor-auditor` `success` (15 s), `gitleaks`/`actionlint`/`sim-tests`/`tests`/`f0-demo`/`schema`/`check-and-automerge` `success`, `Reconcile local Hermes state` `cancelled`. `gh api commits/25b3185.../status` → `"state": "pending"`, pas vert, confirmé au mot près. `architecture/decisions/DECISION-CURSOR-e9a6f4c-codex-passation-full-auto.md` lignes 30-42 : citation vérifiée mot pour mot (« quatre preuves », « aucune étape de cette porte ne peut être rendue facultative sans une nouvelle décision écrite »). `.github/workflows/merge-bot.yml` ligne 50 (le grep de chemins) et ligne 71 (`gh pr merge --auto --squash`) : lignes vérifiées exactement — la garde ne compare que des chemins de fichiers, jamais les quatre preuves de la décision. `docs/adr/0010-...md` ligne 62 cite bien « arbitrages n°1 (porte conditionnelle de fusion) », vérifié. |
| 4 | P1-1 — le registre aurait publié 14 CONFIRMED / 4 REFUTED pour une revue qui confirme 9 points et n'en réfute aucun (parseur de mots sur tout le texte vs parseur de lignes de tableau) | **CONFIRMED** | Rejoué avec le code réel du dépôt, sur le fichier réellement présent sur `master` (`architecture/reviews/CLAUDE-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois.md`) : `harness.audit_review.parse_verdicts(text)` → `{'CONFIRMED': 14, 'REFUTED': 4, 'PARTIAL': 6, 'NEEDS_OWNER': 4}` ; `harness.audit_decision.parse_point_verdicts(text)` → 9× CONFIRMED (points 1-9), 1× PARTIAL (point 10) — identique aux deux comptages publiés par l'audit, aux chiffres près. `grep -n "Aucun point n'est REFUTED"` dans la revue → présent (§4, ligne 92) : le document lui-même dément le compte que le registre aurait pris. `harness/audit_review.py` ligne 174 (`verdicts = parse_verdicts(text)`) et ligne 203 (`verdicts=verdicts`) confirmées : c'est bien le premier parseur, celui par mot, qui part au registre. Cause confirmée : la ligne de légende « Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER. » (ligne 11 du document) est comptée comme quatre occurrences par le parseur de mots. |
| 5 | P1-2 — la cause annoncée de l'ouverture manuelle de la PR (réglage GitHub « Allow Actions to create PRs » inactif) est démentie par le journal, qui montre un refus de permission du PAT (`Resource not accessible by personal access token`) ; l'étape « Publish the review... » conclut `success` sans avoir publié | **PARTIAL** | `.github/workflows/pipeline-challenge.yml` ligne 174 (`GH_TOKEN: ${{ secrets.FORGE_BOT_PAT \|\| secrets.GITHUB_TOKEN }}`) et ligne 201 (`\|\| echo "::warning::gh pr create refused..."`) : vérifiées mot pour mot — le mécanisme « l'échec de `gh pr create` est avalé et l'étape reste verte » est réel et bien à ces lignes. Le contenu exact du message de log (`Resource not accessible by personal access token`) provient du run `31681378615`, dont les logs bruts sont `HTTP 403` sans jeton dans cet environnement — non rejoué directement ici, donc je ne peux pas confirmer au mot près que c'est *ce* message précis qui est apparu plutôt qu'un autre refus de permission. Le raisonnement qui en découle (un PAT à portée fine sans « Pull requests: write » produit ce message, pas le réglage « Allow GitHub Actions to create and approve pull requests ») est correct en général mais reste, pour cette occurrence précise, un point que je ne peux pas rejouer moi-même faute d'accès aux logs. |
| 6 | P1-3 — le maillon `challenge` tourne sur `claude-sonnet-5` sans plancher de modèle écrit, alors que le maillon voisin `pipeline-audit` en impose un (Opus) pour la même classe de risque | **CONFIRMED** | `.github/workflows/pipeline-challenge.yml` ligne 152 : `claude -p "/forge-audit-review ${AUDIT_ID}"` suivi de `--permission-mode`, `--max-turns`, `--max-budget-usd`, `--output-format` — aucun `--model`, vérifié en lisant les lignes 152-157 en entier. `.github/workflows/pipeline-audit.yml` lignes 105-113 : citation vérifiée mot pour mot (« le propriétaire exige au moins Opus pour la critique (2026-08-12 — le défaut claude-4.5-sonnet du premier tour était trop faible) ») avec la sélection outillée qui suit (interrogation `GET /v1/models`, préférence opus/thinking). L'asymétrie de gouvernance est réelle et vérifiée dans le code, indépendamment de la question (hors compétence de cette revue) de savoir si `claude-sonnet-5` est en pratique suffisant. |
| 7 | P2-1 — le point 10 de la revue perdue n'a pas pu être rejoué faute de `GH_TOKEN` dans l'étape d'invocation, alors que l'étape suivante du même job le reçoit bien ; le jeton existe donc « à quelques lignes de là » | **PARTIAL** | Vérifié dans le fichier workflow : l'étape « Invoke claude-challenger headless » (lignes 145-149) ne reçoit que `CLAUDE_CODE_OAUTH_TOKEN` et `ANTHROPIC_API_KEY` ; l'étape « Publish the review as a pull request » (lignes 171-175) reçoit bien `GH_TOKEN`. Le fait de fond (jeton présent dans le job, absent de l'étape qui en aurait besoin pour un `gh` en direct) est confirmé. Mais entre les deux, il y a une étape intermédiaire (« Post-hoc budget marking », lignes 159-169, sans `GH_TOKEN` non plus) : ce n'est donc pas « l'étape suivante » au sens strict, et l'écart réel dans le fichier workflow est de 25 lignes, pas de « trois lignes » — probablement une distance mesurée dans le rendu du journal de run (non accessible ici, `HTTP 403`) plutôt que dans le fichier source. Le point de fond n'est pas affaibli par cette imprécision de formulation. |
| 8 | P2-2 — critère de verdict incohérent entre points 6/8 (rejeu partiel, faute de temps) → CONFIRMED, et point 10 (rejeu partiel, faute d'authentification GitHub) → PARTIAL, pour une preuve de même forme | **CONFIRMED** | Lu directement dans `architecture/reviews/CLAUDE-CURSOR-a4de4bb-*.md` (§2 de la présente revue, lignes 42/44/46 du document source) : point 6 dit « Je n'ai pas rejoué la mesure d'ampleur sur le monde réel [...] faute de temps machine » et conclut CONFIRMED ; point 8 dit « Je n'ai pas de position indépendante sur le seuil "~5 fichiers" » et conclut CONFIRMED ; point 10 dit « cet environnement de revue n'a pas d'authentification GitHub » et conclut PARTIAL. Trois formes de rejeu incomplet, deux verdicts différents — l'incohérence de critère est réelle et objectivement lisible dans le texte, indépendamment de l'honnêteté du document (que l'audit crédite par ailleurs, à raison). |
| 9 | § 8.1 — substance rejouée indépendamment par l'audit : double comptage nourriture (+200 kg d'écart) et dépendance à l'ordre du fichier d'adjacence (`{c1:800,c2:0,c3:200}` vs `{c1:800,c2:200,c3:0}`) | **CONFIRMED** | Sonde réécrite indépendamment dans cette revue (sans copier le code de l'audit), directement contre `sim/engine.py` : après `_apply_consumption` sur une cellule à 100 hab. (besoin 200 kg, stock 0) → `stock=0.0 deficit=200.0` ; après `_apply_commerce` (transfert de 200 kg) → `stock=200.0 deficit=0.0`, soit +200 kg vs un témoin qui aurait payé sa ration (`stock=0.0 deficit=0.0`) — chiffres identiques à ceux de l'audit. Chaîne 1—2—3 (seule 1 a du stock, 3 non adjacente à 1) : ordre `[c1-c2, c2-c3]` → `{c1: 800.0, c2: 0.0, c3: 200.0}` ; ordre `[c2-c3, c1-c2]` → `{c1: 800.0, c2: 200.0, c3: 0}` — identiques à ceux de l'audit. Cause confirmée en lisant `sim/engine.py` : `_apply_commerce` crédite `food_stock_kg` **et** décrémente `food_deficit_kg` sur le même transfert (lignes 93-95 côté a→b, 105-107 côté b→a), sans que le stock reçu soit re-consommé avant le prochain tick ; la boucle `for edge in world.adjacency` mute les cellules en place, donc une cellule peut redonner sur l'arête suivante du même tick ce qu'elle vient de recevoir. `python3 harness/harness_audit.py` → `SCORE: 20/24`, identique au chiffre cité. |
| 10 | § 9 — tableau des sévérités (2×P0, 3×P1, 2×P2, 1×P3) et § 10 — trois briefs atomiques proposés comme proposition, pas instruction | **CONFIRMED** | Le tableau récapitule fidèlement les constats détaillés aux § 3-8 du document (tous vérifiés séparément ci-dessus) sans en ajouter ni en retrancher. La formule « proposition, pas instruction » est cohérente avec le frontmatter (`implementation_authorized: false`, `code_changes_authorized: false`) et avec `architecture/README.md` (« Un seul rôle écrit dans chaque dossier », vérifié) : cet audit ne revendique aucune autorité d'exécution. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **Plancher de modèle pour le maillon `challenge`** (point 6 / P1-3) : le
  fait technique (aucun `--model`, aucun plancher écrit) est établi ;
  décider s'il faut imposer Opus (comme pour `pipeline-audit`) ou si
  `claude-sonnet-5` est acceptable pour ce rôle relève d'un arbitrage
  coût/qualité du propriétaire, pas d'un audit ni de cette revue.
- **Permission du PAT `FORGE_BOT_PAT`** (point 5 / P1-2) : si le message de
  log exact se confirme (« Resource not accessible by personal access
  token »), la correction est un réglage de jeton hors du dépôt (portée
  « Pull requests: write » manquante) — seul le propriétaire peut la faire.
  Cette revue n'a pas pu rejouer le log brut pour confirmer le message au
  mot près (§2 point 5) ; à vérifier avant d'agir sur ce diagnostic.
- **Rattrapage de l'événement `a4de4bb` perdu** (points 1-2) : une PR de
  récupération (#65) existe déjà, ouverte par un agent sous le compte du
  propriétaire, encore non fusionnée au moment de cette revue. Le
  propriétaire est le mieux placé pour décider s'il la fusionne telle
  quelle, l'ajuste, ou attend un correctif structurel (le brief 1 proposé
  par l'audit) avant de rejouer l'événement.
- **Runner auto-hébergé `Reconcile local Hermes state`** : mentionné par
  l'audit comme risque pour une preuve future de CI verte — décision de
  configuration hors du dépôt, propriétaire seul compétent.

## 4. Synthèse

Le contenu technique de cet audit tient. J'ai rejoué indépendamment, sans
recopier ses chiffres, les deux mesures les plus significatives (le double
comptage de nourriture et la dépendance à l'ordre du fichier d'adjacence,
point 9) et j'obtiens des résultats identiques aux décimales près. J'ai
aussi rejoué son propre exercice de reproduction sur le contre-audit perdu
(parseur de mots vs parseur de lignes de tableau, point 4) avec le code
réel du dépôt et je retrouve exactement ses comptes (14/4/6/4 contre 9
CONFIRMED / 1 PARTIAL). Toutes les citations de code et de documents
vérifiées (workflows, décisions, ADR, `audit_review.py`) le sont au numéro
de ligne près.

Deux nuances, aucune ne renverse un constat :

1. Le cœur du P0-1 (le registre de `master` ne contient toujours, à
   l'instant de cette revue, aucune trace de `a4de4bb`) est confirmé sans
   réserve. Mais l'implication « ça restera perdu sauf intervention » est
   partiellement caduque dès la publication de l'audit : une PR de
   récupération (#65) avait déjà été ouverte 24 minutes plus tôt par un
   agent sous le compte du propriétaire — invisible à la recherche `gh
   issue list` de l'audit (qui exclut structurellement les PR), mais bien
   présente. Ce n'est pas une erreur de l'audit ; c'est un fait survenu
   entre le moment où il a fait sa recherche et le moment où je fais la
   mienne, et il change la lecture du risque résiduel (point 2, § 3).
2. Trois points (P1-2, P2-1) reposent sur le contenu exact de logs de run
   dont l'accès brut est `HTTP 403` sans jeton dans cet environnement de
   revue — je n'ai pas pu les rejouer au mot près et je le dis explicitement
   à chaque endroit concerné, plutôt que de les recopier sans les vérifier.
   Le mécanisme sous-jacent de chacun (étape verte qui avale un échec ;
   jeton présent dans le job mais pas dans l'étape qui en a besoin) est,
   lui, confirmé directement dans le code des workflows.

Recommandation : traiter comme un audit techniquement solide. Les trois
briefs atomiques proposés (§10 de l'audit) restent une proposition
raisonnable ; leur adoption, et l'articulation avec la PR #65 déjà en vol,
relèvent du propriétaire (§3 ci-dessus).
