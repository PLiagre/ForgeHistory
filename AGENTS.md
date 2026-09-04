# AGENTS.md — les règles, pour tous

Le seul fichier de règles du dépôt. Il ne paraphrase aucun autre document :
ce qui est ici n'est écrit qu'ici.

Les trois documents vivants : **[VISION.md](VISION.md)** (ce qu'on construit,
gelé) · **[ROADMAP.md](ROADMAP.md)** (où on en est) · **AGENTS.md** (ce
fichier). Le quatrième document, [`sim/MODELE.md`](sim/MODELE.md), dit
comment le monde fonctionne — c'est de lui que les lots sont découpés.

---

## Le projet en trois phrases

ForgeHistory est un moteur de simulation historique vivant (1400-1900) dont
le gameplay émerge. Le produit est `sim/` — `py -m sim`. Le monde lit une
carte figée, `data/world-1400.json`.

## Langue

Tout ce qui s'écrit ici est en **français clair** : messages de commit,
briefs, commentaires, documents. Phrases courtes, concrètes : ce qui a été
fait, pourquoi, ce qui reste. Un terme technique nécessaire s'explique en une
phrase la première fois.

---

## Le workflow

1. Le propriétaire écrit un brief, seul ou avec qui il veut →
   `briefs/NNN-slug.md`.
2. Il le donne à qui il veut — Claude, Cursor, Codex — sur une branche.
3. La CI joue les tests.
4. Il lit le diff.
5. Il fusionne.

Pas de relecture obligatoire, pas de porte mécanique, pas de verdict, pas de
niveau de risque, pas d'ordre d'enchaînement entre les lots.

**La seule règle de rôle : celui qui a écrit le code ne dit pas s'il est
recevable.** Celui qui le dit, c'est le propriétaire, parce que c'est lui qui
fusionne.

