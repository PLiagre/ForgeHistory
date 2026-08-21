# Brief 026 : les gisements extractifs de 1400 (R1) — ce que la terre donne, déclaré et rattaché par contenance

**Authored**: 2026-08-20T21:20:00Z
**Author**: forge-planificateur
**Amendé**: 2026-08-21T07:13:06Z — `amendment-001-arbitrage-gisements.md`
**Statut**: **PRÊT SOUS CONDITION — l'arbitrage produit est rendu ; le lot ne
s'exécute qu'après la fusion du lot 025**

> ## ⚠️ Un seul préalable reste, et il n'est pas satisfait aujourd'hui
>
> **L'arbitrage produit a eu lieu.** Le propriétaire a tranché les trois
> questions `A1`, `A2` et `A3` le 2026-08-21
> (`hermes/requests/DEMANDE-20260821-arbitrage-gisements-026.md`), et
> l'amendement `amendment-001-arbitrage-gisements.md` de ce répertoire porte
> les réponses. Le contenu de données de ce lot est donc décidé.
>
> **Reste la dépendance dure : le lot 025 doit être fusionné avant celui-ci**
> (voir « Provenance » ci-dessous). Vérifié à l'amendement :
> `WORLD_TERMS_FORBIDDEN_KEYS` n'existe pas encore dans
> `pipeline/geo/constants.py`. Le Générateur vérifie les **deux** préalables
> avant sa première action (`SC0`, puis « Execution Contract »), par des
> commandes et non par un souvenir. S'il en manque un, il s'arrête, ne produit
> aucun fichier, et le signale.

> **Note de transparence (contrat du Planificateur) :** le rôle signataire est
> le rôle natif du harnais `forge-planificateur`. L'acteur réel est Claude
> Code (CTO), invoqué en session interactive **par Hermes, pilote du projet,
> sur décision du propriétaire**, à partir de la tâche autoritaire
> `hermes/requests/DEMANDE-20260820-claude-code-prochains-briefs.md` (statut
> `HANDED_TO_CTO`) et de `ROADMAP.md` (F1, jalon E1, « Prochaines étapes »
> point 7). Cette session n'a ni modifié `ROADMAP.md`, ni `hermes/**`, ni
> `docs/**`, ni lancé Cursor, ni committé. Le lot 024 (relief G6) était en
> cours d'exécution dans un worktree isolé au moment de l'écriture : ce
> brief ne le lit pas, ne le juge pas et n'en dépend pas.
>
> **Amendement du 2026-08-21 :** même rôle signataire, même acteur réel,
> saisi par Hermes après la décision du propriétaire du même jour
> (`hermes/requests/DEMANDE-20260821-arbitrage-gisements-026.md`). La session
> d'amendement n'a écrit que sous ce répertoire de brief, n'a rien committé,
> rien poussé, rien fusionné, et n'a lancé ni Cursor ni ForgePilot.

---

## Arbitrage rendu : ce que le propriétaire a tranché

Ce lot pose **quels gisements existent dans le monde de ForgeHistory en
1400**. Ce n'est pas une question d'ingénierie : c'est le premier maillon de
l'économie du jeu, et il décide où naîtront des villes minières, quelles
régions seront riches, et quelles routes compteront. Rien dans le dépôt ne
tranchait cette question — ni `ROADMAP.md` (qui dit « ressources » sans dire
lesquelles ni d'où elles viennent), ni `VISION.md`, ni aucun ADR. Le
Planificateur **n'a donc pas pris cette décision** et l'a remontée, comme la
demande `DEMANDE-20260820-claude-code-prochains-briefs.md` l'exige
explicitement (« signaler toute décision produit ou d'architecture qui ne peut
pas être déduite des décisions existantes, au lieu de la prendre
silencieusement »).

**Le propriétaire a tranché le 2026-08-21**
(`hermes/requests/DEMANDE-20260821-arbitrage-gisements-026.md`). Les réponses
sont portées par `amendment-001-arbitrage-gisements.md`, dans ce répertoire.
Le tableau `A1`/`A2`/`A3` ci-dessous porte désormais les réponses, pas des
propositions.

### Ce qui était déjà déductible du dépôt, et n'a jamais eu besoin d'arbitrage

Le **mécanisme** de la couche des ressources se déduit entièrement de
décisions déjà prises et déjà committées : une donnée historique se déclare
dans un fichier à part avec sa provenance, sa date et son degré de certitude
(`data/corrections_1400.json`, brief 002) ; elle se coupe et se remet sans
laisser de trace (réversibilité G2-bis / G4) ; elle se rattache par
contenance et jamais par proximité (`p1c_containment_only`,
`g7cities_a_containment_only`) ; elle ne disparaît jamais en silence
(`p1b_no_silent_omission`) ; et la géographie ne porte pas de barème
(principe n° 2). Tout ce que ce brief dit du mécanisme — D1, D3, D5 à D11 —
tient sous ces décisions et ne demande rien à personne.

### Les trois questions qui appartenaient au propriétaire, et ses réponses

| # | question | réponse du propriétaire (2026-08-21) |
|---|---|---|
| **A1** | La couche des ressources peut-elle reposer sur de la **connaissance historique générale, non sourcée par citation primaire** ? | **Oui**, à la condition que le degré de certitude soit **déclaré honnêtement gisement par gisement**. La réserve du Planificateur est levée : s'appuyer sur `P1_PROVENANCE` / `P2_PROVENANCE` n'était qu'un raisonnement, c'est désormais une autorisation. Aucune citation primaire n'est exigée, aucune source minière n'entre dans `sources.lock` |
| **A2** | La liste d'amorce de D4 — **vingt-sept gisements, dix natures de ressource** — est-elle celle que le monde doit contenir ? | **Oui, comme amorce provisoire**, non exhaustive et remplaçable. L'amendement **retient la liste de D4** sans en retirer ni en ajouter aucune entrée ; il l'amende d'une seule colonne, `richness_class` (`A3`). Identifiants, noms, natures, coordonnées et certitudes sont inchangés |
| **A3** | « Ressource » signifie-t-elle ici **présence d'un gisement travaillé**, sans aucune quantité ? | **Oui sur les quantités — et une exigence en plus.** Un gisement porte **trois** choses : sa présence, son type de ressource, et une **classe qualitative de richesse**. Il ne porte aucune quantité numérique, aucune réserve, aucun tonnage, aucun rythme d'extraction : ces grandeurs restent du ressort de `sim/`. Le vocabulaire fermé de la classe est tranché par l'amendement et porté ici par D3, D5, D6 et SC6 |

### Comment le déblocage se constate, mécaniquement

Ce brief est exécutable quand, et seulement quand, un fichier
`amendment-001-arbitrage-gisements.md` existe dans ce même répertoire,
signé par le Planificateur, et qui :

1. cite la décision écrite du propriétaire par son chemin (une demande sous
   `hermes/requests/` ou un ADR sous `docs/adr/`) ;
2. répond aux trois questions `A1`, `A2`, `A3` par oui ou par une
   reformulation explicite ;
3. dit **explicitement** ce qu'il fait de la liste de D4 : soit il la retient,
   et il le dit sans la recopier ; soit il la remplace, et il porte alors la
   liste retenue **en entier**, D4 renvoyant explicitement à lui. Jamais deux
   tables concurrentes dans le même répertoire — l'unique instruction de
   l'exécutant reste ce répertoire de brief, et deux listes qui dérivent l'une
   de l'autre le romprait (`CLAUDE.md` › Single Source of Instruction).

**Ce que l'amendement du 2026-08-21 a fait :** il retient D4 et n'en recopie
pas la table. La liste à écrire dans `data/resources_1400.json` est donc **D4
tel qu'amendé**, colonne `richness_class` comprise.

Le Générateur vérifie l'existence de ce fichier **avant sa première action**
(SC0). Il ne l'écrit jamais lui-même : ce serait le producteur s'accordant sa
propre autorisation.

**Ce que l'arbitrage n'a pas remis en cause :** les décisions D1 et D3 à D11
tiennent. L'ajout de la classe de richesse touche D2 (point 4), D3 (un champ
et un en-tête de plus), D4 (une colonne), D5 (le vocabulaire déclaré), D6 (le
contrôle `R1-G`) et ajoute SC6 ; le mécanisme de rattachement, de
réversibilité et de déterminisme ne bouge pas. Aucune condition de succès
n'est formulée en fonction du **contenu** de la liste ni du classement (D2),
et c'est délibéré.

---

## Provenance

### Dépendance dure : le lot 025 doit être fusionné avant celui-ci

Ce lot lit `WORLD_TERMS_FORBIDDEN_KEYS`, la liste des clés de barème
interdites, que **le brief 025 ajoute à `pipeline/geo/constants.py`**. Sans
elle, le contrôle `R1-E` (D6) n'a pas de référence à lire et devrait la
recopier — ce qui en ferait un contrôle qui nomme sa propre référence (règle
n° 2). Ce lot ne s'exécute donc qu'**après** la fusion du lot 025. C'est la
seule dépendance : ce brief ne lit aucun artefact produit par 025, ni aucun
artefact produit par 024.

Un seul lot s'exécute à la fois (décision propriétaire rappelée dans
`DEMANDE-20260820-claude-code-prochains-briefs.md`) : préparer 025 et 026
d'avance n'autorise pas à les lancer ensemble.

### Ce lot n'est pas un portage : il n'a pas d'ancêtre non plus

Vérifié sur le dépôt au moment de l'écriture :

- `harness/queue/geo-pipeline-port-plan.md` énumère l'intégralité de ce que
  VictoriaProject contenait — douze scripts d'étape plus deux modules de QA
  de chaîne. **Aucune étape de ressources.**
- `pipeline/geo/qa/checks.py` ne contient **aucun identifiant `R1-*`** parmi
  ses soixante-douze contrôles.
- `pipeline/geo/constants.py` ne contient **aucune constante de ressource**.
- `pipeline/geo/sources.lock` ne déclare **aucune source géologique ou
  minière**.

Ce que le jeu hérité contient à la place est instructif, et c'est
exactement ce qu'il ne faut pas reproduire :
`unity/game_unity/Assets/StreamingAssets/data/terrain_endowment.json` est une
**table de barème** — un couple (terrain, climat) y produit un bien avec une
`relative_intensity` et un `climate_mod` multiplicateur. C'est du réglage de
jeu, pas un fait de monde ; le principe n° 2 l'interdit dans le pipeline
géographique, et le contrôle `R1-E` de ce lot le rend mécaniquement
impossible.

À partir d'ici, **ce `brief.md` est la SEULE instruction** (voir `CLAUDE.md`
› Single Source of Instruction).

### Ce lot ne livre qu'une moitié des ressources, et le dit

Les ressources d'un monde se séparent en deux familles qui n'ont pas la même
nature :

