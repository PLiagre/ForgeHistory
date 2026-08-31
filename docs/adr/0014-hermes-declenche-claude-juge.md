# ADR-0014: Hermes déclenche et rend compte, Claude juge, Cursor exécute

> **Statut actuel — 2026-08-30 : Archive historique. Les règles de rôle, d'identité, de fournisseur, de relecture, de verdict, de porte, d'orchestration et de fusion décrites ci-dessous sont obsolètes et n'imposent plus rien.**

**Date**: 2026-08-15
**Status**: accepted
**Accepted**: 2026-08-16
**Deciders**: le propriétaire (décision), Claude Code (rédaction, rôle CTO)

Amende ADR-0010 (chaîne à quatre acteurs) et ADR-0013 (pilote ForgePilot).
Ne remplace ni l'un ni l'autre : il tranche **qui déclenche** et **qui juge**,
question qu'aucun des deux ne posait explicitement.

## Context

ADR-0010 fait d'Hermes le chef de projet et le point d'entrée du propriétaire.
ADR-0013 installe ForgePilot : Claude Code planifie et relit en lecture seule,
Cursor est le seul exécutant. Dans les faits, entre le `2026-08-13` et le
`2026-08-15` (briefs `019` à `022`), le propriétaire s'est adressé directement à
Claude Code. Hermes n'a reçu aucun rapport depuis le `2026-08-12`, son tableau
de bord est resté périmé de plus d'un jour, et le brief `022` est né sans
demande. Le propriétaire veut à terme un pipeline qui tourne longtemps sans lui,
sur un VPS portant Hermes, avec des sessions longues qu'il supervise.

Deux mesures commandent la décision.

**Le coût.** Sur la session du `2026-08-15`, le registre de jetons
(`harness/backends/ledger.py tokens`) donne `68.66` USD d'équivalent tarif API
pour un lot, dont **`59.70` pour la seule orchestration** — `87` % du total, sur
`434` appels à `213 801` jetons de contexte moyen. Le plan a coûté `1.08`, la
relecture `1.96`. Le plafond mensuel de l'abonnement a été atteint pendant la
session, pour la troisième fois depuis le `2026-08-13`. Le coût du travail de
Cursor n'est pas mesurable par ce registre.

**Le jugement.** La même session a produit trois défauts que seule une
reconstruction indépendante a attrapés : un brief annonçant `quatre` tests là où
le fichier en contient `six` ; une relecture automatique concluant faux parce
qu'elle lisait le mauvais environnement ; un Générateur déclarant `2` tests
rouges là où il y en avait `5`. Aucun n'était détectable sans refaire la mesure
soi-même.

## Decision

Séparer **déclencher** de **juger**. Hermes tient l'état, déclenche les lots,
agrège les résultats et écrit au propriétaire — il ne juge rien. Claude Code est
appelé à la demande pour les seules décisions de fond : planifier, relire,
rendre un verdict. Cursor reste le seul exécutant.

Autrement dit : **Hermes décide quand, Claude décide si c'est bon, le
propriétaire décide quoi et garde le veto sur la fusion.**

## Alternatives Considered

### Alternative 1 : Hermes devient le cerveau complet, jugement compris
- **Pros** : un seul acteur pilote de bout en bout ; correspond littéralement à
  « Hermes est le chef de projet » ; coût très bas.
- **Cons** : place le maillon le plus faible à l'endroit le plus exigeant.
  Hermes tourne sur `openai/gpt-5.4-mini` — un modèle choisi parce que
  `hermes3:8b` en local n'était pas fiable en appel d'outils. Refuser un travail
  mauvais est la tâche la plus difficile de la chaîne.
- **Why not** : les trois défauts mesurés ci-dessus ont tous demandé de
  reconstruire une mesure pour être vus. Un pilote qui aurait cru la relecture
  automatique aurait fait corriger un compteur qui était juste.

### Alternative 2 : Claude reste l'orchestrateur, on encaisse le coût
- **Pros** : aucune infrastructure à écrire ; c'est ce qui tourne aujourd'hui et
  qui produit des lots corrects.
- **Cons** : `87` % de la dépense part dans l'orchestration, et le plafond
  mensuel a sauté trois fois en trois jours. Un pipeline qui enchaîne les lots
  s'arrête de lui-même — c'est déjà arrivé, et c'est ce qui a produit ADR-0012.
- **Why not** : incompatible avec l'objectif de sessions longues. Le mode de
  panne est structurel, pas accidentel.

### Alternative 3 : full-auto complet, sans humain dans la boucle
- **Pros** : l'objectif affiché du propriétaire, et le dépôt porte déjà
  `mode: full_auto` avec sa dérogation documentée (ADR-0006).
