# WORKFLOW — ce dépôt se branche, il n'orchestre plus

La marche à suivre vit dans **ForgeAtelier** : sept couches, un cycle,
aucune fusion. Branche orpheline en attendant le dépôt détaché :

https://github.com/PLiagre/ForgeHistory/tree/cursor/forgeatelier-ced6

Ce fichier ne paraphrase ni [AGENTS.md](../AGENTS.md) ni l'atelier. Il
dit seulement comment **ce** produit s'y branche.

```bash
python3 -m atelier doctor --projet .
python3 -m atelier portes --brief briefs/044-un-metier-le-mineur.md
python3 -m atelier start briefs/044-un-metier-le-mineur.md --projet .
```

Sans `--run`, rien n'est écrit. L'atelier n'invoque personne et ne
fusionne pas. Les prompts des postes sont les skills de l'atelier.

---

## Les trois postes, ici

| poste | outil | il fait | **il ne fait jamais** |
|---|---|---|---|
| **Écriture** | Claude Code · Claude Pro | écrit et amende les briefs sous `briefs/` | exécuter un lot qu'il a briefé |
| **Exécution** | Cursor · Grok 4.6 High | exécute un brief sur `agent/NNN-slug`, ouvre la PR | juger son travail, fusionner |
| **Contrôle** | Codex · GPT-5.6 | relit le brief, relit le diff | corriger ce qu'il relit |

Le relecteur n'est jamais l'auteur. Le propriétaire fusionne.

## L'interpréteur

`py` sur le PC Windows du propriétaire, `python3` sur Linux, jamais
`python` nu.

## Feuille de suivi

Elle vit dans [ROADMAP.md](../ROADMAP.md) § « Couche 2 ». 044 part
seul ; 046 et 047 partent ensuite, périmètres disjoints.

## Regarder le monde

```bash
py -m sim --ticks 0 --seed 0 --snapshot-json /tmp/monde.json
py -m viewer --snapshot /tmp/monde.json
```

## Worktrees

Si deux agents travaillent en même temps, l'atelier isole. À la main :

```bash
cd /srv/ForgeHistory
git worktree add ../fh-cursor -b agent/NNN-slug origin/master
```
