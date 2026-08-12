---
review_of: CURSOR-e9a6f4c-codex-passation-full-auto
reviewer: claude-code
target_commit: e9a6f4cffe093e982fe262de0ef6e70d713206d3
reviewed_at: 2026-08-11T08:52:00Z
---

# Contre-audit de CURSOR-e9a6f4c-codex-passation-full-auto

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : e9a6f4cffe093e982fe262de0ef6e70d713206d3
- Le commit existe-t-il dans l'historique de la branche cible ? **Oui.**
  `git merge-base --is-ancestor e9a6f4c master` sort en code 0, et
  `git rev-parse e9a6f4c` rend
  `e9a6f4cffe093e982fe262de0ef6e70d713206d3`.
- Mesures de l'audit rejouées ? **Oui, mais sur un `master` qui a bougé.**
  Au moment de ce contre-audit, `master` vaut `89219cc`, soit trois merges
  plus loin (PR #16, #17, #18 — le travail Codex décrit par l'audit lui-même
  comme « à faire »). Toutes les mesures ci-dessous sont donc données deux
  fois quand l'écart compte : *telle que l'audit l'annonce à `e9a6f4c`*, et
  *telle qu'elle est aujourd'hui à `89219cc`*.

Rejeu, environnement Windows du propriétaire (`py`), et non le conteneur
Linux de l'auditeur :

