# Brief 047 — Le bourg est une agrégation dérivée

## But

Donner au monde une **vue dérivée** qui répond à une seule question : *combien
d'habitants de cette cellule ne tirent pas leur nourriture de ses champs ?*
Ceux-là sont le **bourg** ; les autres sont la campagne qui les nourrit.

C'est la première entité de la couche 2, et elle est construite exactement comme
la Province : elle se **recalcule**, elle ne s'estampille pas. Aucun champ n'est
ajouté à `Cell`, aucune seconde clé spatiale n'apparaît, et **le tick ne la
consulte pas**.

Ce lot ne crée **aucun mécanisme** : il ne change aucun nombre du monde, ne
déplace aucun kilogramme, ne fait naître ni mourir personne. C'est une lecture.
Si après ce lot `py -m sim` rend un résultat différent, le lot est faux.

**Ce lot suppose le lot 044 fusionné, et il est bloqué sans lui.** Le bourg se
compte à partir de la part non agricole de la population ; le 044 est le premier —
et aujourd'hui le seul — mécanisme qui la rend non nulle. Avant lui, tout le monde
cultive, la part vaut zéro partout, et **l'échantillon du bourg est vide**. Un
échantillon vide échoue, il ne passe pas en silence : SC4 est le contrôle
mécanique qui le dit. Ce lot n'est pas « à adapter », il ne se lance pas.

**Indépendant du lot 046.** Le bourg est nourri par la campagne de sa **propre**
cellule ; il n'attend aucun transport.

Ce qui rend ce lot caduc : si `sim/` porte déjà une vue qui distingue les
habitants non agricoles d'une cellule, il n'y a rien à faire ici. Ce qui le rend
bloqué : si aucune fonction de `sim/` ne calcule une part non agricole.

```bash
grep -rn "bourg\|Bourg" sim/
grep -rn "part_miniere" sim/
py -m sim --ticks 365 --seed 0 --json
```

## Règle du monde

Découle de [`sim/MODELE.md`](../sim/MODELE.md) : § « Ce qu'est une ville, à
l'échelle d'une cellule » — la décision de modèle dont ce lot découle : pourquoi
le bourg est une concentration *dans* la cellule et non une cellule entière, d'où
vient la donnée, ce qui se refuse plutôt que se devine ; § « La province dérivée
et ses centres », sous-section « Ce que l'agrégation ne fait pas — et le motif que
toute vue recopie » — le motif exact que cette vue reprend sans en changer une
ligne ; et § « L'extraction minière ». Si l'une de ces sections a changé depuis,
la relire avant de lancer.

**Fidélité niveau 2.** La part de la population qu'un bourg représente est un
ordre de grandeur plausible, générée, jamais sourcée.

Aucune donnée de carte nouvelle n'est lue. Ce lot n'introduit **aucune**
constante et **aucun** paramètre — il n'y a donc rien à calibrer, et c'est voulu :
une vue qui aurait son propre réglage serait une seconde vérité.

### Le mécanisme, en une ligne

```
habitants_du_bourg(cellule)   = int(population × part_non_agricole(cellule))
habitants_des_champs(cellule) = population − habitants_du_bourg(cellule)
```

La troncature est délibérée et **personne ne se perd** : la campagne est définie
comme *le reste*, donc la somme des deux vaut exactement la population, pour
toute cellule et sans arrondi à corriger.

**Il n'y a pas de report de fraction ici**, contrairement à la mortalité, à la
natalité et à la migration. Ces trois-là accumulent un état d'un tick sur
l'autre ; une vue ne s'accumule pas, elle se recalcule. Un reste conservé serait
un état stocké — exactement ce que ce lot est écrit pour ne pas faire.

### La part non agricole se lit, elle ne se redérive pas

`part_non_agricole` **est** la fonction unique que le lot 044 installe dans
`sim/constants.py` — celle qui calcule la part de la population qu'un gisement
occupe, plafonnée. Ce lot l'**appelle**. Il n'en écrit pas une seconde version,
n'en recopie pas la formule, et ne duplique pas les facteurs de richesse.

Si le 044 a livré ce calcul sous un autre nom que celui que son brief annonce,
c'est **ce nom-là** qu'il faut lire : il n'y en a qu'un, et SC5 vérifie qu'il n'y
en a toujours qu'un après ce lot.

**Ce que le nom « bourg » promet, et ce qu'il ne promet pas.** Aujourd'hui la
seule façon de ne pas cultiver est de descendre à la mine : le bourg d'une cellule
est donc exactement ses mineurs. Le nom est délibérément plus large que le
mécanisme, pour que le jour où un second métier existera la vue le compte sans
être réécrite. **Cela n'autorise personne à inventer ce second métier ici.**

