# visualisateur/

Regard 3D. Il **lit** une photographie de `sim/` et n'en décide aucun
nombre — comme `viewer/`, un étage plus loin.

Il est le seul dossier du dépôt à demander des bibliothèques extérieures
(`requirements.txt`). C'est assumé : parler à un moteur de rendu écrit en
Rust ne se fait pas en bibliothèque standard. Rien d'autre ne dépend de
lui, et le jeu tourne sans lui — `py -m sim` n'a besoin de rien.
Le moteur de rendu est le fork [PLiagre/forge3d](https://github.com/PLiagre/forge3d)
(paquet PyPI `forge3d`).

```bash
# photographier le monde (le jeu, inchangé)
python3 -m sim --ticks 0 --seed 0 --snapshot-json /tmp/monde.json

# le montrer en relief, via forge3d
python3 -m visualisateur --snapshot /tmp/monde.json --png /tmp/monde-3d.png
```

Raccourci qui photographie puis rend :

```bash
python3 -m visualisateur --ticks 0 --seed 0 --png /tmp/monde-3d.png --apercu /tmp/monde-mnt.png
```

`--png` est obligatoire (argparse refuse avant le reste). Il faut ensuite
`--snapshot` **ou** `--ticks`. Un refus (fichier manquant, GPU absent,
géométrie inconnue) sort en code 2, message sur stderr.

| option | défaut | rôle |
|---|---|---|
| `--largeur` | 640 | largeur du MNT rasterisé |
| `--largeur-px` / `--hauteur-px` | 1280 × 720 | taille de l'image forge3d |
| `--apercu` | — | PNG vue du dessus, contrôle du raster, pas un rendu 3D |
| `--seed` | 0 | graine, seulement avec `--ticks` |

**Piège du raccourci `--ticks`.** Il appelle `tick(monde, rng)` **sans**
numéro de tick : la production joue la saison **moyenne annuelle**, pas le
jour du calendrier. Pour une photographie datée, passer par `python3 -m sim
--ticks N --snapshot-json …` puis `--snapshot`. Voir `sim/MODELE.md` § « Les
trois régimes de production ».

## Ce que c'est

La carte porte des **classes de relief**, pas un MNT en mètres. Le
visualisateur rasterise les polygones du snapshot, pose une altitude
plausible par classe (fidélité 2), et demande à forge3d une image
hors écran. L'exagération verticale est un facteur de **lecture**,
dérivé de l'étendue de la carte — pas de l'étendue kilométrique, qui
écraserait le relief. La caméra est `mesh:zup`, un seul cadre, sans
tuilage.

Une classe de relief inconnue est un refus, pas une invention.

## Dépendances (ce paquet seulement)

```bash
python3 -m pip install -r visualisateur/requirements.txt
```

Le backend wgpu est posé avant l'import : `WGPU_BACKENDS=vulkan` par défaut
dans `visualisateur/__main__.py`. `sim/` et `viewer/` restent en
bibliothèque standard. Rien d'ici n'y entre.

## Si ça refuse

| message | cause | quoi faire |
|---|---|---|
| `Il faut --snapshot ou --ticks.` | ni l'un ni l'autre | en passer un |
| `snapshot introuvable` | fichier absent | photographier d'abord |
| `forge3d est absent` | paquet non installé | `python3 -m pip install forge3d` dans le venv |
| `forge3d ne voit aucun adaptateur GPU` | pas de Vulkan / pas de carte | installer `mesa-vulkan-drivers`, ou une carte |
| `forge3d incomplet` | API trop vieille | `forge3d>=1.35.0` |
| `geometrie inconnue` | polygone hors Polygon / MultiPolygon | le snapshot n'est pas celui de `sim/` |

## Hors périmètre

Pas de fenêtre interactive. Pas de mer navigable. Pas de villes.
Pas de second monde. Le bassin maritime et le bourg ne sont pas dans le
snapshot : le relief ne les montre pas.
