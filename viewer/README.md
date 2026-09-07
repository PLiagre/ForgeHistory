# viewer/

Regard mince sur une photographie `sim/`. Aucune logique métier : le
paquet lit un snapshot et montre les cellules déjà photographiées. Ce n'est
pas une seconde simulation.

Le regard local est un **tableau de bord** : carte au centre, bandeau de
totaux lus du snapshot, distribution de la couche active. Les chiffres
viennent de `/dashboard.json`, agrégat des cellules déjà présentes. Un
champ manquant s'affiche *absent* ; il n'est pas inventé. Un zéro est une
mesure. `-1` est *non calculé* (règle 8).

Le schéma attendu est `SNAPSHOT_SCHEMA_VERSION` (`sim/constants.py`). Un
document qui porte une version morte piège le lot suivant (règle 12).

## Preuve SVG (sans navigateur, sans serveur)

```
py -m sim --ticks 0 --seed 0 --snapshot-json /tmp/world.json
py -m viewer --snapshot /tmp/world.json --proof-svg /tmp/carte.svg
```

`--layer` choisit la couche colorée (`population` par défaut, ou une
clé de panier présente dans le snapshot). Un schéma inconnu, un fichier
absent, `--snapshot` manquant : refus, code 2.

Comparaison de deux photographies :

```
py -m viewer --snapshot /tmp/a.json --compare /tmp/b.json --proof-svg /tmp/diff.svg
```

Une valeur absente ou non calculée d'un côté rend la paire
*incomparable* : pas de fausse différence.

## Tableau de bord local (stdlib uniquement)

```
py -m sim --ticks 0 --seed 0 --snapshot-json /tmp/monde.json
py -m viewer --snapshot /tmp/monde.json
```

Hôte par défaut `127.0.0.1`, port `8765`. Si le port est pris, la commande
refuse (code 2) au lieu d'en choisir un autre en silence.

Pour comparer deux instants, photographier à part (le bandeau suit le
snapshot chargé, il ne resimule pas) :

```
py -m sim --ticks 20 --seed 0 --snapshot-json /tmp/monde-t20.json
py -m viewer --snapshot /tmp/monde-t20.json --port 8766
```

### Ce que le serveur sert

| chemin | contenu |
|---|---|
| `/` | page statique (`viewer/static/`) |
| `/snapshot.json` | la photographie chargée |
| `/compare.json` | la seconde, ou 404 si `--compare` est absent |
| `/dashboard.json` | totaux du monde et agrégats par couche |
| `/meta.json` | `{ "has_compare": true\|false }` |

`/dashboard.json` agrège ce qui est déjà dans le snapshot :

- **monde** : population, cellules affamées, stock de nourriture,
  `tick`, `seed`, `jour_de_tick`, `kg_transportes` — chacun avec un
  `etat` (`mesure`, `absent`, `non_calcule`) ;
- **couches** : `population` puis chaque clé de panier, avec min, max,
  histogramme et sommes par province.

Un snapshot sans aucune cellule rend **409** : un échantillon vide échoue,
il ne passe pas pour un monde à zéro.

Le viewer ne recalcule rien, ne lit que le snapshot, et ne charge
aucune ressource réseau. Le bourg n'y figure pas : la vue existe dans
`sim/aggregation.py`, le snapshot ne la photographie pas encore.
