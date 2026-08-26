# Grille d'évaluation — Brief 036

**Authored**: 2026-08-26T09:20:00Z
**Author**: Claude

Cette grille est écrite avant tout livrable. Elle ne remplace pas le brief et
n'ajoute aucune instruction d'exécution.

| condition | preuve attendue | échec si |
|---|---|---|
| dépendance | `sim/engine.py` sans instruction `global` au démarrage du lot | lot lancé avant la fusion du 034 |
| SC1 — une cellule rassasiée croît | micro-monde déterministe, borne de ticks dérivée du taux et de la population ; rouge cité sur le SHA de base | nombre de ticks écrit en dur, rouge non prouvé, croissance obtenue hors des trois conditions |
| SC2 — une cellule affamée ne croît pas | même micro-monde en pénurie, population et fraction en attente immobiles | naissances ralenties au lieu d'être fermées, condition évaluée sur le stock au lieu de la pénurie |
| SC3 — pas de stérilité par arrondi | cellule dont le produit est inférieur à 1, borne dérivée ; rouge d'une natalité sans report | absence de report, borne recopiée, échantillon choisi après mesure |
| SC4 — sentinelle | `-1.0` hors amorçage, `0.0` sur monde amorcé, les deux distingués | zéro traité comme « non calculé », sentinelle traitée comme une mesure |
| SC5 — le plafond tient | contrôle de plafond existant vert sans modification, plus une vérification à horizon cinq fois plus long, plafond dérivé du moteur | test de survie retouché, plafond écrit en dur, dépassement repoussé au lieu d'être borné |
| SC6 — la démographie répond | trois fractions de survie strictement ordonnées, constante remplacée par le module | constante lue par valeur, maillon aveugle à sa propre constante, ordre non strict |
| SC7 — une région prospère | cellules en croissance après, strictement plus nombreuses que la mesure de base archivée avant édition | mesure de base recopiée du brief, comparaison contre une copie fabriquée après coup |
| SC8 — invariants | suite verte, couverture d'écriture verte, gardes de constantes vertes, motif 033 (aucun nom `NAISSANCES_*` dans `engine.py`), sortie CLI déterministe et modifiée, aucun `global` | champ sans lecteur, littéral numérique dans une fonction, constante nommée dans `engine.py`, nondéterminisme, seconde formule |
| périmètre | diff limité à `engine.py`, `constants.py`, l'ajout du champ dans `model.py`, son amorçage et sa sérialisation dans `world.py`, et des ajouts dans `test_survie.py` | modification de `snapshot_export.py`, `aggregation.py`, des autres fichiers de test, de la carte, du visualiseur ou de l'outil de carte |
| unicité | un seul site d'augmentation de `population` dans tout `sim/` | deuxième endroit qui fait croître la population |
| compteurs | manifeste et mesureur reconstruisent numérateurs et dénominateurs depuis les données et les exécutions | nombre écrit à la main, dénominateur fixe, sentinelle prise pour une mesure |
| indépendance | compte-rendu final produit hors de l'invocation qui a écrit le code, et accepté par la porte mécanique | exécutant qui prononce sa propre recevabilité ou écrit le verdict |
