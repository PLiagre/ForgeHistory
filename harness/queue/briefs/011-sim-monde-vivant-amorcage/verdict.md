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

---

# Verdict — Brief `011`, itération 2

**Authored**: 2026-08-12T16:49:34Z
**Author**: forge-evaluateur

La section ci-dessus (itération 1) est conservée intacte : elle reste le
verdict qui a motivé le REJECT et le feedback auquel le Générateur a
répondu. Les chiffres qu'elle cite sont ceux de l'itération 1 et ne
décrivent plus l'état actuel des preuves, qui ont été régénérées.

## Note de transparence

Comme à l'itération 1, cette évaluation a été exécutée par un sous-agent
hébergé par Cursor, en remplacement de Claude (indisponible), sur
instruction du propriétaire du projet, dans une session distincte de celle
du Générateur et orchestrée depuis l'extérieur du harnais habituel. Aucune
ligne de code du lot n'a été modifiée par l'Évaluateur : les deux
contre-preuves décrites plus bas ont été montées dans des copies de travail
situées hors du dépôt.

## Mechanical Gate Result

Commande exécutée depuis la racine du dépôt :
`.venv/bin/python harness/verdict_audit.py harness/queue/briefs/011-sim-monde-vivant-amorcage`.

Avant l'écriture de la présente section, le gate rendait `VERDICT: ACCEPT`
avec ses dix contrôles au vert. Même avertissement de lecture qu'à
l'itération 1 : le gate ne juge que la forme et ne lit pas la conclusion de
l'Évaluateur ; c'est le verdict de fond ci-dessous qui décide.

## Vérification point par point du feedback

| Point | État | Preuve reconstruite par l'Évaluateur |
|---|---|---|
| B1 — SC8 : parcourir les champs déclarés | **Corrigé** | Voir le détail ci-dessous : le test itère bien sur les champs déclarés, le compteur exige l'égalité avec le total, et les deux contre-preuves rougissent réellement. |
| B2 — condensé SHA256 recopié dans le journal | **Corrigé** | Recherche par motif d'une chaîne hexadécimale longue sur tout `sim/` et sur tout le dossier du brief : aucune occurrence. Le journal cite désormais les seuls noms de variable et renvoie à la commande à rejouer. |
| N1 — code mort dans le test central | **Corrigé** | Le fichier a été réécrit ; les quatre variables inutilisées que je citais ont disparu. Relecture intégrale du nouveau fichier : aucune variable morte, aucune expression sans effet. |
| N2 — paramètre inutilisé dans l'amorçage | **Corrigé** | La fonction d'amorçage du stock ne reçoit plus la superficie ; l'appelant est mis à jour. La formule documentée dans `sim/SEEDING.md` reste exacte, et l'amorçage donne le même résultat qu'avant (vérifié par le déterminisme rejoué, empreinte inchangée). |
| N3 — garde ADR trop étroite | **Corrigé** | La garde refuse maintenant tout nom normalisé commençant par « province ». Deux cas de test ajoutés (forme courte et forme avec suffixe) ; les cinq tests de conformité passent à l'exécution. |
| N4 — inspection statique fermée par exclusion de nom | **Corrigé** | Le parcours est devenu récursif avec exclusion du répertoire de tests. Sortie rejouée : 5 fichiers inspectés au lieu de 4, dont le fichier d'initialisation du paquet auparavant exclu. |
| N5 — nombres d'artefacts recopiés dans le README | **Corrigé** | Recherche par motif dans tout `sim/` : plus aucune occurrence des deux nombres. Le README renvoie au fichier de statistiques. |
| N6 — nom d'événement du registre de coût | **Corrigé pour l'avenir** | La ligne ajoutée à cette itération utilise la forme avec tiret, conforme aux entrées précédentes. La ligne de l'itération 1 conserve la forme fautive : le registre est en ajout seul, on ne réécrit pas une entrée passée. Acceptable. |

### Détail de B1 — les deux contre-preuves refaites par l'Évaluateur

Lecture du test corrigé : il itère bien sur les champs déclarés de la
dataclass, et exige pour chacun au moins un site d'écriture et au moins un
site de lecture, en analysant trois fichiers du moteur au lieu d'un seul.
Les sites d'écriture reconnus incluent les arguments nommés du constructeur,
ce qui est la raison pour laquelle les deux champs auparavant non couverts
le sont désormais sans qu'aucun champ ait été retiré du modèle. Le compteur
exige l'égalité stricte avec le total déclaré, et vaut 5 sur 5 à
l'exécution.

Contre-preuve (a), champ fantôme. Dans une copie hors dépôt, j'ai ajouté à
la dataclass un champ sans écrivain ni lecteur, puis rejoué le fichier de
test : deux tests en échec, code de sortie non nul, message nommant le champ
fautif et précisant les deux sites manquants. Ma sortie est **identique
octet pour octet** au fichier `sim/tests/proof_red/run_phantom_red.txt`
livré, à la seule ligne du répertoire racine près — qui diffère
nécessairement puisque j'ai exécuté hors du dépôt. La preuve livrée n'est
donc pas rédigée à la main.

