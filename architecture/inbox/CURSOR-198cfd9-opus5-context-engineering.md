---
audit_id: CURSOR-198cfd9-opus5-context-engineering
auditor: cursor-cloud
target_branch: master
target_commit: 198cfd976c1e0ec51fe4563545a5aa03744d815d
created_at: 2026-08-08T19:55:09Z
audit_type: context-engineering-and-qa
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

**Question posée par le propriétaire** : faut-il supprimer `CLAUDE.md`, les
règles (`docs/rules/**`) et le harnais (rôles + gate mécanique) pour laisser
Claude Opus 5 « accomplir l'objectif comme il l'entend », avec moins de
relecture ?

**Verdict : partiellement vrai, mais la conclusion demandée ne suit pas des
faits.** Anthropic a réellement supprimé plus de 80 % du system prompt de
Claude Code pour la génération Opus 5 / Fable 5, sans perte mesurable sur ses
évaluations de code — c'est documenté par Anthropic elle-même, pas une
rumeur. Mais ce qu'Anthropic supprime et ce que le propriétaire propose de
supprimer ne sont **pas le même périmètre** :

| Anthropic supprime | Ce dépôt possède-t-il cet élément ? |
|---|---|
| Instructions d'auto-vérification internes à un même agent (« double-check your work », « include a final verification step ») | **Non** — le gate mécanique et l'Évaluateur sont un **rôle et un processus séparés**, jamais le Générateur qui se relit lui-même |
| Échafaudage de raisonnement (« think step by step », « plan before acting ») redondant avec le raisonnement actif par défaut | **Non trouvé** dans les 3 agents `.claude/agents/*.md` |
| Répétition d'instructions entre system prompt et description d'outils | **Oui, un cas concret** — le bloc « Prompt Defense Baseline » (14 lignes) est dupliqué **verbatim** dans les 3 fichiers agents |
| Règles prescriptives remplacées par du jugement (« match surrounding code » plutôt que « never write comments ») | Partiel — `forge-evaluateur.md` porte un registre impératif fort (« Ruthlessly Strict », « Fight it », « NOT here to be encouraging ») qui correspond au style qu'Anthropic dit vouloir adoucir pour Opus 5 |
| `CLAUDE.md` géant, réservoir de toute pratique connue | **Non** — `CLAUDE.md` fait 105 lignes, pointe vers `docs/rules/**` sans paraphraser (la « progressive disclosure » qu'Anthropic recommande est déjà en place) |

Anthropic ne recommande **jamais** de supprimer `CLAUDE.md` : sa propre
doc dit explicitement de le garder « lightweight » et centré sur les
« gotchas » du dépôt — c'est-à-dire exactement le contenu de
`docs/rules/hard-won-rules.md` (chaque règle y est un défaut réel payé une
fois). Et rien dans la documentation Opus 5 ne traite la séparation
Planificateur / Générateur / Évaluateur : cette séparation n'est pas un
« garde-fou de prompt » qu'un modèle plus intelligent rend inutile, c'est une
**contrainte structurelle** (rôles, contextes, et — dans ce dépôt — parfois
process/agents différents) répondant au mode d'échec n°7 documenté dans
`docs/rules/simulation-principles.md` : *« celui qui produit ne prononce pas
sa propre recevabilité »*. Ce mode d'échec est indépendant de la compétence
du modèle — un très bon développeur humain ne serait pas non plus son propre
relecteur final dans un contexte à enjeu.

**Recommandation** : ne supprimer ni `CLAUDE.md`, ni `docs/rules/**`, ni la
séparation à trois rôles, ni le gate mécanique. Un right-sizing ciblé et
mesuré est en revanche justifié par les faits ci-dessous : dédupliquer le
bloc répété, réviser le registre du prompt de l'Évaluateur, et documenter
explicitement la distinction (auto-vérification vs vérification
indépendante) pour que la question ne soit pas re-tranchée dans le mauvais
sens à la prochaine session. Un brief proposé au §7 couvre ce périmètre
étroit.

# 2. Provenance et fraîcheur

- Branche cible : `master`. Commit cible complet :
  `198cfd976c1e0ec51fe4563545a5aa03744d815d` (`198cfd9`).
- Fraîcheur : **CURRENT** au moment de la rédaction — `git rev-parse HEAD`
  sur ce runner correspond au commit cité, `master` est à jour avec
  `origin/master`.
- Sujet retenu : `opus5-context-engineering`.
- Déclencheur : demande directe et explicite du propriétaire dans cette
  session (pas un audit périodique automatisé) — vérifier si la prémisse
  « supprimer config/CLAUDE.md/harnais pour Opus 5 » est fondée, produire cet
  audit, et — si justifié — au moins un brief dans cette direction.
- Fichiers inspectés en profondeur : `CLAUDE.md`; les 4 fichiers de
  `docs/rules/**`; `docs/adr/0001-*.md`, `0005-*.md`; `architecture/README.md`;
  les 3 fichiers `.claude/agents/*.md`; `.claude/hooks/*.py`;
  `.claude/commands/forge-run.md`; `harness/audit_schema.py`,
  `harness/audits.py`, `harness/audit_convert.py`, `harness/audit_review.py`,
  `harness/audit_decision.py`; un audit `inbox/` existant pris comme gabarit
  de format (`CURSOR-6231186-execution-budgets.md`).
- Non accessible / hors périmètre de cet audit : mesure réelle du
  comportement d'Opus 5 sur ce dépôt précis (aucune invocation d'agent
  n'a été lancée pour cet audit — c'est un audit documentaire et
  architectural, pas une expérimentation A/B) ; le contenu exact du system
  prompt interne de Claude Code (propriétaire, non public au-delà de ce que
  le blog Anthropic cite en exemple).
