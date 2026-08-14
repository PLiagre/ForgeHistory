**Author**: forge-evaluateur
**Authored**: 2026-08-14T10:30:00Z

# Verdict — Brief `019` : l'adjacence maritime (G4)

> **Note de transparence.** Le harnais tourne ici via Cursor Cloud : l'acteur
> réel de ce verdict est un sous-agent Cursor Cloud endossant le rôle natif
> `forge-evaluateur`, aucun suffixe n'étant ajouté à la signature pour que le
> contrôle mécanique `verdict_is_not_self_authored` puisse comparer les acteurs
> de part et d'autre du lot.

---

## Porte mécanique

Jouée **en premier**, avant toute lecture de fond, puis rejouée après
l'écriture de ce verdict. Rapports intégraux, hors dépôt :
`/tmp/019-eval/gate_avant.txt` et `/tmp/019-eval/gate_apres.txt` (copiés aussi
sous `/opt/cursor/artifacts/eval_019/`).

**Avant l'existence de ce fichier** : `VERDICT: REJECT`, avec deux contrôles au
rouge — `verdict_numbers_traceable` (« verdict.md missing ») et
`verdict_is_not_self_authored` (« Author frontmatter missing on
generator-log.md or verdict.md »). J'ai vérifié que le second rougissait bien
pour la seule absence de ce fichier, et non pour une signature manquante côté
Générateur : `deliverables/generator-log.md` porte `**Author**:
forge-generateur` en première ligne, et le code de
`check_verdict_not_self_authored` échoue dès que **l'une** des deux listes
d'auteurs est vide. **Aucun autre contrôle mécanique n'était au rouge** : les
huit autres passaient.

**Après l'écriture de ce fichier** : `VERDICT: ACCEPT`, dix contrôles sur dix au
vert.

