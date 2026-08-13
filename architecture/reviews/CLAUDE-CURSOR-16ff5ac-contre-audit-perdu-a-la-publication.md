---
review_of: CURSOR-16ff5ac-contre-audit-perdu-a-la-publication
reviewer: claude-code
target_commit: 16ff5ac77e618551b033b3bccda88ba83523c423
reviewed_at: 2026-08-13T10:15:00Z
---

# Contre-audit de CURSOR-16ff5ac-contre-audit-perdu-a-la-publication

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `16ff5ac77e618551b033b3bccda88ba83523c423` — existe
  dans l'historique de `master` :
  `git log --oneline -1 16ff5ac77e618551b033b3bccda88ba83523c423` →
  `16ff5ac Merge pull request #60 from PLiagre/forge/012-monde-vivant-commerce-ddda`,
  parents `9eef958` et `a4de4bb` confirmés par `git show --no-patch --format=%P`.
- Mesures rejouées directement sur ce dépôt (pas dans un worktree séparé,
  mais sans écriture — `git status` propre avant/après) : voir § 2 pour
  chaque point, sorties collées.
- Différence méthodologique importante découverte pendant cette relecture :
  l'auditeur a délibérément figé son analyse sur l'arbre `16ff5ac`
  (`git worktree add /tmp/audit16 16ff5ac`), mais a rédigé le constat 1 au
  temps présent/passé absolu (« il n'a jamais atteint master ») sans
  revérifier l'état de `master` **au moment où l'audit lui-même a été
  écrit** (`created_at: 2026-08-13T08:55:00Z`, soit 27 minutes après la
  fusion de la PR 60). Cette différence change le verdict du constat 1 —
  voir ligne 1.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | P0 — le contre-audit de la PR #60 est produit à 08:20:30Z, confirme les 9 points techniques, puis « dort sur une branche jamais fusionnée » / « n'a jamais atteint master » / « personne n'était là pour lire l'avertissement » | **PARTIAL** | Le mécanisme technique est CONFIRMED à la lettre : `.github/workflows/pipeline-challenge.yml:197-201` contient bien `gh pr create ... \|\| echo "::warning::gh pr create refused ... open the PR manually."` — la dernière commande (`echo`) réussit toujours, donc l'étape et le job sortent verts même si `gh pr create` a échoué. Mais l'affirmation centrale « il n'a jamais atteint master » / « personne n'était là » est **REFUTED** par l'historique de `master` lui-même : `git log --format='%H %ad %s' --date=iso-strict` montre que le fichier `architecture/reviews/CLAUDE-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois.md` **est entré dans master** au commit `9e35764` (« challenge: revue CLAUDE-CURSOR-a4de4bb-... (#63) »), fusionné **2026-08-13T08:35:08Z** — soit 7 minutes après l'échec de publication (08:22:39Z citée par l'audit) et **20 minutes avant que l'audit CURSOR-16ff5ac lui-même ne soit créé** (08:55:00Z). Le commit est auteuré `Pierre-Edouard Liagre <Liagre.pe@outlook.com>` avec `Co-authored-by: forge-bot`, donc ouvert et fusionné par le propriétaire — précisément l'action que le message d'avertissement demandait (« open the PR manually »). Un contre-audit sœur, `CURSOR-a600532-fusion-sans-contre-audit`, a été récupéré de la même façon une minute plus tôt (commit `96d1565`, PR #62, 08:34:57Z). `git branch --contains 9e35764` → `master`. Aucun workflow de rattrapage automatique n'existe dans `.github/workflows/` pour les branches `forge-bot/review-*` orphelines — la récupération a donc été un geste humain, pas un filet automatique. Le défaut d'ingénierie (le `\|\| echo`) est réel et doit être corrigé ; mais le récit « perdu à la publication, personne ne l'a vu » et la sévérité P0 qui en découle ne tiennent plus une fois qu'on regarde `master` au moment où cet audit a été écrit, pas seulement au commit `16ff5ac` figé. |
| 2 | P1 — `master` est rouge sur `16ff5ac` : `pipeline-orchestrate` rejoue une transition déjà enregistrée (`CURSOR-3b47ffe` est `AUDIT_CONVERTED`, pas `AUDIT_CHALLENGED`) parce que la garde `is_terminal()` teste la terminalité au lieu de l'état attendu | **CONFIRMED** | Reproduit intégralement en lisant et en exécutant le code du dépôt (sans accès `gh`, donc sans revoir le run CI lui-même, mais la mécanique citée est vérifiable indépendamment de la CI). (a) `git diff --name-only 9eef958 16ff5ac -- 'architecture/reviews/*.md'` → exactement `architecture/reviews/CLAUDE-CURSOR-3b47ffe-pr57-monde-sans-faim.md`, confirmant que la fusion a bien apporté ce seul fichier de revue avec les lignes de registre déjà terminales pour cet audit. (b) Rejeu direct de `trigger_resolve.resolve_push(...)` sur ce fichier, sur `master` actuel : sortie obtenue `state=AUDIT_CONVERTED`, `terminal?=False`, `event=review_recorded`, `payload={'audit_id': 'CURSOR-3b47ffe-pr57-monde-sans-faim'}` — identique à la citation de l'audit. (c) Lecture de `harness/pipeline/orchestrator.py:160-179` (`handle_review_recorded`) : quand l'état n'est ni `None` ni `AUDIT_PROPOSED`, `record_challenge` est sauté et `audit_decision.decide_auto` est appelé directement — ce module est le seul à porter le message `"is {state}, not AUDIT_CHALLENGED; only a challenged audit can be decided"` (`harness/audit_decision.py:164,251`), qui correspond mot pour mot au log CI cité. (d) `harness/tests/test_trigger_resolve.py` ne couvre que le cas « non-terminal = AUDIT_CHALLENGED » (`test_non_terminal_audit_still_resolves_to_review_recorded`), jamais le cas « non-terminal mais déjà au-delà de CHALLENGED (ex. AUDIT_CONVERTED) » — confirmant le trou de couverture que l'audit diagnostique. Portée honnête reprise de l'audit : bruyant, sans perte de données, registre non altéré. |
| 3 | P1 — coût mesuré du double comptage : morts +39 442 (+0,52 %), cellules affamées ×3,3 (9→30), et trois ordres de tick donnent trois mondes différents | **CONFIRMED** | Rejoué de zéro (pas seulement relu) avec le moteur actuel de `master` (`sim/engine.py`, `sim/world.py`), mêmes graines `rng_seed=42` / `world_seed=42`, `N=200` ticks. Variante 1 (code fusionné tel quel) : `morts=7544299 survie=0.887172 affamees(tick200)=9 kg_transportes=8171507` — identique chiffre pour chiffre à la ligne 1 du tableau de l'audit. Variante 2 (nourriture reçue soldant un déficit consommée au lieu d'être stockée, patch appliqué localement reproduisant exactement le extrait de code du §7.6 de l'audit) : `morts=7583741 survie=0.886582 affamees(tick200)=30 kg_transportes=7418965` — identique à la ligne 2. Variante 3 (commerce avant consommation, ordre littéral de SC3) : `morts=7560137 survie=0.886935 affamees(tick200)=30 kg_transportes=8144114` — identique à la ligne 3. Écart 2−1 recalculé indépendamment : +39 442 morts (+0,52 %), affamées 9→30 (×3,3) — confirme le chiffre publié. Le mécanisme sous-jacent (`sim/engine.py:_apply_commerce`, lignes `cell_b.food_stock_kg += transfer` **et** `cell_b.food_deficit_kg = max(0.0, cell_b.food_deficit_kg - transfer)` dans la même passe) est lu directement dans le code et correspond au double comptage décrit. Limite reconnue par l'audit et vérifiée ici aussi : le compteur `affamees` utilisé (`hunger_ticks>0` au tick 200 seulement) diffère du compteur publié `261` (cumulatif sur toute la simulation, produit par `measure_cellules_affamees.py`) — en rejouant ce script séparément sur la variante 1 j'obtiens bien `261`, confirmant que l'audit ne mélange pas les deux définitions et signale honnêtement la limite. |
| 4 | P2 — arriéré de la boucle invisible : 27 audits en inbox au commit `16ff5ac`, 14 avec au moins une ligne au registre, 13 sans aucune ligne | **CONFIRMED** | Rejoué dans un worktree séparé positionné exactement sur `16ff5ac` (`git worktree add`) pour ne pas mélanger avec l'état courant de `master`, qui a évolué depuis (deux des « 13 sans ligne » de l'époque, `CURSOR-a600532` et `CURSOR-a4de4bb`, ont depuis reçu des lignes ou ont vu leur revue fusionnée — voir ligne 1). Comptage sur cet arbre figé : `inbox=27`, `avec ligne=14`, `sans ligne=13` — identique aux trois chiffres de l'audit. La nuance de l'audit (absence de ligne `AUDIT_PROPOSED` = convention documentée, pas un défaut d'enregistrement — `harness/audit_ledger.py:74-83`) est également vérifiée : le fichier commente bien cette convention à ces lignes. |
| 5 | P3 — classification CI (6 workflows déclenchés, 5 verts, `pipeline-orchestrate` rouge) et fusion propre (rien introduit hors des deux parents) | **PARTIAL** | La propreté de la fusion est CONFIRMED directement : `git diff --stat a4de4bb 16ff5ac` ne montre que les deux fichiers d'audit déjà présents côté `master` (parent `9eef958`) plus la régénération du tableau de bord Hermes — aucun contenu de résolution de conflit inédit. Le détail « 6 workflows, 5 verts, 1 rouge » n'a pas pu être revérifié indépendamment ici faute d'accès à `gh`/GitHub Actions dans cet environnement (`gh: To use GitHub CLI in a GitHub Actions workflow, set the GH_TOKEN...`) ; je ne peux donc ni le confirmer ni le réfuter par une preuve directe. Le seul rouge cité (`pipeline-orchestrate`) est en revanche confirmé indirectement et fortement par la ligne 2 ci-dessus (reproduction mécanique complète du même échec). |
| 6 | Non-duplication revendiquée : proposition 3 = « le même objet que la proposition 1 de `CURSOR-a4de4bb` » ; briefs 006 et 009 ouverts ne sont pas réinstruits | **CONFIRMED** | `architecture/inbox/CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois.md` § 6 point 1 dit bien « Placer le commerce avant la consommation comme SC3 le spécifiait » — même objet que la proposition 3 de l'audit courant, correctement rapproché plutôt que dupliqué. `harness/queue/briefs/009-full-auto-agent-invocation/brief.md` porte bien dans son titre « give recurring CI spend a real ceiling », confirmant que le thème plafond de coût n'est pas réinstruit. `harness/queue/briefs/006-full-auto-agent-pipeline/` existe et porte la boucle full-auto, cohérent avec la mention de l'audit. |
| 7 | § 4 « Ce qui tient » — portes mécaniques vertes, identiques à l'avant-fusion : `verdict_audit.py` ACCEPT sur 011 et 012, `314 passed, 16 skipped` (harnais), `25 passed` (`sim/`), `harness_audit.py` à `20/24` | **CONFIRMED** | Ré-exécuté directement sur `master` actuel (pas un worktree figé, mais aucun changement de code n'affecte ces sorties depuis `16ff5ac`) : `verdict_audit.py harness/queue/briefs/012-...` → `VERDICT: ACCEPT` ; `verdict_audit.py harness/queue/briefs/011-...` → `VERDICT: ACCEPT` ; `python3 -m pytest harness/tests/ -q` → `314 passed, 16 skipped in 7.37s` ; `python3 -m pytest sim/tests/ -q` → `25 passed in 1.61s` ; `python3 harness/harness_audit.py` → `SCORE: 20/24`. Les quatre nombres sont identiques à ceux cités par l'audit. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **La sévérité P0 du constat 1 doit être réexaminée à la lumière de la
  ligne 1 ci-dessus.** Le défaut d'ingénierie (`\|\| echo` avalant l'échec
  de `gh pr create`) reste réel et mérite d'être corrigé — un système qui
  réussit à vide en silence est dangereux par construction, l'audit a
  raison sur ce principe. Mais l'audit affirme un fait qui s'est révélé
  faux au moment même où il a été écrit : la revue **avait déjà atteint
  master**, récupérée à la main, 20 minutes avant que `CURSOR-16ff5ac` ne
  soit créé. Un P0 qui décrit une perte irréversible n'est pas la même
  chose qu'un P1/P2 qui décrit un filet de rattrapage entièrement manuel et
  sans compteur (personne ne sait aujourd'hui, sans lire le registre à la
  main, que ce rattrapage a eu lieu). Au propriétaire de dire si la
  sévérité doit baisser en conséquence, et si le fait que la récupération
  ait été manuelle (pas automatique) suffit à maintenir un P0 malgré tout.
- **L'écart entre « revue fusionnée dans master » et « ligne AUDIT_CHALLENGED
  au registre »** est un fait nouveau que ni l'audit `CURSOR-16ff5ac` ni son
  prédécesseur n'ont signalé explicitement : `CURSOR-a4de4bb-...` a son
  fichier de revue sur `master` depuis le commit `9e35764`, mais
  `audit_ledger.current_state_for('CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois', ...)`
  renvoie toujours `None` aujourd'hui (rejoué dans cette relecture). C'est
  vraisemblablement une autre manifestation du même bug que le constat 2 —
  le push de la PR #63 a dû se heurter à la même garde mal ciblée — mais je
  n'ai pas pu remonter le run CI correspondant (pas d'accès `gh`). Le
  propriétaire devrait vérifier si cet audit doit rester considéré comme
  « proposé » indéfiniment ou si un rattrapage manuel du registre s'impose.
- **Portée du brief proposition 1** (rendre rouge une revue non publiée) :
  telle que rédigée, elle corrige le symptôme observé au commit `16ff5ac`
  (CI verte malgré `gh pr create` refusé) mais pas la découverte faite dans
  cette relecture (registre qui reste `None` malgré une revue déjà fusionnée
  autrement). Au propriétaire de juger si un seul lot doit couvrir les deux,
  ou si un second brief est nécessaire.

## 4. Synthèse

Ce qui tient, sans réserve : les constats 2, 3 et 4, et les affirmations de
la section « ce qui tient » (§4 de l'audit) ont tous été reproduits
indépendamment, souvent chiffre pour chiffre — y compris la mesure la plus
coûteuse à vérifier (les trois variantes de simulation à 200 ticks), qui
correspond exactement aux nombres publiés. Le mécanisme technique cité en
constat 1 (le `\|\| echo` qui avale l'échec de `gh pr create`) est également
réel et bien localisé.

Ce qui tombe : le récit central du constat 1 — « perdu à la publication »,
« il n'a jamais atteint master », « personne n'était là » — ne résiste pas à
la vérification, précisément parce que l'audit a figé son regard sur le
commit `16ff5ac` sans revérifier `master` au moment où il rédigeait, 27
minutes plus tard. La revue avait déjà été récupérée à la main par le
propriétaire. Cela ne dédouane pas le défaut d'ingénierie ni ne rend le
sujet sans intérêt (le filet de rattrapage était manuel, invisible, et
un audit jumeau — `a600532` — avait connu le même sort une minute plus
tôt) ; mais cela change la nature du problème : ce n'est pas « une confirmation
d'un P0 a disparu sans laisser de trace », c'est « une confirmation de P0 a
été republiée manuellement sans que rien ne le signale, et sans que le
registre ne s'en souvienne ». Je recommande au propriétaire de traiter les
propositions de brief 1 et 2 de cet audit (elles restent bien fondées et
correctement prouvées), d'ajuster la sévérité du constat 1 à la lumière de
ce qui précède, et de statuer sur le point NEEDS_OWNER concernant l'écart
persistant entre revue fusionnée et registre.