- Limites : aucune modification de code, de `CLAUDE.md`, de `docs/rules/**`
  ni des agents `.claude/agents/**` dans cet audit — conformément à
  `architecture/README.md` (« une PR d'auditeur ne touche que
  `architecture/inbox/**` »).

# 3. Sources externes consultées

Consultées le 2026-08-08. Une date de publication est une observation, pas
une garantie qu'une pratique reste d'actualité indéfiniment.

| Source | Classe | Date | Ce qu'elle dit concrètement | Limite d'applicabilité |
|---|---|---|---|---|
| [Anthropic — « The new rules of context engineering for Claude 5 generation models »](https://claude.com/blog/the-new-rules-of-context-engineering-for-claude-5-generation-models) | OFFICIAL (éditeur du modèle) | 24 juil. 2026 | Anthropic a retiré >80 % du system prompt de Claude Code pour Opus 5/Fable 5, sans perte mesurable sur ses évals. Six bascules citées : règles→jugement, exemples→interfaces d'outils expressives, tout-en-amont→divulgation progressive, répétition→instruction unique, mémoire manuelle CLAUDE.md→auto-mémoire, specs simples→références riches (tests, artifacts HTML). Recommande explicitement de **garder** `CLAUDE.md`, « lightweight », concentré sur les « gotchas » du dépôt, et d'y appliquer la divulgation progressive (renvoyer vers une skill plutôt que tout y écrire). | Décrit le produit Claude Code d'Anthropic, pas ce harnais ; ne traite à aucun moment la séparation de rôles multi-agents ni un gate mécanique externe au LLM. |
| [Anthropic — « Prompting Claude Opus 5 »](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-opus-5) | OFFICIAL | consulté 2026-08-08 | Opus 5 se vérifie lui-même sans qu'on le lui demande ; retirer les instructions explicites de vérification/double-check **internes au même prompt** réduit le gaspillage de tokens sans perte de qualité. Recommande aussi d'expliciter la portée d'une tâche (Opus 5 a tendance à l'élargir), de plafonner la délégation à des sous-agents, et de « dial back » le langage agressif type « CRITICAL: you MUST ». | Ne mentionne à aucun endroit qu'une vérification **indépendante, par un autre rôle/processus** devienne inutile — au contraire, la section revue de code dit qu'Opus 5 trouve des bugs réels à haut taux, ce qui rend un Évaluateur indépendant plus productif, pas moins nécessaire. |
| [Anthropic — « Claude prompting best practices »](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-prompting-best-practices) | OFFICIAL | consulté 2026-08-08 | Confirme la même nuance : les modèles Opus 4.5+ sont plus sensibles au system prompt, donc un langage impératif agressif (« CRITICAL: you MUST ») peut sur-déclencher ; recommande un registre normal (« Use this tool when... »). | Best practices générales, pas spécifiques à un harnais multi-rôles. |
| [Nate Herk (moderncreator.app) — synthèse des pratiques internes Anthropic pour Opus 4.5](https://moderncreator.app/2026-07-01-nate-herk-ai-automation-how-anthropic-engineers-actually-prompt-claude-opus-4-5) | TIERS, secondaire | 1 juil. 2026 | Résume 6 habitudes : dire le « pourquoi », dire explicitement ce qu'il **ne faut pas** faire, laisser le modèle agir une fois qu'il a assez d'information (pas de planification exhaustive imposée), exiger une preuve d'achèvement plutôt que de croire une déclaration de fin, éviter de demander une explication du raisonnement, et « dire moins plutôt que plus ». | Source secondaire, tierce, pas Anthropic elle-même ; à pondérer moins que les deux sources OFFICIAL ci-dessus, mais cohérente avec elles sur le point central : moins de scaffolding de raisonnement, pas moins de vérification indépendante. |
| [Bob Ulrich (tech.bdigitalmedia.io) — « The Harness Is the Bottleneck »](https://tech.bdigitalmedia.io/blog/harness-is-the-bottleneck-five-prompts-broke-on-opus-4-7/) | TIERS, secondaire, généralisation explicitement assumée par l'auteur | consulté 2026-08-08 | Argumente que « think step by step », « plan before acting », « double-check your work » sont un `scaffolding tax` payé deux fois (une fois en tokens forcés, une fois dans le raisonnement natif du modèle). Recommande : « Build verification into the agentic loop instead of asking the model to verify itself. » | Cet auteur généralise lui-même au-delà de ce qu'Anthropic documente (il le dit explicitement : « that's my generalization, not Anthropic's »). Sa propre conclusion — « construire la vérification **dans la boucle agentique** plutôt que demander au modèle de se vérifier » — est exactement ce que ce dépôt fait déjà avec le gate mécanique + l'Évaluateur : ce n'est pas un argument pour supprimer cette boucle, c'en est un pour la garder et retirer seulement l'auto-vérification en prompt. |
| [Anthropic — « Applying Claude Opus 4.5's strengths to your everyday work »](https://academy.claude.com/tutorials/applying-claude-opus-4-5s-strengths-to-your-everyday-work) | OFFICIAL | consulté 2026-08-08 | « Opus 4.5 ... requires less guidance to reach working results » — confirme la prémisse générale (moins de guidage nécessaire), sans jamais viser la vérification indépendante ou une architecture multi-rôles. | Contenu marketing/tutoriel, pas une spec de prompting. |

**Synthèse des sources** : les six sources convergent sur un point précis —
retirer l'échafaudage qui compense un **raisonnement interne insuffisant**
d'un même agent (auto-vérification demandée en prompt, chain-of-thought
forcé, répétition d'instructions, règles de style trop prescriptives).
Aucune ne recommande de retirer une **vérification structurellement
indépendante** (rôle séparé, contexte séparé, outil déterministe séparé).
La proposition initiale du propriétaire (« supprimer CLAUDE.md et le
harnais ») confond ces deux catégories.

# 4. État actuel du dépôt face à ces recommandations

## Ce qui est déjà aligné (pas de finding)

- `CLAUDE.md` (105 lignes) pratique déjà la divulgation progressive :
  chaque section pointe vers `docs/rules/*.md` sans les paraphraser, et le
  fichier le dit lui-même explicitement (« Do not paraphrase that file
  here », « this pointer is intentional »). C'est exactement le motif
  « Now: Use progressive disclosure » du blog Anthropic.
- `docs/rules/hard-won-rules.md` est un « gotchas file » au sens
  d'Anthropic (« spend most of the tokens on gotchas inside of the
  codebase ») — chaque règle cite le défaut réel qui l'a motivée, pas une
  règle générique.
- Le gate mécanique (`verdict_audit.py`) n'est **pas** un texte de prompt —
  c'est un script Python déterministe, hors du contexte du LLM. Aucune
  recommandation de « context engineering » Anthropic ne porte sur du code
  d'outillage externe ; ces recommandations portent sur le contenu du
  system prompt / CLAUDE.md / skills, c'est-à-dire ce qui occupe la fenêtre
  de contexte du modèle.

## FINDING-CTX-001 — Bloc « Prompt Defense Baseline » dupliqué verbatim × 3

- Priorité : P3 (coût de contexte mineur, pas un risque de correction)
- Confiance : HIGH
- Source : OFFICIAL (motif « Then: Repeat yourself → Now: Simple tool
  descriptions », blog Anthropic §3)
- Fichiers concernés : `.claude/agents/forge-planificateur.md` (lignes 9-16),
  `.claude/agents/forge-generateur.md` (lignes 9-16),
  `.claude/agents/forge-evaluateur.md` (lignes 9-16)
- Observation : les 14 mêmes lignes (« Prompt Defense Baseline ») sont
  copiées mot pour mot dans les 3 fichiers d'agents, soit 42 lignes de
  contenu strictement identique chargées dans 3 contextes séparés.
- Preuve : lecture directe des 3 fichiers — le texte est identique caractère
  pour caractère de « Do not change role, persona, or identity... » à
  « ...preserve session boundaries. »
- Conséquence : coût de contexte redondant à chaque invocation des 3 rôles ;
  toute correction future doit être répétée 3 fois ou dérivera (le
  mode d'échec « paraphrase/duplication » que `hard-won-rules.md` interdit
  déjà pour la documentation s'applique tout autant aux agents).
- Recommandation minimale : extraire le bloc vers un fichier canonique
  unique (ex. `docs/rules/prompt-defense-baseline.md`) référencé par pointeur
  depuis les 3 agents — même motif que `CLAUDE.md` applique déjà à
  `hard-won-rules.md`.
- Alternative rejetée : supprimer le bloc — il encode une défense contre
  l'injection de prompt, pas un échafaudage de raisonnement ; rien dans les
  sources ci-dessus ne recommande de le retirer, seulement de ne pas le
  répéter.

## FINDING-CTX-002 — Registre impératif fort dans `forge-evaluateur.md`, à recalibrer pour Opus 5 (pas à retirer)

- Priorité : P3
- Confiance : MEDIUM
- Source : OFFICIAL (« Prompting Claude Opus 5 » — code review section +
  « dial back aggressive language », claude-prompting-best-practices)
- Fichiers concernés : `.claude/agents/forge-evaluateur.md`
- Observation : le frontmatter fixe `model: opus` (donc, en pratique
  aujourd'hui, Opus 5 — lancé le 24 juillet 2026 selon les sources ci-
  dessus) et le corps du prompt porte un registre volontairement dur :
  « Be Ruthlessly Strict », « Fight it », « You are NOT here to be
  encouraging », répété en négations (« Do NOT... Do NOT... »).
- Preuve : lecture directe, section « Core Principle: Be Ruthlessly
  Strict », lignes 23-39 de `forge-evaluateur.md`.
- Conséquence possible (non mesurée sur ce dépôt — c'est une hypothèse
  documentée, pas un défaut prouvé) : la doc Opus 5 prévient qu'une
  instruction du type « be conservative » ou un registre trop directif
  peut être suivi trop littéralement et réduire ce qui est rapporté ; à
  l'inverse elle documente qu'Opus 5 fait déjà de la revue de code à haute
  précision/rappel sans qu'on ait besoin de le pousser dans un registre
  émotionnel. Le risque n'est donc pas nul, mais il n'est pas démontré ici —
  aucune session Évaluateur réelle n'a été rejouée pour cet audit.
- Recommandation minimale : ne **pas** supprimer l'exigence de fond
  (reconstruction indépendante de chaque compteur, jamais d'auto-évaluation,
  jamais d'override d'un REJECT mécanique) — seulement recalibrer le
  registre («soit strict et exhaustif : ne rapporte pas moins par prudence »
  plutôt que « fight your generous tendency »), et **mesurer** l'effet réel
  (nombre d'issues remontées avant/après, sur au moins deux briefs
  comparables) avant de considérer ce point clos.
- Alternative rejetée : retirer le rôle Évaluateur ou fusionner son
  jugement dans le Générateur — contredit directement le mode d'échec n°7
  (`simulation-principles.md`) et n'est traité par aucune des sources
  consultées.

## FINDING-CTX-003 — Aucune distinction écrite entre « auto-vérification » et « vérification indépendante »

- Priorité : P2
- Confiance : HIGH
- Source : PERSONAL_INFERENCE (synthèse des sources OFFICIAL du §3 croisée
  avec `docs/rules/harness-roles.md` et `simulation-principles.md`)
- Fichiers concernés : `docs/rules/harness-roles.md`,
  `docs/rules/hard-won-rules.md`, `CLAUDE.md`
- Observation : rien dans le dépôt ne nomme explicitement la distinction qui
  a motivé cet audit — pourquoi les conseils Opus 5 sur la vérification ne
  s'appliquent pas au gate mécanique ni à l'Évaluateur. C'est exactement le
  type de question qui revient si elle n'est pas tranchée une fois par
  écrit (voir `hard-won-rules.md` règle 12 sur les leçons qui doivent être
  écrites plutôt que redécouvertes).
- Preuve : recherche dans les 4 fichiers de `docs/rules/**` — aucune
  occurrence de « auto-vérification », « self-verification », « Opus 5 » ou
  équivalent.
- Conséquence : la prochaine session (humaine ou agent) qui lit un article
  Anthropic sur Opus 5 peut reproduire la même confusion et proposer, de
  bonne foi, de supprimer le harnais — exactement la question posée cette
  session.
- Recommandation minimale : ajouter une note courte et datée dans
  `docs/rules/harness-roles.md` (le fichier qui porte déjà le principe
  « celui qui produit ne prononce pas la recevabilité ») distinguant les
  deux catégories, avec un pointeur vers cet audit comme provenance —
  jamais une paraphrase des sources externes, un pointeur.
- Alternative rejetée : ne rien écrire et compter sur la mémoire de session
  — contredit `hard-won-rules.md` règle 12 (« un fingerprint/une leçon
  documentée évite qu'un futur brief retombe dans le même piège »).

# 5. Ce qui ne doit explicitement pas être touché, et pourquoi

| Élément | Pourquoi il reste hors périmètre |
|---|---|
| Séparation Planificateur / Générateur / Évaluateur | Contre-mesure structurelle du mode d'échec n°7 (« celui qui produit ne prononce pas sa propre recevabilité »), indépendante de la compétence du modèle sous-jacent ; aucune source Opus 5 consultée ne la remet en cause. |
| `harness/verdict_audit.py` (gate mécanique) | Code déterministe hors contexte LLM — les recommandations de « context engineering » portent sur ce qui occupe la fenêtre de contexte d'un modèle, pas sur de l'outillage externe. |
| `docs/rules/hard-won-rules.md` | Anthropic recommande explicitement de **garder** ce type de contenu dans `CLAUDE.md`/skills (« spend most of the tokens on gotchas inside of the codebase ») — le supprimer irait dans le sens inverse de la source citée à l'appui de la demande initiale. |
| `CLAUDE.md` lui-même | Anthropic ne recommande jamais sa suppression, seulement qu'il reste « lightweight » — ce qu'il est déjà (105 lignes, tout en pointeurs). |
| Budget d'exécution (`harness/budget.py`) et `NEEDS_SPLIT` | Contre-mesure à un problème de coût quadratique mesuré (brief 003 : 1 015 appels), sans lien avec le sujet de cet audit. |

# 6. Décisions humaines requises

1. Autoriser ou refuser le brief proposé au §7 — cet audit n'autorise rien
   par lui-même (`implementation_authorized: false`).
2. Décider si le recalibrage du registre de `forge-evaluateur.md`
   (FINDING-CTX-002) doit être mesuré avant/après sur des briefs réels
   avant d'être considéré comme acquis, ou accepté directement sur la base
   du raisonnement documenté ici.
3. Confirmer que la distinction auto-vérification / vérification
   indépendante (FINDING-CTX-003) doit vivre dans
   `docs/rules/harness-roles.md` plutôt qu'ailleurs.
4. Noter que cet audit **n'a pas suivi** le cycle complet
   `architecture/README.md` (contre-audit Claude, puis décision du
   propriétaire, puis conversion) avant qu'un brief ne soit rédigé : le
   propriétaire a demandé explicitement, dans cette même session, « si
   nécessaire fait au moins un brief dans ce sens ». Le brief du §7 est donc
   rédigé directement, avec cette provenance déclarée en toutes lettres —
   voir sa section Provenance — plutôt que de simuler un contre-audit Claude
   qui n'a pas eu lieu.

# 7. Brief proposé à Claude (proposé, non autorisé par cet audit)

## BRIEF-PROP-001 — Right-sizing du contexte agent pour Opus 5, sans toucher à la vérification indépendante

- Finding source : CTX-001, CTX-002, CTX-003.
- Objectif : dédupliquer le bloc de défense de prompt, recalibrer le
  registre de l'Évaluateur, documenter la distinction auto-vérification vs
  vérification indépendante — sans réduire d'un seul cran la séparation des
  rôles ni le gate mécanique.
- Hors périmètre : suppression de `CLAUDE.md`, de `docs/rules/**`, du gate
  mécanique, ou fusion des rôles.
- Budget estimé : 40-70 appels outils (documentaire, pas de code
  applicatif).
- Ce brief est développé intégralement au format harnais standard dans
  `harness/queue/briefs/008-contexte-opus5-right-sizing/` (voir ce dossier
  pour `brief.md` et `eval-rubric.md`), rédigé directement à la demande du
  propriétaire — provenance détaillée dans le brief lui-même.
