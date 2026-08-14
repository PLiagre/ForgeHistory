# Brief 019 : l'adjacence maritime (G4) — quelle mer touche quelle côte, quelle mer communique avec quelle mer

**Authored**: 2026-08-14T08:50:00Z
**Author**: forge-planificateur

> **Note de transparence (contrat du Planificateur) :** le rôle signataire est
> le rôle natif du harnais `forge-planificateur`. L'acteur réel est un
> sous-agent Cursor Cloud (modèle Claude Opus 5), orchestré par un agent Cursor
> Cloud qui remplace le CTO Claude (plafond de quota atteint). Aucun suffixe
> n'est ajouté à la signature : le contrôle mécanique
> `verdict_is_not_self_authored` compare les acteurs de part et d'autre d'un
> lot, et un couple de signatures suffixées serait refusé.

**Amendement en vigueur :** ce brief est amendé par
`amendment-001-escalade-empreinte-g3.md`, qui ouvre la dérogation d'escalade que
D2 annonçait sur l'empreinte du littoral et récrit SC7 en deux branches ; lire
cet amendement avant SC7.

---

## Provenance

Ce brief est le **premier lot atomique du jalon E1 — Fondations monde**, jalon
qui clôt F1 (relief, climat, ressources livrés par `pipeline/geo/`, artefacts
consommables par `sim/`). E1 entier est trop gros pour un seul brief ; il a
donc été découpé, et ce lot-ci ne traite **que** l'adjacence maritime : les
zones de mer et le graphe typé terre-terre / terre-mer / mer-mer / détroit.
Ni relief, ni climat, ni ressources, ni fleuves.

Pourquoi cette pièce d'abord : les fleuves et le relief se rattachent à des
cellules **et** à des arêtes. Les cellules existent (596 cellules livrées et
committées). Les arêtes typées n'existent pas : `artifacts/adjacency_g3.json`
est un graphe de contrôle qualité non typé, où toute la mer est un unique
identifiant fourre-tout. On ne peut donc pas encore poser la question « ce
navire peut-il aller de la Manche au Zuiderzee ? ».

Ce n'est **pas** un `NEEDS_SPLIT` : un seul sous-système (`pipeline/geo/`), un
seul thème causal, un seul jeu d'artefacts.

Ce brief est **neuf et autonome**. Le brief 007 (lot 007b, jamais exécuté) est
de la matière historique, pas une instruction : ses chemins Windows et ses
empreintes VictoriaProject sont périmés. À partir d'ici, **ce `brief.md` est la
SEULE instruction** (voir `CLAUDE.md` › Single Source of Instruction). Tout le
nécessaire est écrit ici ; aucun autre document n'a à être consulté pour savoir
quoi faire.

---

## World-Terms Requirement

**Chaîne causale.**

Une caravane, une flotte, une armée ne se téléportent pas. Tant que le monde
n'est que de la terre, « qui touche qui » suffit : une troupe passe d'une
cellule à sa voisine parce que les deux se touchent. Dès qu'il y a de l'eau,
cela ne suffit plus, et pour une raison physique, pas comptable.

Une cargaison qui quitte un port n'entre pas dans « la mer » : elle entre dans
**une** étendue d'eau précise, qui touche cette côte-là et pas une autre. Pour
qu'elle arrive ailleurs, il faut que cette étendue communique avec l'étendue
qui touche le port d'arrivée — de proche en proche, comme une troupe de cellule
en cellule. Un port dont l'eau ne communique avec aucune autre eau est un port
fermé : les navires y entrent et n'en ressortent pas, non parce qu'une règle
l'interdit, mais parce qu'il n'existe aucun chemin d'eau. Une côte, de son
côté, n'est pas une propriété qu'on inscrit sur une cellule : une terre est
littorale **parce qu'**elle touche une eau. Retirez l'eau, la littoralité
disparaît d'elle-même.

Il existe enfin un cas où deux terres qui ne se touchent pas se comportent
presque comme si elles se touchaient : un bras d'eau si court qu'on le
franchit — un détroit. Une armée qui veut passer de Calais à Douvres ne marche
pas, mais elle ne traverse pas non plus un océan : elle franchit trente
kilomètres. Une flotte qui bloque ce bras coupe le passage. Ce fait-là ne se
lit ni dans « quelle terre touche quelle terre » (elles ne se touchent pas), ni
dans « quelle mer touche quelle côte » (ce serait un simple embarquement) : il
demande son propre type d'arête, et une largeur mesurée.

**L'histoire, enfin, ne coïncide pas avec la géométrie moderne.** En 1400, le
Zuiderzee était un golfe salé ouvert sur la mer du Nord ; l'Afsluitdijk (1932)
l'a refermé, et la Lauwerszee a été coupée en 1969. La géométrie d'aujourd'hui
présente donc deux bassins enfermés là où l'eau communiquait librement. La
correction n'est pas d'ouvrir une brèche dans le trait de côte — cela
falsifierait la source — mais de **déclarer la continuité** : ces deux eaux
communiquaient, voici la source, voici la date. C'est un fait historique daté,
porté par une déclaration relue, pas une retouche silencieuse de la carte.

**Interdit** dans ce lot : « si zone maritime alors +N % de commerce », « bonus
de flotte en détroit », tout barème. Rien de ce brief ne parle de
gains ni de malus. On établit ce que l'eau permet et ce qu'elle empêche.

---

## Vocabulaire (expliqué une fois)

- **cellule** : l'unité spatiale terrestre du projet, identifiée par `cell_id`.
  Seule clé spatiale du monde (ADR-0003).
- **zone de mer** : un morceau d'eau découpé comme une cellule l'est pour la
  terre, identifié par `zone_id`. Ce n'est pas une cellule terrestre et son
  identifiant vit dans une plage à part.
- **arête typée** : un lien entre deux entités, portant la nature du lien
  (`land-land`, `land-sea`, `sea-sea`, `strait`).
- **composante d'eau** : un ensemble d'eaux qui communiquent entre elles de
  proche en proche. La « mer extérieure » est la composante qui touche le bord
  de la fenêtre d'étude ; un **bassin enfermé** est une composante qui ne le
  touche pas.
- **lien topologique déclaré** : une arête ajoutée parce qu'une source
  historique atteste une communication que la géométrie moderne ne montre plus.
- **fenêtre pilote** : le rectangle géographique d'étude, déjà dérivé par
  `constants.py` (`PILOT_WINDOW_LONLAT`) ; ce brief ne la redéfinit pas.
- **empreinte SHA256** : condensé d'un fichier, qui prouve que deux fichiers
  sont octet pour octet identiques. Citée **par son nom de fichier**, jamais
  par sa valeur hexadécimale (règle durement acquise n° 12).

---

## Ce qui existe déjà, et que ce lot lit sans le refaire

Vérifié sur le dépôt au moment de l'écriture de ce brief :

- `pipeline/geo/artifacts/cells_g3.json`, `stats_g3.json`, `adjacency_g3.json`,
  `MANIFEST_g3.json` : committés. `stats_g3.json` porte `cell_count`.
- `pipeline/geo/constants.py` : porte déjà `SEA_ZONE_ID_BASE`,
  `SEA_ZONE_COUNT_MIN`, `SEA_ZONE_COUNT_MAX`, `G4_SEA_R_FLOOR_M`,
  `G4_SEA_R_CEIL_M`, `G4_SEA_MASTER_SEED`, `G4_SEA_LLOYD_ITERATIONS`,
  `G4_SEA_AREA_FLOOR_KM2`, `G4_SEA_AREA_CEIL_KM2`, `G4_SEA_COMPACTNESS_MIN`,
  `G4_STRAIT_MAX_WIDTH_M`, `G4_STRAIT_JUSTIFICATION`, `G4_PIPELINE_VERSION`,
  `G4_REGISTRY_CREATED`, `SEA_CELL_ID`, `LENGTH_EPS`.
- `pipeline/geo/qa/checks.py` : porte déjà `run_g4_green` et les huit contrôles
  qu'il assemble — `q1_polygon_validity`, `q4_no_isolated_entities`,
  `q7_adjacency_contiguous_typed`, `q10_determinism`,
  `g4a_littorality_derived`, `g4b_open_sea_reachable`,
  `g4c_sea_covers_without_holes`, `g4d_sea_ids_no_collision`.
- `pipeline/geo/pipeline.py` : porte déjà `_load_adjacency_module()`,
  `run_adjacency_g4(apply_topology_links=...)` et la branche
  `args.source == "adjacency"`. Le crochet est **déjà câblé** ; il attend un
  module qui n'existe pas.
- `pipeline/geo/data/corrections_1400.json` : porte déjà les deux corrections
  `declare_topology_link` (Zuiderzee/Afsluitdijk et Lauwerszee), avec source,
  date et certitude ; `steps/02b_corrections_1400.py` porte déjà
  `topology_link_corrections()` qui les rend triées.
- `unity/game_unity/Assets/StreamingAssets/data/sea_zones.json` : 14 zones aux
  noms attestés (Manche, Mer du Nord, …), avec des champs héritant d'anciennes
  provinces de jeu.
- **N'existe pas** : `pipeline/geo/steps/04_adjacency.py`,
  `pipeline/geo/tests/run_proof_g4.py`, `pipeline/geo/tests/test_qa_red_g4.py`,
  `pipeline/geo/legacy_game_data/sea_zones.json`, tout artefact G4.