- **Ce que l'on extrait du sol** — sel, métaux, charbon, alun. Leur présence
  est géologique et historique : elle ne se déduit ni du climat, ni du
  relief, ni de la latitude. Elle se **déclare**, avec sa provenance et son
  degré de certitude, exactement comme les corrections de 1400 du brief 002
  (`data/corrections_1400.json`) et les surcharges de navigabilité prévues
  par G5-bis. **C'est ce que ce lot livre.**
- **Ce que le sol produit** — céréales, pâture, bois, vigne. Cela dépend du
  climat et du sol, dont aucun n'est disponible : le brief 025 ne livre que
  les déterminants physiques du climat (insolation, continentalité), pas la
  température ni les précipitations, et aucune source pédologique n'est
  déclarée. **Ce n'est pas livré ici**, et l'inventer serait exactement la
  donnée fabriquée en silence que la règle n° 10 interdit.

---

## World-Terms Requirement

**Chaîne causale.**

Un gisement, c'est un fait têtu : il y a du sel sous Wieliczka et il n'y en a
pas sous Paris. Ce fait ne se négocie pas, ne se déplace pas, et il décide
d'une longue chaîne de conséquences que le monde produira plus tard :

1. **On extrait là où c'est.** Un village de saliniers naît sur la saumure,
   pas à côté. Une route se creuse vers la mine, pas ailleurs. La
   localisation d'un gisement est donc une contrainte spatiale, pas une
   propriété d'un tableau de valeurs.
2. **Ce qui est extrait doit être transporté.** Le sel de Lüneburg n'arrive
   pas magiquement à Lübeck : il descend une route, puis un bateau. C'est le
   principe n° 3 — l'économie est physique, rien ne se téléporte. Ce lot
   n'écrit aucun transport ; il pose l'**origine**, sans laquelle la chaîne
   origine → transport → stockage → destination n'a pas de premier maillon.
3. **La rareté crée la valeur, la valeur crée l'histoire.** L'alun de Phocée
   est le monopole génois qui finance une flotte ; l'étain de Cornouailles
   est ce qui rend le bronze possible. Ce lot ne code **aucun** prix,
   **aucun** rendement, **aucune** quantité — seulement la présence, sourcée
   et datée, et la classe qualitative que le propriétaire a décidée.

**Interdit** dans ce lot : aucun barème, aucune intensité relative, aucun
multiplicateur, et — spécifiquement ici — **aucune quantité**. Un gisement de
ce lot dit « il y a du fer ici, on l'y travaillait autour de 1400, et ce qu'on
en tirait portait jusque-là ». Il ne dit jamais « combien », ni « à quel
rythme », ni « pour combien de temps ». La quantité extraite dépendra de qui
creuse, avec quels outils, à quel moment : c'est de la simulation, elle
appartient à `sim/`, et l'encoder ici figerait dans la géographie une décision
qui n'y a pas sa place. Le contrôle `R1-E` (D6) rend cette interdiction
mécanique.

**La classe de richesse ne desserre pas cet interdit — elle le teste.** Elle
est un **nom** pris dans un vocabulaire fermé de trois valeurs, jamais un
nombre, jamais un rang, jamais un coefficient, et jamais une propriété de la
cellule. C'est précisément la forme que prendrait un barème si on la laissait
glisser : `terrain_endowment.json`, dans le jeu hérité, est exactement cela —
un couple (terrain, climat) qui produit un bien avec une `relative_intensity`
et un `climate_mod`. Le contrôle `R1-G` (D6) rend cette frontière mécanique,
et SC6 la mesure.

---

## Vocabulaire (expliqué une fois)

- **gisement (`deposit`)** : un lieu ponctuel où une ressource extractive
  était travaillée autour de 1400, déclaré par un identifiant, un nom, une
  nature de ressource, une classe de richesse, une position en
  longitude/latitude, une raison historique, une date, une source et un degré
  de certitude.
- **nature de ressource (`resource`)** : l'une des valeurs de
  `R1_VALID_RESOURCE_KINDS` (D5). Une valeur hors de cette liste est un refus,
  jamais un rattrapage silencieux.
- **classe de richesse (`richness_class`)** : l'une des **trois** valeurs de
  `R1_VALID_RICHNESS_CLASSES` (D5), et rien d'autre. C'est la classe
  qualitative décidée par le propriétaire le 2026-08-21. Son **critère
  d'attribution** est écrit une fois pour toutes ici — ce que le monde
  constatait du gisement autour de 1400, c'est-à-dire jusqu'où son produit
  s'échangeait et jusqu'où le site était connu :
  - `mineure` : ce qu'on en tirait se travaillait et se consommait sur place —
    la vallée, le pays immédiat. Au-delà, le site n'était pas connu.
  - `notable` : son produit alimentait sa région et les marchés voisins ; on
    le connaissait à l'échelle d'un pays ou d'un bassin.
  - `majeure` : son produit s'échangeait loin de son lieu d'extraction, et le
    site était connu bien au-delà de son pays.

  Ce critère est **observable** et de même nature que le reste de la
  déclaration. Il ne prétend **pas** mesurer la richesse géologique du
  gisement — teneur du minerai, étendue du filon : ce sont des mesures, le
  dépôt n'en a aucune pour 1400, et les déclarer serait la donnée fabriquée en
  silence que la règle n° 10 interdit. La classe est un **nom**, jamais un
  nombre : ni rang, ni indice, ni coefficient, ni multiplicateur, ni taille de
  point sur une carte, ni propriété d'une cellule (`R1-G`, D6).
- **degré de certitude (`certainty`)** : l'un des quatre niveaux déjà
  employés par `data/corrections_1400.json` — `attested`, `reconstructed`,
  `reconstructed_established`, `gameplay`. Ce lot n'en invente aucun et n'en
  ajoute aucun.
- **rattachement par contenance** : un gisement appartient à la cellule dont
  le polygone **contient** son point projeté. Jamais à la cellule la plus
  proche : un plus-proche-voisin sans borne rattacherait un point en pleine
  mer à une côte lointaine, et c'est la leçon que `P1_DUPLICATE_PROXIMITY_M`
  et `P2_MAX_MATCH_DISTANCE_KM` ont déjà payée dans `constants.py`.
- **déclaration coupée** : l'exécution avec `--no-corrections`, dans laquelle
  aucune déclaration n'est appliquée. C'est le mécanisme de réversibilité
  déjà employé par les corrections de 1400 (G2-bis) et les liens
  topologiques (G4).

---

## Ce qui existe déjà, et que ce lot lit sans le refaire

Vérifié sur le dépôt au moment de l'écriture. Les nombres sont des
**constats de contexte**, non des seuils : aucun contrôle ne s'y compare
(règle n° 2).

- `pipeline/geo/artifacts/cells_g3.json` : committé, suivi par git, lecture
  seule. `596` cellules, identifiants `1175` à `10466`, `geometry` en
  **EPSG:3035**, `centroid` portant `lat`, `lon`, `x_m`, `y_m`.
- `pipeline/geo/constants.py` : `PILOT_WINDOW_LONLAT`, mesurée à l'écriture à
  `(-11.320281, 29.7, 34.820281, 61.5)` ; `TARGET_CRS` (`EPSG:3035`) ;
  `FLOAT_DECIMALS` ; et, **après le lot 025**, `WORLD_TERMS_FORBIDDEN_KEYS`.
- `pipeline/geo/data/corrections_1400.json` : committé, lecture seule.
  C'est le **patron de forme** à suivre pour le fichier de déclarations de ce
  lot : `version`, `comment`, `enabled_by_default`,
  `valid_certainty_levels`, `valid_operations`, puis la liste des entrées,
  chacune portant `id`, `historical_reason`, `date`, `date_note`, `source`,
  `certainty`.
- `pipeline/geo/qa/checks.py` : expose `CheckResult` et `q10_determinism`,
  ainsi que les patrons de contrôle dont ce lot s'inspire sans les modifier —
  `g2b_a_corrections_have_certainty_and_source` (certitude et source
  obligatoires), `g5b_b_reversibility` (couper la déclaration rend l'état de
  référence), `g5b_d_no_upstream_limit_encoded` (parcours récursif de clés
  interdites), `p1c_containment_only` et `g7cities_a_containment_only`
  (contenance seule), `p1b_no_silent_omission` et
  `g7cities_b_three_categories_sum` (la somme des catégories est exacte).
- `pipeline/geo/io_util.py` : `write_json` (JSON déterministe, clés triées,
  retourne le SHA256 écrit), `read_json`, `sha256_file`.
- `pipeline/geo/steps/05_rivers.py` et `pipeline/geo/pipeline.py` : patrons
  de module d'étape et de crochet, décrits à l'identique dans le brief 025.
- `pipeline/geo/pipeline.py` porte déjà un drapeau global
  `--no-corrections`, employé par les branches `natural_earth_1400` et
  `adjacency`. **Ce lot le réemploie ; il n'en crée pas un nouveau.**

---

## Décisions de conception tranchées par le Planificateur

### D1 — Entrées exactes

| entrée | usage |
|---|---|
| `pipeline/geo/data/resources_1400.json` | le fichier de déclarations **créé par ce lot** (D3) ; relu par le module d'étape, jamais codé en dur dans le module |
| `pipeline/geo/artifacts/cells_g3.json` | `cells[]` : `cell_id`, `geometry` — pour le rattachement par contenance |
| `pipeline/geo/constants.py` | `PILOT_WINDOW_LONLAT`, `TARGET_CRS`, `FLOAT_DECIMALS`, `WORLD_TERMS_FORBIDDEN_KEYS`, et le bloc `R1_*` ajouté par ce lot (D5) |
| `pipeline/geo/projection.py` | la projection lon/lat → EPSG:3035, **réemployée telle quelle**, jamais réimplémentée |

Aucun accès réseau, aucune archive, aucune tuile. Ce lot ne télécharge rien.

### D2 — La liste est une amorce déclarée, pas une vérité, et c'est écrit dans l'artefact

**Ce point est une décision de conception, pas une réserve de style.** Le
**contenu** de la liste a été arbitré le 2026-08-21 (`A1`/`A2`, voir
« Arbitrage rendu ») : il est retenu **comme amorce provisoire**. Ce que D2
tranche, c'est la manière dont ce contenu, quel qu'il soit, est traité par le
code.

La liste de gisements de D4 est de la **connaissance historique générale,
non sourcée par citation primaire** — exactement la provenance que
`constants.py` déclare déjà pour les propositions de peuplement
(`P1_PROVENANCE`, `P2_PROVENANCE`). Trois conséquences tranchées ici :

