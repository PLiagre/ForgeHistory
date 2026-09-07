# sim/

Moteur de simulation ForgeHistory — **le produit vivant**. Il tourne
seul, sans moteur de rendu :

```
py -m sim
py -m sim --ticks 0 --json
py -m sim --ticks 0 --seed 0 --snapshot-json /tmp/world.json
```

`--ticks` doit être ≥ 0. Un négatif refuse (code 2), message sur stderr, stdout
vide. `py -m sim --ticks 0 --json` amorce sans avancer.

`--snapshot-json` écrit une photographie cellulaire déterministe (schéma
`SNAPSHOT_SCHEMA_VERSION`) : géométrie, état simulé, province dérivée,
climat. Ce n'est pas une seconde simulation. Le snapshot déclare lui-même,
couche par couche, ce que le moteur consomme et ce qu'il ne consomme pas.

Le nom du schéma n'est pas recopié ici : il est dans `sim/constants.py`.
Un document qui porte une version morte piège le lot suivant (règle 12).

La vision du moteur est dans [`VISION.md`](../VISION.md), les règles dans
[`AGENTS.md`](../AGENTS.md), et le fonctionnement du monde — formules,
constantes, limites — dans [`MODELE.md`](MODELE.md).

---

## Modules

| Fichier | Rôle |
|---|---|
| `sim/__init__.py` | Paquet Python, expose `__version__` |
| `sim/constants.py` | Constantes paramétriques nommées (voir `sim/MODELE.md`) |
| `sim/model.py` | Dataclass `Cell` — entité géographique de base |
| `sim/world.py` | `World` — chargement depuis la carte figée. Porte aussi `stocks_mer`, le bassin maritime, **hors** de `to_dict()` |
| `sim/engine.py` | `tick(world, rng, numero_tick)` — extraction, production, commerce (terre + mer), consommation, faim, mortalité, natalité, migration |
| `sim/aggregation.py` | Vues dérivées : province (`Regroupement`) et bourg (`RepartitionBourg`). Ne modifie rien, n'écrit rien. Le tick ne les consulte pas |
| `sim/__main__.py` | `py -m sim` — lance le monde |
| `sim/snapshot_export.py` | Photographie cellulaire déterministe (`--snapshot-json`). Recalcule la province ; **ne porte ni le bassin ni le bourg** |
| `sim/MODELE.md` | Comment le monde fonctionne : formules, constantes, limites |

---

## Source des données d'entrée

Les artefacts G3 sont générés par le pipeline géographique :

- `data/world-1400.json` — la carte figée : cellules (cell_id, area_km2, centroid,
  geometry, relief, climat, gisements) et adjacence, dans un seul fichier

`sim/aggregation.py` lit deux sources supplémentaires, toujours en lecture
seule :

- `data/world-1400.json` — la position géographique de chaque
  cellule (`centroid.lat`, `centroid.lon`, repère WGS84) ;
- `data/province-centres-1400.json` — les centres
  administratifs hérités du jeu (tableau `coordinates` : `id`, `name`, `lon`,
  `lat`) et le paramètre de projection `projection.mid_latitude`.

Ces centres sont un proxy hérité, pas des frontières historiques : leur
provenance et les limites de ce qu'ils prouvent sont décrites dans
`sim/MODELE.md`, section « La province dérivée et ses centres ».

Le nombre exact de cellules et d'arêtes n'est recopié nulle part : il se lit
dans `data/world-1400.json` et se dérive du chargement. La commande qui le
donne est `py -m sim --ticks 0 --json`.

Ces fichiers sont suivis par git et lisibles depuis un clone frais.

---

## Lancer les tests

```
py -m pytest sim/tests/ -v
```

Depuis la racine du dépôt. La suite attend tous les tests PASSED,
exit code 0. Les fichiers `proof_red/*.txt` ne font pas partie de la
suite de tests (artefacts de preuve, non collectés par pytest).

---

## Règles architecturales importantes

- **Une seule clé spatiale** : `cell_id`. Province et bourg sont des
  agrégations dérivées — jamais un champ stocké. `sim/aggregation.py` met
  cette règle en œuvre, hors de `sim.model`. Consulter le bourg :
  `from sim.aggregation import bourg_depuis_monde`.
- **Commerce physique** : arêtes terrestres bornées par la longueur de
  frontière ; arêtes maritimes vers un bassin commun (`World.stocks_mer`),
  bornées par la façade. Un kilogramme embarqué au tick `t` ne débarque
  qu'à `t+1`. La mer ne porte que les marchandises consommées. Conservation
  stricte de la masse, bassin compris.
- **Division du travail** : une part des habitants d'une cellule à gisement
  cesse de cultiver pour extraire (`part_miniere_de`). Ce qu'ils sortent
  reste dans la cellule : rien ne consomme le minerai.
- **Population agrégée** : pas encore de familles ou de personnes individuelles.
- **stdlib uniquement** : le moteur n'a aucune dépendance tierce (pytest est
  réservé aux tests).

## Pièges

- `python` nu est interdit (règle 1). Sur Linux : `python3` ; sur Windows : `py`.
- `tick(world, rng)` **sans** `numero_tick` ne joue pas le jour du calendrier :
  il moyenne la saison sur l'année. C'est le piège du deuxième régime, décrit
  dans `sim/MODELE.md`. `py -m sim` passe le numéro. `python3 -m visualisateur
  --ticks N` ne le passe pas.
- Le snapshot n'est pas le monde : pas de `stocks_mer`, pas de bourg. Une
  photographie qui « n'a pas de mer » n'est pas un monde sans mer.
