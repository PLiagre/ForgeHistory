# ADR-0019: Claude écrit les briefs, Hermes pilote

> **Statut actuel — 2026-08-30 : Archive historique. Les règles de rôle, d'identité, de fournisseur, de relecture, de verdict, de porte, d'orchestration et de fusion décrites ci-dessous sont obsolètes et n'imposent plus rien.**

**Date**: 2026-08-26
**Status**: accepted
**Deciders**: le propriétaire (décision du 2026-08-26), Claude (rédaction)

Amende **ADR-0018 § 1** sur un seul point : qui rédige le `brief.md`. Tout le
reste d'ADR-0018 — les trois niveaux de fidélité, la carte figée, la règle
d'admission des tests, les suppressions du dégraissage — reste en vigueur, et
ADR-0018 reste le point d'entrée des ADR.

## Contexte

ADR-0018, du 2026-08-25, a donné la rédaction des briefs à Hermes. Deux
constats, le lendemain.

**Le dépôt ne l'a jamais suivi.** `hermes/README.md` dit encore, dans deux
endroits que le dégraissage n'a pas touchés : « Hermes n'écrit **jamais** : […]
un brief », et « si besoin d'un lot : session Claude pour écrire le brief ».
`AGENTS.md`, `ROADMAP.md`, `harness/queue/README.md` et la skill d'Hermes disent
l'inverse. Un agent qui démarre lit donc deux règles contradictoires sur le seul
document qui a le droit de lui donner des instructions.

**Le brief est un acte de conception, pas de pilotage.** Un brief dit quelle
fonction change, quelle donnée elle lit, quel niveau de fidélité s'applique et
quel compteur dérivé prouve l'effet. C'est le même travail que tenir
`sim/MODELE.md`, qui est déjà chez Claude depuis ADR-0018. Les séparer oblige
Hermes à traduire un modèle qu'il ne tient pas.

## Décision

**Claude écrit tous les briefs, à la demande.** Hermes garde le pilotage.

| acteur | ce qu'il fait | ce qu'il ne fait pas |
|---|---|---|
| **Hermes** (VPS) | tient `ROADMAP.md` et le suivi ; **demande un brief** quand un lot manque ; lance ForgePilot ; mesure ; rend compte ; veille | **n'écrit plus de brief** ; ne code pas, ne fusionne pas, ne juge pas un lot |
| **Cursor** (Grok 4.6 plan, Composer code) | exécute le brief, ouvre la PR, se relit dans une invocation neuve | ne décide pas de ce qui est recevable |
| **Claude** (à la demande) | **écrit les briefs** ; tient `sim/MODELE.md` ; regard de dernier recours quand un lot ne converge pas | n'a ni cron, ni agent, ni rôle dans le harnais ; ne relit pas son propre brief ; ne juge aucun lot ; ne fusionne rien |

Le processus complet tient toujours en une ligne :

> Claude écrit un brief → Hermes le fait relire puis le lance → Cursor l'exécute
> et ouvre une PR → les tests passent et la porte mécanique vérifie le
> compte-rendu → le propriétaire fusionne.

### Ce que la règle de rôle couvre, et les deux endroits où elle ne couvre pas

**Celui qui produit ne prononce pas la recevabilité de son propre travail.**
C'est la seule règle de rôle du dépôt. Ce changement en laisse l'essentiel
intact, et laisse ouverts deux trous qu'il vaut mieux nommer que supposer
fermés.

**Ce qui reste couvert :**

- le relecteur de brief est **Grok**, lancé par Hermes
  (`forgepilot brief-review`, prompt `control-plane/prompts/brief-reviewer.md`) ;
- l'exécutant est **Cursor**, qui se relit dans une invocation neuve ;
- le juge de la PR n'est pas Claude ;
- la porte mécanique (`harness/verdict_audit.py`) ne regarde pas qui a écrit
  quoi, elle mesure ;
- le propriétaire fusionne.

**Premier trou — le diagnostic d'un lot qui ne converge pas.** Après trois
itérations, quelqu'un doit dire pourquoi. Ce quelqu'un est Claude, qui a écrit
le brief : il se prononce donc sur son propre travail. Le sens du verdict rend
une moitié de ce pouvoir inoffensive — dire « mon brief était faux » n'est pas
s'absoudre — mais l'autre moitié, « le brief est bon, relancez », l'est
entièrement.