1. **La véracité historique de la liste n'est pas une condition de succès de
   ce lot.** Ce que ce lot doit prouver, c'est que le **mécanisme** est
   honnête : provenance obligatoire, certitude déclarée, contenance seule,
   absence déclarable, réversibilité, aucun barème, aucune quantité,
   déterminisme. Un Évaluateur ne rejette pas ce lot parce qu'un gisement lui
   paraît mal daté ; il le rejette si un gisement est rattaché sans être
   contenu, ou omis sans être compté.
2. **Le propriétaire peut remplacer la liste sans toucher au code.** C'est
   pourquoi elle vit dans `data/resources_1400.json` et non dans le module
   d'étape. Un contrôle le vérifie : `R1-A` échoue si le module contient une
   liste de gisements codée en dur.
3. **Chaque entrée porte son propre degré de certitude**, pas un niveau
   uniforme appliqué à l'ensemble du fichier. Deux entrées de D4 sont marquées
   `reconstructed` plutôt que `reconstructed_established` parce que leur
   activité précisément en 1400 est moins assurée : la discrimination est
   dans la donnée, pas dans un commentaire. Le propriétaire a fait de cette
   honnêteté-là sa condition explicite en répondant `A1`.
4. **La classe de richesse a exactement le même statut que le reste de la
   ligne** : de la donnée déclarée, provisoire, remplaçable sans toucher au
   code. Sa véracité historique n'est **pas** une condition de succès du lot,
   au même titre qu'une date. Ce qui est une condition de succès, c'est que la
   classe soit obligatoire, prise dans un vocabulaire fermé, jamais numérisée,
   jamais portée par une cellule, et dénombrée de façon prouvable (`R1-G`,
   SC6).

**Ce que l'arbitrage `A1` a répondu :** la provenance générale est retenue,
sans citation primaire et sans source minière dans `sources.lock`. La liste de
D4 est retenue telle quelle, augmentée de la seule colonne `richness_class`.
Le mécanisme établi par ce lot n'a pas bougé — c'était le pari de D2, et il
tient.

### D3 — Le fichier de déclarations : forme exacte

`pipeline/geo/data/resources_1400.json`, sur le patron de
`data/corrections_1400.json` :

- `version` (entier), `comment` (une phrase disant ce que le fichier est et
  ce qu'il n'est pas — notamment qu'il est une amorce provisoire et non
  exhaustive, `A2`), `enabled_by_default` (`true`), `valid_certainty_levels`
  (les quatre niveaux, recopiés du vocabulaire existant),
  `valid_resource_kinds` (les natures de `R1_VALID_RESOURCE_KINDS`),
  `valid_richness_classes` (les trois classes de `R1_VALID_RICHNESS_CLASSES`),
  puis `deposits` : la liste.
- Chaque entrée porte, **tous obligatoires et non vides** : `id` (minuscules,
  sans accent, sans espace), `name` (le nom lisible, accentué), `resource`
  (une valeur de `valid_resource_kinds`), `richness_class` (une valeur de
  `valid_richness_classes`), `lon`, `lat` (degrés décimaux WGS84),
  `historical_reason` (une phrase en français clair disant ce qu'on y
  extrayait et pourquoi cela comptait), `date` (l'ancrage temporel employé,
  par exemple `"1400"` ou un siècle), `source` (la nature de la référence),
  `certainty` (un niveau du vocabulaire), `coords_certainty` (`"derived"`),
  `provenance` (la phrase de provenance commune, D2).

**Le schéma d'une entrée est fermé, et c'est le garde-fou principal contre une
quantité.** L'ensemble des clés d'une entrée de `deposits` égale **exactement**
`R1_REQUIRED_DEPOSIT_FIELDS` (D5) : ni champ manquant, ni champ en plus. De
même, l'ensemble des clés d'un gisement publié dans
`artifacts/resources_1400_r1.json` égale exactement
`R1_PUBLISHED_DEPOSIT_FIELDS`. Une liste de clés interdites ne peut, par
construction, que courir après les noms qu'on lui donne (règle n° 6) ; une
liste de clés **autorisées** ferme la porte à `tonnage`, à `richness_index` et
à tout nom qu'on n'a pas encore imaginé. Les deux existent ici, et aucune ne
remplace l'autre : le schéma fermé garde les gisements, la liste interdite de
`R1-E` garde les artefacts d'agrégat.

Aucune entrée ne porte de quantité, de réserve, d'intensité, de rendement ni
de multiplicateur — l'absence est vérifiée par le schéma fermé (`R1-A`) et par
`R1-E`, pas seulement écrite.

### D4 — La liste d'amorce **retenue** : vingt-sept gisements, tous vérifiés contenus dans une cellule terrestre

> **Cette liste est celle que l'amendement retient (`A2`).** Elle est
> l'instruction : `amendment-001-arbitrage-gisements.md` la conserve sans en
> retirer ni en ajouter aucune entrée, et l'amende de la seule colonne
> `richness_class`. Elle reste une **amorce provisoire et non exhaustive**,
> remplaçable sans toucher au code. Ce qui est vérifié ci-dessous, c'est
> uniquement sa **faisabilité géométrique** — que chaque position tombe bien
> dans une cellule terrestre committée. La vérité historique de la liste et de
> son classement n'est ni prouvée ni prouvable par ce brief, et aucune
> condition de succès ne s'y adosse (D2).

Le Planificateur a projeté chacune de ces positions en EPSG:3035 avec la
formule de projection azimutale équivalente de Lambert du référentiel cible,
validée au préalable contre les `596` centroïdes committés (erreur maximale
mesurée `0.0661` m), puis a testé son appartenance aux polygones de
`cells_g3.json` par lancer de rayon. **Les vingt-sept tombent à l'intérieur
d'un polygone de cellule terrestre ; aucun n'est hors fenêtre ; aucun n'est
en mer.** Ils occupent `25` cellules distinctes — deux couples partagent leur
cellule, ce qui est un fait normal et non une erreur (D6, `R1-B`).

**Ce brief ne porte aucun `cell_id`, volontairement.** Le rattachement est
recalculé par le Générateur sur la géométrie committée ; un rattachement
recopié depuis un brief ne dérive de rien (règle n° 3). Les nombres
ci-dessus (`27`, `25`, `0`) sont des constats de contexte qui disent que le
lot est réalisable, non des cibles à atteindre.

| `id` | `name` | `resource` | `richness_class` | `lon` | `lat` | `certainty` |
|---|---|---|---|---|---|---|
| `salins_les_bains` | Salins-les-Bains | `sel` | `notable` | `5.879` | `46.943` | `reconstructed_established` |
| `luneburg` | Lüneburg | `sel` | `majeure` | `10.414` | `53.249` | `reconstructed_established` |
| `wieliczka` | Wieliczka | `sel` | `majeure` | `20.055` | `49.983` | `reconstructed_established` |
| `guerande` | Guérande | `sel` | `majeure` | `-2.428` | `47.328` | `reconstructed_established` |
| `halle_saale` | Halle (Saale) | `sel` | `notable` | `11.969` | `51.482` | `reconstructed_established` |
| `cardona` | Cardona | `sel` | `notable` | `1.680` | `41.914` | `reconstructed_established` |
| `norberg` | Norberg (Bergslagen) | `fer` | `majeure` | `15.921` | `60.066` | `reconstructed_established` |
| `eisenerz` | Eisenerz (Erzberg) | `fer` | `notable` | `14.885` | `47.541` | `reconstructed_established` |
| `somorrostro` | Somorrostro (Biscaye) | `fer` | `majeure` | `-3.100` | `43.300` | `reconstructed_established` |
| `forest_of_dean` | Forest of Dean | `fer` | `notable` | `-2.550` | `51.800` | `reconstructed_established` |
| `val_trompia` | Val Trompia | `fer` | `mineure` | `10.250` | `45.750` | `reconstructed` |
| `falun` | Falun | `cuivre` | `majeure` | `15.626` | `60.606` | `reconstructed_established` |
| `banska_stiavnica` | Banská Štiavnica | `argent` | `majeure` | `18.892` | `48.457` | `reconstructed_established` |
| `rammelsberg` | Rammelsberg (Goslar) | `argent` | `majeure` | `10.428` | `51.894` | `reconstructed_established` |
| `kutna_hora` | Kutná Hora | `argent` | `majeure` | `15.268` | `49.948` | `reconstructed_established` |
| `freiberg` | Freiberg | `argent` | `notable` | `13.342` | `50.918` | `reconstructed_established` |
| `schwaz` | Schwaz | `argent` | `mineure` | `11.709` | `47.348` | `reconstructed` |
| `iglesias` | Iglesias (Sardaigne) | `argent` | `notable` | `8.537` | `39.309` | `reconstructed_established` |
| `camborne_redruth` | Camborne-Redruth (Cornouailles) | `etain` | `majeure` | `-5.300` | `50.230` | `reconstructed_established` |
| `dartmoor` | Dartmoor (Devon) | `etain` | `notable` | `-3.900` | `50.570` | `reconstructed_established` |
| `mendip` | Mendip Hills | `plomb` | `notable` | `-2.700` | `51.280` | `reconstructed_established` |
| `derbyshire_peak` | Peak District (Derbyshire) | `plomb` | `notable` | `-1.700` | `53.180` | `reconstructed_established` |
| `newcastle_tyne` | Newcastle upon Tyne | `charbon` | `notable` | `-1.610` | `54.978` | `reconstructed_established` |
| `liege` | Liège | `charbon` | `mineure` | `5.570` | `50.640` | `reconstructed_established` |
| `almaden` | Almadén | `mercure` | `majeure` | `-4.833` | `38.775` | `reconstructed_established` |
| `phocee` | Phocée (Foça) | `alun` | `majeure` | `26.755` | `38.669` | `reconstructed_established` |
| `kremnica` | Kremnica | `or` | `majeure` | `18.913` | `48.705` | `reconstructed_established` |

**La colonne `richness_class` se recopie telle quelle, sans arbitrage du
Générateur.** Elle est de la donnée déclarée au même titre que `certainty` :
le Générateur ne la recalcule pas, ne la déduit pas de la nature de la
ressource, ne l'ajuste pas parce qu'un site lui paraît sous-classé. Le critère
qui l'a attribuée est écrit une fois pour toutes dans « Vocabulaire ». La
distribution des trois classes sur cette liste est un **constat**, pas une
cible : aucun contrôle ne s'y compare (règle n° 2), et `stats_r1.json` la
mesure.

Le champ `historical_reason` de chaque entrée est rédigé par le Générateur en
français clair, en disant ce qu'on y extrayait et pourquoi cela comptait dans
l'Europe de 1400 — sans chiffre de production, sans valeur marchande, sans
comparaison de rendement. Il **doit rester cohérent avec la classe déclarée** :
une entrée classée `mineure` dont la raison affirmerait un commerce lointain
est une contradiction interne, et se signale dans
`deliverables/generator-log.md` plutôt que de se corriger en changeant la
classe. `date` porte l'ancrage temporel employé ; `source` porte la nature de
la référence ; `provenance` porte, pour toutes les entrées, la phrase déclarée
en D2. **Ces champs se rédigent, ils ne se laissent pas vides** : `R1-A` refuse
toute entrée incomplète.

