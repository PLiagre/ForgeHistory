# sim/MODELE.md — comment le monde fonctionne

> **Tenu par Claude** (architecte du modèle, ADR-0018). C'est le document
> qu'Hermes découpe en briefs. Il dit comment le monde fonctionne — pas
> quoi faire pour un lot donné : ça, c'est le brief.
>
> Il s'appelait `MODELE.md` et était rangé par numéro de brief. Il est
> maintenant rangé par mécanisme : les briefs sont archivés, leurs numéros
> ne veulent plus rien dire pour qui lit le moteur aujourd'hui.

## En une page

Le monde est une grille de 596 cellules lues dans la carte figée
`data/world-1400.json`. À chaque tick, dans cet ordre :

1. **Production** — chaque cellule produit de la nourriture proportionnellement
   à sa surface, multipliée par un aléa de rendement du tick.
2. **Commerce** — les cellules en surplus livrent leurs voisines en manque.
   Un kilogramme ne traverse qu'une arête par tick et ne nourrit qu'une fois.
3. **Consommation** — chaque habitant mange sa ration. Ce qui manque devient
   une **dette** (`food_deficit_kg`), pas un oubli.
4. **Faim et mortalité** — une cellule en manque ce tick est affamée ; la
   dette tue, avec report de la fraction d'habitant non encore morte pour
   qu'une petite cellule ne devienne pas immortelle par arrondi.
5. **Récupération** — un surplus rembourse la dette, jamais plus vite que le
   surplus lui-même.

La **province** ne se stocke pas : elle se recalcule à chaque consultation
comme « le centre administratif le plus proche » (ADR-0003).

## Ce que le moteur ne fait pas encore

La carte porte trois couches que le tick **ne joue pas** : le relief en cinq
classes, les déterminants du climat, les 27 gisements. Le snapshot le dit
lui-même, couche par couche.

Ce n'est pas une déclaration, c'est une **mesure**. Pour chaque couche, le
snapshot charge deux mondes identiques, en altère franchement la couche dans
l'un **avant l'amorçage**, joue trois ticks avec la même graine et compare
l'état obtenu. Différent : le moteur lit la couche. Identique au bit près :
il ne la lit pas.

Conséquence voulue : le jour où le tick consommera le relief,
`utilisee_par_le_moteur` passera à `true` tout seul. Personne n'a de
constante à retourner, et personne ne peut la retourner sans que le moteur
ait changé. C'était auparavant un triplet de booléens écrits à la main, et
le test se contentait de figer leur valeur courante.

Le prochain pas du modèle est de faire compter le relief dans le rendement :
une cellule de montagne ne produit pas comme une plaine. Il se fait **à un
seul endroit**, `production_kg()` dans `sim/engine.py`. Le plafond physique de
survie appelle la même fonction : il suit tout seul, et les tests de survie
n'ont pas à changer. Ce n'était pas vrai avant ce lot — un modèle analytique
prédisait la valeur absolue de la survie et devait être re-dérivé à chaque
changement de la production.

## Déclaration explicite

**L'amorçage décrit dans ce fichier est un proxy paramétrique, pas une donnée
historique.** Aucune valeur de population ou de stock alimentaire initial ne
provient d'une source historique documentée. Conformément à la hard-won rule 10
(« l'absence de données ne s'invente pas en silence »), cette limitation est
déclarée ici de manière explicite.

Les paramètres ci-dessous sont des valeurs d'ordre de grandeur plausibles pour
une simulation médiévale/proto-moderne (1400-1900). Ils peuvent être calibrés
à tout moment par un brief ultérieur disposant de données historiques réelles.

---

## La base de temps

### Constante centrale

```
TICK_DURATION_DAYS = 1
```

Un tick représente **1 jour calendaire**. Toutes les constantes temporelles
ci-dessous sont dérivées de cette valeur — aucune d'elles ne contient de
littéral de durée indépendant.

**Justification** : le jour est la plus petite unité de temps agronomique
pertinente (rotation des convois, consommation alimentaire quotidienne, cycle
de production journalier). Un tick-jour permet une calibration directe avec
les sources historiques (rations, rendements annuels ÷ 365).

---

## Population initiale par cellule

### Formule

```
population = max(0, int(area_km2 × INITIAL_POPULATION_PER_KM2 × variation))
```

où `variation = rng.uniform(SEED_POPULATION_VARIATION_LOW, SEED_POPULATION_VARIATION_HIGH)`.

