# Verdict — Brief `018` : la Province dérivée (agrégation de cellules)

**Authored**: 2026-08-14T07:02:30Z
**Author**: forge-evaluateur

## Note de transparence

Le rôle signataire est le rôle natif du harnais, `forge-evaluateur`. Aucun
suffixe n'est ajouté : le contrôle mécanique `verdict_is_not_self_authored`
compare les acteurs de part et d'autre d'un lot, et un couple de signatures
suffixées serait refusé.

L'acteur réel est un sous-agent Cursor Cloud, modèle Claude Opus 5
(`claude-opus-5-thinking-high`), orchestré par un agent Cursor Cloud (Grok 4.6)
qui remplace le CTO Claude (plafond de quota atteint). La session est
**distincte** de celle du Planificateur **et** de celle du Générateur : aucun
état n'est partagé, aucun raisonnement de production n'est repris.

Je n'ai touché **aucune ligne de code**. Le seul fichier du dépôt que j'écris
est ce `verdict.md`. Toutes mes contre-preuves ont été montées dans des copies
**hors du dépôt**, sous `/tmp/eval-018b/copies/` (copies sans `.git`, caches
Python purgés avant chaque rejeu). Mes scripts de reconstruction sont
`/tmp/eval-018b/recon_sc1.py` et `/tmp/eval-018b/recon_sc3.py`, mes journaux
sous `/tmp/eval-018b/logs/`.

Le commit jugé est celui du Générateur, `5432df7`. Le commit du Planificateur,
`bf6ec07`, n'est pas jugé ici.

**Un mot sur l'état initial du poste de travail.** En ouvrant la session, j'ai
trouvé un `verdict.md` non suivi par git, daté de `06:46`, laissé par une session
d'évaluation antérieure sur cette même machine, ainsi que ses fichiers de
travail sous `/tmp/eval-018/`. Je ne l'ai ni relu comme source, ni repris. Je
l'ai déplacé hors du dépôt (`/tmp/eval-018b/prior_verdict_NOT_MINE.md`) pour
rendre à la porte mécanique l'état vierge qu'elle attend, puis j'ai refait
l'intégralité de la reconstruction et des sabotages dans un répertoire neuf.
Aucun nombre, aucune formulation et aucune réserve du présent verdict n'en
provient : signer un travail que je n'ai pas fait moi-même serait exactement
la complaisance que ce rôle existe pour empêcher.

---

## 1. Résultat de la porte mécanique

Commande, exécutée **avant** la rédaction de ce fichier, sur un dépôt propre :

`.venv/bin/python harness/verdict_audit.py harness/queue/briefs/018-sim-province-derivee`

Rapport conservé : `/tmp/eval-018b/logs/gate_pre_verdict.txt`. Code de sortie 1,
`VERDICT: REJECT`, sur **exactement deux** contrôles :

| contrôle | état | cause |
|---|---|---|
| `verdict_numbers_traceable` | FAIL | `verdict.md` manquant |
| `verdict_is_not_self_authored` | FAIL | en-tête `Author` absent de `verdict.md` |

Les **huit** contrôles de fond sont au vert : `files_declared_exist`,
`mtime_after_brief`, `captures_differ_when_should`,
`waivers_have_command_and_error`, `no_empty_sample_pass`,
`no_bare_python_alias`, `rubric_predates_deliverables`,
`declared_files_are_tracked`.

Ces deux FAIL sont **définitionnels**, pas substantiels : ils constatent
l'absence du fichier que je suis en train d'écrire. Ce n'est donc pas un REJECT
de fond, et je poursuis. Le PASS prononcé plus bas est **conditionné au rejeu
de la porte par l'orchestrateur après le commit de ce `verdict.md`** : elle doit
alors répondre `ACCEPT`.

Deux limites de la porte que j'ai comblées moi-même, parce qu'un contrôle qui
s'arrête à mi-chemin n'a pas contrôlé :

- `declared_files_are_tracked` n'a vérifié que les 5 fichiers internes au
  dossier du brief et a explicitement laissé de côté les `12` déclarés à
  l'extérieur. J'ai vérifié les **`17`** : tous existent, tous sont suivis par
  git.
- `no_empty_sample_pass` vérifie que les échantillons ne sont pas nuls, pas
  qu'ils sont exacts. J'ai reconstruit les 22 compteurs (section 2).

---

## 2. Ce que j'ai reconstruit moi-même

Méthode : je n'ai repris aucun nombre du manifeste, et je n'ai **pas réutilisé
la géométrie de `sim/aggregation.py`**. J'ai réécrit ma propre lecture des
fichiers, ma propre projection (`x = lon × cos(mid_latitude)` via
`math.radians`, `y = −lat`), ma propre recherche du centre le plus proche et
mon propre départage, puis j'ai recompté. Le seul emprunt au dépôt est
`World.from_g3(rng_seed=42)`, qui **est** l'échantillon exigé par le brief.

