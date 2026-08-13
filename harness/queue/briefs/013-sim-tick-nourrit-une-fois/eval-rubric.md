# Eval Rubric — Brief 013 : Le tick nourrit une fois — commerce avant consommation, transport à une arête, mortalité continue

**Authored**: 2026-08-13T08:43:00Z
**Author**: forge-planificateur

Ne pas modifier après avoir vu les livrables du Générateur.
Le Planificateur écrit la rubrique avant tout code ; l'Évaluateur l'applique sans la réécrire.

---

## Guide de lecture

Chaque entrée correspond à une condition de succès du `brief.md`. Pour chaque condition :
- **Vérification** : commande à rejouer ou contre-preuve à monter (depuis une copie hors dépôt = répertoire temporaire sans lien git, sans toucher aucun fichier du dépôt).
- **Reconstruction indépendante** : l'Évaluateur re-dérive le compteur lui-même, sans reprendre les valeurs du manifeste.
- **Résultat attendu** : ce que le Générateur doit avoir produit.
- **Contre-preuve disqualifiante** : comportement qui invalide la condition même si les tests passent.

---

## SC1 — Le commerce précède la consommation ; un kilogramme transfère nourrit exactement une fois

**Vérification de l'ordre du tick :**

1. Lire `sim/engine.py`, fonction `tick()` : l'ordre des appels doit être production → commerce → consommation → faim → mortalité. La boucle `for cell in world.cells.values()` appelant `_apply_consumption` doit se trouver **après** l'appel à `_apply_commerce`, pas avant.

2. Lire `sim/engine.py`, fonction `_apply_commerce()` : aucune ligne ne modifie `food_deficit_kg` sur les cellules (ni décrémentation, ni assignation). Chercher par `rg "food_deficit_kg" sim/engine.py` — les seules occurrences dans `_apply_commerce` doivent être des **lectures** (conditions `> 0`), jamais des écritures.

**Vérification du test d'unicité :**

3. Rejouer depuis la racine :
   ```
   .venv/bin/python -m pytest sim/tests/test_tick_nourrit_une_fois.py::test_ecart_temoin_vs_receveuse -v
   ```
   Résultat attendu : `PASSED`. Vérifier que le test imprime `ecart_stock = 0.0` (ou ≤ 1e-9).

**Reconstruction indépendante :**
L'Évaluateur construit les deux cellules manuellement (témoin et receveuse) dans un script séparé, exécute un tick complet avec `area_km2=0.0` pour désactiver la production, et vérifie l'égalité des stocks finaux. Si l'écart est non nul, SC1 est non satisfaite.

**Résultat attendu :** PASS si l'ordre est correct ET si l'écart est nul. FAIL si `_apply_commerce` modifie `food_deficit_kg`, ou si l'écart de stock entre témoin et receveuse est non nul.

---

## SC2 — Transport atomique ; invariance à l'ordre des arêtes

**Vérification du snapshot :**

1. Lire `sim/engine.py`, `_apply_commerce()` : la fonction doit d'abord construire un snapshot (dict ou structure équivalente) des états `(food_stock_kg, food_deficit_kg)` de toutes les cellules, puis calculer les transferts depuis ce snapshot, puis les appliquer. Une variable locale holding `surplus_snapshot` ou équivalent doit être visible. Aucun stock ne doit être muté à l'intérieur de la boucle de calcul.

2. Lire `sim/SEEDING.md` : une section SC2 brief 013 documente la règle d'allocation déterministe (tri par `cell_id` croissant en cas de plusieurs demandeurs sur le même excédent).

**Vérification des tests :**

3. Rejouer :
   ```
   .venv/bin/python -m pytest sim/tests/test_tick_nourrit_une_fois.py::test_chaine_1_2_3 -v
   ```
   Résultat attendu : `PASSED`. Vérifier que le stock de la cellule 3 (non adjacente à la source) est 0.0 après le tick.

4. Rejouer :
   ```
   .venv/bin/python -m pytest sim/tests/test_tick_nourrit_une_fois.py::test_invariance_ordre_aretes -v
   ```
   Résultat attendu : `PASSED`. L'Évaluateur vérifie que le test exécute bien deux ordres d'arêtes différents (et non le même ordre deux fois).

**Reconstruction indépendante :**
L'Évaluateur monte le scénario chaîne 1–2–3 dans un script séparé, exécute les deux ordres d'arêtes, compare les états finaux.

**Résultat attendu :** PASS si le snapshot est utilisé ET si les deux tests passent. FAIL si la cellule 3 reçoit de la nourriture dans la chaîne 1–2–3, ou si l'état final dépend de l'ordre des arêtes.

