---
audit_id: CURSOR-6231186-execution-budgets
auditor: cursor-cloud
target_branch: master
target_commit: 623118671dd98543a197b06415a240b9912999af
created_at: 2026-08-03T18:44:03Z
audit_type: architecture-and-qa
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

État de l'audit : **CURRENT**. Après `git fetch origin master`, `master`, `origin/master` et le commit cible pointent tous sur `623118671dd98543a197b06415a240b9912999af`.

## Cinq risques majeurs

1. **P1 — Le pré-contrôle de taille n'est pas intégré.** `/forge-run` appelle `split-check` sans `--estimated-calls`; le résultat observé est `NO_ESTIMATE`, code retour 0. `NEEDS_SPLIT` retourne également 0. L'orchestrateur ne dispose donc d'aucun contrat machine fiable pour arrêter la génération.
2. **P1 — Le budget est observé, pas imposé.** Le Générateur doit penser à appeler `status`; aucun hook ni superviseur ne coupe l'agent à 160 appels. Un transcript existant mais incompatible peut être compté à zéro et classé `OK`.
3. **P1 — Le backend Cursor n'est pas mesurable par ce budget.** `budget.py` lit uniquement la forme et l'emplacement des transcripts Claude, tandis que le wrapper Cursor ne conserve qu'un JSON final. Le backend pluggable n'a donc pas une garantie de budget équivalente.
4. **P1 — Il n'existe aucune CI QA du dépôt.** Le seul run GitHub visible au commit cible est le Dependency Graph, vert, avec un job Dependabot et aucun artifact de test. Classification : `CI_GREEN_INCOMPLETE`.
5. **P2 — État et reprise ne sont ni transactionnels ni concurrents.** `progress.jsonl`, la sélection du transcript par `mtime`, la numérotation des checkpoints et le déplacement global de `.claude/settings.json` n'ont ni verrou ni écriture atomique.

## Cinq actions au meilleur ROI

1. Définir un contrat CLI distinct et testé pour `SIZE_OK`, `NO_ESTIMATE` et `NEEDS_SPLIT`, puis tester l'appel exact de l'orchestrateur.
2. Ajouter un superviseur déterministe indépendant du LLM qui observe les événements et interrompt proprement le Générateur.
3. Fournir une source d'événements commune aux backends Claude et Cursor, ou déclarer explicitement le budget Cursor non supporté et bloquer son lancement.
4. Créer une CI PR minimale Linux : portée du diff, tests du harness, gate de démonstration négative/positive, validation documentaire, sécurité des workflows.
5. Ajouter les tests négatifs de transcript invalide, progression forgée, concurrence et reprise avant d'augmenter les seuils ou la sophistication.

## Trois incertitudes importantes

1. La protection de `master` et les checks requis sont **UNVERIFIABLE** : l'API GitHub répond `403 Resource not accessible by integration`.
2. Les licences, secrets et runners Unity disponibles sont inconnus; aucun job Unity hébergé n'est donc recommandé comme obligatoire à ce stade.
3. Les chiffres historiques de coût et d'appels sont présents dans le code et les instructions, mais les transcripts source ne sont pas versionnés; leur recalcul indépendant n'a pas été possible.

# 2. Provenance et fraîcheur

- Branche cible : `master`.
- Commit cible complet : `623118671dd98543a197b06415a240b9912999af`.
- Commit court : `6231186`.
- Sujet retenu : `execution-budgets`.
- Fraîcheur : **CURRENT**; zéro commit entre la cible et `origin/master`.
- Branche documentaire : `cursor/audit-6231186-execution-budgets-3f31`. La politique de branche du runner Cloud impose le préfixe `cursor/` et le suffixe `-3f31`; elle prime sur le format `audit/cursor-*` demandé.
- Diff du commit : six fichiers, 1 077 lignes ajoutées selon `git log --stat` : `.claude/agents/forge-generateur.md`, `.claude/agents/forge-planificateur.md`, `.claude/commands/forge-run.md`, `CLAUDE.md`, `harness/budget.py`, `harness/tests/test_budget.py`.
- Fichiers inspectés en profondeur : les six fichiers du commit; `harness/verdict_audit.py`; `harness/harness_audit.py`; `harness/backends/ledger.py`; `harness/backends/run_cursor_generator.sh`; les cinq fichiers de `harness/tests/`; `unity/run-unity.ps1`; échantillons de tests Unity; tests et preuves geo; règles et ADR du harness; métadonnées du projet Unity.
- Inventaire inspecté : fichiers suivis par Git, tests Python, tests NUnit/Unity, scripts PowerShell, hooks Claude, historique des workflows et runs GitHub accessibles.
- Non accessible : paramètres de protection de branche et permissions Actions (403), secrets, licences Unity, facture Actions, runners auto-hébergés, transcripts historiques Claude locaux, historique CI qui n'existe pas sous forme de workflows versionnés.
- Limites : aucune installation; aucun `/forge-run`; aucune génération agentique; aucun test Unity; aucun test PowerShell faute de `pwsh`; aucune modification hors de ce rapport.

## Résultats exécutés

- `.venv/bin/pytest harness/tests/test_budget.py -q` : **24 passed in 0.75s**.
- `.venv/bin/pytest harness/tests/ -q` : **55 passed, 13 skipped in 1.39s**. Les 13 skips sont les tests PowerShell, car aucun exécutable PowerShell n'est disponible.
- `py -m pytest ...` : non démarré, `py: command not found` sur Linux.
- `split-check` sans estimation : `NO_ESTIMATE`, exit 0.
- `split-check --estimated-calls 400` : `NEEDS_SPLIT`, exit 0.
- `status` sans transcript : `UNMEASURABLE`, exit 2.

Ces mesures prouvent les fonctions couvertes sur ce runner; elles ne prouvent ni l'orchestration réelle, ni l'arrêt d'un agent, ni le backend Cursor, ni Unity.

## Résultats CI existants

