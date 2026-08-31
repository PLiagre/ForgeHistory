# Veille locale facultative

`quotidien.sh` lance `veille.py`, qui mesure l'état Git, l'espace disque, le
déterminisme et les tests de `sim/`. Le rapport local est écrit sous
`hermes/propositions/DERNIERE-VEILLE.md`, chemin ignoré par Git.

```bash
hermes/crons/quotidien.sh
.venv/bin/python hermes/crons/veille.py --repo . --metrics-only --json
```

La commande peut être lancée à la main. Son installation dans `cron` ou dans
un autre planificateur est facultative et relève de l'exploitation locale.
Elle ne fusionne, ne pousse et ne décide rien. Une alerte est un diagnostic,
pas une condition de recevabilité.

Exemple d'installation, seulement si elle est souhaitée :

```cron
15 6 * * * /chemin/vers/ForgeHistory/hermes/crons/quotidien.sh
```
