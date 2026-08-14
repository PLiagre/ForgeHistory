# Brief 020 : réparer la provenance du littoral de G3 — que la terre déclarée soit la terre produite

**Authored**: 2026-08-14T12:03:00Z
**Author**: forge-planificateur

> **Note de transparence (contrat du Planificateur) :** le rôle signataire est
> le rôle natif du harnais `forge-planificateur`. L'acteur réel est un
> sous-agent Cursor Cloud (modèle Claude Opus 5), orchestré par un agent Cursor
> Cloud qui remplace le CTO Claude. Aucun suffixe n'est ajouté à la signature :
> le contrôle mécanique `verdict_is_not_self_authored` compare les acteurs de
> part et d'autre d'un lot, et un couple de signatures suffixées serait refusé.

---

## Provenance

Le brief 019 a livré l'adjacence maritime (G4) et, chemin faisant, a **mesuré**
un trou qu'il n'avait pas le droit de boucher : le littoral corrigé de 1400 que
la chaîne produit aujourd'hui n'est pas celui que `artifacts/MANIFEST_g3.json`
déclare comme entrée des cellules. L'écart a été escaladé
(`019-geo-adjacence-g4/amendment-001-escalade-empreinte-g3.md`), inscrit comme
constat ouvert, et renvoyé à « un brief ultérieur dédié » (non-objectif n° 18 du
019). Ce brief-ci est ce brief ultérieur, et il ne fait que cela.

Un seul sous-système : `pipeline/geo/`. Un seul thème causal : la déclaration
d'entrée du littoral de G3 n'est plus celle que la chaîne produit. Ce n'est donc
pas un `NEEDS_SPLIT`.

Ce `brief.md` est la **SEULE instruction** (voir `CLAUDE.md` › Single Source of
Instruction). Le brief 019, son amendement et son verdict sont de la matière
historique : on peut les lire pour comprendre d'où vient le constat, on n'en
tire aucune consigne, et **on ne les modifie pas**.

---

## World-Terms Requirement

**Chaîne causale.**

Le monde n'a qu'une seule terre. Le trait de côte dit où elle s'arrête ; les
cellules découpent ce qu'elle contient ; les zones de mer occupent ce qu'elle
laisse. Ces trois choses ne sont pas trois données côte à côte : la seconde et
la troisième sont **produites par** la première. Une cellule existe parce qu'un
morceau de terre existait à cet endroit-là ; une côte est littorale parce qu'une
eau la touche.

Un manifeste n'est pas de la décoration : c'est la phrase qui dit « voici la
terre qui a produit ces cellules ». Quand cette phrase désigne une terre que la
chaîne ne produit plus, le monde a **deux réponses** à la question « quelle
terre ? » — celle que la chaîne calcule, et celle que le manifeste raconte.
C'est la même famille de défaut que la double clé spatiale (mode d'échec n° 1) :
non pas une valeur fausse, mais une seconde autorité pour une question qui n'en
admet qu'une. Le jour où quelqu'un rejouera la chaîne en faisant confiance au
manifeste, il croira reconstruire les cellules committées et en obtiendra
d'autres, sans qu'aucun contrôle n'ait rien dit.

Deux réparations sont possibles, et elles ne disent pas la même chose du monde.
Soit la terre a réellement bougé, et alors les cellules décrivent un monde périmé
qu'il faut refaire. Soit la terre n'a pas bougé et seuls les octets qui la
sérialisent ont changé, et alors ce sont les cellules qui ont raison et la phrase
qui est périmée. On ne choisit pas entre les deux par confort : **on mesure la
terre**. C'est ce que fait ce lot avant de toucher quoi que ce soit.

**Interdit** dans ce lot : tout barème, tout bonus, tout pourcentage. Rien ici ne
parle de gains ni de malus. On rend au monde une seule réponse à « quelle
terre ? ».

---

## Vocabulaire (expliqué une fois)

- **littoral vivant** : `pipeline/geo/artifacts/coastline_1400.json` tel que la
  chaîne le régénère aujourd'hui. Il pèse environ 3,6 Mo, il est **ignoré par
  git**, donc absent d'un clone frais ; il se régénère depuis `pipeline/geo/`
  par `../../.venv/bin/python tests/run_proof_g2b.py`.
- **déclaration d'entrée** : la valeur que `artifacts/MANIFEST_g3.json` porte
  sous `inputs.coastline_1400`.
- **empreinte SHA256** : condensé d'un fichier, qui prouve que deux fichiers
  sont octet pour octet identiques. Citée **par nom de source**, jamais par
  valeur hexadécimale (règle durement acquise n° 12) ; comparée à l'exécution.
- **écart de sérialisation** : deux fichiers décrivant la même géométrie aux
  epsilon près, mais dont les octets diffèrent (ordre, arrondi, mise en forme).
- **écart de géométrie** : la terre elle-même a bougé — des surfaces
  apparaissent ou disparaissent au-delà de l'epsilon déclarée.
- **maille gelée** : la maille des cellules committées n'est pas rejouée par ce
  lot ; les identifiants que `sim/` consomme restent donc ceux du fichier
  committé **par construction**, et non parce qu'on aurait régénéré puis
  constaté.
- **sentinelle `-1`** : la valeur qui signifie « non calculé » dans tout le
  projet (règle n° 8). Un zéro est une mesure réelle et ne s'y substitue jamais.

---

## Ce qui existe déjà, et que ce lot lit sans le refaire

Vérifié sur le dépôt au moment de l'écriture de ce brief :

- `artifacts/cells_g3.json`, `adjacency_g3.json`, `stats_g3.json`,
  `MANIFEST_g3.json`, `registry/cell_registry.json` : committés, sans
  modification. `stats_g3.json` porte `cell_count`.
- `MANIFEST_g3.json` porte `inputs.coastline_1400`, `land_source`, un bloc
  `outputs` de cinq entrées, et `fixed_timestamp` figé à `1970-01-01T00:00:00Z`.
  Il ne se déclare **pas** lui-même dans ses propres `outputs`.
- `MANIFEST_g4.json` porte `inputs.coastline_1400` (l'empreinte du littoral
  vivant, déjà juste), `coastline_1400_sha_declared_by_g3` (la copie de la
  déclaration périmée de G3), `coastline_1400_sha_equal`, et un bloc `outputs`
  de six entrées dont `artifacts/stats_g4.json`. Il ne se déclare pas non plus
  dans ses propres `outputs`.
- `artifacts/stats_g4.json` porte `coastline_1400_sha_equals_g3_input`.
- `pipeline/geo/io_util.py` porte `write_json`, `dumps_deterministic`,
  `canonicalize` et `sha256_file`. Les trois fichiers ci-dessus sont déjà écrits
  dans cette forme canonique (clés triées, séparateurs compacts, flottants
  arrondis) : un aller-retour de lecture puis réécriture par ce même écrivain
  les laisse octet-identiques tant qu'aucune valeur ne change.
