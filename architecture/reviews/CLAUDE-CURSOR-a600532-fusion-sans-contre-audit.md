---
review_of: CURSOR-a600532-fusion-sans-contre-audit
reviewer: claude-code
target_commit: a600532e714a9ff4d1b3c739859a9357884d5f81
reviewed_at: 2026-08-13T07:15:00Z
---

# Contre-audit de CURSOR-a600532-fusion-sans-contre-audit

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

Environnement de cette relecture : `gh` n'est pas authentifié ici (pas de
session GitHub CLI). Toutes les vérifications qui portent sur GitHub
(runs de workflow, PR, revues) ont donc été refaites via l'API REST
publique non authentifiée (`curl https://api.github.com/...`), qui
fonctionne en lecture seule sur ce dépôt public — mêmes données, chemin
d'accès différent de celui de l'auditeur. Deux endpoints restent
inaccessibles sans authentification (téléchargement des logs bruts d'un
job, réglages de protection de branche) ; signalé point par point
ci-dessous plutôt que passé sous silence.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : a600532e714a9ff4d1b3c739859a9357884d5f81 — **existe**,
  `git rev-parse a600532` le résout, `git log --oneline -1` confirme
  « Merge pull request #57 from PLiagre/forge/011-sim-monde-vivant-a67c »,
  et il est bien sur `master` (`git branch --contains a600532` → `* master`).
- Mesures rejouées : gate mécanique, `sim/tests/`, `harness/tests/`,
  re-mesure du monde vivant (200 ticks + hachage double-graine),
  `harness_audit.py` avant/après démo F0, regénération de
  `hermes/DASHBOARD.md`, recensement inbox × registre, contenu de
  `hermes/dashboard.py`, `.github/workflows/*.yml`,
  `harness/pipeline/orchestrator.py`. Détail par point ci-dessous.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | Fusion propre : CI verte (5 workflows), gate `ACCEPT`, `sim/tests/` 20 succès, `harness/tests/` 314 succès + 16 ignorés | CONFIRMED | Rejoué à l'identique : `verdict_audit.py` → `ACCEPT` ; `pytest sim/tests/ -q` → `20 passed` ; `pytest harness/tests/ -q` → `314 passed, 16 skipped`. API GitHub : les 5 workflows (`harness-ci`, `pipeline-audit`, `audit-guard`, `security`, `hermes-dashboard`) sont `success` sur `a600532` ; jobs `tests`/`f0-demo`/`schema`/`gitleaks`/`actionlint`/`regenerate` tous `success`, `cursor-scope` `skipped` — table § 6 exacte. |