- **N'existe pas non plus** : `pipeline/geo/artifacts/coastline_1400.json` (le
  littoral corrigé). Il est ignoré par git et absent d'un clone frais ; il se
  régénère (voir D2).

**Ce lot n'est pas un portage octet-à-octet.** L'arborescence
`sandbox/geo/` de VictoriaProject n'est pas sur cette machine. Aucune égalité
d'empreinte contre un chemin Windows n'est exigée, ni recevable comme preuve.
Ce lot est une **reconstruction** contre la barre qualité déjà portée :
`qa/checks.py`, les constantes `G4_*` / `SEA_ZONE_*`, et le crochet de
`pipeline.py`. Et elle vise la maille **actuelle** (596 cellules), pas
l'ancienne maille d'environ 401 cellules.

---

## Décisions de conception tranchées par le Planificateur

Le Générateur n'arbitre aucun de ces points. Il choisit librement les noms de
fonctions et de variables internes, et l'organisation du code dans le périmètre
autorisé.

### D1 — Entrées exactes

Le nouveau module lit, en lecture seule :

| entrée | clés employées |
|---|---|
| `pipeline/geo/artifacts/cells_g3.json` | `cells[]` : `cell_id`, `geometry`, `centroid`, `area_m2` |
| `pipeline/geo/artifacts/stats_g3.json` | `cell_count` (dénominateur de tout compteur par cellule) |
| `pipeline/geo/artifacts/adjacency_g3.json` | `adjacency[]` filtré sur `kind == "land-land"` |
| `pipeline/geo/artifacts/MANIFEST_g3.json` | `inputs.coastline_1400` (empreinte de la terre qui a produit les cellules) |
| `pipeline/geo/data/corrections_1400.json` | via `topology_link_corrections()` de `steps/02b_corrections_1400.py` |
| `pipeline/geo/legacy_game_data/sea_zones.json` | `sea_zones[]` : `name`, `is_ocean`, et le tableau d'identifiants hérités servant d'ancrage (D5) |
| `pipeline/geo/legacy_game_data/province_coordinates.json` | `coordinates[]` : `id`, `lon`, `lat` — **uniquement** comme points d'ancrage de noms (D5) et pour la comparaison QA (D10) |
| `pipeline/geo/constants.py` | toutes les bornes et graines, **lues**, jamais recopiées en littéral |

`pipeline/geo/legacy_game_data/sea_zones.json` n'existe pas encore : il est
créé par **copie octet pour octet** de
`unity/game_unity/Assets/StreamingAssets/data/sea_zones.json`. Le fichier Unity
n'est ni modifié ni déplacé. L'égalité se prouve en calculant les deux
empreintes **à l'exécution** et en les comparant ; aucune valeur hexadécimale
n'est recopiée dans un test, un document ou un commentaire (règle n° 12 — une
empreinte citée par valeur piège tous les briefs suivants, ce qui est
exactement ce qui est arrivé à l'empreinte citée par le brief 007).

### D2 — La mer est dérivée, et la mer de 1400 n'est pas « tout ce qui n'est pas terre »

La substance maritime est obtenue ainsi :

1. Le littoral corrigé de 1400 est produit en appelant
   `run_corrections(apply_corrections=True)` de `steps/02b_corrections_1400.py`
   (module chargé dynamiquement, jamais modifié). Cet appel rend `land_xy` (la
   terre projetée), `open_sea_ll` (les étendues d'eau que les corrections de
   1400 reclassent en mer ouverte) et réécrit
   `artifacts/coastline_1400.json`.
2. L'eau brute est la **fenêtre pilote projetée moins la terre corrigée**.
3. La **mer de 1400** est : la composante d'eau qui touche le bord de la
   fenêtre (la mer extérieure) **union** les étendues de `open_sea_ll`
   projetées. Rien d'autre.
4. Tout autre plan d'eau enclavé (un lac qui était un lac en 1400 — Léman,
   Constance…) est **exclu de la mer** et compté. Ce n'est pas un oubli : un
   lac n'est pas une mer, et le contrôle `g4b_open_sea_reachable` exigerait
   sinon qu'on rejoigne le Léman à la mer du Nord.

Le nombre de plans d'eau exclus est un fait mesuré et rapporté.

**Cohérence avec les cellules :** l'empreinte de
`artifacts/coastline_1400.json` ainsi régénéré doit être **égale** à
`MANIFEST_g3.json`'s `inputs.coastline_1400`, les deux valeurs étant lues à
l'exécution. Si elles diffèrent, la mer et les cellules ne décrivent pas le
même monde : c'est un cas d'escalade (voir Waivers), pas un détail à ignorer.

### D3 — Le découpage en zones de mer réemploie la mécanique de G3, avec un germe obligatoire par composante

Même famille de méthode que la maille terrestre : semis à distance minimale,
relaxation de Lloyd à nombre d'itérations **fixe** (le déterminisme primant sur
la convergence), diagramme de Voronoï découpé sur la mer. Tous les paramètres
sont **lus** de `constants.py` : `G4_SEA_R_FLOOR_M`, `G4_SEA_R_CEIL_M`,
`G4_SEA_MASTER_SEED`, `G4_SEA_LLOYD_ITERATIONS`.

Le Générateur peut soit charger dynamiquement `steps/03_cells.py` pour
réemployer ses fonctions (sans le modifier), soit implémenter la mécanique dans
le nouveau module. Les deux sont permis ; `steps/03_cells.py` reste intouché.

**Contrainte tranchée ici :** chaque composante de mer reçoit **au moins un
germe**, y compris un bassin enfermé minuscule. Sans cela, un bassin enfermé
n'aurait aucune zone, `g4c_sea_covers_without_holes` verrait un trou, et le
Zuiderzee — le cas historique même que ce lot doit démontrer — disparaîtrait du
graphe. Ce point n'est pas laissé à l'appréciation du Générateur.

### D4 — Identifiants de zones : à partir de la base, en sautant les identifiants terrestres déjà pris

Les identifiants de zones de mer sont attribués depuis `SEA_ZONE_ID_BASE`
(lu de `constants.py`) par ordre croissant, **en sautant tout entier présent
dans l'ensemble des `cell_id` lus de `cells_g3.json`**.

Raison mesurée : le commentaire de `constants.py` suppose des identifiants
terrestres inférieurs à la base, mais la maille actuelle en émet bien au-delà
(l'étendue va de 1175 à 10466 pour 596 cellules, donc un ensemble creux qui
chevauche la plage des zones). Une attribution naïve depuis la base
provoquerait une collision réelle, que `g4d_sea_ids_no_collision` refuserait à
juste titre. La base **n'est pas recalibrée** ; c'est l'attribution qui saute
les valeurs prises.

