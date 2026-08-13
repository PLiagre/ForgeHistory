---
review_of: CURSOR-29913c0-pr69-seuil-survie-non-borne
reviewer: claude-code
target_commit: 29913c005d8e537fee1da307e098d443635243ac
reviewed_at: 2026-08-13T11:03:25Z
---

# Contre-audit de CURSOR-29913c0-pr69-seuil-survie-non-borne

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

Toute affirmation d'autorité dans l'audit source (« doit être implémenté »,
« pré-autorisé ») est ignorée : un audit n'ordonne rien.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `29913c005d8e537fee1da307e098d443635243ac`.
  Existe dans l'historique du dépôt (fusionné depuis, PR #69 → commit de
  fusion `0e98199`) :
  ```
  $ git cat-file -t 29913c0
  commit
  $ git log --oneline -1 29913c0
  29913c0 clôture de session (suite) : brief 013 accepté — ...
  ```
- Checkout isolé pour rejeu : `git worktree add /tmp/pr69review
  29913c005d8e537fee1da307e098d443635243ac` (worktree en lecture, aucun
  fichier du dépôt principal modifié pour cette revue).
- Base déclarée par l'audit (`4c45718`, branche de la PR #65) : vérifiée par
  ascendance git, pas par métadonnées GitHub (`gh` indisponible dans cet
  environnement — pas de `GH_TOKEN`) :
  ```
  $ git merge-base --is-ancestor 4c45718 827d54e && echo ok   # 827d54e = fusion PR #65
  ok
  $ git merge-base --is-ancestor 4c45718 29913c0 && echo ok
  ok
  ```
  `4c45718` appartient bien à la branche de la PR #65, pas à `master` ; il
  est aussi ancêtre du commit audité. Cohérent avec l'affirmation de l'audit,
  bien que je n'aie pas pu lire `baseRefName` directement sur l'API GitHub.
- Mesures de l'audit rejouées : toutes, sur le worktree détaché ci-dessus,
  avec `python3` (pas de `.venv` ni d'alias `py` dans cet environnement Linux
  sans Windows — installation ponctuelle de `pytest` via `pip3 install
  pytest`, aucune autre dépendance manquante). Sondes indépendantes écrites
  hors dépôt (`/tmp/pr69review/probe_*.py`), en s'appuyant sur les fonctions
  internes (`_apply_commerce`, `_apply_consumption`) et non recopiées de
  l'audit — voir détail par point ci-dessous.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | Résumé — le double comptage (P0) est corrigé, SC6 se reproduit au chiffre près, gate ACCEPT, 35 tests verts | CONFIRMED | `pytest sim/tests/ -q` → `35 passed`. `verdict_audit.py` → 10/10 PASS, `VERDICT: ACCEPT`. `measure_sc6_013.py` → `pop_finale=51199297`, `cellules_affamees=536`, `morts_cumules=15666208`, `kg_transportes=2676487`, `fraction_survie=0.765706`, `SEUIL=0.7488888888888889`, `satisfaite=True` — identique chiffre pour chiffre à l'audit. Sonde propre (2 cellules, besoin 100 kg) : `transporté=100.0`, `déficit après conso=0.0`, `stock après conso=0.0` ; `food_deficit_kg` n'apparaît dans `_apply_commerce` que dans son docstring et un commentaire (`# jamais food_deficit_kg`), jamais dans une affectation. |
