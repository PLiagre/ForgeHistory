---
author: hermes
kind: demande
created_at: 2026-08-23T19:20:00Z
concerns: workflow
status: CLOSED
---
# Réduire le coût d'un lot : la cérémonie de preuve et les relances du pilote

> **Archive close le 2026-08-30.** La réinitialisation de gouvernance a rendu
> les portes, revues séparées et cérémonies de preuve facultatives. Cette
> demande ne gouverne plus le workflow courant.

Mise en forme par Claude (CTO) d'une décision prise par le propriétaire après
lecture de la PR #131 (`026-geo-gisements-1400-r1`). Les chiffres ci-dessous
sont mesurés sur le dépôt, pas estimés.

## Ce qui a été constaté

**La dernière PR a coûté 5 141 lignes ajoutées pour poser 27 gisements sur
25 cellules.** Répartition :

| part | lignes | quoi |
|---|---|---|
| paperasse du harnais | 3 273 (64 %) | dont **2 405 lignes de copies `.orig`** de fichiers déjà suivis par git |
| code produit | ~1 000 | l'étape, les constantes, le crochet, le fichier de données |
| contrôles et preuves | ~1 100 | `checks_r1.py`, `run_proof_r1.py`, `test_qa_red_r1.py`, `measure_r1_026.py` |

En amont : 1 139 lignes de brief + 510 de rubrique + 210 d'amendement. Le
brief contenait **déjà** la table des 27 gisements avec leurs coordonnées
(section D4).

**Le temps et la dépense ne sont pas partis dans les tests.** Le 23/08, de
13:24 à 18:42 (5 h 20), 16 commits : **12 réparent ForgePilot en direct**
(lire les briefs longs, extraire le JSON de Grok, reprendre une itération
archivée, router les tests, transmettre le bundle par chemin), 2 sont le lot,
1 est du routage. Chaque panne de plomberie relance un lot R2 complet — plan,
exécution, jugement — et chaque relance est payée.

**Déséquilibre de fond**, en lignes suivies par git :

| zone | lignes |
|---|---|
| `harness/queue/briefs/` (briefs + livrables) | 186 533 |
| `architecture/` | 33 933 |
| `pipeline/geo/` étapes + contrôles | 12 247 |
| `sim/` — le produit vivant | 6 812 |

## Décision 1 — Un changement du pilote ne se répare jamais pendant un lot payant

Toute modification de ForgePilot, du harnais ou d'un backend Générateur passe
d'abord par `harness/demo/fake_brief_001`, qui ne coûte rien. Un lot n'est
lancé qu'une fois le pilote vert sur le faux brief.

C'est la mesure qui économise le plus : elle supprime la cause des relances,
pas leur symptôme.

## Décision 2 — Plus de copie `.orig` d'un fichier suivi par git

**Appliquée.** La porte mécanique accepte désormais
`must_differ_from_git: "<rev>:<path>"` dans `deliverables/manifest.json` : git
détient déjà chaque état pré-édition, une copie committée à côté du manifeste
n'ajoute rien. La forme chemin-à-chemin reste admise pour un état que git ne
détient pas (une capture prise avant le run).

Les contrats de rôle (`forge-planificateur`, `forge-generateur`,
`harness/backends/README.md`) exigent maintenant la forme git dès que git
suit le fichier.

**Défaut trouvé en chemin, et corrigé.** Les trois couples `must_differ_from`
du lot 026 ne prouvaient rien : leur manifeste déclare des chemins depuis la
racine du dépôt alors que la porte les résout depuis le répertoire du brief.
Aucun des deux côtés n'était trouvé, et le garde `if p1.exists() and
p2.exists()` transformait ce silence en `PASS`, avec la mention « all declared
pairs differ ». Les 2 405 lignes de copies du dernier lot étaient donc
inertes. Un couple non comparé est désormais un échec nommé (règle n° 7 :
présence n'est pas fonction), prouvé rouge avant d'être prouvé vert.

Les copies déjà committées dans les lots scellés ne sont **pas** supprimées :
ce sont des livrables déclarés de verdicts rendus, et les effacer casserait la
re-vérifiabilité de lots clos. La duplication s'arrête, elle ne se réécrit
pas.

## Décision 3 — Factoriser les contrôles, à ouvrir en lot

`pipeline/geo/tests/` : 19 fichiers, 4 605 lignes, qui refont à chaque lot les
mêmes cinq familles de vérification — contenance, déterminisme, ajout seul,
vocabulaire fermé, réversibilité. Un lot devrait n'écrire que ses données et
ses paramètres.

À ouvrir comme brief : `qa/checks_common.py` paramétré + un lanceur de cas
rouges piloté par table. Ce n'est pas une réduction du niveau de preuve : les
mêmes contrôles tournent, écrits une fois.

## Décision 4 — Rebrancher une mesure de la dépense

`harness/queue/cost-ledger.jsonl` n'enregistre que des événements
`generator-run` : ni tokens, ni coût. `hermes/DASHBOARD.md` affiche
« 0.0 USD sur 0 invocation » parce qu'il ne compte que la CI. La dépense réelle
d'un lot n'est donc visible nulle part — c'est pourquoi celle du lot 026 a
surpris le propriétaire.

La demande `DEMANDE-20260820-abandon-budget-claude` retirait le **budget**
comme préalable de pilotage ; elle ne demandait pas de cesser de **mesurer**.
Il faut un compteur, sans plafond bloquant : coût constaté par lot, écrit dans
le ledger, remonté au tableau de bord.

## Ce qui n'est pas demandé

Réduire le nombre de contrôles métier. Ils empêchent l'invention silencieuse
(règle n° 10), qui est le vrai risque avec un exécutant automatique. Le
problème n'est pas qu'il y ait trop de contrôles, c'est qu'ils soient
**réécrits à neuf à chaque lot** et enveloppés d'une cérémonie qui, dans le
dernier lot, ne vérifiait rien.
