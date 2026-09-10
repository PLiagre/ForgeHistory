# Cohérence du produit face à la vision

Mesure initiale du 9 septembre 2026 sur `3f5045df7b866058efad747e8fd74c7b9372841e`,
puis rejeu sur `master` à la révision
`78b384d4b904dcb2d4cb843979bf18347f623236`, après fusion des six briefs.
Rapport finalisé le 10 septembre 2026.
Python 3.12.14 ; tests avec pytest 9.1.1 ; numpy 2.5.3 et Pillow 12.3.0
pour le visualisateur. `forge3d` n'est pas installé dans l'environnement
mesuré. Le moteur et le regard mince utilisent la bibliothèque standard.

Le produit mesuré est une simulation de cellules, avec agriculture,
extraction, commerce terrestre et maritime, faim et démographie. La province
et le bourg sont des consultations dérivées. Ce n'est pas encore la
hiérarchie d'individus, de familles, de villes et d'États promise.

Les briefs 049 à 054 sont fusionnés ; les implémentations 049 à 053
ne le sont pas sur cette révision.
Leurs effets ne sont donc pas comptés comme réalisés ici. Ce rapport ne
remplace pas le registre des lots de `ROADMAP.md`.

`mesure` désigne le comportement effectivement éprouvé ; `partiel` en borne
la partie présente ; `absent` résulte de la lecture du chemin d'exécution
et de la recherche N1 ; `non_verifie` conserve une preuve non obtenue.
Les références P1 à P6 et N1 renvoient aux commandes ci-dessous, exécutées sur les révisions précisées dans les preuves. Aucun pourcentage d'achèvement n'est
calculé.

## Assertions liminaires

| ID et promesse | État | Code ou recherche | Preuve | Limite ou contradiction |
|---|---|---|---|---|
| L01 — « Le moteur tourne dans sim/ » | mesure | `sim/__main__.py::_simulate` | P1, P2 | Exécution bornée par `--ticks`, sans rendu. |
| L02 — « viewer/ est un regard mince » | mesure | `snapshot_export.build_snapshot_document`, `snapshot_loader.agregats_monde` | P3, P4 | Il lit une photographie, pas un monde qui avance en direct. |
| L03 — « Il n'y a pas de moteur de rendu » | partiel | `visualisateur/rendu.py::rendre_png` | P6, N1 | Un adaptateur vers forge3d existe déjà : la note de statut est périmée. Le rendu GPU n'a pas été vérifié ici. |
| L04 — « amorcé historiquement » | partiel | `world._seed_population`, `_seed_food_stock`, `constants.INITIAL_POPULATION_PER_KM2` | P1, P2, N1 | Carte figée et centres présents ; populations tirées d'une densité et d'un aléa, réserves dérivées de rations. Aucune preuve de population historique à t0. `MODELE.md` § Déclaration explicite le reconnaît. |

## Promesses de la vision

source : Ce que nous construisons

| ID et promesse | État | Code ou recherche | Preuve | Limite ou contradiction |
|---|---|---|---|---|
| C01 — « moteur de simulation historique » | partiel | `engine.tick`, `world.charger` | P1, P2 | Un moteur causal cellulaire tourne. La fidélité historique de l'amorçage n'est pas établie ; voir L04. |
| C02 — « gameplay émerge » | partiel | `_apply_consumption`, `_apply_mortality`, `_apply_natalite`, `_apply_migration` | P2 | Pénurie, dette, décès, naissances et départs ont des effets éprouvés. Pas de campagne jouable ni preuve d'intérêt pour le joueur. |
| C03 — mécaniques de stratégie et batailles tactiques | absent | N1 ; `engine.tick` n'appelle que les mécanismes économiques et démographiques | N1 | Ni États, ni armées, ni couche de bataille. La comparaison à des jeux est une direction, pas une fonctionnalité mesurée. |

source : Principe fondateur : une seule simulation