Sorties : `/tmp/eval-018b/logs/recon_sc1.txt` et `recon_sc3.txt`.

| compteur | manifeste | ma reconstruction | concordance |
|---|---|---|---|
| `cellules_chargees_g3` | 596 / 596 | 596 / 596, `cell_count` relu de `stats_g3.json` = 596 | oui |
| `centroides_lus` | 50 / 50 | 50 / 50, longueur de `coordinates` relue | oui |
| `cellules_avec_province` | 596 / 596 | 596 / 596 ; `cellules_en_double` = 0 | oui |
| `cellules_sans_province` | 0 / 596 | 0 / 596 | oui |
| `cellules_position_absente` | 0 / 596 | 0 / 596 | oui |
| `refus_position_absente_leve` | 1 / 1 | 1 / 1, sur **ma** cellule (`9788`), nommée dans le message | oui |
| `provinces_non_vides` | 50 / 50 | 50 / 50 ; plus petit groupe peuplé = 2 cellules | oui |
| `champs_province_sur_entites` | 0 / 14 | 0 / 14 (7 `Cell` + 4 `CentreAdministratif` + 3 `Regroupement`) | oui |
| `dataclasses_inspectees` | 3 | 3, découvertes par mon propre balayage | oui |
| `garde_prefixe_variantes_rouges` | 5 / 5 | 6 / 6 — j'ai ajouté `provinceid`, elle lève aussi | oui (surensemble) |
| `champs_vue_couverts` | 7 / 7 | 7 / 7, chacun avec un site de lecture réel et **correctement typé** | oui (voir réserve N1) |
| `redessin_change_agregat` | 1 / 1 | 1 / 1 | oui |
| `cellules_changeant_de_province_apres_redessin` | 22 / 596 | 22 / 596 | oui |
| `redessin_cellules_intactes` | 1 / 1 | 1 / 1 (sérialisation **et** relevé `vars()`) | oui |
| `attributs_dynamiques_sur_cellules` | 0 / 596 | 0 / 596 | oui |
| `fichier_centroides_inchange_apres_redessin` | 1 / 1 | 1 / 1 (octets relus) | oui |
| `determinisme_agregation_deux_passes` | 1, sur 596 | 1 — j'ai ajouté un **quatrième** appel en ordre aléatoire : 596 / 596 | oui (surensemble) |
| `departage_egalite_plus_petit_id` | 1, sur 2 ordres | 1 ; gagnants `[3, 3]` ; égalité des carrés vérifiée **exacte** | oui |
| `egalites_de_distance_monde_reel` | 0 / 596 | 0 / 596 (voir réserve N2) | oui |
| `compteurs_en_dur_trouves` | 0 / 41 | 0 ; **41** fonctions recomptées par mon propre balayage AST | oui (voir réserve N5) |
| `tests_sim_passed_018` | 65 / 65 | 65 collectés, 65 passés | oui |
| `tests_harness_passed_018` | 348 / 364 | 348 passés + `16` ignorés = 364 collectés | oui |

**Les 22 compteurs sont reproduits.** Aucun ne m'a résisté. Surtout,
l'appartenance calculée par `sim/aggregation.py` coïncide avec la mienne sur
les **596 cellules, avec 0 désaccord** : la couverture n'est pas seulement
comptée, elle est correcte cellule par cellule.

L'échantillon est bien le monde réel — `World.from_g3(rng_seed=42)`, 596
cellules. Aucun compteur de couverture ne vient d'un monde construit à la main
ni d'un monde à zéro cellule.

---

## 3. Verdict ligne à ligne de la rubrique

### SC1 — Couverture totale : PASS

| point de la rubrique | état | preuve |
|---|---|---|
| les 6 compteurs nommés, chacun avec son dénominateur | PASS | rejeu : `/tmp/eval-018b/logs/rejeu_sc1.txt` |
| `cellules_chargees_g3 == cell_count` du fichier | PASS | 596 = 596, relu par moi de `stats_g3.json` |
| `centroides_lus ==` longueur de `coordinates` | PASS | 50 = 50 |
| `cellules_avec_province == cellules_chargees_g3`, `cellules_sans_province == 0` | PASS | reconstruit ; la sentinelle `-1` n'apparaît nulle part |
| suite `-k province` verte | PASS | incluse dans les 65 verts |
| `provinces_non_vides` sans plancher exigé | PASS | seul assert : `0 < provinces_non_vides <= centroides_lus` ; aucun littéral `50` dans les tests du lot |