### D5 — Les valeurs déclarées, et leur justification écrite

Bloc ajouté en fin de `constants.py`, introduit par
`# --- R1 — gisements extractifs déclarés de 1400 (v1_081) ---`. Chaque nom
est accompagné, dans le fichier, d'une phrase disant pourquoi cette valeur.

| nom | valeur | justification à écrire |
|---|---|---|
| `R1_PIPELINE_VERSION` | `"1.16.0-r1-v1_081"` | suit `1.15.0-c1-v1_080` posée par le lot 025 ; le tag `v1_081` ne collisionne avec aucun tag de journal déjà committé |
| `R1_REGISTRY_CREATED` | `"2026-08-20"` | date figée, jamais une horloge murale — même discipline que `G3_REGISTRY_CREATED` |
| `R1_DECLARATIONS_FILE` | `"data/resources_1400.json"` | le fichier de déclarations est nommé une seule fois, ici ; le module d'étape le lit, il ne le suppose pas |
| `R1_VALID_RESOURCE_KINDS` | `("sel", "fer", "cuivre", "argent", "etain", "plomb", "charbon", "mercure", "alun", "or")` | les dix natures extractives que la liste d'amorce emploie, en ASCII sans accent comme les noms de zones de mer déjà committés. Une nature hors liste est refusée, jamais rattrapée |
| `R1_VALID_CERTAINTY` | `("attested", "reconstructed", "reconstructed_established", "gameplay")` | recopie exacte du vocabulaire déjà employé par `data/corrections_1400.json` et par `G5B_VALID_CERTAINTY` — ce lot n'invente aucun niveau |
| `R1_VALID_RICHNESS_CLASSES` | `("mineure", "notable", "majeure")` | le vocabulaire fermé de la classe qualitative décidée par le propriétaire (`A3`). Trois valeurs, en français ASCII sans accent comme `R1_VALID_RESOURCE_KINDS`. Le critère qui les attribue est écrit dans « Vocabulaire ». Une quatrième classe ne serait pas discriminable honnêtement à partir de connaissance générale ; une valeur hors liste est refusée, jamais rattrapée |
| `R1_REQUIRED_DEPOSIT_FIELDS` | `("id", "name", "resource", "richness_class", "lon", "lat", "historical_reason", "date", "source", "certainty", "coords_certainty", "provenance")` | les champs sans lesquels une déclaration n'est pas une déclaration mais une supposition. Le jeu est **fermé** : une entrée de `deposits` porte exactement ces clés, ni plus ni moins (`R1-A`, D3) |
| `R1_PUBLISHED_DEPOSIT_FIELDS` | `R1_REQUIRED_DEPOSIT_FIELDS + ("cell_id", "attachment")` | les clés exactes d'un gisement publié dans `artifacts/resources_1400_r1.json` — dérivé de la constante précédente, jamais recopié à la main. Fermé pour la même raison (`R1-A`) |
| `R1_FORBIDDEN_QUANTITY_KEYS` | voir ci-dessous | une quantité dans la géographie fige une décision de simulation ; ces clés sont refusées en plus de `WORLD_TERMS_FORBIDDEN_KEYS` |
| `R1_PROVENANCE` | `"connaissance historique générale, non sourcée par citation primaire"` | la phrase exacte déjà employée par `P1_PROVENANCE` et `P2_PROVENANCE` — même honnêteté, même formulation, aucune variante nouvelle |
| `R1_COORDS_CERTAINTY` | `"derived"` | les positions sont dérivées, pas relevées sur une source primaire — même niveau que `P2_CERTAINTY_COORDS` |

`R1_FORBIDDEN_QUANTITY_KEYS` est un `frozenset` contenant au minimum :
`quantity`, `quantite`, `amount`, `volume`, `tonnage`, `reserve`, `reserves`,
`output`, `production`, `production_rate`, `extraction_rate`, `yield`,
`rendement`, `debit`, `rythme`, `cadence`, `capacity`, `capacite`, `grade`,
`teneur`, `intensity`, `intensite`, `stock`, `abondance`, `abundance`,
`coefficient`, `facteur`, `factor`, `ratio`, `taux`, `indice`, `index`,
`rang`, `rank`, `tier`, `poids`, `prix`, `price`, `valeur`.

**Trois précisions, parce qu'une liste de clés interdites mal cadrée protège
mal (règle n° 6).**

1. **Les mots `richness` et `richesse` n'y sont volontairement pas.** Le
   propriétaire a décidé qu'un gisement porte une classe de richesse ; bannir
   le mot même de la décision obligerait à la renommer, c'est-à-dire à la
   détourner en silence. Ce qui interdit une richesse **numérique**, ce n'est
   donc pas un mot banni, mais trois mécanismes qui ne dépendent d'aucun
   vocabulaire : le schéma fermé des gisements (D3), le contrôle `R1-G` (D6),
   et le maintien dans cette liste des mots qui nomment vraiment une grandeur
   de minerai — `grade`, `teneur`, `intensite`, `tonnage`, `reserve`,
   `rendement`.
2. **Les mots `level`, `levels` et `note` n'y sont pas non plus**, et ce n'est
   pas un oubli : `valid_certainty_levels` et `date_note` sont des clés
   légitimes du vocabulaire déjà committé, et les interdire ferait rougir le
   contrôle sur une donnée honnête. Un contrôle qui rougit sur du légitime
   finit par être désarmé, ce qui est pire que son absence.
3. **Les mots déjà portés par `WORLD_TERMS_FORBIDDEN_KEYS` (lot 025) ne sont
   pas redéclarés ici** — `bonus`, `malus`, `modifier`, `multiplier`,
   `multiplicateur`, `penalty`, `score`, `weight`, `relative_intensity`,
   `climate_mod` et les autres. `R1-E` balaie les **deux** ensembles ; les
   recopier ferait de ce lot un contrôle qui nomme sa propre référence
   (règle n° 2).

### D6 — Les sept contrôles `R1`, plus le déterminisme importé : leur sémantique exacte

Les sept contrôles `R1-*` vivent dans un module **neuf**,
`pipeline/geo/qa/checks_r1.py`, qui importe `CheckResult` et
`q10_determinism` de `qa/checks.py` sans le modifier (D8). `Q10` n'est pas
réécrit : il est **importé** et assemblé avec les sept autres, ce qui porte à
**huit** le nombre d'entrées du rapport de preuve.

| id | ce qu'il vérifie |
|---|---|
| `Q10` | déterminisme : chaque paire d'empreintes des deux passes est égale et non vide (fonction importée, non réécrite) |
| `R1-A` | **toute déclaration est complète, légale, fermée, et vient du fichier** : l'ensemble des clés de chaque entrée égale **exactement** `R1_REQUIRED_DEPOSIT_FIELDS` — un champ manquant comme un champ en trop est rouge — et toutes les valeurs sont non vides ; l'ensemble des clés de chaque gisement publié dans `resources_1400_r1.json` égale exactement `R1_PUBLISHED_DEPOSIT_FIELDS` ; `resource` appartient à `R1_VALID_RESOURCE_KINDS` ; `certainty` appartient à `R1_VALID_CERTAINTY` ; `coords_certainty` vaut `R1_COORDS_CERTAINTY` ; le compte d'entrées lues est strictement positif alors que le fichier existe. **Et** la donnée ne vit pas dans le code : le contrôle reçoit le texte source de `steps/r1_resources_1400.py` avec l'ensemble des `id` déclarés, et rouge dès qu'un `id` y apparaît comme chaîne littérale — c'est ce qui garantit que le propriétaire peut remplacer la liste sans toucher au module (D2, point 2). `R1-A` ne juge **pas** la valeur de `richness_class` : c'est le domaine entier de `R1-G`, et deux contrôles qui se recouvrent finissent par se faire confiance l'un l'autre |
| `R1-B` | **contenance seule** : tout gisement rattaché l'est à une cellule dont le polygone contient son point projeté. Le contrôle reçoit, pour chaque rattachement publié, le résultat d'un test de contenance recalculé, et échoue dès qu'un rattachement n'est pas contenu. Plusieurs gisements dans une même cellule sont légitimes ; **aucun** gisement ne peut être rattaché à deux cellules |
| `R1-C` | **aucune omission silencieuse** : `gisements_declares` égale exactement `gisements_rattaches + gisements_hors_fenetre + gisements_hors_terre`, et les identifiants des deux dernières catégories sont **nommés** dans `stats_r1.json`. Un gisement qui disparaît sans catégorie fait rougir le contrôle |
| `R1-D` | **réversibilité** : l'exécution déclarations coupées produit `0` gisement rattaché, `0` cellule dotée, et un `cells_resources_r1.json` portant **toutes** les cellules de `cells_g3.json` avec une liste `resources` vide ; l'empreinte de ce fichier **diffère** de celle produite déclarations actives. Les deux empreintes sont calculées et comparées à l'exécution, jamais recopiées ni citées par valeur (règle n° 12). Couper les déclarations retire les gisements, jamais des cellules : c'est la différence entre un monde sans mines et un monde sans terre |
| `R1-E` | **ni barème, ni quantité** : aucune clé de `WORLD_TERMS_FORBIDDEN_KEYS` (lue de `constants.py`, posée par le lot 025) ni de `R1_FORBIDDEN_QUANTITY_KEYS` n'apparaît, à quelque profondeur que ce soit, dans `resources_1400_r1.json`, `cells_resources_r1.json`, `stats_r1.json`, `MANIFEST_r1.json`, `registry/resource_registry.json` ni `data/resources_1400.json`. Sémantique de comparaison ci-dessous |
| `R1-F` | **la clé spatiale reste la cellule** : `cells_resources_r1.json` porte exactement les `cell_id` de `cells_g3.json`, même compte et mêmes identifiants ; et aucune des sous-chaînes `province`, `owner`, `country`, `pays` n'apparaît dans une clé d'aucun artefact publié par ce lot (ADR-0003 : la Province est une agrégation dérivée, jamais une clé stockée) |
| `R1-G` | **la classe de richesse est un nom, jamais un nombre** : cinq vérifications, détaillées ci-dessous |

`run_r1_green(...)` assemble ces huit contrôles dans cet ordre.

