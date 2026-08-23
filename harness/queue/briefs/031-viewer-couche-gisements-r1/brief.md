# Brief 031 : le viewer montre les gisements photographiés (R1) — regarder, jamais recalculer

**Authored**: 2026-08-23T09:50:00Z
**Author**: forge-planificateur
**Statut**: **BLOQUÉ TANT QUE le lot 030 n'est pas fusionné** — voir « Bloqué
tant que » ci-dessous.
**Classement de risque**: R1 — produit borné

> **Note de transparence (contrat du Planificateur) :** le rôle signataire est
> le rôle natif du harnais `forge-planificateur`. L'acteur réel est Fable
> (Claude), en session Planificateur, saisi pour préparer la file Hermes après
> la fusion de #126. Cette session n'exécute pas le lot, ne commite pas le
> code produit, ne lance pas Cursor, ne rédige aucun verdict et ne fusionne
> rien.

> **Pourquoi R1, et pas R2.** Ce lot ajoute une couche de lecture au regard
> mince déjà fusionné (brief 028). Il ne crée aucun invariant nouveau : le
> viewer lit un snapshot, ne recalcule rien, ne lit pas `pipeline/geo/`, et
> distingue déjà zéro, `null` et `-1`. La discipline visuelle sur la classe
> de richesse (jamais une grandeur) est déjà tranchée par D7 du brief 026 et
> le contrôle `R1-G` ; ce lot l'applique au regard, il ne la décide pas.

À partir d'ici, **ce `brief.md` est la SEULE instruction** (voir `CLAUDE.md`
› Single Source of Instruction).

---

## Bloqué tant que

Ce lot dépend d'une seule chose : **le lot 030 est fusionné**, c'est-à-dire
que la photographie du monde consomme la couche des gisements. Le Générateur
le constate **avant sa première action**, par des commandes (SC0) :

```
.venv/bin/python -c "from sim.constants import SNAPSHOT_SCHEMA_VERSION; print(SNAPSHOT_SCHEMA_VERSION)"
.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json /tmp/v0a2_check.json
.venv/bin/python -c "import json; print(json.load(open('/tmp/v0a2_check.json'))['layers']['resources_r1']['status'])"
```

Si la version imprimée est encore `v0a-1`, ou si le statut imprimé n'est pas
`present`, le lot s'arrête ici, ne produit aucun fichier, et le signale. Le
Générateur ne modifie **jamais** `sim/` pour se débloquer lui-même : produire
la couche est le travail du lot 030.

---

## Provenance

- **ADR-0016** : `sim/` est le produit vivant ; le viewer est un regard.
- **Brief 028** (`harness/queue/briefs/028-visualiseur-web-mince-v0b/brief.md`) :
  le regard mince — architecture, commande locale, preuve SVG, distinction
  zéro / `null` / `-1`, interdits mécaniques dans `viewer/`. Ce lot s'y
  ajoute, il ne le refait pas.
- **Brief 030** (`harness/queue/briefs/030-sim-lit-gisements-r1/brief.md`) :
  la forme exacte de la couche `resources_r1` et de la clé `resources` d'une
  cellule dans le schéma `v0a-2`. Ce lot la lit, il ne la redéfinit pas.
- **Brief 026, D7 (capture) et contrôle `R1-G`** : la classe de richesse est
  un nom, jamais un nombre — si elle se montre, c'est par une **forme de
  marqueur ou un libellé**, jamais par une taille, un rayon, une opacité ou
  une intensité. Cette règle, écrite pour la capture du pipeline, s'applique
  ici au regard pour la même raison : encoder une classe par une grandeur
  visuelle la rend numérique à l'œil.
- **Demande propriétaire** `hermes/requests/DEMANDE-20260821-visualiseur-web-v0.md`
  (CLOSED) : le visualiseur « est conçu pour recevoir progressivement
  relief, climat, population, nourriture, famine, **ressources**… ». Montrer
  la couche des gisements est donc déjà autorisé sur le principe ; seul
  l'ordre d'exécution restait à fixer.

---

## World-Terms Requirement

**Chaîne causale.**

Le monde sait désormais (lot 030) ce que sa terre donne. Un propriétaire qui
regarde la carte doit pouvoir le voir : quelles cellules portent un
gisement, de quelle nature, de quelle classe — et distinguer d'un coup d'œil
une terre **regardée et vide** (liste vide, mesurée) d'une couche **non
consommée** (`null`, absence déclarée).

