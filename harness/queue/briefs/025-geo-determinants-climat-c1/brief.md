# Brief 025 : les déterminants physiques du climat (C1) — insolation astronomique et continentalité

**Authored**: 2026-08-20T21:15:00Z
**Author**: forge-planificateur
**Statut**: PRÊT — exécutable en l'état, aucun arbitrage préalable requis

> **Note de transparence (contrat du Planificateur) :** le rôle signataire est
> le rôle natif du harnais `forge-planificateur`. L'acteur réel est Claude
> Code (CTO), invoqué en session interactive **par Hermes, pilote du projet,
> sur décision du propriétaire**, à partir de la tâche autoritaire
> `hermes/requests/DEMANDE-20260820-claude-code-prochains-briefs.md` (statut
> `HANDED_TO_CTO`) et de `ROADMAP.md` (F1, jalon E1, « Prochaines étapes »
> point 7 : « Poursuivre F1 avec G6 relief, climat et ressources »). Cette
> session n'a ni modifié `ROADMAP.md`, ni `hermes/**`, ni `docs/**`, ni lancé
> Cursor, ni committé — conformément au mandat reçu. Le lot 024 (relief G6)
> était en cours d'exécution dans un worktree isolé au moment de l'écriture :
> ce brief ne le lit pas, ne le juge pas et n'en dépend pas.

---

## Provenance

Ce brief prolonge le jalon E1 — Fondations monde, après les briefs 019
(adjacence maritime G4), 020 (provenance du littoral G3) et 021 (fleuves G5),
tous fusionnés. Le brief 024 (relief G6) est en cours ; **ce lot n'en dépend
pas** et ne lit aucun artefact G6. Aucun rang ordinal n'est revendiqué ici :
le dépôt en compte deux séries différentes (le brief 021 se dit « second lot
atomique » en comptant le 020, le brief 024 se dit « troisième » en ne le
comptant pas), et ajouter un troisième décompte n'aiderait personne.

### Ce lot n'est pas un portage : il n'a pas d'ancêtre

Vérifié sur le dépôt au moment de l'écriture, et c'est la différence
structurelle majeure avec les briefs 019, 021 et 024 :

- `harness/queue/geo-pipeline-port-plan.md` (brief 004) énumère la totalité
  de ce que VictoriaProject contenait : douze scripts d'étape
  (`02_coastline` → `10_id_textures`) plus `qa/run_all.py` (**G11**) et
  `qa/crs_coherence.py` (**G12**). **Aucune étape de climat n'y figure.**
  Aucune étape de ressources non plus.
- `pipeline/geo/qa/checks.py` contient les familles de contrôles `Q1`–`Q10`,
  `G2`, `G2b`, `G3`, `G4`, `G5`, `G5b`, `G5c`, `G6`, `G7`, `G8`, `G9`, `G10`,
  `P1`, `P2` et `A1` (soixante-douze identifiants de contrôle au total, tous
  relevés à l'écriture de ce brief). **Aucun identifiant `C1-*`.**
- `pipeline/geo/constants.py` porte des blocs pré-écrits pour `G3` à `G10`,
  `P1`, `P2` et `A12`. **Aucune constante de climat.** Le seul endroit où le
  mot apparaît est `A12_BIOME_PROVENANCE`, qui cite une « climate province »
  comme indice secondaire faible d'une règle de biome future — une mention,
  pas un contrat.
- `pipeline/geo/sources.lock` déclare exactement quatre sources : le bloc
  `dem` (179 tuiles Copernicus), `10m_physical.zip`, `10m_cultural.zip` et
  `geonames_cities500`. **Aucune source climatique.**
- `pipeline/geo/pipeline.py` accepte huit valeurs de `--source` (`fixture`,
  `natural_earth`, `natural_earth_1400`, `cells`, `adjacency`, `rivers`,
  `navigability`, `relief`). **Aucune n'est le climat.**

Conséquence tranchée ici : **G4, G5 et G6 héritaient d'une barre qualité
écrite avant eux ; le climat n'en a aucune.** Ce lot écrit donc à la fois la
barre et ce qu'elle mesure — et c'est précisément pourquoi son périmètre est
volontairement réduit à ce qui **ne peut pas être inventé** (D2).

### Ce que ce lot ne livre pas, et pourquoi

**Ni température, ni précipitations, ni saisons, ni classification
climatique.** Ces grandeurs ne se dérivent pas honnêtement de ce que le dépôt
contient : elles exigent un jeu de données climatique réel, qui n'est déclaré
dans aucune source du dépôt. Les fabriquer à partir d'une régression
inventée sur la latitude serait exactement la donnée inventée en silence que
la règle durement acquise n° 10 interdit. **Le choix d'une source climatique
(laquelle, sous quelle licence, avec quel usage commercial, et si des normales
modernes valent proxy déclaré pour 1400) est une décision du propriétaire ;
elle n'est ni prise ni préemptée ici.** Un lot ultérieur la portera, et la
valeur `climate` de `--source` lui est réservée : ce lot emploie
`climate_drivers` (D8), jamais `climate`.

### Ce lot ne clôt pas la ligne « climat » du jalon E1

À dire sans détour, parce que la tentation inverse existe : `ROADMAP.md`
demande, pour clore E1, que « relief, climat et ressources » soient livrés par
`pipeline/geo/`. **Ce lot ne livre pas le climat** — il livre les deux
déterminants physiques du climat qui se mesurent sans aucune source externe.
Après lui, la ligne « climat » de E1 **reste ouverte**, et elle le restera
tant qu'une source climatique n'aura pas été choisie par le propriétaire.

Ce lot ne modifie pas `ROADMAP.md` : ce fichier appartient à Hermes
(ADR-0010), et l'y refléter est une écriture d'Hermes sur décision du
propriétaire, pas un effet de bord d'un lot d'exécution. Le titre de ce brief
dit « les déterminants physiques du climat », jamais « le climat », et le
`README.md` du pipeline devra tenir le même langage (SC6).

À partir d'ici, **ce `brief.md` est la SEULE instruction** (voir `CLAUDE.md`
› Single Source of Instruction).

---

## World-Terms Requirement

**Chaîne causale.**

Deux faits physiques, mesurables et invariants, décident d'une grande partie
de ce qu'un lieu permet à ceux qui y vivent. Ce lot les établit, et rien de
plus :

1. **L'énergie que le lieu reçoit du Soleil, et comment elle est répartie
   dans l'année.** Un lieu au sud de la fenêtre reçoit environ une fois et
   demie l'énergie annuelle d'un lieu à son extrémité nord, et son jour le
   plus long dure quatre heures de moins. Ce n'est pas un réglage : c'est de
   la géométrie céleste, exacte, sans paramètre ajusté. Elle décide de la
   longueur de la saison de culture, du nombre d'heures de travail utile en
   hiver, de la saison où une armée peut marcher. Ce lot fournit l'énergie
   et la durée du jour mesurées ; il ne code **aucune** règle agricole,
   militaire ou démographique (interdit : « si insolation < X alors
   rendement −20 % » écrit ici).
2. **La distance à la mer.** La mer amortit : elle réchauffe l'hiver,
   refroidit l'été, apporte l'humidité, et elle ouvre ou ferme l'accès au
   monde. Deux cellules de même latitude, l'une sur la côte, l'autre à
   six cents kilomètres à l'intérieur, ne sont pas le même endroit. Ce lot
   mesure cette distance sur la géométrie réelle déjà committée, et la
   confronte à une seconde dérivation indépendante (le nombre de frontières
   à franchir pour rejoindre le littoral) — deux méthodes qui se contrôlent
   l'une l'autre.

**Interdit** dans ce lot, comme dans les briefs 019, 021 et 024 : aucun
barème, aucun bonus, aucun malus, aucun multiplicateur, aucun coefficient de
rendement. Ce lot établit ce que le monde **reçoit** et **où il se trouve par
rapport à la mer** ; la traduction en récolte, en mortalité hivernale ou en
prix du transport appartient à des lots futurs de `sim/`. Le contrôle `C1-F`
(D5) rend cette interdiction mécanique, pas seulement écrite.

**Rien n'est inventé.** La constante solaire, l'obliquité, les jours de
solstice et les décimales sont déclarés dans `constants.py` par ce lot, avec
leur justification écrite ; les latitudes viennent des centroïdes déjà
committés ; les distances viennent des polygones déjà committés.

---