**Sémantique de comparaison de `R1-E`, écrite pour ne pas dépendre d'une
lecture.** Le parcours est récursif sur les **clés** des documents JSON, comme
`g5b_d_no_upstream_limit_encoded` ; les valeurs de prose ne sont pas balayées,
et c'est délibéré — une `historical_reason` qui écrit « on n'y mesurait aucun
rendement » n'est pas un rendement encodé. Chaque clé rencontrée est
normalisée (minuscules, accents retirés, `-` remplacé par `_`), puis comparée
de **deux** façons : le nom normalisé entier, et chacun de ses jetons obtenus
en découpant sur `_`. La clé est rouge si l'une ou l'autre appartient à l'un
des deux ensembles interdits. Les entrées des ensembles interdits, elles, ne
sont **jamais** découpées. Conséquences à connaître, toutes deux voulues :
`tonnage_estime` est rouge (jeton `tonnage`), tandis que la clé `outputs` du
manifeste ne l'est pas (`outputs` n'est pas `output`). C'est strictement plus
strict que l'égalité de clé employée par `g5b_d_no_upstream_limit_encoded`,
sans en hériter les faux positifs de la recherche par sous-chaîne.

**Les cinq vérifications de `R1-G`**, toutes lues du vocabulaire
`R1_VALID_RICHNESS_CLASSES` importé de `constants.py`, jamais recopié :

1. **type et vocabulaire** : dans `data/resources_1400.json` comme dans
   `resources_1400_r1.json`, chaque gisement porte `richness_class`, une
   **chaîne non vide** appartenant à `R1_VALID_RICHNESS_CLASSES`. Un nombre,
   une liste, un vide ou une valeur hors vocabulaire est rouge ;
2. **le vocabulaire n'est pas dans le code** : aucune valeur de
   `R1_VALID_RICHNESS_CLASSES` n'apparaît comme **chaîne littérale entre
   guillemets** dans `steps/r1_resources_1400.py` ni dans `qa/checks_r1.py` —
   les deux modules l'importent. Une table d'ordre ou de coefficient écrite à
   la main y serait donc visible, et rouge. La restriction aux chaînes
   littérales est délibérée : elle laisse passer un identifiant Python qui
   porterait par hasard le même nom, et ne rouge que sur une valeur écrite en
   dur ;
3. **aucune classe n'est adossée à un nombre** : dans les six fichiers balayés
   par `R1-E`, aucune clé égale à une valeur du vocabulaire ne porte une
   valeur numérique — **à la seule exception** du bloc `par_classe_de_richesse`
   de `stats_r1.json` ;
4. **`par_classe_de_richesse` est un dénombrement, pas un barème** : il porte
   les **trois** classes du vocabulaire, y compris celles à zéro, et la somme
   de ses valeurs égale exactement `gisements_declares`. C'est ce qui
   distingue mécaniquement un comptage d'une table de poids : un tableau de
   coefficients ne satisferait pas cette égalité ;
5. **la classe n'est pas une propriété de la case** : aucune valeur du
   vocabulaire n'apparaît dans `cells_resources_r1.json`, ni en clé ni en
   valeur, à quelque profondeur que ce soit. Une cellule liste des
   identifiants de gisements ; lui attacher un niveau ferait d'elle une case
   notée — la forme exacte de `terrain_endowment.json`.

### D7 — Sorties exactes

Sous `pipeline/geo/` :

