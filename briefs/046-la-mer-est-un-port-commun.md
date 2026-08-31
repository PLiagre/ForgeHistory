# Brief 046 — La mer est un port commun

**Authored**: 2026-08-30T13:20:00Z
**Author**: Claude
**Risque**: R1 — mécanique produit bornée dans `sim/`, sans migration de données ni changement de la clé spatiale.

## But unique

Faire entrer dans le commerce les arêtes maritimes que la carte porte déjà et
que le moteur ne lit pas du tout : une cellule côtière en surplus **expédie**
vers un bassin maritime commun, une cellule côtière en manque y **puise**.

Aujourd'hui `sim/` ne connaît que les arêtes dont les deux bouts sont des
cellules du monde. Toutes les autres — celles qui joignent une cellule à la mer
— sont écartées en silence, avec la longueur de façade qu'elles portent. Plus
d'une cellule sur trois n'a **aucun** voisin terrestre : elle ne peut ni
recevoir un kilogramme, ni en donner, et rien ne le signale.

Ce lot **reste de la couche 1** : il n'ajoute ni ville, ni bourg, ni métier, ni
entreprise, ni route. Il finit le transport de la couche 1. Il ne prétend pas
rendre une ville possible ; ce que le monde en fait se mesure **après** la
fusion, et c'est la charge de l'architecte du modèle, pas de l'exécutant.

Ce lot ne change ni la production, ni la consommation, ni la mortalité, ni la
natalité, ni la migration, ni les règles d'allocation du commerce terrestre, et
ne crée aucune marchandise.

## Dépendance

**Ce lot suppose le lot 043 fusionné.** La capacité dérivée d'une longueur de
frontière est la forme que ce lot réemploie, avec un débit distinct. Si 043
n'est pas fusionné, ce lot est **bloqué**, pas à adapter.

**Ce lot est indépendant du lot 047** (« le bourg est une agrégation dérivée »)
et du lot 044. Aucun des trois n'attend les autres, et aucun ordre n'est à
respecter entre eux.

## Fondement dans le modèle

`sim/MODELE.md` :

- § « La mer : la façade que le moteur ne lit pas » — les deux sortes d'arêtes,
  la façade maritime, le nœud unique, et la limite de la donnée ;
- § « Le commerce entre cellules » — la capacité dérivée d'une longueur, le
  goulot de relief, le plafond partagé entre marchandises, l'allocation
  déterministe et l'écrêtage côté receveur, que ce lot étend sans les changer ;
- § « Le mur qui sépare la couche 1 de la couche 2 » — pourquoi cette donnée
  non lue est la première des deux choses qui restent à faire.

Si l'une de ces sections a changé depuis la rédaction de ce brief, la relire
avant de le lancer.

`sim/MODELE.md` est **hors périmètre** de ce lot. La mise à jour des sections
citées après fusion est une dette de l'architecte du modèle (Claude), pas de
l'exécutant.

## État de départ mesuré

Les commandes qui donnent l'état — à rejouer ; aucun de leurs résultats n'est
recopié ici comme cible :

```bash
grep -rn "kind\|land-sea\|stocks_mer" sim/
.venv/bin/python -c "
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
.venv/bin/python -m sim --ticks 365 --seed 0 --json
.venv/bin/python -m pytest sim/tests/ -q
```

Le SHA de base du lot est le `master` du jour où il est lancé ; le mesureur
l'enregistre et compare contre lui.

**Le fait qualitatif qui rend ce lot caduc** : si `sim/` lit déjà les arêtes
dont un seul bout est une cellule du monde, il n'y a rien à faire ici.

**Le piège que ce lot doit éviter, et qui vient d'être payé.** Le lot 043 a fait
lire `DEBIT_KG_PAR_KM_DE_FRONTIERE_PAR_TICK` par son nom dans `sim/engine.py`
alors que `_MondeEpreuve` n'avait pas de `shared_length_m` :
`test_chaque_constante_du_moteur_change_le_monde` est resté rouge sur `master`
jusqu'à ce que le micro-lot 043-bis donne au monde d'épreuve de quoi exercer
les deux chemins de capacité. Voir `sim/MODELE.md`, § « Le monde d'épreuve, et
pourquoi certaines constantes se cachent ».

