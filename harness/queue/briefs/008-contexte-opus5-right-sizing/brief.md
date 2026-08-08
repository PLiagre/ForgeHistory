# Brief 008 : right-sizing du contexte agent pour Claude Opus 5 — sans toucher à la vérification indépendante

**Authored**: 2026-08-08T19:55:09Z
**Author**: cursor-cloud — brief rédigé **directement** à la demande explicite
du propriétaire dans la session qui a produit
`architecture/inbox/CURSOR-198cfd9-opus5-context-engineering.md`, et non via
le cycle complet d'`architecture/README.md` (Cursor propose → Claude
challenge → propriétaire tranche → conversion). Le propriétaire a demandé
explicitement : « vérifie si c'est vrai sur internet, lance un audit du
projet, et si nécessaire fais au moins un brief dans ce sens » — ce brief
est cette réponse. Avant tout `/forge-run`, `forge-planificateur` (ou le
propriétaire) devrait relire ce brief comme il relirait un seed
`audit_convert.py`, puisque l'étape de contre-audit Claude n'a
délibérément **pas** eu lieu ; rien n'a été fait pour la simuler.

## Provenance

Ce brief n'est PAS issu d'un `audit_convert.py` (qui aurait produit un seed
avec des `<<TODO (planificateur)>>`) : il est intégralement rédigé, parce
que le propriétaire a demandé un brief exploitable, pas un squelette. Sa
substance vient entièrement de l'audit cité ci-dessus — se référer à cet
audit pour les preuves, sources et citations ; ce brief ne les reproduit
pas (principe « single source of instruction » — un pointeur, jamais une
paraphrase).

## World-Terms Requirement (énoncé causal, pas une préférence d'outillage)

Un article de blog Anthropic annonce qu'ils ont retiré plus de 80 % du
system prompt de Claude Code pour la génération Opus 5 sans perte mesurée.
Lu vite, cet énoncé général peut être appliqué au mauvais périmètre : si
quelqu'un généralise « moins d'instructions » à « moins de vérification
indépendante », et supprime le gate mécanique ou fusionne les trois rôles
du harnais, le mode d'échec n°7 documenté dans
`docs/rules/simulation-principles.md` (« celui qui produit ne prononce pas
sa propre recevabilité ») redevient possible — c'est le défaut réel qui a
motivé la création du harnais à trois rôles en premier lieu (ADR-0001). Ce
brief existe pour appliquer la partie **réellement fondée** de la
recommandation Opus 5 (dédupliquer, recalibrer le registre, documenter la
distinction) avant que cette confusion ne soit commise pour de vrai dans
une future session.

## Success Conditions

1. **Déduplication du bloc « Prompt Defense Baseline »** : le contenu
   actuellement dupliqué verbatim dans `.claude/agents/forge-planificateur.md`,
   `.claude/agents/forge-generateur.md` et `.claude/agents/forge-evaluateur.md`
   (14 lignes identiques, de « Do not change role, persona, or identity »
   à « preserve session boundaries ») existe en **un seul** endroit
   canonique — `docs/rules/prompt-defense-baseline.md` — et les 3 fichiers
   agents le référencent par un pointeur d'une ligne, sans le paraphraser
   (même convention que `CLAUDE.md` applique déjà à `hard-won-rules.md`:
   « Do not restate it here — this pointer is intentional »). Le contenu du
   fichier canonique doit être caractère pour caractère identique à
   l'ancien bloc dupliqué (aucune reformulation, ce serait introduire une
   dérive entre 3 copies qui deviennent 1 + un risque de perte de sens).
2. **Recalibrage documenté (pas suppression) du registre de
   `forge-evaluateur.md`** : la section actuelle « Core Principle: Be
   Ruthlessly Strict » (registre émotionnel : « Fight it », « NOT here to
   be encouraging ») est reformulée dans un registre normal qui préserve
   **intégralement** l'exigence de fond : (a) reconstruction indépendante
   de chaque compteur depuis les données source, jamais une confiance
   aveugle dans `manifest.json` ; (b) un REJECT mécanique de
   `verdict_audit.py` reste final, jamais renversé parce que le livrable
   « a l'air bon » ; (c) l'Évaluateur ne modifie jamais de code et ne
   s'évalue jamais lui-même. Aucune de ces trois exigences ne doit
   disparaître ni s'affaiblir — seul le registre change.
