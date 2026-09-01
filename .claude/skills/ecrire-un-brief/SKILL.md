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
adapter » — si ce n'est pas là. L'ordre entre lots se publie dans
`ROADMAP.md`, nulle part ailleurs.

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

Un fichier : `briefs/NNN-slug.md`. Le prochain numéro libre se lit avec
`ls briefs/`, et les lots déjà faits vivent dans l'historique git
(`git ls-tree --name-only v0-avant-degraissage:harness/queue/briefs`).

## Quand tu écris plusieurs briefs d'affilée

Relis la série toi-même sur quatre points que la lecture d'un brief isolé ne
peut pas attraper : un lot qui dépend d'un autre sans le dire ; deux lots qui
se réclament chacun « seul endroit » de la même chose ; un lot qui promet un
état qu'aucun autre ne produit ; deux lots qui touchent la même grandeur sans
dire comment ils se composent.

## Après l'écriture

Tu ne juges pas ton propre brief, et tu ne le lances pas : c'est le
propriétaire qui décide de le lancer, à qui le donner, et s'il fusionne.
