# Eval Rubric — Brief 026 : les gisements extractifs de 1400 (R1)

**Authored**: 2026-08-20T21:20:00Z
**Author**: forge-planificateur
**Amendé**: 2026-08-21T07:13:06Z — `amendment-001-arbitrage-gisements.md`
**Statut du lot jugé**: **PRÊT SOUS CONDITION** — l'arbitrage du propriétaire
est rendu et constatable ; la dépendance dure au lot 025 fusionné reste
entière

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
  copie de travail **hors du dépôt**.
- **Résultat attendu** : ce que le Générateur doit avoir produit.

Vocabulaire : voir la section « Vocabulaire » du brief — non reproduit ici
(Single Source of Instruction).

**Deux avertissements transversaux.**

1. **Ce lot se juge sur son mécanisme, pas sur son érudition.** La liste de
   gisements est une amorce déclarée, de provenance « connaissance historique
   générale, non sourcée par citation primaire » (décision D2 du brief), et
   son contenu relève de l'arbitrage du propriétaire (Condition 0), pas de
   l'Évaluateur. Celui-ci ne rejette pas le lot parce qu'une date lui paraît
   discutable. **Cela vaut mot pour mot pour la classe de richesse** : une
   `richness_class` qui paraît sous-estimée ou sur-estimée est un point à
   consigner, jamais un motif de rejet — le propriétaire a accepté l'amorce
   comme provisoire et remplaçable. Ce qui fait rejeter le lot : un gisement
   rattaché sans être contenu, omis sans être compté, doté d'une quantité, une
   classe numérisée ou posée sur une cellule, ou un mécanisme non réversible.
2. **Les nombres de contexte du brief (`27` gisements, `25` cellules, `596`
   cellules de maille, `0`, et la distribution des trois classes de richesse)
   ne sont pas des cibles.** Un contrôle qui s'y compare au lieu de dériver est
   un contrôle qui nomme sa propre référence (règle n° 2) et se rejette, même
   vert. **Et si l'amendement d'arbitrage avait modifié la liste, ces nombres
   ne vaudraient plus rien du tout** : toute quantité se relit alors de
   `data/resources_1400.json` et de `artifacts/cells_g3.json`, jamais de ce
   document ni du brief. *Constat au 2026-08-21 : l'amendement retient la
   liste de D4 et l'amende d'une seule colonne, `richness_class` ; les nombres
   de contexte restent donc comparables — mais l'Évaluateur le vérifie
   lui-même en Condition 0, il ne le tient pas de cette note.*

**Deux préalables bloquants, à vérifier avant tout le reste.**

**Premier — l'arbitrage du propriétaire.** Ce brief portait une décision
produit que le Planificateur n'avait pas l'autorité de prendre : quels
gisements existent dans le monde de 1400, sous quelle exigence de provenance,
et ce qu'un gisement porte. Le propriétaire a tranché le 2026-08-21
(`hermes/requests/DEMANDE-20260821-arbitrage-gisements-026.md`). Le lot ne
devient exécutable que par l'amendement qui porte ces réponses, **et suivi par
git** — un amendement non committé ne se constate pas.

```
git ls-files harness/queue/briefs/026-geo-gisements-1400-r1/amendment-001-arbitrage-gisements.md
```

Si la sortie est **vide**, le lot n'aurait pas dû tourner : l'Évaluateur ne
juge pas le travail, il constate un lot lancé avant son heure et le renvoie.
Si l'amendement existe, l'Évaluateur vérifie lui-même les quatre points de
SC0 — présence, décision propriétaire citée **et existante**, trois questions
tranchées, liste appliquée conforme. Un amendement écrit par le Générateur
lui-même est disqualifiant : le producteur ne s'accorde pas son autorisation.

**Second — le lot 025 fusionné.** Ce brief lit
`WORLD_TERMS_FORBIDDEN_KEYS` dans `pipeline/geo/constants.py` :

```
.venv/bin/python -c "import sys; sys.path.insert(0,'pipeline/geo'); import constants; print(len(constants.WORLD_TERMS_FORBIDDEN_KEYS))"
```

Si cette commande échoue, le lot n'aurait pas dû être exécuté ; si elle
réussit mais que `qa/checks_r1.py` recopie la liste au lieu de l'importer,
c'est disqualifiant.

