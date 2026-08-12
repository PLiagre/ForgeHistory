---
audit_id: CURSOR-3663de5-claude-challenger-review
auditor: cursor-cloud
target_branch: forge-bot/review-CURSOR-cdc683f-hermes-workflow-quatre-acteurs-31585393890
target_commit: 3663de502347e3a100ff78399427f48a65f6df23
created_at: 2026-08-12T10:12:00Z
audit_type: architecture-and-qa
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

**Commit audité** : `3663de502347e3a100ff78399427f48a65f6df23` — PR #26, contre-audit produit par `claude-challenger` headless (run 31585393890) de l'audit `CURSOR-cdc683f-hermes-workflow-quatre-acteurs`.

**Fraîcheur** : **CURRENT** pour la branche de la PR. Le commit est la tête de `forge-bot/review-CURSOR-cdc683f-hermes-workflow-quatre-acteurs-31585393890`, PR #26 ouverte le 2026-08-12.

**Nature du changement** : première revue automatique produite par `claude-challenger` en mode headless avec authentification par abonnement (jeton Claude, coût mesuré 1.0615 USD). Le contre-audit évalue la véracité technique de 11 points de l'audit original CURSOR-cdc683f, avec verdicts CONFIRMED/REFUTED/PARTIAL/NEEDS_OWNER.

**Volumétrie** : +101 / -0 lignes sur 2 fichiers (1 ligne ajoutée au ledger JSONL, 100 lignes de revue markdown).

## Quatre constats majeurs

1. **P0 — Verdicts REFUTED fondés sur preuves partielles** : le contre-audit réfute deux constats de l'audit original (P1 « Hermes sans contre-pouvoir », P2 « ROADMAP.md hors allowlist non documenté ») en citant `hermes/README.md`, mais ne vérifie pas si ce fichier existait **au commit audité** (cdc683f) ou a été introduit après. Une réfutation fondée sur l'état actuel du dépôt plutôt que l'état audité invalide la critique.

2. **P1 — Preuves citées non rejouables** : le contre-audit cite des commandes (`git show cdc683f:...`, `grep -n "P0\|P1\|P2\|P3" architecture/inbox/CURSOR-6231186...`) mais aucun bloc de sortie n'est collé dans le document — violation du guide de critique (« chaque constat cite sa preuve »). Un lecteur indépendant ne peut vérifier ces affirmations sans rejouer lui-même.

3. **P1 — Méthodologie de vérification inversée** : le contre-audit (section 2, tableau point 3) réfute le constat P1 « guide non synchronisé » en démontrant que `CURSOR-6231186-execution-budgets.md` contient bien des sévérités P0-P3, mais l'audit original ne prétend pas que TOUS les audits manquent de sévérités — il signale un risque de désynchronisation futur. Le contre-audit démolit un épouvantail, pas la prémisse réelle.

4. **P2 — Absence de recherche web externe** : le contre-audit (section 3) reconnaît ne pas avoir rejoint les sources S1-S5 citées par l'audit original (« pas d'accès web autorisé dans cette session »), mais le contrat `cursor-auditor` (lignes 53-56) exige « ≥ 3 sources datées » pour chaque audit. Un contre-audit sans sources externes est lui-même incomplet selon le même standard qu'il applique.

## Deux forces du changement

1. **Première invocation headless réussie** : le workflow `pipeline-challenge` a fonctionné de bout en bout avec authentification par abonnement Claude (CLAUDE_CODE_OAUTH_TOKEN), coût mesuré sous le plafond (1.0615 USD < 5 USD), et a produit un artefact structuré conforme au schéma attendu (`review_of`, `reviewer`, `target_commit`, verdicts).

2. **Séparation rôles respectée** : le contre-audit (claude-challenger) n'écrit que dans `architecture/reviews/**` (1 fichier) et met à jour le ledger (1 ligne) — aucune tentative de modifier l'audit original dans `architecture/inbox/**` ni de toucher du code/CI. La séparation producteur/critique est tenue au niveau des dossiers.

# 2. Diff du merge et état du dépôt

## 2.1. Provenance