- `pipeline/geo/constants.py` porte `G3_AREA_EPS_M2`.
- `sim/world.py` lit `cells_g3.json` (`cell_id`, `area_km2`) et
  `adjacency_g3.json`. Il ne lit **aucune** géométrie de littoral et **aucun**
  manifeste.
- `pipeline/geo/.gitignore` exclut `artifacts/`, `logs/`, `capture/`, `build/` :
  les artefacts committés l'ont été par ajout forcé. `coastline_1400.json` et
  `MANIFEST_g2b.json` ne sont **pas** suivis, et ce lot ne les ajoute pas.
- `harness/queue/briefs/019-geo-adjacence-g4/deliverables/check_provenance_coastline_019.py`
  existe : c'est le **modèle** de la commande d'écart (lecture seule, aucune
  valeur hexadécimale imprimée, codes de sortie 0 / 1 / 2). Il n'est ni modifié,
  ni déplacé, ni réutilisé en place.
- **N'existe pas** : `pipeline/geo/steps/03b_align_coastline_provenance.py`,
  `pipeline/geo/tests/run_proof_coastline_provenance.py`, tout fichier
  `logs/v1_051_*`.

---

## Décisions de conception tranchées par le Planificateur

Le Générateur n'arbitre aucun de ces points. Il choisit librement les noms de
fonctions et de variables internes, et l'organisation du code dans le périmètre
autorisé.

### D1 — Entrées exactes, toutes en lecture seule

| entrée | ce qui en est lu |
|---|---|
| `pipeline/geo/artifacts/coastline_1400.json` (vivant) | la géométrie de terre, et son empreinte calculée à l'exécution |
| `pipeline/geo/artifacts/MANIFEST_g2b.json` | la sortie déclarée pour `artifacts/coastline_1400.json` |
| `pipeline/geo/artifacts/MANIFEST_g3.json` | `inputs.coastline_1400`, le bloc `outputs`, `fixed_timestamp` |
| `pipeline/geo/artifacts/cells_g3.json` | `cells[]` : `cell_id`, `geometry` |
| `pipeline/geo/artifacts/stats_g3.json` | `cell_count` — dénominateur de tout compteur par cellule |
| `pipeline/geo/artifacts/MANIFEST_g4.json` | `inputs.coastline_1400`, `coastline_1400_sha_declared_by_g3`, `coastline_1400_sha_equal`, `outputs` |
| `pipeline/geo/artifacts/stats_g4.json` | `coastline_1400_sha_equals_g3_input` |
| `pipeline/geo/constants.py` | `G3_AREA_EPS_M2`, **lue**, jamais recopiée en littéral |
| `pipeline/geo/io_util.py` | `write_json` / `sha256_file`, importés, jamais modifiés |
| `pipeline/geo/projection.py` | la projection, si la mesure de surface en a besoin |

Le littoral vivant est absent d'un clone frais. S'il manque, la seule conduite
autorisée est de le **régénérer** puis de mesurer (voir Waivers) ; jamais de
conclure sur son absence.

### D2 — Le diagnostic est tranché : l'écart est de sérialisation — et il se remesure quand même

La planification a mesuré, avant l'écriture de ce brief : le débordement des
cellules hors de la terre du littoral vivant est vide, la terre non couverte par
les cellules est un résidu très largement inférieur à `G3_AREA_EPS_M2` lue de
`constants.py`, et la surface de terre du littoral vivant coïncide avec l'union
des cellules. **La terre n'a pas bougé.** Ce sont les octets qui la sérialisent
qui ont changé.

La décision qui en découle est ferme : on n'a **pas** le droit de remailler G3
pour réparer une empreinte. C'est la **déclaration** qui est périmée, pas la
maille.

Mais la présence d'une mesure passée n'est pas la fonction (règle n° 7). Le
Générateur **rejoue** cette mesure et la rapporte :

- `depassement_cellules_hors_terre_m2` : l'aire de la part de l'union des
  cellules qui sort de la terre du littoral vivant ;
- `terre_non_couverte_m2` : l'aire de terre du littoral vivant qu'aucune cellule
  ne couvre ;
- `epsilon_surface_g3_m2` : `G3_AREA_EPS_M2`, **lue** du fichier de constantes ;
- `ecart_est_serialisation` : vaut 1 si les deux aires sont ≤ l'epsilon lue,
  0 sinon.

