---
author: hermes
kind: demande
created_at: 2026-08-12T12:10:00Z
concerns: projet
status: REFLECTED_IN_ROADMAP
---
# Demande propriétaire — Hermes, tableau de bord unique et console de pilotage

Demande exprimée par le propriétaire (session Cursor Cloud, 2026-08-12),
reformulée sans en changer le fond :

1. Le propriétaire se connecte à son Hermes local
   (`http://127.0.0.1:9119/sessions`) mais ne sait pas quoi y regarder ni
   quoi y faire.
2. Il veut que **tout le suivi du projet** (chaque agent qui tourne, chaque
   PR faite et validée, chaque étape de la boucle) soit visible **depuis
   Hermes**, et que le projet soit **pilotable** depuis Hermes.
3. Hermes doit devenir **le seul tableau** que le propriétaire a besoin de
   suivre. Si utile, le brancher sur le compte ChatGPT Plus pour le rendre
   plus pertinent.

## État des lieux (ce qui existe déjà — à ne pas reconstruire)

- **Le tableau de bord du projet existe** : `hermes/DASHBOARD.md`, généré
  par `hermes/dashboard.py`, réécrit à chaque poussée sur `master` et
  toutes les 6 heures (`.github/workflows/hermes-dashboard.yml`). C'est
  déjà « l'endroit où le propriétaire regarde d'abord » (`hermes/README.md`).
- **Le pont GitHub → Hermes local existe** :
  `.github/workflows/hermes-observer.yml` transmet chaque PR et chaque fin
  de workflow au runner auto-hébergé de la machine du propriétaire
  (`runner-event.ps1`, installation Hermes hors dépôt). Hermes local
  **reçoit donc déjà les événements du projet** ; il est en phase « shadow »
  jusqu'au 2026-08-24 selon sa propre configuration.
- **Ce que montre `http://127.0.0.1:9119` aujourd'hui** : uniquement la vie
  de l'agent Hermes lui-même (ses sessions de conversation, ses tâches
  planifiées, ses journaux). La page `/sessions` ne montera jamais l'état
  du projet tant que personne ne lui donne les données du projet — c'est
  l'objet de cette demande.

Le manque n'est donc **pas** de produire des rapports (Hermes le fait) ;
c'est de **raccorder** les deux mondes : les sources de vérité du dépôt
d'un côté, l'interface locale du propriétaire de l'autre — puis, seulement
ensuite, d'ouvrir un pilotage borné.

## Plan d'action proposé (phases H1 → H5)

### H1 — Brancher l'Hermes local en lecture (hors dépôt, aucun brief)

Configuration de l'installation locale d'Hermes uniquement ; le dépôt ne
change pas, aucune décision d'architecture requise.

- Une **compétence (« skill ») de suivi ForgeHistory** pour l'agent Hermes :
  lire `hermes/DASHBOARD.md` (brut, depuis GitHub), les PR ouvertes et les
  runs Actions (`gh` avec un jeton **en lecture seule**), les agents Cursor
  en cours (`GET https://api.cursor.com/v1/agents`, clé déjà provisionnée
  côté CI), et les ledgers (`architecture/audit-ledger.jsonl`,
  `harness/pipeline/ci-budget-ledger.jsonl`, `harness/queue/cost-ledger.jsonl`).
- Des **tâches planifiées** (page Cron du tableau 9119) : un **digest du
  matin** (« ce qui attend le propriétaire », activité de la nuit, dépense
  du mois), et une **alerte immédiate** quand `pipeline-failure-escalate`
  se déclenche ou qu'un workflow échoue — en s'appuyant sur les événements
  que `hermes-observer` pousse déjà au runner local.
- Résultat attendu : le propriétaire ouvre le chat Hermes et demande
  « où en est le projet ? » — la réponse vient des sources de vérité,
  jamais d'une invention.

### H2 — Export machine du tableau de bord (brief à écrire par le CTO)

`hermes/dashboard.py` produit du Markdown pour un humain ; l'Hermes local
doit aujourd'hui le re-parser. Proposition : le même script exporte aussi
`hermes/dashboard.json` (mêmes données, format machine, régénéré par le
même workflow). L'Hermes local lit **un seul fichier** fiable au lieu de
re-mesurer le dépôt. Enrichissements au passage : la section « Agents
lancés récemment (Cursor Cloud) » est aujourd'hui « non disponible » même
en CI (l'API n'est pas interrogée) — l'interroger ; ajouter les liens
directs (PR, runs) pour que chaque ligne soit cliquable depuis Hermes.

### H3 — Une liste d'attentes exhaustive (brief à écrire par le CTO)

