# Eval Rubric — Brief 017 : Le seuil de survie honnête

**Authored**: 2026-08-13T20:30:00Z
**Author**: forge-planificateur

Ce document est rédigé par le Planificateur AVANT tout code.
L'Évaluateur l'applique sans le modifier.
Voir `docs/rules/harness-roles.md` et `docs/rules/simulation-principles.md`.

Note de transparence : ce brief est rédigé par le rôle `forge-planificateur`
orchestré par un agent Cursor Cloud remplaçant le CTO Claude (quota/plafond).
L'acteur réel est Cursor Cloud.

---

## Guide de lecture

Pour chaque condition de succès :

- **Vérification** : commande à rejouer ou contre-preuve à monter (depuis la
  racine, jamais `python` nu — toujours `.venv/bin/python`).
- **Reconstruction indépendante** : l'Évaluateur re-dérive la valeur sans
  reprendre les valeurs du manifeste.
- **Résultat attendu** : ce que le Générateur doit avoir produit.
- **Contre-preuve disqualifiante** : comportement qui invalide la condition
  même si les tests mécaniques passent.

---

## SC1 — Modèle de survie prédit : dépend des constantes de mortalité, cible l'état stationnaire

**Vérification de la formule dans `sim/constants.py` :**

1. Lire `sim/constants.py` : `SURVIE_FRACTION_PREDITE_STATIONNAIRE` doit être
   une expression Python (pas un littéral). Chercher par :
   ```py
   .venv/bin/python -c "
   import ast, sys
   src = open('sim/constants.py').read()
   tree = ast.parse(src)
   for node in ast.walk(tree):
       if isinstance(node, ast.Assign):
           for t in node.targets:
               if hasattr(t, 'id') and 'PREDITE_STATIONNAIRE' in t.id:
                   print('trouve:', t.id)
                   print('est literal num ?', isinstance(node.value, ast.Constant))
   "
   ```
   Résultat attendu : `est literal num ? False`.

2. Vérifier que `HUNGER_DEATH_SCALE`, `MAX_DEATH_RATE_PER_TICK`,
   `DEFICIT_RECOVERY_RATE_PER_TICK` (ou son successeur nommé) apparaissent
   dans l'expression. Chercher :
   ```py
   .venv/bin/python -c "
   src = open('sim/constants.py').read()
   for name in ['HUNGER_DEATH_SCALE', 'MAX_DEATH_RATE_PER_TICK',
                'DEFICIT_RECOVERY_RATE']:
       print(f'{name}: {name in src}')
   "
   ```

3. Vérifier les signes des propriétés dans un script séparé (copie hors dépôt
   ou monkeypatch en mémoire) :
   ```py
   .venv/bin/python -c "
   import importlib, sim.constants as C
   base = C.SURVIE_FRACTION_PREDITE_STATIONNAIRE
   C.HUNGER_DEATH_SCALE = C.HUNGER_DEATH_SCALE * 2
   importlib.reload(C)
   haut = C.SURVIE_FRACTION_PREDITE_STATIONNAIRE
   print('HDS x2 -> SURVIE_PREDITE diminue ?', haut < base)
   "
   ```
   Résultat attendu : `True`.

4. Lire `sim/SEEDING.md` section SC1 brief 017 : la formule ET
   `SURVIE_TOLERANCE_STATIONNAIRE` ET `SURVIE_CONVERGENCE_DELTA` sont
   documentées AVANT toute citation d'un compteur mesuré (`fraction_survie`,
   population, etc.).

**Vérification du test de conformité :**

5. Rejouer :
   ```py
   .venv/bin/python -m pytest sim/tests/test_survie_stationnaire.py -v
   ```
   Résultat attendu : PASSED. Le test doit imprimer `ecart = ...` et `dans_tolerance = True`.

6. Vérifier que le test vérifie DEUX conditions :
   a. La convergence : `|s(N_STAT_SURVIE) - s(N_STAT_SURVIE // 2)| ≤ SURVIE_CONVERGENCE_DELTA`.
   b. La conformité : `|s(N_STAT_SURVIE) - SURVIE_FRACTION_PREDITE_STATIONNAIRE| ≤ SURVIE_TOLERANCE_STATIONNAIRE`.