**La suite est donc verte sur la base, et ce lot doit la laisser verte.**
`_MondeEpreuve` n'a toujours **aucune arête maritime** : une constante de débit
maritime lue par son nom dans `sim/engine.py` y serait inerte et rendrait ce
contrôle rouge à nouveau. C'est pourquoi la règle du monde impose de la lire par
une fonction (§ 3 ci-dessous), et pourquoi SC10 le mesure.

Élargir `_MondeEpreuve` pour lui donner une mer n'est **pas** la solution
retenue ici, et n'est pas autorisé par le périmètre : ce fichier de test n'est
pas modifiable par ce lot.

## Règle du monde

**Fidélité mixte, et la distinction compte.**

- Le trait de côte et la longueur de façade maritime sont de **niveau 1** : ils
  sont dans la carte figée, qui le déclare elle-même dans son champ `fidelite`.
  Ce lot ne les recalcule pas.
- Le **débit maritime par kilomètre de façade** est de **niveau 2** :
  plausible, généré, jamais sourcé. Un flux local surprenant n'est pas un défaut
  historique et n'ouvre ni correctif, ni brief.
- **L'absence de distance en mer est une limite déclarée de la carte**, pas une
  décision de ce lot : voir plus bas.

### 1. Ce qu'est une arête maritime — dérivé, jamais écrit

```
arête maritime := arête d'adjacence dont EXACTEMENT UN des deux bouts
                  est une cellule du monde
nœud mer       := le bout qui n'est pas une cellule du monde
```

La définition est **structurelle**, pas textuelle : aucun nom de `kind` n'est
recopié dans le moteur. Une carte qui renommerait son vocabulaire continuerait
d'être lue, et le nombre d'arêtes maritimes se dérive du fichier à chaque
chargement.

Le champ `kind` sert de **contre-épreuve**, pas de définition : le moteur
dérive l'ensemble des valeurs de `kind` portées par les arêtes qu'il a
identifiées comme maritimes, et **refuse** s'il en trouve plus d'une. Deux
vocabulaires sur le même type d'arête voudraient dire que la définition
structurelle et l'étiquette de la carte ne parlent plus de la même chose.

Un monde dont **aucune** arête n'est maritime n'est pas une erreur : c'est un
monde sans mer, et le maillon maritime n'y joue pas.

### 2. La mer est un bassin, pas un réseau

Toutes les arêtes maritimes de la carte figée touchent **un seul et même
nœud**. Il n'existe donc, dans la donnée, aucune liaison d'un port à un autre
port et aucune distance en mer. Le modèle prend la carte au mot : on expédie
**vers la mer**, on puise **depuis la mer**.

Conséquence assumée, à ne pas prendre pour une propriété du monde : dans un
bassin, deux ports sont à égale distance l'un de l'autre. C'est une limite de
la carte. Le jour où `tools/map/` produira une adjacence port-à-port, elle
tombera sans que le reste bouge.

Si l'adjacence porte **plus d'un** nœud hors du monde, la carte ne décrit pas
un bassin unique : le moteur **refuse** en nommant les identifiants trouvés,
plutôt que d'en choisir un.

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
| `DEBIT_KG_PAR_KM_DE_COTE_PAR_TICK` | 2000.0 × `TICK_DURATION_DAYS` | kilogrammes traversant un kilomètre de façade maritime par tick — **niveau 2**. Soit **dix fois** le débit terrestre au kilomètre : un navire porte sans commune mesure ce que porte un convoi de bêtes de somme, et le fret maritime médiéval revenait à une fraction du fret terrestre à distance égale. Ordre de grandeur plausible, jamais sourcé. |

**Ce facteur dix est décidé ici, sur cet argument seul.** Aucune condition de
succès de ce lot ne dépend de sa valeur : toutes mesurent un rapport, une
direction ou une conservation, jamais un seuil. Si l'exécutant se surprend à
vouloir ajuster cette constante pour faire passer un contrôle, c'est le
contrôle qui est faux, et le brief est à réécrire par son auteur.

**Composition avec les lots 040 et 043**, qu'il ne faut ni écraser ni
dupliquer :

- le débit maritime ne **remplace** aucun débit terrestre : il s'applique à
  d'autres arêtes, et les deux coexistent sans se connaître ;
- le facteur de transport du relief **multiplie** la capacité de quai, comme il
  multiplie déjà la capacité terrestre. Il n'y a **pas** de `min` ici : une
  arête maritime n'a qu'une seule rive terrestre, donc un seul relief à
  consulter. Une côte de haute montagne débarque mal ;