---

## SC3 — Seuil de survie dérivé analytiquement

**Vérification de la dérivation :**

1. Lire `sim/constants.py` : `SEUIL_SURVIE_POPULATION_FRACTION` doit être une expression Python calculée depuis `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK`, `FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK`, `INITIAL_POPULATION_PER_KM2`, `RNG_YIELD_LOW`, `RNG_YIELD_HIGH`, et `SURVIE_MARGE_DERIVEE`. Aucun littéral numérique `0.70` (ou autre valeur calibrée) ne doit apparaître comme unique définition.

2. Lire `sim/SEEDING.md` : une section SC3 brief 013 documente la formule analytique et la justification de `SURVIE_MARGE_DERIVEE`. La justification doit précéder la mesure (elle ne peut pas citer une valeur observée pour se justifier — cf. échecs disqualifiants).

3. Vérifier analytiquement que la formule donne 0.9 avec les constantes actuelles :
   ```
   .venv/bin/python -c "
   from sim.constants import (FOOD_PRODUCTION_KG_PER_KM2_PER_TICK,
     FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK, INITIAL_POPULATION_PER_KM2,
     RNG_YIELD_LOW, RNG_YIELD_HIGH, SURVIE_MARGE_DERIVEE,
     SEUIL_SURVIE_POPULATION_FRACTION)
   rendement_moyen = (RNG_YIELD_LOW + RNG_YIELD_HIGH) / 2
   cap = FOOD_PRODUCTION_KG_PER_KM2_PER_TICK * rendement_moyen / FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK
   pred = cap / INITIAL_POPULATION_PER_KM2
   print('fraction_predite =', pred)
   print('SEUIL =', SEUIL_SURVIE_POPULATION_FRACTION)
   print('coherent:', abs(SEUIL_SURVIE_POPULATION_FRACTION - (pred - SURVIE_MARGE_DERIVEE)) < 1e-9)
   "
   ```
   Résultat attendu : `fraction_predite = 0.9`, `coherent: True`.

4. Rejouer :
   ```
   .venv/bin/python -m pytest sim/tests/test_survie_derivee.py::test_fraction_dans_marge -v
   ```
   Résultat attendu : `PASSED`.

**Contre-preuve de falsifiabilité (copie hors dépôt) :**
Dans une copie hors dépôt, doubler `INITIAL_POPULATION_PER_KM2` (de 10 à 20). Recalculer `fraction_predite` → 0.45. Rejouer `test_fraction_dans_marge`. La fraction mesurée sur le monde réel (~0.80) sera hors de la fenêtre `[0.45 - marge, 0.45 + marge]` → doit FAILED. Si le test reste PASSED, SC3 n'est pas satisfaite.

**Résultat attendu :** PASS si la formule est correcte, documentée avant mesure, et que le test peut échouer.

---

## SC4 — Mortalité continue et plafonnée ; déficit à mémoire graduelle

**Vérification du retrait du plancher :**

1. Lire `sim/engine.py`, `_apply_mortality()` : aucune ligne ne contient `max(1,` ni `max(1 ,`. La formule de calcul de `deaths` est `int(population × death_rate)` sans minimum de 1.

2. Lire le commentaire de `sim/tests/test_causal_chain.py`, `test_sc7c_population_decreases_when_deficit_positive` : le commentaire `max(1, int(100 × 0.025)) = max(1, 2) = 2` doit être corrigé pour refléter la nouvelle formule (`int(100 × 0.025) = 2`). Le test lui-même reste valide.

**Vérification du plafond pour toutes les populations :**

3. Rejouer :
   ```
   .venv/bin/python -m pytest sim/tests/test_mortalite_continue.py::test_plafond_toute_population -v
   ```
   Résultat attendu : `PASSED`. L'Évaluateur vérifie que le test couvre bien les populations 1, 5 et 9 (celles qui dépassaient le plafond avec l'ancien code).

**Reconstruction indépendante du plafond :**
L'Évaluateur crée une cellule avec `population=1`, `food_deficit_kg=1e-9` et appelle `_apply_mortality()` : `deaths` doit être `int(1 × rate)` = 0 (puisque `rate = 1e-9 × 0.005 ≈ 5e-12 << 0.1`). Avant la correction, deaths aurait été 1, soit un taux effectif de 1.0 >> 0.10.

**Vérification de la récupération graduelle :**

4. Lire `sim/constants.py` : `DEFICIT_RECOVERY_RATE_PER_TICK` est déclaré avec une valeur dans (0.0, 1.0).

