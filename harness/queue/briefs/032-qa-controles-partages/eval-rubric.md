# Rubrique d'évaluation — lot 032 (contrôles partagés)

**Authored**: 2026-08-23T19:41:00Z
**Author**: forge-planificateur

Écrite avant toute ligne de code. L'Évaluateur reconstruit chaque constat
lui-même : il ne reprend aucun chiffre du `generator-log.md` sans l'avoir
remesuré.

## Guide de lecture

Ce lot est une factorisation. Le risque n'est donc pas qu'il livre trop peu,
c'est qu'il **change quelque chose en chemin**. La rubrique est construite
autour de ça : la moitié des conditions vérifient que rien n'a bougé.

Une condition est remplie ou ne l'est pas. Il n'y a pas de « presque » : un
octet d'écart sur un artefact, un identifiant de contrôle manquant, un cas
rouge qui ne rougit plus, et la condition tombe.

## Condition 1 — Les cinq familles existent, et ne nomment aucun lot (SC1)

1. Lire `pipeline/geo/qa/checks_common.py` et y retrouver les cinq familles :
   maillage inchangé, réversibilité, absence d'omission silencieuse,
   rattachement par contenance, clés interdites et vocabulaire fermé.
2. Parcourir les signatures : aucun nom de fonction, de paramètre ou de
   constante ne doit contenir `c1`, `r1`, `g2`, `g3`, `g4`, `g5`, `g6`, `p1`
   ni `p2`. Un contrôle nommé d'après sa cible est le défaut que la règle
   n° 2 a coûté six fois — il tombe ici, pas après fusion.
3. Vérifier qu'aucune famille ne prend un paramètre « nom du lot », sous
   quelque orthographe que ce soit.

**Échec si** une famille manque, ou si un identifiant de lot apparaît dans une
signature de `checks_common.py`.

## Condition 2 — C1 et R1 n'implémentent plus les familles (SC2)

1. Vérifier que `checks_c1.py` et `checks_r1.py` importent `checks_common`.
2. Vérifier qu'aucune des dix fonctions suivantes n'y contient encore sa
   logique propre : `c1a_mesh_unchanged`, `c1f_no_gameplay_keys`,
   `r1b_containment_only`, `r1c_no_silent_omission`, `r1d_reversibility`,
   `r1e_no_bareme_ni_quantite`, `r1f_cell_mesh_unchanged`,
   `_key_matches_forbidden`, `_walk_forbidden_keys`, et le corps de
   `r1g_richness_class_is_name` pour sa part vocabulaire.
   Un simple renvoi vers `checks_common` est attendu ; une copie conservée
   « au cas où » est un échec.
3. Vérifier que les quinze identifiants (`Q10`, `C1-A`…`C1-F`, `R1-A`…`R1-G`)
   sont toujours exposés sous leurs noms actuels.

**Échec si** une des cinq familles subsiste en double, ou si un identifiant
disparaît, change de nom ou change de sens.

## Condition 3 — Zéro dérive d'artefact (SC3) — la condition centrale

1. Relever les empreintes SHA-256 des neuf fichiers de l'invariant 1 **tels
   qu'ils sont dans `master`**, par `git show`, pas depuis l'arbre de travail.
2. Rejouer soi-même `tests/run_proof_c1.py` puis `tests/run_proof_r1.py`.
   Les deux doivent sortir en code `0`.
3. Recalculer les neuf empreintes et les comparer une à une.

**Échec si** un seul des neuf fichiers diffère, même d'un octet, même si tous
les contrôles sont verts. Un artefact qui bouge pendant une factorisation
signifie que le comportement a changé sans que personne l'ait demandé.

**Échec également si** le Générateur a « mis à jour » un artefact pour le
faire correspondre : la comparaison se fait contre `master`, jamais contre une
sortie régénérée entre-temps.

## Condition 4 — Les quinze contrôles sont là, verts, et prouvés rouges (SC4)

1. Ouvrir `logs/v1_080_qa.json` et `logs/v1_081_qa.json` régénérés.
2. Vérifier : sept identifiants dans le premier, huit dans le second, dans le
   **même ordre** qu'avant.
3. Vérifier que chacun porte `passed` vrai **et** un `red_proof` non vide.
   Un `red_proof` vide veut dire que le contrôle n'a jamais été vu rouge : il
   ne prouve rien (règle n° 4), et présence n'est pas fonction (règle n° 7).
4. Prendre **deux** contrôles au hasard parmi les quinze, appliquer soi-même
   la mutation déclarée dans la table de `red_common.py`, et vérifier que le
   contrôle vire réellement au rouge. Une table de mutations qui se contente
   d'exister n'est pas une preuve.

**Échec si** un identifiant manque, si l'ordre change, si un `red_proof` est
vide, ou si une des deux mutations tirées au sort ne fait pas rougir son
contrôle.

## Condition 5 — `red_q10` écrit une fois, et les six lots restés en place (SC5)

1. Chercher `def red_q10` dans tout `pipeline/geo/` : une seule définition
   doit subsister, dans `tests/red_common.py`.
2. Vérifier que les sept `tests/test_qa_red_*.py` l'importent.
3. Rejouer les six preuves non migrées — `run_proof_g2.py`,
   `run_proof_g2b.py`, `run_proof_g3.py`, `run_proof_g4.py`,
   `run_proof_g5.py`, `run_proof_g6.py` — et vérifier code `0` pour chacune,
   empreintes d'artefacts inchangées.

**Échec si** une deuxième définition de `red_q10` subsiste, ou si une des six
preuves non migrées casse. Toucher au cas rouge partagé sans casser les lots
qui ne sont pas migrés est tout l'enjeu de ce point.

## Condition 6 — La mesure est faite, pas la note (SC6)

1. Relancer le parcours d'arbre syntaxique de `measure_032.py` et retrouver
   `lignes_familles_avant` et `lignes_familles_apres`.
2. Vérifier que la valeur « avant » se reconstruit depuis `master` (via
   `git show`), et non depuis l'arbre de travail déjà modifié.
3. Vérifier que « après » est inférieur à « avant ».

**Pas de seuil.** Aucun pourcentage de réduction n'est exigé et aucun n'est
récompensé : un lot qui gonflerait `checks_common.py` pour afficher un beau
chiffre échouerait à la condition 3 de toute façon.

**Échec si** la valeur « avant » a été recalculée depuis l'arbre modifié, ou
si l'un des deux compteurs vaut `0` ou `-1` alors que l'affirmation est faite.

## Échecs disqualifiants (transversaux)

- Un artefact modifié, quelle qu'en soit la justification.
- Un contrôle supprimé, renommé, assoupli ou fusionné avec un autre.
- Un cas rouge qui ne rougit plus.
- Une copie `.orig` committée pour un fichier que git suit.
- Un chemin de `manifest.json` écrit depuis la racine du dépôt au lieu du
  répertoire du brief — la porte le résout depuis le brief, et le lot 026 a
  livré trois couples de preuve qui, pour cette raison exacte, ne comparaient
  rien.
- Une affirmation d'impossibilité sans commande et sans erreur exacte.
- Un verdict, un commit, une branche ou une fusion produits par le Générateur.
- `python` invoqué à la place de `py`.

## Ce que l'Évaluateur ne reproche pas

- Que le gain de lignes soit modeste. Ce lot prépare surtout les suivants ;
  l'arriéré des six lots G est explicitement renvoyé au lot 033.
- Que `qa/checks.py` reste gros. Il n'est pas dans le périmètre.
- Un choix de découpage de paramètres différent de celui qu'il aurait fait,
  dès lors que les conditions 1 à 6 tiennent.