- la table de facteurs de transport est celle du lot 040, lue par la même
  fonction. **Aucun second jeu de facteurs.**

### 4. Le refus de deviner sur une longueur de façade

- **non numérique** (chaîne, booléen, `NaN`) : erreur explicite nommant la
  cellule et le nœud mer ;
- **absente** : erreur explicite, nommant la cellule et le nœud mer. C'est
  volontairement **différent** de la règle terrestre, où une longueur absente
  active le repli `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`. Ce repli est une
  capacité de convoi terrestre : elle ne veut rien dire pour un quai, et
  l'emprunter serait deviner. Il n'existe pas d'équivalent maritime à quoi se
  rabattre, donc l'absence se déclare ;
- **nulle** : valide, et rend zéro. Une cellule qui ne touche la mer qu'en un
  point n'a pas de port. Ce zéro est une **mesure réelle** ; la sentinelle
  « non calculé » du projet est `-1`, jamais `0`.

### 5. Où vit le bassin, et pourquoi il ne casse rien

Le bassin est un panier de marchandises porté par le monde, `World.stocks_mer`,
initialisé vide par `World.__init__`. `sim/world.py` déclare ce panier **et les
deux accès nommés** qui le lisent et l'écrivent ; aucun autre module ne
l'indexe directement — la même discipline que le panier d'une cellule.

Le moteur y accède par `getattr(world, "stocks_mer", None)`, **exactement le
motif déjà employé pour `getattr(world, "carte", None)`** : un monde qui ne
porte pas de bassin ne joue pas le maillon maritime et ne lève aucune erreur.
C'est ce qui laisse intacts les mondes d'épreuve des tests existants, qui n'ont
ni carte ni mer.

Une marchandise **absente** du bassin n'est pas une marchandise à zéro : elle
signifie que la mer n'a jamais porté cette denrée, et il n'y a rien à
débarquer. Une marchandise **à zéro** est une mesure réelle : la mer en a porté,
il n'en reste plus.

### 6. Les trois flux, sur un seul instantané

Le maillon commerce prend déjà un **instantané immuable** des stocks avant tout
transfert, et applique les transferts en une passe finale. Ce lot ne change pas
ce principe : il ajoute deux flux au même calcul.

Une fois par tick, avant toute marchandise :

- `capacite_quai_restante[cell_id]`, calculée une fois et **partagée entre
  toutes les marchandises** — il n'y a qu'un quai, comme il n'y a qu'un convoi
  par arête ;
- `bassin_au_debut`, copie du panier de la mer telle qu'elle est au début du
  tick.

Puis, pour chaque marchandise, sur le même instantané qu'aujourd'hui :

1. **Débarquement** — les cellules côtières en besoin puisent dans
   `bassin_au_debut`, dans la limite de leur besoin et de leur capacité de quai
   restante. Si le bassin ne suffit pas, chaque port reçoit une part
   proportionnelle à son besoin, les ports étant parcourus par `cell_id`
   croissant.
2. **Commerce terrestre** — inchangé.
3. **Expédition** — les cellules côtières dont il reste du surplus **après**
   l'allocation terrestre expédient vers le bassin, dans la limite de leur
   capacité de quai restante.

Puis l'écrêtage côté receveur, déjà présent, porte sur le **total entrant**
d'une cellule, terrestre et débarqué confondus : une cellule adjacente à une
voisine en surplus **et** à la mer ne reçoit jamais plus que son besoin.

Puis tout s'applique en une passe.

**Trois invariants que cette forme achète, et qui sont ce que ce lot doit
prouver :**

- **un kilogramme ne traverse qu'une arête par tick.** Le débarquement ne lit
  que `bassin_au_debut` : ce qui est embarqué au tick `t` ne peut pas être
  débarqué avant `t+1`. C'est le délai physique de la traversée, et il n'est
  pas simulé par un compteur : il tombe de la forme du calcul ;
- **le surplus d'une source est engagé une seule fois**, terre et mer
  confondues. Sans cela, une cellule côtière en surplus expédierait le même
  kilogramme à sa voisine et à la mer, et de la masse serait créée ;
- **la masse se conserve, bassin compris.** La somme des stocks des cellules
  **plus** le bassin est identique avant et après le maillon.

Les kilogrammes débarqués et embarqués **comptent** dans `kg_transportes`, au
même titre que les kilogrammes terrestres : c'est de la masse transportée, et
un compteur qui n'en verrait qu'une partie serait une mesure partielle.

