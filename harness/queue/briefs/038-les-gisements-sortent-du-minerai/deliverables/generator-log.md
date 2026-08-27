# Journal du lot 038 — Les gisements sortent du minerai

SHA de base : `782ae0b87d0282e25d6ea6acd19226d41aaedb53`

## Rouge prouvé (SC1)

Sur le SHA de base, après un tick, aucune cellule ne portait de marchandise
minière dans son panier :

```bash
.venv/bin/python -c "from sim.world import World; from sim.engine import tick; import random; from sim.constants import MARCHANDISE_NOURRITURE; w=World.charger(0); tick(w, random.Random(0), 0); print(sum(1 for c in w.cells.values() if any(k!=MARCHANDISE_NOURRITURE for k in c.stocks)))"
```

Résultat : `0` (25 cellules porteuses de gisement dans la carte, 0 extractrice).

## Fichiers modifiés

- `sim/constants.py` — constantes et tables d'extraction (motif 033).
- `sim/engine.py` — maillon `_apply_extraction` en tête de chaîne, sur tous les
  chemins de tick qui portent une carte (y compris sans `numero_tick`).
- `sim/tests/test_monde.py` — ajouts seuls : SC1 à SC6 du brief.

## Commandes jouées

| commande | résultat |
|---|---|
| `pytest sim/tests/ -q` | 86 passed |
| contrôles write_coverage + no_hardcoded | 4 passed |
| `sim --ticks 365 --seed 0 --json` (×2) | sorties identiques |
| sonde `build_snapshot_document` | `gisements.utilisee_par_le_moteur == true` |
| `measure_038.py` | compteurs verts |

## Compteurs clés

- `cellules_extractrices_apres_un_tick` = 25 = `cellules_avec_gisement_carte`
- `ressources_distinctes_extraites` = 10 = ressources de la carte
- `extraction_population_nulle` = 0.0 (mesure réelle)
- `cellules_dont_la_nourriture_a_change` = 0
- `noms_de_constantes_extraction_dans_engine` = 0

## Limites

- Pas de transport, consommation ni épuisement du minerai (hors périmètre).
- `sim/MODELE.md` non mis à jour (dette architecte).
