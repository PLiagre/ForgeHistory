# Brief 044 — Un métier : le mineur

## But

Faire qu'une part des habitants d'une cellule à gisement **cesse de cultiver**
pour extraire. Ce qu'ils sortent de la mine, ils ne le sortent pas des champs.

C'est la première division du travail du jeu : jusqu'ici tout le monde faisait
tout, et la mine tournait en plus de l'agriculture, gratuitement.

**Ce lot ouvre le 047** (« le bourg est une agrégation dérivée »), qui compte la
part non agricole d'une cellule : aujourd'hui elle vaut zéro partout, et son
échantillon est vide. Il est indépendant du 046.

Ce qui rend ce lot caduc : si la production agricole d'une cellule dépend déjà
du nombre de ses habitants affectés à autre chose, il n'y a rien à faire ici.

```bash
grep -rn "part_miniere" sim/
py -m sim --ticks 365 --seed 0 --json
```

## Règle du monde

Découle de [`sim/MODELE.md`](../sim/MODELE.md), § « Le rendement agricole et sa
variabilité » — la production que ce lot atténue — et § « Ce qui dit que le
monde vit », qui explique pourquoi le plafond de survie suit tout seul. Si l'une
de ces sections a changé depuis, la relire avant de lancer.

**Fidélité niveau 2** : la part de la population qu'un gisement occupe est un
ordre de grandeur plausible, généré, jamais sourcé. Une répartition locale
surprenante n'est pas un défaut et n'ouvre ni correctif, ni lot.

### 1. Un gisement occupe des bras

```
part_miniere = min(PART_MINIERE_MAXIMALE,
                   somme sur les gisements de la cellule de
                       PART_MINIERE_PAR_GISEMENT × facteur_richesse)
```

| constante | valeur | ce que c'est |
|---|---:|---|
| `PART_MINIERE_PAR_GISEMENT` | 0.05 | part de la population qu'un gisement notable occupe |
| `PART_MINIERE_MAXIMALE` | 0.30 | plafond : une cellule ne devient jamais entièrement minière |

Les facteurs de richesse sont ceux qui existent déjà : un gisement majeur occupe
plus de bras et rend plus. Ils ne sont **pas** dupliqués.

**Le plafond est un invariant, pas un réglage de confort.** Sans lui, une cellule
chargée de gisements majeurs verrait toute sa population descendre à la mine et
mourir de faim au-dessus d'un filon d'argent. Ce n'est pas invraisemblable —
c'est arrivé — mais le modèle n'a pas de quoi le raconter, et un monde où c'est
le cas par construction n'apprend rien.

### 2. Ces deux constantes se lisent par une fonction, jamais par leur nom

`_MondeEpreuve`, dans `sim/tests/test_write_coverage.py`, n'a pas de gisement.
Une constante lue par son nom dans `sim/engine.py` y serait **inerte** : elle
entrerait dans le dénominateur de `test_chaque_constante_du_moteur_change_le_monde`
sans rien y faire bouger, et ce contrôle deviendrait rouge. C'est exactement le
piège que le lot 043 a payé.

Donc les deux constantes vivent dans `sim/constants.py`, et le moteur consulte la
part minière par **une seule fonction** de ce module, relue à chaque appel — le
même motif que `facteurs_production_par_relief()` :

```
def part_miniere_de(gisements, facteurs_richesse) -> float:
    ...  # relit PART_MINIERE_PAR_GISEMENT et PART_MINIERE_MAXIMALE
```

Interdit dans `engine.py` : `_constantes.PART_MINIERE_PAR_GISEMENT` ou
`_constantes.PART_MINIERE_MAXIMALE`. Autorisé : `_constantes.part_miniere_de(...)`.

Élargir `_MondeEpreuve` pour lui donner un gisement n'est **pas** la solution
retenue, et le périmètre l'interdit.

### 3. Les bras à la mine ne sont pas aux champs

```
production agricole de la cellule = production_actuelle × (1 - part_miniere)
```

Cette atténuation entre dans l'unique formule de production, à côté du facteur de
relief et du facteur de saison. Il n'y a toujours **qu'une** formule de
production alimentaire dans `sim/`.

