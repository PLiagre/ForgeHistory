# Brief 023 — Un faux GitHub sur le banc

## But

Le banc apprend à répondre comme GitHub : contrôles, états de commit,
état d'une PR, mise à jour de branche, fusion. Scriptable, pour rejouer
n'importe quel scénario sans compte ni réseau.

## Règle du monde

Le banc monte déjà un produit jetable, ses worktrees, et de faux agents
pilotés par l'environnement. Il fait un lien `gh` vers le même faux
agent que les autres : il répond au hasard, ce qui suffisait tant que
personne ne lisait sa réponse.

Le cycle automatique la lit partout. Cinq sondes en dépendent : le
verdict de CI, l'état d'une PR, la PR ouverte sur une branche, l'état de
commit de la relecture, la protection de branche. Et une commande écrit
à travers elle : la fusion. Un faux `gh` au hasard ne permet de tester
aucune des huit conditions.

Le faux GitHub est un **état sur le disque**, pas une logique. Un
répertoire par PR, un fichier par chose que GitHub sait dire :

```
$BANC/github/pr/206/etat          ouverte | fusionnee | fermee
$BANC/github/pr/206/branche       agent/046-la-mer-est-un-port-commun
$BANC/github/pr/206/sha           la révision courante
$BANC/github/pr/206/base          la révision de master qu'elle porte
$BANC/github/pr/206/controles     une ligne par contrôle : nom, état
$BANC/github/statuts/<sha>/…      les états de commit posés
$BANC/github/master               la révision courante de master
$BANC/github/appels.log           tout ce qu'on lui a demandé
```

Un scénario écrit ces fichiers, lance un tour, et lit ce qui a changé.
C'est tout. Aucune horloge, aucun serveur, aucun processus qui dort.

Trois propriétés que le faux doit tenir, sans quoi il ment :

- **il refuse ce que GitHub refuse.** `gh pr merge` sur une PR dont un
  contrôle requis n'est pas vert rend une erreur, pas un succès. Sans
  ça, le banc validerait un intégrateur qui fusionne du rouge — ce que
  `enforce_admins` interdit sur le vrai dépôt ;
- **une mise à jour de branche change le SHA.** C'est ce qui fait tomber
  les contrôles et périmer le verdict. Un faux qui garderait le même SHA
  rendrait la condition « la seconde PR est retestée » invérifiable ;
- **il journalise tout.** Un contrôle qui affirme « aucune fusion n'a été
  demandée » a besoin de la trace, pas d'un code de sortie.

Le faux `gh` reste **hors de portée du vrai** : il vit dans le `PATH` du
banc, devant tout le reste, comme les faux agents. Un vrai `gh` n'est pas
*choisi de ne pas être appelé*, il est hors de portée.

## Périmètre

En écriture : `crons/faux-gh.sh`, le faux lui-même. `crons/banc.sh`, qui
le monte, l'installe dans le `PATH` du banc, et enrichit le produit
d'épreuve — quatre lots au lieu de deux, deux paires disjointes et une
paire qui partage un fichier, ce qu'il faut pour jouer les conditions
sans les inventer. `crons/profils/atelier.sh`, pour le cycle du banc.
`tests/test_banc.py` pour les contrôles du faux. Enfin
`briefs/023-un-faux-github-sur-le-banc.md`, ce brief.

Tout autre chemin est interdit, nommément `atelier/echange.py`,
`atelier/verdict.py`, `atelier/integration.py`, `atelier/controles.py`,
`crons/tour.sh`, `crons/epreuve.sh`, `VISION.md`, `AGENTS.md` et les
autres briefs.

Le faux ne connaît aucun module de l'atelier et n'importe rien : il lit
son `argv`, lit des fichiers, écrit des fichiers. Un faux qui saurait ce
que l'atelier attend cesse de le mettre à l'épreuve.

## Conditions de succès

### SC1 — les cinq lectures répondent d'après le disque

Contrôles requis, état d'une PR, PR ouverte sur une branche, états de
commit, protection de branche : cinq cas, cinq réponses au format que
l'atelier lit déjà.

```bash
python3 -m pytest tests/test_banc.py -q -k lectures
```

### SC2 — le rouge est prouvé : le faux refuse de fusionner du rouge

Une PR avec un contrôle requis rouge, une demande de fusion : code non
nul, aucun changement d'état, et la trace dans le journal.

```bash
python3 -m pytest tests/test_banc.py -q -k refuse_le_rouge
```

### SC3 — une mise à jour change le SHA et fait tomber les contrôles

Après une mise à jour, la PR porte un nouveau SHA, ses contrôles sont en
attente, et aucun état de commit ne pointe sur le nouveau SHA.

```bash
python3 -m pytest tests/test_banc.py -q -k mise_a_jour
```

### SC4 — le journal porte tout, et rien d'autre

Le compte des lignes du journal s'accorde au compte des appels du
scénario. Les deux sont dérivés.

```bash
python3 -m pytest tests/test_banc.py -q -k journal
```

### SC5 — le vrai `gh` est hors de portée

Un contrôle vérifie que le `PATH` du profil du banc place ses faux
devant tout, et qu'aucun binaire réel n'est atteignable sous ces noms.

```bash
python3 -m pytest tests/test_banc.py -q -k hors_de_portee
```

### SC6 — le produit d'épreuve porte de quoi jouer une collision

Le registre du banc a quatre lots ; deux paires ont des périmètres
disjoints, une paire partage un fichier. Le compte se dérive des briefs
du banc, pas d'un nombre écrit.

```bash
python3 -m pytest tests/test_banc.py -q -k quatre_lots
```

### SC7 — `crons/banc.sh --neuf` remet tout à zéro

Deux montages d'affilée rendent le même état, quel que soit ce que le
premier a laissé.

```bash
python3 -m pytest tests/test_banc.py -q -k neuf
```

### SC8 — la suite existante reste verte et grossit

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

Les scénarios eux-mêmes, qui sont le lot suivant : celui-ci fournit
l'instrument, pas la mesure.

Le comportement de l'atelier : si un scénario échoue, ce n'est pas au
faux `gh` de s'adapter.
