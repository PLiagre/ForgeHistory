# Brief 055 — Le monde nourrit ceux qu'il amorce

## But

Dériver la population amorcée d'une cellule de ce que cette cellule
produit, pour que le plafond physique de survie — déjà calculé par le
moteur — soit atteignable au tick zéro, au lieu d'être franchi dès
l'amorçage.

État de départ à mesurer :

```bash
python3 -m pytest sim/tests/test_survie.py -k nourrit_pas_plus -q -s
python3 -m sim --ticks 60 --seed 0 --json
python3 -m sim --ticks 200 --seed 0 --json
rg -n 'INITIAL_POPULATION_PER_KM2' sim/
```

Le premier test imprime `plafond derive` : c'est
`production_moyenne_kg_par_tick(monde)` divisé par la ration du monde
amorcé. Ce lot est caduc si ce plafond est **supérieur ou égal à un** sur
la carte figée, ou si la population d'une cellule est déjà dérivée de sa
production. Un plafond inférieur à un dit que le monde amorce plus de
bouches que sa terre n'en nourrit, à rendement moyen, toute l'année.

Le second fait qualitatif à retrouver, cellule par cellule : rapporter
la production moyenne d'une cellule à la ration de ses habitants. Sur la
base, **aucune cellule du monde n'a d'excédent** — la mieux dotée atteint
tout juste sa ration, sans marge, et toutes les autres sont en dessous.
Ce qui ressemble à un grenier sur la carte n'est donc pas une province en
excédent : c'est la province qui meurt en dernier.

Le fait qualitatif qui rend le lot nécessaire : aujourd'hui
`_seed_population` ne lit que `area_km2`. La densité amorcée ne connaît
ni le rendement, ni le relief, ni la durée du jour ; elle est la même
partout à un tirage de plus ou moins dix pour cent près, et ce tirage
n'est pas une géographie. Deux nombres — la densité amorcée et le
rendement au kilomètre carré — ont été choisis chacun de leur côté, et
personne ne les a jamais multipliés l'un par l'autre.

## Règle du monde

Ce lot découle de `sim/MODELE.md` § « Population initiale par cellule »,
dont il remplace la formule et dont il supprime la conséquence déclarée
« le monde démarre plat ». Il lit sans les changer § « Le rendement
agricole et sa variabilité » (les trois régimes de production, le
rendement moyen du tirage) et § « Ce qui dit que le monde vit ».

**Fidélité niveau 2** pour le taux d'occupation. Le reste est de la
conservation : la capacité nourricière n'est pas un nouveau modèle, c'est
la formule de production déjà écrite, lue à rendement moyen.

### Une cellule porte autant d'habitants qu'elle en nourrit

```
capacite_hab   = production_moyenne_par_tick(cellule) / consommation_par_habitant_par_tick
population     = max(0, int(capacite_hab × TAUX_OCCUPATION_INITIAL × variation))
```

`production_moyenne_par_tick(cellule)` est **l'unique formule de
production du moteur**, appelée au rendement moyen du tirage
(`rendement_moyen_courant()`) et au facteur saisonnier moyen sur l'année
— exactement ce que `production_moyenne_kg_par_tick` emploie déjà pour
dériver le plafond de survie. Elle porte donc le relief, la durée du jour
et la part minière de la cellule. Aucune seconde formule n'est écrite :
si le semis calculait la production autrement que le tick, le plafond
cesserait de vouloir dire quelque chose, et c'est précisément le défaut
que ce lot répare.

`variation` reste le tirage existant entre `SEED_POPULATION_VARIATION_LOW`
et `SEED_POPULATION_VARIATION_HIGH`, sur le même `rng` amorcé par la
graine : le déterminisme ne change pas de nature.

`INITIAL_POPULATION_PER_KM2` **disparaît de `sim/constants.py`**. Ce
n'est pas un renommage : c'est le retrait du nombre qui ne pouvait pas
connaître le rendement. Tant qu'il existe, quelqu'un le rééquilibrera un
jour à la main contre une mesure, et ce sera une calibration déguisée.

### Le seul paramètre décidé d'avance

`TAUX_OCCUPATION_INITIAL` : la part de sa capacité qu'une cellule porte à
l'amorçage. **Ordre de grandeur plausible : 0,65** — le monde de 1400
n'est pas à son plafond malthusien, la peste de 1348 l'a vidé et il ne
retrouvera son niveau d'avant qu'au milieu du XVIᵉ siècle. Il est décidé
ici, avant l'exécution ; il ne se règle pas après avoir regardé un
résultat.

Il doit être strictement supérieur à zéro et strictement inférieur ou
égal à un. Une valeur hors de ces bornes est refusée au chargement, avec
la valeur reçue dans le message : un taux supérieur à un amorcerait de
nouveau un monde qui ne peut pas se nourrir, ce qui est le défaut même
que ce lot ferme.

