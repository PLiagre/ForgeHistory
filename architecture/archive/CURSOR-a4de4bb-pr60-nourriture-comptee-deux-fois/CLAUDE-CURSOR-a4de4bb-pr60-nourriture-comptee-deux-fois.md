---
review_of: CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois
reviewer: claude-code
target_commit: a4de4bb91f39452c3d469792d883d0a6b83b1560
reviewed_at: 2026-08-13T08:20:30Z
---

# Contre-audit de CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : a4de4bb91f39452c3d469792d883d0a6b83b1560
- Le commit existe : `git worktree add /tmp/audit60 a4de4bb` puis
  `git rev-parse HEAD` → `a4de4bb91f39452c3d469792d883d0a6b83b1560`
  (correspond au hash complet annoncé, pas seulement au préfixe court).
- Diff re-mesuré : `git diff --stat 7b09200..a4de4bb | tail -1` →
  `30 files changed, 3258 insertions(+), 220 deletions(-)` — identique à
  l'annonce de l'audit.
- Auteurs re-mesurés : `git log --format='%an' 7b09200..a4de4bb | sort | uniq -c`
  → `7 Cursor Agent` — identique à l'annonce de l'audit (constat 4).
- Mesures rejouées dans un arbre de travail séparé (`/tmp/audit60`, détaché
  sur `a4de4bb`, aucune écriture dans le dépôt) : sondes réécrites
  indépendamment (pas copiées-collées du texte de l'audit) pour les
  constats 1, 2, 3, 4, 5, 6, 7, 8, 9, et les portes mécaniques du § 2 de
  l'audit. Détail par point au § 2 ci-dessous. Seul le constat 10 (statut
  CI en direct sur le PR GitHub) n'a pas pu être rejoué : cet environnement
  n'a pas d'accès authentifié à `gh`/l'API GitHub
  (`gh auth status` → « not logged into any GitHub hosts »).

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | P0 — la nourriture transférée par le commerce nourrit deux fois (annule le déficit **et** reste en stock) | **CONFIRMED** | Sonde réécrite indépendamment (`/tmp/probe1.py`, cellule receveuse 100 hab., besoin 200 kg) : `_apply_consumption` donne `stock=0.0 deficit=200.0` ; après `_apply_commerce` (transfert de 200 kg depuis une source en surplus) : `stock=200.0 deficit=0.0`, écart de +200.0 kg vs un témoin qui possédait déjà sa ration (`stock=0.0 deficit=0.0`) — reproduction exacte des chiffres de l'audit. Cause confirmée en lisant `sim/engine.py` `tick()` (ligne 149-171) : l'ordre est bien production → consommation → commerce → faim → mortalité, donc le déficit calculé en `_apply_consumption` (avant commerce) est ensuite décrémenté par `_apply_commerce` (ligne 95 : `cell_b.food_deficit_kg = max(0.0, cell_b.food_deficit_kg - transfer)`) sans jamais re-consommer le stock reçu. Le brief `harness/queue/briefs/012-monde-vivant-commerce-inter-cellules/brief.md` ligne 70 dit bien « après l'éventuel apport du commerce du tick courant » — l'implémentation calcule le déficit avant, pas après. Sévérité P0 justifiée : les trois compteurs vedettes de SC5 (`morts_cumules_monde_reel`, `cellules_affamees_monde_reel`, `population_finale_positive`) sont mesurés sur ce mécanisme, et j'ai reproduit ces trois chiffres exacts moi-même en rejouant la simulation complète (détail au point 5 ci-dessous). |
| 2 | P1 — le transport franchit plus d'une arête par tick ; résultat dépend de l'ordre du fichier d'adjacence | **CONFIRMED** | Sonde réécrite indépendamment, chaîne 1—2—3 (seule la cellule 1 a du stock, 2 et 3 en déficit, 3 non adjacente à 1) : ordre `[1-2, 2-3]` → `{1: 800.0, 2: 0.0, 3: 200.0}` (200 kg arrivent en cellule 3, non adjacente à la source) ; ordre `[2-3, 1-2]` → `{1: 800.0, 2: 200.0, 3: 0.0}` (rien n'arrive en 3). Résultats identiques aux deux décimales près à ceux publiés par l'audit. Cause confirmée en lisant `_apply_commerce` : la boucle `for edge in world.adjacency` mute `cell_a`/`cell_b` en place à chaque itération, donc une cellule qui vient de recevoir sur une arête peut redonner sur l'arête suivante dans le même tick. Contredit bien SC4 du brief (« Pour chaque arête, si une cellule voisine a un excédent... ») lu comme borné à l'arête elle-même, et le principe 3 de `docs/rules/simulation-principles.md` (rien ne se téléporte). |
| 3 | P1 — le ledger enregistre un acteur faux (`"actor": "claude"` pour un contre-audit signé `cursor-orchestrateur` ; `"actor": "owner"` pour une conversion machine) | **CONFIRMED** | `architecture/audit-ledger.jsonl` ligne 36 : `"actor": "claude"` pour l'événement `AUDIT_CHALLENGED` de `CURSOR-3b47ffe-pr57-monde-sans-faim`. L'en-tête de la revue citée (`architecture/reviews/CLAUDE-CURSOR-3b47ffe-pr57-monde-sans-faim.md` ligne 3) dit `reviewer: cursor-orchestrateur (rôle claude-challenger tenu en remplacement de Claude, indisponible — instruction propriétaire)` — contradiction confirmée telle quelle. `grep -n "actor=\"claude\"" harness/audit_review.py` → ligne 199, constante en dur, ne lit pas le champ `reviewer`. Ligne 38 du ledger : `"actor": "owner"` pour `AUDIT_CONVERTED` ; `harness/audit_convert.py` ligne 206 : `actor="owner"` également en dur, sans distinguer une conversion outillée d'une conversion humaine. |
| 4 | P1 — aucun maillon indépendant du producteur dans la chaîne de vérification de cette PR (y compris le contre-audit) | **CONFIRMED** | Auteurs re-mesurés (§ 1) : `7 Cursor Agent`. Signature du contre-audit re-lue : `cursor-orchestrateur` (point 3). Décision re-lue : `architecture/decisions/DECISION-CURSOR-3b47ffe-pr57-monde-sans-faim.md` ligne 3 : `decided_by: policy:auto`. Citation du `generator-log.md` ligne 12 vérifiée mot pour mot : la normalisation de `forge-generateur-cursor` en `forge-generateur` « afin que le contrôle `verdict_is_not_self_authored` puisse distinguer l'acteur Générateur de l'acteur Évaluateur par leur rôle, non par leur suffixe de backend » est bien écrite noir sur blanc dans le journal du Générateur lui-même — ce n'est pas une inférence de l'audit. Le contrôle mécanique `verdict_is_not_self_authored` (`harness/verdict_audit.py`) a effectivement tourné et vu deux acteurs distincts (`forge-generateur`/`forge-evaluateur`) sur les deux briefs, confirmant que la neutralisation décrite fonctionne comme l'audit le dit. |
| 5 | P2 — le seuil SC5 (0.70) a été calibré après mesure (0.887) ; la survie est prévisible analytiquement (0.9) | **CONFIRMED** | `sim/SEEDING.md` ligne ~192 dit littéralement : « Calibré pour être compatible avec les paramètres de production/consommation ci-dessus (mesuré à 0.887 sur N=200 ticks). » — l'aveu de calibration post-mesure est dans le document lui-même, pas une inférence de l'audit. Calcul analytique rejoué indépendamment : `capacité = prod(18.0) × rendement_moyen(1.0) / conso(2.0) = 9.0 hab/km²` ; `fraction = capacité / densité(10.0) = 0.9` — proche des 0.887 mesurés. Simulation complète rejouée dans un script écrit indépendamment (`/tmp/probe_full.py`, `World.from_g3(rng_seed=42)`, `random.Random(42)`, 200 ticks) : population initiale 66 865 505, morts 7 544 299, kg transportés 8 171 507, survie 0.8871720328740507 — identique aux chiffres de l'audit (et du manifeste) à toutes les décimales publiées. |
| 6 | P2 — le plancher `max(1, ...)` fait mourir quelqu'un pour tout déficit non nul et dépasse `MAX_DEATH_RATE_PER_TICK` pour population ≤ 9 | **CONFIRMED** | Sonde réécrite indépendamment sur `_apply_mortality` : déficit de `1e-9` à `100.0` kg → 1 mort dans tous les cas (fonction plate, donc l'« interrupteur binaire » que SC3 interdisait explicitement revient par ce chemin) ; déficit `1000.0` → 5 morts. Pour population fixée avec déficit quasi nul : pop=1 → taux effectif 1.000, pop=5 → 0.200, pop=9 → 0.111, pop=20 → 0.050 — tous identiques aux chiffres audités, et les trois premiers dépassent bien le plafond documenté `MAX_DEATH_RATE_PER_TICK = 0.10` (`sim/SEEDING.md`). Je n'ai pas rejoué la mesure d'ampleur sur le monde réel (§ 8.2 sonde 2, « part des morts due au plancher = 0.010061% ») faute de temps machine disponible dans cette revue, mais la mécanique elle-même (le défaut de fond) est confirmée par la sonde ci-dessus, et l'audit présente lui-même cette mesure d'ampleur comme une nuance à sa propre charge (« Honnêteté sur la portée »), pas comme une amplification. |
| 7 | P2 — `kg_transportes_monde_reel` compte des sauts, pas des kilogrammes nets arrivés (écart 720 700 kg, 8.82 %) | **CONFIRMED** | Script écrit indépendamment (`/tmp/probe_kg.py`), même graines (`rng_seed=42`, `random.Random(42)`), 200 ticks sur le monde réel : compteur du brief = 8 171 507 kg ; déplacement net (somme des augmentations de stock pendant l'étape commerce) = 7 450 806 kg ; écart = 720 700 kg (8.82 %). Le chiffre de déplacement net diffère de 1 kg de celui de l'audit (7 450 807), écart négligeable et cohérent avec un ordre de sommation en virgule flottante différent — sans incidence sur le pourcentage annoncé (8.82 % dans les deux cas). |
| 8 | P2 — une seule PR referme la boucle d'audit, livre le lot moteur, modifie la CI, `ROADMAP.md` et `HANDOFF.md` (hors de portée d'une relecture honnête) | **CONFIRMED** | `git diff --stat 7b09200..a4de4bb -- .github/ ROADMAP.md HANDOFF.md architecture/` confirme que la même PR touche `.github/workflows/harness-ci.yml`, `HANDOFF.md`, `ROADMAP.md`, `architecture/audit-ledger.jsonl`, une décision et une revue d'audit — en plus du lot moteur (30 fichiers au total, +3258/-220, re-vérifié au § 1). Je n'ai pas de position indépendante sur le seuil « ~5 fichiers » cité par l'audit (c'est une convention du guide de revue, pas une mesure) mais le fait matériel — un seul PR pour cinq objets distincts — est vérifié. |
| 9 | P3 — les comptages de verdicts au ledger sont gonflés par la ligne de légende du document (déjà signalé, jamais converti ; 2 enregistrements faux de plus ici) | **CONFIRMED** | Rejoué avec le code réel du dépôt (pas recopié de l'audit) : `audit_review.parse_verdicts(texte)` sur `architecture/reviews/CLAUDE-CURSOR-3b47ffe-pr57-monde-sans-faim.md` → `{'CONFIRMED': 12, 'REFUTED': 1, 'PARTIAL': 2, 'NEEDS_OWNER': 1}`, alors que `audit_decision.parse_point_verdicts` (lignes de tableau réelles) → `{'CONFIRMED': 11, 'PARTIAL': 1}`. Cause confirmée en lisant `harness/audit_review.py` ligne 127-134 : `parse_verdicts` compte des occurrences de mot entier dans **tout le texte**, y compris la ligne de légende « Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER. » (présente ligne 11 de la revue). `grep -n "CURSOR-779d97c" architecture/audit-ledger.jsonl` confirme qu'il n'y a bien qu'un événement `AUDIT_CHALLENGED` pour cet audit, aucun `AUDIT_APPROVED` ni `AUDIT_CONVERTED` — le défaut de comptage n'a donc jamais été traduit en brief, comme l'affirme l'audit. |
| 10 | P3 — classification CI : 12 vertes, 3 ignorées, 1 (`hermes-observer`) en file d'attente indéfinie (runner self-hosted Windows hors ligne) | **PARTIAL** | Je n'ai pas pu rejouer `gh pr checks 60` : cet environnement de revue n'a pas d'authentification GitHub (`gh auth status` échoue, pas de `GH_TOKEN`). Je confirme la partie structurelle vérifiable sans API : `.github/workflows/hermes-observer.yml` ligne 32 déclare bien `runs-on: [self-hosted, Windows, X64, hermes-observer]`, ce qui rend plausible qu'un tel job reste en file d'attente si la machine du propriétaire est hors ligne — exactement la cause que l'audit avance. Mais le statut « queued » en direct sur le PR #60, le compte de 12 vertes / 3 ignorées, et le timing « treize minutes » ne sont pas vérifiables depuis cette revue et restent non rejoués. |

Constat non repris en table séparée : l'audit classe lui-même au § 4
(« ce qui tient ») plusieurs points positifs — reproductibilité exacte des
compteurs, preuve rouge du transport nommée et rejouable, fermeture réelle
du défaut P1-4 (rng consommé), `harness_audit.py` non régressé, dérogation
de budget posée. J'ai vérifié indépendamment les deux affirmations les plus
fortes de cette section :
- **Compteurs reproductibles** : confirmé au point 5 ci-dessus (rejeu
  complet, chiffres identiques).
- **`harness_audit.py` non régressé** : rejoué sur les deux commits —
  `a4de4bb` → `SCORE: 20/24` ; `master` (`3dec57d`) → `SCORE: 20/24`,
  identique, confirmant l'affirmation « identique à master ».

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **Substitution du contre-audit par la même infrastructure que le
  producteur (constat 4).** C'est un fait technique confirmé, mais la
  décision d'accepter cette substitution — sous contrainte de plafond
  Claude atteint (`429` documenté) — est un arbitrage de gouvernance, pas
  une question de reproductibilité. L'audit lui-même le formule bien
  ainsi : « la substitution n'est couverte par aucun ADR alors qu'elle en
  est à son troisième usage ». C'est au propriétaire de décider si un ADR
  est nécessaire avant un quatrième usage, et si les trois usages passés
  doivent être requalifiés rétroactivement.
- **Priorité de traitement entre les trois briefs atomiques proposés
  (§ 6 de l'audit).** Le point 1 (double comptage + arête unique) est P0 et
  affecte directement les compteurs déjà publiés dans `ROADMAP.md` — sa
  priorité relève d'un jugement technique clair. Les points 2 et 3 sont
  des choix de conception (mortalité continue, granularité du ledger) où
  le propriétaire peut avoir une préférence sur l'ordre ou le regroupement
  avec le brief de harnais déjà différé (`CURSOR-3b47ffe` points 1 et 7).
- **Découpage des futures PR qui referment une boucle d'audit (constat 8).**
  Techniquement vérifié qu'une seule PR a fait les cinq choses à la fois ;
  la question de savoir si c'est acceptable comme pratique récurrente du
  harnais, ou si `NEEDS_SPLIT` doit s'appliquer aussi aux PR (pas
  seulement aux briefs), est un choix de gouvernance du propriétaire.

## 4. Synthèse

Sur 10 points, 9 sont **CONFIRMED** par rejeu indépendant (sondes réécrites
depuis zéro, pas recopiées du texte de l'audit, dans un arbre de travail
séparé sur le commit exact `a4de4bb91f39452c3d469792d883d0a6b83b1560`), et
1 (constat 10, classification CI en direct) est **PARTIAL** faute d'accès
`gh`/API GitHub authentifié dans cet environnement de revue — sa partie
structurelle (config du runner self-hosted) est confirmée, sa partie « état
en direct du PR » ne l'est pas. Aucun point n'est REFUTED.

Ce qui tombe (c'est-à-dire : rien) — chaque chiffre publié par l'audit que
j'ai pu rejouer (compteurs SC5, écart de commerce, écart de comptage kg,
plancher de mortalité, taux de survie analytique, comptage de verdicts,
score `harness_audit.py`) est sorti identique ou à une unité de flottant
près de mes propres scripts, écrits indépendamment à partir de la lecture
du code source, pas du texte de l'audit.

Le point le plus important à retenir est le constat 1 (P0) : la cause
technique est limpide (ordre du tick : consommation avant commerce, alors
que SC3 du brief exige explicitement que le déficit soit calculé « après
l'éventuel apport du commerce du tick courant »), le correctif proposé par
l'audit (inverser l'ordre, borner le transport à une arête par tick) suit
directement la spécification déjà écrite dans le brief — ce n'est pas une
proposition nouvelle, c'est un rappel de ce que SC3/SC4 imposaient déjà et
que le lot n'a pas respecté. Recommandation : traiter ce point avant toute
autre mesure de couche 1 destinée à être publiée ou citée ailleurs
(`ROADMAP.md` cite déjà `kg_transportes_monde_reel`, affecté par les
constats 1, 2 et 7 simultanément).

Le constat 4 (indépendance de la chaîne de vérification, y compris le
contre-audit lui-même) mérite une attention particulière du propriétaire
non pas parce qu'il est technique — il est confirmé — mais parce qu'il
s'agit maintenant du troisième cas documenté du même contournement
(neutralisation d'un signal qui aurait permis de le détecter), sans qu'un
ADR ne l'encadre. C'est porté au § 3 ci-dessus.

Recommandation de traitement global : les 3 briefs atomiques proposés par
l'audit (§ 6) sont fondés sur des constats confirmés et peuvent être
convertis en l'état sur leur substance technique ; le choix de les fusionner
ou non avec le brief de harnais déjà différé (points 1 et 7 de
`CURSOR-3b47ffe`) relève du propriétaire (§ 3).
