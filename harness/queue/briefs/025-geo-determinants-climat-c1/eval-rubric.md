# Eval Rubric — Brief 025 : les déterminants physiques du climat (C1)

**Authored**: 2026-08-20T21:15:00Z
**Author**: forge-planificateur
**Amendé le**: 2026-08-21 — amendement 001, par le Planificateur, sur décision
du propriétaire, **après la production du lot et avant sa fusion**. Deux
points seulement : un dénominateur factuellement faux (les branches
`--source`) et l'endroit où cinq compteurs se lisent. Aucune barre n'est
abaissée — le détail, et pourquoi ce n'est pas un assouplissement d'après
coup, est dans `amendment-001-branches-source-et-compteurs-sc1.md`. Les
passages amendés portent la marque **[A1]**.

Ce document est rédigé par le Planificateur AVANT tout code.
L'Évaluateur l'applique sans le modifier.
Voir `docs/rules/harness-roles.md` et `docs/rules/simulation-principles.md`.

---

## Guide de lecture

Pour chaque condition du brief :

- **Vérification** : commandes rejouables, depuis la racine avec
  `.venv/bin/python`, ou depuis `pipeline/geo/` avec
  `../../.venv/bin/python`. Jamais l'alias nu de l'interpréteur (règle n° 1).
- **Reconstruction indépendante** : l'Évaluateur re-dérive la valeur
  lui-même depuis les fichiers du dépôt, sans reprendre un nombre du
  manifeste ni du journal du Générateur.
- **Contre-preuve disqualifiante** : sabotage monté par l'Évaluateur dans une
  copie de travail **hors du dépôt**. Si le contrôle reste vert sous
  sabotage, la condition n'est pas satisfaite.
- **Résultat attendu** : ce que le Générateur doit avoir produit.

Vocabulaire : voir la section « Vocabulaire » du brief — non reproduit ici
(Single Source of Instruction).

**[A1] Où se lit un compteur.** L'emplacement autoritaire de **tout** compteur
de ce lot est `deliverables/manifest.json` › `counters[]`, avec sa valeur, sa
`sample_size` et la commande qui l'a produite. Les artefacts de
`pipeline/geo/artifacts/` portent des **faits du monde** ; quand une condition
ci-dessous en nomme un (par exemple `cellules_par_saut` de `stats_c1.json`),
c'est ce fait-là qu'elle désigne, et la lecture du compteur reste au
manifeste. Un compteur qu'on ne trouve pas dans un artefact n'est pas un
compteur manquant : il est à son domicile. Un compteur qu'on ne trouve **ni**
au manifeste, **ni** dans `logs/v1_080_qa.json`, **ni** reconstructible depuis
les artefacts est, lui, un compteur manquant.

**Avertissement transversal.** Le brief cite des mesures de contexte prises
par le Planificateur (`596` cellules, `372` littorales, `21` îles lacustres,
`11 444.2` et `7 149.7` MJ/m²/an, les médianes par classe de sauts). Ce sont
des **constats**, pas des cibles. Un contrôle qui s'y compare est un contrôle
qui nomme sa propre référence (règle n° 2) et doit être rejeté comme tel,
même s'il est vert.

---

## Condition 1 — L'astronomie (`C1-B`, `C1-C`)

**Vérification** :
```
cd pipeline/geo && ../../.venv/bin/python tests/run_proof_c1.py
```
Lire `C1-B` et `C1-C` dans `logs/v1_080_qa.json`, puis
`ecretages_polaires_total` dans `artifacts/stats_c1.json`.

**[A1] Où lire les cinq compteurs de SC1.** `inversions_insolation_latitude`,
`egalites_insolation_hors_tolerance`,
`paires_consecutives_au_dessus_du_seuil`,
`cellules_jour_ete_non_superieur_hiver` et
`inversions_amplitude_jour_latitude` se lisent dans
`deliverables/manifest.json` › `counters[]` — chacun avec sa valeur, sa
`sample_size` et la commande qui l'a produite. C'est leur emplacement
autoritaire (SC1 du brief). Le rapport `logs/v1_080_qa.json` les corrobore
dans les chaînes `detail` de `C1-B` et `C1-C`, écrites par le contrôle
lui-même à l'exécution ; les deux lectures doivent raconter la même chose, et
un écart entre elles est un fait à consigner.

