# Brief 018 : la Province dérivée — le regroupement qui se recalcule au lieu d'être estampillé

**Authored**: 2026-08-14T05:53:00Z
**Author**: forge-planificateur

> **Note de transparence (contrat du Planificateur) :** le rôle signataire est
> le rôle natif du harnais `forge-planificateur`. L'acteur réel est un
> sous-agent Cursor Cloud (modèle Claude Opus 5), orchestré par un agent Cursor
> Cloud qui remplace le CTO Claude (plafond de quota atteint). Aucun suffixe
> n'est ajouté à la signature : le contrôle mécanique
> `verdict_is_not_self_authored` compare les acteurs de part et d'autre d'un
> lot, et un couple de signatures suffixées serait refusé.

---

## Provenance

Ce brief naît de la feuille de route du projet (jalon E2) et de la décision
structurelle `docs/adr/0003-single-spatial-primary-key.md`, qui a tranché que
la cellule géographique (`cell_id`) est la seule clé spatiale et que la Province
est **toujours** une agrégation dérivée. Le brief 011 avait déjà posé la garde
qui interdit un champ de province sur une entité ; l'agrégation elle-même
n'existe pas encore. Ce lot la livre.

Ce n'est pas un `NEEDS_SPLIT` : un seul thème causal, un seul sous-système
(`sim/`), plus une lecture seule des artefacts géographiques déjà committés.

Une feuille de route n'instruit rien, un ADR non plus. À partir d'ici, **ce
`brief.md` est la SEULE instruction** (voir `CLAUDE.md` › Single Source of
Instruction). Tout le nécessaire est écrit ici ; aucun autre document n'a à
être consulté pour savoir quoi faire.

---

## World-Terms Requirement

Une province n'est pas un lieu que les gens portent sur eux. C'est le
regroupement des terres qui relèvent aujourd'hui d'un centre administratif
plutôt que d'un autre.

**Chaîne causale :**

Un centre administratif exerce son autorité sur les terres qui lui sont les
plus proches — c'est ainsi qu'une province se forme : par proximité au centre
dont elle dépend, pas par un tampon apposé sur ses habitants. Une terre (une
cellule) relève donc du centre le plus proche d'elle, et de personne d'autre.

Quand le géographe redessine un centre — parce qu'une capitale se déplace,
parce qu'une donnée est corrigée — les terres qui basculent d'une province à
l'autre ne basculent pas parce qu'on a réécrit quelque chose sur elles. Elles
basculent parce que **le regroupement est recalculé** : la distance au nouveau
centre a changé, donc la réponse à « de qui cette terre relève-t-elle ? » a
changé. Aucune cellule, aucun habitant, aucun bâtiment n'a été touché : la
terre est restée la terre.

C'est exactement l'inverse du défaut tracé par l'ADR-0003. Si l'appartenance
était stockée sur chaque cellule ou chaque habitant, un redessin de centre
laisserait derrière lui des cellules qui affirment relever d'une province que
la géométrie ne leur reconnaît plus. Deux réponses inscriptibles à la même
question « où ? » finiraient par se contredire, et chacune serait « juste »
selon sa propre source. Il n'y a qu'une réponse : la position de la cellule.
La province s'en déduit.

Les habitants, dans ce lot, vivent dans une cellule (`cell_id`). Ils ne portent
pas de province. Le moteur de temps (`tick`) n'a pas besoin de l'agrégation
pour ce lot : la province est ici une vue du monde, pas encore un acteur
économique.

---

## Décisions de conception tranchées par le Planificateur

Le Générateur n'a pas à arbitrer ces points. Ils sont décidés ici. Il choisit
librement les noms de fichiers, de fonctions et de variables dans le périmètre
autorisé.

### D1 — L'agrégation est une fonction pure

« Fonction pure » signifie : elle ne modifie aucun objet qu'elle reçoit, elle
n'écrit aucun fichier, et deux appels sur les mêmes entrées rendent le même
résultat.

Elle prend en entrée :
- les **positions des cellules** : latitude et longitude géographiques, lues
  dans `pipeline/geo/artifacts/cells_g3.json` sous `centroid.lat` et
  `centroid.lon` (repère WGS84), indexées par `cell_id` ;
