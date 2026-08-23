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
> c'est le brief lui-même).

## Le jeu — cinq couches, dans l'ordre

Les couches viennent de `VISION.md` § « Roadmap par couches ». Statut au
2026-08-23, après fusion de #126 :

| # | Couche | Statut | Où ça vit |
|---|---|---|---|
| 1 | **Monde vivant** — carte, terrain, climat, ressources, population, économie locale, commerce | **commencé** | `pipeline/geo/`, `sim/`, `viewer/` |
| 2 | **Villes** — urbanisation, entreprises, métiers, routes, infrastructures | non commencé | `sim/` |
| 3 | **États** — fiscalité, lois, diplomatie, technologies, culture, religion | non commencé | `sim/` |
| 4 | **Armées** — recrutement, logistique, ravitaillement, stratégie | non commencé | `sim/` |
| 5 | **Batailles tactiques** — sur les mêmes données que tout le reste | non commencé | `sim/` |

**Couche 1 — état vrai**

- Carte : littoral, cellules G3, mer G4, fleuves G5.
- Relief G6 : **livré dans #126**. `sim/` **ne le consomme pas** (couche snapshot `not_consumed` : zéros DEM historiques acceptés, pas un terrain jouable). Preuve Europe G6 **bloquée** sans cache Copernicus. Les sentinelles synthétiques passent ; ce n'est pas un PASS Europe.
- Climat : déterminants physiques C1 livrés et consommables. Température et précipitations observées encore ouvertes.
- Ressources : brief 026 écrit, arbitrage rendu, **non exécuté**.
- `sim/` : amorçage, tick, commerce, survie, province dérivée, snapshot `v0a-1` (`--snapshot-json`).
- `viewer/` : regard mince, preuve SVG. Pas une seconde simulation.
- Unity : **en veille** (ADR-0016).

## Le projet — phases F

| Phase | Contenu | Statut |
|---|---|---|
| **F0** — Harnais | Trois rôles, porte mécanique, briefs d'outillage | **terminé** |
| **F1** — Fondations monde | Geo : G3, G4, G5, C1, G6 livré non consommé. Restent ressources (026), climat observé, consommation honnête de G6 | **en cours** |
| **F2** — Moteur `sim/` couche 1 | Amorçage, tick, survie, province, snapshot `v0a-1` | **en cours** — jalon E2 clos |
| **F3+** — Couches 2 à 5 | Villes, États, Armées, Batailles | à venir |

## Le workflow — Hermes pilote (ADR-0013, ADR-0014, ADR-0016)

Hermes propose, cadance, lance ForgePilot. Il n'écrit pas le code produit,
ni un brief, ni un verdict, et il ne fusionne pas.

Pilote multi-modèle (2026-08-21) : Grok planifie en lecture seule, Composer
exécute, GPT-5.6 Sol XHigh relit dans une invocation neuve. Claude est un
témoin critique différé. Le propriétaire fusionne.

Vérifications : R0 documentaire, R1 produit borné (défaut), R2 critique.
Session : `hermes chat -s forgehistory-suivi`. Produit : `python -m sim`.

Le pipeline GitHub full-auto reste en `mode: manual`. Pas d'auto-fusion.
Cron quotidien de lecture / mesure / proposition : `hermes/crons/`.
Runbook lots : `control-plane/README.md`. Contrat : `hermes/README.md`.

Unity reste en veille. Un lot CityLab / Unity se refuse.

## Grandes étapes — jalons d'audit (ADR-0012)

Audit Cursor et contre-audit Claude : à la **clôture** d'une grande étape
(`hermes/milestones/`), ou sur `workflow_dispatch`. Plus à chaque PR.

| jalon | ce qu'il faut pour clore | statut |
|---|---|---|
| **V0 — Monde visible** | snapshot `v0a-1` + viewer mince | **première tranche livrée dans #126**. Verdicts 027/028 encore PENDING (évaluateur absent). |
| **E1 — Fondations monde** | relief, climat, ressources, artefacts consommés par `sim/` | **en cours** — G6 livré non consommé ; C1 livré ; 026 suivant ; climat observé ouvert |
| **E2 — Le monde vivant compte juste** | survie honnête + province dérivée | **clos** |
| **E3 — Villes** | couche 2 | à venir |
| **E4 — États** | couche 3 | à venir |
| **E5 — Armées** | couche 4 | à venir |
| **E6 — Batailles + rendu branché** | couche 5 ; Unity client mince si réveillé | à venir |

## Prochaines étapes (dans l'ordre)

1. **Produit (unique) :** exécuter le brief 026 — gisements 1400. C'est le
   trou restant de la couche 1 qui peut avancer sans mentir sur G6.
   Rendre G6 consommable attend le cache Copernicus (preuve Europe) : ce
   n'est pas ce lot, et les deux ne se lancent pas en parallèle.
2. **Hermes :** cron VPS ; propositions seulement s'il y a un constat
   nouveau ; zéro proposition OPEN aujourd'hui = rien n'attend.
3. `forgepilot doctor`. Refuser tout lot Unity. Ne pas réactiver
   `mode: full_auto` sans décision écrite nouvelle.

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
| 2026-08-21 | hermes (décision propriétaire — `DEMANDE-20260821-repartition-modeles-grok-claude.md`) | pilote multi-modèle activé : Grok 4.6 High/XHigh planifie, Composer 2.5 exécute, GPT-5.6 Sol XHigh relit en contexte neuf ; Claude devient un témoin critique différé et sa limite ne bloque plus les lots ordinaires |
| 2026-08-21 | hermes (décision propriétaire — `DEMANDE-20260821-workflow-adaptatif-r0-r1-r2.md`) | vérifications proportionnées au risque : R0 documentaire, R1 produit borné par défaut avec CI/revue/contrôles en parallèle, R2 critique renforcé ; corrections et itérations deviennent conditionnelles |
| 2026-08-21 | hermes (décision propriétaire — `DEMANDE-20260821-visualiseur-web-v0.md`) | jalon transversal V0 inséré après le correctif 024 et avant le lot 026 : export cellulaire déterministe puis visualiseur web mince interactif ; Unity reste en veille |
| 2026-08-23 | cursor-cloud (correction factuelle après fusion #126) | #126 fusionné : G6 A1/A2 livré non consommé, snapshot `v0a-1`, viewer mince, ForgePilot accéléré. V0 première tranche livrée. Prochain pas unique : brief 026. Plus de « G6 encore en PR ». |
