# WORKFLOW — ce dépôt intègre, l'atelier invoque

Deux machines font avancer le travail, et elles ne se chevauchent pas.

**ForgeAtelier** invoque les agents : quelle carte, quel rôle, quel prompt,
quel verrou, sur quelle machine. Il ne fusionne rien — `atelier fusionner`
refuse, toujours.

https://github.com/PLiagre/ForgeHistory/tree/cursor/forgeatelier-ced6

**Ce dépôt** intègre : il dit qui a relu, quelle PR entre dans `master`, et
quand une couche finie appelle son palier. C'est ici parce que `master` est
ici — ça tourne sur GitHub, sans machine allumée chez personne.

Trois étages, et ils ne se mélangent pas : `outils/` **décide** sans jamais
écrire sur GitHub, `.github/scripts/` **fait** le geste, et les workflows
appellent l'un puis l'autre sans porter de logique. Chacun se joue seul —
les décisions par leurs contrôles, les gestes sur le banc
(`outils/tests/test_scripts.py`), avec de faux `gh`, `git` et `python` en
tête du `PATH`.

Ce fichier ne paraphrase ni [AGENTS.md](../AGENTS.md) — qui porte les
règles — ni l'atelier. Il dit ce qu'il faut savoir pour **conduire** cette
mécanique-ci : ce qui tourne, ce qui la réveille, et quoi faire quand elle
s'arrête.

---

## Les six travaux

| workflow | quand | ce qu'il dit | le geste |
|---|---|---|---|
| `tests` | poussée, PR, appel | `sim`, `viewer`, `visualisateur`, `outils`, `feuille` | — |
| `security` | poussée, PR, appel | `gitleaks` : aucun secret committé | — |
| `relecture` | PR, revue déposée, appel | pose l'état `relecture` sur la révision de la PR — pour l'œil, pas pour la porte | `scripts/relecture.sh` |
| `integration` | fin des trois autres, revue, chaque heure, appel | fusionne la PR verte suivante, puis dépose le palier s'il est dû | `scripts/integrer.sh`, `scripts/palier.sh` |
| `lot` | une demande de lot est ouverte | écrit sa fiche au registre et ouvre la PR | `scripts/lot.sh` |
| `tableau` | fin de l'intégration, poussée, chaque heure, appel | réécrit la page « où en est le travail » | — |

La liste des contrôles qui gouvernent la fusion n'est pas ici : elle est
dans [`atelier.toml`](../atelier.toml) § `[integration]`, et c'est celle-là
que l'intégration lit. Ajouter un travail à la CI ne le rend pas
obligatoire ; l'ajouter à cette liste, si.

`relecture` n'y est pas, et c'est voulu. L'état de ce nom est posé par un
travail qui tourne sur le code de la PR ; s'y fier pour fusionner
laisserait une PR changer le code qui la juge. L'intégration recalcule le
verdict elle-même, avec le même module, depuis `master`. L'état reste ce
qui rend la PR lisible — il n'est pas ce qui l'ouvre.

Et il se demande **après** le rejeu : un rejeu change la révision, donc
périme l'approbation posée sur l'ancienne. L'exiger avant, ce serait la
payer deux fois.

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

- **Elle ne relit pas.** Elle vérifie qu'un tiers a relu : une
  approbation, sur la révision courante, par une connexion qui n'a écrit
  aucun des commits. Elle ne lit pas ce que cette relecture a dit. Sans
  approbation, aucune PR n'entre.
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

Le relecteur n'est jamais l'auteur, et ce n'est plus une consigne :
`outils/relecture.py` refuse une approbation qui vient d'une connexion
ayant écrit un des commits, et l'intégration rejoue ce verdict avant de
fusionner. Une relecture terminée sans approbation ne verdit rien — une
prose n'a jamais fusionné une PR.

Le pilote (Hermes, 07:00) ne choisit rien : `atelier piloter` lit le
registre de `ROADMAP.md` et dépose la carte du prochain lot admissible.

## La protection de `master`, à poser une fois

L'intégration lit sa propre liste de contrôles : elle est donc correcte
sans réglage GitHub. Mais tant que `master` n'est pas protégé, **une main
peut encore fusionner du rouge** — c'est arrivé le 4 septembre 2026 sur la
PR 225, et c'est ce qui a laissé passer un contrôle absent.

Le réglage à poser une fois, dans *Settings → Branches → master* :

- exiger les contrôles de [`atelier.toml`](../atelier.toml)
  § `[integration].controles` — les mêmes noms, exactement. Y ajouter
  `relecture` ne ferait pas de mal, mais ne suffirait pas : c'est
  l'intégration qui tient cette règle-là ;
- cocher `enforce_admins` : une règle qui s'arrête au propriétaire ne
  protège pas de la seule main capable de la contourner ;
- laisser les approbations requises à zéro. Ce n'est pas un oubli :
  l'approbation est déjà exigée par l'intégration, qui vérifie en plus
  qu'elle porte sur la révision courante et qu'elle ne vient pas d'un
  auteur du code. GitHub ne sait faire ni l'un ni l'autre.

## Demander un lot, et voir où on en est

**Demander** : ouvrir une demande avec le gabarit « Demander un lot ». Le
travail `lot` lit le formulaire, écrit la fiche en tête du registre à
l'état `a-briefer`, ouvre la PR et referme la demande en donnant son
numéro. Une dépendance qui n'existe pas au registre fait refuser la
demande, avec la raison en commentaire — on ne dépend pas d'un fantôme.