Ce lot montre. Il ne décide rien : pas de « richesse » colorée en dégradé,
pas de score de cellule, pas de tri des gisements par importance. Une carte
qui note ses cases est un barème déguisé — la forme exacte de ce que le
contre-exemple `terrain_endowment.json` du jeu hérité faisait.

**Interdit** : toute conséquence visuelle calculée (« si gisement alors
cellule plus visible »). La couche montre des faits, l'œil en tire ses
propres conclusions.

---

## Vocabulaire (expliqué une fois)

- **couche du regard** : un mode d'affichage que l'utilisateur choisit parmi
  celles que le snapshot rend disponibles. Le viewer n'affiche une couche
  que si le snapshot la porte (`present`) ; sinon il dit pourquoi.
- **marqueur catégoriel** : un signe visuel qui distingue des **catégories**
  (formes différentes, libellés) sans exprimer de grandeur (ni taille, ni
  opacité, ni intensité croissante).

---

## Ce qui existe déjà, et que ce lot lit sans le refaire

Vérifié sur le dépôt au moment de l'écriture (constats de contexte, jamais
des seuils — règle n° 2) :

- `viewer/snapshot_loader.py` : valide la version en la comparant à
  `SNAPSHOT_SCHEMA_VERSION` **lu** de `sim/constants.py` ; il acceptera donc
  `v0a-2` dès la fusion du lot 030 sans modification.
- `viewer/classify.py`, `viewer/server.py`, `viewer/static/`,
  `viewer/svg_proof.py` : le regard mince du brief 028 — couches
  population / nourriture / famine, sélection d'une cellule, comparaison de
  deux snapshots, preuve SVG sans navigateur.
- `viewer/README.md` : les trois commandes (preuve SVG, comparaison, regard
  local). Ce lot les conserve à l'identique.
- Le viewer ne lit **jamais** `pipeline/geo/` et n'a aucune dépendance
  tierce : bibliothèque standard seulement (brief 028, D11). Ce lot ne
  change rien à ces deux règles.

---

## Décisions de conception tranchées par le Planificateur

### D1 — Une couche « gisements », lue du snapshot seulement

Le viewer gagne une couche « gisements » :

- disponible si et seulement si `layers.resources_r1.status == "present"`
  dans le snapshot chargé ;
- si la couche n'est pas `present`, le sélecteur la montre **désactivée avec
  la raison lue du snapshot** (`absent` ou `not_consumed`) — jamais masquée
  en silence, jamais simulée ;
- les données affichées viennent **exclusivement** de la clé `resources` des
  cellules du snapshot. Le viewer ne lit ni `pipeline/geo/artifacts/`, ni
  `data/resources_1400.json`, ni aucune autre source.

### D2 — Ce que la couche montre, et comment

1. **Cellule dotée / cellule vide / couche non consommée** : trois états
   visuellement distincts, dans la continuité de la règle « zéro, `null`,
   `-1` » du brief 028 (D7) : une cellule à `resources: []` est une terre
   regardée et vide ; une cellule à `resources: null` appartient à une
   couche non consommée et se montre comme telle.
2. **La nature du gisement** (`resource`) se distingue par un **marqueur
   catégoriel** : une couleur ou une forme par nature, la légende listant
   les natures **lues des données affichées**, jamais une liste codée en
   dur dans le viewer.
3. **La classe de richesse** (`richness_class`) se montre **uniquement** par
   un libellé ou une forme de marqueur — jamais par une taille, un rayon,
   une opacité, une intensité ou une position dans un dégradé (Provenance).
4. **La sélection d'une cellule** liste ses gisements : `id`, `resource`,
   `richness_class`, tels quels, dans l'ordre du snapshot. Aucun tri « par
   importance », aucun cumul, aucun compte pondéré.

### D3 — Aucune grandeur dérivée

Le viewer ne calcule **aucune** valeur à partir des gisements : pas de
« nombre de gisements » transformé en couleur de cellule, pas de densité,
pas de score. Le seul dénombrement autorisé est le libellé textuel exact du
contenu d'une cellule sélectionnée et de la légende (natures présentes dans
le snapshot). Une cellule dotée se distingue d'une cellule vide par un état
binaire (dotée / non dotée), pas par une échelle.

### D4 — Preuve SVG et comparaison

- `--proof-svg` sait rendre la couche « gisements » : une preuve SVG neuve,
  produite depuis un snapshot `v0a-2`, montre les cellules dotées et leurs
  marqueurs par nature.