| commande | résultat de l'audit (e9a6f4c, Linux) | mon résultat (89219cc, Windows) |
|---|---|---|
| suite de tests | 268 passed, 16 skipped | **294 passed**, 0 échec |
| gate sur le brief 009 | 10/10 ACCEPT | **10/10, VERDICT: ACCEPT** |
| `py harness/harness_audit.py` | 20/24, 2 FAIL | **23/24, 1 seul FAIL** (`no_premature_stub_content`) |
| `py harness/audit_schema.py` | tous valides | **All 7 audit(s) valid.** |
| `TODO(operator` dans les workflows | 3 | **3**, mêmes fichiers |

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | §2 — fraîcheur CURRENT et mesures rejouables | **PARTIAL** | La fraîcheur était vraie à l'écriture et ne l'est plus : `master` = `89219cc`, pas `e9a6f4c`. Surtout, le `20/24` de `harness_audit.py` ne se reproduit pas ici : je mesure `SCORE: 23/24` avec un seul `[FAIL] no_premature_stub_content`. Le second FAIL annoncé (`fake_honest_demo_pair`) est un artefact du conteneur Linux, pas un défaut du dépôt. L'audit avait déclaré cette limite d'environnement en §2 — la délimitation lui est donc portée au crédit, mais le chiffre `20/24` ne doit pas être repris tel quel. |
| 2 | §3.1 — le lot 009a a été REJETÉ, corrigé en itération 2, et cette itération n'a jamais été rejugée | **CONFIRMED puis DÉPASSÉ** | Vrai à `e9a6f4c` : `git log --oneline -- .../009.../verdict.md` donnait `de6db4b` (le REJECT) comme dernière écriture, postérieurement à quoi `a16b18c` corrigeait sans rejugement. Depuis, `c9e9291` (PR #16) a ajouté la réévaluation. Le constat était exact et il a été traité — mais **pas dans le sens de la clôture** : le nouveau verdict est lui aussi un **REJECT**, sur quatre défauts neufs C1–C4. La conclusion pratique de l'audit (« rien ne doit démarrer sur 009c avant que 009a ait un verdict à jour ») **tient toujours**. |
| 3 | §3.2 — les trois maillons agents sont non câblés | **CONFIRMED, toujours vrai aujourd'hui** | `git grep -c "TODO(operator" HEAD -- .github/workflows/` rend exactement trois lignes : `pipeline-audit.yml:1`, `pipeline-challenge.yml:1`, `pipeline-forge-run.yml:1`. Lecture des trois blocs : chacun est un `echo` de consigne, aucun n'appelle d'API. La phrase de l'audit « fournir les secrets aujourd'hui ne déclencherait aucun appel d'agent » est littéralement exacte. |
| 4 | §3.3 — `mode:` vaut `full_auto_decision_only` avec un garde fail-closed | **CONFIRMED** | `harness/pipeline/config.yaml:26` porte `mode: full_auto_decision_only` ; `harness/pipeline/full_auto_mode_guard.py` existe et ses tests passent. **Réserve importante** : le caractère « fail-closed » du garde est précisément ce que le REJECT de Codex conteste au point C3 — trois faux workflows sont encore acceptés. Le garde existe et refuse le cas nu ; sa garantie *élargie* est fausse. |
| 5 | §3.3 — Hermes est branché en lecture seule et n'écrit rien | **CONFIRMED** | `.github/workflows/hermes-observer.yml` déclare `permissions:` avec `actions: read`, `checks: read`, `contents: read`, `pull-requests: read` — aucune écriture. Le job transmet l'événement à `runner-event.ps1` hors dépôt. Le runner existe et répond : `gh api .../actions/runners` rend `hermes-forgehistory-pe`, `status: online`, labels `[self-hosted, Windows, X64, hermes-observer]`. |
| 6 | §3.4 — cinq manques pour parler de full automatisation | **CONFIRMED, mais incomplet** | Les cinq sont réels : verdict 009a (toujours REJECT), 009b (produit, non jugé), 009c (non commencé), les deux maillons sans brief, et le remplissage des marqueurs à trous d'un brief converti (`harness/audit_convert.py:243` imprime encore une consigne demandant au Planificateur de les remplir). Il manque un sixième verrou, que l'audit ne mentionne nulle part — voir §3 ci-dessous, point O5. |
| 7 | §4 — répartition des rôles proposée (Codex développe, Cursor audite, Hermes observe, propriétaire décide) | **NEEDS_OWNER** | C'est une proposition d'organisation, pas un fait vérifiable. Techniquement, elle n'entre en conflit avec aucun code existant. Le propriétaire a répondu depuis — voir §3, O1 à O4. |
| 8 | §4 — `.claude/commands/forge-run.md` ne connaît que `--backend claude\|cursor` | **CONFIRMED** | `forge-run.md:3` : `[--backend claude\|cursor]` ; `forge-run.md:74` : `if backend == "cursor"`. `ls harness/backends/` rend `README.md`, `ledger.py`, `run_cursor_generator.sh` — aucun wrapper Codex. Faire de Codex un backend officiel demande donc bien un wrapper, un ADR et une mise à jour de la commande. |
| 9 | §4.1 — techniquement, un sous-agent Codex peut juger | **NON VÉRIFIABLE ICI** | L'affirmation porte sur le produit d'OpenAI (sous-agents en disponibilité générale depuis mars 2026, agents personnalisés en TOML sous `.codex/agents/`). Rien dans ce dépôt ne permet de la confirmer ou de l'infirmer : `ls .codex` rend « No such file or directory ». Je ne la retiens ni ne la conteste ; elle est hors du périmètre de preuve du dépôt et l'audit aurait dû la marquer comme telle. |
| 10 | §4.1 — contractuellement, non : seul le Générateur est délégable | **CONFIRMED** | `docs/rules/harness-roles.md:9` autorise explicitement un backend pour le Générateur (« or a backend under `harness/backends/` »), tandis que les lignes 8 et 10 nomment les fichiers d'agent Claude pour Planificateur et Évaluateur, et que les lignes 18-22 donnent la raison : préserver le sens du contrôle `verdict_is_not_self_authored`. Faire juger Codex par Codex sans changer cette règle serait bien une violation silencieuse du contrat. |
| 11 | §4.1 — structurellement, un sous-agent engendré par le producteur n'est pas indépendant (options A/B/C) | **CONFIRMED sur le raisonnement** | Le raisonnement est solide et je le contresigne : l'indépendance vient de *qui déclenche le juge*, pas du fait d'être un processus séparé. Les trois faiblesses nommées (sélection des preuves, angles morts partagés, consolidation par le parent) sont exactes. Fait notable et à porter au crédit de Codex : dans sa réévaluation de 009a, il a utilisé un sous-agent **en lecture seule** pour reconstruire des compteurs, a reproduit lui-même les sorties avant de les retenir, et l'a écrit noir sur blanc dans `verdict.md` (« La lecture secondaire n'a modifié aucun fichier et n'a rendu aucun verdict »). La règle proposée a donc déjà été respectée en pratique. |
| 12 | §5 R3 — la protection de branche est indisponible sur ce plan GitHub | **CONFIRMED** | `gh api repos/PLiagre/ForgeHistory/branches/master/protection` rend `HTTP 403` : « Upgrade to GitHub Pro or make this repository public to enable this feature. » Le dépôt est `private: true` sur un compte de type `User`. La liste d'exclusion de `merge-bot.yml` est donc bien la seule barrière réelle, et R3 est un risque authentique, pas une précaution de style. |
| 13 | §5 R1, R2, R4, R5, R6 — les autres risques nommés | **CONFIRMED** | R1 (bâtir 009b sur un 009a rejeté) : réalisé à moitié — 009b a bien été produit avant clôture de 009a, mais Codex a explicitement vérifié leur indépendance et l'a écrite dans son verdict, ce que le brief autorise. R2, R4, R6 sont structurels et exacts. R5 est étayé par un chiffre déjà mesuré dans le dépôt (982 appels sur un Générateur). |
| 14 | §6 — prompt de passation | **NEEDS_OWNER (pas un fait)** | Un prompt n'est ni vrai ni faux. Contrôle de non-nuisance effectué : il n'autorise rien, renvoie systématiquement au brief comme source unique, interdit l'auto-jugement, et ses interdits de chemins recopient exactement la denylist réelle de `merge-bot.yml` (`.github/workflows/**`, `harness/verdict_audit.py`, `VISION.md`). Il est cohérent avec le dépôt. Preuve d'usage : Codex l'a suivi et a produit trois PR conformes. |
| 15 | §7 — trois briefs proposés (clore 009, backend Codex officiel, contrat d'écriture Hermes) | **NEEDS_OWNER** | Aucun des trois ne double un brief existant : `harness/queue/briefs/` ne contient rien sur le backend Codex ni sur Hermes. BRIEF-PROP-001 ne demande pas un nouveau brief mais l'exécution du 009 existant — formulation correcte, pas de doublon. |
| 16 | §8 — cinq décisions humaines requises | **NEEDS_OWNER** | Correctement identifiées comme des arbitrages, pas des faits. Le propriétaire y a répondu le 2026-08-11 — voir §3. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

Les questions O1 à O4 reprennent la §8 de l'audit. **Le propriétaire y a
répondu le 2026-08-11** ; je consigne sa réponse ici pour que la décision
qui suivra parte d'un énoncé écrit, mais c'est la décision — pas ce
contre-audit — qui a autorité.

- **O1 — Qui évalue le travail de Codex ?** Réponse du propriétaire :
  Claude reste l'Évaluateur par défaut, **et Codex doit pouvoir le
  remplacer lorsque Claude atteint son plafond de crédit**. C'est
  l'option B de la §4.1 (session Codex distincte, déclenchée par un
  tiers), et non l'option C. Elle exige un ADR modifiant
  `docs/rules/harness-roles.md`, qui aujourd'hui réserve ce rôle à Claude
  (point 10 ci-dessus). Tant que cet ADR n'existe pas, la règle en
  vigueur est celle du fichier, pas celle de l'intention.