La section « Ce qui attend le propriétaire » du tableau doit devenir la
**seule to-do** du propriétaire. Aujourd'hui elle liste les PR ouvertes et
les audits à convertir ; il manque : les secrets absents ou périmés
(`CODEX_AUTH_JSON` expire en ~8 jours sans rafraîchissement), les
dérogations `::warning::` consignées par les workflows, les demandes
`hermes/requests/` encore `OPEN`, et les étapes « propriétaire » de
`ROADMAP.md`. Règle inchangée : une donnée absente est dite absente.

### H4 — Pilotage depuis Hermes (nécessite un ADR, ne pas câbler avant)

ADR-0010 fait d'Hermes un chef de projet qui n'exécute rien ; « tout
pilotable depuis Hermes » est donc une **extension de contrat** à trancher
par ADR (0011 proposé) : Hermes devient la **console du propriétaire** —
il exécute des actions *du propriétaire*, sur ordre explicite dans le chat,
jamais de sa propre initiative. Périmètre fermé proposé :

1. **fusionner ou refuser une PR** (le « clic final humain » actuel) ;
2. **poser/retirer le label `pipeline/pause`** (l'arrêt d'urgence documenté) ;
3. **déclencher `pipeline-forge-run`** (`workflow_dispatch`) sur un brief ;
4. **déposer une demande** dans `hermes/requests/` (déjà dans son contrat).

Garde-fous non négociables : confirmation explicite avant chaque action ;
jeton GitHub dédié à permissions minimales ; chaque action consignée
(rapport `hermes/reports/`) ; Hermes n'écrit toujours **jamais** de code,
de brief, de verdict ni de CI ; le tableau 9119 reste lié à `127.0.0.1`
(pas d'exposition réseau sans authentification).

### H5 — Compte ChatGPT Plus : clarification honnête

Un abonnement ChatGPT Plus **ne fournit pas de clé API** : on ne peut pas
« brancher » l'agent Hermes dessus directement. L'abonnement est déjà
exploité là où c'est possible : le Codex CLI s'y connecte (`codex login`),
et la CI l'utilise via `CODEX_AUTH_JSON` (décision du 2026-08-12 : quota
d'abonnement, jamais de crédit API). Trois options pour Hermes, à trancher :

- **statu quo** : Hermes garde son fournisseur de modèle actuel ;
- **délégation** : pour les analyses lourdes, Hermes appelle le Codex CLI
  local (connecté au compte ChatGPT) comme outil — l'abonnement Plus est
  alors réellement exploité, sans clé API ;
- **clé API OpenAI** : dépense à l'usage, nouvelle par rapport à la
  décision « jamais de crédit API » (qui couvrait la CI) — à arbitrer
  explicitement si choisie.

## Arbitrages demandés au propriétaire

1. Valider **H1** (configuration locale seulement — peut commencer sans
   toucher au dépôt).
2. Autoriser le CTO (Claude) à écrire les briefs **H2** et **H3** depuis la
   roadmap.
3. Trancher **H4** : Hermes console du propriétaire, périmètre des quatre
   actions, ADR-0011 à écrire avant tout câblage.
4. Trancher **H5** : statu quo, délégation Codex CLI, ou clé API.
5. Confirmer (ou avancer) la **sortie de phase « shadow »** de l'Hermes
   local, prévue le 2026-08-24 par sa configuration.

## Décision du propriétaire (2026-08-12)

« **Ok pour tout** » — les cinq arbitrages sont tranchés dans le sens
recommandé, dans la même session :

1. **H1 accepté** : brancher l'Hermes local en lecture (configuration
   locale, hors dépôt).
2. **H2 et H3 acceptés** : le CTO (Claude) écrira les deux briefs depuis la
   roadmap.
3. **H4 accepté avec ses limites** : décision enregistrée dans
   [ADR-0011](../../docs/adr/0011-hermes-console-du-proprietaire.md) —
   quatre actions, ordre explicite, confirmation, jeton minimal, trace.
4. **H5 tranché : délégation** — Hermes garde son fournisseur actuel et
   délègue les analyses lourdes au Codex CLI local connecté au compte
   ChatGPT (pas de clé API nouvelle).
5. **Phase « shadow » confirmée** jusqu'au 2026-08-24, avançable une fois
   H1 et ADR-0011 en place.

Reflet dans `ROADMAP.md` § « Prochaines étapes » (même session).

## Ce que cette demande ne change pas

Le brief reste la seule source d'instruction d'un Générateur ; un fichier
Hermes reste une entrée pour le CTO, jamais une instruction exécutable ;
le tableau de bord reste une vue calculée depuis les sources de vérité,
jamais une base parallèle ; les chemins `hermes/**` restent hors allowlist
du merge-bot (relecture humaine obligatoire).
