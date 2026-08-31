# MODE D'EMPLOI — faire avancer ForgeHistory

Les règles communes sont dans [AGENTS.md](../AGENTS.md), l'état du produit
dans [ROADMAP.md](../ROADMAP.md) et le fonctionnement du monde dans
[sim/MODELE.md](../sim/MODELE.md).

## Workflow minimal

1. Choisir une tâche dans `ROADMAP.md` ou dans un brief existant sous
   `harness/queue/briefs/`.
2. Examiner l'état Git et les fichiers concernés.
3. Modifier directement tous les fichiers nécessaires.
4. Lancer les tests proportionnés au changement.
5. Mettre à jour la documentation factuelle si elle a changé.
6. Examiner le diff, puis ouvrir une PR ou livrer le changement selon le
   contexte.

N'importe quel contributeur ou agent autorisé peut réaliser toutes ces
étapes, y compris la planification, l'implémentation et la relecture. Aucun
acteur, modèle ou fournisseur n'est imposé ou interdit.

Un brief existant décrit un lot et peut être suivi, amendé ou utilisé comme
archive. La roadmap peut aussi suffire pour choisir une tâche. En cas de
contradiction avec les règles courantes, les anciennes clauses de rôle ou de
procédure d'un brief, d'un ADR ou d'un rapport sont historiques.

## Vérifications usuelles

```bash
python3 -m sim --ticks 0 --json
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m pytest viewer/tests/ -q
.venv/bin/python -m pytest harness/tests/ -q
cd control-plane && ../.venv/bin/python -m unittest discover -s tests
```

Choisir les suites pertinentes, puis élargir en fonction du risque. Ne jamais
supprimer ou affaiblir un test métier pour obtenir du vert. Sous Windows,
remplacer `python3` par `py`.

## Outils facultatifs

- `python3 hermes/dashboard.py` régénère une vue factuelle du projet.
- `python3 harness/verdict_audit.py <dossier>` vérifie la cohérence interne de
  livrables historiques ; son résultat est informatif.
- `.venv/bin/forgepilot` automatise, si on le souhaite, certaines étapes de
  préparation, d'exécution ou de suivi. Il n'est pas requis pour modifier le
  dépôt, ouvrir une PR ou livrer un changement.
- `hermes/crons/quotidien.sh` lance manuellement une veille locale. Son
  installation comme tâche planifiée est un choix d'exploitation.
- `.github/workflows/worker-pc.yml` est une tâche manuelle de diagnostic du
  PC Windows ; le développement courant n'en dépend pas.

Ces outils n'attribuent aucun rôle et ne prononcent pas la recevabilité d'un
changement. Le contributeur reste libre d'utiliser les moyens adaptés au
contexte.