- les **centres administratifs** : entrées du tableau `coordinates` de
  `pipeline/geo/legacy_game_data/province_coordinates.json` (`id`, `name`,
  `lon`, `lat`) ;
- le **paramètre de projection** lu dans ce même fichier
  (`projection.mid_latitude`).

Elle rend l'appartenance : pour chaque cellule, le centre dont elle relève.

Elle ne reçoit pas d'objet `Cell` modifiable et n'en modifie aucun. Un
adaptateur mince peut lire `World.cells` en lecture seule pour fournir la liste
des cellules à traiter ; cet adaptateur n'écrit rien non plus.

### D2 — Distance : dans le plan de la projection déjà documentée par le fichier

Le fichier de centres documente lui-même sa projection : équirectangulaire,
`x = lon × cos(mid_latitude)`, `y = −lat`. C'est cette projection qui est
employée, avec `mid_latitude` **lue du fichier**.

Interdiction ferme : la latitude moyenne (ou tout autre paramètre numérique)
ne doit jamais apparaître comme littéral dans un corps de fonction. Le contrôle
`sim/tests/test_no_hardcoded.py` parcourt récursivement tous les modules de
`sim/` hors tests et refuse tout littéral numérique autre que 0, 1, −1 (et
leurs équivalents flottants) dans un corps de fonction. Le nouveau module y
sera donc automatiquement soumis.

Comparer les carrés des distances est admis (même ordre, pas de racine carrée).

### D3 — L'identifiant de province vit sur la vue dérivée, hors de `sim.model`

La vue dérivée (l'objet qui porte les provinces et l'appartenance) est déclarée
**dans le nouveau module d'agrégation, pas dans `sim.model`**. Justification, en
deux temps :

1. *Raison de fond.* `sim.model` contient les entités persistées que le moteur
   fait évoluer. Y déclarer la Province inviterait tout code futur à la traiter
   comme un état stockable — précisément ce que l'ADR-0003 interdit. Une vue
   recalculée n'appartient pas au modèle persistant.
2. *Raison mécanique.* `sim/tests/test_write_coverage.py` découvre par
   introspection toutes les dataclasses membres de `sim.model` et exige, pour
   **chaque champ**, un site d'écriture sur une variable au nom conventionnel
   (nom de classe en minuscules) **et** un site de lecture, en ne balayant que
   `sim/engine.py`, `sim/world.py` et `sim/model.py`. Une dataclass `Province`
   construite dans le module d'agrégation n'aurait aucun site d'écriture dans
   ces trois fichiers : le contrôle passerait au rouge pour une bonne raison
   formelle et une mauvaise raison de fond. Déclarer la vue hors de `sim.model`
   évite ce faux conflit.

Conséquences contraignantes :

- **Aucune nouvelle dataclass n'est ajoutée à `sim.model` dans ce lot.**
- **Aucun champ n'est ajouté à `Cell`.** En particulier, pas de champ de
  position, pas de champ d'appartenance, sous aucun nom.
- La vue peut porter les champs `id`, `name`, `cell_ids` — jamais un champ dont
  le nom normalisé (minuscules, tirets bas retirés) commence par `province` :
  la garde `_NoBadSpatialField` refuse ce préfixe, et SC2 étend cette
  vérification à la vue.
