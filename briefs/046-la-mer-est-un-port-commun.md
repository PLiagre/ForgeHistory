# Brief 046 — La mer est un port commun

## But

Faire entrer dans le commerce les arêtes maritimes que la carte porte déjà et
que le moteur ne lit pas du tout : une cellule côtière en surplus **expédie**
vers un bassin maritime commun, une cellule côtière en manque y **puise**.

Aujourd'hui `sim/` ne connaît que les arêtes dont les deux bouts sont des
cellules du monde. Toutes les autres — celles qui joignent une cellule à la mer —
sont écartées en silence, avec la longueur de façade qu'elles portent. Plus d'une
cellule sur trois n'a **aucun** voisin terrestre : elle ne peut ni recevoir ni
donner un kilogramme, et rien ne le signale.

Ce lot **reste de la couche 1** : il n'ajoute ni ville, ni bourg, ni métier, ni
route. Il finit le transport. Il ne prétend pas rendre une ville possible ; ce
que le monde en fait se mesure **après** la fusion.

**Indépendant des lots 044 et 047.** Aucun ordre à respecter entre eux.

Ce qui rend ce lot caduc : si `sim/` lit déjà les arêtes dont un seul bout est
une cellule du monde, il n'y a rien à faire ici.

```bash
grep -rn "kind\|stocks_mer" sim/
py -c "
import collections, json
doc = json.load(open('data/world-1400.json', encoding='utf-8'))
ids = {c['cell_id'] for c in doc['cellules']}
adj = doc['adjacence']
print('kinds', collections.Counter(e['kind'] for e in adj))
deg = collections.Counter()
for e in adj:
    if e['a'] in ids and e['b'] in ids:
        deg[e['a']] += 1; deg[e['b']] += 1
print('cellules', len(ids))
print('sans_voisin_terrestre', sum(1 for c in ids if deg[c] == 0))
print('noeuds_hors_monde', {x for e in adj for x in (e['a'], e['b'])} - ids)
"
py -m sim --ticks 365 --seed 0 --json
```

## Règle du monde

Découle de [`sim/MODELE.md`](../sim/MODELE.md) : § « La mer : la façade que le
moteur ne lit pas » (les deux sortes d'arêtes, la façade, le nœud unique, la
limite de la donnée), § « Le commerce entre cellules » (la capacité dérivée d'une
longueur, le goulot de relief, le plafond partagé entre marchandises,
l'allocation déterministe et l'écrêtage côté receveur, que ce lot étend sans les
changer), et § « Le mur qui sépare la couche 1 de la couche 2 ». Si l'une de ces
sections a changé depuis, la relire avant de lancer.

**Fidélité mixte, et la distinction compte.** Le trait de côte et la longueur de
façade sont de **niveau 1** : ils sont dans la carte figée, qui le déclare
elle-même. Le **débit maritime par kilomètre de façade** est de **niveau 2** :
plausible, jamais sourcé. L'absence de distance en mer est une limite déclarée de
la carte, pas une décision de ce lot.

### 1. Ce qu'est une arête maritime — dérivé, jamais écrit

```
arête maritime := arête d'adjacence dont EXACTEMENT UN des deux bouts
                  est une cellule du monde
nœud mer       := le bout qui n'est pas une cellule du monde
```

La définition est **structurelle**, pas textuelle : aucun nom de `kind` n'est
recopié dans le moteur. Une carte qui renommerait son vocabulaire continuerait
d'être lue.

Le champ `kind` sert de **contre-épreuve**, pas de définition : le moteur dérive
l'ensemble des valeurs de `kind` portées par les arêtes qu'il a identifiées comme
maritimes, et **refuse** s'il en trouve plus d'une. Deux vocabulaires sur le même
type d'arête voudraient dire que la définition structurelle et l'étiquette de la
carte ne parlent plus de la même chose.

Un monde dont **aucune** arête n'est maritime n'est pas une erreur : c'est un
monde sans mer, et le maillon n'y joue pas.

### 2. La mer est un bassin, pas un réseau