## Vocabulaire (expliqué une fois, dérivé du code lu)

- **insolation extraterrestre** : l'énergie solaire qui arrive au sommet de
  l'atmosphère, par mètre carré horizontal, avant tout effet de nuage ou
  d'atmosphère. Ce n'est **pas** l'ensoleillement au sol : c'est la part
  purement astronomique, exacte et invariante dans le temps.
- **déclinaison solaire** : l'angle entre l'équateur et la direction du
  Soleil, qui varie au fil de l'année entre `−C1_OBLIQUITY_DEG` et
  `+C1_OBLIQUITY_DEG`.
- **angle horaire du coucher** : la moitié de la durée du jour, exprimée en
  angle. Il vaut `arccos(−tan(latitude) × tan(déclinaison))` quand cet
  argument est dans `[−1, +1]` ; en dehors, le lieu est en jour polaire ou
  en nuit polaire.
- **écrêtage polaire** : le cas où l'argument ci-dessus sort de `[−1, +1]`.
  Sur la fenêtre pilote il ne se produit jamais (le seuil est
  `90 − C1_OBLIQUITY_DEG` degrés de latitude, la fenêtre s'arrête plus au
  sud) — mais le code le compte, il ne le suppose pas (D3.5).
- **cellule littorale** : au sens de G4, une cellule qui porte au moins une
  arête `land-sea` dans `artifacts/adjacency_g5.json`. La littoralité est
  **dérivée**, jamais stockée sur la cellule (brief 019).
- **distance de bord à la mer** (`dist_sea_edge_m`) : la distance, en mètres
  projetés EPSG:3035, entre le **polygone** de la cellule et le polygone de
  zone de mer le plus proche. Elle vaut zéro pour une cellule qui touche la
  mer.
- **distance de centroïde à la mer** (`dist_sea_centroid_m`) : la même
  distance, mesurée depuis le **centroïde** de la cellule. C'est le proxy de
  continentalité : il dit à quelle profondeur dans les terres se trouve le
  cœur de la cellule, alors que la distance de bord ne dit que « la mer
  touche-t-elle cette cellule ».
- **sauts au littoral** (`hops_to_sea`) : le nombre minimal d'arêtes à
  franchir, sur le graphe des arêtes `land-land` et `strait` de
  `adjacency_g5.json`, pour aller de la cellule à une cellule littorale.
  Une cellule littorale vaut zéro.

---

## Ce qui existe déjà, et que ce lot lit sans le refaire

Vérifié sur le dépôt au moment de l'écriture de ce brief. Les nombres
ci-dessous sont des **constats de contexte**, non des seuils : aucun contrôle
de ce lot ne s'y compare (règle n° 2 — un contrôle dérive, il ne se nomme
jamais d'après sa cible).

- `pipeline/geo/artifacts/cells_g3.json` : committé, suivi par git, lecture
  seule. `596` cellules, identifiants `1175` à `10466`, chacune portant
  `cell_id`, `geometry` (Polygon, coordonnées **EPSG:3035**), `centroid`
  (`lat`, `lon`, `x_m`, `y_m`) et `area_km2`. Latitudes de centroïde
  mesurées : `30.448217` à `61.498540`.
- `pipeline/geo/artifacts/sea_zones_g4.json` : committé, lecture seule.
  `40` zones de mer, identifiants `5000` à `5039`, géométries EPSG:3035,
  `2` bassins enfermés.
- `pipeline/geo/artifacts/adjacency_g5.json` : committé, lecture seule.
  `2085` arêtes : `917` `land-land`, `437` `land-sea`, `63` `sea-sea`,
  `668` `strait`.
- `pipeline/geo/artifacts/stats_g4.json` : committé, lecture seule. Porte
  `coastal_cell_count` (`372`) et `coastal_cell_ids`. **Ce lot ne lit pas ce
  fichier pour dériver la littoralité** : il la redérive lui-même des arêtes
  `land-sea` (D4), et le compte obtenu est confronté à celui-ci comme
  contrôle croisé, jamais recopié.
- `pipeline/geo/constants.py` : porte `PILOT_WINDOW_LONLAT`, dérivée
  dynamiquement — valeur mesurée à l'écriture :
  `(-11.320281, 29.7, 34.820281, 61.5)`. Porte aussi `TARGET_CRS`
  (`EPSG:3035`), `FLOAT_DECIMALS`, `LENGTH_EPS`.
- `pipeline/geo/io_util.py` : `write_json` (JSON déterministe, clés triées,
  flottants arrondis, retourne le SHA256 écrit), `read_json`, `sha256_file`.
  Patron d'écriture à réemployer tel quel.
- `pipeline/geo/qa/checks.py` : expose `CheckResult` et `q10_determinism`.
  Ce lot les **importe**, il ne les recopie pas et ne modifie pas ce fichier
  (D9).
- `pipeline/geo/steps/05_rivers.py` : patron de module d'étape à suivre
  (docstring déclarant entrées/sorties/usage, `ROOT =
  Path(__file__).resolve().parents[1]`, import depuis `constants`,
  `io_util`, `projection`, une fonction `run_*` sans argument, un `main()`).
- `pipeline/geo/pipeline.py` : patron de crochet à suivre — un
  `_load_*_module()` par étape, chargement par
  `importlib.util.spec_from_file_location` sur un chemin explicite, un
  `run_*()` qui délègue, une branche `args.source == "..."` qui imprime une
  ligne de résumé puis les empreintes.

**Mesures faites par le Planificateur avant d'écrire les décisions**, toutes
rejouables sur les artefacts committés, toutes **non normatives** :

| mesure | valeur | comment elle a été obtenue |
|---|---|---|
| cellules atteintes par un parcours en largeur depuis les cellules littorales, sur les seules arêtes `land-land` | `575` sur `596` | parcours en largeur sur `adjacency_g5.json` |
| cellules sans aucun chemin `land-land` vers le littoral | `21` | les mêmes, complémentaire — toutes des îles lacustres (Saimaa, Ladoga, Vänern) ne portant **que** des arêtes `strait` |
| cellules atteintes en ajoutant les arêtes `strait` | `596` sur `596` | même parcours, arêtes `land-land` + `strait` |
| répartition des sauts au littoral (`land-land` + `strait`) | `0 → 372`, `1 → 124`, `2 → 68`, `3 → 30`, `4 → 2` | même parcours |
| distance approchée centroïde → mer, médiane par classe de sauts | `13.4` / `148.9` / `307.3` / `412.2` / `646.5` km | approximation **majorante** : distance au plus proche **sommet** de zone de mer, `43 999` sommets balayés — la vraie distance polygone sera plus petite, l'ordre des médianes ne peut que se renforcer |
| insolation annuelle extraterrestre aux latitudes extrêmes | `11 444.2` et `7 149.7` MJ/m²/an | formule de D3, appliquée aux latitudes de centroïde extrêmes |
| écrêtages polaires sur les `596` cellules | `0` | même calcul |
| inversions de l'insolation en fonction de la latitude | `0` sur `595` paires **consécutives** du tri | même calcul, valeurs arrondies à `C1_INSOLATION_DECIMALS` |
| égalités d'insolation malgré un écart de latitude ≥ `0.01°` | `0` | même calcul — c'est ce qui rend `C1-B` (D5) satisfaisable |
| paires consécutives dont l'écart de latitude atteint `0.01°` | `495` sur `595`, soit `83.2` % | même tri — c'est ce qui prouve que la seconde moitié de `C1-B` **mord** au lieu d'être vide (règle n° 6) |
| égalités détectées si l'on remplace l'insolation par une constante | `495` | même calcul, sur un champ saboté — le contrôle refuse donc réellement un champ constant |
| inversions et égalités sur **toutes** les paires (`177 310`), et non les seules consécutives | `0` et `0` | vérifié aussi ; la formulation retenue en D5 reste la formulation consécutive, moins coûteuse et déjà mordante |

Ces mesures **ne sont pas des résultats attendus** : le Générateur les
recalcule, et un écart avec elles est un fait à consigner, pas une faute en
soi. Elles servent à prouver que les contrôles de ce brief sont
satisfaisables sur le monde réel, et non écrits à l'aveugle.

---

## Décisions de conception tranchées par le Planificateur

Le Générateur n'arbitre aucun de ces points. Il choisit librement les noms de
fonctions et variables internes et l'organisation du code dans le périmètre
autorisé.

