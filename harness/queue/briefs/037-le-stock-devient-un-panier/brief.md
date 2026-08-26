# Brief 037 — Le stock d'une cellule devient un panier de marchandises

**Authored**: 2026-08-26T09:30:00Z
**Author**: Claude
**Risque**: R2 — changement structurel du modèle de données du moteur, sans changement de comportement observable.

## But unique

Remplacer le champ `Cell.food_stock_kg` — un seul nombre, une seule marchandise
— par un **panier** : `Cell.stocks`, une table marchandise → kilogrammes, dont
la nourriture est la première entrée et, pour l'instant, la seule.

Ce lot ne change **rien** au comportement du monde. C'est sa condition de
succès principale : à graine égale, le jeu doit rendre exactement les mêmes
nombres, et le snapshot exactement le même document, avant et après.

Ce lot n'ajoute aucune marchandise, ne fait rien produire, ne transporte rien de
nouveau et ne touche pas au visualiseur.

## Pourquoi maintenant, et pas plus tard

L'économie du jeu est physique : tout a une origine, un transport, un stockage,
une destination (principe 3). Aujourd'hui, le seul stockage qui existe est un
flottant nommé d'après son contenu. Les deux lots qui suivent — l'extraction des
gisements, puis le commerce de ce qui en sort — ne peuvent pas se poser dessus
sans ajouter un deuxième champ de cas particulier à côté du premier. Deux
magasins parallèles pour deux marchandises, puis trois, sont exactement la dette
que la vision appelle « les systèmes remplis de cas particuliers ».

Le faire maintenant coûte une réécriture mécanique à comportement figé. Le faire
après le lot 038 coûte la même réécriture, plus le démêlage d'un cas
particulier déjà écrit.

## Fondement dans le modèle

**Aucun mécanisme.** Ce lot ne découle d'aucune affirmation de `sim/MODELE.md`
sur le fonctionnement du monde : il change la forme du stockage sans changer
un seul nombre. La sentinelle qu'il doit préserver est décrite au § « Le
déficit alimentaire et la mortalité ».

## État de départ mesuré

Les commandes qui donnent l'état — à rejouer ; aucun de leurs résultats n'est
recopié ici comme cible :

```bash
grep -rn "food_stock_kg" --include=*.py sim/ viewer/ | wc -l
.venv/bin/python -m sim --ticks 365 --seed 0 --json
.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json /tmp/base.json
.venv/bin/python -m pytest sim/tests/ viewer/tests/ -q
```

Le SHA de base du lot est le `master` du jour où il est lancé ; le mesureur
l'enregistre et compare contre lui.

**Le fait qualitatif qui rend ce lot caduc** : si `Cell` porte déjà un champ de
type table pour ses stocks, il n'y a rien à faire ici.

## Règle du monde

**Fidélité : sans objet.** Ce lot ne touche à aucune donnée du monde,
n'introduit aucun paramètre physique et ne lit aucune valeur nouvelle de la
carte. Il n'y a rien ici dont on puisse dire que c'est juste ou plausible. Le
seul critère est l'identité du comportement.

La forme :

```
Cell.stocks : dict[str, float]        # marchandise → kilogrammes
MARCHANDISE_NOURRITURE = "nourriture" # constante nommée, dans sim/constants.py
```

Deux accès nommés, et **seuls** ces deux-là, dans `sim/model.py` :

- lire le stock d'une marchandise ; une marchandise absente du panier rend la
  sentinelle `-1.0`, « non calculé », **exactement** comme le champ absent le
  faisait (règle 8 : un zéro est une mesure réelle, jamais un aveu) ;
- écrire le stock d'une marchandise.

Une `Cell` construite sans panier a un panier vide, et son stock de nourriture
se lit donc `-1.0` : c'est le comportement d'aujourd'hui, à l'identique.

Le panier n'est **pas** une seconde base de données spatiale : il vit sur la
cellule, qui reste la clé spatiale unique (ADR-0003).

## Source de vérité et raccord au moteur

Aucun module de `sim/` hors `sim/model.py` n'accède au dictionnaire `stocks`
directement : tous passent par les deux accès nommés. Un contrôle le vérifie sur
l'arbre syntaxique.

`sim/snapshot_export.py` continue d'exporter, pour chaque cellule, un champ
`food_stock_kg` portant la valeur lue dans le panier. **Le schéma du snapshot ne
change pas**, sa version non plus, et le visualiseur n'a rien à changer. Le jour
où d'autres marchandises existeront, un autre lot décidera comment les
photographier.

La sérialisation canonique `World.to_dict()`, qui sert au déterminisme et à la
sonde des couches, **porte le panier** : chaque cellule y expose ses stocks,
marchandise par marchandise. Les clés historiques restent (population, stock
de nourriture lu dans le panier, faim, dette, remainder) ; le panier s'ajoute.
Ce n'est pas une seconde base spatiale : c'est l'état de la cellule.

