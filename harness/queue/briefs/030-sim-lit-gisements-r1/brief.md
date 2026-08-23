# Brief 030 : sim/ lit les gisements déclarés (R1) — le monde connaît ce que sa terre donne

**Authored**: 2026-08-23T09:45:00Z
**Author**: forge-planificateur
**Statut**: **BLOQUÉ TANT QUE le lot 026 n'est pas fusionné** — voir « Bloqué
tant que » ci-dessous. Aucun arbitrage nouveau n'est requis.
**Classement de risque**: R1 — produit borné

> **Note de transparence (contrat du Planificateur) :** le rôle signataire est
> le rôle natif du harnais `forge-planificateur`. L'acteur réel est Fable
> (Claude), en session Planificateur, saisi pour préparer la file Hermes après
> la fusion de #126. Cette session n'exécute pas le lot, ne commite pas le
> code produit, ne lance pas Cursor, ne rédige aucun verdict et ne fusionne
> rien.

> **Pourquoi R1, et pas R2.** Ce lot fait lire à `sim/` un artefact
> géographique déjà validé par sa propre porte de qualité (les contrôles
> `R1-*` du lot 026), en suivant à la lettre le patron de jointure déjà
> fusionné pour les déterminants climatiques C1 (brief 027, D7). Il n'invente
> aucun invariant nouveau : `cell_id` seule clé spatiale (ADR-0003), absence
> déclarable (règle n° 10), sentinelle `-1` (règle n° 8), déterminisme par
> empreinte. Il ne touche ni au tick, ni à la porte du harnais, ni à une
> masse DEM.

À partir d'ici, **ce `brief.md` est la SEULE instruction** (voir `CLAUDE.md`
› Single Source of Instruction).

---

## Bloqué tant que

Ce lot dépend d'une seule chose : **le lot 026 est fusionné**, c'est-à-dire
que les artefacts de gisements existent dans `master` et sont suivis par git.
Le Générateur le constate **avant sa première action**, par des commandes et
jamais par un souvenir (SC0) :

```
git ls-files pipeline/geo/artifacts/cells_resources_r1.json
git ls-files pipeline/geo/artifacts/resources_1400_r1.json
```

Si l'une des deux sorties est **vide**, le lot s'arrête ici, ne produit aucun
fichier, et le signale. Ce n'est pas un rejet du travail : c'est un lot lancé
avant son heure. Le Générateur ne crée **jamais** ces artefacts lui-même —
les produire est le travail du lot 026, et un producteur ne s'accorde pas sa
propre autorisation.

---

## Provenance

- **ADR-0016** : `sim/` est le produit vivant, sans Unity. Les couches du
  monde s'écrivent dans `sim/`.
- **ADR-0003** : `cell_id` est la seule clé spatiale ; toute jointure se fait
  par `cell_id`, jamais par proximité ni par un identifiant concurrent.
- **Brief 026** (`harness/queue/briefs/026-geo-gisements-1400-r1/brief.md`)
  et son amendement 001 : ce que la couche R1 contient — la présence, la
  nature et la classe qualitative de richesse d'un gisement, **jamais** une
  quantité. Le vocabulaire (gisement, nature, classe, certitude) est défini
  là-bas ; ce brief ne le recopie pas.
- **Brief 027** (`harness/queue/briefs/027-sim-snapshot-cellulaire-v0a/brief.md`) :
  le schéma de photographie `v0a-1`, fermé, et son D2 qui prévoit une
  révision de contrat par changement de suffixe. Ce lot est cette révision :
  `v0a-2`.
- **Amendement 001 du brief 026, §5, point 1** : « comment `sim/` lira la
  classe » — c'est-à-dire comment le tick s'en servira un jour — est
  **explicitement laissé ouvert** par le propriétaire. Ce lot n'y répond
  pas : il fait **lire** la couche, il ne la fait **pas jouer**. Aucun
  comportement du tick ne change (D6). La question est posée au propriétaire
  dans `hermes/propositions/PROPOSITION-20260823-plan-couche-1.md`
  (question `D3`) ; tant qu'elle n'est pas tranchée, faire peser un gisement
  sur la production serait décider à sa place.

