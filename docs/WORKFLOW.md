# WORKFLOW — ce dépôt intègre, l'atelier invoque

Deux machines font avancer le travail, et elles ne se chevauchent pas.

**ForgeAtelier** invoque les agents : quelle carte, quel rôle, quel prompt,
quel verrou, sur quelle machine. Il ne fusionne rien — `atelier fusionner`
refuse, toujours.

https://github.com/PLiagre/ForgeHistory/tree/cursor/forgeatelier-ced6

**Ce dépôt** intègre : il dit qui a relu, quelle PR entre dans `master`, et
quand une couche finie appelle son palier. C'est ici parce que `master` est
ici — ça tourne sur GitHub, sans machine allumée chez personne.

Ce fichier ne paraphrase ni [AGENTS.md](../AGENTS.md) — qui porte les
règles — ni l'atelier. Il dit ce qu'il faut savoir pour **conduire** cette
mécanique-ci : ce qui tourne, ce qui la réveille, et quoi faire quand elle
s'arrête.

---

## Les quatre travaux

| workflow | quand | ce qu'il dit |
|---|---|---|
| `tests` | poussée, PR, appel | `sim`, `viewer`, `outils`, `feuille` |
| `security` | poussée, PR, appel | `gitleaks` : aucun secret committé |
| `relecture` | PR, revue déposée | pose l'état `relecture` sur la révision de la PR |
| `integration` | fin des trois autres, revue, chaque heure, appel | fusionne la PR verte suivante, puis dépose le palier s'il est dû |

La liste des contrôles qui gouvernent la fusion n'est pas ici : elle est
dans [`atelier.toml`](../atelier.toml) § `[integration]`, et c'est celle-là
que l'intégration lit. Ajouter un travail à la CI ne le rend pas
obligatoire ; l'ajouter à cette liste, si.

`apres_rejeu` y nomme ceux qui ne se demandent qu'une fois la PR rejouée
sur le dernier `master` — aujourd'hui `relecture`, et lui seul. Un rejeu
change la révision, donc périme ce qui était posé sur l'ancienne : la
demander avant, ce serait la payer deux fois. Les autres contrôles, une
machine les repose toute seule ; c'est ce qui distingue les deux listes,
pas leur importance.

## Ce qui réveille l'intégration

Elle ne tourne pas en boucle : elle se réveille. Quatre chemins, et le
dernier est un filet, pas le chemin ordinaire.

1. La fin de `tests`, `security` ou `relecture` — c'est le cas nominal :
   la PR passe au vert, l'intégration regarde dans les secondes qui
   suivent.
2. Une revue déposée sur une PR.
3. Une fusion : elle se rappelle elle-même, parce que la PR suivante est
   maintenant en retard sur `master` et qu'il faut la rejouer.
4. Chaque heure, à la minute 17. Ce réveil-là ne sert qu'à rattraper un
   événement perdu.

Un tour à la fois (`concurrency`), une PR à la fois. C'est ce qui rend
l'intégration séquentielle.

## Ce que l'intégration ne fait pas

- **Elle ne relit pas.** L'état `relecture` vient du travail du même nom,
  qui vient d'une approbation posée par une connexion tierce. Sans lui,
  aucune PR n'entre — c'est voulu : une machine qui pourrait s'approuver
  fusionnerait son propre code.
- **Elle ne touche pas aux branches non déclarées.** `agent/`, `brief/`,
  `feuille/` entrent ; le reste attend le propriétaire. Une expérience qui
  passe au vert n'est pas un lot.
- **Elle ne réanime pas une PR conflictuelle.** Elle rejoue une PR *en
  retard* sur `master` ; un conflit demande une décision, elle retient et
  le dit.
- **Elle ne pousse rien dans une PR** sinon ce rejeu, qui n'apporte aucune
  ligne : les commits d'un lot restent ceux de son auteur.
- **Elle n'invoque aucun agent.** Une PR qui attend sa relecture attend
  l'atelier, pas elle.

## Jouer les décisions à la main

Rien n'est écrit : ces trois commandes lisent.

```bash
export PYTHONPATH=/opt/ForgeAtelier            # ou le clone de la branche
py -m outils palier --projet .                 # où en est chaque couche
py -m outils integration --depot PLiagre/ForgeHistory --projet .
py -m outils relecture --depot PLiagre/ForgeHistory --pr 217
```