| ID et promesse | État | Code ou recherche | Preuve | Limite ou contradiction |
|---|---|---|---|---|
| S01 — « monde entier simulé en permanence » | partiel | `World.cells`, `engine.tick` | P1, P4 | Toutes les cellules chargées sont parcourues ; le monde est une carte bornée, le lanceur s'arrête après les ticks demandés. Pas de service persistant démontré. |
| S02 — « une seule source de vérité » | mesure | `Cell.cell_id`, `World`, accès au panier | P2, P3 | Pour le périmètre implémenté : pas de seconde clé spatiale ni de province stockée sur les cellules. Ne préjuge pas des couches absentes. |
| S03 — vues monde, province, ville, quartier, bataille | partiel | `aggregation`, `viewer`, `visualisateur` | P3, P4, P6 | Monde et provinces lisibles ; bourg calculable mais absent du snapshot. Quartiers et batailles absents. |
| S04 — aucune seconde base stratégique/tactique | partiel | `snapshot_export`, `snapshot_loader`, N1 | P3, P4 | Les vues lisent le même export sans logique physique parallèle. Aucune couche tactique à confronter ; une photographie n'est pas une base mutable du moteur. |

source : Philosophie

| ID et promesse | État | Code ou recherche | Preuve | Limite ou contradiction |
|---|---|---|---|---|
| F01 — raisonner en termes de monde | partiel | consommation → dette → mortalité ; pénurie → migration | P2 | Chaînes physiques présentes, mais agents individuels absents. |
| F02 — faim → recherche → vol → criminalité | absent | N1 ; `Cell` ne porte ni recherche, ni vol ; aucun maillon correspondant | N1 | L'exemple de causalité n'est pas simulé. La faim ne suffit pas à le valider. |
| F03 — règles générales, systèmes réutilisables, données | partiel | panier générique, constantes nommées, dérivation des arêtes | P2 ; tests `test_no_hardcoded_numeric_literals`, `test_acces_directs_au_panier_hors_modele` | Contrôles de structure et de sensibilité, pas certification de toute mécanique future. |
| F04 — vérifier émergence contre règle codée en dur | partiel | `sim/tests/test_write_coverage.py`, `test_no_hardcoded.py` | P2 | Ces gardes détectent des paramètres inertes et littéraux. Elles ne remplacent pas le jugement de conception. |

source : Échelles de simulation

| ID et promesse | État | Code ou recherche | Preuve | Limite ou contradiction |
|---|---|---|---|---|
| E01 — Monde → Pays → Province → Ville → Quartier → Bâtiment → Famille → Personne | partiel | `World`, `Cell`, `Regroupement`, `RepartitionBourg` | P3, P4, N1 | Monde cellulaire, province dérivée, partage bourg/champs. Pays et niveaux individuels absents ; un bourg n'est pas une ville autonome. |
| E02 — chaque niveau simulable indépendamment | absent | N1 ; seule `engine.tick` fait évoluer les cellules | N1 | Aucune boucle de simulation pour pays, quartiers, familles ou personnes. |
| E03 — détail augmenté ou diminué selon le contexte | absent | N1 ; `World.charger`, `engine.tick`, `aggregation` inspectés | N1 | Aucun changement de granularité ni désagrégation des habitants. |
| E04 — agrégation et désagrégation conservatives | partiel | `agregat_depuis_monde`, `bourg_depuis_monde` | P3, P4 | Couverture des provinces et somme bourg + champs éprouvées ; aucune désagrégation à éprouver. |

source : Les piliers