**Ne pas les chercher dans `artifacts/stats_c1.json` : ils n'y sont pas, et
c'est voulu.** Cet artefact décrit ce que le monde est ; ces cinq nombres
disent ce qu'un contrôle a trouvé. Leur absence de `stats_c1.json` n'est donc
**pas** un défaut, et **exiger** leur présence là serait exiger un second
domicile pour une valeur qui en a déjà un (principe n° 1). En revanche,
`ecretages_polaires_total`, `coastal_cell_count_derive` et les distributions
sont bien des faits du monde et restent dans `stats_c1.json`.

**Le premier point à vérifier n'est pas que les contrôles sont verts, c'est
qu'ils portent sur quelque chose.** `paires_consecutives_au_dessus_du_seuil`
doit être publié — aux deux emplacements ci-dessus — et strictement positif :
c'est l'échantillon sur lequel la seconde moitié de `C1-B` et de `C1-C`
s'applique. S'il est absent ou nul, les deux contrôles passent sur un
ensemble vide et ne prouvent rien (règle n° 6). Le recompter soi-même depuis
les latitudes de `cells_g3.json` et `C1_MONOTONE_DLAT_DEG` **lu de
`constants.py`** : c'est cette reconstruction qui tranche, jamais le nombre
publié.

**Reconstruction indépendante** : réimplémenter la formule de la décision D3
du brief, **depuis le brief et non depuis le code du Générateur**, pour un
échantillon d'au moins vingt cellules choisies dans
`artifacts/cells_g3.json` en couvrant toute la plage de latitudes ;
comparer l'insolation annuelle et les deux durées de jour aux valeurs
publiées, à la précision de `C1_INSOLATION_DECIMALS` et
`C1_DAYLIGHT_DECIMALS` **lues de `constants.py`** (jamais un littéral en dur
dans le test de l'Évaluateur). Vérifier ensuite, sur l'artefact entier trié
par latitude croissante, qu'aucune **paire consécutive** ne présente
d'inversion — c'est la formulation exacte de `C1-B` et `C1-C` (décision D5 du
brief), et l'Évaluateur applique celle-là, pas une variante plus large ou
plus étroite de son cru.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, échanger les
valeurs `insolation_annual_mj_m2` d'une cellule du sud et d'une cellule du
nord — `C1-B` doit rougir. Remplacer toutes les insolations par une même
constante — `C1-B` doit rougir sur sa seconde moitié (égalités malgré un
écart de latitude suffisant), preuve que le contrôle refuse un champ
constant et pas seulement une inversion. Porter la durée de jour d'hiver
d'une cellule au-dessus de celle d'été — `C1-C` doit rougir.

**Résultat attendu** : `C1-B` et `C1-C` verts, chacun avec un `red_proof` non
vide ; `ecretages_polaires_total` présent et mesuré, quelle que soit sa
valeur.

---

## Condition 2 — Les distances à la mer et la littoralité (`C1-D`)

**Vérification** : même exécution, lire `C1-D`, puis
`coastal_cell_count_derive`, `coastal_cell_count_g4`,
`ecart_littoralite_c1_vs_g4`, `cellules_littorales_hors_epsilon`,
`contact_ponctuel_sans_arete_land_sea` et `zones_de_mer_inconnues`.

**Reconstruction indépendante** :

1. Recompter soi-même les cellules portant au moins une arête
   `kind == "land-sea"` dans `artifacts/adjacency_g5.json`, et confronter au
   `coastal_cell_count` de `artifacts/stats_g4.json`. Les deux doivent
   coïncider.
2. **Vérifier que le code d'étape ne lit pas `coastal_cell_ids`** : la
   littoralité doit être redérivée (D4). Un `grep` sur
   `steps/c1_climate_drivers.py` suffit à trancher — s'il lit cette liste, la
   condition est en échec même si le compte est juste, parce que le compteur
   ne dérive plus (règle n° 3).
3. Pour un échantillon d'au moins dix cellules dont au moins trois littorales
   et trois profondément continentales, recalculer soi-même la distance
   minimale entre le polygone de la cellule et les polygones de
   `artifacts/sea_zones_g4.json`, en EPSG:3035, et comparer à
   `dist_sea_edge_m` et `dist_sea_centroid_m`.
4. Vérifier que chaque `nearest_sea_zone_id` publié existe dans
   `sea_zones_g4.json`, et que la règle de départage employée est bien « le
   plus petit `zone_id` gagne » (D4) sur au moins un cas d'égalité, s'il en
   existe un.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, porter
`dist_sea_edge_m` d'une cellule littorale à une valeur nettement supérieure à
l'epsilon — `C1-D` doit rougir. Puis, dans une autre copie, ramener
`dist_sea_edge_m` d'une cellule **non** littorale sous l'epsilon — `C1-D`
doit rougir aussi. Les deux sens sont exigés : un contrôle qui ne rougit que
dans un sens est trop grossier (règle n° 6).

**Résultat attendu** : `C1-D` vert, `ecart_littoralite_c1_vs_g4` nul,
`cellules_littorales_hors_epsilon` nul,
`contact_ponctuel_sans_arete_land_sea` mesuré et, s'il est non nul,
accompagné d'une escalade écrite plutôt que d'un contournement.

---

## Condition 3 — Les deux dérivations de continentalité concordent (`C1-E`) — la condition centrale de ce brief

**Vérification** : même exécution, lire `C1-E`, puis `cellules_sans_hops`,
`cellules_atteintes_par_strait_seulement`,
`classes_de_sauts_non_monotones`, `cellules_centroide_hors_polygone` et
`violations_bord_vs_centroide`.

**Reconstruction indépendante — la plus importante de ce brief** :

1. Refaire soi-même le parcours en largeur depuis les cellules littorales,
   sur les arêtes `land-land` **et** `strait` de `adjacency_g5.json`, et
   comparer la répartition par nombre de sauts à
   `cellules_par_saut` de `stats_c1.json`.
2. Refaire le même parcours **sans** les arêtes `strait`, et vérifier que
   l'écart entre les deux comptes de cellules atteintes est exactement
   `cellules_atteintes_par_strait_seulement`. Vérifier que ces cellules-là
   sont bien des cellules ne portant aucune arête `land-land`, en les
   nommant.
3. Recalculer les médianes de `dist_sea_centroid_m` par classe de sauts et
   vérifier qu'elles croissent strictement dans l'ordre des classes non
   vides. C'est le cœur du lot : deux méthodes indépendantes — une géométrie
   et un graphe — doivent raconter la même histoire.
4. Vérifier `violations_bord_vs_centroide` sur les cellules à centroïde
   intérieur, et que `cellules_centroide_hors_polygone` a bien été mesuré et
   non supposé nul.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, déplacer une
cellule de la classe `0` vers la classe la plus continentale sans toucher sa
distance — la médiane de la classe visée doit chuter et `C1-E` doit rougir.
Retirer les arêtes `strait` du graphe sans le déclarer : les cellules
lacustres perdent leur `hops_to_sea` et `C1-E` doit rougir sur
`cellules_sans_hops`, jamais passer avec un `0` par défaut (règle n° 8).

**Résultat attendu** : `C1-E` vert, `cellules_sans_hops` nul,
`classes_de_sauts_non_monotones` nul, les deux compteurs de contexte mesurés.

---

## Condition 4 — Maille inchangée, aucun barème (`C1-A`, `C1-F`)

**Vérification** : lire `C1-A` et `C1-F` dans `logs/v1_080_qa.json`, puis
```
git status --porcelain pipeline/geo/artifacts/
```
(doit être vide sur les quinze artefacts G3/G4/G5 déjà committés).

**Reconstruction indépendante** : comparer l'ensemble trié des `cell_id` de
`artifacts/cells_g3.json` à celui de
`artifacts/cells_climate_drivers_c1.json` — strictement identiques. Puis
parcourir soi-même récursivement les quatre fichiers balayés par `C1-F` et
vérifier qu'aucune clé de `WORLD_TERMS_FORBIDDEN_KEYS`, **lu de
`constants.py`**, n'y apparaît. Vérifier que ce `frozenset` contient bien
`relative_intensity` et `climate_mod` : ce sont les noms exacts de la table
de barème du jeu hérité, et leur absence du jeu de clés interdites viderait
le contrôle de sa portée.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, retirer une
cellule de `cells_climate_drivers_c1.json` — `C1-A` doit rougir. Injecter une
clé `multiplier` au fond de `stats_c1.json` — `C1-F` doit rougir, y compris
si elle est imbriquée à plusieurs niveaux.

**Résultat attendu** : `C1-A` et `C1-F` verts, artefacts précédents intacts.

---

## Condition 5 — Les fichiers partagés n'ont reçu que des ajouts

**Vérification** :
```
git diff --numstat -- pipeline/geo/constants.py pipeline/geo/pipeline.py
```
et comparaison avec les instantanés committés
`deliverables/pre-edit/constants.py.orig` et
`deliverables/pre-edit/pipeline.py.orig`.

**Reconstruction indépendante** : c'est la vérification la plus mécanique du
lot, et elle ne doit pas se faire à l'œil.

1. Charger l'instantané pré-édition de `constants.py` **et** le fichier
   publié comme deux modules distincts, relever tous les noms de premier
   niveau de l'instantané, et vérifier que chacun existe encore dans le
   fichier publié avec la **même valeur**, comparée par représentation de
   l'objet. Un seul nom disparu ou changé de valeur est disqualifiant.
2. **[A1]** Extraire de chaque version de `pipeline.py` les blocs
   `if args.source == "..."` préexistants et les comparer texte à texte —
   byte-identiques exigés. **Le dénominateur se recompte sur l'instantané**,
   il ne se lit pas dans un document : l'instantané en porte **sept**, et non
   huit comme l'énoncé d'origine l'affirmait. La commande qui tranche, depuis
   la racine :

   ```
   grep -c 'if args.source == ' \
     harness/queue/briefs/025-geo-determinants-climat-c1/deliverables/pre-edit/pipeline.py.orig
   ```

   Puis extraire le **chemin de repli `fixture`** des deux versions — le bloc
   de `main()` qui suit la dernière branche, de la ligne
   `if args.stage != "all":` au dernier `return 0` de `main()` inclus (D8) —
   et le comparer octet à octet : `chemin_repli_fixture_identique` vaut `1`
   sur `1`. Les deux mesures ensemble couvrent les huit valeurs de
   `--source` ; ne faire que la première laisserait `fixture`, la valeur par
   défaut, hors de toute vérification.

   Extraire enfin la liste `choices` des deux versions et vérifier que les
   **huit** valeurs d'origine y sont toutes — ce dénominateur-là est juste et
   ne change pas — et que `"climate_drivers"` a été ajoutée.

   **Clause transitoire.** Le lot a été produit avant cet amendement : le
   Générateur a publié `branches_source_preexistantes_identiques` à `7/7`
   avec un waiver, et n'a pas publié `chemin_repli_fixture_identique`. Ce
   compteur-là, l'Évaluateur le mesure lui-même sur les deux fichiers
   committés — c'est une comparaison de texte, elle n'exige aucune
   ré-exécution ni aucune retouche de la PR. Son absence du `manifest.json`
   produit n'est pas disqualifiante pour ce lot ; un `7/7` publié sans que le
   repli ait été vérifié par personne le serait.
3. Vérifier que la chaîne `"climate"` **n'est pas** employée comme valeur de
   `--source` : elle est réservée au lot climatique futur (D8). Une
   sur-revendication ici est disqualifiante même si tout le reste est vert.
4. Vérifier que `pipeline/geo/qa/checks.py` est **inchangé**
   (`git status --porcelain` vide) et que `qa/checks_c1.py` **importe**
   `CheckResult` et `q10_determinism` au lieu de les redéfinir. Une copie
   locale de ces deux objets est le défaut « la même faute dans deux copies
   divergentes » et se rejette.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, changer la
valeur d'une constante préexistante (par exemple une borne G3) sans rien
supprimer — la reconstruction du point 1 doit le détecter, alors que
`constants_lignes_supprimees` resterait nul. C'est exactement ce que ce
contrôle existe pour attraper.

**Résultat attendu** : `constants_lignes_supprimees` nul,
`constantes_preexistantes_inchangees` complet,
`pipeline_lignes_supprimees` au plus `2`, **[A1]**
`branches_source_preexistantes_identiques` à `7` sur `7`,
`chemin_repli_fixture_identique` à `1` sur `1`,
`valeurs_source_preexistantes_conservees` à `8` sur `8`,
`source_climate_non_employee` à `1`, `qa/checks.py` intact.

---

## Condition 6 — Déterminisme, crochet, preuves, README (SC6 et SC7)

**Vérification** :
```
cd pipeline/geo && ../../.venv/bin/python pipeline.py --source climate_drivers
```
puis
```
git ls-files pipeline/geo/artifacts/*c1* pipeline/geo/logs/*080* \
  pipeline/geo/capture/*080* pipeline/geo/registry/climate_drivers_registry.json \
  pipeline/geo/steps/c1_climate_drivers.py pipeline/geo/qa/checks_c1.py \
  pipeline/geo/tests/*c1*
```

**Reconstruction indépendante** : relancer `tests/run_proof_c1.py` soi-même
et comparer les empreintes de sortie à celles déjà committées — elles doivent
être identiques à celles produites par le Générateur. Lire le `README.md`
publié et vérifier qu'il énonce C1 comme livré **et** qu'il dit explicitement
que la température, les précipitations, les saisons et la classification
climatique ne le sont pas ; vérifier qu'il ne décrit pas le relief (G6), quel
que soit l'état du lot 024. Comparer au
`deliverables/pre-edit/pipeline-geo-README.md.orig` — doit différer.
**Regarder réellement** `capture/v1_080_insolation_window.png` et
`capture/v1_080_continentality_window.png` (règle n° 11) : la première doit
montrer un dégradé nord-sud continu sans discontinuité artificielle, la
seconde un cœur continental nettement séparé des côtes. Une carte uniforme,
en damier ou vide est un échec, même avec sept contrôles verts.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, introduire un
horodatage courant dans un artefact — `Q10` doit rougir (deux passes,
empreintes différentes). Modifier trivialement `pipeline.py` au-delà des deux
lignes autorisées — `pipeline_lignes_supprimees`,
`branches_source_preexistantes_identiques` ou, **[A1]** si la retouche tombe
dans le chemin `fixture`, `chemin_repli_fixture_identique` doit le détecter.

**Résultat attendu** : code de sortie `0` sur les deux commandes,
`controles_c1_verts` à `7` sur `7`,
`controles_c1_avec_preuve_rouge_non_vide` à `7` sur `7`, toutes les preuves
déclarées suivies par git, README honnête sur le périmètre réellement livré.

**Note pour l'Évaluateur — suite du harnais
(`tests_harness_passed_025`)** : aucun paquet de test n'est installé dans le
venv de cette machine au moment où ce brief est écrit. Le brief autorise le
Générateur à l'installer comme outillage. Si `tests_harness_passed_025` vaut
`-1` avec, dans `deliverables/generator-log.md`, la commande d'installation
réellement tentée et son erreur exacte, ne pas rejeter le lot sur ce seul
point. Rejeter en revanche tout `0` silencieux ou tout `PASS` non
accompagné d'une sortie rejouable.

---

## Échecs disqualifiants (toute la rubrique, transversal)

- Un `red_proof` vide sur n'importe lequel des sept contrôles.
- **[A1]** `paires_consecutives_au_dessus_du_seuil` absent de
  `deliverables/manifest.json` › `counters[]` **et** des `detail` de
  `logs/v1_080_qa.json`, nul, ou non reconstruit par l'Évaluateur : la
  seconde moitié de `C1-B` et `C1-C` porterait alors sur un échantillon vide,
  et un contrôle sur échantillon vide passe toujours. Son absence du seul
  `artifacts/stats_c1.json` n'est **pas** un motif de rejet (Condition 1).
- Un contrôle qui compare une valeur produite à un nombre de contexte du
  brief au lieu de la dériver (règle n° 2).
- `qa/checks.py` modifié, même d'un octet.
- `CheckResult` ou `q10_determinism` recopiés dans `qa/checks_c1.py` au lieu
  d'être importés.
- Une constante préexistante de `constants.py` dont la valeur a changé, même
  sans suppression de ligne.
- `"climate"` employée comme valeur de `--source`.
- Un artefact G3/G4/G5 modifié ou régénéré.
- Une température, une précipitation, une saison ou une classe climatique
  produite sous quelque nom que ce soit.
- Une clé de `WORLD_TERMS_FORBIDDEN_KEYS` présente dans un artefact publié.
- Un compteur rapporté comme `0` alors qu'il n'a jamais été calculé (la
  sentinelle attendue est `-1`, règle n° 8) — distinct d'un `0` réellement
  mesuré comme `ecretages_polaires_total`.
- Une empreinte recopiée par valeur dans un test, un document ou un
  commentaire (règle n° 12).
- Toute lecture, écriture ou citation d'un fichier du lot 024 : ce lot en est
  indépendant, et s'y adosser masquerait sa propre indépendance.
