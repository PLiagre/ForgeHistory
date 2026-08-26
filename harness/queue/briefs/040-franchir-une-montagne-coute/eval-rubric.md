# Grille d'évaluation — Brief 040

**Authored**: 2026-08-26T10:00:00Z
**Author**: Claude

Cette grille est écrite avant tout livrable. Elle ne remplace pas le brief et
n'ajoute aucune instruction d'exécution.

| condition | preuve attendue | échec si |
|---|---|---|
| SC1 — cinq facteurs effectifs | micro-monde, cinq arêtes de classes dérivées de la carte, transferts strictement ordonnés ; rouge cité sur le SHA de base | classe absente sautée au lieu de faire échouer, rouge non prouvé, ordre non strict |
| SC2 — le goulot commande | arête mixte égale à l'arête du bout difficile, et inférieure à l'arête facile ; les deux sens essayés | moyenne au lieu du minimum, capacité dépendant du sens de lecture |
| SC3 — effet visible déterministe | deux sorties à 365 ticks identiques ; `kg_transportes` strictement inférieur à la base rejouée et archivée avant édition | nondéterminisme, effet nul, nombre de base recopié, facteur ajusté après mesure |
| SC4 — conservation | somme des stocks identique avant et après le maillon ; contrôle existant inchangé et vert | kilogrammes perdus avec la capacité, contrôle retouché |
| SC5 — le monde ne meurt pas | trois propriétés de survie vertes sans modification ; fraction strictement positive à cinq fois l'horizon | test de survie retouché, monde éteint, effet qui s'aggrave sans se stabiliser |
| SC6 — refus de l'inconnu | relief muté en mémoire ; erreur portant les deux `cell_id` et la valeur | repli silencieux, capacité neutre implicite |
| SC7 — invariants | suite verte ; quatre contrôles de commerce inchangés ; gardes de constantes vertes ; aucun `global` ; un seul maillon commerce | littéral numérique dans une fonction, constante terminale, import par valeur, second maillon |
| périmètre | diff limité à `engine.py`, `constants.py` et des ajouts dans `test_commerce.py` | modification de `world.py`, `model.py`, `snapshot_export.py`, `__main__.py`, `aggregation.py`, des autres tests, de la carte ou du visualiseur |
| échelles distinctes | facteurs de transport nommés séparément des facteurs de production | réutilisation des facteurs de production comme facteurs de transport |
| compteurs | manifeste et mesureur reconstruisent numérateurs et dénominateurs depuis les données et les exécutions | nombre écrit à la main, dénominateur fixe, sentinelle prise pour une mesure |
| indépendance | compte-rendu final produit hors de l'invocation qui a écrit le code, et accepté par la porte mécanique | exécutant qui prononce sa propre recevabilité ou écrit le verdict |