| fichier | contenu |
|---|---|
| `data/resources_1400.json` | le fichier de déclarations (D3, D4) |
| `artifacts/resources_1400_r1.json` | par gisement, **exactement** les clés de `R1_PUBLISHED_DEPOSIT_FIELDS` (D5) : `id`, `name`, `resource`, `richness_class`, `lon`, `lat`, `historical_reason`, `date`, `source`, `certainty`, `coords_certainty`, `provenance`, plus `cell_id` (ou `null` si non rattaché) et `attachment` (`contained`, `outside_window`, `outside_land`) |
| `artifacts/cells_resources_r1.json` | **toutes les cellules lues de `cells_g3.json`**, chacune avec `cell_id` et `resources` : la liste, éventuellement **vide**, des `id` de gisements contenus. Une liste vide est une absence **mesurée**, jamais un trou (règle n° 10). **Aucune classe de richesse n'y figure** (`R1-G`, point 5) |
| `artifacts/stats_r1.json` | `gisements_declares`, `gisements_rattaches`, `gisements_hors_fenetre` (avec leurs `id`), `gisements_hors_terre` (avec leurs `id`), `cellules_dotees`, `cellules_totales`, `par_nature` (compte par valeur de `R1_VALID_RESOURCE_KINDS`, y compris les natures à zéro), `par_certitude`, `par_classe_de_richesse` (compte par valeur de `R1_VALID_RICHNESS_CLASSES`, y compris les classes à zéro ; sa somme égale `gisements_declares` — `R1-G`, point 4), `cellules_a_plusieurs_gisements`, `apply_declarations` |
| `artifacts/MANIFEST_r1.json` | `pipeline_version`, `crs`, `inputs` (empreintes calculées à l'exécution de `cells_g3.json` et `data/resources_1400.json`), `outputs` |
| `registry/resource_registry.json` | registre des gisements émis, date `R1_REGISTRY_CREATED`, `pipeline_version` — patron : `registry/river_registry.json` |
| `logs/v1_081_resources.log` | journal lisible de la preuve |
| `logs/v1_081_qa.json` | `checks` (`8` entrées, `passed` + `red_proof`) et `determinism.sha256` |
| `logs/v1_081_declarations_on.txt` | sortie de la dérivation déclarations **actives** |
| `logs/v1_081_declarations_off.txt` | sortie de la dérivation déclarations **coupées** — le couple prouve `R1-D`, comme `v1_050_g4b_links_on/off.txt` l'a fait pour les liens topologiques de G4 |
| `capture/v1_081_resources_window.png` | les gisements rattachés sur la fenêtre pilote, une couleur par nature, cellules dotées distinguées des cellules vides. La classe de richesse, **si** elle est montrée, l'est par une **forme de marqueur ou un libellé** — jamais par une taille de point, un rayon, une opacité ou une intensité de couleur : encoder une classe par une grandeur visuelle la rend numérique à l'œil, ce que `R1-G` interdit dans la donnée |
| `steps/r1_resources_1400.py` | le module neuf ; exporte `run_resources(apply_declarations=True)` |
| `qa/checks_r1.py` | les sept contrôles `R1-*` et `run_r1_green(...)` (D6) |
| `tests/run_proof_r1.py` | script de preuve (D9) |
| `tests/test_qa_red_r1.py` | cas rouges, un par contrôle (D10) |
| `README.md` | mise à jour (SC5) |

**Trois couples `must_differ_from`** dans `deliverables/manifest.json` :

1. `deliverables/pre-edit/pipeline-geo-README.md.orig` ↔ le `README.md`
   publié ;
2. `deliverables/pre-edit/constants.py.orig` ↔ `constants.py` publié ;
3. `deliverables/pre-edit/pipeline.py.orig` ↔ `pipeline.py` publié.

**Nommage.** `R1` n'entre en collision avec aucun des soixante-douze
identifiants de contrôle de `qa/checks.py`, ni avec les préfixes `G1`–`G12`
réservés par le plan de portage, `P1`/`P2` ou `A12`. Le nom de fichier
d'étape (`steps/r1_resources_1400.py`) suit le même raisonnement plutôt qu'un
rang numérique déjà réservé.

### D8 — Deux crochets ajoutés, aucun modifié dans son comportement

Mêmes règles qu'au lot 025, à la lettre :

**`constants.py`** : bloc **ajouté en fin de fichier**, zéro ligne
supprimée, zéro constante préexistante changée de valeur.

**`pipeline.py`** : reçoit `_load_resources_module()` (chemin explicite
`steps/r1_resources_1400.py`), `run_resources_r1(apply_declarations=True)`,
la valeur `"resources_1400"` ajoutée à `choices`, et une branche
`if args.source == "resources_1400":` qui **réemploie le drapeau global
`--no-corrections` déjà présent** pour couper les déclarations, et imprime
une ligne de résumé portant au minimum : la projection, `apply_declarations`,
`gisements_rattaches`, `cellules_dotees`, `par_nature`. **Deux lignes
existantes au plus** peuvent être modifiées (la fin de la chaîne d'aide de
`--source` et la `description` de l'analyseur). Les branches `--source`
préexistantes restent byte-identiques.

**La passe coupée ne publie jamais.** `run_resources(apply_declarations,
output_dir=None)` écrit dans `artifacts/` quand `output_dir` vaut `None`, et
dans le répertoire indiqué sinon. Déclarations **coupées**, l'appelant —
script de preuve comme ligne de commande — passe **toujours** un répertoire
temporaire, et la ligne de résumé imprime ce chemin en disant qu'elle ne
publie pas. Motif : `artifacts/` porte l'état de référence du dépôt ; une
exécution de démonstration qui le remplacerait par un monde sans mines
laisserait le dépôt dans un état faux dès que quelqu'un oublie de relancer la
passe active. Un ordre de commandes ne doit jamais être la seule chose qui
protège un artefact.

Après le lot 025, `pipeline.py` porte neuf valeurs de `--source` ; ce lot en
ajoute une dixième. Le compteur de SC4 se lit donc sur **neuf** branches
préexistantes, pas huit.

**`qa/checks.py` n'est pas modifié** (D6). Aucun artefact, registre, journal
ou capture d'un lot précédent n'est réécrit.

### D9 — Déterminisme : deux passes, plus la passe coupée

`tests/run_proof_r1.py` :

1. charge **une fois** les cellules et les déclarations ;
2. exécute la dérivation **et l'export complet** deux fois, déclarations
   actives ;
3. exécute une troisième fois, déclarations coupées, en passant un répertoire
   temporaire à `output_dir` (D8) — `artifacts/` n'est jamais touché par
   cette passe ;
4. compare les empreintes des deux passes actives (`Q10`) : chaque paire
   égale et non vide ;
5. compare l'empreinte de la passe coupée à celle des passes actives : elles
   doivent **différer** (`R1-D`) ;
6. joue les huit contrôles de `run_r1_green`, chacun avec son cas rouge ;
7. écrit `logs/v1_081_qa.json`, `logs/v1_081_resources.log`,
   `logs/v1_081_declarations_on.txt`, `logs/v1_081_declarations_off.txt` ;
8. rend le code `0` si et seulement si les huit contrôles sont verts, chacun
   avec une preuve rouge non vide, les deux passes actives identiques et la
   passe coupée différente.

Aucune horloge murale, aucun horodatage courant, aucune graine non fixée.

### D10 — Preuve rouge d'abord

`tests/test_qa_red_r1.py` fournit **un cas rouge par contrôle** : `Q10`,
`R1-A`, `R1-B`, `R1-C`, `R1-D`, `R1-E`, `R1-F`, `R1-G`. Chaque cas est une
mutation locale explicite sur une copie en mémoire — par exemple une entrée
privée de son champ `source` pour `R1-A` ; un gisement rattaché à une cellule
qui ne le contient pas pour `R1-B` ; un gisement retiré de toutes les
catégories pour `R1-C` ; une empreinte de passe coupée rendue égale à celle de
la passe active pour `R1-D` ; une clé `tonnage` injectée pour `R1-E` ; une clé
`province_id` ajoutée à une cellule pour `R1-F` ; une `richness_class`
remplacée par le nombre `3` pour `R1-G`. **Aucun cas ne passe par une
modification de `qa/checks.py` ni de `qa/checks_r1.py`.** Un `red_proof` vide
vaut échec du contrôle.

`R1-A` et `R1-G` portent chacun plusieurs vérifications ; **une seule mutation
suffit à prouver que le contrôle sait rougir**, et c'est ce que D10 exige. Les
autres portes de ces deux contrôles — le schéma fermé, le vocabulaire écrit en
dur dans le module, la classe adossée à un nombre, la somme de
`par_classe_de_richesse`, la classe posée sur une cellule — sont éprouvées par
les contre-preuves de l'Évaluateur, dans une copie hors dépôt (`eval-rubric.md`,
Conditions 1 et 6).

### D11 — Périmètre de fichiers

**Autorisé (création) :** `pipeline/geo/data/resources_1400.json` ;
`pipeline/geo/steps/r1_resources_1400.py` ; `pipeline/geo/qa/checks_r1.py` ;
`pipeline/geo/tests/run_proof_r1.py` ;
`pipeline/geo/tests/test_qa_red_r1.py` ; les artefacts, journaux, registre et
capture listés en D7 ;
`harness/queue/briefs/026-geo-gisements-1400-r1/deliverables/**`.

**Autorisé (modification bornée, D8) :** `pipeline/geo/constants.py` (ajout
en fin de fichier uniquement) ; `pipeline/geo/pipeline.py` (ajouts, plus deux
lignes existantes au plus) ; `pipeline/geo/README.md` (SC5) ;
`harness/queue/cost-ledger.jsonl` (une seule ligne ajoutée).

**Interdit :** `pipeline/geo/qa/checks.py` ; `pipeline/geo/qa/checks_c1.py` ;
`pipeline/geo/io_util.py` ; `pipeline/geo/projection.py` ;
`pipeline/geo/sources.lock` ; `pipeline/geo/data/corrections_1400.json` ;
`pipeline/geo/data/divergences_1400.json` ; tous les `steps/0*.py` existants
et `steps/c1_climate_drivers.py` ; tous les artefacts, registres, journaux et
captures des lots précédents ; tout fichier sous `sim/` ou `unity/` — et en
particulier `unity/game_unity/Assets/StreamingAssets/data/terrain_endowment.json`,
qui est lu comme **contre-exemple** et jamais copié, importé ni traduit ;
`harness/*.py` ; `harness/pipeline/` ; `architecture/` ; `docs/**` ;
`VISION.md` ; `ROADMAP.md` ; `hermes/**` ; `HANDOFF.md` ; `.github/**` ; les
archives des briefs 001 à 025, et **tout fichier du lot 024**.

---

## Success Conditions

### SC0 — L'arbitrage a eu lieu, et il est constaté avant la première action

**C'est la première chose que fait le Générateur, avant de lire un artefact,
avant d'écrire une ligne.**

- `amendement_arbitrage_present` vaut `1` : le fichier
  `harness/queue/briefs/026-geo-gisements-1400-r1/amendment-001-arbitrage-gisements.md`
  existe, et il est suivi par git.
- `decision_proprietaire_citee` vaut `1` : cet amendement cite le chemin d'une
  décision écrite du propriétaire sous `hermes/requests/` ou `docs/adr/`, et
  ce chemin existe réellement dans le dépôt. Une citation vers un fichier
  absent n'est pas une décision.
- `questions_arbitrage_repondues` vaut `3` sur `3` : `A1`, `A2` et `A3` sont
  chacune tranchées explicitement.
- `liste_appliquee_est_celle_de_l_amendement` vaut `1` : si l'amendement
  remplace la liste de D4, c'est **sa** liste qui est écrite dans
  `data/resources_1400.json` ; sinon c'est celle de D4 **telle qu'amendée**,
  colonne `richness_class` comprise, sans retouche. L'amendement du
  2026-08-21 retient D4 : c'est donc la table de D4, ligne par ligne et
  colonne par colonne, qui fait foi.

Si l'une de ces quatre valeurs n'est pas atteinte, **le lot s'arrête ici**.
Ce n'est pas un `REJECT` du travail — c'est un lot lancé avant son heure, et
le Générateur le signale sans produire aucun fichier. Il n'écrit **jamais**
l'amendement lui-même : le producteur ne s'accorde pas sa propre
autorisation.

### SC1 — Toute déclaration est complète, légale, fermée, et vit dans la donnée (`R1-A`)

Depuis `pipeline/geo/` :

```
../../.venv/bin/python tests/run_proof_r1.py
```

- `gisements_declares` est **lu** de `data/resources_1400.json`, jamais
  recopié d'une constante ni du brief.
- `declarations_incompletes` vaut `0` : chaque entrée porte tous les champs
  de `R1_REQUIRED_DEPOSIT_FIELDS`, tous non vides.
- `champs_de_gisement_hors_schema` vaut `0` : aucune entrée ne porte de clé
  hors de `R1_REQUIRED_DEPOSIT_FIELDS`, et aucun gisement publié ne porte de
  clé hors de `R1_PUBLISHED_DEPOSIT_FIELDS` (D3).
- `natures_hors_vocabulaire` vaut `0` et `certitudes_hors_vocabulaire` vaut
  `0`.
- `gisements_en_dur_dans_le_module` vaut `0` : aucun identifiant de gisement
  n'apparaît comme chaîne littérale dans `steps/r1_resources_1400.py`.
- `R1-A` vert.

### SC2 — Le rattachement est une contenance, et rien n'est perdu (`R1-B`, `R1-C`)

- `rattachements_non_contenus` vaut `0` : chaque rattachement publié est
  re-testé par contenance et confirmé.
- `gisements_a_deux_cellules` vaut `0`.
- `somme_categories_egale_declares` vaut `1` :
  `gisements_rattaches + gisements_hors_fenetre + gisements_hors_terre`
  égale exactement `gisements_declares`.
- Les identifiants des gisements hors fenêtre et hors terre sont **nommés**
  dans `stats_r1.json`, même si les listes sont vides.
- `cellules_dotees` et `cellules_a_plusieurs_gisements` sont mesurés et
  rapportés. Le Planificateur a mesuré `27` gisements dans `25` cellules
  distinctes sur la géométrie committée ; **ce sont des constats de contexte,
  pas des cibles** — un écart n'est pas un échec, il est à consigner dans
  `deliverables/generator-log.md`.
- `R1-B` et `R1-C` verts.

### SC3 — Couper la déclaration referme le monde (`R1-D`)

- `logs/v1_081_declarations_off.txt` montre `gisements_rattaches = 0` et
  `cellules_dotees = 0`.
- `empreinte_off_differe_de_on` vaut `1` : les deux empreintes de
  `cells_resources_r1.json` diffèrent, comparées à l'exécution.
- `cellules_totales_off` égale le nombre de cellules lues de
  `cells_g3.json` : couper les déclarations retire les gisements, **jamais**
  des cellules.
- `R1-D` vert.

### SC4 — Ni barème, ni quantité, ni clé spatiale concurrente (`R1-E`, `R1-F`)

- `cles_de_bareme_trouvees` vaut `0` et `cles_de_quantite_trouvees` vaut `0`
  sur les six fichiers balayés (D6).
- `cles_spatiales_concurrentes` vaut `0` : aucune clé contenant `province`,
  `owner`, `country` ou `pays` dans un artefact publié.
- `R1-E` et `R1-F` verts ; `cells_resources_r1.json` porte exactement les
  `cell_id` de `cells_g3.json`.
- `artefacts_precedents_modifies` vaut `0` : `git status --porcelain` est
  vide sur les artefacts G3/G4/G5 committés et sur les artefacts C1 du lot
  025.
- `constants_lignes_supprimees` vaut `0` ;
  `constantes_preexistantes_inchangees` est complet ;
  `pipeline_lignes_supprimees` vaut au plus `2` ;
  `branches_source_preexistantes_identiques` vaut `9` sur `9` (D8).

### SC5 — Déterminisme, crochet, preuves committées, README sans sur-revendication

Depuis `pipeline/geo/` :

```
../../.venv/bin/python pipeline.py --source resources_1400
```

- La commande sort en code `0` et affiche la ligne de résumé R1.
- `controles_r1_verts` vaut `8` sur `8` ;
  `controles_r1_avec_preuve_rouge_non_vide` vaut `8` sur `8` ;
  `paires_sha_determinisme_egales` est égal au nombre total de paires, lui
  même strictement positif ; `code_sortie_run_proof_r1` vaut `0`.
- `fichiers_preuve_suivis_par_git` égale le nombre de preuves déclarées en
  D7, vérifié par `git ls-files`. `artifacts/`, `logs/` et `capture/` étant
  exclus par `pipeline/geo/.gitignore`, chaque preuve est committée par ajout
  forcé individuel ; **aucun élargissement de `.gitignore` n'est autorisé**.
- `README.md` mis à jour : R1 livre les **gisements extractifs déclarés** —
  présence, nature, et classe qualitative de richesse — et rien d'autre. Le
  texte dit explicitement que ce lot ne livre ni quantité, ni rendement, ni
  ressource agricole ou forestière — ces dernières dépendant d'un climat et
  d'un sol non disponibles ; et que la classe de richesse est un nom pris dans
  un vocabulaire fermé de trois valeurs, jamais un nombre ni un barème. Restent non livrés : le
  climat proprement dit, les villes (G7), la possession (G8), les LOD (G9),
  les textures d'identifiants (G10), l'apparence (A12), G5-bis, G5-ter et la
  QA de chaîne complète (G11/G12). Le relief (G6) n'est **pas** décrit par ce
  lot, quel que soit l'état du lot 024.
- La capture de D7 est **regardée et décrite** dans
  `deliverables/generator-log.md` (règle n° 11) : les gisements doivent
  apparaître aux endroits attendus — étain en Cornouailles, sel en Pologne et
  en Franche-Comté, cuivre et fer en Suède centrale, mercure en Castille — et
  aucun point ne doit flotter en mer. Le journal dit aussi **si** la capture
  montre la classe de richesse et **par quel moyen** : forme ou libellé sont
  admis, une taille ou une intensité ne le sont pas (D7).
- La suite du harnais reste verte :

```
.venv/bin/python -m pytest harness/tests/ -q
```

  `tests_harness_passed_026` rapporté avec le nombre de tests collectés pour
  dénominateur ; sentinelle `-1` si le provisionnement échoue (Waivers),
  jamais `0`.

### SC6 — La classe de richesse est un nom, jamais un nombre (`R1-G`)

C'est la condition que l'arbitrage du propriétaire a ajoutée à ce lot (`A3`).
Elle se lit dans la même exécution que SC1 à SC5.

- `classes_hors_vocabulaire` vaut `0` : chaque gisement, déclaré comme publié,
  porte une `richness_class` qui est une **chaîne non vide** appartenant à
  `R1_VALID_RICHNESS_CLASSES`, **lu de `constants.py`**. Un nombre, une liste
  ou un vide compte ici comme hors vocabulaire.
- `classes_en_dur_dans_le_module` vaut `0` : aucune valeur du vocabulaire
  n'apparaît comme chaîne littérale dans `steps/r1_resources_1400.py` ni dans
  `qa/checks_r1.py`. C'est ce qui rend impossible une table d'ordre ou de
  coefficient écrite à la main.
- `classes_adossees_a_un_nombre` vaut `0` : dans les six fichiers balayés par
  `R1-E`, aucune clé égale à une valeur du vocabulaire ne porte une valeur
  numérique, hors le bloc `par_classe_de_richesse` de `stats_r1.json`.
- `somme_par_classe_egale_declares` vaut `1` : `par_classe_de_richesse` porte
  les **trois** classes, y compris celles à zéro, et la somme de ses valeurs
  égale exactement `gisements_declares`. C'est la preuve mécanique que ce bloc
  est un dénombrement et non un barème.
- `classes_dans_les_cellules` vaut `0` : aucune valeur du vocabulaire
  n'apparaît dans `cells_resources_r1.json`, ni en clé ni en valeur.
- `classes_distinctes_employees` est **mesuré et rapporté**, sur les trois
  classes du vocabulaire pour dénominateur. C'est un **fait constaté, pas une
  cible** : un vocabulaire dont une classe resterait inemployée ne serait pas
  un échec, et un contrôle qui l'exigerait nommerait sa propre référence
  (règle n° 2).
- `R1-G` vert, avec une preuve rouge non vide.

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. Produire une quantité, une réserve, un tonnage, un rendement, une
   intensité, une teneur ou une capacité — sous ce nom ou sous un autre
   (`R1-E`, plus le schéma fermé de D3). Un gisement dit ce qu'il y a et
   jusqu'où cela portait, jamais combien.
2. Produire un barème, un bonus, un malus ou un multiplicateur, ni recopier,
   importer ou traduire le contenu de `terrain_endowment.json` du jeu
   hérité, qui est lu comme contre-exemple et rien d'autre.
3. Livrer les ressources agricoles, forestières ou pastorales : elles
   dépendent d'un climat et d'un sol dont le dépôt ne dispose pas
   (Provenance).
4. Ajouter, modifier ou compléter `pipeline/geo/sources.lock`. Ce lot ne
   consomme aucune source externe.
5. Rattacher un gisement par plus-proche-voisin, avec ou sans borne. La
   contenance est la seule règle de rattachement (`R1-B`).
6. Faire disparaître un gisement sans catégorie : hors fenêtre et hors terre
   sont des catégories **nommées et comptées**, pas des silences (`R1-C`).
7. Introduire un `province_id`, un `owner_tag` ou toute autre clé spatiale
   concurrente de `cell_id` (ADR-0003, `R1-F`).
8. Modifier `pipeline/geo/qa/checks.py` ni `pipeline/geo/qa/checks_c1.py`,
   ni recopier leurs fonctions au lieu de les importer.
9. Modifier ou régénérer un artefact d'un lot précédent, y compris ceux du
   lot 025.
10. Lire, écrire, exécuter ou juger quoi que ce soit du lot 024.
11. Recopier une valeur hexadécimale d'empreinte dans un test, un document ou
    un commentaire (règle n° 12).
12. Reprendre l'un des nombres de contexte de ce brief (`27`, `25`, `596`,
    `0`, ou la distribution des trois classes de richesse sur la liste de D4)
    comme seuil de contrôle : ce sont des constats, et un contrôle qui s'y
    compare nomme sa propre référence (règle n° 2).
