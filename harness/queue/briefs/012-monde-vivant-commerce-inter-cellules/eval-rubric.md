# Eval Rubric — Brief 012 : Le monde vivant vit — base de temps, équilibre alimentaire mesuré et commerce inter-cellules

**Authored**: 2026-08-13T06:27:00Z
**Author**: forge-planificateur

Ne pas modifier après avoir vu les livrables du Générateur.
Le Planificateur écrit la rubrique avant tout code ; l'Évaluateur l'applique sans la réécrire.

---

## Guide de lecture

Chaque ligne correspond à une condition de succès du `brief.md`. Pour chaque condition :
- **Vérification** : commande à rejouer ou contre-preuve à monter.
- **Reconstruction indépendante** : l'Évaluateur re-dérive les compteurs lui-même, sans reprendre les valeurs du manifeste.
- **Résultat attendu** : ce que le Générateur doit avoir produit.

Vocabulaire : « copie hors dépôt » = répertoire temporaire sans lien git, créé par l'Évaluateur, sans toucher aucun fichier du dépôt.

---

## SC1 — Base de temps unique, constantes alignées, noms corrigés

**Vérification :**

1. `.venv/bin/python -c "from sim.constants import TICK_DURATION_DAYS; assert TICK_DURATION_DAYS > 0; print(TICK_DURATION_DAYS)"` depuis la racine → valeur strictement positive affichée sans erreur.
2. Recherche de l'ancien nom `rg INITIAL_FOOD_DAYS sim/` → aucune occurrence (ou l'occurrence est dans un commentaire explicatif de renommage).
3. Recherche de la variable `daily_need` : `rg daily_need sim/` → aucune occurrence.
4. Lecture de `sim/SEEDING.md` : chaque constante temporelle (production, consommation, stock initial) cite `TICK_DURATION_DAYS` dans sa dérivation. La justification est un proxy paramétrique déclaré, pas une donnée historique inventée.

**Reconstruction indépendante :** L'Évaluateur lit `sim/constants.py` et vérifie manuellement que la dérivation `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK` et `FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK` sont cohérentes avec `TICK_DURATION_DAYS`.

**Résultat attendu :** PASS si la constante existe, est positive, et que les deux dérivations sont documentées. FAIL si `TICK_DURATION_DAYS` est absente ou si `INITIAL_FOOD_DAYS` subsiste sans explication de renommage.

---

## SC2 — La production varie réellement par tick (rng consommé)

**Vérification :**

1. **État du rng** — rejouer le script suivant depuis la racine :
   ```
   # script à lancer avec .venv/bin/python -c "..." ou dans un fichier .py
   import random
   from sim.world import World
   from sim import engine
   w = World.from_g3(rng_seed=42)
   rng = random.Random(42)
   state_before = rng.getstate()
   for _ in range(10):
       engine.tick(w, rng)
   state_after = rng.getstate()
   print("rng_change:", state_before != state_after)
   ```
   Résultat attendu : `rng_change: True`. Si `False`, SC2 échoue.

2. **Déterminisme à graine fixe** — deux runs identiques (graine world=42, rng=42) sur N=200 ticks donnent des condensés SHA256 égaux. L'Évaluateur calcule les condensés lui-même et les compare par nom de variable ; si une valeur hexadécimale est recopiée en dur dans un test ou dans le journal, c'est une violation de la hard-won rule 12 (voir § Échecs disqualifiants).

3. **Sensibilité à la graine rng** — run A (graine rng=42) vs run B (graine rng=999), même graine world=42, N=200 ticks → condensés différents. L'Évaluateur vérifie que cet écart survient déjà après 1 tick (la variabilité n'est pas uniquement portée par l'amorçage).

**Reconstruction indépendante :** L'Évaluateur exécute les trois scripts lui-même, hors des tests livrés, et compare à ce que rapporte le manifeste.

