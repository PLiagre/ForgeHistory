# viewer/

Regard mince sur une photographie `sim/`. Aucune logique métier : le
paquet lit un snapshot et montre les cellules déjà photographiées. Ce n'est
pas une seconde simulation.

Le regard local est un **tableau de bord** : carte au centre, bandeau de
totaux lus du snapshot, distribution de la couche active. Les chiffres
viennent de `/dashboard.json`, agrégat des cellules déjà présentes.

Trois états visuels, distincts, jamais confondus (`viewer/classify.py`) :

| état | ce que c'est | affichage |
|---|---|---|
| zéro mesuré | `0` — on a regardé, il n'y a rien | `0` |
| absent | clé manquante ou `null` | *absent* |
| non calculé | sentinelle `-1` | *non calculé* |

Un champ manquant s'affiche *absent* ; il n'est pas inventé. Un échantillon
vide (aucune cellule) rend HTTP 409 sur `/dashboard.json`, il ne rend pas
des zéros.

Le dashboard **ne porte pas le bourg** : cette vue n'est pas dans le
snapshot. La province l'est, recalculée à la photographie.

## Preuve SVG (sans navigateur, sans serveur)

```
py -m sim --ticks 0 --seed 0 --snapshot-json /tmp/world.json
py -m viewer --snapshot /tmp/world.json --proof-svg /tmp/carte.svg
```

Couche (défaut `population`) : `--layer nourriture`, ou toute clé de panier
présente dans le snapshot.

Comparaison de deux photographies :

```
py -m viewer --snapshot /tmp/a.json --compare /tmp/b.json --proof-svg /tmp/diff.svg
```

## Tableau de bord local (stdlib uniquement)

```
py -m sim --ticks 0 --seed 0 --snapshot-json /tmp/monde.json
py -m viewer --snapshot /tmp/monde.json
```

`--snapshot` est obligatoire. Hôte par défaut `127.0.0.1`, port `8765`.
`--host` et `--port` les changent. Si le port est pris, la commande refuse
(code 2) au lieu d'en choisir un autre en silence. Un snapshot illisible
refuse de la même façon.

Pour comparer deux instants, photographier à part (le bandeau suit le
snapshot chargé, il ne resimule pas) :

```
py -m sim --ticks 20 --seed 0 --snapshot-json /tmp/monde-t20.json
py -m viewer --snapshot /tmp/monde-t20.json --port 8766
```

Le viewer ne recalcule rien, ne lit que le snapshot, et ne charge
aucune ressource réseau.