| ID et promesse | État | Code ou recherche | Preuve | Limite ou contradiction |
|---|---|---|---|---|
| P-GEO — géographie | partiel | `World.charger`, `data/world-1400.json` | P1, P4 | Carte, géométries, adjacency chargées ; pas de validation géographique historique complète ici. |
| P-CLI — climat | partiel | `_apply_production`, fonctions climatiques de `constants.py` | P2 | Paramètres de carte consommés, climat non évolutif. |
| P-SAI — saisons | mesure | `constants.jour_de_tick`, `_apply_production_saison_moyenne`, `tick` | P2 ; tests annuels de `test_monde.py` | Trois régimes distincts : nominal sans carte, moyenne sans numéro, jour avec numéro. La saison n'est pas une horloge portée par le monde. |
| P-RES — ressources naturelles | partiel | `_apply_extraction` | P2, P4 | Les gisements produisent leur ressource, avec bras et richesse ; pas d'épuisement du sous-sol. |
| P-POP — population | partiel | `Cell.population`, consommation, mortalité, natalité, migration | P2, P4 | Compteurs entiers par cellule ; pas de personnes individuelles. |
| P-FAM — familles | absent | N1 ; `sim/model.py::Cell` est la seule entité persistée | N1 | Ni appartenance familiale ni patrimoine familial. |
| P-ECO — économie | partiel | stocks, production, extraction, consommation | P2 | Économie alimentaire physique et minerai local ; pas de prix, revenus ou fabrication sur cette base. |
| P-COM — commerce | partiel | `_apply_commerce`, `stocks_mer` | P2 ; conservation avec bassin | Nourriture échangée par terre et mer ; aucune demande non alimentaire. |
| P-INF — infrastructures | partiel | adjacences, capacités et façades maritimes | P2, P4 | Supports de flux fixes ; pas de routes ou ports construits et entretenus par des habitants. |
| P-POL — politique | absent | N1 ; aucun état politique ni maillon | N1 | Les provinces sont des regroupements, pas des pouvoirs simulés. |
| P-DIP — diplomatie | absent | N1 | N1 | Aucun acteur étatique ni relation diplomatique. |
| P-REL — religion | absent | N1 | N1 | Aucun attribut ni évolution religieuse. |
| P-CUL — culture | absent | N1 | N1 | Aucun attribut ni évolution culturelle. |
| P-TEC — technologie | absent | N1 | N1 | Aucune recherche ni diffusion technologique. |
| P-ARM — armées | absent | N1 | N1 | Aucune entité militaire, recrutement ou perte militaire. |
| P-LOG — logistique | partiel | capacités terrestres, `_appliquer_flux_maritimes`, stocks de bassin | P2, P4 | Flux alimentaires et stockage intermédiaire ; pas de convois, trajet en mer ou ravitaillement militaire. |
| P-URB — urbanisation | partiel | `RepartitionBourg`, `bourg_depuis_monde` | P3, P4 | Part non agricole dérivée, pas de construction ni d'investissement. |
| P-IND — industrie | absent | N1 ; extraction sans transformation dans `engine.tick` | P4 : aucun objet dans les cellules | Le lot 049 propose la première fabrication ; il n'est pas le produit mesuré. |
| P-AUT — « chaque système évolue indépendamment » | partiel | maillons séparés de `engine.py` | P2 | Appel isolé possible pour les mécanismes présents ; climat et carte sont fixes, plusieurs systèmes n'existent pas. |

source : Règles par domaine

| ID et promesse | État | Code ou recherche | Preuve | Limite ou contradiction |
|---|---|---|---|---|
| D01 — personne → famille → habitation, patrimoine, revenus, culture, religion, profession | absent | N1 ; champs de `Cell` inspectés | N1 | La part minière est un calcul agrégé, pas une profession individuelle ni un revenu. |
| D02 — villages, villes et États émergent des familles | absent | N1 ; bourg calculé depuis les gisements et la population | P3, N1 | Les familles n'existent pas ; une agrégation ne démontre pas cette émergence. |
| D03 — origine, transport, stockage, destination de chaque ressource | partiel | production/extraction, panier, `_apply_commerce`, consommation | P2, P4 | Chaîne complète pour nourriture, bassin inclus. Minerai extrait et stocké mais sans consommateur ; distribution interne à la cellule gratuite. |
| D04 — rupture logistique → conséquences naturelles | mesure | `_apply_consumption`, faim, dette, mortalité | P2 ; cas de capacité insuffisante dans `test_commerce.py` | Mesuré pour subsistance et transport alimentaire, pas pour les autres chaînes absentes. |
| D05 — soldats issus de population, approvisionnés, morts et retour civil | absent | N1 | N1 | Aucun lien armée/population ni perte militaire ; la mortalité alimentaire n'en est pas une preuve. |
| D06 — habitants construisent, investissent et déménagent | partiel | `_apply_migration` ; N1 | P2 | Déplacement agrégé par famine uniquement. Pas de bâtiment, investissement ni action de joueur créant les conditions. |
| D07 — quartiers évoluant avec richesse, industrie, transport, sécurité, population | absent | N1 | N1 | Aucun quartier ni état de sécurité. |
| D08 — bataille sur même terrain, mêmes armées et pertes renvoyées au monde | absent | N1 | N1 | Pas de couche de bataille ; le terrain rendu ne prouve aucune interaction tactique. |

