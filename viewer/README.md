# viewer/

Regard mince sur une photographie `sim/` (brief 028). Aucune logique
métier : le paquet lit un snapshot `v0a-1` et montre les cellules déjà
photographiées. Ce n'est pas une simulation.

## Preuve SVG (sans navigateur, sans serveur)

```
.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json /tmp/world.json
.venv/bin/python -m viewer --snapshot /tmp/world.json --proof-svg /tmp/carte.svg
```

Comparaison de deux photographies :

```
.venv/bin/python -m viewer --snapshot /tmp/a.json --compare /tmp/b.json --proof-svg /tmp/diff.svg
```

## Regard local (stdlib uniquement)

```
.venv/bin/python -m viewer --snapshot /tmp/world.json
```

Hôte par défaut `127.0.0.1`, port `8765`. Si le port est pris, la commande
refuse (code 2) au lieu d'en choisir un autre en silence.

Le viewer ne recalcule rien, ne lit pas `tools/map/`, et ne charge
aucune ressource réseau.