- Couple `must_differ_from` déclaré dans `deliverables/manifest.json` :
  la preuve SVG **avec** couche gisements ↔ la preuve SVG du même snapshot
  **sans** cette couche (le rendu par défaut). Deux fichiers identiques
  signifieraient que la couche ne dessine rien.
- La comparaison de deux snapshots (`--compare`) reste inchangée ; si les
  deux snapshots diffèrent sur `resources`, l'écart est signalé comme les
  autres écarts de cellule — sans pondération.

### D5 — Tests rouges d'abord

`viewer/tests/` reçoit des tests neufs, en bibliothèque standard, chaque
famille prouvant d'abord qu'elle sait rougir (règle n° 4), par sabotage sur
des copies en mémoire ou en répertoire temporaire :

1. **couche disponible** : un snapshot `present` rend la couche
   sélectionnable ; un snapshot `absent` / `not_consumed` la rend désactivée
   avec la raison ;
2. **vide contre null** : `[]` et `null` produisent deux rendus distincts ;
3. **classe jamais une grandeur** : le SVG produit ne contient aucun
   attribut de taille, rayon ou opacité qui varie selon `richness_class`
   (vérification sur le document SVG produit, pas sur le code) ;
4. **rien d'inventé** : un snapshot sans la clé `resources` sur une cellule
   (schéma cassé) est refusé par le chargeur, jamais complété ;
5. **déterminisme visuel** : deux rendus SVG du même snapshot sont
   byte-identiques.

### D6 — Périmètre de fichiers

**Autorisé (modification)** : `viewer/classify.py`, `viewer/server.py`,
`viewer/svg_proof.py`, `viewer/static/**`, `viewer/README.md` (une section
courte), `viewer/tests/**` (fichiers neufs).

**Autorisé (création)** :
`harness/queue/briefs/031-viewer-couche-gisements-r1/deliverables/**`.

**Interdit** : `viewer/snapshot_loader.py` sauf nécessité prouvée par un
test rouge (le chargeur lit déjà la version depuis la constante — le
modifier sans preuve serait du zèle) ; tout fichier sous `sim/**` ;
`pipeline/geo/**` ; `unity/**` ; `harness/*.py` ; `harness/pipeline/**` ;
`docs/**` ; `VISION.md` ; `ROADMAP.md` ; `HANDOFF.md` ; `hermes/**` ;
`.github/**` ; les répertoires des briefs 001 à 030.

---

## Success Conditions

### SC0 — Le lot 030 est fusionné, constaté avant toute écriture

- `schema_version_est_v0a2` vaut `1` et `couche_r1_present` vaut `1`,
  constatés par les trois commandes de « Bloqué tant que ».
- Sinon, le lot s'arrête sans produire aucun fichier.

### SC1 — La couche se montre quand elle existe, se déclare quand elle manque

```
.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json /tmp/v0a2.json
.venv/bin/python -m viewer --snapshot /tmp/v0a2.json --proof-svg /tmp/carte_gisements.svg
```

- Code de sortie `0` ; la preuve SVG contient la couche gisements.
- `cellules_dotees_dessinees` égale le nombre de cellules du snapshot dont
  `resources` est une liste non vide — recompté depuis le snapshot, jamais
  depuis le viewer.
- Sur un snapshot dont la couche n'est pas `present` (preuve `sans_r1` du
  lot 030, ou copie sabotée hors dépôt) : la couche est désactivée, la
  raison affichée est le statut lu, et le viewer sort en code `0` sans la
  dessiner.

### SC2 — Trois états distincts, aucune grandeur

- `etats_visuels_distincts` vaut `3` : dotée / vide mesurée / couche non
  consommée produisent trois rendus distincts (prouvé par les tests des
  familles 1 et 2).
- `attributs_de_grandeur_par_classe` vaut `0` : dans le SVG produit, aucun
  attribut de taille, rayon ou opacité ne varie selon `richness_class`
  (famille 3).
- La légende des natures est dérivée des données du snapshot :
  `natures_en_dur_dans_le_viewer` vaut `0` (aucune valeur de nature de
  ressource en chaîne littérale dans le code du viewer).

### SC3 — Le viewer ne calcule rien et ne lit rien d'autre

- `lectures_pipeline_geo_dans_viewer` vaut `0` : aucun chemin
  `pipeline/geo` dans le code du viewer (hors commentaires existants du
  brief 028, comptés à l'identique de l'instantané pré-édition).