Ce lot est le deuxième maillon de l'ordre causal du monde : la donnée existe
(026), **`sim/` la lit sans l'inventer et refuse si elle manque** (ce lot),
le tick s'en servira par une chaîne causale (lot futur, après décision
propriétaire), la photographie peut la montrer (le viewer, lot 031).

---

## World-Terms Requirement

**Chaîne causale.**

Il y a du sel sous Wieliczka et il n'y en a pas sous Paris. Le lot 026
déclare ce fait dans la géographie. Mais un fait que le monde simulé ne lit
pas n'existe pas pour lui : aujourd'hui, la photographie du monde dit
`resources_r1: absent`, et aucune cellule ne sait ce que sa terre donne.

Ce lot fait entrer le fait dans le monde : chaque cellule photographiée porte
la liste — éventuellement vide, et un vide est une **absence mesurée** — des
gisements que sa terre contient, avec leur nature et leur classe. Rien
d'autre. Aucune conséquence n'est codée : pas de rendement, pas de flux
d'extraction, pas de bonus de production. La conséquence (« on extrait là où
c'est, ce qui est extrait doit être transporté ») viendra d'un lot futur, en
termes de monde, quand le propriétaire aura tranché comment le tick a le
droit de s'en servir.

**Interdit** : toute règle du type « si gisement alors +N% de production ».
Ce lot écrit ce que la terre contient ; il n'écrit pas ce qu'on en tire.

---

## Vocabulaire (expliqué une fois)

- **jointure par `cell_id`** : rattacher à chaque cellule de la photographie
  les données R1 qui portent le même identifiant de cellule. On copie, on ne
  recalcule pas et on ne déduit rien.
- **couche consommée (`present`)** : la photographie porte les données de la
  couche, cellule par cellule, avec le chemin et l'empreinte de la source.
- **couche non consommée (`not_consumed`)** : le fichier existe sur le
  disque mais la photographie **refuse** de le croire (couverture incomplète,
  forme invalide). Rien n'en est publié dans les cellules.
- **couche absente (`absent`)** : le fichier n'existe pas. L'absence est
  déclarée, jamais comblée par invention (règle n° 10).
- **liste vide vs `null`** : une cellule avec `resources: []` a été regardée
  et sa terre ne contient aucun gisement déclaré — c'est une mesure. Une
  cellule avec `resources: null` appartient à une photographie dont la
  couche n'est pas consommée — c'est une absence déclarée. Les deux ne se
  confondent jamais.

---

## Ce qui existe déjà, et que ce lot lit sans le refaire

Vérifié sur le dépôt au moment de l'écriture. Les nombres sont des
**constats de contexte**, jamais des seuils (règle n° 2).

- `sim/snapshot_export.py` : connaît déjà le chemin
  `pipeline/geo/artifacts/cells_resources_r1.json` (`_R1_RELATIVE`) et
  publie aujourd'hui la couche `resources_r1` en `absent` (fichier
  inexistant) ou `not_consumed` (fichier présent, `consumed=False` câblé).
  Le patron de consommation conditionnelle est celui de C1
  (`_load_c1_index` : `present` si et seulement si l'ensemble des `cell_id`
  égale exactement l'ensemble G3, `not_consumed` sinon).
- `sim/constants.py` : `SNAPSHOT_SCHEMA_VERSION = "v0a-1"`,
  `SNAPSHOT_FLOAT_DECIMALS = 6`.
- `sim/model.py` : `Cell` porte sept champs ; **aucun** ne concerne les
  ressources, et ce lot n'en ajoute aucun (D6).
- `sim/engine.py` : la chaîne du tick (production → commerce → consommation
  → faim → mortalité) ne lit aucune donnée de ressource, et ce lot n'y
  change rien (D6).
- `viewer/snapshot_loader.py` : valide `schema_version` en le comparant à
  `SNAPSHOT_SCHEMA_VERSION` **lu** de `sim/constants.py` — jamais un
  littéral. Le passage à `v0a-2` ne casse donc pas le viewer par
  construction ; SC5 le vérifie quand même par commande.
- Après la fusion du lot 026 : `pipeline/geo/artifacts/cells_resources_r1.json`
  (toutes les cellules G3, chacune avec `cell_id` et `resources`, la liste —
  éventuellement vide — des identifiants de gisements contenus) et
  `pipeline/geo/artifacts/resources_1400_r1.json` (par gisement, les clés
  exactes de `R1_PUBLISHED_DEPOSIT_FIELDS`, dont `id`, `resource`,
  `richness_class`, `cell_id`, `attachment`). Leur forme exacte est définie
  en D7 du brief 026 ; ce brief la lit, il ne la redéfinit pas.

---

## Décisions de conception tranchées par le Planificateur

### D1 — Entrées exactes

| entrée | usage |
|---|---|
| `pipeline/geo/artifacts/cells_resources_r1.json` | la liste des identifiants de gisements par cellule — la source de la jointure |
| `pipeline/geo/artifacts/resources_1400_r1.json` | le détail de chaque gisement — on n'en lit que `id`, `resource`, `richness_class`, `attachment` |
| `pipeline/geo/artifacts/cells_g3.json` | déjà lu par l'export ; sert de référence de couverture (l'ensemble des `cell_id`) |

