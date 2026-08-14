**Author**: forge-generateur

# Journal du Générateur — brief 019, adjacence maritime (G4)

## Note de transparence

Le rôle joué est le rôle natif `forge-generateur`, sans suffixe. L'acteur réel
est un sous-agent Cursor Cloud (modèle Claude Opus 5), orchestré par un agent
Cursor Cloud qui remplace le CTO sur ce lot. La signature reste le rôle natif :
un couple `forge-generateur-cursor` / `forge-evaluateur-cursor` ferait refuser
la porte mécanique.

Je n'ai ni commité, ni poussé, ni créé de branche. Je suis resté sur
`forge/019-geo-adjacence-g4-d07d`.

## Ce qui a été construit

### 1. Copie des noms hérités (D1)

`pipeline/geo/legacy_game_data/sea_zones.json` est une copie octet-pour-octet de
`unity/game_unity/Assets/StreamingAssets/data/sea_zones.json`. Le fichier Unity
n'a pas été touché (lecture seule). L'égalité n'est pas une empreinte recopiée :
elle est **calculée à l'exécution**, des deux côtés, par
`deliverables/measure_g4_019.py` (`sha256_of()` sur chacun des deux fichiers,
puis comparaison). Le compteur vaut 1 sur 1 comparaison.

### 2. `steps/04_adjacency.py`

Un seul module nouveau, exposant `run_adjacency(apply_topology_links_flag=True)`.
Il rend un dictionnaire contenant `metrics` (dont `sea_zone_count`,
`adjacency_count`, `by_kind`, `coastal_cell_count`), `projection` (l'objet
`projector.info` de G3, donc `.epsg`), `reachability.all_enclosed_reachable`,
`captures` et `shas`. Le contrat de retour déjà câblé dans `pipeline.py` a été
lu, pas modifié ; la branche `--source adjacency` fonctionne sans retouche.

Enchaînement du module :

- **La mer (D2).** Eau = fenêtre projetée − terre corrigée de 1400. La fenêtre
  est densifiée (`segmentize`) avant projection : une boîte rectiligne en lon/lat
  devient une forme courbe en LAEA, et sans densification l'écart de surface se
  voyait à l'œil. L'eau est ensuite découpée en composantes, chacune classée :
  mer extérieure (touche le bord de la fenêtre), bassin reclassé `open_sea` par
  G2-bis, lac (exclu et compté), ou éclat sous la tolérance géométrique déclarée
  (exclu et compté à part). Le littoral **committé** est chargé
  (`load_corrected_land(rebuild=False)`), donc aucun artefact G3 n'est réécrit.
- **Les zones (D3).** Germes par disque de Poisson à rayon variable (le rayon
  suit la distance à la terre), puis relaxation de Lloyd, puis Voronoï découpé
  sur chaque composante. Les paramètres `G4_SEA_*` sont **lus** de
  `constants.py`, jamais redéfinis. Règle tenue : **au moins un germe par
  composante d'eau** — 5 germes obligatoires pour 5 composantes. Sans cela le
  Zuiderzee disparaissait purement et simplement.
- **Les identifiants (D4).** Les `zone_id` partent de `SEA_ZONE_ID_BASE` lu
  (5000) et **sautent** tout identifiant déjà pris par une cellule terrestre.
  La collision était réelle, pas théorique : la plage terrestre G3 monte à
  10466. Mesuré : 0 collision, 0 identifiant sous la base.
- **Les noms (D5).** Chaque zone reçoit le nom de l'ancrage le plus proche ;
  l'ancrage est la moyenne lon/lat des identifiants hérités listés par
  `sea_zones.json`, relue de `province_coordinates.json`. En cas d'égalité,
  le plus petit identifiant hérité gagne. Aucun plancher de « noms employés »
  n'est imposé : 13 noms sur 14 sont portés, le quatorzième ne l'est pas, et
  c'est constaté, pas corrigé.
- **Les arêtes (D6, D7).** Les arêtes `land-land` sont **lues** de
  `adjacency_g3.json` (`kind == "land-land"`), pas recalculées.
  `land-sea` vient du contact réel entre une cellule terrestre et une zone de
  mer. `sea-sea` du contact entre deux zones. `strait` (D7) exige deux terres
  **non contiguës** séparées d'au plus `G4_STRAIT_MAX_WIDTH_M` lu. L'identifiant
  fourre-tout `SEA_CELL_ID` (0) n'apparaît dans aucune arête exportée.
