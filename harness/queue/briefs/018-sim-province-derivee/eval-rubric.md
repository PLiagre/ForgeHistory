# Eval Rubric — Brief 018 : la Province dérivée (agrégation de cellules)

**Authored**: 2026-08-14T05:52:00Z
**Author**: forge-planificateur

Ce document est rédigé par le Planificateur AVANT tout code.
L'Évaluateur l'applique sans le modifier.
Voir `docs/rules/harness-roles.md` et `docs/rules/simulation-principles.md`.

Note de transparence : le rôle signataire est le rôle natif du harnais
`forge-planificateur`. L'acteur réel est un sous-agent Cursor Cloud (modèle
Claude Opus 5), orchestré par un agent Cursor Cloud qui remplace le CTO Claude
(plafond de quota atteint). Aucun suffixe n'est ajouté à la signature : le
contrôle mécanique `verdict_is_not_self_authored` compare les acteurs de part
et d'autre d'un lot, et un couple suffixé serait refusé.

---

## Guide de lecture

Pour chaque condition de succès du brief :

- **Vérification** : commande rejouable depuis la racine du dépôt. Jamais
  l'alias nu — toujours `.venv/bin/python`.
- **Reconstruction indépendante** : l'Évaluateur re-dérive la valeur lui-même,
  sans reprendre aucun nombre du manifeste. Un compteur qu'on ne peut pas
  re-dériver n'est pas une mesure.
- **Contre-preuve disqualifiante** : sabotage monté par l'Évaluateur dans une
  copie de travail **hors du dépôt**. Si le test reste vert sous sabotage, la
  condition n'est pas satisfaite même si la suite est verte.
- **Résultat attendu** : ce que le Générateur doit avoir produit.

Vocabulaire employé ci-dessous, expliqué une fois :

- **agrégation dérivée** : ensemble de cellules recalculé à la demande à partir
  des positions et des centres administratifs ; ce n'est pas une donnée stockée.
- **appartenance** : la correspondance « telle cellule relève de tel centre ».
- **redessin** : déplacement d'un centre administratif.
- **fonction pure** : fonction qui ne modifie aucun objet reçu et n'écrit aucun
  fichier ; deux appels sur les mêmes entrées rendent le même résultat.

---

## SC1 — Couverture totale : chaque cellule chargée relève d'exactement une province

**Vérification :**

1. Rejouer le script de mesure de couverture :
   ```py
   .venv/bin/python harness/queue/briefs/018-sim-province-derivee/deliverables/measure_sc1_018.py
   ```
   La sortie doit nommer, avec leur dénominateur :
   `cellules_chargees_g3`, `centroides_lus`, `cellules_avec_province`,
   `cellules_sans_province`, `cellules_position_absente`, `provinces_non_vides`.

2. Vérifier que `cellules_chargees_g3` est **égal** à `cell_count` lu dans
   `pipeline/geo/artifacts/stats_g3.json` (jamais recopié en dur) :
   ```py
   .venv/bin/python -c "import json; print(json.load(open('pipeline/geo/artifacts/stats_g3.json'))['cell_count'])"
   ```

3. Vérifier que `centroides_lus` est égal à la longueur du tableau
   `coordinates` du fichier de centres administratifs :
   ```py
   .venv/bin/python -c "import json; d=json.load(open('pipeline/geo/legacy_game_data/province_coordinates.json')); print(len(d['coordinates']))"
   ```

4. Vérifier `cellules_avec_province == cellules_chargees_g3` et
   `cellules_sans_province == 0`. Ce zéro est une **mesure réelle** (règle
   durement acquise n° 8) : la sentinelle « non calculé » est `-1`, et elle ne
   doit apparaître pour aucun de ces compteurs.

5. Rejouer la suite de couverture :
   ```py
   .venv/bin/python -m pytest sim/tests/ -k province -v
   ```
   Résultat attendu : uniquement `PASSED`.

6. Vérifier que `provinces_non_vides` est présenté comme un **fait mesuré**
   avec `centroides_lus` pour dénominateur, et qu'aucun test n'exige un
   plancher égal au nombre de centres lus. Un test qui affirmerait
   « 50 provinces peuplées » est un plancher déguisé : condition non
   satisfaite.