Toutes les arêtes maritimes de la carte touchent **un seul et même nœud**. Il
n'existe donc, dans la donnée, aucune liaison de port à port et aucune distance
en mer. Le modèle prend la carte au mot : on expédie **vers la mer**, on puise
**depuis la mer**.

Conséquence assumée, à ne pas prendre pour une propriété du monde : dans un
bassin, deux ports sont à égale distance l'un de l'autre. C'est une limite de la
carte ; le jour où elle portera une adjacence port-à-port, elle tombera sans que
le reste bouge.

Si l'adjacence porte **plus d'un** nœud hors du monde, la carte ne décrit pas un
bassin unique : le moteur **refuse** en nommant les identifiants trouvés, plutôt
que d'en choisir un.

### 3. La capacité d'un quai

```
capacite_quai(cellule) = somme, sur les arêtes maritimes de la cellule, de
    debit_maritime_kg_par_km()
    × (shared_length_m / metres_par_km())
    × facteur_transport(relief de la cellule)
```

La somme est **dérivée** : le moteur ne suppose pas qu'une cellule n'a qu'une
seule façade, il additionne celles que la carte lui donne.

| constante | valeur | ce que c'est |
|---|---:|---|
| `DEBIT_KG_PAR_KM_DE_COTE_PAR_TICK` | 2000.0 × `TICK_DURATION_DAYS` | kilogrammes traversant un kilomètre de façade maritime par tick — **niveau 2**. Soit dix fois le débit terrestre au kilomètre : un navire porte sans commune mesure ce que porte un convoi de bêtes de somme. |

**Ce facteur dix est décidé ici, sur cet argument seul.** Aucune condition de
succès n'en dépend : toutes mesurent un rapport, une direction ou une
conservation, jamais un seuil. Si l'exécutant se surprend à vouloir l'ajuster
pour faire passer un contrôle, c'est le contrôle qui est faux, et le brief est à
réécrire par son auteur.

**Composition avec l'existant**, qu'il ne faut ni écraser ni dupliquer : le débit
maritime ne **remplace** aucun débit terrestre — il s'applique à d'autres arêtes,
et les deux coexistent sans se connaître. Le facteur de transport du relief
**multiplie** la capacité de quai, comme il multiplie déjà la capacité terrestre.
Il n'y a **pas** de `min` ici : une arête maritime n'a qu'une seule rive
terrestre, donc un seul relief à consulter. Une côte de haute montagne débarque
mal. La table de facteurs de transport est celle qui existe, lue par la même
fonction — **aucun second jeu**.

### 4. Le refus de deviner sur une longueur de façade

- **non numérique** (chaîne, booléen, `NaN`) : erreur explicite nommant la
  cellule et le nœud mer ;
- **absente** : la même erreur. C'est volontairement **différent** de la règle
  terrestre, où une longueur absente active le repli
  `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`. Ce repli est une capacité de convoi
  terrestre : elle ne veut rien dire pour un quai, et l'emprunter serait deviner.
  Il n'existe pas d'équivalent maritime, donc l'absence se déclare ;
- **nulle** : valide, et rend zéro. Une cellule qui ne touche la mer qu'en un
  point n'a pas de port. Ce zéro est une **mesure réelle** ; la sentinelle « non
  calculé » est `-1`, jamais `0`.

### 5. Où vit le bassin, et pourquoi il ne casse rien

Le bassin est un panier de marchandises porté par le monde, `World.stocks_mer`,
initialisé vide. `sim/world.py` déclare ce panier **et les deux accès nommés**
qui le lisent et l'écrivent ; aucun autre module ne l'indexe directement — la même
discipline que le panier d'une cellule.

Le moteur y accède par `getattr(world, "stocks_mer", None)`, **exactement le
motif déjà employé pour `getattr(world, "carte", None)`** : un monde qui ne porte
pas de bassin ne joue pas le maillon maritime et ne lève aucune erreur. C'est ce
qui laisse intacts les mondes d'épreuve des tests existants.

Une marchandise **absente** du bassin n'est pas une marchandise à zéro : elle
signifie que la mer n'a jamais porté cette denrée. Une marchandise **à zéro** est
une mesure réelle : la mer en a porté, il n'en reste plus.

### 6. Les trois flux, sur un seul instantané