- **Cons** : `HANDOFF.md` consigne ce que la boucle sans humain a produit —
  fusions sans contre-audit, courses d'orchestration, branches parasites, rôles
  committant malgré l'interdiction.
- **Why not** : ce qui a réellement attrapé les défauts, c'est la séparation des
  rôles et le fait qu'un acteur distinct refasse les mesures — pas
  l'automatisation. « Long et supervisé » atteint l'objectif sans le mode de
  panne.

## Consequences

### Positive
- Le coût du jugement devient prévisible : `plan` + `relecture` + `verdict`
  mesurés à `1.08` + `1.96` + environ `2` USD, contre `59.70` d'orchestration.
- Hermes peut rester allumé en continu sur une machine modeste : tenir un état
  et déclencher ne demande pas un grand modèle.
- Hermes retrouve le rôle qu'ADR-0010 lui donne, avec une raison mesurée plutôt
  qu'une déclaration d'intention.
- La séparation Planificateur / Générateur / Évaluateur est préservée : c'est
  elle qui a attrapé les défauts, elle ne doit pas être diluée.

### Negative
- Il faut écrire le déclenchement côté Hermes : aujourd'hui il ne lance rien.
- Deux acteurs à surveiller au lieu d'un pendant la transition.
- L'orchestration par Hermes sera moins fine que celle de Claude sur les cas
  tordus ; les lots inhabituels demanderont encore une session interactive.

### Risks
- **Hermes déclenche un lot qu'il n'aurait pas dû.** Atténuation : il ne
  déclenche que ce que la feuille de route autorise, et ne fusionne jamais.
- **Le tableau de bord reste périmé.** ~~Point ouvert.~~ **Clos par l'amendement
  001** ci-dessous.
- **Deux mémoires se contredisent.** ~~Point ouvert.~~ **Requalifié et clos par
  l'amendement 001** ci-dessous : le diagnostic d'origine était faux, elles ne
  se contredisent pas.
- **Le budget reste non borné.** Tant qu'aucun plafond mensuel n'est assumé, la
  cadence des jugements n'est pas calculable. Point ouvert.

---

## Amendement 001 — le tableau de bord et les deux mémoires

**Amended**: 2026-08-15T22:00:00Z
**Author**: Claude Code (rôle CTO), sur demande du propriétaire

Cet amendement ferme les points `1` et `2` de la liste « Ce que cet ADR ne
décide pas ». L'ADR reste `proposed` : l'amendement complète la proposition, il
ne l'accepte pas à la place du propriétaire.

### A. Le tableau de bord se régénère par le workflow, pas en local

**Décision.** `hermes/DASHBOARD.md` est régénéré en **déclenchant
`hermes-dashboard.yml`** (`gh workflow run hermes-dashboard.yml`), jamais en
lançant `hermes/dashboard.py` en local pour committer le résultat.

**Qui déclenche** : celui qui clôt un lot, jusqu'à ce qu'Hermes sache le faire —
après quoi c'est Hermes, puisque cet ADR lui confie déclencher et rendre compte.
Aucun cron n'est réintroduit ; ADR-0013 reste respecté sur ce point.

**Pourquoi le workflow et pas le script en local.** Mesuré le `2026-08-15` en
régénérant les deux façons : la génération locale **perd la section « Activité
GitHub récente »**, faute d'interroger l'API GitHub. Le script le dit
honnêtement (« non disponible dans cette génération ») au lieu d'inventer — mais
un tableau amputé de l'activité récente n'est plus la vue que le propriétaire
regarde en premier.