### Paramètres

| Constante | Valeur | Unité | Justification |
|---|---|---|---|
| `INITIAL_POPULATION_PER_KM2` | 10.0 | hab/km² | Densité médiévale européenne moyenne (ordre de grandeur : 5–20 hab/km², Bairoch 1988) |
| `SEED_POPULATION_VARIATION_LOW` | 0.9 | — | Variation minimale autour de la densité nominale (±10 %) |
| `SEED_POPULATION_VARIATION_HIGH` | 1.1 | — | Variation maximale autour de la densité nominale (±10 %) |

### Déterminisme

Deux appels à `World.charger(rng_seed=K)` avec la même graine `K` produisent
des populations initiales byte-identiques, car `rng = random.Random(rng_seed)`
initialise un générateur pseudo-aléatoire isolé (jamais de source globale).

---

## Stock alimentaire initial

### Formule

```
food_stock_kg = population × FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK × INITIAL_FOOD_RESERVE_TICKS
```

Le stock de départ couvre `INITIAL_FOOD_RESERVE_TICKS` ticks de consommation
normale. Ce buffer initial est volontairement court (5 ticks = 5 jours) pour
que la dynamique de production/consommation prenne effet rapidement sans
créer une réserve artificielle trop grande.

### Paramètres

| Constante | Valeur | Unité | Justification |
|---|---|---|---|
| `FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK` | 2.0 × TICK_DURATION_DAYS | kg/personne/tick | Ration journalière médiévale approx. 2 kg (céréales + substituts) × 1 jour/tick |
| `INITIAL_FOOD_RESERVE_TICKS` | 5 | ticks | Réserve de subsistance de 5 jours — suffisante pour absorber 2–3 mauvaises journées consécutives sans mort immédiate, sans masquer le comportement de long terme sur 200 ticks |

**Note sur le renommage (SC1 — constat P3-2 de l'audit)** : le champ était
précédemment appelé `INITIAL_FOOD_DAYS` ce qui laissait croire que l'unité
était le jour calendaire. Renommé en `INITIAL_FOOD_RESERVE_TICKS` pour
refléter que l'unité est le tick (= 1 jour ici, mais séparation claire des
unités).

---

## Le rendement agricole et sa variabilité

### Formule (par tick)

```
yield_factor = rng.uniform(RNG_YIELD_LOW, RNG_YIELD_HIGH)
food_produced = area_km2 × FOOD_PRODUCTION_KG_PER_KM2_PER_TICK × yield_factor
```

### Paramètres

| Constante | Valeur | Unité | Dérivation |
|---|---|---|---|
| `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK` | 18.0 × TICK_DURATION_DAYS | kg/km²/tick | Proxy annuel : ~6 570 kg/km²/an (rendement brut médiéval ~1 800 kg/ha à 36 % de surface cultivée, référence Slicher van Bath 1963) ÷ 365 jours/an × 1 jour/tick ≈ 18.0 |
| `RNG_YIELD_LOW` | 0.5 | — | Facteur multiplicatif minimum : mauvaise saison (sécheresse, gel) à 50 % du rendement nominal |
| `RNG_YIELD_HIGH` | 1.5 | — | Facteur multiplicatif maximum : bonne saison à 150 % du rendement nominal |

### L'équilibre que ces valeurs produisent

À 10 hab/km², la production moyenne est de 18 kg/km²/tick et la consommation
de 20. Le monde démarre donc **au-dessus de ce qu'il nourrit** : la population
descend jusqu'à un régime où elle tient, et la variabilité `[0.5, 1.5]` crée
des ticks de surplus qui alimentent le commerce et des ticks de manque qui
créent de la dette.

C'est voulu — un monde qui démarre à l'équilibre exact ne montre ni famine ni
commerce. Aucun chiffre mesuré n'est cité ici : voir « Ce qui dit que le monde
vit », plus bas, et `python -m sim --ticks 20 --json` pour l'état du jour.

---

## Le déficit alimentaire et la mortalité

### Champ `food_deficit_kg`

La nourriture qui a manqué est une **dette**, pas un oubli. Sentinelle `-1.0`
= non encore calculé (règle 8 : zéro est une mesure réelle, jamais un aveu).

- Si `consommation > stock` après production et commerce :
  `food_deficit_kg += (consommation − stock)` et `food_stock_kg = 0`.
- Si la cellule a un surplus, la dette est remboursée par des kilogrammes
  réels — voir « La récupération physique du déficit ».

