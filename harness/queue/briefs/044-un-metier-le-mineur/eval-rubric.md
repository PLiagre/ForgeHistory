# Grille d'évaluation — Brief 044

**Authored**: 2026-08-26T10:40:00Z
**Author**: Claude

Cette grille est écrite avant tout livrable. Elle ne remplace pas le brief et
n'ajoute aucune instruction d'exécution.

| condition | preuve attendue | échec si |
|---|---|---|
| dépendance | extraction en place au démarrage du lot | lot lancé avant la fusion du 038 |
| SC1 — la cellule à gisement cultive moins | paires porteuse/témoin de même relief dérivées de la carte ; rouge cité sur le SHA de base | paire fabriquée à la main, aucune paire trouvée et contrôle sauté, rouge non prouvé |
| SC2 — la richesse ordonne | parts minières strictement ordonnées sur les classes dérivées | classe absente sautée, ordre non strict |
| SC3 — le plafond tient | part minière exactement au plafond sur une cellule surchargée, nombre de gisements dérivé | plafond dépassé, cellule qui cesse de produire, nombre écrit en dur |
| SC4 — une seule définition | une fonction de part minière, une formule de production agricole | calcul dupliqué entre production et extraction |
| SC5 — l'extraction suit les mineurs | proportionnalité à la part minière vérifiée sur deux cellules de parts différentes ; zéro mesuré à part nulle | débit resté proportionnel à la population entière, zéro rapporté sans mesure |
| SC6 — dépendance mesurée | nourriture reçue par les cellules minières strictement supérieure à la base rejouée et archivée avant édition | nombre de base recopié, effet raconté et non mesuré |
| SC7 — le plafond de survie | trois propriétés vertes sans modification ; plafond descendu tout seul ; fraction positive à cinq fois l'horizon | test de survie retouché, plafond découplé de la formule du tick |
| SC8 — invariants | suite verte ; gardes de constantes vertes ; facteurs de richesse non dupliqués ; sortie déterministe et modifiée ; aucun `global` | second jeu de facteurs, littéral numérique dans une fonction, nondéterminisme |
| périmètre | diff limité à `engine.py`, `constants.py` et des ajouts dans `test_monde.py` | modification de `world.py`, `model.py`, `snapshot_export.py`, `__main__.py`, `aggregation.py`, `test_survie.py`, `test_commerce.py`, de la carte ou du visualiseur |
| compteurs | manifeste et mesureur reconstruisent numérateurs et dénominateurs depuis les données et les exécutions | nombre écrit à la main, dénominateur fixe, sentinelle prise pour une mesure, unicités non vérifiées |
| indépendance | compte-rendu final produit hors de l'invocation qui a écrit le code, et accepté par la porte mécanique | exécutant qui prononce sa propre recevabilité ou écrit le verdict |