source : Architecture en couches

| ID et promesse | État | Code ou recherche | Preuve | Limite ou contradiction |
|---|---|---|---|---|
| A01 — Core → Monde → Population → Économie → Politique → Militaire → Présentation | partiel | `sim`, `viewer`, `visualisateur`, N1 | P1, P2, P3 | Séparation moteur/vues présente ; politique et militaire absents. |
| A02 — la présentation ne décide pas la logique métier | mesure | `viewer.snapshot_loader`, `visualisateur.raster` | P3, P4, P6 | Le bandeau additionne les données exportées. Les altitudes du rendu sont des proxies de lecture, pas des changements du monde. |
| A03 — le moteur tourne sans présentation | mesure | `sim.__main__` | P1 | Courses réussies sans serveur de vue ni forge3d. |

source : Roadmap par couches

| ID et promesse | État | Code ou recherche | Preuve | Limite ou contradiction |
|---|---|---|---|---|
| R01 — monde vivant | partiel | `World.charger`, `engine.tick` | P1, P2, P4 | Carte, climat/saisons, ressources, population, subsistance et commerce présents ; limites L04, P-ECO, P-INF. |
| R02 — villes, entreprises, métiers, routes, infrastructures | partiel | extraction et bourg dérivé | P2, P3 | Mineurs agrégés ; entreprises, bâtiments et construction de routes absents. |
| R03 — États, fiscalité, lois, diplomatie, technologies, culture, religion | absent | N1 | N1 | Aucun modèle étatique ou maillon correspondant. |
| R04 — armées, recrutement, logistique, ravitaillement, stratégie | absent | N1 | N1 | Le commerce alimentaire civil ne tient pas lieu de logistique militaire. |
| R05 — batailles tactiques | absent | N1 | N1 | Aucun mécanisme tactique. |

source : Mesure du succès

| ID et promesse | État | Code ou recherche | Preuve | Limite ou contradiction |
|---|---|---|---|---|
| M01 — situations complexes, crédibles et intéressantes | non_verifie | `sim.__main__` produit un résumé ; pas de protocole joueur dans les points d'entrée inspectés | P1 exécuté, N1 | Les courses montrent des effets, pas leur intérêt pour un joueur. Aucun essai de campagne avec joueur réalisé ; aucune conclusion de succès produit tirée des tests. |

## Preuves exécutées et rejeu

Rejouer dans un checkout isolé de la révision mesurée. Les commandes
ci-dessous partent de sa racine. Installer pytest pour les tests ; numpy et
Pillow seulement pour la preuve 3D. Dans l'environnement de mesure, les
paquets sont fournis par `PYTHONPATH` ; aucun fichier du produit n'a été
modifié pour les charger.

### P1 — amorçage et course annuelle

```bash
git rev-parse HEAD
python3 --version
python3 -m sim --ticks 0 --seed 0 --json
python3 -m sim --ticks 365 --seed 0 --json
```

