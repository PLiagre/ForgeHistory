# Brief 026 : les gisements extractifs de 1400 (R1) — ce que la terre donne, déclaré et rattaché par contenance

**Authored**: 2026-08-20T21:20:00Z
**Author**: forge-planificateur
**Statut**: **BLOQUÉ — n'exécuter sous aucun prétexte avant l'arbitrage du propriétaire décrit ci-dessous**

> ## ⛔ Ce brief n'est pas exécutable en l'état
>
> **Le mécanisme décrit ici est prêt. Son contenu de données ne l'est pas :
> il contient une décision produit que le Planificateur n'a pas l'autorité de
> prendre.** Voir la section « Arbitrage requis avant toute exécution »
> ci-dessous. Tant que l'amendement qui y est décrit n'existe pas dans ce
> répertoire, ce brief est un **projet soumis à décision**, pas une
> instruction. Le Générateur qui le recevrait doit s'arrêter immédiatement et
> le signaler.

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

---

## Arbitrage requis avant toute exécution

Ce lot pose **quels gisements existent dans le monde de ForgeHistory en
1400**. Ce n'est pas une question d'ingénierie : c'est le premier maillon de
l'économie du jeu, et il décide où naîtront des villes minières, quelles
régions seront riches, et quelles routes compteront. Rien dans le dépôt ne
tranche cette question — ni `ROADMAP.md` (qui dit « ressources » sans dire
lesquelles ni d'où elles viennent), ni `VISION.md`, ni aucun ADR. Le
Planificateur **ne prend donc pas cette décision** et la remonte, comme la
demande `DEMANDE-20260820-claude-code-prochains-briefs.md` l'exige
explicitement (« signaler toute décision produit ou d'architecture qui ne peut
pas être déduite des décisions existantes, au lieu de la prendre
silencieusement »).

### Ce qui est déjà déductible du dépôt, et n'a pas besoin d'arbitrage

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

### Les trois questions qui, elles, appartiennent au propriétaire

| # | question | ce que ce brief propose, sans le décider |
|---|---|---|
| **A1** | La couche des ressources peut-elle reposer sur de la **connaissance historique générale, non sourcée par citation primaire** ? | Oui, au motif que `constants.py` déclare déjà exactement cette provenance pour `P1_PROVENANCE` et `P2_PROVENANCE`. Réserve honnête : ces deux blocs sont des constantes **héritées du portage VictoriaProject**, jamais livrées ni approuvées comme norme par ce propriétaire. S'en servir de précédent est un raisonnement, pas une autorisation |
| **A2** | La liste d'amorce de D4 — **vingt-sept gisements, dix natures de ressource** — est-elle celle que le monde doit contenir ? | La liste est proposée en D4. Elle est courte, volontairement limitée à des sites de forte notoriété, et chaque entrée porte son propre degré de certitude. Le propriétaire peut l'accepter, la réduire, l'élargir ou la remplacer intégralement : **le code ne change pas**, seul `data/resources_1400.json` change |
| **A3** | « Ressource » signifie-t-elle ici **présence d'un gisement travaillé**, sans aucune quantité ? | Oui : ce brief interdit mécaniquement toute quantité (`R1-E`). C'est un choix de conception fort — il rend la couche inutilisable telle quelle par un système économique qui attendrait un débit, et repousse cette question à `sim/` |

### Comment le déblocage se constate, mécaniquement

Ce brief devient exécutable quand, et seulement quand, un fichier
`amendment-001-arbitrage-gisements.md` existe dans ce même répertoire,
signé par le Planificateur, et qui :

1. cite la décision écrite du propriétaire par son chemin (une demande sous
   `hermes/requests/` ou un ADR sous `docs/adr/`) ;
2. répond aux trois questions `A1`, `A2`, `A3` par oui ou par une
   reformulation explicite ;
3. si la liste de D4 est modifiée, porte la liste retenue **en entier**, en
   remplacement de D4 — parce que l'unique instruction de l'exécutant reste
   ce répertoire de brief, et qu'une liste vivant ailleurs le romprait
   (`CLAUDE.md` › Single Source of Instruction).

Le Générateur vérifie l'existence de ce fichier **avant sa première action**
(SC0). Il ne l'écrit jamais lui-même : ce serait le producteur s'accordant sa
propre autorisation.

**Ce que le blocage ne remet pas en cause :** les décisions D1 et D3 à D11
sont stables et n'attendent rien. Si l'arbitrage change la liste, seuls D4 et
`R1_VALID_RESOURCE_KINDS` (D5) bougent ; aucune condition de succès n'est
formulée en fonction du contenu de la liste (D2), et c'est délibéré.

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
   et datée.

**Interdit** dans ce lot : aucun barème, aucune intensité relative, aucun
multiplicateur, et — spécifiquement ici — **aucune quantité**. Un gisement de
ce lot dit « il y a du fer ici, et on l'y travaillait autour de 1400 ». Il ne
dit jamais « combien », ni « à quel rythme », ni « pour combien de temps ».
La quantité extraite dépendra de qui creuse, avec quels outils, à quel
moment : c'est de la simulation, elle appartient à `sim/`, et l'encoder ici
figerait dans la géographie une décision qui n'y a pas sa place. Le contrôle
`R1-E` (D6) rend cette interdiction mécanique.

---

## Vocabulaire (expliqué une fois)

- **gisement (`deposit`)** : un lieu ponctuel où une ressource extractive
  était travaillée autour de 1400, déclaré par un identifiant, un nom, une
  nature de ressource, une position en longitude/latitude, une raison
  historique, une date, une source et un degré de certitude.
- **nature de ressource (`resource`)** : l'une des valeurs de
  `R1_VALID_RESOURCE_KINDS` (D5). Une valeur hors de cette liste est un refus,
  jamais un rattrapage silencieux.
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

**Ce point est une décision de conception, pas une réserve de style.** Il ne
lève pas le blocage : le **contenu** de la liste reste soumis à l'arbitrage
`A1`/`A2` (voir « Arbitrage requis avant toute exécution »). Ce que D2 tranche,
c'est la manière dont ce contenu, quel qu'il soit, est traité par le code.

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
   dans la donnée, pas dans un commentaire.

**Décision réservée au propriétaire, signalée et non prise ici :** si le
projet exige des citations primaires pour la couche des ressources, ou une
source minière déclarée dans `sources.lock`, c'est la réponse à `A1` qui le
dira. Deux issues sont possibles et ce brief les accepte toutes les deux —
soit l'amendement retient la provenance générale et la liste de D4 telle
quelle, soit il impose une exigence plus haute et **remplace** la liste. Dans
les deux cas le mécanisme établi par ce lot ne bouge pas.

### D3 — Le fichier de déclarations : forme exacte

`pipeline/geo/data/resources_1400.json`, sur le patron de
`data/corrections_1400.json` :

- `version` (entier), `comment` (une phrase disant ce que le fichier est et
  ce qu'il n'est pas), `enabled_by_default` (`true`),
  `valid_certainty_levels` (les quatre niveaux, recopiés du vocabulaire
  existant), `valid_resource_kinds` (les natures de `R1_VALID_RESOURCE_KINDS`),
  puis `deposits` : la liste.
- Chaque entrée porte, **tous obligatoires et non vides** : `id` (minuscules,
  sans accent, sans espace), `name` (le nom lisible, accentué), `resource`
  (une valeur de `valid_resource_kinds`), `lon`, `lat` (degrés décimaux
  WGS84), `historical_reason` (une phrase en français clair disant ce qu'on y
  extrayait et pourquoi cela comptait), `date` (l'ancrage temporel employé,
  par exemple `"1400"` ou un siècle), `source` (la nature de la référence),
  `certainty` (un niveau du vocabulaire), `coords_certainty` (`"derived"`),
  `provenance` (la phrase de provenance commune, D2).

Aucune entrée ne porte de quantité, de réserve, d'intensité, de rendement ni
de multiplicateur — l'absence est vérifiée par `R1-E`, pas seulement écrite.

### D4 — La liste d'amorce **proposée** : vingt-sept gisements, tous vérifiés contenus dans une cellule terrestre

> **Cette liste est une proposition soumise à l'arbitrage `A2`, pas une
> instruction.** Elle n'a d'autorité qu'une fois reprise ou remplacée par
> `amendment-001-arbitrage-gisements.md`. Ce qui est vérifié ci-dessous, c'est
> uniquement sa **faisabilité géométrique** — que chaque position tombe bien
> dans une cellule terrestre committée. La vérité historique de la liste
> n'est ni prouvée ni prouvable par ce brief, et aucune condition de succès
> ne s'y adosse (D2).

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

| `id` | `name` | `resource` | `lon` | `lat` | `certainty` |
|---|---|---|---|---|---|
| `salins_les_bains` | Salins-les-Bains | `sel` | `5.879` | `46.943` | `reconstructed_established` |
| `luneburg` | Lüneburg | `sel` | `10.414` | `53.249` | `reconstructed_established` |
| `wieliczka` | Wieliczka | `sel` | `20.055` | `49.983` | `reconstructed_established` |
| `guerande` | Guérande | `sel` | `-2.428` | `47.328` | `reconstructed_established` |
| `halle_saale` | Halle (Saale) | `sel` | `11.969` | `51.482` | `reconstructed_established` |
| `cardona` | Cardona | `sel` | `1.680` | `41.914` | `reconstructed_established` |
| `norberg` | Norberg (Bergslagen) | `fer` | `15.921` | `60.066` | `reconstructed_established` |
| `eisenerz` | Eisenerz (Erzberg) | `fer` | `14.885` | `47.541` | `reconstructed_established` |
| `somorrostro` | Somorrostro (Biscaye) | `fer` | `-3.100` | `43.300` | `reconstructed_established` |
| `forest_of_dean` | Forest of Dean | `fer` | `-2.550` | `51.800` | `reconstructed_established` |
| `val_trompia` | Val Trompia | `fer` | `10.250` | `45.750` | `reconstructed` |
| `falun` | Falun | `cuivre` | `15.626` | `60.606` | `reconstructed_established` |
| `banska_stiavnica` | Banská Štiavnica | `argent` | `18.892` | `48.457` | `reconstructed_established` |
| `rammelsberg` | Rammelsberg (Goslar) | `argent` | `10.428` | `51.894` | `reconstructed_established` |
| `kutna_hora` | Kutná Hora | `argent` | `15.268` | `49.948` | `reconstructed_established` |
| `freiberg` | Freiberg | `argent` | `13.342` | `50.918` | `reconstructed_established` |
| `schwaz` | Schwaz | `argent` | `11.709` | `47.348` | `reconstructed` |
| `iglesias` | Iglesias (Sardaigne) | `argent` | `8.537` | `39.309` | `reconstructed_established` |
| `camborne_redruth` | Camborne-Redruth (Cornouailles) | `etain` | `-5.300` | `50.230` | `reconstructed_established` |
| `dartmoor` | Dartmoor (Devon) | `etain` | `-3.900` | `50.570` | `reconstructed_established` |
| `mendip` | Mendip Hills | `plomb` | `-2.700` | `51.280` | `reconstructed_established` |
| `derbyshire_peak` | Peak District (Derbyshire) | `plomb` | `-1.700` | `53.180` | `reconstructed_established` |
| `newcastle_tyne` | Newcastle upon Tyne | `charbon` | `-1.610` | `54.978` | `reconstructed_established` |
| `liege` | Liège | `charbon` | `5.570` | `50.640` | `reconstructed_established` |
| `almaden` | Almadén | `mercure` | `-4.833` | `38.775` | `reconstructed_established` |
| `phocee` | Phocée (Foça) | `alun` | `26.755` | `38.669` | `reconstructed_established` |
| `kremnica` | Kremnica | `or` | `18.913` | `48.705` | `reconstructed_established` |

Le champ `historical_reason` de chaque entrée est rédigé par le Générateur en
français clair, en disant ce qu'on y extrayait et pourquoi cela comptait dans
l'Europe de 1400 — sans chiffre de production, sans valeur marchande, sans
comparaison de rendement. `date` porte l'ancrage temporel employé ; `source`
porte la nature de la référence ; `provenance` porte, pour toutes les
entrées, la phrase déclarée en D2. **Ces champs se rédigent, ils ne se
laissent pas vides** : `R1-A` refuse toute entrée incomplète.

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
| `R1_REQUIRED_DEPOSIT_FIELDS` | `("id", "name", "resource", "lon", "lat", "historical_reason", "date", "source", "certainty", "coords_certainty", "provenance")` | les champs sans lesquels une déclaration n'est pas une déclaration mais une supposition (`R1-A`) |
| `R1_FORBIDDEN_QUANTITY_KEYS` | voir ci-dessous | une quantité dans la géographie fige une décision de simulation ; ces clés sont refusées en plus de `WORLD_TERMS_FORBIDDEN_KEYS` |
| `R1_PROVENANCE` | `"connaissance historique générale, non sourcée par citation primaire"` | la phrase exacte déjà employée par `P1_PROVENANCE` et `P2_PROVENANCE` — même honnêteté, même formulation, aucune variante nouvelle |
| `R1_COORDS_CERTAINTY` | `"derived"` | les positions sont dérivées, pas relevées sur une source primaire — même niveau que `P2_CERTAINTY_COORDS` |

`R1_FORBIDDEN_QUANTITY_KEYS` est un `frozenset` contenant au minimum :
`quantity`, `quantite`, `amount`, `volume`, `tonnage`, `reserve`, `reserves`,
`output`, `production`, `production_rate`, `yield`, `rendement`, `capacity`,
`capacite`, `richness`, `richesse`, `grade`, `teneur`, `intensity`,
`intensite`, `stock`.

### D6 — Les six contrôles `R1`, plus le déterminisme importé : leur sémantique exacte

Les six contrôles `R1-*` vivent dans un module **neuf**,
`pipeline/geo/qa/checks_r1.py`, qui importe `CheckResult` et
`q10_determinism` de `qa/checks.py` sans le modifier (D8). `Q10` n'est pas
réécrit : il est **importé** et assemblé avec les six autres, ce qui porte à
**sept** le nombre d'entrées du rapport de preuve.

| id | ce qu'il vérifie |
|---|---|
| `Q10` | déterminisme : chaque paire d'empreintes des deux passes est égale et non vide (fonction importée, non réécrite) |
| `R1-A` | **toute déclaration est complète, légale, et vient du fichier** : chaque entrée porte les champs de `R1_REQUIRED_DEPOSIT_FIELDS`, tous non vides ; `resource` appartient à `R1_VALID_RESOURCE_KINDS` ; `certainty` appartient à `R1_VALID_CERTAINTY` ; `coords_certainty` vaut `R1_COORDS_CERTAINTY` ; le compte d'entrées lues est strictement positif alors que le fichier existe. **Et** la donnée ne vit pas dans le code : le contrôle reçoit le texte source de `steps/r1_resources_1400.py` avec l'ensemble des `id` déclarés, et rouge dès qu'un `id` y apparaît comme littéral — c'est ce qui garantit que le propriétaire peut remplacer la liste sans toucher au module (D2, point 2) |
| `R1-B` | **contenance seule** : tout gisement rattaché l'est à une cellule dont le polygone contient son point projeté. Le contrôle reçoit, pour chaque rattachement publié, le résultat d'un test de contenance recalculé, et échoue dès qu'un rattachement n'est pas contenu. Plusieurs gisements dans une même cellule sont légitimes ; **aucun** gisement ne peut être rattaché à deux cellules |
| `R1-C` | **aucune omission silencieuse** : `gisements_declares` égale exactement `gisements_rattaches + gisements_hors_fenetre + gisements_hors_terre`, et les identifiants des deux dernières catégories sont **nommés** dans `stats_r1.json`. Un gisement qui disparaît sans catégorie fait rougir le contrôle |
| `R1-D` | **réversibilité** : l'exécution déclarations coupées produit `0` gisement rattaché, `0` cellule dotée, et un `cells_resources_r1.json` portant **toutes** les cellules de `cells_g3.json` avec une liste `resources` vide ; l'empreinte de ce fichier **diffère** de celle produite déclarations actives. Les deux empreintes sont calculées et comparées à l'exécution, jamais recopiées ni citées par valeur (règle n° 12). Couper les déclarations retire les gisements, jamais des cellules : c'est la différence entre un monde sans mines et un monde sans terre |
| `R1-E` | **ni barème, ni quantité** : aucune clé de `WORLD_TERMS_FORBIDDEN_KEYS` (lue de `constants.py`, posée par le lot 025) ni de `R1_FORBIDDEN_QUANTITY_KEYS` n'apparaît, à quelque profondeur que ce soit, dans `resources_1400_r1.json`, `cells_resources_r1.json`, `stats_r1.json`, `MANIFEST_r1.json`, `registry/resource_registry.json` ni `data/resources_1400.json` |
| `R1-F` | **la clé spatiale reste la cellule** : `cells_resources_r1.json` porte exactement les `cell_id` de `cells_g3.json`, même compte et mêmes identifiants ; et aucune des sous-chaînes `province`, `owner`, `country`, `pays` n'apparaît dans une clé d'aucun artefact publié par ce lot (ADR-0003 : la Province est une agrégation dérivée, jamais une clé stockée) |

`run_r1_green(...)` assemble ces sept contrôles dans cet ordre.

### D7 — Sorties exactes

Sous `pipeline/geo/` :

| fichier | contenu |
|---|---|
| `data/resources_1400.json` | le fichier de déclarations (D3, D4) |
| `artifacts/resources_1400_r1.json` | par gisement : `id`, `name`, `resource`, `lon`, `lat`, `cell_id` (ou `null` si non rattaché), `attachment` (`contained`, `outside_window`, `outside_land`), `certainty`, `coords_certainty`, `date`, `source`, `provenance`, `historical_reason` |
| `artifacts/cells_resources_r1.json` | **toutes les cellules lues de `cells_g3.json`**, chacune avec `cell_id` et `resources` : la liste, éventuellement **vide**, des `id` de gisements contenus. Une liste vide est une absence **mesurée**, jamais un trou (règle n° 10) |
| `artifacts/stats_r1.json` | `gisements_declares`, `gisements_rattaches`, `gisements_hors_fenetre` (avec leurs `id`), `gisements_hors_terre` (avec leurs `id`), `cellules_dotees`, `cellules_totales`, `par_nature` (compte par valeur de `R1_VALID_RESOURCE_KINDS`, y compris les natures à zéro), `par_certitude`, `cellules_a_plusieurs_gisements`, `apply_declarations` |
| `artifacts/MANIFEST_r1.json` | `pipeline_version`, `crs`, `inputs` (empreintes calculées à l'exécution de `cells_g3.json` et `data/resources_1400.json`), `outputs` |
| `registry/resource_registry.json` | registre des gisements émis, date `R1_REGISTRY_CREATED`, `pipeline_version` — patron : `registry/river_registry.json` |
| `logs/v1_081_resources.log` | journal lisible de la preuve |
| `logs/v1_081_qa.json` | `checks` (`7` entrées, `passed` + `red_proof`) et `determinism.sha256` |
| `logs/v1_081_declarations_on.txt` | sortie de la dérivation déclarations **actives** |
| `logs/v1_081_declarations_off.txt` | sortie de la dérivation déclarations **coupées** — le couple prouve `R1-D`, comme `v1_050_g4b_links_on/off.txt` l'a fait pour les liens topologiques de G4 |
| `capture/v1_081_resources_window.png` | les gisements rattachés sur la fenêtre pilote, une couleur par nature, cellules dotées distinguées des cellules vides |
| `steps/r1_resources_1400.py` | le module neuf ; exporte `run_resources(apply_declarations=True)` |
| `qa/checks_r1.py` | les six contrôles `R1-*` et `run_r1_green(...)` (D6) |
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
6. joue les sept contrôles de `run_r1_green`, chacun avec son cas rouge ;
7. écrit `logs/v1_081_qa.json`, `logs/v1_081_resources.log`,
   `logs/v1_081_declarations_on.txt`, `logs/v1_081_declarations_off.txt` ;
8. rend le code `0` si et seulement si les sept contrôles sont verts, chacun
   avec une preuve rouge non vide, les deux passes actives identiques et la
   passe coupée différente.

Aucune horloge murale, aucun horodatage courant, aucune graine non fixée.

### D10 — Preuve rouge d'abord

`tests/test_qa_red_r1.py` fournit **un cas rouge par contrôle** : `Q10`,
`R1-A`, `R1-B`, `R1-C`, `R1-D`, `R1-E`, `R1-F`. Chaque cas est une mutation
locale explicite sur une copie en mémoire — par exemple une entrée privée de
son champ `source` pour `R1-A` ; un gisement rattaché à une cellule qui ne le
contient pas pour `R1-B` ; un gisement retiré de toutes les catégories pour
`R1-C` ; une empreinte de passe coupée rendue égale à celle de la passe
active pour `R1-D` ; une clé `tonnage` injectée pour `R1-E` ; une clé
`province_id` ajoutée à une cellule pour `R1-F`. **Aucun cas ne passe par une
modification de `qa/checks.py` ni de `qa/checks_r1.py`.** Un `red_proof` vide
vaut échec du contrôle.

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
  `data/resources_1400.json` ; sinon c'est celle de D4, sans retouche.

Si l'une de ces quatre valeurs n'est pas atteinte, **le lot s'arrête ici**.
Ce n'est pas un `REJECT` du travail — c'est un lot lancé avant son heure, et
le Générateur le signale sans produire aucun fichier. Il n'écrit **jamais**
l'amendement lui-même : le producteur ne s'accorde pas sa propre
autorisation.

### SC1 — Toute déclaration est complète, légale, et vit dans la donnée (`R1-A`)

Depuis `pipeline/geo/` :

```
../../.venv/bin/python tests/run_proof_r1.py
```

- `gisements_declares` est **lu** de `data/resources_1400.json`, jamais
  recopié d'une constante ni du brief.
- `declarations_incompletes` vaut `0` : chaque entrée porte tous les champs
  de `R1_REQUIRED_DEPOSIT_FIELDS`, tous non vides.
- `natures_hors_vocabulaire` vaut `0` et `certitudes_hors_vocabulaire` vaut
  `0`.
- `gisements_en_dur_dans_le_module` vaut `0` : aucun identifiant de gisement
  n'apparaît comme littéral dans `steps/r1_resources_1400.py`.
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
- `controles_r1_verts` vaut `7` sur `7` ;
  `controles_r1_avec_preuve_rouge_non_vide` vaut `7` sur `7` ;
  `paires_sha_determinisme_egales` est égal au nombre total de paires, lui
  même strictement positif ; `code_sortie_run_proof_r1` vaut `0`.
- `fichiers_preuve_suivis_par_git` égale le nombre de preuves déclarées en
  D7, vérifié par `git ls-files`. `artifacts/`, `logs/` et `capture/` étant
  exclus par `pipeline/geo/.gitignore`, chaque preuve est committée par ajout
  forcé individuel ; **aucun élargissement de `.gitignore` n'est autorisé**.
- `README.md` mis à jour : R1 livre les **gisements extractifs déclarés** et
  rien d'autre. Le texte dit explicitement que ce lot ne livre ni quantité,
  ni rendement, ni ressource agricole ou forestière — ces dernières
  dépendant d'un climat et d'un sol non disponibles. Restent non livrés : le
  climat proprement dit, les villes (G7), la possession (G8), les LOD (G9),
  les textures d'identifiants (G10), l'apparence (A12), G5-bis, G5-ter et la
  QA de chaîne complète (G11/G12). Le relief (G6) n'est **pas** décrit par ce
  lot, quel que soit l'état du lot 024.
- La capture de D7 est **regardée et décrite** dans
  `deliverables/generator-log.md` (règle n° 11) : les gisements doivent
  apparaître aux endroits attendus — étain en Cornouailles, sel en Pologne et
  en Franche-Comté, cuivre et fer en Suède centrale, mercure en Castille — et
  aucun point ne doit flotter en mer.
- La suite du harnais reste verte :

```
.venv/bin/python -m pytest harness/tests/ -q
```

  `tests_harness_passed_026` rapporté avec le nombre de tests collectés pour
  dénominateur ; sentinelle `-1` si le provisionnement échoue (Waivers),
  jamais `0`.

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. Produire une quantité, une réserve, un tonnage, un rendement, une
   intensité, une teneur ou une capacité — sous ce nom ou sous un autre
   (`R1-E`). Un gisement dit ce qu'il y a, jamais combien.
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
    `0`) comme seuil de contrôle : ce sont des constats, et un contrôle qui
    s'y compare nomme sa propre référence (règle n° 2).
13. Employer l'alias nu de l'interpréteur ni un chemin Windows (règle n° 1).
14. Committer, pousser, créer ou changer de branche, ni fusionner
    (ADR-0014).
15. Écrire, compléter ou interpréter `amendment-001-arbitrage-gisements.md`.
    Le Générateur **constate** son existence (SC0) ; il ne la produit pas, et
    il ne déduit pas d'un silence que l'arbitrage vaut accord. Démarrer sans
    lui, même « pour préparer le terrain », est un dépassement de périmètre.
16. Modifier la liste de D4 de sa propre initiative — l'élargir, la réduire,
    corriger une coordonnée, ajouter un site « évident qui manque ». Toute
    évolution de la liste passe par l'amendement (`A2`).
15. Rapporter un compteur depuis un calcul manqué sans le déclarer (règle
    n° 8 : sentinelle `-1`, jamais `0`) — un zéro réellement mesuré, comme
    `gisements_hors_terre = 0`, est légitime et s'en distingue.

---

## Required Counters (sous-ensemble ; le détail complet est dans les Success Conditions)

| nom | source | dénominateur |
|---|---|---|
| `amendement_arbitrage_present` | présence et suivi git de `amendment-001-arbitrage-gisements.md` | `1` vérification ; doit valoir `1` avant toute autre action |
| `decision_proprietaire_citee` | existence réelle du chemin cité par l'amendement sous `hermes/requests/` ou `docs/adr/` | `1` vérification ; doit valoir `1` |
| `questions_arbitrage_repondues` | réponses explicites à `A1`, `A2`, `A3` dans l'amendement | `3` ; doit valoir `3` |
| `liste_appliquee_est_celle_de_l_amendement` | comparaison entre la liste écrite dans `data/resources_1400.json` et celle que l'amendement retient | `1` comparaison ; doit valoir `1` |
| `gisements_declares` | entrées de `data/resources_1400.json` | `1` lecture de fichier ; strictement positif |
| `declarations_incompletes` | entrées auxquelles manque un champ de `R1_REQUIRED_DEPOSIT_FIELDS` | gisements déclarés ; doit valoir `0` |
| `natures_hors_vocabulaire` | entrées dont `resource` n'est pas dans `R1_VALID_RESOURCE_KINDS` | gisements déclarés ; doit valoir `0` |
| `certitudes_hors_vocabulaire` | entrées dont `certainty` n'est pas dans `R1_VALID_CERTAINTY` | gisements déclarés ; doit valoir `0` |
| `gisements_en_dur_dans_le_module` | identifiants de gisement trouvés comme littéraux dans `steps/r1_resources_1400.py` | gisements déclarés ; doit valoir `0` |
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
| `controles_r1_verts` | tableau `checks` de `logs/v1_081_qa.json` | `7` |
| `controles_r1_avec_preuve_rouge_non_vide` | champ `red_proof` de chaque entrée | `7` |
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
| « l'amendement d'arbitrage n'existe pas » | `git ls-files harness/queue/briefs/026-geo-gisements-1400-r1/amendment-001-arbitrage-gisements.md` depuis la racine | une sortie **vide**. **Ce n'est pas un waiver, c'est le blocage nominal de ce brief** (SC0) : le Générateur s'arrête, ne produit aucun fichier, et le signale. Aucune condition de succès n'est excusée, aucune n'est tentée. Le lot sera relancé après l'arbitrage du propriétaire |
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
   `amendment-001-arbitrage-gisements.md`, ce brief ne se lance pas.
2. **La fusion du lot 025**, qui pose `WORLD_TERMS_FORBIDDEN_KEYS`
   (Provenance).

Les deux se vérifient par des commandes, pas par un souvenir de réunion.

### Estimation d'appels d'outils

**Estimation du Planificateur : `110` appels d'outils**, une fois le brief
débloqué. Sous le seuil de `150` au-delà duquel un brief doit être découpé, et
sous l'arrêt du budget à `160`. Ancres employées : les lots 021 et 024, de
structure identique. Ce lot n'a ni téléchargement, ni lecture matricielle, ni
astronomie ; son coût propre est la rédaction d'une `historical_reason` par
gisement et la troisième passe de réversibilité. L'estimation suppose une
liste de l'ordre de grandeur de celle de D4 ; si l'arbitrage `A2` l'élargit
fortement, elle est à refaire avant génération. À vérifier avant génération :

```
.venv/bin/python harness/budget.py split-check \
  --brief harness/queue/briefs/026-geo-gisements-1400-r1 \
  --estimated-calls 110
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
ses deux modes, que les **six** conditions de succès (SC0 à SC5) sont
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