**Résultat attendu :** PASS si les trois propriétés tiennent. FAIL si le rng n'est pas consommé (état inchangé) ou si les condensés sont identiques pour des graines rng différentes.

---

## SC3 — Le déficit alimentaire est un état persisté

**Vérification :**

1. Lire `sim/model.py` : le champ `food_deficit_kg: float` est déclaré sur `Cell` avec la sentinelle `-1.0` par défaut.
2. Lire `sim/engine.py` (ou le module du maillon consommation) : la ligne `remaining if remaining >= 0.0 else 0.0` (ou équivalent écrasant le déficit) n'apparaît plus. À la place, le manque est ajouté à `food_deficit_kg`.
3. Lire la fonction de mortalité : elle lit `food_deficit_kg` et calcule des décès proportionnels à son ampleur. Un simple `if hunger_ticks >= THRESHOLD` seul est insuffisant (voir § Échecs disqualifiants).
4. **Contre-preuve (copie hors dépôt)** : construire une cellule avec `area_km2=10.0`, `population=1000`, `food_stock_kg=100.0` (consommation >> stock), appeler `tick()` une fois, vérifier `food_deficit_kg > 0`.

**Reconstruction indépendante :** L'Évaluateur construit l'état à la main et rejoue le tick unitaire lui-même.

**Résultat attendu :** PASS si `food_deficit_kg` est déclaré, écrit par la consommation, et lu par la mortalité. FAIL si le déficit est écrasé à 0 sans être enregistré.

---

## SC4 — Commerce inter-cellules physique (conservation de la masse)

**Vérification :**

