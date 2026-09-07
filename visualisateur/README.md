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

Raccourci qui photographie puis rend, sans recalculer le tick :

```bash
python3 -m visualisateur --ticks 0 --seed 0 --png /tmp/monde-3d.png --apercu /tmp/monde-mnt.png
```

## Ce que c'est

La carte porte des **classes de relief**, pas un MNT en mètres. Le
visualisateur rasterise les polygones du snapshot, pose une altitude
plausible par classe (fidélité 2), et demande à forge3d une image
hors écran. L'exagération verticale est un facteur de **lecture**,
dérivé de l'étendue de la carte.

## Dépendances (ce paquet seulement)

Les versions sont dans `requirements.txt` (`forge3d>=1.35.0`). Le jeu
tourne sans ce paquet.

```bash
python3 -m pip install -r visualisateur/requirements.txt
```

`sim/` et `viewer/` restent en bibliothèque standard. Rien d'ici
n'y entre.

## Interface

Il faut `--snapshot` **ou** `--ticks`. Les deux absents : refus, code 2.
`--png` est obligatoire.

| option | défaut | rôle |
|---|---|---|
| `--snapshot` | — | photographie JSON déjà écrite |
| `--ticks` / `--seed` | — / `0` | photographier puis rendre (écrit `/tmp/visualisateur-monde.json`) |
| `--png` | (requis) | image 3D écrite par forge3d |
| `--apercu` | — | PNG vue du dessus du raster, pas un rendu 3D |
| `--largeur` | 640 | largeur du MNT rasterisé (pas de l'image) |
| `--largeur-px` / `--hauteur-px` | 1280 / 720 | taille de l'image forge3d |

La caméra est `mesh:zup`, sans tuilage : un seul cadre pour le monde
entier. L'exagération verticale est un facteur de **lecture**, dérivé de
l'étendue du MNT.

## Quand ça refuse (code 2)

- snapshot introuvable, ou `--snapshot` et `--ticks` tous les deux absents ;
- géométrie inconnue, snapshot sans cellule, classe de relief hors des
  cinq de la carte (`marais`, `plaine`, `colline`, `montagne`,
  `haute_montagne`) — jamais une altitude inventée ;
- `forge3d` absent du venv, ou aucun adaptateur GPU (installer
  `mesa-vulkan-drivers`, ou une carte).

`WGPU_BACKENDS=vulkan` est posé **avant** l'import de forge3d : wgpu
verrouille le backend au premier contexte.

## Hors périmètre

Pas de fenêtre interactive. Pas de mer navigable. Pas de villes.
Pas de second monde. Pas de bourg.