Codes de sortie : tous `0`. Révision et Python : ceux annoncés en tête.
À t0 : 596 cellules, 66 649 511 habitants, 666 495 110 kg de nourriture,
aucun transport et aucune cellule affamée. À 365 ticks : 9 559 160 habitants,
9 605 831 626.475634 kg de nourriture, 15 016 898 402.932928 kg transportés
cumulés, aucune cellule affamée. Les dénominateurs proviennent du monde
chargé. Ces valeurs sont des observations, jamais des attentes de tests.
Le résumé ne contient aucune date calendaire sur cette révision.

### P2 — invariants du moteur et du regard mince

```bash
python3 -m pytest sim/tests/ viewer/tests/ -q
```

Résultat : **196 tests réussis, 456.03 s, code 0**. Aucun échec sur la base.
La suite protège masse, dette, populations, états d'absence, causalité des
maillons et déterminisme ; elle ne valide pas les domaines absents.

### P3 — provinces, bourg et vues

```bash
python3 -m pytest sim/tests/test_province.py viewer/tests/test_viewer_v0b.py -q
python3 -m sim --ticks 20 --seed 0 --snapshot-json /workspace/scratch/150bf724f0a4/fh-coherence.json
```

Mesure initiale sur `3f5045d` : **48 tests réussis, 78.66 s, code 0**.
Rejeu sur `78b384d` : voir P7. La photographie à 20 ticks
est produite, code 0.
Le test `test_bourg_somme_exacte_par_cellule` protège la partition ;
`test_province_couverture_totale_monde_reel` protège la couverture ; les tests
du tableau de bord protègent les agrégats effectivement lus, pas ceux absents.

### P4 — jointures et déterminisme du monde complet

La sonde suivante a été exécutée sans changer le moteur. Elle compare chaque
tick des deux courses, puis les sommes du snapshot et le tableau de bord.
Le test vide porte sur une copie privée.

```bash
python3 - <<'PY'
import json, random
from sim.world import World
from sim.engine import tick, _aretes_maritimes_du_monde
from sim.aggregation import bourg_depuis_monde
from sim.snapshot_export import build_snapshot_document
from viewer.snapshot_loader import agregats_monde, EchantillonVide
worlds = [World.charger(0), World.charger(0)]
rngs = [random.Random(0), random.Random(0)]
mer_observee = 0
for numero in range(20):
    for world, rng in zip(worlds, rngs):
        tick(world, rng, numero)
    assert worlds[0].to_dict() == worlds[1].to_dict()
    assert worlds[0].stocks_mer == worlds[1].stocks_mer
    mer_observee += bool(worlds[0].stocks_mer)
assert mer_observee > 0
world = worlds[0]
doc = build_snapshot_document(world, 0, 20)
assert doc['cells']
kpis = agregats_monde(doc)
for champ, valeurs in {
    'population': [c['population'] for c in doc['cells']],
    'stock_nourriture_kg': [c['stocks']['nourriture'] for c in doc['cells']],
    'cellules_affamees': [int(c['hunger_ticks'] > 0) for c in doc['cells']],
}.items():
    assert valeurs
    assert kpis[champ]['etat'] == 'mesure'
    assert kpis[champ]['valeur'] == sum(valeurs)
    assert kpis[champ]['cellules_lues'] == len(valeurs)
    print(champ, kpis[champ])
try:
    agregats_monde({'cells': []})
except EchantillonVide:
    print('document_vide: EchantillonVide')
else:
    raise AssertionError('échantillon vide accepté')
repartitions = bourg_depuis_monde(world)
assert repartitions
assert all(r.habitants_du_bourg + r.habitants_des_champs == world.cells[r.cell_id].population for r in repartitions)
print('bourg_positif', sum(r.habitants_du_bourg > 0 for r in repartitions), '/', len(repartitions))
print('bourg_exporte', sum('bourg' in c for c in doc['cells']), '/', len(doc['cells']))
print('champs_dashboard', sorted(kpis))
print('kg_transportes_dashboard', kpis['kg_transportes'])
print('temps_monde', hasattr(world, 'ticks_ecoules'), hasattr(world, 'date_simulation'))
print('schema', doc['schema_version'], 'jour_de_tick', doc['jour_de_tick'])
print('determinisme_cellules_et_mer', 20, '/', 20, 'mer_observee', mer_observee, '/', 20)
print('marchandises_mer', sorted(world.stocks_mer), 'mer_dans_to_dict', 'stocks_mer' in world.to_dict())
ids = set(world.cells)
terre = {e[k] for e in world.adjacency if e['a'] in ids and e['b'] in ids for k in ('a','b')}
cotes = {cid for cid, _, _ in _aretes_maritimes_du_monde(world)}
print('cellules', len(ids), 'aretes', len(world.adjacency), 'aretes_mer', len(_aretes_maritimes_du_monde(world)))
print('sans_voisin_terrestre', len(ids - terre), 'cotieres_sans_terre', len(cotes - terre))
print('objets', sum(c.stocks.get('objet', 0) > 0 for c in world.cells.values()), '/', len(ids))

PY
```

