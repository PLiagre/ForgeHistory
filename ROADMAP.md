# ROADMAP — ForgeHistory

> **Propriétaire de ce document : Hermes (chef de projet).** Toute évolution
> de la feuille de route passe par une demande écrite sous `hermes/requests/`
> (format : `hermes/README.md`), tranchée par le propriétaire, puis reflétée
> ici par Hermes. Personne d'autre ne réécrit ce fichier sur le fond ;
> une correction factuelle (statut devenu faux) est permise à tout acteur,
> en la signalant dans le message de commit.
>
> La vision produit (ce que le jeu **est**) vit dans [VISION.md](VISION.md)
> et prime en cas de conflit. Ce fichier dit **où on en est** et **dans quel
> ordre on avance** — jamais ce qu'il faut faire pour un brief donné (ça,
> c'est le brief lui-même, voir `CLAUDE.md` › Single Source of Instruction).

## Le jeu — cinq couches, dans l'ordre

Les couches viennent de `VISION.md` § « Roadmap par couches ». Statut au
2026-08-14 :

| # | Couche | Statut | Où ça vit |
|---|---|---|---|
| 1 | **Monde vivant** — carte, terrain, climat, ressources, population, économie locale, commerce | **commencé** : le pipeline géographique produit le littoral 1400, les cellules G3 et l'adjacence maritime G4 (brief 019 : zones de mer et graphe typé terre-terre / terre-mer / mer-mer / détroit — PR #105 fusionnée le 2026-08-14) ; la déclaration d'entrée du littoral G3 est alignée sur le fichier vivant (brief 020, PR #106, accepté, en attente de fusion) ; le moteur `sim/` est amorcé (brief 011), vit (brief 012, mesuré sur les 596 cellules réelles), compte juste (brief 013 : un kilogramme transféré ne nourrit qu'une fois ; brief 017, fusion des graines 015/016, PR #101 fusionnée le 2026-08-14 : seuil de survie honnête — prédiction stationnaire, accumulateur de mortalité, faim = pénurie, récupération physique) et agrège les terres en provinces dérivées (brief 018 : appartenance recalculée depuis les centroïdes, jamais un champ stocké — ADR-0003, PR #102 fusionnée le 2026-08-14) | `pipeline/geo/`, `sim/` |
| 2 | **Villes** — urbanisation, entreprises, métiers, routes, infrastructures | non commencé | `sim/` |
| 3 | **États** — fiscalité, lois, diplomatie, technologies, culture, religion | non commencé | `sim/` |
| 4 | **Armées** — recrutement, logistique, ravitaillement, stratégie | non commencé | `sim/` |
| 5 | **Batailles tactiques** — sur les mêmes données que tout le reste | non commencé | `sim/` |

**Depuis ADR-0016 (2026-08-20) : `sim/` est le produit vivant.** La
simulation doit tourner sans Unity (`python -m sim`). Les couches 2 à 5
s’écrivent dans `sim/`. Le client Unity (brief 003) est **en veille** :
référence visuelle gelée, pas une seconde simulation, pas de lots Unity
tant que le propriétaire ne le rouvre pas.

## Le projet — phases F

| Phase | Contenu | Statut |
|---|---|---|
| **F0** — Harnais | Trois rôles (Planificateur / Générateur / Évaluateur), gate mécanique `verdict_audit.py`, briefs 001→010, boucle d'audit Cursor, pipeline full-auto (FSM, orchestrateur, budgets) | **terminé** |
| **F1** — Fondations monde | Pipeline géographique (littoral `1400` ✓, cellules G3 ✓, adjacence maritime G4 ✓ brief 019 — PR #105 ; provenance G3 ✓ brief 020 — PR #106 ; fleuves G5 ✓ brief 021 — PR #107 ; déterminants physiques du climat C1 ✓ brief 025 — PR #123 fusionnée le 2026-08-21 ; relief G6 encore en PR #122 ; restent climat observé et ressources), portage Unity ✓ mais Unity en veille | **en cours** |
| **F2** — Moteur `sim/` couche 1 | Premier code de simulation : monde, terrain, population initiale amorcée historiquement (ADR-002), économie locale physique | **en cours** — briefs 011, 012, 013 livrés et fusionnés ; brief 014 (pipeline : contre-audit comme porte, refus fournisseur comme état) livré, accepté et fusionné le 2026-08-13 (PR #83) ; brief 017 (seuil de survie honnête, fusion des graines 015/016) livré, accepté et fusionné le 2026-08-14 (PR #101, sans squash) ; brief 018 (Province dérivée, ADR-0003) livré, accepté et fusionné le 2026-08-14 (PR #102, sans squash) ; les graines 015/016 ne s'exécutent plus (elles pointent vers 017) ; suites F2 moteur : aucune restante pour clôturer E2 ; reste F1 geo (relief, climat, ressources ; provenance G3 livrée par le brief 020, PR #106 fusionnée le 2026-08-14 ; fleuves G5 livrés par le brief 021, PR #107 fusionnée le 2026-08-15) |
| **F3+** — Couches 2 à 5 | Villes, États, Armées, Batailles — chaque couche émerge de la précédente | à venir |

## Le workflow — Hermes pilote (ADR-0013, ADR-0014, ADR-0016)

**Hermes est le cerveau opérationnel.** Il propose des améliorations, tient
la mémoire, cadance le travail (y compris un cron quotidien de lecture),
lance ForgePilot. Il n’écrit pas le code produit, ni un brief, ni un
verdict, et il ne fusionne pas.

Claude Code planifie et relit en lecture seule. Cursor exécute. Le
propriétaire fusionne.

La session s’ouvre par `hermes chat -s forgehistory-suivi`. Le produit se
lance par `python -m sim`.

**Le pilote de trois lots est clos depuis le bilan du `2026-08-19`**
(`hermes/reports/RAPPORT-20260819-bilan-pilote-forgepilot-021-023.md`). Les lots
`021`, `022` et `023` sont finalement tous acceptés. Le bilan propose de
conserver ForgePilot avec ajustements : budget Claude borné, verdict avant toute
proposition de fusion, mesure `reviewer low/high` et portabilité Windows à
traiter par des briefs distincts. Il constate sans la lisser l'entorse : le VPS
a précédé le bilan qui devait conditionner son choix.

Chaîne nominale pendant trois lots d'essai :

```
Propriétaire ──▶ Hermes léger                 point d'entrée et choix de la tâche
  ▼
Claude Code (lecture seule)                   plan pré-écrit et critères mesurables
  ▼
Cursor CLI (worktree agent/*)                 unique exécutant : code et tests
  ▼
CI portable                                   contrôles mécaniques ForgeHistory
  ▼ si le lot touche VictoriaCityLab / Unity
Worker Unity Windows                          commit exact, LFS, tests batchmode
  ▼
Claude Code (nouvelle invocation, lecture seule) revue du diff et des preuves
  ▼
Propriétaire                                  décision de fusion
```

L’ancien pipeline GitHub full-auto reste en `mode: manual` (archive
réversible). Pas d’auto-fusion. Un cron quotidien **de lecture / mesure /
proposition** est autorisé (`hermes/crons/`). Runbook lots :
`control-plane/README.md`. Contrat Hermes : `hermes/README.md`.

Unity 6000.0.43f1 reste installé nativement sous Windows. Un lot qui touche
VictoriaCityLab n'est jamais déclaré validé par les seuls contrôles Linux : il
attend un worker Windows dédié et une preuve Unity. Le contrat cible est décrit
dans `docs/operations/unity-windows-worker.md` ; son implémentation appartient à
une PR VictoriaCityLab séparée.

## Grandes étapes — jalons d'audit (ADR-0012)

Décision propriétaire du 2026-08-13
(`hermes/requests/DEMANDE-20260813-audit-par-grandes-etapes.md`) : l'audit
Cursor et le contre-audit Claude ne tournent plus à chaque PR — trop de
jetons, trop d'allers-retours — mais à la **clôture de chaque grande
étape**, marquée par un fichier `hermes/milestones/ETAPE-NN-<slug>.md`
fusionné sur `master` (contrat : `hermes/milestones/README.md`). Un audit
ponctuel reste possible à tout moment par `workflow_dispatch` (incident,
changement structurel entériné par ADR, doute).

| jalon | ce que l'étape doit réunir pour être close | statut |
|---|---|---|
| **E1 — Fondations monde complètes** (clôt F1) | relief, climat et ressources livrés par `pipeline/geo/` (en plus du littoral ✓, des cellules G3 ✓, de l'adjacence maritime G4 ✓ et des fleuves G5 ✓) ; artefacts consommables par `sim/` | **en cours** — déterminants physiques du climat C1 fusionnés (brief 025, PR #123, le 2026-08-21) ; relief G6 en PR #122 ; restent la fusion du relief, une source climatique réelle pour température/précipitations, les ressources et la consommation des artefacts récents par `sim/` |
| **E2 — Le monde vivant compte juste** (clôt F2, couche 1) | seuil de survie honnête (graines 015/016 traitées) ; agrégation Province dérivée (ADR-0003) ; monde mesuré stable et falsifiable sur les 596 cellules réelles | **clos** — critères réunis (017 + 018) ; la fusion de `hermes/milestones/ETAPE-02-monde-vivant-compte-juste.md` déclenche l'audit d'étape (ADR-0012) |
| **E3 — Villes** (couche 2) | urbanisation, entreprises, métiers, routes, infrastructures — émergeant de la couche 1 | à venir |
| **E4 — États** (couche 3) | fiscalité, lois, diplomatie, technologies, culture, religion | à venir |
| **E5 — Armées** (couche 4) | recrutement, logistique, ravitaillement, stratégie | à venir |
| **E6 — Batailles + rendu branché** (couche 5) | batailles tactiques sur les mêmes données ; Unity rend l'état du moteur (client mince, zéro logique) | à venir |

Moments cruciaux supplémentaires (audit par `workflow_dispatch`, sans
jalon) : tout changement structurel du harnais ou du pipeline entériné par
un ADR ; tout incident de boucle (perte de données, garde contournée) ;
toute veille de décision irréversible du propriétaire.

## Prochaines étapes (dans l'ordre)

1. **Produit :** le lot 025 C1 est fusionné et le lot 026 ressources est prêt
   sous condition désormais satisfaite. Examiner d'abord la PR #122 du relief
   G6 encore ouverte, puis lancer le lot 026. Le climat observé (température,
   précipitations, saisons) exige encore une source réelle et licenciée.
   Chaque couche se **joue** par `python -m sim`, jamais par Unity tant qu’il
   est en veille.
2. **Hermes :** installer le cron quotidien (`hermes/crons/README.md`) sur
   le VPS ; lire la veille locale `hermes/propositions/DERNIERE-VEILLE.md`
   (gitignorée, le cron ne sale pas le dépôt) ; ouvrir des propositions
   au lieu d’attendre qu’on lui demande une feuille de route.
3. Authentifier Claude Code (abonnement Pro, **pas** `ANTHROPIC_API_KEY`)
   et Cursor. `forgepilot doctor`.
4. Refuser tout lot Unity / CityLab tant que le propriétaire n’a pas
   réveillé le visuel.
5. Ne réactiver `mode: full_auto` que par une nouvelle décision écrite.
6. Les lots ForgePilot `021`–`023` sont livrés, verdicts `022`/`023`
   ACCEPT. Un rapport de bilan reste utile ; il n’est plus un verrou.

## Historique des révisions

| date | auteur | changement |
|---|---|---|
| 2026-08-12 | hermes (rédaction initiale déléguée à Cursor, décision propriétaire) | création — état F0/F1, couches jeu, workflow quatre acteurs |
| 2026-08-12 | hermes (rédaction déléguée à Cursor, décision propriétaire « ok pour tout ») | reflet de la demande « tableau de bord unique et pilotage » (H1-H5, ADR-0011) ; correction factuelle : secrets CI provisionnés |
| 2026-08-12 | orchestrateur Cursor (remplaçant du CTO Claude, indisponible — instruction propriétaire) | correction factuelle uniquement : brief 011 (F2, amorçage `sim/`) livré et accepté — statuts couche 1, F2 et étape 4 mis à jour |
| 2026-08-13 | hermes (rédaction déléguée à l'orchestrateur Cursor, décision propriétaire — `DEMANDE-20260813-audit-par-grandes-etapes.md`) | audit/contre-audit par grandes étapes (ADR-0012) : section « Grandes étapes — jalons d'audit » (E1-E6), chaîne quatre acteurs mise à jour (Cursor audite les jalons, plus chaque PR) |
| 2026-08-14 | hermes (rédaction déléguée, décision propriétaire — `DEMANDE-20260814-pilote-forgepilot.md`) | pilote ADR-0013 corrigé : Hermes léger, Claude Code Pro plan/revue en lecture seule, Cursor unique exécutant ; ancien full-auto en mode manuel |
| 2026-08-14 | hermes (rédaction déléguée, décision propriétaire) | hébergement progressif : trois lots locaux, VPS 4 Go seulement si concluant ; Render écarté pour Hermes |
| 2026-08-14 | hermes (rédaction déléguée, correction propriétaire — `DEMANDE-20260814-worker-unity-windows.md`) | correction de plateforme : Unity reste sous Windows ; pilote local Windows/WSL2, puis VPS facultatif + worker Unity Windows manuel et bloquant |
| 2026-08-16 | hermes (rédaction déléguée à Claude Code, rattrapage demandé par le propriétaire) | correction factuelle uniquement, aucune décision nouvelle : PR #106 fusionnée le 2026-08-14 (le texte la disait « en attente ») ; fleuves G5 livrés et fusionnés (brief 021, PR #107) ; état du pilote ajouté aux prochaines étapes (lot 022 fusionné sans verdict, brief 023 non lancé, ADR-0014 `proposed`). Rapports adossés : `hermes/reports/RAPPORT-20260816-*.md` |
| 2026-08-16 | hermes (rédaction déléguée à Claude Code, décision propriétaire — `DEMANDE-20260815-hermes-cerveau-du-pipeline.md`) | **ADR-0014 accepté** : Hermes déclenche et rend compte, Claude juge à la demande, Cursor exécute, le propriétaire garde le veto sur la fusion. Section « Workflow pilote » mise à jour ; point d'entrée unique `forge-start` puis `hermes chat -s forgehistory-suivi` |
| 2026-08-19 | hermes | bilan obligatoire des lots pilotes `021` à `023` écrit : pilote clos, trois verdicts finaux acceptés, proposition de conserver ForgePilot avec ajustements ; dette VPS avant bilan déclarée, budget Claude et ADR-0015 laissés à la décision du propriétaire |
| 2026-08-20 | hermes (décision propriétaire — `DEMANDE-20260820-abandon-budget-claude.md`) | abandon de l'enveloppe mensuelle Claude et de la cadence associée comme préalable ; les limites fournisseur restent des états opérationnels à signaler |
| 2026-08-20 | cursor-cloud (décision propriétaire — `DEMANDE-20260820-simulation-sans-unity-hermes-pilote.md`) | **ADR-0016** : `sim/` sans Unity est le produit vivant ; Unity en veille ; Hermes pilote et propose ; crons quotidiens de lecture autorisés. ADR-0015 accepté. |
| 2026-08-20 | cursor-cloud (ordre propriétaire) | `forgepilot enchaine` : un brief, un `--run`, draft PR, pas de fusion (ADR-0013 amendement 002). |
| 2026-08-21 | hermes | brief 025 C1 fusionné par PR #123 : insolation, durées de jour, continentalité et preuves déterministes livrées ; climat observé toujours ouvert ; brief 026 débloqué par la fusion |
