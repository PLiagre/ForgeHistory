# Grille d'évaluation — Brief 045

**Authored**: 2026-08-29T18:26:23Z
**Author**: Codex, sur demande directe du propriétaire

Cette grille est écrite avant tout livrable. Elle ne remplace pas le brief et
n'ajoute aucune instruction d'exécution.

| condition | preuve attendue | échec si |
|---|---|---|
| dépendance | lot 041 présent ; SHA de base dérivé ; rouge SC1 rejoué avant édition | base choisie après correction, rouge raconté sans commande, objectif adapté parce que le défaut est déjà absent |
| SC1 | destination avec reste positif inférieur à une ration ; mouvement non nul et conservé | une seconde ration est encore retranchée, destination dotée de plusieurs rations qui masquerait le défaut |
| SC2 | stock nul et sentinelle négative refusés par des appels réellement joués | zéro non mesuré, sentinelle prise pour un surplus |
| SC3 | mêmes stocks, populations distinctes, mêmes poids | poids encore dépendant de la ration ou de la population de destination |
| SC4 | deux stocks positifs distincts, répartition dérivée et observable | résultat attendu copié de l'implémentation ou contrôle où l'arrondi masque le rapport |
| SC5 | population conservée ; paniers strictement inchangés ; atomicité et ordre des arêtes | habitant ou kilogramme créé, détruit ou déplacé deux fois |
| SC6 | suite `sim` verte ; tests de migration existants inchangés ; survie et couverture d'écriture inchangées ; CLI déterministe | test existant retouché, collecte réduite, nondéterminisme ou `global` |
| périmètre | diff produit limité à `sim/engine.py` et à des ajouts dans `sim/tests/test_commerce.py` | ancien brief ou livrable retouché, modification de constante, modèle, monde, survie, couverture, carte, viewer, harness ou ForgePilot |
| mesureur | références de base exécutées hors de l'arbre produit courant ; compteurs et dénominateurs reconstruits | `engine.py` remplacé temporairement dans le dépôt, échantillon vide, valeur recopiée |
| indépendance | verdict produit par une invocation neuve, sur le SHA candidat | exécutant qui écrit le verdict ou prononce sa propre recevabilité |
