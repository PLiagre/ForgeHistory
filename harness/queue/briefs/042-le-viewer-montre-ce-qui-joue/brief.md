# Brief 042 — Le regard mince montre ce que le moteur joue vraiment

**Authored**: 2026-08-26T10:20:00Z
**Author**: Claude
**Risque**: R2 — changement du contrat de photographie entre `sim/` et `viewer/`.

## But unique

Faire porter au snapshot, puis montrer par le visualiseur, l'état que les lots
034 à 041 ont ajouté au monde : le **panier de marchandises** de chaque cellule
et la **saison** du tick photographié.

Le snapshot passe au schéma suivant. Le visualiseur le lit ; il ne recalcule
rien, ne simule rien, et ne devient pas une seconde source de vérité.

Ce lot n'ajoute aucune mécanique au moteur et ne change aucun nombre du jeu.

## Dépendance

**Ce lot suppose les lots 037 et 038 fusionnés** : sans panier ni extraction, il
n'y a pas de marchandise à photographier. Il gagne à être lancé après 035, qui
donne une saison à afficher, mais ne l'exige pas : si le tick ne connaît pas
encore le jour de l'année, la partie saison du lot est **retirée**, pas
approximée.

## État de départ mesuré

Les commandes qui donnent l'état — à rejouer ; aucun de leurs résultats n'est
recopié ici comme cible :

```bash
.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json /tmp/base.json
.venv/bin/python -c "import json;d=json.load(open('/tmp/base.json'));print(d['schema_version'], sorted(d['cells'][0]))"
.venv/bin/python -m viewer --snapshot /tmp/base.json --proof-svg /tmp/base.svg
.venv/bin/python -m pytest viewer/tests/ -q
```

Le SHA de base du lot est le `master` du jour où il est lancé ; le mesureur
l'enregistre et compare contre lui.

**Le fait qualitatif qui rend ce lot caduc** : si le document de snapshot porte
déjà le panier de chaque cellule, il n'y a rien à faire ici.

## Règle du monde

**Fidélité : sans objet.** Ce lot ne touche à aucune donnée du monde, n'invente
aucun paramètre et ne fait rien émerger. Il photographie et il montre.

La règle qui gouverne le lot est la quatrième contre-mesure du dépôt : **la
présentation lit, elle ne décide jamais.** Aucun calcul du moteur n'est refait
dans `viewer/`, ni en Python, ni en JavaScript. Le visualiseur ne connaît ni les
facteurs de relief, ni les débits d'extraction, ni la formule de la saison.

Le contrat de photographie change donc de version. Le nom de la nouvelle version
n'est pas écrit dans ce brief : il est dérivé de la constante existante dans
`sim/constants.py`, en suivant la convention déjà en place (règle 12 — un
document qui porte une version morte piège le brief suivant).

## Ce que le snapshot gagne

Par cellule :

- **le panier**, marchandise par marchandise, tel que la cellule le porte ;
- l'ancien champ de stock de nourriture est **retiré** : sa valeur est dans le
  panier, et deux endroits pour la même grandeur seraient une base de données
  parallèle.

Au niveau du document :

- **le jour de l'année** du tick photographié, si le moteur en a un ; l'entrée
  est **absente** du document sinon. Elle ne vaut jamais une valeur inventée.

Les trois états visuels que le visualiseur distingue déjà — zéro mesuré, absent,
non calculé — s'appliquent à chaque valeur du panier, sans exception. Une
marchandise absente du panier n'est pas une marchandise à zéro.

## Ce que le visualiseur gagne

- Un **choix de couche** : la population, la nourriture, ou l'une des
  marchandises minières. La liste des couches proposées est **dérivée du
  snapshot chargé**, jamais écrite dans le code du visualiseur. Un snapshot sans
  marchandise minière ne propose pas de couche minière.
- La **preuve SVG** rend la couche demandée, avec la même échelle de valeurs et
  les mêmes trois états visuels qu'aujourd'hui.
- Le jour de l'année, s'il est dans le document, est affiché tel quel. Absent, il
  est dit absent — il n'est pas remplacé par un tiret muet ni par une date
  inventée.

## Source de vérité et raccord

Le document de snapshot est la **seule** entrée du visualiseur. Il ne lit ni
`data/world-1400.json`, ni `sim/`, ni le réseau — c'est déjà vrai et cela le
reste.

`sim/snapshot_export.py` reste la seule chose qui écrit un document, et il ne
recalcule aucune mécanique : il joint ce que la carte porte et ce que le moteur a
fait évoluer.