`ROADMAP.md` ne s'édite plus à la main. C'est ce qui fait qu'on ne peut
plus s'y tromper de format, de numéro ou d'état.

**Voir** : la page du travail, réécrite à chaque tour de l'intégration.
Elle sort sur GitHub Pages ; le réglage à poser une fois est
*Settings → Pages → Source : GitHub Actions*. Tant qu'il ne l'est pas, la
page est écrite quand même et déposée en pièce jointe du run — elle
existe, elle n'est pas publiée.

Elle ne décide rien : les états viennent du registre, et la raison qui
retient chaque PR vient de la décision de l'intégration — la même
fonction que celle qui fusionne, pas une paraphrase.

## Ce qui reste au propriétaire

Trois gestes, et ce sont les seuls :

1. **Donner une direction.** Une demande de lot par le formulaire ; la
   fiche entre au registre, le bon de travail s'écrit ensuite.
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

Le moteur de rendu vit à part, dans son propre dépôt :
[PLiagre/forge3d](https://github.com/PLiagre/forge3d) — du Rust, une
interface Python. `visualisateur/` est le pont : il lit une photographie
du monde et lui demande une image. Il ne simule rien, et le jeu tourne
sans lui. `viewer/` reste le regard mince, en bibliothèque standard.

```bash
python3 -m sim --ticks 0 --seed 0 --snapshot-json /tmp/monde.json
python3 -m visualisateur --snapshot /tmp/monde.json --png /tmp/monde-3d.png
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
- **Un travail meurt au milieu de son étape** : c'est `errexit`. GitHub
  joue `run:` avec `bash -e`. Le geste doit vivre dans
  `.github/scripts/`, et se rejouer sur le banc avant d'être poussé —
  AGENTS.md, règle 13.
- **Une carte ne bouge pas** : ce n'est pas l'intégration, c'est
  l'atelier. `atelier feuille etat --projet .` dit par quoi le lot est
  retenu.

## Demandes : confiance, réservation et reprise

Le formulaire est public ; il ne donne aucun droit. Le travail `autoriser`
reste en lecture seule. Seules les associations GitHub explicites `OWNER`,
`MEMBER` et `COLLABORATOR` peuvent ouvrir la tâche en écriture. Une simple
contribution (`CONTRIBUTOR`, `FIRST_TIME_CONTRIBUTOR`) ne suffit pas. Le
code vérifie cette association avant tout appel distant, puis la relit
sur l'issue courante avant de préparer la fiche.

Seul `opened` est écouté. Une branche de demande porte le suffixe
`-demande-N`, où N est le numéro de l'issue. Un rejeu retrouve cette
réservation, la PR déjà ouverte et le commentaire de succès du robot.
Une interruption après la poussée ou la création de PR se reprend sans
créer de second lot ; une demande fermée ne repart pas. Le journal du run
porte les vrais refus, sans commentaire d'échec automatique après succès.

Les demandes et l'intégration, y compris son palier, partagent le verrou
`registre`. La file conserve jusqu'à 100 attentes (`queue: max`) ; au-delà,
GitHub annule les nouveaux travaux, qui doivent être rejoués. Sous ce
verrou, le numéro dépasse ceux du registre, des branches distantes et des
fiches des PR `feuille/*` ouvertes, paliers compris. Une erreur de lecture
interdit toute attribution. Le registre est lu après acquisition du verrou.

Une PR en retard peut être rejouée même si un contrôle requis manque.
Les contrôles déjà présents doivent être verts. À jour, elle ne fusionne
qu'après les six contrôles et l'approbation indépendante sur sa tête.
La révision jugée accompagne la décision jusqu'à `--match-head-commit` :
une poussée entre la décision et le geste interdit la fusion de cette
nouvelle tête. Le code de décision et de relecture vient de `master`.

## Réglages GitHub exacts

Dans **Settings → Branches → Add branch protection rule**, cibler `master` :

- activer **Require status checks to pass before merging** ;
- exiger `sim`, `viewer`, `visualisateur`, `outils`, `feuille`, `gitleaks` ;
- activer **Require branches to be up to date before merging** ;
- activer **Do not allow bypassing the above settings** (administrateurs inclus) ;
- activer **Require a pull request before merging**, avec zéro
  approbation GitHub supplémentaire : la relecture
  indépendante sur la révision courante est exigée par l'intégration ;
- interdire les poussées forcées et la suppression de `master`.

Référence : [API officielle de protection](https://docs.github.com/en/rest/branches/branch-protection#update-branch-protection).

Corps équivalent pour `PUT /repos/PLiagre/ForgeHistory/branches/master/protection`
(à appliquer avec un accès d'administration ; ne pas remplacer à l'aveugle
une protection existante plus stricte) :

```json
{
  "required_status_checks": {
    "strict": true,
    "contexts": ["sim", "viewer", "visualisateur", "outils", "feuille", "gitleaks"]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "required_approving_review_count": 0
  },
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false
}
```

Pour Pages : **Settings → Pages → Source → GitHub Actions**. Ensuite,
**Actions → tableau → Run workflow → master**. Vérifier le succès de
`publier`, puis ouvrir l'URL de l'environnement `github-pages`. Un artefact
présent n'est pas une preuve de publication. Un 404 de l'API Pages laisse
l'artefact disponible et explique l'absence dans le résumé ; un 403 ou une
panne réelle reste un échec visible.