### Ce que la règle produit, et qu'il faut attendre

- **Le monde cesse d'être plat.** La densité devient une conséquence du
  relief, de la durée du jour et de la part minière. Une haute montagne
  porte une fraction de ce que porte une plaine, sans qu'aucune règle ne
  le dise : la production le disait déjà.
- **Aucune cellule ne démarre en excédent ou en déficit structurel.**
  Toutes partent à la même fraction de leur propre capacité. Une province
  de plaines cesse d'être un grenier par accident de relief, et une
  province de montagnes cesse d'être condamnée à l'amorçage.
- **La population totale du monde change**, et ce n'est pas un défaut à
  corriger dans ce lot : elle devient la conséquence du rendement au
  kilomètre carré. Le niveau absolu se règle par
  `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK`, qui est un paramètre de niveau 2
  avec sa propre justification. Ancrer les populations sur ce que
  l'histoire dit des pays de 1400 est un autre lot, et il suppose
  celui-ci.
- **La disette ne disparaît pas.** Le tirage de rendement, le creux
  saisonnier et le relief d'une voisine continuent de produire des
  pénuries locales. C'est la famine qu'on veut : une mortalité de famine
  qui emporte la majorité du monde en deux mois n'en est pas une, c'est
  un amorçage faux.

## Périmètre

En écriture : `sim/constants.py`, pour retirer `INITIAL_POPULATION_PER_KM2`,
déclarer `TAUX_OCCUPATION_INITIAL` et la consultation qui le relit ;
`sim/world.py`, pour le semis dérivé et le refus d'un taux hors bornes ;
`sim/MODELE.md`, uniquement la section « Population initiale par cellule » ;
`sim/tests/test_survie.py`, uniquement pour **ajouter** les cas qui
protègent le plafond au semis et la forme du semis — c'est le fichier qui
porte déjà l'invariant du plafond ; `sim/tests/test_determinisme.py`,
uniquement pour ajouter le cas qui protège le déterminisme du nouveau semis.

Tout autre chemin est interdit, nommément : `sim/engine.py`,
`sim/model.py`, `sim/aggregation.py`, `sim/snapshot_export.py`,
`sim/__main__.py`, `sim/README.md`, `sim/tests/test_monde.py`,
`sim/tests/test_commerce.py`, `sim/tests/test_province.py`,
`sim/tests/test_no_hardcoded.py`, `sim/tests/test_write_coverage.py`,
`data/`, `viewer/`, `visualisateur/`, `chronique/`, `outils/`, `.github/`,
`VISION.md`, `AGENTS.md`, `ROADMAP.md` hors la fiche 055, `atelier.toml`
et les autres briefs. Aucun test existant n'est modifié, renommé,
supprimé ou relâché.

Le semis lit la formule de production de `sim/engine.py` ; il ne la
recopie pas et ne la modifie pas. `sim/world.py` importe donc de
`sim/engine.py`, qui n'importe pas `sim/world.py` : aucun cycle n'est
créé, et cette absence de cycle est à vérifier plutôt qu'à supposer.

## Conditions de succès

Chaque commande ciblée doit collecter les nouveaux cas nommés ; zéro test
sélectionné est un échec, jamais une preuve. Tout dénominateur vient du
monde réellement chargé — la carte figée — et jamais d'un nombre recopié
dans ce brief. La base est le dernier `master` au démarrage du lot.

### SC1 — Le monde peut nourrir ceux qu'il amorce

Commande : `python3 -m pytest sim/tests/test_survie.py -k plafond_au_semis -q`.

Charger le monde réel, exiger des cellules, et vérifier que
`production_moyenne_kg_par_tick(monde)` est supérieure ou égale à
`FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK × population_amorcée`. Les deux
membres sont mesurés sur le monde chargé ; aucun n'est écrit ici.

**Rouge d'abord** : sur la base, ce rapport est inférieur à un, et le cas
échoue. C'est le défaut que le lot ferme, et c'est la seule preuve qui
compte.

### SC2 — Le semis n'est plus plat

Commande : `python3 -m pytest sim/tests/test_survie.py -k semis_suit_la_terre -q`.

Grouper les cellules du monde chargé par classe de relief et comparer les
densités médianes obtenues. L'écart entre la classe la plus dense et la
moins dense doit dépasser ce que le seul tirage peut produire — cette
borne se dérive de `SEED_POPULATION_VARIATION_HIGH` divisé par
`SEED_POPULATION_VARIATION_LOW`, jamais d'un nombre écrit à la main.
L'échantillon doit contenir au moins deux classes de relief peuplées ;
un échantillon d'une seule classe échoue.

**Rouge d'abord** : sur la base, les densités par classe sont égales à
la variation près, et l'écart reste sous la borne.

### SC3 — Aucune cellule ne démarre en excédent structurel

