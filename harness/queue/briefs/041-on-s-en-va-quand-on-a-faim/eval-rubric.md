# Grille d'évaluation — Brief 041

**Authored**: 2026-08-26T10:10:00Z
**Author**: Claude

Cette grille est écrite avant tout livrable. Elle ne remplace pas le brief et
n'ajoute aucune instruction d'exécution.

| condition | preuve attendue | échec si |
|---|---|---|
| dépendance | `sim/engine.py` sans instruction `global` au démarrage du lot | lot lancé avant la fusion du 034 |
| SC1 — conservation des personnes | somme des populations identique avant et après le maillon ; rouge cité sur le SHA de base | un habitant créé ou perdu, écart absorbé par une tolérance, rouge non prouvé |
| SC2 — d'où vers où | micro-monde à trois cellules : l'affamée décroît, la voisine en surplus croît d'autant, le témoin ne bouge pas | départ vers une cellule sans surplus, témoin affecté, arrivée non égale au départ |
| SC3 — on ne part pas pour rien | zéro départ sans destination et zéro départ d'une cellule rassasiée, tous deux mesurés | zéro rapporté sans mesure, exil au hasard, condition évaluée sur le stock au lieu de la pénurie |
| SC4 — pas d'immobilité par arrondi | borne de ticks dérivée de la constante et de la population ; rouge d'une migration sans report | absence de report, borne recopiée, échantillon choisi après mesure |
| SC5 — atomicité et déterminisme | aucun renvoi le même tick ; deux ordres d'adjacence donnant le même état ; deux sorties CLI identiques | dépendance à l'ordre des arêtes, chaîne de sauts en un tick, nondéterminisme |
| SC6 — sentinelle | `-1.0` hors amorçage, `0.0` sur monde amorcé, les deux distingués | zéro traité comme « non calculé », sentinelle traitée comme une mesure |
| SC7 — les gens bougent | cellules déplacées strictement positives au taux nominal, nulles au taux zéro, constante remplacée par le module | constante lue par valeur, maillon aveugle à sa propre constante |
| SC8 — invariants | suite verte ; survie et commerce inchangés et verts ; couverture d'écriture verte ; aucun `global` | test retouché, champ sans lecteur, littéral numérique dans une fonction |
| chaîne | après mortalité, et après natalité si ce maillon existe | maillon intercalé avant la mortalité ; natalité exigée alors qu'elle n'existe pas |
| périmètre | diff limité à `engine.py`, `constants.py`, l'ajout du champ dans `model.py`, son amorçage et sa sérialisation dans `world.py`, et des ajouts dans `test_commerce.py` | modification de `snapshot_export.py`, `aggregation.py`, `__main__.py`, des autres tests, de la carte ou du visualiseur |
| simplification déclarée | le brief dit que les migrants ne portent rien, et le code s'y tient | kilogrammes déplacés en douce avec les gens |
| compteurs | manifeste et mesureur reconstruisent numérateurs et dénominateurs depuis les données et les exécutions | nombre écrit à la main, dénominateur fixe, sentinelle prise pour une mesure |
| indépendance | compte-rendu final produit hors de l'invocation qui a écrit le code, et accepté par la porte mécanique | exécutant qui prononce sa propre recevabilité ou écrit le verdict |