**Contre-preuve D5, montée par moi** (`/tmp/eval-018b/logs/contre_preuve_d5.txt`) :
j'ai retiré en mémoire la position de la cellule **`9788`** — mon choix, pas
celui du Générateur, qui avait employé la `1175`. `positions_du_monde` **et**
`agregat_depuis_monde` lèvent tous deux `PositionCelluleInconnue`, et le
message **nomme la cellule**. Aucune province par défaut, aucun écart
silencieux. Le refus n'est pas cantonné à la fonction interne : c'est le chemin
complet qui refuse.

**Sur le 50 / 50, que la rubrique m'invite explicitement à suspecter.** J'ai
vérifié qu'il s'agit d'un fait géométrique et non d'un plancher déguisé. Le
plus petit regroupement peuplé compte **2** cellules, la distribution des
tailles va de 2 à `48`, et leur somme vaut exactement 596. Avec 596 cellules pour
50 centres sur ce territoire, qu'aucun centre ne reste vide est le résultat
attendu de la géométrie. Aucun test n'impose de plancher. Voir tout de même les
réserves N3 et N4.

### SC2 — Garde spatiale exercée, pas affaiblie : PASS

| point de la rubrique | état | preuve |
|---|---|---|
| aucun champ `province*` sur `Cell` | PASS | 7 champs, aucun fautif |
| `TypeError` citant l'`ADR-0003` sur sous-classe fabriquée | PASS | 6 variantes essayées par moi, toutes lèvent en citant l'`ADR-0003` |
| vérification **introspective**, pas nominative | PASS | `inspect.getmembers` ; le préfixe est **dérivé** de `_NoBadSpatialField._FORBIDDEN_PREFIX`, jamais recopié |
| couvre aussi les types du module d'agrégation | PASS | `CentreAdministratif` et `Regroupement` héritent de la garde et sont balayées |
| `test_adr_compliance.py` non affaibli | PASS | `git diff bf6ec07 5432df7` : **purement additif**. La seule ligne retirée de tout le commit est une ligne de `sim/README.md` reformulée |
| `champs_province_sur_entites == 0` avec dénominateur > 0 | PASS | 0 / 14, `dataclasses_inspectees` = 3 |

**Contre-preuve paire A, montée par moi** (`/tmp/eval-018b/logs/sabotage_a.txt`) :
copie hors dépôt `sab_a`, champ `province_id: int = field(default=-1)` ajouté
sur `Cell`, caches purgés. Résultat : **8 FAILED, 6 passed**, dont le cas
nominatif historique `test_cell_has_no_province_id_field` **et** le cas
introspectif ajouté par ce lot,
`test_aucune_dataclass_de_sim_model_ne_porte_de_province`. La garde protège
réellement quelque chose.

J'ai également vérifié que la garde s'applique à une dataclass **gelée** — la
forme qu'ont `CentreAdministratif` et `Regroupement` : `TypeError` citant
l'`ADR-0003`.

### SC3 — Redessin : l'agrégat change, les cellules ne sont pas réécrites : PASS

Scénario monté **par moi**, avec ma propre implémentation de l'appartenance
(`/tmp/eval-018b/recon_sc3.py`, sortie `/tmp/eval-018b/logs/recon_sc3.txt`) :
centre de plus petit `id` (1, Île-de-France) déplacé en mémoire sur la position
exacte de la cellule `1175`, qui relevait du centre `12`.

| fait exigé | mon résultat |
|---|---|
| l'agrégat change | 22 cellules sur 596 changent d'appartenance ; la cible relève désormais du centre 1 |
| `json.dumps(world.to_dict(), sort_keys=True)` identique | identique |
| relevé `vars(cell)` identique champ par champ | identique, 596 / 596 |
| aucun attribut dynamique apparu | 0 / 596 |
| fichier de centres inchangé sur le disque | octets identiques |
| `git status pipeline/geo/` après tous mes rejeux | aucune modification |

Je souligne que le script du Générateur **dérive** sa cellule cible (première
cellule triée ne relevant pas du centre de plus petit `id`) au lieu de la
coder en dur : le `1175` n'est pas un nombre magique, et ma propre dérivation
indépendante retombe dessus.