**Reconstruction indépendante :**
L'Évaluateur lit `sim/constants.py`, recalcule manuellement `SURVIE_FRACTION_PREDITE_STATIONNAIRE`
avec les constantes actuelles, et vérifie que la valeur se modifie quand
`HUNGER_DEATH_SCALE` est doublée (calcul à la main ou en session Python
hors dépôt).

**Contre-preuve de sensibilité (Paire A, `sim/tests/proof_red/`) :**
Le fichier `run_sensibilite_hds_red.txt` doit contenir au moins un `FAILED`.
L'Évaluateur monte sa propre contre-preuve (remplacer `HUNGER_DEATH_SCALE`
par un littéral dans la formule de prédiction) et rejoue le test de
sensibilité → doit FAILED.

**Résultat attendu :** PASS si la formule dépend explicitement de
`HUNGER_DEATH_SCALE`, est documentée avant mesure, et que la convergence +
conformité sont vérifiées à l'horizon N_STAT_SURVIE.

---

## SC2 — Sensibilité prouvée : mesure et prédiction bougent dans le même sens

**Vérification :**

1. Rejouer :
   ```py
   .venv/bin/python -m pytest sim/tests/test_sensibilite_survie.py -v
   ```
   Résultat attendu : PASSED sur tous les régimes (au minimum `HUNGER_DEATH_SCALE`
   ×0.5 et ×2, `DEFICIT_RECOVERY_RATE_PER_TICK` ×2 ou son successeur).

2. Vérifier que le test utilise `World.from_g3(rng_seed=42)`, N ≥ 200 ticks,
   jamais un monde à zéro cellule.

3. Vérifier que la tolérance `SURVIE_TOLERANCE_SENSIBILITE` est une expression
   dans `sim/constants.py` (pas un littéral ajusté) ET documentée dans
   `sim/SEEDING.md` section SC2 brief 017 AVANT toute citation de mesure.

**Reconstruction indépendante :**
L'Évaluateur écrit son propre script hors dépôt : monde réel N=200, modifie
`HUNGER_DEATH_SCALE` ×0.5 et ×2 en mémoire (`import sim.constants as C;
C.HUNGER_DEATH_SCALE = ...`), mesure s_0.5, s_nom, s_2. Vérifie :
`s_0.5 > s_nom > s_2` ET
`SURVIE_FRACTION_PREDITE_STATIONNAIRE(×0.5) > SURVIE_FRACTION_PREDITE_STATIONNAIRE(nom) > SURVIE_FRACTION_PREDITE_STATIONNAIRE(×2)`.

