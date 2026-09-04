# Brief 020 — L'intégration est mécanique, séquentielle et retestée

## But

Une PR entre dans `master` sans intervention humaine quand tous les
contrôles requis sont verts sur sa révision courante, et à cette seule
condition. Une à la fois, retestée sur le dernier `master`.

## Règle du monde

C'est le lot qui occupe la huitième couche, `intégration` — ouverte par
`VISION.md`, et rouge tant qu'aucun module ne la déclare.

L'intégrateur ne juge rien. Il ne lit ni brief, ni diff, ni avis, ni
verdict : il lit **la liste des contrôles requis**, celle qui gouverne le
bouton de fusion de GitHub. Le verdict de relecture y figure comme un
contrôle parmi d'autres — c'est le lot 012 qui l'y a mis, et c'est le lot
013 qui l'a rendu obligatoire. Un intégrateur qui lirait le verdict
lui-même serait un juge, et VISION.md dit que la couche n'en est pas un.

`atelier integrer --projet P` fait, pour **une** PR à la fois, prise
dans l'ordre de la feuille de route :

1. l'état de la PR est `ouverte` — sinon on passe ; l'inconnu retient ;
2. les contrôles requis sont tous verts **sur la révision courante de la
   PR**. Un contrôle en attente n'est pas vert, un contrôle inconnu non
   plus ;
3. la branche est à jour avec `master`. Si elle ne l'est pas, on la met
   à jour et **on s'arrête là** : la mise à jour change la révision, donc
   les contrôles doivent rejouer, donc le verdict de relecture tombe
   avec eux. Le réveil suivant reprendra cette PR ;
4. tout est vert et à jour : on fusionne, on rend le verrou du lot et
   son worktree.

Le point 3 est la condition « la seconde PR est retestée avec la
première intégrée avant sa fusion », et c'est aussi ce qui rend
l'intégration **séquentielle** : deux PR ne peuvent pas être à jour avec
le même `master` après qu'on en a fusionné une. La séquence n'est pas un
verrou de plus, c'est une conséquence.

Un verdict de relecture posé sur l'ancienne révision ne survit pas à la
mise à jour de la branche — c'est ce qu'assure l'état de commit, attaché
à un SHA. Une PR mise à jour repasse donc par une relecture. Ce n'est pas
un effet de bord à contourner : c'est la règle « périmé bloque », et elle
coûte ce qu'elle coûte.

Trois choses que ce lot ferme :

- **`atelier fusionner` refuse toujours.** C'est la commande qu'un agent
  atteindrait, et elle reste un mur. L'intégration n'est pas une
  permission qu'on accorde à un agent, c'est un composant que le cron
  appelle ;
- **le codeur perd `gh` en grand.** Son accord d'écriture porte
  aujourd'hui `Bash(gh:*)`, qui contient `gh pr merge`. L'auteur du code
  a donc le droit de fusion, écrit noir sur blanc dans
  `atelier/backends.py`. Il est remplacé par la liste étroite dont un
  auteur a besoin — ouvrir une PR, la voir, la pousser — et rien de plus ;
- **rien ne fusionne sans avoir lu.** L'intégrateur relit la liste des
  contrôles juste avant de fusionner, sur le SHA qu'il s'apprête à
  fusionner. `enforce_admins` est la garde de GitHub ; celle-ci est la
  sienne. Deux gardes, parce qu'un jeton d'administrateur passe la
  première.

## Périmètre

En écriture : `atelier/integration.py`, le composant, qui déclare la
couche `intégration`. `atelier/couches.py`, pour la valeur de
l'énumération que ce module occupe. `atelier/commandes/integration.py`
pour `atelier integrer`. `atelier/backends.py`, pour l'accord d'écriture
du codeur. `crons/integrer.sh` et `crons/profils/jour.sh`, pour son
réveil. `docs/LE-WORKFLOW.md`, pour la fin du chemin d'une carte.
`tests/test_integration.py` pour ses contrôles, `tests/test_cli.py` et
`tests/test_couches.py` pour y **ajouter** des cas. Enfin
`briefs/020-l-integration-est-mecanique-sequentielle-et-retestee.md`, ce
brief.

Tout autre chemin est interdit, nommément `atelier/verdict.py`,
`atelier/controles.py`, `atelier/boite.py`, `atelier/feuille.py`,
`atelier/prise.py`, `atelier/verrou.py`, `crons/tour.sh`, `VISION.md`,
`AGENTS.md` et les autres briefs.

L'intégrateur ne lit aucun verdict et n'ouvre aucun fichier du canal
d'échange. S'il en a besoin, le découpage est faux.

## Conditions de succès

### SC1 — la huitième couche est occupée

```bash
python3 -m atelier couches
python3 -m pytest tests/test_couches.py -q
```

### SC2 — tout vert et à jour : la PR est fusionnée, sans personne

```bash
python3 -m pytest tests/test_integration.py -q -k nominal
```

### SC3 — le rouge est prouvé sur chaque manque

Un cas par manque — un contrôle rouge, un contrôle en attente, un
contrôle inconnu, un contrôle requis absent de la réponse, l'état de la
PR inconnu — et dans chacun **aucun appel de fusion** n'est émis. Le
contrôle capture l'`argv` de la commande injectée : l'absence d'appel est
la preuve, pas le code de sortie.

```bash
python3 -m pytest tests/test_integration.py -q -k refus
```

### SC4 — une branche en retard est mise à jour, puis on s'arrête

Un seul appel de mise à jour, aucun appel de fusion, et la commande dit
qu'elle rendra la main au réveil suivant.

```bash
python3 -m pytest tests/test_integration.py -q -k en_retard
```

### SC5 — une seule PR par tour

Trois PR toutes vertes : un seul appel de fusion. La séquence n'est pas
un ordre de préférence, c'est une exclusion.

```bash
python3 -m pytest tests/test_integration.py -q -k sequentielle
```

### SC6 — la seconde PR est retestée après la première

Le scénario enchaîne : la première fusionne, la seconde devient en
retard, elle est mise à jour, ses contrôles rejouent sur la nouvelle
révision, et c'est seulement alors qu'elle fusionne.

```bash
python3 -m pytest tests/test_integration.py -q -k retestee
```

### SC7 — un avis en prose ne fait jamais fusionner

La PR porte un commentaire favorable en français et aucun état de
relecture : le contrôle requis manque, aucun appel de fusion n'est émis.

```bash
python3 -m pytest tests/test_integration.py -q -k prose
```

### SC8 — aucun agent n'a le droit de fusion

L'`argv` d'aucun rôle ne contient d'accord qui ouvre `gh pr merge`, et
`atelier fusionner` sort toujours en 2.

```bash
python3 -m pytest tests/test_integration.py -q -k droit_de_fusion
python3 -m pytest tests/test_run.py -q
```

### SC9 — le verrou et le worktree du lot sont rendus après la fusion

```bash
python3 -m pytest tests/test_integration.py -q -k rendus
```

### SC10 — la suite existante reste verte et grossit

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

Poser la protection de branche : c'est le lot 013, et c'est un geste
d'exploitation.

Décider quelle PR mérite d'être intégrée : l'intégrateur ne décide pas,
il constate. La feuille de route donne l'ordre, les contrôles donnent le
droit.