- **Le mode d'échec n° 2 (« champ déclaré que personne n'écrit ») reste couvert
  pour la vue** : chaque champ déclaré sur la vue doit avoir, dans le code de
  production (module d'agrégation et `sim/world.py`), au moins un site de
  construction et au moins un site de lecture. Un champ sans lecteur de
  production doit être supprimé — ou recevoir un lecteur réel, par exemple une
  fonction qui rend le nom de la province dont relève une cellule donnée. Ce
  contrôle est livré comme test, par introspection des types du module, jamais
  par une liste de noms écrite à la main.

### D4 — Départage des égalités de distance : plus petit identifiant de centre

Si une cellule se trouve à distance exactement égale de deux centres ou plus,
elle relève de celui dont l'`id` est le plus petit. Cette règle est stable :
elle ne dépend pas de l'ordre dans lequel les centres sont parcourus. Elle est
documentée dans `sim/SEEDING.md` **avant** toute citation d'un compteur mesuré.

### D5 — Une cellule sans position connue fait échouer, jamais deviner

Si une cellule chargée par `World.from_g3()` n'a pas de position dans les
artefacts, le code lève une erreur explicite nommant la cellule. Il n'attribue
pas de province par défaut et n'écarte pas la cellule en silence. C'est la
règle durement acquise n° 10 : quand une donnée manque, un agent l'invente par
défaut — l'absence doit donc être déclarable et le code doit refuser de deviner.

### D6 — Les provinces vides sont un fait mesuré, pas un plancher

Toute **cellule** relève d'une province. L'inverse n'est pas exigé : un centre
peut n'attirer aucune cellule. Le nombre de provinces peuplées est **mesuré et
rapporté**, jamais imposé. Aucun test n'exige un plancher égal au nombre de
centres lus, et l'algorithme n'est en aucun cas ajusté pour peupler les 50.

### D7 — Le pas de temps ne consomme pas l'agrégation dans ce lot

`sim/engine.py` n'est pas modifié. Ni fiscalité, ni commerce entre provinces,
ni migration. La province est une vue ; elle deviendra un acteur économique
dans un lot ultérieur.

### Suggestion de découpage (non contraignante)

Un nouveau module sous `sim/` (par exemple `sim/aggregation.py`) portant : la
lecture des centres et du paramètre de projection, la lecture des positions de
cellules, la fonction pure d'agrégation, la vue dérivée, et une fonction de
consultation « de quelle province relève cette cellule ». Les tests sous
`sim/tests/`. La documentation dans `sim/SEEDING.md` et `sim/README.md`.

---

## Success Conditions

### SC1 — Couverture totale : chaque cellule chargée relève d'exactement une province

Sur le monde réel chargé par `World.from_g3(rng_seed=42)` :

- `cellules_chargees_g3` est dérivé du chargement et **égal** à `cell_count` lu
  dans `pipeline/geo/artifacts/stats_g3.json`. Aucun nombre de cellules écrit
  en dur, nulle part.
- `centroides_lus` est la longueur du tableau `coordinates` du fichier de
  centres, lue du fichier.
- `cellules_avec_province` est **égal** à `cellules_chargees_g3` : couverture
  totale, chaque cellule relève d'exactement une province (ni zéro, ni deux).
- `cellules_sans_province` vaut **0**. Ce zéro est une mesure réelle, pas un
  « non calculé » : la sentinelle « non calculé » est `-1` (règle durement
  acquise n° 8), et elle ne doit apparaître pour aucun compteur de ce lot qui a
  effectivement été calculé. Ce point est écrit noir sur blanc dans
  `sim/SEEDING.md`.
- `cellules_position_absente` vaut **0** sur le monde réel, et un test prouve
  que si une position est retirée en mémoire, le code lève l'erreur explicite
  de D5 au lieu de deviner.
- `provinces_non_vides` est mesuré, avec `centroides_lus` pour dénominateur.
  Condition : `0 < provinces_non_vides ≤ centroides_lus`. Aucun plancher plus
  élevé n'est exigé (D6).

```py
.venv/bin/python -m pytest sim/tests/ -k province -v
```

Résultat attendu : `PASSED`.

### SC2 — Aucune entité ne porte de province ; la garde existante est exercée, pas affaiblie

Un test livré vérifie, **par introspection** (jamais par une liste de noms
écrite à la main) :

- pour chaque dataclass de `sim.model` : aucun champ dont le nom normalisé
  commence par `province` ;
- pour chaque type déclaré par le module d'agrégation : même vérification ;
- `champs_province_sur_entites` vaut **0**, avec pour dénominateur le nombre de
  champs réellement inspectés, et `dataclasses_inspectees` > 0 — un dénominateur
  nul signifierait qu'aucun champ n'a été regardé.

La garde `_NoBadSpatialField` et `sim/tests/test_adr_compliance.py` **existent
déjà**. Elles sont **exercées**, en aucun cas affaiblies : aucun cas de test
retiré, aucune liste blanche élargie, aucun changement du préfixe interdit.
Ajouter des cas est permis.

Pourquoi l'introspection et pas un test nommant `Cell` : `Person`, `Family` et
`Building` n'existent pas encore. Un contrôle qui ne nomme que `Cell`
laisserait passer la première entité créée après ce lot. Un contrôle dérive, il
n'est jamais nommé d'après sa cible (règle durement acquise n° 2).

```py
.venv/bin/python -m pytest sim/tests/test_adr_compliance.py -v
```

Résultat attendu : `PASSED`, tous les cas existants toujours présents.

### SC3 — Redessin : l'agrégat change, les cellules ne sont pas réécrites

Un test livré monte ce scénario, entièrement **en mémoire** :

1. Charger `World.from_g3(rng_seed=42)` et calculer l'appartenance A.
2. Relever l'empreinte des cellules avant redessin : la sérialisation
   canonique `World.to_dict()` (sérialisée avec clés triées), et, pour chaque
   cellule, le contenu complet de ses attributs d'instance.
3. Déplacer, **dans les enregistrements lus en mémoire**, le centre de plus
   petit `id` sur la position exacte d'une cellule qui relève actuellement d'un
   autre centre. Aucune écriture de fichier.
4. Recalculer l'appartenance B.
5. Vérifier les quatre faits suivants :
   - `redessin_change_agregat` vaut 1 : au moins une cellule change de
     province. `cellules_changeant_de_province_apres_redessin` est rapporté
     comme fait mesuré, avec les cellules chargées pour dénominateur, et est
     strictement positif.
   - `redessin_cellules_intactes` vaut 1 : la sérialisation des cellules est
     **identique** avant et après.
   - `attributs_dynamiques_sur_cellules` vaut 0 : aucune cellule n'a acquis
     d'attribut d'instance au-delà de ses champs déclarés. C'est ce contrôle
     qui interdit l'estampillage discret par attribut dynamique.
   - `fichier_centroides_inchange_apres_redessin` vaut 1 : les octets du
     fichier de centres, relus après l'opération, sont ceux relevés avant.

Le choix « centre de plus petit `id` déplacé sur la position d'une cellule
relevant d'un autre centre » est prescrit ici pour que le scénario soit
déterministe et concluant : à distance nulle le centre déplacé gagne, et si une
égalité survenait, la règle de départage D4 la trancherait encore en sa faveur.

```py
.venv/bin/python -m pytest sim/tests/ -k redessin -v
```

Résultat attendu : `PASSED`.

### SC4 — Fonction pure, déterminisme, départage nommé avant mesure

- `determinisme_agregation_deux_passes` vaut 1 : deux appels sur les mêmes
  entrées, plus un troisième avec la liste des centres passée dans l'ordre
  inverse, rendent la même appartenance, cellule par cellule.
- `departage_egalite_plus_petit_id` vaut 1 : sur un cas synthétique où deux
  centres sont exactement équidistants d'une cellule fabriquée, la cellule
  relève du centre de plus petit `id`, et ce dans les deux ordres de parcours
  possibles.
- `egalites_de_distance_monde_reel` est le nombre d'égalités exactes observées
  sur le monde réel, avec les cellules chargées pour dénominateur. Cette valeur
  peut légitimement être 0 ; si elle est calculée, elle n'est pas rapportée
  avec la sentinelle.
- La règle de départage et la projection employée sont documentées dans
  `sim/SEEDING.md` **avant** toute citation d'un compteur mesuré du lot.
- Aucun littéral numérique hors {0, 1, −1} dans un corps de fonction du
  nouveau module :

```py
.venv/bin/python -m pytest sim/tests/test_no_hardcoded.py -v
```

Résultat attendu : `PASSED`, avec `compteurs_en_dur_trouves = 0`.

### SC5 — La source est déclarée comme proxy, jamais comme frontières historiques

`sim/SEEDING.md` reçoit une section dédiée au brief 018, rédigée **avant** toute
citation de compteur mesuré, qui dit en clair :

- que les 50 centres administratifs proviennent de
  `pipeline/geo/legacy_game_data/province_coordinates.json`, **données héritées
  du jeu** ;
- qu'il ne s'agit **pas** de frontières historiques de 1400, et que rien ici ne
  prétend au statut de source savante ni de reconstitution d'époque (règle
  durement acquise n° 10) ;