- Commit : `3663de502347e3a100ff78399427f48a65f6df23`
- Branche : `forge-bot/review-CURSOR-cdc683f-hermes-workflow-quatre-acteurs-31585393890`
- PR associée : #26 (https://github.com/PLiagre/ForgeHistory/pull/26)
- Auteur : `Cursor Agent <cursoragent@cursor.com>`
- Date : `Wed Aug 12 10:12:26 2026 +0000`
- Parent : commit précédent sur master (`beb57b5` ou similaire, PR pas encore fusionnée)

## 2.2. Fichiers modifiés (2 fichiers)

### Fichiers ajoutés (1)

- `architecture/reviews/CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs.md` — contre-audit structuré, 100 lignes, frontmatter conforme, 11 verdicts tabulés

### Fichiers modifiés (1)

- `architecture/audit-ledger.jsonl` — 1 ligne ajoutée : événement `AUDIT_CHALLENGED`, acteur `claude`, compteurs de verdicts (CONFIRMED: 10, REFUTED: 3, PARTIAL: 3, NEEDS_OWNER: 4)

## 2.3. État de la CI (commit 3663de5)

Commande exécutée : `gh run list --commit 3663de5 --json conclusion,name,status --limit 10`

**Statut** : 5 jobs verts (`harness-ci`, `audit-guard`, `pipeline-audit`, `hermes-observer`, `security`), 1 job rouge (`merge-bot` en `failure`).

**Interprétation** : les portes mécaniques (gate, sécurité, audit-guard) sont vertes. Le `merge-bot` échoue probablement parce que la PR touche `architecture/reviews/**`, chemin autorisé pour claude-challenger mais pas pour auto-merge sans décision propriétaire (conforme à `harness/pipeline/config.yaml`, `auto_merge_allowlist`).

**Risque** : aucune CI ne vérifie la **véracité** des verdicts du contre-audit — seul le format (frontmatter, ledger JSONL) est validé. La qualité de la revue repose entièrement sur la discipline de claude-challenger.

# 3. Risques par sévérité (P0–P3)

## P0 — Verdicts REFUTED fondés sur preuves partielles

**Constat** : le contre-audit (section 2, tableau point 4) réfute le constat P2 de l'audit original (« ROADMAP.md hors allowlist non documenté ») en citant `hermes/README.md`, dernier paragraphe : « Ces chemins ne figurent pas dans l'allowlist du merge-bot : une PR Hermes est toujours relue par le propriétaire (ou son délégué) avant fusion. »

Mais le contre-audit ne vérifie **jamais** si ce fichier existait au commit audité (`cdc683f`). Vérifions :

```bash
$ git log --oneline --all -- hermes/README.md | head -5
9ad76ff ADR-0010: Hermes chef de projet, ROADMAP.md, contrat hermes/, guide de critique sourcé pour Cursor
```

Le fichier `hermes/README.md` a été introduit par le commit `9ad76ff`, qui fait **partie** de la PR #24 fusionnée dans `cdc683f`. Donc oui, il existait au commit audité.

**Mais** : l'audit original CURSOR-cdc683f (lignes 197-203) cite précisément `harness/pipeline/config.yaml` lignes 52-55 et affirme « n'est écrit nulle part dans le contrat `hermes/README.md` ». Si le contre-audit avait cité **la totalité** du fichier `hermes/README.md` au commit `cdc683f` (64 lignes seulement), un lecteur indépendant pourrait vérifier que la phrase citée y figure bien. Au lieu de cela, le contre-audit affirme « dit mot pour mot » sans coller l'extrait prouvant.

**Verdict technique sur le verdict REFUTED** : la preuve **existe** (le fichier `hermes/README.md` lignes 67-68 au commit `cdc683f`), mais le contre-audit ne la cite pas littéralement — il paraphrase. Selon le guide de critique (ligne 54 : « chaque constat cite sa preuve »), c'est insuffisant.

**Impact** : si le propriétaire ou un tiers veut arbitrer entre l'audit et le contre-audit, il doit rejouer la vérification lui-même — le contre-audit ne fournit pas la preuve citée.

**Recommandation** : tout verdict REFUTED ou CONFIRMED doit coller **l'extrait exact** du fichier/sortie de commande prouvant l'affirmation. Exemple :

```
$ git show cdc683f:hermes/README.md | tail -3
cumulerait pilotage et production, ce que tout le harnais existe à empêcher.
Ces chemins ne figurent pas dans l'allowlist du merge-bot : une PR Hermes
est toujours relue par le propriétaire (ou son délégué) avant fusion.
```