Résultat, code `0` :

```text
population : mesure 56437148 ; 596 cellules lues
stock_nourriture_kg : mesure 406959.37531699997 ; 596 cellules lues
cellules_affamees : mesure 540 ; 596 cellules lues
document_vide : EchantillonVide
bourg_positif : 25 / 596 ; bourg_exporte : 0 / 596
kg_transportes_dashboard : absent
temps_monde : compteur absent ; date absente
schema : v0a-3 ; jour_de_tick : 20
comparaisons cellules et bassin égales : 20 / 20
observations avec marchandise dans le bassin : 20 / 20
marchandises_mer : nourriture ; bassin absent de to_dict
cellules : 596 ; arêtes : 1364 ; arêtes maritimes : 447
sans voisin terrestre : 203 ; côtières sans terre : 203
cellules avec objet : 0 / 596
```

Les nombres alimentaires du snapshot sont arrondis par l'export à six
décimales : la sonde compare le bandeau à ce qu'il lit, sans le confondre
avec une somme de flottants non arrondis dans le moteur. Les identifiants
proviennent de `cell_id`. La province et la partition du bourg se recalculent.
Le snapshot omet le bassin, les reports de naissance et de migration, ainsi
que la date réelle. Il n'est pas démontré rechargeable : `World.charger`
réamorce depuis la carte, il ne reprend pas cette photographie.

### P5 — suite de déterminisme

```bash
python3 -m pytest sim/tests/test_determinisme.py -q
```

Mesure initiale sur `3f5045d` : **9 tests réussis, 60.05 s, code 0**.
Rejeu sur `78b384d` : voir P7.
La preuve P4 complète explicitement l'empreinte des cellules par le bassin
maritime non vide. Les empreintes sont désignées par leur rôle ; aucune
valeur de hachage n'est transformée en constante documentaire.

### P6 — limite du rendu 3D

```bash
python3 -m visualisateur --snapshot /workspace/scratch/150bf724f0a4/coherence-snapshot.json --png /workspace/scratch/150bf724f0a4/monde-3d.png
```

Code `2` : `forge3d est absent. Dans le venv du visualisateur : python3 -m pip install forge3d`.
`visualisateur/rendu.py::rendre_png` importe le moteur externe, vérifie
l'adaptateur GPU puis demande l'image. Son existence contredit la note
liminaire, mais l'essai n'a produit aucune image GPU. La rasterisation
par numpy/Pillow et un rendu par forge3d sont deux preuves distinctes.

### N1 — inspection bornée des mécanismes et des absences

```bash
rg -n 'class |def ' sim/model.py sim/world.py sim/engine.py sim/aggregation.py visualisateur/rendu.py
rg -n -i 'famille|personne|recrut|soldat|bataille|fiscal|diploma|religion|culture|technolog|quartier|bâtiment|batiment|entreprise|urbanis|criminal|invest|sauvegarde|reprendre' sim/*.py viewer/*.py visualisateur/*.py
rg -n 'def |forge3d|import' visualisateur/*.py
```