---

## Condition 0 — L'arbitrage a eu lieu (SC0)

**Vérification** : les deux commandes ci-dessus, plus la lecture de
`amendment-001-arbitrage-gisements.md`.

**Reconstruction indépendante** : ouvrir le chemin de décision propriétaire
que l'amendement cite et vérifier qu'il **existe** et qu'il tranche bien la
question — une citation vers un fichier absent, ou vers un document qui ne
parle pas des ressources, ne vaut pas décision. Vérifier ensuite que la liste
réellement écrite dans `data/resources_1400.json` est celle que l'amendement
retient : ni la liste de D4 si l'amendement l'a remplacée, ni une liste
retouchée en cours de route.

**Trois vérifications propres à l'arbitrage du 2026-08-21.**

1. **La colonne `richness_class` est celle de D4, ligne par ligne.** Comparer
   les vingt-sept classes écrites dans `data/resources_1400.json` à la table
   de D4, entrée par entrée. Une seule classe changée en passant est
   disqualifiante — au même titre qu'une coordonnée corrigée : le Générateur
   recopie la donnée déclarée, il ne l'arbitre pas (`Non-Goals` du brief).
2. **Le vocabulaire appliqué est celui de l'amendement.** Vérifier que
   `R1_VALID_RICHNESS_CLASSES` de `constants.py` porte exactement les trois
   valeurs tranchées par l'amendement, et que `valid_richness_classes` du
   fichier de déclarations coïncide avec elles. Une quatrième classe, un
   renommage, une variante accentuée ou traduite est disqualifiante.
3. **L'amendement dit ce qu'il fait de la liste.** Vérifier qu'il l'annonce
   explicitement — retenue ou remplacée — et qu'il n'existe **pas** deux
   tables concurrentes dans le répertoire de brief. Une table recopiée à la
   fois dans l'amendement et dans D4 est un défaut structurel, même si les
   deux coïncident aujourd'hui : elles dériveront (`CLAUDE.md` › Single Source
   of Instruction).

**Contre-preuve disqualifiante** : si l'amendement cite une décision
inexistante et que le lot a tourné quand même, `decision_proprietaire_citee`
doit valoir `0` et la condition être en échec — un amendement qui s'autorise
tout seul est le défaut exact que SC0 existe pour attraper.

**Résultat attendu** : `amendement_arbitrage_present` à `1`,
`decision_proprietaire_citee` à `1`, `questions_arbitrage_repondues` à `3`
sur `3`, `liste_appliquee_est_celle_de_l_amendement` à `1`.

---

## Condition 1 — Déclarations complètes, légales, et dans la donnée (`R1-A`)

**Vérification** :
```
cd pipeline/geo && ../../.venv/bin/python tests/run_proof_r1.py
```
Lire `R1-A` dans `logs/v1_081_qa.json`, puis `gisements_declares`,
`declarations_incompletes`, `champs_de_gisement_hors_schema`,
`natures_hors_vocabulaire`, `certitudes_hors_vocabulaire` et
`gisements_en_dur_dans_le_module` dans `artifacts/stats_r1.json`.

**Reconstruction indépendante** : ouvrir `data/resources_1400.json` et
vérifier soi-même, entrée par entrée, que l'ensemble de ses clés **égale
exactement** `R1_REQUIRED_DEPOSIT_FIELDS`, **lu de `constants.py`** — pas
seulement qu'il le contient — et que chaque valeur est non vide. Faire la
même vérification d'égalité exacte sur chaque gisement de
`artifacts/resources_1400_r1.json` contre `R1_PUBLISHED_DEPOSIT_FIELDS`. Un
schéma qui accepterait une clé en plus laisserait rentrer la quantité que tout
ce lot existe pour interdire. Vérifier
que `valid_certainty_levels` du fichier de déclarations coïncide avec
`R1_VALID_CERTAINTY` et avec ce que `data/corrections_1400.json` emploie déjà
— aucun niveau nouveau n'a le droit d'apparaître. Vérifier que
`historical_reason` est une phrase réelle en français clair, et non un
gabarit répété à l'identique : des raisons identiques au mot près d'une
entrée à l'autre signalent un remplissage automatique, pas une déclaration.
Vérifier enfin
qu'aucun identifiant de gisement n'apparaît comme littéral dans
`steps/r1_resources_1400.py` — la liste doit vivre dans la donnée, pour que
le propriétaire puisse la remplacer sans toucher au code (D2).