- **O2 — Codex devient-il un backend officiel ?** Réponse : oui,
  développeur du projet. Demande le wrapper, l'ADR et la mise à jour de
  `forge-run.md` (point 8).
- **O3 — Hermes doit-il écrire dans le dépôt ?** Réponse : oui, pour des
  **briefs de suivi et des tableaux de bord**. Précision technique que le
  propriétaire n'a peut-être pas en tête : Hermes produit **déjà** des
  rapports quotidiens et hebdomadaires et sert un tableau de bord sur
  `http://127.0.0.1:9119`, mais entièrement **hors dépôt**, dans
  `.private/` de son propre dossier, et il est en phase « shadow »
  jusqu'au 2026-08-24 selon son propre fichier de configuration. La
  question réelle n'est donc pas « Hermes peut-il produire des rapports »
  — il le fait — mais « ces rapports entrent-ils dans le dépôt, où, sous
  quel format, et avec quelle preuve d'auteur ».
- **O4 — `pipeline-audit.yml` doit-il appeler Cursor sur chaque PR ?**
  Réponse : oui, Cursor reste auditeur **de chaque PR**. Écart technique à
  signaler : ce workflow est aujourd'hui déclenché sur des commits, pas
  sur des PR. « Auditer chaque PR » et « auditer chaque commit fusionné »
  ne sont pas le même déclencheur — c'est exactement la nuance que la §8
  de l'audit avait relevée, et elle reste entière.