Aucun nombre de la planification n'est un objectif à retrouver : ce sont les
deux aires **mesurées** confrontées à l'epsilon **lue** qui décident. Si la
mesure contredit la planification (une aire au-dessus de l'epsilon), le
Générateur **escalade** — il ne remaille pas de sa propre initiative (voir
Waivers).

### D3 — La maille n'est pas rejouée ; les identifiants consommés sont gelés par construction

`sim/world.py` consomme 596 identifiants de cellule et les surfaces associées. Le
registre `registry/cell_registry.json` ne réattribue jamais un identifiant, et
G3-C garantit que la même clé de domaine rend le même identifiant. Mais rien de
tout cela n'a besoin d'être invoqué ici, pour une raison plus simple : **ce lot
ne rejoue pas la maille**. Les identifiants restent donc ceux du fichier
committé, par construction.

Constatations exigées, dans cet ordre :

1. **Avant toute écriture**, un instantané des identifiants actifs est committé
   sous `deliverables/pre-edit/cell_ids_actifs.txt` : la liste triée des
   `cell_id` lus de `artifacts/cells_g3.json`, un par ligne. C'est un relevé
   dérivé, pas une empreinte citée.
2. Après le lot, `git status --porcelain` est **vide** sur `cells_g3.json`,
   `adjacency_g3.json`, `stats_g3.json` et `registry/cell_registry.json`.
3. `cellules_actives_inchangees` égale `cellules_actives_instantane`, qui égale
   `cell_count` lu de `stats_g3.json`.
4. `cellules_actives_ajoutees` et `cellules_actives_retirees` valent **0**, et ce
   sont des zéros **mesurés** — jamais la sentinelle `-1`.

**Interdit :** relancer `run_cells` ou `tests/run_proof_g3.py` **dans le dépôt**.
Une copie hors dépôt est permise uniquement comme **mesure** ; ses artefacts ne
sont alors recopiés nulle part, et les mêmes compteurs d'identifiants y sont
rapportés. Si cette copie montrait un écart d'identifiants, c'est un **constat
ouvert** (la maille n'est pas rejouable bit à bit dans cet environnement) et non
une licence pour remplacer les cellules committées : la géométrie actuelle les
recouvre encore, D2 le mesure.

`sim/` est en **lecture seule** dans tous les cas de ce lot.

### D4 — Ce qui est aligné dans `MANIFEST_g3.json`, et rien d'autre

Un seul champ change : `inputs.coastline_1400` reçoit l'empreinte du littoral
vivant, **calculée à l'exécution** depuis le fichier lui-même. Jamais recopiée
d'un littéral, ni de `MANIFEST_g2b.json`, ni de `MANIFEST_g4.json` : une valeur
copiée d'un autre manifeste réussirait la comparaison sans avoir jamais lu la
terre.

Le bloc `outputs` de G3 n'est **pas** retouché : les fichiers de sortie n'ont pas
changé, et la vérification exigée le prouve (`sorties_g3_conformes`). Le
`fixed_timestamp` reste `1970-01-01T00:00:00Z` — aucune horloge murale dans un
artefact.

L'écriture passe par `io_util.write_json`, la forme canonique de la chaîne. Avant
la première écriture réelle, le Générateur prouve que l'aller-retour est neutre :
relire puis réécrire un artefact **sans changer aucune valeur** doit laisser le
fichier octet-identique (`roundtrip_serialisation_neutre`). Sans cette preuve, un
changement de mise en forme se ferait passer pour une réparation.

### D5 — Ce qui est relu dans G4, et rien d'autre

Après alignement de G3, trois champs sont **relus** — c'est-à-dire recalculés
depuis les fichiers, jamais recopiés à la main :

| fichier | champ | ce qu'il doit devenir |
|---|---|---|
| `artifacts/stats_g4.json` | `coastline_1400_sha_equals_g3_input` | 1 |
| `artifacts/MANIFEST_g4.json` | `coastline_1400_sha_declared_by_g3` | l'entrée que G3 déclare désormais |
| `artifacts/MANIFEST_g4.json` | `coastline_1400_sha_equal` | 1 |

Puis, et seulement pour les fichiers G4 **effectivement réécrits**, les
empreintes de sortie de `MANIFEST_g4.json` sont recalculées à l'exécution. Dans
l'ordre : `stats_g4.json` d'abord, `MANIFEST_g4.json` ensuite (le second déclare
le premier ; l'inverse n'est pas vrai).

Ce n'est **pas** une régénération de G4. `git status --porcelain` doit rester
**vide** sur `sea_zones_g4.json`, `adjacency_g4.json`, `topology_links_g4.json`,
`adjacency_divergence_g4.json`, `registry/sea_zone_registry.json`,
`logs/v1_050_*` et toutes les captures. Interdit : rejouer le semis de zones,
recalculer les arêtes, « améliorer » `sea_zone_count`, `by_kind` ou la saturation
de `SEA_ZONE_COUNT_MAX`. Ce dernier point est un constat ouvert du 019 ; il
appartient à un autre lot.

### D6 — Où vit la réparation : un script d'étape dédié

La réparation vit dans un fichier neuf,
`pipeline/geo/steps/03b_align_coastline_provenance.py`, lançable seul depuis
`pipeline/geo/` :

```
../../.venv/bin/python steps/03b_align_coastline_provenance.py
```

Il est **idempotent** : une seconde exécution ne change aucun octet. Il imprime
la liste des fichiers qu'il écrit, ce qui donne le dénominateur de
`passes_alignement_identiques` — un dénombrement dérivé, pas un nombre écrit à
la main.

Restent intouchés, en lecture seule : `constants.py`, `qa/checks.py`,
`pipeline.py`, `steps/02_coastline.py`, `steps/02b_corrections_1400.py`,
`steps/03_cells.py`, `steps/04_adjacency.py`. Une modification de
`04_adjacency.py` n'est tolérée **que** pour exposer une relecture de provenance
qui existe déjà dans ce fichier, jamais pour changer le graphe — et par défaut
elle n'a pas lieu.

### D7 — La garde durable, et sa preuve rouge

Un contrôle neuf, `pipeline/geo/tests/run_proof_coastline_provenance.py`, rougit
si la déclaration d'entrée de G3 cesse d'être le littoral vivant. Il est nommé
d'après ce qu'il **dérive** — la provenance du littoral — et non d'après le
fichier qu'il surveille (règles n° 2 et n° 3).

Contrat, depuis `pipeline/geo/` :

```
../../.venv/bin/python tests/run_proof_coastline_provenance.py
```

- il recalcule l'empreinte du littoral vivant **à chaque exécution** et lit les
  déclarations depuis les manifestes du disque ; il ne porte **aucune** valeur
  attendue en dur ;
- il vérifie l'entrée de G3, la sortie déclarée par G2-bis, et les trois champs
  de provenance de G4 (D5) ;
- il n'imprime **aucune** valeur hexadécimale : seulement des noms de source et
  des résultats de comparaison ;
- code de sortie **0** si tout concorde ; **1** en cas d'écart, avec un message
  nommant les sources en désaccord ; **2** si une source est absente du disque,
  en nommant la commande qui la régénère — jamais 1, pour qu'une absence ne soit
  jamais confondue avec un écart mesuré (règle n° 10) ;
- il écrit sa sortie verte dans `logs/v1_051_provenance_vert.txt` et un rapport
  lisible dans `logs/v1_051_provenance.json`.

**Preuve rouge d'abord** (règle n° 4) : le Générateur monte un sabotage dans une
copie de travail **hors du dépôt** — il y mute la déclaration d'entrée de G3 —
rejoue la garde, et committe la sortie obtenue sous
`logs/v1_051_provenance_rouge.txt`. Le sabotage porte sur la **déclaration**,
jamais sur le code de la garde. Les deux sorties forment un couple
`must_differ_from`.

`pipeline/geo/qa/checks.py` n'est **pas** modifié : ajouter une quinzième entrée
à `run_g3_green` sortirait du thème de ce lot et déclencherait un risque de
découpe. La garde est un script de preuve dédié.

### D8 — La commande d'écart du lot : rouge avant, verte après

Un script committé sous le dossier du brief,
`deliverables/check_provenance_coastline_020.py`, tient exactement le contrat du
script homologue du 019 — dont il s'inspire sans le modifier ni le déplacer.
Depuis la racine :

```py
.venv/bin/python harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/check_provenance_coastline_020.py
```

- il lit trois choses, en lecture seule : le littoral vivant, l'entrée
  `inputs.coastline_1400` de `MANIFEST_g3.json`, et la sortie que
  `MANIFEST_g2b.json` déclare pour ce même fichier ;
- il calcule l'empreinte du fichier vivant à l'exécution et la compare aux deux
  valeurs déclarées ; il n'imprime, n'écrit et ne consigne **aucune** valeur
  hexadécimale (règles n° 9 et n° 12 tenues ensemble) ;
- code **0** si le vivant égale l'entrée déclarée par G3 ; **1** en cas d'écart,
  avec le message nommant ses deux sources puis une ligne disant si le vivant
  égale la sortie déclarée par G2-bis ; **2** si une source est absente, en
  nommant la commande de régénération.

Séquence exigée, et son ordre est la preuve :

1. **avant** toute écriture, la commande est jouée et sa sortie committée sous
   `deliverables/pre-edit/check_provenance_avant.txt` — code de sortie attendu
   **1** ;
2. **après** l'alignement, la même commande est rejouée et sa sortie committée
   sous `deliverables/check_provenance_apres.txt` — code de sortie attendu
   **0**.

Les deux fichiers forment un couple `must_differ_from`. Une réparation dont on
n'a pas vu l'état rouge n'est pas une réparation prouvée.

### D9 — Sorties exactes

| fichier | contenu |
|---|---|
| `pipeline/geo/steps/03b_align_coastline_provenance.py` | l'alignement (D4, D5, D6) |
| `pipeline/geo/tests/run_proof_coastline_provenance.py` | la garde durable (D7) |
| `pipeline/geo/logs/v1_051_provenance.json` | rapport de la garde : sources comparées, résultats, aucun hexadécimal |
| `pipeline/geo/logs/v1_051_provenance_vert.txt` | sortie de la garde sur le dépôt réparé |
| `pipeline/geo/logs/v1_051_provenance_rouge.txt` | sortie de la garde sous sabotage hors dépôt |
| `pipeline/geo/artifacts/MANIFEST_g3.json` | un champ aligné (D4) |
| `pipeline/geo/artifacts/stats_g4.json` | un champ relu (D5) |
| `pipeline/geo/artifacts/MANIFEST_g4.json` | trois champs relus + les sorties recalculées des fichiers réécrits (D5) |
| `pipeline/geo/README.md` | constat de 019 fermé, sans sur-revendication (SC6) |
| `deliverables/check_provenance_coastline_020.py` | la commande d'écart (D8) |
| `deliverables/measure_g3_provenance_020.py` | le script de mesure (SC7) |
| `deliverables/check_provenance_apres.txt` | sortie de la commande d'écart après réparation |
| `deliverables/pre-edit/check_provenance_avant.txt` | sortie de la même commande avant réparation |
| `deliverables/pre-edit/cell_ids_actifs.txt` | instantané des identifiants actifs (D3) |
| `deliverables/pre-edit/MANIFEST_g3.json.orig` | instantané du manifeste G3 avant édition |
| `deliverables/pre-edit/stats_g4.json.orig` | instantané des statistiques G4 avant édition |
| `deliverables/pre-edit/pipeline-geo-README.md.orig` | instantané du README avant édition |
| `deliverables/manifest.json` | fichiers, couples, compteurs, dérogations |
| `deliverables/generator-log.md` | journal d'exécution en français clair |

**Cinq couples `must_differ_from`** doivent être déclarés dans
`deliverables/manifest.json`, en chemins relatifs au dossier du brief. La porte
mécanique ne peut pas deviner qu'un couple doit différer ; non déclaré, il n'est
pas vérifié :

1. `pre-edit/pipeline-geo-README.md.orig` ↔ le `README.md` publié ;
2. `pre-edit/MANIFEST_g3.json.orig` ↔ le `MANIFEST_g3.json` publié ;
3. `pre-edit/stats_g4.json.orig` ↔ le `stats_g4.json` publié ;
4. `pre-edit/check_provenance_avant.txt` ↔ `check_provenance_apres.txt` ;
5. `logs/v1_051_provenance_rouge.txt` ↔ `logs/v1_051_provenance_vert.txt`.

### D10 — Périmètre de fichiers

**Autorisé (création ou modification) :**

- `pipeline/geo/steps/03b_align_coastline_provenance.py` (nouveau) ;
- `pipeline/geo/tests/run_proof_coastline_provenance.py` (nouveau) ;
- `pipeline/geo/logs/v1_051_*` (nouveaux) ;
- `pipeline/geo/artifacts/MANIFEST_g3.json` (un champ), `stats_g4.json` (un
  champ), `MANIFEST_g4.json` (trois champs + sorties recalculées) ;
- `pipeline/geo/README.md` ;
- `harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/**` ;
- `harness/queue/cost-ledger.jsonl` (une seule ligne ajoutée en fin de fichier).

**Interdit (lecture seule, ou hors périmètre) :** `pipeline/geo/constants.py` ;
`qa/checks.py` ; `pipeline.py` ; `io_util.py` ; `projection.py` ;
`steps/02_coastline.py` ; `steps/02b_corrections_1400.py` ; `steps/03_cells.py` ;
`steps/04_adjacency.py` (sauf la tolérance étroite de D6) ; `data/**` ;
`sources.lock` ; `sources/**` ; `legacy_game_data/**` ; `.gitignore` ; les
artefacts et registres de maille (`cells_g3.json`, `adjacency_g3.json`,
`stats_g3.json`, `registry/cell_registry.json`,
`registry/g6_density_refinement.json`) ; les artefacts de graphe G4
(`sea_zones_g4.json`, `adjacency_g4.json`, `topology_links_g4.json`,
`adjacency_divergence_g4.json`, `registry/sea_zone_registry.json`) ;
`logs/v1_049_*`, `logs/v1_050_*` et toutes les captures ; `pipeline/geo/tests/`
sauf le fichier neuf ci-dessus ; tout fichier sous `sim/` ; tout fichier sous
`unity/` ; `harness/*.py` ; `harness/pipeline/` ; `architecture/` ;
`docs/adr/**` ; `docs/rules/**` ; `VISION.md` ; `ROADMAP.md` ; `HANDOFF.md` ;
`.github/**` ; les archives des briefs 001 à 019.

---

## Success Conditions

### SC1 — Le diagnostic est rejoué, et il conclut « sérialisation », pas « géométrie »

- `terre_vivante_m2` est l'aire de terre du littoral vivant, mesurée, > 0. Une
  mesure faite sur une géométrie vide n'est pas une mesure (mode d'échec n° 6).
- `depassement_cellules_hors_terre_m2` et `terre_non_couverte_m2` sont mesurés et
  rapportés, chacun avec `terre_vivante_m2` pour dénominateur.
- `epsilon_surface_g3_m2` est **lue** de `constants.py`. Cette valeur n'apparaît
  en littéral ni dans le script de mesure, ni dans l'alignement, ni dans la
  garde.
- `ecart_est_serialisation` vaut **1** : les deux aires sont ≤ l'epsilon lue.
- `code_sortie_ecart_avant` vaut **1** et `code_sortie_ecart_apres` vaut **0**
  (D8). Un code 2 n'est pas un écart : il faut régénérer puis rejouer.
- `cellules_lues_g3` égale `cell_count` lu de `stats_g3.json`.

Si l'une des deux aires dépasse l'epsilon lue, la mesure contredit la
planification : le Générateur **escalade** (voir Waivers) et ne remaille pas.

### SC2 — La maille n'a pas bougé et les identifiants consommés par `sim/` sont gelés

- `cellules_actives_instantane` égale `cell_count` lu de `stats_g3.json`,
  l'instantané ayant été pris **avant** toute écriture (D3).
- `cellules_actives_inchangees` égale `cellules_actives_instantane`.
- `cellules_actives_ajoutees` et `cellules_actives_retirees` valent **0**, zéros
  **mesurés** et jamais la sentinelle `-1`.
- `artefacts_maille_diff_vides` vaut 4 sur 4 : `cells_g3.json`,
  `adjacency_g3.json`, `stats_g3.json`, `registry/cell_registry.json` sont sans
  aucune modification.
- `fichiers_sim_modifies` vaut **0** : `sim/` est resté en lecture seule.
- `tests_sim_passed_020` est rapporté avec le nombre de tests collectés pour
  dénominateur — non-régression de la simulation, qui consomme la maille.

### SC3 — `MANIFEST_g3.json` déclare enfin le littoral que la chaîne produit

- `empreinte_entree_g3_egale_vivant` vaut **1** et
  `empreinte_vivant_egale_sortie_g2b` vaut **1** : le vivant, l'entrée déclarée
  par G3 et la sortie déclarée par l'étape qui le produit désignent le même
  fichier. Les trois valeurs sont lues ou calculées à l'exécution ; aucune n'est
  imprimée.
- `sorties_g3_conformes` égale le nombre d'entrées du bloc `outputs` de
  `MANIFEST_g3.json` : les sorties déclarées décrivent bien les fichiers
  présents, ce qui établit que seule l'entrée était périmée.
- `champs_manifeste_g3_modifies` vaut **1** : un seul chemin de feuille diffère
  entre l'instantané pre-edit et le manifeste publié, et c'est
  `inputs.coastline_1400`. Le `fixed_timestamp` est conservé.
- `roundtrip_serialisation_neutre` égale le nombre d'artefacts réécrits par
  l'alignement : un aller-retour sans changement de valeur laisse chaque fichier
  octet-identique (D4).

### SC4 — G4 relit la provenance réparée, sans que son graphe bouge d'un octet

- `provenance_g4_egale_entree_g3` vaut **1** : `coastline_1400_sha_declared_by_g3`
  égale ce que `MANIFEST_g3.json` déclare désormais en entrée.
- `drapeau_egalite_manifeste_g4` et `drapeau_egalite_stats_g4` valent **1**.
- `sorties_g4_conformes` égale le nombre d'entrées du bloc `outputs` de
  `MANIFEST_g4.json` : chaque sortie déclarée correspond au fichier présent,
  y compris celles réécrites par ce lot.
- `artefacts_g4_modifies_hors_liste` vaut **0** : hors `stats_g4.json` et
  `MANIFEST_g4.json`, aucun fichier G4 suivi par git n'a de modification.
- `graphe_g4_diff_vides` égale le nombre de fichiers de graphe G4 listés en D5 :
  tous sans diff. Le semis n'est pas rejoué, les arêtes ne sont pas recalculées,
  aucun compteur de zones n'est « amélioré ».

### SC5 — La garde durable existe, elle a été vue rougir, et l'alignement est déterministe

- `code_sortie_garde_verte` vaut **0** : la garde passe sur le dépôt réparé (D7).
- `code_sortie_garde_rouge_hors_depot` est **strictement positif** : sous
  sabotage de la déclaration d'entrée, monté hors du dépôt, la garde rougit. Le
  sabotage porte sur la déclaration, jamais sur le code de la garde (règle n° 4).
- Les deux sorties sont committées, diffèrent, et sont déclarées en couple
  `must_differ_from` (D9).
- `passes_alignement_identiques` égale le nombre de fichiers écrits par
  l'alignement : deux exécutions successives produisent des fichiers
  octet-identiques.
- `diff_apres_seconde_passe` vaut **0** : `git status --porcelain` sur
  `pipeline/geo/artifacts` est vide après la seconde passe. Un zéro de
  différences est ici l'état vert attendu, et c'est un zéro mesuré.

### SC6 — Le README ferme le constat de 019 sans rien sur-revendiquer

- La section « Constats ouverts » de `pipeline/geo/README.md` ne porte plus
  l'écart d'empreinte du littoral comme constat **ouvert** ; une mention fermée
  la remplace, disant ce qui a été réparé et comment : la déclaration d'entrée de
  G3 alignée sur le littoral que la chaîne produit, la maille non rejouée, la
  terre inchangée aux epsilon près (SC1). Le constat sur les **bornes d'intention
  de surface et de compacité** reste ouvert : ce lot ne le traite pas.
- `constats_ouverts_README` est strictement inférieur au même compte pris sur
  l'instantané pre-edit, ces deux comptes étant dérivés du fichier.
- `readme_differe_instantane` vaut **1**, le couple étant déclaré (D9).
- Aucune sur-revendication : le README n'affirme ni que le jalon E1 est clos, ni
  que le relief, le climat, les ressources, les fleuves ou les villes sont
  livrés, ni que la mer est « simulée ». La liste des non-livrés reste complète.
- Le README reste **descriptif** : il dit ce qui existe et ce que cela lit ; il
  n'adresse aucune instruction à un agent — le brief est la seule instruction, et
  `harness/tests/test_single_source_of_instruction.py` le vérifie.

### SC7 — Mesure rejouable, manifeste complet, périmètre tenu, suites vertes

- Un script committé sous
  `deliverables/measure_g3_provenance_020.py`, exécuté depuis la racine :

```
.venv/bin/python harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/measure_g3_provenance_020.py
```

  Il imprime **chaque compteur du tableau ci-dessous avec son dénominateur**, en
  lisant les artefacts et les constantes — jamais une valeur recopiée à la main.
  Un compteur sans dénominateur imprimé est irrecevable.
- `deliverables/manifest.json` déclare tous les fichiers (y compris ceux hors du
  dossier du brief, en chemins relatifs), les cinq couples `must_differ_from`,
  chaque compteur avec un `sample_size` réel — non nul et différent de la
  sentinelle — et les dérogations éventuelles avec leur commande et leur erreur.
- `valeurs_hexadecimales_citees` vaut **0** sur les fichiers de texte et de code
  produits ou modifiés par ce lot. Sont **exclus du balayage**, et pour une raison
  nommée : les artefacts JSON de la chaîne et les deux instantanés
  `pre-edit/MANIFEST_g3.json.orig` et `pre-edit/stats_g4.json.orig`, qui sont des
  copies machine d'artefacts, non des citations dans de la prose ou du code.
- `alias_python_nu` vaut **0** : aucune commande n'emploie l'alias nu de
  l'interpréteur ni un chemin `.venv/Scripts/` (règle n° 1 ; la machine est
  Linux).