### Ce qui se refuse plutôt que se deviner

- une cellule dont la part non agricole vaut zéro a un bourg de **zéro
  habitant**. C'est une **mesure réelle** — la vue a regardé et n'a trouvé
  personne — et non une absence. La sentinelle « non calculé » est `-1`, jamais
  `0` ;
- si **aucune** cellule du monde n'a de part non agricole, la vue ne rend pas
  « zéro bourg » en silence : c'est un échantillon vide, et il **échoue** (SC4) ;
- aucun seuil ne « fait » un bourg. Il n'y a pas de nombre d'habitants au-dessus
  duquel une cellule serait déclarée urbaine : le bourg n'est pas déclaré, il est
  **compté**.

### Où ça se raccorde

La population vient de `world.cells`, la part non agricole de l'unique fonction
du lot 044. Rien d'autre n'est lu, et **rien n'est écrit** : ni sur les cellules,
ni sur disque, ni dans la carte.

La vue vit dans `sim/aggregation.py`, **hors de `sim.model`**, pour la raison
exacte qui vaut déjà pour `Regroupement` : `sim.model` contient les entités
*persistées* que le moteur fait évoluer, et y déclarer le bourg inviterait à le
traiter comme un état stockable.

Elle recopie le motif de la Province sans en changer une ligne : **pure** (elle
ne mute aucun objet reçu, deux appels sur les mêmes entrées rendent le même
résultat), enregistrement **immuable** (`frozen`) héritant de la garde
`_NoBadSpatialField`, indexée par **`cell_id` et rien d'autre**, ordre de sortie
stable par `cell_id` croissant, et **le tick ne la consulte pas**.

`sim/engine.py`, `sim/model.py`, `sim/constants.py` et `sim/world.py` ne sont pas
touchés. C'est ce périmètre étroit qui **prouve** que la vue ne décide rien : si
elle avait besoin de changer le moteur, elle ne serait pas une vue.

## Périmètre

En écriture : `sim/aggregation.py`, et `sim/tests/test_province.py` pour y
**ajouter** des cas — c'est le fichier qui porte déjà l'invariant « la vue est
dérivée, jamais stockée ». Aucun test déjà vert n'est modifié.

Tout autre chemin est interdit, nommément : `sim/MODELE.md`, `sim/engine.py`,
`sim/model.py`, `sim/constants.py`, `sim/world.py`, `sim/snapshot_export.py`,
`sim/__main__.py`, les autres fichiers de `sim/tests/` — dont
`test_write_coverage.py` et `_MondeEpreuve` —, la carte figée, le visualiseur, les
briefs 044 et 046, et ce brief.

## Conditions de succès

Les comparaisons « avant / après » se font contre `master` rejoué au démarrage du
lot, jamais contre un nombre recopié d'ici.

### SC1 — La vue se recalcule, et elle ne touche à rien

Deux appels successifs sur le même monde rendent un résultat **égal**, et le
monde en sort **inchangé** : aucun attribut ajouté à une cellule, aucune valeur
modifiée. L'empreinte de `world.to_dict()` avant et après l'appel est identique.

Le nombre de cellules comparées est dérivé du monde chargé ; un échantillon vide
échoue.

**Rouge prouvé d'abord** : sur `master`, la vue n'existe pas et l'appel lève une
erreur d'attribut.

### SC2 — Aucune seconde clé spatiale

Un contrôle parcourt l'arbre syntaxique des modules de `sim/` hors tests et
échoue si une entité déclare un champ dont le nom normalisé commence par `bourg`,
`ville` ou `city`. Le dénominateur est le nombre de classes de données réellement
découvertes, jamais une liste écrite à la main.

**Rouge prouvé** sur une entité d'épreuve délibérée portant un tel champ,
exactement comme le contrôle existant le fait pour le préfixe `province` : le
contrôle doit rougir sur elle, sinon il ne protège rien.

La garde d'exécution de `sim/model.py` n'est **pas** étendue : elle continue de ne
connaître que `province`, et ce lot ne modifie pas ce fichier. C'est le contrôle
ci-dessus qui porte les nouveaux préfixes.

### SC3 — Le tick ne consulte pas la vue

Un contrôle parcourt l'arbre syntaxique de `sim/engine.py` et échoue si le module
importe `sim.aggregation` ou référence le nom de la vue. Le dénominateur est le
nombre de modules de `sim/` hors tests réellement parcourus.

C'est la même propriété que porte déjà la Province, et elle est ici la définition
même de ce lot : une vue lit, elle ne décide jamais.

### SC4 — L'échantillon n'est jamais vide, et c'est le signal de dépendance

