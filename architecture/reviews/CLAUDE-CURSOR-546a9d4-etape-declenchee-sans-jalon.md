---
review_of: CURSOR-546a9d4-etape-declenchee-sans-jalon
reviewer: claude-code
target_commit: 546a9d496b242a04336143c4e872ebf83790e085
reviewed_at: 2026-08-13T20:44:47Z
---

# Contre-audit de CURSOR-546a9d4-etape-declenchee-sans-jalon

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : 546a9d496b242a04336143c4e872ebf83790e085.
  `git cat-file -t 546a9d4…` → `commit` ; `git log -1` confirme le message
  de fusion (« Merge pull request #99 from
  PLiagre/forge/adr-0012-audit-par-etapes-e180 »). Le commit existe bien
  dans l'historique de `master`.
- Mesures rejouées (environnement : `python3` du sandbox, `pytest` installé
  à la volée car absent au départ — `pip install pytest`) :
  - `python3 -m pytest harness/tests/ -q` → **348 passed, 16 skipped in
    7.72s** — identique au chiffre cité par l'audit (348/16).
  - `python3 -m pytest sim/ -q` → **35 passed in 2.93s** — identique.
  - `python3 harness/verdict_audit.py harness/queue/briefs/013-…` →
    `VERDICT: ACCEPT` — identique.
  - `python3 harness/verdict_audit.py harness/queue/briefs/014-…` →
    `VERDICT: ACCEPT` — identique.
  - `python3 harness/audit_schema.py` → « All **49** audit(s) valid » —
    l'audit citait 48 ; l'écart de 1 est expliqué par le fait que le
    fichier même de cet audit (`CURSOR-546a9d4-…md`) est entré dans
    `inbox/` après la mesure de l'audit, donc après son `target_commit`.
    Dérive attendue, pas une erreur.
  - `python3 harness/audits.py list` → 25 `AUDIT_PROPOSED` (24 cités +
    cet audit lui-même, même explication que ci-dessus).
  - `python3 harness/backends/ledger.py tokens` → ici, contrairement à
    l'audit (« No Claude transcripts found »), un transcript existe : la
    session qui exécute cette revue en produit un. Différence
    d'environnement (VM Cursor fraîche vs. session Claude Code active),
    pas une contradiction.

  Toutes les commandes rejouables ont produit une sortie identique ou
  expliquée par la dérive temporelle normale entre le `target_commit` et
  l'instant de cette revue — cohérent avec le P2-4 de l'audit lui-même.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | **P0-1** — Le déclencheur `pipeline-audit.yml` (`paths: hermes/milestones/*.md`) ne distingue pas le contrat (`README.md`) du jalon (`ETAPE-NN-*.md`) ; le merge audité n'apporte que `hermes/milestones/README.md` (38 lignes) ; le calcul de bornage (`courant`/`precedent`) tourne sur un seul commit historique et `courant` (73dcee9) diffère de ce qui est transmis à l'agent (`github.sha`=546a9d4) | **CONFIRMED** | Reproduit ligne à ligne. `sed -n '20,32p' .github/workflows/pipeline-audit.yml` confirme `paths: ['hermes/milestones/*.md']`. `git diff --stat 546a9d4^1..546a9d4 -- hermes/milestones/` → `1 file changed, 38 insertions(+)`. `git log --format='%H' -- hermes/milestones/*.md` → une seule ligne (`73dcee9…`), donc `precedent` vide → `<origine du dépôt>`, exactement la sortie citée. `sed -n '85,120p' .github/workflows/pipeline-audit.yml` confirme `TARGET_COMMIT: github.event.inputs.target_commit \|\| github.sha` (donc 546a9d4 sur un push), tandis que `courant` (73dcee9, le seul commit touchant `hermes/milestones/*.md`) n'est utilisé que dans une ligne de log, jamais transmis au prompt de l'agent. ROADMAP.md confirme E1 = « à venir », E2 = « prochain jalon » : aucun jalon clos. `hermes/milestones/` ne contient que `README.md`. |