- `fichiers_hors_perimetre_modifies` vaut **0**, mesuré sur `git status
  --porcelain` confronté au périmètre de D10.
- `fichiers_preuve_suivis_par_git` égale le nombre de preuves déclarées sous
  `pipeline/geo/` : `logs/` est exclu par `.gitignore`, donc les preuves y sont
  ajoutées par `git add -f`, et le suivi est prouvé par `git ls-files`.
- `tests_harness_passed_020` est rapporté avec le nombre de tests collectés pour
  dénominateur. Les `SKIP` propres à Linux (tests Unity) sont acceptés et
  déclarés. Les sorties réelles sont recopiées dans
  `deliverables/generator-log.md`.
- Registre de coût, une ligne :

```
.venv/bin/python harness/backends/ledger.py append --backend cursor \
  --brief harness/queue/briefs/020-geo-provenance-littoral-g3 \
  --event generator-run
```

  `ligne_ledger_ajoutee` vaut 1. Aucun `--audit-id` n'est requis : ce brief naît
  du constat escaladé par 019, pas d'un audit converti.

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. Régénérer, recalculer ou remplacer la maille des cellules. La terre n'a pas
   bougé (D2) : remailler pour réparer une déclaration détruirait des
   identifiants consommés par `sim/` sans nécessité mesurée.