13. Employer l'alias nu de l'interpréteur ni un chemin Windows (règle n° 1).
14. Committer, pousser, créer ou changer de branche, ni fusionner
    (ADR-0014).
15. Écrire, compléter ou interpréter `amendment-001-arbitrage-gisements.md`.
    Le Générateur **constate** son existence (SC0) ; il ne la produit pas, et
    il ne déduit pas d'un silence que l'arbitrage vaut accord. Démarrer sans
    lui, même « pour préparer le terrain », est un dépassement de périmètre.
16. Modifier la liste de D4 de sa propre initiative — l'élargir, la réduire,
    corriger une coordonnée, ajouter un site « évident qui manque ». Toute
    évolution de la liste passe par un amendement (`A2`).
17. Attribuer, recalculer, déduire ou « corriger » une `richness_class`. Elle
    se recopie de D4 telle quelle, comme `certainty`. La déduire de la nature
    de la ressource, du nombre de gisements d'une cellule ou de la longueur
    d'une `historical_reason` en ferait une valeur calculée par le producteur,
    c'est-à-dire une donnée inventée (règle n° 10).
18. Ajouter une quatrième classe de richesse, en renommer une, ou en
    introduire une variante accentuée ou traduite. Le vocabulaire de
    `R1_VALID_RICHNESS_CLASSES` est fermé (D5).
19. Ordonner, indexer, numéroter ou pondérer les classes de richesse — table
    d'ordre, comparaison entre classes, conversion en entier, coefficient par
    classe, ou encodage par une taille ou une intensité sur la capture
    (`R1-G`, D7). La classe est un nom.
20. Attacher une classe de richesse à une cellule dans
    `cells_resources_r1.json`. Une cellule liste des identifiants de
    gisements ; une case notée est un barème de terrain (`R1-G`, point 5).
21. Rapporter un compteur depuis un calcul manqué sans le déclarer (règle
    n° 8 : sentinelle `-1`, jamais `0`) — un zéro réellement mesuré, comme
    `gisements_hors_terre = 0`, est légitime et s'en distingue.

---

## Required Counters (sous-ensemble ; le détail complet est dans les Success Conditions)

| nom | source | dénominateur |
|---|---|---|
| `amendement_arbitrage_present` | présence et suivi git de `amendment-001-arbitrage-gisements.md` | `1` vérification ; doit valoir `1` avant toute autre action |
| `decision_proprietaire_citee` | existence réelle du chemin cité par l'amendement sous `hermes/requests/` ou `docs/adr/` | `1` vérification ; doit valoir `1` |
| `questions_arbitrage_repondues` | réponses explicites à `A1`, `A2`, `A3` dans l'amendement | `3` ; doit valoir `3` |
| `liste_appliquee_est_celle_de_l_amendement` | comparaison entre la liste écrite dans `data/resources_1400.json` et celle que l'amendement retient — colonne `richness_class` comprise | `1` comparaison ; doit valoir `1` |
| `gisements_declares` | entrées de `data/resources_1400.json` | `1` lecture de fichier ; strictement positif |
| `declarations_incompletes` | entrées auxquelles manque un champ de `R1_REQUIRED_DEPOSIT_FIELDS`, ou dont un champ est vide | gisements déclarés ; doit valoir `0` |
| `champs_de_gisement_hors_schema` | clés d'entrée hors de `R1_REQUIRED_DEPOSIT_FIELDS`, plus clés de gisement publié hors de `R1_PUBLISHED_DEPOSIT_FIELDS` | gisements déclarés + gisements publiés ; doit valoir `0` |
| `natures_hors_vocabulaire` | entrées dont `resource` n'est pas dans `R1_VALID_RESOURCE_KINDS` | gisements déclarés ; doit valoir `0` |
| `certitudes_hors_vocabulaire` | entrées dont `certainty` n'est pas dans `R1_VALID_CERTAINTY` | gisements déclarés ; doit valoir `0` |
| `classes_hors_vocabulaire` | gisements, déclarés et publiés, dont `richness_class` n'est pas une chaîne non vide de `R1_VALID_RICHNESS_CLASSES` | gisements déclarés + gisements publiés ; doit valoir `0` |
| `gisements_en_dur_dans_le_module` | identifiants de gisement trouvés comme chaînes littérales dans `steps/r1_resources_1400.py` | gisements déclarés ; doit valoir `0` |
| `classes_en_dur_dans_le_module` | valeurs de `R1_VALID_RICHNESS_CLASSES` trouvées comme chaînes littérales dans `steps/r1_resources_1400.py` et `qa/checks_r1.py` | valeurs du vocabulaire ; doit valoir `0` |
| `gisements_rattaches` | entrées de `resources_1400_r1.json` avec `attachment == "contained"` | gisements déclarés |
| `rattachements_non_contenus` | rattachements dont le test de contenance recalculé échoue | gisements rattachés ; doit valoir `0` |
| `gisements_a_deux_cellules` | gisements apparaissant dans plus d'une cellule de `cells_resources_r1.json` | gisements rattachés ; doit valoir `0` |
| `gisements_hors_fenetre` | entrées hors `PILOT_WINDOW_LONLAT`, **nommées** | gisements déclarés ; fait mesuré |
| `gisements_hors_terre` | entrées dans la fenêtre mais dans aucun polygone de cellule, **nommées** | gisements déclarés ; fait mesuré |
| `somme_categories_egale_declares` | égalité de la somme des trois catégories avec le total déclaré | `1` comparaison ; doit valoir `1` |
| `cellules_dotees` | cellules de `cells_resources_r1.json` à liste `resources` non vide | cellules lues de `cells_g3.json` |
| `cellules_a_plusieurs_gisements` | cellules à plus d'un gisement | cellules dotées ; fait mesuré |
| `empreinte_off_differe_de_on` | comparaison des empreintes de `cells_resources_r1.json` entre les deux modes | `1` comparaison ; doit valoir `1` |
| `cellules_totales_off` | cellules du fichier produit déclarations coupées | cellules lues de `cells_g3.json` ; doit les égaler exactement — couper une déclaration ne retire jamais une cellule |
| `cles_de_bareme_trouvees` | clés de `WORLD_TERMS_FORBIDDEN_KEYS` rencontrées dans les six fichiers balayés | clés du `frozenset` ; doit valoir `0` |
| `cles_de_quantite_trouvees` | clés de `R1_FORBIDDEN_QUANTITY_KEYS` rencontrées dans les mêmes fichiers | clés du `frozenset` ; doit valoir `0` |
| `cles_spatiales_concurrentes` | clés contenant `province`, `owner`, `country` ou `pays` dans un artefact publié | clés balayées ; doit valoir `0` |
| `classes_adossees_a_un_nombre` | clés égales à une valeur de `R1_VALID_RICHNESS_CLASSES` portant une valeur numérique, hors `par_classe_de_richesse` de `stats_r1.json` | clés balayées ; doit valoir `0` |
| `somme_par_classe_egale_declares` | égalité de la somme de `par_classe_de_richesse` avec `gisements_declares`, les trois classes présentes | `1` comparaison ; doit valoir `1` |
| `classes_dans_les_cellules` | valeurs de `R1_VALID_RICHNESS_CLASSES` rencontrées dans `cells_resources_r1.json`, en clé ou en valeur | clés et valeurs balayées ; doit valoir `0` |
| `classes_distinctes_employees` | valeurs distinctes de `richness_class` réellement employées par la liste appliquée | `3` classes du vocabulaire ; **fait mesuré, sans cible** |
| `controles_r1_verts` | tableau `checks` de `logs/v1_081_qa.json` | `8` |
| `controles_r1_avec_preuve_rouge_non_vide` | champ `red_proof` de chaque entrée | `8` |
| `paires_sha_determinisme_egales` | bloc `determinism.sha256` de `logs/v1_081_qa.json` | total de paires ; strictement positif |
| `code_sortie_run_proof_r1` | code de sortie de `tests/run_proof_r1.py` | `1` exécution ; doit valoir `0` |
| `constants_lignes_supprimees` | lignes supprimées au diff contre l'instantané pré-édition | `1` mesure ; doit valoir `0` |
| `constantes_preexistantes_inchangees` | noms de premier niveau de l'instantané encore présents et de même valeur | nombre de noms de l'instantané ; doit être complet |
| `pipeline_lignes_supprimees` | lignes supprimées au diff contre l'instantané pré-édition | `1` mesure ; au plus `2` |
| `branches_source_preexistantes_identiques` | branches `if args.source == "..."` byte-identiques | `9` |
| `artefacts_precedents_modifies` | `git status --porcelain` sur les artefacts G3/G4/G5 et C1 committés | nombre d'artefacts vérifiés ; doit valoir `0` |
| `fichiers_preuve_suivis_par_git` | `git ls-files` croisé avec les preuves déclarées en D7 | nombre de preuves déclarées |
| `tests_harness_passed_026` | tests réussis de `harness/tests/` | tests collectés ; sentinelle `-1` si le provisionnement échoue — jamais `0` |