## Source de vérité et raccord au moteur

La longueur de façade vient **uniquement** de l'arête de `world.adjacency`,
telle que la carte figée la porte. Le moteur ne la recalcule pas depuis la
géométrie, ne la duplique pas, et n'écrit jamais dans la carte.

Le relief vient de `world.carte`, par la **même** fonction de table que le lot
040 emploie déjà.

La capacité de quai se calcule à **un seul endroit** dans le moteur, comme la
capacité d'arête terrestre. Il reste **un seul maillon commerce**.

`world.to_dict()` n'est **pas** modifié : le bassin n'entre ni dans la
sérialisation canonique, ni dans le snapshot, ni dans l'empreinte de
déterminisme. Ces trois-là sont hors périmètre, et deux exécutions de même
graine restent strictement identiques.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/engine.py` ;
- `sim/constants.py` ;
- `sim/world.py`, **uniquement** pour déclarer `stocks_mer`, l'initialiser vide
  et fournir ses deux accès nommés. Aucune autre modification de ce fichier, et
  en particulier pas de `to_dict()` ;
- `sim/tests/test_commerce.py`, uniquement pour **ajouter** les cas qui
  protègent cette règle visible. Aucun test déjà vert n'est modifié.

Livrables du lot autorisés :

- `harness/queue/briefs/046-la-mer-est-un-port-commun/deliverables/manifest.json` ;
- `harness/queue/briefs/046-la-mer-est-un-port-commun/deliverables/generator-log.md` ;
- `harness/queue/briefs/046-la-mer-est-un-port-commun/deliverables/measure_046.py` ;
- les sorties textuelles déterministes produites par ce mesureur dans le même
  dossier `deliverables/`.

Tout autre chemin est interdit. En particulier : ne modifier ni `sim/MODELE.md`,
ni `sim/model.py`, ni `sim/aggregation.py`, ni `sim/snapshot_export.py`, ni
`sim/__main__.py`, ni `sim/tests/test_monde.py`, ni
`sim/tests/test_write_coverage.py`, ni `sim/tests/test_survie.py`, ni
`sim/tests/test_determinisme.py`, ni `sim/tests/test_province.py`, ni
`sim/tests/test_no_hardcoded.py`, ni la carte figée `data/world-1400.json`, ni
le visualiseur, ni l'outil de fabrication de la carte, ni le brief 044, ni le
harnais, ni ce brief, ni sa grille, ni un `verdict.md`.

## Conditions de succès

### SC1 — Le moteur identifie les arêtes maritimes, et il les dérive

Un contrôle joue le moteur sur la carte figée et compare le nombre d'arêtes que
le moteur traite comme maritimes au nombre obtenu **indépendamment**, en
relisant `data/world-1400.json` dans le contrôle. Les deux doivent être égaux.

Le dénominateur est le nombre total d'arêtes de la carte, lu du fichier. **Un
échantillon vide échoue** : zéro arête maritime identifiée fait rougir le
contrôle, il ne le fait pas passer.

Un second contrôle vérifie qu'aucun nom de `kind` n'apparaît comme littéral de
comparaison dans `sim/engine.py` : la définition est structurelle.

**Le rouge est prouvé avant la correction** : sur le SHA de base, `sim/` ne lit
ni `kind`, ni les arêtes dont un seul bout est une cellule du monde, et le
compte des arêtes maritimes traitées vaut zéro.

### SC2 — Un port en surplus expédie, un port en manque débarque

Sur un micro-monde déterministe portant une arête maritime pour chacune de deux
cellules et **aucune arête terrestre entre elles** : la cellule en surplus voit
son stock baisser et le bassin monter d'autant ; au tick suivant, la cellule en
manque voit son stock monter et le bassin baisser d'autant.

**Le rouge est prouvé avant la correction** : sur le SHA de base, aucun des
deux stocks ne bouge et le bassin n'existe pas.

### SC3 — Le grain embarqué à `t` n'est pas débarqué à `t`

Même micro-monde, un seul expéditeur et un seul receveur. Au tick où
l'expédition a lieu, le receveur ne reçoit **rien** ; au tick suivant, il
reçoit. Le nombre de ticks joués est dérivé du contrôle, jamais écrit comme
attendu.

Ce zéro du premier tick est une **mesure réelle** : le maillon a été joué et a
compté zéro. La sentinelle « non calculé » du projet est `-1`, jamais `0`.

### SC4 — La masse se conserve, bassin compris

Sur un micro-monde, la somme des stocks de toutes les cellules **plus** le
contenu du bassin est identique avant et après le maillon commerce, à chaque
tick joué. L'écart mesuré doit valoir **0**, et ce zéro est une mesure.

Le contrôle existant `test_conservation_masse_transport` reste vert **sans être
modifié** : son monde n'a pas de mer, et la somme sur les seules cellules y
reste donc l'invariant complet.

### SC5 — Les cellules hermétiques cessent de l'être

Le mesureur dérive de la carte figée l'ensemble des cellules qui n'ont **aucune**
arête terrestre vers une autre cellule du monde mais qui portent au moins une
arête maritime. Pour cet ensemble :

- sur le SHA de base, le nombre de partenaires de commerce de chacune vaut
  **zéro** ;
- après changement, chacune a une capacité de quai strictement positive, sauf
  celles dont la façade est de longueur nulle — comptées séparément, et leur
  zéro est une mesure.

Le dénominateur est le nombre de cellules réellement examinées. **Un échantillon
vide échoue.**

### SC6 — La façade commande le débit

Sur un micro-monde déterministe où plusieurs cellules identiques puisent dans un
bassin suffisamment fourni par des façades de longueurs différentes, les
quantités débarquées sont dans le **même rapport** que les longueurs, tant
qu'aucune n'est bornée par le besoin ou par le bassin. Les longueurs de
l'échantillon sont **dérivées de la carte figée** : la plus courte, la médiane
et la plus longue façade maritime réellement présentes.

**Le rouge est prouvé avant la correction** : sur le SHA de base, les trois
débarquent la même chose, à savoir rien.

### SC7 — Le goulot de relief se compose, sans second jeu de facteurs

À longueur de façade égale, deux cellules de classes de relief différentes
n'expédient pas la même quantité, et l'ordre suit strictement la table de
transport du lot 040. Les classes de l'échantillon sont dérivées de la carte ;
si l'une manque, le contrôle échoue au lieu de la sauter.

Un contrôle parcourt l'arbre syntaxique des modules de `sim/` hors tests et
échoue si un second jeu de facteurs de transport apparaît. Le nombre de modules
parcourus est dérivé du répertoire.

### SC8 — Le refus de deviner

Chacun de ces cas est **essayé** — une commande, un message d'erreur — et pas
seulement affirmé :

- une longueur de façade **non numérique** (chaîne, `None`, `NaN`) sur une arête
  maritime provoque une erreur explicite nommant la cellule et le nœud mer ;
- une longueur de façade **absente** sur une arête maritime provoque la même
  erreur explicite. Aucun repli sur la capacité plate terrestre n'est admis ;
- une adjacence portant **plus d'un** nœud hors du monde provoque une erreur
  explicite nommant les identifiants trouvés ;
- des arêtes maritimes portant **plusieurs valeurs de `kind`** distinctes
  provoquent une erreur explicite nommant les valeurs.

Et le cas qui n'est **pas** une erreur, à prouver aussi : un monde qui ne porte
pas `stocks_mer` joue le tick sans maillon maritime, sans lever d'exception et
sans changer de résultat par rapport au SHA de base.

Le compteur des refus est le nombre de mutations réellement exécutées ; un
échantillon vide échoue.

### SC9 — Le monde réel transporte davantage

Sur le monde réel, le champ `kg_transportes` de
`.venv/bin/python -m sim --ticks 365 --seed 0 --json` est **strictement
supérieur** à celui rejoué sur le SHA de base.

Aucun seuil n'est exigé et aucun nombre de ce brief n'est recopié : le maillon
maritime **ajoute** des arêtes et n'en retire aucune, donc la direction est la
seule chose que ce contrôle a le droit d'affirmer. Le mesureur archive la sortie
de base **avant** l'édition et la relit ; la comparaison emploie
`must_differ_from_git` contre la référence Git du SHA de base, jamais une copie
fabriquée après coup.

### SC10 — Les invariants existants restent intacts, et la suite reste verte

- `.venv/bin/python -m pytest sim/tests/ -q` est **vert**, comme sur le SHA de
  base. La liste des tests en échec après changement est **vide**, et elle est
  comparée à celle rejouée sur la base plutôt que supposée ;
- `test_chaque_constante_du_moteur_change_le_monde` reste vert **sans que le
  test ni `_MondeEpreuve` soient touchés**. C'est le contrôle que ce lot risque
  le plus de casser, et il ne doit pas l'être en le modifiant ;
- `DEBIT_KG_PAR_KM_DE_COTE_PAR_TICK` **n'apparaît pas** comme attribut lu dans
  `sim/engine.py` — le moteur passe par `debit_maritime_kg_par_km()`, motif
  déjà employé par les tables de facteurs. Le compteur des noms trouvés vaut
  **0**. C'est précisément ce qui empêche cette constante d'être inerte sur un
  monde d'épreuve qui n'a pas de mer, et donc de rouvrir le rouge que le
  micro-lot 043-bis vient de fermer ;
- `test_conservation_masse_transport`, `test_invariance_ordre_aretes`,
  `test_recepteur_pas_sur_livre` et `test_kg_transportes_egal_deltas_positifs`
  restent verts **sans modification**. Leurs mondes n'ont pas de mer : la somme
  des deltas positifs des cellules y reste égale à `kg_transportes` ;
- les trois propriétés de régime de `sim/tests/test_survie.py` restent vertes
  **sans modification de ce fichier**. Le plafond employé est celui que le
  moteur dérive : un commerce plus large déplace mieux la nourriture, il n'en
  crée pas ;
- `test_le_moteur_ne_lie_aucune_constante_par_valeur`,
  `test_aucune_constante_terminale` et `test_no_hardcoded_numeric_literals`
  restent verts ;
- deux exécutions de `.venv/bin/python -m sim --ticks 365 --seed 0 --json` sont
  strictement identiques entre elles ;
- aucune instruction `global` n'apparaît dans `sim/engine.py` ;
- il n'y a toujours qu'un seul maillon commerce dans `sim/` ;
- le nombre de tests collectés dans `sim/tests/` est au moins celui du SHA de
  base.

## Compteurs exigés

Le mesureur `deliverables/measure_046.py` reconstruit chaque compteur ; il ne
porte aucun résultat en dur.

| compteur | source d'échantillon | dénominateur dérivé |
|---|---|---|
| `aretes_maritimes_carte` | relecture indépendante de `data/world-1400.json` | nombre total d'arêtes de la carte |
| `aretes_maritimes_traitees_par_le_moteur` | instrumentation du maillon commerce sur le monde chargé | `aretes_maritimes_carte` |
| `noeuds_hors_monde_distincts` | parcours de l'adjacence | nombre d'identifiants distincts rencontrés |
| `valeurs_de_kind_sur_aretes_maritimes` | même parcours | `aretes_maritimes_carte` |
| `litteraux_de_kind_dans_engine` | parcours de l'arbre syntaxique de `sim/engine.py` | nombre de comparaisons de chaînes trouvées |
| `cellules_sans_voisin_terrestre` | parcours de l'adjacence du monde chargé | nombre total de cellules chargées |
| `cellules_sans_voisin_terrestre_a_quai_positif` | capacité de quai calculée sur chacune | `cellules_sans_voisin_terrestre` |
| `cellules_sans_voisin_terrestre_a_facade_nulle` | même parcours | `cellules_sans_voisin_terrestre` |
| `longueurs_de_facade_distinctes` | façades maritimes du monde chargé | nombre de cellules côtières réellement mesurées |
| `rapports_debarques_sur_longueurs` | micro-monde, façades courte, médiane et longue dérivées de la carte | nombre de façades réellement essayées |
| `debarque_au_tick_d_embarquement` | micro-monde de SC3 | nombre de ticks réellement joués |
| `debarque_au_tick_suivant` | même micro-monde | nombre de ticks réellement joués |
| `ecart_de_masse_bassin_compris` | somme des stocks des cellules plus le bassin, avant et après le maillon | nombre de cellules réellement sommées, plus un |
| `expeditions_ordonnees_par_relief` | micro-monde, classes de relief dérivées de la carte | nombre de classes réellement mesurées |
| `jeux_de_facteurs_de_transport` | parcours de l'arbre syntaxique des modules de `sim/` hors tests | nombre de modules réellement parcourus |
| `refus_de_deviner_declenches` | mutations en mémoire des quatre cas de SC8 | nombre de mutations réellement exécutées |
| `monde_sans_bassin_joue_sans_erreur` | tick joué sur un monde dépourvu de `stocks_mer` | nombre de ticks réellement joués |
| `noms_de_debit_maritime_dans_engine` | parcours de l'arbre syntaxique de `sim/engine.py` | nombre de noms du motif recherchés |
| `kg_transportes_avant` | sortie de base rejouée et archivée avant édition | nombre d'exécutions réellement lancées |
| `kg_transportes_apres` | même commande après changement | nombre d'exécutions réellement lancées |
| `tests_en_echec_avant` | collecte pytest sur le SHA de base | nombre de tests collectés |
| `tests_en_echec_apres` | collecte pytest après changement | nombre de tests collectés |
| `tests_collectes_avant` | collecte pytest sur le SHA de base | nombre de fichiers de test collectés |
| `tests_collectes_apres` | collecte pytest après changement | nombre de fichiers de test collectés |

Contraintes sur ces compteurs, toutes vérifiables :

- `aretes_maritimes_traitees_par_le_moteur` est égal à `aretes_maritimes_carte`,
  et strictement positif ;
- `noeuds_hors_monde_distincts` et `valeurs_de_kind_sur_aretes_maritimes`
  valent **1** sur la carte figée ;
- `litteraux_de_kind_dans_engine`, `debarque_au_tick_d_embarquement`,
  `ecart_de_masse_bassin_compris` et `noms_de_debit_maritime_dans_engine`
  valent **0** ; `jeux_de_facteurs_de_transport` vaut **1**. Ces zéros sont des
  mesures réelles ; la sentinelle « non calculé » du projet est `-1`, jamais
  `0` ;
- `debarque_au_tick_suivant` est strictement positif ;
- `cellules_sans_voisin_terrestre` est strictement positif, et la somme de
  `…_a_quai_positif` et `…_a_facade_nulle` lui est égale ;
- `monde_sans_bassin_joue_sans_erreur` est strictement positif ;
- `kg_transportes_apres` est strictement supérieur à `kg_transportes_avant` ;
- `tests_en_echec_avant` et `tests_en_echec_apres` valent **0**, et les deux
  listes de noms sont vides. Ces zéros sont des mesures réelles, obtenues en
  jouant la suite deux fois ;
- `tests_collectes_apres` est au moins `tests_collectes_avant`.

## Livrables et porte mécanique

Le dossier `deliverables/` contient au minimum :

- `manifest.json`, avec les commandes exactes et les compteurs ci-dessus ;
- `generator-log.md`, en français clair : le rouge prouvé de SC1, SC2, SC3 et
  SC6 avant correction, les fichiers modifiés, les commandes jouées, les
  résultats et les limites — dont la limite de fidélité du bassin sans
  distance ;
- `measure_046.py`, rejouable depuis la racine avec `.venv/bin/python`.

Les chemins du manifeste sont relatifs au dossier du brief. Les sorties
comparées avant/après utilisent `must_differ_from_git` avec la référence Git du
SHA de base, pas une copie `.orig` fabriquée après coup.

## Hors périmètre

- `sim/MODELE.md` (dette de l'architecte après fusion) ;
- **la migration par la mer** : les partants continuent de ne suivre que les
  arêtes terrestres. Une cellule sans voisin terrestre reste inquittable, et
  c'est un autre lot ;
- les routes, les ponts, les ports comme ouvrages, les fleuves, et tout
  investissement dans une infrastructure ;
- la distance en mer, les routes maritimes port-à-port, et toute reconstruction
  de la carte ;
- le coût du transport en nourriture, en temps, en pertes en route ou en
  naufrages ;
- la saison de navigation, les vents, la glace ;
- la définition d'un bourg ou d'une ville — c'est le lot 047, et il ne dépend
  pas de celui-ci ;
- la production, la consommation, la mortalité, la natalité, la migration ;
- la division du travail et le lot 044 ;
- `world.to_dict()`, le schéma du snapshot, sa version, et le visualiseur ;
- l'élargissement de `_MondeEpreuve` et toute modification de
  `sim/tests/test_write_coverage.py`, que le micro-lot 043-bis vient de
  reprendre ;
- calibration d'un test existant après observation ;
- Unity, architecture, sécurité, CI, ForgePilot et fusion.

## Interdictions pour l'exécutant

L'exécutant n'écrit pas de `verdict.md`, ne modifie ni ce brief ni
`eval-rubric.md`, ne juge pas son propre travail, ne fusionne rien et ne pousse
pas directement sur `master`.
