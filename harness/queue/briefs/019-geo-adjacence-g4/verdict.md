**Author**: forge-evaluateur
**Authored**: 2026-08-14T11:05:00Z

# Verdict — Brief `019` : l'adjacence maritime (G4) — passe `2`

> **Note de transparence.** Le harnais tourne ici via Cursor Cloud : l'acteur
> réel de ce verdict est un sous-agent Cursor Cloud endossant le rôle natif
> `forge-evaluateur`, sans aucun suffixe ajouté à la signature, pour que le
> contrôle mécanique `verdict_is_not_self_authored` puisse comparer les acteurs
> de part et d'autre du lot.

**Ce qui est jugé.** L'état du dépôt à `61b387b` (itération `2` du Générateur),
contre le `brief.md` et l'`eval-rubric.md` **tels qu'amendés** par
`amendment-001-escalade-empreinte-g3.md`. Le commit du Planificateur `6654af2`
n'est pas du travail de Générateur et n'est pas jugé comme tel. Ce verdict est
neuf : il remplace celui de l'itération `1` (REJECT), qui valait contre le texte
antérieur à l'amendement et qui reste consultable dans l'historique git.

**Session distincte.** Je n'ai repris aucune valeur de la passe `1`, ni du
manifeste. Tout ce qui suit a été re-mesuré dans cette session.

---

## Porte mécanique

Jouée **en premier**, avant toute lecture de fond. Rapport intégral, hors dépôt :
`/tmp/019-eval2/gate_avant.txt`, copié sous
`/opt/cursor/artifacts/eval_019_pass2/gate_avant.txt`.

`VERDICT: ACCEPT`, code de sortie `0`, dix contrôles applicables au vert, aucun
échec — ni sur les contrôles `verdict_*`, ni sur les autres. La porte signale,
comme prévu, que `20` chemins déclarés sortent du dossier du brief et ne sont
donc pas vérifiés par elle : c'est SC9 qui les vérifie, à la main, et je l'ai
fait (voir plus bas).

Cet `ACCEPT` ne m'autorise à rien relâcher : la porte juge la forme, pas le
fond. Elle ne sait ni recompter une arête, ni comparer une empreinte de littoral
à celle qu'un manifeste amont déclare, ni regarder une capture.

---

## Ce que j'ai reconstruit et rejoué moi-même

Mon relevé indépendant vit hors dépôt : `/tmp/019-eval2/recount.py` (le script),
`/tmp/019-eval2/recount_out.txt` (sa sortie), `/tmp/019-eval2/evaluateur_counters.json`
(mes compteurs sérialisés), `/tmp/019-eval2/comparaison.txt` (la confrontation).
Tout est aussi sous `/opt/cursor/artifacts/eval_019_pass2/`. **Mon script ne lit
jamais `deliverables/manifest.json`** : il re-dérive des artefacts, des
constantes lues et de git. Je n'ai comparé qu'à la fin.

Rejoué de ma main :

- `../../.venv/bin/python tests/run_proof_g4.py` depuis `pipeline/geo/` — code
  de sortie `0` (`/tmp/019-eval2/rerun_proof_g4.txt`).
- Empreintes des `41` fichiers de
  `pipeline/geo/{artifacts,logs,capture,registry,legacy_game_data}` relevées
  avant et après cette ré-exécution (`sha_before_rerun.txt` /
  `sha_after_rerun.txt`) : **un seul écart**, `logs/v1_050_adjacency.log`, et le
  `git diff` montre que la seule ligne changée est la durée d'horloge de la
  preuve. Artefacts, registre et captures : octet pour octet identiques. J'ai
  remis ce journal dans son état committé (`git checkout --`) ; l'arbre est
  redevenu propre.
- `../../.venv/bin/python pipeline.py --source adjacency` depuis `pipeline/geo/`
  (`/tmp/019-eval2/hook_out.txt`) — code de sortie `0`. Après ce **second point
  d'entrée**, l'arbre reste propre : le déterminisme tient entre deux chemins
  d'appel différents, pas seulement entre deux passes d'une même exécution.
- `.venv/bin/python .../deliverables/check_provenance_coastline_019.py`
  (`/tmp/019-eval2/provenance_out.txt`).
- `.venv/bin/python .../deliverables/measure_g4_019.py`
  (`/tmp/019-eval2/measure_out.txt`).