**Contre-preuve disqualifiante** : dans une copie hors dépôt, retirer le
champ `source` d'une entrée — `R1-A` doit rougir en nommant l'entrée.
Remplacer une `certainty` par une valeur inventée — `R1-A` doit rougir aussi.
**Ajouter** à une entrée une clé qui n'est pas au schéma, par exemple
`tonnage_estime`, sans rien retirer — `R1-A` doit rougir sur le schéma fermé.
Coller enfin un identifiant de gisement comme littéral dans
`steps/r1_resources_1400.py`, sans rien changer d'autre — `R1-A` doit rougir
là encore. Si ce dernier sabotage laisse le contrôle vert, la promesse
« le propriétaire peut remplacer la liste sans toucher au code » n'est pas
tenue mécaniquement, et la condition est en échec.

**Résultat attendu** : `R1-A` vert, tous les compteurs de contrôle nuls,
`gisements_declares` strictement positif et lu du fichier.

---

## Condition 2 — Contenance seule, aucune omission silencieuse (`R1-B`, `R1-C`) — la condition centrale de ce brief

**Vérification** : même exécution ; lire `R1-B` et `R1-C`, puis
`gisements_rattaches`, `rattachements_non_contenus`,
`gisements_a_deux_cellules`, `gisements_hors_fenetre`,
`gisements_hors_terre`, `somme_categories_egale_declares`,
`cellules_dotees` et `cellules_a_plusieurs_gisements`.

**Reconstruction indépendante — la plus importante de ce brief** :

1. Pour **chaque** gisement rattaché, projeter soi-même sa position en
   EPSG:3035 et tester soi-même son appartenance au polygone de la cellule
   déclarée. Ce test se refait **entièrement**, sans échantillonnage : à
   l'échelle de la liste retenue et de la maille committée, c'est un calcul de
   quelques secondes.
2. **Chercher activement un plus-proche-voisin déguisé** dans
   `steps/r1_resources_1400.py` : tout appel à une distance, à un arbre de
   recherche spatiale, à un `nearest`, ou toute borne kilométrique est
   suspect. La contenance est la seule règle autorisée (D1, `R1-B`). Un
   rattachement juste obtenu par une méthode interdite est un échec : le lot
   fonctionnerait par coïncidence, et casserait au premier gisement mal
   placé.
3. Vérifier que la somme des trois catégories égale exactement le nombre de
   gisements déclarés, et que les identifiants des catégories `hors_fenetre`
   et `hors_terre` sont **nommés**, y compris quand les listes sont vides —
   une clé absente et une liste vide ne se valent pas (règle n° 10).
4. Vérifier qu'aucun gisement n'apparaît dans deux cellules de
   `cells_resources_r1.json`, et que la somme des longueurs des listes
   `resources` sur toutes les cellules égale `gisements_rattaches`.
5. Comparer `cellules_dotees` et `cellules_a_plusieurs_gisements` aux
   constats du Planificateur (`25` cellules distinctes pour `27` gisements)
   **sans en faire une condition**, et **uniquement si l'amendement
   d'arbitrage a retenu les entrées de D4 sans en ajouter ni en retirer**.
   L'ajout de la colonne `richness_class` ne change rien à ces deux constats :
   il ne touche ni les positions, ni le nombre d'entrées. Si en revanche la
   liste elle-même a changé, ces constats sont caducs et il n'y a rien à
   comparer : les deux compteurs restent simplement mesurés et rapportés. Dans
   tous les cas, un écart s'explique dans le journal du Générateur, il ne se
   sanctionne pas.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, déplacer un
gisement dans la cellule voisine sans changer sa position — `R1-B` doit
rougir. Retirer un gisement de `resources_1400_r1.json` sans l'inscrire dans
aucune catégorie — `R1-C` doit rougir sur la somme, et pas seulement sur le
compte. Déplacer une coordonnée de quelques degrés pour l'envoyer en mer :
elle doit basculer en `outside_land`, être **nommée**, et **ne pas** être
rattachée à la côte la plus proche.