La prémisse inverse — « un gisement ne change rien à la nourriture » — vivait
dans `sim/tests/test_monde.py`. Elle a été retirée par un geste séparé **avant**
ce lot, parce que cette règle-ci la rend fausse. Ne la recrée pas : SC1 et SC2
disent ce qui la remplace.

### 4. Ce qu'ils extraient est proportionnel à ceux qui extraient

Le débit d'extraction, aujourd'hui calculé sur la population entière, se calcule
désormais sur la **part minière** de cette population. Le même gisement, sur une
cellule deux fois plus peuplée, rend deux fois plus — c'était déjà vrai — mais il
rend maintenant ce que ses mineurs, et eux seuls, produisent.

### Ce que ça produit sans être écrit

Une cellule à gisement cultive moins. Sa production baisse, sa dette alimentaire
monte si la ration ne suit pas. Aucune règle ne dit « les villes minières
importent leur nourriture » ; le commerce peut rester trop petit pour les
nourrir. Ce lot **ne mesure pas un flux** : il mesure la production et la dette.

### Où ça se raccorde

Les gisements, leur richesse et leur ressource viennent **uniquement** de
`world.carte[cell_id]["gisements"]`. La part minière se calcule à **un seul
endroit**, lu à la fois par la production agricole et par l'extraction : deux
calculs séparés dériveraient l'un de l'autre au premier changement. Le plafond de
survie, `production_moyenne_kg_par_tick()`, reste dérivé de la **même** formule de
production que le tick — il tient compte de l'atténuation sans qu'on le lui dise.

## Périmètre

En écriture : `sim/engine.py`, `sim/constants.py`, et `sim/tests/test_monde.py`
— ce dernier **uniquement pour y ajouter des cas**.

**Aucun test existant n'est modifié, renommé, supprimé ni relâché**, dans aucun
fichier, `sim/tests/test_monde.py` compris. Si un test existant devient rouge,
c'est le code de ce lot qui est faux, ou c'est ce brief : on s'arrête et on le
dit — on ne touche pas au test.

Tout autre chemin est interdit, nommément : `sim/world.py`, `sim/model.py`,
`sim/snapshot_export.py`, `sim/__main__.py`, `sim/aggregation.py`,
`sim/tests/test_survie.py`, `sim/tests/test_commerce.py`,
`sim/tests/test_write_coverage.py`, `sim/tests/test_determinisme.py`,
`sim/tests/test_province.py`, `sim/tests/test_no_hardcoded.py`, la carte figée,
le visualiseur, et ce brief.

## Conditions de succès

Les comparaisons « avant / après » se font contre `master` rejoué au démarrage du
lot, jamais contre un nombre recopié d'ici.

### SC1 — Une cellule à gisement cultive moins

À surface, relief, date et rendement aléatoire identiques, une cellule porteuse
d'un gisement produit **strictement moins** de nourriture qu'une cellule sans
gisement. Les deux cellules sont **dérivées de la carte** : une porteuse et une
non porteuse de même classe de relief. Si aucune paire de ce genre n'existe, le
contrôle échoue au lieu de la fabriquer.

**Rouge prouvé d'abord** : sur `master`, ces deux cellules produisent exactement
la même chose.

### SC2 — La baisse ne touche que les porteuses

Deux mondes sont joués sur la même graine à partir de la **même** carte : l'un
tel quel, l'autre avec la liste des gisements vidée sur toutes les cellules,
**rien d'autre n'étant changé**. Au **premier tick**, l'ensemble des cellules
dont le stock de nourriture diffère entre les deux mondes est **exactement**
l'ensemble des cellules que la carte déclare porteuses. Les deux ensembles sont
dérivés de la carte ; ni l'un ni l'autre n'est écrit, et un ensemble de
porteuses vide fait échouer le contrôle.

Ce contrôle porte sur le premier tick, et sur lui seul : au-delà, le commerce et
la migration propagent légitimement l'écart aux cellules voisines, et il n'y a
plus d'ensemble attendu à dériver.

**Rouge prouvé d'abord** : sur `master`, l'ensemble qui change est vide alors que
celui des porteuses ne l'est pas.

### SC3 — La richesse ordonne l'atténuation

