# Brief 050 — On migre aussi par la mer

## But

Faire qu'une cellule qui n'a **aucun voisin terrestre** — et qui aujourd'hui,
même affamée, ne peut être quittée par aucun migrant — puisse envoyer ses
affamés vers une autre cellule côtière en surplus, **par la mer**.

C'est exactement le manque que le lot 046 a déclaré hors de son périmètre :
« la migration par la mer : les partants continuent de ne suivre que les
arêtes terrestres. Une cellule sans voisin terrestre reste inquittable, et
c'est un autre lot. » (`briefs/046-la-mer-est-un-port-commun.md`, § Hors
périmètre). C'est ce lot.

**Dépend de 046** (« la mer est un port commun »), **déjà fusionné**
(PR #206) : ce lot réutilise `_aretes_maritimes_du_monde`, la fonction que
046 a introduite dans `sim/engine.py` pour dériver, sans jamais recopier de
nom de `kind`, l'ensemble des arêtes dont exactement un bout est une cellule
du monde, et pour refuser un monde dont les arêtes maritimes porteraient
plusieurs nœuds mer ou plusieurs valeurs de `kind`. Ce lot ne redérive pas
cette notion une seconde fois : il appelle la fonction existante. 046 étant
livré, ce lot n'est **pas bloqué**.

**Indépendant du bassin de marchandises `stocks_mer`, de `capacite_quai` et
de tout le commerce maritime.** Un migrant ne transporte aucun kilogramme
(`sim/MODELE.md` § « La migration de famine ») : ce lot n'a besoin ni du
panier de la mer, ni d'une capacité de quai, ni d'aucun débit en kg. Il ne
touche donc à rien de ce que 046 a ajouté à `sim/world.py` ou
`sim/constants.py` — seule la fonction d'identification des arêtes
maritimes de `sim/engine.py` est réutilisée.

Ce qui rend ce lot caduc : si `sim/` permet déjà à une cellule sans voisin
terrestre d'être quittée par un migrant, il n'y a rien à faire ici.

```bash
grep -n "_aretes_maritimes_du_monde\|_voisins_avec_surplus\|_apply_migration" sim/engine.py
py -c "
import json
doc = json.load(open('data/world-1400.json', encoding='utf-8'))
ids = {c['cell_id'] for c in doc['cellules']}
adj = doc['adjacence']
deg = {cid: 0 for cid in ids}
for e in adj:
    if e['a'] in ids and e['b'] in ids:
        deg[e['a']] += 1; deg[e['b']] += 1
print('sans_voisin_terrestre', sum(1 for c in ids if deg[c] == 0))
"
py -m pytest sim/tests/test_commerce.py -k migration -q
```

## Règle du monde

Découle de [`sim/MODELE.md`](../sim/MODELE.md) : § « La migration de famine »
(qui part — la pénurie du tick strictement positive —, où l'on va — les
voisines pondérées par leur surplus post-consommation sur un instantané —,
et l'atomicité — un migrant ne traverse qu'une arête par tick, une
réceptrice n'envoie pas le même tick) ; et § « La mer : la façade que le
moteur ne lit pas », pour sa définition **structurelle** d'une arête
maritime et pour le fait que la carte ne porte **aucune distance en mer** ni
aucune liaison port-à-port — seulement un bassin commun. Si l'une de ces
sections a changé depuis, la relire avant de lancer.

**Avertissement de dette documentaire, pas de ce lot** : `sim/MODELE.md`
affirme encore, au moment d'écrire ce brief, que « le moteur ne lit pas du
tout » les arêtes maritimes. C'est faux depuis la fusion de 046 pour le
commerce — la mise à jour de ce document reste une dette de l'architecte du
modèle (`ROADMAP.md`, § cycle). Seule sa définition structurelle de l'arête
maritime et du bassin est citée ici ; elle n'a pas changé.

**Fidélité niveau 2** : aucune nouvelle constante numérique n'est
introduite. La fraction de départ (`FRACTION_MIGRANTE_PAR_TICK`) et la
pondération par surplus post-consommation (`_surplus_nourriture_tick`) sont
exactement celles déjà en place pour la route terrestre — ce lot ne fait que
leur ouvrir une seconde route, pas un second réglage.

### 1. Qui devient éligible à la route maritime

Une cellule est éligible à la route maritime **seulement si** :

- elle **n'a aucune arête terrestre** vers une autre cellule du monde
  (structurel : pour toute arête qui la touche, l'autre bout n'est jamais
  une cellule de `world.cells`) ;
- elle porte **au moins une arête maritime** (`cell_id` présent dans
  `_aretes_maritimes_du_monde(world)`).

C'est exactement l'ensemble mesuré par `sim/MODELE.md` § « La mer » —
« plus d'une cellule sur trois n'a aucun voisin terrestre » — et par la
SC5 du lot 046. Ces deux conditions sont indépendantes : une cellule sans
aucune arête du tout (ni terrestre, ni maritime — donnée absente) reste
inquittable, et ce lot ne peut rien y faire sans données supplémentaires ;
ce n'est pas un défaut de ce lot.

**Une cellule qui a au moins un voisin terrestre n'utilise jamais la route
maritime, même si elle touche aussi la mer**, et même si son unique
voisine terrestre n'a aucun surplus ce tick-là. La route maritime n'est pas
une seconde option pour une cellule déjà connectée par la terre : elle
n'existe que pour la sortir de la boîte fermée qu'aucune arête terrestre ne
lui a ouverte.

### 2. Où l'on va

Vers **toute cellule qui porte une arête maritime** (le même ensemble que
ci-dessus, sans restriction d'être elle-même hermétique) **et** dont il
reste un surplus alimentaire post-consommation strictement positif, sur le
même instantané que la route terrestre — jamais un second instantané.

Le poids d'une destination est **exactement** `_surplus_nourriture_tick`, la
fonction déjà employée par `_voisins_avec_surplus`. Aucune seconde fonction
de pondération n'est écrite. La répartition entre plusieurs destinations
suit `_repartir_habitants_proportionnellement`, inchangée.

Une cellule éligible qui ne trouve **aucune** destination maritime en
surplus (les autres ports sont tous à sec) ne part pas non plus : c'est un
zéro mesuré, pas une erreur.

### 3. Une seule traversée, comme la terre — et pourquoi ce n'est pas un délai

Contrairement au bassin de marchandises (`stocks_mer`), qui doit conserver
une masse stockée d'un tick sur l'autre et pour cela retarde d'un tick le
débarquement (046, SC3), un migrant n'est **stocké nulle part** entre son
départ et son arrivée : ce n'est pas un kilogramme dont il faut garder la
trace dans un panier. Ce lot traite donc la traversée maritime comme **une
seule arête franchie en un tick**, exactement comme un déplacement vers une
voisine terrestre — la carte ne porte de toute façon **aucune distance en
mer** à partir de laquelle dériver un délai plus long (`sim/MODELE.md` §
« La mer »). C'est une décision de modélisation, déclarée ici : elle
n'invente aucun nombre, elle choisit de ne pas en inventer un
(un délai de « n ticks » n'aurait aucune donnée pour dériver n).

Conséquence directe : **aucun champ n'est ajouté à `Cell` ni à `World`.**
Le mouvement se calcule sur le même instantané, dans la même passe, que la
route terrestre, et s'applique dans le même lot final de transferts —
l'invariant « une cellule qui reçoit n'envoie pas le même tick » s'applique
donc uniformément aux arrivées par terre et par mer, sans code séparé pour
les distinguer.

### 4. Le refus hérité

Ce lot appelle `_aretes_maritimes_du_monde`, il ne la réimplémente pas.
Un monde dont les arêtes maritimes portent plusieurs nœuds mer distincts ou
plusieurs valeurs de `kind` fait donc lever `NoeudsMerMultiplesError` ou
`KindsMaritimesMultiplesError` **pendant la migration aussi**, exactement
comme pendant le commerce — la preuve que la migration consulte la même
dérivation, jamais une version plus permissive écrite pour l'occasion.

### Ce qui se refuse plutôt que se deviner

- **Aucune capacité de port pour les migrants.** Ni `capacite_quai`, ni
  aucune nouvelle constante de débit ou de tonnage par personne : la route
  terrestre n'a elle-même aucune contrainte de capacité par arête, seulement
  la fraction `FRACTION_MIGRANTE_PAR_TICK` côté source. La route maritime
  reste symétrique à cela.
- **Aucune priorité entre terre et mer au-delà de « la terre d'abord si elle
  existe ».** Une cellule qui a un voisin terrestre n'essaie jamais la mer
  (§ 1) ; ce n'est pas une histoire de préférence économique, c'est que la
  route maritime n'existe que pour l'absence structurelle de route
  terrestre.
- **Aucune distance en mer, aucun port-à-port.** Comme pour le commerce :
  dans un bassin, deux ports sont à égale distance l'un de l'autre.

### Où ça se raccorde

`tick()` garde exactement sept maillons dans le même ordre ; seul le
maillon 7 (`_apply_migration`) change. `_apply_commerce`, `stocks_mer`,
`capacite_quai` et tout le reste du commerce maritime restent inchangés :
ce lot ne les appelle jamais et ne les modifie pas.

## Périmètre

En écriture :

- `sim/engine.py`, pour étendre `_apply_migration` (et la ou les fonctions
  qu'elle appelle pour dériver l'ensemble des cellules sans voisin
  terrestre et la route maritime de repli), et pour mettre à jour le
  docstring d'en-tête et celui du maillon 7 qui décrivent les sept
  maillons ;
- `sim/tests/test_commerce.py`, **uniquement pour y ajouter** des cas —
  c'est le fichier qui porte déjà tous les tests de migration et de
  commerce maritime. Aucun test déjà présent n'est modifié, renommé ni
  supprimé.

Tout autre chemin est interdit, nommément : `sim/MODELE.md`, `sim/model.py`,
`sim/world.py`, `sim/constants.py`, `sim/aggregation.py`,
`sim/snapshot_export.py`, `sim/__main__.py`, `sim/tests/test_write_coverage.py`
— dont `_MondeEpreuve` —, les autres fichiers de `sim/tests/`, la carte
figée `data/world-1400.json`, le visualiseur, les briefs 044, 046, 047, 048,
049, 051, 052, 053, 054, et ce brief.

**Aucun test existant n'est modifié, renommé, supprimé ni relâché.** Si un
test existant devient rouge, c'est le code de ce lot qui est faux, ou c'est
ce brief : on s'arrête et on le dit — on ne touche pas au test.

## Conditions de succès

Les comparaisons « avant / après » se font contre `master` rejoué au
démarrage du lot, jamais contre un nombre recopié d'ici.

### SC1 — Rouge prouvé d'abord : une cellule hermétique reste bloquée aujourd'hui

Sur un micro-monde déterministe portant une cellule sans aucune arête
terrestre mais avec une façade maritime, forcée en pénurie sur plusieurs
ticks consécutifs, et une autre cellule côtière (sans arête terrestre
commune avec la première) en surplus alimentaire : sur `master`, la
population de la cellule affamée ne baisse **jamais** par migration
(`_apply_migration` seule, appelée directement, sans mortalité).

### SC2 — Après ce lot, elle est quittée par la mer

Sur le même micro-monde : la cellule hermétique perd des habitants, la
cellule côtière en surplus en gagne, dans le même tick.

### SC3 — Conservation de la population, mer comprise

Sur le même micro-monde, la somme des populations de toutes les cellules
est identique avant et après `_apply_migration`. L'écart mesuré vaut **0**,
et ce zéro est une mesure.

### SC4 — Pondération proportionnelle au surplus entre plusieurs ports

Sur un micro-monde où une cellule hermétique fait face à **au moins deux**
cellules côtières en surplus de valeurs différentes, aucune des deux
n'ayant d'arête terrestre avec la première ni entre elles : les migrants se
répartissent dans un rapport correspondant à leurs surplus respectifs,
comme `_repartir_habitants_proportionnellement` le fait déjà pour la route
terrestre.

### SC5 — Une cellule connectée par la terre n'utilise jamais la mer

Sur un micro-monde où une cellule affamée a **une** voisine terrestre sans
aucun surplus ce tick **et** une façade maritime vers un port en surplus :
zéro partant. La route maritime ne s'active que pour l'absence structurelle
de voisin terrestre (§ 1), jamais en complément d'une route terrestre à
sec.

**Rouge prouvé** sur une implémentation délibérément trop permissive qui
ouvrirait la mer dès que les destinations terrestres du tick sont vides
(plutôt que dès l'absence structurelle de voisin terrestre) : ce contrôle
doit la faire échouer.

### SC6 — Atomicité : une réceptrice par la mer n'envoie pas le même tick

Sur un micro-monde à trois cellules — une hermétique affamée A, un port en
léger surplus B qui est lui-même affamé et n'a que B comme voisin terrestre
d'un troisième C en surplus — la cellule B reçoit les migrants de A par la
mer et n'envoie aucun migrant vers C ce même tick, exactement comme
l'invariant terrestre déjà protégé par
`test_receveuse_ne_renvoie_pas_meme_tick`.

### SC7 — Zéro partant si aucun port n'a de surplus

Sur un micro-monde où la cellule hermétique affamée est la **seule**
cellule côtière du monde (ou où tous les autres ports sont eux-mêmes à
sec) : zéro partant, mesuré, pas supposé.

### SC8 — La migration hérite du refus de bassin ambigu

Sur un monde dont les arêtes maritimes portent deux nœuds mer distincts (le
même motif que `test_refus_maritimes_compteur_mutations` construit déjà
pour le commerce), un appel à `_apply_migration` lève
`NoeudsMerMultiplesError` — la preuve que la migration appelle
`_aretes_maritimes_du_monde` plutôt qu'une dérivation à elle, plus
permissive.

### SC9 — Le déterminisme tient

Deux exécutions de `py -m sim --ticks 30 --seed 0 --json` sont strictement
identiques entre elles.

### SC10 — Les invariants existants restent intacts

```bash
py -m pytest sim/tests/ viewer/tests/ -q
```

- vert, et la liste des tests en échec est **vide**, comparée à celle de
  `master` plutôt que supposée ;
- `test_conservation_population_migration`,
  `test_depart_affame_vers_surplus_temoin_inchange`,
  `test_zero_partant_sans_destination_surplus`,
  `test_zero_partant_depuis_cellule_rassasiee`,
  `test_pas_immobilite_par_arrondi_migration`,
  `test_receveuse_ne_renvoie_pas_meme_tick`,
  `test_invariance_ordre_aretes_migration`,
  `test_sentinelle_migration_remainder`, et tous les tests de commerce
  maritime déjà présents (`test_aretes_maritimes_derivees_de_la_carte`,
  `test_port_surplus_expedie_port_manque_debarque`,
  `test_monde_sans_stocks_mer_inchange`, etc.) restent verts **sans
  modification** ;
- `git diff master -- sim/constants.py` et `git diff master -- sim/world.py`
  ne montrent **rien** : ce lot n'introduit aucune nouvelle constante et ne
  touche à aucun champ du monde ;
- `test_no_hardcoded_numeric_literals` reste vert : aucun littéral
  numérique hors 0, 1 et −1 dans le code ajouté ;
- aucune instruction `global` dans `sim/engine.py` ;
- le nombre de tests collectés est strictement supérieur à celui de
  `master`.

```bash
git diff master -- sim/tests/ | grep "^-" | grep -v "^---"
```

Cette seconde commande ne sort **rien** : aucune ligne retirée d'aucun
fichier de test.

## Hors périmètre

- `sim/MODELE.md` — la mise à jour après fusion est une dette de
  l'architecte du modèle, pas de l'exécutant ;
- toute distance en mer, toute route port-à-port : la mer reste un bassin
  commun, comme 046 l'a posé ;
- toute capacité de port, de quai ou de tonnage par personne pour les
  migrants : voir « Ce qui se refuse » ;
- tout délai de traversée en ticks, tout champ ajouté à `Cell` ou `World`
  pour stocker des migrants « en mer » : voir § 3 ;
- toute route maritime pour une cellule qui a déjà un voisin terrestre : la
  mer n'est un repli que pour l'absence structurelle de voisin terrestre
  (§ 1, SC5) ;
- le commerce maritime, `stocks_mer`, `capacite_quai` : inchangés, non
  appelés ;
- le bourg, la ville, le métier, la fabrication ;
- le schéma du snapshot, sa version, le visualiseur ;
- la calibration d'un test existant après observation, et toute autre
  retouche d'un test existant, quel qu'en soit le motif.