**Résultat attendu** : `R1-B` et `R1-C` verts,
`rattachements_non_contenus` nul, `gisements_a_deux_cellules` nul,
`somme_categories_egale_declares` à `1`.

---

## Condition 3 — Réversibilité (`R1-D`)

**Vérification** :
```
cd pipeline/geo && ../../.venv/bin/python pipeline.py --source resources_1400 --no-corrections
```
puis la même commande sans le drapeau ; lire
`logs/v1_081_declarations_off.txt` et `logs/v1_081_declarations_on.txt`.

**Avant toute chose, vérifier que la passe coupée ne publie pas.** La
décision D8 du brief exige que, déclarations coupées, la sortie parte dans un
répertoire temporaire dont le chemin est **imprimé**, et que `artifacts/` ne
soit pas touché. Le constater sur la sortie de la première commande, puis
confirmer que l'empreinte de `artifacts/cells_resources_r1.json` est
inchangée après elle. Un lot qui dépublierait ses artefacts dès qu'on lui
demande une démonstration de réversibilité est un lot dont l'état correct ne
tient qu'à l'ordre des commandes — c'est un défaut, même si les deux fichiers
diffèrent bien.

**Reconstruction indépendante** : calculer soi-même les empreintes des deux
`cells_resources_r1.json` produits (celui publié et celui du répertoire
temporaire) et vérifier qu'elles **diffèrent** — sans recopier aucune valeur
hexadécimale dans un document (règle n° 12) : la comparaison se fait à
l'exécution, et le rapport nomme les deux sources. Vérifier que le fichier
produit déclarations coupées contient toujours **toutes** les cellules de
`cells_g3.json`, chacune avec une liste `resources` vide : couper la
déclaration retire les gisements, **jamais** des cellules. C'est la
différence entre un monde sans mines et un monde sans terre.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, faire produire
au mode coupé un fichier identique au mode actif — `R1-D` doit rougir.
Faire produire au mode coupé un fichier amputé de ses cellules vides — la
reconstruction doit le détecter, même si `R1-D` est vert sur la seule
différence d'empreinte.

**Résultat attendu** : `R1-D` vert, `empreinte_off_differe_de_on` à `1`,
`cellules_totales_off` égal au nombre de cellules de `cells_g3.json`.

---

## Condition 4 — Ni barème, ni quantité, ni clé spatiale concurrente (`R1-E`, `R1-F`)

**Vérification** : lire `R1-E` et `R1-F`, puis `cles_de_bareme_trouvees`,
`cles_de_quantite_trouvees` et `cles_spatiales_concurrentes` ; puis
```
git status --porcelain pipeline/geo/artifacts/ pipeline/geo/registry/
```

**Reconstruction indépendante** : parcourir soi-même récursivement les six
fichiers balayés et vérifier qu'aucune clé de `WORLD_TERMS_FORBIDDEN_KEYS` ni
de `R1_FORBIDDEN_QUANTITY_KEYS`, **lus de `constants.py`**, n'y apparaît, à
quelque profondeur que ce soit. Refaire la comparaison avec la sémantique
exacte que D6 décrit — clé normalisée, comparée entière **et** par jetons
découpés sur `_`, les entrées interdites n'étant jamais découpées. Deux
conséquences à confirmer plutôt qu'à supposer : `tonnage_estime` serait rouge,
et la clé `outputs` du manifeste ne l'est pas. Un contrôle implémenté par
simple sous-chaîne rougirait sur `outputs` et serait donc désarmé au premier
lancement ; un contrôle implémenté par pure égalité laisserait passer
`tonnage_estime`. Vérifier que `R1_FORBIDDEN_QUANTITY_KEYS` contient bien les
mots de quantité les plus tentants (`tonnage`, `reserve`, `yield`,
`rendement`, `teneur`, `grade`, `intensite`) : un jeu de clés interdites trop
court viderait le contrôle de sa portée (règle n° 6).