- `grandeurs_derivees_des_gisements` vaut `0` : aucun score, densité,
  cumul pondéré ou tri par classe dans le code ni dans le rendu.
- La sélection d'une cellule liste `id`, `resource`, `richness_class` tels
  quels (famille 4 : un snapshot au schéma cassé est refusé, jamais
  complété).

### SC4 — Déterminisme visuel, suites vertes, preuves committées

- `rendus_svg_identiques` vaut `1` : deux rendus du même snapshot sont
  byte-identiques (famille 5).
- Le couple `must_differ_from` de D4 est déclaré et vérifié : la preuve avec
  couche diffère de la preuve sans couche.
- Les suites restent vertes, dénominateurs rapportés :

```
.venv/bin/python -m pytest viewer/tests/ -q
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m pytest harness/tests/ -q
```

- `viewer/README.md` documente la couche en une section courte : elle
  montre présence, nature et classe **lues du snapshot**, et rien d'autre.
- `controles_rouges_mordants` vaut `5` sur `5` (journal des familles de D5
  dans `deliverables/generator-log.md`).
- Règle n° 11 : la preuve SVG est **regardée et décrite** dans
  `deliverables/generator-log.md` — les gisements doivent apparaître aux
  endroits attendus de la fenêtre pilote, aucun marqueur en mer, et le
  journal dit par quel moyen la classe est montrée (libellé ou forme).

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. Recalculer, dériver ou pondérer quoi que ce soit à partir des gisements —
   ni score de cellule, ni densité, ni tri par importance, ni conversion de
   la classe en nombre ou en grandeur visuelle.
2. Lire `pipeline/geo/**` ou toute source autre que le snapshot passé en
   argument.
3. Modifier `sim/**` — y compris pour « aider » la couche à exister : c'est
   le lot 030.
4. Introduire une dépendance tierce ou un chargement réseau dans `viewer/`.
5. Consommer le relief G6, inventer une température ou une précipitation,
   publier une quantité de ressource.
6. Toucher aux briefs 001 à 030, à `VISION.md`, à `docs/rules/**`, à
   `ROADMAP.md`, à `HANDOFF.md`, à `hermes/**`, à `.github/**`.
7. Réactiver Unity ou CityLab, ou un `mode: full_auto`.
8. Committer, pousser, créer ou changer de branche, ni fusionner
   (ADR-0014).
9. Recopier une valeur d'empreinte dans un document (règle n° 12), employer
   l'alias nu de l'interpréteur (règle n° 1), ou rapporter un compteur
   manqué comme un zéro (règle n° 8 : sentinelle `-1`).

---

## Required Counters

| nom | source d'échantillon | dénominateur |
|---|---|---|
| `schema_version_est_v0a2` | constante lue par la première commande de SC0 | `1` vérification ; doit valoir `1` |
| `couche_r1_present` | statut lu du snapshot de contrôle de SC0 | `1` vérification ; doit valoir `1` |
| `cellules_dotees_dessinees` | marqueurs de cellules dotées dans la preuve SVG | cellules du snapshot à liste `resources` non vide, recomptées du snapshot ; doit l'égaler |
| `etats_visuels_distincts` | rendus produits par les familles 1 et 2 | `3` états ; doit valoir `3` |
| `attributs_de_grandeur_par_classe` | attributs SVG de taille/rayon/opacité corrélés à `richness_class` | attributs balayés dans la preuve SVG ; doit valoir `0` |
| `natures_en_dur_dans_le_viewer` | valeurs de nature de ressource en chaîne littérale dans le code du viewer | fichiers de `viewer/` hors tests ; doit valoir `0` |
| `lectures_pipeline_geo_dans_viewer` | occurrences de chemins `pipeline/geo` dans le code du viewer, comparées à l'instantané pré-édition | fichiers de `viewer/` hors tests ; doit valoir `0` en écart |
| `grandeurs_derivees_des_gisements` | scores, densités, cumuls pondérés ou tris par classe trouvés dans le code ou le rendu | fichiers et preuve balayés ; doit valoir `0` |
| `rendus_svg_identiques` | empreintes de deux rendus du même snapshot | `1` paire ; doit valoir `1` |
| `preuves_svg_differentes` | couple `must_differ_from` de D4 | `1` comparaison ; doit valoir `1` |
| `controles_rouges_mordants` | familles de D5 ayant rougi sous sabotage | `5` |
| `tests_viewer_passed_031` | tests réussis de `viewer/tests/` | tests collectés ; sentinelle `-1` si le provisionnement échoue, jamais `0` |
| `tests_sim_passed_031` | tests réussis de `sim/tests/` | tests collectés ; sentinelle `-1` idem |
| `tests_harness_passed_031` | tests réussis de `harness/tests/` | tests collectés ; sentinelle `-1` idem |

