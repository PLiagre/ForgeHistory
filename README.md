# ForgeAtelier

Infrastructure d'agents pour exécuter des lots. **Pas un agent de plus.**

Le schéma est plus vaste que n'importe quel agent unique :

> intelligence → outils → mémoire → exécution → orchestration → coordination → vérification

On n'a pas collectionné les démos. On a retenu, dépôt par dépôt, ce
dont les agents ont besoin pour travailler de manière fiable — et
laissé le reste. L'analyse tient dans [ANALYSE.md](ANALYSE.md).
La vision gelée dans [VISION.md](VISION.md).

## Premier client

[ForgeHistory](https://github.com/PLiagre/ForgeHistory) se branche
avec un `atelier.toml` à sa racine. L'atelier ne connaît pas le
moteur de simulation. Il connaît un brief, un périmètre, des rôles.

## Commandes

```bash
python3 -m pytest tests/ -q

python3 -m atelier couches
python3 -m atelier doctor --projet /chemin/vers/ForgeHistory
python3 -m atelier portes --brief /chemin/vers/briefs/044-un-metier-le-mineur.md
python3 -m atelier start /chemin/vers/briefs/044-un-metier-le-mineur.md \
    --projet /chemin/vers/ForgeHistory
```

Sans `--run`, `start` n'écrit rien : il imprime qui ferait quoi.
Avec `--run`, il crée le worktree, pose le verrou, ouvre le canal
d'échange, enregistre l'état. Il n'invoque aucun agent.

```bash
python3 -m atelier fusionner    # sort toujours en erreur — c'est voulu
```

## Ce dépôt n'est pas

- Hermes, Superpowers, Goose, Qwen Code (intelligence / skills : on
  s'en sert, on ne les vend pas)
- Mem0 (la mémoire ici, c'est git)
- E2B (l'isolation ici, c'est le worktree)
- Mission Control (l'orchestration ici, c'est `start` / `status`)
- llmquota (le quota ici, c'est un fait, `-1` si inconnu)
- un conseil à chaque PR

Le moteur de jeu reste dans ForgeHistory. Si l'atelier redevient trop
gros, on le dégraisse ici, pas là-bas.