### La mortalité

```
si food_deficit_kg > 0 et population > 0 :
    deficit_par_tete = food_deficit_kg / population
    taux = min(deficit_par_tete × HUNGER_DEATH_SCALE, MAX_DEATH_RATE_PER_TICK)
    brut   = population × taux + mortality_remainder
    morts  = int(brut)
    mortality_remainder = brut − morts
    population = max(0, population − morts)
```

| Constante | Valeur | Unité | Ordre de grandeur |
|---|---|---|---|
| `HUNGER_DEATH_SCALE` | 0.005 | 1/(kg/personne) | 1 kg de dette par tête → 0,5 % de mortalité par tick. Une famine médiévale sévère est documentée à 10–30 % de mortalité annuelle sur les populations les plus touchées, soit 0,03–0,08 %/jour ; ce facteur permet à une dette de 5–10 kg/tête d'atteindre 2–5 % par jour. |
| `MAX_DEATH_RATE_PER_TICK` | 0.10 | — | Plafond de 10 % par tick : pas d'effondrement instantané, même à dette extrême. |

**Il n'y a pas de plancher `max(1, …)`.** Une famine légère ne tue plus au
moins une personne par cellule et par tick : le report de la fraction
(`mortality_remainder`, plus bas) fait ce travail correctement, sans inventer
de mort.

> Les formules antérieures — plancher de mortalité binaire, récupération de
> dette multiplicative `D × (1 − r)`, seuil de coupure `DEFICIT_ZERO_EPSILON` —
> ne sont plus décrites ici. Une formule morte décrite au présent piège le
> brief suivant. Elles sont dans l'historique git, avec les raisons de leur
> retrait dans les messages de commit.

---

## Ce qui dit que le monde vit

Il n'y a **pas de prédiction analytique** de la fraction de survivants. Il y a
trois propriétés, mesurées sur le moteur, dans `sim/tests/test_survie.py`.

**1. Le monde ne s'éteint pas et ne nourrit pas plus de monde qu'il ne produit.**

```
plafond = production_moyenne_du_monde / (ration × population_de_départ)
0 < fraction_de_survivants ≤ plafond
```

Le plafond est **dérivé du moteur** : `production_moyenne_kg_par_tick()` appelle
`production_kg()`, la même et unique formule que le tick emploie, avec le
rendement moyen au lieu d'un tirage. Il ne peut donc pas diverger de ce que le
monde produit — et il suivra tout seul le jour où le relief modulera le
rendement.

Ce que son dépassement voudrait dire : la population survivante mange plus que
le monde ne produit, donc des kilogrammes apparaissent ailleurs que dans la
production. Un commerce qui duplique, une consommation qui ne prélève pas, une
dette effacée sans surplus pour la payer : tout cela se voit ici.

**2. La survie répond à la mortalité.** `s(HDS×0.5) > s(HDS) > s(HDS×2)`.

**3. La survie répond à la nourriture.** `s(production) > s(production÷2)`.

### Pourquoi la direction, et pas la valeur

Le modèle précédent prédisait la valeur **absolue** de la fraction de
survivants : capacité de charge, oscillateur déficit/population, espérance du
manque, trois tolérances dérivées, horizon de 1 000 ticks. Il occupait 262 des
358 lignes de `sim/constants.py`.

Sa dérivation suppose **une** capacité de charge globale, `cap = F × ȳ / C`.
Cette grandeur cesse d'exister dès que la production varie d'une cellule à
l'autre — c'est-à-dire au prochain pas du modèle, le relief. Mesuré, en
faisant jouer le relief : la survie tombe à **0,447** contre une prédiction de
**0,797 ± 0,101** — 3,5 fois la tolérance. Le test devient rouge sans qu'aucun
défaut n'existe, et la seule issue commode est d'élargir la tolérance après
avoir vu la mesure. C'est la calibration après mesure, que ce document
interdisait ailleurs.

La garde payée par un vrai défaut est conservée intacte : le critère de survie
ne doit pas être **aveugle aux constantes qui gouvernent la mort** — c'est ce
que le brief 017 reprochait à celui du brief 013, où une famine deux fois plus
meurtrière passait le même contrôle. La propriété n° 2 la tient directement,
sur le moteur, et survit à tout changement du modèle de production. Rouge
prouvé : avec une mortalité qui ignore `HUNGER_DEATH_SCALE`, les trois régimes
rendent la même fraction et le test échoue.