`palier --ecrire` est la seule qui touche un fichier, et seulement le
registre. Les deux autres n'écrivent jamais : le geste — fusionner,
rejouer, poser un état — appartient au workflow, où il se voit dans un
journal.

Les commandes de l'atelier, elles, vivent chez lui :

```bash
py -m atelier doctor --projet .
py -m atelier portes --brief briefs/046-la-mer-est-un-port-commun.md
py -m atelier feuille valider --projet .       # la feuille est-elle cohérente ?
py -m atelier feuille etat --projet .          # chaque lot, son état
py -m atelier piloter --projet .               # ce que le pilote déposerait
```

Sans `--run`, rien n'est déposé. Comment brancher Hermes et les crons :
[docs/MISE-EN-PLACE.md](https://github.com/PLiagre/ForgeHistory/blob/cursor/forgeatelier-ced6/docs/MISE-EN-PLACE.md).

---

## Les trois postes, ici

| poste | outil | il fait | **il ne fait jamais** |
|---|---|---|---|
| **Écriture** | Claude Code · Claude Pro | écrit le brief sur `brief/NNN-slug`, passe la fiche à `pret`, ouvre la PR | exécuter un lot qu'il a briefé |
| **Exécution** | Cursor · Grok 4.6 High | exécute un brief sur `agent/NNN-slug`, passe la fiche à `livre`, ouvre la PR | approuver son travail |
| **Contrôle** | Claude Code · Claude Pro | relit le brief, relit le diff, et **approuve ou refuse la PR** | corriger ce qu'il relit |

Le relecteur n'est jamais l'auteur, et ce n'est plus une consigne : le
travail `relecture` refuse une approbation qui vient d'une connexion ayant
écrit un des commits. Une relecture terminée sans approbation ne verdit
rien — une prose n'a jamais fusionné une PR.

Le pilote (Hermes, 07:00) ne choisit rien : `atelier piloter` lit le
registre de `ROADMAP.md` et dépose la carte du prochain lot admissible.

## Ce qui reste au propriétaire

Trois gestes, et ce sont les seuls :

1. **Donner une direction.** Une phrase devient des fiches, les fiches
   deviennent des briefs.
2. **Reprendre ce qui est tombé.** Une carte en `echec/`, une PR fermée,
   un verrou qui traîne : [ROADMAP.md](../ROADMAP.md) § « Quand ça casse ».
3. **Fusionner ce qui n'est pas un lot.** Une branche à lui, une
   expérience : l'intégration n'y touche pas.

Il ne fusionne plus les lots, et il n'a pas à le faire : la CI et la
relecture disent ce que son œil disait.

## L'interpréteur

`py` sur le PC Windows du propriétaire, `python3` sur Linux, jamais
`python` nu.

## Feuille de suivi

Le registre des lots vit dans [ROADMAP.md](../ROADMAP.md) § « Le registre
des lots », une fiche par lot ; le cycle (états, transitions, le palier,
quoi faire quand ça casse) est décrit juste après. Ce fichier-ci n'en
recopie rien.

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

## Quand la chaîne s'arrête

La question est toujours la même : **où est-ce que ça attend, et
qu'est-ce qui manque ?**

- **La PR est verte et rien ne se passe** : l'état `relecture` manque, ou
  il est périmé. Une poussée l'invalide — c'est voulu, le code a bougé.
  Faire relire à nouveau.
- **La PR vient d'être rejouée et tout a disparu** : c'est attendu une
  minute, pas dix. L'intégration redemande `tests`, `security` et
  `relecture` dans le même geste ; si les trois ne sont pas repartis,
  c'est là qu'il faut regarder.
- **`relecture` est rouge sans raison visible** : le journal du travail
  dit laquelle des quatre causes (absente, périmée, de l'auteur,
  changements demandés).
- **Une PR ouverte par la machine n'a aucun contrôle** : GitHub refuse de
  déclencher un travail sur un événement qu'un jeton d'Actions a produit.
  L'intégration les redemande nommément ; si elle ne l'a pas fait,
  `gh workflow run tests.yml --ref <branche>` le fait à la main.
- **Une carte ne bouge pas** : ce n'est pas l'intégration, c'est
  l'atelier. `atelier feuille etat --projet .` dit par quoi le lot est
  retenu.
