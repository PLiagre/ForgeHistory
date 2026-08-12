---
audit_id: CURSOR-e849633-hermes-demande-pilotage
auditor: cursor-cloud
target_branch: master
target_commit: e8496336391ada87719ee0fa210de4d71a8f9487
created_at: 2026-08-12T12:28:07Z
audit_type: pull-request-review
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

**Objet audité** : la pull request **#32** — « hermes: demande — tableau de
bord unique et pilotage du projet depuis Hermes »
(<https://github.com/PLiagre/ForgeHistory/pull/32>). Un seul commit,
`e8496336391ada87719ee0fa210de4d71a8f9487`, fusionné sur `master` par le
propriétaire à 12:18:05 UTC (commit de fusion `e7c4895`).

**Ce que la PR livre** : un seul fichier neuf, documentaire —
`hermes/requests/DEMANDE-20260812-hermes-tableau-de-bord-pilotage.md`
(+142 / −0 lignes). Elle formalise une demande du propriétaire en cinq
phases (H1 → H5) et cinq arbitrages, avec le statut `OPEN`. Aucune ligne de
code, de CI, de brief ni de test.

**CI du commit audité** : **verte, aucun échec**. Sur les 18 vérifications
attachées à `e849633` : 13 en `success`, 4 en `skipped`, 1 en `cancelled`
(sortie complète § 6.2). Les `skipped` ne sont pas des pannes : ce sont
trois gardes qui, par construction, ne s'appliquaient pas à cette PR — et
c'est précisément là que se trouvent deux des constats de cet audit.

**Verdict d'ensemble** : le document est **honnête et majoritairement
exact**. Les sept chemins qu'il cite existent tous, l'exclusion de
`hermes/**` de l'allowlist du merge-bot est correctement rapportée,
l'expiration du secret Codex est sourcée dans le dépôt, et le manque qu'il
signale au tableau de bord est réel — au point que **cette demande `OPEN`
n'apparaît pas elle-même** dans la section « Ce qui attend le
propriétaire » (§ 6.5). Rien ici ne justifie un `P0` : le livrable est un
texte, il n'a pas de comportement à casser.

**Ce qui ne tient pas** : trois choses.

1. La **fenêtre de critique a duré 4 secondes** — la PR est passée de
   brouillon à fusionnée en 4 s, l'auditeur ayant été lancé 2 s avant la
   fusion. Le « maillon critique » d'ADR-0010 n'a pas pu s'exercer, et
   aucune porte mécanique ne l'exigeait (§ 4.1).
2. Le **diagnostic de la phase H2 est factuellement faux** : la demande
   affirme que l'API Cursor « n'est pas interrogée » en CI ; le workflow
   l'interroge, avec succès, et le message du tableau de bord dit la même
   phrase quand la réponse est vide *et* quand l'appel n'a pas lieu. Un
   brief écrit sur ce diagnostic recoderait un appel qui existe (§ 4.2).
3. La **traçabilité d'auteur d'`hermes/**` est nominale** : le fichier
   déclare `author: hermes`, le commit est signé par un agent Cursor, la
   PR explique noir sur blanc comment le nom de branche évite la garde de
   périmètre, et **aucune porte mécanique** ne vérifie ni le frontmatter
   ni l'auteur réel (§ 4.3).

# 2. Intention avant diff (lentille 1)

## 2.1 L'intention est lisible, et le diff y correspond

La description de la PR annonce « une seule nouveauté, documentaire » et un
fichier ; le diff contient exactement cela. La demande respecte le format
imposé par `hermes/README.md` § « Format imposé (frontmatter) » :
`author: hermes`, `kind: demande`, `created_at` ISO 8601, `concerns`,
`status: OPEN`. Le message de commit commence par `hermes:` comme exigé au
même endroit. Le nom de fichier suit le gabarit
`DEMANDE-AAAAMMJJ-<slug>.md`.

Sur le fond, la demande fait ce qu'un bon cadrage doit faire : elle
commence par un **état des lieux de ce qui existe déjà** (« à ne pas
reconstruire »), puis sépare ce qui ne demande aucune décision (H1,
configuration locale) de ce qui exige un ADR (H4, pilotage). C'est la
bonne direction : déplacer le jugement en amont, avant le code
[E1, E2].

## 2.2 Chronologie réelle de la PR

| horodatage (UTC) | événement | acteur |
|---|---|---|
| 12:07:05 | PR #32 ouverte **en brouillon** | PLiagre |
| 12:07:09 | `pipeline-audit` / `invoke-cursor-auditor` → **skipped** (brouillon) | CI |
| 12:18:01 | `ready_for_review` | PLiagre |
| 12:18:03 | `pipeline-audit` relancé → auditeur Cursor **lancé** | CI |
| 12:18:05 | **fusionnée**, 0 revue, `reviewDecision` vide | PLiagre |

Preuve : § 6.1 (API GitHub `issues/32/timeline`, `gh pr view`,
`gh run list`).

# 3. Lecture par les six lentilles

| # | lentille | lecture de cette PR |
|---|---|---|
| 1 | Intention avant diff | **Tenue.** Description et diff concordent ; l'état des lieux précède le plan (§ 2). |
| 2 | Preuve d'exécution | **Partielle.** La preuve citée (`309 passed, 16 skipped`) est exacte — rejouée § 6.3 — mais n'exerce aucune ligne du livrable (§ 4.6). Les affirmations factuelles du document, elles, ne sont adossées à rien : l'une est fausse (§ 4.2), une autre est invérifiable depuis le dépôt (§ 4.7). |
| 3 | Portes mécaniques d'abord | **Tenue côté machine, aveugle côté contenu.** Toutes les portes ont tourné et sont vertes, mais aucune ne valide un fichier `hermes/**` (§ 4.3, § 4.6). |
| 4 | Cadrage adverse | **Non tenue en pratique.** Le producteur et le critique sont bien deux acteurs distincts, mais le critique a été lancé 2 s avant la fusion (§ 4.1). |
| 5 | Taille et découpage | **Tenue en volume, pas en surface de décision.** 1 fichier, 142 lignes — très en dessous du seuil de ~400 lignes. Mais le fichier porte **cinq arbitrages indépendants** pour **un seul** champ `status` (§ 4.5). |
| 6 | Pièges du code généré par IA | **Un piège présent.** Diagnostic naïf/halluciné en H2 : une cause est affirmée sans être mesurée, et le message d'interface qui sert de preuve est ambigu (§ 4.2). Pas de sur-ingénierie, pas de dépendance inventée : les 7 chemins cités existent (§ 6.4). |

# 4. Constats par sévérité

Aucun `P0`. Le livrable est un document au statut `OPEN` : il n'a pas de
comportement, ne s'exécute pas, et le contrat `hermes/README.md` rappelle
qu'« aucun workflow n'exécute ce que Hermes écrit ». Rien ne devait bloquer
la fusion.

## 4.1 P1 — La fenêtre de critique a duré 4 secondes

**Constat.** ADR-0010 et le contrat `architecture/agents/cursor-auditor.md`
§ Déclencheur posent que Cursor est « le maillon **critique** de chaque
PR ». Pour la PR #32, la critique était mécaniquement impossible :

- de 12:07:05 à 12:18:01 la PR est en **brouillon**, et le job
  `invoke-cursor-auditor` est explicitement exclu des brouillons —
  `.github/workflows/pipeline-audit.yml` ligne 43 :
  `if: github.event_name != 'pull_request' || (github.event.pull_request.draft == false && ...)`.
  Le run de 12:07:09 est donc `skipped` (§ 6.2) ;
- à 12:18:01 le passage en `ready_for_review` déclenche enfin l'auditeur
  (12:18:03) ;
- **2 secondes plus tard**, à 12:18:05, la PR est fusionnée.

L'audit que vous lisez a donc été produit **après** la fusion. Il n'a rien
pu empêcher, et ce n'est pas un accident de calendrier : rien ne rend
l'arrivée de l'audit nécessaire avant la fusion. `docs/rules/full-auto-pipeline.md`
§ « Known gap » l'écrit déjà pour les PR de bots — la protection de branche
est **indisponible sur ce plan GitHub** (`403 Upgrade to GitHub Pro`), donc
aucune vérification ne peut être rendue obligatoire. Pour une PR humaine
sur `hermes/**`, il ne reste que la discipline du propriétaire : ici,
`reviews: 0` et `reviewDecision` vide (§ 6.1).

**Pourquoi c'est un P1 et pas un P3.** Le contenu fusionné est inoffensif,
mais l'invariant abîmé est structurel : la chaîne à quatre acteurs affiche
un contrôle qu'elle n'exerce pas. L'état de l'art de 2026 est net sur ce
point : la frontière que l'on garde humaine et *outillée*, c'est la
**décision de fusion**, et l'autorité de fusion doit être hiérarchisée par
niveau de risque, pas laissée à la vitesse du « temps jusqu'au vert »
[E1, E2, E6].

**Élément nouveau par rapport à l'existant.** L'audit
`CURSOR-73022bd-hermes-dashboard-modele-auditeur` (état `AUDIT_CHALLENGED`
au ledger) a déjà soulevé l'**exemption** de `hermes/**` par le filtre
anti-boucle des poussées. Je ne re-formule pas ce point — il est en cours
d'arbitrage. Le constat ici est différent et mesuré : sur le déclencheur
`pull_request`, ce n'est pas une exemption de chemin qui a joué, c'est
l'**ordre des événements** (brouillon → prêt → fusion en 4 s).

**Ce qui pourrait être envisagé** (proposition, pas instruction) : puisque
la fusion ne peut pas être bloquée sur ce plan, rendre le trou **visible**
— consigner au tableau de bord et au ledger « PR #N fusionnée avant
l'arrivée de son audit », de la même manière que les dérogations
`::warning::` sont déjà consignées ailleurs. Un contrôle qu'on ne peut pas
imposer doit au moins être compté.

## 4.2 P1 — Le diagnostic de la phase H2 est faux, et la preuve qu'il invoque est ambiguë

**Constat.** La demande écrit (lignes 80-83 du fichier livré) :

> « la section "Agents lancés récemment (Cursor Cloud)" est aujourd'hui
> "non disponible" **même en CI (l'API n'est pas interrogée)** —
> l'interroger »

Les trois affirmations de cette parenthèse sont contredites par le dépôt et
par les journaux :

1. **Le workflow interroge bien l'API.**
   `.github/workflows/hermes-dashboard.yml` contient une étape dédiée,
   « Collect recent Cursor Cloud agents (optional) », qui appelle
   `https://api.cursor.com/v1/agents?limit=10` en Basic auth, puis passe
   `--agents-json` au script si le fichier existe.
2. **L'appel a réussi lors de la dernière génération.** Le run
   `31595782109` (12:18, celui qui a produit le `hermes/DASHBOARD.md`
   actuel) montre `CURSOR_API_KEY: ***` — donc un secret non vide — et
   **aucune** des deux sorties d'échec prévues : ni
   « Pas de CURSOR_API_KEY », ni le `::warning::liste des agents Cursor
   indisponible ». `curl --fail-with-body` étant silencieux en cas de
   succès, l'appel a renvoyé un code 2xx (§ 6.6).
3. **Le message affiché ne prouve pas ce que la demande lui fait dire.**
   `hermes/dashboard.py` ligne 274 imprime la phrase
   « Non disponible dans cette génération (API Cursor non interrogée) »
   dans le `else` d'un test qui est faux dans **trois** situations
   distinctes : script lancé sans `--agents-json`, JSON illisible, et
   **réponse valide contenant une liste vide**. J'ai rejoué les trois cas
   (§ 6.6) : une réponse `{"agents": []}` — API bel et bien interrogée —
   affiche **mot pour mot** la même phrase que l'absence d'appel.

**Contradiction interne.** La phase H1 du même document affirme, pour la
même API, « clé **déjà provisionnée** côté CI ». H1 et H2 ne peuvent pas
être vrais ensemble : si la clé est provisionnée et que le workflow appelle
l'API, alors « l'API n'est pas interrogée » est faux.

**Pourquoi ça compte.** La demande sert d'entrée à un brief (« brief à
écrire par le CTO »). Écrit sur ce diagnostic, le brief demanderait
d'« interroger l'API » — un travail déjà fait — et laisserait intact le
défaut réel : le tableau de bord **affirme une cause qu'il n'a pas
mesurée**, ce qui viole sa propre règle « une donnée absente est dite
absente » (`hermes/README.md`). C'est le piège classique du code assisté
par IA : un succès (ou ici une cause) affirmé sans mesure, appuyé sur une
sortie d'interface prise pour une preuve [E3, E6, E5].

**Preuve.** § 6.6 (trois cas rejoués), § 6.7 (journal du run
`31595782109`), `hermes/dashboard.py` lignes 257-275,
`.github/workflows/hermes-dashboard.yml` étape « Collect recent Cursor
Cloud agents (optional) ».

## 4.3 P1 — La propriété d'auteur d'`hermes/**` n'est vérifiée par personne

**Constat.** Trois faits mis côte à côte :

1. Le fichier livré déclare `author: hermes`. Or le commit `e849633` a pour
   auteurs `cursoragent@cursor.com` (**Cursor Agent**) et PLiagre en
   co-auteur (§ 6.1). L'auteur déclaré et l'auteur réel diffèrent.
   `hermes/README.md` promet pourtant : « L'auteur est toujours traçable :
   `author: hermes` dans le frontmatter **et** un message de commit qui
   commence par `hermes:` ». Ces deux critères sont **déclaratifs** :
   n'importe quel acteur peut les satisfaire en écrivant deux chaînes de
   caractères. Ils ne tracent rien.
2. **Aucune porte mécanique ne valide un fichier `hermes/**`.**
   `harness/audit_schema.py` ne regarde que `architecture/inbox`
   (`INBOX = REPO_ROOT / "architecture" / "inbox"`, ligne 26) ; une
   recherche sur tout le dépôt ne trouve aucun validateur du frontmatter
   Hermes (§ 6.4). Le format dit « imposé » n'est imposé que par la
   relecture.
3. La description de la PR **documente le contournement** de la seule garde
   de périmètre existante : « Branche `forge/*` et non `cursor/*` :
   `audit-guard` (job `cursor-scope`) échoue mécaniquement toute PR
   `cursor/*` touchant autre chose que `architecture/inbox/` ». C'est exact
   — et c'est le problème : `.github/workflows/audit-guard.yml` ligne 30
   conditionne cette garde à
   `startsWith(github.head_ref, 'cursor/')`. Elle est donc pilotée par une
   **chaîne que l'agent producteur choisit lui-même**. Sur la PR #32, le
   job `cursor-scope` est `skipped` deux fois (§ 6.2).

**Ce qui a quand même fonctionné.** Le contrôle compensatoire prévu a joué :
`hermes/**` est bien hors de l'allowlist du merge-bot
(`.github/workflows/merge-bot.yml` ligne 50 : seuls
`architecture/inbox/`, `architecture/reviews/` et
`harness/queue/briefs/.*/feedback/`), donc `check-and-automerge` est
`skipped` et la fusion a été un clic humain. C'est pourquoi ce constat est
`P1` et non `P0` : la frontière n'est pas prouvable, mais elle n'a pas été
franchie sans humain.

**État de l'art.** Les architectures d'orchestration d'agents de 2026
traitent exactement ce cas : l'application se fait sur l'**identité** de
l'acteur (identité de charge de travail, jeton éphémère, entrée de
registre) et non sur une convention de nommage ; la garde de branche
existe précisément « pour empêcher les agents d'improviser un nom de
branche » quand une poussée est refusée [E4, E7, E8]. Une règle qui ne vaut
que si l'agent veut bien s'appeler `cursor/*` n'est pas une règle : c'est
une politesse.

## 4.4 P2 — H4 confierait à un agent le dernier contrôle humain, sans exclure ses propres PR

**Constat.** La phase H4 propose qu'Hermes puisse « **fusionner ou refuser
une PR** » (action n°1 de son périmètre fermé), en plus de poser
`pipeline/pause` et de déclencher `pipeline-forge-run`. La liste de
garde-fous qui suit (confirmation explicite, jeton minimal, journalisation,
`127.0.0.1`) est sérieuse mais il y manque une exclusion : **rien n'y
interdit à Hermes de fusionner une PR qu'Hermes a écrite**.

Or c'est précisément ce que le contrat actuel empêche par construction :
`hermes/README.md` conclut que « ces chemins ne figurent pas dans
l'allowlist du merge-bot : une PR Hermes est **toujours relue par le
propriétaire** (ou son délégué) avant fusion », et
`docs/rules/full-auto-pipeline.md` § « Known gap » rappelle que, la
protection de branche étant indisponible, la vérification de chemins du
merge-bot est « the **only** thing standing between a bot PR and a merge ».
La PR #32 est l'illustration : elle a été fusionnée à la main *parce que*
son chemin n'est pas auto-fusionnable. Donner la fusion à Hermes retire ce
dernier cran, dans le cas même où il compte le plus.

**Précision, pas objection.** H4 est explicitement conditionnée à un
ADR-0011 et la demande dit « ne pas câbler avant » — la trajectoire est
correcte. Le constat porte sur le **contenu** du périmètre proposé, pas sur
sa procédure : si l'ADR est écrit, l'exclusion des PR dont Hermes est
l'auteur et une hiérarchisation par risque (documentaire / logique /
sécurité-CI) mériteraient d'être des garde-fous de premier rang, au même
titre que la confirmation explicite [E1, E2, E7].

**Preuve.** Fichier livré lignes 95-112 ; `hermes/README.md` dernier
paragraphe ; `docs/rules/full-auto-pipeline.md` lignes 150-172 ;
`.github/workflows/merge-bot.yml` ligne 50 ; § 6.2 (`check-and-automerge`
skipped).

## 4.5 P2 — Cinq arbitrages indépendants pour un seul champ `status`

**Constat.** La demande porte cinq décisions de natures très différentes :
H1 est une configuration locale hors dépôt, H2 et H3 sont des briefs à
écrire, H4 exige un ADR, H5 est un choix de fournisseur de modèle. Le cycle
de vie décrit par `hermes/README.md` n'offre qu'**un seul** champ `status`
par fichier (`OPEN` → `HANDED_TO_CTO` → `REFLECTED_IN_ROADMAP` → `CLOSED`).

Conséquence concrète : si le propriétaire valide H1, autorise H2/H3 et
diffère H4, aucun état du fichier ne le dit. Le fichier restera `OPEN` en
donnant l'impression que rien n'est tranché, ou passera
`REFLECTED_IN_ROADMAP` en donnant l'impression que tout l'est. C'est la
discipline `NEEDS_SPLIT` que le harnais applique déjà aux briefs
(lentille 5 de `architecture/review-guidelines.md`), transposée à une
demande : le volume est petit (142 lignes, très en dessous du seuil de
relecture honnête), mais la **surface de décision** est celle de cinq
demandes.

**Preuve.** Fichier livré lignes 130-140 (les cinq arbitrages) ;
`hermes/README.md` § « Format imposé » et § « Cycle d'une demande
d'évolution ».

## 4.6 P3 — La preuve d'exécution citée est exacte, mais orthogonale au livrable

**Constat.** La PR cite comme validation :
`.venv/bin/python -m pytest harness/tests/ -q` → « 309 passed, 16 skipped ».
Rejoué : **chiffre identique**, au test près (§ 6.3). Rien à reprocher à
l'honnêteté du chiffre.

Mais cette suite n'exerce **aucune** ligne du fichier livré : il n'existe
aucun test qui lise `hermes/requests/**` (§ 6.4), et le seul fichier de
test citant Hermes est `test_hermes_dashboard.py`, qui couvre le générateur
du tableau, pas les demandes. La même suite serait verte avec un fichier
vide, mal formaté, ou contenant des affirmations fausses — c'est d'ailleurs
ce qui s'est produit pour § 4.2.

Ce n'est pas un défaut de la PR : on ne teste pas un texte. C'est une
raison de **ne pas lire « 309 passed » comme une preuve portant sur ce
contenu**. La forme forte de preuve, pour un document factuel, c'est la
citation vérifiable de chaque affirmation — ce que la demande fait très
bien pour ses chemins, et pas du tout pour sa phrase la plus décisive
[E3, E1].

## 4.7 P3 — Deux affirmations non vérifiables depuis le dépôt

1. « Hermes local […] est en phase "shadow" jusqu'au **2026-08-24** selon
   sa propre configuration » (lignes 39-40) et l'arbitrage n°5 qui en
   dépend. Aucune trace de cette date ni de cette phase dans le dépôt : la
   configuration vit sur la machine du propriétaire
   (`C:\Users\liagr\Documents\ChatGPT\hermes\scripts\runner-event.ps1`,
   `.github/workflows/hermes-observer.yml`). Le propriétaire ne peut pas
   contrôler cette échéance en lisant le dépôt (§ 6.4).
2. « `hermes-observer.yml` transmet **chaque** PR et **chaque** fin de
   workflow ». Presque : la liste `workflows:` de ce fichier énumère neuf
   workflows et **n'inclut pas `hermes-dashboard`** (ni `hermes-observer`
   lui-même) ; côté PR, cinq types d'événements sont écoutés, sans
   `edited` ni les revues. « Chaque » est donc une généralisation, pas une
   description.

À l'inverse, l'affirmation sur l'expiration du secret Codex
(« ~8 jours sans rafraîchissement ») est **correctement sourcée** dans le
dépôt : `HANDOFF.md` et `docs/rules/full-auto-pipeline.md` disent la même
chose.

## 4.8 P3 — H3 demande au tableau une donnée que le tableau ne peut pas mesurer

**Constat.** H3 veut que la liste d'attentes inclue « les secrets absents
ou **périmés** ». « Absent » est mesurable ; « périmé » ne l'est pas :
l'API REST de GitHub, pour un dépôt, ne renvoie d'un secret que son `name`,
`created_at` et `updated_at` — jamais sa valeur ni sa validité [E9] — et
le bloc `permissions:` d'un workflow n'a pas de portée « secrets » du tout
(`hermes-dashboard.yml` : `contents: write`, `actions: read`,
`pull-requests: read`). Le seul indicateur honnête est donc
« secret présent / absent » et « inchangé depuis N jours ».

Formulé tel quel, H3 invite le brief à produire un indicateur inventé — ce
que la règle « une donnée absente est dite absente » interdit. Reformulé en
termes mesurables, le besoin reste entièrement satisfait (un secret Codex
inchangé depuis plus de 8 jours **est** l'alerte voulue).

**En revanche, le manque signalé par H3 est bien réel** — et la
démonstration est cette PR : le tableau de bord régénéré à 12:18 UTC, après
la fusion, liste les PR #33 et #31 et un audit à convertir, mais **pas la
demande `OPEN` qui vient d'être fusionnée** (§ 6.5). La demande qui veut
faire d'Hermes « le seul tableau à suivre » est invisible sur ce tableau.

## 4.9 Récapitulatif

| § | sévérité | constat | preuve principale |
|---|---|---|---|
| 4.1 | **P1** | Fenêtre de critique de 4 s ; l'audit arrive après la fusion, rien ne l'exige avant | chronologie § 6.1 ; `pipeline-audit.yml` l. 43 |
| 4.2 | **P1** | Diagnostic H2 faux (l'API *est* interrogée) ; le message du tableau confond 3 états | § 6.6, § 6.7 ; `dashboard.py` l. 274 |
| 4.3 | **P1** | Auteur d'`hermes/**` non vérifiable ; garde de périmètre pilotée par le nom de branche | § 6.1, § 6.2, § 6.4 ; `audit-guard.yml` l. 30 |
| 4.4 | **P2** | H4 donnerait la fusion à Hermes sans exclure ses propres PR | fichier l. 95-112 ; `hermes/README.md` |
| 4.5 | **P2** | 5 arbitrages indépendants, 1 seul champ `status` | fichier l. 130-140 ; `hermes/README.md` |
| 4.6 | **P3** | « 309 passed » exact mais n'exerce pas le livrable | § 6.3, § 6.4 |
| 4.7 | **P3** | Phase « shadow » et « chaque workflow » non vérifiables / imprécis | § 6.4 ; `hermes-observer.yml` |
| 4.8 | **P3** | « secrets périmés » non mesurable ; manque H3 néanmoins réel | [E9] ; § 6.5 |

Jobs CI concernés par ces constats : `pipeline-audit / invoke-cursor-auditor`
(4.1), `hermes-dashboard / regenerate` (4.2, 4.8), `audit-guard /
cursor-scope` et `merge-bot / check-and-automerge` (4.3, 4.4),
`harness-ci / tests` (4.6).

# 5. Sources externes

| # | source | date | consulté le |
|---|---|---|---|
| E1 | DEV Community — *Evidence Gates for AI Coding Agents in CI — Recoverable Merge over Mean Time to Green* — <https://dev.to/lo_an_e746e473b842ff53cf9/evidence-gates-for-ai-coding-agents-in-ci-recoverable-merge-over-mean-time-to-green-2a8h> | 2026 | 2026-08-12 |
| E2 | Augment Code — *From Assisted to Autonomous: How Far Can the Engineering Loop Close?* (« As of July 2026 … Merge decision: explicit approval gate ») — <https://www.augmentcode.com/guides/autonomous-engineering-loop> | juillet 2026 | 2026-08-12 |
| E3 | CurrentStack — *AI Coding at Scale Needs Verification Pipelines, Not Just Faster Generation* — <https://currentstack.io/stories/ai-coding-verification-pipeline-design-2026/> | 2026 | 2026-08-12 |
| E4 | `jwbron/egg` — *SDLC pipeline guide* (« Push-target enforcement … preventing agents from improvising branch names on push failure ») — <https://github.com/jwbron/egg/blob/main/docs/guides/sdlc-pipeline.md> | 2026 | 2026-08-12 |
| E5 | Zylos Research — *Token Budget Management and Cost Control for Autonomous AI Agents* — <https://zylos.ai/research/2026-06-30-token-budget-management-cost-control-autonomous-agents/> | 2026-06-30 | 2026-08-12 |
| E6 | ProgressiveRobot — *AI Cost Governance: Proven Controls to Stop Costly Waste* — <https://www.progressiverobot.com/2026/08/09/ai-cost-governance/> | 2026-08-09 | 2026-08-12 |
| E7 | Harness — *Least-Privilege AI Agents: Identity & Permissions* (« an agent that shouldn't merge to main, even if its prompt asks it to — the architecture enforces it ») — <https://www.harness.io/blog/identity-and-permissions-for-ai-worker-agents-in-harness> | 2026 | 2026-08-12 |
| E8 | `shailvshah/openagent-control` — identité de charge de travail + politique OPA devant chaque appel d'outil — <https://github.com/shailvshah/openagent-control> | 2026 | 2026-08-12 |
| E9 | GitHub REST — *Actions secrets* (la liste renvoie `name`, `created_at`, `updated_at` ; jamais la valeur) — <https://docs.github.com/en/rest/actions/secrets> | — | 2026-08-12 |

## 5.1 Ce que ces sources changent à la lecture de cette PR

- **Autorité de fusion.** E1 et E2 convergent : en 2026, la frontière tenue
  par l'humain dans les boucles agentiques, c'est la fusion, et elle doit
  être **hiérarchisée par risque** (documentaire = fusion souple, sécurité
  et CI = second relecteur). ForgeHistory a la bonne intention (ADR-0010)
  mais pas l'outillage : § 4.1 mesure 4 secondes de fenêtre, et § 4.4
  propose d'y retirer le dernier cran humain.
- **Identité plutôt que convention.** E4, E7 et E8 décrivent tous la même
  règle : on applique la politique sur l'identité de l'acteur et sur le
  chemin, jamais sur un nom que l'acteur choisit. C'est exactement l'écart
  de § 4.3, et E4 nomme le scénario (« improviser un nom de branche »).
- **Preuve, pas affirmation.** E3 insiste sur la « porte d'intention » :
  vérifier que le produit satisfait l'intention *et les contraintes*, pas
  qu'un pipeline est vert. § 4.2 et § 4.6 sont deux faces de cela : une
  suite verte sans rapport avec le livrable, et une affirmation décisive
  jamais mesurée.
- **Budget de jetons.** E5 et E6 (le plus récent, 2026-08-09) recommandent
  un plafond **par exécution** en plus du plafond mensuel, et un
  « détecteur d'action répétée ». Ce n'est pas un constat sur cette PR — le
  plafond mensuel `ci_budget_guard.py` existe et le sujet est déjà porté
  par un audit antérieur — mais cela conforte l'idée que chaque invocation
  d'agent, y compris l'audit d'une PR documentaire de 142 lignes, doit
  être comptée quelque part.

## 5.2 Non-doublon avec les audits en cours

Vérifié au ledger (§ 6.8) : `CURSOR-cdc683f` (`AUDIT_APPROVED`) porte sur
le workflow à quatre acteurs et le cumul des rôles d'Hermes ;
`CURSOR-73022bd` et `CURSOR-65c3ac1` (`AUDIT_CHALLENGED` / déposé) portent
sur le tableau de bord, le filtre anti-boucle et le modèle de l'auditeur.
Les constats 4.1, 4.2, 4.3, 4.5 et 4.8 de cet audit sont neufs (fenêtre de
critique mesurée sur un déclencheur `pull_request`, diagnostic H2,
vérifiabilité de l'auteur, granularité du `status`, mesurabilité des
secrets). Le constat 4.4 recoupe le thème « Hermes cumule des rôles » déjà
`PARTIAL` dans `CLAUDE-CURSOR-cdc683f` ; je ne le rejoue pas, je le limite
à l'élément nouveau : le périmètre H4 tel qu'écrit dans **cette** PR
n'exclut pas les PR d'Hermes.

# 6. Commandes rejouées

## 6.1 Chronologie et paternité de la PR

```
$ gh pr view 32 --json createdAt,mergedAt,mergedBy,reviews,reviewDecision
{"created":"2026-08-12T12:07:05Z","decision":"","merged":"2026-08-12T12:18:05Z","mergedBy":"PLiagre","reviews":0}

$ gh api repos/PLiagre/ForgeHistory/issues/32/timeline --paginate \
    -q '.[] | select(.event != null) | [.created_at, .event, (.actor.login // "-")] | @tsv'
        committed       -
2026-08-12T12:18:01Z    ready_for_review        PLiagre
2026-08-12T12:18:05Z    merged                  PLiagre
2026-08-12T12:18:05Z    closed                  PLiagre

$ gh run list --workflow=pipeline-audit.yml --limit 6 \
    -q '.[]|[.createdAt,.event,.headBranch,.conclusion]|@tsv'
2026-08-12T12:18:08Z    push            master                            success
2026-08-12T12:18:03Z    pull_request    forge/hermes-tableau-pilotage-c2dd success
2026-08-12T12:14:44Z    pull_request    cursor/audit-pr-30-122d           skipped
2026-08-12T12:07:09Z    pull_request    forge/hermes-tableau-pilotage-c2dd skipped

# écarts
ready_for_review -> lancement auditeur : 2 s
lancement auditeur -> fusion          : 2 s
ready_for_review -> fusion            : 4 s

# auteurs du commit audité
$ gh pr view 32 --json commits    (extrait)
authors: cursoragent@cursor.com (Cursor Agent) + PLiagre@users.noreply.github.com
messageHeadline: "hermes: demande — tableau de bord unique et pilotage du projet depuis…"
```

## 6.2 CI du commit audité — classification

```
$ gh api repos/PLiagre/ForgeHistory/commits/e8496336391ada87719ee0fa210de4d71a8f9487/check-runs \
    -q '.check_runs[] | [.name, .conclusion] | @tsv' | sort | uniq -c | sort -rn
      2 tests                          success
      2 schema                         success
      2 Reconcile local Hermes state   success
      2 gitleaks                       success
      2 f0-demo                        success
      2 cursor-scope                   skipped
      2 actionlint                     success
      1 Reconcile local Hermes state   cancelled
      1 invoke-cursor-auditor          success
      1 invoke-cursor-auditor          skipped
      1 check-and-automerge            skipped
```

Lecture : **aucun échec**. Les `skipped` sont les trois gardes qui ne
s'appliquaient pas — `cursor-scope` (branche non `cursor/*`, § 4.3),
`invoke-cursor-auditor` (PR en brouillon, § 4.1), `check-and-automerge`
(`hermes/**` hors allowlist, § 4.4). Le `cancelled` est un job
`hermes-observer` interrompu par sa propre `concurrency:
cancel-in-progress: true`, remplacé 8 s plus tard par un `success`.

## 6.3 Suite de tests du harnais (affirmation de la PR)

```
$ .venv/bin/python -m pytest harness/tests/ -q
........................................................................ [ 44%]
........................................................................ [ 66%]
......................ssssssssssssssss.................................. [ 88%]
.....................................                                    [100%]
309 passed, 16 skipped in 16.83s
```

Chiffre annoncé par la PR : identique. Les 16 `s` sont groupés
(`test_run_unity.py`, Unity/PowerShell absents sur Linux).

## 6.4 Existence des chemins cités, et absence de validateur Hermes

```
$ for p in <les 7 chemins cités par la demande> ; do ... ; done
PRESENT  hermes/dashboard.py
PRESENT  hermes/DASHBOARD.md
PRESENT  .github/workflows/hermes-dashboard.yml
PRESENT  .github/workflows/hermes-observer.yml
PRESENT  architecture/audit-ledger.jsonl
PRESENT  harness/pipeline/ci-budget-ledger.jsonl
PRESENT  harness/queue/cost-ledger.jsonl
PRESENT  .github/workflows/pipeline-failure-escalate.yml
PRESENT  .github/workflows/pipeline-forge-run.yml

$ rg -n "author: hermes|hermes/requests|hermes/reports" --glob '*.py'
hermes/dashboard.py:325:    out.append("(`hermes/requests/`) → le propriétaire tranche → ...")
(aucun validateur)

$ rg -ln "hermes" harness/tests/
harness/tests/test_hermes_dashboard.py

$ rg -n "2026-08-24" --glob '!hermes/requests/**'
(aucun résultat)
```

Aucune dépendance inventée : tous les chemins cités existent. Aucun test ni
script ne valide le frontmatter d'un fichier `hermes/**`. La date de sortie
de phase « shadow » n'existe pas dans le dépôt.

## 6.5 Le tableau de bord ne connaît pas cette demande

```
$ git show origin/master:hermes/DASHBOARD.md | sed -n '16,20p'
## Ce qui attend le propriétaire

- Fusionner (ou refuser) la PR #33 — « cursor-auditor: audit de la PR #30 … »
- Fusionner (ou refuser) la PR #31 — « challenge: revue de l'audit CURSOR-65c3ac1… »
- Convertir l'audit retenu `CURSOR-cdc683f-hermes-workflow-quatre-acteurs` en brief
```

Ce tableau a été généré à **12:18 UTC**, après la fusion de 12:18:05. La
demande `OPEN` livrée par la PR #32 n'y figure pas — le manque décrit par
H3 est donc réel et immédiatement démontré.

## 6.6 Falsification du diagnostic H2 — trois cas rejoués

```
$ python3 /workspace/hermes/dashboard.py --repo-root /workspace \
    --output /tmp/dashprobe/A.md --agents-json agents_full.json    # {"agents":[{...}]}
## Agents lancés récemment (Cursor Cloud)
| agent      | statut  | lancé par | branche  |
|---|---|---|---|
| agent-demo | RUNNING | api       | cursor/x |

$ ... --agents-json agents_empty.json                              # {"agents":[]}  (API interrogée, 200 OK)
## Agents lancés récemment (Cursor Cloud)
Non disponible dans cette génération (API Cursor non interrogée).

$ ... (aucun --agents-json)                                        # API jamais appelée
## Agents lancés récemment (Cursor Cloud)
Non disponible dans cette génération (API Cursor non interrogée).
```

Cas B et cas C produisent la **même** phrase. Le message ne distingue pas
« interrogée mais vide » de « non interrogée » : il ne peut donc pas servir
de preuve que l'API n'est pas interrogée. Le parseur, lui, fonctionne
(cas A).

*(Exécuté avec `--output /tmp/...` : cet audit est en lecture seule.
`git status --porcelain` est resté vide.)*

## 6.7 Le workflow interroge bien l'API — journal du run `31595782109`

```
$ gh run view 31595782109 --log | rg -i "CURSOR_API_KEY|Pas de|agents Cursor"
regenerate  Collect recent Cursor Cloud agents (optional)  … if [ -z "${CURSOR_API_KEY:-}" ]; then
regenerate  Collect recent Cursor Cloud agents (optional)  …   echo "Pas de CURSOR_API_KEY -- section agents marquée non disponible."
regenerate  Collect recent Cursor Cloud agents (optional)  … auth_header="Authorization: Basic $(printf '%s:' "$CURSOR_API_KEY" | base64 -w0)"
regenerate  Collect recent Cursor Cloud agents (optional)  … || { echo "::warning::liste des agents Cursor indisponible …"; }
regenerate  Collect recent Cursor Cloud agents (optional)  …   CURSOR_API_KEY: ***
```

Les quatre premières lignes sont l'**écho du script** (bloc `##[group]`), pas
sa sortie. La cinquième est le dump d'environnement : `***` signifie un
secret **non vide**. Aucune ligne de sortie réelle n'apparaît : ni la
branche « Pas de CURSOR_API_KEY », ni le `::warning::` d'échec. `curl -sS
--fail-with-body` étant muet en succès, l'appel a abouti — et le fichier
`agents.json` a donc été passé au script.

## 6.8 Non-doublon : derniers événements du ledger d'audits

```
$ tail -3 architecture/audit-ledger.jsonl
… "audit_id": "CURSOR-cdc683f-hermes-workflow-quatre-acteurs", "event": "AUDIT_CHALLENGED" …
… "audit_id": "CURSOR-cdc683f-hermes-workflow-quatre-acteurs", "event": "AUDIT_APPROVED",
  "retained_points": [1, 2, 5, 8, 10, 11] …
… "audit_id": "CURSOR-73022bd-hermes-dashboard-modele-auditeur", "event": "AUDIT_CHALLENGED",
  "verdicts": {"CONFIRMED": 12, "REFUTED": 2, "PARTIAL": 4, "NEEDS_OWNER": 4} …
```

# 7. Briefs proposés (au plus 3)

Ces trois propositions sont des **entrées**, pas des instructions. Aucune
n'est autorisée par cet audit (les trois flags du frontmatter sont `false`) ;
seul le propriétaire, puis un brief écrit par le CTO, peuvent leur donner
force exécutoire.

## Brief proposé 1 — Dire la vérité sur la section « agents Cursor » (remplace le diagnostic H2)

*Motivé par § 4.2 et § 4.8.* Le travail utile n'est pas « interroger
l'API » (déjà fait) mais **distinguer les états et journaliser la cause** :
trois messages distincts dans `hermes/dashboard.py` (clé absente / appel
échoué / réponse valide mais vide), l'étape du workflow écrivant la cause
dans un fichier que le script lit, et un test par état. Petit, mécanique,
et il rend au tableau sa règle « une donnée absente est dite absente ».
Corollaire naturel du même lot, en version mesurable : « secret présent /
absent » et « inchangé depuis N jours », jamais « périmé ».

## Brief proposé 2 — Rendre l'appartenance d'auteur vérifiable, pas déclarative

*Motivé par § 4.3.* Deux volets, tous deux mécaniques : (a) un validateur
de frontmatter pour `hermes/**` sur le modèle de `harness/audit_schema.py`
(champs requis, `kind` dans l'énumération, `status` dans l'énumération, nom
de fichier conforme), appelé par la CI ; (b) faire dépendre la garde de
périmètre de l'**identité** de l'auteur du commit ou de la PR, et non du
préfixe de branche — aujourd'hui `startsWith(github.head_ref, 'cursor/')`
suffit à s'y soustraire, comme la description de la PR #32 le documente
elle-même. Volet CI : à écrire par le CTO, jamais par l'auditeur.

## Brief proposé 3 — Compter les fusions non auditées

*Motivé par § 4.1.* La protection de branche étant indisponible sur ce plan
GitHub, l'audit ne peut pas être rendu bloquant. Il peut être rendu
**visible** : une ligne au ledger et une ligne au tableau de bord quand une
PR est fusionnée avant l'arrivée de son audit (comparaison
`mergedAt` / date de dépôt de l'audit correspondant), sur le modèle des
dérogations `::warning::` déjà consignées. Un contrôle qu'on ne peut pas
imposer doit au moins être compté — sinon le dépôt affiche un maillon
critique qui ne critique rien.

# 8. Conclusion

La PR #32 est un bon document : intention lisible, format respecté, état
des lieux honnête avant le plan, et la quasi-totalité de ses affirmations
vérifiables se vérifient. Elle ne devait rien bloquer et n'a rien cassé.

Ce que sa relecture met au jour est ailleurs, et concerne la boucle
plutôt que le texte : la critique indépendante a disposé de **4 secondes**
avant la fusion (§ 4.1), la seule garde de périmètre séparant « Cursor
audite » de « Cursor développe » dépend d'un **nom de branche que l'agent
choisit** (§ 4.3), et aucune porte mécanique ne lit un fichier `hermes/**`
— si bien que la phrase la plus décisive du document, celle qui
commanderait un brief, est **fausse** sans que rien ne l'ait signalé
(§ 4.2).

Aucun de ces points n'est une décision : ils sont proposés à
`architecture/reviews/` puis au propriétaire, conformément à
`architecture/README.md` et à ADR-0005/0006.
