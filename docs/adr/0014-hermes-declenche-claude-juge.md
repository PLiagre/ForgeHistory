# ADR-0014: Hermes déclenche et rend compte, Claude juge, Cursor exécute

**Date**: 2026-08-15
**Status**: proposed
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
- **Le tableau de bord reste périmé.** Cause racine : ADR-0013 a coupé
  l'automatisme sans désigner de responsable. Cet ADR ne tranche pas ce point —
  il le laisse à la décision jointe du propriétaire, avec l'orchestrateur en fin
  de lot comme candidat par défaut.
- **Deux mémoires se contredisent.** `HANDOFF.md` et `hermes/` se disputent le
  rôle de mémoire du projet et pourrissent tous deux. Point ouvert, à trancher
  avant d'installer quoi que ce soit sur un VPS.
- **Le budget reste non borné.** Tant qu'aucun plafond mensuel n'est assumé, la
  cadence des jugements n'est pas calculable. Point ouvert.

## Ce que cet ADR ne décide pas

1. Qui régénère `hermes/DASHBOARD.md`.
2. Laquelle de `HANDOFF.md` ou `hermes/` fait foi comme mémoire du projet.
3. Le budget mensuel Claude, et donc le nombre de lots par mois.
4. Le passage au VPS — reporté jusqu'à ce que la répartition ci-dessus ait
   tourné en local.

Aucune auto-fusion, aucun cron, aucune réactivation du full-auto n'est introduite
par cet ADR.
