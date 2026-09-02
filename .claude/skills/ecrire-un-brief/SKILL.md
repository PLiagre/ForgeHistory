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

## Quand tu écris plusieurs briefs d'affilée

Relis la série toi-même sur quatre points que la lecture d'un brief isolé ne
peut pas attraper : un lot qui dépend d'un autre sans le dire ; deux lots qui
se réclament chacun « seul endroit » de la même chose ; un lot qui promet un
état qu'aucun autre ne produit ; deux lots qui touchent la même grandeur sans
dire comment ils se composent.

## Après l'écriture

Tu ne juges pas ton propre brief, et tu ne le lances pas : c'est le
propriétaire qui décide de le lancer, à qui le donner, et s'il fusionne.