**Contre-preuve paire B, montée par moi** (`/tmp/eval-018b/logs/sabotage_b.txt`) :
copie hors dépôt `sab_b`, l'agrégation estampille
`world.cells[cell_id].zone_admin = centre_id`. J'ai **d'abord** vérifié que
`zone_admin` échappe bien à la garde de préfixe (une dataclass portant ce champ
s'instancie sans erreur) — le sabotage porte donc sur la propriété, pas sur le
nom. Résultat : le test de redessin passe au **FAILED**
(`attributs_dynamiques_sur_cellules = 596 / 596`,
`redessin_cellules_intactes = 0 / 1`), **tandis que `test_adr_compliance.py`
reste vert (6 passed)**. C'est exactement la discrimination exigée par la règle
n° 6 : c'est le test de redessin qui garde la propriété, pas la règle de nom.

Le test ne se contente pas de compter les provinces peuplées : il vérifie les
deux faits simultanément (assertions sur `redessin_change_agregat`,
`cellules_changeant`, `attributs_dynamiques`, `redessin_cellules_intactes` et
`fichier_centroides_inchange`). J'ai confirmé au passage que la sérialisation
`to_dict()` seule ne suffirait pas — elle n'expose que les champs déclarés,
donc un attribut dynamique lui échapperait. C'est le relevé `vars()` qui ferme
ce trou, et il est présent.

### SC4 — Fonction pure, déterminisme, départage nommé avant mesure : PASS

| point de la rubrique | état | preuve |
|---|---|---|
| signature sans état global mutable, ne rend aucune `Cell` modifiable | PASS | `derive_appartenance(positions, centres, latitude_moyenne)` ; la vue ne transporte que des `int` (vérifié) |
| suite `-k "determinisme or departage or purete"` verte | PASS | incluse dans les 65 verts |
| déterminisme : 2 appels + ordre inverse | PASS | reconstruit ; j'ai ajouté un ordre **aléatoire** : 596 / 596 identiques sur quatre appels |
| pureté : les entrées ne sont pas modifiées | PASS | dictionnaire de positions et liste de centres inchangés après appel |
| départage documenté dans `SEEDING.md` **avant** toute citation de compteur | PASS | la section `018` énonce la règle et ne cite **aucune** valeur mesurée du lot |
| cas synthétique équidistant, deux ordres, plus petit `id` gagne | PASS | gagnants `[3, 3]` ; j'ai vérifié que l'égalité des carrés est **exacte**, sans quoi le test ne mesurerait rien |
| `egalites_de_distance_monde_reel` sans sentinelle | PASS | 0 / 596, réellement calculé (réserve N2) |
| `test_no_hardcoded.py` PASSED, `compteurs_en_dur_trouves = 0` | PASS | 0 / 41 ; aucun littéral `47.5`, `180` ni `pi` dans `sim/aggregation.py` |

**Contre-preuve du départage, montée par moi**
(`/tmp/eval-018b/logs/sabotage_departage.txt`) : copie hors dépôt `sab_dep`,
départage remplacé par « dernier centre parcouru gagne » (`carre <=
meilleur_carre`). Résultat : `test_departage_egalite_plus_petit_id` passe au
**FAILED**, avec `gagnants = [7, 3]` — le départage devient dépendant de
l'ordre, et le test le voit. Il mesure donc la stabilité, pas seulement le
résultat.

### SC5 — Source déclarée comme proxy : PASS

La section « Brief `018` » de `sim/SEEDING.md` dit explicitement : centres
**hérités du jeu**, lus de
`pipeline/geo/legacy_game_data/province_coordinates.json` ; **pas** des
frontières historiques de `1400` et aucune prétention au statut de source savante
ni de reconstitution d'époque (la seule occurrence de « `1400` » du texte est
dans cette négation) ; la projection employée et le fait que son paramètre est
**lu du fichier** ; la règle de départage ; la politique de refus de deviner ;
et la distinction entre le zéro mesuré et la sentinelle `-1`.

Ordre d'écriture : la section ne cite **aucune** valeur mesurée du lot — j'ai
vérifié qu'aucune occurrence de 596, 50, 22, 65 ou 348 n'y figure. L'exigence
« documentation avant mesure » est donc satisfaite sans ambiguïté possible :
il n'y a aucune valeur à ordonner.

Reconstruction : j'ai relu le fichier de centres. Il déclare lui-même
`projection.type = equirectangular`, son `mid_latitude`, et se décrit comme
« coordonnées approximatives, corrigeables à vue ». La documentation ne prête
aux données **aucune** propriété que le fichier ne porte pas — elle reprend au
contraire son aveu d'approximation.

`sim/README.md` est **descriptif** (quels modules existent, quelles données ils
lisent) et ne contient aucune instruction adressée à un agent.
`harness/tests/test_single_source_of_instruction.py` : PASSED.

### SC6 — Preuves rouges : deux paires, sabotage hors dépôt : PASS

