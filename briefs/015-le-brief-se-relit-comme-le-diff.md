# Brief 015 — Le brief se relit comme le diff

## But

Un brief est relu par un agent qui ne l'a pas écrit, qui rend un verdict
de la même forme que celui du diff. Un brief refusé retourne
automatiquement à son auteur.

## Règle du monde

Le brief est la seule source d'instruction d'un lot. Un brief faux coûte
plus cher qu'un code faux : le code faux se voit à la relecture du diff,
le brief faux fait écrire le mauvais lot, puis fait relire le mauvais
diff, puis se découvre à la fusion.

La porte mécanique de `atelier/porte.py` refuse déjà un brief infirme —
titre, cinq sections, un fichier dans le périmètre, une condition qui
nomme une commande. Elle ne juge pas le fond, et c'est bien : elle est
déterministe. Mais le fond, personne ne le juge. Le skill
`relire-un-brief` existe depuis le premier jour et n'est appelé par
aucune boîte.

Après ce lot, le cycle a cinq rôles au lieu de quatre :

```
a-briefer → brief-a-relire → brief-a-integrer
     ↑              |
     └──── FAIL ────┘
```

- le briefer écrit le brief, ouvre sa PR, dépose son numéro : sa carte
  va dans `brief-a-relire`, pas dans une salle d'attente pour le
  propriétaire ;
- le rôle `relire-brief` lit le brief sur la branche de la PR et rend un
  verdict — même format, même validation, même publication d'état de
  commit que pour un diff, avec `"objet": "brief"` ;
- `PASS` → la carte va dans `brief-a-integrer` : la PR du brief est
  candidate à l'intégration mécanique, comme n'importe quelle autre ;
- `FAIL` → la carte retourne dans `a-briefer` avec les motifs. **C'est
  l'auteur du brief qui le corrige, dans son brief** — jamais le codeur
  dans le code, jamais « à la voix » ;
- absent, périmé, illisible → la carte attend.

Qui relit le brief n'est pas qui l'a écrit. `[roles]` du produit dit
`ecriture` et `controle` ; ils peuvent être le même agent pour un brief
et du code — écrire un brief n'est pas écrire du code, et
`atelier/projet.py` le permet déjà. Mais pour un **brief**, l'auteur du
brief est l'auteur : le verdict refuse sa signature exactement comme il
refuse celle du codeur sur un diff. Si le branchement nomme le même
agent des deux côtés, l'atelier le **déclare** et le tour ne dépense
rien.

## Périmètre

En écriture : `atelier/boite.py`, pour le rôle `relire-brief`, ses deux
boîtes et le retour d'une carte vers `a-briefer`. `atelier/backends.py`,
pour le prompt du nouveau rôle et le champ de `[roles]` qu'il lit.
`crons/tour.sh`, pour son tour. `crons/profils/jour.sh` et
`crons/profils/atelier.sh`, pour son réveil. `docs/LE-WORKFLOW.md`, pour
le chemin d'une carte. `tests/test_boite.py`, `tests/test_roles.py` et
`tests/test_run.py` pour y **ajouter** des cas. Enfin
`briefs/015-le-brief-se-relit-comme-le-diff.md`, ce brief.

Tout autre chemin est interdit, nommément `atelier/verdict.py`,
`atelier/porte.py`, `atelier/feuille.py`, `atelier/reprise.py`,
`atelier/projet.py`, `VISION.md`, `AGENTS.md` et les autres briefs.

Le format du verdict ne change pas : `"objet"` distingue déjà un brief
d'un diff, et le composant du lot 011 le porte. Un lot qui a besoin d'y
ajouter un champ se trompe de découpage.

## Conditions de succès

### SC1 — un brief relu PASS devient candidat à l'intégration

```bash
python3 -m pytest tests/test_run.py -q -k brief_pass
```

### SC2 — un brief refusé revient à son auteur, avec ses motifs

La carte est dans `a-briefer`, sa note porte le premier motif, et le
brief n'a été corrigé par personne d'autre.

```bash
python3 -m pytest tests/test_run.py -q -k brief_refuse
```

### SC3 — le rouge est prouvé : l'auteur du brief ne signe pas son brief

Un verdict sur un brief, signé du même agent que celui qui l'a écrit,
rend 2 et ne publie rien.

```bash
python3 -m pytest tests/test_roles.py -q -k auteur_du_brief
```

### SC4 — un branchement qui met le même agent des deux côtés se déclare

Le tour de `relire-brief` le dit sur `stderr` avant toute invocation, et
ne dépense rien. Une impossibilité se constate par une commande et un
message, jamais par une abdication.

```bash
python3 -m pytest tests/test_roles.py -q -k branchement_confondu
```

### SC5 — la boîte du briefer ne promet plus une fusion

`brief-a-fusionner` n'existe plus : aucune carte ne dit au propriétaire
qu'il a une PR à fusionner, puisqu'il ne fusionne plus.

```bash
! grep -rn 'brief-a-fusionner' atelier/ crons/ docs/
```

### SC6 — le nombre de rôles se dérive

Le compte des rôles de `atelier/boite.py`, celui des réveils du profil
`atelier`, et celui des files du chemin d'une carte s'accordent. Aucun
des trois n'écrit un nombre attendu.

```bash
python3 -m pytest tests/test_boite.py -q -k roles_derives
```

### SC7 — la suite existante reste verte et grossit

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

Le fond de la relecture : le skill `relire-un-brief` dit déjà quoi
chercher, et ce lot ne le réécrit pas.

Le nombre de fiches qu'une direction produit, qui est un autre lot.
