# Grille d'évaluation — Brief 035

**Authored**: 2026-08-26T09:10:00Z
**Author**: Claude

Cette grille est écrite avant tout livrable. Elle ne remplace pas le brief et
n'ajoute aucune instruction d'exécution.

| condition | preuve attendue | échec si |
|---|---|---|
| dépendance | `sim/engine.py` sans instruction `global` au démarrage du lot | lot lancé avant la fusion du 034 |
| SC1 — le rendement dépend de la date | été et hiver comparés sur la cellule de plus grande amplitude, dérivée de la carte ; rouge cité sur le SHA de base | échantillon vide, cellule nommée en dur, rouge non prouvé |
| SC2 — nord contre sud | rapport été/hiver strictement plus grand pour la grande amplitude que pour la petite, deux cellules dérivées | `cell_id` écrit en dur, critère obtenu en retouchant une constante après mesure |
| SC3 — couche consommée | sortie de la sonde existante : relief et climat vrais, gisements faux | booléen retourné à la main, `snapshot_export.py` modifié, formule invariante à la sonde |
| SC4 — plafond dérivé | moyenne annuelle du facteur calculée, et coïncidant avec celle employée par le plafond ; `test_survie.py` inchangé | moyenne supposée égale à 1, plafond découplé de la formule du tick, test de survie retouché |
| SC5 — rien ne se crée sur l'année | somme annuelle saisonnière égale à la somme au facteur moyen, nombre de jours dérivé | tolérance élargie après mesure, nombre de jours écrit en dur |
| SC6 — refus de l'incomplet | climat retiré ou corrompu en mémoire ; erreur portant le `cell_id` et la clé | repli silencieux, facteur neutre implicite |
| SC7 — effet visible déterministe | deux sorties à 365 ticks identiques entre elles, différentes de la base rejouée | nondéterminisme, sortie inchangée, nombre de base recopié |
| SC8 — invariants | suite `sim/tests` verte ; gardes de constantes et de littéraux vertes ; aucun `global` réapparu | littéral numérique dans une fonction, constante terminale, import par valeur, seconde formule |
| périmètre | diff limité à `engine.py`, `constants.py`, la transmission du numéro de tick dans `__main__.py`, et des ajouts dans `test_monde.py` | modification de `world.py`, `model.py`, `snapshot_export.py`, `aggregation.py`, `test_survie.py`, de la carte, du visualiseur ou de l'outil de carte |
| compteurs | manifeste et mesureur reconstruisent numérateurs et dénominateurs depuis les données et les exécutions | nombre écrit à la main, dénominateur fixe, sentinelle prise pour une mesure, écart avant non nul |
| indépendance | compte-rendu final produit hors de l'invocation qui a écrit le code, et accepté par la porte mécanique | exécutant qui prononce sa propre recevabilité ou écrit le verdict |