- **Les liens déclarés (D8).** Les deux corrections `declare_topology_link` de
  `data/corrections_1400.json` sont appliquées comme arêtes `sea-sea` portant
  leur source et leur date. La cible est la zone de mer extérieure au nom
  attesté demandé (« Mer du Nord »), vérifiée et non supposée.

### 3. Artefacts

`sea_zones_g4.json`, `adjacency_g4.json`, `topology_links_g4.json`,
`stats_g4.json`, `adjacency_divergence_g4.json` (`qa_only: true`),
`MANIFEST_g4.json`, `registry/sea_zone_registry.json`.

La sous-chaîne `province` vaut **0 occurrence** dans les six premiers, et
263 dans le seul fichier de divergence — qui est un fichier de QA (D10). Aucun
autre code que la preuve QA ne le lit : 0 lecteur hors QA sur 20 fichiers de
code balayés sous `pipeline/geo/`.

### 4. Preuve rouge d'abord (D12)

`tests/test_qa_red_g4.py` fournit **un cas rouge par contrôle** de
`run_g4_green` (Q1, Q4, Q7, Q10, G4-A, G4-B, G4-C, G4-D), soit 8 sur 8. Sept
cas sont des mutations locales sur des copies en mémoire (polygone auto-sécant,
graphe vide, arête terre-terre requalifiée en détroit, empreintes divergentes,
littoralité citant une cellule inexistante, zone de mer retirée de la
couverture, identifiant forcé sur une valeur terrestre). Le huitième, **G4-B,
n'est pas une mutation** : c'est le cas naturel demandé — on rejoue la chaîne
avec `apply_topology_links_flag=False` et les deux bassins deviennent
injoignables d'eux-mêmes.

Chaque cas a été constaté rouge avant d'être constaté vert : la preuve
imprime `became_red=True` pour les huit, avec le nom du cas.

### 5. Captures — je les ai regardées (règle 11)

**`capture/v1_050_sea_zones_window.png`** — la fenêtre pilote entière. Les 40
zones de mer couvrent la Méditerranée, l'Adriatique, l'Égée, la mer de Marmara,
l'Atlantique ibérique, le golfe de Gascogne, la Manche, la mer d'Irlande, la
mer du Nord, la Baltique. Les zones sont des polygones de Voronoï nets, sans
trou visible entre elles ni chevauchement, et elles s'arrêtent proprement sur
le trait de côte : la terre reste beige, aucune zone ne mord dessus. Les
étiquettes vont de `S000` à `S039` avec leur nom hérité sous l'identifiant. Les
traits bleus pointillés sont les arêtes `sea-sea` entre centroïdes ; ils forment
un réseau connexe d'un bout à l'autre de la fenêtre. Deux traits rouges épais,
et deux seulement, sortent du réseau bleu : ce sont les liens déclarés, tous
deux aux Pays-Bas. Ce que la capture montre honnêtement : les zones du large
sont **très grandes** (Méditerranée orientale, Atlantique ibérique), ce qui est
la conséquence directe du plafond de 40 zones pour 5 111 079.462 km² de mer,
et c'est le constat ouvert D13 ci-dessous. Ce qu'elle montre aussi : les noms
sont un proxy, pas une source — `S037` porte « Mer de Norvège » alors qu'elle
est au nord de la Baltique. C'est le comportement déclaré du plus-proche-ancrage,
pas un bug ; le README le dit **avant** d'aligner les compteurs.

