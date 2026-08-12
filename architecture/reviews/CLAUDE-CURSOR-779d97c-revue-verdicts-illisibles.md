---
review_of: CURSOR-779d97c-revue-verdicts-illisibles
reviewer: claude-code
target_commit: 779d97c8fd66d16e2bad4f81ca88d968358b96d8
reviewed_at: 2026-08-12T12:35:00Z
---

# Contre-audit de CURSOR-779d97c-revue-verdicts-illisibles

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `779d97c8fd66d16e2bad4f81ca88d968358b96d8`.
- Le commit existe et est bien un ancêtre de `master` :
  `git cat-file -t 779d97c8fd66d16e2bad4f81ca88d968358b96d8` → `commit` ;
  `git merge-base --is-ancestor 779d97c… HEAD && echo ok` → `ok`.
- Diff annoncé (2 fichiers, +108/−0) reproduit à l'identique :
  `git show 779d97c… --stat` →
  `architecture/audit-ledger.jsonl | 1 +` et
  `...SOR-73022bd-hermes-dashboard-modele-auditeur.md | 107 +++…`.
- Environnement de cette revue : pas de `GH_TOKEN`/`gh auth` disponible ici
  non plus (`gh auth status` → « not logged into any GitHub hosts »). Tout
  ce qui dépend de l'API GitHub en direct (chronologie exacte des runs § 6
  et fin de P1-4) n'a pas pu être rejoué indépendamment — noté PARTIAL /
  NEEDS_OWNER plutôt que supposé.
