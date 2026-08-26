# Grille d'évaluation — Brief 039

**Authored**: 2026-08-26T09:50:00Z
**Author**: Claude

Cette grille est écrite avant tout livrable. Elle ne remplace pas le brief et
n'ajoute aucune instruction d'exécution.

| condition | preuve attendue | échec si |
|---|---|---|
| dépendance | panier présent et extraction en place au démarrage du lot | lot lancé avant la fusion du 037 ou du 038 |
| SC1 — identité au bit près | sorties CLI 20 et 365 ticks archivées avant édition et rejouées, comparées champ par champ | une seule différence, comparaison contre une copie fabriquée après coup |
| SC2 — le maillon ne nomme plus la nourriture | contrôle sur l'arbre syntaxique, nombre de fonctions dérivé ; rouge cité sur le SHA de base | occurrence résiduelle, rouge non prouvé, contrôle qui ne peut pas rougir |
| SC3 — deuxième marchandise consommée | micro-monde, consommation déclarée dans le seul accès prévu, circulation selon les cinq règles sans ligne ajoutée au maillon ; somme des transferts d'une arête saturée égale à sa capacité, jamais deux fois | branche spéciale dans le maillon, copie spécialisée, règle altérée, capacité recopiée par marchandise |
| SC4 — ce que personne ne consomme ne bouge pas | stocks miniers égaux à l'extraction cumulée, mesurés | transport inventé sans consommateur, zéro rapporté sans mesure |
| SC5 — conservation par marchandise | sommes avant et après identiques, pour la nourriture et pour la marchandise d'essai | kilogrammes créés ou détruits, écart absorbé par une tolérance |
| SC6 — dette hors d'atteinte | contrôle existant vert sans modification, compteur de modifications nul | dette touchée par le commerce, contrôle retouché |
| SC7 — déterminisme | deux sorties identiques ; deux ordres d'insertion des paniers donnant le même résultat | dépendance à l'ordre d'un dictionnaire, un seul ordre essayé |
| SC8 — invariants | suite verte ; quatre contrôles de commerce inchangés et verts ; tests collectés non réduits ; aucun `global` | contrôle supprimé, littéral numérique dans une fonction, second maillon commerce |
| unicité | un seul maillon commerce dans tout `sim/` ; une seule capacité d'arête par tick, partagée | copie spécialisée pour la nourriture ; plafond dupliqué par marchandise |
| périmètre | diff limité à `engine.py`, éventuellement `constants.py`, et des ajouts dans `test_commerce.py` | modification de `world.py`, `model.py`, `snapshot_export.py`, `__main__.py`, `aggregation.py`, des autres tests, de la carte ou du visualiseur |
| compteurs | manifeste et mesureur reconstruisent numérateurs et dénominateurs depuis les données et les exécutions | nombre écrit à la main, dénominateur fixe, sentinelle prise pour une mesure, occurrence avant nulle |
| indépendance | compte-rendu final produit hors de l'invocation qui a écrit le code, et accepté par la porte mécanique | exécutant qui prononce sa propre recevabilité ou écrit le verdict |