Contre-preuve (b), sabotage de SC10. Même méthode, en retirant cette fois le
champ de faim de la dataclass : un test en échec, code de sortie non nul,
message citant l'attribut écrit mais non déclaré. Là encore ma sortie est
identique octet pour octet au fichier `sim/tests/proof_red/run_sabotage.txt`
livré, à la ligne du répertoire racine près.

Les deux sorties vertes livrées sont, elles, identiques octet pour octet à
ce que produit le dépôt dans son état actuel.

Les deux paires sont déclarées au manifeste sous `must_differ_from`, les
quatre fichiers sont suivis par git, et les horodatages montrent bien le
rouge produit avant le vert dans chaque paire.

## Per-Rubric-Line Verdict (grille complète re-déroulée)

| Success Condition | PASS/FAIL | Evidence (rejouée par l'Évaluateur à l'itération 2) |
|---|---|---|
| SC1 — paquet importable et documenté | PASS | Importation du paquet : version non vide affichée. README à jour. |
| SC2 — chargement depuis les artefacts G3 | PASS | Recompté depuis les artefacts : 596 cellules, égal au champ de comptage du fichier de statistiques ; 1364 arêtes. Aucune de ces valeurs n'apparaît plus nulle part dans `sim/`, code, tests ou documentation. |
| SC3 — `cell_id` seule clé spatiale (ADR `0003`) | PASS | Cinq tests de conformité exécutés, tous verts, dont deux nouveaux couvrant la forme courte et la forme suffixée. La garde lève une erreur explicite à l'instanciation, vérifiée par exécution. |
| SC4 — amorçage documenté et déterministe | PASS | Deux chargements avec la même graine : populations et stocks strictement identiques. Rejoué deux fois de suite, même résultat. La documentation d'amorçage reste exacte après le retrait du paramètre inutilisé. |
| SC5 — boucle de tick déterministe | PASS | Rejoué deux fois de suite, hors des tests livrés : deux séries de dix pas avec la même graine donnent des condensés égaux, une graine différente donne un condensé différent. Aucune valeur de condensé écrite en dur, ni dans le code, ni dans les tests, ni désormais dans le journal. |
| SC6 — économie physique de la nourriture | PASS | Les deux champs et leurs sentinelles `-1` sont inchangés ; l'ordre production puis consommation puis faim puis mortalité dans le pas de temps est inchangé. |
| SC7a / SC7b / SC7c / SC7d | PASS | Les quatre tests rejoués : maillons isolés avec états construits à la main, seuil importé d'une constante nommée, intégration passant par le pas de temps complet. |
| SC8 — couverture d'écriture sur tous les champs | **PASS** (était FAIL) | Le test parcourt les champs déclarés, le compteur exige l'égalité avec le total et vaut 5 sur 5, et les deux contre-preuves refaites par mes soins rougissent réellement. Le défaut de l'itération 1 est fermé, et il l'est en ajoutant des sites d'écriture au périmètre d'analyse, non en retirant des champs du modèle. |
| SC9 — aucun compteur codé en dur | PASS | Inspection statique rejouée, désormais récursive : 0 littéral non nommé sur 5 fichiers inspectés. |
| SC10 — preuve rouge d'abord | PASS | Quatre artefacts de preuve suivis par git, deux paires déclarées au manifeste, rouge avant vert dans chaque paire, et authenticité établie par reproduction octet pour octet. Diffs recalculés : 53 lignes pour la paire de sabotage, 94 pour la paire du champ fantôme. |
| SC11 — suite de tests entièrement verte | PASS | Suite complète du paquet rejouée : code de sortie nul, tous les tests collectés au vert (`20` tests, contre `18` à l'itération 1). Suite du harnais rejouée également : intacte, seuls les cas Unity restent ignorés comme prévu sous Linux. |
| SC12 — `sim/README.md` mis à jour | PASS | Modules, commande de test, source des données et histoire du stub toujours présents ; les deux nombres d'artefacts ont été remplacés par un renvoi au fichier de statistiques. |

### Reconstruction indépendante des compteurs

Tous reconstruits par mes propres commandes, aucun repris du manifeste :
cellules chargées 596 sur 596 ; arêtes 1364 sur 1364 ; champs de modèle
couverts 5 sur 5 ; compteurs en dur trouvés 0 sur 5 fichiers ; amorçage
déterministe 1 sur 1 ; ticks déterministes 1 sur 1 ; maillons unitaires 3
sur 3 ; intégration 1 sur 1 ; lignes différentes de la paire de sabotage 53
sur 53 ; lignes différentes de la paire du champ fantôme 94 sur 94.

Le manifeste porte en outre un compteur d'archive valant 70, qui documente
la mesure de l'itération 1 citée plus haut dans ce fichier. Je l'ai
reconstruit à partir des artefacts de l'itération 1 conservés dans
l'historique git : la valeur est exacte. Voir toutefois la réserve R1
ci-dessous, la commande déclarée pour ce compteur n'étant pas celle qui
produit sa valeur.

## Overall Verdict: PASS

Les douze conditions de succès sont satisfaites et vérifiées par
reconstruction indépendante. Les deux points bloquants de l'itération 1 sont
fermés, les six points non bloquants aussi. Aucun échec disqualifiant de la
grille n'est présent.

## Boundary Violations

Aucune. Le périmètre est respecté : hors de `sim/`, seuls le dossier du
brief et le registre de coût du harnais ont changé, ce dernier par un simple
ajout en fin de fichier. `VISION.md`, `pipeline/geo/`, `unity/`,
`docs/adr/` et le code du harnais sont intacts, vérifié par comparaison avec
la branche de référence sur tous les chemins hors périmètre.

Les non-objectifs de contenu restent tenus : pas de commerce entre cellules,
pas d'objet Province, pas de familles ni de personnes, aucune donnée
historique inventée.

## What Improved Since Last Iteration

- **Le contrôle de SC8 est devenu un vrai contrôle.** Il part maintenant des
  champs déclarés, ce qui est le sens qui permet de détecter le mode d'échec
  visé, et il possède deux familles de preuve rouge au lieu d'une. C'est la
  correction demandée, faite de la bonne manière : le périmètre d'analyse a
  été élargi pour trouver les sites d'écriture manquants, aucun champ n'a
  été retiré du modèle pour faire passer le test.
- **Les preuves rouges sont authentifiables.** Deux d'entre elles se
  reproduisent octet pour octet depuis une copie hors dépôt. C'est le
  standard le plus élevé que je puisse demander à ce type d'artefact.
- **La garde de l'ADR couvre maintenant l'intention et pas seulement la
  lettre**, avec des cas de test pour les formes qui passaient auparavant.
- **Les deux sources de valeurs périmées ont été supprimées** : le condensé
  dans le journal et les nombres d'artefacts dans le README. La hard-won
  rule `12` est respectée dans tous les documents du lot.
- **L'inspection statique ne se referme plus par exclusion de nom** : un
  futur sous-module sera inspecté sans intervention.

## What Regressed Since Last Iteration

Rien. Les conditions déjà satisfaites à l'itération 1 le sont encore, y
compris le déterminisme, dont l'empreinte est inchangée malgré la
modification de la signature de la fonction d'amorçage du stock — ce qui
confirme que ce changement était bien neutre.

## Réserves — à traiter dans un brief ultérieur, non bloquantes ici

**R1 — le compteur d'archive du manifeste n'est pas reproductible par sa
propre commande.** L'entrée qui conserve la mesure de l'itération 1 déclare
comme commande le diff des deux fichiers de preuve courants ; or ces
fichiers ont été régénérés, et cette commande produit aujourd'hui la valeur
de l'itération 2, pas celle qu'elle documente. J'ai pu retrouver la valeur
annoncée, mais seulement en comparant les versions de ces fichiers telles
qu'elles existent dans le commit de l'itération 1, ce que la commande
déclarée ne dit pas. Le journal du Générateur explique honnêtement pourquoi
cette entrée existe — garder traçable un nombre cité dans la section
d'itération 1 de ce fichier — et c'est ce qui m'empêche d'y voir un
compteur fabriqué. Correction attendue : remplacer la commande déclarée par
celle qui produit réellement la valeur, c'est-à-dire un diff des deux
fichiers extraits du commit de l'itération 1, ou retirer l'entrée le jour où
plus aucun document ne cite ce nombre. Un compteur dont la commande ne rend
pas la valeur est précisément ce que la hard-won rule 3 interdit ; ici la
mesure est réelle et vérifiée, c'est son étiquette qui est fausse.

**R2 — le contrôle de SC8 est lié à une seule dataclass.** Il nomme
explicitement la dataclass du modèle au lieu de découvrir toutes les
dataclasses du paquet. Aujourd'hui il n'en existe qu'une, donc la couverture
est complète ; le jour où une deuxième entité apparaît, elle échappera
silencieusement au contrôle. Faire découvrir les dataclasses par
introspection du module de modèle.

**R3 — la détection des sites d'écriture ne vérifie pas l'objet écrit.**
Toute affectation d'attribut portant le nom d'un champ compte comme un site
d'écriture, quel que soit l'objet. Un futur champ dont le nom coïnciderait
avec un attribut sans rapport ailleurs dans le paquet serait compté comme
couvert sans l'être. Restreindre la reconnaissance aux objets dont le type
est celui de l'entité, ou au minimum aux variables dont le nom est celui
attendu, comme le fait déjà l'autre assertion du fichier.

**R4 — deux fichiers de preuve verte identiques.** La sortie verte de la
paire de sabotage et celle de la paire du champ fantôme sont le même texte,
puisque c'est le même run vert. Ce n'est pas une faute — chaque paire diffère
bien — mais un seul fichier vert référencé par les deux paires dirait la
même chose sans dupliquer un artefact.