Le maillon commerce prend déjà un **instantané immuable** des stocks avant tout
transfert, et applique les transferts en une passe finale. Ce lot ne change pas ce
principe : il ajoute deux flux au même calcul.

Une fois par tick, avant toute marchandise : `capacite_quai_restante[cell_id]`,
calculée une fois et **partagée entre toutes les marchandises** — il n'y a qu'un
quai, comme il n'y a qu'un convoi par arête — et `bassin_au_debut`, copie du
panier de la mer au début du tick.

Puis, pour chaque marchandise, sur le même instantané qu'aujourd'hui :

1. **Débarquement** — les cellules côtières en besoin puisent dans
   `bassin_au_debut`, dans la limite de leur besoin et de leur capacité de quai
   restante. Si le bassin ne suffit pas, chaque port reçoit une part
   proportionnelle à son besoin, les ports parcourus par `cell_id` croissant.
2. **Commerce terrestre** — inchangé.
3. **Expédition** — les cellules côtières dont il reste du surplus **après**
   l'allocation terrestre expédient vers le bassin, dans la limite de leur
   capacité de quai restante.

Puis l'écrêtage côté receveur, déjà présent, porte sur le **total entrant** d'une
cellule, terrestre et débarqué confondus. Puis tout s'applique en une passe.

**Trois invariants que cette forme achète :**

- **un kilogramme ne traverse qu'une arête par tick.** Le débarquement ne lit que
  `bassin_au_debut` : ce qui est embarqué au tick `t` ne peut pas être débarqué
  avant `t+1`. C'est le délai physique de la traversée, et il n'est pas simulé par
  un compteur — il tombe de la forme du calcul ;
- **le surplus d'une source est engagé une seule fois**, terre et mer confondues.
  Sans cela, une cellule côtière expédierait le même kilogramme à sa voisine et à
  la mer, et de la masse serait créée ;
- **la masse se conserve, bassin compris.** La somme des stocks des cellules
  **plus** le bassin est identique avant et après le maillon.

Les kilogrammes débarqués et embarqués **comptent** dans `kg_transportes` : c'est
de la masse transportée, et un compteur qui n'en verrait qu'une partie serait une
mesure partielle.

### Où ça se raccorde

La longueur de façade vient **uniquement** de l'arête de `world.adjacency`, telle
que la carte la porte : le moteur ne la recalcule pas depuis la géométrie, ne la
duplique pas, et n'écrit jamais dans la carte. Le relief vient de `world.carte`,
par la **même** fonction de table qu'aujourd'hui. La capacité de quai se calcule
à **un seul endroit**, et il reste **un seul maillon commerce**.

`world.to_dict()` n'est **pas** modifié : le bassin n'entre ni dans la
sérialisation canonique, ni dans le snapshot, ni dans l'empreinte de
déterminisme.

## Périmètre

En écriture : `sim/engine.py`, `sim/constants.py`, `sim/world.py` **uniquement**
pour déclarer `stocks_mer`, l'initialiser vide et fournir ses deux accès nommés
(pas de `to_dict()`), et `sim/tests/test_commerce.py` pour y **ajouter** des cas.
Aucun test déjà vert n'est modifié.

**`sim/tests/test_write_coverage.py` n'est pas modifiable.** Élargir
`_MondeEpreuve` pour lui donner une mer n'est pas la solution retenue : c'est la
lecture par fonction (§ 3) qui protège
`test_chaque_constante_du_moteur_change_le_monde`, et SC10 le mesure.

Tout autre chemin est interdit, nommément : `sim/MODELE.md`, `sim/model.py`,
`sim/aggregation.py`, `sim/snapshot_export.py`, `sim/__main__.py`, les autres
fichiers de `sim/tests/`, la carte figée `data/world-1400.json`, le visualiseur,
les briefs 044 et 047, et ce brief.

## Conditions de succès

Les comparaisons « avant / après » se font contre `master` rejoué au démarrage du
lot, jamais contre un nombre recopié d'ici.

### SC1 — Le moteur identifie les arêtes maritimes, et il les dérive

