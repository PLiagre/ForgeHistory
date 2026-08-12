---
review_of: CURSOR-bb8fe11-hermes-console-adr-0011
reviewer: claude-code
target_commit: bb8fe11b860f8383e5178994f35ca116f89da2fd
reviewed_at: 2026-08-12T13:00:57Z
---

# Contre-audit de CURSOR-bb8fe11-hermes-console-adr-0011

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `bb8fe11b860f8383e5178994f35ca116f89da2fd`.
- Le commit existe : `git cat-file -e bb8fe11b860f8383e5178994f35ca116f89da2fd`
  → succès ; `git log --oneline -1 bb8fe11` → « hermes: décision « ok pour
  tout » consignée et reflétée dans la roadmap ». Ancêtre de `origin/master`
  confirmé (`git merge-base --is-ancestor bb8fe11 origin/master` → succès).
- Reproduction : `python3 -m pytest harness/tests/ -q` rejoué directement
  sur le HEAD actuel (après `pip install pytest`, absent de cet
  environnement) → `309 passed, 16 skipped` — identique au caractère près à
  la sortie collée en § 8 de l'audit.
- `git show --stat --format= bb8fe11` → `ROADMAP.md | 23 ++++++-------`,
  `hermes/requests/DEMANDE-...md | 22 +++++++++-`, soit `2 files changed,
  37 insertions(+), 8 deletions(-)` ; `git show --stat --format= e641c0b`
  (l'autre commit de la PR) → `docs/adr/0011-...md | 120 ++++`,
  `hermes/README.md | 11 +++`, soit `2 files changed, 131 insertions(+)`.
  Somme : 4 fichiers, +168, −8 — exactement le résumé exécutif de l'audit.
- **Environnement de cette revue : pas de `GH_TOKEN`/`gh auth` disponible**
  (`gh auth status` → « not logged into any GitHub hosts »). Tout ce qui
  dépend d'un appel `gh` en direct (tableau CI § 1, `gh pr view/checks 34`
  en § 8) n'a pas pu être rejoué tel quel ici — noté en PARTIAL avec preuve
  de substitution (timestamps `git log`) plutôt que passé sous silence.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| §1 | Classification CI « verte », tableau des jobs, deux `skipping` (`cursor-scope`, `check-and-automerge`) | **PARTIAL — logique confirmée, appel `gh` live non rejouable ici** | Impossible de rappeler `gh pr checks 34` sans jeton dans ce bac à sable. Ce que j'ai pu vérifier indépendamment : `audit-guard.yml:30` — `cursor-scope` a bien `if: github.event_name == 'pull_request' && startsWith(github.head_ref, 'cursor/')`, donc `skipping` est *structurellement* correct pour une branche `forge/*` ; `merge-bot.yml:27` — `check-and-automerge` a bien `if: startsWith(github.head_ref, 'cursor/') || startsWith(github.head_ref, 'forge-bot/')`, même conclusion. La reproduction exacte de `309 passed, 16 skipped` (ci-dessus) corrobore que le job `tests` décrit comme vert l'est réellement sur ce SHA. Je ne conteste pas le tableau, je ne peux simplement pas le certifier moi-même faute d'accès réseau/API GitHub authentifié. |
