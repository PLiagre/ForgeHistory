---
audit_id: CURSOR-bbe6da5-bare-python-matcher
auditor: cursor-cloud
target_branch: master
target_commit: bbe6da50f180395a36d5ad255e682f727525cca6
created_at: 2026-08-03T19:02:41Z
audit_type: architecture-and-qa
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

État de l'audit : **CURRENT**. Après fetch, `origin/master` et le commit cible pointent tous deux sur `bbe6da50f180395a36d5ad255e682f727525cca6`.

## Cinq risques majeurs

1. **P1 — Faux négatifs confirmés.** Le matcher laisse passer des invocations Bash réelles : `"python" count.py`, `! python count.py` et `sudo -u user python count.py`; le hook retourne 0 et le gate accepte la commande quotée.
2. **P1 — Faux positifs confirmés.** Le matcher bloque de la prose telle que `The rejected option (python tooling) was not used.` et l'argument de `echo \`date\` python`; le gate rejette effectivement le premier cas.
3. **P1 — Une fonction commune traite deux domaines différents.** Le hook reçoit une commande shell structurée; le gate infère des commandes dans des fichiers `.md`, `.txt` et `.log` non structurés. « Une implémentation » ne signifie pas « une sémantique correcte » pour les deux entrées.
4. **P2 — Le fail-closed est partiel.** Un import cassé bloque, mais un JSON malformé ou sans `tool_input.command` autorise tout; une évolution de payload peut donc désarmer silencieusement le hook.
5. **P1 — Aucune CI QA n'exerce ce commit.** Aucun run n'est associé au SHA cible et aucun workflow n'est suivi sous `.github/workflows/`.

## Cinq actions au meilleur ROI

1. Séparer la détection sur champs de commandes structurés de l'analyse des preuves textuelles; ne pas prétendre parser de la prose comme du shell.
2. Écrire d'abord un corpus oracle MUST_BLOCK/MUST_ALLOW couvrant quoting, `!`, options de wrappers, substitutions, heredocs et dialectes.
3. Corriger localement les positions de commande vérifiées par le corpus, ou adopter un vrai parseur après décision licence/dépendance; ne pas étendre la regex au hasard.
4. Faire échouer explicitement les payloads hook absents/inconnus, avec version de schéma et message de diagnostic.
5. Ajouter une CI PR Linux/Windows qui exécute les tests hook/gate, les mutations critiques et les deux démos du gate.

## Trois incertitudes importantes

1. Le périmètre shell voulu n'est pas formalisé : Bash seulement, Git Bash Windows, PowerShell imbriqué, ou tous les dialectes.
2. La protection de `master` reste **UNVERIFIABLE** : l'API GitHub répond 403.
3. Les 15 tests PowerShell skipped sur ce runner ont été annoncés comme passés par l'implémenteur, mais aucun artifact CI indépendant ne permet de le confirmer.

# 2. Provenance et fraîcheur

- Branche cible : `master`.
- Commit cible complet : `bbe6da50f180395a36d5ad255e682f727525cca6`.
- Commit court : `bbe6da5`.
- Sujet retenu : `bare-python-matcher`.
- Fraîcheur : **CURRENT**; `origin/master` = cible au moment de la publication.
- Branche documentaire : `cursor/audit-bbe6da5-bare-python-matcher-3f31`, imposée par la politique Cloud.
- Parent direct : `10ee7ff4577de96ae8e58195c0b08233b333e2b2`.
- Diff cible : `.claude/hooks/no_bare_python.py`, nouveau `harness/bare_python.py`, `harness/tests/test_verdict_audit.py`, `harness/verdict_audit.py`; 226 insertions et 105 suppressions annoncées.
- Commits adjacents inspectés : `10ee7ff` (matcher positionnel du hook) et `fca251b` (armement des hooks et permissions), car le commit cible centralise leur comportement.
- Fichiers inspectés : les quatre fichiers du commit, tests du harness, configuration hooks, wrapper Cursor, gate, règles, historique Git, inventaire des tests Unity et geo.
- Non accessibles : paramètres de branche, secrets, runners/licences Unity, transcripts locaux de l'implémenteur, checks privés éventuels hors Actions.
- Limites : aucune installation, aucun `/forge-run`, aucun agent de génération, aucun test Unity, aucun changement hors de ce rapport.

## Mesures exécutées

- Tests ciblés : `.venv/bin/pytest harness/tests/test_no_bare_python_hook.py harness/tests/test_verdict_audit.py -q` → **59 passed in 1.67s**.
- Suite harness : `.venv/bin/pytest harness/tests/ -q` → **109 passed, 15 skipped in 2.50s**.
- Faux négatifs de `find_invocation` : commande quotée, `!` et `sudo -u` → `False`.
- Faux positifs de `find_invocation` : backtick fermant, parenthèse de prose et prose en début de ligne → `True`.
- Hook black-box : quoted/bang/sudo-options → exit 0; backtick-data → exit 2.
- Gate black-box : prose parenthésée → exit 1 avec `no_bare_python_alias` en FAIL; commande quotée réelle → exit 0 avec check en PASS.

Ces sondes sont des mesures sur le commit cible. Elles ne modifient pas le dépôt et ne prouvent pas l'exhaustivité du corpus shell.

## CI au commit cible

- `gh run list --commit bbe6da5...` : liste vide.
- Dernier run visible sur `master` : Dependency Graph au SHA antérieur `6231186`, sans tests QA.
- Workflow versionné : aucun.
- Artifacts, retries, timeouts, tests flaky : aucune donnée au SHA cible.
- Classification : **CI_UNAVAILABLE** pour le commit cible et **CI_GREEN_INCOMPLETE** pour l'état historique du dépôt.