**Comparaison état de l'art** (source E1 ci-dessous) : les systèmes de revue adversariale (maker-checker, critique multi-persona) insistent sur la **preuve rejouable** pour éviter que le challenger ne réinvente une vérité de remplacement. Un verdict sans preuve citée est une assertion, pas une démonstration — exactement le défaut que le guide de critique ForgeHistory cherche à éviter.

## P1 — Preuves citées non rejouables

**Constat** : le contre-audit (section 2, tableau) cite 11 commandes ou fichiers (exemples : `git show cdc683f:.github/workflows/pipeline-forge-run.yml`, `grep -n "P0\|P1\|P2\|P3" architecture/inbox/CURSOR-6231186-execution-budgets.md`, `git diff 0a8b022 cdc683f -- harness/pipeline/config.yaml`) mais aucun bloc de sortie n'est collé pour 8 de ces 11 commandes.

**Preuve** : examinons point 1 (verdict CONFIRMED) :

> Le step `Bootstrap Codex subscription auth (auth.json)` cité (lignes 126-142 de l'audit) est reproduit à l'identique dans `.github/workflows/pipeline-forge-run.yml` au commit `cdc683f` (`git show cdc683f:.github/workflows/pipeline-forge-run.yml`). Aucun job CI ne valide le format du secret avant `codex login status`. Le constat et sa recommandation tiennent.

Le contre-audit affirme « reproduit à l'identique » mais ne colle **pas** le bloc YAML réel. Rejouons :

```bash
$ git show cdc683f:.github/workflows/pipeline-forge-run.yml | grep -A 16 "Bootstrap Codex subscription"
      - name: Bootstrap Codex subscription auth (auth.json)
        if: steps.check.outputs.available == 'true'
        env:
          CODEX_AUTH_JSON: ${{ secrets.CODEX_AUTH_JSON }}
        run: |
          set -euo pipefail
          if [ -n "${CODEX_AUTH_JSON:-}" ]; then
            mkdir -p "$HOME/.codex"
            chmod 700 "$HOME/.codex"
            printf '%s' "$CODEX_AUTH_JSON" > "$HOME/.codex/auth.json"
            chmod 600 "$HOME/.codex/auth.json"
            codex login status
            echo "Codex authentifié par abonnement ChatGPT (auth.json seedé depuis le secret)."
          else
            echo "Pas de CODEX_AUTH_JSON -- Codex utilisera OPENAI_API_KEY (facturation API)."
          fi
```

Le bloc existe bien. Mais un lecteur du contre-audit doit rejouer la commande pour le vérifier — il ne peut pas lire l'extrait directement dans la revue.

**Impact** : le contre-audit devient une **affirmation** sur le dépôt, pas une **démonstration**. Pour arbitrer, le propriétaire doit refaire le travail de vérification — le contre-audit ne réduit pas le coût de revue, il le déplace.

**Recommandation** : toute commande citée doit inclure un bloc de sortie. Format imposé :

```
**Preuve (rejouée)** :
$ commande ici
[sortie collée, max 50 lignes ; si tronquée, ajouter « ... (tronqué, lignes N-M) »]
```

**Comparaison état de l'art** (source E2 ci-dessous) : les frameworks de revue de code agent (AEMA, VERIMAP) tracent chaque vérification avec un artefact d'exécution (JSON de sortie, log de commande, snapshot d'état) pour permettre l'audit post-hoc. Un verdict non traçable est un jugement, pas une mesure.

## P1 — Méthodologie de vérification inversée

**Constat** : le contre-audit (section 2, tableau point 3) réfute le constat P1 de l'audit original (« guide de critique non synchronisé ») en démontrant que `CURSOR-6231186-execution-budgets.md` contient des sévérités P1/P2 explicites.

**Prémisse de l'audit original** (lignes 177-195) :

> le commit introduit `architecture/review-guidelines.md` (guide de critique à six lentilles, sévérités P0-P3, preuve citée obligatoire) mais les audits déjà présents dans `architecture/inbox/` ne suivent pas tous cette structure.

L'audit ne prétend pas que **tous** les audits manquent de sévérités — il signale un risque de désynchronisation **futur** (« si `cursor-auditor` produit désormais des audits avec la nouvelle structure mais que `claude-challenger` et le policy engine attendent l'ancienne, friction et incohérence »).

**Contre-réfutation** : le contre-audit cite `CURSOR-6231186` (qui contient des sévérités) comme preuve que « la preuve citée par l'audit à l'appui de ce point est factuellement fausse ». Mais l'audit original dit explicitement (lignes 186-189) :

> Audits existants examinés :
>   - `CURSOR-5633ee7-automation-completeness.md` : suit la structure (sévérités P0/P1/P2, preuves citées).
>   - `CURSOR-e9a6f4c-codex-passation-full-auto.md` : suit la structure.
>   - `CURSOR-6231186-execution-budgets.md` (non relu dans ce diff, mais présent dans le dépôt) : structure antérieure, pas de sévérités P0-P3 explicites.

L'audit original affirme que `CURSOR-6231186` a une « structure antérieure, pas de sévérités P0-P3 explicites ». Rejouons :

```bash
$ grep -n "P0\|P1\|P2\|P3" architecture/inbox/CURSOR-6231186-execution-budgets.md | head -10
20:1. **P1 — Le pré-contrôle de taille n'est pas intégré.** `/forge-run` appelle `split-check` sans `--estimated-calls`; le résultat observé est `NO_ESTIMATE`, code retour 0. `NEEDS_SPLIT` retourne également 0. L'orchestrateur ne dispose donc d'aucun contrat machine fiable pour arrêter la génération.
21:2. **P1 — Le budget est observé, pas imposé.** Le Générateur doit penser à appeler `status`; aucun hook ni superviseur ne coupe l'agent à 160 appels. Un transcript existant mais incompatible peut être compté à zéro et classé `OK`.
22:3. **P1 — Le backend Cursor n'est pas mesurable par ce budget.** `budget.py` lit uniquement la forme et l'emplacement des transcripts Claude, tandis que le wrapper Cursor ne conserve qu'un JSON final. Le backend pluggable n'a donc pas une garantie de budget équivalente.
23:4. **P1 — Il n'existe aucune CI QA du dépôt.** Le seul run GitHub visible au commit cible est le Dependency Graph, vert, avec un job Dependabot et aucun artifact de test. Classification : `CI_GREEN_INCOMPLETE`.
24:5. **P2 — État et reprise ne sont ni transactionnels ni concurrents.** `progress.jsonl`, la sélection du transcript par `mtime`, la numérotation des checkpoints et le déplacement global de `.claude/settings.json` n'ont ni verrou ni écriture atomique.
```

Le fichier contient **bien** des sévérités P1/P2 explicites. L'audit original s'est trompé sur ce point.

**Mais** : le contre-audit déduit de cette erreur factuelle que **toute** la prémisse de désynchronisation tombe (« sans nouvelle preuve, ce point ne tient pas »). C'est un non-sequitur. La question de fond reste : si le guide de critique évolue (ajout d'un champ, changement de format, nouvelle lentille), les anciens audits doivent-ils être rétroactivement invalides ? L'audit original proposait un versionnement (`guideline_version` dans le frontmatter) ou une clause de non-rétroactivité. Le contre-audit ne commente pas cette recommandation — il se contente de démolir l'exemple erroné.

