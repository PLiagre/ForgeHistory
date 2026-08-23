---
author: forge-planificateur
kind: proposition
created_at: 2026-08-23T10:00:00Z
concerns: couche 1, jalon E1
status: OPEN
---
# Plan de relance de la couche 1 — gisements d'abord, blocages déclarés

Cette proposition vient d'une session Planificateur (Fable) du 2026-08-23.
Elle n'est **pas** un brief : elle ne porte aucune condition de succès
d'exécutant ni aucune consigne de code. Elle dit ce qui a été mesuré, ce que
le propriétaire peut demander, et où sont les briefs.

Tout a été mesuré par commande sur le dépôt du jour (`master`, après la
fusion de #127), avec `.venv/bin/python`. Aucun chiffre n'est un souvenir.

---

## A. Diagnostic — où en est la couche 1 (monde vivant)

Mesures du jour : le monde s'amorce (`.venv/bin/python -m sim --ticks 0
--json` : 596 cellules, 66 649 511 habitants), 5 ticks consomment du stock
sans perte de population. Le snapshot `v0a-1` (`--snapshot-json`) publie
trois couches : `climate_drivers_c1` = `present`, `relief_g6` =
`not_consumed`, `resources_r1` = `absent`. Sur le disque : artefacts G3, G4,
G5, G6, C1 présents ; R1 absent ; cache DEM absent
(`sources/dem_cache` inexistant, variable d'environnement non définie).

| besoin couche 1 (mots de VISION) | fichier / module aujourd'hui | joué par sim/ à chaque tick ? | trou honnête | lot ou BLOCAGE |
|---|---|---|---|---|
| carte (cellules, adjacence) | `cells_g3.json`, `adjacency_g3.json` — consommés par `sim/world.py` | **oui** (le commerce parcourt les arêtes G3) | aucun | rien — clos |
| carte (mer, fleuves) | `sea_zones_g4.json`, `rivers_g5.json`, `adjacency_g5.json` — livrés | non (le commerce ignore le type des arêtes) | l'échange ne distingue pas terre, mer, fleuve | plus tard — après R1, et après décision sur ce que le tick lit (question D3) |
| terrain (relief, barrière, col) | `cells_relief_g6.json` — livré, snapshot `not_consumed` | non | 473 cellules à zéros non mesurés ; preuve Europe impossible sans cache Copernicus (DEM_CACHE absent, mesuré) | **BLOCAGE** — question D2 |
| climat | `cells_climate_drivers_c1.json` — joint au snapshot (`present`) | non (le tick n'en lit rien) | déterminants livrés, mais ni température, ni précipitations, ni saisons : aucune source dans le dépôt | **BLOCAGE** pour l'observé — question D1 |
| ressources (ce que la terre donne) | **rien** — `cells_resources_r1.json` absent ; brief 026 prêt, arbitrage rendu | non | la couche n'existe pas | **lots 026 → 030 → 031** (la file B) |
| population | `sim/world.py` — amorçage proxy déclaré (`sim/SEEDING.md`) | **oui** (consommation, faim, mortalité) | l'amorçage est un proxy paramétrique, pas une donnée historique — c'est déclaré, pas caché | pas de lot — question D4 |
| économie locale (nourriture) | `sim/engine.py` — production, consommation, dette, mortalité | **oui** | la production est uniforme au km², aveugle au terrain, au climat et aux ressources | pas encore un lot : exige climat ou sol (bloqués) ou la décision D3 |
| commerce | `_apply_commerce` — physique, borné, conservatif | **oui** (1 arête par kg et par tick, capacité lue) | le coût ignore relief et voies d'eau | plus tard — dépend des lignes mer/fleuves/relief ci-dessus |

**Où le jeu façon Victoria en est vraiment.** Le monde tourne sans Unity et
compte juste : les gens mangent, échangent, ont faim, meurent, et la
province n'est qu'une vue dérivée. C'est le socle, et il est sain. Mais la
vie d'un habitant de 1400 y est encore celle d'un plat pays uniforme : sa
terre ne donne rien de particulier (aucun gisement), son relief n'existe pas
honnêtement, son climat n'a ni hiver ni pluie, et son commerce ne sait pas
qu'un fleuve porte mieux qu'un col. Le trou le plus mûr — le seul qui peut
se fermer sans mentir et sans nouvelle source — est celui des ressources :
le brief 026 est écrit, arbitré, et ses deux préalables sont **satisfaits
aujourd'hui** (vérifiés par commande : l'amendement d'arbitrage est suivi
par git, et `WORLD_TERMS_FORBIDDEN_KEYS` existe dans
`pipeline/geo/constants.py`). Après 026, il faut que le monde **lise** ce
que la géographie déclare, puis que le regard le montre — c'est la file B.

**Déjà clos, à ne plus retravailler** : le jalon E2 (survie honnête,
tick-nourriture, province dérivée — lots 017 et 018), le snapshot `v0a-1`
(027) et le regard mince (028).

**Deux écarts constatés, sans lot à ouvrir ici** : `VISION.md` pointe vers
`docs/adr/ADR-001-moteur-vivant-lod.md` et
`docs/adr/ADR-002-monde-amorce-historiquement.md`, qui n'existent pas dans
le dépôt (héritage VictoriaProject non migré — la doctrine vit dans le corps
de `VISION.md`). Et les verdicts des lots 027 et 028 sont `PENDING` : dette
de revue, pas trou de monde (question D5).

---

## B. La file de lots, dans l'ordre

Un seul lot à la fois. Horizon : fermer ce qui peut l'être de la couche 1 et
le rendre visible. Rien des couches 2 à 5.

| n° | id | slug | objectif (termes de monde) | dépendances | sous-système | brief | décision propriétaire | validation | estimation appels | risque |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 026 | geo-gisements-1400-r1 | la terre déclare ses gisements de 1400 — présence, nature, classe, jamais une quantité | aucune (préalables vérifiés satisfaits le 2026-08-23) | pipeline/geo | `harness/queue/briefs/026-geo-gisements-1400-r1/brief.md` (existant — non réécrit) | aucune (arbitrage A1/A2/A3 rendu le 2026-08-21) | `../../.venv/bin/python tests/run_proof_r1.py` depuis `pipeline/geo/` (code 0) | 120 (SIZE_OK mesuré) | R1 |
| 2 | 030 | sim-lit-gisements-r1 | le monde lit ce que sa terre donne et refuse d'inventer si la donnée manque | 026 fusionné | sim | `harness/queue/briefs/030-sim-lit-gisements-r1/brief.md` (écrit ce jour) | aucune (lecture pure — l'usage par le tick reste la question D3) | `.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json /tmp/v0a2.json` puis lecture du statut de couche | 90 (SIZE_OK mesuré) | R1 |
| 3 | 031 | viewer-couche-gisements-r1 | le regard montre les gisements photographiés — trois états distincts, jamais une grandeur | 030 fusionné (et question D5 si le propriétaire l'antépose) | viewer | `harness/queue/briefs/031-viewer-couche-gisements-r1/brief.md` (écrit ce jour) | aucune | `.venv/bin/python -m viewer --snapshot /tmp/v0a2.json --proof-svg /tmp/carte.svg` (code 0) | 80 (SIZE_OK mesuré) | R1 |
| — | L4 | climat-observe | **BLOCAGE** : aucune source de température/précipitations licenciée dans le dépôt. Aucun brief n'est écrit — en écrire un présupposerait la réponse | décision D1 | pipeline/geo | à écrire après D1 | **D1** | — | — |
| — | L5 | sim-consomme-g6 | **BLOCAGE** : cache Copernicus absent de cette machine (mesuré), preuve Europe impossible. Déblocage : poser le cache complet là où les lots tournent, rejouer la preuve G6 Europe en vrai, puis seulement un lot « sim lit G6 » | décision D2 (opérationnelle) | pipeline/geo puis sim | à écrire après D2 | **D2** | — | — |
| — | L6 | population-familles | **non** : aucun trou de couche 1 ne l'exige aujourd'hui ; l'amorçage proxy est déclaré et le moteur compte juste avec lui | — | — | — | D4 (source seulement) | — | — | — |
| — | L7 | villes-etats-armees-batailles-unity | **non** : couches 2 à 5, et Unity en veille (ADR-0016) | — | — | — | aucune à poser | — | — | — |

Le premier lot est exécutable demain matin sans le Planificateur :
`forgepilot enchaine harness/queue/briefs/026-geo-gisements-1400-r1/brief.md
--repo . --run` (plan → execute → draft PR → review, **pas de fusion**).
Le pipeline GitHub full-auto reste en `mode: manual` : « full-auto » signifie
ici qu'Hermes enchaîne jusqu'à la PR brouillon, jamais qu'une fusion se fait
seule.

---

## C. Les briefs écrits ce jour (pointeurs)

- `harness/queue/briefs/030-sim-lit-gisements-r1/` — brief + rubrique.
- `harness/queue/briefs/031-viewer-couche-gisements-r1/` — brief + rubrique.

Chacun porte son « Bloqué tant que » mécanique (constaté par commande, arrêt
sans production sinon) : 030 attend la fusion de 026, 031 attend celle de
030. Le brief 026 existant n'a pas été touché.

---

## D. Décisions qui appartiennent au propriétaire

Le Planificateur ne tranche pas. Chaque question est posée pour qu'une
réponse courte débloque.

| id | question (oui / non / reformulation) | pourquoi ce n'est pas déductible | ce qui reste bloqué tant que ce n'est pas tranché | brief impacté |
|---|---|---|---|---|
| D1 | Quelle source, et quelle licence, pour la température et les précipitations autour de 1400 — ou quel proxy déclaré (par exemple un climat moderne assumé comme proxy, dit tel quel) ? | Aucune source climatique n'est déclarée dans `pipeline/geo/sources.lock` ; en inventer une serait la donnée fabriquée en silence (règle n° 10). Le choix d'une source et de sa licence est un choix produit | tout brief « climat observé », et par ricochet les ressources agricoles/forestières (026 les exclut explicitement faute de climat et de sol) | L4 (à écrire) |
| D2 | Qui pose le cache DEM Copernicus complet, où (VPS, machine locale), et quand ? | La décision produit existe déjà (`hermes/requests/DEMANDE-20260821-couverture-dem-complete-g6.md`, CLOSED : couverture complète, pas de repli) ; ce qui manque est opérationnel — le cache n'est sur aucune machine mesurée (`DEM_CACHE=non` ce jour) | la preuve Europe G6, donc tout lot « sim lit G6 » ; le relief reste `not_consumed` et le terrain n'est pas jouable | L5 (à écrire) |
| D3 | Un gisement lu par `sim/` porte-t-il seulement présence + nature + classe (état après le lot 030), ou le tick a-t-il le droit d'en dériver un flux extractif **qualitatif** (jamais un tonnage — A3 de 026 tient) ? | L'amendement 001 du brief 026 (§5, point 1) laisse explicitement ouvert « comment sim/ lira la classe ». Décider comment une classe pèse sur le monde est une décision de jeu, pas d'ingénierie | le premier lot où le tick **se sert** des gisements (production locale, spécialisation, commerce de biens non alimentaires) ; le lot 030 (lecture pure) n'est **pas** bloqué | le lot d'après 030/031, non écrit |
| D4 | L'amorçage de la population reste-t-il un proxy paramétrique (déclaré dans `sim/SEEDING.md`) jusqu'à ce qu'une source historique soit choisie — ou le propriétaire a-t-il une source en tête que le dépôt ignore ? | `sim/SEEDING.md` déclare le proxy et prévoit une calibration « par un brief ultérieur disposant de données historiques réelles » ; choisir cette source est un choix produit | rien d'immédiat — le monde compte juste avec le proxy ; seule la crédibilité historique de t=0 attend | aucun brief tant que la réponse est « proxy » |
| D5 | Faut-il rendre les verdicts des lots 027 et 028 (`PENDING`, évaluateur jamais passé) avant de lancer le prochain lot visuel (031), ou la première tranche V0 livrée suffit-elle ? | La dette de revue est un fait (`ROADMAP.md`, jalon V0) ; arbitrer entre payer la dette et avancer est une décision de cadence qui appartient au propriétaire | rien mécaniquement — mais si la réponse est « d'abord les verdicts », le lot 031 recule d'un cran derrière ce pas de revue | 031 (ordre seulement) |

Aucune de ces questions n'est déjà tranchée dans `hermes/requests/` : la
seule décision voisine trouvée est celle de D2 (couverture DEM, produit
tranché, opération restante), citée dans le tableau.

---

## Ce que cette proposition ne demande pas

Pas de lot Unity ou CityLab, pas de « G6 Europe PASS » sans cache, pas de
température inventée, pas de tonnage de gisement, pas de couche 2 à 5, pas
de réactivation du `mode: full_auto`, pas de réécriture de `ROADMAP.md` (il
appartient à Hermes, qui y reflétera l'ordre ci-dessus si le propriétaire le
valide).