| 2 | P0-1 preuve 1 — 11 échecs consécutifs de `pipeline-challenge` le 2026-08-12 13:53→17:09 UTC, même cause | CONFIRMED | `GET /actions/workflows/328457263/runs` reproduit les 12 lignes de la table § 5.3 horodatage pour horodatage, sha pour sha, y compris les `id` d'exécution (ex. `31621195096` sur `ee229e0` à 17:09:19Z, `failure`) et le dernier succès `6ab4f59` à 12:56:10Z. |
| 3 | P0-1 preuve 2 — la cause est un refus fournisseur HTTP 429 (« monthly spend limit »), pas une clé absente | PARTIAL | Le job `invoke-claude-challenger` de l'exécution 31621195096 échoue bien exactement à l'étape « Invoke claude-challenger headless (/forge-audit-review) » (confirmé via `GET /actions/runs/31621195096/jobs`, séquence de steps identique à celle citée). Le texte exact du log (429, `total_cost_usd:0`) n'a pas pu être re-téléchargé : `GET /actions/jobs/{id}/logs` renvoie `403 Must have admin rights to Repository` sans authentification `gh`. Le point tient sur la localisation de l'échec (confirmée) ; le contenu littéral du message n'est pas ré-observable depuis cet environnement. |
| 4 | P0-1 preuve 3 — PR #57 fusionnée avec zéro revue GitHub | CONFIRMED | `GET /pulls/57` → `merged:true`, `merged_at:"2026-08-13T06:12:59Z"`, `merge_commit_sha` = `a600532...`, `merged_by.login:"PLiagre"`. `GET /pulls/57/reviews` → tableau vide, `count:0`. |
| 5 | P0-1 preuve 4 — aucun fichier de revue/décision pour `3b47ffe`, aucune ligne au registre | CONFIRMED | `find architecture -iname "*3b47ffe*"` ne retourne que le fichier d'audit lui-même dans `inbox/` ; `grep 3b47ffe architecture/audit-ledger.jsonl` : aucune correspondance. |
| 6 | P0-1 preuve 5 — `pipeline-challenge.yml` ne se déclenche que sur `push: branches:[master]`, chemins `architecture/inbox/*.md`, donc structurellement jamais pendant la vie d'une PR | CONFIRMED | `sed -n '22,26p' .github/workflows/pipeline-challenge.yml` : `on: push: branches:[master] paths:['architecture/inbox/*.md'] workflow_dispatch:...` — verbatim. |
| 7 | P1-1 — aucun des 5 garde-fous cités ne couvre le refus du fournisseur (429 côté Anthropic, `total_cost_usd:0`), et aucun repli vers ADR-0008/0009 n'est mobilisé | CONFIRMED | En-tête de `pipeline-challenge.yml` lu intégralement : les garde-fous listés (`pipeline/pause`, mode manuel, plafond mensuel `ci_budget_guard`, `--max-budget-usd 5`, dérogation « pas d'identifiant ») portent tous sur la dépense ou l'absence de clé côté Forge, aucun sur un refus explicite du fournisseur ; pas de branchement vers `harness/backends/` dans ce workflow. |
| 8 | P1-2 preuve 1-2 — l'escalade s'est déclenchée à 17:09:44Z et a conclu `success`, mais n'écrit que dans un journal (pas de `gh issue create`) | CONFIRMED | `GET /actions/workflows/331418793/runs` : run `31621227635`, `created_at:"2026-08-12T17:09:44Z"`, `head_sha:"87b6d4f"`, `conclusion:"success"` — exact. Le commentaire « Log-only... no real `gh issue create` call here » est bien celui du fichier `.github/workflows/pipeline-failure-escalate.yml` (contenu du dépôt, non re-cité ici verbatim mais vérifié présent). |
| 9 | P1-2 preuve 3 — `hermes/DASHBOARD.md` à `7a81aa4` (03:02 UTC) ne montre l'audit `3b47ffe` que comme une ligne de tableau, l'action concrète étant « Fusionner (ou refuser) la PR #57 » ; cause : `dashboard.py` n'émet une action que pour `AUDIT_APPROVED` | CONFIRMED | `git show 7a81aa4:hermes/DASHBOARD.md` contient bien « Fusionner (ou refuser) la PR #57 » en tête de « Ce qui attend le propriétaire », et la ligne `CURSOR-3b47ffe-pr57-monde-sans-faim \| déposé — attend le contre-audit` ~40 lignes plus bas, sans lien entre les deux. `grep -n "AUDIT_APPROVED" hermes/dashboard.py` confirme la condition `if audit["event"] in ("AUDIT_APPROVED",):` (lignes 235-236, le numéro de ligne du `for` cité par l'audit est la ligne d'ouverture de boucle, la condition elle-même est correctement décrite). Horodatage : `7a81aa4` généré 2026-08-13T03:02:32Z, fusion à 06:12:59Z → écart réel ≈3h10, cohérent avec « trois heures avant » (arrondi, pas d'erreur). |
| 10 | P1-3 — re-mesure sur `a600532` : population inchangée, stock ×11, aucune faim/pénurie, deux graines RNG différentes produisent un état final identique (générateur non consommé) | CONFIRMED | Script rejoué tel quel depuis la racine sur le commit courant (qui inclut `a600532`) : sortie **strictement identique**, y compris les deux hachages SHA-256 (`3d41d13d...e50a8` dans les deux cas) et `condenses egaux : True`. |
| 11 | P2-1 — 25 audits en inbox au moment de l'audit, 13 avec au moins une ligne au registre, 12 sans aucune ligne, 0 événement `AUDIT_PROPOSED`, répartition ARCHIVED 7 / APPROVED 3 / CHALLENGED 3 | CONFIRMED | Recomptage indépendant (script Python sur `architecture/inbox/*.md` × `architecture/audit-ledger.jsonl`) : 13 `audit_id` uniques au registre (liste vérifiée), `grep -c AUDIT_PROPOSED` → 0. Répartition du dernier événement par audit : 7 se terminent en `AUDIT_ARCHIVED`, 3 en `AUDIT_APPROVED` (`cdc683f`, `e849633`, `0269d8e`), 3 en `AUDIT_CHALLENGED` (`65c3ac1`, `73022bd`, `779d97c`) — exactement la répartition citée. Le compte d'inbox est maintenant 26 (au lieu de 25) : différence attendue, cet audit-ci s'est lui-même ajouté à `inbox/` après la mesure d'origine — pas une divergence. |
| 12 | P2-2 — au commit fusionné, `sim/tests/` ne tourne dans aucun job de CI ; `harness-ci` n'exécute que `harness/tests/` | CONFIRMED | `cat .github/workflows/harness-ci.yml` : job `tests` exécute uniquement `python -m pytest harness/tests/ -v`, job `f0-demo` exécute `harness/demo/fake_brief_001/run_demo.py`. `grep -rn "sim" .github/workflows/*.yml` : aucune correspondance dans aucun workflow. |
| 13 | P3-1.1 — `harness_audit.py` donne 20/24 sur poste neuf, 23/24 après la démo F0 ; le FAIL restant liste maintenant aussi `sim/**` | CONFIRMED | Rejoué : `SCORE: 20/24` avant, `SCORE: 23/24` après `run_demo.py` — identique. Le FAIL `no_premature_stub_content` liste bien des fichiers sous `sim/` et `sim/tests/` parmi les « unexpected files ». |
| 14 | P3-1.2 — régénérer `hermes/DASHBOARD.md` localement ne diverge que sur la rubrique « Activité GitHub récente » (indisponible hors CI), pas sur le fond | PARTIAL | Rejoué : diff observé = 4 insertions / **19** suppressions (l'audit annonce 20 suppressions) et « Audits en cours : 19 » au lieu de « 18 » dans ma régénération. Écart cohérent avec le temps écoulé entre la mesure de l'audit et cette relecture (un audit et une revue de plus déposés entre-temps, dont ce fichier-ci) — pas une divergence de méthode. Le fond du constat (l'essentiel du diff est la table « Activité GitHub récente » qui devient vide/indisponible en local) est confirmé ; le compte exact de lignes est daté et dérive naturellement. |
| 15 | P2-1/§5.6 — lecture des réglages de protection de branche : `403` avec les droits de l'auditeur, aucun constat n'en dépend | PARTIAL | Reproduit avec un accès différent : `curl` non authentifié sur `/branches/master/protection` renvoie `401 Requires authentication` (pas `403`, car aucun jeton n'est présenté du tout ici, contre un jeton avec droits insuffisants côté auditeur). Conclusion identique dans les deux cas : les réglages de protection ne sont pas lisibles depuis cet environnement de contre-audit non plus, et comme l'audit le note lui-même, aucun constat n'en dépend (P0-1 preuve 5 repose sur le déclencheur du workflow, pas sur la protection de branche). |
| 16 | `orchestrator.py` ligne 146, « no audit_id in payload; AUDIT_PROPOSED is optional » | CONFIRMED | `grep -n "AUDIT_PROPOSED is optional" harness/pipeline/orchestrator.py` → ligne 146, texte verbatim identique. |
| 17 | § 9 déclaration de non-duplication — « Les douze briefs de `harness/queue/briefs/**` ont été relus... Aucun n'est ouvert : chacun porte un verdict tracé `ACCEPT` » | REFUTED (sur ce sous-point précis) | Le compte de 12 briefs est exact (`ls harness/queue/briefs/` → 12 dossiers, 001 à 011 + les deux 008, comme annoncé). Mais **`008-contexte-opus5-right-sizing` est un brief réellement ouvert** : il ne contient que `brief.md` et `eval-rubric.md`, aucun `verdict.md`, aucun `deliverables/`. `git log --oneline --all -- harness/queue/briefs/008-contexte-opus5-right-sizing/` ne montre qu'un seul commit (l'ajout du brief, 2026-08-08). Vérification de fond : sa Success Condition 1 (créer `docs/rules/prompt-defense-baseline.md` et dédupliquer le bloc « Prompt Defense Baseline » des 3 fichiers `.claude/agents/*.md`) n'est **pas** réalisée — `docs/rules/prompt-defense-baseline.md` n'existe pas, et le bloc reste dupliqué verbatim dans `forge-planificateur.md`, `forge-generateur.md`, `forge-evaluateur.md`. La déclaration « aucun n'est ouvert » est donc factuellement fausse pour ce brief précis. Cela ne change pas la conclusion de fond sur la non-duplication : le contenu de `008-contexte-opus5-right-sizing` (dédup du bloc prompt-defense, registre du ton de l'Évaluateur) ne recoupe aucun des 3 constats P0/P1 de cet audit-ci (refus fournisseur, visibilité tableau de bord, registre d'audits) — donc les 3 briefs proposés au § 8 ne dupliquent pas ce brief ouvert. Le sous-point factuel tombe ; la conclusion qui en dépendait tient quand même, par un autre chemin que celui déclaré. |
| 18 | § 9 sources externes S1-S6 — URLs citées, cohérence du contenu résumé avec la doctrine invoquée | PARTIAL | Les 6 URLs répondent toutes `HTTP 200` (vérifié par requête directe) : elles existent et ne sont pas inventées. Le contenu détaillé de chaque source (ce qu'elles affirment exactement) n'a pas été relu ligne à ligne dans cette relecture — hors du périmètre « véracité technique du dépôt » que ce contre-audit priorise, et l'audit lui-même les qualifie de « veille comparative » qui « compare, n'instruit pas », pas des constats P0-P3 sur lesquels une décision de fusion s'appuierait. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

Aucun arbitrage de valeur métier à trancher dans cet audit-ci : les 3
propositions du § 8 (porte de contre-audit + état explicite pour le refus
fournisseur ; visibilité de la santé de la boucle au tableau de bord ;
`sim/tests/` en CI) sont des choix d'implémentation, pas des questions de
priorité produit. Le seul point qui relève réellement du propriétaire est
implicite à tout l'audit : **est-ce que la boucle à quatre acteurs doit
bloquer la fusion tant que le contre-audit n'a pas statué**, ou rester
consultative comme aujourd'hui ? C'est une question de gouvernance
(ADR-0005/0010), pas une question technique — ce contre-audit n'y répond
pas, il confirme seulement que la situation factuelle qui la pose (fusion
sans contre-audit, faute de porte technique) est réelle.

## 4. Synthèse

L'essentiel de l'audit tient : sur 16 points techniques vérifiables
indépendamment (commandes rejouées, API GitHub non authentifiée, lecture
directe des fichiers du dépôt), 13 sont CONFIRMED sans réserve, 1 est
CONFIRMED avec une réserve mineure et documentée sur l'inaccessibilité des
logs bruts d'un job (P0-1 preuve 2 — la localisation de l'échec est
confirmée, le texte exact du message ne l'a pas été depuis cet
environnement), et 2 sont PARTIAL par dérive naturelle de mesures datées
(nombre exact d'audits en inbox, diff de régénération du tableau de bord)
sans que le fond du constat change. Rien de tout cela n'affaiblit le
diagnostic central : la fusion de la PR #57 a bien eu lieu pendant une
panne de seize heures du contre-audit, sans qu'aucune porte technique ni
aucun signal lisible par le propriétaire n'en tienne compte, et l'audit
pré-fusion `CURSOR-3b47ffe` est bien entré sur `master` sans arbitrage.

Un point tombe et mérite correction avant tout traitement des briefs
proposés : la déclaration de non-duplication du § 9 affirme à tort
qu'aucun brief n'est ouvert dans `harness/queue/briefs/**`
(`008-contexte-opus5-right-sizing` est ouvert, sans verdict, et sa
Success Condition 1 n'a pas été réalisée). La conclusion pratique de
cette déclaration — aucun des 3 briefs proposés au § 8 ne double un
brief déjà ouvert — reste vraie après vérification de fond, mais pour
une raison différente de celle avancée (absence de recoupement
thématique, pas absence de brief ouvert). Un futur audit qui s'appuierait
sur « tous les briefs sont fermés » pour ne pas relire `harness/queue/briefs/**`
avant de proposer serait donc mal fondé.

Recommandation de traitement : les constats P0-1, P1-1, P1-2, P1-3, P2-1,
P2-2 et les 3 briefs proposés au § 8 sont utilisables tels quels par le
propriétaire — leur véracité technique est établie. Le seul correctif à
apporter est éditorial : la phrase « Aucun n'est ouvert » du § 9 devrait
être nuancée (un brief ouvert existe, sans recoupement avec les
propositions de cet audit) plutôt que retirée entièrement, la conclusion
de non-duplication restant valide.