5. Lire `sim/SEEDING.md` : une section SC4 brief 013 documente la justification de cette valeur (physique attendue de la récupération — par exemple : « une semaine de surplus efface un déficit accumulé sur autant de semaines »).

6. Rejouer :
   ```
   .venv/bin/python -m pytest sim/tests/test_mortalite_continue.py::test_deficit_non_efface_en_1_tick -v
   ```
   Résultat attendu : `PASSED`.

**Contre-preuve du plancher déguisé (copie hors dépôt) :**
Dans une copie hors dépôt, remettre `deaths = max(1, int(population × death_rate))`. Rejouer `test_plafond_toute_population` avec population=1 et déficit minuscule → doit FAILED (taux effectif = 1.0 > 0.10). Si le test reste PASSED, la garde n'est pas fonctionnelle.

**Résultat attendu :** PASS si le plancher est absent, le plafond tenu pour pop=1 à pop=9, la constante de récupération documentée, et la mémoire graduelle vérifiable.

---

## SC5 — Le compteur de transport mesure des kilogrammes arrivés

**Vérification :**

1. Rejouer :
   ```
   .venv/bin/python -m pytest sim/tests/test_kg_transportes_est_arrives.py -v
   ```
   Résultat attendu : `PASSED`.

2. L'Évaluateur vérifie que le test construit bien un monde avec **au moins 3 cellules et 2 arêtes actives** (le cas 2 cellules / 1 arête est trivial — un seul saut ne peut pas se compter deux fois).

**Reconstruction indépendante :**
L'Évaluateur monte le scénario chaîne 1–2–3 (seule cellule 1 a du stock) dans un script séparé, exécute 1 tick, compare `total_transported` (accumulateur interne) à la somme des variations positives de `food_stock_kg` des cellules pendant l'étape commerce. L'écart doit être ≤ 1e-9 kg.

**Résultat attendu :** PASS si l'écart est nul par construction. FAIL si `total_transported` est supérieur à la somme des deltas positifs (indice de double comptage résiduel).

---

## SC6 — Re-mesure complète du monde réel

**Vérification des quatre compteurs :**

Rejouer le script depuis la racine :
```
.venv/bin/python harness/queue/briefs/013-sim-tick-nourrit-une-fois/deliverables/measure_sc6_013.py
```

Les quatre valeurs produites doivent satisfaire simultanément :
- `cellules_affamees_monde_reel_re` > 0 (sur 596 cellules)
- `morts_cumules_monde_reel_re` > 0
- `kg_transportes_monde_reel_re` > 0
- `fraction_survie_monde_reel_re` > `SEUIL_SURVIE_POPULATION_FRACTION` (valeur dérivée de SC3)

**Reconstruction indépendante :**
L'Évaluateur écrit son propre script (sans reprendre le script livré) depuis `World.from_g3(rng_seed=42)` et `random.Random(42)`, 200 ticks, et calcule les quatre compteurs. Les valeurs doivent correspondre à celles du manifeste (à virgule flottante près).

**Vérification que les archives 012 sont intactes :**
```
git diff HEAD -- harness/queue/briefs/012-monde-vivant-commerce-inter-cellules/
```
Résultat attendu : aucun diff (aucune modification des archives du brief 012).

**Résultat attendu :** PASS si les quatre conditions tiennent sur les valeurs re-mesurées, et si le script est committé et rejoue depuis la racine. FAIL si une seule condition est fausse, ou si les valeurs du brief 012 ont été modifiées.

---

## SC7 — Tests du brief 012 adaptés ; suite complète verte

**Vérification des adaptations :**

1. Lire `deliverables/generator-log.md`, section « Adaptation des tests du brief 012 » : chaque test adapté ou retouché est nommé avec sa motivation. Vérifier que la liste n'est pas vide (le commentaire SC7c dans `test_causal_chain.py` doit au minimum apparaître).

2. Rejouer la suite complète :
   ```
   .venv/bin/python -m pytest sim/tests/ -v
   .venv/bin/python -m pytest harness/tests/ -q
   ```
   Aucun `FAILED`. Les `SKIP` sur Linux (tests Unity) sont acceptés.

**Vérification de la non-suppression silencieuse :**
```
git diff master -- sim/tests/
```
Si un fichier de test du brief 012 a disparu (0 lignes restantes ou fichier absent), vérifier que le journal mentionne ce fichier et justifie la suppression. Un `git diff` qui montre une suppression sans entrée dans le journal est un échec disqualifiant.

**Résultat attendu :** PASS si la suite est verte ET si chaque adaptation est motivée par écrit.

---

## SC8 — Registre de coût

**Vérification :**

1. `.venv/bin/python harness/backends/ledger.py report` → le brief `013-sim-tick-nourrit-une-fois` apparaît avec au moins `cursor=1`.