- Mesures rejouées : voir tableau ci-dessous, une commande par ligne
  majeure ; en plus des commandes citées par l'audit, une reproduction de
  bout en bout indépendante dans `/tmp` (inbox + reviews + ledger
  recréés, fichier de revue réel de la PR #30 copié tel quel) — détail au
  § 4 « Synthèse ».

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | § 1 — Provenance : PR #30 fusionnée par `PLiagre` (humain), diff 2 fichiers +108/−0, target_commit `73022bd` est un ancêtre de `master` | CONFIRMED | Reproduit ci-dessus (§ 1 de cette revue) : diff stat identique, `merge-base --is-ancestor` → `ok`. |
| 2 | § 2, tableau des 3 promesses (numérotation non conforme, `pipeline-orchestrate` rouge, ledger publie `REFUTED: 2` sur une revue qui dit « Aucun REFUTED ») | CONFIRMED | Fichier réel extrait (`git show 779d97c…:architecture/reviews/CLAUDE-CURSOR-73022bd-….md`) : ses lignes de tableau commencent par `\| P1-1 \|`, `\| P2-5 \|`, `\| § 2 (…) \|` — jamais un entier seul — et chaque verdict est bien en gras (`**CONFIRMED**`, `**PARTIAL**`…). La ligne ledger extraite du même commit est `{"...verdicts": {"CONFIRMED": 12, "REFUTED": 2, "PARTIAL": 4, "NEEDS_OWNER": 4}}`, alors que le corps de la revue écrit littéralement « **Aucun REFUTED.** » (ligne 98) et « **11 sont intégralement confirmés** » (ligne 75). Sur `pipeline-orchestrate` : je n'ai pas d'accès `gh` ici pour relire le run CI lui-même (voir § 1), mais j'ai reproduit indépendamment la cause mécanique exacte (point 4 ci-dessous) avec le même message d'erreur que celui cité en preuve — je considère donc ce sous-point confirmé par la mécanique, pas par la lecture directe du log CI. |
| 3 | P0-1 — Le motif `_POINT_VERDICT_ROW` (`harness/audit_decision.py:64-67`) exige une première cellule entière et une cellule de verdict sans autre texte ; les deux exigences sont violées par le fichier livré ; `_parse_point_verdicts` renvoie `[]` sur ce fichier | CONFIRMED | Citation du regex vérifiée verbatim aux lignes indiquées. Reproduction indépendante : `python3 -c "…; audit_decision._parse_point_verdicts(text)"` sur le fichier réel extrait du commit de fusion → `[]`, exactement la sortie citée par l'audit. |
| 4 | P0-1 — Le message d'erreur CI cité (`… has no '\| N \| ... \| VERDICT \| ... \|' rows; --policy auto refuses to guess a verdict`, code de sortie 2) | CONFIRMED | `grep -n "has no '\|" harness/audit_decision.py` → ligne 251-252, message identique au caractère près. Reproduction de bout en bout indépendante dans `/tmp` (inbox/reviews/ledger recréés hors du dépôt, fichier de revue réel copié) : `record_challenge` accepte et écrit `AUDIT_CHALLENGED` avec `{'CONFIRMED': 12, 'REFUTED': 2, 'PARTIAL': 4, 'NEEDS_OWNER': 4}`, puis `python3 harness/audit_decision.py auto --audit-id … --ledger …` échoue avec code de sortie 2 et exactement ce message. Je n'ai pas pu relire le run CI réel (`31594525965`) faute de `gh auth` ici, mais la reproduction locale produit une sortie identique au caractère près à celle citée — la mécanique est donc confirmée indépendamment de la lecture du log. |
| 5 | P0-1 — Perte silencieuse même après correction : `P1-3`/`P2-7` de la revue PR #30 restent perdus ; la revue précédente (`CLAUDE-CURSOR-cdc683f-…`) a bien été décidée mais avec seulement 9 lignes sur 11 captées, `retained_points: [1, 2, 5, 8, 10, 11]` omettant le point 9 | CONFIRMED | Fichier réel `architecture/reviews/CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs.md` : ligne 3 = `\| 3 \| … \| REFUTED (sur la preuve citée) \| …`, ligne 9 = `\| 9 \| … \| CONFIRMED (avec réserve sur le brief 2) \| …` — les deux verdicts nuancés tombent hors du motif. Ledger réel : `grep cdc683f architecture/audit-ledger.jsonl` → `retained_points: [1, 2, 5, 8, 10, 11]`, point 9 (pourtant CONFIRMED avec réserve) absent. `audit_decision._parse_point_verdicts` rejoué sur ce fichier → `[(1,'CONFIRMED'), (2,'PARTIAL'), (4,'REFUTED'), (5,'PARTIAL'), (6,'NEEDS_OWNER'), (7,'NEEDS_OWNER'), (8,'CONFIRMED'), (10,'CONFIRMED'), (11,'CONFIRMED')]` — 9 lignes, identique au chiffre cité par l'audit. |
| 6 | P0-2 — Deux définitions de « un verdict » : `audit_review.parse_verdicts` (mot présent n'importe où) garde l'écriture du ledger, `audit_decision._parse_point_verdicts` (ligne de table stricte) garde la décision — la porte valide une propriété plus faible que celle dont dépend son consommateur | CONFIRMED | Code lu verbatim : `audit_review.py:126-133` (`re.findall(rf"\b{token}\b", text)`, comptage de mots) vs `audit_decision.py:64-67/185-191` (regex de ligne de table). Reproduction bout-en-bout indépendante (voir point 4) : étape 1 accepte, étape 2 refuse avec le même message que celui cité par l'audit — la divergence des deux définitions est donc vérifiée mécaniquement, pas seulement lue dans le code. |
| 7 | P0-2 — Le format n'est un contrat écrit nulle part hors du gabarit `scaffold_text` (qui émet bien `\| 1 \| … \|`) et du regex d'un autre module | CONFIRMED | `audit_review.py:87` (`scaffold_text`) émet exactement `\| 1 \|` suivi de trois cellules placeholder — entier non gras, forme stricte. Aucun schéma ou documentation intermédiaire ne relie les deux modules (voir aussi point 12 sur `audit_schema.py`, qui ne couvre que l'inbox). |
| 8 | P1-3 — `parse_verdicts` compte des occurrences de mots dans tout le document ; toute revue produite par ce gabarit porte par construction +1 sur chacun des 4 jetons via la phrase de garde (« Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER », `audit_review.py:75`) et la phrase de négation (« Aucun REFUTED ») compte elle-même comme un REFUTED | CONFIRMED | `scaffold_text` ligne 75 contient bien les 4 jetons en clair. Rejoué : `parse_verdicts(text)` sur le fichier réel de la PR #30 → `{'CONFIRMED': 12, 'REFUTED': 2, 'PARTIAL': 4, 'NEEDS_OWNER': 4}`, identique au champ inscrit au ledger réel du commit de fusion. Les deux occurrences de `REFUTED` s'expliquent bien par la ligne de gabarit + la phrase « Aucun REFUTED » elle-même — vérifié par lecture du texte (ligne 11 et ligne 98 du fichier réel). |
| 9 | P1-3 — Bornage : `hermes/dashboard.py` ne lit pas le champ `verdicts` du ledger, le préjudice est limité au journal | CONFIRMED | `grep -n "verdicts" hermes/dashboard.py` → aucune occurrence (code de sortie 1). |
| 10 | P1-3 — Citation fidèle de `generator-log` du brief 006 (ligne 121) sur le sens du champ `verdicts` | CONFIRMED (paraphrase fidèle) | `harness/queue/briefs/006-full-auto-agent-pipeline/deliverables/generator-log.md:119-123` dit littéralement : « le ledger's `verdicts` field only carries *counts* per token, not which point number holds which verdict » — l'audit le paraphrase sans en déformer le sens. |
| 11 | P1-4 — Le calcul de périmètre du merge-bot (`.github/workflows/merge-bot.yml:38-39`) utilise une base mobile `origin/${BASE_REF}...HEAD`, qui peut inclure des commits fusionnés sur `master` après l'ouverture de la PR | CONFIRMED | Lignes vérifiées verbatim à l'emplacement cité : `git fetch --no-tags origin "$BASE_REF"` (l.38), `changed="$(git diff --name-only "origin/${BASE_REF}...HEAD")"` (l.39). Le commit `4a5995a "hermes: tableau de bord régénéré"` cité comme ayant fait avancer `master` pendant l'attente existe bien dans l'historique local. |
| 12 | P1-4 — `test_merge_bot_policy.py` teste l'allowlist extraite du YAML, jamais le calcul du diff ; `.github/workflows/**` figure dans `auto_merge_denylist` | CONFIRMED | `grep -nE "origin/\|BASE_REF\|\.\.\." harness/tests/test_merge_bot_policy.py` → aucune occurrence. `harness/pipeline/config.yaml:61-63` : `auto_merge_denylist:` contient bien `.github/workflows/**` en première entrée. |
| 13 | P1-4 — Chronologie exacte des événements GitHub (run créé 11:56:02Z, démarré 12:01:42Z après 5min40 de file, PR fusionnée 12:01:47Z par un humain, étape de périmètre exécutée 12:01:58Z après la fusion) | NEEDS_OWNER | Ni `gh auth` ni `GH_TOKEN` disponibles dans cet environnement de revue (`gh auth status` → non connecté) : je ne peux ni confirmer ni infirmer ces horodatages précis par ma propre mesure. Le mécanisme sous-jacent (base mobile, cf. point 11) est confirmé par le code ; la chronologie précise qui en fait la cause de ce faux-positif précis reste à vérifier par le propriétaire, qui a accès à l'historique des runs GitHub Actions. |
| 14 | P2-5 — Le smoke test `mechanical-scaffold-smoke` (`pipeline-challenge.yml:187-223`) fabrique lui-même la ligne idéale `\| 1 \| mock point \| CONFIRMED \| ci smoke, no placeholder left \|` (ligne 217) au lieu de tester le format réel produit ; les fixtures `test_audit_decision.py:205`, `test_audit_review.py:60`, `test_full_auto_pipeline.py:65-66`, `test_orchestrator.py:56` font le même choix ; la suite passe entièrement pendant que le livrable réel casse | CONFIRMED | Ligne du workflow relue verbatim, identique à la citation. Les 4 fixtures grep-ées donnent exactement les lignes citées : `\| 1 \| budget non imposé \| CONFIRMED \| …` / `\| 1 \| budget non impose \| CONFIRMED \| …` aux emplacements indiqués. Suite complète rejouée : `python3 -m pytest harness/tests/ -q` → `309 passed, 16 skipped in 7.53s`, chiffres identiques à ceux cités par l'audit. |
| 15 | P2-6 — `audit_schema.py` ne valide que l'inbox (`INBOX` l.26, `inbox.glob("CURSOR-*.md")` l.98) ; `cursor-scope` (`audit-guard.yml`) est conditionné à `startsWith(github.head_ref, 'cursor/')` donc inactif pour une branche `forge-bot/*` | CONFIRMED | Lignes relues verbatim aux emplacements cités. `audit-guard.yml:30` : `if: github.event_name == 'pull_request' && startsWith(github.head_ref, 'cursor/')` — confirmé, une branche `forge-bot/…` (celle de la PR #30) ne déclenche pas ce job. |
| 16 | P3-7 — Le coût mesuré (1,593695 USD) du run 31593583378 est jeté : `pipeline-challenge.yml` ne committe que `architecture/reviews` et `architecture/audit-ledger.jsonl` ; `harness/pipeline/ci-budget-ledger.jsonl` fait 1 octet dans le dépôt | CONFIRMED | `grep -n "git add" .github/workflows/pipeline-challenge.yml` → ligne 178, exactement `git add architecture/reviews architecture/audit-ledger.jsonl`, `ci-budget-ledger.jsonl` absent de la liste. `wc -c harness/pipeline/ci-budget-ledger.jsonl` → `1`, confirmé. Le chiffre exact « 1,593695 USD » provient d'un artefact CI (post-hoc budget marking du run 31593583378) que je ne peux pas relire ici faute d'accès `gh` — je ne peux donc pas re-mesurer ce chiffre précis, mais la mécanique qui explique sa perte (fichier non committé) est intégralement confirmée, et l'audit lui-même est explicite sur le fait que ce point est déjà confirmé par la revue qu'il audite, pas un constat nouveau. |
| 17 | § 4 — Ce que la PR tient bien (taille du diff, séparation des rôles, honnêteté sur les limites d'accès, reconstruction indépendante du P1-2) | CONFIRMED | Taille du diff revérifiée au point 1. Séparation des rôles : `architecture/agents/claude-challenger.md:27-36` (« Interdits ») confirme qu'écrire hors de `architecture/reviews/**` est interdit à ce rôle, et « Preuve de fin » (lignes 46-52) exige bien un verdict par point numéroté — la revue livrée respecte ce périmètre (2 fichiers touchés, aucun code/test/workflow). Le contenu détaillé de « l'honnêteté sur les limites » et de la reconstruction du P1-2 est vérifiable par lecture directe du fichier réel de la revue (`§ 1` : mention explicite de l'absence de `GH_TOKEN`, `§ 2` ligne `P1-2` : reconstruction via `git log` local) — cohérent avec ce que l'audit en dit. |
| 18 | § 6 — Classification CI : commit de fusion `779d97c` rouge (`pipeline-orchestrate` en échec, 5 autres verts), tête de PR `ae66c1a` rouge (`check-and-automerge` en échec, `cursor-scope` skipping, reste vert) | NEEDS_OWNER | Sans `gh auth`/`GH_TOKEN` dans cet environnement, je ne peux pas relire les runs GitHub Actions eux-mêmes pour confirmer indépendamment ce tableau. Les workflows nommés existent tous bel et bien dans `.github/workflows/` (vérifié : `audit-guard.yml`, `harness-ci.yml`, `hermes-dashboard.yml`, `hermes-observer.yml`, `pipeline-audit.yml`, `pipeline-failure-escalate.yml`, `pipeline-orchestrate.yml`, `security.yml`), et l'échec de `pipeline-orchestrate` est cohérent avec la reproduction mécanique indépendante du point 4 — mais la classification verte/rouge run par run reste à confirmer par le propriétaire, qui a accès à l'API GitHub. |
| 19 | § 6 — `auto_policy.yaml` interdit en `full_auto` « merge vers master si un workflow requis est rouge » | CONFIRMED | `grep -n "requis" harness/pipeline/auto_policy.yaml` → ligne 86, `- merge vers master si un workflow requis est rouge`, citation exacte. |
| 20 | § 8 — Les 3 briefs proposés couvrent bien les constats P0-1/P0-2 (brief 1), P2-5/P2-6 (brief 2), P1-4 (brief 3), respectent le plafond de 3 briefs par audit et ne modifient/n'autorisent rien eux-mêmes | CONFIRMED | Lecture directe : chaque brief cible exactement les fichiers et lignes déjà vérifiés dans les points ci-dessus (`audit_review.py`/`audit_decision.py` pour le brief 1, `audit_schema.py`/`pipeline-challenge.yml` pour le brief 2, `merge-bot.yml`/`test_merge_bot_policy.py` pour le brief 3). Le brief 3 note lui-même correctement qu'il touche `.github/workflows/**`, donc `auto_merge_denylist` (revérifié au point 12) — auto-évaluation cohérente. Les trois flags `*_authorized` du frontmatter de cet audit sont à `false` (vérifié en tête de fichier), et aucun des 20 points ci-dessus ne dépend d'une autorité que l'audit se serait arrogée. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **Chronologie exacte du run merge-bot sur la PR #30 (point 13)** — je n'ai
  pas d'accès `gh`/API GitHub dans cet environnement de revue pour
  reconfirmer les horodatages précis (`createdAt`/`startedAt`/`mergedAt`/log
  de l'étape de périmètre). Le mécanisme causal (base `origin/${BASE_REF}...HEAD`
  mobile) est, lui, confirmé par lecture directe du code. Si une garantie
  indépendante de la chronologie est nécessaire avant d'ouvrir le brief 3,
  elle doit venir d'un accès `gh` authentifié.
- **Classification CI complète du § 6 (point 18)** — même limitation : la
  liste des workflows déclenchés est vérifiée, mais pas le statut
  succès/échec de chaque run pris individuellement. La partie qui compte le
  plus pour la décision (`pipeline-orchestrate` en échec) est corroborée
  indépendamment par la reproduction mécanique du point 4, ce qui donne une
  confiance élevée même sans lecture directe du log CI.
- **Arbitrage déjà signalé par l'audit lui-même (P3-7, point 16)** — l'audit
  ne propose pas de brief pour ce point et le traite comme déjà tranché par
  une revue antérieure ; je ne rouvre pas cet arbitrage, je confirme
  seulement que le mécanisme de perte (fichier non committé par la CI) est
  réel.

## 4. Synthèse

Ce qui tient : les deux constats P0 sont la partie la plus solide de cet
audit et j'ai pu les reproduire **mécaniquement**, pas seulement les lire —
sur le fichier réel de la PR #30 extrait du commit de fusion, puis dans une
reproduction de bout en bout entièrement hors du dépôt (`/tmp`) :
`record_challenge` accepte et écrit `AUDIT_CHALLENGED` avec
`{'CONFIRMED': 12, 'REFUTED': 2, 'PARTIAL': 4, 'NEEDS_OWNER': 4}`, puis
`audit_decision.py auto` refuse aussitôt après avec code de sortie 2 et le
message exact cité par l'audit (« has no '\| N \| ... \| VERDICT \| ... \|'
rows »). C'est la meilleure preuve possible que l'écart entre les deux
définitions de « un verdict » (P0-2) est réel et pas seulement une lecture
de code : deux fonctions du même dépôt, appliquées au même fichier, donnent
des réponses incompatibles, et c'est la plus permissive qui garde
l'écriture du ledger. La perte silencieuse des cellules nuancées (P0-1,
`P1-3`/`P2-7` du fichier PR #30, et rétrospectivement les points 3/9 de la
revue `cdc683f`) est vérifiée de la même façon, sur un cas réel déjà
tranché par la policy. P1-3 (comptage de mots plutôt que de verdicts),
P2-5 (fixtures et smoke test au seul format idéal) et P2-6 (schéma
asymétrique inbox/reviews) sont tous confirmés par lecture directe du code
aux emplacements cités, sans écart avec l'audit.

Ce qui reste hors de portée de cette revue : uniquement ce qui dépend d'un
accès `gh`/API GitHub en direct — la chronologie précise du run merge-bot
(point 13) et la classification exacte des checks CI (point 18). Ces deux
points sont marqués `NEEDS_OWNER` non parce qu'ils sont douteux, mais parce
que cet environnement de revue n'a pas les moyens de les recontrôler
indépendamment ; le mécanisme causal qui les sous-tend (base de diff mobile
pour l'un, échec réel de `pipeline-orchestrate` pour l'autre) est, lui,
confirmé.

Aucun point de cet audit ne s'effondre à la reproduction. Recommandation :
traiter les points P0-1/P0-2 comme la priorité — ils bloquent aujourd'hui
même la boucle (`CURSOR-73022bd-hermes-dashboard-modele-auditeur` reste
`AUDIT_CHALLENGED` dans le ledger réel de ce dépôt, vérifié à la date de
cette revue) — et laisser au propriétaire le soin de fixer la chronologie
exacte du § 6/P1-4 avant, le cas échéant, d'autoriser le brief 3.