| exigence | état | détail |
|---|---|---|
| 4 fichiers en `.txt` | PASS | jamais `.log` ; les 4 sont suivis par git |
| rouge A contient un `FAILED` | PASS | `2 failed, 4 passed` |
| vert A uniquement `PASSED` | PASS | `6 passed` |
| rouge B contient un `FAILED` | PASS | `1 failed, 1 passed` |
| vert B uniquement `PASSED` | PASS | `2 passed` |
| paires déclarées avec `must_differ_from` en chemins relatifs | PASS | les deux ; `captures_differ_when_should` au vert |
| sabotage hors dépôt | PASS | en-têtes des rouges : `rootdir: /tmp/forge-018-red-a` et `/tmp/forge-018-red-b` ; les verts portent `rootdir: /workspace` |
| aucune valeur hexadécimale de condensé recopiée | PASS | balayage de `sim/` et du dossier du brief : aucune |
| **je reproduis les deux rouges moi-même** | PASS | sections SC2 et SC3 ci-dessus |

Mes deux rouges concordent avec ceux du Générateur (`2 failed, 4 passed` pour
A ; `1 failed, 1 passed` pour B), obtenus indépendamment, dans mes propres
copies.

### SC7 — Scripts de mesure, manifeste, suite verte : PASS

| exigence | état | détail |
|---|---|---|
| les 2 scripts rejouent depuis la racine | PASS | `/tmp/eval-018b/logs/rejeu_sc1.txt`, `rejeu_sc3.txt`, code de sortie 0 |
| chaque compteur imprimé porte son dénominateur | PASS pour les 2 scripts | voir réserve N5 pour `compteurs_en_dur_trouves`, produit par un autre test |
| `sample_size` réel, non nul, hors sentinelle | PASS | 22 compteurs, aucun à 0 ni à −1 |
| `pytest sim/tests/ -v` | PASS | 65 passés, 0 échec |
| `pytest harness/tests/ -q` | PASS | 348 passés, `16` ignorés (Unity/Linux, déclarés), 0 échec |
| archives `011`–`017` intactes | PASS | absentes du diff, `git status` propre |
| `sim/engine.py` inchangé | PASS | absent du diff |
| le Générateur n'a ni committé, ni poussé, ni créé de branche | PASS | les 2 commits du lot portent l'identité de l'orchestrateur (`Cursor Agent`) ; branches locales : `forge/018-province-derivee-779a` (fournie) et `master` |

Périmètre du commit : **`17` fichiers**, tous autorisés. J'ai vérifié
explicitement qu'aucun fichier interdit n'est touché : `sim/engine.py`,
`pipeline/geo/`, `unity/`, `architecture/`, `harness/pipeline/`, tout
`harness/*.py`, `VISION.md`, `ROADMAP.md`, `HANDOFF.md`, `.github/`, et les
archives `011` à `017`. Ni `brief.md` ni `eval-rubric.md` n'ont été modifiés.

Le journal du Générateur est signé `forge-generateur`, porte sa note de
transparence, et **ne prononce pas la recevabilité** — il constate au contraire
que les deux contrôles de verdict échouent nécessairement à son stade et que
c'est à l'Évaluateur d'écrire le fichier.

### SC8 — Registre de coût : PASS

`ledger.py report` fait apparaître
`harness/queue/briefs/018-sim-province-derivee: cursor=1`. La dernière ligne de
`harness/queue/cost-ledger.jsonl` porte `event = generator-run`,
`backend = cursor`, et un chemin de brief contenant `018` ; pas d'`audit_id`,
ce qui est normal puisque ce brief naît de la feuille de route.

### Dérogation invoquée