Un contrôle joue le moteur sur la carte figée et compare le nombre d'arêtes que
le moteur traite comme maritimes au nombre obtenu **indépendamment**, en relisant
`data/world-1400.json` dans le contrôle. Les deux doivent être égaux. Le
dénominateur est le nombre total d'arêtes, lu du fichier. **Un échantillon vide
échoue** : zéro arête maritime identifiée fait rougir le contrôle.

Un second contrôle vérifie qu'aucun nom de `kind` n'apparaît comme littéral de
comparaison dans `sim/engine.py`.

**Rouge prouvé d'abord** : sur `master`, le compte des arêtes maritimes traitées
vaut zéro.

### SC2 — Un port en surplus expédie, un port en manque débarque

Sur un micro-monde déterministe portant une arête maritime pour chacune de deux
cellules et **aucune arête terrestre entre elles** : la cellule en surplus voit
son stock baisser et le bassin monter d'autant ; au tick suivant, la cellule en
manque voit son stock monter et le bassin baisser d'autant.

**Rouge prouvé d'abord** : sur `master`, aucun des deux stocks ne bouge et le
bassin n'existe pas.

### SC3 — Le grain embarqué à `t` n'est pas débarqué à `t`

Même micro-monde, un expéditeur et un receveur. Au tick de l'expédition, le
receveur ne reçoit **rien** ; au tick suivant, il reçoit. Le nombre de ticks joués
est dérivé du contrôle, jamais écrit comme attendu.

Ce zéro du premier tick est une **mesure réelle**. La sentinelle « non calculé »
est `-1`, jamais `0`.

### SC4 — La masse se conserve, bassin compris

Sur un micro-monde, la somme des stocks de toutes les cellules **plus** le
contenu du bassin est identique avant et après le maillon commerce, à chaque tick
joué. L'écart mesuré vaut **0**, et ce zéro est une mesure.

`test_conservation_masse_transport` reste vert **sans être modifié** : son monde
n'a pas de mer, et la somme sur les seules cellules y reste l'invariant complet.

### SC5 — Les cellules hermétiques cessent de l'être

Dériver de la carte l'ensemble des cellules qui n'ont **aucune** arête terrestre
vers une autre cellule du monde mais portent au moins une arête maritime. Pour cet
ensemble : sur `master`, le nombre de partenaires de commerce de chacune vaut
**zéro** ; après changement, chacune a une capacité de quai strictement positive,
sauf celles dont la façade est de longueur nulle — comptées séparément, et leur
zéro est une mesure.

Le dénominateur est le nombre de cellules réellement examinées. **Un échantillon
vide échoue.**

### SC6 — La façade commande le débit

Sur un micro-monde où plusieurs cellules identiques puisent dans un bassin
suffisamment fourni par des façades de longueurs différentes, les quantités
débarquées sont dans le **même rapport** que les longueurs, tant qu'aucune n'est
bornée par le besoin ou par le bassin. Les longueurs sont **dérivées de la carte
figée** : la plus courte, la médiane et la plus longue réellement présentes.

**Rouge prouvé d'abord** : sur `master`, les trois débarquent la même chose, à
savoir rien.

### SC7 — Le goulot de relief se compose, sans second jeu de facteurs

À longueur de façade égale, deux cellules de classes de relief différentes
n'expédient pas la même quantité, et l'ordre suit strictement la table de
transport existante. Les classes sont dérivées de la carte ; si l'une manque, le
contrôle échoue au lieu de la sauter.

Un contrôle parcourt l'arbre syntaxique des modules de `sim/` hors tests et
échoue si un second jeu de facteurs de transport apparaît.

### SC8 — Le refus de deviner

Chacun de ces cas est **essayé** — une commande, un message d'erreur — et pas
seulement affirmé :

- une longueur de façade **non numérique** sur une arête maritime provoque une
  erreur explicite nommant la cellule et le nœud mer ;
- une longueur **absente** provoque la même erreur. Aucun repli sur la capacité
  plate terrestre n'est admis ;
- une adjacence portant **plus d'un** nœud hors du monde provoque une erreur
  explicite nommant les identifiants trouvés ;
- des arêtes maritimes portant **plusieurs valeurs de `kind`** provoquent une
  erreur explicite nommant les valeurs.