C'est une correction de ce que le CTO avait d'abord proposé au propriétaire
(« l'orchestrateur régénère en local en fin de lot ») : la mesure a contredit la
proposition.

**Conséquence à traiter par un brief, pas ici.** `hermes/dashboard.py:205-206`
écrit en dur que le tableau est « réécrite à chaque poussée sur `master` et
toutes les 6 heures ». C'est **faux depuis ADR-0013**, qui a mis le workflow en
`workflow_dispatch` seul — vérifié : `.github/workflows/hermes-dashboard.yml:13-14`
ne déclare que `workflow_dispatch:`. L'en-tête doit dire son vrai déclencheur.

C'est du **code** dans `hermes/` : le contrat d'`hermes/README.md` interdit à
Hermes d'écrire du code, donc la correction passe par un brief, pas par Hermes.

### B. `HANDOFF.md` et `hermes/` ne se contredisent pas — le diagnostic d'origine était faux

**Requalification.** L'ADR annonçait « deux mémoires qui se disputent le rôle ».
En les relisant, elles ne répondent pas à la même question :

| document | répond à | destinataire |
|---|---|---|
| `HANDOFF.md` | « comment le prochain **agent** reprend » | un agent, en début de session |
| `hermes/reports/` | « où en est le **projet** » | le propriétaire |

Le défaut réel n'est pas un conflit, c'est que **les deux sont périmées**, et que
l'une est devenue illisible. Mesuré le `2026-08-15` : `HANDOFF.md` fait `818`
lignes et empile `9` sessions, du `2026-08-12` au `2026-08-14` — trois lots de
retard. Une mémoire que personne ne relit n'est plus une mémoire.

**Décision.** Garder les deux, déclarer la frontière, et **borner `HANDOFF.md`** :

1. `HANDOFF.md` porte l'état technique de reprise des **trois sessions les plus
   récentes**, pas davantage. Les sessions plus anciennes sont retirées :
   l'historique git les conserve intégralement.
2. Si la substance d'une session compte pour le récit du projet, sa place est un
   rapport sous `hermes/reports/` — pas une strate de plus dans `HANDOFF.md`.
3. `hermes/reports/` porte la mémoire projet. C'est là que le propriétaire lit
   ce qui s'est passé et pourquoi.

**Qui écrit quoi, et quand** : `/forge-checkpoint` réécrit `HANDOFF.md` en fin de
session, depuis l'état réel des commandes et non depuis un récit. L'orchestrateur
écrit un rapport `hermes/reports/` à la clôture d'un lot.

**Conséquence à traiter par un brief, pas ici.** Borner `HANDOFF.md` à trois
sessions est un changement de comportement de `/forge-checkpoint` — du code, donc
un brief. Tant qu'il n'est pas fait, la règle vaut comme consigne éditoriale.

## Ce que cet ADR ne décide toujours pas

Après l'amendement 001, deux points restent ouverts :

1. **Le budget mensuel Claude**, et donc le nombre de lots par mois. Il demande
   un chiffre du propriétaire. L'arithmétique est prête : `~65` USD par lot
   aujourd'hui, `~5` une fois cet ADR appliqué.
2. **Le passage au VPS.** ADR-0013 avait fixé la règle — bilan après trois lots
   réels. Deux sont faits (`021`, `022`) ; le lot `023` est le troisième. Le
   bilan s'écrit après, pas avant.

Aucune auto-fusion, aucun cron, aucune réactivation du full-auto n'est introduite
par cet ADR ni par son amendement 001.

## Briefs que cet ADR appelle

Aucun n'est écrit par cet ADR ; ils sont nommés ici pour ne pas se perdre.

| objet | pourquoi c'est un brief et pas une décision | état |
|---|---|---|
| `hermes/dashboard.py` : l'en-tête doit dire son vrai déclencheur | du code dans `hermes/`, qu'Hermes n'a pas le droit d'écrire | **fait hors brief** — voir la note d'acceptation |
| `/forge-checkpoint` : borner `HANDOFF.md` à trois sessions | changement de comportement d'une commande | à écrire |
| Hermes sait déclencher un lot | la brique qui rend cet ADR applicable ; suppose le brief `023` livré | à écrire |

---

## Note d'acceptation — 2026-08-16

Le propriétaire a accepté cet ADR le `2026-08-16`, en formulant la décision de
lui-même : « je veux pouvoir lancer une vraie session via Hermes, ensuite c'est
Hermes qui pilote ». Le partage décrit ici — Hermes déclenche et rend compte,
Claude juge à la demande, Cursor exécute, le propriétaire garde le veto sur la
fusion — devient la règle.

**Ce qui a été mis en place le même jour** (commit `711b3bf`) :

- `hermes/skills/forgehistory-suivi/SKILL.md` réécrit. Hermes ouvre désormais
  une session en lisant l'état réel, connaît les six commandes de ForgePilot
  (`iterate` compris, absente de l'ancienne version), et **doit** écrire un
  rapport après chaque lot fusionné. Vérifié en exécution, pas seulement en
  lecture.
- `control-plane/forge-start` versionné — il vivait hors dépôt alors qu'il est
  le point d'entrée.

**Une entorse à déclarer.** L'amendement `001` exigeait que la correction de
l'en-tête de `hermes/dashboard.py` passe par un brief, au motif que c'est du
code dans `hermes/`. Elle a été appliquée directement, sur instruction du
propriétaire (« mets de l'ordre dans tout ça »), dans le même commit. Le texte
corrigé est un bandeau de quatre lignes, sans effet sur le calcul de la vue.
C'est écrit ici plutôt que passé sous silence : la règle de l'amendement `001`
reste valable pour la suite, et cette exception ne la périme pas.

**Ce qui reste ouvert et ne l'est pas devenu moins :** le budget mensuel Claude,
le bilan des trois lots avant toute décision de VPS, et la dette du verdict
manquant du lot `022`.