Une seule, recevable au titre du tableau du brief : budget d'exécution
`UNMEASURABLE`. J'ai rejoué la commande : elle reproduit exactement l'erreur
citée (aucune transcription d'agent nommant le brief sous le chemin attendu).
C'est la situation connue sur VM fraîche, pas un défaut.
`waivers_have_command_and_error` : PASS.

---

## 4. Verdict global : **PASS**

Les huit conditions de succès sont satisfaites, chacune vérifiée par
reconstruction indépendante et, là où la rubrique l'exigeait, par un sabotage
que j'ai monté moi-même hors dépôt. Les trois contre-preuves disqualifiantes
rougissent bien :

1. champ `province_id` sur `Cell` → 8 FAILED, dont le cas introspectif du lot ;
2. estampillage `zone_admin`, hors garde de préfixe → test de redessin FAILED,
   `test_adr_compliance.py` resté vert ;
3. départage « dernier parcouru gagne » → test de départage FAILED.

Ce que je retiens de plus solide : la chaîne causale du brief est réellement
mise en œuvre, pas simulée. Le déplacement d'un centre fait basculer 22
cellules sans qu'une seule ligne de cellule ne soit réécrite, et j'ai obtenu ce
même 22 avec une implémentation entièrement indépendante de celle du
Générateur.

**Condition suspensive :** ce PASS suppose que l'orchestrateur rejoue la porte
mécanique après le commit de ce `verdict.md` et obtienne `VERDICT: ACCEPT`. Les
deux seuls FAIL constatés en section 1 étaient l'absence de ce fichier ; ils
doivent disparaître. Si un autre contrôle rougissait au rejeu, ce verdict
serait caduc.

---

## 5. Écarts de périmètre

**Aucun.** Les 14 non-goals sont respectés :

- `sim/engine.py` intact ; le pas de temps ne consomme pas l'agrégation (D7) ;
- rien d'écrit sous `pipeline/geo/` — vérifié par comparaison d'octets **et**
  par `git status` après exécution de tous les scripts et des deux suites ;
- aucune dataclass ajoutée à `sim.model`, aucun champ ajouté à `Cell` (D3) : la
  vue vit dans `sim/aggregation.py` ;
- aucune constante de survie, de nourriture ou de population retouchée ;
  `sim/constants.py` n'est pas dans le diff ;
- garde et tests ADR élargis, jamais restreints ;
- archives `011`–`017` intactes ; la réserve N1 du lot `017` n'est pas traitée, comme
  exigé.

Une observation de forme, sans conséquence : le brief définit le dénominateur
de `champs_vue_couverts` comme « le nombre de champs déclarés **sur la vue** »,
c'est-à-dire les 3 champs de `Regroupement`. Le Générateur a balayé les deux
dataclasses du module (7 champs). C'est un surensemble, donc plus exigeant et
non plus laxe — j'y vois une extension légitime, pas un écart.

---

## 6. Réserves non bloquantes (pour le prochain Planificateur)

Aucune de ces réserves ne contredit une condition de succès du brief `018` : les
SC tiennent. Elles nomment des trous de robustesse qu'un lot ultérieur devrait
fermer avant que la Province ne devienne un acteur économique.

**N1 — La détection de lecture de la couverture de la vue ignore le type de
l'objet lu.** C'est la réserve la plus importante, et la seule que j'aie réussi
à transformer en faux vert. `_sites_de_lecture()` dans
`sim/tests/test_province_aggregation.py` retient **tout** accès d'attribut
`quelquechose.champ` dont le nom figure parmi les champs de la classe, sans
regarder sur quel objet la lecture porte. Or `id` et `name` sont des champs de
`CentreAdministratif` **et** de `Regroupement` : une lecture `centre.id` compte
comme lecture de `Regroupement.id`.

Aujourd'hui, cela ne produit **aucun faux vert** : j'ai vérifié un par un que
les trois champs de `Regroupement` ont chacun une lecture réelle et
correctement typée (`regroupement.cell_ids` et `regroupement.id` dans
`appartenance_depuis_regroupements`, `regroupement.name` dans
`nom_de_province_de_cellule`), et de même pour les quatre champs de
`CentreAdministratif`. Le 7 / 7 est donc honnête. Mais le contrôle est
exploitable, et je l'ai prouvé
(`/tmp/eval-018b/logs/sonde_couverture_vue.txt`) : dans une copie hors dépôt,
j'ai ajouté à `Regroupement` un champ `lat` construit par `lat=centre.lat` que
**personne** ne lit jamais sous la forme `regroupement.lat` — le test annonce
`champs_vue_couverts = 8 / 8` et **passe**. Le mode d'échec n° 2 (« champ
déclaré que personne ne lit ») n'est donc fermé, pour la vue, que pour les noms
de champs qui ne sont partagés avec aucune autre classe.

*Comment corriger, précisément :* aligner la détection de lecture sur la
convention que `sim/tests/test_write_coverage.py` emploie déjà, à savoir exiger
que la base de l'accès soit une variable au nom conventionnel (nom de classe en
minuscules). Concrètement, pour `Regroupement`, n'accepter comme lecture qu'un
`ast.Attribute` dont le `.value` est un `ast.Name` valant `regroupement` — ou
une variable dont l'affectation provient d'un appel `Regroupement(...)`. Le
même durcissement doit s'appliquer à `CentreAdministratif`. Ne **pas** se
contenter de retirer `CentreAdministratif` du balayage : ce serait rétrécir le
contrôle, ce que le contrat d'exécution interdit.

**N2 — `egalites_de_distance_monde_reel` n'a presque aucun pouvoir de
discrimination, et son assertion est vide.** Le compteur vaut 0 / 596, et c'est
honnête : il est réellement calculé, la sentinelle n'est pas employée. Mais une
égalité **exacte** entre deux carrés de distance en virgule flottante sur des
coordonnées réelles est structurellement quasi impossible : ce zéro serait
sorti identique même si le départage était faux. J'ai mesuré l'écart relatif
entre le premier et le deuxième centre le plus proche, cellule par cellule : le
minimum observé est **`3,5 × 10⁻³`** (cellule `1324`, centres `34` contre `35`),
soit environ **`1,6 × 10¹³` fois l'epsilon machine**. S'ajoute que l'assertion du test
est `assert egalites >= 0`, vraie par construction. Deux lectures :

