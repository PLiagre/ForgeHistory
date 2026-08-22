# Brief 028 : visualiseur web mince (V0-B) — regarder le monde déjà photographié

**Authored**: 2026-08-22T12:30:00Z
**Author**: forge-planificateur
**Statut**: PRÊT SOUS CONDITION — ne s'exécute qu'après la fusion du lot 027
**Classement de risque**: R1 — produit borné
**Dépendance**: fusion du brief
`harness/queue/briefs/027-sim-snapshot-cellulaire-v0a/`

> **Note de transparence (contrat du Planificateur) :** le rôle signataire est
> le rôle natif du harnais `forge-planificateur`. L'acteur réel est Cursor
> Cloud (Grok 4.6 XHigh), invoqué en lecture et planification seulement.
> Cette session n'exécute pas le lot, ne rédige aucun verdict, ne modifie
> aucun code produit, et ne fusionne rien.

> **Pourquoi R1, et pas R2.** Ce lot crée un regard, pas une architecture
> nouvelle. Le contrat de données est déjà tranché par le lot 027. Le
> viewer ne décide rien du monde. Pas de secret, pas de service distant,
> pas de donnée massive, pas de faux vert antérieur. Le risque réel —
> qu'un client redevienne une seconde simulation — est fermé ici par des
> interdits mesurables, pas par un passage en R2.

À partir d'ici, **ce `brief.md` est la SEULE instruction**. Le schéma du
snapshot n'est **pas** redéfini ici : il est celui du brief 027 fusionné.

---

## Provenance

Le jalon V0 demande deux lots. Le premier photographie. Celui-ci
**regarde**. La demande
`hermes/requests/DEMANDE-20260821-visualiseur-web-v0.md` interdit au
viewer toute règle métier. Unity reste en veille.

**Dépendance dure :** le lot 027 doit être fusionné. Sans
`--snapshot-json` et sans schéma `v0a-1`, ce lot n'a rien à lire. Le
Générateur constate cette fusion **avant sa première écriture** (SC0).
Il n'exécute pas le lot 027, il n'en modifie pas le brief, et il n'en
copie pas le schéma dans ce fichier.

---

## World-Terms Requirement

**Chaîne causale.**

Des habitants ont faim dans une terre, ou non ; une terre reçoit plus
d'énergie du Soleil qu'une autre, ou non. Ces faits existent déjà dans
la photographie produite par `sim/`. Un regard humain qui veut les voir
n'a pas à les recalculer : il lit la photographie, il montre ce qui y
est, et il laisse vide ce qui n'y est pas.

Si le regard invente un zéro là où la photographie dit « absent » ou
« non calculé », il ment sur le monde. Si le regard calcule une faim, un
stock ou une mortalité, il devient une seconde simulation — le défaut
diagnostiqué n° 4. Ce lot est des yeux, pas un estomac.

**Interdit** : toute formule de production, de consommation, de
commerce, de faim, de mortalité, d'insolation ou de distance à la mer,
dans quelque langage que ce soit sous `viewer/`.

---

## Vocabulaire (expliqué une fois)

- **snapshot** : le fichier JSON produit par le lot 027. Unique source
  de données de ce lot.
- **regard / viewer** : le programme qui lit un ou deux snapshots et
  les montre. Il ne fait pas avancer le monde.
- **couche affichable** : une grandeur déjà présente dans le snapshot,
  proposée à l'œil. Une couche `absent` ou `not_consumed` se nomme
  comme indisponible ; elle ne se peint pas avec des zéros.
- **valeur incomparable** : lors d'une comparaison, au moins une des
  deux photographies n'a pas de nombre honnête pour cette cellule et
  cette grandeur (`null` ou `-1`).
- **preuve visuelle** : un dessin SVG déterministe, produit sans
  navigateur, que l'on peut regarder et dont on peut hasher les octets.

---

## Décisions de conception tranchées par le Planificateur

### D0 — Préalable : le lot 027 est fusionné

Avant toute création de fichier, le Générateur exécute :

```
.venv/bin/python -m sim --help
```

La sortie doit mentionner `--snapshot-json`. Puis :

```
.venv/bin/python -c "from sim.constants import SNAPSHOT_SCHEMA_VERSION; print(SNAPSHOT_SCHEMA_VERSION)"
```

La valeur imprimée est celle du brief 027 (`v0a-1`). Si l'une des deux
commandes échoue, **le lot s'arrête**, aucun fichier n'est écrit. Ce
n'est pas un `REJECT` du travail : c'est un lot lancé trop tôt.