2. Écrire dans `sim/`. Le jalon E2 est clos ; ce lot ne le rouvre pas.
3. Régénérer le graphe G4 — semis de zones, arêtes, `sea_zone_count`, `by_kind`,
   saturation de `SEA_ZONE_COUNT_MAX`. Ce lot met à jour les champs que G4
   **lit**, rien de plus. La saturation reste un constat ouvert d'un autre lot.
4. Modifier une seule valeur de `pipeline/geo/constants.py`, dans quelque sens
   que ce soit. Une borne inatteignable s'escalade.
5. Modifier `qa/checks.py`, ni ajouter une entrée à `run_g3_green` : la garde de
   ce lot est un script de preuve dédié (D7).
6. Modifier `pipeline.py`, `steps/02_coastline.py`,
   `steps/02b_corrections_1400.py` ou `steps/03_cells.py`. Une retouche de
   `steps/04_adjacency.py` n'est tolérée que sous la condition étroite de D6.
7. Obtenir l'égalité en changeant de cible — comparer le littoral à
   `MANIFEST_g2b.json` plutôt qu'aligner l'entrée de G3. C'est renommer la cible
   pour la toucher, et 019 l'interdisait déjà.
8. Ajouter `coastline_1400.json` ou `MANIFEST_g2b.json` au suivi git. Ils sont
   volumineux et régénérables ; un clone régénère G2-bis, après quoi le contrôle
   d'égalité tient.