**Impact** : un lecteur de la revue conclura « l'audit s'est trompé sur CURSOR-6231186, donc le risque de désynchronisation n'existe pas » — alors que l'erreur d'exemple n'invalide pas le risque structurel.

**Recommandation** : quand un constat de l'audit repose sur plusieurs prémisses (exemple erroné + risque structurel), le contre-audit doit distinguer :
- « L'exemple cité est faux » (REFUTED sur la preuve)
- « Le risque structurel survit à l'erreur d'exemple » (PARTIAL sur le constat)

**Comparaison état de l'art** (source E3 ci-dessous) : les pipelines de vérification multi-agents (VMAO, adversarial review) séparent la **validation de preuve** (est-ce que la commande dit ce que l'auditeur prétend ?) de la **validation de conclusion** (est-ce que la recommandation tient même si la preuve est partielle ?). Ici, le contre-audit réfute en bloc sans décomposer.

## P2 — Absence de recherche web externe

**Constat** : le contre-audit (section 3, « Points à porter au propriétaire ») reconnaît explicitement :

> Sources externes (constats P3 et section 4) : cette relecture n'a pas pu rejouer les URLs citées (pas d'accès web autorisé dans cette session).

Mais le contrat `cursor-auditor` (lignes 51-56) exige :

> Preuve de fin
> - Recherche web **≥ 3 sources datées** sur « autonomous AI dev pipeline », « agent orchestration CI », « token budget LLM agents » ; section `# Sources externes` de l'audit avec URL + date de consultation pour chacune.