Lecture de **données** JSON uniquement : `sim/` n'importe aucun module de
`pipeline/geo/` (le moteur reste en bibliothèque standard, règle déjà posée
par D5 du brief 027). Aucun accès réseau. Ce lot ne lit **pas**
`pipeline/geo/data/resources_1400.json` (le fichier de déclarations est
l'affaire du pipeline ; la photographie lit les artefacts publiés).

### D2 — Version de schéma : `v0a-2`

`SNAPSHOT_SCHEMA_VERSION` passe de `"v0a-1"` à `"v0a-2"` dans
`sim/constants.py`, avec une phrase de justification : le contrat public
change (une clé de plus par cellule, une couche qui peut devenir `present`),
donc le nom change — c'est exactement la révision que D2 du brief 027
prévoyait. Aucun code ne compare la version à un littéral : tout lecteur la
lit de la constante.

### D3 — La couche `resources_r1` : trois statuts, conditions exactes

Dans `layers.resources_r1` du document racine :

- **`present`** si et seulement si **toutes** ces conditions tiennent :
  1. les deux fichiers d'entrée de D1 existent ;
  2. l'ensemble des `cell_id` de `cells_resources_r1.json` égale
     **exactement** l'ensemble des `cell_id` de `cells_g3.json` ;
  3. chaque identifiant de gisement listé par une cellule existe dans
     `resources_1400_r1.json`, avec `resource` et `richness_class` qui sont
     des chaînes non vides et `attachment` égal à `"contained"` ;
  4. aucun gisement n'apparaît dans plus d'une cellule.

  La couche vaut alors :

  ```
  {"status": "present",
   "path": "pipeline/geo/artifacts/cells_resources_r1.json",
   "sha256": "<calculé à l'export>",
   "deposits_path": "pipeline/geo/artifacts/resources_1400_r1.json",
   "deposits_sha256": "<calculé à l'export>"}
  ```

  Les deux empreintes sont **calculées à l'export**, jamais recopiées
  (règle n° 12).

- **`not_consumed`** si les fichiers existent mais qu'une condition 2 à 4
  échoue : `{"status": "not_consumed", "path": "<chemin>"}`, et
  `resources: null` sur **toutes** les cellules. On ne complète pas par
  invention, on ne publie pas une couche à moitié vraie.

- **`absent`** si l'un des deux fichiers n'existe pas :
  `{"status": "absent"}`, et `resources: null` sur toutes les cellules.

**`sim/` ne revalide pas le vocabulaire** (natures, classes) : cette porte
vit dans le pipeline géographique (contrôles `R1-A` et `R1-G` du lot 026),
et une seconde validation qui divergerait de la première ferait deux
autorités. `sim/` vérifie la **forme** (chaînes non vides, couverture,
unicité, contenance déclarée) et refuse sinon.

### D4 — L'objet cellule gagne exactement une clé : `resources`

Chaque entrée de `cells` porte les onze clés du schéma `v0a-1` (D4 du brief
027, inchangées) **plus une** :

| clé | contenu | absence |
|---|---|---|
| `resources` | liste, triée par `id` de gisement croissant (ordre lexicographique d'octets UTF-8), d'objets **fermés** `{"id": <chaîne>, "resource": <chaîne>, "richness_class": <chaîne>}` | `[]` = absence **mesurée** (aucun gisement dans cette cellule) ; `null` = couche `not_consumed` ou `absent` |

Un objet de gisement publié dans une cellule a **exactement** ces trois
clés. En particulier : pas de `lon`/`lat` (la cellule est le lieu, ADR-0003),
pas de `certainty` ni de `historical_reason` (la photographie montre l'état
du monde, pas le dossier de provenance — il reste lisible dans l'artefact
source), et **aucune quantité, aucun rang, aucun coefficient** : la classe
est une chaîne du vocabulaire du lot 026, jamais un nombre.

### D5 — Sérialisation : les règles de D5 du brief 027 s'appliquent telles quelles

Cellules par `cell_id` entier croissant, clés triées, flottants arrondis à
`SNAPSHOT_FLOAT_DECIMALS` lu, `json.dumps(..., ensure_ascii=False,
separators=(",", ":"), sort_keys=True)`, un `\n` final, pas de `NaN`.
Deux exports identiques (même graine, mêmes ticks, mêmes artefacts)
produisent des octets identiques.

### D6 — Le tick ne change pas ; `Cell` ne change pas

- `sim/engine.py` : **aucune modification**. Le compteur
  `occurrences_resources_dans_engine` (SC4) le prouve mécaniquement.
- `sim/model.py` : **aucun champ ajouté**. Un champ que le moteur n'écrit ni
  ne lit serait une variable terminale (mode d'échec n° 3) ; la jointure vit
  dans l'export, comme celle de C1.
- `sim/world.py` : **aucune modification**. Le chargement du monde n'a pas
  besoin des gisements tant que le tick ne s'en sert pas.

### D7 — Tests rouges d'abord

`sim/tests/` reçoit des tests neufs. Chaque famille prouve d'abord qu'elle
sait rougir (règle n° 4), par sabotage sur des copies **en mémoire ou dans
un répertoire temporaire**, jamais en modifiant les artefacts committés :

1. **couche absente** : sans les fichiers R1, la couche vaut `absent` et
   toutes les cellules portent `resources: null` ;
2. **couverture incomplète** : un `cells_resources_r1.json` privé d'une
   cellule rend la couche `not_consumed` et toutes les cellules `null` ;
3. **gisement inconnu** : une cellule listant un identifiant absent de
   `resources_1400_r1.json` rend la couche `not_consumed` ;
4. **gisement dupliqué** : un identifiant présent dans deux cellules rend la
   couche `not_consumed` ;
5. **vide contre null** : couche `present`, une cellule sans gisement porte
   `[]` et jamais `null` ;
6. **schéma fermé** : un objet de gisement avec une clé en trop (par exemple
   `tonnage`) fait rougir le test de schéma ;
7. **déterminisme** : deux exports successifs, mêmes octets, même empreinte ;
8. **tick intact** : le texte de `sim/engine.py` ne contient aucune
   occurrence de `resources` (voir SC4).

Pour monter les cas 1 à 4 sans toucher aux artefacts committés, les tests
peuvent rediriger les chemins de module (`_R1_PATH`, `_DEPOSITS_PATH`) vers
un répertoire temporaire — c'est un remplacement en mémoire, pas une
écriture dans `pipeline/geo/`.

### D8 — Preuves committées

Sous `harness/queue/briefs/030-sim-lit-gisements-r1/deliverables/proofs/` :

| fichier | commande de production |
|---|---|
| `snapshot_seed0_tick0_v0a2.json` | `.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json <chemin>` sur le dépôt avec R1 fusionné |
| `snapshot_seed0_tick0_v0a2_b.json` | la même commande une seconde fois — les octets doivent être identiques |
| `snapshot_seed0_tick0_sans_r1.json` | la même commande dans une **copie du dépôt hors arbre de travail** dont les deux artefacts R1 ont été retirés — la couche doit y être `absent` |

Couple `must_differ_from` déclaré dans `deliverables/manifest.json` :
`snapshot_seed0_tick0_v0a2.json` ↔ `snapshot_seed0_tick0_sans_r1.json`
(un monde qui connaît ses gisements ne se photographie pas comme un monde
qui les ignore). Second couple : `deliverables/pre-edit/snapshot_export.py.orig`
↔ `sim/snapshot_export.py` publié. Troisième couple :
`deliverables/pre-edit/constants.py.orig` ↔ `sim/constants.py` publié.

### D9 — Périmètre de fichiers

**Autorisé (modification)** : `sim/snapshot_export.py` ;
`sim/constants.py` (changement de valeur de `SNAPSHOT_SCHEMA_VERSION`
uniquement, plus l'ajout éventuel d'une constante R1 nommée en fin de
fichier ; zéro ligne supprimée) ; `sim/README.md` (documentation du schéma
`v0a-2`, une section courte) ; `sim/tests/` (fichiers de test neufs).

**Autorisé (création)** :
`harness/queue/briefs/030-sim-lit-gisements-r1/deliverables/**`.

**Interdit** : `sim/engine.py` ; `sim/model.py` ; `sim/world.py` ;
`sim/aggregation.py` ; `sim/__main__.py` ; tout fichier sous
`pipeline/geo/**` (y compris les artefacts R1 — ils se lisent, jamais ne se
régénèrent ici) ; `viewer/**` ; `unity/**` ; `harness/*.py` ;
`harness/pipeline/**` ; `docs/**` ; `VISION.md` ; `ROADMAP.md` ;
`HANDOFF.md` ; `hermes/**` ; `.github/**` ; les répertoires des briefs 001
à 029 et 031.

---

## Success Conditions

### SC0 — Le lot 026 est fusionné, constaté avant toute écriture

- `artefacts_r1_suivis_par_git` vaut `2` sur `2` : les deux commandes
  `git ls-files` de « Bloqué tant que » rendent chacune une ligne.
- Si l'une rend une sortie vide, le lot s'arrête sans produire aucun
  fichier, et le signale.

### SC1 — La photographie consomme la couche R1

```
.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json /tmp/v0a2.json
```

- Code de sortie `0`.
- `schema_version` du document égale `SNAPSHOT_SCHEMA_VERSION` lu de
  `sim/constants.py`, et cette valeur est `"v0a-2"`.
- `layers.resources_r1.status` vaut `"present"`, avec les quatre champs de
  chemin et d'empreinte de D3, empreintes recalculables depuis les fichiers
  sources.
- `cellules_avec_cle_resources` égale `cell_count` : **toutes** les cellules
  portent la clé `resources`.
- `cellules_dotees_snapshot` (cellules dont `resources` est une liste non
  vide) est strictement positif, et chaque identifiant de gisement publié
  s'y retrouve **une seule fois** sur l'ensemble des cellules.
- `gisements_publies_snapshot` égale le nombre de gisements de
  `resources_1400_r1.json` dont `attachment` vaut `"contained"` — dénombré
  depuis l'artefact source à l'exécution, jamais recopié du brief 026.

### SC2 — L'absence se déclare, elle ne s'invente pas

Prouvé par les tests de D7 (familles 1 à 4) et par la preuve
`snapshot_seed0_tick0_sans_r1.json` (D8) :

- couche `absent` quand un fichier manque, `not_consumed` quand la
  couverture ou la forme est fausse ; dans les deux cas `resources: null`
  sur toutes les cellules ;
- `cellules_null_quand_non_consomme` égale `cell_count` dans ces deux modes ;
- aucun repli, aucune complétion, aucun gisement « rattrapé ».

### SC3 — Vide et null ne se confondent jamais

- Dans la preuve `present` : `cellules_resources_liste_vide` est un fait
  mesuré (dénominateur : `cell_count`), et **aucune** cellule ne porte
  `null`.
- Dans les preuves non consommées : **aucune** cellule ne porte `[]`.
- Le test de la famille 5 (D7) rougit si un `[]` remplace un `null` ou
  inversement.

### SC4 — Le tick est intact, le schéma est fermé, aucune quantité

- `occurrences_resources_dans_engine` vaut `0` : le texte de
  `sim/engine.py` ne contient pas la chaîne `resources` (commande :
  recherche textuelle, dénominateur : 1 fichier).
- `diff_engine_model_world` vaut `0` : `git diff --stat master -- sim/engine.py
  sim/model.py sim/world.py sim/aggregation.py sim/__main__.py` est vide.
- `cles_hors_schema_cellule` vaut `0` : chaque cellule porte exactement les
  douze clés (onze de `v0a-1` plus `resources`) ; chaque objet de gisement
  publié porte exactement `id`, `resource`, `richness_class`.
- `cles_de_quantite_dans_snapshot` vaut `0` : aucune clé d'un objet publié
  n'appartient à `R1_FORBIDDEN_QUANTITY_KEYS` ni à
  `WORLD_TERMS_FORBIDDEN_KEYS` (les deux **lues** de
  `pipeline/geo/constants.py` par le script de mesure — pas par le moteur).
- `constants_lignes_supprimees` vaut `0` (diff contre
  `deliverables/pre-edit/constants.py.orig`).

### SC5 — Déterminisme, suites vertes, README honnête

- `paires_sha_snapshot_identiques` vaut `1` : les deux preuves `v0a2` de D8
  sont byte-identiques, empreintes égales et non vides.
- `empreinte_avec_r1_differe_sans_r1` vaut `1` : la preuve `present` et la
  preuve `sans_r1` diffèrent (le couple `must_differ_from` de D8).
- Les suites restent vertes, avec dénominateurs rapportés :

```
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m pytest viewer/tests/ -q
.venv/bin/python -m pytest harness/tests/ -q
```

- La preuve SVG du viewer fonctionne encore sur un snapshot `v0a-2` :
  `.venv/bin/python -m viewer --snapshot /tmp/v0a2.json --proof-svg /tmp/carte.svg`
  sort en code `0` (le viewer lit la version depuis la constante — c'est le
  constat de « Ce qui existe déjà », vérifié ici par commande).
- `sim/README.md` documente le schéma `v0a-2` : une clé `resources` par
  cellule, présence / nature / classe seulement, aucune quantité — et dit
  que le tick ne consomme pas encore cette couche (décision propriétaire en
  attente). Le texte ne promet rien de plus.
- `controles_rouges_mordants` vaut `8` sur `8` : chaque famille de D7 a
  rougi sous sabotage avant de verdir (journal dans
  `deliverables/generator-log.md`).

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. Faire consommer les gisements par le tick — aucun rendement, aucun flux
   d'extraction, aucun effet sur la production, la consommation, le commerce
   ou la mortalité. C'est la question `D3` de la proposition
   `PROPOSITION-20260823-plan-couche-1.md`, réservée au propriétaire.
2. Convertir la classe de richesse en nombre, rang, coefficient ou ordre —
   sous aucune forme, y compris un tri « par importance ».
3. Ajouter un champ à `Cell` ou modifier `sim/engine.py`, `sim/model.py`,
   `sim/world.py`, `sim/aggregation.py`, `sim/__main__.py`.
4. Modifier, régénérer ou « corriger » un artefact de `pipeline/geo/`, y
   compris les artefacts R1 — ils se lisent tels quels.
5. Revalider le vocabulaire des natures ou des classes à la place des
   contrôles `R1-*` du pipeline (deux autorités divergeraient).
6. Consommer le relief G6 ou publier une altitude — la couche `relief_g6`
   reste `not_consumed` tant que la preuve Europe n'existe pas.
7. Inventer une température, une précipitation, une saison.
8. Publier une quantité, une réserve, un tonnage, un rythme d'extraction.
9. Toucher au viewer (`viewer/**`) — le montrer est le lot 031.
10. Toucher aux briefs 001 à 029, à `VISION.md`, à `docs/rules/**`, à
    `ROADMAP.md`, à `HANDOFF.md`, à `hermes/**`, à `.github/**`.
11. Réactiver Unity ou CityLab, ou un `mode: full_auto`.
12. Committer, pousser, créer ou changer de branche, ni fusionner
    (ADR-0014).
13. Recopier une valeur d'empreinte dans un test, un document ou un
    commentaire (règle n° 12).
14. Employer l'alias nu de l'interpréteur (règle n° 1).
15. Rapporter un compteur depuis un calcul manqué sans le déclarer
    (règle n° 8 : sentinelle `-1`, jamais `0`).

---

## Required Counters

| nom | source d'échantillon | dénominateur |
|---|---|---|
| `artefacts_r1_suivis_par_git` | sorties des deux `git ls-files` de SC0 | `2` ; doit valoir `2` avant toute écriture |
| `cellules_avec_cle_resources` | cellules du snapshot `present` portant la clé `resources` | `cell_count` du snapshot ; doit l'égaler |
| `cellules_dotees_snapshot` | cellules dont `resources` est une liste non vide | `cell_count` ; strictement positif, fait mesuré |
| `cellules_resources_liste_vide` | cellules dont `resources` vaut `[]` | `cell_count` ; fait mesuré |
| `gisements_publies_snapshot` | objets de gisement distincts publiés dans les cellules | gisements `contained` de `resources_1400_r1.json`, dénombrés à l'exécution ; doit l'égaler |
| `gisements_publies_en_double` | identifiants publiés dans plus d'une cellule | gisements publiés ; doit valoir `0` |
| `cellules_null_quand_non_consomme` | cellules à `resources: null` dans les modes `absent` / `not_consumed` | `cell_count` ; doit l'égaler dans ces modes |
| `cles_hors_schema_cellule` | clés de cellule hors des douze attendues, plus clés d'objet de gisement hors des trois attendues | cellules + objets publiés ; doit valoir `0` |
| `cles_de_quantite_dans_snapshot` | clés du snapshot appartenant à `R1_FORBIDDEN_QUANTITY_KEYS` ∪ `WORLD_TERMS_FORBIDDEN_KEYS` (lues de `pipeline/geo/constants.py` par le script de mesure) | clés balayées ; doit valoir `0` |
| `occurrences_resources_dans_engine` | occurrences textuelles de `resources` dans `sim/engine.py` | `1` fichier ; doit valoir `0` |
| `diff_engine_model_world` | lignes du diff sur les cinq modules interdits de D9 | `5` fichiers ; doit valoir `0` |
| `constants_lignes_supprimees` | lignes supprimées au diff contre l'instantané pré-édition | `1` mesure ; doit valoir `0` |
| `paires_sha_snapshot_identiques` | empreintes des deux preuves `v0a2` | `1` paire ; doit valoir `1` |
| `empreinte_avec_r1_differe_sans_r1` | comparaison des empreintes `present` / `sans_r1` | `1` comparaison ; doit valoir `1` |
| `controles_rouges_mordants` | familles de D7 ayant rougi sous sabotage | `8` |
| `tests_sim_passed_030` | tests réussis de `sim/tests/` | tests collectés ; sentinelle `-1` si le provisionnement échoue, jamais `0` |
| `tests_viewer_passed_030` | tests réussis de `viewer/tests/` | tests collectés ; sentinelle `-1` idem |
| `tests_harness_passed_030` | tests réussis de `harness/tests/` | tests collectés ; sentinelle `-1` idem (les SKIP Unity sous Linux sont déclarés, pas comptés en échec) |

Un script committé sous
`harness/queue/briefs/030-sim-lit-gisements-r1/deliverables/measure_r1_030.py`,
exécuté depuis la racine avec `.venv/bin/python`, imprime chaque compteur
avec son dénominateur, dérivé des fichiers — jamais une valeur recopiée à la
main.

---

## Acceptable Waivers (si une impossibilité est invoquée)

Une impossibilité s'éprouve avant d'être invoquée : une commande et le
message d'erreur qu'elle produit (règle n° 9).

| affirmation d'impossibilité | commande exigée | erreur attendue |
|---|---|---|
| « les artefacts R1 ne sont pas suivis par git » | `git ls-files pipeline/geo/artifacts/cells_resources_r1.json` depuis la racine | une sortie **vide**. **Ce n'est pas un waiver, c'est le blocage nominal de ce brief** (SC0) : le lot 026 n'est pas fusionné, le Générateur s'arrête sans produire aucun fichier |
| « le paquet de test n'est pas installé » | `.venv/bin/python -m pytest --version` depuis la racine | `No module named pytest`. Outillage de test, pas code produit : le Générateur peut l'installer ; si l'installation échoue, les compteurs de suites valent `-1`, consigné dans `deliverables/generator-log.md` |
| « `resources_1400_r1.json` est illisible » | `.venv/bin/python -c "import json; json.load(open('pipeline/geo/artifacts/resources_1400_r1.json'))"` depuis la racine | `FileNotFoundError` ou `JSONDecodeError` nommant le fichier. Si le fichier est suivi par git mais illisible, c'est une escalade vers le propriétaire, jamais une réparation locale de l'artefact |
| « les constantes geo ne sont pas importables par le script de mesure » | `.venv/bin/python -c "import sys; sys.path.insert(0,'pipeline/geo'); import constants; print(len(constants.WORLD_TERMS_FORBIDDEN_KEYS))"` depuis la racine | `AttributeError` ou `ImportError` nommant le symbole. Escalade : la dépendance au lot 025 fusionné aurait régressé |

---

## Execution Contract

### Interpréteur et commandes

Sur cette machine Linux, l'interpréteur est `.venv/bin/python` depuis la
racine. L'alias nu est interdit (règle n° 1). Aucune commande de ce lot n'a
besoin d'Unity, d'un accès réseau ou d'une pile scientifique : le moteur
reste en bibliothèque standard (le script de mesure, lui, peut lire
`pipeline/geo/constants.py` en ajoutant son chemin à `sys.path`).

### Estimation d'appels d'outils

**Estimation du Planificateur : `90` appels d'outils.** Sous le seuil de
`150` (découpage) et l'arrêt du budget à `160`. Ancres : le lot 027 (même
sous-système, périmètre plus large — il créait l'export entier) a été estimé
et exécuté dans le budget ; ce lot ne fait qu'étendre une jointure existante
sur le patron C1, plus huit familles de tests et trois preuves. À vérifier
avant génération :

```
.venv/bin/python harness/budget.py split-check \
  --brief harness/queue/briefs/030-sim-lit-gisements-r1 \
  --estimated-calls 90
```

### Preuves committées et re-vérifiables

Toutes les preuves de D8 vivent sous `deliverables/proofs/` de ce brief,
suivies par git (aucun `.log` gitignoré, aucun raster, aucun `__pycache__`).
Jamais `git add -A` : chaque fichier s'ajoute nommément.

### Deliverables obligatoires

Sous `harness/queue/briefs/030-sim-lit-gisements-r1/deliverables/` :

- `manifest.json` — `files[]` (avec les trois couples `must_differ_from` de
  D8), `counters[]` (valeur, `sample_size` réelle, commande), `waivers[]`
  (chacun avec sa commande et son erreur) ;
- `generator-log.md` — en français clair : ce qui a été fait, ce qui a
  résisté, le journal des huit rouges de D7 ;
- `measure_r1_030.py` — le script de reconstruction des compteurs ;
- `pre-edit/snapshot_export.py.orig`, `pre-edit/constants.py.orig` ;
- `proofs/` — les trois snapshots de D8.

### Interdictions pour le Générateur

Il ne prononce jamais la recevabilité de son propre travail, ne rédige aucun
`verdict.md`, ne modifie ni `brief.md` ni `eval-rubric.md`, ne commite pas,
ne pousse pas, ne crée ni ne change de branche, et ne fusionne rien
(ADR-0014). Unity n'est jamais lancé (aucune étape n'en a besoin ; si un
jour un lot en avait besoin, ce serait `unity/run-unity.ps1` en un appel,
jamais un journal pollé).

### Fin de lot

Le lot est terminé quand SC0 est constatée, que la commande de SC1 sort en
code `0` avec la couche `present`, que les six conditions (SC0 à SC5) sont
couvertes par des compteurs reconstruits, et que les deliverables sont
committés par l'orchestrateur.

---

## Registre de coût

Une ligne, sans `--audit-id` :

```
.venv/bin/python harness/backends/ledger.py append --backend cursor \
  --brief harness/queue/briefs/030-sim-lit-gisements-r1 \
  --event generator-run
```