**Contre-preuve disqualifiante :**
Si `SURVIE_FRACTION_PREDITE_STATIONNAIRE` reste constant quand `HUNGER_DEATH_SCALE`
varie (comme dans l'audit P1 de 0e98199), SC2 est non satisfaite même si le
test général passe. Le fichier `run_sensibilite_hds_red.txt` doit attester
que le test rougissait avant correction.

**Résultat attendu :** PASS si les deux conditions (direction + tolérance) sont
satisfaites pour tous les régimes, avec documentation avant mesure.

---

## SC3 — Accumulateur de mortalité fractionnaire

**Vérification du champ :**

1. Lire `sim/model.py` : `Cell` doit avoir un champ `mortality_remainder`
   (type float, valeur par défaut `-1.0`, sentinel hard-won rule 8).
   ```py
   .venv/bin/python -c "
   from sim.model import Cell
   c = Cell(cell_id=1, area_km2=1.0, population=10)
   print('mortality_remainder =', c.mortality_remainder)
   print('sentinel -1 ?', c.mortality_remainder == -1.0)
   "
   ```
   Résultat attendu : `sentinel -1 ? True`.

2. Lire `sim/engine.py`, `_apply_mortality()` : la formule doit :
   - Lire le reste précédent (`remainder = cell.mortality_remainder if >= 0 else 0.0`)
   - Calculer `raw = cell.population * death_rate + remainder`
   - Appliquer `deaths = int(raw)`
   - Persister `cell.mortality_remainder = raw - deaths`
   Vérifier l'absence de `int(population * death_rate)` sans reste.

**Vérification des tests :**

3. Rejouer :
   ```py
   .venv/bin/python -m pytest sim/tests/test_mortalite_accumulateur.py -v
   ```
   Résultat attendu : PASSED. Vérifier que le test « 5 habitants famine totale »
   énonce sa borne `N_BOUND_MORT` avec justification (dérivée de
   `MAX_DEATH_RATE_PER_TICK`).

4. Vérifier que le test de précision vérifie :
   `|somme_morts_appliques - somme_exacte_pop_x_taux| ≤ 1` par cellule.

**Reconstruction indépendante :**
L'Évaluateur crée une cellule `population=5`, `food_deficit_kg` très grand
(≥ 5/HUNGER_DEATH_SCALE de sorte que `death_rate = MAX_DEATH_RATE_PER_TICK`),
`mortality_remainder = -1.0`. Appelle `_apply_mortality()` en boucle et
note le tick où `deaths ≥ 1`. Doit être ≤ `ceil(1 / MAX_DEATH_RATE_PER_TICK)` = 10.

**Contre-preuve (Paire B, `sim/tests/proof_red/`) :**
Le fichier `run_accumulateur_mort_red.txt` doit contenir au moins un `FAILED`.
L'Évaluateur monte sa propre contre-preuve (remettre `deaths = int(pop * rate)`
sans reste) et rejoue `test_famine_tue_en_borne_de_ticks` → doit FAILED.

**Résultat attendu :** PASS si le champ `mortality_remainder` existe, la formule
est correcte, et les deux tests passent. FAIL si une cellule de 5 habitants
ne perd aucun mort en famine totale sur les 10 premiers ticks.

---

## SC4 — « Affamée » = en manque ce tick, pas garde-manger vide

**Vérification :**

1. Lire `sim/engine.py` : le critère d'incrémentation de `hunger_ticks` dans
   `_update_hunger()` ne doit pas être `food_stock_kg <= 0.0`. Chercher :
   ```py
   .venv/bin/python -c "
   src = open('sim/engine.py').read()
   # La condition 'food_stock_kg <= 0' seule est le critère brisé
   import re
   hits = re.findall(r'food_stock_kg\s*<=\s*0', src)
   print('critere brise toujours present ?', len(hits) > 0)
   "
   ```

2. Rejouer :
   ```py
   .venv/bin/python -m pytest sim/tests/test_hunger_criterion.py -v
   ```
   Résultat attendu : PASSED. Vérifier que le test inclut le scénario
   témoin/receveuse (ration exacte fournie par commerce, déficit=0) →
   `hunger_ticks == 0`.

**Reconstruction indépendante :**
L'Évaluateur construit un monde à deux cellules : source abondante, receveuse
avec stock nul mais déficit nul (commerce livre exactement le besoin). Exécute
un tick complet. Vérifie que `receveuse.hunger_ticks == 0` après le tick.

**Contre-preuve disqualifiante :**
Remettre `if food_stock_kg <= 0.0: hunger_ticks += 1`. La receveuse nourrie
exactement à son besoin termine à `stock=0, deficit=0, hunger_ticks=1`. Si le
test SC4 passe malgré ça, la garde n'est pas fonctionnelle.

**Résultat attendu :** PASS si `hunger_ticks` n'est incrémenté que lors d'une
pénurie réelle ce tick (non un stock vide sans manque).

---

## SC5 — Récupération du déficit : physique (kilogrammes effectivement consommés)

**Vérification :**

1. Lire `sim/engine.py`, `_apply_consumption()` : lors d'un tick de surplus
   (remaining > 0), la réduction de `food_deficit_kg` doit être bornée par
   `remaining` (le surplus en kg ce tick). La formule
   `food_deficit_kg × (1 - DEFICIT_RECOVERY_RATE_PER_TICK)` (réduction
   indépendante du surplus) ne doit plus apparaître.

2. Lire `sim/SEEDING.md` section SC5 brief 017 : la conversion (ratio 1:1 ou
   constante nommée) est documentée. Si `DEFICIT_RECOVERY_RATE_PER_TICK` est
   conservée avec sémantique modifiée, la nouvelle sémantique est expliquée.