- Run : [Dependency Graph 30840361290](https://github.com/PLiagre/ForgeHistory/actions/runs/30840361290).
- SHA : commit cible exact.
- Période observée : 2026-08-03 18:14:17Z à 18:15:41Z, soit 84 secondes mesurées.
- Job : `update-pip-graph`; étapes de setup et Dependabot réussies.
- Jobs QA exécutés : aucun.
- Jobs ignorés, retries, flaky tests : aucune donnée.
- Artifacts : zéro.
- Workflows suivis sous `.github/workflows/` : aucun.
- Classification : **CI_GREEN_INCOMPLETE**, pas `CI_GREEN_VERIFIED`.

## Sources externes retenues

Consultées le 2026-08-03. Une date d'activité est une observation GitHub, pas une garantie de maintenance future.

| Source | Classe | Activité/licence | Pratique pertinente | Limite et applicabilité |
|---|---|---|---|---|
| [GitHub Actions — concurrency](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency) | OFFICIAL | Documentation consultée 2026-08-03; licence N/A | Groupes de concurrence et `cancel-in-progress` | Utile pour PR et verrou Unity; ne remplace pas un verrou interne hors Actions. |
| [GitHub Actions — workflow logs](https://docs.github.com/en/actions/how-tos/monitor-workflows/use-workflow-run-logs) | OFFICIAL | Documentation consultée 2026-08-03; licence N/A | Checks, logs, artifacts et diagnostic des runs | Ne garantit pas que les bons jobs existent ou soient requis. |
| [Anthropic — Claude Code hooks](https://docs.anthropic.com/en/docs/claude-code/hooks) | OFFICIAL | Documentation consultée 2026-08-03; licence N/A | `PreToolUse` peut bloquer avant l'effet | Applicable au backend Claude; pas une preuve de compatibilité Cursor. |
| [Cursor — Headless CLI](https://cursor.com/docs/cli/headless) | OFFICIAL | Documentation consultée 2026-08-03; licence N/A | `stream-json` expose les événements de tool calls | Candidat pour mesurer Cursor; le schéma doit être contractuellement testé. |
| [Unity 6.0 — licence en ligne de commande](https://docs.unity3d.com/6000.0/Documentation/Manual/ManagingYourUnityLicense.html) | OFFICIAL | Page bâtie 2026-07-30; licence documentaire Unity | Une licence activée est requise; contraintes Pro/Personal et OS | Interdit de supposer qu'un runner standard est prêt ou licencié. |
| [game-ci/unity-test-runner](https://github.com/game-ci/unity-test-runner) | OPEN_SOURCE_PRACTICE | Push 2026-06-20; MIT | Exécution et publication de tests Unity en Actions | Tiers; exige validation de licence, version, cache et secrets avant adoption. |
| [rhysd/actionlint](https://github.com/rhysd/actionlint) | OPEN_SOURCE_PRACTICE | Push 2026-07-16; MIT | Validation statique des workflows | Ne prouve pas leur comportement ni les règles de protection. |
| [boxed/mutmut](https://github.com/boxed/mutmut) | OPEN_SOURCE_PRACTICE | Push 2026-08-02; BSD-3-Clause | Mutation ciblée du code Python | À réserver au nightly et à `budget.py`; coût supérieur à pytest. |
| [step-security/harden-runner](https://github.com/step-security/harden-runner) | OPEN_SOURCE_PRACTICE | Push 2026-08-03; Apache-2.0 | Observation/réduction des effets réseau du runner | Tiers et potentiellement coûteux; à évaluer, pas à rendre obligatoire d'emblée. |
| [actions/dependency-review-action](https://github.com/actions/dependency-review-action) | OPEN_SOURCE_PRACTICE | Push 2026-08-01; MIT | Refus de dépendances vulnérables/licences indésirables en PR | Ne couvre ni secrets ni dépendances non reconnues par GitHub. |

# 3. Architecture actuelle

## Composants et responsabilités

| Composant | Responsabilité observée | Nature du contrôle |
|---|---|---|
| Planificateur | Écrit brief et rubric, estime les appels, juge l'indépendance des lots | LLM pour le découpage; script consultatif |
| Générateur Claude/Cursor | Implémente, mesure, journalise, enregistre sa progression | LLM; auto-déclaration de la progression |
| `budget.py` | Trouve un transcript, compte appels, classe seuils, écrit ledger/checkpoint, signale taille | Déterministe une fois les entrées acceptées |
| `verdict_audit.py` | Neuf contrôles mécaniques sur les livrables | Déterministe, exit 0/1/2 |
| Évaluateur | Juge après acceptation mécanique | LLM indépendant en intention |
| `/forge-run` | Décrit la boucle, les branches d'état et les arrêts | Markdown interprété, pas automate exécutable |
| `ledger.py` | Attribue invocations et tokens Claude | Mesuré pour Claude; Cursor non observable |
| `run_cursor_generator.sh` | Prépare prompt, désactive temporairement les hooks Claude, lance Cursor | Effets globaux sur le worktree; trap de restauration |
| `run-unity.ps1` | Attend Unity en un appel, propage codes, résume les logs | Déterministe mais Windows seulement dans l'état actuel |

## Flux et états

Flux déclaré : Planificateur → `split-check` → Générateur → `status/progress/checkpoint` → gate mécanique → Évaluateur → feedback/retry.

États budget : `OK`, `WARN`, `CHECKPOINT_DUE`, `BUDGET_EXHAUSTED`, `NO_PROGRESS_STOP`, `UNMEASURABLE`; état de taille : `SIZE_OK`, `NO_ESTIMATE`, `NEEDS_SPLIT`; états gate : `ACCEPT`, `REJECT`, erreur interne.

Mémoire persistante : brief/rubric, `progress.jsonl`, `checkpoint-NNN.md`, manifest/log/verdict/feedback, run report, ledger de coût. Mémoire externe cachée : arborescence et schéma des transcripts sous `~/.claude`, état d'auth Cursor, licence Unity et fichiers `Logs/`.

Effets de bord : append JSONL non verrouillé, création de checkpoints, déplacement de `.claude/settings.json`, lancement d'agents et Unity, écritures de ledger. Les worktrees ne sont pas orchestrés. Aucun verrou de brief ou Unity n'est présent dans le harness.

Points de reprise : checkpoint à neuf sections et dépôt. Le checkpoint est créé vide puis rempli par le Générateur; aucune validation ne garantit qu'une session neuve peut réellement reprendre. Aucun rollback fonctionnel n'est défini; seul le trap du wrapper Cursor tente de restaurer les hooks.

# 4. Cartographie des tests existants

| Zone | Type de test | Fichiers | Garantie réelle | Lacune | Confiance |
|---|---|---|---|---|---|
| Budget | UNIT + CLI subprocess | `harness/tests/test_budget.py` | Seuils, exits de `status`, compte tool/API, transcript absent, cinq kinds, preuve non vide, checkpoint, signaux split | Pas d'intégration `/forge-run`, pas de schéma réel/versionné, pas Cursor, concurrence, interruption ou preuve authentifiée | HIGH sur fonctions; LOW système |
| Gate | CONTRACT + négatif | `test_verdict_audit.py`, démos fake/honest | Les neuf checks peuvent rougir; exit 0/1/2 exercés | Pas exécuté en CI; certains contrôles reposent sur mtime/manifest déclaratif | HIGH local |
| Ledger | UNIT + CLI | `test_ledger_tokens.py` | Déduplication, attribution, modèle inconnu, JSON, absence transcript | Pas de coût Cursor, pas de concurrence append, table de prix non validée automatiquement | HIGH local |
| Orchestration | Scan documentaire indirect | `test_single_source_of_instruction.py`, `test_run_unity.py` | Interdit certaines instructions dupliquées/polling | `/forge-run` n'est jamais joué comme machine à états | LOW |
| Hooks | Aucun test direct | `.claude/hooks/*.py` | Présence détectée par `harness_audit.py` | Entrées invalides autorisées, contournement shell, hooks Cursor désactivés | LOW |
| Harness maturity | Auto-audit de présence/pattern | `harness/harness_audit.py` | Rapport 0 constant; présence de composants | Pas une gate; « présent » peut être non fonctionnel | LOW |
| PowerShell | INTEGRATION avec faux Unity | `test_run_unity.py` | Exit, timeout, arbre de processus, résumé borné sur Windows | 13 tests skipped ici; pas de Pester natif; pas de matrice CI | MEDIUM |
| Unity Edit/Play mode | NUnit/Unity Test Framework | `unity/game_unity/Assets/Tests/**` | Beaucoup de comportements, déterminisme, parité et budgets existent dans les sources | Aucun résultat au commit cible; fixtures Logs parfois externes; licence/runner inconnus | LOW sur exécution |
| Geo | Scripts de preuve rouge + proofs | `pipeline/geo/tests/*`, `pipeline/geo/qa/checks.py` | Cas volontairement cassés et déterminisme dans scripts dédiés | Aucun `def test_` pytest; non collectés par la suite harness | MEDIUM sur code, LOW CI |
| Sécurité | Règles de prompt et hooks | agents, hooks, wrapper Cursor | Quelques gardes locales | Aucun secret scan, CodeQL, dependency review, pinning d'actions ou test d'injection | LOW |
| Mutation | Mutations manuelles ponctuelles | budget Unity et cas rouges geo | Certaines garanties démontrent leur morsure | Aucun moteur ni score de mutation pour `budget.py`/gate | LOW global |
| Reprise/rollback | Tests de non-écrasement séquentiel | `test_budget.py` | Deux checkpoints séquentiels ont des noms distincts | Pas de crash, atomicité, reprise neuve, rollback ou double writer | LOW |
| Concurrence/worktrees | Aucun | — | Aucune | Sélection par `mtime`, append et déplacement settings non protégés | NONE |
| Multiplateforme | Skip PowerShell conditionnel | `test_run_unity.py` | La suite reste verte sans PowerShell | Le skip masque l'absence de garantie Windows; `py` absent sous Linux | LOW |
| Non-régression | Tests ciblés et nombreuses fixtures Unity | plusieurs | Régressions connues encodées | Aucun pipeline reproductible ne les exécute | MEDIUM local, NONE CI |

# 5. Matrice de risques QA

| Risque | Probabilité | Impact | Détection actuelle | Test recommandé | Priorité |
|---|---|---|---|---|---|
| `NO_ESTIMATE` traité comme permission de générer | HIGH | HIGH | Sortie prose uniquement | BUD-CONTRACT-001 | P1 |
| Transcript incompatible compté à zéro/`OK` | MEDIUM | HIGH | Aucun | BUD-TRANSCRIPT-001 | P1 |
| Backend Cursor sans budget | HIGH | HIGH | `UNMEASURABLE` seulement si appelé | BUD-BACKEND-001 | P1 |
| Faux progrès par chaîne non vide | HIGH | MEDIUM | Teste seulement vide/non vide | BUD-PROGRESS-001 | P1 |
| Double writer corrompt ledger/checkpoints | MEDIUM | MEDIUM | Aucun | BUD-CONCURRENCY-001 | P2 |
| Checkpoint inutilisable après interruption | MEDIUM | HIGH | Présence des titres | BUD-RECOVERY-001 | P1 |
| Suite verte avec tests Windows skipped | HIGH sur Linux | MEDIUM | Skip visible localement, absent CI | PLATFORM-001 | P1 |
| PR modifie workflow et contourne ses propres checks | MEDIUM | HIGH | Aucun workflow | CI-SCOPE-001 | P1 |
| Coût dérive malgré seuil fixe | MEDIUM | MEDIUM | Chiffres historiques manuels | COST-REGRESSION-001 | P2 |
| Unity parallèle corrompt `Library/` ou licence | MEDIUM si CI ajoutée | HIGH | Aucun job/verrou | UNITY-CONCURRENCY-001 | P2 préalable |

# 6. Constats d’architecture

## FINDING-ARCH-001 — `NEEDS_SPLIT` n'a pas de contrat d'orchestration

- Priorité : P1
- Confiance : HIGH
- Source : PERSONAL_INFERENCE
- Fichiers concernés : `.claude/commands/forge-run.md`, `harness/budget.py`, `harness/tests/test_budget.py`
- Risque : génération monolithique malgré le pré-contrôle
- Complexité : faible à moyenne
- Rollback : revenir au contrat d'exit antérieur et conserver l'ancien test CLI

### Observation

L'appel prescrit omet l'unique entrée déclenchante, `--estimated-calls`. Le script renvoie 0 pour `NO_ESTIMATE`, `SIZE_OK` et `NEEDS_SPLIT`.

### Preuve

Exécution observée sur le brief 005 : sans estimation, `NO_ESTIMATE`, exit 0; avec 400, `NEEDS_SPLIT`, exit 0. Aucun test n'exécute la ligne exacte de `/forge-run`.

### Conséquence

La branche « ne pas lancer le Générateur » dépend de l'interprétation du LLM et non d'une condition machine.

### Recommandation minimale

Définir un schéma JSON et des exits distincts, rendre l'estimation obligatoire avant lancement, puis tester l'appel de bout en bout sans agent.

### Alternatives

Faire porter la décision au Planificateur dans un fichier d'état validé par JSON Schema; refuser tout fichier sans estimation.

### Critères d’acceptation

`NO_ESTIMATE` et `NEEDS_SPLIT` sont non-zéro et distincts; `SIZE_OK` seul autorise la suite; un test joue l'orchestrateur.

### Métriques avant/après

Avant mesuré : 0/2 états bloquants ont un exit bloquant. Après attendu : 2/2.

## FINDING-ARCH-002 — Le plafond d'appels est coopératif

- Priorité : P1
- Confiance : HIGH
- Source : OFFICIAL
- Fichiers concernés : `harness/budget.py`, `.claude/agents/forge-generateur.md`, `.claude/commands/forge-run.md`
- Risque : dépassement silencieux et coût non borné
- Complexité : moyenne
- Rollback : conserver `status` comme diagnostic si le superviseur est retiré

### Observation

Le script classe un compteur lorsqu'il est invoqué; il ne reçoit aucun événement en continu et n'interrompt rien. Anthropic documente que `PreToolUse` peut bloquer avant un outil, mais aucun hook budget n'est câblé.

### Preuve

Les instructions disent « check it when you finish a step ». `/forge-run` délègue le contrôle au Générateur. Aucun code d'orchestration n'appelle `status` avant chaque effet.

### Conséquence

Un agent qui oublie, boucle ou perd le contexte peut dépasser 160 appels. Le garde placé après plusieurs effets ne borne pas ces effets.

### Recommandation minimale

Superviser les événements hors du LLM et refuser le prochain outil au seuil; conserver une marge pour écrire un checkpoint atomique.

### Alternatives

Limiter le nombre de tours au niveau du SDK/backend; utiliser le flux `stream-json` Cursor et les hooks Claude derrière une interface commune.

### Critères d’acceptation

Un faux agent qui tente 161 appels est bloqué avant le 161e; le checkpoint est écrit ou un état `checkpoint_failed` explicite est produit.

### Métriques avant/après

Avant : nombre maximal effectivement imposé = non borné. Après : plafond vérifié ≤ seuil + marge documentée.

## FINDING-ARCH-003 — Mesure non portable entre backends et schémas

- Priorité : P1
- Confiance : HIGH
- Source : OFFICIAL
- Fichiers concernés : `harness/budget.py`, `harness/backends/run_cursor_generator.sh`, `harness/backends/ledger.py`
- Risque : faux `OK`, attribution erronée, backend Cursor sans contrôle
- Complexité : moyenne
- Rollback : bloquer Cursor avec `UNSUPPORTED_BUDGET_SOURCE`

### Observation

Le budget recherche les quatre premières lignes de transcripts Claude et choisit le plus récent par `mtime`. Un fichier lisible sans messages `assistant.usage` produit zéro appel, pas `UNMEASURABLE`. Cursor propose officiellement `stream-json`, mais le wrapper demande un JSON final.

### Preuve

`ledger.py` reconnaît explicitement que les tokens Cursor ne sont pas observables. `find_agent_transcript` documente qu'il devient faux en concurrence.

### Conséquence

La même politique a des garanties différentes selon le backend; une évolution de schéma peut transformer « inconnu » en « zéro ».

### Recommandation minimale

Créer un format d'événements interne versionné, valider au moins un événement compatible avant `OK`, et adapter chaque backend.

### Alternatives

Déclarer Cursor non compatible budget et interdire ce backend pour les briefs soumis au plafond.

### Critères d’acceptation

Fixtures réelles anonymisées Claude/Cursor; schéma inconnu, fichier vide, permission refusée et concurrence échouent fermement.

### Métriques avant/après

Avant : 1/2 backends mesurable en intention. Après : 2/2, ou 1/1 autorisé et Cursor explicitement bloqué.

## FINDING-ARCH-004 — Progression et checkpoints sont déclaratifs

- Priorité : P1
- Confiance : HIGH
- Source : PERSONAL_INFERENCE
- Fichiers concernés : `harness/budget.py`, `.claude/agents/forge-generateur.md`
- Risque : reset frauduleux du no-progress et reprise impossible
- Complexité : moyenne
- Rollback : ignorer le ledger nouveau et relire les checkpoints existants

### Observation

La « preuve » de progression est uniquement une chaîne non vide. Le script ne vérifie ni commande, ni fichier, ni monotonie de `tool_calls_at`. Le checkpoint contient neuf sections vides et n'est pas validé.

### Preuve

`append_progress` persiste l'entrée telle quelle; `cmd_status` prend le dernier événement sans validation. Les tests acceptent une phrase arbitraire comme preuve.

### Conséquence

Une entrée future, régressive ou forgée peut maintenir `OK`; un checkpoint peut exister mais ne transmettre aucun contexte exploitable.

### Recommandation minimale

Valider monotonie, borne et type d'évidence; lier l'évidence à un artifact/hash/exit; ajouter un validateur de checkpoint.

### Alternatives

Supprimer le no-progress automatique tant que ses entrées ne sont pas authentifiables; le garder comme métrique informative.

### Critères d’acceptation

Les événements forgés, futurs et régressifs sont refusés; une reprise en processus neuf termine un lot synthétique sans transcript.

### Métriques avant/après

Avant : 0 type de preuve vérifié mécaniquement. Après : 5/5 types ont au moins un prédicat vérifiable, ou sont renommés « déclaratifs ».

## FINDING-ARCH-005 — Absence d'isolation, verrou et rollback concurrent

- Priorité : P2
- Confiance : HIGH
- Source : PERSONAL_INFERENCE
- Fichiers concernés : `harness/budget.py`, `harness/backends/run_cursor_generator.sh`, `unity/run-unity.ps1`
- Risque : corruption d'état, hooks laissés désactivés, collision Unity
- Complexité : moyenne
- Rollback : sérialiser globalement avant d'introduire des worktrees

### Observation

Deux processus peuvent choisir le même `checkpoint-001.md`, entrelacer `progress.jsonl`, sélectionner le mauvais transcript et déplacer le même fichier settings. Aucun lock Unity n'existe.

### Preuve

Numéro de checkpoint dérivé de `len(glob)` puis `write_text`; append sans lock; settings déplacé vers un nom global fixe; aucun `worktree`/lock dans le harness.

### Conséquence

Les retries et parallélismes futurs peuvent produire faux état, perte de hooks ou `Library/` concurrente.

### Recommandation minimale

Un lock par brief et un lock Unity global, écriture temp + rename atomique, identifiant de run explicite; ne pas partager un worktree entre générateurs.

### Alternatives

Interdire toute concurrence dans l'orchestrateur et dans Actions avec un groupe de concurrence unique.

### Critères d’acceptation

Deux writers simultanés produisent deux checkpoints intacts; une interruption restaure settings; deux jobs Unity ne se chevauchent pas.

### Métriques avant/après

Avant : 0 ressource partagée verrouillée. Après : 3/3 (`progress`, checkpoint, Unity/settings) protégées ou explicitement sérialisées.

# 7. Constats QA

## FINDING-QA-001 — Contrat CLI et orchestration non testés

- Priorité : P1
- Type de test : CONTRACT, INTEGRATION, END_TO_END
- Environnement : PR_REQUIRED
- Confiance : HIGH
- Risque couvert : faux lancement après `NO_ESTIMATE`/`NEEDS_SPLIT`
- Coût estimé : faible
- Durée cible : < 5 s
- Déterministe : YES

### Lacune observée

Les tests cherchent des mots dans stdout mais ne vérifient pas l'exit de `split-check` ni le branchement de `/forge-run`.

### Preuve

Les deux états observés retournent 0.

### Scénario de test proposé

- Identifiant : `BUD-CONTRACT-001`
- Préconditions : brief minimal; orchestrateur déterministe ou adaptateur CLI.
- Entrée : aucune estimation, estimation 150, 151 et 400.
- Action : exécuter le préflight puis la décision de lancement.
- Résultat attendu : absence/oversize bloquent; 150 suit la règle de frontière explicitée; aucun Générateur factice n'est appelé dans les cas bloquants.
- Priorité : P1; déterministe; coût faible; Linux et Windows; chaque PR touchant harness/orchestration.
- Non-doublon : les tests actuels ne jouent pas le consommateur du statut.

### Résultat attendu

Un seul état autorise le lancement; la décision est machine-readable.

### Faux positifs possibles

Faux adaptateur qui ne représente pas le vrai point d'entrée.

### Faux négatifs possibles

Modification de Markdown non reliée à l'adaptateur.

### Critères d’acceptation

Test black-box sur la commande réellement utilisée et mutation de chaque exit qui fait rougir.

## FINDING-QA-002 — Transcripts invalides et backend Cursor non couverts

- Priorité : P1
- Type de test : ADVERSARIAL, SECURITY, CONTRACT
- Environnement : PR_REQUIRED
- Confiance : HIGH
- Risque couvert : sous-comptage et contournement de budget
- Coût estimé : faible à moyen
- Durée cible : < 10 s
- Déterministe : YES

### Lacune observée

Seul le transcript absent est testé; pas le fichier vide, illisible, tronqué, sans `usage`, multi-candidat ou Cursor.

### Preuve

Le parseur ignore silencieusement lignes malformées et messages sans usage.

### Scénario de test proposé

- Identifiant : `BUD-TRANSCRIPT-001`
- Préconditions : fixtures anonymisées versionnées pour chaque backend et version de schéma.
- Entrée : vide, JSON tronqué, usage absent, compteur décroissant, deux transcripts même brief, symlink hors racine.
- Action : appeler `status`.
- Résultat attendu : `UNMEASURABLE`/`AMBIGUOUS`, jamais `OK`; chemin hors racine refusé; sélection liée à un run id.
- Priorité : P1; déterministe; coût faible; PR_REQUIRED; sans réseau.
- Non-doublon : l'existant ne couvre que « répertoire inexistant ».

### Résultat attendu

Fail closed pour toute source non validée.

### Faux positifs possibles

Une évolution officielle de schéma exige une nouvelle fixture.

### Faux négatifs possibles

Fixture synthétique différente des sorties réelles.

### Critères d’acceptation

Au moins une fixture capturée par backend; mutation « ignorer usage » tuée.

## FINDING-QA-003 — Preuve de progression falsifiable

- Priorité : P1
- Type de test : UNIT, MUTATION, ADVERSARIAL
- Environnement : PR_REQUIRED
- Confiance : HIGH
- Risque couvert : reset arbitraire du no-progress
- Coût estimé : faible
- Durée cible : < 5 s
- Déterministe : YES

### Lacune observée

Une chaîne comme `lots done` serait acceptée avec un kind autorisé.

### Preuve

Seul `evidence.strip()` est vérifié.

### Scénario de test proposé

- Identifiant : `BUD-PROGRESS-001`
- Préconditions : ledger à 40 appels et validateur d'évidence.
- Entrée : preuve inexistante, hash faux, exit non-zéro, `tool_calls_at=-1`, futur et régressif.
- Action : enregistrer puis reclassifier.
- Résultat attendu : chaque entrée invalide est refusée sans reset; une preuve réelle validée reset exactement au compteur courant.
- Priorité : P1; déterministe; coût faible; LOCAL_FAST + PR_REQUIRED.
- Non-doublon : le test actuel ne distingue que chaîne vide/non vide.

### Résultat attendu

Les mutants supprimant une validation sont tués.

### Faux positifs possibles

Artifacts légitimes déplacés après mesure.

### Faux négatifs possibles

Artifact valide mais sans lien causal avec le progrès déclaré.

### Critères d’acceptation

Monotonie et borne vérifiées; au moins une mutation par kind.

## FINDING-QA-004 — Concurrence et reprise non prouvées

- Priorité : P1
- Type de test : CONCURRENCY, RECOVERY
- Environnement : NIGHTLY
- Confiance : HIGH
- Risque couvert : perte d'état et handoff inutilisable
- Coût estimé : moyen
- Durée cible : < 2 min sans agent réel
- Déterministe : YES

### Lacune observée

Le test séquentiel de deux checkpoints ne couvre aucune course ni interruption.

### Preuve

Les écritures utilisent glob/len, append et move global sans verrou.

### Scénario de test proposé

- Identifiant : `BUD-CONCURRENCY-001`
- Préconditions : dossier temporaire, deux processus synchronisés par barrier.
- Entrée : deux progressions/checkpoints simultanés; interruption entre temp et rename; settings présent.
- Action : lancer les writers, tuer précisément le processus au point injecté, reprendre dans un processus neuf.
- Résultat attendu : JSONL parseable, IDs uniques, settings restauré, aucun checkpoint partiel; reprise sans transcript.
- Priorité : P1; déterministe avec synchronisation; coût moyen; NIGHTLY Linux + Windows.
- Non-doublon : aucun test multiprocess/recovery existant.

### Résultat attendu

État final unique, complet et reprenable.

### Faux positifs possibles

Filesystem local plus fort que le filesystem du runner réel.

### Faux négatifs possibles

Synchronisation basée sur sommeil au lieu d'une barrier.

### Critères d’acceptation

100 répétitions sans corruption; injection d'interruption déterministe, pas probabiliste.

## FINDING-QA-005 — Contrat multiplateforme masqué par skips

- Priorité : P1
- Type de test : INTEGRATION, COMPATIBILITY
- Environnement : PR_REQUIRED
- Confiance : HIGH
- Risque couvert : suite verte sans exécuter PowerShell et commande `py` absente
- Coût estimé : faible à moyen
- Durée cible : Linux < 2 min; Windows < 5 min
- Déterministe : YES

### Lacune observée

Sur Linux, 13 tests passent en skip et `py` est absent; aucun job Windows n'existe.

### Preuve

Résultat local observé : 55 passed, 13 skipped; `pwsh` et `py` introuvables.

### Scénario de test proposé

- Identifiant : `PLATFORM-001`
- Préconditions : matrice Ubuntu/Windows sans Unity.
- Entrée : suite harness et faux Unity PowerShell.
- Action : exécuter avec la commande officielle de chaque OS; interdire les skips PowerShell sur Windows.
- Résultat attendu : harness vert sur les deux OS; 13 tests PowerShell exécutés sur Windows; un skip inattendu échoue.
- Priorité : P1; déterministe; coût moyen; PR_REQUIRED par chemins harness/unity wrapper.
- Non-doublon : aucun environnement CI n'exécute aujourd'hui ces contrats.

### Résultat attendu

Rapports JUnit séparés par OS.

### Faux positifs possibles

Image Windows différente du poste Unity final.

### Faux négatifs possibles

Le faux Unity ne couvre pas une incompatibilité Editor.

### Critères d’acceptation

Zéro skip non allow-listé dans le job Windows; versions de runtime publiées.

## FINDING-QA-006 — Régression de performance et coût non recalculée

- Priorité : P2
- Type de test : PERFORMANCE, COST_REGRESSION
- Environnement : NIGHTLY
- Confiance : MEDIUM
- Risque couvert : seuils obsolètes et coût croissant
- Coût estimé : moyen, sans LLM en PR
- Durée cible : < 5 min pour replay synthétique
- Déterministe : PARTIAL

### Lacune observée

Les seuils et calibrations sont des constantes issues de cinq observations non rejouables ici.

### Preuve

Les sources historiques sont commentées; les transcripts nécessaires ne sont pas versionnés.

### Scénario de test proposé

- Identifiant : `COST-REGRESSION-001`
- Préconditions : fixtures de transcripts anonymisées, corpus versionné et provenance.
- Entrée : corpus historique + mutations de taille/contexte.
- Action : recalculer appels, aire sous la courbe de contexte et classification.
- Résultat attendu : mêmes mesures sur fixture; alerte informative si distribution franchit les marges; aucune facture inventée.
- Priorité : P2; calcul déterministe, seuil statistique partiel; coût moyen; NIGHTLY.
- Non-doublon : les tests actuels vérifient des nombres codés, pas leur dérivation.

### Résultat attendu

Rapport JSON avec provenance, médiane, quantiles et limites; pas de LLM.

### Faux positifs possibles

Corpus historique non représentatif des briefs futurs.

### Faux négatifs possibles

Coût fournisseur change sans changement de tokens.

### Critères d’acceptation

Toute mesure est reproductible depuis une fixture; prix séparés des unités physiques.

## FINDING-QA-007 — Qualité du handoff exige une validation humaine ciblée

- Priorité : P2
- Type de test : END_TO_END, MANUAL_QA
- Environnement : MANUAL_QA
- Confiance : MEDIUM
- Risque couvert : redécouverte du contexte malgré checkpoint valide
- Coût estimé : temps humain modéré, exécution rare
- Durée cible : non mesurée; à chronométrer lors du pilote
- Déterministe : NO

### Lacune observée

La présence de neuf titres ne mesure ni précision ni suffisance.

### Preuve

Le template est généré vide; aucun test ne fait reprendre un lot par une session sans transcript.

### Scénario de test proposé

- Identifiant : `HANDOFF-MANUAL-001`
- Préconditions : lot pilote interrompu au checkpoint, reviewer sans accès au transcript.
- Entrée : dépôt + checkpoint uniquement.
- Action : faire exécuter l'action suivante par un humain/agent distinct, puis noter questions et fichiers redécouverts.
- Résultat attendu : reprise correcte; zéro recours au transcript; toutes incertitudes déclarées; temps et relectures mesurés.
- Priorité : P2; probabiliste/humain; coût modéré; MANUAL_QA à chaque changement de format, puis échantillonnage release.
- Non-doublon : la structure est testée, pas l'utilité.

### Résultat attendu

Mesures avant/après : temps de reprise, nombre de questions, fichiers relus, erreurs de décision.

### Faux positifs possibles

Reviewer déjà familier du brief.

### Faux négatifs possibles

Lot pilote trop simple.

### Critères d’acceptation

Pilote en aveugle sur au moins un lot réaliste; seuils humains décidés avant l'essai.

# 8. Architecture GitHub Actions cible

## Workflow `pr-scope-and-docs`

- Déclencheur : `pull_request` vers `master`.
- Jobs : `changed-paths`, `cursor-audit-contract`, `workflow-policy`.
- Dépendances : checkout en lecture seule; actionlint ou script local piné.
- Runner : `ubuntu-latest`.
- Timeout : 5 min/job.
- Cache : aucun.
- Artifacts : résumé JSON des chemins et erreurs, rétention courte.
- Statut : obligatoire.
- Chemins : toujours; règles renforcées pour `architecture/inbox/CURSOR-*.md` et `.github/workflows/**`.
- Succès : une PR documentaire Cursor modifie exactement un audit correctement nommé/frontmatter; une PR workflow ne peut masquer les checks.
- Échec : chemin hors périmètre, frontmatter invalide, action non pinée, permissions trop larges.
- Retry : aucun retry automatique; rerun humain uniquement sur panne GitHub.
- Concurrence : `${workflow}-${pull_request}`, annulation des anciens runs.
- Coût relatif : très faible.
- Flakiness : faible.

## Workflow `harness-pr`

- Déclencheur : PR avec changements `harness/**`, `.claude/**`, `unity/run-unity.ps1`, règles du harness.
- Jobs : `pytest-linux`, `gate-fake-red`, `gate-honest-green`, `budget-cli-contract`, publication JUnit.
- Dépendances : Python version explicitée; dépendances verrouillées existantes.
- Runner : Ubuntu.
- Timeout : 10 min.
- Cache : packages par lock + OS + version Python; jamais résultats de tests.
- Artifacts : JUnit, sorties gate, versions runtime; même en échec.
- Statut : obligatoire.
- Succès : aucun skip inattendu hors allow-list; exits attendus; artifacts présents.
- Échec : test rouge, gate faux/honnête inversée, artifact absent, `NO_ESTIMATE` permissif.
- Retry : zéro pour échec test; un rerun permis pour incident runner annoté.
- Concurrence : annuler ancien run de la même PR.
- Coût relatif : faible.
- Flakiness : faible si réseau exclu.

## Workflow `windows-contract`

- Déclencheur : mêmes chemins, PR.
- Jobs : harness sous Windows, `test_run_unity.py` avec faux Unity.
- Runner : `windows-latest`; aucune licence Unity.
- Timeout : 10 min.
- Cache : dépendances Python par lock.
- Artifacts : JUnit et stdout borné.
- Statut : obligatoire pour harness/wrapper, informatif ailleurs.
- Succès : tests PowerShell exécutés, zéro skip inattendu.
- Échec : code retour perdu, timeout/orphelin, absence PowerShell.
- Retry : aucun pour assertions; rerun manuel pour incident image.
- Concurrence : par PR.
- Coût relatif : moyen.
- Flakiness : moyenne pour test process-tree; mesurer avant statut obligatoire global.

## Workflow `security-and-dependencies`

- Déclencheur : PR; hebdomadaire pour analyse complète.
- Jobs : dependency review, secret scan selon fonctionnalités disponibles, pinning SHA, permissions minimales, analyse statique Python/shell/PowerShell.
- Runner : Ubuntu.
- Timeout : 15 min.
- Cache : bases d'outils versionnées, pas de cache de verdict.
- Artifacts : SARIF/JSON et résumé.
- Statut : dependency review/pinning/scope obligatoires; scan avancé informatif jusqu'à calibration.
- Succès : aucune nouvelle vulnérabilité au seuil décidé, aucun secret, actions pinées.
- Échec : détection vérifiée par fixture canari non secrète.
- Retry : aucun sur résultat; retry borné réseau uniquement.
- Concurrence : annuler ancien run PR.
- Coût relatif : faible à moyen.
- Flakiness : réseau et bases externes; distinguer infrastructure/code.

## Workflow `nightly-resilience`

- Déclencheur : planifié et manuel.
- Jobs : mutation ciblée `budget.py`/gate, 100 répétitions concurrence, recovery injecté, replay coût/performance, proofs geo.
- Runner : Ubuntu + Windows ciblé.
- Timeout : 30 min/job.
- Cache : dépendances uniquement.
- Artifacts : mutation report, timings, fixtures de sortie, état final.
- Statut : informatif avec création d'incident humain, pas blocage rétroactif.
- Succès : score mutation pré-défini, aucune corruption, budgets de durée hors zone de bruit.
- Échec : mutant critique survivant, corruption, variance au-delà de marge.
- Retry : répétitions font partie du test; pas de retry masquant.
- Concurrence : un nightly actif; conserver le plus récent.
- Coût relatif : moyen.
- Flakiness : contrôlée par barriers et marges mesurées.

## Workflow `unity-validation`

- Déclencheur : manuel, nightly ou release; chemins Unity; pas chaque petite PR avant validation d'infrastructure.
- Jobs : EditMode ciblé, PlayMode ciblé, suite release, artifact validation.
- Dépendances : décision préalable sur licence, runner, OS, Unity `6000.0.43f1`, secrets, taille/cache `Library/`, coûts.
- Runner : de préférence Windows auto-hébergé dédié ou file externe signée; GitHub standard seulement après pilote concluant.
- Timeout : 45 min ciblé, 90 min release, à recalibrer sur mesures.
- Cache : `Library/` par version Unity + manifests + projet; jamais résultats/logs comme succès.
- Artifacts : NUnit XML, log Unity complet, résumé, captures requises.
- Statut : informatif pendant pilote; obligatoire release une fois fiabilité et licence établies.
- Succès : Unity exit 0, XML présent/parseable, zéro failed, artifacts attendus présents.
- Échec : licence indisponible classée infrastructure; tests rouges classés code; XML absent jamais succès.
- Retry : aucun automatique pour test; un retry infrastructure après classification.
- Concurrence : groupe global `unity-6000.0.43f1`, file FIFO; jamais deux jobs sur le même `Library/`.
- Coût relatif : élevé et non mesuré.
- Flakiness : potentiellement élevée; pilote et historique requis.

## Workflow `release-assurance`

- Déclencheur : tag candidat ou manuel protégé.
- Jobs : toutes gates précédentes, Unity complet, déterminisme répété, compatibilité Windows, rollback/checkpoint, validation artifacts.
- Runner : combinaison GitHub et runner Unity dédié.
- Timeout : somme bornée par job, pas un timeout monolithique.
- Cache : dépendances/Library seulement.
- Artifacts : bundle de preuves signé par SHA, JUnit/NUnit, logs, manifest.
- Statut : obligatoire avant release.
- Succès : chaque besoin a son artifact et son hash; aucune étape `continue-on-error`.
- Échec : absence ou incohérence d'artifact, test rouge, rollback non démontré.
- Retry : uniquement infrastructure classifiée.
- Concurrence : une release par version.
- Coût relatif : élevé, fréquence faible.
- Flakiness : à réduire avant obligation.

# 9. Pipeline recommandé

Claude local  
→ `LOCAL_FAST` ciblé avec commande compatible OS  
→ commit/push  
→ `pr-scope-and-docs` + `harness-pr` + sécurité  
→ `windows-contract` selon chemins  
→ artifacts JUnit/gate et résumé Actions  
→ audit Cursor documentaire indépendant  
→ décision humaine  
→ brief atomique éventuel  
→ nightly résilience  
→ Unity en file dédiée si licence/runner validés  
→ release assurance.

Le gate mécanique reste avant l'Évaluateur. GitHub Actions reproduit les garanties déterministes; aucun LLM n'est utilisé pour remplacer un test. L'audit Cursor reste informatif et humainement autorisé.

# 10. Briefs proposés à Claude

Ces briefs sont **proposés, non autorisés**.

## BRIEF-PROP-001 — Contrat machine du budget et superviseur

- Finding source : ARCH-001, ARCH-002, QA-001.
- Objectif : rendre `NO_ESTIMATE`, `NEEDS_SPLIT` et le hard stop impossibles à ignorer.
- Contexte vérifié : exits identiques observés; orchestration Markdown non testée.
- Périmètre : contrat CLI/JSON, adaptateur d'orchestration déterministe, arrêt avant outil, tests black-box.
- Hors périmètre : nouveaux seuils, coût fournisseur, Unity, refonte des rôles.
- Fichiers probablement concernés : `harness/budget.py`, nouveau module d'orchestration sous `harness/`, tests, instructions comme pointeurs.
- Tests à ajouter : `BUD-CONTRACT-001`, test 161e appel, mutations d'exits.
- Modifications CI éventuelles : job `budget-cli-contract` dans un brief CI séparé si séparation requise.
- Critères d'acceptation : un seul état permissif; arrêt réellement exercé; exit interne jamais succès.
- Métriques : états bloquants correctement propagés 2/2; appels après plafond 0.
- Budget estimé : 90 à 130 appels outils.
- Risques : couplage aux hooks SDK; deadlock avant checkpoint.
- Rollback : conserver `status` diagnostic et désactiver superviseur par configuration explicite.
- Dépendances : décision humaine sur marge réservée au checkpoint et source d'événements.

## BRIEF-PROP-002 — Source d'événements backend et état atomique

- Finding source : ARCH-003, ARCH-004, ARCH-005, QA-002 à QA-004.
- Objectif : mesurer Claude/Cursor avec un format versionné et protéger progression/checkpoints.
- Contexte vérifié : Cursor final JSON; transcripts Claude heuristiques; aucun lock.
- Périmètre : adaptateurs Claude/Cursor, run id, validation schéma, lock par brief, temp+rename, validator checkpoint.
- Hors périmètre : facturation exacte Cursor, exécution agentique réelle en test PR, worktrees généralisés.
- Fichiers probablement concernés : budget, wrapper backend, ledger, tests de fixtures/concurrence.
- Tests à ajouter : `BUD-TRANSCRIPT-001`, `BUD-PROGRESS-001`, `BUD-CONCURRENCY-001`.
- Modifications CI éventuelles : tests rapides PR, multiprocess nightly.
- Critères d'acceptation : source inconnue fail closed; deux writers intacts; reprise sans transcript.
- Métriques : backends couverts 2/2; corruption sur 100 runs 0; preuves vérifiées 5/5 ou explicitement déclaratives.
- Budget estimé : **NEEDS_SPLIT** en deux lots estimés chacun à 100–140 appels : adaptateurs; atomicité/reprise.
- Risques : schémas fournisseurs instables, différences filesystem Windows.
- Rollback : backend non supporté bloqué; lecture backward-compatible des anciens checkpoints.
- Dépendances : fixtures réelles anonymisées et décision de compatibilité.

## BRIEF-PROP-003 — CI QA minimale reproductible

- Finding source : risque CI, QA-005/006, architecture Actions cible.
- Objectif : rendre obligatoires les contrôles rapides sans introduire Unity prématurément.
- Contexte vérifié : aucun workflow suivi; seul Dependency Graph vert.
- Périmètre : scope/docs, harness Linux, contrat Windows, sécurité/pinning, JUnit/artifacts, concurrence.
- Hors périmètre : Unity obligatoire, secrets/licences, nightly mutation complet.
- Fichiers probablement concernés : `.github/workflows/**`, scripts déterministes de validation, tests de workflow.
- Tests à ajouter : `PLATFORM-001`, `CI-SCOPE-001`, canaris négatifs de gate/artifact.
- Modifications CI éventuelles : objet même du brief.
- Critères d'acceptation : PR documentaire limitée; harness exécuté deux OS; artifacts requis; permissions minimales; checks nommés stables.
- Métriques : jobs QA au commit ≥ 3; skips PowerShell Windows 0; artifacts attendus présents 100 %.
- Budget estimé : 110 à 145 appels outils.
- Risques : coût Windows, permissions GitHub, check names avant protection.
- Rollback : workflows informatifs puis promotion humaine en required.
- Dépendances : propriétaire active les checks requis après observation; politique de dépendances/actions.

# 11. Tests à exécuter immédiatement

| Commande | Durée probable | Effet de bord | Justification | Résultat observé |
|---|---:|---|---|---|
| `.venv/bin/pytest harness/tests/test_budget.py -q` | < 5 s | fichiers temporaires pytest | Cible le commit | 24 passed, 0.75 s |
| `.venv/bin/pytest harness/tests/ -q` | < 10 s | fichiers temporaires pytest | Régression harness | 55 passed, 13 skipped, 1.39 s |
| `py -m pytest harness/tests/ -q` | < 10 s si disponible | aucun avant démarrage | Contrat documenté du dépôt | non démarré : `py` absent |
| `budget.py split-check` avec/sans estimation | < 1 s | aucun | Vérifie exits et faux succès | `NO_ESTIMATE=0`, `NEEDS_SPLIT=0` |
| `budget.py status --transcripts <absent>` | < 1 s | aucun | Fail closed absence | `UNMEASURABLE=2` |
| `verdict_audit.py` sur démos fake/honest | < 5 s | horodatage stdout seulement | Gate rouge/verte | non exécuté séparément; couvert par pytest |

Aucun test Unity ou PowerShell long n'est à exécuter immédiatement sur ce runner.

# 12. Tests à ajouter ultérieurement

## P0

Aucun P0 démontré dans ce commit.

## P1

- `BUD-CONTRACT-001` — contrat exits + consommateur.
- `BUD-TRANSCRIPT-001` — schémas invalides/ambigus et backend Cursor.
- `BUD-PROGRESS-001` — preuve, monotonie et mutations.
- `BUD-CONCURRENCY-001` — concurrence et recovery.
- `PLATFORM-001` — Linux/Windows sans skip masqué.
- `CI-SCOPE-001` — une PR d'audit ne modifie qu'un fichier et ne peut altérer son workflow de validation.

## P2

- `COST-REGRESSION-001` — replay sans LLM.
- `HANDOFF-MANUAL-001` — reprise aveugle.
- `UNITY-CONCURRENCY-001` — deux demandes, un seul accès Editor/Library; uniquement après décision d'infrastructure.
- Mutation ciblée de `classify`, exits, parseur transcript et gate.

## P3

- Rapport de tendance de durée par test.
- Compatibilité macOS uniquement si une cible produit l'exige.

# 13. Recommandations à rejeter

- Exécuter toute la suite Unity à chaque changement documentaire : coût élevé, aucun risque couvert.
- Utiliser un LLM pour décider si un exit code est bloquant : un automate déterministe suffit.
- Installer Unity sur `ubuntu-latest` avant décision de licence, secrets, cache, durée et compatibilité : prématuré.
- Rendre un scan tiers obligatoire avant calibrage de faux positifs : risque de blocage sans preuve de ROI.
- Ajouter Kubernetes, base de données ou file distribuée pour trois fichiers d'état : complexité non justifiée; lock fichier + atomic rename d'abord.
- Calculer un coût dollar Cursor inventé à partir d'appels : non mesurable avec les données actuelles.
- Lancer mutation et 100 répétitions concurrence sur chaque petit commit : nightly ciblé offre un meilleur ratio.
- Mettre en cache résultats/tests ou `Logs/` comme preuve : risque de faux vert; seuls dépendances et `Library/` correctement clés sont admissibles.
- Autoriser `continue-on-error` sur gate, scope, artifacts ou sécurité critique : détruit la garantie annoncée.
- Considérer le score `harness_audit` comme gate de fonctionnalité : il retourne toujours 0 et vérifie surtout présence/pattern.

# 14. Décisions humaines requises

1. Autoriser ou refuser chacun des trois briefs; aucune recommandation de ce rapport n'autorise une implémentation.
2. Décider si `NO_ESTIMATE` doit bloquer et définir la frontière exacte 150/151 avant codage.
3. Choisir le mode d'enforcement : hooks Claude/Cursor, superviseur SDK, ou backend non compatible explicitement interdit.
4. Décider quelles preuves de progression sont réellement vérifiables et lesquelles doivent rester déclaratives.
5. Autoriser une politique de concurrence : un run par brief, worktree par run, lock Unity global.
6. Choisir les checks GitHub requis et donner l'accès nécessaire à leur configuration; état actuel non vérifiable.
7. Valider budget de minutes, rétention artifacts, runners Windows et politique de dépendances/actions tierces.
8. Décider l'infrastructure Unity après inventaire licence, OS, runner, secrets, cache, taille et coût; jusque-là, garder Unity informatif/externe.
9. Définir les seuils de promotion d'un job informatif vers obligatoire à partir d'un historique mesuré.
10. Accepter explicitement toute modification future de `harness/`, tests, workflows ou `.claude/` dans des briefs séparés.