Un script committé sous
`harness/queue/briefs/026-geo-gisements-1400-r1/deliverables/measure_r1_026.py`,
exécuté depuis la racine, imprime chaque compteur avec son dénominateur,
dérivé des artefacts et des constantes — jamais une valeur recopiée à la
main.

---

## Acceptable Waivers (si une impossibilité est invoquée)

Une impossibilité s'éprouve avant d'être invoquée : une commande et le
message d'erreur qu'elle produit (règle n° 9).

| affirmation d'impossibilité | commande exigée | erreur attendue |
|---|---|---|
| « l'amendement d'arbitrage n'est pas suivi par git » | `git ls-files harness/queue/briefs/026-geo-gisements-1400-r1/amendment-001-arbitrage-gisements.md` depuis la racine | une sortie **vide**. **Ce n'est pas un waiver, c'est le blocage nominal de ce brief** (SC0) : le Générateur s'arrête, ne produit aucun fichier, et le signale. Aucune condition de succès n'est excusée, aucune n'est tentée. L'arbitrage a été rendu le 2026-08-21 et l'amendement écrit ; s'il n'est pas suivi par git, c'est qu'il n'a pas encore été committé par l'orchestrateur — le lot attend ce commit, pas une nouvelle décision |
| « `WORLD_TERMS_FORBIDDEN_KEYS` n'existe pas » | `.venv/bin/python -c "import sys; sys.path.insert(0,'pipeline/geo'); import constants; print(len(constants.WORLD_TERMS_FORBIDDEN_KEYS))"` depuis la racine | `AttributeError` nommant la constante. **C'est un motif de blocage, pas un waiver** : ce lot dépend du lot 025 fusionné (Provenance). Le Générateur s'arrête et escalade ; il ne recopie pas la liste, ce qui en ferait un contrôle qui nomme sa propre référence |
| « la pile scientifique n'est pas installée » | `.venv/bin/python -c "import shapely, geopandas, pyproj, matplotlib; print('ok')"` depuis la racine | `ModuleNotFoundError` nommant le module — **vérifié à l'écriture de ce brief** : le venv de la racine est un environnement Python `3.12.3` nu. `pipeline/geo/requirements.txt` déclare les paquets nécessaires ; la provision normale est `.venv/bin/pip install -r pipeline/geo/requirements.txt` et ne relève pas de D11. Le waiver ne s'applique que si cette installation échoue elle-même. **`rasterio` n'est pas requis par ce lot** |
| « le paquet de test du harnais n'est pas installé » | `.venv/bin/python -m pytest --version` depuis la racine | `No module named pytest` — **vérifié à l'écriture de ce brief**. Outillage de test, pas code produit : le Générateur peut l'installer. Si l'installation échoue, `tests_harness_passed_026` vaut `-1`, consigné dans `deliverables/generator-log.md` |
| « un gisement déclaré n'est contenu dans aucune cellule » | sortie de `tests/run_proof_r1.py` nommant le gisement, sa position et le résultat du test de contenance | la sortie réelle. **Ce n'est pas un blocage** : c'est exactement ce que la catégorie `outside_land` existe pour représenter (`R1-C`). Le gisement est compté, nommé, et **n'est pas rattaché**. Le Générateur ne déplace **jamais** une coordonnée pour la faire tomber sur la terre : ce serait fabriquer de la donnée pour satisfaire un contrôle. Le Planificateur a mesuré que les vingt-sept sont contenus ; un écart est un fait nouveau à consigner |
| « la géométrie ne permet pas un test de contenance fiable » | le code de rattachement écrit tel quel, plus la sortie de `../../.venv/bin/python pipeline.py --source resources_1400` | la sortie réelle montrant l'incohérence. **Si invoquée, SC2 n'est pas excusée** : c'est une escalade vers le propriétaire, jamais un repli sur un plus-proche-voisin |
| « `cells_g3.json` n'est pas lisible » | `.venv/bin/python -c "import json; json.load(open('pipeline/geo/artifacts/cells_g3.json'))"` depuis la racine | `FileNotFoundError` ou `JSONDecodeError` nommant le fichier |

---

## Execution Contract

### Interpréteur et commandes

Sur cette machine Linux, l'interpréteur est `.venv/bin/python` depuis la
racine, `../../.venv/bin/python` depuis `pipeline/geo/`. L'alias nu est
interdit (règle n° 1). Aucune commande de ce lot n'a besoin d'Unity : aucun
worker Windows n'est requis, et aucune condition de succès ne dépend d'une
preuve Unity.

### Deux préalables, dans cet ordre

1. **L'arbitrage du propriétaire** (SC0). Sans
   `amendment-001-arbitrage-gisements.md` **suivi par git**, ce brief ne se
   lance pas. *État au 2026-08-21 : l'amendement est écrit et répond aux trois
   questions ; il doit être committé par l'orchestrateur pour que `SC0` puisse
   le constater.*
2. **La fusion du lot 025**, qui pose `WORLD_TERMS_FORBIDDEN_KEYS`
   (Provenance). *État au 2026-08-21 : cette constante n'existe pas encore
   dans `pipeline/geo/constants.py`. Ce préalable n'est donc pas satisfait, et
   le lot ne s'exécute pas aujourd'hui.*

Les deux se vérifient par des commandes, pas par un souvenir de réunion, ni
par la présence de ces deux notes d'état : elles disent ce qui était vrai à
l'amendement, pas ce qui est vrai au lancement.

### Estimation d'appels d'outils

**Estimation du Planificateur : `120` appels d'outils**, une fois le brief
débloqué. Sous le seuil de `150` au-delà duquel un brief doit être découpé, et
sous l'arrêt du budget à `160`. Ancres employées : les lots 021 et 024, de
structure identique. Ce lot n'a ni téléchargement, ni lecture matricielle, ni
astronomie ; son coût propre est la rédaction d'une `historical_reason` par
gisement et la troisième passe de réversibilité. **Révisée de `110` à `120` à
l'amendement du 2026-08-21** : l'arbitrage `A3` ajoute un huitième contrôle
(`R1-G`, cinq vérifications), son cas rouge, un champ au schéma et une
condition de succès. L'arbitrage `A2` n'a pas élargi la liste — elle reste
celle de D4 —, donc l'ordre de grandeur tient. À vérifier avant génération :

```
.venv/bin/python harness/budget.py split-check \
  --brief harness/queue/briefs/026-geo-gisements-1400-r1 \
  --estimated-calls 120
```

### Preuves committées et re-vérifiables

`pipeline/geo/.gitignore` exclut `artifacts/`, `logs/` et `capture/`. Chaque
fichier de preuve déclaré en D7 est committé par ajout forcé individuel,
jamais par élargissement de `.gitignore`. Une preuve qu'un clone frais ne
retrouve pas n'est pas une preuve. `data/resources_1400.json` n'est **pas**
sous un répertoire exclu : il se commite normalement.

### Deliverables obligatoires

Sous `harness/queue/briefs/026-geo-gisements-1400-r1/deliverables/` :

- `manifest.json` — `files[]` (avec les trois couples `must_differ_from` de
  D7), `counters[]` (chaque compteur, avec sa valeur, sa `sample_size` réelle
  et la commande qui l'a produite), `waivers[]` (chacun avec sa commande et
  son erreur) ;
- `generator-log.md` — en français clair : ce qui a été fait, ce qui a
  résisté, la description **vue** de la capture (règle n° 11), et tout écart
  avec les constats de contexte du Planificateur ;
- `measure_r1_026.py` — le script de reconstruction des compteurs ;
- `pre-edit/pipeline-geo-README.md.orig`, `pre-edit/constants.py.orig`,
  `pre-edit/pipeline.py.orig`.

### Interdictions pour le Générateur

Il ne prononce jamais la recevabilité de son propre travail, ne rédige aucun
`verdict.md`, ne modifie ni `brief.md` ni `eval-rubric.md`, ne commite pas,
ne pousse pas, ne crée ni ne change de branche, et ne fusionne rien
(ADR-0014).

### Fin de lot

Le lot est terminé quand SC0 est constatée, que `tests/run_proof_r1.py` sort
en code `0`, que `pipeline.py --source resources_1400` sort en code `0` dans
ses deux modes, que les **sept** conditions de succès (SC0 à SC6) sont
couvertes par des compteurs reconstruits, et que les deliverables ci-dessus
sont committés par l'orchestrateur.

---

## Registre de coût

Une ligne, sans `--audit-id` :

```
.venv/bin/python harness/backends/ledger.py append --backend cursor \
  --brief harness/queue/briefs/026-geo-gisements-1400-r1 \
  --event generator-run
```
