---
review_of: CURSOR-3b47ffe-pr57-monde-sans-faim
reviewer: cursor-orchestrateur (rôle claude-challenger tenu en remplacement de Claude, indisponible — instruction propriétaire)
target_commit: 3b47ffe4ac808831cee71cb83817b098e08d7e49
reviewed_at: 2026-08-13T06:45:00Z
---

# Contre-audit de CURSOR-3b47ffe-pr57-monde-sans-faim

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## Note de transparence

Le challenger habituel (Claude Code, workflow `pipeline-challenge`) n'a pas
pu tourner : le run déclenché par la fusion de cet audit s'est terminé en
erreur `429` avec le message « You've hit your org's monthly spend limit »
(run GitHub Actions `31621195096`, 2026-08-12T17:09Z). Ce contre-audit est
donc rédigé par l'orchestrateur Cursor qui remplace le CTO, sur instruction
du propriétaire — c'est-à-dire par un agent de la même infrastructure que
l'auditeur, dans une session distincte. Cette limite est réelle et connue
(même famille que le troisième angle mort du contrôle d'auto-jugement,
consigné dans `HANDOFF.md`) ; elle est déclarée ici plutôt que masquée.
Toutes les mesures de l'audit ont été rejouées indépendamment avant chaque
verdict, commandes et sorties à l'appui.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : 3b47ffe4ac808831cee71cb83817b098e08d7e49
- Le commit existe-t-il dans l'historique de la branche cible ? Oui :
  `git merge-base --is-ancestor 3b47ffe4ac808831cee71cb83817b098e08d7e49 master`
  sort en code `0` (la branche `forge/011-sim-monde-vivant-a67c` a été
  fusionnée dans `master` par la PR #57 le 2026-08-13T06:12Z).
- Mesures de l'audit rejouées ? Oui, les deux scripts du § 5 de l'audit,
  exécutés tels quels depuis la racine avec `.venv/bin/python` sur l'état
  `master` d'aujourd'hui (identique à `3b47ffe` sur tout `sim/`) :
  - script 5.3 : sortie identique ligne à ligne — 596 cellules, aire
    minimale 1.444877 km², production 50.0 contre consommation 20.0 par
    km², et après 200 ticks : delta de population 0, stock total ×11.0,
    0 cellule avec `hunger_ticks > 0`, 0 cellule à stock épuisé ;
  - script 5.4 : les deux condensés (graines de rng 42 et 999999) sont
    égaux, l'état interne du générateur pseudo-aléatoire est inchangé
    après 10 ticks, et `adjacency` est absent de `to_dict()`.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | P0-1 — état final de la PR jugé par personne, acteur des trois rôles tracé nulle part | CONFIRMED | Rejoué : les six commits du lot portent la même identité git (`git log a600532~1..a600532^2` : « Cursor Agent <cursoragent@cursor.com> » partout) ; `harness/verdict_audit.py` compare bien des chaînes `**Author**:` auto-déclarées (`read_all_fields(..., "Author")`, appelé sur `generator-log.md` et `verdict.md`) ; le commit `3b47ffe` (16:54Z) modifie `ROADMAP.md`/`HANDOFF.md` après le verdict (16:49Z pour l'itération 2). Les trois preuves tiennent. |
| 2 | P1-1 — `ROADMAP.md` modifié dans une PR de lot, contre ADR-0010 et le non-but 5 du brief | PARTIAL | Les faits cités sont exacts (diff réel, ADR-0010 ligne 32, non-but 5). Délimitation : l'en-tête de `ROADMAP.md` autorise expressément « une correction factuelle (statut devenu faux) […] à tout acteur, en la signalant dans le message de commit », ce qui a été fait ; le contenu est reconnu exact par l'audit lui-même. Ce qui reste confirmé : la ligne d'historique signe un acteur hors du contrat d'écriture d'Hermes (la forme attendue est la délégation), et l'édition est arrivée dans le commit post-verdict — c'est le point 1 qui porte ce défaut-là. |
| 3 | P1-2 — sur le monde réellement chargé, la chaîne faim → mortalité ne se déclenche jamais | CONFIRMED | Mesure rejouée à l'identique (script 5.3, sortie ligne à ligne égale à celle de l'audit) : 0 cellule affamée, 0 mort, stock ×11.0 après 200 ticks. Le rapport production/consommation vaut 2,5 pour 1 dans toutes les cellules — structurel, pas accidentel. Le test d'intégration SC7d utilise bien `area_km2 = 0.0`, inatteignable dans les données G3 (aire minimale réelle : 1.444877 km²). |
| 4 | P1-3 — les deux constantes de l'économie alimentaire ne sont pas dans la même unité de temps | CONFIRMED | Relu dans `sim/SEEDING.md` : ration justifiée « journalière » (tick = 1 jour, ligne du tableau des constantes) contre production justifiée « ÷ 100 (ticks/an proxy) » (tick = 3,65 jours). `INITIAL_FOOD_DAYS` porte « DAYS » pour une unité déclarée « ticks ». La durée du tick n'est définie nulle part dans `sim/`. Les deux arithmétiques de réparation de l'audit se recalculent (7,3 kg/tick → 73 > 50 ; 13,7 < 20) : l'équilibre livré est bien un artefact du mélange d'unités. |
| 5 | P1-4 — la preuve de déterminisme du tick ne mesure rien, `tick()` ne consomme jamais son rng | CONFIRMED | Mesure rejouée (script 5.4) : condensés égaux pour deux graines de rng différentes, état interne du générateur inchangé après 10 ticks. Aucune des quatre fonctions de maillon de `sim/engine.py` ne lit `rng`. Le test SC5 ne peut pas échouer, quelle que soit l'implémentation. |
| 6 | P2-1 — `World.adjacency` : 1364 arêtes chargées, jamais lues, invisibles pour SC8 | CONFIRMED | `rg -n adjacency sim/*.py` : toutes les occurrences sont dans le chargement (`sim/world.py`), aucune lecture ailleurs. `World` n'est pas une dataclass, donc hors du périmètre de `sim/tests/test_write_coverage.py` ; `to_dict()` exclut `adjacency` (vérifié à l'exécution, script 5.4). Mode d'échec n°2 des principes de simulation, présent dans le premier lot du moteur. |
| 7 | P2-2 — le gate ne vérifie le suivi git que de 2 fichiers déclarés sur 22 ; la forme `must_differ_from` du brief diverge de celle que lit le gate | CONFIRMED | `harness/verdict_audit.py` classe les chemins `../../../../` « outside the brief dir, not checked » (message construit ligne 183) ; le gate lit `must_differ_from` par fichier (ligne 206) alors que le § Execution Contract du brief 011 (lignes 295-301) montre une liste de paires à la racine du manifeste. Les deux moitiés du constat se vérifient dans le code et dans le brief. |
| 8 | P2-3 — le budget d'exécution exigé par le brief n'est ni mesuré ni waivé | CONFIRMED | `rg -n -i "budget|split-check|UNMEASURABLE" harness/queue/briefs/011-sim-monde-vivant-amorcage/deliverables/generator-log.md` : aucune correspondance ; `"waivers": []` au manifeste. La dérogation d'une ligne (UNMEASURABLE hors session Claude locale, documentée dans `AGENTS.md`) était disponible et n'a pas été posée. |
| 9 | P2-4 — mortalité en seuil binaire « si faim ≥ seuil alors −N% », déficit écrasé sans être compté | CONFIRMED | `sim/engine.py` : `if cell.hunger_ticks >= HUNGER_DEATH_THRESHOLD: deaths = cell.population * HUNGER_DEATH_RATE_PER_TICK` — la forme que le brief interdit en toutes lettres ; `remaining if remaining >= 0.0 else 0.0` fait disparaître le manque sans l'enregistrer. `sim/SEEDING.md` restreint bien l'interdiction « dans les tests », restriction absente du brief. L'ampleur du manque n'existe pas comme état. |
| 10 | P2-5 — aucun des 20 tests `sim/` ne tourne en intégration continue | CONFIRMED | `.github/workflows/harness-ci.yml`, job `tests` : `python -m pytest harness/tests/ -v`, aucune collecte de `sim/tests/`. La CI verte du commit audité n'a exécuté aucune ligne du code livré par la PR #57. |
| 11 | P3-1 — deux noms d'événement pour la même opération dans le registre de coût | CONFIRMED | `harness/queue/cost-ledger.jsonl` lignes 35-36 : `"event": "generator_run"` puis `"event": "generator-run"` pour le même brief 011 ; le défaut de l'outil est le tiret (`harness/backends/ledger.py`). Effet nul aujourd'hui, divergence de forme réelle. |
| 12 | P3-2 — nommages trompeurs dans l'amorçage (`daily_need` par tick, `INITIAL_FOOD_DAYS` en ticks) | CONFIRMED | `sim/world.py` ligne 37 (`daily_need = population × consommation par tick`) et `sim/constants.py` ligne 40 (`INITIAL_FOOD_DAYS = 30`, documenté en ticks). Même famille que le point 4. |

