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

Les couches viennent de `VISION.md` § « Roadmap par couches ». Statut au 2026-08-25,
après le dégraissage (ADR-0018) :

| # | Couche | Statut | Où ça vit |
|---|---|---|---|
| 1 | **Monde vivant** — carte, terrain, climat, ressources, population, économie locale, commerce | **commencé** | `data/`, `sim/`, `viewer/` |
| 2 | **Villes** — urbanisation, entreprises, métiers, routes, infrastructures | non commencé | `sim/` |
| 3 | **États** — fiscalité, lois, diplomatie, technologies, culture, religion | non commencé | `sim/` |
| 4 | **Armées** — recrutement, logistique, ravitaillement, stratégie | non commencé | `sim/` |
| 5 | **Batailles tactiques** — sur les mêmes données que tout le reste | non commencé | `sim/` |

**Couche 1 — état vrai** (après le dégraissage ADR-0018, 2026-08-25)

- La carte est **figée** : `data/world-1400.json`, un seul fichier lu par
  `sim/`. Elle porte 596 cellules, 1 364 arêtes d'adjacence, le relief en
  cinq classes, les déterminants du climat et 27 gisements nommés de 1400.
- `tools/map/` (ex-`pipeline/geo/`) est l'outil qui fabrique la carte. Il
  est hors du chemin quotidien : on ne le ressort que pour refaire la carte.
- `sim/` : amorçage, tick, commerce, survie, province dérivée, snapshot
  `v0a-2`. Le tick **ne joue encore aucune des trois couches** (relief,
  climat, gisements) : le snapshot le dit lui-même, couche par couche.
- `viewer/` : regard mince, preuve SVG.
- Unity : archivé, au commit `da1596d` (le tag `archive/2026-08` n'a
  jamais pu être poussé — voir `AGENTS.md` § « Les archives »).

## Le projet — phases F

| Phase | Contenu | Statut |
|---|---|---|
| **F1** — Fondations monde | carte figée complète : littoral, cellules, adjacence, relief, climat, gisements | **terminée** |
| **F2** — Moteur `sim/` couche 1 | amorçage, tick, survie, province, snapshot | **en cours** — reste à faire jouer le relief et les gisements par le tick |
| **F3+** — Couches 2 à 5 | Villes, États, Armées, Batailles | à venir |

## Le workflow — trois acteurs (ADR-0018)

> Hermes écrit un brief → Cursor l'exécute et ouvre une PR → les tests
> passent et la porte mécanique vérifie le compte-rendu → le propriétaire
> fusionne.

- **Hermes** (Sol 5.6, VPS) : roadmap, suivi, **écrit les briefs**, lance
  Cursor, mesure. Ne code pas, ne fusionne pas, ne juge pas.
- **Cursor** (Grok 4.6 pour le plan, Composer pour le code) : exécute,
  ouvre la PR, se relit dans une invocation neuve.
- **Claude** (à la demande) : architecte du modèle (`sim/MODELE.md`), et
  regard de dernier recours quand un lot ne converge pas en trois
  itérations. Hors du harnais, sans cron ni agent.

Règle de fond conservée : celui qui produit ne prononce pas la recevabilité
de son propre travail.

Le déroulé d'un lot, étape par étape et commande par commande, est dans
[`docs/MODE-EMPLOI.md`](docs/MODE-EMPLOI.md).

Trois workflows GitHub : les tests, le scan de sécurité, et le ping du
worker PC (`workflow_dispatch` seulement, ADR-0020). Il n'y a plus de
pipeline full-auto, plus de bot de fusion, plus de machine d'états d'audit.

## Prochaines étapes (dans l'ordre)

1. **Produit :** faire jouer le relief par le tick — le rendement d'une
   cellule de montagne n'est pas celui d'une plaine. C'est le premier lot
   qui rend la carte vivante plutôt que décorative. Il se fait à un seul
   endroit, `production_kg()` dans `sim/engine.py` : le plafond physique de
   survie appelle la même fonction et suit tout seul, donc les tests de
   survie n'ont pas à changer. Déroulé complet dans
   [`docs/MODE-EMPLOI.md`](docs/MODE-EMPLOI.md).
2. **Puis :** les gisements consommés par l'économie.
3. **Hermes :** cron quotidien de lecture et de mesure ; une proposition
   seulement s'il y a un constat nouveau.

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
| 2026-08-23 | cursor-cloud (décision propriétaire — ADR-0017) | Grok 4.6 planifie et juge la PR finale ; Composer 2.5 code ; Claude Opus 5 témoin rare ; `forgepilot merge` si PASS + checks verts. Hermes principal : `openai/gpt-5.4`. |
| 2026-08-23 | hermes (correction factuelle après #130 et preuve VPS) | cache Copernicus complet vérifié `1110/1110` ; preuve Europe G6 verte et déterministe. Le relief est calculé mais reste `not_consumed` par `sim/`. Le prochain pas unique reste le brief 026. |
| 2026-08-25 | claude (correction factuelle, ADR-0018) | dégraissage : trois acteurs, carte figée, phases F et prochaines étapes réécrites sur l'état réel |
| 2026-08-25 | claude (**correction factuelle uniquement**, aucune décision nouvelle) | deux affirmations devenues fausses : le tag `archive/2026-08` n'existe pas sur `origin` (403 au push, deux sessions) — le commit `da1596d` le remplace ; et le prochain lot ne demande plus de re-dériver un modèle analytique de survie, celui-ci ayant été retiré au profit de trois propriétés mesurées. Renvoi ajouté vers `docs/MODE-EMPLOI.md`. |
| 2026-08-26 | cursor (correction factuelle, ADR-0020 proposed) | un troisième workflow GitHub : ping worker PC en `workflow_dispatch` seulement ; ce n'est pas le retour du full-auto |