2. Lire la dernière ligne ajoutée à `harness/queue/cost-ledger.jsonl` : `"event": "generator-run"` (tiret, pas tiret bas), `"brief"` contient `013`, `"audit_id"` est `CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois`.

**Résultat attendu :** PASS si la ligne est présente avec les bons champs.

---

## Preuves rouges — vérification des deux paires

**Paire A — sabotage « ordre du tick inversé » :**
- `sim/tests/proof_red/run_ordre_tick_red.txt` : contient au moins un `FAILED` (sur `test_ecart_temoin_vs_receveuse`).
- `sim/tests/proof_red/run_ordre_tick_green.txt` : contient uniquement `PASSED`.
- L'Évaluateur monte sa propre contre-preuve (remettre `_apply_commerce` après `_apply_consumption` dans `tick()`) dans une copie hors dépôt et rejoint le test. Sa sortie doit contenir `FAILED`.
- Le gate `captures_differ_when_should` a vérifié que les deux fichiers sont différents.

**Paire B — sabotage « transferts appliqués en boucle, non atomiques » :**
- `sim/tests/proof_red/run_transport_atomique_red.txt` : contient au moins un `FAILED` (sur `test_invariance_ordre_aretes`).
- `sim/tests/proof_red/run_transport_atomique_green.txt` : contient uniquement `PASSED`.
- L'Évaluateur monte sa propre contre-preuve (retirer le snapshot, appliquer les transferts en place dans la boucle) dans une copie hors dépôt et rejoue le test. Sa sortie doit contenir `FAILED`.

---

## Gate mécanique

La commande suivante doit retourner `VERDICT: ACCEPT` avec tous les contrôles au vert avant que l'Évaluateur rédige son verdict de fond :

```
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/013-sim-tick-nourrit-une-fois
```

**Avertissement de lecture :** le gate juge la *forme* du lot, pas sa substance. Un lot peut obtenir `VERDICT: ACCEPT` du gate tout en recevant `FAIL` de l'Évaluateur de fond.

---

## Échecs disqualifiants

| Comportement | Raison |
|---|---|
| Réintroduire la double alimentation sous une autre forme (par exemple : décrémentation de `food_deficit_kg` dans une autre fonction appelée avant `_apply_consumption`) | Mode d'échec physique P0 — la nourriture nourrit encore deux fois ; SC1 non satisfaite |
| Commerce résiduel multi-sauts (cellule non adjacente à la source reçoit de la nourriture en un tick) | Principe 3 — rien ne se téléporte ; SC2 non satisfaite |
| Seuil `SEUIL_SURVIE_POPULATION_FRACTION` calibré à la main sur la valeur mesurée, ou marge `SURVIE_MARGE_DERIVEE` ajustée après avoir observé `fraction_survie_monde_reel_re` | Mode d'échec n°5 — compteur calibré sur sa propre mesure (hard-won rule 2) ; SC3 non satisfaite |
| Plancher de mortalité déguisé par l'arrondi : par exemple `deaths = max(0, round(population × death_rate))` qui donne `round(1 × 5e-12) = 0` pour pop=1 mais `round(1 × 0.001) = 0` systématiquement pour pop=1000 (test non couvert) | SC4 non satisfaite si le plafond n'est pas testé pour toutes les tailles de population |
| `kg_transportes_monde_reel_re` supérieur à la somme des deltas positifs de stock (double comptage résiduel) | SC5 non satisfaite |
| Retouche des constantes `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK`, `FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK`, `INITIAL_POPULATION_PER_KM2` etc. pour faire rentrer les compteurs re-mesurés dans les bornes | Masque l'effet des corrections SC1/SC2 sur les compteurs ; falsification des archives |
| Suppression ou modification des archives du brief 012 (`brief.md`, `eval-rubric.md`, `verdict.md`, `manifest.json`, preuves) | Archives intangibles — cf. § Non-Goals |
| Suppression d'un test `sim/tests/` du brief 012 sans motivation écrite dans `generator-log.md` | Mode d'échec n°7 — absence de traçabilité ; SC7 non satisfaite |
| Compteur SC6 mesuré sur un monde construit à la main ou depuis zéro cellule | Mode d'échec n°6 — échantillon vide passe en silence |
| Condensé SHA256 recopié en valeur hexadécimale dans un test ou un document | Hard-won rule 12 — piège pour tout brief ultérieur modifiant un paramètre |
| Modification de `brief.md`, `eval-rubric.md` ou `verdict.md` par le Générateur | Principe 7 — « celui qui produit ne prononce pas la recevabilité » |
