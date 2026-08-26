# Grille d'évaluation — Brief 038

**Authored**: 2026-08-26T09:40:00Z
**Author**: Claude

Cette grille est écrite avant tout livrable. Elle ne remplace pas le brief et
n'ajoute aucune instruction d'exécution.

| condition | preuve attendue | échec si |
|---|---|---|
| dépendance | panier sur `Cell` **et** panier dans `World.to_dict()` au démarrage du lot | lot lancé avant un 037 qui gèle `to_dict`, champ de cas particulier par ressource, SC4 infaisable |
| SC1 — chaque gisement produit | cellules extractrices égales aux cellules porteuses, ressources extraites égales aux ressources déclarées, les deux dérivées de la carte ; rouge cité | échantillon vide, ensemble de ressources écrit dans le code, rouge non prouvé |
| SC2 — la richesse ordonne | trois débits strictement ordonnés, classes dérivées de la carte | classe absente sautée au lieu de faire échouer, ordre non strict |
| SC3 — sans bras, pas de minerai | extraction nulle à population nulle, mesurée ; décroissance suivie sur le monde réel | zéro rapporté sans mesure, débit indépendant de la population |
| SC4 — couche consommée | sortie de la sonde existante : gisements vrai, via le panier dans `to_dict` | booléen retourné à la main, `snapshot_export.py` modifié, `to_dict` sans panier |
| SC5 — le minerai n'est pas de la nourriture | stock de nourriture identique cellule par cellule à la référence rejouée sur le SHA de base | une seule cellule dont la nourriture change, minerai compté dans la consommation ou dans le remboursement de dette |
| SC6 — invalide et incomplet | richesse **présente** hors vocabulaire refusée ; gisement sans `ressource`/`richesse` ignoré sans erreur ; ressource inconnue acceptée | repli silencieux sur une richesse, levée sur l'enregistrement-sonde, refus d'une ressource nouvelle |
| SC7 — invariants | suite verte ; `test_survie.py` inchangé et vert ; gardes de constantes vertes ; motif 033 (aucun nom de constante d'extraction dans `engine.py`) ; sortie CLI déterministe ; aucun `global` ; formule agricole inchangée | test de survie retouché, littéral numérique dans une fonction, seconde formule de production, constante nommée dans `engine.py` |
| périmètre | diff limité à `engine.py`, `constants.py` et des ajouts dans `test_monde.py` | modification de `world.py`, `model.py`, `snapshot_export.py`, `aggregation.py`, `test_survie.py`, de la carte, du visualiseur ou de l'outil de carte |
| compteurs | manifeste et mesureur reconstruisent numérateurs et dénominateurs depuis les données et les exécutions | nombre écrit à la main, dénominateur fixe, sentinelle prise pour une mesure, égalités attendues non vérifiées |
| indépendance | compte-rendu final produit hors de l'invocation qui a écrit le code, et accepté par la porte mécanique | exécutant qui prononce sa propre recevabilité ou écrit le verdict |
