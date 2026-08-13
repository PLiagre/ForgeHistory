# Feedback — Brief 011, itération 1

**Authored**: 2026-08-12T16:33:00Z
**Auteur du feedback**: forge-evaluateur
**Verdict de fond**: REJECT (une condition de succès en échec : SC8)

Note de transparence : évaluation exécutée par un sous-agent hébergé par
Cursor, en remplacement de Claude (indisponible), sur instruction du
propriétaire, dans une session distincte de celle du Générateur.

Ce fichier ne répète pas le tableau par condition — il est dans
`verdict.md`. Il liste seulement ce qu'il faut changer, et comment.

Rappel de vocabulaire, expliqué une fois : « preuve rouge » = la sortie
d'un test qu'on fait volontairement échouer pour démontrer qu'il détecte
bien le défaut qu'il prétend surveiller ; « compteur dérivé » = un chiffre
calculé par le test lui-même à partir des données, jamais écrit à la main.

---

## B1 (bloquant) — SC8 : le test de couverture ne regarde pas les champs déclarés

### Ce qui ne va pas

`sim/tests/test_write_coverage.py` part des attributs **écrits** sur la
variable `cell` dans `sim/engine.py`, et vérifie :

1. que tout attribut écrit est bien déclaré sur `Cell` ;
2. que tout attribut écrit possède aussi un site de lecture.

Le sens que le brief exige — **partir des champs déclarés** et exiger pour
chacun un écrivain et un lecteur — n'est jamais vérifié. Le troisième test
se contente d'exiger qu'au moins un champ soit couvert.

### Comment je l'ai prouvé

Dans une copie de travail hors dépôt (aucun fichier du dépôt modifié), j'ai
ajouté à `Cell` un champ factice sans écrivain ni lecteur, puis rejoué :

`.venv/bin/python -m pytest sim/tests/test_write_coverage.py -v -s`

Résultat : trois tests au vert, code de sortie nul, et le compteur affiche
« 3 / 6 ». Le champ fantôme est compté comme non couvert et n'empêche rien.

Le dépôt livré est déjà dans cette situation : le compteur annonce
« 3 / 5 », c'est-à-dire deux champs déclarés (`cell_id` et `area_km2`) sans
site d'écriture dans le périmètre analysé, et la suite reste verte. C'est
littéralement le mode d'échec n°2 des principes de simulation, que SC8
devait rendre impossible.

### Correction attendue

1. **Élargir le périmètre analysé** à `sim/engine.py`, `sim/world.py` et
   `sim/model.py`. Les sites manquants existent déjà : `cell_id` et
   `area_km2` sont écrits comme arguments nommés du constructeur `Cell(...)`
   dans `sim/world.py`, et relus dans `World.to_dict()` ainsi que dans le
   moteur pour `area_km2`. Aucun champ n'a donc besoin d'être supprimé.
2. **Inverser la boucle** : itérer sur `dataclasses.fields(Cell)` et, pour
   chaque champ déclaré, exiger au moins un site d'écriture et au moins un
   site de lecture. Le message d'échec doit nommer le champ fautif et
   préciser lequel des deux sites manque.
3. **Durcir le compteur** : `champs_modele_couverts` doit devoir égaler le
   nombre total de champs déclarés. Il reste dérivé du parcours, jamais
   écrit en dur.
4. **Conserver** l'assertion « tout attribut écrit sur `cell` doit être
   déclaré » : c'est elle que le sabotage de SC10 fait passer au rouge.
5. **Deux preuves rouges au lieu d'une.** Rejouer le sabotage existant
   (retrait de `hunger_ticks`) pour régénérer `run_sabotage.txt`, et ajouter
   une seconde sortie rouge obtenue en déclarant un champ fantôme sans
   écrivain, sauvegardée dans `sim/tests/proof_red/` sous un nom distinct,
   déclarée dans le manifeste et appariée en `must_differ_from` avec la
   sortie verte correspondante. Sans cette seconde preuve, rien ne montre
   que le test corrigé détecte le défaut dans le sens exigé par SC8.

### Ce qu'il ne faut surtout pas faire

Retirer `cell_id` ou `area_km2` du modèle, ou relâcher l'assertion pour
tolérer les champs non couverts. Le premier est l'échec disqualifiant n°6
de la grille (« faire passer SC8 en retirant des champs plutôt qu'en
ajoutant des écrivains et des lecteurs ») ; le second revient à réécrire la
grille après coup.

---

## B2 (bloquant) — hard-won rule 12 : condensé SHA256 recopié dans le journal

