# Crons Hermes

Contrat des tâches planifiées (ADR-0015 amendement 001, ADR-0016).

## Ce qui est autorisé

Un cron **quotidien**, en lecture / mesure / proposition :

1. lire l’état git, `ROADMAP.md`, l’âge de `hermes/DASHBOARD.md` ;
2. exécuter `python -m sim` (la simulation sans Unity) ;
3. exécuter les tests `sim/` ;
4. écrire `hermes/propositions/DERNIERE-VEILLE.md` ;
5. n’ouvrir une `PROPOSITION-*.md` que s’il y a un constat **nouveau**.

## Ce qui est interdit

- `git push`, `gh pr merge`, toute fusion ;
- écrire hors de `hermes/propositions/` et du log local ;
- lancer ForgePilot `--run` ;
- rédiger un brief, un verdict, du code produit ;
- réactiver `mode: full_auto`.

## Installer (VPS, crontab de l’utilisateur hermes)

Une fois par jour, hors heures de pointe Claude si possible :

```cron
15 6 * * * /home/ubuntu/src/ForgeHistory/hermes/crons/quotidien.sh >> /home/ubuntu/.hermes/cron-quotidien.log 2>&1
```

Ajuster le chemin. Le script refuse de tourner si le dépôt n’est pas
celui de ForgeHistory (présence de `sim/__main__.py` et `hermes/crons/`).
