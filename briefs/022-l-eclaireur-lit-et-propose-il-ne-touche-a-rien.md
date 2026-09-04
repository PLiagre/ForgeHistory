# Brief 022 — L'éclaireur lit et propose, il ne touche à rien

## But

Un rôle logique `eclaireur`, strictement en lecture seule, lit le
produit et propose au propriétaire les prochains lots. Il ne crée ni
lot, ni brief, ni fiche, et ne change aucune priorité.

## Règle du monde

Le propriétaire ne garde qu'une chose : donner des directions. Une
direction se donne mieux quand on sait ce qui se passe — ce qui est
livré, ce qui casse, ce qui traîne, ce que la CI dit depuis trois jours,
quelle PR est ouverte depuis une semaine. Personne ne lui prépare cette
lecture, et c'est un travail qui se fait à la main aujourd'hui.

L'éclaireur lit, et rien d'autre :

- `VISION.md` du produit, pour savoir ce qu'on construit ;
- le modèle du produit, pour savoir comment le monde fonctionne ;
- la feuille de route, les briefs livrés, le code, les tests ;
- la CI, les PR, les incidents.

Et il propose, dans cinq registres nommés : **progrès du jeu**,
**stabilisation**, **correction de défauts**, **observabilité**, ou
**dette réellement prouvée**. Le mot *prouvée* n'est pas décoratif : une
dette se prouve par une mesure — un défaut qu'elle a causé, une durée,
un compte — et une proposition de dette sans mesure est une opinion sur
le style.

Ce qu'il ne fait pas, et qui doit être **tenu**, pas demandé :

- il n'écrit dans aucun fichier du dépôt produit ;
- il n'ouvre aucune PR, ne pousse rien, ne fusionne rien ;
- il ne dépose ni ne déplace aucune carte ;
- il n'écrit aucune fiche, aucun brief, ne change aucun état, aucune
  couche, aucune dépendance, aucun ordre.

Sa sortie est un rapport dans le canal d'échange — git-invisible et
lisible — que le propriétaire lit. Une proposition n'est pas une
décision : c'est le propriétaire qui, après l'avoir lue, donne une
direction, et la direction est le seul chemin vers le registre.

La lecture seule ne se demande pas dans un prompt. `atelier/backends.py`
sait déjà retirer la main qui écrit à un relecteur, et sait déclarer
`non-tenue` quand le binaire n'a pas le drapeau. L'éclaireur est plus
strict encore : il n'a **aucun** accord d'écriture, et son tour refuse
de l'invoquer si la garde n'est pas tenue. Un relecteur sans garde est
un risque déclaré ; un éclaireur sans garde est un tour qui n'a pas
lieu.

## Périmètre

En écriture : `atelier/backends.py`, pour le rôle, son prompt, sa garde
et le champ de `[roles]` qu'il lit. `skills/eclairer/SKILL.md`, la
compétence qui dit quoi lire et sous quelle forme rendre.
`atelier/skills_index.py`, pour l'y inscrire. `crons/eclaireur.sh` et
`crons/profils/jour.sh`, pour son réveil. `docs/LE-WORKFLOW.md`.
`tests/test_roles.py` et `tests/test_couches.py` pour y **ajouter** des
cas. Enfin
`briefs/022-l-eclaireur-lit-et-propose-il-ne-touche-a-rien.md`, ce brief.

Tout autre chemin est interdit, nommément `atelier/boite.py`,
`atelier/feuille.py`, `atelier/direction.py`, `atelier/verdict.py`,
`atelier/integration.py`, `crons/tour.sh`, `VISION.md`, `AGENTS.md` et
les autres briefs.

L'éclaireur n'a **pas** de boîte : il ne prend pas de carte, ne tient
aucun fichier, ne bloque personne. C'est ce qui le distingue d'un rôle
du cycle, et c'est pourquoi `atelier/boite.py` est hors du périmètre.

## Conditions de succès

### SC1 — l'argv de l'éclaireur ne porte aucun accord d'écriture

Le contrôle compare l'`argv` de l'éclaireur à celui d'un rôle qui écrit :
tout accord présent chez l'un est absent chez l'autre. La référence est
**dérivée** de l'autre rôle, elle n'est pas recopiée.

```bash
python3 -m pytest tests/test_roles.py -q -k eclaireur_sans_accord
```

### SC2 — l'argv porte la garde de lecture seule

```bash
python3 -m pytest tests/test_roles.py -q -k eclaireur_garde
```

### SC3 — le rouge est prouvé : sans garde, le tour n'a pas lieu

Un branchement qui nomme un binaire sans drapeau de refus d'outils fait
sortir le tour en erreur, avant toute invocation, et le dit. Il ne sort
pas 0 avec un avertissement : un éclaireur qui garde la main qui écrit
n'est pas un éclaireur.

```bash
python3 -m pytest tests/test_roles.py -q -k eclaireur_sans_garde
```

### SC4 — l'éclaireur n'a pas de boîte

Le compte des rôles de la boîte ne change pas, et `eclaireur` n'y figure
pas. Les deux comptes sont dérivés.

```bash
python3 -m pytest tests/test_roles.py -q -k eclaireur_hors_boite
```

### SC5 — un tour d'éclaireur ne laisse aucune trace dans le produit

Le scénario complet sur un produit jetable : après le tour, `git status`
du produit est propre, aucun fichier n'a changé, aucune carte n'a bougé,
aucun verrou n'est posé. La seule trace est le rapport, dans le canal
d'échange, que git ignore.

```bash
python3 -m pytest tests/test_roles.py -q -k eclaireur_sans_trace
```

### SC6 — le rapport nomme ses registres

Le skill exige que chaque proposition porte l'un des cinq registres, et
qu'une proposition de dette cite la mesure qui la prouve. Un contrôle
vérifie que le skill les nomme tous les cinq — le compte se dérive du
document, pas d'une liste recopiée.

```bash
python3 -m pytest tests/test_couches.py -q -k eclairer
```

### SC7 — la suite existante reste verte et grossit

```bash
python3 -m pytest tests/ -q
```

## Hors périmètre

Le contenu des propositions : l'atelier fournit le poste et la garde,
pas le jugement.

La transformation d'une proposition en direction : c'est le propriétaire
qui décide, et le lot de la direction qui porte le chemin.