### Sur les valeurs mesurées citées dans ce document

Elles sont datées et elles vieillissent. La règle 12 le dit pour les empreintes
de parité, et vaut ici : **un compteur se cite par son nom, pas par sa valeur.**
Avant ce lot, ce document affirmait, sur 200 ticks aux graines 42/42 :

| affirmé | mesuré au 2026-08-25 |
|---|---|
| 261 cellules affamées | 8 |
| 7 544 299 morts | 16 211 220 |
| 8 171 507 kg transportés | 4 503 375 |
| fraction de survie 0,887 | 0,757555 |

Les quatre étaient faux, de deux à trente fois. Ils dataient d'un moteur deux
révisions plus vieux, et ce document est celui qu'Hermes découpe en briefs.

Aucune valeur mesurée n'est donc citée ci-dessous comme une propriété du
modèle. Pour connaître l'état du monde : `python -m sim --ticks 20 --json`.

## Le commerce entre cellules

### Paramètre

| Constante | Valeur | Unité | Justification |
|---|---|---|---|
| `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK` | 200.0 × TICK_DURATION_DAYS | kg/arête/tick | Proxy : convoi à dos de mulet (capacité ~200 kg, une liaison rurale par jour, Pounds 1974) |

### SC2 brief 013 — Commerce atomique, snapshot et allocation déterministe

**Définition du besoin et du surplus au moment du commerce** (commerce avant consommation) :

Lorsque le commerce précède la consommation, le « besoin » d'une cellule pour
ce tick n'est plus `food_deficit_kg` (déficit cumulé des ticks précédents) mais
le **manque prévisible du tick courant** :

```
besoin(c)  = max(0, population_c × FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK - food_stock_kg_c)
surplus(c) = max(0, food_stock_kg_c - population_c × FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK)
```

Ces valeurs sont calculées sur le snapshot pris avant tout transfert.

**Algorithme en deux passes** :

1. Snapshot immuable : `{cell_id: (food_stock_kg, population)}` pour toutes les cellules, avant modification.
2. Calcul des transferts à partir du snapshot uniquement.
3. Application de tous les transferts en une seule passe finale.

Une cellule qui vient de recevoir de la nourriture sur une arête ne peut pas
en redistribuer sur une autre arête du même tick (transport atomique).

**Allocation déterministe en cas de demandes concurrentes** :

Si plusieurs cellules en besoin sont adjacentes à la même source, l'allocation est :
- Proportionnelle à leurs besoins respectifs (calculés depuis le snapshot).
- Traitée dans l'ordre stable des `cell_id` croissants.
- Chaque transfert est borné par `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`.
- Si la somme des demandes dépasse le surplus de la source, chaque receveur
  reçoit `surplus_source × (besoin_i / total_besoin)`, plafonné à la capacité.

**Invariant** : `food_deficit_kg` n'est jamais modifié par le maillon commerce
(SC1 brief 013). Seul `food_stock_kg` est mis à jour.

**Écrêtage côté receveur** (N3 feedback 001, itération 2) :

Chaque source alloue proportionnellement à ses receveurs en fonction du surplus
snapshot. Mais une cellule adjacente à plusieurs sources en surplus pouvait
recevoir plus que son besoin (chaque source lui allouait son besoin entier).
Exemple détecté par l'Évaluateur : cellule avec besoin 200 kg, adjacente à deux
sources en surplus de 200 kg → recevrait 400 kg.