- la projection employée et le fait que son paramètre est lu du fichier ;
- la règle de départage des égalités (D4) ;
- la politique de refus de deviner (D5) ;
- le fait que `cellules_sans_province = 0` est une mesure réelle et non une
  sentinelle (règle n° 8).

`sim/README.md` reçoit une mise à jour **descriptive** : quels modules existent
et quelles données ils lisent. Aucune instruction adressée à un agent — le
brief est la seule instruction, et
`harness/tests/test_single_source_of_instruction.py` le vérifie.

### SC6 — Preuves rouges : deux paires, sabotage hors dépôt

Un contrôle qui ne peut pas rougir ne prouve rien (règle durement acquise n° 4).
Chaque paire est produite depuis une **copie de travail sabotée hors du dépôt**.
Les sorties sont committées sous `sim/tests/proof_red/` en `.txt` — jamais
`.log`, que `.gitignore` exclut, ce qui rendrait la preuve non re-vérifiable
depuis un clone.

**Paire A — garde spatiale :**
- Sabotage : ajouter un champ `province_id` (ou `province`) sur `Cell` dans la
  copie hors dépôt.
- Test affecté : les tests de conformité ADR (existants et/ou le nouveau test
  introspectif de SC2).
- `sim/tests/proof_red/run_garde_province_red.txt` : au moins un `FAILED`.
- `sim/tests/proof_red/run_garde_province_green.txt` : uniquement `PASSED`, même
  test sur le code correct.

