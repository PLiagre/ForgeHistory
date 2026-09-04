# Brief 011 — Le verdict est une donnée, pas une prose

## But

Une relecture rend un **verdict** : une donnée structurée, validée,
liée à la révision relue et à son auteur. `PASS`, `FAIL`, ou rien —
et rien bloque.

## Règle du monde

Le 3 septembre 2026, le relecteur du lot 046 a rendu un avis en prose
qui disait, en substance, que le lot n'était pas recevable. La carte a
avancé quand même, jusqu'à `faite`, parce que la seule porte entre
« l'agent s'est arrêté » et la boîte suivante était son code de sortie.
Une relecture terminée valait approbation. C'est ce que VISION.md
interdit désormais.

Un avis en prose n'est pas lisible par une machine, et le rendre
lisible en le devinant serait pire : un « attention » compté pour un
refus, un « je note que » compté pour un accord. La machine ne lit pas
la prose — la même règle que pour la feuille de route.

Un verdict est donc un fichier, avec un format, et le format refuse
tout ce qu'il ne comprend pas :

```json
{
  "objet": "diff",
  "lot": "046-la-mer-est-un-port-commun",
  "pr": 206,
  "sha": "e5589e3…",
  "auteur": "codex",
  "verdict": "FAIL",
  "motifs": ["SC3 n'est pas mesurée : le contrôle nomme sa référence"]
}
```

Quatre refus, et ils comptent autant que les deux acceptations :

1. **absent** — le relecteur n'a rien déposé ;
2. **illisible** — ce n'est pas du JSON, ou un champ manque, ou un
   champ inconnu s'y trouve ;
3. **périmé** — le `sha` du verdict n'est pas la révision courante de
   la branche : l'auteur a repoussé depuis, et le verdict porte sur du
   code qui n'existe plus ;
4. **interdit** — l'`auteur` du verdict est celui qui a écrit le code.

Les trois premiers sont la même réponse pour la machine : *je ne sais
pas*, et un inconnu n'est ni un oui ni un non — c'est la doctrine que
`atelier/echange.py` tient déjà pour la CI et l'état d'une PR. Le
quatrième est un refus dur : c'est la première règle non négociable de
VISION.md, et elle ne s'obtient pas en le demandant poliment dans un
prompt.

Un `FAIL` sans motif est illisible : un refus qui ne dit pas ce qu'il
refuse ne peut pas revenir à son auteur.

Ce lot ne fait que le composant. Il ne publie rien, ne déplace aucune
carte, ne touche à aucune boîte, ne parle pas à GitHub.

## Périmètre

En écriture : `atelier/verdict.py`, le composant — lecture, validation,
comparaison au SHA et à l'auteur du code. `atelier/commandes/verdict.py`
pour la commande `atelier verdict lire`. `tests/test_verdict.py` pour
ses contrôles. Et `briefs/011-le-verdict-est-une-donnee-pas-une-prose.md`,
ce brief.

Tout autre chemin est interdit, nommément `atelier/echange.py`,
`atelier/boite.py`, `atelier/reprise.py`, `atelier/feuille.py`,
`atelier/backends.py`, `crons/tour.sh`, `docs/LE-WORKFLOW.md`,
`VISION.md`, `AGENTS.md` et les autres briefs.

Le composant ne lance aucun processus et n'ouvre aucune connexion. Il
reçoit un chemin, un SHA et un nom d'auteur, et il répond. Ce qui
appelle GitHub est le lot suivant.

## Conditions de succès

### SC1 — trois réponses, trois codes de sortie

`atelier verdict lire --fichier F --sha S --auteur-code A` rend 0 pour
un `PASS` valide, 1 pour un `FAIL` valide, 2 pour tout le reste. Le
code 2 n'est jamais un feu vert.

```bash
python3 -m pytest tests/test_verdict.py -q -k codes
```

### SC2 — le rouge est prouvé sur les quatre refus

Un cas par refus — absent, illisible, périmé, auteur interdit — et
chacun rend 2 et **nomme** ce qui cloche sur `stderr`. Un cas qui ne
peut pas rougir ne prouve rien : chaque cas part d'un verdict valide
et casse exactement une chose.

```bash
python3 -m pytest tests/test_verdict.py -q -k refus
```

### SC3 — l'avis en prose du 046 rend 2

Un cas prend un texte d'avis en français, sans JSON, le donne au
lecteur, et exige 2. C'est la condition observable que le propriétaire
a nommée : un avis textuel bloquant ne peut jamais valoir accord.

```bash
python3 -m pytest tests/test_verdict.py -q -k prose
```

### SC4 — un champ inconnu est un refus, pas un champ ignoré

Un verdict qui porte une clé de plus rend 2. Un lecteur qui ignore ce
qu'il ne comprend pas accepte demain un champ qui voulait dire non.

```bash
python3 -m pytest tests/test_verdict.py -q -k inconnu
```

### SC5 — un FAIL sans motif est illisible

`"verdict": "FAIL"` avec `"motifs": []` rend 2, pas 1.

```bash
python3 -m pytest tests/test_verdict.py -q -k motifs
```

### SC6 — l'auteur du code ne signe pas

Le refus se déclenche sur l'égalité des deux noms, quel que soit le
verdict rendu : un `PASS` signé par l'auteur du code rend 2, pas 0.

```bash
python3 -m pytest tests/test_verdict.py -q -k auteur
```

### SC7 — le SHA est comparé, pas cité

Le composant reçoit le SHA courant en argument et le compare ; il ne
lit ni git, ni GitHub, ni un nom de branche. Un contrôle vérifie que
`atelier/verdict.py` n'importe ni `subprocess` ni `shutil`.

```bash
python3 -m pytest tests/test_verdict.py -q -k sha
```

### SC8 — la suite existante reste verte et grossit

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

La publication du verdict vers GitHub, qui est le lot suivant. Le
déplacement des cartes selon le verdict, qui est encore un autre lot.
Le contenu de l'avis : ce composant ne juge pas la qualité d'une
relecture, il juge sa **forme** et sa **fraîcheur**.