Correctif : après l'allocation proportionnelle par source (passe 1c), une passe
supplémentaire (passe 1d) plafonne le total reçu par chaque cellule à son besoin
snapshot. Si le total entrant excède ce besoin, toutes les livraisons entrantes
sont réduites proportionnellement (l'excédent reste aux sources).

Ce changement conserve la masse (rien n'est créé), réduit le transport effectif,
et change les compteurs du monde réel (SC6 re-mesuré en conséquence).

---

## Le report de la fraction de mortalité

`int(population × death_rate)` arrondit à zéro dès que
`population × death_rate < 1`. Une cellule de 5 habitants en famine totale
produit `5 × 0.10 = 0.5` mort par tick : `int(0.5) = 0`, à chaque tick, pour
toujours. Cinq habitants deviennent immortels par arrondi, tandis que leurs
voisins de 5 000 habitants meurent normalement.

Le champ `Cell.mortality_remainder` (float, sentinelle `-1.0` = non calculé)
conserve la fraction non appliquée :

```py
remainder = cell.mortality_remainder if cell.mortality_remainder >= 0.0 else 0.0
raw = cell.population * death_rate + remainder
deaths = int(raw)
cell.mortality_remainder = raw - deaths
cell.population = max(0, cell.population - deaths)
```

**Borne `N_BOUND_MORT`** : au plafond de mortalité, une cellule accumule au
moins `MAX_DEATH_RATE_PER_TICK` mort par habitant et par tick ; il faut donc au
plus `ceil(1 / MAX_DEATH_RATE_PER_TICK) = 10` ticks pour qu'une mort entière
soit appliquée, quelle que soit la taille de la cellule.

---

## Ce que veut dire « affamée »

L'ancien critère incrémentait `hunger_ticks` quand `food_stock_kg <= 0` après
consommation. Une cellule ravitaillée **exactement** à son besoin par le
commerce termine le tick avec un stock nul et un déficit nul : elle a mangé sa
ration. La compter comme affamée confond le garde-manger vide et la
sous-alimentation.

Nouveau critère causal : `_apply_consumption` retourne la pénurie du tick en kg
(`shortage = besoin − stock_avant_consommation`, nulle s'il n'y a pas de
manque) et `_update_hunger` n'incrémente que si cette pénurie est positive.
Propriété : `food_stock_kg == 0` et `food_deficit_kg == 0` après consommation
→ `hunger_ticks` non incrémenté.

---

## La récupération physique du déficit

`DEFICIT_RECOVERY_RATE_PER_TICK` (brief 013) est **supprimée**. Sa formule,
`food_deficit_kg × (1 − r)`, effaçait 10 % de la dette indépendamment du
surplus réel : un surplus d'un nanogramme effaçait 1 000 kg d'une dette de
10 000 kg. Des kilogrammes disparaissaient sans contrepartie physique
(principe 3 : rien ne se téléporte).

Successeur nommé : `DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG = 1.0` — kilogrammes
de dette remboursés par kilogramme de surplus **réellement consommé** au-delà
du besoin d'entretien. Ratio 1:1.

```py
remboursement = min(food_deficit_kg, surplus_du_tick × ratio)
food_deficit_kg -= remboursement
food_stock_kg = surplus_du_tick − remboursement    # les kg quittent le stock
```

Le ratio est borné à 1.0 dans le moteur : la réduction de la dette ne peut
jamais dépasser le surplus physique du tick, quelle que soit la valeur donnée à
la constante.

**La coupure `DEFICIT_ZERO_EPSILON` est supprimée.** Elle n'avait plus de
travail. Le remboursement est une **soustraction** : `dette − min(dette,
surplus × ratio)`. Quand le surplus couvre, `min` rend la dette elle-même et
la soustraction donne **exactement `0.0`** en IEEE 754 — il n'y a pas d'asymptote
à nettoyer. Tout résidu est donc une dette réelle que le surplus n'a pas payée,
et l'effacer faisait disparaître des kilogrammes sans contrepartie : la même
faute de principe 3 que le seuil avait été écrit pour accompagner.

Mesuré sur 1 000 ticks du monde réel (596 000 passages du maillon
consommation) : 9 147 remboursements sur dette, dont 1 536 à résidu nul exact,
7 611 à résidu réel — et **zéro** dans l'intervalle que la coupure effaçait.

**Conséquence assumée** : la dette se rembourse vite dès qu'il y a un vrai
surplus, et pas du tout quand le surplus est infime.

---

## La province dérivée et ses centres

Cette section décrit d'où viennent les données de l'agrégation, comment elle
calcule, et ce qu'elle refuse de faire. Elle est écrite avant toute citation
d'un compteur mesuré du lot 018 : une justification rédigée après la mesure
serait une calibration déguisée.

### Provenance : des données héritées du jeu, pas des frontières de 1400

Les centres administratifs sont lus dans
`data/province-centres-1400.json`. Ce sont des
**données héritées du jeu**, reprises telles quelles et en lecture seule.

Ce ne sont **pas** des frontières historiques de 1400. Rien ici ne prétend au
statut de source savante, de reconstitution d'époque, ni de découpage
administratif attesté. Le fichier lui-même se décrit comme des
« coordonnées approximatives, corrigeables à vue ». Ces centres sont un
**proxy** : un point de départ commode pour éprouver le mécanisme
d'agrégation, destiné à être remplacé par une source documentée quand le
projet en aura une. Leur nombre n'est pas recopié ici : il est celui du
tableau `coordinates` du fichier, lu à chaque exécution.

De même, le nombre de cellules du monde n'est écrit nulle part dans le code :
il est lu de `data/world-1400.json` et dérivé
du chargement par `World.charger()`.

### Projection : celle que le fichier documente lui-même

Le fichier de centres déclare sa propre projection sous la clé `projection` :
équirectangulaire, `x = lon × cos(mid_latitude)`, `y = −lat`. C'est cette
projection qu'emploie `sim/aggregation.py`, et son paramètre
`projection.mid_latitude` est **lu du fichier** par
`charger_latitude_moyenne()`. Aucune valeur de latitude moyenne n'apparaît
comme littéral dans un corps de fonction — `sim/tests/test_no_hardcoded.py`
parcourt récursivement les modules de `sim/` hors tests et refuse tout
littéral numérique autre que 0, 1 et −1.

Les distances sont comparées **au carré** : même ordre que la distance, sans
racine carrée. La conversion des degrés en radians passe par la bibliothèque
standard (`math.radians`), jamais par un facteur recopié à la main.

### Règle de départage des égalités (D4)

Une cellule relève du centre le plus proche d'elle. Si deux centres ou plus
sont à distance **exactement** égale, la cellule relève de celui dont l'`id`
est le **plus petit**.

Cette règle est stable : elle ne dépend pas de l'ordre dans lequel les centres
sont parcourus. La comparaison retenue est
`carré < meilleur` **ou** (`carré == meilleur` **et** `id < meilleur_id`). Un
simple « le premier rencontré gagne » donnerait le même résultat dans un ordre
de parcours et un autre résultat dans l'ordre inverse : le déterminisme serait
espéré, pas prouvé. `sim/tests/test_determinisme.py` monte le
cas d'égalité exacte et l'essaie dans les deux ordres.

### Refus de deviner (D5)

Si une cellule chargée par `World.charger()` n'a pas de position dans les
artefacts géographiques, le code lève `PositionCelluleInconnue` en **nommant
la cellule**. Il n'attribue pas de province par défaut et n'écarte pas la
cellule en silence : une couverture obtenue en jetant les cellules gênantes
n'est pas une couverture. Quand une donnée manque, l'absence se déclare — elle
ne s'invente pas.

### Zéro mesuré contre sentinelle « non calculé »

Le compteur `cellules_sans_province` doit valoir **0**. Ce zéro est une
**mesure réelle** : le code a bien regardé chaque cellule chargée et n'en a
trouvé aucune sans province. La sentinelle « non calculé » du projet est
`-1`, jamais `0` ; un `0` rapporté ici affirme donc quelque chose, il n'avoue
pas une absence de mesure. La même distinction vaut pour
`cellules_position_absente`, `attributs_dynamiques_sur_cellules` et
`egalites_de_distance_monde_reel` : ce dernier peut légitimement valoir zéro
sans que cela signifie qu'on ne l'a pas calculé.

### Provinces peuplées : un fait mesuré, pas un plancher

Toute cellule relève d'une province ; l'inverse n'est pas exigé. Un centre
peut n'attirer aucune cellule. Le nombre de provinces peuplées est donc
rapporté tel qu'il sort de la mesure, avec le nombre de centres lus pour
dénominateur. Aucun test n'impose de plancher, et l'algorithme n'est en aucun
cas ajusté pour peupler tous les centres.

### Ce que l'agrégation ne fait pas

Elle ne modifie aucun objet reçu, n'écrit aucun fichier, et n'ajoute aucun
champ à `Cell`. La vue dérivée (`Regroupement`) vit dans `sim/aggregation.py`,
hors de `sim.model`, parce que `sim.model` contient les entités **persistées**
que le moteur fait évoluer : y déclarer la Province inviterait à la traiter
comme un état stockable, exactement ce que l'ADR-0003 interdit. Le pas de
temps (`tick`) ne consomme pas l'agrégation : la Province est ici une vue du
monde, pas encore un acteur économique.

---

## Référence de code

Tous les paramètres ci-dessus sont définis comme constantes nommées dans
`sim/constants.py`. Aucun littéral numérique de ces valeurs n'apparaît dans
les fonctions de calcul de `sim/engine.py` ou `sim/world.py` (vérifiable
via `sim/tests/test_no_hardcoded.py`).