## Sources externes retenues

Consultées le 2026-08-03. Les pratiques open source ne sont pas des recommandations officielles.

| Source | Classe | Activité/licence | Pratique pertinente | Limites et applicabilité |
|---|---|---|---|---|
| [GNU Bash Reference Manual](https://tiswww.case.edu/php/chet/bash/bashref.html) | OFFICIAL | Consulté 2026-08-03; GNU documentation | Bash tokenise puis parse mots/opérateurs avec règles de quoting; `!` est un mot réservé | Confirme qu'une regex de positions n'est pas un parseur complet. |
| [Microsoft PowerShell about_Parsing](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_parsing?view=powershell-7.6) | OFFICIAL | Consulté 2026-08-03; Microsoft Learn | Modes expression/argument et nouvelles syntaxes de commande | Le hook est nommé Bash; PowerShell doit être un contrat séparé si supporté. |
| [Anthropic Claude Code hooks](https://docs.anthropic.com/en/docs/claude-code/hooks) | OFFICIAL | Consulté 2026-08-03; licence N/A | `PreToolUse` peut bloquer avant l'effet; exit 2 a une sémantique de blocage | Ne garantit pas un payload futur non validé par les tests du dépôt. |
| [GitHub Actions workflow syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax) | OFFICIAL | Consulté 2026-08-03; licence N/A | Déclenchement PR, jobs nommés, permissions et timeouts | Les checks requis restent une configuration de protection séparée. |
| [koalaman/shellcheck](https://github.com/koalaman/shellcheck) | OPEN_SOURCE_PRACTICE | Push 2026-06-19; GPL-3.0 | Analyse syntaxique shell avec corpus important | GPL et binaire tiers; ne l'ajouter qu'après décision de dépendance. |
| [mvdan/sh](https://github.com/mvdan/sh) | OPEN_SOURCE_PRACTICE | Push 2026-07-28; BSD-3-Clause | Parseur/formatter/interpréteur Bash et zsh | Écosystème Go; intégration disproportionnée possible pour une seule règle. |
| [tree-sitter/tree-sitter-bash](https://github.com/tree-sitter/tree-sitter-bash) | OPEN_SOURCE_PRACTICE | Push 2025-12-02; MIT | Grammaire Bash structurée | Dépendance native et arbre syntaxique à interpréter; pas une solution prête à l'emploi. |
| [idank/bashlex](https://github.com/idank/bashlex) | OPEN_SOURCE_PRACTICE | Push 2024-04-08; GPL-3.0 | Parseur Bash Python | Activité plus ancienne et GPL; ne couvre pas PowerShell. |
| [oils-for-unix/oils](https://github.com/oils-for-unix/oils) | OPEN_SOURCE_PRACTICE | Push 2026-05-31; licence « Other » déclarée par GitHub | Montre la complexité réelle des syntaxes shell | Projet beaucoup trop large pour ce contrôle; référence, pas dépendance recommandée. |

# 3. Architecture actuelle

## Composants et responsabilités

| Composant | Entrée | Responsabilité réelle | Effet |
|---|---|---|---|
| Hook Claude | JSON `PreToolUse`, champ commande | Bloquer avant Bash | exit 2 ou 0 |
| `bare_python.py` | Chaîne arbitraire | Heuristique positionnelle Bash/PowerShell partielle | premier match ou `None` |
| Gate | commandes manifest + textes libres | Rejeter une preuve déclarant une invocation interdite | check PASS/FAIL |
| Tests hook | payloads synthétiques subprocess | Contrat exit sur corpus | 30 cas paramétrés environ |
| Tests gate | brief synthétique | Contrat gate sur quatre nouveaux scénarios | ACCEPT/REJECT |
| Test de partage | texte source des consommateurs | Empêcher le retour d'une ancienne regex exacte | assertion de sous-chaînes |

## Flux, états et contrôles

Flux live : commande proposée → payload hook → import du matcher → heuristique → blocage/autorisation → effet shell.

Flux gate : manifest et fichiers récursifs → masking partiel des code spans → même heuristique → verdict global.

États explicites : import cassé = blocage; match = blocage/FAIL; non-match = autorisation/PASS; payload malformé = autorisation. Il n'existe pas d'état `UNMEASURABLE` ou `UNKNOWN_DIALECT`.

Mémoire : aucune pour le matcher; le gate lit tout le brief. Dépendances cachées : dialecte réel du shell, payload Anthropic, convention Markdown, format des logs et ordre du `sys.path`.

Effets de bord : le hook précède l'effet, ce qui est correct. Le gate est après l'effet et ne peut que rejeter la preuve. La restauration des hooks Cursor et les locks restent hors de ce commit.

Reprise/rollback : un revert du commit rétablit deux matchers divergents; il n'existe pas de feature flag. Aucun worktree, lock Unity ou orchestration concurrente n'est modifié par ce commit.

# 4. Cartographie des tests existants

| Zone | Type de test | Fichiers | Garantie réelle | Lacune | Confiance |
|---|---|---|---|---|---|
| Matcher hook | UNIT via subprocess paramétré | `test_no_bare_python_hook.py` | 15 MUST_BLOCK, 15 MUST_ALLOW, heredoc simple, liste, payload invalide | Quotes de commande, `!`, wrappers avec options, backtick fermant, heredocs multiples | HIGH sur corpus, LOW hors corpus |
| Matcher gate | CONTRACT/INTEGRATION | `test_verdict_audit.py` | Prose médiane autorisée, commande indentée refusée, counter réel refusé, grep autorisé | Prose en position syntaxique, code d'exemple, commande quotée, dialecte | MEDIUM |
| Source unique | Test structurel | `test_gate_and_hook_share_one_matcher` | Cherche `bare_python` et l'ancienne regex exacte | Peut passer si le matcher n'est plus appelé ou si une autre regex est ajoutée | LOW |
| Import cassé | Test manuel annoncé | aucun test automatisé ciblé | Aucun dans la suite | ImportError, exception à l'import, module shadowing | LOW |
| Payload hook | Négatif permissif | `test_no_bare_python_hook.py` | JSON malformé et clé absente autorisent | Contradiction avec fail-closed et absence déclarable | HIGH sur comportement, LOW sécurité |
| Armement hooks | Structure/config | `test_hooks_armed.py` | settings parse, scripts présents et référencés | Sémantique de matcher/config non jouée en session neuve | MEDIUM |
| Gate global | CONTRACT | `test_verdict_audit.py`, démos | Checks positifs/négatifs, fichiers suivis, exits | Pas de CI; quelques checks dépendent du contexte Git | HIGH local |
| Budget/transcripts/ledger | UNIT + CLI | autres tests harness | Comptage et statuts déterministes sur fixtures | Hors changement direct; CI absente | MEDIUM |
| PowerShell wrapper | INTEGRATION simulée | `test_run_unity.py` | Processus, timeout, exits si PowerShell présent | 15 skips observés ici | LOW sur ce runner |
| Unity | NUnit Edit/PlayMode | `unity/game_unity/Assets/Tests/**` | Nombreux comportements et déterminisme présents | Aucun résultat au SHA, licence/runner inconnus | LOW |
| Geo | Preuves rouges et scripts | `pipeline/geo/tests/**` | Cas négatifs spécialisés | Non collectés par suite harness standard | MEDIUM local |
| Sécurité | Hooks/permissions | `.claude/**`, tests hooks | Quelques limites locales | Pas de secret scan, CodeQL, workflow policy | LOW CI |
| Mutation | Cas négatifs écrits à la main | tests gate/hook | Quelques mutations sémantiques | Aucun score ni mutants du matcher | LOW global |
| Concurrence/recovery/rollback | Aucun ciblé | — | Aucune | Non pertinent au pur matcher; import/config restent sensibles | NONE |
| Multiplateforme | Python Linux + skips Windows | harness | Python pur exercé sur Linux | Bash Windows/Git Bash et PowerShell non reproduits | LOW |

# 5. Matrice de risques QA

| Risque | Probabilité | Impact | Détection actuelle | Test recommandé | Priorité |
|---|---|---|---|---|---|
| Commande quotée autorisée | MEDIUM | HIGH | Aucune | MATCH-CMD-001 | P1 |
| `! python` autorisé | MEDIUM | HIGH | Aucune | MATCH-CMD-001 | P1 |
| Wrapper avec options autorisé | MEDIUM | MEDIUM | Seulement wrapper sans option | MATCH-WRAPPER-001 | P1 |
| Prose parenthésée rejetée | HIGH dans docs | MEDIUM | Aucune | GATE-PROSE-001 | P1 |
| Backtick fermant pris pour ouverture | MEDIUM | MEDIUM | Aucune | MATCH-SUBST-001 | P1 |
| Heredoc complexe mal découpé | LOW/MEDIUM | MEDIUM | Un heredoc simple | MATCH-HEREDOC-001 | P2 |
| Payload inconnu autorisé | LOW | HIGH | Comportement explicitement testé comme allow | HOOK-SCHEMA-001 | P2 |
| Faux test « source unique » | MEDIUM | MEDIUM | Sous-chaînes source | MATCH-WIRING-001 | P2 |
| Régression de temps sur gros logs | LOW | MEDIUM | Aucune | MATCH-PERF-001 | P3 |
| Aucun check PR | HIGH | HIGH | Aucun | CI-HARNESS-001 | P1 |

# 6. Constats d’architecture

## FINDING-ARCH-001 — La mutualisation confond commande structurée et prose

- Priorité : P1
- Confiance : HIGH
- Source : PERSONAL_INFERENCE
- Fichiers concernés : `harness/bare_python.py`, `harness/verdict_audit.py`, `.claude/hooks/no_bare_python.py`
- Risque : faux succès live et faux rejet gate
- Complexité : moyenne
- Rollback : conserver le module partagé pour les commandes manifest seulement; retirer le scan heuristique de prose

### Observation

Le hook et les champs `command` contiennent du shell. Les fichiers Markdown/log contiennent du langage humain, des exemples et parfois des sorties. Une position syntaxique de regex n'a pas la même signification dans ces domaines.

### Preuve

Le gate rejette la phrase mesurée `The rejected option (python tooling) was not used.` parce que `(` est traité comme opérateur shell.

### Conséquence

Une source unique propage désormais chaque erreur aux deux garanties; la duplication est réduite, le blast radius augmente.

### Recommandation minimale

Appliquer le matcher partagé seulement aux champs structurés. Pour les fichiers, exiger des blocs/entrées de commandes explicitement structurés plutôt que scanner toute prose.

### Alternatives

Analyser uniquement un journal JSON de commandes; conserver le texte comme preuve humaine sans prétention mécanique.

### Critères d’acceptation

Toute commande réellement exécutée provient d'un champ structuré; 100 phrases adversariales n'affectent pas le gate; les commandes interdites structurées échouent.

### Métriques avant/après

Avant mesuré : 1/3 phrases adversariales testées est déjà un faux positif gate démontré, avec deux autres au niveau matcher. Après attendu : 0 sur corpus pré-écrit.

## FINDING-ARCH-002 — Une regex ne couvre pas la grammaire shell annoncée

- Priorité : P1
- Confiance : HIGH
- Source : OFFICIAL
- Fichiers concernés : `harness/bare_python.py`
- Risque : contournement involontaire du hard-won rule 1
- Complexité : moyenne
- Rollback : réduire explicitement le contrat aux formes supportées

### Observation

Le manuel Bash décrit tokenisation, quoting et mots réservés. Le matcher simule quelques positions avec une regex; `!`, quotes du nom de commande et options intermédiaires ne sont pas représentés.

### Preuve

Mesuré : `"python" count.py`, `! python count.py`, `sudo -u user python count.py`, `xargs -n1 python` ne matchent pas.

### Conséquence

Le message « blocks bare python invocations » est plus large que la garantie réelle. Étendre la liste des préfixes sans oracle risque de nouveaux faux positifs.

### Recommandation minimale

Définir le dialecte et le corpus avant le mécanisme. Corriger les cas P1; évaluer un parseur seulement si la couverture requise dépasse une heuristique maintenable.

### Alternatives

Remplacer la règle par l'interdiction exacte du token initial dans les commandes simples; utiliser ShellCheck/AST hors hook en complément.

### Critères d’acceptation

Corpus oracle versionné, exécuté contre hook et gate; limites documentées avec statut `UNKNOWN`, pas « allow » silencieux.

### Métriques avant/après

Avant : 4 faux négatifs et 3 faux positifs dans les 7 sondes nouvelles. Après : 0 sur corpus, taux hors corpus non revendiqué.

## FINDING-ARCH-003 — Le fail-closed ne couvre pas le contrat d'entrée

- Priorité : P2
- Confiance : HIGH
- Source : OFFICIAL
- Fichiers concernés : `.claude/hooks/no_bare_python.py`, `harness/tests/test_no_bare_python_hook.py`
- Risque : hook silencieusement désarmé après évolution de payload
- Complexité : faible
- Rollback : allow uniquement derrière version explicitement reconnue

### Observation

L'import cassé exit 2. En revanche, JSON invalide et clé commande absente exit 0, et les tests imposent ce comportement.

### Preuve

`test_malformed_payload_allows_rather_than_crashes` et `test_missing_command_key_allows` rendent l'autorisation obligatoire.

### Conséquence

Le terme « fail closed » est vrai pour une dépendance et faux pour l'entrée. L'absence est confondue avec « aucune invocation ».

### Recommandation minimale

Valider le schéma minimal; payload absent/inconnu doit bloquer avec diagnostic, ou produire un état explicite que l'appelant ne traite jamais comme succès.

### Alternatives

Versionner le payload attendu dans un test contractuel capturé depuis Claude Code; basculer fail-open uniquement via option d'urgence auditée.

### Critères d’acceptation

Import cassé, JSON invalide, type de commande invalide et clé absente retournent tous 2; payload sans match retourne 0.

### Métriques avant/après

Avant : 2/4 erreurs d'entrée ciblées autorisent. Après : 0/4.

## FINDING-ARCH-004 — Le contrôle « one matcher » vérifie du texte source

- Priorité : P2
- Confiance : HIGH
- Source : PERSONAL_INFERENCE
- Fichiers concernés : `harness/tests/test_verdict_audit.py`
- Risque : test vert alors que les consommateurs divergent
- Complexité : faible
- Rollback : conserver ce test en complément, pas comme preuve principale

### Observation

Le test cherche la sous-chaîne `bare_python` et une ancienne regex exacte. Il ne prouve ni l'import de la même fonction, ni son appel, ni la parité des résultats.

### Preuve

Un commentaire contenant `bare_python` suffit à la première assertion; toute nouvelle regex différente de l'ancien littéral contourne la seconde.

### Conséquence

Le check peut rester vert face à la régression qu'il annonce empêcher.

### Recommandation minimale

Tester la parité comportementale sur un corpus partagé et monkeypatcher `find_invocation` pour prouver que chaque consommateur l'appelle.

### Alternatives

Importer les consommateurs et comparer l'identité de fonction, tout en gardant un test black-box.

### Critères d’acceptation

Mutants « appel supprimé », « regex locale ajoutée » et « résultat inversé » font rougir.

### Métriques avant/après

Avant : 0 mutant de wiring démontré rouge. Après : 3/3.

# 7. Constats QA

## FINDING-QA-001 — Corpus de commandes Bash incomplet

- Priorité : P1
- Type de test : UNIT, CONTRACT, ADVERSARIAL
- Environnement : PR_REQUIRED
- Confiance : HIGH
- Risque couvert : invocation interdite autorisée
- Coût estimé : faible
- Durée cible : < 3 s
- Déterministe : YES

### Lacune observée

Le corpus couvre les formes directes mais pas quoting, negation ni wrappers avec options.

### Preuve

Trois exits hook 0 confirmés sur des commandes réelles.

### Scénario de test proposé

- Identifiant : `MATCH-CMD-001`
- Préconditions : Bash disponible; fonction `python` factice qui marque l'exécution sans lancer d'interpréteur.
- Entrée : `"python"`, `\python`, `! python`, `command python`, `sudo -u user python`, `env -i python`, `xargs -n1 python`.
- Action : obtenir l'oracle d'exécution Bash, puis appeler matcher et hook.
- Résultat attendu : toute forme qui exécute le factice est bloquée; les syntaxes invalides sont classées séparément.
- Priorité : P1; déterministe; coût faible; Linux PR et Git Bash Windows.
- Non-doublon : ces formes ne figurent pas dans MUST_BLOCK.

### Résultat attendu

Parité oracle/matcher sur chaque cas.

### Faux positifs possibles

`sudo` indisponible; l'oracle doit parser sans privilège réel ou utiliser un wrapper factice.

### Faux négatifs possibles

Différence Bash/Git Bash.

### Critères d’acceptation

Cas mesurés actuels passent de 3 faux négatifs à 0; matrice publie le dialecte.

## FINDING-QA-002 — Prose et substitutions provoquent des faux rejets

- Priorité : P1
- Type de test : INTEGRATION, ADVERSARIAL
- Environnement : PR_REQUIRED
- Confiance : HIGH
- Risque couvert : gate REJECT sur preuve honnête
- Coût estimé : faible
- Durée cible : < 3 s
- Déterministe : YES

### Lacune observée

La seule prose autorisée place le mot au milieu d'une phrase sans ponctuation shell.

### Preuve

Parenthèse, début de ligne et backtick fermant matchent; parenthèse rejette le gate.

### Scénario de test proposé

- Identifiant : `GATE-PROSE-001`
- Préconditions : brief honnête synthétique.
- Entrée : `(python tooling)`, ligne commençant par `python was rejected`, ``echo `date` python`` cité comme texte, bloc d'exemple interdit, chemin Python.
- Action : placer chaque forme dans `.md`, `.txt`, `.log` puis exécuter le gate.
- Résultat attendu : texte non exécuté n'échoue pas; seule une commande structurée interdit.
- Priorité : P1; déterministe; coût faible; chaque PR gate/matcher.
- Non-doublon : l'existant ne couvre qu'une phrase médiane et un chemin.

### Résultat attendu

Zéro faux rejet sur corpus prose.

### Faux positifs possibles

Une ligne de log réellement copiée depuis une commande sans structure.

### Faux négatifs possibles

Une narration mensongère n'est pas preuve d'exécution; le gate doit s'appuyer sur le journal structuré.

### Critères d’acceptation

Le test rouge mesuré devient vert sans faire passer `counters[].command = python ...`.

## FINDING-QA-003 — Heredocs insuffisamment modélisés

- Priorité : P2
- Type de test : UNIT, ADVERSARIAL
- Environnement : LOCAL_FAST
- Confiance : MEDIUM
- Risque couvert : body pris pour commande ou commande aval supprimée
- Coût estimé : faible
- Durée cible : < 1 s
- Déterministe : YES

### Lacune observée

Un seul heredoc simple à délimiteur `\w+` est couvert.

### Preuve

Le parseur utilise un seul `_HEREDOC_START.search` par ligne et un delimiter `\w+`.

### Scénario de test proposé

- Identifiant : `MATCH-HEREDOC-001`
- Préconditions : aucune.
- Entrée : deux heredocs sur une ligne, `<<'END-MARK'`, `<<-TAB`, body contenant délimiteur partiel, commande après second terminator.
- Action : comparer texte dépouillé et oracle shell.
- Résultat attendu : aucun body n'est analysé; toute commande aval reste analysée.
- Priorité : P2; déterministe; coût faible; LOCAL_FAST + PR_REQUIRED.
- Non-doublon : uniquement `EOF` simple aujourd'hui.

### Résultat attendu

Tous les terminators et commandes aval conservés correctement.

### Faux positifs possibles

Dialecte non Bash.

### Faux négatifs possibles

Expansion dans delimiter dynamique non supportée.

### Critères d’acceptation

Corpus explicite; limites déclarées.

## FINDING-QA-004 — Schéma hook et import cassé non contractuels

- Priorité : P2
- Type de test : SECURITY, RECOVERY, CONTRACT
- Environnement : PR_REQUIRED
- Confiance : HIGH
- Risque couvert : désarmement silencieux
- Coût estimé : faible
- Durée cible : < 3 s
- Déterministe : YES

### Lacune observée

L'import cassé a été vérifié manuellement; payload cassé est explicitement autorisé.

### Preuve

Aucun test ne rend le module indisponible dans un environnement isolé.

### Scénario de test proposé

- Identifiant : `HOOK-SCHEMA-001`
- Préconditions : copie temporaire du hook et root isolé.
- Entrée : module absent, module levant à l'import, JSON invalide, `command` absent/list/entier.
- Action : lancer le vrai subprocess.
- Résultat attendu : erreurs structurales exit 2; liste valide normalisée; message nomme la récupération.
- Priorité : P2; déterministe; coût faible; PR_REQUIRED.
- Non-doublon : les tests actuels imposent le résultat opposé pour deux entrées.

### Résultat attendu

Fail-closed cohérent et observable.

### Faux positifs possibles

Changement officiel de payload non accompagné de fixture.

### Faux négatifs possibles

Import shadowé par environnement global non reproduit.

### Critères d’acceptation

Fixture payload officielle versionnée; 4 erreurs sur 4 bloquées.

## FINDING-QA-005 — Wiring partagé non soumis à mutation

- Priorité : P2
- Type de test : MUTATION, CONTRACT
- Environnement : NIGHTLY
- Confiance : HIGH
- Risque couvert : consommateurs divergents avec suite verte
- Coût estimé : faible à moyen
- Durée cible : < 5 min
- Déterministe : YES

### Lacune observée

Le test source ne vérifie pas l'appel effectif.

### Preuve

Assertions par sous-chaîne et ancienne regex exacte.

### Scénario de test proposé

- Identifiant : `MATCH-WIRING-001`
- Préconditions : seam injectable ou monkeypatch subprocess.
- Entrée : matcher sentinelle qui renvoie match/non-match.
- Action : exécuter hook et gate; mutants suppriment l'appel, ajoutent matcher local, inversent résultat.
- Résultat attendu : consommateurs suivent la sentinelle; trois mutants meurent.
- Priorité : P2; déterministe; coût faible; nightly mutation, test de parité en PR.
- Non-doublon : le test actuel ne mute aucun comportement.

### Résultat attendu

3/3 mutants critiques tués.

### Faux positifs possibles

Monkeypatch ne traverse pas le subprocess.

### Faux négatifs possibles

Import cache masque un second matcher.

### Critères d’acceptation

Test black-box avec module temporaire, pas inspection de texte seule.

## FINDING-QA-006 — Compatibilité Windows non prouvée

- Priorité : P1
- Type de test : INTEGRATION, COMPATIBILITY
- Environnement : PR_REQUIRED
- Confiance : HIGH
- Risque couvert : règle Windows non testée sur environnement Windows
- Coût estimé : moyen
- Durée cible : < 8 min
- Déterministe : YES

### Lacune observée

La règle existe à cause du Store alias Windows, mais ce runner Linux skip 15 tests PowerShell et aucune CI Windows n'existe.

### Preuve

109 passed, 15 skipped localement; aucun run au SHA cible.

### Scénario de test proposé

- Identifiant : `MATCH-WINDOWS-001`
- Préconditions : `windows-latest`, `py`, PowerShell et Git Bash.
- Entrée : corpus hook sous Git Bash; commandes PowerShell via `pwsh -Command`; faux alias Store.
- Action : exécuter suites hook/gate et wrapper.
- Résultat attendu : aucun skip; comportements dialectaux séparés; versions publiées.
- Priorité : P1; déterministe; coût moyen; PR_REQUIRED sur chemins concernés.
- Non-doublon : aucune exécution Windows accessible.

### Résultat attendu

JUnit Windows et zéro skip non allow-listé.

### Faux positifs possibles

Image GitHub différente du poste propriétaire.

### Faux négatifs possibles

Alias Store absent du runner.

### Critères d’acceptation

Un shim contrôlé reproduit l'alias; Bash et PowerShell ont des attentes distinctes.

## FINDING-QA-007 — Performance sur preuves volumineuses non bornée

- Priorité : P3
- Type de test : PERFORMANCE, COST_REGRESSION
- Environnement : NIGHTLY
- Confiance : MEDIUM
- Risque couvert : gate lent/coûteux sur gros logs
- Coût estimé : faible
- Durée cible : < 30 s
- Déterministe : PARTIAL

### Lacune observée

Le gate lit récursivement tous les logs/textes et applique masking + regex; aucune enveloppe n'est mesurée.

### Preuve

Boucle sur trois globs et lecture intégrale de chaque fichier.

### Scénario de test proposé

- Identifiant : `MATCH-PERF-001`
- Préconditions : fixtures générées hors dépôt, tailles 1/10/100 MiB.
- Entrée : textes sans match, backticks denses, heredocs denses.
- Action : mesurer temps et mémoire, trois répétitions.
- Résultat attendu : croissance proche de linéaire; timeout explicite; rapport sans seuil inventé.
- Priorité : P3; partiellement déterministe; coût faible; NIGHTLY.
- Non-doublon : aucun benchmark actuel.

### Résultat attendu

Baseline et tendance publiées; seuil décidé après mesure.

### Faux positifs possibles

Bruit runner partagé.

### Faux négatifs possibles

Fixture non représentative des logs Unity.

### Critères d’acceptation

Trois tailles, médiane et variance; pas de gate performance obligatoire avant calibration.

## MANUAL_QA

Aucun test `MANUAL_QA` n'est recommandé pour décider si une commande shell est exécutée : cette propriété doit être déterministe. Une revue humaine reste requise pour choisir le dialecte supporté et accepter les limites, pas pour remplacer l'oracle.

# 8. Architecture GitHub Actions cible

## Workflow `harness-fast`

- Déclencheur : PR et push `master`; chemins `harness/**`, `.claude/**`, règles.
- Jobs : pytest hook/gate, démos fake/honest, contrat payload, JUnit.
- Dépendances : Python explicite, dépendances verrouillées.
- Runner : Ubuntu.
- Timeout : 10 min.
- Cache : packages par lock; jamais résultats.
- Artifacts : JUnit, sorties des deux démos, versions.
- Statut : obligatoire.
- Chemins concernés : matcher, gate, hooks, tests, settings.
- Retry : aucun pour assertion; un rerun infrastructure annoté.
- Concurrence : workflow + ref, anciens runs PR annulés.
- Coût relatif : faible.
- Flakiness : faible.
- Succès : tous tests exécutés, démo honnête ACCEPT et fake REJECT.
- Échec : skip inattendu, exit perdu, artifact absent.

## Workflow `shell-contract-matrix`

- Déclencheur : PR sur matcher/hooks/gate.
- Jobs : corpus Bash Ubuntu, Git Bash Windows, PowerShell séparé si contractuel.
- Dépendances : oracles locaux, aucun interpréteur externe exécuté comme `python`.
- Runner : `ubuntu-latest`, `windows-latest`.
- Timeout : 10 min/job.
- Cache : minimal.
- Artifacts : matrice JSON cas/oracle/matcher, JUnit.
- Statut : obligatoire après pilote vert; Windows informatif au premier stade.
- Chemins : ciblés.
- Retry : aucun test; rerun incident runner seulement.
- Concurrence : par PR.
- Coût relatif : moyen.
- Flakiness : faible si wrappers factices et pas de réseau.
- Succès : zéro désaccord sur corpus.
- Échec : tout faux positif/négatif.

## Workflow `workflow-and-security-policy`

- Déclencheur : toute PR.
- Jobs : contrôle paths des PR documentaires, actionlint, permissions minimales, pinning SHA, secret/dependency review selon disponibilité.
- Runner : Ubuntu.
- Timeout : 10 min.
- Cache : aucun verdict.
- Artifacts : SARIF/JSON.
- Statut : scope/actionlint obligatoire; scanners avancés informatifs jusqu'à calibration.
- Chemins : toujours, renforcement `.github/workflows/**`.
- Retry : réseau seulement, borné.
- Concurrence : par PR.
- Coût relatif : faible à moyen.
- Flakiness : bases externes.
- Succès : diff conforme et aucune nouvelle vulnérabilité au seuil décidé.
- Échec : workflow invalide, permissions larges, action mutable, artifact attendu absent.

## Workflow `nightly-adversarial`

- Déclencheur : planifié et manuel.
- Jobs : mutation wiring/matcher, fuzz corpus seedé, heredocs, performance logs.
- Runner : Linux + Windows ciblé.
- Timeout : 30 min.
- Cache : dépendances uniquement.
- Artifacts : mutants, seed, corpus minimisé, timings.
- Statut : informatif.
- Chemins : tous.
- Retry : seed conservée; aucun retry masquant.
- Concurrence : un nightly actif.
- Coût relatif : moyen.
- Flakiness : faible pour seeds fixes; performance partielle.
- Succès : mutants critiques tués, aucun crash/désaccord.
- Échec : corpus minimal reproductible archivé.

## Workflow `unity-validation`

- Déclencheur : inchangé par ce commit; manuel/nightly/release seulement après décision.
- Jobs : EditMode/PlayMode ciblés et artifacts.
- Dépendances : licence, secrets, runner, OS, Unity `6000.0.43f1`, cache Library et coût vérifiés avant activation.
- Runner : Windows auto-hébergé/file externe de préférence pendant pilote.
- Timeout : 45–90 min à calibrer.
- Cache : Library par version/manifests, jamais résultats.
- Artifacts : NUnit XML, logs, captures.
- Statut : informatif puis release-required après historique.
- Chemins : Unity uniquement.
- Retry : infrastructure classifiée seulement.
- Concurrence : lock global Unity/Library.
- Coût relatif : élevé, non mesuré.
- Flakiness : inconnue.
- Succès : exit 0 + XML parseable + artifacts présents.
- Échec : licence = infrastructure; test rouge = code; XML absent = échec.

# 9. Pipeline recommandé

Claude local
→ corpus ciblé hook/gate
→ suite harness
→ push
→ `harness-fast`
→ matrice shell Linux/Windows
→ artifacts JUnit et corpus
→ audit Cursor indépendant
→ décision humaine sur dialecte/limites
→ brief atomique éventuel
→ nightly mutation/fuzz
→ Unity séparé uniquement si concerné et autorisé.

Le LLM ne décide jamais si une chaîne est une commande. Le gate mécanique précède toujours l'Évaluateur, mais il doit s'appuyer sur une donnée structurée plutôt que sur une interprétation de prose.

# 10. Briefs proposés à Claude

Ces briefs sont proposés, jamais autorisés.

## BRIEF-PROP-001 — Séparer commandes structurées et preuves textuelles

- Identifiant : `BRIEF-bbe6da5-01`.
- Finding source : ARCH-001, QA-002.
- Objectif : supprimer les faux rejets de prose sans affaiblir les commandes manifest.
- Contexte vérifié : parenthèse prose REJECT; commande quotée ACCEPT.
- Périmètre : contrat de journal de commandes structuré, gate limité à ces champs, migration minimale des fixtures.
- Hors périmètre : parseur shell complet, Unity, CI.
- Fichiers probablement concernés : gate, manifest contract, tests gate, documentation pointeur.
- Tests à ajouter : `GATE-PROSE-001`, commandes structurées positives/négatives.
- Modifications CI éventuelles : aucune dans ce lot.
- Critères d'acceptation : corpus prose 0 faux rejet; commandes interdites structurées 100 % rejetées.
- Métriques : avant 1 faux rejet gate et 1 faux succès gate démontrés; après 0/0 sur corpus.
- Budget estimé : 70–110 appels outils.
- Risques : anciens briefs sans journal structuré.
- Rollback : mode lecture backward-compatible informatif, sans prétendre PASS.
- Dépendances : décision humaine sur schéma et migration.

## BRIEF-PROP-002 — Corpus oracle et couverture shell explicite

- Identifiant : `BRIEF-bbe6da5-02`.
- Finding source : ARCH-002/003/004, QA-001/003/004/005.
- Objectif : formaliser le dialecte et rendre les limites/mutations mesurables.
- Contexte vérifié : 4 faux négatifs, 3 faux positifs sur sondes nouvelles.
- Périmètre : corpus oracle, quoting/`!`/wrappers/heredocs, schéma hook fail-closed, wiring comportemental.
- Hors périmètre : ajout immédiat d'une dépendance GPL/native, PowerShell si non retenu.
- Fichiers probablement concernés : matcher, hook, tests hook/gate.
- Tests à ajouter : `MATCH-CMD-001`, `MATCH-HEREDOC-001`, `HOOK-SCHEMA-001`, `MATCH-WIRING-001`.
- Modifications CI éventuelles : aucune dans ce lot.
- Critères d'acceptation : zéro désaccord corpus; 3/3 mutants wiring tués; erreurs payload bloquées.
- Métriques : matrice faux positif/faux négatif par dialecte.
- Budget estimé : 100–145 appels outils.
- Risques : croissance d'une regex fragile; **NEEDS_SPLIT** si Bash et PowerShell sont tous deux exigés.
- Rollback : limiter le contrat aux commandes simples documentées.
- Dépendances : choix humain Bash/Git Bash/PowerShell et politique de licence.

## BRIEF-PROP-003 — CI harness et matrice Windows

- Identifiant : `BRIEF-bbe6da5-03`.
- Finding source : QA-006 et absence CI.
- Objectif : exécuter automatiquement les garanties rapides au SHA de chaque PR.
- Contexte vérifié : aucun workflow; aucun run cible; 15 skips locaux.
- Périmètre : `harness-fast`, matrice shell ciblée, JUnit, artifacts, timeouts, concurrence, scope documentaire.
- Hors périmètre : Unity obligatoire, secrets Unity, nightly mutation complet.
- Fichiers probablement concernés : `.github/workflows/**`, scripts déterministes de validation.
- Tests à ajouter : `MATCH-WINDOWS-001`, `CI-HARNESS-001`, canaris fake/honest.
- Modifications CI éventuelles : objet du brief.
- Critères d'acceptation : checks au SHA, zéro skip Windows inattendu, artifacts présents, PR docs limitée.
- Métriques : jobs QA 0 → au moins 3; artifacts 0 → JUnit + gate logs.
- Budget estimé : 90–130 appels outils.
- Risques : coût Windows, noms de checks avant protection.
- Rollback : jobs informatifs pendant pilote, promotion humaine en required.
- Dépendances : autorisation workflows et accès propriétaire à la protection.

# 11. Tests à exécuter immédiatement

| Commande | Durée probable | Effet de bord | Justification | Résultat observé |
|---|---:|---|---|---|
| `.venv/bin/pytest harness/tests/test_no_bare_python_hook.py harness/tests/test_verdict_audit.py -q` | < 10 s | tmp pytest | Changement ciblé | 59 passed, 1.67 s |
| `.venv/bin/pytest harness/tests/ -q` | < 15 s | tmp pytest/repos Git temporaires | Régression harness | 109 passed, 15 skipped, 2.50 s |
| Sondes `find_invocation` sur 7 cas adversariaux | < 1 s | aucun | Tester hors corpus | 4 faux négatifs, 3 faux positifs |
| Hook subprocess sur quoted/bang/sudo/backtick | < 1 s | aucun | Contrat exit réel | 0/0/0/2 |
| Gate black-box prose/commande quotée | < 2 s | tmp uniquement | Prouver conséquence système | faux REJECT=1, faux ACCEPT=0 |
| Gate démos fake/honest | < 5 s | horodatage stdout | Contrôle gate général | couvert par suite; pas rejoué séparément |

N'exécuter ni Unity ni fuzz long immédiatement.

# 12. Tests à ajouter ultérieurement

## P0

Aucun P0 démontré.

## P1

- `MATCH-CMD-001` — oracle shell et formes réelles manquées.
- `GATE-PROSE-001` — prose, code d'exemple, backticks et parenthèses.
- `MATCH-WRAPPER-001` — options de `sudo`, `env`, `xargs`, `time`, `command`.
- `MATCH-WINDOWS-001` — Git Bash/PowerShell et faux Store alias.
- `CI-HARNESS-001` — checks au SHA et artifacts.

## P2

- `MATCH-HEREDOC-001` — multiple/délimiteurs complexes.
- `HOOK-SCHEMA-001` — fail-closed payload/import.
- `MATCH-WIRING-001` — parité et mutation des consommateurs.
- Fuzz seedé avec minimisation de cas.

## P3

- `MATCH-PERF-001` — temps/mémoire sur gros logs.
- Rapport de tendance sans seuil obligatoire avant calibration.

# 13. Recommandations à rejeter

- Ajouter encore des préfixes à la regex sans corpus oracle : reproduit le défaut actuel.
- Déclarer « parser shell » ce qui reste une heuristique : garantie non mesurable.
- Utiliser un LLM pour distinguer commande et prose : non déterministe et coûteux.
- Ajouter simultanément ShellCheck, tree-sitter, bashlex et shfmt : redondant, licences et maintenance sans ROI.
- Scanner toute prose plus agressivement : augmente les faux rejets.
- Autoriser payload invalide « pour ne pas bloquer » sans état explicite : désarmement silencieux.
- Rendre les tests performance obligatoires avant baseline : flakiness probable.
- Exécuter Unity pour ce changement Python/hook : aucun risque pertinent couvert.
- Lancer la suite Unity sur runner standard sans licence/OS/cache/secrets/coût vérifiés.
- Considérer 109 passed comme preuve des 15 tests skipped.
- Marquer un job `continue-on-error` s'il porte le check hook/gate.

# 14. Décisions humaines requises

1. Autoriser ou refuser chaque brief; ce rapport n'autorise aucune implémentation.
2. Définir le dialecte garanti : Bash, Git Bash, PowerShell ou sous-ensemble explicite.
3. Décider si une commande quotée/échappée doit être bloquée selon le but réel « Store alias ».
4. Choisir la source de vérité des commandes exécutées : manifest structuré, journal JSON ou texte libre.
5. Décider fail-closed pour payload malformé/absent et procédure de récupération.
6. Accepter ou refuser une dépendance de parsing après examen licence, plateforme et maintenance.
7. Autoriser un brief CI séparé et choisir les checks requis.
8. Donner accès à la configuration de protection ou confirmer manuellement les checks de `master`.
9. Décider le budget de runner Windows et la période pilote avant statut obligatoire.
10. Maintenir Unity hors de cette chaîne tant qu'aucun changement Unity et aucune infrastructure validée ne le justifient.
