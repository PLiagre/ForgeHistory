# Hébergement facultatif de ForgePilot

ForgePilot peut tourner sur une machine locale, dans WSL2 ou sur un VPS Linux.
Le projet n'en dépend pas : le dépôt peut être modifié, testé et livré sans ce
service.

## Installation type

```bash
python3 -m venv .venv
.venv/bin/pip install -e ./control-plane
.venv/bin/forgepilot doctor --repo .
```

Installer ensuite seulement les CLI réellement choisis pour les commandes à
automatiser. Le backend d'exemple est configurable et ne constitue pas une
préférence ni une interdiction à l'échelle du dépôt.

Pour un VPS léger, prévoir un clone Git, un environnement Python, assez
d'espace pour les worktrees et les authentifications des services utilisés.
Les secrets restent hors de Git. Un dashboard éventuel n'est exposé que sur
une interface privée ou derrière une authentification adaptée.

## Worker Windows

Le PC Windows peut rester un runner GitHub auto-hébergé pour les diagnostics
ou tâches qui exigent cette machine. Son absence ne bloque pas `sim/`,
`viewer/` ni le travail courant. Voir [pc-windows-worker.md](pc-windows-worker.md).

## Précautions

- ne jamais committer de secret ou de contenu de répertoire personnel ;
- ne pas exécuter automatiquement le code d'une PR non fiable sur un runner
  auto-hébergé ;
- mesurer l'espace disque avant de multiplier les worktrees ;
- arrêter ou remplacer librement cette installation si elle n'apporte plus
  d'utilité.

Les anciens choix de machine, de modèle et de répartition des rôles sont
conservés dans les ADR et rapports historiques. Ils ne s'appliquent plus.