**Reconstruction indépendante :**
L'Évaluateur écrit son propre script hors dépôt : il charge
`World.from_g3(rng_seed=42)`, lit `centroid.lat` / `centroid.lon` de chaque
cellule dans `pipeline/geo/artifacts/cells_g3.json`, lit les centres et le
paramètre `projection.mid_latitude` du fichier de centres, projette
(`x = lon × cos(mid_latitude)`, `y = −lat`), attribue chaque cellule au centre
le plus proche, et recompte les six compteurs. Les valeurs doivent coïncider
avec celles du manifeste.

**Contre-preuve disqualifiante :**
Dans une copie hors dépôt, retirer une entrée de positions (une cellule
chargée sans position connue). Le code doit **refuser explicitement** (exception
nommée, message citant la cellule) et non attribuer une province par défaut,
ni ignorer la cellule en silence. Une couverture obtenue en écartant les
cellules sans position n'est pas une couverture (règle durement acquise n° 10 :
l'absence de donnée ne s'invente pas).

Deuxième contre-preuve : un compteur mesuré sur un monde construit à la main,
ou sur un monde à zéro cellule, est disqualifiant (mode d'échec n° 6 —
l'échantillon vide qui passe en silence). L'échantillon est l'ensemble des
cellules chargées par `World.from_g3()`.

**Résultat attendu :** PASS si les six compteurs sont dérivés des fichiers,
si la couverture est totale, et si le nombre de provinces peuplées est rapporté
sans plancher imposé.

---

## SC2 — Aucune entité ne porte de province : la garde spatiale est exercée, pas affaiblie

**Vérification :**

1. Inspecter les champs de `Cell` :
   ```py
   .venv/bin/python -c "import dataclasses; from sim.model import Cell; print(sorted(f.name for f in dataclasses.fields(Cell)))"
   ```
   Aucun nom dont la forme normalisée (minuscules, tirets bas retirés) ne
   commence par `province`.

2. Vérifier que la garde lève bien une `TypeError` citant l'ADR-0003 sur une
   sous-classe fabriquée pour l'occasion :
   ```py
   .venv/bin/python -c "
   import dataclasses
   from sim.model import _NoBadSpatialField
   @dataclasses.dataclass
   class Sonde(_NoBadSpatialField):
       cell_id: int
       province_id: str
   try:
       Sonde(cell_id=1, province_id='X')
       print('AUCUNE ERREUR — garde inactive')
   except TypeError as exc:
       print('TypeError ADR-0003 :', 'ADR-0003' in str(exc))
   "
   ```
   Résultat attendu : `TypeError ADR-0003 : True`.

3. Vérifier que la vérification est **introspective** et non nominative : le
   test livré doit découvrir les dataclasses de `sim.model` par introspection
   (jamais une liste de noms écrite à la main), et couvrir aussi les types
   déclarés par le nouveau module d'agrégation. Un test qui ne nomme que
   `Cell` laisserait passer toute entité future (règle n° 2 : un contrôle
   dérive, il n'est jamais nommé d'après sa cible).

4. Rejouer :
   ```py
   .venv/bin/python -m pytest sim/tests/test_adr_compliance.py -v
   ```
   Résultat attendu : tous les tests existants toujours présents et `PASSED`.

5. Vérifier qu'aucun test existant n'a été **affaibli** :
   ```py
   .venv/bin/python -c "
   import subprocess
   d = subprocess.run(['git','diff','--','sim/tests/test_adr_compliance.py'],
                      capture_output=True, text=True).stdout
   print('lignes retirees :', [l for l in d.splitlines() if l.startswith('-') and not l.startswith('---')])
   "
   ```
   Aucune ligne retirée qui supprime un cas de test, élargit une liste blanche,
   ou change le préfixe interdit. Ajouter des cas est permis ; en retirer non.

6. Vérifier `champs_province_sur_entites == 0` avec son dénominateur (le nombre
   de champs inspectés), et `dataclasses_inspectees` > 0. Un dénominateur nul
   voudrait dire qu'aucun champ n'a été regardé — le contrôle n'aurait rien
   contrôlé.

**Reconstruction indépendante :**
L'Évaluateur écrit son propre balayage hors dépôt : pour chaque classe
dataclass de `sim.model` et du module d'agrégation, il liste les champs,
normalise les noms et compte ceux qui commencent par `province`. Le total doit
être zéro, et le nombre de champs inspectés doit correspondre au dénominateur
déclaré.

**Contre-preuve disqualifiante (paire rouge A) :**
Dans une copie hors dépôt, ajouter un champ `province_id: int = -1` sur `Cell`.
Rejouer les tests de conformité ADR : au moins un `FAILED` est obligatoire. Si
tout reste vert, la garde ne protège rien (règle n° 7 : la présence n'est pas
la fonction).

**Résultat attendu :** PASS si aucune entité ne porte de champ interdit, si la
vérification est introspective, et si aucun test de garde n'a été retiré ni
élargi.

---

## SC3 — Redessin : l'agrégat change, les cellules ne sont pas réécrites

**Vérification :**

1. Rejouer le script de mesure du redessin :
   ```py
   .venv/bin/python harness/queue/briefs/018-sim-province-derivee/deliverables/measure_sc3_018.py
   ```
   La sortie doit nommer : `redessin_change_agregat`,
   `cellules_changeant_de_province_apres_redessin`,
   `redessin_cellules_intactes`, `attributs_dynamiques_sur_cellules`,
   `fichier_centroides_inchange_apres_redessin`.

2. Rejouer le test de redessin :
   ```py
   .venv/bin/python -m pytest sim/tests/ -k redessin -v
   ```
   Résultat attendu : uniquement `PASSED`.

3. Vérifier dans le test que le déplacement du centre est fait **en mémoire**,
   sur une copie des enregistrements lus, et qu'aucune écriture n'a lieu dans
   `pipeline/geo/legacy_game_data/province_coordinates.json` ni dans
   `pipeline/geo/artifacts/`. Le compteur
   `fichier_centroides_inchange_apres_redessin` atteste la comparaison des
   octets du fichier avant et après.

4. Vérifier que le dépôt est propre côté géo après exécution :
   ```py
   .venv/bin/python -c "
   import subprocess
   print(subprocess.run(['git','status','--porcelain','--','pipeline/geo/'],
                        capture_output=True, text=True).stdout or 'aucune modification')
   "
   ```
   Résultat attendu : aucune modification.

**Reconstruction indépendante :**
L'Évaluateur monte le scénario lui-même, hors dépôt :

1. Charger `World.from_g3(rng_seed=42)`, calculer l'appartenance A.
2. Prendre l'empreinte de sérialisation des cellules avant redessin :
   `json.dumps(world.to_dict(), sort_keys=True)`, et un relevé de `vars(cell)`
   pour chaque cellule.
3. Déplacer **en mémoire** le centre de plus petit identifiant sur la position
   exacte d'une cellule qui relève actuellement d'un autre centre.
4. Recalculer l'appartenance B.
5. Vérifier : au moins une cellule change de province (B ≠ A) ; la
   sérialisation des cellules est **identique** à celle de l'étape 2 ; le
   relevé `vars(cell)` est identique champ par champ (aucun attribut
   dynamique apparu) ; le fichier de centres est inchangé sur le disque.

**Contre-preuve disqualifiante (paire rouge B) :**
Dans une copie hors dépôt, faire écrire l'appartenance par l'agrégation sur
chaque cellule, sous un nom que la garde de préfixe ne rattrape pas (par
exemple `zone_admin`), puis rejouer le test de redessin : il doit passer au
`FAILED`. Ce sabotage doit précisément échapper à la règle de préfixe, afin de
prouver que c'est bien le test de redessin qui garde la propriété, et non le
nom du champ (règle n° 6 : un contrôle trop grossier coûte aussi cher qu'un
contrôle laxiste).

Est également disqualifiant : un test de redessin qui ne compare que le nombre
de provinces peuplées, ou qui ne vérifie pas l'intégrité des cellules — la
condition porte sur les deux faits simultanément (l'agrégat bouge ET les
cellules ne bougent pas).

**Résultat attendu :** PASS si le redessin change l'appartenance d'au moins une
cellule sans qu'aucune cellule ne soit modifiée, et si rien n'est écrit sur
disque.

---

## SC4 — Fonction pure, déterminisme, départage nommé avant mesure

**Vérification :**

1. Vérifier la signature de la fonction d'agrégation : elle reçoit des
   positions de cellules, des centres, et le paramètre de projection lu du
   fichier. Elle ne reçoit ni n'accepte de dépendance implicite sur un état
   global mutable, et ne renvoie jamais un objet qui référence une `Cell`
   modifiable.

2. Vérifier l'absence d'écriture sur les objets reçus par lecture du module,
   puis rejouer :
   ```py
   .venv/bin/python -m pytest sim/tests/ -k "determinisme or departage or purete" -v
   ```
   Résultat attendu : uniquement `PASSED`.

3. Vérifier le déterminisme : dans un script hors dépôt, appeler deux fois la
   fonction d'agrégation sur les mêmes entrées, puis une troisième fois avec la
   liste des centres passée dans l'ordre inverse. Les trois appartenances
   doivent être identiques cellule par cellule
   (`determinisme_agregation_deux_passes == 1`, dénominateur = nombre de
   cellules comparées). Vérifier également que le compteur du manifeste est
   produit par un test livré, et non par une affirmation du journal.

4. Vérifier que la règle de départage des égalités de distance est **nommée et
   documentée dans `sim/SEEDING.md` avant toute citation de compteur mesuré**.
   La règle attendue par le brief est : plus petit identifiant de centre.

5. Vérifier le test d'égalité synthétique : deux centres exactement
   équidistants d'une cellule fabriquée ; la cellule doit relever du centre de
   plus petit identifiant, quel que soit l'ordre de parcours. Vérifier aussi
   que le test inverse l'ordre des centres en entrée et obtient le même
   résultat — sans quoi le départage n'est pas stable, il est accidentel.

6. Vérifier `egalites_de_distance_monde_reel` : ce compteur est un fait mesuré
   sur le monde réel et peut légitimement valoir zéro. Il ne doit pas être
   rapporté avec la sentinelle `-1` s'il a été calculé.

**Reconstruction indépendante :**
L'Évaluateur construit hors dépôt deux centres symétriques autour d'une
position de cellule fabriquée, appelle la fonction d'agrégation dans les deux
ordres possibles, et vérifie que le même centre gagne les deux fois.

**Contre-preuve disqualifiante :**
Remplacer, hors dépôt, le départage par « le dernier centre parcouru gagne ».
Le test d'égalité doit passer au `FAILED` dans au moins un des deux ordres. S'il
reste vert, le test ne mesure pas la stabilité du départage.

Est aussi disqualifiant : un littéral numérique de projection écrit dans une
fonction (par exemple la latitude moyenne recopiée à la main). Vérifier :
```py
.venv/bin/python -m pytest sim/tests/test_no_hardcoded.py -v
```
Résultat attendu : `PASSED`, avec `compteurs_en_dur_trouves = 0`.

**Résultat attendu :** PASS si l'agrégation est pure, déterministe, et si le
départage est nommé et documenté avant toute mesure.

---

## SC5 — Source déclarée comme proxy, jamais comme frontières historiques

**Vérification :**

1. Lire la section brief 018 de `sim/SEEDING.md`. Elle doit dire, en clair :
   - que les centres administratifs proviennent de
     `pipeline/geo/legacy_game_data/province_coordinates.json`, données
     **héritées du jeu** ;
   - qu'il ne s'agit **pas** de frontières historiques de 1400 et que rien ici
     ne prétend au statut de source savante ;
   - la projection employée et le paramètre lu du fichier ;
   - la règle de départage des égalités ;
   - la politique de refus : une cellule sans position connue provoque une
     erreur explicite, jamais une attribution par défaut.

2. Vérifier l'**ordre d'écriture** : cette documentation précède, dans le
   fichier, toute citation de compteur mesuré du présent brief. Une
   justification écrite après la mesure est une calibration déguisée.

3. Lire `sim/README.md` : la mise à jour doit être **descriptive** (quels
   modules existent, quelles données ils lisent) et ne doit pas contenir
   d'instruction pour un agent — le brief est la seule instruction.

4. Vérifier :
   ```py
   .venv/bin/python -m pytest harness/tests/test_single_source_of_instruction.py -v
   ```
   Résultat attendu : `PASSED`.

**Reconstruction indépendante :**
L'Évaluateur relit le fichier de centres et confirme que le paramètre de
projection documenté est bien celui qu'il contient, et que la documentation ne
prête aux données aucune propriété que le fichier ne porte pas.

**Contre-preuve disqualifiante :**
Une phrase de `sim/SEEDING.md` ou de `sim/README.md` présentant les 50 centres
comme des frontières historiques, une reconstitution savante, ou une source
d'époque. Également disqualifiant : une documentation qui omet la règle de
départage tout en la mettant en œuvre dans le code.

**Résultat attendu :** PASS si l'origine des données est déclarée comme proxy
hérité, avant mesure, et si la documentation reste descriptive.

---

## SC6 — Preuves rouges : deux paires, sabotage hors dépôt

**Paire A — garde spatiale :**
- `sim/tests/proof_red/run_garde_province_red.txt` : contient au moins un
  `FAILED` (champ `province_id` ajouté sur `Cell` dans une copie hors dépôt).
- `sim/tests/proof_red/run_garde_province_green.txt` : uniquement `PASSED`,
  même test sur le code correct.
- L'Évaluateur monte son propre sabotage et confirme le `FAILED`.

**Paire B — redessin sans réécriture :**
- `sim/tests/proof_red/run_redessin_red.txt` : contient au moins un `FAILED`
  (l'appartenance est estampillée sur chaque cellule, sous un nom que la garde
  de préfixe ne rattrape pas).
- `sim/tests/proof_red/run_redessin_green.txt` : uniquement `PASSED`.
- L'Évaluateur monte son propre sabotage et confirme le `FAILED`.

**Vérifications communes :**

1. Les quatre fichiers sont en `.txt` (jamais `.log` : `.gitignore` exclut
   `*.log`, une preuve laissée là ne serait pas re-vérifiable depuis un clone).
2. Les deux paires sont déclarées dans `deliverables/manifest.json` avec le
   champ `must_differ_from`, en chemins relatifs au dossier du brief.
3. Le contrôle mécanique `captures_differ_when_should` doit être au vert : il
   compare les deux fichiers de chaque paire. Une paire non déclarée n'est pas
   vérifiée par la porte — l'absence de déclaration est en soi un défaut.
4. Aucune valeur hexadécimale de condensé SHA256 n'est recopiée dans les
   preuves, les tests ou la documentation (règle n° 12 : une empreinte est
   citée par son nom, jamais par sa valeur).

**Résultat attendu :** PASS si les deux paires existent, diffèrent, sont
déclarées, et si l'Évaluateur reproduit lui-même les deux rouges.

---

## SC7 — Script de mesure, manifeste, suite verte

**Vérification :**

1. Les deux scripts de mesure sont committés et rejouables depuis la racine :
   ```py
   .venv/bin/python harness/queue/briefs/018-sim-province-derivee/deliverables/measure_sc1_018.py
   .venv/bin/python harness/queue/briefs/018-sim-province-derivee/deliverables/measure_sc3_018.py
   ```
   Chaque compteur imprimé porte son dénominateur. Un compteur sans
   dénominateur est irrecevable.

2. Chaque compteur du manifeste porte un `sample_size` réel, non nul et
   différent de la sentinelle. Contrôle mécanique correspondant :
   `no_empty_sample_pass`.

3. La suite complète est verte :
   ```py
   .venv/bin/python -m pytest sim/tests/ -v
   .venv/bin/python -m pytest harness/tests/ -q
   ```
   Aucun `FAILED`. Les `SKIP` propres à Linux (tests Unity) sont acceptés.

4. Aucune archive n'est retouchée :
   ```py
   .venv/bin/python -c "
   import subprocess
   dirs = ['harness/queue/briefs/011-sim-monde-vivant-amorcage/',
           'harness/queue/briefs/012-monde-vivant-commerce-inter-cellules/',
           'harness/queue/briefs/013-sim-tick-nourrit-une-fois/',
           'harness/queue/briefs/014-pipeline-contre-audit-porte/',
           'harness/queue/briefs/015-pr69-seuil-survie-ignore-mortalite/',
           'harness/queue/briefs/016-pr69-seuil-survie-non-borne/',
           'harness/queue/briefs/017-sim-seuil-survie-honnete/']
   out = subprocess.run(['git','status','--porcelain','--']+dirs,
                        capture_output=True, text=True).stdout
   print(out or 'archives 011-017 intactes')
   "
   ```
   Résultat attendu : archives intactes.

5. Vérifier que `sim/engine.py` n'a pas été modifié (le pas de temps ne
   consomme pas l'agrégation dans ce lot) :
   ```py
   .venv/bin/python -c "
   import subprocess
   print(subprocess.run(['git','status','--porcelain','--','sim/engine.py'],
                        capture_output=True, text=True).stdout or 'engine.py inchange')
   "
   ```

6. Vérifier que le Générateur n'a **ni committé, ni poussé, ni créé de
   branche** : la branche courante est celle fournie par l'orchestrateur, et
   l'historique ne contient aucun commit signé du Générateur.

**Résultat attendu :** PASS si les scripts rejouent, si tous les compteurs sont
échantillonnés, si la suite est verte et si le périmètre a été respecté.

---

## SC8 — Registre de coût

**Vérification :**

1. ```py
   .venv/bin/python harness/backends/ledger.py report
   ```
   Le brief `018-sim-province-derivee` apparaît avec au moins `cursor=1`.

2. Lire la dernière ligne ajoutée à `harness/queue/cost-ledger.jsonl` :
   `"event": "generator-run"`, `"backend": "cursor"`, et un chemin de brief
   contenant `018`. L'absence d'`audit_id` est normale : ce brief naît de la
   feuille de route, pas d'un audit converti.

**Résultat attendu :** PASS si la ligne est présente avec ces champs.

---

## Porte mécanique

```py
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/018-sim-province-derivee
```

Doit répondre `VERDICT: ACCEPT`, tous contrôles applicables au vert, avant que
l'Évaluateur ne rédige son verdict de fond.

**Avertissement :** la porte juge la forme du lot, pas sa substance. Un lot peut
obtenir `ACCEPT` de la porte et `FAIL` de l'Évaluateur.

---

## Échecs disqualifiants

| Comportement | Raison |
|---|---|
| Un champ dont le nom normalisé commence par `province` apparaît sur `Cell` ou sur une autre entité de `sim.model` | ADR-0003 violé : la seconde copie inscriptible du « où » est réintroduite |
| L'appartenance est stockée sur les cellules « pour la performance » ou « en cache invalidé » | Même défaut sous un autre nom — c'est exactement le cas tracé par l'ADR-0003 |
| `_NoBadSpatialField` ou `test_adr_compliance.py` affaibli (cas retiré, liste blanche élargie, préfixe changé) | Une garde affaiblie après coup ne protège plus rien (règle n° 5) |
| Le test de conformité ne nomme que `Cell` au lieu de découvrir les entités par introspection | Règle n° 2 : un contrôle dérive, il n'est jamais nommé d'après sa cible ; toute entité future passerait |
| Le redessin ne change aucune appartenance | La province n'est pas dérivée : elle est figée |
| Une cellule est modifiée par le redessin (sérialisation différente, ou attribut dynamique apparu) | La province est réécrite sur les habitants : chaîne causale du brief non respectée |
| L'agrégation écrit dans `pipeline/geo/` ou dans le fichier de centres | Le lot est en lecture seule sur les données géographiques |
| Un plancher de provinces peuplées est imposé (par exemple « au moins 50 ») | Compteur en dur (mode d'échec n° 5) : le nombre de provinces peuplées est un fait mesuré |
| Un compteur de couverture mesuré sur un monde à la main ou à zéro cellule | Mode d'échec n° 6 : l'échantillon vide passe en silence |
| Une cellule sans position reçoit une province par défaut ou est écartée en silence | Règle n° 10 : l'absence de donnée s'invente par défaut si le code ne refuse pas |
| `cellules_sans_province` rapporté à `-1` alors qu'il a été calculé | Règle n° 8 : zéro est ici une mesure réelle, la sentinelle dit autre chose |
| Départage des égalités non documenté, ou dépendant de l'ordre de parcours | Le déterminisme n'est pas prouvé, il est espéré |
| Latitude moyenne (ou tout autre littéral numérique) écrite dans un corps de fonction | `test_no_hardcoded.py` : un compteur ou un paramètre dérive, ou il n'existe pas |
| Les 50 centres présentés comme frontières historiques de 1400 ou source savante | Règle n° 10 : le proxy doit être déclaré comme tel |
| Documentation de la règle de départage ou de la projection écrite après la mesure | Calibration après mesure |
| Une paire rouge manquante, identique à son vert, ou non déclarée par `must_differ_from` | Règle n° 4 : un contrôle qui ne peut pas rougir ne prouve rien |
| Sabotage de la paire B choisi de sorte que la garde de préfixe le rattrape | Le rouge prouverait la garde de nom, pas le test de redessin (règle n° 6) |
| `sim/engine.py` modifié, ou une consommation de l'agrégation par le pas de temps | Hors périmètre du lot |
| Condensé SHA256 recopié en valeur hexadécimale | Règle n° 12 : piège pour tout brief ultérieur |
| L'alias nu du langage employé dans une commande au lieu de `.venv/bin/python` | Règle n° 1 |
| Commit, poussée ou création de branche par le Générateur | Interdiction explicite du brief ; l'orchestrateur seul dépose |
| Archives des briefs 011 à 017 modifiées | Archives intangibles |
