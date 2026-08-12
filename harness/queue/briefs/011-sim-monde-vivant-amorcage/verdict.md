# Verdict — Brief `011` (amorçage du moteur `sim/`)

**Authored**: 2026-08-12T16:30:35Z
**Author**: forge-evaluateur

## Note de transparence

Cette évaluation a été exécutée par un sous-agent hébergé par Cursor, en
remplacement de Claude (indisponible ce jour), sur instruction du
propriétaire du projet. Elle s'est déroulée dans une session distincte de
celle du Générateur, orchestrée depuis l'extérieur du harnais habituel : le
rôle d'Évaluateur n'a donc jamais partagé de contexte d'exécution avec le
rôle de Générateur, et aucune ligne de code du lot n'a été modifiée pendant
l'évaluation. Le contrôle mécanique `verdict_is_not_self_authored` reste la
garantie formelle de cette séparation ; la présente note en explique
l'organisation réelle.

Vocabulaire utilisé plus bas, expliqué à sa première apparition :
« gate mécanique » = le script de contrôle automatique
`harness/verdict_audit.py`, qui applique dix vérifications de forme avant
toute lecture humaine ; « compteur » = une valeur chiffrée déclarée dans
`deliverables/manifest.json` avec la commande qui l'a produite ;
« preuve rouge » = la sortie d'un test qui échoue volontairement, montrant
que ce test est capable de détecter le défaut qu'il prétend surveiller.

## Mechanical Gate Result

Commande exécutée depuis la racine du dépôt :
`.venv/bin/python harness/verdict_audit.py harness/queue/briefs/011-sim-monde-vivant-amorcage`.

Au moment de l'ouverture de l'évaluation (avant l'écriture du présent
fichier), le gate sortait en code d'erreur non nul avec la ligne
`VERDICT: REJECT`, et exactement deux contrôles en échec :
`verdict_numbers_traceable` et `verdict_is_not_self_authored`, tous deux
motivés par l'absence de `verdict.md`. Les huit autres contrôles étaient au
vert. C'est l'état attendu à ce stade du déroulé, et il autorisait la
poursuite de l'examen manuel.

Le rapport du Générateur reprend cette même sortie dans
`deliverables/generator-log.md` (section des auto-contrôles) ; les chiffres
ne sont pas recopiés ici, conformément à la hard-won rule `12`.

**Avertissement de lecture.** Le gate ne juge que la *forme* du lot. Une
fois ce `verdict.md` écrit, il peut afficher `VERDICT: ACCEPT` alors que le
verdict de fond ci-dessous est REJECT : le gate ne lit pas la conclusion de
l'Évaluateur. C'est le verdict de fond qui décide de l'acceptation du lot.

## Per-Rubric-Line Verdict