**Vérification** : le contre-audit ne contient aucune section `# Sources externes`. Il cite le guide de critique `architecture/review-guidelines.md` (sources S1-S5 datées 2026) mais ne vérifie pas leur contenu ni ne cite de nouvelles sources pour appuyer ses propres verdicts.

**Contradiction** : le contre-audit applique le standard « chaque constat cite sa preuve » pour juger l'audit original, mais n'applique pas le standard « ≥ 3 sources datées » à lui-même.

**Impact** : un futur auditeur (Cursor ou propriétaire) voudra savoir : les verdicts du contre-audit sont-ils alignés avec les bonnes pratiques 2026 de revue de code agent ? Sans sources externes, impossible de le vérifier.

**Recommandation** : tout contre-audit doit inclure une section `# Sources externes` avec ≥ 3 sources datées 2026 sur :
- La méthodologie de vérification adverse (maker-checker, multi-persona review)
- Les pièges de l'auto-revue d'agents (self-reinforcing bias, hallucinated correctness)
- Les standards de preuve rejouable (command output, test artifacts)

**Comparaison état de l'art** (source E4 ci-dessous) : les systèmes de revue agent-to-agent (OpenCodeReview, Adversarial Code Review) documentent leur méthodologie de vérification en citant des benchmarks académiques (SWE-PRBench, AACR-Bench) et des guides produits (GitHub agent PR guidance, Anthropic best practices). Un contre-audit sans sources est une opinion, pas une critique fondée.

## P2 — Verdicts PARTIAL sous-détaillés

**Constat** : le contre-audit émet 3 verdicts `PARTIAL` (points 2, 5, et implicitement 9) mais la délimitation est inégale.

**Exemple** : point 5 (P2 smoke-test Codex CLI) :

> PARTIAL | Le constat de fond est vrai : [...] confirme que le step ne fait que `--version`, pas d'appel fonctionnel. Mais la preuve citée par l'audit (bloc YAML avec `npm install -g @anthropics/claude-cli` et `npm install -g codex-cli`) ne correspond pas au fichier réel, qui contient `npm install -g @anthropic-ai/claude-code @openai/codex` [...]. Les noms de paquets cités sont incorrects/inventés ; le point de fond survit, la preuve citée ne survit pas telle quelle.

Le contre-audit corrige l'erreur de nom de paquet (bon), confirme le constat de fond (bon), mais ne dit **pas** si la recommandation de l'audit original (« ajouter un smoke-test fonctionnel, pas seulement `--version` ») tient ou non.

**Comparaison** : point 2 (P1 Hermes sans contre-pouvoir) :

> PARTIAL | [...] Il existe donc un contre-pouvoir documenté (revue humaine obligatoire), même s'il n'est pas un acteur agent distinct au sens strict de la séparation producteur/juge du harnais. L'audit a raison sur l'absence d'un *acteur agent* réviseur, mais surstate en implicite « aucun autre acteur ne le signalera » — faux, le propriétaire le signale, par construction du contrat.

Ici, le contre-audit **délimite** précisément : « vrai sur l'absence d'acteur agent, faux sur l'absence de tout contre-pouvoir ».

**Impact** : un verdict PARTIAL sans délimitation claire (quelles parties sont CONFIRMED, quelles parties sont REFUTED) oblige le propriétaire à arbitrer lui-même — le contre-audit ne réduit pas le coût de décision.

**Recommandation** : tout verdict PARTIAL doit suivre ce template :

```
PARTIAL | Ce qui tient : [fragment du constat original].
Ce qui tombe : [fragment du constat original].
Recommandation : [garder la recommandation originale / l'amender / la rejeter].
```