**Paire B — redessin sans réécriture :**
- Sabotage : faire écrire l'appartenance par l'agrégation sur chaque cellule.
  Le nom employé doit être un nom que la garde de préfixe **ne rattrape pas**
  (par exemple `zone_admin`), de sorte que ce soit bien le test de redessin qui
  rougisse, et non la règle de nom. Un contrôle trop grossier coûte aussi cher
  qu'un contrôle laxiste (règle n° 6).
- Test affecté : le test de redessin de SC3.
- `sim/tests/proof_red/run_redessin_red.txt` : au moins un `FAILED`.
- `sim/tests/proof_red/run_redessin_green.txt` : uniquement `PASSED`.

Les deux paires sont déclarées dans `deliverables/manifest.json` avec le champ
`must_differ_from`, en chemins relatifs au dossier du brief :

```json
{
  "path": "../../../../sim/tests/proof_red/run_redessin_green.txt",
  "must_differ_from": "../../../../sim/tests/proof_red/run_redessin_red.txt"
}
```

(idem pour la paire A). Les quatre fichiers sont écrits avant le journal.

### SC7 — Scripts de mesure reproductibles, manifeste complet, suite verte

Deux scripts committés sous
`harness/queue/briefs/018-sim-province-derivee/deliverables/`, exécutés depuis
la racine du dépôt :

```py
.venv/bin/python harness/queue/briefs/018-sim-province-derivee/deliverables/measure_sc1_018.py
.venv/bin/python harness/queue/briefs/018-sim-province-derivee/deliverables/measure_sc3_018.py
```

- `measure_sc1_018.py` charge le monde réel et produit les compteurs de
  couverture de SC1, chacun avec son dénominateur imprimé.
- `measure_sc3_018.py` monte le scénario de redessin de SC3 et produit ses
  compteurs, chacun avec son dénominateur imprimé.

Chaque compteur du manifeste porte un `sample_size` réel, non nul, différent de
la sentinelle. La suite complète est verte :

```py
.venv/bin/python -m pytest sim/tests/ -v
.venv/bin/python -m pytest harness/tests/ -q
```

Aucun `FAILED`. Les `SKIP` propres à Linux (tests Unity) sont acceptés. Les
deux sorties réelles sont recopiées dans `deliverables/generator-log.md`.

### SC8 — Registre de coût

```py
.venv/bin/python harness/backends/ledger.py append --backend cursor \
  --brief harness/queue/briefs/018-sim-province-derivee \
  --event generator-run
```

Aucun `--audit-id` n'est requis : ce brief naît de la feuille de route, pas d'un
audit converti.

---

## Non-Goals

Ce brief ne doit explicitement PAS :

1. Traiter la réserve N1 du verdict du brief 017 (prédiction de survie trop peu
   sensible à `HUNGER_DEATH_SCALE`) — hors périmètre, brief ultérieur.