### D1 — Entrées exactes

Le nouveau module lit, en lecture seule :

| entrée | usage |
|---|---|
| `pipeline/geo/artifacts/cells_g3.json` | `cells[]` : `cell_id`, `centroid` (latitude pour l'astronomie, `x_m`/`y_m` pour la distance), `geometry` (polygone pour la distance de bord) |
| `pipeline/geo/artifacts/sea_zones_g4.json` | `sea_zones[]` : `zone_id`, `geometry` — les polygones vers lesquels les distances sont mesurées |
| `pipeline/geo/artifacts/adjacency_g5.json` | `adjacency[]` : arêtes `land-sea` (littoralité dérivée), `land-land` et `strait` (graphe des sauts) |
| `pipeline/geo/artifacts/stats_g4.json` | `coastal_cell_count` — **uniquement** pour le contrôle croisé de SC2, jamais comme source de la littoralité |
| `pipeline/geo/constants.py` | toutes les bornes, décimales et constantes astronomiques, **lues**, jamais recopiées en littéral dans le code d'étape |

Aucune tuile, aucune archive, aucun accès réseau. Ce lot ne télécharge rien.

### D2 — Le périmètre est borné par ce qui ne peut pas être inventé

Ce lot livre exactement trois familles de grandeurs, et rien d'autre :

1. **l'insolation extraterrestre annuelle** et **la durée du jour aux deux
   solstices**, calculées par la formule fixée en D3 ;
2. **les deux distances à la mer** et **la zone de mer atteinte**, mesurées
   sur les polygones committés ;
3. **les sauts au littoral**, dérivés du graphe committé.

Toute grandeur qui exigerait une donnée absente du dépôt — température,
précipitations, humidité, vent, saison de culture, classe climatique — est
**hors de portée** et son absence est un fait déclaré, pas un trou silencieux
(Non-Goals n° 1, et règle n° 10).

### D3 — La formule astronomique, fixée ici, sans paramètre libre

Deux implémentations qui divergent donnent deux mondes. La formule est donc
fixée intégralement, dans l'ordre, et le Générateur ne l'altère pas.

Pour un jour `n` de `1` à `C1_DAYS_IN_YEAR` et une latitude `φ` (radians) :

1. **Déclinaison** : `δ = radians(C1_OBLIQUITY_DEG) × sin(2π × (284 + n) / C1_DAYS_IN_YEAR)`.
2. **Facteur de distance Terre-Soleil** : `E0 = 1 + C1_ECCENTRICITY_FACTOR × cos(2π × n / C1_DAYS_IN_YEAR)`.
3. **Argument de l'angle horaire** : `u = −tan(φ) × tan(δ)`.
   - si `u ≥ 1` : nuit polaire, `ωs = 0`, **écrêtage compté** ;
   - si `u ≤ −1` : jour polaire, `ωs = π`, **écrêtage compté** ;
   - sinon `ωs = arccos(u)`, aucun écrêtage.
4. **Insolation du jour, en joules par mètre carré** :
   `H = (86400 / π) × C1_SOLAR_CONSTANT_W_M2 × E0 × (cos(φ)·cos(δ)·sin(ωs) + ωs·sin(φ)·sin(δ))`.
5. **Insolation annuelle** : somme des `H` sur les `C1_DAYS_IN_YEAR` jours,
   convertie en mégajoules par division par `C1_MJ_PER_J`, arrondie à
   `C1_INSOLATION_DECIMALS`.
6. **Durée du jour**, en heures : `N = (2/15) × degrés(ωs)`, arrondie à
   `C1_DAYLIGHT_DECIMALS`, évaluée pour `n = C1_SUMMER_SOLSTICE_DAY` et
   `n = C1_WINTER_SOLSTICE_DAY`.

`86400` (secondes par jour), `2`, `15`, `284`, `π` et les constantes
trigonométriques sont des grandeurs structurelles ; chacune des autres
valeurs est **lue** de `constants.py`. La latitude employée est `centroid.lat` de la cellule,
telle qu'elle est committée — jamais recalculée, jamais arrondie avant
usage.

Le nombre d'écrêtages polaires rencontrés est porté par cellule
(`polar_clamp_days`) et agrégé dans `stats_c1.json`
(`ecretages_polaires_total`). Il est **mesuré**, jamais supposé nul.

### D4 — Littoralité redérivée, distances mesurées, zone nommée

- **Littoralité** : une cellule est littorale si et seulement si elle porte
  au moins une arête `kind == "land-sea"` dans `adjacency_g5.json`. Ce lot
  la redérive lui-même ; il ne lit pas `coastal_cell_ids`.
- **`dist_sea_edge_m`** : distance minimale, en mètres projetés EPSG:3035,
  entre le polygone `geometry` de la cellule et le polygone `geometry` de
  chacune des zones de `sea_zones_g4.json` ; on retient le minimum.
- **`dist_sea_centroid_m`** : la même chose, depuis le point
  `(centroid.x_m, centroid.y_m)`.
- **`nearest_sea_zone_id`** : le `zone_id` qui réalise le minimum de
  `dist_sea_centroid_m`. Départage des égalités de distance : **le plus petit
  `zone_id` gagne** — même règle de départage que les noms de mer en G4
  (brief 019) et que les cols en G6 (brief 024).
- **`centroid_inside_cell`** : booléen, vrai si le centroïde committé tombe à
  l'intérieur du polygone de sa propre cellule. Ce n'est pas garanti pour un
  polygone non convexe ; le compte des cellules où il est faux
  (`cellules_centroide_hors_polygone`) est **mesuré et rapporté**, et il
  conditionne l'invariant de `C1-E` (D5).

### D5 — Les six contrôles `C1`, plus le déterminisme importé : leur sémantique exacte

Les six contrôles `C1-*` vivent dans un module **neuf**,
`pipeline/geo/qa/checks_c1.py`, qui importe `CheckResult` et
`q10_determinism` de `qa/checks.py` sans le modifier (D9). Chacun retourne un
`CheckResult` de la même forme que les contrôles existants (`id`, `name`,
`passed`, `detail`). `Q10` n'est pas réécrit : il est **importé** et
simplement assemblé avec les six autres, ce qui porte à **sept** le nombre
d'entrées du rapport de preuve.

**Où « paire » se lit.** Partout dans `C1-B` et `C1-C`, une « paire » est une
paire **consécutive dans la liste des cellules triée par latitude croissante**
— pas une paire quelconque. Ce choix est mesuré, pas commode : le
Planificateur a vérifié que `495` des `595` paires consécutives ont un écart
de latitude d'au moins `C1_MONOTONE_DLAT_DEG`, si bien qu'un champ constant
serait pris en défaut `495` fois. Le balayage de toutes les paires
(`177 310`) donne le même verdict et coûte trois cents fois plus.

| id | ce qu'il vérifie |
|---|---|
| `Q10` | déterminisme : chaque paire d'empreintes des deux passes est égale et non vide (fonction importée, non réécrite) |
| `C1-A` | **maille inchangée** : l'ensemble trié des `cell_id` de `cells_climate_drivers_c1.json` est exactement celui de `cells_g3.json` — même compte, mêmes identifiants, aucun en trop, aucun manquant |
| `C1-B` | **l'insolation ne croît jamais avec la latitude** : sur la liste triée par latitude croissante, aucune paire consécutive n'a une insolation supérieure à la précédente ; **et** toute paire consécutive dont les latitudes diffèrent d'au moins `C1_MONOTONE_DLAT_DEG` a une insolation strictement décroissante. Les deux moitiés sont exigées : la première interdit l'inversion, la seconde interdit un champ constant |
| `C1-C` | **la durée du jour se comporte comme le Soleil l'impose** : pour toute cellule, la durée au solstice d'été est strictement supérieure à celle au solstice d'hiver ; **et** sur la même liste triée, l'amplitude (été moins hiver) ne décroît sur aucune paire consécutive, et croît strictement sur toute paire consécutive dont les latitudes diffèrent d'au moins `C1_MONOTONE_DLAT_DEG` |
| `C1-D` | **la littoralité de G4 et la géométrie de C1 disent la même chose** : toute cellule portant au moins une arête `land-sea` a `dist_sea_edge_m ≤ C1_SEA_DISTANCE_EPS_M`, **et** toute cellule n'en portant aucune a `dist_sea_edge_m > C1_SEA_DISTANCE_EPS_M`. Les deux sens sont exigés ; chaque cellule en défaut est nommée |
| `C1-E` | **les deux dérivations de continentalité concordent** : toutes les cellules de la maille lue ont un `hops_to_sea` calculé (jamais la sentinelle) ; la médiane de `dist_sea_centroid_m` croît strictement avec `hops_to_sea` sur les classes non vides prises dans l'ordre ; et pour toute cellule dont `centroid_inside_cell` est vrai, `dist_sea_edge_m ≤ dist_sea_centroid_m + C1_SEA_DISTANCE_EPS_M` |
| `C1-F` | **aucun barème** : aucune clé appartenant à `WORLD_TERMS_FORBIDDEN_KEYS` n'apparaît, à quelque profondeur que ce soit, dans `cells_climate_drivers_c1.json`, `stats_c1.json`, `MANIFEST_c1.json` ni `registry/climate_drivers_registry.json` — même parcours récursif que `g5b_d_no_upstream_limit_encoded` |

`run_c1_green(...)` assemble ces sept contrôles dans cet ordre et les
retourne en liste, comme `run_g6_green` le fait pour le relief.

**`C1-D` peut légitimement rougir sur un cas réel** : deux polygones qui se
touchent en un seul point ont une longueur de frontière partagée nulle, donc
pas d'arête `land-sea`, tout en étant à distance nulle. Si cela se produit,
le compte est nommé et **c'est une escalade** (Waivers), jamais un
assouplissement du seuil ni un contrôle réécrit.

### D6 — Les valeurs déclarées, et leur justification écrite

Le bloc ajouté à `constants.py` (D9) porte exactement ces noms. Chacun est
accompagné, dans le fichier, d'une phrase disant **pourquoi cette valeur** —
un nombre sans justification est un nombre inventé.

| nom | valeur | justification à écrire |
|---|---|---|
| `C1_PIPELINE_VERSION` | `"1.15.0-c1-v1_080"` | la plus haute version déjà déclarée est `1.14.0-p2-v1_072` ; `1.15.0` est la suivante libre. Le tag `v1_080` ne collisionne avec aucun tag de journal déjà committé (`v1_049`, `v1_050`, `v1_051`, `v1_060`) ni avec `v1_052`, employé par le lot 024 |
| `C1_REGISTRY_CREATED` | `"2026-08-20"` | date figée (déterminisme), jamais une horloge murale — même discipline que `G3_REGISTRY_CREATED` |
| `C1_SOLAR_CONSTANT_W_M2` | `1367.0` | constante solaire de référence, watts par mètre carré au sommet de l'atmosphère. Valeur physique standard, pas un réglage |
| `C1_OBLIQUITY_DEG` | `23.45` | obliquité de l'écliptique, en degrés, dans l'approximation employée par la formule de déclinaison de D3 |
| `C1_ECCENTRICITY_FACTOR` | `0.033` | amplitude de la correction de distance Terre-Soleil sur l'année, dans la même approximation |
| `C1_DAYS_IN_YEAR` | `365` | l'année de la formule de D3 est de longueur fixe : une année bissextile rendrait la sortie dépendante d'une date, donc non déterministe |
| `C1_SUMMER_SOLSTICE_DAY` | `172` | rang du solstice d'été dans cette année de longueur fixe |
| `C1_WINTER_SOLSTICE_DAY` | `355` | rang du solstice d'hiver dans la même année |
| `C1_MJ_PER_J` | `1_000_000.0` | conversion joules → mégajoules, unité de publication de l'insolation annuelle |
| `C1_INSOLATION_DECIMALS` | `1` | le dixième de mégajoule par mètre carré et par an : plus fin serait du bruit de sommation, plus grossier écraserait l'écart entre deux cellules voisines |
| `C1_DAYLIGHT_DECIMALS` | `3` | le millième d'heure vaut `3.6` secondes — assez fin pour que deux latitudes distinctes se distinguent, assez grossier pour rester stable |
| `C1_DISTANCE_DECIMALS` | `1` | le décimètre, sur des distances de l'ordre de la centaine de kilomètres |
| `C1_MONOTONE_DLAT_DEG` | `0.01` | écart de latitude au-delà duquel la décroissance de l'insolation doit être **stricte** (`C1-B`, `C1-C`). Dérivé : sur la fenêtre, l'insolation annuelle varie d'environ `138` MJ/m² par degré de latitude, donc `0.01°` produit environ `1.4` MJ/m², très au-dessus du pas d'arrondi `C1_INSOLATION_DECIMALS`. En dessous de cet écart, deux cellules peuvent légitimement partager la même valeur arrondie |
| `C1_SEA_DISTANCE_EPS_M` | `1.0` | tolérance en mètres projetés sur les comparaisons de distance, alignée sur `LENGTH_EPS` déjà déclarée |
| `WORLD_TERMS_FORBIDDEN_KEYS` | voir ci-dessous | clés interdites dans tout artefact du monde physique : elles encodent un barème de jeu, ce que le principe n° 2 interdit |

`WORLD_TERMS_FORBIDDEN_KEYS` est un `frozenset` contenant au minimum, dans
les deux langues employées par le dépôt :
`bonus`, `malus`, `modifier`, `modifiers`, `multiplier`, `multiplicateur`,
`penalty`, `penalite`, `pénalité`, `yield_bonus`, `yield_modifier`,
`rendement_bonus`, `production_bonus`, `movement_cost`, `cout_deplacement`,
`score`, `weight`, `poids_gameplay`, `relative_intensity`, `climate_mod`.
Les deux derniers ne sont pas décoratifs : ce sont les noms exacts employés
par `unity/game_unity/Assets/StreamingAssets/data/terrain_endowment.json`, la
table de barème du jeu hérité — précisément la forme que le pipeline ne doit
jamais produire.

### D7 — Sorties exactes

Sous `pipeline/geo/` :

| fichier | contenu |
|---|---|
| `artifacts/cells_climate_drivers_c1.json` | par cellule : `cell_id`, `insolation_annual_mj_m2`, `daylight_h_summer_solstice`, `daylight_h_winter_solstice`, `polar_clamp_days`, `dist_sea_edge_m`, `dist_sea_centroid_m`, `nearest_sea_zone_id`, `hops_to_sea`, `coastal`, `centroid_inside_cell`. **Aucune géométrie, aucune latitude recopiée** : la clé spatiale reste `cell_id` et `cells_g3.json` reste la source unique (ADR-0003, principe n° 1) |
| `artifacts/stats_c1.json` | `cell_count`, `insolation_mj_m2` (au moins `min`, `median`, `max`), `daylight_amplitude_h` (`min`, `median`, `max`), `ecretages_polaires_total`, `coastal_cell_count_derive`, `coastal_cell_count_g4`, `dist_sea_centroid_m` (`min`, `median`, `max`), `mediane_dist_sea_centroid_par_saut` (tableau ordonné par nombre de sauts), `cellules_par_saut`, `cellules_atteintes_par_strait_seulement`, `cellules_centroide_hors_polygone`, `contact_ponctuel_sans_arete_land_sea` |
| `artifacts/MANIFEST_c1.json` | `pipeline_version`, `crs`, `inputs` (empreintes **calculées à l'exécution** de `cells_g3.json`, `sea_zones_g4.json`, `adjacency_g5.json`), `outputs` (empreintes des sorties). Aucune empreinte recopiée d'un autre manifeste |
| `registry/climate_drivers_registry.json` | registre des cellules émises, date `C1_REGISTRY_CREATED`, `pipeline_version` — même forme que `registry/river_registry.json` |
| `logs/v1_080_climate_drivers.log` | journal lisible de la preuve |
| `logs/v1_080_qa.json` | rapport : tableau `checks` (`7` entrées, `passed` + `red_proof`) et bloc `determinism.sha256` |
| `capture/v1_080_insolation_window.png` | insolation annuelle par cellule sur la fenêtre pilote, palette continue |
| `capture/v1_080_continentality_window.png` | `dist_sea_centroid_m` par cellule sur la même fenêtre |
| `steps/c1_climate_drivers.py` | le module neuf ; exporte `run_climate_drivers()` sans argument |
| `qa/checks_c1.py` | les six contrôles `C1-*` et `run_c1_green(...)` (D5) |
| `tests/run_proof_c1.py` | script de preuve (D10) |
| `tests/test_qa_red_c1.py` | cas rouges, un par contrôle (D11) |
| `README.md` | mise à jour (SC6) |

**Nommage.** Les préfixes `G1` à `G12` sont réservés par l'inventaire de
portage (`geo-pipeline-port-plan.md`), `P1`/`P2` par les propositions de
peuplement et `A12` par l'apparence. `C1` n'entre en collision avec aucun des
soixante-douze identifiants de contrôle relevés dans `qa/checks.py` : c'est
pourquoi il est choisi, et le nom de fichier d'étape
(`steps/c1_climate_drivers.py`) suit le même raisonnement plutôt qu'un rang
numérique déjà réservé par le plan de portage.

**Trois couples `must_differ_from`** doivent être déclarés dans
`deliverables/manifest.json` :

1. `deliverables/pre-edit/pipeline-geo-README.md.orig` ↔ le `README.md`
   publié ;
2. `deliverables/pre-edit/constants.py.orig` ↔ `pipeline/geo/constants.py`
   publié ;
3. `deliverables/pre-edit/pipeline.py.orig` ↔ `pipeline/geo/pipeline.py`
   publié.

Les instantanés pré-édition sont committés dans le répertoire du brief, et
ce sont eux qui rendent la reconstruction de SC5 (ajout seulement)
vérifiable par un tiers sans reconstruire l'historique git.

### D8 — Ce lot ajoute deux points d'accroche, sans changer le comportement d'aucun existant

C'est la différence de périmètre assumée avec les briefs 019, 021 et 024, qui
interdisaient toute modification de `constants.py` et `pipeline.py` parce que
leurs contrats **existaient déjà**. Ici ils n'existent pas : ils doivent être
écrits. L'autorisation est donc accordée, mais **bornée et mesurée**.

L'alternative — loger les constantes `C1_*` dans un fichier séparé pour ne
toucher à rien — a été examinée et écartée : `constants.py` est le seul
endroit du pipeline où une borne se déclare, et la fragmenter créerait deux
endroits où chercher une valeur, c'est-à-dire la première marche vers deux
réponses à la même question. Le précédent existe d'ailleurs dans le dépôt :
le brief 007 a modifié `constants.py` (trois bornes G3 re-dérivées, marquées
`# FORGEHISTORY-G3-REPAIR`) et le brief 007 a aussi introduit `pipeline.py`.
Ce que ce lot ajoute est strictement plus prudent, puisqu'il ne **change**
aucune valeur existante et que SC5 le prouve nom par nom. À l'inverse,
`qa/checks.py` n'a jamais été modifié depuis le portage du brief 002 et ne
l'est pas ici (D9).

**`pipeline/geo/constants.py`** : un bloc **ajouté en fin de fichier**,
introduit par une ligne de commentaire
`# --- C1 — déterminants physiques du climat (v1_080) ---`. Aucune ligne
existante n'est supprimée ni modifiée. Mesuré par
`constants_lignes_supprimees` (D12) et par la reconstruction de SC5.

**`pipeline/geo/pipeline.py`** : reçoit
`_load_climate_drivers_module()` (chemin explicite
`steps/c1_climate_drivers.py`, même patron que `_load_relief_module`),
`run_climate_drivers_c1()`, la valeur `"climate_drivers"` ajoutée à la liste
`choices` de `--source`, et une branche `if args.source == "climate_drivers":`
imprimant une ligne de résumé puis les empreintes. La ligne de résumé porte
au minimum : la projection, `cell_count`, la médiane d'insolation, la médiane
de `dist_sea_centroid_m`, `ecretages_polaires_total`.

**La valeur `climate` reste libre** : elle est réservée au lot futur qui
importera une source climatique réelle (Provenance). Employer `climate` ici
serait une sur-revendication.

**Deux lignes existantes au plus** peuvent être modifiées dans
`pipeline.py` : la dernière ligne de la chaîne d'aide de `--source` et la
chaîne `description` de l'analyseur d'arguments, toutes deux pour mentionner
la nouvelle valeur. Toute autre suppression est un dépassement de périmètre.
Mesuré par `pipeline_lignes_supprimees` (D12) et par la reconstruction de
SC5, qui vérifie en plus que **chacune des huit branches `--source`
préexistantes est byte-identique** à sa version d'origine.

### D9 — Ce qui reste intouché

`pipeline/geo/qa/checks.py` n'est **pas modifié**. Il a été écrit une seule
fois, au portage du brief 002, et n'a jamais bougé depuis ; il est la barre
qualité héritée. Les contrôles `C1-*` vivent dans un module frère
(`qa/checks_c1.py`) qui **importe** `CheckResult` et `q10_determinism` — ce
qui préserve à la fois l'unicité du contrat de forme et l'intégrité du
fichier hérité. Aucun artefact, registre, journal ou capture d'un lot
précédent n'est réécrit.

### D10 — Déterminisme : deux passes, empreintes comparées

`tests/run_proof_c1.py` :

1. charge **une fois** les cellules, les zones de mer et l'adjacence ;
2. exécute la dérivation **et l'export complet** deux fois ;
3. compare, empreinte par empreinte, les artefacts des deux passes (`Q10`) :
   chaque paire doit être égale et non vide ;
4. joue les sept contrôles de `run_c1_green`, chacun avec son cas rouge ;
5. écrit `logs/v1_080_qa.json` et `logs/v1_080_climate_drivers.log` ;
6. rend le code de sortie `0` si et seulement si les sept contrôles sont
   verts, chacun avec une preuve rouge non vide, et les deux passes
   identiques.

Aucune horloge murale, aucun horodatage courant, aucune graine non fixée dans
un artefact.

### D11 — Preuve rouge d'abord

`tests/test_qa_red_c1.py` fournit **un cas rouge par contrôle** des sept
assemblés par `run_c1_green` : `Q10`, `C1-A`, `C1-B`, `C1-C`, `C1-D`,
`C1-E`, `C1-F`. Chaque cas est une mutation locale explicite sur une copie en
mémoire — par exemple un `cell_id` retiré pour `C1-A` ; deux insolations
échangées entre une cellule du sud et une du nord pour `C1-B` ; une durée de
jour d'hiver portée au-dessus de celle d'été pour `C1-C` ; une cellule
littorale dont `dist_sea_edge_m` est poussée au-delà de l'epsilon pour
`C1-D` ; une médiane rendue non monotone en déplaçant une cellule de classe
de sauts pour `C1-E` ; une clé `multiplier` injectée pour `C1-F`. **Aucun cas
ne passe par une modification de `qa/checks.py` ni de `qa/checks_c1.py`.** Un
`red_proof` vide vaut échec du contrôle, même si le vert est vert.

### D12 — Périmètre de fichiers

**Autorisé (création) :**

- `pipeline/geo/steps/c1_climate_drivers.py` ;
- `pipeline/geo/qa/checks_c1.py` ;
- `pipeline/geo/tests/run_proof_c1.py`, `pipeline/geo/tests/test_qa_red_c1.py` ;
- les artefacts, journaux, registre et captures listés en D7 ;
- `harness/queue/briefs/025-geo-determinants-climat-c1/deliverables/**`.

**Autorisé (modification bornée, D8) :**

- `pipeline/geo/constants.py` — **ajout en fin de fichier uniquement**,
  zéro ligne supprimée ;
- `pipeline/geo/pipeline.py` — ajouts, plus **deux lignes existantes au
  plus** modifiées, les huit branches `--source` préexistantes restant
  byte-identiques ;
- `pipeline/geo/README.md` (SC6) ;
- `harness/queue/cost-ledger.jsonl` (une seule ligne ajoutée).

**Interdit (lecture seule, ou hors périmètre) :** `pipeline/geo/qa/checks.py` ;
`pipeline/geo/io_util.py` ; `pipeline/geo/projection.py` ;
`pipeline/geo/sources.lock` ; `pipeline/geo/steps/02_coastline.py` ;
`pipeline/geo/steps/02b_corrections_1400.py` ; `pipeline/geo/steps/03_cells.py` ;
`pipeline/geo/steps/03b_align_coastline_provenance.py` ;
`pipeline/geo/steps/04_adjacency.py` ; `pipeline/geo/steps/05_rivers.py` ;
tous les artefacts, registres, journaux et captures G2/G2-bis/G3/G4/G5 déjà
committés ; tout fichier sous `sim/` ou `unity/` ; `harness/*.py` ;
`harness/pipeline/` ; `architecture/` ; `docs/**` ; `VISION.md` ;
`ROADMAP.md` ; `hermes/**` ; `HANDOFF.md` ; `.github/**` ; les archives des
briefs 001 à 024, et en particulier **tout fichier du lot 024**, dont ce lot
ne lit ni les artefacts ni le verdict.

---

## Success Conditions

### SC1 — L'astronomie est calculée sur les vraies latitudes, et elle se comporte comme le Soleil (`C1-B`, `C1-C`)

Depuis `pipeline/geo/` :

```
../../.venv/bin/python tests/run_proof_c1.py
```

- `cellules_avec_insolation` égale le nombre de cellules lues de
  `cells_g3.json` ; aucune cellule sans valeur.
- `inversions_insolation_latitude` vaut `0` : l'insolation ne croît jamais
  quand la latitude croît.
- `egalites_insolation_hors_tolerance` vaut `0` : aucune paire consécutive
  dont les latitudes diffèrent d'au moins `C1_MONOTONE_DLAT_DEG` ne partage la
  même insolation arrondie.
- `paires_consecutives_au_dessus_du_seuil` est **strictement positif** et
  publié. Sans lui, la seconde moitié de `C1-B` porterait sur un ensemble
  éventuellement vide, et un contrôle sur un échantillon vide passe toujours
  (règle n° 6, et contrôle `no_empty_sample_pass` du gate mécanique).
- `cellules_jour_ete_non_superieur_hiver` vaut `0` et
  `inversions_amplitude_jour_latitude` vaut `0`.
- `ecretages_polaires_total` est **mesuré et rapporté**. Il est attendu nul
  sur cette fenêtre ; une valeur non nulle n'est pas un échec en soi mais un
  fait à consigner dans `deliverables/generator-log.md`, avec les cellules
  concernées nommées.
- `C1-B` et `C1-C` verts.

### SC2 — Les distances à la mer sont mesurées, et la littoralité de G4 les confirme (`C1-D`)

- `coastal_cell_count_derive` est dérivé des seules arêtes `land-sea` de
  `adjacency_g5.json`, **sans lire** `coastal_cell_ids`.
- `coastal_cell_count_g4` est lu de `stats_g4.json`, et
  `ecart_littoralite_c1_vs_g4` vaut `0` : les deux comptes coïncident. Un
  écart non nul est disqualifiant — il signalerait que la littoralité n'a pas
  été redérivée mais devinée.
- `cellules_littorales_hors_epsilon` vaut `0` : aucune cellule littorale
  n'a `dist_sea_edge_m` au-delà de `C1_SEA_DISTANCE_EPS_M`.
- `contact_ponctuel_sans_arete_land_sea` est **mesuré et rapporté** : c'est
  le nombre de cellules non littorales dont `dist_sea_edge_m` est pourtant
  sous l'epsilon. Attendu nul ; s'il ne l'est pas, `C1-D` rougit et le lot
  **escalade** (Waivers) — il ne déplace pas l'epsilon.
- `nearest_sea_zone_id` de chaque cellule existe dans
  `sea_zones_g4.json` : `zones_de_mer_inconnues` vaut `0`.
- `C1-D` vert.

### SC3 — Les deux dérivations de continentalité concordent (`C1-E`)

- `cellules_sans_hops` vaut `0` : toutes les cellules lues de
  `cells_g3.json` ont un nombre de sauts calculé. La sentinelle `-1` est
  réservée à un échec de calcul réel et n'apparaît pas si le parcours aboutit.
- `cellules_atteintes_par_strait_seulement` est **mesuré et rapporté** :
  c'est le nombre de cellules qui n'auraient aucun chemin vers le littoral
  sans les arêtes `strait`. Le Planificateur en a mesuré `21` (îles
  lacustres) ; ce nombre est un constat, pas un seuil, et une valeur
  différente n'est pas un échec — elle est à consigner.
- `classes_de_sauts_non_monotones` vaut `0` : la médiane de
  `dist_sea_centroid_m` croît strictement d'une classe de sauts à la
  suivante, sur les classes non vides prises dans l'ordre.
- `cellules_centroide_hors_polygone` est **mesuré et rapporté** ; pour toutes
  les autres, `violations_bord_vs_centroide` vaut `0`.
- `C1-E` vert.

### SC4 — La maille est inchangée et aucun barème n'est produit (`C1-A`, `C1-F`)

- `C1-A` vert : les `cell_id` de `cells_climate_drivers_c1.json` sont
  exactement ceux de `cells_g3.json`.
- `cles_de_bareme_trouvees` vaut `0` sur les quatre fichiers balayés (D5) ;
  `C1-F` vert.
- `artefacts_precedents_modifies` vaut `0` : `git status --porcelain` est
  vide sur `cells_g3.json`, `adjacency_g4.json`, `adjacency_g5.json`,
  `sea_zones_g4.json`, `stats_g3.json`, `stats_g4.json`, `stats_g5.json`,
  `rivers_g5.json`, `mouths_g5.json`, `adjacency_g3.json`,
  `topology_links_g4.json`, `adjacency_divergence_g4.json`,
  `MANIFEST_g3.json`, `MANIFEST_g4.json`, `MANIFEST_g5.json`.

### SC5 — Les deux fichiers partagés n'ont reçu que des ajouts, et rien d'existant n'a changé de sens

C'est la condition qui remplace, pour ce lot, le « `constants.py` intouché »
des briefs 019/021/024.

- `constants_lignes_supprimees` vaut `0` : le diff de
  `pipeline/geo/constants.py` par rapport à son instantané pré-édition
  committé ne contient aucune ligne supprimée.
- `constantes_preexistantes_inchangees` égale le nombre de noms de premier
  niveau présents dans l'instantané pré-édition : chacun est encore présent
  après l'ajout, avec la **même valeur**, comparée par la représentation
  Python de l'objet chargé — jamais par lecture visuelle du diff.
- `pipeline_lignes_supprimees` vaut au plus `2` (D8).
- `branches_source_preexistantes_identiques` vaut `8` sur `8` : chacune des
  huit branches `if args.source == "..."` de l'instantané pré-édition se
  retrouve byte-identique dans le fichier publié.
- `valeurs_source_preexistantes_conservees` vaut `8` sur `8` : les huit
  valeurs de `choices` d'origine sont toujours acceptées.
- `source_climate_non_employee` vaut `1` : la chaîne `"climate"` n'est
  employée comme valeur de `--source` nulle part (D8).

### SC6 — Le crochet neuf fonctionne, les preuves sont committées, le README ne sur-revendique pas

Depuis `pipeline/geo/` :

```
../../.venv/bin/python pipeline.py --source climate_drivers
```

- La commande sort en code `0` et affiche la ligne de résumé C1 (projection,
  `cell_count`, médiane d'insolation, médiane de distance à la mer,
  écrêtages polaires).
- `fichiers_preuve_suivis_par_git` égale le nombre de fichiers de preuve
  déclarés sous `pipeline/geo/` en D7, vérifié par `git ls-files`. Les
  répertoires `artifacts/`, `logs/` et `capture/` sont exclus par
  `pipeline/geo/.gitignore` : chaque preuve est donc committée par ajout
  forcé individuel, exactement comme les lots 019, 020 et 021 l'ont fait
  pour les leurs. **Aucun élargissement de `.gitignore` n'est autorisé.**
- `README.md` mis à jour : une section neuve décrit ce que C1 livre **et
  surtout ce qu'il ne livre pas** — ni température, ni précipitations, ni
  saisons, ni classification climatique, ces grandeurs restant suspendues au
  choix d'une source climatique par le propriétaire. Restent également non
  livrés : les ressources, les villes (G7), la possession (G8), les LOD
  (G9), les textures d'identifiants (G10), l'apparence (A12), G5-bis et
  G5-ter, ainsi que la QA de chaîne complète (G11/G12). Aucune
  sur-revendication ; le relief (G6) n'est **pas** décrit par ce lot, quel
  que soit l'état du lot 024.
- Les deux captures de D7 sont **regardées et décrites** dans
  `deliverables/generator-log.md` (règle n° 11) : la carte d'insolation doit
  montrer un dégradé nord-sud continu, la carte de continentalité un cœur
  continental clairement séparé des côtes.
- La suite du harnais reste verte (aucune régression) :

```
.venv/bin/python -m pytest harness/tests/ -q
```

  `tests_harness_passed_025` est rapporté avec le nombre de tests collectés
  pour dénominateur. **Aucun paquet de test n'est installé dans le venv de
  cette machine** (vérifié à l'écriture de ce brief — voir la table des
  Waivers) : le provisionnement est une étape normale et n'est pas soumis à
  D12. Si l'installation échoue réellement, le waiver dédié s'applique et
  `tests_harness_passed_025` vaut la sentinelle `-1`, jamais `0` ni un
  `PASS` supposé.

### SC7 — Déterminisme sur deux passes, sept contrôles verts, chacun mordant

- `paires_sha_determinisme_egales` égale le nombre total de paires du bloc
  `determinism.sha256` de `logs/v1_080_qa.json` ; ce total est strictement
  positif et aucune empreinte n'est vide. Deux passes qui ne comparent rien
  ne prouvent pas le déterminisme.
- `controles_c1_verts` vaut **7** sur `7`.
- `controles_c1_avec_preuve_rouge_non_vide` vaut **7** sur `7` (D11). Un vert
  sans preuve rouge ne compte pas : un contrôle qui ne peut pas rougir ne
  prouve rien (règle n° 4).
- `code_sortie_run_proof_c1` vaut **0**.

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. Produire une température, une précipitation, une humidité, un vent, une
   saison de culture ou une classe climatique, ni sous ce nom ni sous un
   autre. Ces grandeurs exigent une source climatique déclarée dans
   `sources.lock`, décision réservée au propriétaire (Provenance). Un champ
   nommé autrement mais qui prétendrait les représenter tombe sous la même
   interdiction.
2. Ajouter, modifier ou compléter `pipeline/geo/sources.lock`. Ce lot ne
   consomme aucune source externe et n'en déclare aucune.
3. Employer `"climate"` comme valeur de `--source` (D8) — elle est réservée.
4. Modifier `pipeline/geo/qa/checks.py` (D9), ni recopier ses fonctions au
   lieu de les importer.
5. Modifier ou régénérer un artefact, registre, journal ou capture d'un lot
   précédent — y compris `cells_g3.json`, `adjacency_g5.json` et
   `sea_zones_g4.json`.
6. Lire, écrire, exécuter ou juger quoi que ce soit du lot 024 (relief G6) :
   ni son brief, ni ses artefacts, ni son verdict, ni son worktree. Ce lot
   est indépendant de son résultat.
7. Livrer les ressources, les villes (G7), la possession (G8), les LOD (G9),
   les textures d'identifiants (G10), l'apparence (A12), G5-bis, G5-ter ou
   la QA de chaîne complète (G11/G12).
8. Recopier une valeur hexadécimale d'empreinte dans un test, un document ou
   un commentaire (règle n° 12) : les empreintes se comparent à l'exécution
   et se nomment par leur source.
9. Reprendre l'un des nombres de contexte de ce brief comme seuil de
   contrôle. Les valeurs mesurées par le Planificateur (`596`, `372`, `21`,
   `11 444.2`, `7 149.7`, les médianes par saut) sont des constats de
   contexte ; un contrôle qui s'y comparerait serait un contrôle qui nomme sa
   propre référence (règle n° 2).
10. Employer l'alias nu de l'interpréteur ni un chemin Windows ; sur cette
    machine l'interpréteur est `.venv/bin/python` (règle n° 1).
11. Committer, pousser, créer ou changer de branche. L'orchestrateur seul
    dépose, et le producteur ne fusionne jamais son propre travail
    (ADR-0014).
12. Rapporter un compteur depuis un échantillon vide ou un calcul manqué sans
    le déclarer comme tel (règle n° 8 : la sentinelle du projet pour « non
    calculé » est `-1`, jamais `0` — un zéro réellement mesuré, par exemple
    `ecretages_polaires_total = 0`, est légitime et s'en distingue).
13. Réinterpréter en silence une des décisions D2-D9 : si le Générateur
    constate en écrivant le code une lecture incompatible avec ce brief,
    c'est une escalade (Waivers), pas une réinterprétation tacite.

---

## Required Counters (sous-ensemble ; le détail complet est dans les Success Conditions)

| nom | source | dénominateur |
|---|---|---|
| `cellules_avec_insolation` | cellules de `cells_climate_drivers_c1.json` dont `insolation_annual_mj_m2` est présent et non `null` | cellules lues de `cells_g3.json` |
| `inversions_insolation_latitude` | paires **consécutives** du tri par latitude croissante dont l'insolation croît | paires consécutives totales ; doit valoir `0` |
| `egalites_insolation_hors_tolerance` | paires **consécutives** dont les latitudes diffèrent d'au moins `C1_MONOTONE_DLAT_DEG` et dont l'insolation arrondie est égale | paires consécutives dont l'écart de latitude atteint le seuil (compte à publier, strictement positif — sinon le contrôle est vide) ; doit valoir `0` |
| `paires_consecutives_au_dessus_du_seuil` | paires consécutives dont l'écart de latitude atteint `C1_MONOTONE_DLAT_DEG` | paires consécutives totales ; **doit être strictement positif** — c'est la preuve que la seconde moitié de `C1-B` et `C1-C` n'est pas un contrôle vide (règle n° 6) |
| `cellules_jour_ete_non_superieur_hiver` | cellules dont la durée de jour au solstice d'été n'excède pas celle d'hiver | cellules totales ; doit valoir `0` |
| `inversions_amplitude_jour_latitude` | paires consécutives où l'amplitude été-moins-hiver décroît quand la latitude croît | paires consécutives totales ; doit valoir `0` |
| `ecretages_polaires_total` | somme de `polar_clamp_days` | jours-cellule évalués (`cellules × C1_DAYS_IN_YEAR`) ; fait mesuré |
| `coastal_cell_count_derive` | cellules portant au moins une arête `land-sea` | cellules totales |
| `ecart_littoralite_c1_vs_g4` | valeur absolue de la différence avec `coastal_cell_count` de `stats_g4.json` | `1` comparaison ; doit valoir `0` |
| `cellules_littorales_hors_epsilon` | cellules littorales dont `dist_sea_edge_m` dépasse `C1_SEA_DISTANCE_EPS_M` | cellules littorales dérivées ; doit valoir `0` |
| `contact_ponctuel_sans_arete_land_sea` | cellules non littorales dont `dist_sea_edge_m` est sous l'epsilon | cellules non littorales ; fait mesuré, une valeur non nulle escalade |
| `zones_de_mer_inconnues` | `nearest_sea_zone_id` absents de `sea_zones_g4.json` | cellules totales ; doit valoir `0` |
| `cellules_sans_hops` | cellules sans `hops_to_sea` calculé | cellules totales ; doit valoir `0` |
| `cellules_atteintes_par_strait_seulement` | cellules sans chemin `land-land` vers le littoral | cellules totales ; fait mesuré |
| `classes_de_sauts_non_monotones` | classes de sauts dont la médiane de `dist_sea_centroid_m` ne dépasse pas celle de la classe précédente | classes non vides moins une ; doit valoir `0` |
| `cellules_centroide_hors_polygone` | cellules dont le centroïde committé n'est pas dans leur propre polygone | cellules totales ; fait mesuré |
| `violations_bord_vs_centroide` | cellules à centroïde intérieur où `dist_sea_edge_m` dépasse `dist_sea_centroid_m` au-delà de l'epsilon | cellules à centroïde intérieur ; doit valoir `0` |
| `cles_de_bareme_trouvees` | clés de `WORLD_TERMS_FORBIDDEN_KEYS` rencontrées dans les quatre fichiers balayés | clés du `frozenset` ; doit valoir `0` |
| `controles_c1_verts` | tableau `checks` de `logs/v1_080_qa.json` | `7` |
| `controles_c1_avec_preuve_rouge_non_vide` | champ `red_proof` de chaque entrée | `7` |
| `paires_sha_determinisme_egales` | bloc `determinism.sha256` de `logs/v1_080_qa.json` | total de paires ; strictement positif |
| `code_sortie_run_proof_c1` | code de sortie de `tests/run_proof_c1.py` | `1` exécution ; doit valoir `0` |
| `constants_lignes_supprimees` | lignes supprimées au diff contre l'instantané pré-édition | `1` mesure ; doit valoir `0` |
| `constantes_preexistantes_inchangees` | noms de premier niveau de l'instantané pré-édition encore présents et de même valeur | nombre de noms de l'instantané ; doit être complet |
| `pipeline_lignes_supprimees` | lignes supprimées au diff contre l'instantané pré-édition | `1` mesure ; doit valoir au plus `2` |
| `branches_source_preexistantes_identiques` | branches `if args.source == "..."` byte-identiques | `8` |
| `valeurs_source_preexistantes_conservees` | valeurs de `choices` d'origine encore acceptées | `8` |
| `source_climate_non_employee` | absence de `"climate"` comme valeur de `--source` | `1` ; doit valoir `1` |
| `artefacts_precedents_modifies` | `git status --porcelain` sur les quinze artefacts G3/G4/G5 committés | `15` ; doit valoir `0` |
| `fichiers_preuve_suivis_par_git` | `git ls-files` croisé avec les preuves déclarées en D7 | nombre de preuves déclarées |
| `tests_harness_passed_025` | tests réussis de `harness/tests/` | tests collectés (SKIP Linux/Unity acceptés et déclarés) ; sentinelle `-1` si le provisionnement échoue (Waivers) — jamais `0` |

Un script committé sous
`harness/queue/briefs/025-geo-determinants-climat-c1/deliverables/measure_c1_025.py`,
exécuté depuis la racine, imprime chaque compteur avec son dénominateur,
dérivé des artefacts et des constantes — jamais une valeur recopiée à la
main.

---

## Acceptable Waivers (si une impossibilité est invoquée)

Une impossibilité s'éprouve avant d'être invoquée : une commande et le
message d'erreur qu'elle produit (règle n° 9).

| affirmation d'impossibilité | commande exigée | erreur attendue |
|---|---|---|
| « la pile scientifique n'est pas installée » | `.venv/bin/python -c "import shapely, geopandas, pyproj, matplotlib; print('ok')"` depuis la racine | `ModuleNotFoundError` nommant le module — **vérifié à l'écriture de ce brief** : le venv de la racine est un environnement Python `3.12.3` nu, aucun paquet de `pipeline/geo/requirements.txt` n'y est installé, et l'import échoue d'abord sur `shapely`. `requirements.txt` déclare déjà les huit paquets nécessaires ; la provision normale est `.venv/bin/pip install -r pipeline/geo/requirements.txt` et ne relève pas de D12. Le waiver ne s'applique que si cette installation elle-même échoue (réseau, dépôt inaccessible), jamais au simple constat initial d'absence. **`rasterio` n'est pas requis par ce lot** : aucune donnée matricielle n'est lue |
| « le paquet de test du harnais n'est pas installé » | `.venv/bin/python -m pytest --version` depuis la racine | `No module named pytest` — **vérifié à l'écriture de ce brief**. Ce paquet n'est déclaré dans aucun fichier de dépendances du dépôt alors que `harness/tests/*.py` est écrit dans le style de découverte correspondant. C'est de l'outillage de test, pas du code produit : le Générateur peut l'installer sans toucher un fichier protégé par D12. Si l'installation échoue, `tests_harness_passed_025` vaut `-1` et le fait est consigné dans `deliverables/generator-log.md` |
| « un artefact d'entrée n'est pas lisible » | `.venv/bin/python -c "import json; json.load(open('pipeline/geo/artifacts/sea_zones_g4.json'))"` depuis la racine | `FileNotFoundError` ou `JSONDecodeError` nommant le fichier |
| « `C1-D` ne peut pas être vert : des cellules non littorales touchent la mer en un point » | sortie de `tests/run_proof_c1.py` montrant `contact_ponctuel_sans_arete_land_sea` non nul, avec les cellules nommées | le rapport réel. **Si invoquée, SC2 n'est pas excusée** : c'est une escalade vers le propriétaire — soit `C1-D` est reformulé par un amendement du Planificateur, soit l'incohérence G4/géométrie est un défaut à corriger dans un lot dédié. Le Générateur ne déplace ni `C1_SEA_DISTANCE_EPS_M`, ni le contrôle |
| « la médiane de la distance ne croît pas avec le nombre de sauts » | sortie de `tests/run_proof_c1.py` montrant les médianes par classe | les médianes réelles. C'est un motif de **blocage** de SC3 : le Planificateur a mesuré la propriété vraie sur une approximation majorante (`13.4` / `148.9` / `307.3` / `412.2` / `646.5` km), et la vraie distance polygone ne peut que la renforcer. Un échec signale une erreur de dérivation, pas un fait de monde plausible |
| « une cellule n'a aucun `hops_to_sea` calculable » | sortie de `tests/run_proof_c1.py` nommant la cellule et ses arêtes par nature | la sortie réelle. Le Planificateur a mesuré que les `596` cellules sont atteignables en incluant les arêtes `strait` ; une cellule isolée serait un fait nouveau, à consigner et à escalader, jamais un `hops_to_sea = 0` par défaut |

---

## Execution Contract

### Interpréteur et commandes

Sur cette machine Linux, l'interpréteur est `.venv/bin/python` depuis la
racine du dépôt, `../../.venv/bin/python` depuis `pipeline/geo/`. L'alias nu
est interdit (règle n° 1) et un chemin Windows n'existe pas ici. Aucune
commande de ce lot n'a besoin d'Unity : aucun worker Windows n'est requis, et
aucune condition de succès ne dépend d'une preuve Unity.

### Estimation d'appels d'outils

**Estimation du Planificateur : `120` appels d'outils.** Sous le seuil de
`150` au-delà duquel un brief doit être découpé, et sous l'arrêt du budget à
`160`. Ancres employées : le lot 021 (fleuves G5) et le lot 024 (relief G6)
ont une structure identique — un module d'étape, un script de preuve, un
fichier de cas rouges, cinq à sept artefacts, deux captures, une mise à jour
de README. Ce lot n'a **ni téléchargement, ni lecture matricielle**, ce qui
le rend plus court que 024, mais il écrit **en plus** un module de contrôles
et deux crochets, ce qui le rallonge. À vérifier avant génération :

```
.venv/bin/python harness/budget.py split-check \
  --brief harness/queue/briefs/025-geo-determinants-climat-c1 \
  --estimated-calls 120
```

### Preuves committées et re-vérifiables

`pipeline/geo/.gitignore` exclut `artifacts/`, `logs/` et `capture/`. Chaque
fichier de preuve déclaré en D7 est donc committé par ajout forcé
individuel, jamais par élargissement de `.gitignore`. Une preuve qu'un clone
frais ne retrouve pas n'est pas une preuve
(`check_declared_files_are_tracked` du gate mécanique).

### Deliverables obligatoires

Sous `harness/queue/briefs/025-geo-determinants-climat-c1/deliverables/` :

- `manifest.json` — `files[]` (tout fichier produit ou modifié, avec les
  trois couples `must_differ_from` de D7), `counters[]` (chaque compteur des
  Required Counters, avec sa valeur, sa `sample_size` réelle et la commande
  qui l'a produite), `waivers[]` (chacun avec sa commande et son erreur) ;
- `generator-log.md` — en français clair : ce qui a été fait, dans quel
  ordre, ce qui a résisté, la description **vue** des deux captures (règle
  n° 11), et tout écart avec les mesures de contexte du Planificateur ;
- `measure_c1_025.py` — le script de reconstruction des compteurs ;
- `pre-edit/pipeline-geo-README.md.orig`, `pre-edit/constants.py.orig`,
  `pre-edit/pipeline.py.orig` — les trois instantanés pré-édition.

### Interdictions pour le Générateur

Il ne prononce jamais la recevabilité de son propre travail, ne rédige aucun
`verdict.md`, ne modifie ni `brief.md` ni `eval-rubric.md`, ne commite pas,
ne pousse pas, ne crée ni ne change de branche, et ne fusionne rien
(ADR-0014).

### Fin de lot

Le lot est terminé quand `tests/run_proof_c1.py` sort en code `0`, que
`pipeline.py --source climate_drivers` sort en code `0`, que les **sept**
conditions de succès (SC1 à SC7) sont couvertes par des compteurs
reconstruits, et que les deliverables ci-dessus sont committés par
l'orchestrateur.

---

## Registre de coût

Une ligne, sans `--audit-id` (ce brief naît de la feuille de route et de la
demande `DEMANDE-20260820-claude-code-prochains-briefs.md`, pas d'un audit
converti) :

```
.venv/bin/python harness/backends/ledger.py append --backend cursor \
  --brief harness/queue/briefs/025-geo-determinants-climat-c1 \
  --event generator-run
```
