# AGENTS.md — règles communes de ForgeHistory

## Le projet

ForgeHistory est un moteur de simulation historique vivant couvrant la période
1400-1900. Le produit principal est `sim/`, lancé avec `python3 -m sim`. Il lit
la carte figée `data/world-1400.json`. Le dossier `viewer/` présente un
snapshot sans réimplémenter la simulation.

## Langue

Toute communication avec le propriétaire et tout écrit du dépôt sont en
français clair. Les termes techniques nécessaires sont expliqués à leur
première utilisation.

## Principes durables

1. **Une seule source de vérité.** Le monde suit la hiérarchie Monde → Pays →
   Province → Ville → Quartier → Bâtiment → Famille → Personne. Les vues lisent
   cette hiérarchie ; elles ne créent pas une base parallèle. `cell_id` reste
   la clé spatiale unique et les agrégations sont dérivées.
2. **Le moteur raisonne en termes de monde.** Une conséquence de jeu découle
   de causes simulées ; elle n'est pas ajoutée comme bonus arbitraire.
3. **L'économie est physique.** Toute ressource a une origine, un transport,
   un stockage et une destination. Rien ne se téléporte.
4. **Vraisemblable, pas véridique.** Les grandes lignes géographiques et
   historiques sont justes ; les valeurs locales peuvent être plausibles et
   générées ; ce qui exige une source précise pour exister n'est pas simulé.
5. **Les constantes sont nommées.** Une constante du moteur reçoit un nom et
   un commentaire d'ordre de grandeur ; aucun nombre magique n'est enfoui dans
   une règle du monde.
6. **Les tests protègent le produit.** Un test couvre un invariant physique,
   une règle de jeu visible, le déterminisme ou une non-régression concrète.
   Un nouveau cas rejoint le fichier qui porte déjà l'invariant concerné.
7. **Les contrôles dérivent leurs références.** Un échantillon vide échoue et
   la sentinelle `-1`, jamais zéro, signifie « non calculé ».

## Contribution

Toute personne, tout agent et tout outil autorisé à intervenir sur le dépôt
peut lire et modifier n'importe quel fichier, planifier, coder, tester,
documenter, relire et corriger. Aucun rôle, acteur, modèle ou fournisseur ne
possède une action ou un document en exclusivité. Une même personne peut
réaliser plusieurs ou toutes les étapes d'un changement.

Les outils sous `hermes/`, `harness/` et `control-plane/` sont facultatifs. Ils
peuvent aider à mesurer, vérifier ou automatiser un travail, mais ils ne sont
ni une autorité ni un passage obligé. La CI GitHub exécute les tests et les
contrôles de sécurité ; elle ne distribue pas les rôles.

Chaque intervention préserve le code et les comportements existants hors du
périmètre demandé. Avant de livrer, le contributeur examine le diff, lance les
tests pertinents et met à jour la documentation factuelle nécessaire. Les
changements locaux déjà présents et sans rapport avec la tâche sont conservés.

Workflow courant : choisir une tâche dans `ROADMAP.md` ou un brief existant,
modifier les fichiers nécessaires, lancer les tests pertinents, mettre à jour
la documentation factuelle, puis ouvrir une PR ou livrer le changement selon
le contexte.

## Carte du dépôt

| chemin | contenu |
|---|---|
| `sim/` | moteur vivant et tests métier |
| `data/` | carte figée et centres de province |
| `viewer/` | vue mince sur les snapshots |
| `tools/map/` | fabrication facultative de la carte |
| `harness/` | briefs, livrables et vérificateurs facultatifs |
| `hermes/` | rapports, propositions, mesures et tableau de bord |
| `control-plane/` | automatisation facultative ForgePilot |
| `docs/` | mode d'emploi, opérations et décisions historiques |
| `.github/workflows/` | tests, sécurité et tâche manuelle du worker PC |

Les anciens ADR, briefs, demandes et rapports conservent l'historique du
projet. Leurs clauses de rôle ou de procédure sont historiques et ne
constituent plus des règles actives. Les dossiers Unity et architecture
retirés de l'arbre restent consultables au commit `da1596d`.

## Commandes utiles

```bash
python3 -m sim
python3 -m sim --ticks 0 --json
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m pytest viewer/tests/ -q
.venv/bin/python -m pytest harness/tests/ -q
cd control-plane && ../.venv/bin/python -m unittest discover -s tests
python3 tools/map/build_world.py
python3 hermes/dashboard.py
```

Sous Windows, utiliser `py` à la place de `python3`. Sur Linux, employer
`python3` ou l'environnement virtuel du dépôt ; le Python système ne reçoit
pas d'installation `pip --user`.
