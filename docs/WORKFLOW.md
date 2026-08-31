# WORKFLOW — je veux faire avancer le jeu, je fais quoi maintenant ?

Une page : **la marche à suivre**, avec les commandes et les prompts exacts.

Les **règles** vivent dans [AGENTS.md](../AGENTS.md), l'**état du projet** dans
[ROADMAP.md](../ROADMAP.md), le **modèle du monde** dans
[sim/MODELE.md](../sim/MODELE.md). Cette page ne les paraphrase pas : elle dit
qui agit, sur quelle machine, avec quelle commande. En cas de contradiction,
AGENTS.md fait foi.

---

## Les trois postes

| poste | outil | il fait | **il ne fait jamais** |
|---|---|---|---|
| **Écriture** | Claude Code · Claude Pro | écrit et amende les briefs sous `briefs/`, tient `sim/MODELE.md`, relit un diff qu'il n'a pas écrit | exécuter un lot qu'il a briefé, ni relire son propre brief |
| **Exécution** | Cursor · Grok 4.6 High | exécute un brief de bout en bout sur `agent/NNN-slug`, ouvre la PR quand la suite est verte | juger son travail, écrire un compte-rendu, fusionner |
| **Contrôle** | Codex · GPT-5.6 | relit le brief avant qu'une ligne soit écrite, relit le diff, exécute les lots que Cursor n'a pas pris | corriger ce qu'il relit — il constate, le propriétaire tranche |

Ce n'est pas une hiérarchie, c'est une rotation. **Le seul invariant : le
relecteur n'est jamais l'auteur.** Si Codex exécute un lot, c'est Cursor ou
Claude qui le relit.

Et le propriétaire fusionne. C'est le seul geste que personne d'autre ne fait.

---

## L'interpréteur dépend de la machine

Règle 1 d'AGENTS.md, appliquée : **`py` sur le PC Windows du propriétaire**,
**`python3` sur le VPS et sous Linux**, jamais `python` nu. Les prompts
ci-dessous sont écrits pour la machine de leur poste ; si tu déplaces un agent,
change l'interpréteur avec lui.

---

## Le cycle d'un lot

L'étape 3 est la moins chère du tableau et celle qui économise le plus : un
brief relu avant qu'une ligne soit écrite coûte une lecture ; le même défaut
découvert par l'échec du code coûte une exécution complète.