1. `rg -n "adjacency" sim/engine.py sim/*.py` (ou le module de commerce s'il est séparé) → au moins une lecture de `world.adjacency` dans le code du moteur (hors chargement dans `world.py`).
2. Rejouer le test de conservation de masse localement :
   ```
   .venv/bin/python -m pytest sim/tests/test_commerce.py::test_conservation_masse -v
   ```
   Résultat attendu : PASSED.
3. **Contre-preuve transport-conservatif (copie hors dépôt)** : modifier le maillon commerce pour qu'il ajoute 1 kg au destinataire sans en soustraire à la source, puis rejouer le test → doit afficher FAILED. La sortie doit être identique octet pour octet à `sim/tests/proof_red/run_transport_red.txt` (à la ligne du répertoire de travail près).
4. Lire `sim/SEEDING.md` : `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK` est documentée avec sa justification paramétrique.

**Reconstruction indépendante :** L'Évaluateur monte sa propre contre-preuve de non-conservation dans une copie hors dépôt et compare sa sortie au fichier rouge livré.

**Résultat attendu :** PASS si `world.adjacency` est lue par le moteur, la conservation est vérifiable, et la capacité de transport est documentée. FAIL si `rg adjacency sim/engine.py sim/*.py` ne retourne aucune lecture dans le moteur.

---

## SC5 — Le monde vit, mesuré sur les 596 cellules réelles

**Vérification :**

Rejouer le script de mesure complet depuis la racine (adapter le script § 5.3 de l'audit source) :

```
# script à lancer avec .venv/bin/python mesure_sc5.py depuis la racine
import random
from sim.world import World
from sim import engine

w = World.from_g3(rng_seed=42)
rng = random.Random(42)
N = 200
pop0 = sum(c.population for c in w.cells.values())
kg_transport = 0.0  # le maillon commerce doit accumuler ce total

affames_ever = set()
for t in range(N):
    engine.tick(w, rng)
    for c in w.cells.values():
        if c.hunger_ticks > 0:
            affames_ever.add(c.cell_id)

pop1 = sum(c.population for c in w.cells.values())
print("cellules_affamees_monde_reel:", len(affames_ever))
print("morts_cumules_monde_reel    :", pop0 - pop1)
print("population_finale_fraction  :", pop1 / pop0)
# kg_transportes_monde_reel : à extraire du maillon commerce selon l'implémentation
```

L'Évaluateur adapte ce script pour comptabiliser les kg transportés selon l'implémentation livrée.

**Quatre conditions à vérifier simultanément :**
- `cellules_affamees_monde_reel` > 0
- `morts_cumules_monde_reel` > 0
- `kg_transportes_monde_reel` > 0
- `population_finale_fraction` > `SEUIL_SURVIE_POPULATION_FRACTION` déclaré dans `sim/SEEDING.md`

**Reconstruction indépendante :** L'Évaluateur reconstruit les quatre compteurs lui-même depuis `World.from_g3()`, sans reprendre les valeurs du manifeste.

**Vérification du cas inatteignable :** `rg "area_km2 = 0" sim/tests/` → si une occurrence existe, le test doit être annoté comme structurellement hors données G3, et non utilisé comme preuve de SC5.

**Résultat attendu :** PASS si les quatre conditions sont vraies simultanément. FAIL si l'une quelconque est fausse (notamment : 0 cellule affamée, 0 mort, 0 kg transporté).

---

## SC6 — `sim/tests/` tourne en intégration continue

**Vérification :**

1. Lire `.github/workflows/harness-ci.yml` : un job collecte `sim/tests/` (ou le chemin correspondant).
2. Rejouer localement la commande CI exacte spécifiée dans le workflow :
   ```
   .venv/bin/python -m pytest sim/tests/ -v
   ```
   (ou la commande exacte du workflow) → code de sortie nul, aucun FAILED.
3. `.venv/bin/python -m pytest sim/tests/ --collect-only -q` → `ci_sim_tests_collectes` > 0.

**Résultat attendu :** PASS si le workflow est modifié et que la commande CI passe localement. FAIL si `sim/tests/` n'apparaît nulle part dans le workflow.

---

## SC7 — Réserves R1-R4 fermées, couverture d'écriture étendue

**R1 — Commande de `lignes_differentes_preuve_rouge_iter1` corrigée :**
Rejouer la commande déclarée dans l'entrée du manifeste 011 → résultat doit être 70. Vérifier que la commande utilise `git show <hash>:...` ou que l'entrée a été retirée après vérification qu'aucun document sous `harness/queue/briefs/011-*/` ne cite 70 en référence à ce compteur.

**R2 — Découverte par introspection :**
**Contre-preuve (copie hors dépôt)** : ajouter à `sim/model.py` une deuxième dataclass portant au moins un champ sans aucun site d'écriture ni de lecture (exemple : `@dataclass class GhostEntity(_NoBadSpatialField): phantom_field: float = -1.0`), puis rejouer `test_write_coverage.py` → FAILED attendu, avec un message nommant la dataclass et le champ fautifs. Si le test reste PASSED, R2 n'est pas fermée. Une dataclass sans aucun champ n'est pas une contre-preuve valide : elle n'a rien à couvrir et un PASS serait légitime.

**R3 — Vérification du type de l'objet écrit :**
**Contre-preuve (copie hors dépôt)** : ajouter dans `sim/engine.py` une ligne `autre_objet.food_stock_kg = 0` où `autre_objet` est une instance d'un type différent de `Cell`, puis rejouer `test_write_coverage.py` → le test ne doit pas compter cette ligne comme site d'écriture valide pour `Cell.food_stock_kg`.

**R4 — Consolidation (optionnel) :**
Vérifier si `run_correct.txt` et `run_phantom_green.txt` ont le même contenu : `diff sim/tests/proof_red/run_correct.txt sim/tests/proof_red/run_phantom_green.txt`. S'ils sont identiques et non consolidés, noter comme non bloquant.

**Extension de la couverture :**
- `food_deficit_kg` apparaît dans les champs découverts par introspection et est couvert (écrivain + lecteur identifiés).
- L'assertion sur `World.adjacency` est présente dans le test. **Contre-preuve** : commenter le maillon commerce dans une copie hors dépôt → `test_write_coverage.py` doit afficher FAILED pour `World.adjacency`.

**Résultat attendu :** PASS si R1 et R2 et R3 sont fermées ET si les nouveaux champs et `World.adjacency` sont couverts. R4 est non bloquant.

---

## SC8 — Registre de coût

**Vérification :**

1. `.venv/bin/python harness/backends/ledger.py report` → le brief `012-monde-vivant-commerce-inter-cellules` apparaît avec au moins `cursor=1`.
2. Lire la dernière ligne de `harness/queue/cost-ledger.jsonl` et vérifier : `"event": "generator-run"` (avec tiret, pas tiret bas), `"brief"` contient `012`, `"audit_id"` est `CURSOR-3b47ffe-pr57-monde-sans-faim`.

**Résultat attendu :** PASS si la ligne est présente avec les bons champs. FAIL si l'événement utilise un tiret bas (`generator_run`) ou si le brief ou l'audit_id est incorrect.

---

## Preuves rouges — vérification des deux paires

**Paire transport-conservatif :**
- `sim/tests/proof_red/run_transport_red.txt` : contient au moins un `FAILED` (sur le test de conservation de masse).
- `sim/tests/proof_red/run_transport_green.txt` : contient uniquement `PASSED`.
- L'Évaluateur monte sa propre contre-preuve et compare sa sortie au fichier rouge livré (identique octet pour octet à la ligne du répertoire de travail près).
- Le gate `captures_differ_when_should` a vérifié que les deux fichiers sont différents.

**Paire couverture étendue :**
- `sim/tests/proof_red/run_coverage_ext_red.txt` : contient au moins un `FAILED` (sur le test de couverture avec champ fantôme).
- `sim/tests/proof_red/run_coverage_ext_green.txt` : contient uniquement `PASSED`.
- L'Évaluateur monte sa propre contre-preuve (champ fantôme sur une dataclass) et compare.

---

## Gate mécanique

La commande suivante doit retourner `VERDICT: ACCEPT` avec les 10 contrôles au vert avant que l'Évaluateur rédige son verdict de fond :

```
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/012-monde-vivant-commerce-inter-cellules
```

**Avertissement de lecture :** le gate juge la *forme* du lot, pas sa substance. Un lot peut obtenir `VERDICT: ACCEPT` du gate tout en recevant `FAIL` de l'Évaluateur de fond (par exemple si SC5 n'est pas satisfaite).

---

## Échecs disqualifiants

Les comportements suivants constituent un échec disqualifiant, indépendamment de tout autre résultat :

| Comportement | Raison |
|---|---|
| Retirer un champ existant de `Cell` pour faire passer le test de couverture | Mode d'échec n°2 en sens inverse ; falsification de la couverture (voir brief 011, verdict itération 1 § « Ce qu'il ne faut surtout pas faire ») |
| `test_tick_determinisme` passe alors que `rng.getstate()` est inchangé après un tick | Le test ne peut pas échouer → preuve structurellement vide (hard-won rule 4) |
| `cellules_affamees_monde_reel = 0` sur les 596 cellules après 200 ticks | La chaîne causale ne se déclenche pas sur le monde réel ; SC5 non satisfaite |
| `kg_transportes_monde_reel = 0` après 200 ticks | `World.adjacency` est toujours non lue en pratique ; SC4 non satisfaite |
| La somme des stocks change dans l'étape de commerce (non-conservation de la masse) | Violation du principe 3 (rien ne se téléporte) ; SC4 non satisfaite |
| Condensé SHA256 recopié en valeur hexadécimale dans un test ou un document | Hard-won rule 12 ; piège pour tout brief ultérieur qui modifie un paramètre |
| Compteur `cellules_affamees_monde_reel` mesuré sur un monde construit à la main ou sur zéro cellule | Mode d'échec n°6 (référence auto-nommée, échantillon vide) |
| Modification de `brief.md`, `eval-rubric.md` ou `verdict.md` par le Générateur | Violation du principe 7 : « celui qui produit ne prononce pas la recevabilité » |