Sur le monde réel, le nombre de cellules dont le bourg compte au moins un
habitant est **strictement positif**.

Un zéro ici **fait échouer le lot**. Il ne signifie pas « ce monde n'a pas de
ville » : il signifie que rien ne produit de part non agricole, c'est-à-dire que
le lot 044 n'est pas fusionné. C'est la déclaration mécanique de la dépendance, et
il est **interdit** de la contourner en fabriquant une part non agricole ici.

Le dénominateur est le nombre de cellules du monde chargé. Rapporter aussi le
nombre de cellules que la carte déclare porteuses d'au moins un gisement, dérivé
du fichier, pour que les deux comptes se confrontent.

### SC5 — Le bourg suit la part non agricole, et il n'y a qu'une définition

- **l'ordre.** À population égale, le bourg d'une cellule dont le gisement est de
  richesse majeure compte strictement plus d'habitants que celui d'une cellule de
  richesse notable, lui-même strictement plus qu'une cellule de richesse mineure.
  Les trois classes sont **dérivées de la carte** ; si l'une manque, le contrôle
  échoue au lieu de la sauter ;
- **l'unicité.** Un contrôle parcourt l'arbre syntaxique des modules de `sim/`
  hors tests et échoue si plus d'une fonction calcule la part non agricole, ou si
  un second jeu de facteurs de richesse apparaît.

Ce second point **se compose** avec SC4 du lot 044, qui exige déjà une définition
unique : ce lot ne revendique pas cette grandeur, il la lit, et le contrôle
vérifie qu'il ne l'a pas dupliquée en la lisant.

### SC6 — La somme est exacte, pour chaque cellule

Pour **toute** cellule du monde chargé :
`habitants_du_bourg + habitants_des_champs == population`, à l'entier près et
sans tolérance. Le compteur d'écarts vaut **0**, et ce zéro est une mesure réelle :
chaque cellule a été sommée.

Le dénominateur est le nombre de cellules réellement sommées ; un échantillon vide
échoue.

### SC7 — Ce lot ne change aucun nombre du monde

`py -m sim --ticks 365 --seed 0 --json` rend, après changement, une sortie
**identique octet pour octet** à celle rejouée sur `master`. Archiver la sortie de
base **avant** l'édition, la relire, et comparer les empreintes SHA-256. L'écart
vaut **0** — une mesure réelle, obtenue en comparant deux fichiers réellement
produits, jamais une affirmation.

C'est le critère qui distingue une vue d'un mécanisme. S'il rougit, ce lot a
touché au monde, et il est faux quelles que soient ses autres mesures.

### SC8 — Les invariants existants restent intacts, et la suite reste verte

```bash
py -m pytest sim/tests/ viewer/tests/ -q
```

- vert, et la liste des tests en échec est **vide**, comparée à celle de `master`
  plutôt que supposée ;
- tous les contrôles de `sim/tests/test_province.py` déjà présents restent verts
  **sans modification**, y compris `test_province_couverture_totale_monde_reel`,
  `test_province_aucun_champ_province_sur_entites` et
  `test_province_garde_prefixe_variantes_rouges` ;
- `sim/tests/test_write_coverage.py` reste vert sans modification : ce lot
  n'ajoute aucun champ à une entité de `sim.model`, donc son dénominateur ne bouge
  pas ;
- `test_no_hardcoded_numeric_literals` reste vert : aucun littéral numérique hors
  0, 1 et −1 ;
- `test_aucune_constante_terminale` reste vert : ce lot n'ajoute aucune constante ;
- deux exécutions de `py -m sim --ticks 365 --seed 0 --json` sont strictement
  identiques entre elles ;
- le nombre de tests collectés est au moins celui de `master`.

## Hors périmètre

- `sim/MODELE.md` — la mise à jour après fusion est une dette de l'architecte du
  modèle, pas de l'exécutant ;
- **tout mécanisme** : ce lot ne change aucun nombre du monde, et SC7 le mesure ;
- tout métier autre que celui du lot 044, et toute nouvelle source de population
  non agricole ;
- les quartiers, les bâtiments, les familles, les personnes ;
- le salaire, le prix, le marché, la propriété, la classe sociale, la fiscalité ;
- l'attraction urbaine, une migration qui viserait le bourg, une natalité
  différente au bourg et aux champs ;
- la nourriture du bourg comme flux distinct : la campagne de la cellule le
  nourrit sans transport, c'est une limite déclarée du modèle et non un mécanisme
  à écrire ici ;
- les routes, la mer et le lot 046 ;
- le schéma du snapshot, sa version, le visualiseur ;
- la calibration d'un test existant après observation.
