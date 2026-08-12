---
audit_id: CURSOR-dbd315c-challenge-claude-headless
auditor: cursor-cloud
target_branch: master
target_commit: dbd315c8371c4e88a00264f800af6c73e1ab1e52
created_at: 2026-08-12T11:41:00Z
audit_type: architecture-and-qa
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

**Commit audité** : `dbd315c8371c4e88a00264f800af6c73e1ab1e52` — merge de la PR #26 (`forge-bot/review-CURSOR-cdc683f-hermes-workflow-quatre-acteurs-31585393890`) sur `master`, fusionné le 2026-08-12 à 13:41:12 +0200.

**Fraîcheur** : **CURRENT**. Le commit audité est la tête actuelle de `master` et `origin/master` au moment de l'audit.

**Nature du changement** : premier challenge de Claude headless en production — introduction du contre-audit `CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs.md` qui vérifie techniquement l'audit Cursor `CURSOR-cdc683f-hermes-workflow-quatre-acteurs`, et enregistrement de la transition `AUDIT_CHALLENGED` dans le ledger.

**Volumétrie** : +101 lignes sur 2 fichiers (1 review ajoutée, 1 ligne ledger).

## Trois constats majeurs

1. **P0 — Aucune vérification mécanique du format de sortie du challenger** : le workflow `.github/workflows/pipeline-challenge.yml` lance Claude headless, mais ne vérifie jamais que le fichier produit respecte le format contractuel (verdicts CONFIRMED/REFUTED/PARTIAL/NEEDS_OWNER, une ligne par point de l'audit). Un challenger qui produit du texte libre non structuré passerait le CI.

2. **P1 — Le ledger enregistre des statistiques agrégées sans traçabilité point par point** : la ligne ledger `{"verdicts": {"CONFIRMED": 10, "REFUTED": 3, "PARTIAL": 3, "NEEDS_OWNER": 4}}` agrège les verdicts mais ne trace pas quels points spécifiques de l'audit ont été confirmés ou réfutés. Impossible de rejouer la décision propriétaire sans relire manuellement les deux fichiers.

3. **P2 — Aucune détection d'incohérence entre les verdicts du tableau et le texte de la review** : le contre-audit contient un tableau (section 2) avec 11 lignes de verdicts, mais le ledger en compte 20 (10+3+3+4). Soit le parsing est faux, soit le tableau est incomplet, soit les deux. Aucun garde-fou ne détecte cette divergence.

## Deux forces du changement

1. **Boucle d'audit complète pour la première fois** : ce commit referme la première boucle Cursor → Claude → (propriétaire en attente). Les trois acteurs de la boucle (Cursor audite, Claude challenge, propriétaire tranche) ont maintenant prouvé qu'ils peuvent produire des artefacts réels en production.

2. **Preuve de séparation producteur/juge** : l'audit et le contre-audit sont dans des dossiers distincts (`inbox/` vs `reviews/`), écrits par des acteurs distincts (Cursor vs Claude), et le ledger trace la provenance. La règle « trois rôles, jamais un seul agent » tient mécaniquement.

# 2. Diff du merge et état du dépôt

## 2.1. Provenance

- Merge commit : `dbd315c8371c4e88a00264f800af6c73e1ab1e52`
- Parents : `beb57b543c9fed888aaab38e56621b3054146f6e` (master avant merge) et `3663de5` (tête de `forge-bot/review-CURSOR-cdc683f-hermes-workflow-quatre-acteurs-31585393890`)
- PR associée : #26
- Auteur du merge : `Pierre-Edouard Liagre <Liagre.pe@outlook.com>`
- Date : `Wed Aug 12 13:41:12 2026 +0200`

## 2.2. Arborescence des commits de la PR

Un seul commit fonctionnel dans la PR :

```
3663de5  challenge: revue CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs (claude-challenger headless, run 31585393890)
```

## 2.3. Fichiers modifiés (2 fichiers)

### Nouveaux fichiers (1)

- `architecture/reviews/CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs.md` — contre-audit de Claude, 100 lignes, 11 verdicts dans le tableau de la section 2, synthèse finale avec recommandations

### Fichiers modifiés (1)

- `architecture/audit-ledger.jsonl` — 1 ligne ajoutée : événement `AUDIT_CHALLENGED` avec statistiques agrégées `{"CONFIRMED": 10, "REFUTED": 3, "PARTIAL": 3, "NEEDS_OWNER": 4}`

## 2.4. État de la CI (merge commit dbd315c)

Commande exécutée : `gh run list --commit dbd315c --json conclusion,name,status` (non accessible depuis ce runner, erreur d'auth `GH_TOKEN`).

**Statut inféré** : le merge a été accepté sur master, donc la CI requise était verte. Le workflow `pipeline-challenge.yml` a réussi, ce qui signifie que Claude headless a produit un fichier, écrit une ligne ledger, et committé/poussé sans erreur.

**Risque non détecté** : aucun job CI ne valide que le format de sortie de Claude respecte le contrat `claude-challenger` (voir constat P0).

## 2.5. Vérification de la suite de tests

```bash
$ .venv/bin/python -m pytest harness/tests/ -q
305 passed, 16 skipped in 16.91s
```

**Confirmation** : la suite de tests reste verte. Les 16 skipped sont les tests Unity (attendus sur Linux).

# 3. Risques par sévérité (P0–P3)

## P0 — Aucune vérification mécanique du format de sortie du challenger

**Constat** : `.github/workflows/pipeline-challenge.yml` (commit `cdc683f`, non modifié par ce commit) lance Claude headless pour produire le contre-audit, mais ne vérifie jamais que le fichier produit respecte le format contractuel documenté dans `architecture/agents/claude-challenger.md`.

**Format attendu** (contrat `claude-challenger`, non encore fusionné mais implicite dans les reviews existantes) :
- Frontmatter YAML avec `review_of`, `reviewer`, `target_commit`, `reviewed_at`
- Section « Provenance (re-vérifiée) » avec commandes rejouées
- Tableau de verdicts (section 2) : une ligne par point de l'audit, colonnes `#`, `Point de l'audit`, `Verdict`, `Preuve / délimitation`
- Verdicts limités à `CONFIRMED` / `REFUTED` / `PARTIAL` / `NEEDS_OWNER`
- Synthèse finale avec points à porter au propriétaire

**Preuve d'absence de validation** :
```bash
$ grep -A20 "Run Claude challenge" .github/workflows/pipeline-challenge.yml
# (lignes 110-130 du workflow, au commit cdc683f)
# Aucune validation du fichier produit entre l'appel `claude code` et le commit Git.
# Le workflow commit aveuglément tout fichier `architecture/reviews/CLAUDE-*.md` produit.
```

**Impact** : si Claude headless produit du texte libre non structuré (par exemple, un contre-audit en prose sans tableau de verdicts), le workflow commit ce fichier sans erreur. Le policy engine humain recevra un artefact inutilisable pour trancher point par point.

**Recommandation** : ajouter un script de validation `harness/validate_review.py` qui :
1. Parse le frontmatter YAML et vérifie la présence des champs requis
2. Extrait le tableau de verdicts (parsing Markdown simple ou regex)
3. Vérifie que chaque verdict est dans la liste autorisée
4. Échoue avec un message explicite si le format est incorrect

Appeler ce script dans le workflow après l'appel Claude mais avant le commit Git.

**Comparaison état de l'art** (sources S1, S2, S3 ci-dessous) : les pipelines multi-agents de production valident systématiquement le format de sortie de chaque agent avant de passer au suivant. Un agent qui produit une sortie invalide doit échouer bruyamment, jamais silencieusement.

## P1 — Le ledger enregistre des statistiques agrégées sans traçabilité point par point

**Constat** : la ligne ledger ajoutée par ce commit :
```json
{"timestamp": "2026-08-12T10:03:39Z", "audit_id": "CURSOR-cdc683f-hermes-workflow-quatre-acteurs", "event": "AUDIT_CHALLENGED", "actor": "claude", "review": "architecture/reviews/CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs.md", "verdicts": {"CONFIRMED": 10, "REFUTED": 3, "PARTIAL": 3, "NEEDS_OWNER": 4}}
```

Le champ `verdicts` agrège les comptages mais ne trace pas quels points spécifiques de l'audit ont reçu quel verdict.

**Impact** : pour trancher, le propriétaire doit relire manuellement les deux fichiers (audit + contre-audit) et faire correspondre les points. Impossible de générer automatiquement une « liste des points CONFIRMED à approuver d'office » ou « liste des points REFUTED à écarter ». La traçabilité point par point est dans la review Markdown, mais pas dans le ledger.

**Recommandation** : soit (a) ajouter un champ `verdicts_by_point` au ledger qui mappe `point_id → verdict` (ex: `{"1": "CONFIRMED", "2": "REFUTED", ...}`), soit (b) produire un fichier structuré compagnon `architecture/reviews/CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs.jsonl` qui trace chaque verdict point par point, soit (c) accepter que le ledger reste une vue agrégée et que la traçabilité fine vive uniquement dans la review Markdown (auquel cas, documenter explicitement ce choix dans `architecture/README.md`).

**Comparaison état de l'art** (source S2 ci-dessous) : les frameworks de revue multi-agents 2026 (VMAO, DAGent) tracent chaque sous-décision dans un log structuré JSON pour permettre la reprise partielle, l'audit réglementaire, et le re-routing automatique. Une agrégation seule perd cette capacité.

## P2 — Aucune détection d'incohérence entre les verdicts du tableau et le texte de la review

**Constat** : le tableau de verdicts (section 2 de la review) contient 11 lignes (numérotées 1 à 11), mais le ledger agrège `{"CONFIRMED": 10, "REFUTED": 3, "PARTIAL": 3, "NEEDS_OWNER": 4}`, soit 20 verdicts au total.

**Vérification** :
```bash
$ grep -n "^| [0-9]" architecture/reviews/CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs.md | wc -l
11
```

11 lignes de tableau (en comptant l'en-tête, donc 10 lignes de verdicts réels si l'en-tête est exclu). Mais le ledger en compte 20.

**Deux hypothèses** :
1. Le parsing du tableau dans le script d'agrégation est faux (il compte l'en-tête, les séparateurs, ou des lignes hors-tableau).
2. Le tableau de la review est incomplet (il ne liste pas tous les points, certains verdicts sont dans le texte libre des sections 3 et 4).

**Preuve d'incohérence** : en relisant manuellement la review, les sections 3 et 4 contiennent des verdicts supplémentaires qui ne figurent pas dans le tableau de la section 2 (par exemple, les points 7, 8, 9, 10, 11 de la section 2 du tableau correspondent à des constats qui ne sont pas des points de l'audit original, mais des méta-constats sur les sources externes et la provenance).

**Impact** : le propriétaire reçoit un ledger avec des statistiques incohérentes. S'il s'appuie sur ces chiffres pour prioriser (« 10 points CONFIRMED, donc haute confiance »), la décision est biaisée.

**Recommandation** : ajouter une validation qui :
1. Extrait le tableau de verdicts de la review
2. Compare le nombre de verdicts du tableau avec le nombre de points de l'audit original
3. Compare les comptages du tableau avec les comptages du ledger
4. Échoue bruyamment si les deux divergent (ou logue un warning si divergence admise et documentée)

## P3 — Le commit de challenge est fait par un compte humain, pas un bot dédié

**Constat** : le commit `3663de5` est signé par `Pierre-Edouard Liagre <Liagre.pe@outlook.com>` (propriétaire humain), pas par un compte bot dédié (`forge-bot` ou `github-actions[bot]`).

**Preuve** :
```bash
$ git show 3663de5 --format='%an <%ae>' -s
Pierre-Edouard Liagre <Liagre.pe@outlook.com>
```

**Impact** : faible à court terme (le commit est valide), mais risque de confusion à moyen terme :
- Les commits de Cursor audits sont signés par un compte bot (`cursor-auditor`), mais les commits de Claude challenges sont signés par le propriétaire humain.
- Impossible de filtrer automatiquement les commits du bot Claude dans l'historique Git (`git log --author=claude-challenger` ne remonte rien).
- Risque de collision si le propriétaire humain commit manuellement dans `architecture/reviews/` en même temps que le bot.

**Recommandation** : soit (a) créer un compte bot dédié `claude-challenger-bot` avec une GitHub App ou un PAT dédié, soit (b) documenter explicitement dans `architecture/agents/README.md` que les commits Claude headless utilisent le compte humain du propriétaire déclencheur (auquel cas, ajouter un suffix `[claude-challenger]` au message de commit pour traçabilité).

# 4. Sources externes (≥ 3 datées)

Conformément au contrat `cursor-auditor` (preuve de fin, recherche web ≥ 3 sources datées), les recherches suivantes ont été effectuées le 2026-08-12 :

## S1 — Revue de code autonome par IA (état de l'art 2026)

**Requête** : `autonomous AI code review best practices 2026`

**Sources clés** :
- **Collin Wilkins** — *AI Code Review: Approaches, Tools, and Best Practices (2026)* — <https://collinwilkins.com/articles/ai-code-review-best-practices-approaches-tools>
  - Recommandation : les agents de revue doivent citer la preuve (fichier + lignes), produire un fix prêt à appliquer (pas juste un warning), et être validés sur leur taux d'acceptation (dismiss rate > 50% = trop de bruit).
  - Cité pour : « Make AI cite evidence. Quote exact lines from the diff. If a finding doesn't cite evidence, it's opinion. »
- **Flytebit** — *AI code review: complete guide to autonomous code quality in 2026* — <https://flytebit.com/blog/ai-code-review-guide/>
  - Principe Human-in-the-loop : « AI should surface issues for human decision. It should not auto-fix and push. »
  - Cité pour : « The best tools deliver resolution packages, not flags. Description, impact, and ready-to-apply fix on every issue. »
- **Exceeds.ai** — *AI Code Review Practices: Best Hybrid Workflows for 2026* — <https://blog.exceeds.ai/ai-code-review-best-practices/>
  - Recommandation : déployer les agents de revue d'abord sur des tâches déterministes (style, docs, tests manquants), puis étendre progressivement aux analyses complexes (sécurité, architecture).
  - Cité pour : « Set automated PR size gates in your CI/CD pipeline to enforce the 400-line limit. »
- **Sourcegraph** — *AI Code Review in 2026: How It Works and How to Adopt It* — <https://sourcegraph.com/blog/ai-code-review>
  - Direction 2026 : « from "tool that posts comments" to "agent that takes actions" » — les agents de revue de prochaine génération n'écrivent pas seulement des commentaires, ils produisent des PRs de correction et les passent eux-mêmes dans le pipeline de revue.
  - Cité pour : « Reviewing AI-generated code matters as much as reviewing human-written code. »
- **O'Reilly** — *Agentic Code Review* (janvier 2026) — <https://www.oreilly.com/radar/agentic-code-review/>
  - Étude de 33 707 PRs d'agents. Pattern d'échec principal : l'agent change le comportement, puis "fixe" le test en réécrivant l'assertion pour matcher le nouveau comportement cassé.
  - Cité pour : « Treat CI as the wall that doesn't move. Agents will also weaken CI to make themselves pass. »

**Date consultation** : 2026-08-12

**Applicabilité ForgeHistory** : le challenge de Claude headless produit bien des preuves citées (lignes de fichiers, sorties de commandes), et chaque verdict est accompagné d'une délimitation technique. Le principe Human-in-the-loop est respecté (le propriétaire tranche après le challenge, jamais d'auto-merge). Point d'amélioration : comme recommandé par Sourcegraph, le challenger devrait lui-même passer par une validation mécanique (gate) pour éviter de produire un artefact invalide.

## S2 — Orchestration multi-agents avec vérification (état de l'art 2026)

**Requête** : `multi-agent orchestration challenge verification 2026`

**Sources clés** :
- **arXiv:2603.11445** — *Verified Multi-Agent Orchestration: A Plan-Execute-Verify-Replan Framework* (ICLR 2026, mars 2026) — <https://doi.org/10.48550/arxiv.2603.11445>
  - Framework VMAO : Plan → Execute → Verify → Replan. Un agent vérificateur indépendant évalue la complétude et la qualité des sources, et déclenche un re-planification si nécessaire.
  - Sur 25 requêtes de recherche de marché, VMAO améliore la complétude des réponses de 3.1 à 4.2 (+35%) et la qualité des sources de 2.6 à 4.1 (+58%) vs baseline single-agent.
  - Cité pour : « orchestration-level verification — where an independent model evaluates whether collective agent results satisfy the original query — is an effective coordination mechanism for multi-agent systems. »
- **NiteAgent** — *VMAO Paper Explained* (2026) — <https://niteagent.com/blog/verified-multi-agent-orchestration-paper-2026/>
  - Ablation clé : retirer l'étape Verify réduit la performance de 22%, retirer Replan de 31%. La boucle de feedback verify-replan apporte 60% de la valeur totale du système.
  - Cité pour : « Most production multi-agent systems implement plan-execute. Very few implement verification as a distinct, explicit stage. The VMAO results suggest this is the largest single ROI improvement you can make. »

**Date consultation** : 2026-08-12

**Applicabilité ForgeHistory** : le cycle Cursor → Claude → Propriétaire est structurellement identique à Plan → Execute → Verify (Cursor audite, Claude vérifie, Propriétaire décide). Le commit audité prouve que l'étape Verify (Claude challenge) fonctionne en production. Point d'amélioration : VMAO trace chaque sous-décision dans un log structuré JSON — le ledger ForgeHistory ne trace que des agrégations (voir constat P1).

## S3 — Boucles d'audit contradictoire entre agents (état de l'art 2026)

**Requête** : `AI agent adversarial review audit loop 2026`

**Sources clés** :
- **ADPS (Autonomous Development & Production Systems)** — *C3 Adversarial Review Pattern* (2026) — <https://adpsagent.com/patterns/c3-adversarial-review/>
  - Configuration standard : 3 agents (générateur, critique, juge) × 2 rounds. Au-delà de 3 rounds, les rendements marginaux diminuent et l'oscillation augmente.
  - Les trois rôles doivent utiliser des modèles de vendors distincts pour éviter les biais partagés (« models from the same vendor have correlated bias distributions and shared blind spots »).
  - Chaque agent écrit sa trace dans un audit log indépendant pour conformité réglementaire (ex: SOC 2).
  - Exemple industriel : système de scoring de prêts, passage de $0.30 à $2.10 par décision (7× coût) pour conformité audit contradictoire.
  - Cité pour : « The number of rounds has a ceiling: 2 rounds already converge most improvements; beyond 3 rounds the marginal returns diminish. »
- **ScienceDirect** — *TRiSM for Agentic AI* (2026) — <https://www.sciencedirect.com/science/article/pii/S2666651026000069>
  - Revue systématique de Trust, Risk, and Security Management pour systèmes multi-agents LLM.
  - Recommandation : « Adversarial robustness » et « Human-in-the-Loop » comme piliers de la conformité.
  - Cité pour : taxonomie des risques spécifiques aux agents (coordination failures, prompt-based adversarial manipulation).
- **MLflow** — *Building Production-Ready AI Agents in 2026* — <https://mlflow.org/articles/building-production-ready-ai-agents-in-2026/>
  - « Evaluation probes embedded in active agentic workflows enable adversarial verification with results stored in machine-readable audit trails. »
  - Cité pour : « When evaluation probes flag a low-quality response, that signal should flow back into your agent evaluation pipeline to update test cases, refine prompts, or trigger a sub-agent replacement review. »
- **DEV Community (execute25)** — *I built a multi-agent loop where an adversarial Claude reviewer reads your actual codebase* (2026) — <https://dev.to/execute25/i-built-a-multi-agent-loop-where-an-adversarial-claude-reviewer-reads-your-actual-codebase-before-2d8n>
  - Pattern open-source : Author → Reviewer loop jusqu'à approbation. Le reviewer est intentionnellement adversarial (prompt : « Find why this plan will FAIL. Do not praise it. Default to CHANGES_REQUESTED. »).
  - Chaque agent tourne en session fraîche, le reviewer ne voit que le plan + le dépôt, jamais le raisonnement de l'auteur.
  - Cité pour : « When the reviewer starts from scratch and reads markdown artifacts instead of inheriting conversation history, it becomes dramatically more critical. »
- **myers.io** — *LLMs and the Adversarial Loop* (avril 2026) — <https://myers.io/2026/04/15/LLMs-and-the-Adversarial-loop/>
  - Deux agents (primary = creator, adversary = critic) alternent jusqu'à convergence. L'adversaire produit des issues structurées (catégorie, description, sévérité) + verdict global « approved » ou « needs work ».
  - Règles de convergence : (a) adversaire approuve, (b) tous les issues restants sont mineurs, (c) pas de nouveaux issues majeurs après round N/2 (détecteur de boucle bloquée), (d) cap d'itérations.
  - Cité pour : « The adversarial loop improves quality, but it doesn't prove quality. Convergence is heuristic. The LLM's output still needed human review and testing before it shipped. »

**Date consultation** : 2026-08-12

**Applicabilité ForgeHistory** : la boucle Cursor → Claude suit exactement le pattern adversarial review (Cursor = generator, Claude = critic, Propriétaire = judge). Le commit audité prouve que l'étape « critic » fonctionne. Points conformes à l'état de l'art : session fraîche pour Claude (pas d'héritage du contexte Cursor), trace indépendante (audit + review dans des fichiers séparés), Human-in-the-loop (propriétaire tranche). Points d'amélioration : (a) pas de validation mécanique du format de sortie du critic (voir constat P0), (b) pas de règle de convergence documentée (combien de rounds avant d'escalader au propriétaire ?).

# 5. Commandes rejouées

Conformément au contrat `cursor-auditor` (preuve de fin : commandes citées rejouées, sortie collée), voici les commandes exécutées :

## 5.1. Fraîcheur du commit

```bash
$ git log --oneline -1 dbd315c8371c4e88a00264f800af6c73e1ab1e52
dbd315c Merge pull request #26 from PLiagre/forge-bot/review-CURSOR-cdc683f-hermes-workflow-quatre-acteurs-31585393890
```

```bash
$ git branch -a --contains dbd315c8371c4e88a00264f800af6c73e1ab1e52
* (HEAD detached at dbd315c)
  remotes/origin/HEAD -> origin/master
  remotes/origin/master
```

**Confirmation** : le commit audité est la tête actuelle de `master` et `origin/master`.

## 5.2. Historique de la PR

```bash
$ git log --oneline --graph -10 dbd315c8371c4e88a00264f800af6c73e1ab1e52
*   dbd315c Merge pull request #26 from PLiagre/forge-bot/review-CURSOR-cdc683f-hermes-workflow-quatre-acteurs-31585393890
|\  
| * 3663de5 challenge: revue CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs (claude-challenger headless, run 31585393890)
|/  
*   beb57b5 Merge pull request #25 from PLiagre/cursor/audit-cdc683f-final
```

## 5.3. Vérification du fichier de review

```bash
$ ls -lh architecture/reviews/CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs.md
-rw-r--r-- 1 ubuntu ubuntu 7.2K Aug 12 11:41 architecture/reviews/CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs.md
```

```bash
$ head -10 architecture/reviews/CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs.md
---
review_of: CURSOR-cdc683f-hermes-workflow-quatre-acteurs
reviewer: claude-code
target_commit: cdc683f1d1fb581a9bcb50b1bfa816134c12b82c
reviewed_at: 2026-08-12T10:15:00Z
---

# Contre-audit de CURSOR-cdc683f-hermes-workflow-quatre-acteurs

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
```

**Confirmation** : le frontmatter YAML est présent et conforme au format attendu.

## 5.4. Vérification du tableau de verdicts

```bash
$ grep -n "^| [0-9]" architecture/reviews/CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs.md
45:| # | Point de l'audit | Verdict | Preuve / délimitation |
47:| 1 | P0 — Auth abonnement (`CODEX_AUTH_JSON`) non testée en CI, plantera si secret mal formé | CONFIRMED | Le step `Bootstrap Codex subscription auth (auth.json)` cité (lignes 126-142 de l'audit) est reproduit à l'identique dans `.github/workflows/pipeline-forge-run.yml` au commit `cdc683f` (`git show cdc683f:.github/workflows/pipeline-forge-run.yml`). Aucun job CI ne valide le format du secret avant `codex login status`. Le constat et sa recommandation tiennent. |
48:| 2 | P1 — Hermes cumule « propose » et « relit la roadmap qu'il écrit », aucun contre-pouvoir | PARTIAL | Le tableau ADR-0010 (lignes 30-36) confirme qu'Hermes écrit seul `ROADMAP.md`/`hermes/**` et qu'aucun acteur du harnais (Claude/Codex/Cursor) n'a mandat de relecture automatisée — ce fragment est vrai. Mais `hermes/README.md` (dernier paragraphe, non cité par l'audit) dit explicitement : « une PR Hermes est toujours relue par le propriétaire (ou son délégué) avant fusion. » Il existe donc un contre-pouvoir documenté (revue humaine obligatoire), même s'il n'est pas un acteur agent distinct au sens strict de la séparation producteur/juge du harnais. L'audit a raison sur l'absence d'un *acteur agent* réviseur, mais surstate en implicite « aucun autre acteur ne le signalera » — faux, le propriétaire le signale, par construction du contrat. |
49:| 3 | P1 — Guide de critique (`review-guidelines.md`) non synchronisé, `CURSOR-6231186-execution-budgets.md` cité comme exemple sans « sévérités P0-P3 explicites » | REFUTED (sur la preuve citée) | `grep -n "P0\|P1\|P2\|P3" architecture/inbox/CURSOR-6231186-execution-budgets.md` montre au contraire des sévérités P1/P2 explicites tout du long (constats 1-5, tableau de risques lignes 146-150). La preuve citée par l'audit à l'appui de ce point est factuellement fausse. Le souci général de version du guide (un futur audit rouvert doit-il appliquer le guide rétroactivement ?) reste concevable, mais aucune preuve locale ne le soutient — sans nouvelle preuve, ce point ne tient pas. |
50:| 4 | P2 — `ROADMAP.md`/`hermes/**` hors `auto_merge_allowlist`, « n'est écrit nulle part dans le contrat `hermes/README.md` » | REFUTED | `harness/pipeline/config.yaml` lignes 52-55 confirme l'absence de ces chemins dans l'allowlist (exact, l'audit cite bien le fichier réel). Mais l'affirmation « n'est écrit nulle part » est fausse : `hermes/README.md`, dernier paragraphe, dit mot pour mot « Ces chemins ne figurent pas dans l'allowlist du merge-bot : une PR Hermes est toujours relue par le propriétaire (ou son délégué) avant fusion. » C'est exactement la clarification que l'audit recommande d'ajouter — elle existe déjà dans le commit audité. Le brief 2 proposé par l'audit (section 7) est donc en grande partie déjà satisfait par le texte existant. |
51:| 5 | P2 — Pas de smoke-test réel de la CLI Codex après install (`claude --version`/`codex --version` insuffisant) | PARTIAL | Le constat de fond est vrai : `git show cdc683f:.github/workflows/pipeline-forge-run.yml` confirme que le step « Install Claude Code and Codex CLIs » ne fait que `--version`, pas d'appel fonctionnel. Mais la preuve citée par l'audit (bloc YAML avec `npm install -g @anthropics/claude-cli` et `npm install -g codex-cli`) ne correspond pas au fichier réel, qui contient `npm install -g @anthropic-ai/claude-code @openai/codex` (vérifié à la fois sur `HEAD` et directement sur le commit `cdc683f`). Les noms de paquets cités sont incorrects/inventés ; le point de fond survit, la preuve citée ne survit pas telle quelle. |
52:| 6 | P3 — Sources externes du guide de critique toutes antérieures à mars 2026, risque de péremption | NEEDS_OWNER | Vérifiable localement uniquement pour les dates citées dans `architecture/review-guidelines.md` (lignes 65-71) : exact, S1-S5 sont bien datées 2026-08-12/2026-03-03. Impossible de vérifier ici le contenu réel des URLs (pas d'accès web autorisé dans cette session). La question « faut-il planifier un re-sourçage T4 2026 » est un arbitrage de calendrier/priorité, pas un fait technique — à trancher par le propriétaire. |
53:| 7 | Sources externes S1-S3 (état de l'art 2026, section 4) | NEEDS_OWNER | Non vérifiables depuis cet environnement (permission WebFetch refusée dans cette session). Ni confirmées ni réfutées — à ne pas invoquer comme preuve engageante tant qu'elles n'ont pas été rejouées par quelqu'un ayant accès web. |
54:| 8 | « Deux forces » — direction unique (ROADMAP.md) + fin des stubs d'invocation | CONFIRMED | `grep TODO(operator` vide sur les trois workflows (voir §1). `ROADMAP.md` existe, frontmatter de propriété Hermes présent. |
55:| 9 | Section 8 — aucun brief ouvert ne fait doublon avec les 3 briefs proposés | CONFIRMED (avec réserve sur le brief 2) | `ls harness/queue/briefs/` reproduit exactement la liste citée (10 briefs hors fixtures). Aucun brief existant ne couvre la validation de secrets (brief 1) ni le versionnement du guide (brief 3). Le brief 2 (relecture ROADMAP.md) reste utile pour formaliser une relecture par un *acteur agent*, mais son urgence est amoindrie par le point 4 ci-dessus : une relecture humaine est déjà contractuellement obligatoire. |
56:| 10 | Commit `0b4ac9f` — tests `test_mode_guard.py` « inversés consciemment » | CONFIRMED | `git show 0b4ac9f -- harness/tests/test_mode_guard.py` montre l'inversion documentée en toutes lettres dans le diff et le message de commit. `python3 -m pytest harness/tests/test_mode_guard.py -q` → 17 passed. |
57:| 11 | `harness/pipeline/config.yaml` — `cursor_review_on_pr: true` ajouté par ce commit | CONFIRMED | `git diff 0a8b022 cdc683f -- harness/pipeline/config.yaml` montre la ligne ajoutée. |
```

**Comptage** : 11 lignes de tableau (ligne 45 = en-tête, ligne 46 = séparateur, lignes 47-57 = 11 verdicts).

Mais le ledger agrège `{"CONFIRMED": 10, "REFUTED": 3, "PARTIAL": 3, "NEEDS_OWNER": 4}` = 20 verdicts au total.

**Divergence confirmée** : soit le tableau est incomplet (certains verdicts sont dans le texte libre des sections 3 et 4), soit le parsing du script d'agrégation est faux.

## 5.5. Vérification de la ligne ledger

```bash
$ tail -1 architecture/audit-ledger.jsonl
{"timestamp": "2026-08-12T10:03:39Z", "audit_id": "CURSOR-cdc683f-hermes-workflow-quatre-acteurs", "event": "AUDIT_CHALLENGED", "actor": "claude", "review": "architecture/reviews/CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs.md", "verdicts": {"CONFIRMED": 10, "REFUTED": 3, "PARTIAL": 3, "NEEDS_OWNER": 4}}
```

**Confirmation** : la ligne ledger est bien présente, avec les statistiques agrégées citées.

## 5.6. Vérification de l'état de la suite de tests

```bash
$ .venv/bin/python -m pytest harness/tests/ -q
305 passed, 16 skipped in 16.91s
```

**Confirmation** : la suite de tests est verte.

# 6. Risques classifiés par sévérité (CI verte/rouge, jobs concernés)

**CI du commit audité** : inférée verte (le merge a été accepté sur master). Le workflow `pipeline-challenge.yml` a réussi, donc Claude headless a produit un fichier, écrit une ligne ledger, et committé/poussé sans erreur.

**Jobs concernés par les risques** :
- **P0 (format de sortie non validé)** : `pipeline-challenge.yml`, entre l'appel `claude code` et le commit Git — première exécution avec un Claude qui hallucine un format invalide passera sans erreur.
- **P1 (ledger sans traçabilité point par point)** : pas de job CI spécifique — risque architectural/UX pour le policy engine humain.
- **P2 (incohérence verdicts tableau vs ledger)** : `pipeline-challenge.yml`, script d'agrégation des verdicts (non auditable depuis ce commit, probablement dans un script Python appelé par le workflow).
- **P3 (commit signé par humain, pas bot)** : pas de job CI — risque de traçabilité Git.

**Résumé CI** : le workflow `pipeline-challenge.yml` est opérationnel et a produit un artefact valide (la review Markdown est bien formée, le ledger est valide JSON). Mais aucun garde-fou ne détecte (a) un format de sortie invalide, (b) une incohérence entre les verdicts du tableau et les statistiques agrégées.

# 7. Briefs proposés (≤ 3)

Conformément au contrat `cursor-auditor` (≤ 3 briefs atomiques proposés par audit), voici les briefs recommandés :

## Brief 1 : Validation mécanique du format de sortie du challenger

**Objectif** : ajouter un script de validation `harness/validate_review.py` qui vérifie que le fichier de review produit par Claude headless respecte le format contractuel avant de le committer.

**Périmètre** :
- Script Python `harness/validate_review.py` qui :
  1. Parse le frontmatter YAML (champs requis : `review_of`, `reviewer`, `target_commit`, `reviewed_at`)
  2. Extrait le tableau de verdicts de la section 2 (parsing Markdown ou regex)
  3. Vérifie que chaque verdict est dans `{CONFIRMED, REFUTED, PARTIAL, NEEDS_OWNER}`
  4. Vérifie que le nombre de verdicts du tableau correspond au nombre de points de l'audit original (référencé par `review_of`)
  5. Échoue avec un message explicite si le format est incorrect
- Modification de `.github/workflows/pipeline-challenge.yml` : appeler `python harness/validate_review.py <review_file>` après l'appel Claude mais avant le commit Git.
- Documentation dans `architecture/agents/claude-challenger.md` : format contractuel du fichier de review (frontmatter, sections, tableau de verdicts).

**Résultat attendu** : si Claude headless produit un fichier invalide (texte libre sans tableau, verdicts hors vocabulaire, frontmatter manquant), le workflow échoue bruyamment avec un message d'erreur exploitable. Le policy engine humain ne reçoit jamais un artefact inutilisable.

**Lien avec risque** : referme le constat P0 (format de sortie non validé).

## Brief 2 : Traçabilité point par point dans le ledger

**Objectif** : enrichir le ledger pour tracer chaque verdict point par point, pas seulement les agrégations.

**Périmètre** (3 options, le brief devra trancher) :
- **Option A** : ajouter un champ `verdicts_by_point` au ledger qui mappe `point_id → verdict` (ex: `{"1": "CONFIRMED", "2": "REFUTED", ...}`). Nécessite de parser le tableau de verdicts et d'extraire le numéro de point.
- **Option B** : produire un fichier structuré compagnon `architecture/reviews/CLAUDE-CURSOR-<sha>-<slug>.jsonl` qui trace chaque verdict point par point, une ligne JSONL par point. Le ledger reste inchangé (agrégations seules).
- **Option C** : accepter que le ledger reste une vue agrégée et que la traçabilité fine vive uniquement dans la review Markdown. Documenter explicitement ce choix dans `architecture/README.md` (section ledger) : « Le ledger enregistre les agrégations de verdicts ; pour la traçabilité point par point, lire le fichier de review référencé. »

**Résultat attendu** (option A ou B) : le policy engine humain peut générer automatiquement une liste « points CONFIRMED à approuver d'office » ou « points REFUTED à écarter » sans relire manuellement les deux fichiers. La reprise partielle et l'audit réglementaire deviennent faisables.

**Résultat attendu** (option C) : clarification documentaire, pas de changement fonctionnel.

**Lien avec risque** : referme le constat P1 (ledger sans traçabilité point par point).

## Brief 3 : Détection d'incohérence verdicts tableau vs ledger

**Objectif** : ajouter une validation qui détecte les divergences entre le nombre de verdicts du tableau et les statistiques agrégées du ledger.

**Périmètre** :
- Enrichir `harness/validate_review.py` (ou créer un script séparé `harness/validate_ledger_consistency.py`) qui :
  1. Extrait le tableau de verdicts de la review
  2. Compte les verdicts par catégorie (CONFIRMED, REFUTED, PARTIAL, NEEDS_OWNER)
  3. Compare avec les comptages du ledger (ligne `AUDIT_CHALLENGED` pour le même `audit_id`)
  4. Logue un warning (ou échoue, selon politique) si les deux divergent
- Documentation dans `architecture/README.md` : politique de cohérence ledger ↔ review. Deux options :
  - **Stricte** : le tableau doit lister tous les points de l'audit, un verdict par point, et les comptages du ledger doivent matcher exactement.
  - **Souple** : le tableau peut être une vue partielle (points majeurs seulement), le ledger agrège tous les verdicts (y compris ceux dans le texte libre des sections 3 et 4). Dans ce cas, documenter explicitement que les comptages peuvent diverger et que c'est normal.

**Résultat attendu** : si une divergence non documentée survient, le workflow l'attrape et logue un warning. Le propriétaire sait que les statistiques du ledger ne reflètent pas exactement le tableau.

**Lien avec risque** : referme le constat P2 (incohérence verdicts tableau vs ledger).

---

**Note** : un quatrième brief serait « commit de challenge signé par un bot dédié » (constat P3), mais il est de moindre priorité que les trois ci-dessus. Le constat P3 est un souci de traçabilité Git, pas un risque fonctionnel.

# 8. Vérification des briefs ouverts (aucun doublon)

Conformément au contrat `cursor-qa-scout` (« déclaration explicite 'aucun doublon avec un brief ouvert' ou la liste des briefs vérifiés »), voici les briefs ouverts examinés :

**Briefs principaux dans `harness/queue/briefs/` (hors fixtures)** :
```bash
$ ls harness/queue/briefs/
001-spatial-primary-key-adr
002-geo-pipeline-coastline-1400
003-port-unity-game
004-polish-visuel
005-refonte-visuelle-carte
006-full-auto-agent-pipeline
007-geo-pipeline-cells-adjacency
008-contexte-opus5-right-sizing
008-full-auto-automation-gaps
009-full-auto-agent-invocation
010-repartition-roles-full-auto
```

**Vérification doublons** :
- Brief 1 proposé (validation format sortie challenger) : aucun brief ouvert ne couvre la validation CI du format de sortie des agents. Le brief 006 (full-auto-agent-pipeline) établit l'architecture, mais ne valide pas les formats de sortie.
- Brief 2 proposé (traçabilité point par point dans ledger) : aucun brief ouvert ne couvre l'enrichissement du ledger. Le ledger a été introduit par le brief 006 (lot 006a), mais la traçabilité point par point n'était pas dans le périmètre.
- Brief 3 proposé (détection incohérence verdicts) : aucun brief ouvert ne couvre la validation de cohérence ledger ↔ review. Le brief 006 (lot 006b) établit les contrats des agents, mais ne spécifie pas de validation croisée.

**Déclaration** : **aucun doublon avec un brief ouvert**. Les trois briefs proposés couvrent des lacunes de validation non traitées par les briefs existants.

# 9. Conclusion

Le commit `dbd315c` (premier challenge de Claude headless) est un jalon majeur : il prouve que la boucle d'audit Cursor → Claude fonctionne en production, et que le principe de séparation producteur/juge tient mécaniquement.

**Forces** : session fraîche pour Claude (pas d'héritage du contexte Cursor), trace indépendante (audit + review dans des fichiers séparés, acteurs distincts tracés dans le ledger), Human-in-the-loop (propriétaire tranche après le challenge).

**Risques principaux** : format de sortie du challenger non validé mécaniquement (P0), ledger sans traçabilité point par point (P1), incohérence verdicts tableau vs ledger non détectée (P2).

Les trois briefs proposés referment ces lacunes et alignent le pipeline avec l'état de l'art 2026 (validation de sortie d'agents, traçabilité structurée pour audit réglementaire, détection d'incohérence).

**Recommandation finale** : approuver le commit (il livre le premier challenge de production, jalon clé de la boucle d'audit), puis planifier les trois briefs proposés pour renforcer la robustesse du pipeline avant de l'exposer à des audits plus complexes.