SC2 ne se joue pas sur `to_dict`. Il se joue sur la CLI et le snapshot, qui
restent **byte-identiques**. Geler les clés de `to_dict` rendrait invisible
toute marchandise autre que la nourriture, et le lot 038 ne pourrait pas
prouver que la sonde voit les gisements.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/model.py` — le champ `stocks` et les deux accès nommés ;
- `sim/constants.py` — la constante de nom de marchandise ;
- `sim/engine.py`, `sim/world.py`, `sim/__main__.py`,
  `sim/snapshot_export.py` — la substitution, et rien d'autre ;
- `sim/tests/test_commerce.py`, `sim/tests/test_survie.py`,
  `sim/tests/test_monde.py`, `sim/tests/test_determinisme.py`,
  `sim/tests/test_write_coverage.py` — **la substitution seule**, selon la règle
  ci-dessous.

Livrables du lot autorisés :

- `harness/queue/briefs/037-le-stock-devient-un-panier/deliverables/manifest.json` ;
- `harness/queue/briefs/037-le-stock-devient-un-panier/deliverables/generator-log.md` ;
- `harness/queue/briefs/037-le-stock-devient-un-panier/deliverables/measure_037.py` ;
- les sorties textuelles déterministes produites par ce mesureur dans le même
  dossier `deliverables/`.

Tout autre chemin est interdit. En particulier : ne modifier ni
`sim/aggregation.py`, ni `viewer/`, ni la carte figée, ni l'outil de fabrication
de la carte, ni ce brief, ni sa grille, ni un `verdict.md`.

### La règle de substitution dans les tests, et pourquoi ce n'est pas une calibration

Ce lot supprime un champ que soixante-six lignes de test nomment. Ces lignes
doivent changer, sinon rien ne compile. C'est la seule modification de test
autorisée, et elle est **mécanique**, donc vérifiable :

> Pour chaque ligne modifiée d'un fichier de test, la ligne d'origine à laquelle
> on applique la substitution du champ par l'accès nommé doit être **identique**
> à la ligne d'arrivée.

Le mesureur applique cette règle ligne à ligne au diff des fichiers de test et
compte les lignes qui la violent. Ce compte doit être **nul**.

Aucune valeur attendue, aucune tolérance, aucun seuil, aucun nom de test ne
change. Le défaut n° 4 que cherche le relecteur — desserrer un critère après
avoir vu une mesure qui le dépasse — n'a pas lieu ici : aucun critère n'a
échoué, et aucun ne change de valeur.

## Conditions de succès

### SC1 — Le champ a disparu et le panier existe

`Cell` n'a plus de champ `food_stock_kg`. Il a un champ de type table, amorcé
vide par défaut, et deux accès nommés dans `sim/model.py`.

Un contrôle parcourt l'arbre syntaxique des modules de `sim/` hors tests et
échoue si un attribut `food_stock_kg` y est encore lu ou écrit. Le nombre de
modules parcourus est dérivé du répertoire ; un parcours vide fait échouer le
contrôle au lieu de passer.

**Le rouge est prouvé avant la correction** : ce contrôle, lancé sur le SHA de
base, échoue en nommant les modules fautifs.

### SC2 — Le monde ne bouge pas d'un octet

Trois identités, toutes vérifiées contre une référence rejouée sur le SHA de
base et archivée **avant** l'édition :

- `.venv/bin/python -m sim --ticks 20 --seed 0 --json` : sortie byte-identique ;
- `.venv/bin/python -m sim --ticks 365 --seed 0 --json` : sortie
  byte-identique ;
- `.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json …` : document
  byte-identique, y compris son numéro de version de schéma.

Le mesureur compare champ par champ ; le nombre de champs est dérivé du contenu
comparé. Il ne recopie aucun nombre du présent brief.

Une seule différence fait échouer le lot.

### SC3 — La sentinelle survit à la traduction

Une marchandise absente du panier se lit `-1.0`. Une marchandise présente à
zéro se lit `0.0`. Un contrôle distingue les deux et échoue si l'un prend la
valeur de l'autre — c'est la règle 8, et c'est le point où une migration bâclée
transforme une absence en mesure.

### SC4 — Personne ne contourne les accès nommés

Aucun module de `sim/` hors `sim/model.py` n'indexe le dictionnaire de stocks
directement. Un contrôle sur l'arbre syntaxique le vérifie, avec un nombre de
modules dérivé du répertoire.

### SC5 — Le panier accepte une deuxième marchandise sans code nouveau

Écrire puis relire une marchandise qui n'existe nulle part ailleurs dans le jeu
fonctionne, n'affecte pas le stock de nourriture de la même cellule, et ne
demande aucune ligne de code supplémentaire.

C'est la seule chose que ce lot ajoute au monde, et elle n'est encore utilisée
par personne : c'est ce qui permet aux lots 038 et 039 d'exister.

`World.to_dict()` d'un monde amorcé **porte le panier** de chaque cellule. Une
marchandise absente du snapshot et de la CLI y figure dès qu'elle est écrite
via l'accès nommé. L'empreinte interne n'est pas gelée : seules la CLI et le
snapshot le sont (SC2).

### SC6 — Les invariants existants restent intacts

- `.venv/bin/python -m pytest sim/tests/ viewer/tests/ -q` est vert ;
- `test_all_dataclass_fields_have_write_and_read_sites` reste vert : le champ
  ajouté a un site d'écriture et un site de lecture ;
- aucune assertion de test n'a changé de valeur, de seuil ou de nom — le compte
  de violations de la règle de substitution est nul ;
- `test_no_hardcoded_numeric_literals` reste vert ;
- aucune instruction `global` n'apparaît dans `sim/engine.py` ;
- le nombre de tests collectés dans `sim/tests/` est **au moins** celui du SHA
  de base : aucun contrôle n'a été supprimé au passage.

## Compteurs exigés

Le mesureur `deliverables/measure_037.py` reconstruit chaque compteur ; il ne
porte aucun résultat en dur.

| compteur | source d'échantillon | dénominateur dérivé |
|---|---|---|
| `modules_sim_parcourus` | parcours du répertoire `sim/` hors tests | nombre de fichiers Python réellement trouvés |
| `references_au_champ_supprime_avant` | même parcours joué sur le SHA de base | `modules_sim_parcourus` à ce SHA |
| `references_au_champ_supprime_apres` | même parcours après changement | `modules_sim_parcourus` |
| `acces_directs_au_panier_hors_modele` | parcours de l'arbre syntaxique après changement | `modules_sim_parcourus` |
| `champs_cli_identiques` | comparaison des sorties CLI archivées et d'après | nombre de champs réellement présents dans la sortie |
| `cles_snapshot_identiques` | comparaison des documents snapshot archivé et d'après | nombre de clés réellement présentes dans le document |
| `cellules_to_dict_avec_panier` | `World.to_dict()` d'un monde amorcé, cellules portant un panier | nombre de cellules réellement chargées |
| `lignes_de_test_modifiees` | diff des fichiers de test contre le SHA de base | nombre de lignes du diff réellement examinées |
| `lignes_de_test_hors_substitution` | mêmes lignes, règle de substitution appliquée | `lignes_de_test_modifiees` |
| `tests_collectes_avant` | collecte pytest sur le SHA de base | nombre de fichiers de test collectés |
| `tests_collectes_apres` | collecte pytest après changement | nombre de fichiers de test collectés |

`references_au_champ_supprime_apres`, `acces_directs_au_panier_hors_modele` et
`lignes_de_test_hors_substitution` doivent valoir **0**, et ces zéros sont des
mesures réelles : le mesureur a parcouru et compté. La sentinelle « non
calculé » du projet est `-1`, jamais `0`.
`references_au_champ_supprime_avant` doit être strictement positif, sans quoi le
rouge n'a pas été prouvé. `tests_collectes_apres` ne peut pas être inférieur à
`tests_collectes_avant`. `cellules_to_dict_avec_panier` doit égaler le nombre
de cellules réellement chargées.

## Livrables et porte mécanique

Le dossier `deliverables/` contient au minimum :

- `manifest.json`, avec les commandes exactes et les compteurs ci-dessus ;
- `generator-log.md`, en français clair : le rouge prouvé de SC1, les fichiers
  modifiés, les commandes jouées, les résultats et les limites ;
- `measure_037.py`, rejouable depuis la racine avec `.venv/bin/python`.

Les chemins du manifeste sont relatifs au dossier du brief. Les comparaisons
avant/après passent par la référence Git du SHA de base, pas par une copie
`.orig` fabriquée après coup.

Attention : `sim/model.py` et `sim/engine.py` doivent **différer** du SHA de
base, tandis que la sortie CLI et le snapshot doivent lui être **identiques**.
`World.to_dict()` **diffère** : il porte le panier. Ce n'est pas une violation
de SC2.

## Hors périmètre

- toute marchandise réellement produite, consommée ou transportée ;
- les gisements, le climat, la natalité, la migration ;
- le schéma du snapshot, sa version, et le visualiseur ;
- toute modification du comportement du monde, si petite soit-elle ;
- toute constante physique nouvelle ou modifiée ;
- calibration d'un test existant après observation ;
- Unity, architecture, sécurité, CI, ForgePilot et fusion.

## Interdictions pour l'exécutant

L'exécutant n'écrit pas de `verdict.md`, ne modifie ni ce brief ni
`eval-rubric.md`, ne juge pas son propre travail, ne fusionne rien et ne pousse
pas directement sur `master`.
