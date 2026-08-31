# Grille d'évaluation — Brief 046

**Authored**: 2026-08-30T13:20:00Z
**Author**: Claude

Cette grille est écrite avant tout livrable. Elle ne remplace pas le brief et
n'ajoute aucune instruction d'exécution.

| condition | preuve attendue | échec si |
|---|---|---|
| dépendance | lot 043 fusionné au démarrage ; aucun ordre imposé vis-à-vis de 044 et 047 | lot lancé avant 043, ou séquencé derrière 044 ou 047 |
| SC1 — les arêtes maritimes sont vues | compte du moteur égal au compte relu indépendamment du fichier, dénominateur = arêtes totales ; rouge cité | définition par littéral de `kind`, compte recopié, échantillon vide qui passe |
| SC1 bis — définition structurelle | aucun nom de `kind` comme littéral de comparaison dans `engine.py` | vocabulaire de la carte figé dans le moteur |
| SC2 — on expédie et on débarque | micro-monde sans arête terrestre ; stock source en baisse, bassin en hausse d'autant, puis l'inverse chez le receveur ; rouge cité | survie du receveur obtenue par autre chose que le bassin, arête terrestre laissée dans le micro-monde |
| SC3 — le délai d'une traversée | receveur à zéro au tick d'embarquement, positif au tick suivant ; nombre de ticks dérivé | délai obtenu par un compteur ad hoc au lieu de la lecture du bassin au début du tick ; zéro rapporté sans mesure |
| SC4 — conservation | somme des stocks **plus** le bassin identique avant/après le maillon, écart nul mesuré ; `test_conservation_masse_transport` vert sans modification | conservation vérifiée sur les seules cellules d'un monde qui a une mer ; kilogrammes créés par le double engagement d'un surplus |
| SC5 — les hermétiques s'ouvrent | ensemble dérivé de la carte (aucune arête terrestre, au moins une maritime) ; zéro partenaire sur la base, quai positif après ; façades nulles comptées à part | ensemble choisi à la main, échantillon vide qui passe, façade nulle traitée comme invalide |
| SC6 — la façade commande | trois façades dérivées de la carte (courte, médiane, longue), débarquements dans le rapport des longueurs, aucune bornée par le besoin ou le bassin sans que le contrôle le dise ; rouge cité | longueurs choisies à la main, bornes non déclarées, rouge non prouvé |
| SC7 — composition du relief | ordre strict suivant la table du lot 040, classes dérivées de la carte ; un seul jeu de facteurs de transport dans `sim/` | `min` appliqué à une arête qui n'a qu'une rive, second jeu de facteurs, classe manquante sautée au lieu de faire échouer |
| SC8 — refus de deviner | quatre mutations réellement exécutées, chacune avec sa commande et son message : longueur non numérique, longueur absente, plusieurs nœuds hors monde, plusieurs `kind` ; plus le cas non-erreur du monde sans bassin | repli silencieux sur `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK` pour un quai ; nœud mer choisi arbitrairement parmi plusieurs ; impossibilité affirmée sans être essayée |
| SC9 — le monde réel bouge | `kg_transportes` strictement supérieur à la base rejouée, via `must_differ_from_git` contre le SHA de base ; kilogrammes maritimes comptés dedans | seuil chiffré exigé, nombre de base recopié, copie `.orig` fabriquée après coup, maritime exclu du compteur |
| SC10 — la suite reste verte | suite jouée deux fois, avant et après ; liste des échecs vide dans les deux cas, comparée et non supposée | suite déclarée verte sans être jouée ; échec introduit ; `test_chaque_constante_du_moteur_change_le_monde` rendu vert en touchant le test ou `_MondeEpreuve` |
| motif des constantes | `DEBIT_KG_PAR_KM_DE_COTE_PAR_TICK` lu par `debit_maritime_kg_par_km()`, jamais comme attribut dans `engine.py` ; compteur nul | constante nommée dans le moteur : inerte sur un monde d'épreuve sans mer, elle rouvre le rouge que le micro-lot 043-bis vient de fermer |
| constante décidée d'avance | le facteur dix est justifié dans le brief et aucun critère n'en dépend | constante ajustée après avoir vu une mesure, seuil de succès adossé à sa valeur |
| fidélité | niveau 1 pour la façade lue dans la carte, niveau 2 pour le débit, limite du bassin sans distance déclarée dans le `generator-log.md` | niveau absent, limite du bassin présentée comme une propriété du monde |
| invariants | suite jouée ; contrôles de commerce et de survie verts sans modification ; sortie déterministe ; aucun `global` ; un seul maillon commerce ; tests collectés non réduits | contrôle supprimé ou relâché, littéral numérique dans une fonction, second maillon commerce, `to_dict()` modifié |
| périmètre | diff limité à `engine.py`, `constants.py`, la déclaration et les deux accès de `stocks_mer` dans `world.py`, et des ajouts dans `test_commerce.py` | modification de `model.py`, `aggregation.py`, `snapshot_export.py`, `__main__.py`, `to_dict()`, d'un autre fichier de test, de la carte, du visualiseur ou du brief 044 |
| compteurs | manifeste et mesureur reconstruisent numérateurs et dénominateurs depuis les données et les exécutions | nombre écrit à la main, dénominateur fixe, sentinelle prise pour une mesure, zéro mesuré traité comme « non calculé » |
| indépendance | compte-rendu final produit hors de l'invocation qui a écrit le code, et accepté par la porte mécanique | exécutant qui prononce sa propre recevabilité ou écrit le verdict |