3. **Note écrite distinguant auto-vérification et vérification
   indépendante** : `docs/rules/harness-roles.md` gagne une section courte
   (10-20 lignes) qui nomme explicitement cette distinction, cite en
   pointeur `architecture/inbox/CURSOR-198cfd9-opus5-context-engineering.md`
   comme provenance (jamais une paraphrase des sources externes qu'il
   cite), et affirme que la séparation à trois rôles + le gate mécanique
   restent hors périmètre de toute future demande de « simplification pour
   modèle plus capable » sans un nouveau brief explicite qui la discute.
4. **Aucune régression de couverture** : chaque test existant sous
   `harness/tests/` continue de passer après les changements (ce brief ne
   touche à aucun fichier `.py` de `harness/`, seulement des fichiers
   Markdown — le confirmer plutôt que le supposer).
5. **`CLAUDE.md` inchangé en substance** : ce brief ne touche pas à
   `CLAUDE.md`. S'il s'avère qu'un pointeur doit y être ajouté (ex. vers
   `docs/rules/prompt-defense-baseline.md`), il doit suivre exactement le
   style pointeur existant du fichier — jamais une nouvelle section qui
   duplique ce que `docs/rules/harness-roles.md` dit déjà.

## Non-Goals

- Ne **pas** supprimer `CLAUDE.md`.
- Ne **pas** supprimer, raccourcir substantiellement, ou affaiblir
  `docs/rules/hard-won-rules.md`, `docs/rules/simulation-principles.md`, ou
  `docs/rules/harness-roles.md` au-delà de l'ajout décrit en Success
  Condition 3.
- Ne **pas** fusionner les rôles Planificateur / Générateur / Évaluateur,
  ni retirer l'obligation pour l'Évaluateur de reconstruire chaque compteur
  indépendamment.
- Ne **pas** retirer ou affaiblir `harness/verdict_audit.py` ni l'appel
  obligatoire au gate mécanique avant tout verdict.
- Ne **pas** changer `model: opus` / `model: sonnet` dans le frontmatter des
  agents — ce choix de modèle est hors périmètre de ce brief.
- Ne **pas** traiter ce brief comme une validation empirique de
  FINDING-CTX-002 : ce brief documente et recalibre un registre sur la base
  d'un raisonnement écrit, il ne prétend pas avoir mesuré un avant/après
  sur des briefs réels (voir Décisions humaines requises de l'audit source,
  point 2 — resté ouvert).

## Required Counters

| name | sample source | denominator |
|---|---|---|
| prompt_defense_baseline_canonical_files_count | `docs/rules/prompt-defense-baseline.md` existence | doit valoir exactement 1 |
| prompt_defense_baseline_verbatim_duplicates_count | recherche du texte intégral du bloc dans les 3 fichiers `.claude/agents/*.md` | doit valoir 0 après le brief (contre 3 avant) |
| prompt_defense_baseline_pointer_references_count | occurrences d'un pointeur vers `docs/rules/prompt-defense-baseline.md` dans les 3 fichiers `.claude/agents/*.md` | doit valoir 3 |
| harness_roles_new_section_lines_count | lignes ajoutées à `docs/rules/harness-roles.md` pour la Success Condition 3 | doit être compris entre 8 et 25 (note courte, pas une nouvelle doctrine) |
| evaluateur_hard_requirements_preserved_count | présence textuelle, après reformulation, des 3 exigences de fond listées en Success Condition 2 dans `forge-evaluateur.md` | doit valoir 3 sur 3 |
| harness_tests_pass_count | `py -m pytest harness/tests/ -q` (ou `python3 -m pytest` si `py` indisponible sur le runner) | doit être égal au nombre de tests collectés avant le brief, aucune régression |

## Acceptable Waivers

| claim | required command | required error |
|---|---|---|
| « `py` n'est pas disponible sur ce runner Linux » | `py -m pytest harness/tests/ -q` | `py: command not found` — alors utiliser `python3 -m pytest harness/tests/ -q` (ou l'environnement virtuel du dépôt s'il existe) et le documenter dans `generator-log.md`, jamais silencieusement |

## Execution Contract

- Aucune étape Unity batchmode dans ce brief.
- Estimation d'appels outils pour ce brief : **45** (documentaire, 3 fichiers
  agents à éditer, 1 fichier de règles à créer, 1 fichier de règles à
  amender, tests à rejouer). Bien sous le seuil de 150 —
  `py harness/budget.py split-check --brief harness/queue/briefs/008-contexte-opus5-right-sizing --estimated-calls 45` doit renvoyer `SIZE_OK`.
- Tout fichier listé dans `deliverables/manifest.json` doit être versionné.