**`capture/v1_050_zuiderzee_links_on.png`** — zoom sur les Pays-Bas, liens
**actifs**. Le Zuiderzee est la tache turquoise fermée au centre (`S025`,
étiquetée « Mer du Nord » par proximité d'ancrage) ; l'Afsluitdijk le sépare
visiblement de la mer rose au nord. La Lauwerszee est le petit chapelet de
taches saumon le long de la côte, avec l'étiquette `S027` à droite. Deux
segments rouges partent du même point **hors cadre en haut à gauche** — le
centroïde de `S028`, la zone de mer du Nord ouverte — et descendent, l'un
jusqu'au cœur du Zuiderzee, l'autre jusqu'à la Lauwerszee. La géométrie n'est
pas retouchée : la digue est toujours dessinée, le lien passe par-dessus.
C'est exactement ce qu'un lien topologique déclaré doit donner à voir.

**`capture/v1_050_zuiderzee_links_off.png`** — même cadre, même échelle, mêmes
zones, mêmes couleurs, liens **coupés**. La seule différence visible est
l'absence des deux segments rouges. Les deux bassins sont alors des culs-de-sac
d'eau : rien ne les relie au large. Comparées côte à côte, les deux images
isolent une variable et une seule, ce qui est le but du couple
`must_differ_from`.

### 6. Divergence QA et README

`adjacency_divergence_g4.json` compare le graphe `land-land` dérivé au graphe
hérité `province_adjacency.json`. Il est marqué `qa_only: true` et n'est lu que
par la preuve. Sur 72 arêtes héritées lues : 5 confirmées, 53 contredites,
14 manquantes. Ces chiffres ne sont pas un échec du pilote — ils mesurent
l'écart entre un graphe hérité de jeu et un graphe dérivé de la géométrie, et
c'est précisément ce que le brief demande de rendre visible.

`README.md` a été mis à jour. L'instantané d'avant édition est dans
`deliverables/pre-edit/pipeline-geo-README.md.orig`. La section ajoutée dit
d'abord la provenance des noms (proxy hérité du jeu, pas source historique),
**avant** tout compteur, puis décrit ce que G4 ajoute, puis liste les constats
ouverts. Elle ne revendique rien qui ne soit mesuré ici.

## Comment chaque compteur a été mesuré

Un seul script imprime les 48 compteurs, chacun avec son dénominateur :

```
.venv/bin/python harness/queue/briefs/019-geo-adjacence-g4/deliverables/measure_g4_019.py
```

Il ne recopie aucune valeur : il relit les artefacts (`stats_g4.json`,
`adjacency_g4.json`, `sea_zones_g4.json`, `topology_links_g4.json`,
`adjacency_divergence_g4.json`, `MANIFEST_g4.json`, `stats_g3.json`), les
constantes (`import constants as C` — donc `SEA_ZONE_ID_BASE`,
`SEA_ZONE_COUNT_MIN/MAX`, `G4_STRAIT_MAX_WIDTH_M`, `SEA_CELL_ID` sont lues, pas
écrites en dur), les journaux (`logs/v1_050_qa.json`), l'état git
(`git status --porcelain`, `git ls-files`) et exécute la suite du harnais.
`--rerun-proof` rejoue la preuve en direct ; sans l'option, le code de sortie
est relu du rapport QA que la preuve a écrit elle-même.

Les compteurs de `deliverables/manifest.json` n'ont pas été recopiés à la main :
ils sont sortis de ce même script (option `--json`), et leur `sample_size` est
le dénominateur que le script imprime. Après écriture du manifeste, une mesure
fraîche a été comparée à ce qu'il contient — 48 compteurs de part et d'autre,
aucun écart.

Quelques dénominateurs qui méritent d'être dits, parce qu'un dénominateur faux
est un compteur faux :

- `plans_eau_exclus_lacs` = 107 sur **112**, pas sur 116 ni sur 101. 116 est le
  nombre brut de composantes d'eau ; 4 sont des éclats sous la tolérance
  géométrique et ne sont pas des plans d'eau ; restent 112 examinés, dont 107
  écartés comme lacs et 5 retenus comme mer. Le dénominateur « eaux enclavées »
  (101) était **faux** : il était plus petit que son propre numérateur, parce
  que 6 lacs exclus touchent le bord de la fenêtre et ne sont donc pas
  enclavés. Corrigé après l'avoir constaté sur la sortie du script.
- `cellules_littorales` = 372 sur 596, dénominateur = `cell_count` **lu** de
  `stats_g3.json`, pas le nombre de cellules que G4 a chargées. Les deux sont
  d'ailleurs comparés et égaux (596 = 596), ce qui est le vrai contrôle.
- `detroits_entre_masses_differentes` = 551 sur 668 arêtes `strait` : les 117
  autres relient deux terres non contiguës appartenant à la même masse.
- `ecart_min_detroit_m` = 297.134615 m, à comparer au seuil **lu**
  `G4_STRAIT_MAX_WIDTH_M` = 45000.0 m. L'écart maximal mesuré est
  44798.699015 m : sous le seuil, donc aucun détroit ne le franchit.
- `empreinte_terre_g4_egale_entree_g3` = **0** sur 1. C'est un zéro réel,
  mesuré, pas un « non calculé » — la sentinelle « non calculé » du projet est
  -1 (règle 8). Voir le constat ouvert ci-dessous.
- `captures_regardees_et_decrites` : le script compte les captures dont le nom
  de fichier apparaît dans ce journal. Il ne peut pas vérifier que je les ai
  vraiment regardées ; il vérifie seulement que chacune est nommée et décrite
  ici. Les descriptions ci-dessus viennent de l'ouverture réelle des trois PNG.

## Constats ouverts (escalade, aucune borne déplacée)

Deux points ne passent pas « à paramètres inchangés ». Conformément à la
consigne, je les remonte tels quels ; je n'ai bougé ni `constants.py`, ni les
bornes, ni un artefact G3.

**1. Empreinte du littoral (D2 / SC7).** L'empreinte du littoral corrigé de
1400 régénéré ici **diffère** de celle que `MANIFEST_g3.json` déclare comme son
entrée. Elle est en revanche **égale** à celle que `MANIFEST_g2b.json` déclare
comme la sortie de l'étape qui produit ce littoral. Autrement dit l'incohérence
est **antérieure à ce lot** et interne aux artefacts G2-bis/G3 committés ; G4
la constate au lieu de la masquer. Les deux valeurs sont calculées à
l'exécution, aucune n'est recopiée. Compteurs :
`empreinte_terre_g4_egale_entree_g3` = 0, `empreinte_terre_g4_egale_sortie_declaree_g2b` = 1.
La dérogation d'escalade du manifeste porte la commande rejouable
(`deliverables/check_provenance_coastline_019.py`) et le message qu'elle
imprime, l'un et l'autre sans aucune valeur hexadécimale.

**2. Bornes d'intention de surface (D13).** 24 zones sur 40 sortent des bornes
d'**intention** de surface ou de compacité. Ces bornes ne sont pas bloquantes.
La cause est mesurée, pas supposée : la mer retenue fait 5 111 079.462 km²
pour au plus 40 zones (`SEA_ZONE_COUNT_MAX` lu), soit une surface moyenne très
au-dessus du plafond d'intention. Le semis sature effectivement le plafond
(`seed_saturated_at_ceiling: true`). Deux zones de plus sont exemptées parce
qu'elles couvrent un bassin enclavé entier. Je n'ai pas touché aux bornes : les
déplacer aurait fait disparaître le constat au lieu de le régler.

## Commandes de validation réellement exécutées

Depuis `pipeline/geo/` :

```
../../.venv/bin/python tests/run_proof_g4.py            → code de sortie 0
../../.venv/bin/python pipeline.py --source adjacency   → code de sortie 0
```

Fin de sortie de la preuve :

```
=== determinisme SHA256 (passe 1 vs passe 2) ===  (9 fichiers, match=True partout)
MESURE : 40 zones de mer (fourchette lue [20, 40]), 5/5 composantes d'eau
couvertes, 2085 aretes {'land-land': 917, 'land-sea': 437, 'sea-sea': 63,
'strait': 668}, 372 cellules littorales derivees, 551 detroits entre masses
distinctes (ecart min 297.134615 m, seuil lu 45000.0 m), 2 liens declares
appliques ; liens actifs -> tout bassin atteignable, liens coupes -> 2 bassins
injoignables ; 8/8 controles verts et rouges constates ; deux passes identiques
en SHA256=OK sur 9 fichiers
```

La branche `--source adjacency` produit **les mêmes empreintes** que la preuve.
Les empreintes sont citées ici **par leur nom**, jamais par leur valeur : les
paires sont dans le bloc `determinism.sha256` de `logs/v1_050_qa.json`, et
l'égalité se revérifie en rejouant les deux points d'entrée puis en constatant
que

```
git status --porcelain -- pipeline/geo/artifacts pipeline/geo/registry
```

ne renvoie rien. Deux points d'entrée, un seul résultat.

Depuis la racine :

```
.venv/bin/python harness/queue/briefs/019-geo-adjacence-g4/deliverables/measure_g4_019.py   → code de sortie 0
.venv/bin/python -m pytest harness/tests/ -q                                                → code de sortie 0
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/019-geo-adjacence-g4         → voir ci-dessous
.venv/bin/python harness/backends/ledger.py append --backend cursor --brief harness/queue/briefs/019-geo-adjacence-g4 --event generator-run
```

Sortie de la suite du harnais :

```
348 passed, 16 skipped in 16.89s
```

Aucun `FAILED`. Les 16 `SKIP` sont les cas Unity/PowerShell, attendus sur
Linux.

Auto-contrôle de la porte : `verdict_audit.py` a été joué avant de rendre. Les
seuls contrôles qui ne passent pas sont ceux qui **exigent `verdict.md`** —
`verdict_numbers_traceable` et `verdict_is_not_self_authored`. C'est normal et
voulu : le Générateur n'écrit pas de verdict. L'Évaluateur les fera passer en
écrivant le sien. Aucun autre contrôle de la porte n'est en échec.

## Suivi git

`pipeline/geo/.gitignore` ignore `artifacts/` et `logs/`. Les preuves existent
bien sur disque et ont été ajoutées à l'index avec `git add -f` (même mécanisme
que les lots 002 et 007a), afin que `git ls-files` les voie. **Aucun commit,
aucun push, aucune branche créée** : l'orchestrateur s'en charge.