- `.venv/bin/python -m pytest harness/tests/ -q` (`/tmp/019-eval2/pytest_out.txt`).
- Les trois captures, ouvertes et regardées (règle durement acquise n° `11`).

### Compteurs : ma reconstruction contre le manifeste

Le manifeste déclare `48` compteurs. J'en ai re-dérivé **`43`** de façon
strictement indépendante, dans toutes les familles demandées (dénombrement de
zones, types d'arêtes, géométrie des détroits, atteignabilité, noms, frontière
ADR-`0003`, déterminisme, empreintes de provenance, suivi git). Les `5` restants
ont été établis autrement : `code_sortie_run_proof_g4` par mon exécution réelle
(`0`), `tests_harness_passed_019` par ma propre exécution de la suite
(`348` passés, `16` ignorés, donc `364` collectés), `captures_regardees_et_decrites`
en regardant les trois PNG et en comparant aux descriptions du journal, et le
couple `composantes_mer_totales` / `plans_eau_exclus_lacs` par vérification
arithmétique de la découpe de l'eau (`107` lacs exclus `+` `5` composantes
retenues `=` `112` plans d'eau examinés ; `112` `+` `4` éclats sous la
tolérance `=` `116` composantes d'eau brutes).

**Écart entre mes valeurs et le manifeste : aucun, sur les `43` re-dérivés.**
Aucun `sample_size` nul ni à la sentinelle, aucune sentinelle `-1` sur un
compteur calculé, aucun compteur du manifeste absent de la sortie du script de
mesure, aucun compteur imprimé sans dénominateur.

Quelques reconstructions que je signale parce qu'elles refont vraiment le
travail plutôt que de relire un chiffre :

| compteur | manifeste | ma reconstruction | méthode |
|---|---|---|---|
| `aretes_terre_terre` | `917` | `917` | et **différence symétrique vide** avec les arêtes `land-land` de `adjacency_g3.json` : le lot les lit, il ne les recalcule pas |
| `cellules_littorales` | `372` | `372` | re-dérivées des seules arêtes `land-sea` ; l'ensemble obtenu est **exactement** celui que `stats_g4.json` déclare, et il est strictement compris entre `0` et `596` |
| `ecart_min_detroit_m` | `297.134615` | `297.134615` | j'ai rechargé les géométries de `cells_g3.json` et recalculé les `668` distances : écart maximal entre largeur déclarée et distance recalculée = `0.000000` m ; `0` détroit entre deux cellules contiguës ; `0` au-dessus du seuil lu |
| `detroits_entre_masses_differentes` | `551` | `551` | composantes connexes de la terre recalculées des arêtes `land-land` (`212` masses) |
| `bassins_enfermes_non_atteignables_liens_inactifs` | `2` | `2` | parcours refait sur les **seules** arêtes `sea-sea` non déclarées, depuis les zones de mer extérieure : `5025` et `5027` sont alors injoignables ; en réinjectant les arêtes déclarées, elles le deviennent. Le lien est bien la **cause** de l'atteignabilité, pas un ornement |
| `zones_nommees` | `40` | `40` | attribution du plus-proche-ancrage entièrement refaite (moyenne des coordonnées riveraines héritées, projetée en `EPSG:3035`, départage par le plus petit identifiant) : `40` sur `40` identiques, `0` écart |
| `occurrences_province_dans_divergence` | `263` | `263` | balayage refait ; et `0` sur les six autres artefacts |
| `aretes_heritees_confirmees` / `_contredites` / `_manquantes` | `5` / `53` / `14` | idem | dénominateur `72` recompté moi-même depuis `province_adjacency.json` ; la somme fait exactement `72` |
| `zones_hors_bornes_intention` | `24` | `24` | trois bornes relues de `constants.py`, exemption de bassin enfermé entier appliquée (`2` zones) |
| `empreinte_terre_g4_egale_entree_g3` | `0` | `0` | trois empreintes calculées et lues par moi à l'exécution, aucune recopiée nulle part |
| `empreinte_terre_g4_egale_sortie_declaree_g2b` | `1` | `1` | même méthode |
| `fichiers_preuve_suivis_par_git` | `14` | `14` | `git ls-files` croisé avec la liste de preuves de D9 |

---