Le Générateur produit ensuite, pour ses preuves, des snapshots via la
commande du brief 027. Il ne reconstruit pas l'export.

### D1 — Architecture la plus mince qui satisfait honnêtement le besoin

**Retenu : un paquet `viewer/` en bibliothèque standard, des fichiers
statiques (HTML, CSS, JavaScript sans cadre), un serveur HTTP local
pris dans la bibliothèque standard, et une preuve SVG écrite sans
navigateur.**

Justification, alternatives écartées :

| option | pourquoi écartée |
|---|---|
| Unity | en veille, ADR-0016 |
| application Node / React / bundler | dépendances, compte, secret possible, trop lourd pour 596 polygones |
| Leaflet ou carte chargée depuis un réseau | chargement externe silencieux, interdit |
| Matplotlib interactif seul | ce n'est pas un visualiseur web local au sens de la demande |
| Flask / FastAPI | dépendance tierce inutile |
| serveur qui relit `pipeline/geo/` | seconde source, interdit : le snapshot suffit |

Le JavaScript ne calcule aucune grandeur du monde : il projette des
polygones déjà fournis, colorie selon une valeur **déjà écrite**,
affiche un panneau, compare deux nombres déjà écrits.

Le module Python du viewer :

- lit un ou deux chemins de snapshot ;
- refuse une version de schéma inconnue (D3) ;
- sert les fichiers statiques **et** le ou les snapshots fournis, rien
  d'autre ;
- sait écrire la preuve SVG (D8) sans ouvrir de navigateur.

Aucun fichier sous `sim/` n'est modifié. Aucun artefact geo n'est lu
par le viewer.

### D2 — Commande locale unique

Linux :

```
.venv/bin/python -m viewer --snapshot <A> [--compare <B>] [--host 127.0.0.1] [--port 8765]
```

Windows : `py -m viewer` avec les mêmes options.

- Sans `--snapshot` : refus, code `2`, message qui dit qu'il faut une
  photographie.
- `--host` par défaut `127.0.0.1` (pas `0.0.0.0` : on n'ouvre pas la
  machine au réseau).
- `--port` par défaut `8765`. Si le port est pris : refus, code `2`,
  le port est nommé — on ne cherche pas un autre port en silence.
- La commande **bloque** tant que le serveur tourne (usage humain). Les
  tests et la preuve n'utilisent **pas** ce mode bloquant : ils
  appellent les fonctions de lecture / dessin, ou passent
  `--proof-svg <CHEMIN>` qui écrit et sort `0`.

Preuve visuelle (même module) :

```
.venv/bin/python -m viewer --snapshot <A> [--compare <B>] --proof-svg <CHEMIN.svg>
```

Écrit le SVG et sort `0`. Aucun serveur.

`viewer/README.md` documente ces deux commandes, en français clair, et
dit que le viewer ne simule rien.

### D3 — Contrat avec le schéma V0-A, sans le redéfinir

Le viewer lit `schema_version` à la racine du JSON.

- Si la valeur égale `SNAPSHOT_SCHEMA_VERSION` **importée de
  `sim.constants`** (pas recopiée en littéral dans `viewer/`) : lecture
  acceptée.
- Toute autre valeur, y compris une clé absente : **refus**, code `2`,
  message qui nomme la version vue et la version connue. Aucun dessin,
  aucun serveur.

Le viewer n'écrit pas la liste des champs du brief 027. Il **consomme**
le document : `cells`, `layers`, `cell_id`, et les grandeurs qu'il
affiche (D5). Si un champ exigé par le brief 027 manque, c'est un
snapshot illisible : refus, code `2`, champ nommé. Le viewer ne le
remplit pas.