Codes `0`. La recherche thématique rend six mentions : cinq emplois
ordinaires de « personne » dans des commentaires/docstrings et la ration
alimentaire par personne dans les constantes. Aucun de ces résultats
n'est une entité individuelle. Cette recherche n'est pas seule à établir
les absences : `Cell` et ses champs persistés, `World.charger`, tous les
maillons appelés par `tick`, les types `Regroupement` et `RepartitionBourg`,
les points d'entrée CLI et les lecteurs de vue ont été inspectés.
Ils forment une simulation de cellules et ses consultations, sans boucle
politique, militaire ou individuelle ni fonction de reprise du snapshot.
La portée est l'arbre actif ; elle ne porte pas sur les archives git,
les projets externes ou des propositions en PR.

## Écarts reproductibles et lots recensés

| Écart | Reproduction et invariant | Couverture dans le registre |
|---|---|---|
| Amorçage paramétrique face à une promesse historique | P1 et lecture de `_seed_population` / `_seed_food_stock` ; distinguer donnée historique et proxy. | Aucun lot recensé pour remplacer cet amorçage. |
| Minerai sans transformation | P4 et ordre des maillons de `tick` ; une origine/stockage ne constitue pas une chaîne complète. | 049 propose une fabrication générique, sans demande ni bras. |
| Cellules côtières sans issue terrestre pour migrer | P4 dérive 203 cellules ; `_voisins_avec_surplus` ne retient que les deux extrémités dans `World.cells`. | 050 ouvre ce trajet ; la mesure présente ne le déclare pas livré. |
| Bourg calculable mais absent des vues | P4 : partition positive, clé absente du snapshot et du bandeau ; une vue doit lire une donnée effectivement exportée. | 051 pour l'export, 052 pour le bandeau. |
| Monde non daté | P4 : aucun compteur ; `jour_de_tick(20)` est une consultation du numéro reçu, pas du temps porté par le monde. | 053 pour le monde et la CLI ; date du snapshot hors de ce lot. |
| État maritime absent de `to_dict` et du snapshot | P4 : bassin non vide, clés absentes ; une preuve d'état complet doit le comparer séparément. | Aucun lot recensé pour la sérialisation complète ou la reprise. |
| `kg_transportes` absent du bandeau | P4 : `agregats_monde` rapporte l'absence alors que la CLI cumule les transports ; absence honnête, pas zéro. | Aucun lot recensé pour l'export du cumul. |
| Documentation en retard | `VISION.md` nie le rendu ; `sim/MODELE.md` présente encore la mer comme non lue et annonce le bourg au futur dans le motif d'agrégation ; comparer P2, P3 et `rendre_png`. | Les PR documentaires #234 et #235 sont des propositions ; aucune correction n'est incluse ici. |

Les niveaux sous la cellule, la politique, les armées et les batailles
sont des capacités absentes, pas des anomalies numériques à recalibrer.
La date du snapshot, les distances maritimes, la distribution interne à la
cellule et la reprise de sauvegarde restent des limites explicites. Ce
rapport ne décide ni nouvelle priorité ni nouveau numéro de lot.


### P7 — rejeu après fusion des briefs

```bash
git diff --exit-code 3f5045df7b866058efad747e8fd74c7b9372841e 78b384d4b904dcb2d4cb843979bf18347f623236 -- sim viewer visualisateur outils data VISION.md AGENTS.md atelier.toml .github
python3 -m pytest sim/tests/test_province.py viewer/tests/test_viewer_v0b.py sim/tests/test_determinisme.py -q
```

Code `0` pour l'identité du code, des données et des références examinées.
Les changements fusionnés portent seulement les briefs et leurs fiches.
Le rejeu groupé de P3 et P5 donne **57 tests réussis, 146.87 s, code 0**.
La suite complète P2 a également été rejouée sur `78b384d` et sa durée
ci-dessus est celle de ce dernier rejeu. La course annuelle P1 et la sonde
P4 ont été relancées : leurs sorties sont identiques aux mesures initiales
(`cmp`, code `0` pour chacune). Le rapport ne déclare aucun effet des
implémentations en attente de validation.
