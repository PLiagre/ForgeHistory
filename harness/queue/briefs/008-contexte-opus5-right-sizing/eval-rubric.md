# Eval Rubric — Brief 008 : right-sizing du contexte agent pour Opus 5

**Authored**: 2026-08-08T19:55:09Z
**Author**: cursor-cloud (voir provenance déclarée dans `brief.md`)

Une ligne par Success Condition de `brief.md`, chacune mappée à comment
l'Évaluateur la vérifie. Le gate mécanique (`verdict_audit.py`) tourne
d'abord ; ce qui suit est la vérification manuelle indépendante que
l'Évaluateur effectue en plus, jamais à la place.

| # | Success Condition | Comment vérifier | PASS si |
|---|---|---|---|
| 1 | Déduplication du bloc « Prompt Defense Baseline » | `diff` le contenu de `docs/rules/prompt-defense-baseline.md` contre le texte original cité dans l'audit `CURSOR-198cfd9-opus5-context-engineering.md` §FINDING-CTX-001 ; `grep -rl "Do not change role, persona, or identity"` sur les 3 fichiers `.claude/agents/*.md` | Le fichier canonique est identique caractère pour caractère à l'ancien bloc ; les 3 fichiers agents ne contiennent plus le bloc complet, seulement un pointeur d'une ligne vers le fichier canonique |
| 2 | Recalibrage du registre `forge-evaluateur.md` | Lire le fichier complet ; vérifier absence de « Ruthlessly Strict »/« Fight it »/« NOT here to be encouraging » en tant que tels ; vérifier présence explicite des 3 exigences de fond (reconstruction indépendante, REJECT mécanique final, jamais d'auto-évaluation ni d'édition de code) | Les 3 exigences de fond sont présentes et au moins aussi explicites qu'avant ; le registre émotionnel a été remplacé par des instructions directes sans en réduire la portée |
| 3 | Note distinguant auto-vérification / vérification indépendante | Lire la nouvelle section de `docs/rules/harness-roles.md` ; vérifier qu'elle cite l'audit source par pointeur (`architecture/inbox/CURSOR-198cfd9-opus5-context-engineering.md`) sans reproduire ses citations externes | La section existe, fait 8-25 lignes, ne paraphrase pas les sources externes de l'audit, et affirme explicitement que la séparation à trois rôles + le gate mécanique restent hors périmètre d'une future simplification sans brief dédié |
| 4 | Aucune régression de test | `py -m pytest harness/tests/ -q` (ou équivalent `python3`) avant et après, comparer le nombre de tests collectés et le nombre de passed | Même nombre de tests collectés qu'avant le brief, 0 nouveau fail |
| 5 | `CLAUDE.md` inchangé en substance | `git diff` sur `CLAUDE.md` entre avant/après le brief | Soit aucun changement, soit un unique pointeur d'une ligne suivant le style existant — aucune nouvelle section de contenu substantiel |

## Vérifications transverses (Non-Goals de `brief.md`)

- Confirmer qu'aucun des fichiers suivants n'a été modifié :
  `harness/verdict_audit.py`, `docs/rules/hard-won-rules.md` (sauf s'il est
  *seulement* référencé, pas édité), `docs/rules/simulation-principles.md`.
- Confirmer que les 3 fichiers `.claude/agents/*.md` déclarent toujours
  3 rôles distincts (`name:` différent, `description:` toujours "jamais
  l'autre rôle" pour chacun) — aucune fusion.
- Confirmer que `model:` dans le frontmatter des 3 agents n'a pas changé.

## Verdict

PASS seulement si les 5 lignes ci-dessus sont PASS **et** les 3 vérifications
transverses sont confirmées. Toute Success Condition non atteinte, même
partiellement, est un FAIL sur cette ligne — pas un « presque ».