2. Toucher au relief, au climat ou aux ressources, ni réécrire quoi que ce soit
   sous `pipeline/geo/` : les artefacts géographiques committés sont en
   **lecture seule** pour ce lot.
3. Implémenter les villes, les États, la fiscalité, le commerce entre
   provinces, `Person` / `Family` / `Building`, la natalité, la migration ou
   les prix.
4. Modifier `harness/*.py`, `harness/pipeline/`, `architecture/`, `unity/`,
   `VISION.md`, `ROADMAP.md`, `HANDOFF.md`, `.github/workflows/`.
5. Retoucher les archives des briefs 011 à 017 (fichiers intangibles).
6. Affaiblir `_NoBadSpatialField` ou `sim/tests/test_adr_compliance.py` :
   élargir la liste blanche, retirer un cas de test, changer le préfixe
   interdit.
7. Stocker une appartenance de province sur `Cell` — ni « pour la performance »,
   ni « en cache invalidé », ni sous un autre nom. C'est le défaut que
   l'ADR-0003 existe pour rendre impossible.
8. Inventer des frontières historiques de 1400, ni présenter les 50 centres
   hérités comme une source savante.
9. Recalibrer les constantes de survie, de nourriture ou de population du
   brief 017.
10. Réparer le déclenchement d'audit d'étape sans jalon — hors périmètre.
11. Écrire un brief de harnais, ou traiter l'arriéré d'audits en attente.
12. Rapporter un compteur de couverture depuis un monde construit à la main ou
    depuis un monde à zéro cellule. L'échantillon est l'ensemble des cellules
    chargées par `World.from_g3()`.
13. Modifier `sim/engine.py` ni faire consommer l'agrégation par le pas de
    temps (D7).
14. Ajouter une dataclass à `sim.model`, ni un champ à `Cell` (D3).

---

## Required Counters

Un compteur sans source d'échantillon déclarée est irrecevable : la porte
mécanique refuse tout compteur dont l'échantillon est nul ou non calculé
(`no_empty_sample_pass`).

| nom | source de l'échantillon | dénominateur |
|---|---|---|
| `cellules_chargees_g3` | cellules chargées par `World.from_g3(rng_seed=42)` | `cell_count` lu dans `pipeline/geo/artifacts/stats_g3.json` (doit être égal) |
| `centroides_lus` | entrées du tableau `coordinates` du fichier de centres | longueur de ce même tableau, lue du fichier |
| `cellules_avec_province` | cellules chargées ayant exactement une province | `cellules_chargees_g3` (doit être égal — couverture totale) |
| `cellules_sans_province` | cellules chargées sans province | `cellules_chargees_g3` ; **doit valoir 0**, mesure réelle et non sentinelle |
| `cellules_position_absente` | cellules chargées sans position dans les artefacts | `cellules_chargees_g3` ; **doit valoir 0** |
| `refus_position_absente_leve` | cas synthétique : une position retirée en mémoire | 1 cas monté ; l'erreur explicite de D5 est levée |
| `provinces_non_vides` | provinces de l'agrégat comptant au moins une cellule | `centroides_lus` ; fait mesuré, `0 < valeur ≤ centroides_lus` |
| `champs_province_sur_entites` | champs de toutes les dataclasses de `sim.model` et des types du module d'agrégation, découverts par introspection | nombre de champs inspectés ; **doit valoir 0** |
| `dataclasses_inspectees` | classes découvertes par cette même introspection | nombre de classes examinées ; doit être > 0 |
| `garde_prefixe_variantes_rouges` | variantes de nom interdit instanciées (`province_id`, `ProvinceId`, `province`, `province_code`, et toute variante ajoutée) | nombre de variantes essayées ; chacune doit lever une `TypeError` citant l'ADR-0003 |
| `champs_vue_couverts` | champs de la vue dérivée ayant un site de construction **et** un site de lecture dans le code de production | nombre de champs déclarés sur la vue (doit être égal) |
| `redessin_change_agregat` | scénario de redessin de SC3 | 1 scénario ; vaut 1 si au moins une cellule change de province |
| `cellules_changeant_de_province_apres_redessin` | cellules dont l'appartenance diffère entre A et B | `cellules_chargees_g3` ; fait mesuré, strictement positif |
| `redessin_cellules_intactes` | sérialisation canonique des cellules avant / après redessin | `cellules_chargees_g3` cellules comparées ; vaut 1 si identique |
| `attributs_dynamiques_sur_cellules` | attributs d'instance de chaque cellule comparés à ses champs déclarés | `cellules_chargees_g3` ; **doit valoir 0** |
| `fichier_centroides_inchange_apres_redessin` | octets du fichier de centres relus après l'opération | 1 comparaison ; vaut 1 si identique |
| `determinisme_agregation_deux_passes` | deux appels identiques + un appel avec les centres en ordre inverse | `cellules_chargees_g3` cellules comparées ; vaut 1 si les trois appartenances coïncident |
| `departage_egalite_plus_petit_id` | cas synthétique de deux centres équidistants, essayé dans les deux ordres | nombre de cas et d'ordres essayés ; vaut 1 si le plus petit `id` gagne toujours |
| `egalites_de_distance_monde_reel` | égalités de distance exactes observées sur le monde réel | `cellules_chargees_g3` ; fait mesuré, peut valoir 0 |
| `compteurs_en_dur_trouves` | corps de fonctions de tous les modules de `sim/` hors tests | nombre de fonctions inspectées ; **doit valoir 0** |
| `tests_sim_passed_018` | tests `PASSED` de `sim/tests/` | nombre de tests collectés dans `sim/tests/` |
| `tests_harness_passed_018` | tests `PASSED` de `harness/tests/` | nombre de tests collectés dans `harness/tests/` (les `SKIP` Linux sont acceptés et déclarés) |