## 3. Points à porter au propriétaire (hors technique)

Aucun point de la table n'exige un arbitrage métier pour être tranché : les
douze se vérifient mécaniquement. Une question de gouvernance reste posée au
propriétaire, hors verdict : l'audit relève qu'aucun ADR ne couvre la
substitution « Cursor tient l'orchestration et les trois rôles quand Claude
est indisponible », employée sur instruction propriétaire depuis le
2026-08-12 (et de nouveau pour ce contre-audit même). Si cette substitution
doit durer, un ADR court la nommant, avec ses limites (traçage d'acteur,
angle mort du couple natif), fermerait l'écart entre la pratique et
ADR-0010.

## 4. Synthèse

Tout tient. Onze points sur douze sont confirmés par rejeu indépendant ;
le douzième (P1-1, `ROADMAP.md`) est partiel — l'en-tête de `ROADMAP.md`
autorise la correction factuelle signalée au commit, mais la forme de la
signature et le véhicule (commit post-verdict) restent fautifs. Les deux
mesures centrales de l'audit (monde sans faim, rng jamais consommé) se
reproduisent ligne à ligne. Recommandation de traitement : conversion en
brief — le prochain lot `sim/` doit faire vivre le monde (base de temps
unique, équilibre mesuré sur les 596 cellules réelles, déficit comme état,
rng réellement consommé, `adjacency` dotée d'un lecteur par le commerce
inter-cellules) et câbler `sim/tests/` en CI ; les points de harnais et de
gouvernance (1, 7, et la moitié « processus » de 2 et 8) relèvent d'un
brief de harnais séparé, à écrire ensuite, pour ne pas mélanger moteur et
outillage dans un même lot.