Commande : `python3 -m pytest sim/tests/test_survie.py -k semis_sans_excedent -q`.

Pour chaque cellule du monde chargé, calculer le rapport entre sa
production moyenne par tick et sa ration. Tous ces rapports doivent
tomber dans l'intervalle que le tirage autorise autour de
`1 / TAUX_OCCUPATION_INITIAL` : aucune cellule n'est structurellement
mieux ou moins bien nourrie qu'une autre à l'amorçage. Les bornes se
dérivent du taux et des deux constantes de variation.

**Rouge d'abord** : sur la base, l'intervalle est largement débordé — une
plaine et une haute montagne ne sont pas nourries dans le même rapport,
puisqu'elles portent la même densité et ne produisent pas la même chose.

### SC4 — Le monde ne perd pas plus qu'il ne peut nourrir

Commande : `python3 -m pytest sim/tests/test_survie.py -k pas_d_effondrement -q`.

Jouer le monde réel sur une année calendaire complète, avec la graine de
`test_survie.py`, et exiger que la fraction de survivants soit supérieure
ou égale au minimum entre un et le plafond dérivé du monde amorcé. Les
deux termes sont mesurés ; ni la durée ni la fraction attendue ne sont
écrites ici — la durée se dérive de `CALENDAR_DAYS_PER_YEAR` et de
`TICK_DURATION_DAYS`.

**Rouge d'abord** : sur la base, le plafond vaut moins d'un et la fraction
survivante lui est très inférieure. Le cas échoue par les deux bouts.

Si ce cas reste rouge après un semis conforme aux SC1 à SC3, **ne pas
relâcher le critère** : le rapporter tel quel dans la PR. Une perte que
le semis n'explique plus est un second défaut, dans la dette, la natalité
ou le commerce, et il appartient à un autre lot.

### SC5 — Le nombre qui ignorait le rendement n'existe plus

```bash
rg -n 'INITIAL_POPULATION_PER_KM2' sim/ ; test $? -eq 1
python3 -m pytest sim/tests/test_write_coverage.py -q
```

Aucune occurrence dans `sim/`, y compris dans `sim/MODELE.md`. Les gardes
existantes restent vertes sans être touchées : `TAUX_OCCUPATION_INITIAL`
doit changer le monde quand on le remplace en mémoire, sinon
`test_chaque_constante_du_moteur_change_le_monde` le dira, et aucun
littéral nu n'entre dans le semis, sinon `test_no_hardcoded` le dira.

### SC6 — Même graine, même semis

Commande : `python3 -m pytest sim/tests/test_determinisme.py -k semis -q`.

Deux chargements à la même graine donnent des populations identiques
cellule par cellule ; deux graines différentes donnent des populations
différentes sur un échantillon non vide. Un taux d'occupation remplacé en
mémoire change les populations, et les restaure quand il est remis :
la consultation relit la constante à chaque appel.

### SC7 — Un taux hors bornes est refusé avant le semis

Commande : `python3 -m pytest sim/tests/test_survie.py -k taux_refuse -q`.

Remplacer en mémoire le taux par zéro, par un négatif, par une valeur
supérieure à un, par un booléen et par un non-nombre : chacun lève une
erreur nommée au chargement, avec la valeur reçue dans le message, et
aucun monde n'est construit. La garde est posée **avant** la boucle de
semis, et sa sensibilité se prouve en la déplaçant temporairement après,
dans une copie privée.

### SC8 — Le reste du moteur ne bouge pas

```bash
python3 -m pytest sim/tests/ viewer/tests/ -q
git diff --exit-code origin/master -- sim/engine.py sim/model.py sim/snapshot_export.py viewer/ data/
```

Toute la suite reste verte. En particulier, les cas qui mesurent la
**direction** de la réponse démographique — plus la faim tue, moins il
reste de monde ; la natalité fait remonter la survie — doivent rester
verts et rester sensibles : un monde qui ne meurt plus du tout les
rendrait aveugles, et une réponse qui devient du bruit est un échec, pas
une amélioration. Le rapporter dans la PR si c'est le cas.

## Hors périmètre

- ancrer les populations sur ce que l'histoire dit des pays de 1400 ; ce
  lot rend le monde cohérent avec lui-même, pas conforme à une source ;
- changer `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK`, la consommation, la
  réserve initiale, la mortalité, la natalité ou la migration ;
- la part cultivable d'une cellule, les sols, les rivières, les villes,
  et toute autre entrée de rendement que le relief et la durée du jour ;
- la dette qu'une cellule vidée ne rembourse jamais, et une cellule à
  population nulle qui ne peut plus repeupler : mesuré, réel, et pas dans
  ce lot ;
- le semis du stock alimentaire, qui reste la réserve en ticks existante ;
- le snapshot, les vues, la chronique et la page de pilotage.