Elle est donc **interdite**. Devant un lot qui ne converge pas, Claude n'a que
deux réponses possibles : *le brief est faux, en voici la réécriture*, ou *je
ne peux pas trancher, c'est au propriétaire*. Il ne demande jamais un
quatrième essai sur le même brief. Un brief réécrit repasse par
`brief-review`, comme le premier.

**Second trou — `sim/MODELE.md` ne passe aucune porte.** Les briefs en sont
découpés, et rien de mécanique ne le relit : ni la porte, ni le relecteur de
brief, qui ne lit que le brief et les fichiers que celui-ci cite. Une erreur
de modèle se propage donc à tous les lots suivants sans rougir nulle part.

Faute de pouvoir fermer ce trou par un contrôle, deux bornes :

1. **Un brief cite la section de `sim/MODELE.md` dont il découle.** Le
   relecteur de brief lit les fichiers cités : l'affirmation de modèle entre
   ainsi dans son champ, au lieu de rester derrière lui.
2. **Un changement de `sim/MODELE.md` se nomme dans le message de commit**, en
   disant ce qui devient faux et ce qui devient vrai. Le propriétaire est le
   seul lecteur qu'ait ce fichier ; il faut donc qu'il sache quoi y relire.

Ces deux trous existaient sous ADR-0018 : Hermes écrivait alors les briefs à
partir d'un `sim/MODELE.md` qu'il ne tenait pas, et personne ne relisait
celui-ci non plus. Ce changement ne les crée pas — il les écrit.

### Ce qui ne change pas

Le `brief.md` reste la **seule source d'instruction** d'un lot ; aucun autre
document ne le paraphrase (`harness/tests/test_single_source_of_instruction.py`).
Les briefs vivent toujours sous `harness/queue/briefs/NNN-slug/`. Le rôle
Planificateur d'ADR-0001 ne renaît pas : Claude n'a pas d'agent, pas de cron,
pas de place dans le harnais, et n'est appelé que quand on l'appelle.

## Alternatives considérées

### Garder ADR-0018 tel quel et corriger `hermes/README.md`
- **Pour** : une décision de moins ; le fichier à corriger est petit.
- **Contre** : laisse Hermes rédiger les instructions produit d'un modèle qu'il
  ne tient pas, et le fait traduire `sim/MODELE.md` à chaque lot.
- **Pourquoi non** : la contradiction n'était pas une faute de frappe, c'était
  le symptôme. ADR-0018 a créé cette règle, et le dégraissage du même jour a
  laissé `hermes/README.md` dire l'inverse : elle n'a jamais été appliquée.

### Faire écrire les briefs par Cursor
- **Pour** : un acteur de moins dans la chaîne, aucune session à ouvrir.
- **Contre** : l'exécutant écrirait sa propre commande de travail.
- **Pourquoi non** : c'est le mode de défaillance n° 7 du dépôt, celui qui a
  coûté le plus cher.

### Rendre Claude permanent (cron, agent, rôle dans le harnais)
- **Pour** : plus d'attente quand un brief manque.
- **Contre** : rejoue exactement ce qu'ADR-0018 a supprimé — trois agents
  Claude, un budget, un comptage de jetons, des gardes à maintenir.
- **Pourquoi non** : le coût mesuré du harnais était six fois celui du jeu.

## Conséquences

### Positives
- Le modèle et les briefs sont tenus par le même acteur : plus de traduction
  entre `sim/MODELE.md` et la commande de travail d'un lot.
- Les documents du dépôt cessent de se contredire sur qui rédige.
- Hermes redevient ce que `hermes/README.md` décrivait déjà : un pilote qui
  mesure, propose et lance.

### Négatives
- **Un brief coûte désormais une session Claude.** Hermes ne peut plus débloquer
  un lot tout seul à trois heures du matin ; il constate le manque, il attend.
  Contrepartie assumée : un brief mal écrit coûte un lot entier, une session
  coûte moins.
- Un acteur de plus à solliciter entre le constat et le lancement.

### Risques
- **Le brief part sans relecture parce que « c'est Claude qui l'a écrit ».**
  Atténuation : `forgepilot brief-review` reste obligatoire avant tout
  lancement, exactement comme avant, et c'est l'étape la moins chère du
  processus. L'auteur du brief n'est jamais son relecteur.
- **La file de briefs se remplit plus vite qu'elle ne se vide**, et les lots
  écrits en avance vieillissent contre un moteur qui bouge. Atténuation : un
  brief cite son état de départ par la **commande qui le mesure**, jamais par un
  nombre recopié (règle 12), et nomme le fait qualitatif qui le rendrait caduc.