3. Rejouer :
   ```py
   .venv/bin/python -m pytest sim/tests/test_deficit_physique.py -v
   ```
   Résultat attendu : PASSED sur les deux tests (surplus infinitésimal et
   surplus proportionnel).

**Reconstruction indépendante :**
L'Évaluateur construit : `food_deficit_kg=10000`, `population=10`,
`food_stock_kg = population × FOOD_CONSUMPTION + 1e-9` (surplus = 1e-9 kg).
Appelle `_apply_consumption()`. Vérifie : `food_deficit_kg > 9999.9`
(réduction ≤ 1e-9, pas 1000 kg comme avec l'ancienne formule).

**Contre-preuve disqualifiante :**
Remettre `food_deficit_kg × (1 - DEFICIT_RECOVERY_RATE_PER_TICK)`. Un surplus
de 1e-9 kg efface `10000 × 0.1 = 1000 kg` de déficit → test FAILED.

**Résultat attendu :** PASS si `deficit_reduction ≤ surplus_kg` ce tick dans
tous les cas.

---

## SC6 — Re-mesure du monde réel (017)

**Vérification :**

1. Rejouer le script de mesure :
   ```py
   .venv/bin/python harness/queue/briefs/017-sim-seuil-survie-honnete/deliverables/measure_sc6_017.py
   ```
   Les quatre compteurs doivent satisfaire simultanément :
   - `cellules_affamees_monde_reel_017` > 0 (sur 596 cellules — dénominateur déclaré)
   - `morts_cumules_monde_reel_017` > 0
   - `kg_transportes_monde_reel_017` > 0
   - `fraction_survie_monde_reel_017` : valeur tracée dans le manifeste (pas de
     borne minimale imposée ici — la fraction changera légitimement avec SC5)

2. Vérifier que le script utilise `World.from_g3(rng_seed=42)`,
   `random.Random(42)`, N = N_STAT_SURVIE ticks.

3. Vérifier que `cellules_affamees_monde_reel_017` est mesuré avec la
   **nouvelle définition SC4** (pénurie réelle, pas garde-manger vide).

**Vérification archives intactes :**
```py
# Depuis la racine
.venv/bin/python -c "
import subprocess
result = subprocess.run(
    ['git', 'diff', 'HEAD', '--', 'harness/queue/briefs/012-monde-vivant-commerce-inter-cellules/',
     'harness/queue/briefs/013-sim-tick-nourrit-une-fois/'],
    capture_output=True, text=True)
print('diff archives 012/013 :', 'vide' if not result.stdout else 'ATTENTION : non vide')
"
```
Résultat attendu : diff vide.

**Reconstruction indépendante :**
L'Évaluateur écrit son propre script depuis `World.from_g3(rng_seed=42)`,
`random.Random(42)`, N_STAT_SURVIE ticks, calcule les quatre compteurs. Les
valeurs doivent correspondre au manifeste.

**Résultat attendu :** PASS si les quatre conditions tiennent et le script est
committé reproductible.

---

## SC7 — Tests 013 adaptés ; suite complète verte

**Vérification :**

1. Lire `deliverables/generator-log.md`, section dédiée aux adaptations des
   tests précédents : chaque test adapté ou remplacé est nommé avec sa motivation.
   En particulier `test_survie_derivee.py::test_fraction_dans_marge` est soit
   supprimé (motivation écrite), soit adapté au nouveau modèle SC1.

2. Rejouer la suite complète :
   ```py
   .venv/bin/python -m pytest sim/tests/ -v
   .venv/bin/python -m pytest harness/tests/ -q
   ```
   Aucun `FAILED`. Les `SKIP` Linux (tests Unity) sont acceptés.

3. Vérifier l'absence de suppression silencieuse :
   tout fichier de test du brief 013 disparu doit avoir une entrée dans le journal.

**Résultat attendu :** PASS si la suite est verte ET chaque adaptation est
motivée par écrit.

---

## SC8 — Registre de coût

**Vérification :**

1. `.venv/bin/python harness/backends/ledger.py report` → le brief
   `017-sim-seuil-survie-honnete` apparaît avec au moins `cursor=1`.

2. Lire la dernière ligne ajoutée à `harness/queue/cost-ledger.jsonl` :
   `"event": "generator-run"`, brief contient `017`, audit_id contient
   `CURSOR-0e98199` ou `CURSOR-29913c0`.

**Résultat attendu :** PASS si la ligne est présente avec les bons champs.

---

## Preuves rouges — vérification des deux paires

**Paire A — Sabotage « prédiction aveugle à HUNGER_DEATH_SCALE » :**
- `sim/tests/proof_red/run_sensibilite_hds_red.txt` : au moins un `FAILED`.
- `sim/tests/proof_red/run_sensibilite_hds_green.txt` : uniquement `PASSED`.
- L'Évaluateur monte sa propre contre-preuve (remplacer `HUNGER_DEATH_SCALE`
  par un littéral dans `SURVIE_FRACTION_PREDITE_STATIONNAIRE`) → test FAILED.
