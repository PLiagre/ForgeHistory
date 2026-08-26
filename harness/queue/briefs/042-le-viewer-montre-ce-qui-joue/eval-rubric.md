# Grille d'évaluation — Brief 042

**Authored**: 2026-08-26T10:20:00Z
**Author**: Claude

Cette grille est écrite avant tout livrable. Elle ne remplace pas le brief et
n'ajoute aucune instruction d'exécution.

| condition | preuve attendue | échec si |
|---|---|---|
| dépendance | panier et extraction présents au démarrage du lot ; partie saison retirée si le moteur ignore le jour de l'année | lot lancé avant 037 ou 038, saison approximée au lieu d'être retirée |
| SC1 — le panier dans le document | chaque cellule porte son panier, ancien champ absent, dénombrement dérivé ; rouge cité | document vide accepté, ancien champ conservé « pour compatibilité », rouge non prouvé |
| SC2 — fidélité de la photographie | comparaison cellule par cellule et marchandise par marchandise avec le monde source | conversion, arrondi supplémentaire, valeur recalculée côté export |
| SC3 — jour de l'année | présent avec la vraie valeur, ou clé absente ; les deux cas montés | date inventée, tiret muet, valeur par défaut |
| SC4 — couches dérivées | liste des couches dérivée du document ; aucun nom de marchandise en dur dans `viewer/` | liste écrite dans le code, couche minière proposée sur un document qui n'en a pas |
| SC5 — la présentation ne décide pas | aucune constante du moteur trouvée dans `viewer/`, parcours dérivé du répertoire | facteur, ration, capacité ou formule réimplémentés côté vue |
| SC6 — trois états visuels | absent, zéro et non calculé rendus distinctement sur une marchandise | deux états confondus, absence affichée comme zéro |
| SC7 — refus d'un schéma inconnu | refus des deux côtés, message nommant la version reçue et attendue | affichage « au mieux », refus silencieux |
| SC8 — preuve SVG déterministe | deux rendus byte-identiques par couche, nombre de couches dérivé | rendu variable, une seule couche essayée |
| SC9 — invariants | suites `sim/` et `viewer/` vertes ; tests collectés non réduits ; sortie CLI du jeu byte-identique | contrôle supprimé, jeu modifié, assertion retouchée |
| substitution | règle appliquée ligne à ligne au diff des tests, compte de violations nul | valeur attendue, seuil ou nom de test modifié |
| périmètre | diff limité à `snapshot_export.py`, la version dans `constants.py`, le paquet `viewer/` et la substitution dans les deux fichiers de test | modification de `engine.py`, `world.py`, `model.py`, `aggregation.py`, `__main__.py`, de la carte ou de l'outil de carte |
| compteurs | manifeste et mesureur reconstruisent numérateurs et dénominateurs depuis les données et les exécutions | nombre écrit à la main, dénominateur fixe, sentinelle prise pour une mesure, égalités attendues non vérifiées |
| indépendance | compte-rendu final produit hors de l'invocation qui a écrit le code, et accepté par la porte mécanique | exécutant qui prononce sa propre recevabilité ou écrit le verdict |