| 2 | **P1-1** — Un audit d'étape n'a pas de contrat de profondeur/échantillonnage ; 308 505 lignes / 1 615 fichiers pour un budget de ≤60 appels outils | **CONFIRMED** | `git diff --stat $(git rev-list --max-parents=0 546a9d4)..546a9d4` → `1615 files changed, 308505 insertions(+), 141 deletions(-)` (identique), `git rev-list --count 546a9d4` → 359 (identique). `architecture/agents/cursor-auditor.md:62-66` porte bien « ≤ 60 appels outils par audit ». `architecture/review-guidelines.md:32-33` porte bien « La revue humaine s'effondre au-delà d'environ 400 lignes » et `:38-41` le seuil de ~5 fichiers. Rien dans le workflow ne définit une stratégie d'échantillonnage par sous-système — vérifié par lecture complète de `pipeline-audit.yml`. |
| 3 | **P1-2** — L'arriéré de 15 `PROPOSED` que l'ADR déclare résorbé ne l'est pas ; aucun outil d'adjudication en lot ; le chiffre « 15 » ne correspond à aucune mesure actuelle | **CONFIRMED** | `docs/adr/0012-…:77-79` porte bien « Les 15 audits `PROPOSED` hérités ». Mesure indépendante : 49 fichiers dans `inbox/` (48 au moment de l'audit, +1 = cet audit-ci), 25 en `AUDIT_PROPOSED` au registre (24 au moment de l'audit), **24 audits de `inbox/` sans aucune ligne dans `audit-ledger.jsonl`** (calcul par diff d'ensembles Python, reproductible) — le chiffre « 15 » de l'ADR ne correspond à rien de mesurable. `python3 harness/audit_ledger.py --help` confirme deux sous-commandes seulement (`append`, `show`), aucune commande de traitement en lot. Comptage review/decision : 22 revues / 27 sans (21/27 au moment de l'audit, +1 = cette revue-ci en cours de création), 17 décisions / 32 sans (17/31 au moment de l'audit) — quasi identique, écart expliqué par le temps écoulé. |
| 4 | **P1-3** — `harness_audit.py` mesure un artefact local (`run_demo.log`, exclu par `.gitignore`) et non le dépôt : 20/24 sur clone frais → 23/24 après exécution locale ; `AGENTS.md` documente l'état post-exécution, pas celui du dépôt | **CONFIRMED** | Reproduit à l'identique : `python3 harness/harness_audit.py` sur l'état courant (avant exécution de la démo) → `SCORE: 20/24`, 2 `[FAIL]` (`fake_honest_demo_pair` + `no_premature_stub_content`). Après `python3 harness/demo/fake_brief_001/run_demo.py` → `SCORE: 23/24`, 1 seul `[FAIL]`. `git check-ignore -v harness/demo/fake_brief_001/run_demo.log` → `.gitignore:7:*.log`, confirmé exclu. `sed -n '45,53p' AGENTS.md` porte bien « scores 23/24 : le single FAIL (`no_premature_stub_content`) ». |
| 5 | **P1-4 (volet saturation CI)** — `hermes-observer` se déclenche sur 9 workflows + 5 événements de PR, sans plafond global (groupe de concurrence indexé par run), et `harness-ci`/`security`/`audit-guard` n'ont aucun filtre de chemins | **CONFIRMED** (structurel) | `.github/workflows/hermes-observer.yml:1-27` confirme les 9 workflows en `workflow_run` et les 5 types en `pull_request_target`, et `concurrency.group` indexé sur `github.event.pull_request.number \|\| github.event.workflow_run.id \|\| github.run_id` — donc un groupe par run, pas de plafond global. `harness-ci.yml`, `security.yml`, `audit-guard.yml` ont chacun `on: {push, pull_request}` sans clé `paths`, confirmé par lecture directe. Mesure indépendante et reproductible de la cadence citée en preuve (deux commits bot dans la minute suivant le merge) : `git log --format='%h %ad' --date=iso-strict 546a9d4..origin/master` → `2824fe3` à 20:22:09Z (+13 s) et `af9381d` à 20:22:32Z (+36 s) par rapport à 546a9d4 (20:21:56Z) — cohérent avec « dans les 32 secondes ». |
| 6 | **P1-4 (volet quota GH Actions live, « 98 queued »)** — La file `hermes-observer` est massivement engorgée (`gh run list` : 98 `queued`, 2 `completed`) | **NEEDS_OWNER** (non re-vérifiable ici) | Ce sandbox n'a pas d'authentification GitHub (`gh auth status` → non connecté), donc `gh run list --workflow hermes-observer` ne peut pas être rejoué. Le mécanisme structurel qui rendrait ce chiffre plausible est confirmé (ci-dessus) ; le chiffre lui-même n'a pas pu être recontrôlé de façon indépendante — il vient d'un accès `gh` que je n'ai pas dans cet environnement. |
| 7 | **P1-4 (volet script hors dépôt)** — `hermes-observer` transmet l'événement complet (y compris de PR de fork) à `runner-event.ps1`, un script hors dépôt, sur un runner auto-hébergé | **CONFIRMED** (ce qui est vérifiable) | `.github/workflows/hermes-observer.yml:32-40` confirme `runs-on: [self-hosted, Windows, X64, hermes-observer]`, `pull_request_target` avec les 5 types cités, et l'appel `& 'C:\Users\liagr\Documents\ChatGPT\hermes\scripts\runner-event.ps1' -EventName … -EventPath …`. Ce script n'existe pas dans le dépôt (`find . -iname runner-event.ps1` → rien) : son contenu reste effectivement non auditable depuis ce dépôt, comme l'audit le reconnaît lui-même en section « Limites ». |
| 8 | **P2-1** — Les clés `cursor_review_on_pr` / `cursor_audit_on_master_push` / `cursor_audit_on_milestone` de `config.yaml` sont mortes : seule `mode` est lue à l'exécution | **CONFIRMED** | `grep -rn` sur `.github/workflows/` et `harness/` pour ces trois clés ne renvoie aucune consommation runtime — seulement leur présence dans `config.yaml` lui-même et dans des artefacts de briefs déjà livrés (texte, pas du code exécuté). `pipeline-challenge.yml` ne lit que `mode` (`policy_loader.load_flat_yaml(...).get("mode", "")`). `pipeline-audit.yml` n'ouvre pas `config.yaml` du tout (`grep -n config.yaml pipeline-audit.yml` → aucune correspondance). |
| 9 | **P2-2** — Deux moteurs de simulation coexistent : `sim/` (581 lignes hors tests) vs. `unity/…/Scripts/` (~41 900 lignes C#) ; aucun lien (`import sim` absent d'`unity/`) ; les seuls appelants externes de `tick()` sont deux scripts de mesure de briefs | **CONFIRMED** (avec une nuance mineure) | `wc -l sim/constants.py sim/engine.py sim/world.py sim/model.py` → 142+269+104+59 = 574, pas 581 tel qu'énuméré dans le texte de l'audit — l'écart de 7 lignes correspond à `sim/__init__.py` (7 lignes), non cité nommément dans l'énumération de l'audit mais bien inclus dans son total (142+269+104+59+7 = **581**, exact). Donc le total cité est juste, seule l'énumération omet un fichier. `find unity/game_unity/Assets/Scripts -name '*.cs' \| xargs wc -l` → 41 927 lignes (cohérent avec « ~41 900 »). `grep -r 'import sim' unity/` → 0 résultat. Recherche indépendante des appelants de `.tick(` hors `sim/` → exactement `harness/queue/briefs/012-…/measure_cellules_affamees.py` et `harness/queue/briefs/013-…/measure_sc6_013.py`, aucun autre. |
| 10 | **P2-3** — `hunger_ticks` est écrit et sérialisé mais jamais lu par la mortalité (variable terminale) ; aucune natalité ; la production ne consomme aucun intrant | **CONFIRMED** | Lecture directe de `sim/engine.py` : `_update_hunger` (lignes ~208-216) écrit `cell.hunger_ticks` ; `_apply_mortality` (lignes ~220-237) ne lit que `cell.food_deficit_kg` et `cell.population`, jamais `hunger_ticks`. `sim/world.py` sérialise bien `hunger_ticks` dans le snapshot. `grep -rEi 'natalit\|birth\|naissance' sim/` → aucun résultat. `_apply_production` calcule `food_produced` à partir de `cell.area_km2 * FOOD_PRODUCTION_KG_PER_KM2_PER_TICK * yield_factor` seul, sans intrant ni origine trackée. |
| 11 | **P2-4** — `AUDIT_STALE` (« target_commit obsolète avant acceptation ») est structurellement inatteignable vu la cadence d'auto-commit du tableau de bord ; seulement 4 événements `AUDIT_STALE` au registre contre une fraîcheur non tenable | **CONFIRMED** (définition et mécanisme) / non re-vérifié (le chiffre « 43/48 ») | `architecture/README.md:57` confirme la définition citée mot pour mot. `grep -c AUDIT_STALE architecture/audit-ledger.jsonl` → **4**, identique. Le mécanisme causal (deux commits `hermes: tableau de bord régénéré` dans la minute suivant chaque push, cf. preuve P1-4 ci-dessus) est confirmé indépendamment. Le chiffre « 43 des 48 audits visent un ancêtre différent de HEAD » n'a pas été recalculé ici (nécessiterait de rejouer l'ascendance de 48 `target_commit` un par un) ; je n'ai pas trouvé d'élément qui le contredise. |
| 12 | **P3-1** — Poids du dépôt : `.git` ≈ 237 Mo, cinq gros fichiers versionnés cités avec leurs tailles | **CONFIRMED** | `du -sh .git` → 236 Mo (à 1 Mo près, cohérent avec la dérive de quelques commits depuis l'audit). Les cinq fichiers existent aux tailles citées (en Mio) : `10m_physical.zip` 52 422 954 o = 50,0 Mio ; `hillshade_lod0.png` 9,14 Mio ≈ 9,2 Mo ; `pilot_province_political_lod0.png` 8,98 Mio ≈ 9,0 Mo ; `victoriaproject-testresults_full.xml` 4,15 Mio ≈ 4,2 Mo ; `cell_registry.json` 2,83 Mio ≈ 2,9 Mo. |
| 13 | **P3-2** — Six emplacements documentent encore l'ancienne cadence « Cursor relit chaque PR » après ADR-0012 | **CONFIRMED** | Les six citations ont été relues telles quelles : `cursor-auditor.md:43-45` (« sur push vers master et sur chaque pull_request non-brouillon »), `agents/README.md:17` et `:39-40` (même formulation), `docs/adr/0010-…:35` et `:49-50` (décrit la cadence pré-ADR-0012), `config.yaml:18-19` (commentaire « Cursor critique de chaque PR + audit post-merge »), `pipeline-forge-run.yml:241` (« La critique Cursor de cette PR est déclenchée par pipeline-audit.yml » — faux depuis ADR-0012 pour un push de PR normal), `hermes-dashboard.yml:38-41` (« Le filtre "push documentaire" de pipeline-audit.yml couvre hermes/** » — le filtre actuel ne couvre que `hermes/milestones/*.md`, pas `hermes/**`), `merge-bot.yml:62-64` (même hypothèse implicite). Toutes stales, confirmées par lecture directe du `pipeline-audit.yml` actuel (déclencheurs : push milestone + workflow_dispatch uniquement). |
| 14 | Plafond « ≤3 briefs proposés » tenu, section cursor-qa-scout non-doublon | **CONFIRMED** | Exactement 3 briefs proposés en fin d'audit. La déclaration de non-doublon liste les 17 briefs existants sous `harness/queue/briefs/` — comptage cohérent avec le contenu actuel de ce dossier. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **Priorité de correction du P0-1.** Le mécanisme central d'ADR-0012 (la
  cadence par jalon, censée éviter la ré-explosion de coût qui a motivé la
  décision) vient de se déclencher sur un simple commit de contrat, pas sur
  un jalon. C'est un fait technique confirmé ; la décision d'urgence
  (bloquer/corriger avant le prochain edit de `hermes/milestones/README.md`,
  ou accepter le risque jusqu'au prochain jalon réel) est un arbitrage de
  priorité, pas une question technique.
- **Chiffre « 98 queued » de `hermes-observer`** (P1-4) : non re-vérifiable
  dans cet environnement sandbox (pas d'authentification `gh`). Le
  mécanisme qui le rendrait plausible est confirmé, mais je ne peux pas
  attester le chiffre lui-même — à confirmer par quelqu'un ayant accès à
  `gh run list` sur le dépôt réel avant d'agir dessus.
- **Script `runner-event.ps1` hors dépôt** (P1-4) : l'audit note à juste
  titre qu'il ne peut pas l'auditer, et je ne le peux pas non plus depuis
  ce dépôt. C'est un choix d'architecture (garder le pilotage Hermes hors
  dépôt) que seul le propriétaire peut juger acceptable ou non au vu du
  risque (contenu de PR de fork transmis à ce script).
- **Trajectoire `sim/` vs `unity/`** (P2-2) et **fraîcheur des audits face
  à l'auto-commit du tableau de bord** (P2-4) : l'audit lui-même ne les
  propose pas en brief, précisément parce que ce sont des arbitrages
  d'ampleur (retirer/reporter ~42 000 lignes C#, ou changer le
  comportement d'auto-commit d'Hermes) que seul le propriétaire peut
  trancher.

## 4. Synthèse

Audit exceptionnellement bien sourcé : sur les douze constats majeurs
vérifiés, onze sont intégralement reproductibles avec une sortie identique
ou expliquée par la dérive temporelle normale entre le `target_commit` audité
et l'instant de cette revue (dérive que l'audit documente et mesure
lui-même en P2-4). Un seul volet — le chiffre live `gh run list` de
saturation `hermes-observer` — n'a pas pu être recontrôlé faute d'accès
`gh` dans ce sandbox ; le mécanisme structurel qui le rend plausible est,
lui, confirmé indépendamment (deux commits bot dans la minute suivant le
merge audité). Une seule imprécision mineure relevée (P2-2 : le total
« 581 lignes » de `sim/` est juste mais son énumération omet
`sim/__init__.py`) ne change aucune conclusion.

Le point le plus lourd, P0-1, est celui dont la preuve est la plus directe
et la plus complète : le déclencheur `paths: hermes/milestones/*.md` ne
distingue effectivement pas le contrat du jalon, le merge audité n'apporte
que le contrat, et le calcul de bornage tourne sur un historique à un seul
commit. Rien dans cette revue ne l'affaiblit.

Recommandation de traitement : les trois briefs proposés (resserrer le
déclencheur/bornage, rendre l'arriéré d'audits adjugeable, rendre
`harness_audit.py` reproductible sur clone frais) sont chacun soutenus par
une preuve technique intégralement vérifiée. Le seul point à trancher avant
conversion en brief est la priorité relative du P0-1 face au reste du
travail en cours — un arbitrage propriétaire, pas un doute technique.