| §2 | Chronologie : PR ouverte 12:24:28, fusionnée 12:25:24 (56 s), `reviewDecision` vide, `reviews: 0`, aucun audit `architecture/inbox/` ne portait `target_commit: bb8fe11` à l'instant de la fusion | **CONFIRMED** (via une voie indépendante de `gh`) | Le commit de fusion existe et son horodatage `git` corrobore l'audit à la seconde : `git show -s --format="%ci %s" 0269d8e` → `2026-08-12 14:25:23 +0200 Merge pull request #34 …` = **12:25:23 UTC**, contre 12:25:24 annoncé (écart d'1 s, cohérent avec l'écart entre horodatage `commit` Git et horodatage `mergedAt` de l'API GitHub). Le commit de tête `bb8fe11` est horodaté `2026-08-12 12:23:51 +0000`, antérieur et cohérent avec une ouverture de PR à 12:24:28. Sur l'absence d'audit au moment de la fusion : reconstruit sans dépendre de `gh` — `grep -rl "bb8fe11b860f8383e5178994f35ca116f89da2fd" architecture/inbox/` sur l'état actuel du dépôt ne renvoie que **ce fichier-même** (le seul audit visant ce SHA), et l'unique autre fichier qui mentionne cette chaîne (`CURSOR-0269d8e-hermes-console-droit-executer.md`) a `created_at: 2026-08-12T12:33:05Z`, donc lui-même postérieur à la fusion (12:25:24) et n'a jamais pu satisfaire le prédicat 4 avant elle. Confirme la thèse mesurée. |
| P1-1 | Fusion sans qu'aucune des 4 preuves de `conditional-merge-gate.md` n'ait été lue ; prédicat 4 impossible à satisfaire dans le délai mesuré | **CONFIRMED** | `docs/rules/conditional-merge-gate.md:27-55` décrit bien les 4 prédicats et, ligne 52, « Chaque lecture est refaite immédiatement avant la tentative de fusion » — texte vérifié verbatim. Prédicat 4 (lignes 46-50) exige « exactement un fichier … portant `target_commit: <SHA de tête>` » : voir ligne ci-dessus, aucun fichier ne portait `bb8fe11` avant 12:25:24 (le premier, ce fichier même, est daté 12:45:00 dans son propre frontmatter). ADR-0011:33 affirme bien mot pour mot « les conditions de fusion elles-mêmes (CI verte, gate ACCEPT, verdict d'un acteur différent du producteur, audit Cursor) ne sont ni levées ni affaiblies » — citation exacte. Le raisonnement de l'audit (l'affirmation décrit un état qui n'existe pas encore) tient. |
| P1-2 | ADR-0011 confie à Hermes l'appréciation des 4 preuves (« refuser d'exécuter … si une preuve manque »), en contradiction avec l'interdit ADR-0010 sur les verdicts, et sans que ce point figure dans la demande d'origine | **CONFIRMED** | `docs/adr/0011-...:117-118` — citation exacte vérifiée (« Hermes doit refuser d'exécuter une fusion si une preuve manque et le dire au propriétaire »). `docs/adr/0010-...:32` — la ligne du tableau des interdits contient bien littéralement `verdicts` dans la colonne « n'écrit jamais » pour Hermes, citation exacte. `hermes/requests/DEMANDE-...:97,102-106` — vérifié verbatim : l'action n°1 y est formulée comme un geste mécanique (« le clic final humain actuel ») et les garde-fous cités (confirmation, jeton minimal, trace, `127.0.0.1`) y figurent mot pour mot ; aucune mention d'une appréciation des preuves par Hermes dans la demande. `docs/adr/0011-...:108-109` — citation exacte de l'aveu (« la discipline de l'installation locale, que le dépôt ne peut pas vérifier mécaniquement »). La tension entre cet aveu et l'affirmation « seule la main qui l'exécute change » (`:101-102`, citation exacte) est réelle et non réconciliée dans le texte. Seule réserve mineure : la citation `:101-102` pour cette dernière phrase est exacte à la ligne ; c'est un point de forme, sans effet sur le fond. |
| P1-3 | Le canal d'entrée réel non traité est `hermes-observer.yml` (`pull_request_target`, runner self-hosted, `EventPath` transmis en entier), pas le port 9119 que l'ADR cite comme garantie | **CONFIRMED**, avec une imprécision de citation mineure et sans effet sur le fond | `.github/workflows/hermes-observer.yml:4` → `pull_request_target:` (ligne exacte) ; `:32` → `runs-on: [self-hosted, Windows, X64, hermes-observer]` (ligne exacte) ; `:37-40` → bloc `runner-event.ps1 -EventName … -EventPath '${{ github.event_path }}'`, `EventPath` à la ligne 40 (audit cite « lignes 37-40 » pour le bloc entier — exact). `docs/adr/0011-...:55-56` → citation exacte de la garantie « surface réseau inchangée … 127.0.0.1 … aucune exposition réseau sans couche d'authentification ». `hermes/requests/DEMANDE-...:29-34` → citation exacte de « Hermes local reçoit donc déjà les événements du projet ». Seule imprécision : l'audit cite `docs/adr/0011-...:116` pour la phrase « Hermes agit sans ordre (bug, prompt-injection via un événement reçu) » — la phrase existe verbatim, mais à la ligne **112**, pas 116 (`grep -n "Hermes agit sans ordre" docs/adr/0011-hermes-console-du-proprietaire.md` → `112:`). Décalage de 4 lignes, sans incidence sur la validité du constat : la garantie réseau porte effectivement sur le port 9119 alors que le canal d'entrée non fiable documenté est `hermes-observer.yml`, ce que je vérifie de façon indépendante ci-dessus. |
| P1-4 | ADR-0011 absent de `docs/adr/README.md` après fusion ; aucun test ne couvre cette classe d'erreur | **CONFIRMED** | `grep -c "0011" docs/adr/README.md` → `0`, rejoué à l'identique. `git log --oneline -3 -- docs/adr/README.md` → `9ad76ff ADR-0010: …` en tête, confirmant que le précédent ADR avait bien mis l'index à jour dans son propre commit alors que la PR #34 (2 commits, 4 fichiers touchés au total) ne touche jamais `docs/adr/README.md`. `grep -rn "docs/adr" harness/tests/` → seules des occurrences dans des fixtures de test (`test_budget.py`, `test_verdict_audit.py`, `test_verdict_audit_actor_identity.py`), aucun contrôle de complétude de l'index — confirmé, aucune porte mécanique n'existe pour cette classe d'erreur. |
| P2-1 | Le triplet de permissions PAT (`contents, pull-requests, actions`) dépasse le périmètre fermé de 4 actions ; `contents:write` autorise la poussée sur n'importe quelle branche ; la protection de branche est indisponible (HTTP 403) | **CONFIRMED** | `docs/adr/0011-...:47-49` — citation exacte du triplet. Le comportement GitHub cité (`contents:write` d'un PAT fine-grained autorise l'écriture sur toute branche du dépôt en portée, non restreinte par action) est un fait de plateforme externe au dépôt, cohérent avec la documentation GitHub des permissions fine-grained PAT. Le « HTTP 403 vérifié » est bien sourcé et daté ailleurs dans le dépôt : `docs/rules/full-auto-pipeline.md:152-153` (« `gh api repos/{owner}/{repo}/branches/master/protection` … renvoie `403` ») et `architecture/decisions/DECISION-CURSOR-e9a6f4c-...:40` (« protection de branche indisponible sur ce plan GitHub (`HTTP 403`, vérifié le 2026-08-11) ») — deux citations retrouvées verbatim. La nuance reconnue par l'audit lui-même (fusionner via l'API exige `contents:write` en pratique) est correcte et honnête. |
| P2-2 | La trace obligatoire (`hermes/reports/`) est auto-rédigée par l'acteur qui agit et n'inclut pas les 4 lectures ; contradiction avec le principe « celui qui produit ne prononce pas la recevabilité » et avec `check_verdict_not_self_authored` | **CONFIRMED** | `docs/adr/0011-...:50-51` (l'audit cite `:50-52`, léger débordement d'une ligne sans effet sur le fond) — citation exacte de « chaque action exécutée est consignée dans un rapport `hermes/reports/` (quoi, quand, sur ordre de qui) » ; ce contenu n'inclut effectivement pas « les preuves examinées ». `architecture/review-guidelines.md:37` — citation exacte de « celui qui produit ne prononce pas la recevabilité » (l'audit cite `:36-37`, la phrase s'étend bien sur ces deux lignes). `harness/verdict_audit.py:308` définit bien `check_verdict_not_self_authored`, référencé aussi dans `codex_preflight.py:40` — le parallèle mécanique cité par l'audit est réel. |
| P2-3 | `audit-guard / cursor-scope` atteste un préfixe de branche (`startsWith(github.head_ref, 'cursor/')`), pas une identité d'acteur, alors que les deux commits de la PR ont `Author: Cursor Agent` | **CONFIRMED** (la condition du workflow), **NEEDS_OWNER** pour la partie identité d'auteur non re-vérifiable ici | `audit-guard.yml:30` → `if: github.event_name == 'pull_request' && startsWith(github.head_ref, 'cursor/')` confirmé verbatim — c'est bien une chaîne de caractères choisie par l'auteur de la PR, pas un contrôle d'identité GitHub. Je n'ai pas pu revérifier `Author: Cursor Agent <cursoragent@cursor.com>` sur les deux commits sans accès `gh`/API GitHub authentifiée dans cet environnement — `git log` local montre les commits `bb8fe11` et `e641c0b` sans afficher d'auteur Cursor distinct de l'auteur de commit habituel de ce dépôt, donc ce sous-point précis reste à la charge du propriétaire s'il veut une garantie indépendante. Le constat central (la garde teste un préfixe, pas une identité) est prouvé par le code du workflow seul, indépendamment de ce sous-point. |
| P3-1 | `ADR-0010:96-97` et `hermes/README.md:41` (phrase « aucun workflow n'exécute ce que Hermes écrit ») non annotés malgré la révision de leur prémisse ; nouvelle section ADR-0011 insérée juste après | **CONFIRMED sur le fond, imprécision de numéro de ligne sans effet** | `docs/adr/0010-...:96-97` — citation exacte confirmée (« bornée … par le fait qu'aucun workflow n'exécute ce que Hermes écrit »), section Negative toujours sans annotation `superseded by`. `hermes/README.md` — `grep -n "Aucun workflow n'exécute\|Ce qu'Hermes peut exécuter"` → phrase inchangée à la ligne **45** (l'audit dit « ligne 41 ») et nouvelle section à la ligne **47** (l'audit dit « ligne 43 ») — décalage constant de 4 lignes sur les deux citations (cohérent avec une différence de rendu, pas une erreur de fond), mais la relation « juste après » est vérifiée exacte : une seule ligne blanche sépare les deux dans les deux cas. `docs/adr/template.md:4` — confirmé, le gabarit prévoit bien `superseded by ADR-NNNN`. |
| P3-2 | Deux imprécisions `ROADMAP.md` : étape barrée « faite » en position 1 des prochaines étapes ; « H1-H5 » dans l'historique alors que le corps n'énumère que H1-H4 | **CONFIRMED** | `ROADMAP.md:68` → `1. ~~**Provisionner les secrets CI**~~ — **fait le 2026-08-12**` bien en position 1 de la liste « Prochaines étapes (dans l'ordre) » (`:66`). `ROADMAP.md:94` → « (H1-H5, ADR-0011) » confirmé verbatim ; le corps (`:77-83`) énumère explicitement H1, H2, H3, H4 et ne définit jamais H5 — H5 n'existe que dans `hermes/requests/DEMANDE-...:108-122`, confirmé par lecture directe de ce fichier. |
| P3-3 | Deux affirmations du corps de PR inexactes sans changer la conclusion (raison invoquée pour l'absence d'auto-merge ; regroupement des commits) | **CONFIRMED** | `merge-bot.yml:27` → `if: startsWith(github.head_ref, 'cursor/') || startsWith(github.head_ref, 'forge-bot/')`, confirmé : le job ne s'exécute pas du tout sur `forge/*`, donc l'allowlist n'a jamais été consultée — la raison donnée dans le corps de PR (« hors allowlist ») est bien une reconstruction a posteriori inexacte, même si la conclusion (relecture humaine) est correcte. `git show --stat e641c0b` → confirmé, `hermes/README.md` est bien dans le commit `adr-0011:` et non dans un commit « hermes » séparé. |
| P3-4 | L'affirmation « HTTP 403 vérifié » (ADR-0011:90-92) ne cite pas où la vérification est consignée | **CONFIRMED** | `docs/adr/0011-...:90-92` — citation exacte vérifiée (« la denylist du merge-bot est la seule barrière réelle (protection de branche indisponible sur ce plan GitHub, `HTTP 403` vérifié) … »), sans renvoi. La preuve existe bien ailleurs et est datée : `docs/rules/full-auto-pipeline.md:152-153` et `architecture/decisions/DECISION-CURSOR-e9a6f4c-...:40`, les deux retrouvés verbatim (voir P2-1 ci-dessus). |
| §6 | 9 points « ce que la PR fait bien » (preuve d'exécution exacte, CI verte, taille du diff, cycle de vie de la demande, correction factuelle permise, actions 2/3 réelles, dépense plafonnée, garde-fous non ajoutés par le rédacteur, gabarit ADR suivi) | **CONFIRMED pour tout ce qui est vérifiable sans `gh`**, PARTIAL sur la seule sous-partie CI verte | Point 1 : reproduit à l'identique (`309 passed, 16 skipped`, voir § 1 ci-dessus). Point 3 : `git show --stat bb8fe11` + `e641c0b` → 4 fichiers, +168/−8, confirmé sous le seuil ~5 fichiers de `review-guidelines.md:33`. Point 4 : `hermes/requests/DEMANDE-...` a bien `status: REFLECTED_IN_ROADMAP` en frontmatter, transition listée dans `hermes/README.md:66`, confirmé. Point 5 : commit `bb8fe11` signale la correction, `HANDOFF.md` (non relu ligne à ligne ici, hors périmètre de cette PR) est cité comme preuve externe — plausible, non contesté. Point 6 : `pipeline-forge-run.yml:31-34` (`workflow_dispatch`, `brief_dir`) confirmé exact ; `docs/rules/full-auto-pipeline.md` documente bien `pipeline/pause` comme coupe-circuit — confirmé. Point 7 : `pipeline-forge-run.yml` contient bien `ci_budget_guard.py precheck`, `--max-budget-usd 5.00`, `ci_budget_guard.py record`, aux abords des lignes citées — confirmé. Point 8 : comparaison directe demande/ADR faite ci-dessus (P1-2) — confirmé, les garde-fous préexistent dans la demande. Point 9 : structure de l'ADR (Date/Status/Deciders/Context/Decision/Alternatives avec Why not/Consequences Positive-Negative-Risks) vérifiée conforme à `docs/adr/template.md`. Point 2 (CI verte) hérite de la même réserve que § 1 : non re-vérifiable via `gh` dans ce bac à sable. |
| §7 | 3 briefs atomiques proposés (porte de fusion lisible ; garde de complétude de l'index ADR ; garde de périmètre par identité d'acteur) | **CONFIRMED comme conséquences logiques des constats ci-dessus** ; leur conversion effective est **NEEDS_OWNER** | Chaque brief proposé répond exactement aux constats P1-1/P1-2/P2-2 (brief 1), P1-4 (brief 2), P2-3 (brief 3) déjà confirmés ci-dessus — la correspondance est vérifiée par lecture croisée. L'audit lui-même ne prescrit rien (`status: PROPOSED`, 3 flags `false`, confirmé en frontmatter) : la conversion en brief appartient au propriétaire, pas à cette revue. |
| §9 | Sources externes (S1-S8), citées avec URL et date de consultation par thème | **NEEDS_OWNER / non vérifiable ici** | Cet environnement de revue n'a pas d'accès réseau sortant vérifié pour aller lire chacune des 8 URLs citées ; je ne peux ni les confirmer ni les infirmer. Le format (URL + date de consultation, une phrase de synthèse par thème) respecte la forme imposée par `architecture/review-guidelines.md`. Ceci ne change aucune conclusion technique du dépôt : les sources servent, par le texte même de l'audit, à situer les constats dans l'état de l'art, jamais à trancher un fait vérifiable localement. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **P1-2 / P1-3 — que fait-on de la délégation d'appréciation à Hermes ?**
  L'ADR contient à la fois l'aveu (la frontière « ordre explicite » n'est
  pas vérifiable mécaniquement) et l'affirmation contraire (« seule la main
  change »). C'est un arbitrage de contrat entre acteurs (ADR-0010 vs
  ADR-0011), pas un fait technique contestable — au propriétaire de dire si
  ADR-0011 doit être amendé pour lever l'ambiguïté avant que le câblage H4
  ne se fasse hors dépôt.
- **P1-3 — séparation architecturale avant câblage H4.** Le canal d'entrée
  réel (`hermes-observer.yml`, self-hosted, payload complet) et le futur
  jeton de fusion vivront dans la même installation locale. C'est un choix
  de sécurité du propriétaire sur sa propre machine, hors de portée d'un
  audit du dépôt — mais le moment (avant H4) est le seul où l'ADR peut
  encore le poser.
- **P2-3 — deux commits `Author: Cursor Agent`** : je n'ai pas pu revérifier
  cette métadonnée d'auteur GitHub sans jeton dans cet environnement. Si le
  propriétaire veut une garantie indépendante avant de convertir le brief 3,
  `gh pr view 34 --json commits` avec un jeton authentifié le confirme en
  une commande.
- **§9 — sources externes** : leur existence et leur contenu réel ne sont
  pas vérifiables depuis ce bac à sable ; à confirmer par le propriétaire
  s'il veut s'appuyer explicitement sur elles pour trancher les briefs 1
  et/ou 3.
- **Chevauchement avec un second audit** (hors périmètre de cette revue,
  signalé pour information) : `architecture/inbox/CURSOR-0269d8e-hermes-console-droit-executer.md`
  audite le **commit de fusion** `0269d8e` de la même PR #34, créé
  `2026-08-12T12:33:05Z` — donc avant le présent audit (`12:45:00Z`) mais
  après la fusion. C'est cohérent avec ADR-0010 (Cursor se déclenche « aussi
  sur chaque `pull_request` », donc sur le SHA de tête `bb8fe11`, **et** sur
  `push` vers `master`, donc sur le SHA de fusion `0269d8e`) — ce n'est donc
  probablement pas une anomalie, mais deux audits actifs sur la même PR
  peuvent proposer des briefs qui se recoupent. Je n'ai pas challengé le
  contenu de cet autre audit ; le propriétaire voudra peut-être les traiter
  ensemble avant conversion pour éviter un double brief sur les mêmes
  constats.

## 4. Synthèse

Sur les 11 constats numérotés de l'audit (P1-1 à P1-4, P2-1 à P2-3, P3-1 à
P3-4) plus le résumé exécutif, la chronologie, la section « ce qui va bien »
et les briefs proposés, **la quasi-totalité se confirme à la reproduction
indépendante**, souvent par une voie alternative à celle de l'audit plutôt
qu'une simple relecture (notamment la chronologie, reconstruite ici sans
`gh` à partir des horodatages `git log` locaux et de l'absence de tout
fichier `architecture/inbox/` visant `bb8fe11` avant la fusion).