---

## Acceptable Waivers (si une impossibilité est invoquée)

Une impossibilité s'éprouve avant d'être invoquée : une commande et le message
d'erreur qu'elle produit, sinon ce n'est pas un constat mais un abandon (règle
durement acquise n° 9).

| affirmation d'impossibilité | commande exigée | erreur attendue |
|---|---|---|
| « le budget d'exécution n'est pas mesurable sur cette machine » | `.venv/bin/python harness/budget.py status --brief harness/queue/briefs/018-sim-province-derivee` | la sortie contient la chaîne `UNMEASURABLE` |
| « les artefacts de cellules ne sont pas lisibles depuis ce chemin » | `.venv/bin/python -c "import json; json.load(open('pipeline/geo/artifacts/cells_g3.json'))"` depuis la racine | le message d'erreur exact (`FileNotFoundError` ou équivalent) |
| « le fichier de centres administratifs n'est pas lisible » | `.venv/bin/python -c "import json; json.load(open('pipeline/geo/legacy_game_data/province_coordinates.json'))"` depuis la racine | le message d'erreur exact (`FileNotFoundError` ou équivalent) |
| « le paquet du moteur ne s'importe pas » | `.venv/bin/python -c "import sim"` depuis la racine | le message `ImportError` exact, avec le nom du module manquant |

Aucune autre dérogation n'est recevable. En particulier :

- « il est impossible de dériver l'appartenance sans stocker un identifiant de
  province sur `Cell` » **n'est pas une dérogation** : l'agrégation est
  recalculable à la demande, et son coût n'est pas un sujet de ce lot.
- « le nombre de provinces peuplées est inférieur au nombre de centres lus »
  **n'est pas une dérogation** : c'est le fait mesuré attendu (D6).

---

## Execution Contract

### Périmètre autorisé

- un nouveau module sous `sim/` (nom au choix du Générateur, par exemple
  `sim/aggregation.py`) ;
