# WORKFLOW — ce dépôt se branche, il n'orchestre plus

La marche à suivre vit dans **ForgeAtelier** : sept couches, un cycle,
aucune fusion. Branche orpheline en attendant le dépôt détaché :

https://github.com/PLiagre/ForgeHistory/tree/cursor/forgeatelier-ced6

Comment brancher Hermes et les crons indépendants :
[docs/MISE-EN-PLACE.md](https://github.com/PLiagre/ForgeHistory/blob/cursor/forgeatelier-ced6/docs/MISE-EN-PLACE.md).

Ce fichier ne paraphrase ni [AGENTS.md](../AGENTS.md) ni l'atelier. Il
dit seulement comment **ce** produit s'y branche.

```bash
export PYTHONPATH=/opt/ForgeAtelier            # ou le clone de la branche
python3 -m atelier doctor --projet .
python3 -m atelier portes --brief briefs/046-la-mer-est-un-port-commun.md
python3 -m atelier feuille valider --projet .  # la feuille est-elle cohérente ?
python3 -m atelier feuille etat --projet .     # chaque lot, son état
python3 -m atelier piloter --projet .          # ce que le pilote déposerait
```

Sans `--run`, rien n'est écrit. L'atelier n'invoque personne et ne
fusionne pas. Les prompts des postes sont les skills de l'atelier.

---

## Les trois postes, ici

| poste | outil | il fait | **il ne fait jamais** |
|---|---|---|---|
| **Écriture** | Claude Code · Claude Pro | écrit le brief sur `brief/NNN-slug`, passe la fiche à `pret`, ouvre la PR | exécuter un lot qu'il a briefé |
| **Exécution** | Cursor · Grok 4.6 High | exécute un brief sur `agent/NNN-slug`, passe la fiche à `livre`, ouvre la PR | juger son travail, fusionner |
| **Contrôle** | Claude Code · Claude Pro | relit le brief, relit le diff | corriger ce qu'il relit |

Le relecteur n'est jamais l'auteur. Le propriétaire fusionne. Le pilote
(Hermes, 07:00) ne choisit rien : `atelier piloter` lit le registre de
`ROADMAP.md` et dépose la carte du prochain lot admissible ; Hermes reçoit
cette décision et la résume.

## L'interpréteur

`py` sur le PC Windows du propriétaire, `python3` sur Linux, jamais
`python` nu.

## Feuille de suivi

Le registre des lots vit dans [ROADMAP.md](../ROADMAP.md) § « Le registre
des lots », une fiche par lot ; le cycle (états, transitions, qui fait
quoi, quoi faire quand ça casse) est décrit juste après. Ce fichier-ci
n'en recopie rien : ce qui est écrit là-bas n'est écrit que là-bas.

La CI joue `atelier feuille valider --base origin/master` sur chaque PR :
une PR de lot dont la fiche ne passe pas à `livre`, une fiche qui saute
un état, un brief orphelin — rouge.

## Regarder le monde

```bash
py -m sim --ticks 0 --seed 0 --snapshot-json /tmp/monde.json
py -m viewer --snapshot /tmp/monde.json
```

Un moteur de rendu terrain récupéré vit à part :
[PLiagre/forge3d](https://github.com/PLiagre/forge3d). Il n'est pas
branché. `viewer/` reste le regard mince, en bibliothèque standard.

## Worktrees

Si deux agents travaillent en même temps, l'atelier isole. À la main :

```bash
cd /srv/ForgeHistory
git worktree add ../fh-cursor -b agent/NNN-slug origin/master
```

## Quand ça casse

Une carte en `echec/`, une PR fermée, un verrou qui traîne : la marche
à suivre est dans [ROADMAP.md](../ROADMAP.md) § « Quand ça casse », et
les commandes dans l'atelier (`atelier reprendre`, `atelier echouer`,
`atelier lever`).
