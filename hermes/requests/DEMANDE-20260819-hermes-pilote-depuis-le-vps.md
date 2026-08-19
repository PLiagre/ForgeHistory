---
author: hermes
kind: demande
created_at: 2026-08-19T15:43:41Z
concerns: projet
status: OPEN
---
# Piloter ForgeHistory depuis le VPS avec les capacités d’Hermes

## Situation exprimée par le propriétaire

Hermes est désormais installé sur un VPS Linux dédié. Il devient le pilote
permanent du projet, accessible au propriétaire par Discord.

Le modèle principal d’Hermes est un modèle OpenAI distant. Les abonnements
Claude et Cursor sont destinés à faire avancer le projet, sans confondre ces
abonnements avec une facturation par API.

Le partage souhaité par le propriétaire est :

- Hermes pilote le projet et lui rend compte ;
- Cursor développe ;
- Claude évalue.

À terme, le propriétaire souhaite une version « full automatisée » du
pilotage, maintenant que le cerveau du projet est distant et peut rester
allumé.

## Ce qui est déjà décidé

La répartition « Hermes pilote, Cursor exécute, Claude juge » n’est pas une
nouvelle demande. Elle est déjà décidée par l’ADR-0014, accepté le
2026-08-16 : Hermes déclenche et rend compte, Claude Code planifie, relit et
rend les verdicts, Cursor reste l’unique exécutant, et le propriétaire conserve
le veto sur la fusion.

Le rôle d’Hermes comme chef de projet, point d’entrée du propriétaire et
teneur de la feuille de route est déjà établi par l’ADR-0010. Son droit
d’écriture reste limité à `ROADMAP.md` et `hermes/**`, conformément à
`hermes/README.md`.

L’ADR-0013 prévoit déjà un VPS Linux comme cible possible pour Hermes et
ForgePilot. Il impose toutefois qu’un bilan des trois lots pilotes précède la
décision d’hébergement. Le VPS ayant été mis en service avant ce bilan, cet
écart à l’ordre prévu doit rester déclaré. La clôture du pilote et ses
conséquences ne sont pas présumées par la présente demande.

L’ADR-0013 place l’ancien pipeline en mode manuel, interdit les crons et
l’auto-fusion pendant le pilote, et limite celui-ci à une seule tâche active.
L’ADR-0014 ne réactive ni les crons, ni le full-auto, ni l’auto-fusion.

ForgePilot sait déjà choisir un modèle et un niveau d’effort par rôle depuis
le lot 023. Cette capacité de configuration ne constitue pas une évaluation du
meilleur modèle pour chaque rôle.

## Capacités nouvelles à encadrer

Le propriétaire souhaite tirer parti de trois capacités d’Hermes qui ne font
actuellement l’objet d’aucun cadre de gouvernance propre au projet :

- la délégation de travaux à plusieurs sous-agents en parallèle ;
- les tâches planifiées, notamment les crons ;
- la création d’issues GitHub.

Les workflows existants ne définissent pas ce cadre. Aucun déclencheur
planifié n’est déclaré sous `.github/workflows/`. Certains workflows réagissent
à des issues ou à leurs labels, mais ils ne donnent pas à Hermes un mandat
général de création d’issues. Le mécanisme d’escalade existant indique
explicitement que l’ouverture réelle d’une issue reste un travail futur.

## Besoin soumis à arbitrage et à conception

Le propriétaire demande que soit étudiée l’évolution du pilote vers un
fonctionnement durable depuis le VPS, pouvant aller à terme vers davantage
d’automatisation, tout en conservant la répartition déjà décidée entre Hermes,
Cursor, Claude et le propriétaire.

Restent ouverts à la décision du propriétaire :

- l’opportunité et le périmètre d’un retour vers une forme de full
  automatisation ;
- l’autorisation d’utiliser des tâches planifiées après clôture explicite du
  pilote ;
- le mandat accordé à Hermes pour créer des issues GitHub ;
- le degré d’autonomie accordé à Hermes entre deux interventions du
  propriétaire ;
- les limites budgétaires, notamment pour l’abonnement Claude ;
- le maintien du veto humain sur la fusion et les autres décisions
  irréversibles.

Restent ouverts à la conception du CTO :

- la place de la délégation parallèle sans affaiblir la séparation entre
  production et évaluation ;
- l’articulation entre les tâches planifiées d’Hermes et les workflows GitHub
  existants ;
- la représentation des demandes, incidents et travaux dans les issues GitHub,
  sans créer une nouvelle source de vérité concurrente ;
- les garde-fous nécessaires à un pilote distant fonctionnant dans la durée ;
- l’évaluation du meilleur modèle disponible pour chacun des rôles, en tenant
  compte de la qualité, du coût, des quotas et des modes d’accès réellement
  disponibles.

La présente demande n’emporte aucune décision d’architecture, aucune
réactivation du full-auto, aucun cron, aucune auto-fusion et aucune extension
du droit d’écriture d’Hermes. `ROADMAP.md` ne sera modifié qu’après arbitrage
du propriétaire.