9. Livrer le relief, le climat, les ressources, les fleuves ou les villes, ni
   déclarer le jalon E1 clos. E1 reste ouvert.
10. Réouvrir le brief 007, ni retoucher les archives des briefs 001 à 019, y
    compris `019-geo-adjacence-g4/deliverables/**`.
11. Traiter le point N1 du brief 017, les briefs de harnais, les demandes de
    fusion en cours ou les audits PROPOSED.
12. Rapporter un compteur depuis un monde vide, une liste vide ou un échantillon
    nul. Un zéro **mesuré** est légitime et se distingue d'un « non calculé »,
    dont la sentinelle est `-1` (règle n° 8).
13. Recopier une valeur hexadécimale d'empreinte dans un test, un document, un
    commentaire ou un champ `error` (règle n° 12). Les empreintes se comparent à
    l'exécution.
14. Employer l'alias nu de l'interpréteur, ni `.venv/Scripts/python.exe` (chemin
    Windows) : la machine est Linux (règle n° 1).
15. Committer, pousser, créer ou changer de branche. L'orchestrateur seul dépose.

---

## Required Counters

Un compteur sans source d'échantillon déclarée est irrecevable : la porte
mécanique refuse tout compteur dont l'échantillon est nul ou non calculé
(`no_empty_sample_pass`). Tous sont produits par
`deliverables/measure_g3_provenance_020.py`, qui les dérive des artefacts, des
constantes et de l'état du dépôt.