Trois imprécisions mineures et localisées, toutes de la même nature (un
décalage de quelques lignes dans une citation `fichier:ligne`, jamais dans
le texte cité lui-même) : P1-3 cite `:116` pour une phrase qui est à la
ligne 112 ; P3-1 cite les lignes 41/43 de `hermes/README.md` pour des
phrases qui sont aux lignes 45/47 (décalage constant de 4, sans doute un
rendu Markdown différent du mien) ; P2-2 déborde d'une ligne (`:50-52` pour
un texte qui s'arrête à `:51`). Aucune de ces trois imprécisions n'affecte
la validité du constat qu'elle prétend étayer — le texte cité est, dans les
trois cas, retrouvé verbatim ailleurs dans le fichier.

**Aucun REFUTED.** Le seul point que je ne peux ni confirmer ni infirmer par
moi-même dans cet environnement (pas de `GH_TOKEN`) est tout ce qui dépend
d'un appel direct à l'API GitHub — le tableau CI complet du § 1, la
métadonnée `Author: Cursor Agent` de P2-3, et l'existence réelle des 8
sources externes du § 9. Sur chacun de ces points, j'ai soit trouvé une
preuve de substitution qui corrobore la même conclusion sans dépendre de
`gh` (chronologie, logique des `if:` de workflow), soit je l'ai marqué
explicitement PARTIAL/NEEDS_OWNER plutôt que de supposer.

**Recommandation de traitement** : les constats techniques P1-1 à P1-4,
P2-1, P2-2 et P3-1 à P3-4 sont solides et peuvent fonder une conversion en
brief sans réserve technique. P2-3 est solide sur son constat central
(la garde teste un préfixe, pas une identité) mais sa preuve d'auteur
GitHub mérite une vérification `gh` avant conversion si le propriétaire
veut une garantie complète. Rien ici ne bloque le passage à l'étape
suivante de la boucle (`/forge-audit-accept` ou `-reject`) ; le point sur
le second audit `CURSOR-0269d8e` (§ 3 ci-dessus) est une information utile
pour le séquencement, pas un blocage.