## Verdict par condition de succès

| Condition | Verdict | Preuve |
|---|---|---|
| **SC1** — zones dénombrées dans la fourchette lue, sans collision | PASS | `40` zones, fourchette relue de `constants.py` à l'exécution ; intersection des identifiants de mer et de terre **vide** ; `0` identifiant sous la base lue ; `5` composantes d'eau, `5` couvertes ; copie des noms hérités égale au fichier Unity, égalité **calculée** des deux côtés ; `596` cellules lues = `cell_count` de `stats_g3.json`. Vérifié aussi : aucune des bornes (`SEA_ZONE_COUNT_MIN`, `SEA_ZONE_COUNT_MAX`, `SEA_ZONE_ID_BASE`, `G4_STRAIT_MAX_WIDTH_M`) n'apparaît en littéral dans `steps/04_adjacency.py`, `tests/run_proof_g4.py` ni `tests/test_qa_red_g4.py` — elles sont toutes importées de `constants.py`. |
| **SC2** — graphe typé, quatre natures mesurées | PASS | `2085` arêtes, les quatre types strictement positifs (`917` / `437` / `63` / `668`) ; `0` arête portant l'identifiant fourre-tout de mer de G3 ; littoralité re-dérivée **exactement** égale à celle déclarée ; `land-land` identiques une pour une à `adjacency_g3.json` ; `Q4`, `Q7` et `G4-A` verts avec preuve rouge non vide. |
| **SC3** — détroit : seuil lu, largeur mesurée, au moins un inter-masses | PASS | seuil lu de `constants.py`, jamais écrit en littéral ; les `668` largeurs déclarées coïncident au millionième de mètre près avec la distance géométrique que je recalcule ; aucune arête `strait` entre deux cellules contiguës ; aucune au-dessus du seuil ; `551` relient deux masses terrestres distinctes sur `212` masses recalculées. |
| **SC4** — le lien déclaré est porteur | PASS | `2` corrections `declare_topology_link` lues, `2` appliquées, chacune portant identifiant, source, date et certitude. Atteignabilité **reconstruite par moi** dans les deux configurations (voir tableau ci-dessus). Les deux liens visent la **même** zone de mer extérieure au nom attesté « Mer du Nord », et partent chacun d'un bassin enfermé : `0` lien décoratif. Les deux journaux `G4-B` diffèrent et celui des liens coupés **nomme** les bassins par leur eau historique (IJsselmeer, Lauwerszee). Le cas rouge de `G4-B` est alimenté par la troisième passe réelle liens coupés (`unreachable_off` passé à `run_all_red_g4`), jamais par une mutation. |
| **SC5** — noms : proxy hérité déclaré avant mesure | PASS | attribution complète refaite : `40` zones sur `40` identiques à l'artefact, `0` écart. `0` nom hors de la liste attestée. `README.md` déclare la provenance héritée, la nature de proxy, la règle du plus-proche-ancrage et le départage d'égalité **avant** toute citation de compteur mesuré ; aucun plancher de noms employés n'est imposé (`13` sur `14`, `1` non employé, constaté et non corrigé). |
| **SC6** — ADR-`0003` dans les artefacts | PASS | `0` occurrence de la sous-chaîne `province` dans les six artefacts G4 hors divergence ; `263` dans le seul fichier de divergence, qui porte `"qa_only": true` dans son propre contenu ; `0` lecteur hors QA sur `20` fichiers de code balayés sous `pipeline/geo/` (seuls le module qui l'écrit et la preuve QA le mentionnent) ; les trois constats de comparaison sont rendus sans aucun seuil. Réserve nommée plus bas (observation n° `3`). |
| **SC7** — déterminisme, huit contrôles mordants, empreinte du littoral | **PASS** (par la **branche escalade**, pas par égalité) | Voir la section dédiée ci-dessous. |
| **SC8** — le crochet existant est réellement satisfait | PASS | `pipeline.py --source adjacency` exécuté par moi : il affiche la projection, le nombre de zones, les arêtes par type, les cellules littorales et l'atteignabilité, puis les captures et les empreintes. J'ai relu la branche `adjacency` du crochet dans le dépôt et retrouvé, une par une, chaque clé qu'elle consulte dans la sortie réelle. `pipeline.py` inchangé ; `fichiers_partages_modifies` = `0` sur `8`, vérifié par mon propre `git status`. |
| **SC9** — preuves committées, README sans sur-revendication | PASS | les `26` fichiers déclarés existent et sont **tous** suivis par git, y compris les `14` preuves sous `pipeline/geo/` que `.gitignore` exclut ; aucune preuve G4 produite mais non déclarée (`git ls-files` croisé dans les deux sens) ; `.gitignore` intact ; l'instantané `pre-edit` est octet pour octet le README de `master` et diffère du README publié ; le README énumère ce qui n'est pas livré (fleuves, relief et climat, ressources, villes, propriété, LOD, textures d'identifiants, QA de chaîne complète) et ne revendique nulle part le jalon E1 ; `test_single_source_of_instruction` passe. |
| **SC10** — mesure rejouable, manifeste complet, suites vertes, registre | **PASS** | Voir la section dédiée ci-dessous. |

## Verdict global : PASS

Les dix conditions de succès sont satisfaites contre le brief amendé. Les deux
motifs de rejet de l'itération `1` sont l'un et l'autre levés, et pour des
raisons différentes : l'un par un amendement du Planificateur qui ouvre la porte
que D2 annonçait, l'autre par une correction réelle du Générateur que j'ai
vérifiée par balayage, pas par confiance.

---

## SC7 en détail — une inégalité mesurée et escaladée, jamais une égalité

C'est le point que l'amendement `001` a rendu jugeable, et il mérite d'être dit
sans ambiguïté.

**Ce que j'ai mesuré moi-même**, en calculant l'empreinte du fichier vivant et
en lisant les deux valeurs déclarées à l'exécution, sans en recopier aucune :

- l'empreinte de `artifacts/coastline_1400.json` régénéré ici **diffère** de
  celle que `artifacts/MANIFEST_g3.json` déclare sous `inputs.coastline_1400` ;
- elle est **égale** à celle que `artifacts/MANIFEST_g2b.json` déclare comme
  sortie de l'étape qui produit ce fichier.

Les deux compteurs du brief tombent donc à `0` et `1`, et c'est exactement ce
que le manifeste déclare.

**Les six exigences de la branche escalade valent ensemble** — je les ai
vérifiées une par une, aucune n'est prise sur parole :

1. **Le `0` est une mesure, pas la sentinelle.** Je l'ai re-dérivé moi-même ;
   aucun compteur du manifeste ne porte `-1`.
2. **La dérogation d'escalade est invoquée avec une commande rejouable et son
   message d'erreur, sans aucun hexadécimal.** J'ai joué la commande depuis la
   racine : code de sortie `1`, message d'écart nommant ses **deux** sources
   (`artifacts/coastline_1400.json` calculé contre `MANIFEST_g3.json`
   `inputs.coastline_1400`), puis une seconde ligne répondant « oui » à la
   question de l'égalité avec la sortie déclarée par `MANIFEST_g2b.json`. J'ai
   balayé sa sortie : `0` occurrence d'une chaîne hexadécimale, même en
   abaissant le seuil de détection à huit caractères. Le champ `error` de la
   dérogation reproduit fidèlement cette sortie et n'en contient pas davantage.
   J'ai relu le script : il est en lecture seule, calcule l'empreinte à
   l'exécution et n'imprime que des noms de source et des résultats de
   comparaison. Il distingue bien l'absence (code `2`) de l'écart (code `1`),
   ce que le contrat du brief exigeait pour qu'une absence ne puisse jamais se
   faire passer pour une mesure.
3. **`empreinte_terre_g4_egale_sortie_declaree_g2b` vaut `1`**, mesuré par moi :
   l'écart est bien situé **en amont** du lot.
4. **Aucun artefact G3 n'a été réécrit, régénéré ni retouché.** `git status` est
   vide sur les quatre, et `git log master..HEAD` ne porte aucun commit les
   concernant.
5. **La comparaison n'a pas été retargetée.** Le compteur exigé compare toujours
   le littoral relu à `MANIFEST_g3.json` ; la comparaison à `MANIFEST_g2b.json`
   est un **second** compteur, distinct, jamais substitué au premier.
6. **Le constat est ouvert aux trois endroits exigés** : dans le journal de
   preuve `logs/v1_050_adjacency.log`, dans `deliverables/generator-log.md` et
   dans la section « Constats ouverts » de `pipeline/geo/README.md`. J'ai lu les
   trois : ils disent l'écart, nomment les deux manifestes, et **aucun** n'écrit
   ni ne laisse entendre que la mer et les cellules décrivent le même monde. Les
   artefacts sont cohérents avec cela : `stats_g4.json` porte
   `coastline_1400_sha_equals_g3_input: 0` et `MANIFEST_g4.json`
   `coastline_1400_sha_equal: 0`.

**Ce que ce PASS ne dit pas.** Il ne dit pas que l'empreinte est bonne. Il dit
que l'incohérence de la chaîne amont a été **mesurée, nommée et escaladée** dans
la forme exacte que l'amendement `001` a ouverte. La mer et les cellules ne
décrivent pas le même monde, et ce fait reste entier. Le réparer — trancher
lequel des deux artefacts committés est faux, et à quel prix pour les
consommateurs de la maille actuelle — est un brief ultérieur dédié
(non-objectif n° `18`), pas un acquis de ce lot.

Le reste de SC7 tient par ailleurs : `8` contrôles sur `8` verts, chacun avec
une preuve rouge non vide et un cas par identifiant (`Q1`, `Q4`, `Q7`, `Q10`,
`G4-A`, `G4-B`, `G4-C`, `G4-D`) ; `9` paires d'empreintes sur `9` égales et non
vides ; ma ré-exécution ne produit aucune différence sur les artefacts, le
registre et les captures ; `constants.py` intact ; le constat ouvert des bornes
d'intention inscrit dans le journal **et** dans le README ; `MANIFEST_g4.json`
porte un horodatage figé, jamais une horloge courante, et ses six empreintes de
sortie correspondent aux fichiers réellement présents (recalculées par moi).

---

## SC10 en détail — l'hexadécimal a bien disparu

C'était le second motif de rejet de l'itération `1`, et le seul qui était dans
les mains du Générateur. Je l'ai vérifié **par balayage, pas par confiance**.

Balayage d'une chaîne hexadécimale de `64` caractères sur
`deliverables/` (dossier entier), `pipeline/geo/README.md`,
`steps/04_adjacency.py`, `tests/run_proof_g4.py` et `tests/test_qa_red_g4.py` :
**aucune occurrence**. Même résultat en abaissant le seuil à seize caractères.
Le champ `error` de chacune des deux dérogations du manifeste : `0` occurrence,
seuil `64` comme seuil `8`. La démonstration de parité entre les deux points
d'entrée est désormais faite par nom (le bloc `determinism.sha256` de
`logs/v1_050_qa.json`) et par une commande rejouable — c'est aussi ainsi que je
l'ai vérifiée de mon côté, et cela marche.

Le reste de SC10 tient : le script de mesure se rejoue depuis la racine et
imprime les `48` compteurs **chacun avec son dénominateur**, en lisant les
artefacts, les constantes et git plutôt qu'en récitant des valeurs ; les trois
couples `must_differ_from` sont déclarés et diffèrent réellement ; la suite du
harnais est verte (`348` passés, `16` ignorés — les cas Unity/PowerShell,
attendus sur Linux, et déclarés) ; le registre de coût porte en dernière ligne
un événement `generator-run` sur le backend `cursor` pour ce brief ; aucun
fichier hors du périmètre de D16 n'a bougé ; et le Générateur n'a ni committé,
ni poussé, ni créé de branche.

---

## Violations de frontière et de périmètre

Rien à signaler, et je l'ai vérifié activement plutôt que supposé :

- `constants.py`, `qa/checks.py`, `pipeline.py`, `io_util.py`, `projection.py`,
  `steps/02_coastline.py`, `steps/02b_corrections_1400.py`, `steps/03_cells.py`
  — `git status` vide sur les huit.
- Les artefacts G3 (`cells_g3.json`, `stats_g3.json`, `adjacency_g3.json`,
  `MANIFEST_g3.json`) : ni modifiés, ni jamais committés par ce lot.
- `sim/`, `unity/`, `docs/adr/`, `architecture/`, `ROADMAP.md`, `HANDOFF.md`,
  `VISION.md`, `.github/`, `harness/*.py`, `harness/pipeline/`,
  `pipeline/geo/data/`, `pipeline/geo/sources.lock`, `pipeline/geo/.gitignore`,
  archives des briefs `001` à `018` : aucun n'apparaît dans le diff de la
  branche. Le fichier de noms de mer d'Unity est lu, jamais écrit ; sa copie lui
  est égale octet pour octet, égalité que j'ai recalculée.
- Aucun barème, aucun bonus, aucun malus, aucun pourcentage de jeu dans les
  artefacts G4 (le seul mot capté par mon balayage est le verbe « modifier »
  dans la phrase « sans modifier le trait de côte » d'une déclaration
  historique).
- Aucune brèche dans le trait de côte : les deux captures du Zuiderzee montrent
  **la même** géométrie au pixel près, seuls les liens changent.
- Aucun alias nu de l'interpréteur, aucun chemin `.venv/Scripts/` dans les
  livrables.
- Le diff de l'itération `2` porte sur cinq fichiers exactement — le script
  d'escalade (nouveau), le journal, le manifeste, `progress.jsonl` et le
  registre de coût. Aucun artefact n'a été régénéré pour cette itération, ce
  qui est cohérent avec ce que le journal annonce.

---

## Les captures, regardées de mes yeux (règle n° `11`)

- `capture/v1_050_sea_zones_window.png` : la fenêtre pilote entière, de
  l'Atlantique ibérique à Chypre et du Maghreb à la Baltique. Les zones sont des
  polygones de Voronoï nets, sans trou ni chevauchement visible, qui s'arrêtent
  sur le trait de côte sans mordre sur la terre. Les arêtes `sea-sea` sont
  tracées en pointillés bleus entre centroïdes et forment un réseau connexe d'un
  bout à l'autre. Un faisceau rouge, et un seul, sort de ce réseau, aux Pays-Bas.
  On voit à l'œil la cause du constat des bornes d'intention : des zones comme
  `S002` / `S003` (Méditerranée orientale) couvrent des centaines de milliers de
  kilomètres carrés, très au-delà du plafond d'intention. Et on voit que les noms
  sont bien un proxy et non une géographie : `S037`, au nord de la Baltique,
  porte « Mer de Norvège ». Le journal du Générateur signale ce même détail
  gênant de lui-même, ce que je note à son crédit.
- `capture/v1_050_zuiderzee_links_on.png` : zoom sur les Pays-Bas. Le Zuiderzee
  est le bassin turquoise fermé au centre (`S025`), la Lauwerszee le chapelet de
  taches saumon le long de la côte (`S027`). Deux segments rouges descendent d'un
  point hors cadre en haut à gauche — la zone de mer du Nord ouverte — l'un
  jusqu'au cœur du Zuiderzee, l'autre jusqu'à la Lauwerszee. La digue reste
  dessinée : le lien passe par-dessus, il ne perce rien.
- `capture/v1_050_zuiderzee_links_off.png` : même cadre, même échelle, mêmes
  couleurs, même trait de côte — les deux segments rouges ont disparu, et les
  deux bassins sont des culs-de-sac d'eau.

Les trois descriptions du journal correspondent à ce que je vois.
`captures_regardees_et_decrites` = `3` sur `3` est une mesure honnête.

---

## Ce qui a progressé depuis l'itération `1`

- **L'empreinte citée par sa valeur a réellement disparu**, et pas seulement de
  l'endroit signalé : mon balayage ne trouve plus aucune chaîne hexadécimale
  dans l'ensemble du dossier `deliverables/`, ni dans le README, ni dans les
  trois fichiers de code G4. La démonstration de parité n'a rien perdu en force
  en passant de la valeur au nom.
- **La commande d'escalade est un vrai instrument, pas un habillage.** Elle
  tient ensemble deux règles qui tiraient en sens contraire : l'impossibilité
  est éprouvée par une commande et un message (règle n° `9`), et ce message ne
  contient aucune constante morte (règle n° `12`). Le point que je retiens le
  plus : elle sépare l'absence de l'écart par deux codes de sortie distincts,
  ce que le brief exigeait et qu'il aurait été facile de négliger.
- **Le champ `error` de la dérogation ne se contredit plus.** Sa phrase affirme
  que la commande n'imprime aucune empreinte, et c'est désormais vrai — je l'ai
  vérifié en jouant la commande, pas en lisant la phrase.
- **Rien d'autre n'a bougé.** Le Générateur n'a pas rejoué la preuve, pas
  régénéré un artefact, pas « amélioré » un compteur au passage. C'est
  exactement ce que le feedback demandait, et c'est la bonne discipline : une
  correction de document ne doit pas déplacer une mesure.

## Ce qui a régressé depuis l'itération `1`

Rien. Les `43` compteurs que j'ai re-dérivés dans cette session neuve tombent
sur les mêmes valeurs, les artefacts sont octet pour octet ceux d'avant, et les
huit conditions déjà solides le sont restées sous des vérifications refaites de
zéro et non recopiées.

---

## Observations pour la suite (aucune n'est un motif de rejet)

1. **Le semis de zones sature toujours sur la borne d'acceptation.**
   `stats_g4.json` déclare `seed_saturated_at_ceiling: true` et le compte tombe
   exactement sur `SEA_ZONE_COUNT_MAX`, si bien que la question « le compte
   est-il dans la fourchette ? » ne discrimine plus rien. La cause est mesurée :
   la mer retenue par la fenêtre actuelle est d'environ `5,1` millions de km²,
   alors que le commentaire de `constants.py` a calibré rayons et fourchette sur
   une fenêtre bien plus petite. L'amendement `001` a explicitement laissé ce
   point à un lot ultérieur ; je le redis pour qu'il ne se perde pas.
2. **`logs/v1_050_adjacency.log` embarque une durée d'horloge murale** et des
   chemins absolus de la machine. D11 n'interdit l'horloge que dans un
   *artefact*, et ce journal n'en est pas un — ce n'est donc pas une infraction,
   et ce n'était déjà pas un motif de rejet. Mais c'est, cette fois encore, le
   **seul** fichier que ma ré-exécution a fait diverger. Arrondir la durée, ou
   la sortir du fichier suivi, rendrait la propriété « rejouer ne produit aucune
   différence » vraie sur l'ensemble des preuves committées.
3. **Le manifeste ne décrit le fichier de divergence qu'indirectement.** SC6
   demande que `README.md` **et** `deliverables/manifest.json` le décrivent
   explicitement comme comparaison QA unique et jamais autorité spatiale. Le
   README le fait longuement et sans réserve ; le manifeste ne le fait qu'à
   travers les intitulés et les notes de ses compteurs
   (`lecteurs_du_fichier_divergence_hors_qa`, « les six artefacts G4 hors
   divergence »). Le fond de SC6 est tenu — le fichier porte `"qa_only": true`,
   personne ne le lit hors QA, la frontière est mécaniquement vérifiée — et la
   table des échecs disqualifiants ne vise pas ce cas ; je ne fabrique donc pas
   un rejet là-dessus. Mais c'est le maillon le plus mince de SC6, et il se
   renforce d'une ligne : une note portée sur l'entrée du fichier dans `files`.
4. **Le cas rouge de `Q4` reste le plus grossier des huit** : il obtient son
   rouge en passant une liste d'arêtes vide, donc en isolant tout le graphe.
   Isoler **une seule** entité prouverait ce que le contrôle doit repérer. La
   rubrique n'exige pas la minimalité du cas rouge — ce n'est pas un motif de
   rejet — mais c'est le coût de la règle n° `6` : un contrôle trop grossier
   coûte aussi cher qu'un contrôle laxiste.
5. **`MANIFEST_g4.json` fige l'empreinte périmée que G3 déclare** (champ
   `coastline_1400_sha_declared_by_g3`). Elle est **lue** à l'exécution et non
   recopiée à la main, et un manifeste est un artefact, pas un document : ce
   n'est donc ni une infraction à la règle n° `12` ni un motif de rejet. Mais la
   valeur morte se propage d'un cran, et le brief de réparation de la provenance
   G3 devra s'en souvenir.

---

## Ce que je demande pour la suite

Rien du Générateur sur ce lot : il est reçu. Pour le Planificateur, une seule
chose vraiment structurante — ouvrir le brief de réparation de la provenance du
littoral que le non-objectif n° `18` et l'amendement `001` renvoient tous deux à
plus tard. Tant qu'il n'est pas ouvert, chaque lot aval héritera d'une chaîne
amont dont on sait, mesure à l'appui, qu'elle se contredit.
