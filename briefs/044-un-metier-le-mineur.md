# Brief 044 — Un métier : le mineur

## But

Faire qu'une part des habitants d'une cellule à gisement **cesse de cultiver**
pour extraire. Ce qu'ils sortent de la mine, ils ne le sortent pas des champs.

C'est la première division du travail du jeu : jusqu'ici tout le monde faisait
tout, et la mine tournait en plus de l'agriculture, gratuitement.

**Ce lot ouvre le 047** (« le bourg est une agrégation dérivée »), qui compte la
part non agricole d'une cellule : aujourd'hui elle vaut zéro partout, et son
échantillon est vide. Il est indépendant du 046.

Ce qui rend la réparation ci-dessous caduque : si la constante de rendement
agricole n'est plus lue qu'à un seul endroit de `sim/`, il n'y a rien à faire.

```bash
grep -rn "FOOD_PRODUCTION_KG_PER_KM2_PER_TICK" sim/*.py
py -m sim --ticks 365 --seed 0 --json
```

## État du lot, et ce qui reste

**Le lot est exécuté et fusionné** (PR #184) : une part des habitants d'une
cellule à gisement a cessé de cultiver, et le monde le mesure. La « Règle du
monde » ci-dessous est désormais le compte rendu de ce qui tourne, pas une
commande à rejouer.

Ce brief rouvre **une réparation, et rien d'autre**. Deux défauts ont survécu à
la relecture finale :

1. **La formule agricole de base existe en deux exemplaires** :
   `sim/engine.py:126-132`, dans `_production_base_kg`, et
   `sim/engine.py:180-184`, recopiée dans `production_kg`. La duplication est
   **antérieure** à ce lot — elle vient du chemin unitaire historique. Ce lot ne
   l'a pas créée : il a écrit le contrôle qui devait l'attraper, et ce contrôle
   ne l'a pas attrapée.
2. **SC5 comparait le nombre de formules à `master`**, qui porte la duplication.
   Deux égale deux : le contrôle restait vert sur une propriété fausse. Une
   référence rejouée sur `master` ne peut pas voir un défaut que `master`
   contient déjà — c'est le sixième mode de défaillance d'AGENTS.md sous une
   forme neuve, où le contrôle nomme sa référence en la recopiant sur l'état du
   jour.

Ce que la réparation **ne rouvre pas** : la règle du monde, les constantes, la
part minière, l'extraction, et les conditions déjà prouvées par l'exécution
fusionnée.

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

### 5. La réparation : une seule formule, et un contrôle qui sait rougir

`production_kg` recopie le noyau que `_production_base_kg` porte déjà. Les deux
copies calculent le même produit — surface × rendement au km² × facteur de
rendement — et rien ne les tient ensemble : le jour où l'une gagne un facteur,
l'autre le perd en silence. C'est déjà arrivé ici, en petit : le chemin sans
carte de `production_kg` ignore la part minière que la formule du tick applique.

`production_kg` appelle donc `_production_base_kg` au lieu de le recopier. La
constante de rendement agricole n'est plus lue qu'à **un** endroit.

**La réparation est neutre en sortie** : même arithmétique, mêmes facteurs, même
ordre. Si le monde produit un kilogramme de plus ou de moins après, ce n'est pas
une réparation mais un changement de règle, et le brief est à réécrire par son
auteur.

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

En écriture : `sim/engine.py`, **pour cette déduplication et rien d'autre**, et
`sim/tests/test_monde.py` **uniquement pour y ajouter** le contrôle dérivé que
SC5 demande.

**Un préalable, qui n'appartient pas à ce lot.** La dernière assertion de
`test_une_seule_definition_part_miniere`, `formules_ici == formules_master`, est
le défaut lui-même : elle épingle le moteur sur la forme que `master` porte
aujourd'hui, duplication comprise. Elle devient rouge à l'instant où la
duplication tombe — mesuré, `1 != 2`. Elle ne peut donc pas rester, et un lot ne
retouche pas un test existant : **elle se retire par un geste séparé, avant la
réparation**, comme la prémisse du minerai l'a été au commit `91cea39`. Le reste
de ce contrôle — modules parcourus, motif relief, jeux de richesse — n'est pas
concerné et reste en place.

Si ce préalable n'a pas été fait, la réparation **s'arrête et le dit**. Elle ne
retouche pas l'assertion elle-même.

**Aucun test existant n'est modifié, renommé, supprimé ni relâché**, dans aucun
fichier, `sim/tests/test_monde.py` compris. Si un test existant devient rouge,
c'est le code de cette réparation qui est faux, ou c'est ce brief : on s'arrête
et on le dit — on ne touche pas au test.

Tout autre chemin est interdit, nommément : `sim/constants.py`, `sim/world.py`,
`sim/model.py`, `sim/snapshot_export.py`, `sim/__main__.py`,
`sim/aggregation.py`, `sim/tests/test_survie.py`, `sim/tests/test_commerce.py`,
`sim/tests/test_write_coverage.py`, `sim/tests/test_determinisme.py`,
`sim/tests/test_province.py`, `sim/tests/test_no_hardcoded.py`, la carte figée,
le visualiseur, et ce brief.

## Conditions de succès

Les comparaisons « avant / après » se font contre `master` rejoué au démarrage du
lot, jamais contre un nombre recopié d'ici — **sauf là où `master` est justement
ce qu'on répare**, et SC5 dit pourquoi.

**SC1 à SC4 et SC6 à SC9 ont été prouvées par l'exécution fusionnée** (#184) :
elles sont le compte rendu de ce qui tourne et doivent rester vertes. La
réparation porte sur **SC5, réécrite ci-dessous parce qu'elle ne savait pas
rougir**, et sur **SC10, nouvelle**.

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

Un contrôle parcourt l'arbre syntaxique des modules de `sim/` hors tests. Le
nombre de modules parcourus est dérivé du répertoire, et un parcours qui n'en
trouve aucun échoue au lieu de conclure.

**Ce qui a raté la première fois** : la troisième référence était `master`
rejoué. Or `master` porte la duplication qu'elle devait interdire, et deux égale
deux. Une référence recopiée sur l'état du jour ne peut pas voir un défaut que
cet état contient : elle mesure la conformité à ce qui est, pas à ce qui doit
être. La référence se prend donc **ailleurs que dans le nombre observé**, sur un
motif du même arbre dont le brief affirme qu'il est correct — celui que la § 2
prend déjà pour modèle : une constante, une fonction qui la lit.

Le contrôle ne se compare à aucun nombre écrit ici.

- **La part minière se calcule à un seul endroit.** Le nombre de fonctions qui
  lisent `PART_MINIERE_PAR_GISEMENT` ou `PART_MINIERE_MAXIMALE` est **égal** au
  nombre de fonctions qui lisent la table des facteurs de relief, compté par le
  même parcours sur le même arbre.
- **La formule agricole de base n'existe qu'à un seul endroit.** Le nombre de
  fonctions qui lisent la constante de rendement agricole au km² est **égal** au
  même nombre de lectrices de la table de relief. C'est la référence qui
  manquait, et elle ne dépend d'aucun comptage observé.
- **Les facteurs de richesse ne sont pas dupliqués.** Cette référence reste celle
  que l'exécution fusionnée a implémentée — le même parcours rejoué sur `master`.
  La corriger de la même façon exigerait de retoucher une assertion existante et
  verte, ce qu'un lot ne fait pas. C'est une **dette notée ici**, pas un oubli.

Si le parcours ne trouve aucune lectrice de la table de relief, la référence est
vide et le contrôle échoue : un parcours qui ne sait pas voir le motif de
référence ne prouve rien sur ses copies.

**Rouge prouvé d'abord, et c'est le cœur de la réparation** : sur le `master`
d'aujourd'hui, la deuxième égalité est **fausse** — la constante de rendement
agricole a deux lectrices là où la table de relief n'en a qu'une. Le contrôle
doit rougir **avant** la déduplication, et sa sortie en échec est citée.

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
- le contrôle parcourt l'arbre syntaxique de `sim/engine.py` et dresse la liste
  des constantes que le moteur lit par leur nom : cette liste est le
  **dénominateur**, dérivée du fichier et jamais écrite ici. Une liste vide fait
  échouer le contrôle — un parcours qui ne trouve aucune lecture ne prouve rien
  sur celles qu'il cherche. Ni `PART_MINIERE_PAR_GISEMENT` ni
  `PART_MINIERE_MAXIMALE` n'y figurent, et `part_miniere_de` est **présente**
  parmi les fonctions du même module que le moteur appelle : la sonde voit donc
  quelque chose là où il y a quelque chose à voir ;
- deux exécutions de `py -m sim --ticks 365 --seed 0 --json` sont strictement
  identiques entre elles. La différence avec `master` a mesuré l'exécution
  initiale, qui changeait le monde ; pour la réparation c'est SC10 qui
  s'applique, et elle exige exactement l'inverse ;
- aucune instruction `global` dans `sim/engine.py` ;
- le nombre de tests collectés est strictement supérieur à celui de `master`.

### SC10 — La réparation ne change pas le monde

La sortie de `py -m sim --ticks 365 --seed 0 --json` est **identique** à celle
rejouée sur `master`. C'est ce qui sépare une réparation de forme d'un
changement de règle : la déduplication ne déplace rien.

La comparaison se fait contre `master` rejoué, jamais contre un nombre recopié
d'ici. Un écart, fût-il d'un kilogramme, fait échouer la réparation au lieu
d'être expliqué après coup.

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
  d'un test existant, quel qu'en soit le motif ;
- rouvrir la règle du monde, les constantes ou la part minière : la réparation
  est de forme, pas de fond ;
- corriger la troisième référence de SC5, celle des facteurs de richesse : elle
  exigerait de retoucher une assertion existante et verte, donc un geste séparé.
