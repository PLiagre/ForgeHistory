# Brief 014 — Une relecture terminée n'est pas une approbation

## But

Une carte ne quitte `a-relire` que sur un verdict valide. `PASS` la fait
avancer ; `FAIL` la renvoie à l'auteur du code avec ses motifs ; absent,
périmé ou illisible la laisse attendre.

## Règle du monde

Aujourd'hui, la carte du relecteur avance parce que l'agent a rendu 0.
La boîte `faite` veut dire « relu », et `docs/LE-WORKFLOW.md` la traduit
en « à fusionner par le propriétaire ». C'est l'assimilation que la
vision interdit : une relecture terminée n'est pas une approbation.

Après ce lot :

- le tour de `relire` lit le verdict que l'agent a déposé, le valide
  contre le SHA relu et contre le nom de l'auteur du code, et publie
  l'état de commit. C'est le résultat de cette lecture, pas le code de
  sortie de l'agent, qui décide où va la carte ;
- `PASS` → la carte va dans `relu`, avec son numéro de PR. `relu` ne
  promet rien à personne : c'est l'intégrateur qui décidera, en lisant
  les contrôles ;
- `FAIL` → la carte retourne dans `a-coder`, avec les motifs dans sa
  note et la cause `refus`. L'auteur reprend son propre travail, sans
  qu'une personne ait à taper quoi que ce soit ;
- absent, périmé, illisible → la carte **reste** dans `a-relire` et le
  tour sort 0. Une porte qui s'ouvre quand la sonde se tait cède
  exactement quand elle ne répond plus. C'est déjà la doctrine du
  verdict de CI ; elle vaut ici mot pour mot.

**La boîte `faite` disparaît, elle ne se renomme pas.** Un renommage
laisserait les cartes qui y dorment glisser vers le nouveau sens : ce
sont précisément les cartes qui ont avancé sur parole. Une carte trouvée
dans `faite` tombe dans `echec` avec la cause `verdict`, et sa note dit
pourquoi : elle a été relue sous l'ancienne règle, et aucun ancien état
`faite` ne vaut approbation.

Le retour à l'auteur se **borne**. `refus` est retentable une fois : un
aller-retour, pas une boucle. Deux refus de suite sur le même lot ne
sont pas une panne passagère — c'est le brief ou le code qui est en
cause, et c'est une décision.

## Périmètre

En écriture : `atelier/boite.py`, pour la boîte `relu`, la disparition
de `faite` et le retour d'une carte à la file de son auteur.
`atelier/reprise.py`, pour la cause `refus` et son plafond.
`crons/tour.sh`, pour la porte du verdict à la sortie du tour de
`relire`. `docs/LE-WORKFLOW.md`, pour le chemin d'une carte et le
tableau des reprises. `tests/test_boite.py`, `tests/test_reprise.py` et
`tests/test_run.py` pour y **ajouter** des cas. Enfin
`briefs/014-une-relecture-terminee-n-est-pas-une-approbation.md`, ce
brief.

Tout autre chemin est interdit, nommément `atelier/verdict.py`,
`atelier/feuille.py`, `atelier/echange.py`, `atelier/backends.py`,
`atelier/couches.py`, `VISION.md`, `AGENTS.md` et les autres briefs.

Le composant du verdict ne change pas : ce lot l'appelle, il ne le
modifie pas. Si l'appel demande une signature qu'il n'a pas, c'est que
le découpage est faux, et c'est à dire — pas à contourner en écrivant
dans `atelier/verdict.py`.

## Conditions de succès

### SC1 — PASS avance, FAIL revient, le reste attend

Trois cas, trois destinations, sur un produit jetable : `relu`,
`a-coder`, et `a-relire` inchangée.

```bash
python3 -m pytest tests/test_run.py -q -k verdict
```

### SC2 — le rouge est prouvé : un agent qui rend 0 sans verdict n'avance pas

Le faux relecteur sort 0 et ne dépose rien. La carte ne bouge pas, le
tour sort 0, et rien n'est publié. C'est exactement le scénario du
3 septembre 2026.

```bash
python3 -m pytest tests/test_run.py -q -k sans_verdict
```

### SC3 — les motifs voyagent avec la carte

Après un `FAIL`, la note de la carte dans `a-coder` contient le premier
motif du verdict. Un retour à l'auteur qui ne dit pas ce qu'on lui
reproche lui fait refaire la même chose.

```bash
python3 -m pytest tests/test_boite.py -q -k motifs
```

### SC4 — la boucle est bornée

Un lot refusé deux fois reste dans `echec` : `rappeler` ne le remet pas
en circulation, et dit pourquoi sur `stderr`.

```bash
python3 -m pytest tests/test_reprise.py -q -k refus
```

### SC5 — aucune carte de `faite` n'est promue

Une carte déposée dans `faite` avant le lot se retrouve dans `echec`
avec la cause `verdict`, jamais dans `relu`. Un contrôle le vérifie sur
une boîte montée à la main.

```bash
python3 -m pytest tests/test_boite.py -q -k ancienne_faite
```

### SC6 — le mot `faite` a disparu du code et des documents

```bash
! grep -rn '"faite"' atelier/ crons/
```

### SC7 — la suite existante reste verte et grossit

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

L'intégration : `relu` est une salle d'attente, pas une autorisation.
Ce que l'intégrateur en fait est un autre lot.

La relecture du brief, qui suit la même mécanique mais sur une autre
boîte, et qui est un lot à elle.
