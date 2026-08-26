# Grille d'évaluation — Brief 043

**Authored**: 2026-08-26T10:30:00Z
**Author**: Claude

Cette grille est écrite avant tout livrable. Elle ne remplace pas le brief et
n'ajoute aucune instruction d'exécution.

| condition | preuve attendue | échec si |
|---|---|---|
| dépendance | lot 040 fusionné au démarrage | lot lancé avant 040 |
| SC1 — la capacité suit la longueur | trois arêtes dérivées de la carte (courte, médiane, longue), transferts dans le rapport des longueurs ; rouge cité | longueurs choisies à la main, transferts bornés par le besoin sans que le contrôle le dise, rouge non prouvé |
| SC2 — la constante plate a disparu | parcours des modules de `sim/` hors tests, nombre dérivé | constante conservée à côté de la capacité dérivée, deux chemins de décision |
| SC3 — frontière ponctuelle | transfert nul sur une arête de longueur nulle, mesuré | zéro rapporté sans mesure, longueur nulle traitée comme invalide |
| SC4 — le commerce cesse d'être décoratif | `kg_transportes` strictement supérieur à la base rejouée, d'au moins le rapport dérivé avant exécution | rapport fixé après avoir vu la mesure, nombre de base recopié |
| SC5 — une cellule peut être nourrie | cellule sans production maintenue par ses voisines ; dépérissement montré à capacité plate remplacée en mémoire | constante lue par valeur, cas non construit, survie obtenue par autre chose que le commerce |
| SC6 — conservation | somme des stocks identique avant et après le maillon ; contrôle existant inchangé | kilogrammes créés par la capacité élargie |
| SC7 — le plafond tient | trois propriétés de survie vertes à la substitution de nom près, plafond dérivé du moteur | plafond dépassé, test de survie recalibré |
| SC8 — refus de l'invalide | longueur retirée ou corrompue ; erreur portant les deux `cell_id` | repli sur une longueur par défaut |
| SC9 — invariants | suite verte ; contrôles de commerce verts ; gardes de constantes vertes ; sortie déterministe ; aucun `global` ; un seul maillon ; tests collectés non réduits | contrôle supprimé, littéral numérique dans une fonction, second maillon commerce |
| composition | facteur de relief du lot 040, s'il est présent, multiplié et non remplacé | règle de relief écrasée par la nouvelle capacité |
| substitution | règle appliquée ligne à ligne au diff des tests, compte de violations nul ; toute valeur attendue tirée de la même expression que le moteur ; `_MondeEpreuve` reçoit `shared_length_m` sans changement d'assertion | valeur attendue recopiée après mesure, seuil ou nom de test modifié, fixture recalibré |
| périmètre | diff limité à `engine.py`, `constants.py`, des ajouts dans `test_commerce.py`, la substitution de nom, et l'ajout de `shared_length_m` au fixture de `test_write_coverage.py` | modification de `world.py`, `model.py`, `snapshot_export.py`, `__main__.py`, `aggregation.py`, `test_monde.py`, d'une assertion de `test_write_coverage.py`, de la carte ou du visualiseur |
| compteurs | manifeste et mesureur reconstruisent numérateurs et dénominateurs depuis les données et les exécutions | nombre écrit à la main, dénominateur fixe, sentinelle prise pour une mesure |
| indépendance | compte-rendu final produit hors de l'invocation qui a écrit le code, et accepté par la porte mécanique | exécutant qui prononce sa propre recevabilité ou écrit le verdict |
