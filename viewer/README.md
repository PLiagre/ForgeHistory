# viewer/

Regard mince sur une photographie `sim/`. Aucune logique métier : le
paquet lit un snapshot et montre les cellules déjà photographiées. Ce n'est
pas une seconde simulation.

## Preuve SVG (sans navigateur, sans serveur)

```
py -m sim --ticks 0 --seed 0 --snapshot-json /tmp/world.json
py -m viewer --snapshot /tmp/world.json --proof-svg /tmp/carte.svg
```

Comparaison de deux photographies :

```
py -m viewer --snapshot /tmp/a.json --compare /tmp/b.json --proof-svg /tmp/diff.svg
```

## Regard local (stdlib uniquement)

```
py -m viewer --snapshot /tmp/world.json
```

Hôte par défaut `127.0.0.1`, port `8765`. Si le port est pris, la commande
refuse (code 2) au lieu d'en choisir un autre en silence.

Le viewer ne recalcule rien, ne lit que le snapshot, et ne charge
aucune ressource réseau.
