---
name: ecrire-un-brief
description: >
  Écrire le brief d'un lot ForgeHistory. À invoquer dès qu'on demande un
  brief, un lot, une commande de travail, ou qu'on dit « occupe-toi de X »
  sur le jeu. Le brief est la seule source d'instruction d'un lot ; il se
  range sous briefs/NNN-slug.md.
---

# Écrire un brief

Le **format** du brief et les cinq façons de le rater sont dans
[`AGENTS.md`](../../../AGENTS.md) § « Le brief ». Ce fichier ne les recopie
pas : il dit comment s'y prendre. La skill générique du cycle vit dans
ForgeAtelier (`skills/ecrire-un-brief`) ; celle-ci ajoute ce que seul
ce dépôt sait : citer `sim/MODELE.md`.

Avant d'écrire, lire dans cet ordre : `AGENTS.md` (les règles, les douze
règles payées, les six modes de défaillance, les trois niveaux de fidélité),
`sim/MODELE.md` (comment le monde fonctionne), puis les fichiers que le lot va
toucher. Pas plus.

## Les deux choses que personne d'autre ne tiendra

**Cite la section de `sim/MODELE.md` dont le brief découle.** C'est de ce
document que les lots sont découpés ; une affirmation fausse là-bas se propage
à tout ce qui suit. Un lot qui ne touche pas au monde le dit — « aucun
fondement, ce lot ne change aucun nombre » — plutôt que d'inventer une
section.

**Écris les dépendances dans les deux sens.** Le lot amont dit ce qu'il ouvre,
le lot aval dit ce qu'il suppose et se déclare **bloqué** — jamais « à
adapter » — si ce n'est pas là. L'ordre entre lots et la dépendance se
publient dans la fiche du lot, dans le registre de `ROADMAP.md`, nulle part
ailleurs : c'est là que la machine les lit.

**Sépare les fichiers autorisés des fichiers interdits.** Dans la section
« Périmètre », les autorisés dans leur phrase, les interdits dans la leur
(« Tout autre chemin est interdit, nommément : … »). L'atelier lit les
premiers pour poser le verrou et écarte les seconds ; une phrase qui mêle
les deux fait tenir des fichiers qui ne sont pas au lot.

## Ce qui vieillit, et comment l'écrire

Un brief écrit aujourd'hui peut être exécuté dans trois semaines, sur un
moteur qui aura bougé. D'où la règle 12, appliquée au brief :

- l'**état de départ** est la commande qui le mesure, plus le fait qualitatif
  qui rend le lot caduc s'il est déjà vrai — jamais une liste de nombres
  recopiés ;
- un nombre ne s'écrit dans un brief que s'il est un **paramètre décidé avant
  l'exécution** (un facteur de rendement, un plafond), jamais s'il est un
  résultat attendu.

## Où le ranger

Un fichier : `briefs/NNN-slug.md`, au chemin exact que nomme la fiche du lot
dans `ROADMAP.md`. Le numéro et le slug sont décidés dans la fiche, avant le
brief ; les lots déjà faits ont leur fiche aussi (`archive`), et leur brief
vit dans l'historique git
(`git ls-tree --name-only v0-avant-degraissage:harness/queue/briefs`).

Le brief part dans une PR, et la fiche du lot passe à `pret` dans la même PR
(`python3 -m atelier feuille marquer --projet . --lot NNN --etat pret`). Sans
cette ligne, la CI rougit : une fiche `a-briefer` dont le brief existe est une
incohérence.

## Le brief d'un palier

Une fiche `NNN-stabilisation-couche-N` n'est pas un lot comme les autres à
écrire, et c'est la seule exception que ce fichier connaît. Ce qu'un palier
**est**, et quand il se déclenche, vit dans [`AGENTS.md`](../../../AGENTS.md)
§ « Le palier ». Ce qui suit dit comment l'écrire.

**Sa matière est déjà là.** La `dépend de` de sa fiche nomme les lots qu'il
couvre : ce sont leurs briefs, et eux seuls, qu'il faut relire avant
d'écrire. Chacun a promis une règle du monde ; le palier demande ce
qu'aucun ne pouvait demander seul — est-ce qu'elles tiennent **ensemble**,
sur le même monde, au même tick ?

**Ses conditions de succès sont transversales, ou il n'en a pas.** Un
critère qui rejoue ce qu'un lot couvert a déjà prouvé ne prouve rien de
neuf. Ce qui se mesure au palier, et nulle part ailleurs :

- une **grandeur que deux mécanismes font bouger sans se connaître** — le
  panier, la population, la capacité d'une arête ;
- la **conservation à la jointure** : ce que le mécanisme A retire, le
  mécanisme B le retrouve, sur une longue course, pas sur un tick ;
- le **déterminisme de la couche entière** : même graine, même monde, après
  tous les lots ;
- ce que la **vue montre** contre ce que le moteur joue — la présentation
  lit, elle ne recalcule pas ;
- une course **longue** : ce qui tient à 365 ticks et cède à 3 000.

**Son périmètre reste étroit, et c'est ce qui le rend exécutable.** Il
ajoute ses cas aux fichiers de test qui portent déjà les invariants
concernés — jamais un fichier de test par palier — et il ne corrige que ce
que sa propre mesure prouve cassé **dans les fichiers qu'il nomme**. Tout
le reste de ce qu'il trouve devient des fiches, par une PR de feuille. Un
palier qui s'autorise à réparer partout est un lot qui ne se relit pas.

**Il dit ce qu'il a trouvé, même quand il ne trouve rien.** « Aucun défaut
transversal mesuré » est un résultat, à condition que les commandes qui
l'établissent aient pu échouer. Un palier dont aucun critère ne peut rougir
n'a pas mesuré la couche : il l'a déclarée bonne.

## Quand tu écris plusieurs briefs d'affilée

Relis la série toi-même sur quatre points que la lecture d'un brief isolé ne
peut pas attraper : un lot qui dépend d'un autre sans le dire ; deux lots qui
se réclament chacun « seul endroit » de la même chose ; un lot qui promet un
état qu'aucun autre ne produit ; deux lots qui touchent la même grandeur sans
dire comment ils se composent.

## Après l'écriture

Tu ne juges pas ton propre brief et tu ne le lances pas. Ta PR passe par la
relecture d'un tiers et par la CI ; c'est ce qui la fusionne, pas un avis —
le tien moins que tout autre.