L'ordre d'attribution est **stable et géométrique** : les zones sont triées par
une clé dérivée de leur géométrie (par exemple le centre, ordonné en y puis en
x, arrondi à la décimale d'export), jamais par un ordre de parcours de
dictionnaire ni par l'ordre d'arrivée du semis.

### D5 — Les noms de mer sont un proxy hérité, déclaré comme tel, et jamais une clé spatiale

Les noms sont **lus** de `legacy_game_data/sea_zones.json` (la copie créée en
D1). Ils ne sont ni inventés, ni traduits, ni complétés.

Règle d'attribution, tranchée ici : pour chacune des 14 zones nommées héritées,
on calcule un **point d'ancrage** = la moyenne des coordonnées (lon, lat) des
identifiants hérités que cette zone nommée déclare comme riverains, ces
coordonnées étant lues de `legacy_game_data/province_coordinates.json`, puis
projetées. Chaque zone de mer prend le nom de l'ancrage **le plus proche** de sa
géométrie. Égalité de distance : le plus petit identifiant hérité gagne.

Conséquences assumées et mesurées, pas corrigées après coup :

- Plusieurs zones de mer peuvent porter le **même** nom — une mer nommée est
  plus grande qu'une zone. C'est attendu.
- Un nom hérité peut n'être employé par aucune zone. Le nombre de noms employés
  et non employés est **mesuré et rapporté**, jamais imposé par un plancher.

Le tableau d'identifiants riverains hérités est un **proxy de localisation de
nom**, en aucun cas une clé spatiale : il ne sert qu'à situer un ancrage, il
n'entre dans aucun artefact exporté, et il ne définit aucune appartenance.
Cette déclaration est écrite dans `pipeline/geo/README.md` **avant** toute
citation de compteur mesuré.

### D6 — Les quatre types d'arêtes, et d'où chacun vient

| type | dérivation |
|---|---|
| `land-land` | **lu** de `artifacts/adjacency_g3.json` (`kind == "land-land"`), non recalculé — les cellules committées sont la source unique, et `q7_adjacency_contiguous_typed` revérifie de toute façon la contiguïté géométriquement |
| `land-sea` | dérivé : une cellule et une zone dont les frontières partagent une longueur ≥ `LENGTH_EPS` (lu de `constants.py`) ; la longueur partagée mesurée est portée par l'arête |
| `sea-sea` | dérivé : deux zones dont les frontières partagent une longueur ≥ `LENGTH_EPS` ; **plus** les liens topologiques déclarés (D8), marqués comme tels |
| `strait` | dérivé : voir D7 |

L'identifiant fourre-tout `SEA_CELL_ID` (la mer unique du graphe G3) **ne doit
apparaître dans aucune arête exportée par G4** : toute arête `land-sea` désigne
une zone réelle. Le nombre d'arêtes portant encore l'identifiant fourre-tout est
mesuré et doit valoir 0.

La **littoralité est dérivée** : une cellule est littorale parce qu'elle porte
au moins une arête `land-sea`. Elle n'est ni saisie, ni stockée sur la cellule,
ni recopiée d'ailleurs — c'est ce que vérifie `g4a_littorality_derived`.

### D7 — Détroit : deux terres non contiguës séparées par une eau plus courte que le seuil déclaré

Une arête `strait` relie deux cellules terrestres telles que :

- elles ne sont **pas** contiguës (distance > `LENGTH_EPS`), et
- leur distance est ≤ `G4_STRAIT_MAX_WIDTH_M`, **lu** de `constants.py` (le
  seuil et sa justification sont déjà écrits là ; ce lot ne les rediscute pas).

Chaque arête `strait` porte la largeur mesurée en mètres. Exigence de fond :
au moins un détroit doit relier **deux masses terrestres distinctes** (deux
composantes connexes de la terre), car c'est là le fait de monde qu'un détroit
énonce — deux terres qu'on ne peut pas rejoindre à pied et qu'un bras d'eau
court sépare. Le nombre de détroits inter-masses et la largeur minimale mesurée
sont rapportés.

### D8 — Contrat de `run_adjacency(apply_topology_links_flag)` : le lien déclaré est porteur, et son absence est le cas rouge naturel

`pipeline.py` appelle déjà
`adj.run_adjacency(apply_topology_links_flag=...)`. Le contrat est :

- **Avec liens (`True`)** : chaque correction `declare_topology_link` lue de
  `corrections_1400.json` produit une arête `sea-sea` marquée
  `declared_topology_link: true`, portant l'identifiant de la correction, sa
  source, sa date et sa certitude. La cible du lien est la zone qui porte le
  **nom attesté** demandé par la correction **et** qui appartient à la mer
  extérieure ; à égalité, la plus proche du bassin enfermé, puis le plus petit
  `zone_id`. Résultat attendu : `reachability.all_enclosed_reachable` vaut vrai,
  et aucun bassin enfermé ne reste injoignable.
- **Sans liens (`False`)** : aucune arête déclarée. Les bassins du Zuiderzee et
  de la Lauwerszee redeviennent injoignables depuis la mer extérieure, et
  `g4b_open_sea_reachable` passe au **rouge**.

Ce rouge-là est le cas rouge **naturel** du contrôle G4-B : on ne mute rien, on
coupe la déclaration historique et on constate que le monde se referme. C'est
la seule forme recevable de preuve rouge pour G4-B (une mutation synthétique
prouverait le code du contrôle, pas le caractère porteur de la déclaration).

Si, avec les liens actifs, un bassin restait injoignable, ce n'est **pas** un
motif pour relâcher le contrôle : c'est une escalade (voir Waivers).

### D9 — Sorties exactes

Sous `pipeline/geo/` :

| fichier | contenu |
|---|---|
| `artifacts/sea_zones_g4.json` | les zones : `zone_id`, `name`, `is_ocean`, `enclosed`, `geometry`, `area_km2`, `compactness_polsby_popper`, `name_source`, `name_anchor_distance_m` |
| `artifacts/adjacency_g4.json` | les arêtes typées : `a`, `b`, `kind`, plus `shared_length_m` / `gap_m` / `declared_topology_link` selon le type. **Zéro occurrence de la sous-chaîne `province`** |
| `artifacts/topology_links_g4.json` | les liens déclarés appliqués (identifiant de correction, source, date, certitude, zones reliées) + le bloc `reachability` dont `all_enclosed_reachable` |
| `artifacts/stats_g4.json` | `sea_zone_count`, `adjacency_count`, `by_kind`, `coastal_cell_count`, distributions de surface et de compacité des zones, comptes d'exclusion (lacs), largeur minimale de détroit |
| `artifacts/adjacency_divergence_g4.json` | **QA seulement** (D10) : confrontation du graphe terre-terre dérivé au graphe hérité `legacy_game_data/province_adjacency.json` |
| `artifacts/MANIFEST_g4.json` | version, projection, `inputs` (empreintes des entrées, calculées à l'exécution), `outputs` (empreintes des sorties) |
| `registry/sea_zone_registry.json` | registre des zones émises (identifiant, clé de domaine, date de création lue de `G4_REGISTRY_CREATED`) |
| `logs/v1_050_qa.json` | rapport de contrôle : tableau `checks` (huit entrées, `passed` + `red_proof`) + bloc `determinism.sha256` (paires de deux passes) |
| `logs/v1_050_adjacency.log` | journal lisible de la preuve |
| `logs/v1_050_g4b_links_on.txt` | sortie du contrôle G4-B **avec** liens : atteignable |
| `logs/v1_050_g4b_links_off.txt` | sortie du contrôle G4-B **sans** liens : bassins injoignables nommés |
| `capture/v1_050_sea_zones_window.png` | les zones de mer nommées sur la fenêtre pilote, avec les arêtes `sea-sea` |
| `capture/v1_050_zuiderzee_links_on.png` | zoom Zuiderzee, liens actifs |
| `capture/v1_050_zuiderzee_links_off.png` | même zoom, liens coupés |
| `steps/04_adjacency.py` | le nouveau module (exporte `run_adjacency(apply_topology_links_flag=...)`) |
| `tests/test_qa_red_g4.py` | cas rouges, un par contrôle (D12) |
| `tests/run_proof_g4.py` | script de preuve (D11) |
| `README.md` | mise à jour (SC9) |

**Trois couples `must_differ_from`** doivent être déclarés dans
`deliverables/manifest.json`, en chemins relatifs au dossier du brief. La porte
mécanique ne peut pas deviner qu'un couple doit différer ; non déclaré, il n'est
pas vérifié :

1. `deliverables/pre-edit/pipeline-geo-README.md.orig` ↔ le `README.md` publié.
2. `logs/v1_050_g4b_links_off.txt` ↔ `logs/v1_050_g4b_links_on.txt`.
3. `capture/v1_050_zuiderzee_links_off.png` ↔ `capture/v1_050_zuiderzee_links_on.png`.

### D10 — ADR-0003 dans l'artefact exporté, pas seulement dans la prose

`docs/adr/0003-single-spatial-primary-key.md` a tranché : la cellule est la
seule clé spatiale, la province est une agrégation dérivée. Le fichier hérité
de noms de mer parle, lui, d'anciennes provinces de jeu. La frontière est donc
mécanique :

- `artifacts/adjacency_g4.json`, `sea_zones_g4.json`, `topology_links_g4.json`,
  `stats_g4.json`, `MANIFEST_g4.json` et `registry/sea_zone_registry.json`
  contiennent **zéro** occurrence de la sous-chaîne `province`.
- La confrontation au graphe hérité vit **uniquement** dans
  `artifacts/adjacency_divergence_g4.json`, étiqueté `"qa_only": true` dans son
  propre contenu, et décrit comme tel dans `README.md` et dans le manifeste :
  comparaison unique, jamais consommée par un autre artefact, jamais lue par
  aucun code hors la preuve QA, jamais autorité spatiale.

Cette confrontation est **exigée**, pas optionnelle, et pour une raison :
un contrôle de frontière qui n'a rien à retenir ne prouve rien (règle n° 7 —
la présence n'est pas la fonction). En produisant vraiment la comparaison, le
compte d'occurrences à zéro dans les autres artefacts devient une mesure et non
une tautologie. Elle rend en outre trois faits utiles : arêtes héritées
confirmées, contredites, et manquantes.

La localisation d'un identifiant hérité y est faite par la cellule la plus
proche de sa coordonnée, distance mesurée reportée — un repère de comparaison,
jamais une appartenance.

### D11 — Déterminisme : deux passes, empreintes comparées

`tests/run_proof_g4.py` :

1. charge **une fois** la terre corrigée et les entrées (D1, D2) ;
2. exécute la dérivation **et l'export complet** deux fois, liens actifs ;
3. compare, empreinte par empreinte, les artefacts des deux passes
   (`q10_determinism`) : chaque paire doit être égale et non vide ;
4. exécute une troisième fois, **liens coupés**, pour le rouge naturel de G4-B ;
5. écrit `logs/v1_050_qa.json` et `logs/v1_050_adjacency.log` ;
6. rend le code de sortie 0 si et seulement si les huit contrôles sont verts,
   chacun avec une preuve rouge non vide, et les deux passes identiques.

Ce qui est chargé une fois et ce qui est rejoué deux fois est écrit ici
explicitement pour qu'aucun affaiblissement ne passe pour un détail : la
lecture des entrées ne conditionne pas le déterminisme de l'export, l'égalité
des empreintes de la terre avec l'entrée de G3 (D2) le fait.

Aucune horloge murale, aucun horodatage courant dans un artefact : le manifeste
emploie l'horodatage figé déjà employé par G2b/G3.

### D12 — Preuve rouge d'abord

`tests/test_qa_red_g4.py` expose `run_all_red_g4(...)` sur le modèle de
`tests/test_qa_red_g3.py` déjà présent, et fournit **un cas rouge par contrôle**
des huit assemblés par `run_g4_green` : `Q1`, `Q4`, `Q7`, `Q10`, `G4-A`,
`G4-B`, `G4-C`, `G4-D`.

- `G4-B` : cas **naturel** (liens coupés, D8) — jamais une mutation.
- Les sept autres : mutations locales explicites sur des copies en mémoire (par
  exemple une zone dont on retire la géométrie pour ouvrir un trou de
  couverture ; un identifiant de zone ramené sous la base pour la collision ;
  une littoralité saisie à la main divergeant des arêtes). Aucun cas ne doit
  passer par une modification de `qa/checks.py`.

Un contrôle qui ne peut pas rougir ne prouve rien (règle n° 4). Un `red_proof`
vide vaut échec du contrôle, même si le vert est vert.

### D13 — Les bornes déclarées ne se recalibrent pas ; les bornes d'intention se rapportent

Deux familles de bornes, traitées différemment et explicitement :

**Bornes d'acceptation** (bloquantes, lues de `constants.py`, jamais
modifiées) : `SEA_ZONE_COUNT_MIN`, `SEA_ZONE_COUNT_MAX`, `SEA_ZONE_ID_BASE`,
`G4_STRAIT_MAX_WIDTH_M`, `LENGTH_EPS`, et les paramètres de semis
(`G4_SEA_R_FLOOR_M`, `G4_SEA_R_CEIL_M`, `G4_SEA_MASTER_SEED`,
`G4_SEA_LLOYD_ITERATIONS`).

**Bornes d'intention** (rapportées, non bloquantes) :
`G4_SEA_AREA_FLOOR_KM2`, `G4_SEA_AREA_CEIL_KM2`, `G4_SEA_COMPACTNESS_MIN`.
Elles n'entrent pas dans les huit contrôles de `run_g4_green`. Le nombre de
zones hors de ces bornes est **mesuré, rapporté et, s'il n'est pas nul, inscrit
comme constat ouvert** dans le journal et dans `README.md` — comme la dette de
compacité de G3 l'a été, honnêtement, plutôt que maquillée. Il ne bloque pas
l'acceptation, et il ne justifie **jamais** de déplacer la borne.

**Exemption déclarée d'avance** : une zone qui constitue à elle seule un bassin
enfermé entier est exemptée du plancher de surface — un bassin de quatre-vingts
kilomètres carrés ne peut pas en faire deux cents. Cette exemption est déclarée
ici, avant mesure, et le nombre de zones exemptées est rapporté. Elle est la
sœur de l'exemption d'île singleton déjà admise par la maille terrestre.

**Interdiction ferme :** aucune valeur de `pipeline/geo/constants.py` n'est
modifiée par ce lot, dans aucun sens, pour aucune raison. Si une borne
d'acceptation s'avère mathématiquement inatteignable sur la maille de 596
cellules, le Générateur **escalade** avec commande et sortie mesurée (voir
Waivers) ; il ne bouge pas la borne. C'est la leçon coûteuse des amendements du
brief 007 : une borne déplacée après mesure n'est plus une borne.

### D14 — `pipeline.py` n'est pas modifié

Par défaut, `pipeline/geo/pipeline.py` est **intouché**. Le nouveau module doit
satisfaire le crochet existant, qui attend un dictionnaire portant au minimum :

- `metrics.sea_zone_count`, `metrics.adjacency_count`, `metrics.by_kind`
  (les quatre types), `metrics.coastal_cell_count` ;
- `projection` (objet portant `.epsg`) ;
- `reachability.all_enclosed_reachable` ;
- `captures` (dictionnaire de chemins), `shas` (dictionnaire chemin → empreinte).

Aucun ajustement de `pipeline.py` n'est autorisé par ce brief. Si le crochet
s'avérait incompatible avec le contrat ci-dessus, c'est une escalade, pas une
retouche : `pipeline.py` est lu, cité, et laissé tel quel.

Vérification exigée après coup : la commande
`../../.venv/bin/python pipeline.py --source adjacency` doit fonctionner depuis
`pipeline/geo/` et afficher la ligne de résumé G4 — c'est la preuve que le
contrat de retour est réellement satisfait, et non seulement décrit.

### D15 — Les preuves sont committées, malgré `.gitignore`

`pipeline/geo/.gitignore` exclut `artifacts/`, `logs/`, `capture/`, `build/`.
Une preuve laissée là serait invisible à `git status` et absente d'un clone :
personne ne pourrait la revérifier. Le mécanisme employé est **le même** que
les briefs 002 et 007a (dont les preuves G2/G3 sont bien suivies malgré la
règle d'exclusion) : `git add -f` sur chaque fichier de preuve déclaré.
Décision enregistrée, jamais silencieuse. `build/` reste exclu — ce sont des
intermédiaires, pas des preuves.

À savoir, et c'est la raison d'être d'un compteur dédié : la porte mécanique ne
vérifie le suivi git que des fichiers **internes** au dossier du brief ; ceux
déclarés par un chemin qui en sort sont signalés « non vérifiés ». Le suivi des
preuves sous `pipeline/geo/` doit donc être prouvé par `git ls-files`, compté,
et reporté.

### D16 — Périmètre de fichiers

**Autorisé (création ou modification) :**

- `pipeline/geo/steps/04_adjacency.py` (nouveau) ;
- `pipeline/geo/tests/run_proof_g4.py`, `pipeline/geo/tests/test_qa_red_g4.py`
  (nouveaux) ;
- `pipeline/geo/legacy_game_data/sea_zones.json` (copie octet pour octet) ;
- `pipeline/geo/README.md` (SC9) ;
- les artefacts, journaux, registres et captures listés en D9 ;
- `harness/queue/briefs/019-geo-adjacence-g4/deliverables/**` ;
- `harness/queue/cost-ledger.jsonl` (une seule ligne ajoutée en fin de fichier).

**Interdit (lecture seule, ou hors périmètre) :** `pipeline/geo/constants.py` ;
`pipeline/geo/qa/checks.py` ; `pipeline/geo/pipeline.py` ;
`pipeline/geo/io_util.py` ; `pipeline/geo/projection.py` ;
`pipeline/geo/steps/02_coastline.py` ; `pipeline/geo/steps/02b_corrections_1400.py` ;
`pipeline/geo/steps/03_cells.py` ; `pipeline/geo/data/**` ;
`pipeline/geo/sources.lock` ; `pipeline/geo/sources/**` ;
les artefacts et registres G2/G3 déjà committés
(`artifacts/cells_g3.json`, `adjacency_g3.json`, `stats_g3.json`,
`MANIFEST_g3.json`, `registry/cell_registry.json`,
`registry/g6_density_refinement.json`, `capture/v1_049_*`, `logs/v1_049_*`) ;
`pipeline/geo/.gitignore` ; `pipeline/geo/tests/*_g2*.py` ;
`pipeline/geo/tests/*_g3*.py` ; tout fichier sous `sim/` ; tout fichier sous
`unity/` (le fichier de noms de mer est **lu**, jamais écrit) ; `harness/*.py` ;
`harness/pipeline/` ; `architecture/` ; `docs/adr/**` ; `VISION.md` ;
`ROADMAP.md` ; `HANDOFF.md` ; `.github/**` ; les archives des briefs 001 à 018.

---

## Success Conditions

### SC1 — Les zones de mer existent, dénombrées dans la fourchette lue, sans collision d'identifiant

Depuis `pipeline/geo/` :

```
../../.venv/bin/python tests/run_proof_g4.py
```

- `zones_mer_denombrees` est le nombre d'entrées de `artifacts/sea_zones_g4.json`
  et se situe dans `[SEA_ZONE_COUNT_MIN, SEA_ZONE_COUNT_MAX]`, ces deux bornes
  étant **lues de `constants.py`** à l'exécution, jamais recopiées en dur dans
  un test ni dans un document.
- `composantes_mer_couvertes` est **égal** à `composantes_mer_totales` : chaque
  composante d'eau retenue comme mer de 1400 porte au moins une zone (D3).
- `collisions_id_mer_terre` vaut **0** : aucun `zone_id` n'appartient à
  l'ensemble des `cell_id` lus de `cells_g3.json`. Ce zéro est une mesure
  réelle ; la sentinelle « non calculé » du projet est `-1` (règle n° 8) et ne
  doit apparaître pour aucun compteur effectivement calculé.
- `ids_mer_sous_la_base` vaut **0**.
- `plans_eau_exclus_lacs` est mesuré et rapporté (D2) — il peut valoir zéro,
  mais il est calculé, pas supposé.
- `copie_sea_zones_identique` vaut 1 : la copie sous `legacy_game_data/` est
  octet pour octet celle du fichier Unity, l'égalité étant établie par calcul
  des deux empreintes à l'exécution.
- `cellules_lues_g3` est **égal** à `cell_count` lu de `stats_g3.json`.

Résultat attendu : `run_proof_g4.py` sort avec le code 0.

### SC2 — Le graphe est typé : les quatre natures d'arête existent, chacune mesurée sur le monde réel

- `aretes_totales` est strictement positif, et `kinds_non_vides` vaut **4** :
  `aretes_terre_terre`, `aretes_terre_mer`, `aretes_mer_mer` et `aretes_detroit`
  ont chacun un compte strictement positif dans `stats_g4.json`'s `by_kind`.
  Ce n'est pas une formalité : c'est la preuve mesurée que la détection de
  détroit et le lien déclaré ont réellement mordu sur la géométrie réelle, et
  pas seulement que le script s'est terminé sans erreur.
- `aretes_avec_id_mer_placeholder` vaut **0** : aucune arête exportée ne porte
  encore l'identifiant fourre-tout de la mer du graphe G3 (D6).
- `cellules_littorales` est dérivé des arêtes `land-sea`, avec `cellules_lues_g3`
  pour dénominateur, et `g4a_littorality_derived` est vert : la littoralité
  déclarée coïncide exactement avec celle que les arêtes impliquent.
- `q4_no_isolated_entities` est vert : aucune cellule et aucune zone n'est
  isolée du graphe.
- `q7_adjacency_contiguous_typed` est vert : aucune arête ne relie deux entités
  non contiguës, hors détroit et hors lien déclaré.

### SC3 — Détroit : seuil lu, largeur mesurée, au moins un entre deux masses terrestres distinctes

- `seuil_detroit_m` est lu de `constants.py` (`G4_STRAIT_MAX_WIDTH_M`) et
  rapporté ; aucun seuil n'est écrit en littéral dans le code.
- `aretes_detroit` est strictement positif.
- `detroits_entre_masses_differentes` est strictement positif : au moins un
  détroit relie deux composantes connexes distinctes de la terre — le fait de
  monde qu'un détroit énonce (D7).
- `ecart_min_detroit_m` est la plus petite largeur mesurée parmi les détroits,
  rapportée avec le nombre de détroits pour dénominateur.
- Chaque arête `strait` porte sa largeur mesurée dans l'artefact.

### SC4 — Le lien topologique déclaré est porteur : sans lui, le monde se referme

- `liens_topologiques_declares_appliques` est **égal** au nombre de corrections
  `declare_topology_link` lues de `data/corrections_1400.json` par
  `topology_link_corrections()`. Chaque lien appliqué porte, dans
  `artifacts/topology_links_g4.json`, l'identifiant de la correction, sa source,
  sa date et sa certitude — la déclaration reste traçable jusqu'à sa source.
- `bassins_enfermes_total` est mesuré (au minimum le Zuiderzee et la
  Lauwerszee).
- `bassins_enfermes_non_atteignables_liens_actifs` vaut **0**, et
  `topology_links_g4.json`'s `reachability.all_enclosed_reachable` vaut vrai.
- `bassins_enfermes_non_atteignables_liens_inactifs` est **strictement positif**
  et nomme les bassins concernés : c'est le rouge naturel de `G4-B` (D8).
- `zone_cible_nom_atteste_existe` vaut 1 : une zone de la mer extérieure porte
  bien le nom attesté que la correction demande, sans quoi le lien n'aurait
  aucune cible.
- Les deux sorties de contrôle sont committées et **diffèrent** :
  `logs/v1_050_g4b_links_on.txt` et `logs/v1_050_g4b_links_off.txt`, déclarées
  en couple `must_differ_from` (D9).
- Les deux captures du Zuiderzee (liens actifs / liens coupés) sont committées,
  déclarées en couple `must_differ_from`, et **regardées** par le Générateur :
  le journal décrit en une ou deux phrases ce que chacune montre, et
  `captures_regardees_et_decrites` est égal au nombre de captures produites
  (règle n° 11 — quatre défauts majeurs ont été trouvés à l'œil que des suites
  entièrement vertes n'avaient pas vus).

### SC5 — Les noms de mer sont un proxy hérité, déclaré avant mesure

- `noms_attestes_lus` est le nombre d'entrées de la copie
  `legacy_game_data/sea_zones.json`, lu du fichier.
- `zones_nommees` est mesuré avec `zones_mer_denombrees` pour dénominateur ;
  sous la règle de D5, l'attribution au plus proche ancrage nomme toute zone.
- `noms_distincts_employes` et `noms_attestes_non_employes` sont des faits
  mesurés, avec `noms_attestes_lus` pour dénominateur. **Aucun plancher n'est
  exigé** : rien n'oblige les 14 noms à être employés, et l'algorithme n'est en
  aucun cas ajusté pour y parvenir.
- `README.md` déclare, **avant** toute citation de compteur mesuré : que les
  noms viennent de données héritées du jeu ; que ce n'est pas une source
  savante ni une reconstitution d'époque ; que le tableau d'identifiants
  riverains hérités n'est qu'un **proxy de localisation de nom**, jamais une
  clé spatiale ; et quelle règle d'attribution et quel départage d'égalité sont
  employés (D5). Une justification écrite après la mesure est une calibration
  déguisée.

### SC6 — ADR-0003 dans les artefacts, pas seulement dans la prose

- `occurrences_province_dans_artefacts_g4` vaut **0**, mesuré par balayage de la
  sous-chaîne `province` dans les six artefacts G4 exportés hors le fichier de
  divergence (D10), avec le nombre de fichiers balayés pour dénominateur.
- `occurrences_province_dans_divergence` est **strictement positif** : la
  comparaison a réellement eu lieu, et elle vit exclusivement là. Un zéro ici
  signifierait que le contrôle de frontière n'a rien eu à retenir.
- `lecteurs_du_fichier_divergence_hors_qa` vaut **0** : aucun fichier de code
  sous `pipeline/geo/` autre que la preuve QA ne lit
  `adjacency_divergence_g4.json`, mesuré par balayage des fichiers de code du
  répertoire, avec leur nombre pour dénominateur.
- Le fichier de divergence porte `"qa_only": true` dans son propre contenu, et
  `README.md` comme `deliverables/manifest.json` le décrivent explicitement
  comme comparaison QA unique, jamais autorité spatiale.
- `aretes_heritees_confirmees`, `aretes_heritees_contredites` et
  `aretes_heritees_manquantes` sont rapportées avec le nombre d'arêtes héritées
  lues pour dénominateur. Ces trois nombres sont des constats, pas des
  objectifs : aucun seuil n'est exigé sur eux, et le graphe dérivé n'est en
  aucun cas ajusté pour ressembler davantage au graphe hérité.

### SC7 — Déterminisme sur deux passes, huit contrôles verts, chacun mordant

- `paires_sha_determinisme_egales` est **égal** au nombre total de paires du
  bloc `determinism.sha256` de `logs/v1_050_qa.json`, ce total étant strictement
  positif, et aucune empreinte n'étant vide.
- `controles_g4_verts` vaut **8** sur 8 entrées du tableau `checks`.
- `controles_g4_avec_preuve_rouge_non_vide` vaut **8** sur 8 : chaque contrôle a
  été vu rougir (D12), `G4-B` par le cas naturel.
- `code_sortie_run_proof_g4` vaut **0**.
- `empreinte_terre_g4_egale_entree_g3` : deux branches, **une seule** est à
  satisfaire (amendée par `amendment-001-escalade-empreinte-g3.md`). Aucune
  valeur hexadécimale n'est recopiée nulle part, dans l'une comme dans l'autre.

  - **Branche égale (monde unique).** Le compteur vaut **1** : l'empreinte du
    littoral corrigé employé par G4 est égale à celle que `MANIFEST_g3.json`
    déclare comme entrée des cellules, les deux étant lues à l'exécution (D2).
  - **Branche escalade (chaîne amont incohérente).** Le compteur vaut **0**, et
    toutes les exigences suivantes valent **ensemble** : ce `0` est une **mesure**
    et jamais la sentinelle `-1` ; la dérogation d'escalade de la table des
    dérogations est invoquée, avec sa commande rejouable et son message d'erreur,
    l'un et l'autre dépourvus de toute valeur hexadécimale, le message nommant
    ses **deux** sources ; `empreinte_terre_g4_egale_sortie_declaree_g2b` vaut
    **1**, ce qui situe l'écart en amont du lot ; aucun artefact G3 n'est
    réécrit, régénéré ni retouché (D16) ; la comparaison n'est **pas** retargetée
    vers `MANIFEST_g2b.json` pour faire dire 1 au compteur ; le constat est
    **ouvert** — nommé dans le journal de preuve, dans
    `deliverables/generator-log.md` et dans `pipeline/geo/README.md`.

    Cette branche **satisfait SC7 pour ce lot**. Elle ne vaut pas égalité : elle
    n'autorise en aucun cas à écrire, dans un document ou dans un artefact, que
    la mer et les cellules décrivent le même monde. La réparation de la
    provenance de G3 est un brief ultérieur dédié (non-objectif n° 18).
- `zones_hors_bornes_intention` et `zones_exemptees_bassin_entier` sont mesurées
  et rapportées (D13). Si la première n'est pas nulle, le journal et
  `README.md` l'inscrivent comme constat ouvert, sans déplacer aucune borne.
- `constantes_g4_inchangees` vaut 1 : `pipeline/geo/constants.py` n'a aucune
  modification, prouvé par la sortie de `git status --porcelain` sur ce fichier.

### SC8 — Le contrat du crochet existant est réellement satisfait

Depuis `pipeline/geo/` :

```
../../.venv/bin/python pipeline.py --source adjacency
```

La commande doit se terminer sans erreur et afficher la ligne de résumé G4
(projection, nombre de zones, nombre d'arêtes par type, cellules littorales,
atteignabilité). Sa sortie réelle est recopiée dans le journal.
`pipeline/geo/pipeline.py` reste **inchangé** :
`fichiers_partages_modifies` vaut 0, mesuré par `git status --porcelain` sur
`pipeline.py`, `qa/checks.py`, `constants.py`, `io_util.py`, `projection.py`,
`steps/02_coastline.py`, `steps/02b_corrections_1400.py`, `steps/03_cells.py`.

### SC9 — Preuves committées, re-vérifiables depuis un clone, et README sans sur-revendication

- `fichiers_preuve_suivis_par_git` est **égal** au nombre de fichiers de preuve
  déclarés sous `pipeline/geo/` (artefacts, journaux, registre, captures de
  D9), vérifié par `git ls-files` (D15).
- `README.md` est mis à jour : G4 — zones de mer et adjacence typée — est
  désormais livré ; **restent non livrés** les fleuves (`05`, `05b`, `05c`), le
  relief et le climat (`06`), les ressources, les villes (`07`), la propriété
  (`08`), le LOD (`09`), les textures d'identifiants (`10`) et la QA de chaîne
  complète (`qa/run_all.py`, `qa/crs_coherence.py`). Aucune sur-revendication :
  ce lot ne livre pas E1 entier, seulement son premier lot.
- Un instantané du README **avant** édition est committé sous
  `deliverables/pre-edit/pipeline-geo-README.md.orig`, déclaré en couple
  `must_differ_from` avec le README publié (D9).
- Le README reste **descriptif** : il dit ce qui existe et ce que cela lit ; il
  n'adresse aucune instruction à un agent — le brief est la seule instruction,
  et `harness/tests/test_single_source_of_instruction.py` le vérifie.

### SC10 — Mesure rejouable, manifeste complet, suites non régressées, registre de coût

- Un script committé sous
  `harness/queue/briefs/019-geo-adjacence-g4/deliverables/measure_g4_019.py`,
  exécuté depuis la racine du dépôt :

```
.venv/bin/python harness/queue/briefs/019-geo-adjacence-g4/deliverables/measure_g4_019.py
```

  Il imprime **chaque compteur du tableau ci-dessous avec son dénominateur**,
  en lisant les artefacts et les constantes — jamais une valeur recopiée à la
  main. Un compteur sans dénominateur imprimé est irrecevable.
- `deliverables/manifest.json` déclare tous les fichiers (y compris ceux hors
  du dossier du brief, en chemins relatifs), les trois couples
  `must_differ_from`, chaque compteur avec un `sample_size` réel — non nul et
  différent de la sentinelle — et les dérogations éventuelles avec leur commande
  et leur erreur.
- La suite du harnais reste verte (aucune régression ; ce lot n'y touche pas) :

```
.venv/bin/python -m pytest harness/tests/ -q
```

  `tests_harness_passed_019` est rapporté avec le nombre de tests collectés pour
  dénominateur. Les `SKIP` propres à Linux (tests Unity) sont acceptés et
  déclarés. Les sorties réelles sont recopiées dans
  `deliverables/generator-log.md`.
- Registre de coût, une ligne :

```
.venv/bin/python harness/backends/ledger.py append --backend cursor \
  --brief harness/queue/briefs/019-geo-adjacence-g4 \
  --event generator-run
```

  Aucun `--audit-id` n'est requis : ce brief naît de la feuille de route, pas
  d'un audit converti.

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. Livrer le relief, le climat, les ressources ou les fleuves — ce sont les lots
   suivants du jalon E1. Aucun fichier `05*`, `06*`, `07*`, `08*`, `09*`, `10*`
   n'est créé ni copié.
2. Régénérer, recalculer ou modifier la maille des cellules : les artefacts G3
   committés sont en **lecture seule**. Aucun paramètre `G3_*` n'est touché.
3. Modifier une seule valeur de `pipeline/geo/constants.py`, dans quelque sens
   que ce soit. Une borne inatteignable s'escalade (D13, Waivers).
4. Modifier `pipeline/geo/qa/checks.py` : la barre qualité n'est ni élargie, ni
   assouplie, ni contournée. Un contrôle rendu vert en modifiant le contrôle
   n'est pas un contrôle.
5. Modifier `pipeline/geo/pipeline.py` (D14).
6. Prétendre à une égalité d'empreinte avec une arborescence VictoriaProject
   absente de cette machine, ni invoquer son absence comme dérogation : la
   reconstruction est déjà tranchée.
7. Laisser `artifacts/adjacency_g4.json` — ou tout artefact G4 autre que le
   fichier de divergence — porter la moindre occurrence de la sous-chaîne
   `province`. C'est une frontière dure, pas une préférence de style.
8. Traiter le fichier de divergence comme une autorité spatiale, le faire lire
   par un autre code, ou fonder sur lui une appartenance.
9. Inventer, traduire ou compléter un nom de mer ; ni présenter les noms
   hérités comme une source savante ou des frontières historiques.
10. Ouvrir une brèche dans le trait de côte pour rendre le Zuiderzee
    atteignable : la continuité est **déclarée**, la géométrie n'est pas
    retouchée.
11. Rendre `G4-B` vert par une mutation synthétique : son cas rouge est le cas
    naturel (liens coupés).
12. Rapporter un compteur depuis un monde vide, une liste vide ou un
    échantillon nul. Un zéro **mesuré** (par exemple les occurrences de
    `province`) est légitime et doit être distingué d'un « non calculé », dont
    la sentinelle est `-1` (règle n° 8).
13. Écrire dans `sim/`, `unity/` (le fichier de noms de mer est lu, jamais
    écrit), `harness/*.py`, `harness/pipeline/`, `architecture/`, `docs/adr/`,
    `ROADMAP.md`, `HANDOFF.md`, `VISION.md`, `.github/`.
14. Retoucher les archives des briefs 001 à 018, ni réouvrir le brief 007 (son
    lot 007a a déjà un verdict).
15. Employer l'alias nu de l'interpréteur dans une commande, ni
    `.venv/Scripts/python.exe` (chemin Windows) : sur cette machine
    l'interpréteur est `.venv/bin/python` (règle n° 1).
16. Recopier une valeur hexadécimale d'empreinte dans un test, un document ou
    un commentaire (règle n° 12). Les empreintes se comparent à l'exécution.
17. Committer, pousser, créer ou changer de branche. L'orchestrateur seul
    dépose.
18. Réparer la provenance de G3, ni trancher lequel des deux artefacts committés
    est faux quand l'empreinte du littoral diffère de ce que `MANIFEST_g3.json`
    déclare. C'est un **brief ultérieur dédié**, hors 019 : il touchera des
    cellules déjà consommées par `sim/`, ce que ce lot n'a pas le droit de faire.
    Ici, l'écart se mesure, s'escalade et s'inscrit comme constat ouvert
    (SC7, branche escalade) — il ne se répare pas.

---

## Required Counters

Un compteur sans source d'échantillon déclarée est irrecevable : la porte
mécanique refuse tout compteur dont l'échantillon est nul ou non calculé
(`no_empty_sample_pass`). Tous sont produits par
`deliverables/measure_g4_019.py`, qui les dérive des artefacts et des
constantes.

| nom | source de l'échantillon | dénominateur |
|---|---|---|
| `copie_sea_zones_identique` | empreintes calculées à l'exécution du fichier Unity de noms de mer et de sa copie sous `legacy_game_data/` | 1 comparaison ; doit valoir 1 |
| `noms_attestes_lus` | entrées du tableau de zones de la copie `legacy_game_data/sea_zones.json` | longueur de ce même tableau, lue du fichier |
| `cellules_lues_g3` | cellules lues de `artifacts/cells_g3.json` | `cell_count` lu de `artifacts/stats_g3.json` (doit être égal) |
| `zones_mer_denombrees` | entrées de `artifacts/sea_zones_g4.json` | `[SEA_ZONE_COUNT_MIN, SEA_ZONE_COUNT_MAX]` lues de `constants.py` ; doit s'y situer |
| `composantes_mer_totales` | composantes d'eau retenues comme mer de 1400 (D2) | leur nombre ; doit être > 0 |
| `composantes_mer_couvertes` | composantes portant au moins une zone | `composantes_mer_totales` (doit être égal) |
| `plans_eau_exclus_lacs` | plans d'eau enclavés non reclassés en mer par les corrections de 1400 | tous les plans d'eau examinés au-dessus de la tolérance de découpe, soit les plans exclus **plus** les composantes retenues comme mer — un ensemble qui contient donc le compteur ; fait mesuré, sans seuil |
| `collisions_id_mer_terre` | `zone_id` confrontés à l'ensemble des `cell_id` de `cells_g3.json` | `zones_mer_denombrees` ; **doit valoir 0**, mesure réelle |
| `ids_mer_sous_la_base` | `zone_id` inférieurs à `SEA_ZONE_ID_BASE` lu de `constants.py` | `zones_mer_denombrees` ; **doit valoir 0** |
| `aretes_totales` | arêtes de `artifacts/adjacency_g4.json` | leur nombre ; doit être > 0 |
| `aretes_terre_terre` | arêtes de type `land-land` | `aretes_totales` ; doit être > 0 |
| `aretes_terre_mer` | arêtes de type `land-sea` | `aretes_totales` ; doit être > 0 |
| `aretes_mer_mer` | arêtes de type `sea-sea` | `aretes_totales` ; doit être > 0 |
| `aretes_detroit` | arêtes de type `strait` | `aretes_totales` ; doit être > 0 |
| `kinds_non_vides` | les quatre types de `stats_g4.json`'s `by_kind` à compte > 0 | 4 (doit valoir 4) |
| `aretes_avec_id_mer_placeholder` | arêtes exportées portant encore l'identifiant fourre-tout de mer de G3 | `aretes_totales` ; **doit valoir 0** |
| `cellules_littorales` | cellules portant au moins une arête `land-sea` | `cellules_lues_g3` ; fait mesuré, > 0 |
| `seuil_detroit_m` | `G4_STRAIT_MAX_WIDTH_M` lu de `constants.py` | 1 valeur lue ; jamais un littéral du code |
| `ecart_min_detroit_m` | largeurs mesurées des arêtes `strait` | `aretes_detroit` ; doit être ≤ `seuil_detroit_m` |
| `detroits_entre_masses_differentes` | arêtes `strait` reliant deux composantes connexes distinctes de la terre | `aretes_detroit` ; doit être > 0 |
| `liens_topologiques_declares_appliques` | liens appliqués dans `artifacts/topology_links_g4.json` | corrections `declare_topology_link` lues de `data/corrections_1400.json` (doit être égal) |
| `bassins_enfermes_total` | composantes d'eau ne touchant pas le bord de la fenêtre (D2) | `composantes_mer_totales` ; fait mesuré, > 0 |
| `bassins_enfermes_non_atteignables_liens_actifs` | passe liens actifs | `bassins_enfermes_total` ; **doit valoir 0** |
| `bassins_enfermes_non_atteignables_liens_inactifs` | passe liens coupés (rouge naturel de `G4-B`) | `bassins_enfermes_total` ; doit être > 0 |
| `zone_cible_nom_atteste_existe` | zone de la mer extérieure portant le nom attesté demandé par la correction | 1 vérification ; doit valoir 1 |
| `zones_nommees` | zones portant un nom hérité attribué (D5) | `zones_mer_denombrees` |
| `noms_distincts_employes` | noms hérités effectivement portés par au moins une zone | `noms_attestes_lus` ; fait mesuré, sans plancher |
| `noms_attestes_non_employes` | noms hérités portés par aucune zone | `noms_attestes_lus` ; fait mesuré, peut valoir 0 |
| `occurrences_province_dans_artefacts_g4` | balayage de la sous-chaîne `province` dans les six artefacts G4 hors fichier de divergence | nombre de fichiers balayés ; **doit valoir 0** |
| `occurrences_province_dans_divergence` | même balayage sur `artifacts/adjacency_divergence_g4.json` seul | 1 fichier ; doit être > 0 |
| `lecteurs_du_fichier_divergence_hors_qa` | fichiers de code sous `pipeline/geo/` mentionnant le fichier de divergence, hors la preuve QA | nombre de fichiers de code balayés ; **doit valoir 0** |
| `aretes_heritees_confirmees` | arêtes du graphe hérité retrouvées par le graphe dérivé | arêtes héritées lues de `legacy_game_data/province_adjacency.json` ; constat, sans seuil |
| `aretes_heritees_contredites` | arêtes héritées que le graphe dérivé nie | même dénominateur ; constat, sans seuil |
| `aretes_heritees_manquantes` | arêtes héritées sans correspondance dérivable | même dénominateur ; constat, sans seuil |
| `paires_sha_determinisme_egales` | bloc `determinism.sha256` de `logs/v1_050_qa.json` | nombre total de paires (doit être égal, total > 0, aucune empreinte vide) |
| `controles_g4_verts` | tableau `checks` de `logs/v1_050_qa.json` | 8 entrées (doit valoir 8) |
| `controles_g4_avec_preuve_rouge_non_vide` | champ `red_proof` de chaque entrée du même tableau | 8 entrées (doit valoir 8) |
| `code_sortie_run_proof_g4` | code de sortie de `tests/run_proof_g4.py` | 1 exécution ; **doit valoir 0** |
| `empreinte_terre_g4_egale_entree_g3` | empreinte du littoral corrigé employé par G4 vs `MANIFEST_g3.json`'s `inputs.coastline_1400`, les deux lues à l'exécution | 1 comparaison ; vaut **1** si égal, **0** si escalade documentée (SC7, dérogation d'escalade) ; ce 0 est une mesure, jamais la sentinelle |
| `empreinte_terre_g4_egale_sortie_declaree_g2b` | même empreinte du littoral relu vs la sortie que `MANIFEST_g2b.json` déclare pour `coastline_1400.json`, les deux lues à l'exécution | 1 comparaison ; **doit valoir 1** si la branche escalade de SC7 est invoquée |
| `zones_hors_bornes_intention` | zones hors `G4_SEA_AREA_FLOOR_KM2` / `G4_SEA_AREA_CEIL_KM2` / `G4_SEA_COMPACTNESS_MIN` lues de `constants.py`, exemptions de D13 appliquées | `zones_mer_denombrees` ; fait mesuré, non bloquant, inscrit comme constat ouvert s'il n'est pas nul |
| `zones_exemptees_bassin_entier` | zones constituant à elles seules un bassin enfermé entier (exemption déclarée en D13) | `zones_mer_denombrees` ; fait mesuré |
| `captures_regardees_et_decrites` | captures de D9 dont le journal décrit ce qu'elles montrent | captures produites (doit être égal) |
| `constantes_g4_inchangees` | `git status --porcelain` sur `pipeline/geo/constants.py` | 1 fichier vérifié ; doit valoir 1 (aucune modification) |
| `fichiers_partages_modifies` | `git status --porcelain` sur les huit fichiers partagés listés en SC8 | 8 fichiers vérifiés ; **doit valoir 0** |
| `fichiers_preuve_suivis_par_git` | `git ls-files` croisé avec la liste déclarée des preuves sous `pipeline/geo/` | nombre de preuves déclarées (doit être égal) |
| `tests_harness_passed_019` | tests `PASSED` de `harness/tests/` | tests collectés dans `harness/tests/` (les `SKIP` Linux sont acceptés et déclarés) |

---

## Acceptable Waivers (si une impossibilité est invoquée)

Une impossibilité s'éprouve avant d'être invoquée : une commande et le message
d'erreur qu'elle produit, sinon ce n'est pas un constat mais un abandon (règle
durement acquise n° 9).

| affirmation d'impossibilité | commande exigée | erreur attendue |
|---|---|---|
| « la pile scientifique n'est pas installée sur cette machine » | `.venv/bin/python -c "import shapely, geopandas, pyproj; print('ok')"` depuis la racine | le message d'erreur exact (`ModuleNotFoundError` nommant le module) ; si invoqué, **aucune** condition de succès n'est excusée : sans exécution il n'y a pas de mesure, et le lot s'arrête sur ce constat |
| « les artefacts de cellules ne sont pas lisibles » | `.venv/bin/python -c "import json; json.load(open('pipeline/geo/artifacts/cells_g3.json'))"` depuis la racine | le message d'erreur exact (`FileNotFoundError` ou équivalent) |
| « le littoral corrigé de 1400 ne se régénère pas » | depuis `pipeline/geo/` : `../../.venv/bin/python tests/run_proof_g2b.py` | la sortie réelle complète montrant l'échec, code de sortie inclus |
| « l'empreinte du littoral corrigé de 1400 relu par G4 diffère de l'entrée que `MANIFEST_g3.json` déclare » — **dérogation d'escalade**, ouverte par `amendment-001-escalade-empreinte-g3.md` pour tenir la promesse de D2 | depuis la racine : `.venv/bin/python harness/queue/briefs/019-geo-adjacence-g4/deliverables/check_provenance_coastline_019.py` (contrat fixé sous la table) | le message d'écart que la commande imprime, **nommant ses deux sources et dépourvu de toute valeur hexadécimale** — de la forme « écart entre `artifacts/coastline_1400.json` calculé et `MANIFEST_g3.json` `inputs.coastline_1400` » — avec le code de sortie 1. Si invoquée : `empreinte_terre_g4_egale_entree_g3` reste le `0` **mesuré** (jamais 1, jamais la sentinelle `-1`), `empreinte_terre_g4_egale_sortie_declaree_g2b` vaut 1, aucun artefact G3 n'est réécrit ni régénéré, la comparaison n'est pas retargetée vers `MANIFEST_g2b.json`, et le constat est **ouvert** dans le journal de preuve, dans `deliverables/generator-log.md` et dans `pipeline/geo/README.md`. Ce n'est **pas** un succès d'égalité : c'est l'escalade que D2 annonçait |
| « le fichier Unity de noms de mer est introuvable » | `.venv/bin/python -c "import pathlib; print(pathlib.Path('unity/game_unity/Assets/StreamingAssets/data/sea_zones.json').read_bytes()[:1])"` depuis la racine | le message d'erreur exact (`FileNotFoundError`) |
| « le nombre de zones de mer ne peut pas tomber dans la fourchette déclarée avec les paramètres de semis lus » | depuis `pipeline/geo/` : `../../.venv/bin/python tests/run_proof_g4.py` | la sortie réelle et la ligne de `logs/v1_050_qa.json` nommant le compte hors fourchette, **plus** le relevé des tentatives faites à paramètres inchangés. Si invoqué : la borne n'est pas déplacée, `constants.py` n'est pas modifié, et le constat est **escaladé au Planificateur** — jamais auto-accordé comme un succès |
| « un bassin enfermé reste injoignable malgré les liens déclarés » | même commande | la sortie de `G4-B` nommant le bassin injoignable, plus le contenu de `logs/v1_050_g4b_links_on.txt`. Si invoqué : le contrôle n'est pas relâché, la géométrie du littoral n'est pas retouchée, et le constat est escaladé |
| « le budget d'exécution n'est pas mesurable sur cette machine » | `.venv/bin/python harness/budget.py status --brief harness/queue/briefs/019-geo-adjacence-g4` | la sortie contient la chaîne `UNMEASURABLE` |

**Contrat de la commande d'escalade.** Le Générateur écrit le script ; le brief
fixe son comportement, et rien de plus. Un seul appel bloquant, depuis la racine :

```py
.venv/bin/python harness/queue/briefs/019-geo-adjacence-g4/deliverables/check_provenance_coastline_019.py
```

- Il **lit** trois choses, en lecture seule : le
  `pipeline/geo/artifacts/coastline_1400.json` vivant (celui que la chaîne vient
  de régénérer), l'entrée `inputs.coastline_1400` déclarée par
  `pipeline/geo/artifacts/MANIFEST_g3.json`, et la sortie que
  `pipeline/geo/artifacts/MANIFEST_g2b.json` déclare pour ce même fichier.
- Il **calcule** l'empreinte du fichier vivant à l'exécution et la compare aux
  deux valeurs déclarées. Il n'imprime, n'écrit et ne consigne **aucune** valeur
  hexadécimale, nulle part : seulement des noms de source et des résultats de
  comparaison (règles n° 9 et n° 12 tenues ensemble).
- **Si les deux empreintes sont égales** : il l'énonce en nommant ses deux
  sources et sort avec le code **0**.
- **Sinon** : il imprime le message d'écart nommant ses deux sources, puis une
  seconde ligne disant si le fichier vivant correspond bien à la sortie que
  `MANIFEST_g2b.json` déclare — la source du compteur
  `empreinte_terre_g4_egale_sortie_declaree_g2b` — et sort avec le code **1**.
- **Si le fichier vivant ou `MANIFEST_g2b.json` est absent** (les deux sont
  ignorés par git, donc absents d'un clone frais) : il le dit, nomme la commande
  qui les régénère (`../../.venv/bin/python tests/run_proof_g2b.py` depuis
  `pipeline/geo/`) et sort avec le code **2** — jamais 1, pour qu'une absence ne
  soit jamais confondue avec un écart mesuré.

C'est cette sortie-là, et non celle d'une assertion brute, qui est consignée dans
le champ d'erreur de la dérogation du manifeste.

Aucune autre dérogation n'est recevable. En particulier :

- « l'arborescence `sandbox/geo/` de VictoriaProject est absente » **n'est pas
  une dérogation** : la reconstruction est déjà tranchée, et aucune égalité
  d'empreinte contre un chemin Windows n'est exigée par ce brief.
- « l'empreinte du fichier de noms de mer citée par le brief 007 ne correspond
  plus » **n'est pas une dérogation** : ce brief n'exige aucune valeur citée,
  seulement l'égalité octet pour octet avec le fichier Unity présent dans le
  dépôt, calculée à l'exécution.
- « un nom hérité n'est employé par aucune zone » **n'est pas une dérogation** :
  c'est un fait mesuré attendu (D5).
- « les bornes d'intention de surface ou de compacité ne sont pas toutes
  respectées » **n'est pas une dérogation** : c'est un constat ouvert à
  inscrire, non bloquant, et surtout pas un motif de recalibration (D13).
- « l'empreinte du littoral diffère de ce que G3 déclare » n'est recevable que
  sous la **forme exacte** de la ligne d'escalade ci-dessus : la commande, son
  message sans hexadécimal, les deux compteurs et le constat ouvert. Accordée
  autrement — ou muée en égalité en changeant de cible — ce n'est pas une
  dérogation, c'est un maquillage.

---

## Execution Contract

### Interpréteur et commandes

Machine : Linux (Cursor Cloud). L'interpréteur est **`.venv/bin/python`**,
jamais l'alias nu (règle n° 1), jamais `.venv/Scripts/python.exe`. Les preuves
géographiques se lancent **depuis `pipeline/geo/`** avec
`../../.venv/bin/python`, conformément à `AGENTS.md`.

Aucune étape Unity dans ce lot : `unity/run-unity.ps1` ne s'applique pas, et
`unity/` n'est touché qu'en lecture d'un fichier de données.

Les scripts de preuve reconstruisent de la géométrie réelle : chaque exécution
peut prendre de quelques dizaines de secondes à quelques minutes. Chaque
exécution est **un seul appel bloquant**. Ne jamais lancer en arrière-plan puis
relire le journal toutes les trente secondes : chaque relecture est un appel
d'outil qui renvoie tout le contexte accumulé, et c'est ainsi qu'un lot
précédent a dépensé 586 appels à relire un seul fichier.

### Estimation d'appels d'outils

**Estimation : 140 appels.** Ancres réelles : un brief d'ADR a coûté environ
108 appels ; le portage complet du jeu Unity, environ 1 119 ; le lot 007a,
réparation comprise, environ 135. Le présent lot est du même ordre que 007a :
un module géométrique neuf, ses cas rouges, son script de preuve, plusieurs
exécutions à itérer contre huit contrôles, un README, un manifeste, un script
de mesure, un journal — mais un seul sous-système et un seul thème.

Vérification préalable, **avant tout travail de fond** :

```
.venv/bin/python harness/budget.py split-check \
  --brief harness/queue/briefs/019-geo-adjacence-g4 \
  --estimated-calls 140
```

Verdict attendu : sous le seuil mécanique de 150. Les signaux imprimés à titre
indicatif ne déclenchent rien : le Planificateur a déjà jugé qu'il n'y a ici
qu'un sous-système et qu'un thème causal.

Plafond dur : 160 appels. **Point de contrôle obligatoire à 130 appels** :

```
.venv/bin/python harness/budget.py checkpoint \
  --brief harness/queue/briefs/019-geo-adjacence-g4
```

Le point de contrôle nomme ce qui est vert, ce qui reste, et l'état du dépôt —
de sorte qu'une session neuve reprenne depuis les fichiers du dépôt et ce
document, **jamais** depuis une transcription antérieure.

### Preuves committées et re-vérifiables

Tout fichier nommé dans `deliverables/manifest.json` doit être suivi par git.
`pipeline/geo/.gitignore` exclut `artifacts/`, `logs/`, `capture/` : les preuves
de ce lot sont donc ajoutées par `git add -f`, comme celles des briefs 002 et
007a (D15). Le suivi est **prouvé** par `git ls-files` et compté, parce que la
porte mécanique ne vérifie pas le suivi des chemins qui sortent du dossier du
brief.

### Deliverables obligatoires

Le dossier `harness/queue/briefs/019-geo-adjacence-g4/deliverables/` (à créer
par le Générateur) doit contenir :

- `manifest.json` — tous les fichiers déclarés, les trois couples
  `must_differ_from`, tous les compteurs avec un `sample_size` réel, les
  dérogations éventuelles avec commande et erreur ;
- `measure_g4_019.py` — script rejouable imprimant chaque compteur avec son
  dénominateur (SC10) ;
- `check_provenance_coastline_019.py` — la commande de la dérogation d'escalade,
  exigée **seulement** si la branche escalade de SC7 est invoquée ; son
  comportement est fixé par le contrat de la table des dérogations ;
- `pre-edit/pipeline-geo-README.md.orig` — instantané du README avant édition ;
- `generator-log.md` — journal d'exécution en **français clair** : ce qui a été
  fait, pourquoi, ce qui reste ; les sorties réelles des commandes de SC1, SC8
  et SC10 ; la description de ce que montrent les trois captures (règle n° 11) ;
  tout constat ouvert (D13) énoncé sans maquillage.

### Interdictions pour le Générateur

- **Ne pas committer. Ne pas pousser. Ne créer ni changer de branche.**
  L'orchestrateur seul dépose.
- Ne pas modifier `brief.md`, `eval-rubric.md`, ni écrire `verdict.md`.
- Ne pas modifier `constants.py`, `qa/checks.py`, `pipeline.py`, ni aucun
  fichier de la liste interdite de D16.
- Ne pas rendre un contrôle vert en modifiant le contrôle.
- Ne pas recopier de valeur hexadécimale d'empreinte (règle n° 12).
- Ne pas rapporter la sentinelle `-1` pour un compteur calculé, ni `0` pour un
  compteur qui ne l'a pas été (règle n° 8).
- Ne pas prononcer la recevabilité de son propre travail.

### Fin de lot

La porte mécanique doit répondre `ACCEPT` :

```
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/019-geo-adjacence-g4
```

La preuve géographique doit sortir avec le code 0 et la suite du harnais rester
verte :

```
.venv/bin/python -m pytest harness/tests/ -q
```

Les sorties réelles sont recopiées dans le journal.

**Celui qui produit ne prononce pas la recevabilité.**
