# Grille d'évaluation — Brief 033

**Authored**: 2026-08-25T18:12:50Z  
**Author**: Hermes  

Cette grille est écrite avant tout livrable. Elle ne remplace pas le brief et
n'ajoute aucune instruction d'exécution.

| condition | preuve attendue | échec si |
|---|---|---|
| SC1 — cinq classes, cinq facteurs effectifs | mesure rejouable sur les classes dérivées de la carte, à surface et rendement identiques ; preuve rouge avant changement | échantillon vide, classe absente, facteur sans effet ou cible recopiée au lieu d'être mesurée |
| SC2 — lecture de la carte et refus de l'inconnu | mutation en mémoire d'une classe ; erreur portant le `cell_id` et la valeur inconnue | repli silencieux, facteur neutre implicite dans le chemin réel du tick, lecture d'une autre source |
| SC3 — consommation dérivée | sortie de la sonde existante : relief vrai, climat et gisements faux | booléen retourné à la main, modification de `snapshot_export.py`, ou autre couche activée |
| SC4 — effet visible déterministe | deux sorties CLI à 20 ticks/graine 0 identiques entre elles, différentes d’une sortie de base rejouée ; cellules affamées strictement supérieures à cette mesure de base dérivée | nondéterminisme, sortie inchangée, nombre de base recopié ou effet trop faible selon le critère fixé avant exécution |
| SC5 — invariants préservés | suite `sim/tests` verte ; tests de survie inchangés ; gardes de constantes vertes | test existant calibré, formule dupliquée, constante terminale ou import par valeur |
| périmètre | diff limité aux trois fichiers produit et aux livrables autorisés | toute modification de `world.py`, `model.py`, `snapshot_export.py`, `test_survie.py`, de la carte figée, du visualiseur ou de l'outil de fabrication de la carte |
| compteurs | manifeste et mesureur reconstruisent numérateurs et dénominateurs depuis les données et exécutions | nombre écrit à la main, dénominateur fixe non dérivé, sentinelle utilisée comme mesure |
| indépendance | compte-rendu final produit hors de l'invocation qui a écrit le code et accepté par la porte mécanique | exécutant qui prononce sa propre recevabilité ou écrit le verdict |