Et le cas qui n'est **pas** une erreur, à prouver aussi : un monde qui ne porte
pas `stocks_mer` joue le tick sans maillon maritime, sans exception et sans
changer de résultat par rapport à `master`.

Le compteur des refus est le nombre de mutations réellement exécutées ; un
échantillon vide échoue.

### SC9 — Le monde réel transporte davantage

Le champ `kg_transportes` de `py -m sim --ticks 365 --seed 0 --json` est
**strictement supérieur** à celui rejoué sur `master`.

Aucun seuil n'est exigé et aucun nombre de ce brief n'est recopié : le maillon
maritime **ajoute** des arêtes et n'en retire aucune, donc la direction est la
seule chose que ce contrôle a le droit d'affirmer.

### SC10 — Les invariants existants restent intacts, et la suite reste verte

```bash
py -m pytest sim/tests/ viewer/tests/ -q
```

- vert, et la liste des tests en échec est **vide**, comparée à celle de `master`
  plutôt que supposée ;
- `test_chaque_constante_du_moteur_change_le_monde` reste vert **sans que le test
  ni `_MondeEpreuve` soient touchés**. C'est le contrôle que ce lot risque le plus
  de casser, et il ne doit pas l'être en le modifiant ;
- `DEBIT_KG_PAR_KM_DE_COTE_PAR_TICK` **n'apparaît pas** comme attribut lu dans
  `sim/engine.py` — le moteur passe par `debit_maritime_kg_par_km()`. Le contrôle
  parcourt l'arbre syntaxique de `sim/engine.py` et dresse la liste des constantes
  que le moteur lit par leur nom : cette liste est le **dénominateur**, dérivée du
  fichier et jamais écrite ici. Une liste vide fait échouer le contrôle — un
  parcours qui ne trouve aucune lecture ne prouve rien sur celle qu'il cherche. La
  constante maritime est **absente** de cette liste, et `debit_maritime_kg_par_km`
  est **présente** parmi les fonctions du même module que le moteur appelle : la
  sonde voit donc quelque chose là où il y a quelque chose à voir. C'est
  précisément ce qui empêche cette constante d'être inerte sur un monde d'épreuve
  sans mer ;
- `test_conservation_masse_transport`, `test_invariance_ordre_aretes`,
  `test_recepteur_pas_sur_livre` et `test_kg_transportes_egal_deltas_positifs`
  restent verts **sans modification** ;
- les propriétés de régime de `sim/tests/test_survie.py` restent vertes **sans
  modification de ce fichier** : un commerce plus large déplace mieux la
  nourriture, il n'en crée pas ;
- `test_le_moteur_ne_lie_aucune_constante_par_valeur`,
  `test_aucune_constante_terminale` et `test_no_hardcoded_numeric_literals`
  restent verts ;
- deux exécutions de `py -m sim --ticks 365 --seed 0 --json` sont strictement
  identiques entre elles ;
- aucune instruction `global` dans `sim/engine.py` ;
- le nombre de maillons commerce que le parcours trouve dans `sim/` est **égal**
  à celui que le même parcours compte sur `master` : ce lot **étend** le maillon
  existant, il n'en ajoute pas un second. La référence est rejouée, jamais écrite
  ici, et un parcours qui n'en trouve aucun fait échouer le contrôle au lieu de
  passer — c'est la garde que porte déjà
  `test_maillon_commerce_sans_nom_nourriture` ;
- le nombre de tests collectés est au moins celui de `master`.

## Hors périmètre

- `sim/MODELE.md` — la mise à jour après fusion est une dette de l'architecte du
  modèle, pas de l'exécutant ;
- **la migration par la mer** : les partants continuent de ne suivre que les
  arêtes terrestres. Une cellule sans voisin terrestre reste inquittable, et c'est
  un autre lot ;
- les routes, les ponts, les ports comme ouvrages, les fleuves ;
- la distance en mer, les routes maritimes, le port-à-port ;
- la ville, le bourg, le métier — ce lot reste de la couche 1 ;
- le schéma du snapshot, sa version, le visualiseur ;
- la calibration d'un test existant après observation.