Interdit dans `viewer/` : tout champ spatial concurrent de `cell_id`
(`province_id`, `owner`, `country`, `pays` comme clé d'index). La
province affichée, si le snapshot la porte, est une **étiquette lue**,
jamais une clé de jointure inventée par le client.

### D4 — Données : exclusivement les snapshots passés en argument

- Le serveur n'expose que : les fichiers statiques du paquet `viewer/`,
  et le ou les snapshots dont les chemins ont été donnés.
- Aucune lecture de `pipeline/geo/`, aucun téléchargement, aucune
  police, carte ou script depuis une URL externe (pas de CDN, pas de
  `http://` dans les sources statiques hors commentaires d'interdiction).
- Un chemin de snapshot qui n'existe pas : refus, code `2`.
- Deux snapshots : le second vient de `--compare`. Pas de dossier
  « magique » scanné.

### D5 — Carte, interaction, couches, comparaison

Le regard, en mode serveur, permet :

1. de voir les polygones de **toutes** les cellules du snapshot (le
   nombre est celui du fichier, jamais un littéral) ;
2. de zoomer et de se déplacer ;
3. de sélectionner une cellule et d'afficher **toutes** les grandeurs
   que le snapshot porte pour elle, y compris `null` et `-1` sous des
   libellés distincts (« absent », « non calculé ») — jamais une case
   vide qui ressemble à zéro ;
4. de choisir une couche parmi celles que D6 déclare affichables **et**
   que le snapshot porte réellement comme nombres ;
5. de comparer deux snapshots quand `--compare` est fourni : pour la
   couche active, chaque cellule montre une différence `B − A` **seulement
   si** les deux valeurs sont des nombres et qu'aucune n'est `-1`.
   Sinon la cellule est **incomparable** (D7).

Le mode `--proof-svg` dessine la même information sans interaction :
carte de la couche par défaut (D6), légende des indisponibles, et si
`--compare` est là, une seconde carte ou un calque de différences
obéissant à la même règle.

### D6 — Couches initiales et palettes

Couches **proposées**, dans cet ordre, **si** le snapshot fournit un
nombre honnête pour au moins une cellule. Les noms de champs sont ceux
déjà publiés par le brief 027 ; ce tableau n'en ajoute aucun :

| couche | champ lu dans le snapshot | palette |
|---|---|---|
| population | `population` | séquentielle, du clair au foncé (une teinte) |
| stock alimentaire | `food_stock_kg` | séquentielle, autre teinte |
| déficit alimentaire | `food_deficit_kg` | séquentielle, autre teinte |
| faim | `hunger_ticks` | séquentielle, autre teinte |
| insolation | `climate_drivers.insolation_annual_mj_m2` | séquentielle, autre teinte |
| distance à la mer | `climate_drivers.dist_sea_centroid_m` | séquentielle, autre teinte |

Les teintes sont des couleurs **fixes**, déclarées une fois dans le
viewer (constantes nommées), sans signification de « bon » ou « mauvais ».
Interdit : vert = riche, rouge = famine, ou tout barème visuel.

Couches **nommées comme indisponibles**, jamais colorées comme des
zéros :

- `layers.relief_g6.status` différent de `present` → « relief non
  disponible » ;
- `layers.resources_r1.status` différent de `present` → « gisements non
  disponibles » ;
- `layers.climate_drivers_c1.status` différent de `present` → les deux
  dernières lignes du tableau ci-dessus sont indisponibles.

La couche par défaut de la preuve SVG est `population`.

Le viewer ne propose aucune couche dont le champ n'existe pas dans le
schéma 027. Il n'invente pas de couche « production », « rendement » ou
« score ».

### D7 — Zéro, `null`, `-1` : trois signes visuels distincts

| valeur lue | dessin | texte du détail |
|---|---|---|
| nombre `0` | couleur de palette au bout bas (zéro mesuré) | `0` |
| `null` ou champ absent | hachure ou gris neutre **légendé « absent »** | `absent` |
| `-1` | hachure ou gris **différent**, légendé « non calculé » | `non calculé` |

Convertir `null` ou `-1` en `0` avant de colorier est un échec du lot,
même si la suite de tests « passe ». La comparaison (D5) n'écrit jamais
`0` pour une valeur incomparable.

### D8 — Preuve visuelle reproductible, sans navigateur propriétaire

Deux passes de :

```
.venv/bin/python -m viewer --snapshot <A> --compare <B> --proof-svg <CHEMIN>
```

avec `A` = snapshot `seed0_tick0` et `B` = snapshot `seed0_tick5`
**produits par** `.venv/bin/python -m sim --snapshot-json …` (commande
du brief 027, mêmes options que ses preuves).

- Les deux SVG ont la même empreinte SHA256, non vide.
- Un SVG de `seed0_tick0` **seul** (sans `--compare`) a une empreinte
  **différente** du SVG de comparaison.
- Les fichiers sont committés sous
  `harness/queue/briefs/028-visualiseur-web-mince-v0b/deliverables/proofs/`.
- `generator-log.md` **décrit ce que l'œil voit** (règle n° 11) : les
  596 polygones forment la fenêtre, le dégradé de population n'est pas
  plat, les cellules `absent` / `non calculé` ne ressemblent pas aux
  zéros, la comparaison ne peint pas en zéro les incomparables.

Aucun Playwright, aucun Chrome, aucun navigateur dans les conditions
de succès. Si le Générateur en ouvre un pour se rassurer, ce n'est pas
une preuve.

### D9 — Tests fonctionnels sans navigateur propriétaire

Nouveau paquet de tests `viewer/tests/` (pytest, stdlib).

**Rouges :**

1. snapshot dont `schema_version` vaut `"v0a-999"` → code `2` ;
2. remplacer un `null` de `climate_drivers` par `0` dans une copie, puis
   vérifier que le **code de présentation** (fonction testable) d'une
   cellule non jointe **sans** cette mutation rend « absent » et non
   `0` — et qu'un test refuse la conversion inverse ;
3. appeler le viewer sans `--snapshot` → code `2` ;
4. injecter une lecture de `cells_g3.json` dans une copie du code : un
   contrôle statique du lot (parcours des sources sous `viewer/`)
   **rougit** s'il trouve `pipeline/geo` ou une URL `http` ;
5. une fonction de différence qui reçoit `(-1, 4)` doit rendre
   « incomparable », pas `5` ni `0`.

**Verts :** lecture d'un snapshot 027 valide ; nombre de polygones
dessinés égal à `cell_count` du fichier ; D7 sur un échantillon ;
déterminisme D8 ; le contrôle statique de D9.4 est vert sur le code
publié ; `sim/tests/` et `harness/tests/` restent vertes.

Les fonctions de lecture, de classification (zéro / absent / non
calculé) et de différence sont des fonctions pures, testables sans
serveur.

### D10 — Périmètre de fichiers

**Autorisé (création) :**

- `viewer/` (paquet : `__init__.py`, `__main__.py`, modules de lecture
  et de preuve, fichiers statiques HTML/CSS/JS, `README.md`,
  `tests/`) ;
- `harness/queue/briefs/028-visualiseur-web-mince-v0b/deliverables/**`.

**Autorisé (modification bornée) :**

- `harness/queue/cost-ledger.jsonl` — une seule ligne ajoutée.

**Interdit :** tout fichier sous `sim/` ; tout fichier sous
`pipeline/` ; `unity/` ; `control-plane/` ; `.github/` ; `docs/adr/**` ;
`ROADMAP.md` ; `HANDOFF.md` ; `hermes/**` ; `VISION.md` ; `harness/*.py` ;
les archives des briefs 001 à 027 (y compris modifier le brief 027).

### D11 — Interdits mécaniques dans `viewer/`

Le parcours récursif des fichiers texte sous `viewer/` (hors
`deliverables` du brief) compte `0` occurrence, comme chaînes
littérales, de :

- formules ou identifiants du moteur : `FOOD_CONSUMPTION`,
  `FOOD_PRODUCTION`, `MAX_DEATH_RATE`, `TRADE_CAPACITY`,
  `mortality_remainder` **employé pour un calcul** (le lire pour
  l'afficher est permis ; s'en servir dans une multiplication ne l'est
  pas) ;
- `http://` et `https://` hors du README pour dire « n'en chargez pas ».

Le plus simple à mesurer, et c'est ce que ce lot exige :

- `urls_externes_dans_sources` vaut `0` dans `viewer/**/*.html`,
  `viewer/**/*.js`, `viewer/**/*.css` ;
- `lectures_pipeline_geo` vaut `0` dans `viewer/**/*.py` et
  `viewer/**/*.js` ;
- `imports_sim_hors_constante_de_schema` : le seul import autorisé
  depuis `sim` est `SNAPSHOT_SCHEMA_VERSION` (et, si besoin pour le
  refus, rien d'autre). Ni `tick`, ni `World`, ni `engine`.

---

## Success Conditions

### SC0 — Le lot 027 est fusionné, constaté avant toute écriture

- `option_snapshot_json_presente` vaut `1`.
- `schema_version_connue_lue` vaut `1` : `SNAPSHOT_SCHEMA_VERSION`
  s'importe et égale la version que le brief 027 a fixée.
- Si l'un manque : arrêt, zéro fichier créé par ce lot.

### SC1 — Le regard démarre localement et refuse le silence

- `--snapshot` manquant : code `2`.
- Snapshot à version inconnue : code `2`.
- `--proof-svg` sur un snapshot 027 valide : code `0`, fichier écrit.
- `viewer/README.md` documente les deux commandes de D2.

### SC2 — Toutes les cellules du snapshot sont dessinées

- `polygones_dessines` égale `cell_count` **lu du snapshot**, pas un
  littéral.
- `cellules_snapshot_non_dessinees` vaut `0`.
- La preuve SVG de D8 contient autant d'éléments de cellule (chemins
  ou polygones) que de cellules.

### SC3 — Absent et non calculé ne deviennent jamais zéro

- `conversions_null_vers_zero` vaut `0` sur les fonctions de
  classification, éprouvées par les cas de D9.
- `conversions_sentinelle_vers_zero` vaut `0`.
- `differences_incomparables_numerisees` vaut `0`.

### SC4 — Aucune simulation, aucune donnée externe, aucune clé concurrente

- `urls_externes_dans_sources` vaut `0`.
- `lectures_pipeline_geo` vaut `0`.
- `imports_sim_hors_constante_de_schema` vaut `0`.
- `cles_spatiales_concurrentes_viewer` vaut `0` (même sous-chaînes que
  le brief 027, dans les sources `viewer/`).

### SC5 — Comparaison et déterminisme visuel

- SVG A+B (tick 0 vs tick 5, même graine) : deux passes, même
  empreinte.
- SVG A seul : empreinte différente du SVG A+B.
- `generator-log.md` décrit la preuve vue (D8).

### SC6 — Suites et preuves suivies

```
.venv/bin/python -m pytest viewer/tests/ -q
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m pytest harness/tests/ -q
```

`tests_viewer_passed_028`, `tests_sim_passed_028`,
`tests_harness_passed_028` portent les collectés pour dénominateur ;
SKIP Unity Linux déclarés ; `-1` si waiver pytest. Preuves SVG et
snapshots utilisés pour la preuve sont suivis par git (les snapshots
de preuve de ce lot peuvent être régénérés par `--snapshot-json` et
**doivent** être committés sous `deliverables/proofs/`, pas recopiés
depuis le brief 027 par valeur d'empreinte).

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. Redéfinir, étendre ou « améliorer » le schéma du brief 027.
2. Recalculer une grandeur du monde.
3. Corriger G6, exécuter le brief 026, ou lire `pipeline/geo/`.
4. Modifier `sim/`.
5. Réveiller Unity.
6. Ouvrir un compte, un secret, une base, un hôte autre que
   `127.0.0.1` par défaut.
7. Charger une ressource réseau.
8. Convertir `null` ou `-1` en `0`.
9. Employer `cell_id` et une autre clé spatiale comme index parallèle.
10. Recopier une empreinte hexadécimale (règle n° 12).
11. Reprendre `596` comme seuil : le nombre se lit du snapshot.
12. Employer l'alias nu de l'interpréteur.
13. Committer, pousser, fusionner, ou rédiger un verdict.
14. Lancer un navigateur comme preuve.
15. Exécuter le lot 027.

---

## Required Counters

| nom | source | dénominateur |
|---|---|---|
| `option_snapshot_json_presente` | `--help` de `-m sim` contient `--snapshot-json` | `1` ; doit valoir `1` avant toute écriture |
| `schema_version_connue_lue` | import de `SNAPSHOT_SCHEMA_VERSION` | `1` ; doit valoir `1` |
| `code_refus_sans_snapshot` | code de `-m viewer` sans `--snapshot` | `1` ; doit valoir `2` |
| `code_refus_schema_inconnu` | code sur un JSON `schema_version=v0a-999` | `1` ; doit valoir `2` |
| `code_preuve_svg_ok` | code de `--proof-svg` sur un snapshot valide | `1` ; doit valoir `0` |
| `polygones_dessines` | chemins/polygones de cellule dans le SVG A | `cell_count` lu du snapshot A ; doit l'égaler |
| `cellules_snapshot_non_dessinees` | `cell_id` du snapshot A absents du SVG | cellules du snapshot A ; doit valoir `0` |
| `conversions_null_vers_zero` | cas de classification où `null` devient `0` | cas `null` exercés ; doit valoir `0` |
| `conversions_sentinelle_vers_zero` | cas où `-1` devient `0` | cas `-1` exercés ; doit valoir `0` |
| `differences_incomparables_numerisees` | différences qui rendent un nombre alors qu'une entrée est `null` ou `-1` | paires incomparables exercées ; doit valoir `0` |
| `urls_externes_dans_sources` | `http://` ou `https://` dans html/js/css de `viewer/` | fichiers statiques ; doit valoir `0` |
| `lectures_pipeline_geo` | sous-chaîne `pipeline/geo` dans `viewer/**/*.py` et `*.js` | fichiers ; doit valoir `0` |
| `imports_sim_hors_constante_de_schema` | imports `sim` autres que `SNAPSHOT_SCHEMA_VERSION` | imports `sim` ; doit valoir `0` |
| `cles_spatiales_concurrentes_viewer` | clés interdites dans les sources `viewer/` | fichiers ; doit valoir `0` |
| `paires_sha_svg_identiques` | deux passes du SVG A+B | `1` ; doit valoir `1` |
| `empreinte_svg_compare_differente_du_simple` | SVG A seul ≠ SVG A+B | `1` ; doit valoir `1` |
| `controles_rouges_mordants_028` | familles de D9 dont le sabotage rougit | `5` ; doit valoir `5` |
| `fichiers_preuve_suivis_par_git` | `git ls-files` croisé avec les preuves | preuves déclarées |
| `tests_viewer_passed_028` | `viewer/tests/` | collectés ; `-1` si waiver |
| `tests_sim_passed_028` | `sim/tests/` | collectés ; `-1` si waiver |
| `tests_harness_passed_028` | `harness/tests/` | collectés ; `-1` si waiver |

Script
`harness/queue/briefs/028-visualiseur-web-mince-v0b/deliverables/measure_viewer_028.py`,
depuis la racine, imprime chaque compteur et son dénominateur.

---

## Acceptable Waivers

| affirmation d'impossibilité | commande exigée | erreur attendue |
|---|---|---|
| « `--snapshot-json` n'existe pas » | `.venv/bin/python -m sim --help` | une aide **sans** `--snapshot-json`. **Blocage SC0**, pas waiver : le lot attend la fusion 027 |
| « `SNAPSHOT_SCHEMA_VERSION` n'existe pas » | `.venv/bin/python -c "from sim.constants import SNAPSHOT_SCHEMA_VERSION"` | `ImportError` ou `AttributeError`. **Blocage SC0** |
| « pytest n'est pas installé » | `.venv/bin/python -m pytest --version` | `No module named pytest`. Installable dans `.venv` ; sinon les compteurs de tests valent `-1` |
| « le port 8765 est pris » | la commande serveur sur ce port | l'erreur réelle de bind. Ce n'est **pas** un waiver des preuves : `--proof-svg` et les tests n'ont pas besoin du port. Le serveur doit refuser, pas se reporter |

---

## Execution Contract

Linux : `.venv/bin/python`. Windows : `py`. Stdlib seulement sous
`viewer/` (pytest pour les tests). Pas d'Unity.

**Estimation : `120` appels.** Un sous-système neuf (`viewer/`) mais un
seul thème (lire et montrer). Sous `150`.

```
.venv/bin/python harness/budget.py split-check --brief harness/queue/briefs/028-visualiseur-web-mince-v0b --estimated-calls 120
```

### Deliverables

Sous `harness/queue/briefs/028-visualiseur-web-mince-v0b/deliverables/` :

- `manifest.json` — `files[]` (couple `must_differ_from` : SVG A seul ↔
  SVG A+B), `counters[]`, `waivers[]` ;
- `generator-log.md` — y compris la description **vue** des SVG ;
- `measure_viewer_028.py` ;
- `proofs/snapshot_a.json`, `proofs/snapshot_b.json` (produits par
  `-m sim --snapshot-json`, pas fabriqués à la main) ;
- `proofs/carte_population.svg`, `proofs/carte_population_b.svg`
  (seconde passe), `proofs/carte_comparaison.svg`,
  `proofs/carte_comparaison_b.svg`.

### Interdictions pour le Générateur

Pas de verdict, pas d'édition des briefs, pas de commit, pas de
fusion, pas d'exécution du lot 027, pas de modification de `sim/`.

### Fin de lot

SC0 constatée, `--proof-svg` à `0`, SC1–SC6 couvertes par des
compteurs reconstruits, cinq familles rouges de D9 mordantes,
deliverables prêts pour l'orchestrateur.

---

## Registre de coût

```
.venv/bin/python harness/backends/ledger.py append --backend cursor --brief harness/queue/briefs/028-visualiseur-web-mince-v0b --event generator-run
```