| nom | source de l'échantillon | dénominateur |
|---|---|---|
| `cellules_lues_g3` | cellules lues de `artifacts/cells_g3.json` | `cell_count` lu de `artifacts/stats_g3.json` (doit être égal) |
| `terre_vivante_m2` | aire de la terre du littoral vivant | 1 mesure géométrique ; doit être > 0 |
| `depassement_cellules_hors_terre_m2` | aire de la part de l'union des cellules sortant de la terre du littoral vivant | `terre_vivante_m2` ; doit être ≤ `epsilon_surface_g3_m2` |
| `terre_non_couverte_m2` | aire de terre du littoral vivant qu'aucune cellule ne couvre | `terre_vivante_m2` ; doit être ≤ `epsilon_surface_g3_m2` |
| `epsilon_surface_g3_m2` | `G3_AREA_EPS_M2` lu de `constants.py` | 1 valeur lue ; jamais un littéral du code |
| `ecart_est_serialisation` | conjonction des deux comparaisons d'aire à l'epsilon lue | 1 comparaison composée ; **doit valoir 1** |
| `code_sortie_ecart_avant` | exécution de `deliverables/check_provenance_coastline_020.py` avant réparation | 1 exécution ; **doit valoir 1** |
| `code_sortie_ecart_apres` | même commande après réparation | 1 exécution ; **doit valoir 0** |
| `cellules_actives_instantane` | identifiants lus de `deliverables/pre-edit/cell_ids_actifs.txt` | `cell_count` lu de `stats_g3.json` (doit être égal) |
| `cellules_actives_inchangees` | intersection de l'instantané et des `cell_id` de `cells_g3.json` après le lot | `cellules_actives_instantane` (doit être égal) |
| `cellules_actives_ajoutees` | identifiants présents après le lot, absents de l'instantané | `cellules_actives_instantane` ; **0 mesuré**, jamais la sentinelle |
| `cellules_actives_retirees` | identifiants de l'instantané absents après le lot | `cellules_actives_instantane` ; **0 mesuré**, jamais la sentinelle |
| `artefacts_maille_diff_vides` | `git status --porcelain` sur les quatre fichiers de maille | 4 fichiers vérifiés (doit être égal) |
| `fichiers_sim_modifies` | `git status --porcelain -- sim/` | fichiers suivis sous `sim/`, comptés par `git ls-files sim` ; **doit valoir 0** |
| `tests_sim_passed_020` | tests `PASSED` de `sim/tests/` | tests collectés dans `sim/tests/` |
| `empreinte_entree_g3_egale_vivant` | empreinte du littoral vivant calculée à l'exécution vs `MANIFEST_g3.json` `inputs.coastline_1400` | 1 comparaison ; **doit valoir 1** |
| `empreinte_vivant_egale_sortie_g2b` | même empreinte vs la sortie déclarée par `MANIFEST_g2b.json` pour ce fichier | 1 comparaison ; **doit valoir 1** |
| `sorties_g3_conformes` | entrées du bloc `outputs` de `MANIFEST_g3.json` dont l'empreinte recalculée égale la déclaration | nombre d'entrées `outputs` lues du manifeste (doit être égal) |
| `champs_manifeste_g3_modifies` | chemins de feuille différant entre `pre-edit/MANIFEST_g3.json.orig` et le manifeste publié | nombre de feuilles du manifeste publié ; **doit valoir 1** |
| `roundtrip_serialisation_neutre` | artefacts relus puis réécrits sans changement de valeur restant octet-identiques | nombre d'artefacts réécrits par l'alignement, lu de sa sortie (doit être égal) |
| `provenance_g4_egale_entree_g3` | `coastline_1400_sha_declared_by_g3` vs `MANIFEST_g3.json` `inputs.coastline_1400` | 1 comparaison ; **doit valoir 1** |
| `drapeau_egalite_manifeste_g4` | `coastline_1400_sha_equal` lu de `MANIFEST_g4.json` | 1 champ lu ; **doit valoir 1** |
| `drapeau_egalite_stats_g4` | `coastline_1400_sha_equals_g3_input` lu de `stats_g4.json` | 1 champ lu ; **doit valoir 1** |
| `sorties_g4_conformes` | entrées du bloc `outputs` de `MANIFEST_g4.json` dont l'empreinte recalculée égale la déclaration | nombre d'entrées `outputs` lues du manifeste (doit être égal) |
| `artefacts_g4_modifies_hors_liste` | fichiers G4 suivis par git portant une modification, hors `stats_g4.json` et `MANIFEST_g4.json` | fichiers G4 suivis par git, comptés par `git ls-files` ; **doit valoir 0** |
| `graphe_g4_diff_vides` | `git status --porcelain` sur les fichiers de graphe G4 listés en D5 | nombre de fichiers listés (doit être égal) |
| `code_sortie_garde_verte` | exécution de `tests/run_proof_coastline_provenance.py` sur le dépôt réparé | 1 exécution ; **doit valoir 0** |
| `code_sortie_garde_rouge_hors_depot` | même garde sur une copie hors dépôt dont la déclaration d'entrée est mutée | 1 exécution ; doit être **> 0** |
| `passes_alignement_identiques` | fichiers dont l'empreinte est identique entre deux exécutions successives de l'alignement | nombre de fichiers écrits par l'alignement, lu de sa sortie (doit être égal) |
| `diff_apres_seconde_passe` | lignes de `git status --porcelain -- pipeline/geo/artifacts` après la seconde passe | même nombre de fichiers écrits par l'alignement ; **doit valoir 0** |
| `constats_ouverts_README` | entrées de la section « Constats ouverts » du `README.md` publié | même compte sur `pre-edit/pipeline-geo-README.md.orig` ; doit être strictement inférieur |
| `readme_differe_instantane` | empreintes calculées à l'exécution du README publié et de son instantané | 1 comparaison ; **doit valoir 1** |
| `valeurs_hexadecimales_citees` | balayage de longues suites hexadécimales dans les fichiers de texte et de code produits ou modifiés par ce lot, hors artefacts JSON et hors les deux instantanés d'artefact | nombre de fichiers balayés ; **doit valoir 0** |
| `alias_python_nu` | balayage du même ensemble de fichiers | nombre de fichiers balayés ; **doit valoir 0** |
| `fichiers_hors_perimetre_modifies` | lignes de `git status --porcelain` confrontées au périmètre de D10 | nombre total de lignes de `git status --porcelain` ; **doit valoir 0** |
| `fichiers_preuve_suivis_par_git` | `git ls-files` croisé avec la liste déclarée des preuves sous `pipeline/geo/` | nombre de preuves déclarées (doit être égal) |
| `tests_harness_passed_020` | tests `PASSED` de `harness/tests/` | tests collectés dans `harness/tests/` (les `SKIP` Linux sont acceptés et déclarés) |
| `ligne_ledger_ajoutee` | dernière ligne de `harness/queue/cost-ledger.jsonl` | 1 ligne vérifiée ; **doit valoir 1** |

---

## Acceptable Waivers (si une impossibilité est invoquée)

Une impossibilité s'éprouve avant d'être invoquée : une commande et le message
d'erreur qu'elle produit, sinon ce n'est pas un constat mais un abandon (règle
durement acquise n° 9).

| affirmation d'impossibilité | commande exigée | erreur attendue |
|---|---|---|
| « la pile scientifique n'est pas installée sur cette machine » | depuis la racine : `.venv/bin/python -c "import shapely, geopandas, pyproj; print('ok')"` | le message d'erreur exact (`ModuleNotFoundError` nommant le module). Si invoqué, **aucune** condition n'est excusée : sans exécution il n'y a pas de mesure, et le lot s'arrête sur ce constat |
| « le littoral vivant est absent du disque » | depuis la racine : `.venv/bin/python harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/check_provenance_coastline_020.py` | le message d'absence et la commande de régénération qu'il nomme, avec le code de sortie **2**. **N'excuse aucune condition** : il faut régénérer par `../../.venv/bin/python tests/run_proof_g2b.py` depuis `pipeline/geo/`, puis mesurer |
| « `MANIFEST_g2b.json` est absent du disque » | même commande | même message d'absence, code de sortie **2**, même conduite : régénérer puis mesurer |
| « le littoral corrigé de 1400 ne se régénère pas » | depuis `pipeline/geo/` : `../../.venv/bin/python tests/run_proof_g2b.py` | la sortie réelle complète montrant l'échec, code de sortie inclus |
| « la mesure de géométrie contredit la planification : un débordement de cellules hors terre, ou une terre non couverte, dépasse l'epsilon lue » — **escalade** | depuis la racine : `.venv/bin/python harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/measure_g3_provenance_020.py` | la ligne imprimant `depassement_cellules_hors_terre_m2` et `terre_non_couverte_m2` avec leur dénominateur et l'epsilon **lue**, sans aucune valeur hexadécimale, et `ecart_est_serialisation` à **0** mesuré. Si invoquée : **rien n'est aligné, rien n'est remaillé**, aucun artefact n'est réécrit, le constat est escaladé au Planificateur et inscrit dans `deliverables/generator-log.md`. Le Générateur ne tranche pas seul entre « refaire la maille » et « aligner la déclaration » |
| « un rejeu de la maille hors dépôt ne rend pas les mêmes identifiants » | la commande de rejeu employée dans la copie hors dépôt, plus la comparaison de ses identifiants à `deliverables/pre-edit/cell_ids_actifs.txt` | le relevé d'écart d'identifiants (comptes ajoutés / retirés, sans hexadécimal). Si invoquée : **constat ouvert** dans le journal et dans `README.md`, aucun artefact de la copie n'est recopié dans le dépôt, et la maille committée reste en place — la géométrie actuelle la recouvre encore (SC1) |
| « le budget d'exécution n'est pas mesurable sur cette machine » | `.venv/bin/python harness/budget.py status --brief harness/queue/briefs/020-geo-provenance-littoral-g3` | la sortie contient la chaîne `UNMEASURABLE` |