## Périmètre

Rien n'a été touché hors périmètre. Vérifié par `git status --porcelain` sur
les fichiers partagés : `constants.py`, `qa/checks.py`, `pipeline.py`,
`io_util.py`, `projection.py`, `steps/02_coastline.py`,
`steps/02b_corrections_1400.py`, `steps/03_cells.py` — 0 modifié sur 8.
`steps/03_cells.py` est chargé dynamiquement et réemployé, jamais édité.

## Itération 2 — ce qui a été corrigé

Trois changements, et rien d'autre. Le code G4, les artefacts, les captures et
les 48 compteurs n'ont pas bougé : `tests/run_proof_g4.py` n'a pas été rejoué,
donc aucun artefact n'a été régénéré.

1. **Empreinte citée par sa valeur (feedback point 2).** La section qui compare
   `--source adjacency` et la preuve écrivait la valeur hexadécimale complète de
   l'empreinte de `adjacency_g4.json`. Elle cite désormais les empreintes par
   leur nom — le bloc `determinism.sha256` de `logs/v1_050_qa.json` — avec la
   commande qui rejoue l'égalité. Plus aucun chiffre hexadécimal dans ce
   journal.
2. **Commande d'escalade (amendement 001, SC7 branche escalade).** Nouveau
   fichier `deliverables/check_provenance_coastline_019.py`, écrit selon le
   contrat du brief : lecture seule, il calcule l'empreinte du
   `artifacts/coastline_1400.json` vivant et la compare à l'entrée déclarée par
   `MANIFEST_g3.json` puis à la sortie déclarée par `MANIFEST_g2b.json`, en
   n'imprimant que des noms de source et des résultats. Commande jouée depuis
   la racine :

   ```
   .venv/bin/python harness/queue/briefs/019-geo-adjacence-g4/deliverables/check_provenance_coastline_019.py
   ```

   Sortie réelle :

   ```
   ECART : ecart entre artifacts/coastline_1400.json calcule et
   MANIFEST_g3.json inputs.coastline_1400.
   Le meme fichier vivant egale-t-il la sortie declaree par MANIFEST_g2b.json
   outputs[artifacts/coastline_1400.json] ? oui.
   → code de sortie 1
   ```

   Le rouge a été prouvé avant de s'en servir : les quatre branches — égalité,
   écart, fichier vivant absent, `MANIFEST_g2b.json` absent — ont été éprouvées
   hors dépôt en chargeant le script comme module et en pointant ses trois
   constantes de chemin vers un dossier temporaire rempli de faux fichiers.
   Codes obtenus : 0, 1, 2 et 2. Une absence ne peut donc pas se faire passer
   pour un écart. Le fichier de contrôle était jetable : il vivait hors du
   dépôt et n'a pas été conservé, il ne fait pas partie du lot.
3. **Dérogation du manifeste (feedback point 3).** La première dérogation porte
   désormais cette commande et la sortie réelle qu'elle imprime, sans aucune
   valeur hexadécimale, et le nouveau script est déclaré dans `files`. La phrase
   de la dérogation dit ce qui est vrai : la commande n'imprime aucune
   empreinte.

Le compteur `empreinte_terre_g4_egale_entree_g3` reste le `0` mesuré. Il n'a été
ni retargeté vers `MANIFEST_g2b.json`, ni remplacé par la sentinelle `-1`.
Aucun artefact G3 (`MANIFEST_g3.json`, `cells_g3.json`, `stats_g3.json`,
`adjacency_g3.json`) n'a été lu autrement qu'en lecture, et `constants.py` n'a
pas été touché.
