---
decision_of: CURSOR-5633ee7-automation-completeness
decided_by: owner
verdict: APPROVED
retained_points: [1, 2, 3, 4]
---

# Décision sur CURSOR-5633ee7-automation-completeness

**Verdict : APPROVED**

## Raison

5/5 constats CONFIRMES par le contre-audit (be86205). ARCH-001 est un incident CI reel et non traite sur master (run 31085883052, exit 2) -- priorite. ARCH-002/003/004 documentent l'ecart reel entre 'full_auto' et ce qui est cable. ARCH-005 NON retenu: redondant avec CURSOR-6231186 FINDING-ARCH-003 deja ouvert et non resolu -- a rattacher a ce suivi plutot qu'ouvrir un doublon.

## Points retenus

1, 2, 3, 4

## Décision produit du propriétaire (2026-08-09) — débloque le lot 008c

Le lot 008c (ARCH-002 + ARCH-004) était BLOQUÉ faute de ces trois réponses.
Le Planificateur avait explicitement refusé de les deviner. Elles sont
tranchées ici ; une passe Planificateur fraîche les convertit en lot réel.

### 1. Quel maillon agent câbler en premier

**`claude-challenger` (`pipeline-challenge.yml`).** Motifs retenus :

- Il est en lecture seule : il produit une review `.md` et une ligne de
  ledger, jamais du code. `architecture/reviews/**` figure déjà dans
  `auto_merge_allowlist` — le rayon d'explosion est un fichier markdown sur
  une branche bot.
- Son coût est borné et prévisible (un audit → une review), là où
  `forge-run` porte une boucle de reprise (`max_forge_run_iterations: 3`,
  jusqu'à 160 appels d'outil par passe).
- C'est le vrai maillon manquant : les audits Cursor arrivent déjà seuls,
  et le propriétaire décide déjà par commandes. C'est l'étape de challenge
  qui exige aujourd'hui qu'un humain s'assoie.

Ordre retenu : **challenge → cursor-auditor → forge-run**. `forge-run` en
dernier : il écrit du code, c'est l'outlier de coût mesuré (119,96 $), et
sa boucle de reprise n'est pas bornée en dollars.

### 2. Renommage / scission de `mode: full_auto`

**Scinder en deux modes.**

- `full_auto_decision_only` — audit → challenge → décision du propriétaire
  requise. Tout sauf la génération de code.
- `full_auto` — réservé au jour où `forge-run` est réellement câblé.

Motif : `config.yaml` porte `full_auto` aujourd'hui alors que trois maillons
sont des `echo TODO(operator...)`. C'est exactement l'écart que ARCH-002 et
ARCH-004 nomment. Le renommage fait dire la vérité à la valeur de config.

Migration **fail-closed** exigée : `policy_loader` doit refuser un
`full_auto` nu tant que `forge-run` n'est pas câblé, et la valeur actuelle
doit être réécrite en `full_auto_decision_only` dans le commit même qui
introduit le renommage — jamais laissée à se réinterpréter en plus permissif.

### 3. Budget LLM récurrent accepté en CI

Plafonds durs **par invocation**, plus un plafond mensuel :

| poste | plafond |
|---|---|
| étape challenge | 5 $ par invocation |
| étape forge-run | 50 $ par invocation |
| cumul mensuel | 200 $ |

Au dépassement du plafond mensuel, la CI bascule elle-même en
`mode: manual` — on réutilise le coupe-circuit existant (ADR-0006,
`docs/rules/full-auto-pipeline.md`) au lieu d'en inventer un second.

Chiffres de calibrage, mesurés et non estimés (`py harness/backends/ledger.py tokens`) :
boucle complète du brief 008 = 13,37 $ ; médiane Générateur ≈ 20–45 $ ;
pire Générateur observé = 119,96 $ sur 982 appels — le port Unity du brief
003, atypique en périmètre et affecté par le bug de log-polling depuis
corrigé (`unity/run-unity.ps1`). Le levier de coût mesuré est le contexte
moyen par appel (371 000 pour cet outlier), pas le nombre d'appels.

Le choix de modèle reste `claude-opus-5` pour le challenge : c'est l'étape
de vérification, celle dont la qualité de jugement *est* le produit. Le coût
se maîtrise par `effort` et par les plafonds ci-dessus, pas en dégradant le
modèle de l'étape d'intégrité.