**Sur l'absence délibérée de `richness` et `richesse` dans cette liste.** Le
brief (D5) les en retire, parce que le propriétaire a décidé qu'un gisement
porte une classe de richesse et qu'une liste bannissant le mot de la décision
forcerait à la renommer. L'Évaluateur ne traite donc pas cette absence comme
un affaiblissement — il vérifie que les **trois** garde-fous qui la
remplacent sont bien en place et rougissent : le schéma fermé des gisements
(Condition 1), `R1-G` (Condition 6), et le maintien de `grade`, `teneur`,
`intensite`, `tonnage`, `reserve`, `rendement` dans la liste. Si l'un des
trois manque, l'absence de `richness` devient un trou, et la condition est en
échec.
Vérifier ensuite que l'ensemble trié des `cell_id` de
`cells_resources_r1.json` est exactement celui de `cells_g3.json`, et
qu'aucune clé d'aucun artefact publié ne contient `province`, `owner`,
`country` ni `pays` — ADR-0003 : la Province est une agrégation dérivée,
jamais une clé stockée.

Vérifier enfin, par lecture directe de `steps/r1_resources_1400.py`, qu'aucun
contenu de `unity/game_unity/Assets/StreamingAssets/data/terrain_endowment.json`
n'a été copié, importé ou traduit : ce fichier est le contre-exemple du lot,
et le retrouver sous une autre forme dans le pipeline est disqualifiant.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, injecter une
clé `tonnage` au fond de `stats_r1.json` — `R1-E` doit rougir, y compris
imbriquée à plusieurs niveaux. Ajouter une clé `province_id` à une cellule —
`R1-F` doit rougir.

**Résultat attendu** : `R1-E` et `R1-F` verts, trois compteurs de clés nuls,
artefacts des lots précédents intacts.

---

## Condition 5 — Fichiers partagés en ajout seul, déterminisme, crochet, preuves, README

**Vérification** :
```
cd pipeline/geo && ../../.venv/bin/python pipeline.py --source resources_1400
```
puis
```
git diff --numstat -- pipeline/geo/constants.py pipeline/geo/pipeline.py
```
et
```
git ls-files pipeline/geo/artifacts/*r1* pipeline/geo/logs/*081* \
  pipeline/geo/capture/*081* pipeline/geo/registry/resource_registry.json \
  pipeline/geo/data/resources_1400.json pipeline/geo/steps/r1_resources_1400.py \
  pipeline/geo/qa/checks_r1.py pipeline/geo/tests/*r1*
```

**Reconstruction indépendante** :

1. Charger l'instantané pré-édition de `constants.py` et le fichier publié
   comme deux modules distincts, relever tous les noms de premier niveau de
   l'instantané, et vérifier que chacun existe encore avec la **même
   valeur**, comparée par représentation de l'objet. Un seul nom disparu ou
   changé est disqualifiant — y compris une constante `C1_*` posée par le lot
   025.
2. Extraire des deux versions de `pipeline.py` les neuf blocs
   `if args.source == "..."` préexistants et les comparer texte à texte —
   byte-identiques exigés. Vérifier que la valeur `"resources_1400"` a été
   ajoutée et que le drapeau `--no-corrections` a été **réemployé**, pas
   dupliqué sous un nouveau nom.
3. Vérifier que `pipeline/geo/qa/checks.py` et
   `pipeline/geo/qa/checks_c1.py` sont **inchangés**
   (`git status --porcelain` vide) et que `qa/checks_r1.py` **importe**
   `CheckResult` et `q10_determinism` au lieu de les redéfinir.
4. Relancer `tests/run_proof_r1.py` soi-même et comparer les empreintes de
   sortie à celles déjà committées — identiques exigées.
5. Lire le `README.md` publié : il doit énoncer R1 comme livré — présence,
   nature et classe qualitative de richesse —, dire explicitement que ni
   quantité, ni rendement, ni ressource agricole ou forestière ne le sont, que
   la classe est un nom pris dans un vocabulaire fermé de trois valeurs et
   jamais un nombre, et ne pas décrire le relief (G6) quel que soit l'état du
   lot 024. Comparer au `pre-edit` committé — doit différer.
6. **Regarder réellement** `capture/v1_081_resources_window.png` (règle
   n° 11) : les gisements doivent apparaître aux endroits attendus — étain en
   Cornouailles, sel en Pologne et en Franche-Comté, cuivre et fer en Suède
   centrale, mercure en Castille — et **aucun point ne doit flotter en mer**.
   Une carte où tous les points s'agglutinent au même endroit, ou où un point
   est en pleine eau, est un échec même avec huit contrôles verts. C'est
   exactement le genre de défaut qu'une suite verte ne voit pas. **Regarder
   aussi comment la classe de richesse est rendue, si elle l'est** : une forme
   de marqueur ou un libellé sont admis ; des points de tailles, de rayons ou
   d'opacités différents selon la classe ne le sont pas — c'est un barème
   dessiné, et aucun contrôle sur la donnée ne l'attrapera (D7 du brief).

