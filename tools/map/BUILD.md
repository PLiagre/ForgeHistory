# Refaire la carte

Le jeu lit `data/world-1400.json`. Ce fichier est produit ici, et versionné.

```bash
python tools/map/build_world.py              # (re)fabrique la carte
python tools/map/build_world.py --verifier   # la carte versionnée est-elle à jour ?
```

`--verifier` est joué par `sim/tests/test_monde.py` : une carte versionnée
qui ne correspond plus à ce que produit l'outil fait rougir les tests. C'est
la garde contre le risque nommé par ADR-0018 — une carte figée qui devient
périmée sans que personne ne le voie.

## Ce que la carte contient

| champ | provenance | niveau de fidélité |
|---|---|---|
| `cell_id`, `area_km2`, `centroid`, `geometry` | étape G3 (littoral 1400) | 1 — juste dans les grandes lignes |
| `relief` | étape G6, ramené à cinq classes | 1 |
| `climat` | étape C1 (déterminants physiques) | 1 |
| `gisements` | étape R1 (27 gisements nommés de 1400) | 1 |
| `adjacence` | étape G3 | 1 |

Tout ce que le jeu **déduit** de cette carte — rendements, populations,
gisements secondaires — est de niveau 2 : plausible, généré, jamais sourcé.
Une anomalie de niveau 2 n'est pas un défaut (ADR-0018).

## Les cinq classes de relief

`marais`, `plaine`, `colline`, `montagne`, `haute_montagne`. Les bornes
sont dans `build_world.py` : des ordres de grandeur plausibles, pas des
seuils sourcés. Ce qui compte est qu'une plaine ne soit pas classée
montagne, pas la valeur exacte de la borne.

Répartition actuelle : marais 20, plaine 322, colline 162, montagne 77,
haute montagne 15 — sur 596 cellules.
