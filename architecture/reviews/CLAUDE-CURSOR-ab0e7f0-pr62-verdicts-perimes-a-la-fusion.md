---
review_of: CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion
reviewer: claude-code
target_commit: ab0e7f0c0a2b7bf313e9cc8d86b8188eb143072e
reviewed_at: 2026-08-13T08:57:10Z
---

# Contre-audit de CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

Environnement de cette relecture : `gh` n'est pas authentifié ici. Toutes
les vérifications GitHub (PR #62, jobs de workflow) ont été refaites via
l'API REST publique non authentifiée (`curl https://api.github.com/...`),
qui fonctionne en lecture seule sur ce dépôt public — même source de
données que `gh`, chemin d'accès différent. `pytest` n'était pas installé
au démarrage de cette relecture ; installé localement (`pip install pytest`,
aucune modification du dépôt) pour rejouer §6 de l'audit.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `ab0e7f0c0a2b7bf313e9cc8d86b8188eb143072e` — **existe**.
  Non présent dans l'historique local (dépôt non superficiel : `git
  rev-parse --is-shallow-repository` → `false`) avant `git fetch origin
  ab0e7f0...`, qui le résout aussitôt : commit unique, auteur `forge-bot`,
  `2026-08-13T06:32:51Z`, +107/−0 sur
  `architecture/reviews/CLAUDE-CURSOR-a600532-fusion-sans-contre-audit.md`.
  Squashé dans `96d1565` (confirmé : `ab0e7f0` n'est **pas** ancêtre
  d'`origin/master`, mais `96d1565` y est, avec le même diff).
- Mesures rejouées : chronologie GitHub (API REST publique), git log /
  merge-base sur la péremption du point 12, `parse_verdicts` /
  `parse_point_verdicts` sur le fichier réel de la PR, contenu du registre
  sur `master`, inventaire des 13 briefs et dernier verdict de chacun,
  `audit_decision.py` (ordre des règles), `orchestrator.py:146`,
  `dashboard.py:235`, `harness/tests/` (pytest). Détail par constat en § 2.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | P0-1 — PR #62 fusionnée 31 s après ouverture, chronologie à la seconde (job auditeur 08:34:31→08:34:51, auto-merge armé 08:34:42, fusion 08:34:57) | CONFIRMED, avec une imprécision interne à corriger | `GET /pulls/62` : `created_at 08:34:26Z`, `merged_at 08:34:57Z`, `merge_commit_sha 96d15654...` — identiques. `GET /actions/runs/31682657161/jobs` : `invoke-cursor-auditor` `started_at 08:34:31Z`, `completed_at 08:34:51Z`, `success` — identique. `GET /actions/runs/31682657145/jobs` (`merge-bot`) : `check-and-automerge` `08:34:32Z→08:34:45Z`, `success`. Timeline de la PR (`GET /issues/62/timeline`) : `auto_squash_enabled 08:34:42Z` — identique au tableau de l'audit. Chronologie donc reproduite à la seconde près, par un chemin d'accès différent (API publique non authentifiée vs `gh`). Imprécision : le § 1 résume « fusionnée … 6 secondes après le lancement du job » — 08:34:57 − 08:34:31 (lancement) = 26 s, pas 6 s ; les 6 s séparent la fusion (08:34:57) de la **fin** du job (08:34:51), pas son lancement. Le tableau chronologique (§ 3, ligne par ligne) est correct et sans ambiguïté ; seule la phrase de résumé du § 1 confond « lancement » et « fin ». Le constat structurel (le maillon ne pouvait pas peser) ne dépend pas de ce chiffre et reste entier. |
| 2 | P1-1 — verdict `CONFIRMED` périmé (point 12, `sim/tests/` hors CI) entré dans la décision automatique via `policy:auto` | CONFIRMED | `git merge-base ab0e7f0 origin/master` → `4acb8e2` (`2026-08-13T06:26:37Z` local, ≡ 08:26:37+02:00 — cohérent avec « base de la revue, 06:26:37Z »). `git log --oneline -S"sim-tests" -- .github/workflows/harness-ci.yml` → un seul commit, `444ec45`. `git merge-base --is-ancestor 444ec45 ab0e7f0` → **NON** (sim-tests n'existait pas sur la base mesurée). `git merge-base --is-ancestor 444ec45 d61f02d` (post-fusion du brief 012, avant l'ouverture de la PR #62) → **OUI**. `git show ab0e7f0:.github/workflows/harness-ci.yml \| grep sim` → aucune occurrence (cohérent avec ce que la revue a mesuré). État courant : `harness-ci.yml` contient bien le job `sim-tests` (`pytest sim/tests/ -v`). Registre réel sur `master` (`grep a600532-fusion… architecture/audit-ledger.jsonl`) : `AUDIT_APPROVED` par `policy:auto`, `retained_points: [1,…,16,18]` — le point 12 y est bien inclus. Non rejoué : le run GitHub réel de `sim-tests` sur la PR #62 (`gh pr checks 62`, hors de portée sans authentification) ; le raisonnement git (merge-base + `-S`) suffit déjà à établir la péremption. |
| 3 | P1-2 — description PR (« 11 CONFIRMED, 1 PARTIAL ») ≠ tableau du fichier (13/4/1) ≠ registre (16/2/6/2), et le seul `REFUTED` n'apparaît pas dans la description | CONFIRMED | `GET /pulls/62` → `body` contient verbatim « verdicts par point : 11 CONFIRMED, 1 PARTIAL ». Comptage indépendant du tableau réel (18 lignes de `CLAUDE-CURSOR-a600532-fusion-sans-contre-audit.md` tel que fusionné) : 13 `CONFIRMED` (lignes 1,2,4,5,6,7,8,9,10,11,12,13,16), 4 `PARTIAL` (3,14,15,18), 1 `REFUTED` (17) — exactement 13/4/1, aucun REFUTED mentionné dans la description. Trois surfaces, trois chiffres, confirmé. |
| 4 | P2-1 — `harness/audit_review.py:parse_verdicts` compte des mots dans tout le texte (gabarit inclus), pas les verdicts du tableau ; c'est ce compte qui part au registre (16/2/6/2) | CONFIRMED | Rejeu direct : `audit_review.parse_verdicts(text)` sur le fichier réel de la PR → `{'CONFIRMED': 16, 'REFUTED': 2, 'PARTIAL': 6, 'NEEDS_OWNER': 2}`. `audit_decision.parse_point_verdicts(text)` → 18 lignes, `Counter({'CONFIRMED': 13, 'PARTIAL': 4, 'REFUTED': 1})`. Registre réel (`architecture/audit-ledger.jsonl`, ligne `AUDIT_CHALLENGED` de `a600532-fusion-sans-contre-audit`) : `"verdicts": {"CONFIRMED": 16, "REFUTED": 2, "PARTIAL": 6, "NEEDS_OWNER": 2}` — identique au calcul local. La cause mécanique (comptage de mots sur tout le texte, y compris la phrase de gabarit « CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER ») est vérifiée en lisant `harness/audit_review.py:127-134` (`re.findall(rf"\b{token}\b", text)` sur tout `text`). |
| 5 | P2-2 — le § 4 (synthèse) compte 16 points (« 13 CONFIRMED sans réserve, 1 CONFIRMED avec réserve, 2 PARTIAL ») alors que le tableau porte 18 lignes ; la ligne 3 est `PARTIAL` dans le tableau et « CONFIRMED avec réserve » dans la prose | CONFIRMED | Lecture directe du § 4 du fichier fusionné : « sur 16 points … 13 sont CONFIRMED sans réserve, 1 est CONFIRMED avec une réserve mineure … et 2 sont PARTIAL » = 13+1+2 = 16, en excluant silencieusement les lignes 17 (REFUTED) et 18 (PARTIAL, sources externes) du décompte. Ligne 3 du tableau : verdict `PARTIAL` littéral ; la prose du § 4 la redécrit comme la « réserve mineure » comptée à part du bloc PARTIAL — les deux surfaces du même fichier donnent deux inventaires non superposables, confirmé exactement comme décrit. |
| 6 | P2-3 — `harness/audit_schema.py` ne valide que `architecture/inbox/**`, rien sur `architecture/reviews/**` ; le frontmatter `reviewed_at: 2026-08-13T07:15:00Z` du fichier audité est postérieur de 42 min au commit qui le contient (`06:32:51Z`) | CONFIRMED | `grep -n "INBOX\|glob(" harness/audit_schema.py` → `INBOX = REPO_ROOT/"architecture"/"inbox"`, `validate_inbox` itère `inbox.glob("CURSOR-*.md")` — aucune fonction équivalente pour `reviews/`. `.github/workflows/audit-guard.yml`, job `schema` → `run: python harness/audit_schema.py` (seule porte de schéma du dépôt, et elle ne couvre pas `reviews/`). Fichier réel : `reviewed_at: 2026-08-13T07:15:00Z` vs commit `2026-08-13T06:32:51Z` → écart de 42 min 9 s, un horodatage de relecture postérieur à l'écriture du fichier qui le porte — impossible si écrit honnêtement au moment de la relecture, confirmé comme écrit à la main sans contrainte mécanique. |
| 7 | P2-4 — la revue teste la moitié de la citation § 9 (« aucun brief ouvert ») et re-cite sans tester l'autre moitié (« chacun ACCEPT ») ; 4 briefs (001, 002, 005, 007) portent en réalité `REJECT` comme dernier verdict tracé | CONFIRMED | Rejeu indépendant du dernier verdict de chacun des 13 dossiers de `harness/queue/briefs/**` (dernière occurrence de `VERDICT: (ACCEPT\|REJECT)` par fichier, pas la première — un brief peut avoir plusieurs itérations) : `001 REJECT, 002 REJECT, 003 ACCEPT, 004 ACCEPT, 005 REJECT, 006 ACCEPT, 007 REJECT, 008-contexte-opus5-right-sizing AUCUN verdict.md, 008-full-auto ACCEPT, 009 ACCEPT, 010 ACCEPT, 011 ACCEPT, 012 ACCEPT` — identique ligne pour ligne à la table du § 5(e) de l'audit, et 4 `REJECT` exactement (001, 002, 005, 007). `008-contexte-opus5-right-sizing` confirmé ouvert (ni `verdict.md` ni `deliverables/`) et sa Success Condition 1 confirmée non réalisée (`docs/rules/prompt-defense-baseline.md` absent, bloc « Prompt Defense Baseline » présent dans les 3 `.claude/agents/forge-*.md`). |
| 8 | P2-5 — `audit_decision.decide_auto` applique la règle « points retenus → APPROVED » avant de regarder `has_needs_owner`, donc une question de gouvernance nommée en prose sans ligne `NEEDS_OWNER` dans le tableau ne bloque jamais la décision automatique | CONFIRMED | `grep -n "if retained:\|if has_needs_owner:" harness/audit_decision.py` → `if retained:` ligne 283 (bloc `review_has_confirmed_or_partial`, `return`), `if has_needs_owner:` ligne 292, **après** et donc jamais atteint si `retained` est non vide — exactement `audit_decision.py:283-290` cité par l'audit. Le fichier audité n'a aucune ligne de tableau à `NEEDS_OWNER` (18 lignes, verdicts `CONFIRMED/PARTIAL/REFUTED` uniquement), donc la question du § 3 (arbitrage à quatre acteurs) ne pouvait mécaniquement pas remonter. |
| 9 | P3-1 — la ligne 18 du tableau attribue les sources S1–S6 au « § 9 », alors qu'elles sont au § 10 ; le § 9 est bien l'endroit de la déclaration de non-duplication citée en ligne 17 | CONFIRMED | `grep -n "^# [0-9]" architecture/inbox/CURSOR-a600532-fusion-sans-contre-audit.md` → `# 9. Veille comparative` à la ligne 597, `# 10. Sources externes` à la ligne 651. La déclaration de non-duplication (« Les douze briefs … Aucun n'est ouvert ») est aux lignes ~638-647, donc bien dans le § 9 (597-650). Le tableau S1-S6 est après la ligne 651, donc au § 10. Le fichier audité (ligne 18 de son propre tableau) écrit littéralement « § 9 sources externes S1-S6 » — erreur d'attribution confirmée, sans conséquence sur un verdict (imprécision de citation, pas une preuve fausse). |
| 10 | § 5(e) — les rejeux cités comme preuves de fond fonctionnent réellement (`orchestrator.py:146`, `dashboard.py:235`, `pytest harness/tests/`) | CONFIRMED | `sed -n '146p' harness/pipeline/orchestrator.py` → `return {"action": "no_op", "reason": "no audit_id in payload; AUDIT_PROPOSED is optional"}` — verbatim identique. `grep -n "AUDIT_APPROVED" hermes/dashboard.py` → ligne 235 : `if audit["event"] in ("AUDIT_APPROVED",):` — exact. `pip install pytest` (environnement de départ sans pytest) puis `python3 -m pytest harness/tests/ -q` → `314 passed, 16 skipped in 7.84s` — identique au chiffre cité par l'audit. `python3 harness/audit_schema.py` → « All 30 audit(s) valid » contre « All 28 audit(s) valid » cité par l'audit : dérive attendue (2 audits déposés dans le dépôt entre la mesure de l'audit et cette relecture, dont cet audit-ci et sa propre revue), pas une divergence de méthode. |
| 11 | § 9 (non-duplication de l'audit lui-même) — `CURSOR-779d97c` P1-3 et `CURSOR-063d7eb` P1-2/P2-5/P2-6/P3-8 couvrent bien les motifs récurrents cités, et restent non arbitrés | CONFIRMED | `grep "779d97c" architecture/audit-ledger.jsonl` → une seule ligne, `AUDIT_CHALLENGED`, aucune ligne `AUDIT_APPROVED`/`AUDIT_REJECTED` ; `ls architecture/decisions/ \| grep 779d97c` → rien. `grep "063d7eb" architecture/audit-ledger.jsonl` → **aucune** ligne (encore moins arbitré que ce que dit l'audit, qui ne prétendait que « non arbitré », pas « absent du registre »). `grep -n "^### P1-2\|^### P2-5\|^### P2-6\|^### P3-8" architecture/inbox/CURSOR-063d7eb-pr35-challenge-perte-decision.md` → les 4 sous-titres existent bien tels que cités. `grep -n "^### P1-3" architecture/inbox/CURSOR-779d97c-revue-verdicts-illisibles.md` → existe. Aucun brief proposé sur ces motifs par l'audit courant : cohérent avec le fait qu'ils sont déjà en file, non arbitrés. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **Brief 1 proposé (§ 8)** — lier un verdict à la base réelle de la PR et
  refuser/marquer périmé un point mesuré sur une base obsolète
  (`harness/audit_review.py`, `harness/audit_decision.py`). Techniquement
  fondé (voir ligne 2 du tableau ci-dessus : le point 12 est un cas réel et
  reproductible de verdict périmé retenu). La décision de convertir cette
  proposition en brief, son calibrage exact et sa priorité face à la file
  déjà longue de motifs non arbitrés (ligne 11) restent un arbitrage du
  propriétaire, pas une question de véracité.
- **Brief 2 proposé (§ 8)** — rendre factuelle la zone factuelle du corps de
  PR généré par `pipeline-challenge.yml` (`gh pr create --body`).
  Techniquement fondé (ligne 3 : les trois chiffres divergent réellement, et
  le `REFUTED` est absent de la description). Note de périmètre confirmée
  par l'audit lui-même : ce brief touche `.github/workflows/**`, donc le
  denylist du merge-bot — jamais auto-mergeable, arbitrage propriétaire
  requis par construction pour ce brief-là, indépendamment de cette revue.
- **Priorité de la file de motifs récurrents non arbitrés** (ligne 11 :
  `CURSOR-779d97c` P1-3, `CURSOR-063d7eb` P1-2/P2-5/P2-6/P3-8) — deux audits
  challengés (ou même pas) depuis plus d'un jour sans qu'aucune décision
  n'ait été prise. Ce n'est pas un constat de cet audit-ci, mais son § 9
  s'appuie sur cet état pour justifier de ne pas re-proposer de brief ; le
  propriétaire pourrait vouloir purger cette file avant qu'elle ne masque
  d'autres motifs récurrents.
- **§ 11 de l'audit** (« il n'autorise aucune implémentation ») et les trois
  flags `*_authorized: false` sont corrects et n'appellent pas d'arbitrage
  technique — rappelés ici seulement pour mémoire, aucune action requise.

## 4. Synthèse

L'audit tient presque intégralement : les 9 constats numérotés (P0-1, P1-1,
P1-2, P2-1 à P2-5, P3-1) sont tous CONFIRMED sur reproduction indépendante,
par un chemin d'accès différent de celui de l'auditeur (API GitHub publique
non authentifiée au lieu de `gh`, calculs Python rejoués directement plutôt
que cités). Les chiffres les plus vérifiables mécaniquement — les deux
comptages de verdicts (13/4/1 au tableau, 16/2/6/2 au registre), la
chronologie de fusion à la seconde, l'ordre des règles de
`audit_decision.py`, le dernier verdict des 13 briefs — se rejouent tous à
l'identique.

Une seule chose tombe, et c'est mineur : le § 1 de l'audit dit que la PR a
fusionné « 6 secondes après le lancement » du job auditeur, alors que ses
propres horodatages (§ 3) montrent que ce sont 6 secondes après la **fin**
du job (le lancement remonte à 26 secondes avant la fusion). Le tableau
chronologique lui-même est exact ; seule la phrase de résumé confond
lancement et fin. Cela n'affaiblit en rien le constat structurel de P0-1
(le maillon ne pouvait pas peser, quel que soit le chiffre exact), mais
mérite une correction si ce fichier sert de référence citée ailleurs — dans
un audit dont l'un des constats centraux (P2-2) est précisément qu'un
fichier ne doit pas dire deux choses différentes de lui-même.

Recommandation de traitement : les deux briefs proposés au § 8 sont
techniquement bien fondés sur des défauts réels et reproductibles (verdict
périmé retenu en décision automatique ; description de PR factuellement
fausse sur la seule surface lisible avant l'auto-fusion). Leur conversion
effective, leur priorité relative face à la file de motifs déjà proposés et
non arbitrés, et l'arbitrage de gouvernance nommé en § 3 (bloquer la fusion
tant que le contre-audit n'a pas statué) restent au propriétaire — cette
revue n'objecte à aucun des deux sur le plan technique.
