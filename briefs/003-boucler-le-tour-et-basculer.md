# Brief 003 — boucler le tour, et pouvoir basculer

## But

Que le tour se referme sans toi, et qu'on puisse le vérifier **avant**
de poser `ATELIER_INVOQUER=1`. Trois trous empêchent aujourd'hui la
boucle de tourner seule ; ce lot les bouche, et rien d'autre.

1. **Le dernier saut est cassé.** Composer ouvre une PR ; le numéro ne
   revient nulle part. À 19 h, le relecteur reçoit « relis le lot 044 »
   sans savoir quoi relire.
2. **Une file bloquée ressemble à une file vide.** Si tous les lots de
   `a-coder` sont verrouillés, le cron sort `RIEN` en silence. Personne
   ne sait que rien n'avance, ni pourquoi.
3. **On bascule à l'aveugle.** Rien ne dit si les binaires sont
   installés, si les verrous sont inscriptibles, si `flock` est là.

## Règle du monde

Aucun fondement dans `sim/MODELE.md`. Ce lot ne change aucun nombre du
jeu.

**Fidélité : hors jeu.**

Le brief reste la seule source d'instruction : le numéro de PR n'est pas
une consigne, c'est une **coordonnée** — il dit *où regarder*, pas *quoi
faire*. Il voyage donc sur la carte, comme les fichiers du périmètre.

Une absence se déclare. Une file bloquée n'est pas une file vide, et un
`pret` qui ne sait pas répondre dit `?`, il ne dit pas `oui`.

L'atelier n'ouvre pas la PR et ne la lit pas. Il transporte un numéro
que l'exécutant a écrit.

## Périmètre

Écriture autorisée, et rien d'autre :

- `atelier/backends.py`
- `atelier/__main__.py`
- `crons/tour.sh`
- `docs/MISE-EN-PLACE.md`
- `ROADMAP.md`
- `tests/test_boucle.py` (nouveau)

Interdit : `sim/`, `viewer/`, `data/`, `VISION.md`, le `atelier.toml`
du jeu, fusionner, invoquer un agent pendant les tests ou la CI.

## Conditions de succès

### SC1 — le numéro de PR remonte de l'exécutant à la carte

L'exécutant dépose son numéro dans `atelier-echange/pr.txt`. Après un
tour `coder` réussi :

```bash
python3 -m atelier prochain --projet <p> --role relire --champ pr
# imprime le numéro, et atelier-echange/pr.txt a disparu
```

Le fichier est retiré : un numéro périmé ne s'attache pas au lot suivant.

### SC2 — le relecteur sait quoi relire

```bash
python3 -m atelier invocation --role relire --projet <p> \
    --lot 044-mineur --brief briefs/044-mineur.md --pr 44
# le prompt nomme la PR 44 et la branche agent/044-mineur
```

Sans numéro, il nomme la branche seule — jamais un numéro inventé.

### SC3 — une file bloquée se déclare

Un verrou tient `sim/engine.py` pour un autre lot, et la seule carte de
`a-coder` le réclame :

```bash
python3 -m atelier prochain --projet <p> --role coder
# stdout = RIEN, code = 0
# stderr nomme le lot qui tient le fichier
```

Une file **vide** ne dit rien sur stderr : le silence reste le silence.

### SC4 — `pret` répond avant qu'on bascule

```bash
python3 -m atelier pret --projet <p>
# une ligne PASS/FAIL/? par contrôle ; code 1 si un FAIL
```

Un binaire absent est un `FAIL`. Un quota qu'on ne sait pas lire est un
`?`, pas un `FAIL` : l'atelier ne compte pas un inconnu pour un zéro.

### SC5 — `pret` refuse un branchement illisible

```bash
python3 -m atelier pret --projet /un/chemin/sans/atelier.toml
# code = 1, la première ligne nomme atelier.toml
```

### SC6 — rien n'est lancé par `pret`

```bash
python3 -m atelier pret --projet <p>
# aucun binaire d'agent n'est exécuté, même avec ATELIER_INVOQUER=1
```

`pret` regarde le `PATH` ; il n'appelle personne.

## Hors périmètre

- Ouvrir ou lire la PR depuis l'atelier. Composer l'ouvre, toi tu la
  fusionnes.
- Lever un verrou tout seul après ta fusion. `atelier lever` reste ton
  geste : l'atelier ne sait pas ce que tu as fusionné.
- Reprendre un juge, convoquer un conseil, sonder un fournisseur de
  quota, un second produit.
- Toute fusion. Toute invocation dans la CI.