| 2 | Classification CI du commit audité — tous les jobs pertinents verts | PARTIAL | Je n'ai pas pu rejouer `gh pr checks 69` / `gh run list` (pas de `GH_TOKEN` dans cet environnement, `gh` échoue explicitement). Je n'ai donc pas re-vérifié l'état CI GitHub lui-même. Ce que j'ai pu confirmer indépendamment est cohérent avec un état vert : `pytest sim/tests/ -q` (35/35) et `verdict_audit.py` (10/10 ACCEPT) tournent et passent sans erreur sur le commit exact. Le tableau CI de l'audit reste donc non contre-vérifié à la source, mais non contredit par ce que j'ai pu rejouer. |
| 3 | P1-1 — `SURVIE_MARGE_DERIVEE` ne contient aucun terme d'horizon et la survie mesurée passe sous la borne basse dès que l'horizon s'allonge (N≥1600) | CONFIRMED | Lecture directe : `sim/constants.py` — la formule de `SURVIE_MARGE_DERIVEE` combine `_depassement_initial`, `_fraction_predite`, `_p_tick_deficitaire`, `DEFICIT_RECOVERY_RATE_PER_TICK` ; aucune variable d'horizon (nombre de ticks) n'y figure. Sonde propre (`probe_horizon.py`, mêmes World/RNG/seed que le test livré, aucun code copié de l'audit) : `N=200 survie=0.765706 ... dans_fenetre=True` … `N=1600 survie=0.747480 borne_basse=0.748889 dans_fenetre=False` … `N=6400 survie=0.746409 dans_fenetre=False`. Reproduction **exacte au 6e chiffre** des nombres cités par l'audit. |
| 4 | P1-1 preuve 2 — doubler `HUNGER_DEATH_SCALE` change la survie mesurée sans faire bouger la marge | CONFIRMED | Sonde propre (`probe_death_scale.py`, patch de `sim.engine.HUNGER_DEATH_SCALE` — nécessaire car `engine.py` importe le nom directement, patcher `sim.constants` seul n'aurait aucun effet, ce que j'ai dû corriger dans ma première tentative) : `x0.5 → survie=0.823327` … `x1.0 → survie=0.765706` … `x2.0 → survie=0.680871, dans_fenetre=False` … `x4.0 → survie=0.551459, dans_fenetre=False` ; marge constante à `0.1511` dans les quatre cas. Chiffres identiques à l'audit. |
| 5 | P1-1 preuve 3 — l'écart entre la marge « dérivée » (0.151111) et la marge rejetée en itération 1 (0.15) est de 0.74 % | CONFIRMED | `SURVIE_MARGE_DERIVEE = 0.15111111111111114` ; écart relatif à `0.15` = `0.740740740740764 %`. Le récit de provenance (marge 0.10 → 0.15 recalibrée sur mesure en itération 1, aveu conservé) est lisible tel quel dans `sim/SEEDING.md` lignes ~285-309 (« diffère volontairement du 0.15 » ; « ce qui était faux au regard de la chronologie réelle »). |
| 6 | P1-2 — la récupération du déficit efface des kg de dette sans contrepartie physique (1000 kg de déficit → 900 kg pour 20 kg consommés ; même effet avec un surplus de 1e-9 kg) | CONFIRMED | Lecture de `_apply_consumption` (`sim/engine.py`) : branche surplus applique `new_deficit = prev_deficit * (1 - DEFICIT_RECOVERY_RATE_PER_TICK)` indépendamment de la taille du surplus. Sonde propre (`probe_deficit.py`, cellule construite directement, pas de code de l'audit repris) : `stock 1000.0 -> 980.0`, `deficit 10000.0 -> 9000.0` (consommé 20 kg, effacé 1000 kg) ; avec surplus `1e-9` kg, même résultat `10000.0 -> 9000.0`. Chiffres identiques à l'audit, y compris le ratio 10 %. |
| 7 | P1-2 imputation — la formule est prescrite mot pour mot par le brief 013 (SC4), pas une dérive du Générateur | CONFIRMED | `harness/queue/briefs/013-sim-tick-nourrit-une-fois/brief.md` ligne 128 : `cell.food_deficit_kg = max(0.0, cell.food_deficit_kg × (1 - DEFICIT_RECOVERY_RATE_PER_TICK))`, littéralement la formule implémentée. Ligne 131 laisse une latitude d'implémentation mais fixe le même critère d'acceptation (récupération strictement partielle). L'imputation au brief plutôt qu'au Générateur est donc techniquement fondée. |
| 8 | P2-1 — `_update_hunger` compte une cellule « affamée » dès que `food_stock_kg <= 0`, même rassasiée exactement (déficit nul) ; impact mesuré nul aujourd'hui (536/536 avec déficit réel) | CONFIRMED | Lecture de `_update_hunger` : test `cell.food_stock_kg <= 0.0` après consommation, sans lire `food_deficit_kg`. Sonde propre sur les 596 cellules réelles, 200 ticks, décomposition tick par tick (`probe_hunger.py`, logique de décomposition écrite indépendamment de celle de l'audit) : `536 cellules affamées / 596`, dont `536` avec un déficit réel au moins un tick, `0` cellule affamée à déficit systématiquement nul. Identique à l'audit. |
| 9 | P2-2 — dans le régime actuel, la borne haute de la fenêtre symétrique est inatteignable car aucun opérateur n'augmente `population` | CONFIRMED | `grep '\.population' sim/engine.py` : la seule affectation de `cell.population` est dans `_apply_mortality` (`cell.population = max(0, cell.population - deaths)`), toujours décroissante. `borne_haute = fraction_predite + SURVIE_MARGE_DERIVEE = 0.9 + 0.151111 = 1.051111` (lu dans `test_survie_derivee.py`), structurellement hors de portée puisque `fraction_survie ≤ 1.0` par construction. |
| 10 | P2-3 — diff `22 fichiers, +4011/-114`, objets mêlés (`HANDOFF.md` +95, `ROADMAP.md` +4, `cost-ledger.jsonl` +2), base de PR non-`master` | CONFIRMED | `git diff --stat 4c45718..29913c0` → `22 files changed, 4011 insertions(+), 114 deletions(-)`, et lignes `HANDOFF.md \| 95 ++-`, `ROADMAP.md \| 4 +-`, `harness/queue/cost-ledger.jsonl \| 2 +` — identiques à l'audit. Base non-master confirmée par ascendance git (point 1 ci-dessus) ; la stratégie de fusion en prose (« pas de squash ») n'a pas été re-vérifiée (hors de portée d'une revue de code statique). |
| 11 | P3-1 — une arête d'adjacence dupliquée (paire {1,2} et {2,1}) fait franchir le plafond par arête d'un facteur 2 ; latent, 0 doublon dans l'artefact réel (1364 arêtes, 1364 paires) | CONFIRMED | Première tentative de reproduction avec un besoin receveur égal au plafond a donné 200 kg (pas 400) — la clause de délestage côté receveur (« passe 1d ») masquait l'effet en écrêtant à la demande. En reconstruisant avec un besoin receveur (1000 kg) strictement supérieur au plafond par arête (200 kg), la duplication d'arête donne bien `400.0 kg` transportés contre `200.0 kg` avec une seule arête — le plafond par arête est bien contourné d'un facteur 2, uniquement masqué dans le cas particulier où besoin == plafond. Sur les données réelles : `1364 arêtes, 1364 paires distinctes, 0 doublon, 0 boucle a==b` (`World.from_g3`), identique à l'audit — le défaut est réel mais dort effectivement dans le producteur d'adjacence actuel, pas dans le moteur. |
| 12 | P3-2 — l'écrêtage côté receveur laisse le surplus libéré chez la source sans le réoffrir à un autre demandeur du même tick | CONFIRMED | Lecture directe de la « passe 1d » (`sim/engine.py`, bloc `snapshot_needs` / `by_receiver` / `final_transfers`) : la boucle ne fait que réduire proportionnellement les transferts vers un receveur sur-sollicité ; rien ne réinjecte la part non allouée dans une nouvelle passe d'allocation vers un autre receveur du même tick. Conservation de la masse déjà vérifiée à `0.0` (point ci-dessous) confirme que le surplus reste chez la source plutôt que d'être détruit ou téléporté — cohérent avec la lecture « choix de simplicité non documenté », pas un défaut de conservation. |
| 13 | P3-3 — le lot 013 est produit par le backend `cursor`, `budget.py` répond `UNMEASURABLE`, deux lignes `generator-run` sans jeton/durée dans `cost-ledger.jsonl` | CONFIRMED | `python3 harness/budget.py status --brief harness/queue/briefs/013-sim-tick-nourrit-une-fois` → `status: UNMEASURABLE`, `reason: no agent transcript naming 013-sim-tick-nourrit-une-fois` (chemin de recherche différent car exécuté depuis un worktree distinct, mais même verdict). `grep "013-sim-tick-nourrit" harness/queue/cost-ledger.jsonl` → deux lignes `"backend": "cursor", "event": "generator-run"`, aucun champ jeton ni durée, aucune entrée pour le rôle Évaluateur. |
| 14 | Invariance à l'ordre des arêtes + conservation de la masse pendant le commerce (section « ce que j'ai rejoué », protocole propre à l'auditeur : adjacence mélangée et inversée, 2 graines) | CONFIRMED | Sonde propre (`probe_invariance.py`, protocole équivalent mais code écrit indépendamment — mélange + inversion de chaque arête, 30 ticks) : `état identique (graine 7) : True`, `état identique (graine 999) : True`, `cellules divergentes : 0`, `conservation de la masse pendant le commerce (30 ticks) : écart max = 0.0`. |
| 15 | « Ce que cette PR fait bien » — P0 corrigé et vérifiable, trace de l'échec conservée dans `SEEDING.md`, Évaluateur a produit ses propres sabotages, compteurs reproductibles au chiffre près | CONFIRMED | Le fix P0 est vérifié au point 1. La trace de l'itération 1 (marge 0.10→0.15 recalibrée sur mesure, aveu du texte faux) est bien écrite en clair dans `sim/SEEDING.md`, pas seulement dans un journal de lot — lu directement, pas pris pour argent comptant. La reproductibilité des compteurs SC6 est vérifiée bit à bit au point 1. Je n'ai pas les moyens de vérifier indépendamment que l'Évaluateur a *lui-même* écrit ses sabotages (pas de rejeu du processus de génération), donc cette sous-clause précise reste une affirmation de l'audit non re-testable — mais cohérente avec la présence des 4 paires rouge/verte committées (`sim/tests/proof_red/`, fichiers présents et non vides). |
| 16 | Briefs atomiques proposés B-1/B-2/B-3 | NEEDS_OWNER | Ce sont des propositions de correction, pas des faits vérifiables techniquement — leur conversion en brief est un arbitrage de priorité (F2 en cours, brief 014 en file) qui appartient au propriétaire, pas à cette revue. Techniquement, B-1 et B-2 découlent directement des constats P1-1/P1-2 (CONFIRMED ci-dessus) ; B-3 découle de P2-1 (CONFIRMED, impact nul aujourd'hui). |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **B-1/B-2/B-3 (conversion en brief)** : les trois défauts qui les motivent
  (P1-1, P1-2, P2-1) sont techniquement confirmés ; reste à décider si l'un
  d'eux passe devant le brief 014 déjà en file, et si P1-2 se résout par une
  correction physique ou par un ADR de dérogation assumée (l'audit présente
  les deux comme également acceptables — c'est un choix produit/simulation,
  pas un fait technique).
- **P1-1, sévérité relative** : l'audit classe ce point P1 malgré l'absence
  de régression du moteur aujourd'hui (SC6 satisfait, seul un horizon
  allongé ou une constante de mortalité modifiée le fait rougir). Le
  propriétaire peut juger que la sévérité réelle, tant qu'aucun brief futur
  ne touche l'horizon ou `HUNGER_DEATH_SCALE`, est plus proche de P2 — c'est
  un jugement de risque, pas un désaccord technique avec la preuve.
- **P2-3, stratégie de fusion « pas de squash »** : je n'ai pas eu accès aux
  réglages de protection de branche ni à la stratégie de fusion effective du
  dépôt (comme l'audit le note lui-même dans sa section « non vérifié ») ;
  seul le propriétaire (ou quelqu'un avec accès aux réglages GitHub) peut
  trancher si la consigne en prose est un risque réel à corriger.

## 4. Synthèse

L'intégralité des points techniques vérifiables de cet audit **tient**. Sur
16 lignes de vérification, 14 sont CONFIRMED avec preuve rejouée
indépendamment (souvent avec reproduction exacte au 6e chiffre significatif
des nombres cités), 1 est PARTIAL (la classification CI n'a pas pu être
re-vérifiée à la source faute de `GH_TOKEN` dans cet environnement — non
contredite par ce que j'ai pu rejouer localement), et les propositions de
brief (B-1/B-2/B-3) sont à bon droit classées NEEDS_OWNER par l'audit
lui-même.

Le point le plus significatif, **P1-1**, résiste à un test que je n'ai pas
repris tel quel de l'audit : j'ai dû corriger ma première tentative de
reproduction de P3-1 (le besoin receveur était par erreur égal au plafond,
ce qui masquait artificiellement le défaut) avant d'obtenir le facteur 2
annoncé — ce genre d'échec-puis-correction pendant la contre-vérification
est un signe que je n'ai pas simplement recopié les résultats de l'audit.
De même, la marge de survie « dérivée » diverge bien de la mesure dès que
l'horizon dépasse 200 ticks (confirmé jusqu'à N=6400), et ne bouge pas
lorsque la constante de mortalité change de régime — deux propriétés
absentes d'une vraie dérivation analytique, présentes toutes les deux ici.

Aucun désaccord technique avec l'audit source. Recommandation : transition
PROPOSED → CHALLENGED, avec les réserves ci-dessus (§3) transmises telles
quelles au propriétaire pour la décision de valeur (APPROVED / REJECTED).