- **O5 — verrou que l'audit ne mentionne pas : la fusion.** C'est le seul
  point où je vais au-delà de l'audit, parce qu'il vise directement
  l'objectif « aucune action du propriétaire ».
  `.github/workflows/merge-bot.yml` n'auto-fusionne que les branches
  commençant par `cursor/` ou `forge-bot/` (ligne `if:`), et uniquement
  quand **tous** les chemins modifiés tombent dans
  `architecture/inbox/`, `architecture/reviews/` ou
  `harness/queue/briefs/*/feedback/`. Conséquences mesurables :
  1. une branche `codex/` n'est **jamais** auto-fusionnée, quel que soit
     son contenu — les trois PR Codex ont donc bien dû être fusionnées à
     la main, ce qui correspond à ce que le propriétaire décrit ;
  2. **aucune PR de code** n'est auto-fusionnable, même sur une branche
     autorisée, puisque tout chemin hors des trois dossiers
     documentaires fait échouer l'étape.
  Autrement dit : même si les trois maillons agents étaient câblés
  demain, la boucle s'arrêterait encore à la fusion. Le zéro-intervention
  demande une décision explicite du propriétaire sur ce point, et cette
  décision est lourde : la denylist de `merge-bot.yml` est la **seule**
  barrière réelle, la protection de branche étant indisponible (point 12).

## 4. Synthèse

**Ce qui tient.** L'ossature factuelle de l'audit est solide et se
reproduit : les trois maillons sont bien des stubs (point 3), le contrat
des rôles interdit bien aujourd'hui ce que le propriétaire veut permettre
(point 10), la protection de branche est bien indisponible (point 12), et
le raisonnement sur l'indépendance du juge (point 11) est le meilleur
apport du document — il a d'ailleurs déjà été appliqué correctement par
Codex dans les faits. Le constat central de la §3.1 était exact.

**Ce qui tombe ou doit être délimité.** Trois choses. Le `20/24` de
`harness_audit.py` ne se reproduit pas — je mesure `23/24` (point 1) ; le
chiffre de l'audit reflète son conteneur, pas le dépôt. La §3.1 est
**dépassée** : l'itération 2 a été rejugée depuis, et le verdict reste
REJECT sur quatre défauts neufs — le blocage subsiste donc, pour d'autres
raisons que celles écrites (point 2). Et la §4.1 « techniquement, oui »
repose sur des affirmations produit invérifiables depuis ce dépôt
(point 9) : à ne pas citer comme un fait établi.

**Ce que l'audit ne voit pas.** Le verrou de fusion (O5). Un document qui
mesure l'écart vers la full automatisation et ne mentionne pas que rien
de substantiel n'est auto-fusionnable a un angle mort sur sa propre
question. Ce n'est pas une erreur de mesure, c'est un manque de
périmètre.

**Recommandation de traitement.** L'audit mérite d'être approuvé pour ses
points 3, 5, 8, 10, 11, 12 et 13 et pour ses trois briefs proposés, avec
trois réserves inscrites : chiffre `20/24` non retenu, §3.1 marquée comme
dépassée par `c9e9291`, et §4.1 « techniquement » marquée comme non
vérifiée. La conversion en briefs doit **ajouter** le verrou de fusion
(O5), que l'audit n'a pas couvert, sans quoi les briefs produits
résoudraient tout sauf la dernière marche.