Un script committé sous
`harness/queue/briefs/031-viewer-couche-gisements-r1/deliverables/measure_r1_031.py`,
exécuté depuis la racine avec `.venv/bin/python`, imprime chaque compteur
avec son dénominateur, dérivé des fichiers — jamais une valeur recopiée à la
main.

---

## Acceptable Waivers (si une impossibilité est invoquée)

| affirmation d'impossibilité | commande exigée | erreur attendue |
|---|---|---|
| « le snapshot est encore en v0a-1 » | `.venv/bin/python -c "from sim.constants import SNAPSHOT_SCHEMA_VERSION; print(SNAPSHOT_SCHEMA_VERSION)"` | la sortie `v0a-1`. **Ce n'est pas un waiver, c'est le blocage nominal** (SC0) : le lot 030 n'est pas fusionné, le Générateur s'arrête sans produire aucun fichier |
| « la couche n'est pas `present` sur ce dépôt » | les trois commandes de SC0 | un statut `absent` ou `not_consumed` imprimé. Même blocage nominal : escalade, pas de contournement |
| « le paquet de test n'est pas installé » | `.venv/bin/python -m pytest --version` depuis la racine | `No module named pytest`. Outillage : le Générateur peut l'installer ; en cas d'échec, compteurs de suites à `-1`, consigné dans `deliverables/generator-log.md` |

---

## Execution Contract

### Interpréteur et commandes

Sur cette machine Linux, l'interpréteur est `.venv/bin/python` depuis la
racine. L'alias nu est interdit (règle n° 1). Aucune commande n'a besoin
d'Unity, du réseau ou d'une pile tierce : le viewer reste en bibliothèque
standard.

### Estimation d'appels d'outils

**Estimation du Planificateur : `80` appels d'outils.** Sous le seuil de
`150` et l'arrêt à `160`. Ancre : le lot 028 a construit le viewer entier ;
ce lot ajoute une couche de lecture, cinq familles de tests et une preuve
SVG. À vérifier avant génération :

```
.venv/bin/python harness/budget.py split-check \
  --brief harness/queue/briefs/031-viewer-couche-gisements-r1 \
  --estimated-calls 80
```

### Preuves committées et re-vérifiables

Les preuves SVG et les instantanés pré-édition vivent sous
`deliverables/` de ce brief, suivis par git. Jamais `git add -A`.

### Deliverables obligatoires

Sous `harness/queue/briefs/031-viewer-couche-gisements-r1/deliverables/` :

- `manifest.json` — `files[]` (avec le couple `must_differ_from` de D4),
  `counters[]` (valeur, `sample_size`, commande), `waivers[]` ;
- `generator-log.md` — en français clair, avec la description **vue** de la
  preuve SVG (règle n° 11) et le journal des cinq rouges ;
- `measure_r1_031.py` — le script de reconstruction des compteurs ;
- `pre-edit/` — instantanés des fichiers de `viewer/` modifiés ;
- `proofs/` — la preuve SVG avec couche, la preuve sans couche, et le
  snapshot d'entrée employé.

### Interdictions pour le Générateur

Il ne prononce jamais la recevabilité de son propre travail, ne rédige aucun
`verdict.md`, ne modifie ni `brief.md` ni `eval-rubric.md`, ne commite pas,
ne pousse pas, ne crée ni ne change de branche, et ne fusionne rien
(ADR-0014). Unity n'est jamais lancé.

### Fin de lot

Le lot est terminé quand SC0 est constatée, que les commandes de SC1 sortent
en code `0`, que les cinq conditions (SC0 à SC4) sont couvertes par des
compteurs reconstruits, et que les deliverables sont committés par
l'orchestrateur.

---

## Registre de coût

Une ligne, sans `--audit-id` :

```
.venv/bin/python harness/backends/ledger.py append --backend cursor \
  --brief harness/queue/briefs/031-viewer-couche-gisements-r1 \
  --event generator-run
```
