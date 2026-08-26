# Grille d'évaluation — Brief 034

**Authored**: 2026-08-26T09:00:00Z
**Author**: Claude

Cette grille est écrite avant tout livrable. Elle ne remplace pas le brief et
n'ajoute aucune instruction d'exécution.

| condition | preuve attendue | échec si |
|---|---|---|
| SC1 — plus d'état global | contrôle sur l'arbre syntaxique du moteur, nombre de fonctions dérivé du module ; sortie rouge citée sur le SHA de base | contrôle qui ne peut pas rougir, module vide accepté, rouge non prouvé |
| SC2 — la carte par la signature | deux appels identiques rendent le même résultat sans mise en place hors de l'appel | dépendance résiduelle à un état de module, deuxième formule de production |
| SC3 — le monde ne bouge pas | sortie CLI d'après byte-identique à la sortie de base rejouée, 20 ticks graine 0 et 200 ticks graine 42 | un champ qui diffère, comparaison contre une copie fabriquée après coup, nombre recopié du brief |
| SC4 — chemin unitaire | production sur une `Cell` hors `World`, sans carte et sans erreur ; `test_survie.py` inchangé | exigence de carte imposée au chemin unitaire, test de survie retouché |
| SC5 — refus de l'inconnu | les deux contrôles de refus verts, sans une ligne changée | repli silencieux, erreur sans `cell_id` ou sans valeur fautive |
| SC6 — sonde des couches | relief vrai, climat et gisements faux, rendus par la sonde existante | déclaration manuelle, `snapshot_export.py` modifié, couche activée par effet de bord |
| SC7 — invariants | suite `sim/tests` verte ; gardes de constantes vertes | test existant calibré, constante ajoutée, formule dupliquée |
| périmètre | diff limité à `sim/engine.py`, aux deux lignes de mise en place et à l'ajout du contrôle SC1 dans `sim/tests/test_monde.py`, plus les livrables | toute autre modification de test, de `constants.py`, `world.py`, `model.py`, `snapshot_export.py`, `__main__.py`, de la carte, du visualiseur ou de l'outil de carte |
| assertions | diff du fichier de test sans aucune ligne `assert` modifiée | une assertion retouchée, même « équivalente » |
| compteurs | manifeste et mesureur reconstruisent numérateurs et dénominateurs depuis les données et les exécutions | nombre écrit à la main, dénominateur fixe, sentinelle prise pour une mesure, `fonctions_avec_global_avant` nul |
| indépendance | compte-rendu final produit hors de l'invocation qui a écrit le code, et accepté par la porte mécanique | exécutant qui prononce sa propre recevabilité ou écrit le verdict |