| Success Condition | PASS/FAIL | Evidence (commandes rejouées par l'Évaluateur) |
|---|---|---|
| SC1 — paquet importable et documenté | PASS | `.venv/bin/python -c "import sim; print(sim.__version__)"` affiche une version non vide (`0.1.0`). `sim/README.md` décrit les modules, la commande de test, la source des données, et pointe vers `VISION.md` et l'ADR sans les réécrire. |
| SC2 — chargement depuis les artefacts G3 | PASS | Recompté directement depuis les artefacts, hors code livré : le tableau `cells` de `cells_g3.json` contient 596 entrées, `cell_count` de `stats_g3.json` vaut 596, `len(world.cells)` vaut 596 ; `adjacency_g3.json` contient 1364 arêtes, `len(world.adjacency)` vaut 1364. Aucune de ces valeurs n'apparaît en dur dans `sim/world.py` ni dans les tests (recherche par motif sur tout `sim/`). |
| SC3 — `cell_id` seule clé spatiale (ADR `0003`) | PASS | Les trois tests de `sim/tests/test_adr_compliance.py` s'exécutent : la garde `_NoBadSpatialField` lève bien une `TypeError` explicite (message contenant la référence à l'ADR) à l'instanciation d'une entité déclarant `province_id` ou `ProvinceId`. Vérification par exécution, pas par lecture. Réserve non bloquante en fin de document. |
| SC4 — amorçage documenté et déterministe | PASS | `sim/SEEDING.md` est suivi par git, non vide, donne formule, paramètres, unités, et déclare explicitement que l'amorçage est un proxy paramétrique et non une donnée historique. Déterminisme reconstruit par mes soins hors des tests livrés : deux chargements avec la même graine donnent des populations et des stocks strictement identiques sur toutes les cellules. |
| SC5 — boucle de tick déterministe | PASS | Reconstruit hors des tests livrés : deux séries de dix pas de temps avec la même graine donnent des condensés SHA256 égaux, une graine différente donne un condensé différent. Aucun condensé n'est écrit en dur dans le code ni dans les tests (recherche par motif sur tout `sim/`). Le générateur pseudo-aléatoire est fourni par l'appelant. Réserve sur le journal en fin de document. |
| SC6 — économie physique de la nourriture | PASS | `Cell` déclare `food_stock_kg` et `hunger_ticks` avec la sentinelle `-1` par défaut. Dans `tick()`, la production écrit le stock avant que la consommation ne le relise et le modifie ; la faim relit le stock ; la mortalité relit la faim. Rien n'est calculé hors du champ persisté. |
| SC7a — production insuffisante fait baisser le stock | PASS | Test unitaire rejoué : état construit à la main, appel des seules fonctions de production et de consommation, stock avant et après affichés, baisse constatée. Le tick complet n'est pas utilisé. |
| SC7b — stock épuisé fait progresser la faim | PASS | Test unitaire rejoué : état construit à la main, appel de la seule fonction de faim, compteur de faim passant de zéro à un. Aucune dépendance à SC7a. |
| SC7c — faim au seuil fait baisser la population | PASS | Test unitaire rejoué : le seuil vient de la constante nommée `HUNGER_DEATH_THRESHOLD` importée de `sim/constants.py`, jamais d'un nombre écrit dans le test. Population avant et après affichées, baisse constatée. |
| SC7d — intégration de bout en bout | PASS | Test rejoué : cellule à rendement nul, population initiale strictement positive, vingt appels à `tick()`, population finale strictement inférieure. Le résultat passe bien par le tick complet, pas par un appel direct à la règle de mortalité. |
| SC8 — couverture d'écriture sur **tous** les champs du modèle | **FAIL** | Contre-preuve montée par l'Évaluateur dans une copie de travail hors dépôt (aucun fichier du dépôt modifié) : un champ supplémentaire déclaré sur `Cell` sans aucun site d'écriture ni de lecture laisse `sim/tests/test_write_coverage.py` intégralement au vert. Le test ne parcourt jamais les champs déclarés ; il ne parcourt que les attributs écrits dans `sim/engine.py`. Le compteur `champs_modele_couverts` le dit lui-même : 3 champs couverts sur 5 déclarés, suite verte malgré tout. Détail et correction attendue plus bas. |
| SC9 — aucun compteur codé en dur | PASS | Test d'inspection statique rejoué : 0 littéral non nommé trouvé dans les corps de fonctions des 4 modules du moteur. Les valeurs paramétriques sont toutes dans `sim/constants.py` et documentées dans `sim/SEEDING.md`. Réserve non bloquante en fin de document. |
| SC10 — preuve rouge d'abord | PASS | Les deux fichiers de preuve sont suivis par git. `run_sabotage.txt` contient un échec réel (`FAILED` sur le test de couverture, avec le message d'assertion citant le champ retiré) ; `run_correct.txt` ne contient que des tests au vert. Diff recalculé par mes soins : 70 lignes de différence, conforme au compteur déclaré. La paire est bien déclarée en `must_differ_from` dans le manifeste. |
| SC11 — suite de tests entièrement verte | PASS | `.venv/bin/python -m pytest sim/tests/ -v` rejoué depuis la racine : code de sortie nul, tous les tests collectés au vert. La suite du harnais, rejouée aussi, reste intacte (aucun échec, seuls les cas Unity sont ignorés comme prévu sous Linux). |
| SC12 — `sim/README.md` mis à jour | PASS | Le README décrit les modules, la commande de lancement des tests, la source des données G3, et l'histoire du stub resté vide jusqu'à ce brief. Il pointe vers `VISION.md`, l'ADR de la clé spatiale et les principes de simulation au lieu de les recopier. |

### Reconstruction indépendante des compteurs

Chaque compteur du manifeste a été recalculé par mes propres commandes,
sans reprendre le chiffre annoncé. Tous se reproduisent à l'identique :
cellules chargées 596 sur un dénominateur de 596 ; arêtes d'adjacence 1364
sur 1364 ; champs de modèle couverts 3 sur 5 ; compteurs en dur trouvés 0
sur 4 fichiers inspectés ; amorçage déterministe 1 sur 1 ; ticks
déterministes 1 sur 1 ; maillons testés unitairement 3 sur 3 ; test
d'intégration 1 sur 1 ; lignes différentes entre les deux preuves 70 sur 70.

Aucun compteur n'est faux. Le problème de SC8 n'est pas que la valeur soit
inexacte — elle est exacte — c'est que le test qui la produit n'échoue pas
quand elle est mauvaise.

## Overall Verdict: REJECT