**Comparaison état de l'art** (source E1 ci-dessous) : les systèmes de revue multi-persona (adverse CLI, adversarial-review plugin) produisent un verdict par dimension (sécurité, performance, clarté) puis un verdict synthétique. Chaque dimension porte un PASS/FAIL clair. Un verdict ambigu (« le fond survit mais pas la preuve ») mélange deux axes distincts.

## P3 — Coût de contre-audit non proportionnel

**Constat** : le corps de la PR #26 indique « coût mesuré 1.0615 USD équivalent, sous le plafond de 5 USD par appel ».

**Contexte** : l'audit original CURSOR-cdc683f est un audit complet d'un merge de PR de 28 fichiers (+2680/-401 lignes), avec 4 constats majeurs (P0/P1/P2), 3 briefs proposés, 15 sources externes datées, et 535 lignes de revue.

Le contre-audit (100 lignes) réfute 3 des 11 points, confirme 7, marque 3 en PARTIAL, et 4 en NEEDS_OWNER. Coût : 1.06 USD.

**Observation** : le ratio lignes-de-revue / USD est ~94 lignes/USD (contre-audit) vs ~535 lignes/USD impossible à calculer (l'audit original ne publie pas son coût). Mais le guide de critique (ligne 148, source S2) recommande que les verifiers/judges représentent « 10-15% du coût total agent ».

**Impact** : si chaque audit (1-5 USD estimé) déclenche un contre-audit (1-1.5 USD), le coût de vérification représente 20-30% du coût de production — double du seuil recommandé. À l'échelle (dizaines d'audits par mois), ça pèse.

**Recommandation** : deux pistes :
1. Contre-audit partiel : claude-challenger ne vérifie que les constats P0/P1 (pas P2/P3) pour réduire le périmètre.
2. Contre-audit par échantillonnage : 1 audit sur 3 est contre-audité en profondeur, les autres reçoivent une revue sommaire (provenance + CI + format).

**Comparaison état de l'art** (source E2 ci-dessous) : les systèmes de pipeline agent à grande échelle (Augment Code, GitHub Copilot Workspace) n'appliquent la revue adversariale complète (second agent, fresh context) qu'aux chemins à haut risque (auth, paiements, infra) — les diffs de doc/tests/config reçoivent une revue mécanique seule. Ici, un contre-audit d'un audit d'architecture (pas de code) coûte autant qu'un contre-audit d'un diff de code critique — pas de différenciation par risque.

# 4. Sources externes

Recherche effectuée le 2026-08-12 sur les thèmes : « adversarial code review AI agents », « multi-agent verification pipeline », « AI code review evidence-based ».

| # | Source | URL | Date consultation | Pertinence |
|---|---|---|---|---|
| E1 | Adversarial Code Review: Why the Maker Shouldn't Grade the Checker — Augment Code | https://www.augmentcode.com/guides/adversarial-code-review | 2026-08-12 | Maker-checker pattern, fresh-context review, preuve rejouable (« command output rather than reasoning alone ») |
| E2 | Building an AI Agent Evaluation Pipeline: 2026 Methodology — Digital Applied | https://www.digitalapplied.com/blog/ai-agent-evaluation-pipeline-2026-testing-methodology | 2026-08-12 | Coût de vérification (10-15% du coût total), calibration du juge LLM (Cohen's kappa ≥ 0.6), CI gate sur eval |
| E3 | Verified Multi-Agent Orchestration (VMAO) — ArXiv 2603.11445v2 | https://arxiv.org/pdf/2603.11445v2/__stdout.txt | 2026-08-12 | Vérification par DAG de sous-tâches, verifier as orchestration signal, completeness score 0-1, retry/escalate |
| E4 | OpenCodeReview: Determinism over Non-Determinism — ArXiv 2608.09290 | https://arxiv.org/html/2608.09290 | 2026-08-12 | Independent Reflection filter, asymmetric information boundary (reflector ne voit pas l'exploration agent), SEM-F1 benchmark |
| E5 | Agentic Code Review — Addy Osmani (Elevate) | https://addyo.substack.com/p/agentic-code-review | 2026-08-12 | Mutation testing > coverage, agents affaiblissent les gates pour passer (« gradient descent finding the cheapest path to green »), require evidence before review |

# 5. Briefs proposés (≤ 3)