Le refus d'un schéma inconnu, déjà en place des deux côtés, reste en place : un
visualiseur qui reçoit un document d'une version qu'il ne connaît pas refuse en
le disant, au lieu d'afficher au mieux.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/snapshot_export.py` ;
- `sim/constants.py`, uniquement pour la version du schéma ;
- `viewer/snapshot_loader.py`, `viewer/svg_proof.py`, `viewer/server.py`,
  `viewer/__main__.py`, `viewer/static/app.js`, `viewer/static/index.html`,
  `viewer/static/style.css` ;
- `viewer/tests/test_viewer_v0b.py`, uniquement pour **ajouter** les cas qui
  protègent ces règles visibles ; les assertions déjà présentes restent
  inchangées, sauf celles qui nomment la version du schéma ou l'ancien champ de
  stock, qui sont substituées selon la règle ci-dessous ;
- `sim/tests/test_monde.py`, aux mêmes conditions.

Livrables du lot autorisés :

- `harness/queue/briefs/042-le-viewer-montre-ce-qui-joue/deliverables/manifest.json` ;
- `harness/queue/briefs/042-le-viewer-montre-ce-qui-joue/deliverables/generator-log.md` ;
- `harness/queue/briefs/042-le-viewer-montre-ce-qui-joue/deliverables/measure_042.py` ;
- les sorties déterministes produites par ce mesureur dans le même dossier
  `deliverables/`, y compris les preuves SVG.

Tout autre chemin est interdit. En particulier : ne modifier ni `sim/engine.py`,
ni `sim/world.py`, ni `sim/model.py`, ni `sim/aggregation.py`, ni
`sim/__main__.py`, ni la carte figée, ni l'outil de fabrication de la carte, ni
ce brief, ni sa grille, ni un `verdict.md`.

### La règle de substitution dans les tests

Deux choses changent de nom pour les contrôles existants : la version du schéma,
et le chemin d'accès au stock de nourriture d'une cellule photographiée. Pour
chaque ligne modifiée, la ligne d'origine à laquelle on applique **la seule
substitution du nom ou du chemin** doit être identique à la ligne d'arrivée.
Aucune valeur attendue, aucun seuil, aucun nom de test ne change. Le mesureur
compte les lignes qui violent cette règle ; ce compte doit être **nul**.

## Conditions de succès

### SC1 — Le document porte le panier, et une seule fois

Chaque cellule du document porte son panier. L'ancien champ de stock de
nourriture n'apparaît plus nulle part dans le document. Le nombre de cellules
inspectées est dérivé du document ; un document vide fait échouer le contrôle.

**Le rouge est prouvé avant la correction** : sur le SHA de base, aucune cellule
du document ne porte de panier.

### SC2 — La valeur photographiée est celle du moteur

Pour chaque cellule et chaque marchandise, la valeur du document est
**exactement** celle que porte la cellule du monde qui a servi à le produire.
Aucune conversion, aucun arrondi autre que celui, déjà nommé, que le module
applique à tous ses flottants.

Le contrôle compare cellule par cellule et marchandise par marchandise ; les deux
dénombrements sont dérivés du monde chargé.

### SC3 — Le jour de l'année est présent ou absent, jamais inventé

Si le moteur connaît le jour de l'année, le document le porte et sa valeur est
celle du tick photographié. Sinon, la clé est **absente** du document, et le
visualiseur le dit. Un contrôle vérifie les deux cas, le second en retirant la
notion côté moteur dans une copie en mémoire.

### SC4 — Le visualiseur propose exactement les couches du document

La liste des couches proposées est dérivée du document chargé : la population,
la nourriture, et une couche par marchandise minière **réellement présente**.
Un document sans marchandise minière n'en propose aucune.

Aucun nom de marchandise n'apparaît en dur dans `viewer/`. Un contrôle le
vérifie sur les sources du paquet, avec un nombre de fichiers dérivé du
répertoire.

### SC5 — Le visualiseur ne recalcule rien

Aucune constante du moteur — facteur de relief, débit d'extraction, ration,
capacité d'arête, formule de saison — n'apparaît dans `viewer/`, ni en Python ni
en JavaScript. Un contrôle cherche ces noms et ces valeurs dans les sources du
paquet et échoue s'il en trouve un.

C'est la contre-mesure n° 4 du dépôt, et c'est la faute la plus coûteuse que ce
lot pourrait introduire : une présentation qui réimplémente la simulation
diverge d'elle silencieusement.

### SC6 — Les trois états visuels survivent au panier

Une marchandise absente du panier, une marchandise à zéro et une marchandise
portant la sentinelle « non calculé » donnent **trois** rendus distincts. Un
contrôle les monte tous les trois et échoue si deux d'entre eux se confondent.

### SC7 — Le refus d'un schéma inconnu tient des deux côtés

Un document dont la version de schéma est inconnue est refusé par le
visualiseur, avec un message qui nomme la version reçue et celle qu'il attend.
Le contrôle existant qui porte ce refus reste vert, sa version substituée.