Deux conséquences, à ne pas confondre. D'une part, le REJECT prononcé ci-dessous
n'est pas un REJECT mécanique recyclé : la porte accepte la **forme** du lot, et
mon refus porte sur le **fond**, contre la rubrique — cas que la rubrique prévoit
explicitement (« un lot peut obtenir `ACCEPT` de la porte et `FAIL` de
l'Évaluateur »). D'autre part, un `ACCEPT` de la porte ne m'autorise à rien
relâcher : la porte ne sait pas lire une empreinte citée par sa valeur dans une
phrase, ni comparer une empreinte de littoral à celle qu'un manifeste amont
déclare.

---

## Ce que j'ai reconstruit moi-même

Je n'ai importé aucune valeur du manifeste. J'ai écrit mon propre relevé hors
dépôt (`/tmp/019-eval/recount.py`, sortie dans
`/tmp/019-eval/recount_out.txt`, compteurs sérialisés dans
`/tmp/019-eval/evaluateur_counters.json`), qui re-dérive les compteurs des
artefacts, des constantes lues et de git. J'ai ensuite seulement comparé.

Ce que j'ai rejoué de ma main :

- `../../.venv/bin/python tests/run_proof_g4.py` depuis `pipeline/geo/` —
  code de sortie `0`, durée mesurée d'environ `76` secondes
  (`/tmp/019-eval/rerun_proof_g4.txt`). Les huit contrôles ressortent verts,
  chacun avec sa preuve rouge, et les `9` paires d'empreintes de la double
  passe sont égales.
- Empreintes des `41` fichiers de `pipeline/geo/{artifacts,logs,capture,registry,legacy_game_data}`
  relevées **avant** ma ré-exécution (`/tmp/019-eval/sha_before_rerun.json`)
  puis après : **un seul écart**, `logs/v1_050_adjacency.log`, et uniquement
  sur la ligne de durée d'exécution. Les artefacts et le registre sont
  octet pour octet identiques. J'ai remis ce journal dans son état committé
  (`git checkout --`) ; l'arbre est propre.
- `../../.venv/bin/python pipeline.py --source adjacency` — sortie complète
  dans `/tmp/019-eval/hook_out.txt`. Après ce **second point d'entrée**, les
  artefacts restent encore octet pour octet identiques : le déterminisme tient
  aussi entre deux chemins d'appel différents, pas seulement entre deux passes
  d'une même exécution.
- `.venv/bin/python -m pytest harness/tests/ -q` — `348` passés, `16` ignorés
  (`/tmp/019-eval/pytest_out.txt`).
- `.venv/bin/python .../deliverables/measure_g4_019.py` —
  `/tmp/019-eval/measure_out.txt`.
- Les huit preuves rouges : rejouées par `run_proof_g4.py`, et j'ai relu
  `tests/test_qa_red_g4.py` ligne par ligne pour vérifier qu'il existe un cas
  par identifiant de contrôle et qu'aucun ne modifie `qa/checks.py`.
- Les trois captures, regardées de mes yeux (voir plus bas).

### Compteurs : manifeste contre reconstruction

Le manifeste déclare `48` compteurs (les `46` exigés, plus
`noms_hors_liste_attestee` et `empreinte_terre_g4_egale_sortie_declaree_g2b`).
J'en ai re-dérivé `42` directement et de façon strictement indépendante, et
j'ai établi les `6` autres autrement (exécution réelle, ou lecture croisée du
code de découpe de l'eau).

**Écart constaté entre mes valeurs et le manifeste : aucun, sur les `48`.**
Aucun `sample_size` nul, aucune sentinelle `-1` sur un compteur calculé.
Quelques reconstructions notables, choisies dans des familles différentes :

| compteur | manifeste | ma reconstruction | comment je l'ai obtenu |
|---|---|---|---|
| `zones_mer_denombrees` | `40` | `40` | entrées de `sea_zones_g4.json`, fourchette relue de `constants.py` |
| `collisions_id_mer_terre` | `0` | `0` | intersection des `zone_id` et des `cell_id`, calculée : vide |
| `ids_mer_sous_la_base` | `0` | `0` | `zone_id` comparés à `SEA_ZONE_ID_BASE` lu |
| `aretes_terre_terre` | `917` | `917` | et **identiques une pour une** à celles de `adjacency_g3.json` (différence symétrique vide) |
| `cellules_littorales` | `372` | `372` | re-dérivées des seules arêtes `land-sea`, **exactement** l'ensemble déclaré |
| `ecart_min_detroit_m` | `297.134615` | `297.134615` | et les `668` largeurs recalculées sur la géométrie des cellules concordent à `0.000000` m près |
| `detroits_entre_masses_differentes` | `551` | `551` | composantes connexes de la terre re-calculées des arêtes `land-land` (`212` masses) |
| `bassins_enfermes_non_atteignables_liens_inactifs` | `2` | `2` | parcours refait sur les seules arêtes `sea-sea` **non déclarées** |
| `zones_nommees` | `40` | `40` | attribution du plus-proche-ancrage entièrement refaite : `40` sur `40` identiques |
| `occurrences_province_dans_divergence` | `263` | `263` | balayage refait ; et `0` sur les six autres artefacts |
| `aretes_heritees_confirmees` / `_contredites` / `_manquantes` | `5` / `53` / `14` | idem | dénominateur `72` recompté depuis `province_adjacency.json` ; la somme fait bien `72` |
| `zones_hors_bornes_intention` | `24` | `24` | bornes relues, exemption de bassin entier appliquée |
| `fichiers_preuve_suivis_par_git` | `14` | `14` | `git ls-files` croisé avec la liste de D9 |
| `empreinte_terre_g4_egale_entree_g3` | `0` | `0` | trois empreintes calculées par moi (voir constat A) |

---

## Verdict par condition de succès

| Condition | Verdict | Preuve |
|---|---|---|
| **SC1** — zones dénombrées dans la fourchette lue, sans collision | PASS | `40` zones dans la fourchette relue ; intersection identifiants mer/terre vide ; `0` identifiant sous la base ; `5` composantes d'eau, `5` couvertes ; copie de noms octet pour octet vérifiée par calcul ; `596` cellules lues égales à `cell_count`. Aucune borne de `constants.py` en littéral dans les trois fichiers de code (vérifié). Voir aussi l'observation n° 1. |
| **SC2** — graphe typé, quatre natures mesurées | PASS | `2085` arêtes, les quatre types strictement positifs ; `0` arête portant l'identifiant fourre-tout ; littoralité re-dérivée exactement égale à la liste déclarée et strictement comprise entre `0` et `596` ; `land-land` identiques une pour une à G3 ; `Q4`, `Q7`, `G4-A` verts avec preuve rouge non vide. |
| **SC3** — détroit : seuil lu, largeur mesurée, au moins un inter-masses | PASS | seuil lu, jamais écrit en littéral ; les `668` largeurs déclarées concordent avec la distance géométrique que je recalcule ; `0` détroit entre deux cellules contiguës ; `0` au-dessus du seuil ; `551` relient deux masses terrestres distinctes. |
| **SC4** — le lien déclaré est porteur | PASS | `2` déclarations lues, `2` appliquées, chacune portant identifiant, source, date et certitude. Atteignabilité **reconstruite par moi** : sans les arêtes déclarées, les zones `5025` et `5027` sont injoignables depuis la mer extérieure ; avec elles, elles le deviennent. Les deux arêtes déclarées joignent chacune un bassin enfermé à une zone de la mer extérieure au nom attesté — `0` lien décoratif. Les deux journaux diffèrent et celui des liens coupés **nomme** les bassins par leur eau historique. Le cas rouge de `G4-B` vient de la troisième passe réelle liens coupés, pas d'une mutation. |
| **SC5** — noms : proxy hérité déclaré avant mesure | PASS | j'ai refait l'attribution complète (ancrage = moyenne des coordonnées riveraines projetée, plus proche gagne, plus petit identifiant en cas d'égalité) : `40` zones sur `40` identiques à l'artefact. `0` nom hors de la liste attestée. `README.md` déclare la provenance, la nature de proxy, la règle et le départage **avant** toute citation de compteur ; aucun plancher de noms employés n'est imposé. |
| **SC6** — ADR-`0003` dans les artefacts | PASS | `0` occurrence de `province` dans les six artefacts, `263` dans le seul fichier de divergence, qui porte `"qa_only": true` ; `0` lecteur hors QA sur `20` fichiers de code balayés (seuls le module qui l'écrit et la preuve QA le mentionnent) ; les trois constats sont rendus sans seuil. |
| **SC7** — déterminisme, huit contrôles mordants, empreinte du littoral | **FAIL** | Tout est vert **sauf un point**, et ce point est nommément disqualifiant par la rubrique : `empreinte_terre_g4_egale_entree_g3` vaut `0`. Détail au constat A. Le reste tient : `8` contrôles sur `8` verts avec preuve rouge non vide, `9` paires d'empreintes sur `9` égales et non vides, re-exécution sans aucune différence sur les artefacts, `constants.py` intact, constat ouvert des bornes d'intention inscrit dans le journal et dans le README. |
| **SC8** — le crochet existant est réellement satisfait | PASS | `pipeline.py --source adjacency` exécuté par moi : il affiche projection, nombre de zones, arêtes par type, cellules littorales, atteignabilité, puis les captures et les empreintes. J'ai relu la branche `adjacency` du crochet et retrouvé chaque clé qu'elle consulte dans la sortie réelle. `git diff` sur `pipeline.py` vide ; `fichiers_partages_modifies` = `0` sur `8`. |
| **SC9** — preuves committées, README sans sur-revendication | PASS | les `25` fichiers déclarés existent et sont **tous** suivis par git, y compris les `14` preuves sous `pipeline/geo/` que `.gitignore` exclut ; aucune preuve G4 produite mais non déclarée ; `.gitignore` intact ; l'instantané `pre-edit` est bien le README d'avant le lot (identique à celui de `master`) et diffère du README publié ; le README énumère ce qui n'est pas livré et dit explicitement que le jalon E1 n'est pas clos ; `test_single_source_of_instruction` passe. |
| **SC10** — mesure rejouable, manifeste complet, suites vertes, registre | **FAIL** | La mesure se rejoue, imprime `48` compteurs chacun avec son dénominateur, ne code aucune valeur en dur et emploie bien la sentinelle `-1` quand un compteur n'est pas calculé ; les trois couples `must_differ_from` sont déclarés et diffèrent ; la suite du harnais est verte (`348` sur `364`) ; le registre de coût porte la ligne attendue ; aucun fichier hors périmètre n'a bougé. **Mais** une empreinte est citée par sa valeur hexadécimale dans un document livré — motif disqualifiant explicite de la rubrique. Détail au constat C. |

## Verdict global : REJECT

Deux motifs, tous deux nommés dans la table « Échecs disqualifiants » de la
rubrique, laquelle a été écrite avant le code et que je n'ai pas le pouvoir
d'assouplir.

---

## Constats motivant le rejet

### A — L'empreinte du littoral employé par G4 diffère de l'entrée déclarée par G3

**Ce que j'ai mesuré moi-même**, en calculant les trois empreintes à
l'exécution, sans en recopier aucune :

- l'empreinte de `artifacts/coastline_1400.json` régénéré est **égale** à
  celle que `artifacts/MANIFEST_g2b.json` déclare comme sortie de l'étape qui
  produit ce fichier ;
- elle est **différente** de celle que `artifacts/MANIFEST_g3.json` déclare
  comme `inputs.coastline_1400` ;
- et les deux manifestes G2-bis et G3 sont l'un et l'autre antérieurs à ce lot
  (`MANIFEST_g3.json` est suivi par git et n'a aucune modification ;
  `MANIFEST_g2b.json` n'est pas suivi, c'est un artefact régénéré).

**Donc oui, l'incohérence est antérieure au lot** : le Générateur ne l'a pas
créée, et il ne peut pas la réparer dans le périmètre de D16, qui met
`MANIFEST_g3.json` et les artefacts G3 en lecture seule. Son comportement a
été correct sur ce point : compteur rapporté à `0` — un zéro **mesuré**, pas la
sentinelle —, constat ouvert inscrit dans le journal de preuve, dans
`README.md` et dans une dérogation portant sa commande et son erreur réelles.

**Et pourtant SC7 n'est pas satisfaite.** SC7 exige la valeur `1`. La rubrique
range l'inégalité parmi les échecs disqualifiants, au motif que « la mer et les
cellules ne décrivent pas le même monde ». La table des dérogations recevables
du brief ne contient aucune entrée pour cette affirmation, et elle se clôt sur
« aucune autre dérogation n'est recevable » ; D2 route explicitement ce cas
vers une **escalade au Planificateur**, « jamais auto-accordé comme un succès ».
Ni le Générateur ni moi ne pouvons l'accorder.

Conséquence à ne pas laisser passer sous silence : `MANIFEST_g4.json` fige
désormais, dans un artefact neuf et committé, l'empreinte périmée que G3
déclarait (`coastline_1400_sha_declared_by_g3`). Elle a été **lue** à
l'exécution, pas recopiée à la main, donc ce n'est pas une infraction à la
règle n° `12` ; mais cela propage la valeur morte d'un cran.

**Ce que je demande** — au Planificateur, pas au Générateur : trancher lequel
des deux artefacts committés est faux. Soit `MANIFEST_g3.json` décrit un
littoral que la chaîne ne produit plus (et il faut un lot dédié pour
régénérer G3 ou corriger sa provenance), soit `steps/02b_corrections_1400.py`
a changé de sortie depuis G3 sans que G3 soit rejoué. Tant que ce n'est pas
tranché, SC7 restera hors d'atteinte pour ce lot, quel que soit le nombre
d'itérations : la relancer à l'identique ne peut rien changer.

### B — Les `24` zones hors bornes d'intention ne sont **pas** un motif de rejet

Décision explicite, puisque la question m'est posée. J'ai reconstruit le
compteur moi-même et je retrouve `24` sur `40`, en relisant les trois bornes de
`constants.py` et en appliquant l'exemption de bassin entier (`2` zones).

La rubrique n'en fait **pas** un critère de rejet : elle en fait un compteur à
reconstruire (SC7, point `5`) et exige seulement que, s'il n'est pas nul, il
figure comme constat ouvert dans le journal **et** dans `README.md`, sans
qu'aucune borne ait bougé. J'ai vérifié les trois : le journal de preuve
l'inscrit, la section « Constats ouverts » du README l'inscrit avec sa cause
mesurée, et `git status` sur `constants.py` est vide. D13 le déclare non
bloquant avant toute mesure, et la clause de fin de brief dit noir sur blanc
que le non-respect des bornes d'intention « n'est pas une dérogation : c'est un
constat ouvert à inscrire ».

**Donc : PASS sur ce point.** Ce serait maquiller la rubrique après coup que
d'en faire un rejet. J'en tire en revanche une observation de fond pour le
Planificateur, ci-dessous (observation n° 1) : la cause est réelle et mérite un
lot.

### C — Une empreinte citée par sa valeur, dans un document livré

`deliverables/generator-log.md` écrit, en prose : « la branche
`--source adjacency` produit **les mêmes empreintes** que la preuve (par
exemple `adjacency_g4.json` = `1aba2adc…` dans les deux) », la valeur
hexadécimale étant donnée en entier.

C'est exactement la forme que la règle durement acquise n° `12` interdit : une
empreinte de parité citée par sa **valeur** au lieu de son **nom**, et citée
précisément pour affirmer une égalité. Le brief l'interdit à son non-objectif
n° `16` ; la rubrique la range parmi les échecs disqualifiants, avec le motif
inscrit d'avance : « piège pour tout brief ultérieur, exactement ce qui est
arrivé à l'empreinte citée par le brief `007` ». La démonstration voulue est de
toute façon déjà faite ailleurs, et mieux : le bloc `determinism.sha256` de
`logs/v1_050_qa.json` porte les paires, et j'ai vérifié la parité entre les
deux points d'entrée en recalculant les empreintes, sans qu'aucun texte n'ait
eu besoin de les contenir.

Cas voisin, à trancher et non à ignorer : le champ `error` de la première
dérogation du manifeste contient les **deux** empreintes du constat A, parce
que c'est la sortie littérale d'un `AssertionError`. Il y a là une tension
réelle entre la règle n° `9` (une impossibilité s'éprouve par une commande
**et** son message d'erreur) et la règle n° `12`. Je ne la tranche pas seul,
mais je relève que la dérogation se contredit elle-même : sa propre phrase
affirme « aucune empreinte recopiée » alors que son champ `error` en contient
deux. Voir le feedback pour la correction que je recommande.

---

## Violations de frontière et de périmètre

Rien à signaler, et je l'ai vérifié activement plutôt que supposé :

- `constants.py`, `qa/checks.py`, `pipeline.py`, `io_util.py`,
  `projection.py`, `steps/02_coastline.py`,
  `steps/02b_corrections_1400.py`, `steps/03_cells.py` : `git status` vide sur
  les huit.
- `sim/`, `unity/`, `docs/adr/`, `architecture/`, `ROADMAP.md`, `HANDOFF.md`,
  `VISION.md`, `.github/`, `harness/verdict_audit.py`, `pipeline/geo/data/`,
  `pipeline/geo/sources.lock`, `pipeline/geo/.gitignore` : aucun n'a bougé. Le
  fichier de noms de mer d'Unity est lu, non modifié ; sa copie lui est égale
  octet pour octet, égalité que j'ai recalculée.
- Les `27` fichiers que la branche ajoute par rapport à `master` sont tous dans
  le périmètre de D16 (les deux exceptions apparentes, `brief.md` et
  `eval-rubric.md`, viennent du commit du Planificateur, pas du Générateur).
- Aucun barème, aucun bonus, aucun pourcentage de jeu dans les artefacts.
- Aucune brèche dans le trait de côte : les deux captures du Zuiderzee montrent
  **la même** géométrie, seuls les liens changent.
- Aucun `python` nu, aucun chemin `.venv/Scripts/` dans les livrables.

---

## Les captures, regardées de mes yeux (règle n° `11`)

- `capture/v1_050_sea_zones_window.png` : la fenêtre pilote entière, de
  l'Atlantique ibérique à la mer Noire et du Maghreb à la Baltique. Les zones
  sont des polygones de Voronoï nets, sans trou ni chevauchement visible, qui
  s'arrêtent sur le trait de côte sans mordre sur la terre. Les arêtes
  `sea-sea` sont tracées en pointillés, et **un seul segment rouge** — le lien
  déclaré — apparaît, aux Pays-Bas. On voit à l'œil la cause du constat B :
  des zones comme la Méditerranée orientale couvrent plusieurs centaines de
  milliers de kilomètres carrés, très au-delà du plafond d'intention. On voit
  aussi que les noms sont bien un proxy et non une géographie : une zone au
  nord de la Baltique porte « Mer de Norvège ». Le journal du Générateur
  signale ce même point de lui-même, ce que je note à son crédit.
- `capture/v1_050_zuiderzee_links_on.png` : zoom sur les Pays-Bas. Le
  Zuiderzee est le bassin turquoise fermé au centre, la Lauwerszee le petit
  chapelet saumon le long de la côte. Deux segments rouges descendent d'un
  point hors cadre en haut à gauche jusqu'au cœur de chacun des deux bassins.
  La digue reste dessinée : le lien passe par-dessus, il ne perce rien.
- `capture/v1_050_zuiderzee_links_off.png` : même cadre, même échelle, mêmes
  couleurs, même trait de côte au pixel près — les deux segments rouges ont
  disparu, et les deux bassins sont des culs-de-sac d'eau.

Les trois descriptions du journal du Générateur correspondent à ce que je vois,
y compris le détail gênant qu'il aurait pu taire. `captures_regardees_et_decrites`
= `3` sur `3` est une mesure honnête.

---

## Ce qui a progressé / ce qui a régressé

Première itération de ce brief : il n'y a ni verdict antérieur ni
`feedback/` préexistant, donc rien à comparer. Pour que la boucle reste
calibrée, je consigne quand même ce qui est nettement bien fait, parce que la
sévérité n'est pas une négativité de principe :

- La reconstruction est réelle et non un habillage : `42` compteurs re-dérivés
  indépendamment tombent au même chiffre, y compris ceux qui demandent de
  refaire une géométrie (les `668` largeurs de détroit, l'attribution des `40`
  noms, le parcours d'atteignabilité).
- Le cas rouge de `G4-B` est le cas naturel demandé, alimenté par une vraie
  troisième passe liens coupés — pas une mutation déguisée.
- Le déterminisme tient mieux que ce que le brief exigeait : identique aussi
  entre deux points d'entrée différents.
- Les deux constats gênants sont énoncés sans maquillage, avec leur cause
  mesurée, et aucune borne n'a été déplacée pour les faire disparaître — c'est
  précisément la leçon du brief `007`, et elle a été tenue.

---

## Observations de fond pour le Planificateur (hors motifs de rejet)

1. **Le semis de zones sature sur la borne d'acceptation.**
   `stats_g4.json` déclare `seed_saturated_at_ceiling: true`, et la boucle de
   remplissage de `steps/04_adjacency.py` s'arrête bien sur
   `SEA_ZONE_COUNT_MAX` lu. Conséquence : `zones_mer_denombrees` vaut
   exactement le plafond, si bien que le test « le compte est-il dans la
   fourchette ? » de SC1 ne peut plus rien discriminer. La cause est dans le
   commentaire de `constants.py` lui-même, qui suppose « environ `650 000` km²
   de mer pilote » et prédit « `20` à `30` zones » : la mer réellement retenue
   par la fenêtre actuelle fait environ `5,1` millions de km². Les rayons de
   semis et la fourchette de comptage ont donc été calibrés sur une fenêtre qui
   n'est plus celle du dépôt. C'est la cause commune de la saturation **et** du
   constat B. Le Générateur a eu raison de ne pas toucher la borne ; il revient
   au Planificateur de la re-dériver dans un lot dédié.
2. **Le dénominateur de `plans_eau_exclus_lacs` écrit dans le brief est
   incohérent.** Le brief demande « plans d'eau enclavés examinés », que le
   pipeline mesure à `101`, alors que le nombre de lacs exclus est `107` — un
   compteur plus grand que son dénominateur. Le Générateur a employé `112`
   (lacs exclus plus composantes retenues, ce qui se vérifie : `116`
   composantes d'eau moins `4` éclats sous la tolérance) et a nommé le `101`
   dans sa note. Son choix est le bon ; c'est la formulation du brief qu'il
   faut corriger.
3. **Le cas rouge de `Q4` est le plus grossier des huit.** Il obtient son rouge
   en passant une liste d'arêtes **vide**, donc en isolant tout le graphe d'un
   coup. Il prouve que le contrôle rougit sur un monde entièrement déconnecté,
   pas qu'il repère **une** entité isolée. Ce n'est pas un motif de rejet — la
   rubrique n'exige pas la minimalité du cas rouge — mais c'est exactement le
   coût de la règle n° `6` : un contrôle trop grossier coûte aussi cher qu'un
   contrôle laxiste.
4. **`logs/v1_050_adjacency.log` embarque une durée d'horloge murale** et des
   chemins absolus de la machine. D11 n'interdit l'horloge que dans un
   *artefact*, et ce journal n'en est pas un, donc ce n'est pas une infraction.
   Mais c'est le seul fichier que ma ré-exécution a fait diverger, ce qui
   affaiblit la belle propriété « rejouer ne produit aucune différence ».