| # | quoi | qui | ce qui sort |
|---|---|---|---|
| 1 | choisir le lot | **toi** | un numéro de brief |
| 2 | écrire le brief | **Claude Code** | `briefs/NNN-slug.md` |
| 3 | relire le brief | **Codex** (jamais l'auteur) | PASS, ou une liste de défauts |
| 4 | exécuter | **Cursor** | branche `agent/NNN-slug` + PR, CI verte |
| 5 | relire le diff | **celui qui n'a pas écrit** | une liste de constats |
| 6 | lire, regarder, fusionner | **toi** | `master` |

---

### 1. Choisir le lot

[ROADMAP.md](../ROADMAP.md) § « Couche 2 » dit lesquels sont écrits et dans
quel ordre. **Un seul lot de code à la fois par fichier touché.**

### 2. Écrire le brief — Claude Code

À sauter si le lot a déjà son brief. Les 044, 046 et 047 sont écrits.

```text
/ecrire-un-brief NNN — <ce que le monde saura faire après>

Un seul changement. Cite la section de sim/MODELE.md dont ça découle.
Chaque condition de succès nomme une commande qui peut échouer.
Périmètre d'écriture nommé fichier par fichier, tout le reste interdit.
Ne code rien, n'ouvre pas de branche.
```

### 3. Relire le brief — Codex

Jamais l'auteur du brief. Aucun code écrit. Un défaut se corrige **dans le
brief**, par son auteur — jamais dans le code.

```text
Relis briefs/NNN-slug.md contre AGENTS.md, section « Le brief ».
Tu ne l'as pas écrit, et tu ne le corriges pas.

Cherche les six façons de rater un brief qu'AGENTS.md énumère, et
rien d'autre. Le plus grave est le quatrième : ajuster un contrôle
après avoir vu une mesure est une calibration déguisée.

Rends PASS, ou la liste des défauts avec le numéro de ligne.
Pas de réécriture, pas de suggestion de code.
```

### 4. Exécuter — Cursor

Le brief est la seule source d'instruction. Rien d'autre ne lui est passé — ni
cette page, ni un rappel oral, ni une correction en cours de route. **Si le
brief est faux, on arrête et on le réécrit ; on ne le rattrape pas à la voix.**

Faire le plan et le code dans deux invocations séparées. Si le plan sort du
périmètre du brief, c'est le brief qui est faux.

```text
Exécute briefs/NNN-slug.md sur une branche agent/NNN-slug.

Ce brief est ta SEULE source d'instruction. AGENTS.md dit les règles.
N'écris que dans les fichiers que sa section « Périmètre » autorise ;
tout autre chemin est interdit, y compris un fichier de test.

Prouve le rouge d'abord : chaque contrôle nouveau doit échouer sur
master avant que tu corriges quoi que ce soit. Cite la sortie en échec.

Avant d'ouvrir la PR :
  python3 -m pytest sim/tests/ viewer/tests/ -q     # doit être vert
  python3 -m sim --ticks 0 --json                   # doit s'amorcer

Ouvre la PR. Ne juge pas ton travail, n'écris pas de compte-rendu,
ne fusionne rien, ne pousse pas sur master.
```

### 5. Relire le diff — celui qui n'a pas écrit

En invocation neuve, par un agent qui n'a pas vu le code s'écrire.

```text
Relis la PR #N. Tu n'as pas écrit ce code et tu ne le corriges pas.

Vérifie dans cet ordre :
 1. le diff ne sort pas du « Périmètre » de briefs/NNN-slug.md ;
 2. chaque condition de succès est réellement mesurée, pas affirmée ;
 3. aucun test existant n'a été modifié, renommé ou relâché ;
 4. aucun contrôle ne nomme sa propre référence, et aucun échantillon
    vide ne passe en silence ;
 5. aucune constante n'est lue par son nom dans sim/engine.py si le
    monde d'épreuve de test_write_coverage.py ne l'exerce pas ;
 6. les zéros rapportés sont des mesures, pas la sentinelle -1.

Rends la liste des constats, du plus grave au plus léger, avec le
fichier et la ligne. Pas de correctif, pas de PR.
```

### 6. Lire, regarder, fusionner — toi

La CI dit que les tests passent, pas que le lot est bon. Lis le diff toi-même,
et si le lot touche à ce qui se voit, ouvre la carte — règle 11.

```bash
py -m sim --ticks 0 --seed 0 --snapshot-json /tmp/monde.json
py -m viewer --snapshot /tmp/monde.json
```

### Hors cycle — après chaque fusion qui change un mécanisme

`sim/MODELE.md` est le document dont les lots suivants sont découpés. Une
formule morte décrite au présent piège le lot d'après.

```text
Le lot NNN est fusionné. Mets sim/MODELE.md à jour : la section
<titre> décrit un mécanisme que ce lot vient de changer.

Décris ce que le moteur fait AUJOURD'HUI. Retire ce qu'il ne fait
plus, plutôt que de l'écrire au passé. Mets aussi à jour ROADMAP.md
si « ce que le monde ne sait pas encore faire » a bougé.
```

---

## Feuille de suivi

Les trois lots de couche 2 sont écrits, et relus par leur seul auteur :
l'étape 3 leur reste due.

| lot | brief écrit | brief relu | exécuté | diff relu | CI | fusionné |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
| **044** — un métier : le mineur | ✅ | ☐ | ☐ | ☐ | ☐ | ☐ |
| **046** — la mer est un port commun | ✅ | ☐ | ☐ | ☐ | ☐ | ☐ |
| **047** — le bourg est une agrégation dérivée | ✅ | ☐ | ☐ | ☐ | ☐ | ☐ |

**044 avant 047** : le bourg compte une part non agricole que le métier doit
d'abord créer, et son SC4 échoue tant que l'échantillon est vide.
**046 part en parallèle** : il ne dépend d'aucun des deux, et ne touche aucun
fichier en commun avec 044.

Cette table est un aide-mémoire. La vérité reste GitHub : les PR, les checks et
`master`.

---

## Le VPS Hermes

Depuis le dégraissage V1, plus une ligne du dépôt n'en dépend. La machine
reste ; elle n'a plus qu'**une seule bonne raison d'exister**.

**À garder — si deux ou trois agents travaillent en même temps.** Trois agents
sur le même clone se marchent dessus. Un *worktree* git par agent règle ça :
chacun sa branche, chacun son répertoire, un seul `.git`.

```bash
cd /srv/ForgeHistory
git worktree add ../fh-cursor -b agent/NNN-slug   origin/master
git worktree add ../fh-codex  -b codex/NNN-revue  origin/master
git worktree list
```

Un agent ne sort jamais de son répertoire. Lot fusionné :
`git worktree remove ../fh-cursor`.

**À couper — si tu lances un agent à la fois.** C'est déjà la règle. Le dépôt
tient en une cinquantaine de fichiers, et tout ce que le VPS portait est dans
l'historique git au tag `v0-avant-degraissage`.

---

## Parler à chacun

**Claude Code** lit `CLAUDE.md`, qui renvoie à `AGENTS.md` : les règles sont
déjà chargées, ne les recopie pas dans ton message. Parle-lui en **intention**,
pas en instructions — « le monde ne sait pas encore fabriquer, ouvre-moi ça »
vaut mieux qu'une liste de fichiers. C'est lui qui découpe.

**Cursor** lit `AGENTS.md`. Donne-lui le chemin du brief et rien d'autre : tout
ce que tu ajoutes oralement devient une instruction qui n'est écrite nulle part.

**Codex** ne partage aucun fil avec les deux autres, et c'est exactement ce qui
en fait un bon relecteur : il n'a pas vu le code s'écrire. Son prompt doit donc
être autoportant — le numéro de PR, le chemin du brief, ce qu'il doit chercher.

---

## Deux pièges d'organisation

Ceux du code sont dans AGENTS.md — les douze règles payées par un vrai défaut
et les six modes de défaillance. Les deux ci-dessous ne sont pas des règles de
code : ce sont des façons de mal conduire le cycle.

1. **Lancer deux lots qui touchent le même fichier, en parallèle.** Conflit
   garanti, et il se résout en re-décidant, pas en fusionnant. C'est pourquoi
   l'étape 1 dit *un seul lot de code à la fois par fichier touché*.
2. **Laisser une constante se faire lire par son nom dans `sim/engine.py`**
   alors que le monde d'épreuve de `test_write_coverage.py` ne l'exerce pas.
   Elle devient inerte, `test_chaque_constante_du_moteur_change_le_monde`
   rougit sur `master`, et le lot suivant paie la note. Elle se lit par une
   fonction de `sim/constants.py`. Ce piège s'attrape à l'étape 3, dans le
   brief — pas à l'étape 5, quand le code est écrit.