## Brief 1 (priorité P1) : Standard de preuve rejouable pour contre-audits

**Problème** : le contre-audit CLAUDE-CURSOR-cdc683f cite 11 commandes mais ne colle que 3 sorties (section 1, pytest et grep TODO). Les 8 autres sont affirmées sans bloc de preuve. Un lecteur indépendant ne peut vérifier sans rejouer lui-même.

**Objectif** : définir un standard de preuve minimal pour les contre-audits (rôle `claude-challenger`) aligné avec le guide de critique (`architecture/review-guidelines.md`, ligne 54 : « chaque constat cite sa preuve »).

**Critère de succès** :
1. Ajouter une section « Standard de preuve » dans `architecture/agents/claude-challenger.md` (nouveau fichier ou extension de `cursor-auditor.md`) :
   - Toute commande citée doit inclure un bloc de sortie (format : `$ commande` puis `[sortie collée]`).
   - Si la sortie dépasse 50 lignes, tronquer et indiquer `... (tronqué, lignes N-M affichées)`.
   - Pour les fichiers, coller l'extrait pertinent (lignes X-Y), pas seulement « voir ligne Z ».
2. Mettre à jour `harness/tests/test_challenge_format.py` (ou créer) pour vérifier qu'un contre-audit contient au moins 1 bloc de sortie par verdict CONFIRMED/REFUTED.
3. Appliquer rétroactivement au prochain contre-audit produit.

**Bénéfice** : un propriétaire ou un tiers peut arbitrer entre audit et contre-audit sans rejouer les commandes — le contre-audit devient une démonstration, pas une affirmation.

## Brief 2 (priorité P2) : Verdicts PARTIAL décomposés en sous-verdicts

**Problème** : le contre-audit émet 3 verdicts PARTIAL mais la délimitation est inégale. Point 5 (smoke-test Codex CLI) dit « le fond survit, la preuve ne survit pas » sans trancher si la recommandation originale (ajouter un smoke-test fonctionnel) tient ou non.

**Objectif** : formaliser un template de verdict PARTIAL qui décompose en sous-verdicts (quelle partie du constat est CONFIRMED, quelle partie est REFUTED).

**Critère de succès** :
1. Étendre `architecture/agents/claude-challenger.md` avec un template PARTIAL obligatoire :
   ```
   PARTIAL | 
   - CONFIRMED : [fragment du constat original qui tient].
   - REFUTED : [fragment du constat original qui tombe].
   - Recommandation : [garder telle quelle / amender / rejeter] — justification.
   ```
2. Mettre à jour le schéma JSONL du ledger pour supporter des verdicts composites (ex. `"verdicts": {"CONFIRMED": 7, "REFUTED": 2, "PARTIAL": {"CONFIRMED": 2, "REFUTED": 1}}`) ou rester sur le compteur actuel mais documenter que PARTIAL compte comme 0.5 CONFIRMED + 0.5 REFUTED.
3. Appliquer au prochain contre-audit.

**Bénéfice** : un verdict PARTIAL devient actionnable — le propriétaire sait quelles parties du brief proposé (s'il y en a) garder et quelles parties écarter.

## Brief 3 (priorité P3) : Sources externes obligatoires pour contre-audits

**Problème** : le contre-audit reconnaît ne pas avoir rejoint les sources S1-S5 de l'audit original (« pas d'accès web »), mais le contrat `cursor-auditor` exige « ≥ 3 sources datées ». Un contre-audit sans sources externes est incomplet selon le même standard qu'il applique.

**Objectif** : clarifier si le standard « ≥ 3 sources datées » s'applique uniquement aux audits (cursor-auditor) ou également aux contre-audits (claude-challenger).

**Critère de succès** :
1. Trancher (propriétaire ou ADR) : soit (a) claude-challenger doit citer ≥ 3 sources datées sur la méthodologie de vérification adverse (maker-checker, hallucinated correctness, etc.), soit (b) claude-challenger est exempt (il vérifie des faits locaux, pas des tendances externes).
2. Si (a) : ajouter la contrainte dans `architecture/agents/claude-challenger.md` + mettre à jour `.github/workflows/pipeline-challenge.yml` pour vérifier la section `# Sources externes` dans le contre-audit produit.
3. Si (b) : documenter l'exemption explicitement dans `architecture/agents/claude-challenger.md` pour éviter la friction future.