### SC8 — La preuve SVG reste déterministe

Deux exécutions de la preuve SVG sur le même document rendent des fichiers
byte-identiques, pour chacune des couches proposées. Le nombre de couches
essayées est dérivé du document.

### SC9 — Les invariants existants restent intacts

- `.venv/bin/python -m pytest sim/tests/ viewer/tests/ -q` est vert ;
- le nombre de tests collectés dans les deux suites est **au moins** celui du
  SHA de base ;
- `.venv/bin/python -m sim --ticks 365 --seed 0 --json` rend une sortie
  **byte-identique** à celle rejouée sur le SHA de base : ce lot ne touche pas
  au jeu ;
- aucune assertion existante n'a changé de valeur, de seuil ou de nom — le
  compte de violations de la règle de substitution est nul.

## Compteurs exigés

Le mesureur `deliverables/measure_042.py` reconstruit chaque compteur ; il ne
porte aucun résultat en dur.

| compteur | source d'échantillon | dénominateur dérivé |
|---|---|---|
| `cellules_du_document` | document produit après changement | nombre de cellules du monde chargé |
| `cellules_avec_panier` | même document | `cellules_du_document` |
| `occurrences_ancien_champ_dans_le_document` | même document, parcours récursif | nombre de clés réellement parcourues |
| `ecarts_panier_moteur_document` | comparaison cellule par cellule, marchandise par marchandise | nombre de couples cellule–marchandise réellement comparés |
| `couches_proposees_par_le_viewer` | liste dérivée du document chargé | nombre de marchandises réellement présentes, plus la population |
| `noms_de_marchandise_en_dur_dans_le_viewer` | parcours des sources du paquet `viewer/` | nombre de fichiers réellement parcourus |
| `constantes_du_moteur_trouvees_dans_le_viewer` | même parcours | nombre de constantes réellement cherchées |
| `etats_visuels_distincts` | absent, zéro et non calculé montés sur une marchandise | nombre d'états réellement montés |
| `svg_deterministes` | deux rendus par couche proposée | `couches_proposees_par_le_viewer` |
| `lignes_de_test_hors_substitution` | diff des fichiers de test contre le SHA de base | nombre de lignes du diff réellement examinées |
| `champs_cli_identiques` | sortie CLI du jeu, archivée avant édition et rejouée | nombre de champs réellement présents |
| `tests_collectes_avant` | collecte pytest sur le SHA de base | nombre de fichiers de test collectés |
| `tests_collectes_apres` | collecte pytest après changement | nombre de fichiers de test collectés |

`occurrences_ancien_champ_dans_le_document`, `ecarts_panier_moteur_document`,
`noms_de_marchandise_en_dur_dans_le_viewer`,
`constantes_du_moteur_trouvees_dans_le_viewer` et
`lignes_de_test_hors_substitution` doivent valoir **0**, et ces zéros sont des
mesures réelles. La sentinelle « non calculé » du projet est `-1`, jamais `0`.
`cellules_avec_panier` doit égaler `cellules_du_document`, et
`etats_visuels_distincts` doit valoir **3**.

## Livrables et porte mécanique

Le dossier `deliverables/` contient au minimum :

- `manifest.json`, avec les commandes exactes et les compteurs ci-dessus ;
- `generator-log.md`, en français clair : le rouge prouvé de SC1, les fichiers
  modifiés, les commandes jouées, les résultats et les limites ;
- `measure_042.py`, rejouable depuis la racine avec `.venv/bin/python` ;
- une preuve SVG par couche proposée, produite par la commande documentée.

Les chemins du manifeste sont relatifs au dossier du brief. Les comparaisons
avant/après passent par la référence Git du SHA de base, pas par une copie
`.orig` fabriquée après coup.

Attention : le document de snapshot doit **différer** du SHA de base, tandis que
la sortie CLI du jeu doit lui être **identique**.

## Hors périmètre

- toute mécanique nouvelle dans le moteur ;
- l'interactivité au-delà du choix de couche : pas de sélection, pas de
  chronologie, pas de lecture d'une suite de snapshots ;
- les couleurs, la typographie et la mise en page au-delà de ce qu'exige le
  choix de couche — le propriétaire juge le rendu, ce brief ne le décrit pas ;
- toute ressource réseau, police distante ou bibliothèque externe ;
- Unity, architecture, sécurité, CI, ForgePilot et fusion.

## Interdictions pour l'exécutant

L'exécutant n'écrit pas de `verdict.md`, ne modifie ni ce brief ni
`eval-rubric.md`, ne juge pas son propre travail, ne fusionne rien et ne pousse
pas directement sur `master`.