- rassurante — aucune attribution du monde réel ne peut basculer par bruit de
  flottant ; le déterminisme constaté est robuste, pas chanceux ;
- limitante — la preuve de D4 repose **entièrement** sur l'unique cas
  synthétique. Le compteur du monde réel est un garde-fou, pas une preuve.

*Comment corriger :* faire dire cela à `sim/SEEDING.md` (le compteur monde réel
est un contrôle de cohérence ; la preuve du départage est le cas synthétique),
et ajouter au lot suivant un cas de **quasi**-égalité — deux centres dont les
carrés de distance ne diffèrent que de quelques epsilons — pour documenter le
comportement à la frontière, là où le `==` exact ne s'applique plus.

**N3 — La branche « un centre n'attire aucune cellule » (D6) n'est exercée par
aucun test livré.** Sur le monde réel, les 50 centres sont peuplés : le chemin
« regroupement vide » n'est jamais parcouru par la suite. J'ai vérifié à la
main que le code le tolère — avec deux centres et toutes les cellules sur le
premier, `regroupements_depuis_appartenance` produit bien un `Regroupement` à
`cell_ids = ()`, `regroupements_non_vides` rend 1 / 2, et `province_de_cellule`
d'une cellule inconnue rend `None`. Mais **rien dans la suite ne le prouve** :
si une correction de données vidait un centre, ou si une refonte cassait ce
cas, aucun test ne rougirait.

*Comment corriger :* un test synthétique avec un centre volontairement non
peuplé, vérifiant que la vue le contient avec `cell_ids` vide, que
`regroupements_non_vides` l'exclut, et que rien ne lève.

**N4 — `provinces_non_vides = 50 / 50` est indistinguable, au seul vu du
nombre, d'un plancher imposé.** J'ai établi que c'est bien un fait mesuré
(aucune assertion de plancher, aucun littéral `50` dans les tests du lot, plus
petit groupe peuplé = 2 cellules). Mais un lecteur futur — ou un auditeur — n'a
que le nombre, et ce nombre est exactement celui qu'un plancher produirait.

*Comment corriger :* rapporter aussi la **distribution** (taille du plus petit
et du plus grand regroupement) comme compteur, ou consigner dans
`sim/SEEDING.md` que le plus petit groupe compte 2 cellules. C'est cette
distribution, et non l'égalité 50 = 50, qui atteste l'origine géométrique du
résultat.

**N5 — Le dénominateur de `compteurs_en_dur_trouves` n'est pas imprimé par la
commande qui produit le compteur.** Le manifeste déclare 41 fonctions
inspectées, mais `test_no_hardcoded.py` n'imprime que la liste des 6 fichiers.
J'ai dû recompter par balayage AST pour vérifier : c'est **exactement 41**
(`aggregation.py` 14, `constants.py` `12`, `engine.py` 9, `world.py` 5,
`model.py` 1, `__init__.py` 0). Le nombre est juste, mais il n'est pas
re-vérifiable en lisant la sortie du test — seulement en réécrivant le
balayage.

*Comment corriger :* faire imprimer par `test_no_hardcoded.py` le nombre de
fonctions inspectées, et pas seulement les noms de fichiers. C'est un test
historique du dépôt, pas une création de ce lot ; l'élargir est autorisé.

**N6 — Le chemin de consultation est linéaire, et ce sera le chemin chaud.**
`province_de_cellule` parcourt tous les regroupements puis teste
l'appartenance à un tuple : un coût de l'ordre du nombre de centres multiplié
par la taille des groupes, **par consultation**. Le brief exclut explicitement
le coût de son périmètre, et j'applique cette exclusion. Mais dès que le pas de
temps consommera l'agrégation (fiscalité, commerce inter-provinces), cette
fonction sera appelée par cellule et par tick.

*Comment corriger, le jour où ce sera pertinent :* exposer l'appartenance
`cell_id → id` (déjà produite par `appartenance_depuis_regroupements`) comme
chemin de consultation, et garder `province_de_cellule` pour l'usage ponctuel.
Ce n'est pas un cache d'état persistant — donc pas une violation de l'`ADR-0003`
— mais une valeur de retour recalculée. Le distinguer explicitement évitera
qu'un lot ultérieur croie devoir stocker l'appartenance « pour la
performance », ce que l'ADR interdit.