- Le gate `captures_differ_when_should` a vérifié que les deux fichiers diffèrent.

**Paire B — Sabotage « int() sans accumulateur de mort » :**
- `sim/tests/proof_red/run_accumulateur_mort_red.txt` : au moins un `FAILED`.
- `sim/tests/proof_red/run_accumulateur_mort_green.txt` : uniquement `PASSED`.
- L'Évaluateur monte sa propre contre-preuve (retirer `mortality_remainder`,
  revenir à `deaths = int(pop * rate)`) → test `test_famine_tue_en_borne_de_ticks` FAILED.

---

## Gate mécanique

```py
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/017-sim-seuil-survie-honnete
```
Doit retourner `VERDICT: ACCEPT` avec tous les contrôles au vert avant que
l'Évaluateur rédige son verdict de fond.

**Avertissement :** le gate juge la forme du lot, pas sa substance. Un lot peut
obtenir `VERDICT: ACCEPT` du gate tout en recevant `FAIL` de l'Évaluateur de fond.

---

## Échecs disqualifiants

| Comportement | Raison |
|---|---|
| `SURVIE_FRACTION_PREDITE_STATIONNAIRE` ne varie pas quand `HUNGER_DEATH_SCALE` change de régime | SC1+SC2 non satisfaites — signe absent, le critère est aveugle à la mortalité |
| Tolérance ou convergence calibrées APRÈS observation des compteurs | Mode d'échec n°5 (calibration après mesure) — hard-won rule 2 ; SC1 non satisfaite |
| `mortality_remainder` absent ou non persisté d'un tick au suivant | SC3 non satisfaite — la troncature jette encore les morts fractionnaires |
| Cellule de 5 habitants ne perd aucun mort en famine totale en ≤ N_BOUND_MORT ticks | SC3 non satisfaite — immunité structurelle subsiste |
| `hunger_ticks` incrémenté pour une cellule dont le déficit est resté à 0 ce tick | SC4 non satisfaite — compteur dit « affamée » une cellule rassasiée |
| Réduction de `food_deficit_kg` non bornée par le surplus_kg ce tick | SC5 non satisfaite — principe 3 violé (kilogrammes de dette disparaissent sans contrepartie physique) |
| Recalibration de `HUNGER_DEATH_SCALE`, `MAX_DEATH_RATE_PER_TICK` ou `FOOD_*` pour faire rentrer un compteur | Masque l'effet réel des corrections sur le monde simulé |
| Réintroduction de `max(1, …)` sur la mortalité | Brief 013 SC4 violé — propriété acquise |
| Modification des archives briefs 012 et 013 | Archives intangibles |
| Compteur SC6 mesuré sur un monde construit à la main ou depuis zéro cellule | Mode d'échec n°6 — échantillon vide passe en silence |
| Condensé SHA256 recopié en valeur hexadécimale dans un test ou document | Hard-won rule 12 — piège pour tout brief ultérieur |
| `python` nu dans une commande (au lieu de `.venv/bin/python`) | Hard-won rule 1 |
| Suppression silencieuse d'un test 013 sans motivation écrite dans le journal | Traçabilité brisée |
