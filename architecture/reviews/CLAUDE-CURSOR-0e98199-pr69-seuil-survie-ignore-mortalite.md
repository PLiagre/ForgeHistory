---
review_of: CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite
reviewer: claude-code
target_commit: 0e98199dac39a4a5a9a5f9d62f206c40d442d3f5
reviewed_at: 2026-08-13T12:00:00Z
---

# Contre-audit de CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `0e98199dac39a4a5a9a5f9d62f206c40d442d3f5`.
  Existe et est ancêtre de `HEAD` :
  `git cat-file -t 0e98199...` → `commit` ;
  `git merge-base --is-ancestor 0e98199... HEAD` → vrai.
- Parents annoncés (`538be56` côté master, `29913c0` côté lot) vérifiés :
  `git log -1 0e98199^1` → `538be56 hermes: tableau de bord régénéré` ;
  `git log -1 0e98199^2` → `29913c0 clôture de session (suite) : brief 013
  accepté …`.
- `git diff --stat 538be56..0e98199 | tail -1` → `22 files changed, 4011
  insertions(+), 114 deletions(-)` — identique au chiffre cité.
- `git log --format='%an' 538be56..29913c0 | sort | uniq -c` → `7 Cursor
  Agent` — identique.
- Portes mécaniques rejouées (environnement local, `pytest` installé pour
  l'occasion, aucun autre changement) :
  - `python harness/verdict_audit.py harness/queue/briefs/013-sim-tick-nourrit-une-fois`
    → `VERDICT: ACCEPT`.
  - `python -m pytest sim/tests/ -q` → `35 passed in 2.98s`.
  - `python -m pytest harness/tests/ -q` → `314 passed, 16 skipped in 7.75s`.
  - `python harness/harness_audit.py` → `SCORE: 20/24`.
  - `python harness/queue/briefs/013-sim-tick-nourrit-une-fois/deliverables/measure_sc6_013.py`
    → `pop_finale=51199297`, `morts=15666208`, `kg_transportes=2676487`,
    `fraction_survie=0.765706`, `cellules_affamees=536` — les quatre
    compteurs cités par l'audit, au chiffre près.
  Les cinq portes reproduisent exactement les valeurs annoncées § 2/§ 8.6.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | **P1 — le seuil dérivé (`SEUIL_SURVIE_POPULATION_FRACTION`, `sim/constants.py:135-142`) ignore `HUNGER_DEATH_SCALE`/`MAX_DEATH_RATE_PER_TICK` et varie en sens inverse de `DEFICIT_RECOVERY_RATE_PER_TICK`.** | **CONFIRMED** | Lecture du code : la formule (`SURVIE_MARGE_DERIVEE = _depassement_initial*_fraction_predite + _p_tick_deficitaire*DEFICIT_RECOVERY_RATE_PER_TICK`) ne référence ni `HUNGER_DEATH_SCALE` ni `MAX_DEATH_RATE_PER_TICK`. Rejeu indépendant (monkeypatch en mémoire sur `sim.engine`, seed 42, 200 ticks) : en faisant varier `HUNGER_DEATH_SCALE` (0.001→0.05) j'obtiens `survie mesurée` = 0.869657 / 0.765706 / 0.680871 / 0.551459 / 0.338088 pendant que le seuil reste à `0.748889` — identique au tableau § 8.3, au chiffre près. En faisant varier `DEFICIT_RECOVERY_RATE_PER_TICK` (0.00→1.00) j'obtiens `seuil`/`survie` = (0.8100, 0.150687) / (0.7794, 0.620905) / (0.7489, 0.765706) / (0.6572, 0.846542) / (0.5044, 0.869985) / (0.1989, 0.886762) — identique au tableau § 8.2, au chiffre près. La direction opposée (seuil ↓ quand survie ↑) est donc bien reproduite, pas une lecture erronée d'une table. J'ai aussi vérifié que les 4 régimes cassés par l'Évaluateur à l'itération 2 (`verdict.md:630-635` : densité doublée, production ×2, production ÷2, consommation ×2) portent tous sur l'approvisionnement — aucun ne touche `HUNGER_DEATH_SCALE`, `MAX_DEATH_RATE_PER_TICK` ni `DEFICIT_RECOVERY_RATE_PER_TICK`. La citation `verdict.md` (« Cela ne prouve pas que la composition des deux termes soit la bonne physique ») est exacte (ligne 641). Le point neuf de l'audit (sensibilité côté mortalité, jamais testée) tient. |
| 2 | **P2 — `deaths = int(population × death_rate)` rend une cellule immortelle tant que le déficit cumulé reste sous 200 kg, quelle que soit la population.** | **CONFIRMED** | Lecture de `sim/engine.py:215-237` (`_apply_mortality`) : sous le plafond, `death_rate = per_capita_deficit × HUNGER_DEATH_SCALE` et `deaths = int(population × death_rate) = int(food_deficit_kg × HUNGER_DEATH_SCALE)` — algébriquement indépendant de la population, confirmé par lecture directe, pas seulement par la table. Rejeu sur une `Cell` isolée : `(deficit=199, pop=20)→0 morts`, `(deficit=199, pop=200000)→0 morts`, `(deficit=200, pop=20)→1 mort`, `(deficit=200, pop=200000)→1 mort` — identique § 8.4. Rejeu sur le monde réel (seed 42, 200 ticks, instrumentation indépendante) : `cellules-ticks en déficit=76932`, `troncature→0 morts=37384 (48.6 %)`, `morts fractionnaires perdus=24345.7`, `cellules-ticks pop<10 en déficit=0` — les quatre chiffres du § 8.5 reproduits exactement. |
| 3 | **P3 — `MAX_DEATH_RATE_PER_TICK` n'est jamais atteint sur le monde réel (0/76932) et le faire varier de 0.02 à 0.30 ne change quasiment rien.** | **CONFIRMED** | Rejeu indépendant (instrumentation post-tick, seed 42) : `76932/76932` cellules-ticks en déficit sous le plafond, `0/76932` au plafond — identique § 8.4. Sensibilité rejouée (monkeypatch `MAX_DEATH_RATE_PER_TICK` ∈ {0.02, 0.05, 0.10, 0.30}) : survie = 0.769788 / 0.765706 / 0.765706 / 0.765706 — identique § 8.3 (seul le premier palier bouge, ensuite plat). |
| 4 | **P3 — `_update_hunger` confond « stock à zéro après avoir mangé sa ration » avec « en manque » ; le compteur publié `cellules_affamees_monde_reel_re=536` n'est cependant pas gonflé par cet effet.** | **CONFIRMED** | Lecture de `sim/engine.py:201-212` : le test est `if cell.food_stock_kg <= 0.0`, donc une cellule nourrie exactement à son besoin (stock final = 0) est comptée « affamée » au même titre qu'une cellule en déficit réel — confirmé par simple lecture, sans ambiguïté. Sonde reconstruite indépendamment (deux cellules adjacentes, une avec surplus, l'autre nourrie entièrement par le commerce) : les deux terminent à `stock=0.0, deficit=0.0, hunger_ticks=1` — identique § 8.1 § A. Sur le monde réel (seed 42, 200 ticks) : 536 cellules ont eu `hunger_ticks>0` au moins une fois, et 0 d'entre elles n'ont **jamais** eu `food_deficit_kg>0` — identique § 8.5 (« zéro » cellule affamée sans déficit réel). Le cadrage adverse de l'audit (chercher si le compteur publié est gonflé, et conclure que non) est donc lui aussi vérifié, pas seulement affirmé. |
| 5 | **P3 — classification CI du commit (5 workflows `push` verts) et de la PR #69 (14 pass / 3 skipping / 1 pending) ; ledger `a4de4bb` bloqué à `AUDIT_CONVERTED` alors que le brief est fusionné depuis 12:48.** | **PARTIAL** | La partie ledger est **CONFIRMED** : `architecture/audit-ledger.jsonl` ligne 46 → dernier événement pour `CURSOR-a4de4bb...` est `AUDIT_CONVERTED` à `08:40:34Z` ; le commit de fusion `0e98199` est horodaté `2026-08-13 12:48:46 +0200` — l'écart est réel. La partie GitHub Actions/checks PR (5 workflows `push` verts, 14/3/1 sur la PR) n'a **pas pu être rejouée** dans cet environnement : `gh` n'est pas authentifié ici (pas de token), donc `gh run list` / `gh pr checks` échouent sans preuve indépendante possible. Je ne peux ni confirmer ni réfuter cette sous-partie faute d'accès. Sévérité P3, déjà classée comme « non ré-instruite » par l'audit lui-même : l'absence de contre-preuve locale ne change rien à la portée du point. |
| 6 | **§ 4 — Ce qui tient : le P0 du lot précédent (double alimentation) est fermé ; le transport ne franchit qu'une arête ; le compteur de transport mesure des kg réellement arrivés (écart 0.0) ; les 4 compteurs se reproduisent au chiffre près ; le déficit a une mémoire (10 %/tick).** | **CONFIRMED** | P0 fermé : reproduit ci-dessus (point 4, sonde A). Transport à une arête : monde à 3 cellules reconstruit indépendamment (`1→2→3`, seule 1 a du stock, 2 et 3 ont une population qui consomme), deux ordres d'arêtes testés — cellule 3 termine à `stock=0.0` dans les deux cas, cellule 1 passe de 1000 à 800 (capacité d'arête 200 kg/tick) — cohérent avec § 8.1 § B (écart mineur de mise en scène du probe, invariant identique). 4 compteurs du monde réel : reproduits au chiffre près (§ 1 ci-dessus). Le calcul d'écart kg comptés/kg arrivés et le mécanisme de mémoire du déficit (`sim/engine.py:177,188`, facteur `(1 - DEFICIT_RECOVERY_RATE_PER_TICK)`) sont cohérents avec le code lu ; non rejoués isolément (redondants avec les points déjà vérifiés). |
| 7 | **§ 1 — Routage des points de l'audit `a4de4bb` dans le brief 013 (points 1,2,5,6,7,10 traités ; 3,9→brief 014 ; 4→propriétaire).** | **CONFIRMED** | `harness/queue/briefs/013-sim-tick-nourrit-une-fois/brief.md:11` → « Points retenus : 1, 2, 5, 6, 7 (physique du moteur) ; 10 (CI, état de fait) ; 3, 4, 8, 9 → voir § Non-Goals » ; § Non-Goals ligne 1 renvoie les constats 3 et 9 au brief 014, ligne 2 renvoie le constat 4 au propriétaire comme question de gouvernance — correspond exactement à la description de l'audit. |
| 8 | **Citations exactes (`verdict.md` ligne ~641, `generator-log.md` ligne ~394, `manifest.json` § waivers).** | **CONFIRMED** | Les trois citations ont été retrouvées mot pour mot aux emplacements indiqués (numéros de ligne à ±1-2 lignes près, contenu identique). |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **Constat 1 (P1) — que faire du seuil de survie ?** C'est un arbitrage de
  méthode, pas seulement un bug : le seuil actuel *fonctionne* (il rougit
  bien sous des perturbations d'approvisionnement) mais ne modélise pas la
  mortalité. L'audit propose un remède minimal (comparer survie mesurée à
  une survie *prédite par un modèle de mortalité*, § 6.1) — c'est au
  propriétaire de décider si ce remède justifie un brief maintenant ou si
  le P1 (pas P0) permet d'attendre.
- **Constat 2 (P2) — l'immunité sous 200 kg est-elle un risque réel
  aujourd'hui ?** L'audit lui-même la nuance : 0,16 % des morts du monde
  réel, et aucune cellule ne passe sous 10 habitants sur cette
  simulation. C'est un jugement produit/risque (le défaut apparaîtra
  seulement quand des cellules deviendront petites) que je ne peux pas
  trancher techniquement.
- **Point 5 (CI) — la sous-partie GitHub Actions non rejouable ici.** Le
  propriétaire (ou un agent avec accès `gh` authentifié) doit revérifier
  cette sous-partie s'il veut une confirmation indépendante ; je ne l'ai ni
  confirmée ni réfutée.

## 4. Synthèse

Tout ce qui était vérifiable en local a été rejoué indépendamment et
reproduit **exactement** : les cinq portes mécaniques, les quatre compteurs
publiés, les deux tables de sensibilité (§ 8.2, § 8.3), les agrégats de
troncature de mortalité (§ 8.5), la sonde A du P0 fermé, le routage des
points de l'audit précédent dans le brief 013, et les trois citations de
`verdict.md`/`generator-log.md`/`manifest.json`. Rien de ce qui a pu être
testé ne s'effondre : cet audit est d'une rigueur inhabituelle — chaque
affirmation chiffrée est reproductible au chiffre près par un tiers, ce qui
n'est pas garanti par construction (aucune sonde n'est dans le dépôt).

Seule zone non vérifiable ici : la sous-partie GitHub Actions/checks PR du
constat 5 (P3, déjà classée comme non ré-instruite), faute de `gh`
authentifié dans cet environnement — marquée PARTIAL, pas REFUTED.

Aucun désaccord technique avec le corps de l'audit. Les deux briefs
proposés (§ 6) sont bien fondés sur les constats 1 et 2, tels que confirmés
ci-dessus ; leur priorité relative face au reste de la file reste un
arbitrage du propriétaire, pas un point technique.