Une seule condition de succès est en échec, mais elle porte précisément sur
le mode d'échec numéro 2 des principes de simulation (un champ déclaré que
personne n'écrit ou que personne ne lit) et sur la hard-won rule 7
(« la présence n'est pas la fonction »). Un contrôle qui ne peut pas
devenir rouge sur le défaut qu'il surveille ne prouve rien ; la grille dit
qu'une condition « satisfaite en substance » ne l'est pas.

## Boundary Violations

1. **Empreinte SHA256 recopiée en clair dans un document livré.**
   `deliverables/generator-log.md`, section du compteur des ticks
   déterministes, recopie la valeur hexadécimale du condensé de chaque run
   en plus de la citer par son nom de variable. Le brief exige au contraire
   que le condensé soit cité « par son nom de variable […] jamais par
   recopie d'une valeur hexadécimale en dur dans un test ou un document »
   (hard-won rule `12`). Le code et les tests sont propres sur ce point,
   c'est bien le journal qui porte la valeur morte. Ce n'est pas ce qui
   motive le REJECT, mais cela doit être corrigé dans la même itération.

2. **Fichier modifié hors de `sim/`, jugé recevable.** Le lot ajoute une
   ligne à `harness/queue/cost-ledger.jsonl`. Le non-objectif numéro 5
   interdit de modifier `harness/`, mais ce fichier est le registre de coût
   du harnais lui-même, il est déclaré dans le manifeste, et l'ajout est un
   simple ajout en fin de fichier, sans modification d'une ligne existante.
   Je le compte comme faisant partie du fonctionnement normal du harnais et
   non comme une sortie de périmètre. Aucun autre fichier hors `sim/` n'est
   touché : `VISION.md`, `pipeline/geo/`, `unity/`, `docs/adr/` et le code
   du harnais sont intacts (vérifié par l'état git de la copie de travail).

3. **Aucun non-objectif de contenu enfreint.** Pas de commerce entre
   cellules, pas d'objet Province, pas de familles ni de personnes, aucune
   donnée historique inventée : l'amorçage se déclare paramétrique, ce que
   le brief demandait explicitement.

## What Improved Since Last Iteration

Première itération de ce brief : il n'y a pas d'état antérieur à comparer.
À signaler tout de même, parce que ce sont les pièges qui font
habituellement échouer un lot et qu'ils ont été évités ici : la preuve
rouge est réelle et non fabriquée (le fichier de sabotage contient un échec
authentique, pas une suite verte déguisée) ; aucune constante des artefacts
G3 n'est écrite en dur dans le code de chargement ni dans les tests ; le
seuil de mortalité est bien importé d'une constante nommée au lieu d'être
écrit dans le test ; le déterminisme se reproduit quand je le rejoue
moi-même, hors des tests livrés.

## What Regressed Since Last Iteration

Sans objet — première itération.

## Feedback for Next Iteration

### Bloquant

**B1 — SC8 : le test de couverture ne parcourt pas les champs déclarés.**

Constat. `sim/tests/test_write_coverage.py` construit l'ensemble des
attributs *écrits* sur la variable `cell` dans `sim/engine.py`, puis vérifie
deux choses : que tout attribut écrit est déclaré sur `Cell`, et que tout
attribut écrit a aussi un site de lecture. Le sens inverse — celui que le
brief exige — n'est jamais vérifié : l'ensemble `champs déclarés moins
champs écrits` n'est jamais confronté à quoi que ce soit. Le test de
compteur, lui, se contente d'exiger qu'au moins un champ soit couvert.

Preuve. Dans une copie de travail hors dépôt, j'ai ajouté à `Cell` un champ
factice sans aucun écrivain ni lecteur, puis rejoué
`.venv/bin/python -m pytest sim/tests/test_write_coverage.py -v -s` : les
trois tests passent, code de sortie nul, et le compteur affiche simplement
3 champs couverts sur 6. Le champ fantôme est vu, compté comme non couvert,
et n'empêche rien. C'est exactement le mode d'échec numéro 2 que SC8 devait
rendre impossible. Dans l'état livré du dépôt, deux champs sont déjà dans
ce cas (`cell_id` et `area_km2`), ce que le compteur 3 sur 5 dit
explicitement, et la suite est verte.

Correction attendue, précisément :

- Élargir le périmètre d'analyse : scanner `sim/engine.py`, `sim/world.py`
  et `sim/model.py`, et non le seul moteur. Les sites d'écriture de
  `cell_id` et `area_km2` existent — ce sont les arguments nommés du
  constructeur `Cell(...)` dans `sim/world.py` — et leurs sites de lecture
  existent dans `to_dict()` et dans le moteur. Il n'est donc pas nécessaire
  de retirer des champs pour rendre le test vert, ce qui serait de toute
  façon un échec disqualifiant de la grille.
- Inverser la boucle d'assertion : itérer sur
  `dataclasses.fields(Cell)`, et pour **chaque** champ déclaré, exiger au
  moins un site d'écriture et au moins un site de lecture. Le message
  d'échec doit nommer le champ fautif et dire lequel des deux sites manque.
- Faire du compteur `champs_modele_couverts` une valeur qui doit égaler le
  nombre total de champs déclarés, et non un simple « au moins un ». Le
  compteur doit rester dérivé, jamais écrit en dur.
- Conserver l'assertion existante « tout attribut écrit sur `cell` doit être
  déclaré », qui est celle que le sabotage de SC10 fait passer au rouge. Le
  test corrigé doit continuer à échouer sur ce sabotage, et échouer
  également sur un champ déclaré sans écrivain. Il faut donc **deux**
  preuves rouges, pas une : rejouer le sabotage existant, et ajouter une
  seconde sortie rouge obtenue en déclarant un champ fantôme, sauvegardée
  à côté des deux fichiers actuels dans `sim/tests/proof_red/`.

**B2 — hard-won rule `12` : condensé recopié dans le journal.**

Dans `deliverables/generator-log.md`, remplacer les deux lignes qui
affichent la valeur hexadécimale du condensé par une formulation qui cite
uniquement les noms de variable et le résultat de leur comparaison, en
renvoyant à la commande à rejouer pour obtenir la valeur du jour. La valeur
elle-même n'a pas à survivre dans un document : elle changera au premier
changement de paramètre d'amorçage et piégera le brief suivant.

### Non bloquant, à traiter tant que le fichier est ouvert

**N1 — code mort dans le test central de SC8.** Dans
`sim/tests/test_write_coverage.py`, la variable `declared` est initialisée
par une expression qui ne fait pas ce que son nom annonce : elle construit
un ensemble à partir du *nom de la classe* renvoyée par l'appel, jamais des
noms de champs, et la branche de repli n'est donc jamais évaluée. Cette
variable, ainsi que `model_like`, `cell_field_names` et
`written_cell_fields`, ne sert à rien dans la suite du test. À supprimer :
du code mort dans le contrôle qui porte la condition la plus délicate du
brief rend la relecture du contrôle plus difficile qu'elle ne devrait
l'être.

**N2 — paramètre inutilisé dans l'amorçage.** `_seed_food_stock` de
`sim/world.py` reçoit la superficie mais ne l'utilise pas ; la formule
documentée dans `sim/SEEDING.md` n'en a effectivement pas besoin. Retirer
le paramètre, ou l'utiliser si l'intention était de faire dépendre le stock
initial de la superficie — dans ce cas la documentation doit suivre.

**N3 — la garde de l'ADR est plus étroite que son intention.** Dans
`sim/model.py`, l'ensemble des noms interdits ne contient qu'une seule
forme normalisée, celle de `province_id`. Un champ nommé simplement
`province`, ou `province_code`, passerait la garde. Le brief parle de
`province_id` « ou équivalent » : élargir l'ensemble aux formes commençant
par `province`, et ajouter un cas de test pour la forme courte.

**N4 — inspection statique de SC9 fermée par exclusion de nom.** Le test
exclut `sim/__init__.py` et ne descend pas dans les sous-répertoires. C'est
sans effet aujourd'hui, ce fichier ne contenant aucune fonction, mais
l'exclusion est écrite par nom de fichier : le jour où le paquet gagne un
sous-module, il ne sera pas inspecté sans que rien ne le signale. Préférer
un parcours récursif des modules du moteur, avec les tests exclus par leur
répertoire et non le reste par leur nom.

**N5 — chiffres des artefacts recopiés dans le README.** `sim/README.md`
cite le nombre de cellules et le nombre d'arêtes des artefacts G3 en clair.
Ce n'est pas un échec au sens de la grille, qui ne vise que le code de
chargement et les tests, mais c'est la même famille de piège que la hard-won
rule `12` : ces deux nombres deviendront faux au prochain rejeu du pipeline
géographique. Renvoyer au fichier de statistiques plutôt que recopier ses
valeurs.

**N6 — nom d'événement du registre de coût.** La ligne ajoutée à
`harness/queue/cost-ledger.jsonl` utilise un nom d'événement avec un tiret
bas, là où les entrées précédentes et la valeur par défaut de l'outil de
registre utilisent un tiret. Le rapport agrégé fonctionne malgré tout,
mais l'incohérence gênera tout filtrage ultérieur par nom d'événement.

### Ce qu'il ne faut surtout pas faire pour repasser au vert

Retirer `cell_id` ou `area_km2` de `Cell` ferait passer un test de
couverture corrigé sans rien prouver — c'est l'échec disqualifiant numéro 6
de la grille. De même, relâcher l'assertion pour tolérer les champs non
couverts reviendrait à réécrire la grille après coup. La sortie est
d'ajouter les sites d'écriture et de lecture manquants au périmètre
d'analyse, pas de rétrécir le modèle.