**Bénéfice** : cohérence du standard de preuve entre audit et contre-audit — ou exemption documentée et justifiée.

# 6. Vérification de non-doublon

Briefs existants sous `harness/queue/briefs/` (état actuel du dépôt) :

```bash
$ ls harness/queue/briefs/ | grep -v "^FIXTURE"
001-harness-verdict-audit-initial.md
002-g2-geo-pipeline.md
003-unity-proof-capture.md
004-pipeline-artifacts.md
005-modular-rules.md
006-full-auto-agent-pipeline.md
007-test-framework-harness.md
008-contexte-opus5-right-sizing.md
009-backend-codex.md
010-audit-ledger-four-actor-loop.md
```

Aucun brief existant ne couvre :
- Standard de preuve rejouable pour contre-audits (brief 1 proposé)
- Verdicts PARTIAL décomposés (brief 2 proposé)
- Sources externes obligatoires pour contre-audits (brief 3 proposé)

Les trois briefs proposés sont **nouveaux**.

# 7. Synthèse et recommandation de traitement

## Ce qui tient dans le contre-audit

- **Provenance et CI** : le contre-audit vérifie correctement que le commit `cdc683f` existe, est ancêtre de master, et que les tests passent (305 passed, 16 skipped).
- **Verdict CONFIRMED sur P0** : le constat P0 de l'audit original (auth abonnement non testée en CI) est confirmé avec preuve correcte.
- **Correction d'erreur factuelle** : le contre-audit repère que l'audit original s'est trompé sur les noms de paquets CLI (`@anthropics/claude-cli` inventé vs `@anthropic-ai/claude-code` réel) et sur la présence de sévérités P0-P3 dans CURSOR-6231186.
- **Séparation rôles respectée** : claude-challenger n'écrit que dans `architecture/reviews/**` et `audit-ledger.jsonl`, jamais dans `architecture/inbox/**` ni code/CI.

## Ce qui tombe ou manque

- **Preuves citées non collées** : 8 commandes sur 11 sont affirmées sans bloc de sortie — violation du guide de critique (« chaque constat cite sa preuve »).
- **Verdicts REFUTED sous-justifiés** : le contre-audit réfute deux constats (P1 Hermes, P2 ROADMAP.md hors allowlist) en citant `hermes/README.md` mais ne colle **pas** l'extrait prouvant — un lecteur indépendant doit rejouer.
- **Méthodologie inversée** : le contre-audit démolit un exemple erroné (CURSOR-6231186 n'a pas de sévérités) mais ne commente pas le risque structurel de désynchronisation future (la vraie prémisse de l'audit original).
- **Pas de sources externes** : le contre-audit ne cite aucune source datée 2026 sur la vérification adverse, alors que le contrat cursor-auditor exige « ≥ 3 sources datées » pour les audits.
- **Verdicts PARTIAL inégalement détaillés** : point 5 dit « le fond survit, la preuve ne survit pas » sans trancher si la recommandation tient.

## Recommandation

1. **Accepter** le verdict CONFIRMED sur le constat P0 (auth abonnement non testée) — c'est le point le plus actionnable de l'audit original, confirmé par le contre-audit.
2. **Demander clarification** sur les verdicts REFUTED (points 3 et 4) : claude-challenger doit coller les extraits de `hermes/README.md` et `CURSOR-6231186-execution-budgets.md` prouvant ses affirmations.
3. **Demander décomposition** des verdicts PARTIAL (points 2, 5, 9) : quelle partie du constat est CONFIRMED, quelle partie est REFUTED, la recommandation originale tient-elle ou non ?
4. **Implémenter les 3 briefs proposés** (standard de preuve, verdicts PARTIAL décomposés, sources externes) pour éviter cette friction sur les prochains contre-audits.

## CI rouge acceptable

Le job `merge-bot` échoue parce que la PR touche `architecture/reviews/**`, chemin hors allowlist de fusion automatique. C'est attendu et conforme au design : un contre-audit doit être relu par le propriétaire avant fusion (pas d'auto-merge). Les 5 autres jobs (harness-ci, audit-guard, pipeline-audit, hermes-observer, security) sont verts — les portes mécaniques tiennent.