`deliverables/generator-log.md`, section du compteur des ticks
déterministes, affiche la valeur hexadécimale du condensé des deux runs en
plus de la citer par son nom de variable. Le brief l'interdit
explicitement : le condensé se cite « par son nom de variable […] jamais
par recopie d'une valeur hexadécimale en dur dans un test ou un document ».

Le code et les tests sont propres sur ce point : la faute est uniquement
dans le journal.

Correction : remplacer les deux lignes portant la valeur par une
formulation qui cite les noms de variable, affirme leur égalité, et renvoie
à la commande à rejouer pour obtenir la valeur du jour. Cette valeur
changera au premier ajustement d'un paramètre d'amorçage ; un document qui
la conserve piège le brief suivant.

---

## N1 (non bloquant) — code mort dans le test central de SC8

Dans `sim/tests/test_write_coverage.py`, la variable `declared` est
construite à partir du **nom de la classe** renvoyée par l'appel aux champs
de la dataclass, pas des noms de champs ; l'expression étant toujours vraie,
la branche de repli n'est jamais évaluée. Cette variable, comme
`model_like`, `cell_field_names` et `written_cell_fields`, n'est utilisée
nulle part ensuite. À supprimer : du code mort dans le contrôle qui porte
la condition la plus délicate du brief rend sa relecture plus difficile
qu'elle ne devrait l'être.

## N2 (non bloquant) — paramètre inutilisé dans l'amorçage

`_seed_food_stock` dans `sim/world.py` reçoit la superficie et ne s'en sert
pas ; la formule documentée dans `sim/SEEDING.md` n'en a pas besoin. Soit
retirer le paramètre, soit l'utiliser — et dans ce cas mettre la
documentation à jour.

## N3 (non bloquant) — la garde ADR est plus étroite que son intention

Dans `sim/model.py`, l'ensemble des noms interdits ne contient que la forme
normalisée de `province_id`. Un champ nommé `province` ou `province_code`
passerait la garde, alors que le brief vise `province_id` « ou équivalent ».
Élargir aux noms normalisés commençant par `province`, et ajouter un cas de
test pour la forme courte.

## N4 (non bloquant) — l'inspection statique de SC9 exclut par nom de fichier

Le test parcourt les modules du paquet à plat en excluant `sim/__init__.py`.
Sans effet aujourd'hui (ce fichier ne contient aucune fonction), mais le
jour où le paquet gagne un sous-module, celui-ci ne sera pas inspecté et
rien ne le signalera. Préférer un parcours récursif des modules du moteur,
en excluant le répertoire de tests plutôt qu'en énumérant des noms.

## N5 (non bloquant) — nombres d'artefacts recopiés dans le README

`sim/README.md` cite en clair le nombre de cellules et le nombre d'arêtes
des artefacts G3. La grille ne vise que le code de chargement et les tests,
donc ce n'est pas un échec — mais c'est la même famille de piège que la
hard-won rule 12 : ces nombres deviendront faux au prochain rejeu du
pipeline géographique. Renvoyer au fichier de statistiques au lieu de
recopier ses valeurs.

## N6 (non bloquant) — nom d'événement du registre de coût

La ligne ajoutée à `harness/queue/cost-ledger.jsonl` nomme l'événement avec
un tiret bas, là où toutes les entrées précédentes et la valeur par défaut
de l'outil de registre utilisent un tiret. Le rapport agrégé compte bien
l'entrée, mais l'incohérence gênera tout filtrage par nom d'événement.

---

## Ce qui est acquis et n'a pas à être refait

Ces points ont été reconstruits par mes propres commandes et sont
satisfaits ; l'itération suivante ne doit pas les dégrader :

- chargement des artefacts G3 sans aucune constante écrite en dur, valeurs
  comparées côte à côte au fichier de statistiques ;
- garde de l'ADR sur la clé spatiale, prouvée par exécution et non par
  lecture de code ;
- amorçage paramétrique documenté, déclaré non historique, et déterministe
  à graine égale ;
- déterminisme du tick sur dix pas, condensés comparés, aucune valeur en
  dur dans le code ni les tests ;
- chaîne causale testée maillon par maillon avec états construits à la main
  et seuil importé d'une constante nommée, plus le test d'intégration qui
  passe bien par le tick complet ;
- preuve rouge de SC10 authentique : la sortie de sabotage contient un échec
  réel, pas une suite verte déguisée ;
- suite `sim/` verte et suite du harnais intacte.