**Contre-preuve disqualifiante** : dans une copie hors dépôt, changer la
valeur d'une constante préexistante sans supprimer de ligne — la
reconstruction du point 1 doit le détecter alors que
`constants_lignes_supprimees` resterait nul. Introduire un horodatage courant
dans un artefact — `Q10` doit rougir.

**Résultat attendu** : code de sortie `0`,
`controles_r1_verts` à `8` sur `8`,
`controles_r1_avec_preuve_rouge_non_vide` à `8` sur `8`,
`constants_lignes_supprimees` nul,
`branches_source_preexistantes_identiques` à `9` sur `9`, toutes les preuves
déclarées suivies par git, README honnête.

**Note pour l'Évaluateur — suite du harnais
(`tests_harness_passed_026`)** : aucun paquet de test n'est installé dans le
venv de cette machine au moment où ce brief est écrit. Si le compteur vaut
`-1` avec, dans `deliverables/generator-log.md`, la commande d'installation
réellement tentée et son erreur exacte, ne pas rejeter le lot sur ce seul
point. Rejeter tout `0` silencieux ou tout `PASS` non rejouable.

---

## Condition 6 — La classe de richesse est un nom, jamais un nombre (`R1-G`)

C'est la condition que l'arbitrage du propriétaire a ajoutée à ce lot. Elle ne
juge **pas** si une classe est bien attribuée — cela relève de l'amorce
provisoire, et l'avertissement transversal n° 1 s'applique. Elle juge que la
classe ne peut pas devenir un barème.

**Vérification** : même exécution que la Condition 1 ; lire `R1-G` dans
`logs/v1_081_qa.json`, puis `classes_hors_vocabulaire`,
`classes_en_dur_dans_le_module`, `classes_adossees_a_un_nombre`,
`somme_par_classe_egale_declares`, `classes_dans_les_cellules` et
`classes_distinctes_employees` dans `artifacts/stats_r1.json`.

**Reconstruction indépendante** :

1. Relire `R1_VALID_RICHNESS_CLASSES` de `constants.py` et vérifier soi-même,
   gisement par gisement, dans `data/resources_1400.json` **et** dans
   `artifacts/resources_1400_r1.json`, que `richness_class` est une chaîne non
   vide de ce vocabulaire. Un `3`, un `"majeure "` avec espace, un `null` ou
   une liste comptent comme hors vocabulaire.
2. Chercher soi-même les trois valeurs du vocabulaire comme **chaînes
   littérales** dans `steps/r1_resources_1400.py` et `qa/checks_r1.py` : il ne
   doit y en avoir aucune, et les deux modules doivent **importer** la
   constante. Une valeur écrite en dur est le premier pas d'une table d'ordre.
   Chercher aussi, par lecture directe, toute comparaison entre classes
   (`<`, `>`, tri, `index()`, dictionnaire classe → nombre) : une seule suffit
   à mettre la condition en échec, même si tous les compteurs sont nuls.
3. Recalculer soi-même la somme de `par_classe_de_richesse` et la comparer à
   `gisements_declares` relu du fichier de déclarations. Vérifier que les
   **trois** classes y figurent, y compris une classe à zéro le cas échéant :
   une clé absente et un zéro ne se valent pas (règle n° 10).
4. Parcourir soi-même `cells_resources_r1.json` de bout en bout et vérifier
   qu'aucune des trois valeurs n'y apparaît, ni en clé ni en valeur. Une
   cellule porte des identifiants de gisements, rien d'autre.
5. Vérifier que `classes_distinctes_employees` est **mesuré**, avec les trois
   classes du vocabulaire pour dénominateur, et qu'aucun contrôle ne s'y
   compare : ce compteur est un constat. Un `R1-G` qui exigerait que les trois
   classes soient employées nommerait sa propre référence (règle n° 2) et se
   rejette, même vert.