À population égale, la part minière suit strictement l'ordre des richesses :
majeure > notable > mineure. Les trois classes sont dérivées de la carte ; si
l'une manque, le contrôle échoue au lieu de la sauter.

### SC4 — Le plafond tient

Une cellule à laquelle on ajoute, en mémoire, assez de gisements majeurs pour
dépasser le plafond garde une part minière **exactement** égale au plafond, et
continue de produire de la nourriture. Le nombre de gisements ajoutés est dérivé
des constantes, jamais écrit.

### SC5 — Une seule définition

Un contrôle parcourt l'arbre syntaxique des modules de `sim/` hors tests et
échoue si plus d'une fonction calcule la part minière, ou si un second jeu de
facteurs de richesse apparaît. Le nombre de modules parcourus est dérivé du
répertoire. De même, il n'y a toujours qu'une seule formule de production
alimentaire.

### SC6 — L'extraction suit les mineurs, pas la population

À gisement identique, la quantité extraite est **proportionnelle à la part
minière**, non à la population entière. Le contrôle compare deux cellules dont
les parts minières diffèrent d'un facteur dérivé de leurs gisements.

Une cellule dont la part minière est nulle n'extrait rien. Ce zéro est une
**mesure réelle** : le maillon a été joué et a compté zéro. La sentinelle « non
calculé » est `-1`, jamais `0`.

### SC7 — Les cellules minières produisent moins et s'endettent plus

Sur le monde réel joué à un horizon dérivé, pour l'ensemble des cellules que la
carte déclare porteuses d'au moins un gisement :

- la somme de leurs productions agricoles est **strictement inférieure** à celle
  rejouée sur `master` ;
- la somme de leurs `food_deficit_kg` est **strictement supérieure**.

On ne mesure **pas** les kilogrammes reçus par le commerce.

**Rouge prouvé d'abord** : sur `master`, les deux écarts sont nuls.

### SC8 — Le plafond de survie reste dérivé, et il tient

Les propriétés de régime de `sim/tests/test_survie.py` restent vertes **sans que
ce fichier soit modifié**. Le plafond descend tout seul parce qu'il appelle la
même formule de production que le tick : c'est ce que ce couplage a été écrit
pour garantir. Vérifier aussi à un horizon cinq fois plus long, pour montrer que
l'effet se stabilise.

### SC9 — Les invariants existants restent intacts

```bash
py -m pytest sim/tests/ viewer/tests/ -q
```

```bash
git diff master -- sim/tests/ | grep "^-" | grep -v "^---"
```

- la suite est verte, et la liste des tests en échec est **vide**, comparée à
  celle de `master` plutôt que supposée ;
- la seconde commande ne sort **rien** : aucune ligne retirée d'aucun fichier de
  test, donc aucun test existant modifié, renommé, supprimé ni relâché ;
- `test_chaque_constante_du_moteur_change_le_monde`,
  `test_le_moteur_ne_lie_aucune_constante_par_valeur`,
  `test_aucune_constante_terminale` et `test_no_hardcoded_numeric_literals`
  restent verts, **sans que `_MondeEpreuve` soit touché** ;
- aucun des noms `PART_MINIERE_PAR_GISEMENT` ni `PART_MINIERE_MAXIMALE`
  n'apparaît comme attribut lu dans `sim/engine.py` ; le compteur des noms
  trouvés vaut **0** ;
- deux exécutions de `py -m sim --ticks 365 --seed 0 --json` sont strictement
  identiques entre elles, et différentes de celle de `master` ;
- aucune instruction `global` dans `sim/engine.py` ;
- le nombre de tests collectés est strictement supérieur à celui de `master`.

## Hors périmètre

- `sim/MODELE.md` — la mise à jour après fusion est une dette de l'architecte du
  modèle, pas de l'exécutant ;
- tout métier autre que le mineur ;
- le salaire, le prix, le marché, la propriété, la classe sociale ;
- la transformation du minerai, la fonte, l'artisanat ;
- le déplacement des mineurs vers les gisements ; l'épuisement d'un gisement ;
- la définition d'un bourg ou d'une ville — c'est le lot 047 ;
- le schéma du snapshot, sa version, le visualiseur ;
- la calibration d'un test existant après observation, et toute autre retouche
  d'un test existant, quel qu'en soit le motif.