Aucune autre dérogation n'est recevable. En particulier :

- « on retargete la comparaison vers `MANIFEST_g2b.json` pour faire dire 1 au
  compteur » **n'est pas une dérogation** : c'est le maquillage que 019 avait
  déjà nommé. Après ce lot, l'égalité contre l'entrée de G3 doit être **vraie**,
  ou le lot échoue.
- « `coastline_1400.json` n'est pas suivi par git » **n'est pas une
  dérogation** : le fichier se régénère, et c'est la conduite exigée.
- « il serait plus simple de rejouer la maille » **n'est pas une dérogation** :
  la mesure de D2 dit que la terre n'a pas bougé, et un rejeu toucherait des
  identifiants consommés par `sim/`.
- « la déclaration périmée est sans conséquence puisque `sim/` ne lit pas les
  manifestes » **n'est pas une dérogation** : le défaut est qu'il existe deux
  réponses à « quelle terre ? », pas qu'un lecteur précis en souffre aujourd'hui.

---

## Execution Contract

### Interpréteur et commandes

Machine : Linux (Cursor Cloud). L'interpréteur est **`.venv/bin/python`**, jamais
l'alias nu (règle n° 1), jamais `.venv/Scripts/python.exe`. Les preuves
géographiques se lancent **depuis `pipeline/geo/`** avec `../../.venv/bin/python`,
conformément à `AGENTS.md`.

Aucune étape Unity dans ce lot : `unity/run-unity.ps1` ne s'applique pas, et
`unity/` n'est ni lu ni écrit.

La régénération de G2-bis reconstruit de la géométrie réelle : elle peut prendre
de quelques dizaines de secondes à quelques minutes. Chaque exécution est **un
seul appel bloquant**. Ne jamais lancer en arrière-plan puis relire le journal
toutes les trente secondes : chaque relecture est un appel d'outil qui renvoie
tout le contexte accumulé, et c'est ainsi qu'un lot précédent a dépensé 586
appels à relire un seul fichier.

### Estimation d'appels d'outils

**Estimation : 90 appels.** Ancres réelles : un brief d'ADR a coûté environ 108
appels ; le lot 007a, réparation comprise, environ 135 ; le lot 019, environ 140.
Le présent lot est plus petit : aucun nouveau découpage géométrique, aucun semis,
aucun graphe. Il aligne des déclarations, mesure, pose une garde et met à jour un
README — un seul sous-système, un seul thème causal.

Vérification préalable, **avant tout travail de fond** :

```
.venv/bin/python harness/budget.py split-check \
  --brief harness/queue/briefs/020-geo-provenance-littoral-g3 \
  --estimated-calls 90
```

Verdict attendu : sous le seuil mécanique de 150, donc pas de `NEEDS_SPLIT`. Les
signaux imprimés à titre indicatif ne déclenchent rien : le Planificateur a déjà
jugé qu'il n'y a ici qu'un sous-système et qu'un thème.

Plafond dur : 160 appels. **Point de contrôle obligatoire à 130 appels** :

```
.venv/bin/python harness/budget.py checkpoint \
  --brief harness/queue/briefs/020-geo-provenance-littoral-g3
```

Le point de contrôle nomme ce qui est vert, ce qui reste, et l'état du dépôt — de
sorte qu'une session neuve reprenne depuis les fichiers du dépôt et ce document,
**jamais** depuis une transcription antérieure.

### Preuves committées et re-vérifiables

Tout fichier nommé dans `deliverables/manifest.json` doit être suivi par git.
`pipeline/geo/.gitignore` exclut `artifacts/` et `logs/` : les preuves de ce lot
y sont donc ajoutées par `git add -f`, comme celles des briefs 002, 007a et 019.
Le suivi est **prouvé** par `git ls-files` et compté, parce que la porte mécanique
ne vérifie pas le suivi des chemins qui sortent du dossier du brief.

`coastline_1400.json` et `MANIFEST_g2b.json` restent **hors suivi** : ils sont
volumineux et régénérables, et un clone les reconstruit par
`../../.venv/bin/python tests/run_proof_g2b.py` depuis `pipeline/geo/` avant de
rejouer la garde.

### Deliverables obligatoires

Le dossier `harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/`
(à créer par le Générateur) doit contenir :

- `manifest.json` — tous les fichiers déclarés, les cinq couples
  `must_differ_from`, tous les compteurs avec un `sample_size` réel, les
  dérogations éventuelles avec commande et erreur ;
- `check_provenance_coastline_020.py` — la commande d'écart (D8) ;
- `measure_g3_provenance_020.py` — script rejouable imprimant chaque compteur
  avec son dénominateur (SC7) ;
- `check_provenance_apres.txt` — sortie de la commande d'écart après réparation ;
- `pre-edit/check_provenance_avant.txt` — sortie de la même commande avant
  réparation ;
- `pre-edit/cell_ids_actifs.txt` — instantané des identifiants actifs (D3) ;
- `pre-edit/MANIFEST_g3.json.orig`, `pre-edit/stats_g4.json.orig`,
  `pre-edit/pipeline-geo-README.md.orig` — instantanés avant édition ;
- `generator-log.md` — journal d'exécution en **français clair** : ce qui a été
  fait, pourquoi, ce qui reste ; les sorties réelles des commandes de SC1, SC3,
  SC5 et SC7 ; tout constat ouvert énoncé sans maquillage.

### Interdictions pour le Générateur

- **Ne pas committer. Ne pas pousser. Ne créer ni changer de branche.**
  L'orchestrateur seul dépose.
- Ne pas modifier `brief.md`, `eval-rubric.md`, ni écrire `verdict.md`.
- Ne pas modifier `constants.py`, `qa/checks.py`, `pipeline.py`, ni aucun fichier
  de la liste interdite de D10.
- Ne pas rendre un contrôle vert en modifiant le contrôle.
- Ne pas recopier de valeur hexadécimale d'empreinte (règle n° 12).
- Ne pas rapporter la sentinelle `-1` pour un compteur calculé, ni `0` pour un
  compteur qui ne l'a pas été (règle n° 8).
- Ne pas prononcer la recevabilité de son propre travail.

### Fin de lot

La porte mécanique doit répondre `ACCEPT` :

```
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/020-geo-provenance-littoral-g3
```

La garde doit sortir avec le code 0, et les deux suites rester vertes :

```
.venv/bin/python -m pytest harness/tests/ -q
```

```
.venv/bin/python -m pytest sim/tests/ -q
```

Les sorties réelles sont recopiées dans le journal.

**Celui qui produit ne prononce pas la recevabilité.**