- `sim/world.py` (adaptateur en lecture seule, si le chargement des centres ou
  des positions s'y accroche) ;
- `sim/model.py` — **documentation seulement** : aucun champ, aucune dataclass
  ajoutée (D3) ;
- `sim/constants.py` — seulement si une constante nommée non numérique-calibrée
  s'avère nécessaire ; aucune constante de survie, de nourriture ou de
  population n'est retouchée ;
- `sim/SEEDING.md` (section dédiée au brief 018) et `sim/README.md` (mise à
  jour descriptive) ;
- `sim/tests/` : nouveaux tests. `sim/tests/test_write_coverage.py` et
  `sim/tests/test_adr_compliance.py` ne peuvent être modifiés que pour
  **élargir** ce qu'ils balaient (ajouter un fichier, ajouter un cas) — jamais
  pour le restreindre ;
- `sim/tests/proof_red/*.txt` (les quatre preuves de SC6) ;
- `harness/queue/briefs/018-sim-province-derivee/` (livrables du présent lot) ;
- `harness/queue/cost-ledger.jsonl` (une seule ligne ajoutée en fin de fichier,
  SC8).

### Fichiers interdits

`sim/engine.py` ; tout fichier sous `pipeline/geo/` (lecture seule) ;
`harness/*.py` ; `harness/pipeline/` ; `architecture/` ; `unity/` ;
`VISION.md` ; `ROADMAP.md` ; `HANDOFF.md` ; `.github/workflows/` ; et tout
fichier sous `harness/queue/briefs/011-*/` à `harness/queue/briefs/017-*/`.

### Estimation d'appels d'outils

**Estimation : 110 appels.** Ancres réelles : le brief 011, premier code de
simulation, a coûté environ 108 appels ; le brief 017, quatre corrections
corrélées dans le même sous-système, 130. Le présent lot est plus étroit : un
module d'agrégation, ses tests, deux paires rouge/vert, deux petits scripts de
mesure, un manifeste, un journal — un seul sous-système, un seul thème causal.

Plafond dur : 160 appels. Point de contrôle obligatoire à 130.

Vérification préalable, à exécuter avant tout travail de fond :

```py
.venv/bin/python harness/budget.py split-check \
  --brief harness/queue/briefs/018-sim-province-derivee \
  --estimated-calls 110
```

Verdict attendu : `SIZE_OK` (110 est sous le seuil mécanique de 150). Les
signaux imprimés à titre indicatif ne déclenchent rien : le Planificateur a
déjà jugé qu'il n'y a ici qu'un sous-système et qu'un thème.

### Preuves committées et re-vérifiables

Tout fichier nommé dans `deliverables/manifest.json` doit être suivi par git.
`.gitignore` exclut `*.log` : une preuve laissée dans un `.log` ne serait pas
re-vérifiable depuis un clone frais. Les preuves de ce lot sont donc en `.txt`.

### Deliverables obligatoires

Le dossier `harness/queue/briefs/018-sim-province-derivee/deliverables/` doit
contenir :

- `manifest.json` (format standard : tous les fichiers déclarés sous contrôle
  de version, tous les compteurs avec un `sample_size` réel, les deux paires
  `must_differ_from`, les dérogations éventuelles avec commande et erreur) ;
- `measure_sc1_018.py` et `measure_sc3_018.py` (scripts rejouables) ;
- `generator-log.md` (journal d'exécution, en français clair, rédigé par le
  Générateur, avec les sorties réelles des suites de tests) ;
- `.gitkeep` (déjà présent, pour que le dossier existe dès maintenant).

### Interdictions pour le Générateur

- **Ne pas committer. Ne pas pousser. Ne créer aucune branche.** L'orchestrateur
  seul dépose.
- Ne pas modifier `brief.md`, `eval-rubric.md`, ni écrire `verdict.md`.
- Toujours `.venv/bin/python` — jamais l'alias nu (règle durement acquise n° 1).
- Ne recopier aucune valeur hexadécimale de condensé SHA256 : une empreinte est
  citée par son nom, jamais par sa valeur (règle n° 12).
- Ne pas affaiblir `_NoBadSpatialField` ni les tests de conformité ADR.
- Ne pas ajouter de champ à `Cell` ni de dataclass à `sim.model`.
- Ne pas retoucher les archives des briefs 011 à 017.
- Ne pas rapporter la sentinelle `-1` pour un compteur qui a été calculé, ni
  `0` pour un compteur qui ne l'a pas été (règle n° 8).
- Ne pas prononcer la recevabilité de son propre travail.

### Fin de lot

La porte mécanique doit répondre `ACCEPT` :

```py
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/018-sim-province-derivee
```

Les deux suites doivent être vertes :

```py
.venv/bin/python -m pytest sim/tests/ -v
.venv/bin/python -m pytest harness/tests/ -q
```

Les sorties réelles sont recopiées dans le journal.

**Celui qui produit ne prononce pas la recevabilité.**
