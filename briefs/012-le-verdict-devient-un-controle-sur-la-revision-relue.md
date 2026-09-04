# Brief 012 — Le verdict devient un contrôle sur la révision relue

## But

Un verdict `PASS` valide pose un contrôle vert sur la PR, attaché au
SHA relu. Rien d'autre ne le pose. Sans lui, le contrôle reste attendu
et la PR ne peut pas entrer dans `master`.

## Règle du monde

Le lot précédent sait lire un verdict. Il faut maintenant que GitHub le
sache : le bouton de fusion obéit à la liste des contrôles requis, pas
à ce que l'atelier pense.

Le véhicule est un **état de commit** — `commit status` — de contexte
`atelier/relecture`, posé sur le SHA relu. Ce choix n'est pas un détail
d'implémentation, il porte deux des huit conditions à lui seul :

- **un état est attaché à une révision, pas à une PR.** L'auteur
  repousse, le SHA change, l'état ne suit pas : le contrôle redevient
  attendu, et la PR se referme d'elle-même. « Périmé bloque » n'est
  alors pas une garde qu'on écrit, c'est une propriété du support ;
- **un avis en prose ne pose rien.** Aucun état, donc un contrôle
  requis en attente, donc pas de fusion. La condition observable « un
  avis textuel bloquant comme celui de la PR 206 ne peut jamais
  produire un contrôle vert » est tenue par l'absence, pas par une
  détection.

Un `FAIL` valide pose l'état en `failure`, avec ses motifs en
description : le refus est visible sur la PR, pas seulement dans un
journal.

La commande qui parle à GitHub est **injectable**, comme les deux
sondes qui existent déjà : `ATELIER_STATUT_CMD` la nomme, et vaut `gh`
en son absence. Aucun contrôle de ce dépôt ne demande de compte
GitHub. Et comme les autres sondes : si elle ne répond pas, l'atelier
ne pose rien et le dit — il ne suppose pas que c'est passé.

Qui pose l'état n'est **pas** le relecteur. Le relecteur dépose un
fichier dans le canal d'échange ; il n'a ni le droit de pousser, ni
celui d'appeler l'API. C'est le tour qui l'a invoqué qui lit le
fichier, le valide, et pose l'état — un composant, pas un agent.

## Périmètre

En écriture : `atelier/verdict.py`, qui gagne la publication à côté de
la lecture. `atelier/commandes/verdict.py` pour `atelier verdict
publier`. `tests/test_verdict.py` pour y **ajouter** des cas. Et
`briefs/012-le-verdict-devient-un-controle-sur-la-revision-relue.md`,
ce brief.

Tout autre chemin est interdit, nommément `atelier/echange.py`,
`atelier/boite.py`, `atelier/backends.py`, `crons/tour.sh`,
`docs/LE-WORKFLOW.md`, `VISION.md`, `AGENTS.md` et les autres briefs.

Le nom du contexte, `atelier/relecture`, s'écrit une fois dans
`atelier/verdict.py` et se lit ailleurs par une fonction. Un document
ou un test qui le recopie fige une constante qui sera renommée un
jour, et piège tous les lots suivants.

## Conditions de succès

### SC1 — un PASS valide pose un état vert sur le SHA relu

```bash
python3 -m pytest tests/test_verdict.py -q -k publie_vert
```

### SC2 — un FAIL valide pose un état rouge qui porte ses motifs

L'état est `failure` et sa description cite le premier motif. Un refus
qu'on doit aller chercher dans un journal n'est pas un refus visible.

```bash
python3 -m pytest tests/test_verdict.py -q -k publie_rouge
```

### SC3 — le rouge est prouvé : la prose ne pose rien

Un avis en français, sans verdict, ne déclenche aucun appel : le
contrôle capture les appels de la commande injectée et exige qu'il n'y
en ait **aucun**. La commande sort en 2.

```bash
python3 -m pytest tests/test_verdict.py -q -k prose_ne_pose_rien
```

### SC4 — un verdict périmé ne pose rien

Le verdict porte un SHA, la branche en porte un autre : aucun appel,
code 2. C'est le cas de l'auteur qui repousse après la relecture.

```bash
python3 -m pytest tests/test_verdict.py -q -k perime_ne_pose_rien
```

### SC5 — l'état porte le SHA relu, pas la tête de la branche

Un contrôle lit l'`argv` capturé et vérifie que le SHA qu'il porte est
celui du verdict. Poser l'état sur la tête courante rendrait vert du
code que personne n'a lu.

```bash
python3 -m pytest tests/test_verdict.py -q -k porte_le_sha
```

### SC6 — sans commande, on ne suppose rien

`ATELIER_STATUT_CMD` nomme un binaire absent : la commande sort 2 et le
dit sur `stderr`. Elle ne sort pas 0.

```bash
python3 -m pytest tests/test_verdict.py -q -k sonde_muette
```

### SC7 — le contexte se lit par une fonction

Aucun fichier de `tests/` ne contient la chaîne du contexte en dur ;
les contrôles la demandent au module.

```bash
! grep -rq 'atelier/relecture' tests/
```

### SC8 — la suite existante reste verte et grossit

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

Rendre ce contrôle **obligatoire** sur `master` : c'est une protection
de branche, et c'est le lot suivant. Tant qu'il n'est pas requis, il
s'affiche sans rien bloquer — et c'est la bonne façon de le mettre en
service.

Le déplacement des cartes selon le verdict.