La marche à suivre — quel agent à quelle étape, quel prompt, sur quelle
machine — vit dans **[ForgeAtelier](https://github.com/PLiagre/ForgeHistory/tree/cursor/forgeatelier-ced6)**
(`python3 -m atelier`). Ce fichier-ci dit les **règles du jeu** ; l'atelier
dit **comment on s'y prend**. Aucun des deux ne paraphrase l'autre, et
celui-ci fait foi pour le monde. Le branchement de ce dépôt est
[`atelier.toml`](atelier.toml). Un rappel local tient dans
[`docs/WORKFLOW.md`](docs/WORKFLOW.md).

## Le brief

Un fichier, cinq sections. C'est la seule source d'instruction d'un lot :
aucun autre document ne le paraphrase.

```
# Brief NNN — titre

## But                    une phrase : ce que le monde saura faire après
## Règle du monde         comment ça marche, en termes de monde ;
                          cite la section de sim/MODELE.md dont ça découle
## Périmètre              les fichiers autorisés en écriture, rien d'autre
## Conditions de succès   SC1…SCn ; chacune nomme une commande qui peut échouer
## Hors périmètre         ce que ce lot ne fait pas
```

Six façons de rater un brief :

1. **Un lot = un changement.** Si deux parties pourraient être livrées et
   jugées séparément, ce sont deux briefs. Le test : « si la moitié marche et
   l'autre pas, est-ce que je fusionne ? » Si oui, couper.
2. **Chaque critère nomme une commande, un fichier ou une valeur
   observable**, et doit pouvoir échouer. « Le code est propre » n'est pas un
   critère.
3. **Tout compteur a un dénominateur dérivé des données.** Jamais un nombre
   attendu écrit en dur. Un échantillon vide **échoue**, il ne passe pas.
4. **Ne jamais demander de modifier un test existant.** Ajuster un contrôle
   après avoir vu une mesure est une calibration déguisée. Un lot **ajoute**
   ses cas au fichier qui porte déjà l'invariant concerné.
5. **Tout brief qui touche au monde dit son niveau de fidélité** (1, 2 ou 3).
6. **Le périmètre d'écriture n'autorise que ce que le travail décrit exige.**
 Tout autre chemin est interdit, nommément. Un périmètre large est une
 permission qu'on ne se souvient pas d'avoir donnée. Les fichiers
 autorisés dans leur phrase, les interdits dans la leur : l'atelier lit
 les premiers pour poser le verrou et écarte les seconds.

## La feuille de route

L'état d'un lot ne s'écrit qu'à **un** endroit : sa fiche dans le registre
de [ROADMAP.md](ROADMAP.md), qui décrit aussi les états, les transitions et
qui tient chaque geste. Trois règles tiennent ici :

1. **Un lot n'existe que s'il a une fiche.** Un brief sans fiche est un
 orphelin, une fiche `pret` sans brief est un mensonge : les deux
 rougissent.
2. **La fiche d'un lot fait partie du périmètre implicite de sa PR**, et
 rien d'autre de `ROADMAP.md`. La PR du brief la passe à `pret` ; la PR du
 lot la passe à `livre` avec son numéro. C'est ce qui fait que `master` ne
 dit « livré » qu'après la fusion, jamais avant.
3. **La machine ne lit pas la prose.** `python3 -m atelier feuille valider
 --projet .` est la seule lecture qui compte ; la CI la joue sur chaque
 PR, avec les transitions contre `master`.

---

## Les trois principes non négociables

1. **Une seule source de vérité.** Monde → Pays → Province → Ville →
   Quartier → Bâtiment → Famille → Personne. Les vues lisent cette
   hiérarchie ; elles ne deviennent jamais une base de données parallèle.

2. **Le moteur raisonne en termes de monde, jamais de gameplay.**
   Interdit : « si famine alors +20 % de criminalité ».
   Exigé : ils ont faim → ils cherchent → certains volent → la criminalité
   monte.

3. **L'économie est physique.** Rien ne se téléporte. Tout a une origine, un
   transport, un stockage, une destination.

## Vraisemblable, pas véridique

- **Niveau 1 — juste dans les grandes lignes. Obligatoire.** La Méditerranée
  est là où elle est ; les Alpes sont des montagnes ; Venise est grande en
  1400.
- **Niveau 2 — plausible, généré, jamais sourcé.** Rendements, gisements
  secondaires, population des villages, climat local. **Une anomalie de
  niveau 2 n'est pas un défaut** : elle n'ouvre ni correctif, ni lot.
- **Niveau 3 — pas simulé.** Ce qui a besoin d'une source pour exister
  n'entre pas dans le jeu.

Pas de nombre magique dans le code du moteur — la règle tient. Justifier
chaque constante par une source — abandonné ; « ordre de grandeur plausible »
en commentaire suffit.

## La règle d'admission des tests

Un test existe s'il protège **l'une de ces trois choses**, et seulement :

1. un **invariant physique** (la masse se conserve, l'adjacence est
   symétrique, une dette ne se rembourse pas plus vite que le surplus) ;
2. une **règle de jeu visible** (on ne mange pas deux fois, on a faim, on
   meurt) ;
3. le **déterminisme** (même graine, même monde).

Corollaire : **ne pas ajouter un fichier de test par lot.** Un lot ajoute ses
cas au fichier qui porte déjà l'invariant concerné.

Et la suite doit rester **jouable à la main avant chaque fusion**. Une suite
qu'on n'attend pas est une suite qu'on ne joue pas.

---

## Les douze règles payées par un vrai défaut

Chacune a coûté un défaut mesuré. Elles portent sur le code, pas sur le
processus : elles survivent à tout changement de workflow.

1. `py`, jamais `python` (sur la machine Windows du propriétaire, `python`
   est un faux alias du Microsoft Store). Sur Linux : `python3`. Tenu
   mécaniquement par `.claude/hooks/no_bare_python.py`.
2. Un contrôle **dérive** sa référence ; il n'est jamais nommé d'après sa
   cible. (Six récurrences historiques.)
3. Un compteur dérive aussi.
4. **Prouver le rouge d'abord.** Un contrôle qui ne peut pas rougir ne
   prouve rien.
5. Une garde placée après l'effet qu'elle doit empêcher ne protège rien.
6. Un contrôle trop grossier coûte aussi cher qu'un contrôle laxiste.
7. La présence n'est pas la fonction.
8. Un zéro peut être une vraie mesure — sentinelle `-1`, jamais `0`, pour
   « non calculé ».
9. Une impossibilité se teste avant d'être invoquée : une commande et un
   message d'erreur, sinon ce n'est pas un constat mais une abdication.
10. Quand une donnée manque, l'agent l'invente en silence par défaut —
    l'absence doit donc être **déclarable**, et le code doit refuser de
    deviner.
11. **Regarder les captures soi-même.** Quatre défauts majeurs ont été vus à
    l'œil que des suites 100 % vertes n'ont jamais attrapés.
12. Une empreinte de parité se cite par **nom**, jamais par valeur : elle
    sera rebasée un jour, et le document qui porte la constante morte piège
    tous les lots suivants.

## Les six modes de défaillance diagnostiqués

| # | mode de défaillance | contre-mesure structurelle |
|---|---|---|
| 1 | double clé primaire | UNE clé spatiale : `cell_id`, décidée avant tout code |
| 2 | champ déclaré que personne n'écrit | `sim/tests/test_write_coverage.py` : chaque champ a un site d'écriture et un site de lecture, rouge sinon |
| 3 | variable terminale (calculée, lue par personne) | avant d'ouvrir un levier, vérifier que sa conséquence atteint quelque chose de mesurable hors de son module |
| 4 | la présentation réimplémente la simulation | la présentation LIT, elle ne décide jamais |
| 5 | compteur codé en dur | un compteur dérive des données, ou il n'existe pas |
| 6 | contrôle qui nomme sa propre référence | référence DÉRIVÉE de la mesure ; un échantillon vide doit ÉCHOUER, jamais passer |

---

## Où vit quoi

| chemin | quoi |
|---|---|
| `sim/` | **le produit** — `py -m sim`. Voir `sim/README.md` et `sim/MODELE.md`. |
| `data/` | la carte figée `data/world-1400.json` et les centres de province. La seule entrée géographique du jeu. |
| `viewer/` | un regard mince sur une photographie. Jamais une seconde simulation. |
| `briefs/` | un fichier par lot. |
| `ROADMAP.md` | où on en est, et le registre des lots — la seule représentation de l'état d'un lot. |
| `atelier.toml` | comment ce dépôt se branche sur ForgeAtelier. |
| `docs/WORKFLOW.md` | rappel local : les trois postes de *ce* produit, et le lien vers l'atelier. |
| hors arbre : [forge3d](https://github.com/PLiagre/forge3d) | moteur de rendu terrain récupéré (Rust/WebGPU, API Python). Dépôt séparé. Il photographie un relief ; il ne simule pas. Il n'entre ni dans `sim/` ni dans `viewer/`. |
| `visualisateur/` | regard 3D, **cette branche seulement**, comme l'atelier. Lit une photographie, parle à forge3d. Ne fusionne pas dans le jeu. |

## Les archives

L'outil qui fabrique la carte, les quarante lots déjà faits avec leurs
preuves, l'ancien pilote et l'ancien harnais sont sortis de l'arbre de
travail au dégraissage V1. Ils vivent dans l'historique git, au tag
**`v0-avant-degraissage`** :

```bash
git show v0-avant-degraissage:<chemin>        # relire un fichier
git checkout v0-avant-degraissage -- tools/   # récupérer l'outil carte
git ls-tree --name-only v0-avant-degraissage  # voir ce qu'il y avait
```

La carte est figée : on ne refait pas `data/world-1400.json`. Le jour où il
faudrait, l'outil se récupère par la commande ci-dessus.

## Les commandes

`py` sur la machine Windows du propriétaire, `python3` sur Linux — jamais
`python` nu (règle 1).

```bash
py -m sim                                # le produit
py -m sim --ticks 0 --json               # fumée : le monde s'amorce
py -m pytest sim/tests/ -q               # les tests du jeu
py -m pytest viewer/tests/ -q            # le regard mince

# la feuille de route : cohérente ? où en est chaque lot ?
# (l'atelier sur le PYTHONPATH — voir docs/WORKFLOW.md)
py -m atelier feuille valider --projet .
py -m atelier feuille etat --projet .

# regarder le monde : photographier, puis ouvrir
py -m sim --ticks 0 --seed 0 --snapshot-json /tmp/monde.json
py -m viewer --snapshot /tmp/monde.json
```

Le moteur et le regard sont en bibliothèque standard seule ; seuls les tests
demandent `pytest`. Il n'y a pas de linter : les garde-fous du dépôt sont les
tests et l'œil du propriétaire sur le diff.