**N7 — L'adaptateur reçoit le `World` vivant, pas une vue en lecture seule.**
`agregat_depuis_monde(world, …)` lit `world.cells`, et rien, structurellement,
n'empêcherait un futur contributeur d'y écrire : c'est mon sabotage B, en trois
lignes. La propriété est gardée par **un seul** test
(`sim/tests/test_redessin_province.py`), certes efficace — je l'ai fait rougir.
La sortie est bien protégée (la vue ne transporte que des `int`, vérifié) ;
c'est l'entrée qui reste une référence mutable.

*Comment corriger :* faire que l'adaptateur extraie les identifiants et
positions, puis n'appelle l'agrégation qu'avec des données inertes, de sorte
que `derive_appartenance` ne détienne jamais de référence à une `Cell`. La
garde resterait le test, mais la tentation disparaîtrait du type.

---

## 7. Ce qui s'est amélioré / régressé

**Amélioré depuis le lot `017` :**

- *Le sabotage est enfin choisi pour discriminer, pas seulement pour rougir.*
  La paire B emploie `zone_admin`, un nom que la garde de préfixe ne rattrape
  pas — j'ai vérifié les deux faces : le test de redessin rougit,
  `test_adr_compliance.py` reste vert. Un lot moins rigoureux aurait saboté
  avec `province_*` et « prouvé » la mauvaise garde. C'est la règle n° 6
  réellement appliquée.
- *Le préfixe interdit est dérivé de la garde, pas recopié.*
  `_PREFIXE_INTERDIT = _NoBadSpatialField._FORBIDDEN_PREFIX` : si la garde
  change, le test suit au lieu de la contredire. Règle n° 2 bien comprise.
- *Le test de conformité ADR a été élargi sans être touché ailleurs.* Le diff
  est purement additif : le cas introspectif s'ajoute aux quatre cas nominatifs
  historiques, qui restent tous présents et verts. Sur l'ensemble du commit, la
  seule ligne retirée est une ligne de README reformulée.
- *L'égalité du cas synthétique est elle-même testée.* Un test dédié prouve que
  les deux carrés de distance sont **exactement** égaux — sans quoi le test de
  départage n'aurait rien mesuré. Peu de lots pensent à valider la validité de
  leur propre cas de test.
- *La documentation ne cite aucun nombre du lot.* Le débat « documentation
  avant ou après la mesure » est rendu sans objet : il n'y a aucune valeur
  mesurée à ordonner. C'est plus robuste que de simplement placer le texte
  avant.
- *Deux niveaux de refus pour D5.* Le refus est vérifié à la fois sur la
  fonction interne et sur le chemin complet — l'exception n'est pas cantonnée à
  une couche que l'appelant pourrait contourner.
- *La cellule cible du redessin est dérivée, pas codée en dur.* Le scénario
  reste concluant même si les données changent.

**Régressé :** rien. Aucune archive touchée, aucune garde affaiblie, aucun
contrôle rétréci, `sim/engine.py` intact, les deux suites entièrement vertes.

Le seul point où ce lot est **moins strict** que le dépôt existant est nommé en
N1 : `test_write_coverage.py` exigeait une variable au nom conventionnel ; le
nouveau contrôle de couverture de la vue a relâché cette exigence sur la
lecture. Ce n'est pas la régression d'un contrôle existant — celui-ci n'a pas
été modifié — mais un contrôle **neuf** plus faible que son aîné.

---

## 8. Retour pour la suite

Le lot est recevable et la chaîne causale est prouvée. Pour le prochain
Planificateur, par ordre d'urgence :

1. **N1 en premier** : durcir la détection de lecture de la couverture de la
   vue. C'est le seul contrôle de ce lot que j'aie réussi à faire passer au
   vert avec un champ mort. Tant qu'il reste tel quel, le mode d'échec n° 2 est
   partiellement ouvert sur la vue dérivée — et la vue est précisément l'objet
   qui va grandir quand la Province deviendra un acteur économique.
2. **N3 et N4 ensemble** : ils portent tous deux sur le fait que le monde réel
   ne produit aucune province vide. Un seul test synthétique ferme N3, et
   rapporter la distribution ferme N4.
3. **N7 avant que le pas de temps ne consomme l'agrégation** : le jour où
   `tick` appellera l'agrégation, l'entrée mutable deviendra un vrai risque, et
   N6 (chemin de consultation linéaire) un vrai coût. Les traiter dans le lot
   qui branche la Province sur l'économie, pas après.
4. **N2 et N5** relèvent de la rigueur de mesure, pas du risque de défaut : à
   grouper avec un lot de nettoyage.

Un mot sur la méthode, à conserver : ce lot est celui où j'ai pu reconstruire
**les 22 compteurs sans exception** et retrouver le même nombre de bascules
(22 sur 596) avec une implémentation entièrement indépendante, sans aucun
désaccord sur les 596 cellules. C'est cela qui rend le PASS solide — pas la
suite verte, qui ne prouve jamais rien à elle seule.