**Contre-preuve disqualifiante** : dans une copie hors dépôt, remplacer une
`richness_class` par le nombre `3` — `R1-G` doit rougir en nommant l'entrée.
Ajouter à `stats_r1.json` un bloc associant chaque classe à un coefficient
décimal — `R1-G` doit rougir sur `classes_adossees_a_un_nombre`. Fausser une
valeur de `par_classe_de_richesse` sans toucher au reste — `R1-G` doit rougir
sur la somme, et pas seulement sur la présence des trois clés. Ajouter enfin
`"richness_class": "majeure"` à une cellule de `cells_resources_r1.json` —
`R1-G` doit rougir : c'est le sabotage le plus important des quatre, parce
qu'une case notée est exactement la forme de `terrain_endowment.json` que ce
lot existe pour ne pas produire.

**Résultat attendu** : `R1-G` vert avec une preuve rouge non vide,
`classes_hors_vocabulaire`, `classes_en_dur_dans_le_module`,
`classes_adossees_a_un_nombre` et `classes_dans_les_cellules` nuls,
`somme_par_classe_egale_declares` à `1`, `classes_distinctes_employees`
mesuré et rapporté sans être comparé à quoi que ce soit.

---

## Échecs disqualifiants (toute la rubrique, transversal)

- Le lot exécuté sans `amendment-001-arbitrage-gisements.md` suivi par git.
- Un amendement d'arbitrage écrit ou complété par le Générateur lui-même.
- Un amendement citant une décision propriétaire dont le chemin n'existe pas
  dans le dépôt.
- Une liste écrite dans `data/resources_1400.json` qui n'est ni celle de D4
  ni celle que l'amendement retient — y compris une liste de D4 « corrigée »
  d'un site, d'une coordonnée ou d'une `richness_class` en passant.
- Une `richness_class` attribuée, déduite ou recalculée par le Générateur au
  lieu d'être recopiée de D4.
- Une quatrième classe de richesse, une classe renommée, accentuée ou
  traduite, dans `constants.py` ou dans le fichier de déclarations.
- Une classe de richesse convertie en nombre, ordonnée, indexée, pondérée, ou
  portée par une cellule de `cells_resources_r1.json`.
- Une valeur du vocabulaire des classes écrite en dur dans
  `steps/r1_resources_1400.py` ou `qa/checks_r1.py` au lieu d'être importée.
- Un `par_classe_de_richesse` dont la somme ne fait pas `gisements_declares`,
  ou auquel manque une classe à zéro.
- Une capture qui encode la classe de richesse par une taille, un rayon, une
  opacité ou une intensité de couleur.
- Deux tables de gisements concurrentes dans le répertoire de brief.
- Un `red_proof` vide sur n'importe lequel des huit contrôles.
- Une passe déclarations coupées qui écrit dans `pipeline/geo/artifacts/`.
- Un rattachement obtenu autrement que par contenance, même s'il tombe juste.
- Une coordonnée déplacée pour faire tomber un gisement sur la terre.
- Un gisement disparu sans catégorie nommée.
- Une quantité, une réserve, un tonnage, un rendement ou une teneur dans un
  artefact publié, sous quelque nom que ce soit.
- Une clé de `WORLD_TERMS_FORBIDDEN_KEYS` dans un artefact publié.
- Une clé contenant `province`, `owner`, `country` ou `pays` dans un artefact
  publié.
- Du contenu de `terrain_endowment.json` recopié, importé ou traduit dans le
  pipeline.
- `WORLD_TERMS_FORBIDDEN_KEYS` recopiée dans `qa/checks_r1.py` au lieu d'être
  importée.
- `qa/checks.py` ou `qa/checks_c1.py` modifiés, même d'un octet.
- Une constante préexistante de `constants.py` dont la valeur a changé, même
  sans suppression de ligne.
- Un artefact d'un lot précédent modifié ou régénéré.
- Un compteur rapporté comme `0` alors qu'il n'a jamais été calculé (la
  sentinelle attendue est `-1`, règle n° 8) — distinct d'un `0` réellement
  mesuré comme `gisements_hors_terre`.
- Une empreinte recopiée par valeur dans un test, un document ou un
  commentaire (règle n° 12).
- Toute lecture, écriture ou citation d'un fichier du lot 024.
