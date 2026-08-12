---
audit_id: CURSOR-73022bd-hermes-dashboard-modele-auditeur
auditor: cursor-cloud
target_branch: forge/hermes-dashboard-modele-auditeur-977d
target_commit: 73022bdab6d2fff7c4d08812c281bcc56172dcc8
created_at: 2026-08-12T10:30:00Z
audit_type: pull-request-critique
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

**Objet audité** : la pull request [#27](https://github.com/PLiagre/ForgeHistory/pull/27)
« Hermes : tableau de bord lisible + auditeur Cursor en Opus 5 + anti-boucle
d'audits », branche `forge/hermes-dashboard-modele-auditeur-977d`, tête
`73022bdab6d2fff7c4d08812c281bcc56172dcc8`, base `master` (`beb57b5`).

**Volumétrie** : +801 / −13 lignes sur 8 fichiers, 2 commits (`04b98b5`,
`73022bd`).

**Nature** : trois sujets distincts dans une seule PR — (a) un tableau de bord
généré pour le propriétaire (`hermes/dashboard.py`, `hermes/DASHBOARD.md`,
`.github/workflows/hermes-dashboard.yml`, 4 tests), (b) le choix du modèle de
l'auditeur Cursor dans `pipeline-audit.yml`, (c) un filtre « push
documentaire » qui empêche un audit de relancer un audit.

**État mécanique** : **vert**. Tous les contrôles CI du commit audité
réussissent (§ 6), et les 4 nouveaux tests passent (§ 5.1). Aucun constat
ci-dessous ne repose sur une porte mécanique rouge : ils portent sur ce que
les portes ne regardent pas.

**Ce que je retiens en une phrase** : la PR répond bien à la demande « je dois
savoir exactement ce qu'il se passe » côté *structure* (un seul endroit à
regarder, une vue calculée, pas de base parallèle), mais trois des indicateurs
que le propriétaire lira en premier disent quelque chose de faux ou
d'invérifiable — un « Rien ne vous attend » produit à partir d'une donnée
absente, une dépense mensuelle qui restera à `0.0 USD` quoi qu'il arrive, et
un plafond recopié à la main — tandis que les deux morceaux de logique les
plus risqués de la PR (les scripts shell des deux workflows) n'ont aucune
preuve d'exécution alors que le dépôt sait déjà les tester.

**Aucun P0.** Rien dans ce diff ne casse `master`, ne contourne une porte
existante, ni ne rend le dépôt incohérent. Les quatre P1 sont des affirmations
fausses ou des contrôles trop lâches, pas des ruptures.

**Cet audit n'instruit rien** : il propose. La décision revient à la boucle
(`architecture/README.md`, ADR-0005/0006) et au propriétaire.

## Les six lentilles, en une ligne chacune

| lentille (`architecture/review-guidelines.md`) | verdict |
|---|---|
| 1. Intention avant diff | Intention lisible et légitime (deux demandes propriétaire datées, citées dans le corps de PR). Mais le corps de PR décrit un comportement que le second commit a remplacé → constat **P2-7**. |
| 2. Preuve d'exécution | Tenue pour `dashboard.py` (4 tests, rejoués verts). Absente pour les deux scripts shell des workflows → **P1-4**. Et trois indicateurs affirment sans mesurer → **P1-2**, **P1-3**. |
| 3. Portes mécaniques d'abord | Les portes ont tourné et sont vertes (§ 6) ; je n'ai dépensé aucun jugement sur ce que `actionlint`/`gitleaks`/`pytest` couvrent. |
| 4. Cadrage adverse | Chaque constat est formulé comme « voici où l'affirmation est fausse », avec la commande ou le fichier:ligne qui le montre. |
| 5. Taille et découpage | Dépassement net du seuil du guide (8 fichiers, +801) pour trois sujets non liés → **P2-9**. |
| 6. Pièges du code généré par IA | Trois occurrences trouvées : succès affirmé non mesuré (**P1-3**), porte de test qui verrouille l'écart au lieu de le révéler (**P1-2**), sur-ingénierie inerte (**P3-10**, **P3-11**). |

# 2. Ce que la PR annonce, et ce que le diff fait

Le corps de PR fait quatre promesses vérifiables. Voici leur état réel.

| promesse (corps de PR) | état vérifié |
|---|---|
| « Une **vue générée** (jamais éditée à la main) depuis les sources de vérité du dépôt » | **Tenu** pour la structure : `hermes/dashboard.py` lit `config.yaml`, les deux ledgers et les briefs. **Deux exceptions** : le plafond mensuel est une constante recopiée (**P2-5**) et le total mensuel est une seconde implémentation d'un calcul déjà existant (**P2-5**). |
| « Une donnée indisponible est dite indisponible — jamais inventée » | **Non tenu** pour la section la plus décisive : « Ce qui attend le propriétaire » affirme « Rien » quand la donnée est absente (**P1-2**). Tenu pour les deux autres sections optionnelles. |
| « `claude-opus-5-thinking-high` par défaut » | **Périmé** : le second commit (`73022bd`) a remplacé cet identifiant par une résolution dynamique. Le run réel a retenu `claude-opus-5` (sans palier « thinking »), preuve au § 5.3. `HANDOFF.md` et le commentaire du workflow, eux, décrivent correctement le nouveau comportement — seul le corps de PR est resté en arrière (**P2-7**). |
| « le job refuse de pousser autre chose que ce seul fichier » | **Tenu** : l'étape `Refuse to push anything but the dashboard` (`.github/workflows/hermes-dashboard.yml:87-95`) inspecte `git status --porcelain` avant tout `git add` et échoue sur tout chemin autre que `hermes/DASHBOARD.md`. C'est un garde bien placé, avant l'effet qu'il prévient. |

# 3. Constats

## P1-1 — Le filtre « push documentaire » couvre `hermes/` en bloc, donc aussi du code exécutable

**Preuve** — `.github/workflows/pipeline-audit.yml:67` :

```
hors_boucle="$(printf '%s\n' "$changed" | grep -vE '^(architecture/(inbox|reviews|decisions|archive)/|architecture/audit-ledger\.jsonl$|hermes/)' || true)"
```

Le commentaire qui introduit l'étape (lignes 49-54) annonce « un push sur
master qui ne touche QUE les **artefacts** de la boucle elle-même ». Or
`hermes/` n'est pas un dossier d'artefacts : la même PR y ajoute
`hermes/dashboard.py`, **364 lignes de Python exécutable**, que
`.github/workflows/hermes-dashboard.yml` lance sur `master` avec
`permissions: contents: write` (lignes 29-31, 85). Un push ne touchant que
`hermes/**` — donc un push qui ne modifie *que* ce script — est classé
documentaire et n'est jamais critiqué.

**Portée réelle, sans exagération** : l'étape porte `if: github.event_name ==
'push'` (ligne 57), donc la critique **au niveau PR reste active** pour un
changement de `hermes/dashboard.py` arrivant par une PR — c'est précisément ce
qui se passe ici. Le trou concerne (a) l'audit post-fusion, et (b) les pushes
directs sur `master` touchant seulement `hermes/**` — ce qui inclut les pushes
du bot `hermes` que cette PR met en place (workflow `hermes-dashboard.yml`,
lignes 104-109).

**Pourquoi c'est un P1 et pas un P2** : la règle 6 de
`docs/rules/hard-won-rules.md` est verbatim « A check that's too coarse costs
as much as a lax one », et le dépôt possède déjà le motif correct à
l'identique — `.github/workflows/pipeline-orchestrate.yml:99` énumère
précisément `architecture/audit-ledger.jsonl|architecture/decisions/|harness/queue/briefs/`
au lieu d'un préfixe de dossier. La forme resserrée serait
`hermes/DASHBOARD\.md$|hermes/reports/|hermes/requests/`, qui correspond
exactement à ce que le contrat `hermes/README.md` autorise Hermes à écrire
(« Hermes n'écrit **jamais** : du code, de la CI, un brief… »).

**Source externe** : la pratique convergente en 2026 est que la configuration
des portes est la chose qui mérite la relecture la plus stricte, jamais la
plus rapide — « Changing the gates themselves is the one thing that always
demands the strictest review — the control protects its own configuration »
[S1]. Le classement par niveau de risque doit se faire sur ce que le chemin
*contient* (doc vs code/infra), pas sur son préfixe [S2].

## P1-2 — « Ce qui attend le propriétaire » affirme « Rien » à partir d'une donnée absente, et le test verrouille l'écart

**Preuve 1, le code** — `hermes/dashboard.py:238` :

```python
if not attentes:
    attentes.append("- Rien : aucune PR ouverte connue, aucun audit en attente de décision.")
```

`attentes` reste vide dans deux cas indiscernables : il n'y a réellement rien,
ou `prs_json` est absent/vide. Le workflow rend le second cas ordinaire —
`.github/workflows/hermes-dashboard.yml:58-60` :

```
gh pr list --state open --json number,title,headRefName,isDraft \
  > "$RUNNER_TEMP/prs.json" || echo '[]' > "$RUNNER_TEMP/prs.json"
```

Un appel `gh` qui échoue produit `[]`, donc une liste vide, donc « Rien ».
L'échec d'une mesure devient une affirmation rassurante.

**Preuve 2, le fichier livré** — `hermes/DASHBOARD.md:18` (version commitée,
générée à 10:13 UTC) dit :

```
- Rien : aucune PR ouverte connue, aucun audit en attente de décision.
```

Au même instant, **deux** PR attendaient le propriétaire :

```
$ gh pr view 26 --json number,state,createdAt
{"createdAt":"2026-08-12T10:12:26Z","number":26,"state":"OPEN", ...}
$ gh pr list --state open --json number,headRefName
[{"number":27,"headRefName":"forge/hermes-dashboard-modele-auditeur-977d"},
 {"number":26,"headRefName":"forge-bot/review-CURSOR-cdc683f-...-31585393890"}]
```

La section dont le titre est « Ce qui attend le propriétaire » est donc, dans
le fichier même que la PR livre, factuellement fausse.

**Preuve 3, la porte de test verrouille l'écart** —
`harness/tests/test_hermes_dashboard.py:98` :

```python
assert contenu.count("Non disponible dans cette génération") == 2
```

Deux sections dégradent honnêtement (« Activité GitHub récente », « Agents
lancés récemment ») ; la troisième — la seule qui porte une décision — ne le
fait pas, et le test grave ce `== 2` comme le comportement attendu. C'est le
piège n°6 du guide sous sa forme la plus coûteuse : un test qui documente
l'écart au lieu de le révéler.

**Doctrine du dépôt** : règle 10 de `docs/rules/hard-won-rules.md`, verbatim
— « When data is missing, the agent invents it silently by default — so
absence must be DECLARABLE and the code must refuse to guess. »

**Source externe** : « A gate should surface uncertainty, not manufacture
confidence » [S3].

## P1-3 — « Dépense CI ce mois-ci » lit un ledger que rien ne persiste : elle affichera `0.0 USD` indéfiniment

**Preuve 1, le ledger est vide et n'a jamais bougé** :

```
$ wc -c harness/pipeline/ci-budget-ledger.jsonl
1 harness/pipeline/ci-budget-ledger.jsonl
$ git log --oneline -1 -- harness/pipeline/ci-budget-ledger.jsonl
cd89141 harness: poser un plafond budgétaire CI traçable
```

Un octet (le saut de ligne), inchangé depuis le commit qui a créé le fichier.

**Preuve 2, aucun workflow ne le committe** :

```
$ grep -rn "ci-budget-ledger" .github/workflows/
.github/workflows/pipeline-audit.yml:51:      # ledger, dashboard/rapports Hermes) est documentaire -- l'auditer
```

La seule occurrence est un commentaire. `pipeline-challenge.yml:178` committe
`architecture/reviews architecture/audit-ledger.jsonl` ;
`pipeline-orchestrate.yml:115` committe
`architecture/audit-ledger.jsonl architecture/decisions harness/queue/briefs`.
Le `ci_budget_guard.py record` des workflows écrit donc dans le disque
éphémère du runner, qui disparaît à la fin du job.

**Preuve 3, la PR se contredit elle-même** :

- `hermes/DASHBOARD.md:13` (livré) : « **Dépense CI ce mois-ci** : 0.0 USD
  **mesurés** sur 0 invocation(s), plafond 200 USD. »
- `HANDOFF.md` ajouté par la même PR : « **1.0615 USD équivalent, sous le
  plafond 5** — **ligne réelle au `ci-budget-ledger.jsonl`**. »

Les deux affirmations ne peuvent pas être vraies ensemble. La seconde est la
bonne description de ce qui s'est passé dans le runner ; la première est ce
que le propriétaire lira.

**Doctrine du dépôt** : règle 8 de `docs/rules/hard-won-rules.md`, verbatim —
« A zero can be a real measurement — use sentinel `-1`, never `0`, for "not
computed" », avec application déclarée « repo-wide » (§ *Enforcement in F0*).
Ici un « non calculé » est rendu comme `0.0` et qualifié de « mesurés ».

**Cadrage honnête** : la cause racine (la non-persistance du ledger) est
antérieure à cette PR et n'est pas de son fait. L'élément nouveau, qui est de
son fait, est que ce zéro devient un **indicateur affiché au propriétaire**
sous le mot « mesurés », dans le document qu'elle désigne comme « l'endroit
où le propriétaire regarde d'abord ».

**Sources externes** : « They had observability. They did not have
enforcement » [S4] ; et sur la même faille de posture : « Monitor any gate
whose failure mode is "spend money". Fail-open guards are indistinguishable
from working ones until the invoice lands » [S5].

## P1-4 — Les deux logiques les plus risquées de la PR n'ont aucune preuve d'exécution, alors que le dépôt sait déjà les tester

**Preuve 1, ce qui est couvert** : `harness/tests/test_hermes_dashboard.py`
(122 lignes, 4 tests) porte **entièrement** sur `hermes/dashboard.py`. Aucun
test ne touche :

- le filtre `hors_boucle` de `pipeline-audit.yml:67` — la logique qui décide
  si un commit sera critiqué ou pas ;
- la cascade de résolution du modèle, `pipeline-audit.yml:132-152` ;
- l'étape « Refuse to push anything but the dashboard »,
  `hermes-dashboard.yml:87-95` — le seul rempart entre un bot et `master`.

**Preuve 2, la convention existe déjà dans le dépôt** :

```
$ grep -rln "github/workflows" harness/tests/
harness/tests/test_mode_guard.py
harness/tests/test_orchestrator.py
harness/tests/test_merge_bot_policy.py
```

`harness/tests/test_merge_bot_policy.py` extrait la frontière **depuis le YAML
du workflow lui-même** (ligne 17 : `WORKFLOW = REPO_ROOT / ".github" /
"workflows" / "merge-bot.yml"`) et prouve qu'elle devient rouge quand on
l'élargit :

```
29: def test_merge_bot_current_boundary_is_extracted_from_workflow_itself()
42: def test_adding_a_branch_prefix_makes_the_boundary_assertion_red(tmp_path)
57: def test_adding_an_allowed_path_makes_the_boundary_assertion_red(tmp_path)
```

C'est exactement la règle 4 (« Prove red first. A check that cannot go red
proves nothing. ») appliquée à un garde de chemin dans un workflow. Le nouveau
garde de chemin de cette PR élargit ce qui échappe à l'audit et n'a pas
d'équivalent. L'omission n'est donc pas un manque de moyens : c'est un écart à
une convention déjà outillée dans le même dépôt.

**Ce qui est bien fait, pour être juste** : les 4 tests livrés testent des
choses qui comptent (ligne de ledger corrompue, données optionnelles absentes,
écriture effective du fichier) et ils passent réellement (§ 5.1). Le reproche
porte sur le périmètre, pas sur la qualité.

**Source externe** : sur l'écart structurel entre « les tests passent » et
« le code fait ce que la spec dit », et la nécessité d'un garde que l'agent ne
peut pas contourner [S6].

## P2-5 — Le plafond mensuel est recopié en dur, et le total mensuel est une seconde implémentation avec la posture d'échec inverse

**Preuve 1, la constante existe déjà** :

- `hermes/dashboard.py:177` : `monthly_cap_usd: float = 200.0` (et à nouveau
  dans `main`, ligne 347, comme défaut d'argparse) ;
- `harness/pipeline/ci_budget_guard.py:41` :
  `DEFAULT_MONTHLY_CAP_USD = 200.0`.

Le workflow `hermes-dashboard.yml` ne passe jamais `--monthly-cap-usd` : la
valeur affichée est donc toujours la copie. Et `dashboard.py:36` fait déjà
`sys.path.insert(0, REPO_ROOT / "harness" / "pipeline")` pour importer
`policy_loader` — importer la constante au lieu de la recopier coûtait une
ligne.

**Preuve 2, le calcul aussi est dupliqué, avec une posture opposée** :

| | `ci_budget_guard.current_month_total_usd` (l. 121-134) | `dashboard.budget_du_mois` (l. 115-131) |
|---|---|---|
| horodatage illisible | `_parse_timestamp(..., line_number=…)` **lève** | `except ValueError: continue` — **ignoré** |
| montant illisible | `_parse_usd(..., line_number=…)` **lève** | `except (TypeError, ValueError): continue` — **ignoré** |
| conséquence | échec bruyant, chiffre jamais sous-estimé | chiffre silencieusement **sous-estimé** |

Les deux nombres portent le même nom pour le lecteur (« la dépense du mois »)
et peuvent diverger, le tableau de bord étant celui qui minore. La ligne
corrompue est même célébrée comme une qualité par le test
(`test_hermes_dashboard.py`, docstring : « une ligne de ledger corrompue
n'abat pas la génération ») alors qu'elle enlève de l'argent du total sans le
dire — même famille que le P1-2 : l'absence doit être déclarable.

**Doctrine du dépôt** : principe non négociable n°1, « One source of truth —
views never become parallel databases » (invoqué par la docstring de
`dashboard.py` elle-même) ; et règle 12, « A parity fingerprint is cited by
NAME, never by VALUE — it will get rebased someday, and the doc holding the
dead constant traps every subsequent brief. »

## P2-6 — Le fichier généré change à chaque exécution : le garde « rien à pousser » ne peut jamais se déclencher

**Preuve, commande rejouée** (§ 5.2) : deux générations successives ne
diffèrent que par l'horodatage.

```
$ python hermes/dashboard.py && git diff --stat -- hermes/DASHBOARD.md
 hermes/DASHBOARD.md | 2 +-
-> Générée le 2026-08-12 10:13 UTC.
+> Générée le 2026-08-12 10:23 UTC.
```

`hermes/dashboard.py:208` écrit la minute courante dans le corps du fichier.
Conséquence sur `hermes-dashboard.yml:100-103` :

```
if git diff --quiet -- hermes/DASHBOARD.md; then
  echo "Tableau de bord inchangé -- rien à pousser."
```

Ce test est **structurellement mort** : le fichier diffère toujours. Avec
`schedule: cron '17 */6 * * *'` (ligne 26) plus le déclencheur `push`, cela
fait au moins **4 commits par jour sur `master`, indéfiniment**, chacun sans
contenu nouveau.

**Coût mesurable** : les trois workflows déclenchés sans filtre de chemin
(`harness-ci.yml`, `audit-guard.yml`, `security.yml` — tous `on: push:` nu)
tournent à chaque commit du tableau de bord, plus un runner
`pipeline-audit.yml` qui fait un `checkout` complet (`fetch-depth: 0`) avant
de décider de sauter. Sur le commit audité, ces trois workflows représentent
7 jobs (§ 6).

**Effet de bord qui touche l'objectif même de la PR** : la section « Activité
GitHub récente » est alimentée par `gh run list --limit 20`
(`hermes-dashboard.yml:55`). Les runs déclenchés par les régénérations du
tableau de bord occuperont cette fenêtre de 20 et chasseront l'activité que
le propriétaire veut voir. La vue se remplira de sa propre production.

**Piste** (non instruite) : sortir l'horodatage du test de changement — par
exemple comparer le contenu hors ligne « Générée le », ou n'écrire que la
date sans la minute.

**Sources externes** : sur l'amplification du coût par commit sous charge
d'agents et la recommandation explicite de « skip CI for documentation-only
changes » [S5], [S7].

## P2-7 — Le modèle est choisi par `head -1` sur une liste non contractuelle et aplatie ; le modèle réellement utilisé n'est tracé nulle part de durable

**Preuve décisive : la liste réelle vue par le workflow**, run
[31586836026](https://github.com/PLiagre/ForgeHistory/actions/runs/31586836026),
job `invoke-cursor-auditor` — en-tête ligne 237 du log, **99 identifiants aux
lignes 238 à 336**, modèle retenu ligne 337 :

```
237: Modèles disponibles (GET /v1/models):
238: default
239: auto
240: grok-4.5
...  composer-2.5 / composer-latest / composer / composer-2-5
245: claude-opus-5      246: opus-latest  247: opus  248: opus-5
249: claude-opus-4-8    250: opus-latest  251: opus  252: opus-4.8  253: opus-4-8
...  claude-opus-4-7 / claude-opus-4-6 / claude-opus-4-5 (avec leurs alias)
...  gpt-5.6-sol / claude-fable-5 / claude-sonnet-5 / sonnet-latest / …
337: Modèle retenu: claude-opus-5
```

Quatre choses en découlent, toutes vérifiées sur les 99 entrées de ce log :

1. **Aucun identifiant ne contient « thinking »** — `grep -ci thinking` sur la
   liste extraite renvoie `0`. La première préférence,
   `pipeline-audit.yml:141` (`grep -i opus | grep -i thinking | head -1`), est
   donc une **branche morte** : elle ne peut jamais correspondre. Le palier
   « thinking » que le corps de PR annonce n'existe pas dans cette API.
2. **Le choix final repose sur l'ordre de la liste**, non sur une contrainte.
   La liste contient **cinq** modèles Opus (`claude-opus-5`,
   `claude-opus-4-8`, `claude-opus-4-7`, `claude-opus-4-6`,
   `claude-opus-4-5`) ; `grep -i opus | head -1` (ligne 144) a rendu
   `claude-opus-5` parce que l'API l'a listé en premier. Si l'API réordonne un
   jour, chaque audit basculera silencieusement sur un modèle plus ancien,
   sans erreur et sans avertissement.
3. **L'aplatissement id + alias crée des collisions** : l'alias nu `opus`
   apparaît **quatre fois** (une pour `claude-opus-5`, `claude-opus-4-8`,
   `claude-opus-4-6` et `claude-opus-4-5`) et `opus-latest` deux fois. Une
   variable `CURSOR_AUDITOR_MODEL=opus` passe donc le `grep -qxF` de la ligne
   134 tout en désignant quatre modèles à la fois ; `default` et `auto`
   (lignes 238-239) la passent aussi, alors qu'ils signifient « pas de
   choix ».
4. **Rien ne trace durablement le modèle retenu.** Le log de run est la seule
   trace (rétention limitée). Ni le frontmatter d'un audit, ni
   `architecture/audit-ledger.jsonl`, ni `hermes/DASHBOARD.md` n'enregistrent
   avec quel modèle un audit donné a été produit. La demande propriétaire qui
   motive ce commit — « l'agent d'audit en claude-4.5-sonnet, c'est pas
   terrible » — devient donc invérifiable après coup.

**À créditer** : la forme du `jq` (`.items[].id`, `.aliases[]?`) est
**validée par ce même run** — la liste s'imprime, donc le schéma supposé est
le bon. Passer d'un identifiant deviné à une interrogation de l'API est un
vrai progrès ; le reproche porte sur la sélection, pas sur la démarche.

**Corollaire documentaire** : le corps de PR § 2 annonce
« `claude-opus-5-thinking-high` par défaut » ; le comportement fusionné est
« premier « opus » de la liste, sinon « grok », sinon le défaut du compte ».
`HANDOFF.md` et le commentaire du workflow (lignes 105-113) décrivent
correctement le nouveau comportement — c'est le document que le propriétaire
lit pour décider de fusionner qui est resté en arrière (lentille 1).

## P2-8 — La montée en gamme du modèle n'est accompagnée d'aucun garde de consommation côté `pipeline-audit.yml`

**Preuve** :

```
$ grep -n "budget\|precheck\|max-budget" .github/workflows/pipeline-audit.yml
(aucune occurrence hors les lignes de résolution du modèle)
```

`pipeline-challenge.yml:107` et `pipeline-forge-run.yml:121` appellent
`ci_budget_guard.py precheck` avant d'invoquer un agent ;
`pipeline-audit.yml` ne le fait pas, ne lit pas `mode:` au moment de
l'exécution, et ne pose aucun plafond par appel. Son seul frein est le label
`pipeline/pause`.

**Nuance importante, pour ne pas surcharger le constat** : c'est un état
antérieur à cette PR, et `docs/rules/full-auto-pipeline.md:93-96` limite
explicitement sa promesse de plafond aux appels **Claude headless** — la
règle ne se contredit donc pas. En revanche `harness/pipeline/config.yaml`
affirme, dans son commentaire de tête, que les trois workflows sont « each
behind the `pipeline/pause` kill-switch, this `mode:` key, the monthly
`ci_budget_guard` precheck and a per-call `--max-budget-usd` cap » — ce qui
est faux pour `pipeline-audit.yml` sur les trois derniers points, et le même
fichier se contredit plus bas en précisant que seuls `pipeline-forge-run.yml`
et `pipeline-challenge.yml` lisent `mode:` à l'exécution.

**L'élément nouveau apporté par cette PR** est que l'enjeu monte : elle
remplace le modèle par défaut par un modèle de gamme supérieure sur un
workflow déclenché à **chaque PR non-brouillon et chaque push master**, tout
en livrant un tableau de bord qui, par construction (P1-3), n'affichera
jamais cette consommation — et dont la section « Agents lancés récemment » ne
montre que des noms et des statuts, aucun volume.

## P2-9 — Trois sujets non liés dans une PR de +801 / −13 sur 8 fichiers

Le guide fixe le seuil : « Un diff qui dépasse ~5 fichiers ou quelques
centaines de lignes dépasse ce qu'une relecture honnête peut connecter à
l'intention » (lentille 5). Ici : 8 fichiers, +801 lignes, et trois sujets
qui n'ont aucune dépendance technique entre eux — le tableau de bord (3
fichiers neufs + 1 test), la résolution du modèle, le filtre anti-boucle.

Élément aggravant, propre à ce dépôt : deux des trois sujets modifient
`.github/workflows/**`, chemin que `harness/pipeline/config.yaml` place dans
`auto_merge_denylist` précisément parce qu'il est sensible. Les mélanger avec
une fonctionnalité de confort fait relire les deux avec la même attention
moyenne. Les deux commits séparent partiellement les sujets, ce qui aide ;
deux PR l'auraient fait complètement.

## P3-10 — `ETATS_EN_ATTENTE` est défini, jamais utilisé, et en désaccord silencieux avec la logique employée

```
$ grep -rn "ETATS_EN_ATTENTE" --include=*.py .
./hermes/dashboard.py:53:ETATS_EN_ATTENTE = {
```

Une seule occurrence : la définition. Le calcul réellement utilisé est
`hermes/dashboard.py:197` — `[a for a in audits if a["event"] !=
"AUDIT_ARCHIVED"]`. Les deux ne disent pas la même chose (l'ensemble mort
inclut `AUDIT_REJECTED`, la logique vivante aussi mais par accident du
complément). Sur-ingénierie inerte (piège n°6 du guide) : le prochain lecteur
croira que la constante est la règle.

## P3-11 — Sentinelle en texte libre dans un champ d'horodatage, et une assertion inopérante

**Sentinelle** — `hermes/dashboard.py:103` place la phrase
`"— (fichier inbox, pas encore au ledger)"` dans un champ `timestamp`, puis
`ligne 283` doit tester `str(a["timestamp"]).startswith("—")` pour distinguer
les deux natures de valeur. La règle 8 du dépôt prescrit une sentinelle
explicite et typée pour « non calculé », pas un message dans le champ de
données.

**Assertion inopérante** — `harness/tests/test_hermes_dashboard.py:82-83` :

```python
assert "CURSOR-aaa-clos" not in contenu.split("## La boucle d'audit")[1].split("##")[0].replace(
    "boucle(s) close(s)", "")
```

Le `.replace("boucle(s) close(s)", "")` ne peut pas influencer le résultat :
la chaîne retirée ne contient jamais `CURSOR-aaa-clos`. C'est une incantation
qui donne à l'assertion un air de précision qu'elle n'a pas.

## P3-12 — Une fixture de test est comptée parmi les « boucles closes » montrées au propriétaire

`hermes/DASHBOARD.md:14` affiche « **Audits en cours** : 1 — boucles closes :
7 ». Le compte est exact au regard du ledger, mais l'une des sept est
`CURSOR-FIXTURE-full-auto-demo` — un audit-fixture de démonstration, présent
dans l'inbox à des fins de test. Le compteur que le propriétaire lit comme
« sept boucles réellement menées à terme » en contient six.

# 4. Ce que cette PR fait bien

À porter au crédit du changement, sans réserve :

1. **Un seul endroit à regarder, et c'est une vue.** Le tableau de bord dérive
   ses chiffres des fichiers du dépôt plutôt que de tenir un état propre —
   c'est le principe n°1 respecté dans sa forme, y compris pour les états de
   la machine à états, traduits en phrases humaines
   (`ETATS_HUMAINS`, `dashboard.py:41-51`) au lieu de codes.
2. **Le garde de poussée est placé avant l'effet qu'il prévient.**
   `hermes-dashboard.yml:87-95` inspecte l'arbre de travail avant tout
   `git add` et fait échouer le job sur tout chemin inattendu — la règle 5 du
   dépôt appliquée correctement, et les fichiers JSON intermédiaires sont
   écrits dans `$RUNNER_TEMP`, hors du dépôt, donc invisibles pour ce garde.
3. **Un audit présent dans l'inbox mais absent du ledger est listé quand
   même** (`dashboard.py:99-103`) : le cas « la machine n'a pas encore vu ce
   fichier » ne devient pas un silence. C'est exactement la posture que le
   P1-2 réclame ailleurs — elle est donc déjà comprise, juste pas appliquée
   partout.
4. **La correction du modèle part d'une mesure, pas d'une intuition** :
   l'identifiant deviné a été refusé `invalid_model`, et la réponse a été
   d'interroger l'API plutôt que de deviner mieux.
5. **Le premier tour réel est raconté avec ses ratés** dans `HANDOFF.md`
   (deux branches de brouillon, PR non ouverte par l'agent, boucle sur la
   boucle). Un compte-rendu qui liste ses propres échecs est ce qui rend le
   suivant améliorable.

# 5. Commandes rejouées

Toutes les commandes ci-dessous ont été exécutées sur un worktree du commit
audité (`73022bdab6d2fff7c4d08812c281bcc56172dcc8`), en lecture seule sur le
dépôt.

## 5.1. Les 4 nouveaux tests passent

```
$ .venv/bin/python -m pytest harness/tests/test_hermes_dashboard.py -q
....                                                                     [100%]
4 passed in 0.02s
```

## 5.2. Le fichier généré diffère du fichier livré à chaque exécution (P2-6)

```
$ python hermes/dashboard.py
OK: /tmp/wt27/hermes/DASHBOARD.md
$ git diff --stat -- hermes/DASHBOARD.md
 hermes/DASHBOARD.md | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
$ git diff -- hermes/DASHBOARD.md
-> Générée le 2026-08-12 10:13 UTC.
+> Générée le 2026-08-12 10:23 UTC.
```

Une seule ligne de différence, et c'est l'horodatage : le contenu utile est
bien déterministe, mais le fichier ne l'est pas.

## 5.3. Le modèle réellement retenu par la CI (P2-7)

```
$ gh run view 31586836026 --log | grep -n "Modèle"
237: Modèles disponibles (GET /v1/models):
337: Modèle retenu: claude-opus-5

$ gh run view 31586836026 --log | sed -n '238,336p' > /tmp/models.txt
$ wc -l /tmp/models.txt ; grep -ci thinking /tmp/models.txt
99
0

$ gh run view 31586836026 --log | grep agent_id
  "agent_id": "bc-cb1edd56-1954-428d-9576-22a15b83066f",
  "status": "RUNNING"
```

99 identifiants disponibles, aucun contenant « thinking » : la première
préférence du workflow ne peut pas aboutir.

## 5.4. Le ledger de budget CI est vide et n'est committé par personne (P1-3)

```
$ wc -c harness/pipeline/ci-budget-ledger.jsonl
1 harness/pipeline/ci-budget-ledger.jsonl
$ grep -rn "ci-budget-ledger" .github/workflows/ | grep -v "^\S*:[0-9]*: *#"
(rien : la seule occurrence est un commentaire)
```

## 5.5. État du ledger d'audits et de l'inbox (P3-12)

```
$ python3 -c "…dernier événement par audit_id…" < architecture/audit-ledger.jsonl
CURSOR-FIXTURE-full-auto-demo               AUDIT_ARCHIVED
CURSOR-5633ee7-automation-completeness      AUDIT_ARCHIVED
CURSOR-e9a6f4c-codex-passation-full-auto    AUDIT_ARCHIVED
CURSOR-6231186-execution-budgets            AUDIT_ARCHIVED
CURSOR-bbe6da5-bare-python-matcher          AUDIT_ARCHIVED
CURSOR-POSTMERGE-42cb054-audit-system       AUDIT_ARCHIVED
CURSOR-198cfd9-opus5-context-engineering    AUDIT_ARCHIVED
```

(Le script signalait `CORRUPT` sur toute ligne illisible ; aucune n'est
apparue — le ledger réel est intact.)

Sept boucles closes, dont une fixture ; le huitième fichier de l'inbox
(`CURSOR-cdc683f-…`) n'a pas de ligne au ledger et est bien listé comme
« déposé » par le tableau de bord.

# 6. CI du commit audité

**Classification : verte.** Aucun échec, aucun contrôle en attente.

| contrôle | résultat |
|---|---|
| `tests` (harness-ci) | SUCCESS |
| `f0-demo` (harness-ci) | SUCCESS |
| `schema` (audit-guard) | SUCCESS |
| `cursor-scope` (audit-guard) | SKIPPED — attendu : la branche n'est pas `cursor/*` |
| `actionlint` (security) | SUCCESS |
| `gitleaks` (security) | SUCCESS |
| `invoke-cursor-auditor` (pipeline-audit) | SUCCESS — a lancé l'agent `bc-cb1edd56` (le présent audit) |
| `Reconcile local Hermes state` (hermes-observer) | SUCCESS |
| `check-and-automerge` (merge-bot) | SKIPPED — attendu : `.github/workflows/**` est dans `auto_merge_denylist` |

Les deux `SKIPPED` sont des sauts conformes au contrat, pas des contrôles
manquants. Conformément à la lentille 3, je n'ai pas re-jugé ce que ces
portes couvrent (lint YAML, secrets, schéma des audits, suite de tests) : les
constats du § 3 portent uniquement sur ce qu'aucune de ces portes ne regarde.

# 7. Briefs proposés (3 au maximum)

Propositions, pas instructions — la conversion en brief reste au propriétaire
(`architecture/README.md`).

## Brief proposé n°1 — Rendre les indicateurs du tableau de bord non
trompeurs, et stabiliser le fichier généré

Couvre P1-2, P1-3, P2-5, P2-6, P3-11, P3-12. Idée directrice : toute section
du tableau de bord doit pouvoir dire « je ne sais pas », y compris « Ce qui
attend le propriétaire » ; un « non calculé » ne s'affiche jamais comme
`0.0` (règle 8) ; le plafond et le total mensuels sont importés de
`ci_budget_guard` au lieu d'être recopiés et recalculés ; les lignes de
ledger illisibles sont comptées et affichées au lieu d'être absorbées ; et le
fichier généré cesse de changer quand rien n'a changé. Question ouverte à
trancher dans le brief, pas ici : faut-il aussi persister
`ci-budget-ledger.jsonl` depuis la CI (sans quoi l'indicateur restera vide,
même honnête) ?

## Brief proposé n°2 — Resserrer le filtre « push documentaire » et le doter
d'un test rouge d'abord

Couvre P1-1 et P1-4. Idée directrice : énumérer les chemins réellement
documentaires au lieu du préfixe `hermes/`, et adosser cette frontière à un
test construit sur le modèle déjà en place dans
`harness/tests/test_merge_bot_policy.py` — frontière extraite du YAML, et
assertion qui devient rouge dès qu'on l'élargit. Le même test devrait couvrir
l'étape « Refuse to push anything but the dashboard ».

## Brief proposé n°3 — Épingler et tracer le modèle de l'auditeur

Couvre P2-7 et P2-8. Idée directrice : choisir le modèle sur un identifiant
exact (jamais un alias, jamais `head -1` d'une liste dont l'ordre n'est pas
garanti), échouer bruyamment plutôt que se replier en silence quand la gamme
exigée par le propriétaire n'est pas disponible, et inscrire le modèle
réellement utilisé dans une trace durable — frontmatter de l'audit produit
ou ligne de ledger — afin qu'une critique puisse être attribuée après coup.
Le brief pourra trancher s'il faut aussi aligner `pipeline-audit.yml` sur les
gardes de consommation de ses deux workflows frères, et corriger le
commentaire de `harness/pipeline/config.yaml` qui les décrit comme déjà
présents.

# 8. Sources externes

Recherche menée le 2026-08-12 sur les trois thèmes imposés par le contrat
(`architecture/agents/cursor-auditor.md` › Preuve de fin) : *autonomous AI
dev pipeline*, *agent orchestration CI*, *token budget LLM agents*. Quand la
page n'affiche pas de date de publication, seule la date de consultation est
donnée — je ne l'invente pas.

| # | source | date | consulté le |
|---|---|---|---|
| S1 | *agent-pr-flow — Delivery governance for agent-driven engineering* — <https://github.com/jasonjgarcia24/agent-pr-flow> — « Changing the gates themselves is the one thing that always demands the strictest review — the control protects its own configuration. » | date de publication non affichée | 2026-08-12 |
| S2 | *AI Code Review Agent: A Build Blueprint for Reviewing PRs and Gating Risky Changes (2026)* — <https://resources.rework.com/libraries/ai-agents/ai-code-review-agent> — classement par niveau de risque : doc et tests passent vite, l'infra et la configuration sont barrées. | 2026 (dans le titre) | 2026-08-12 |
| S3 | *I made stale coding-agent context fail CI instead of failing silently* — <https://dev.to/agentskit/i-made-stale-coding-agent-context-fail-ci-instead-of-failing-silently-3434> — sur les artefacts générés committés : vérifier l'état committé, et « a gate should surface uncertainty, not manufacture confidence ». | date de publication non affichée | 2026-08-12 |
| S4 | *AI Agent Token Budget Enforcement [2026]* — <https://waxell.ai/blog/ai-agent-token-budget-enforcement> — « They had observability. They did not have enforcement. » | 2026 (dans le titre) | 2026-08-12 |
| S5 | *Agents Broke the Economics of Your CI* — <https://understandingdata.com/posts/agents-broke-your-ci-economics/> — multiplicateur de 6 à 10 sur le volume de commits sous charge d'agents ; « Monitor any gate whose failure mode is "spend money". Fail-open guards are indistinguishable from working ones until the invoice lands. » | date de publication non affichée | 2026-08-12 |
| S6 | *CI/CD for AI Agents: How to Integrate Agent Orchestration into Your Pipeline* — <https://www.augmentcode.com/guides/cicd-ai-agents-pipeline-integration> — l'écart entre « les tests passent » et « le code respecte la spec » ; le vérificateur doit être une porte obligatoire que l'agent ne peut pas contourner. | date de publication non affichée | 2026-08-12 |
| S7 | *The Agent Code Explosion Is Breaking Your CI. Here's How to Adapt.* — <https://www.prateek-sharma.com/blog/agent-code-explosion-breaking-ci/> — configurer la retenue en amont : commiter moins souvent, regrouper, sauter la CI pour les changements purement documentaires. | date de publication non affichée | 2026-08-12 |
| S8 | *Building Real-Time AI Cost Controls with agentgateway* — <https://www.solo.io/blog/building-real-time-ai-cost-controls-with-agentgateway> — « Provider dashboards remain useful, but they are usually retrospective » ; échouer fermé quand le service de coût ne peut pas décider. | date de publication non affichée | 2026-08-12 |

Sources internes citées, par pointeur et jamais recopiées :
`architecture/review-guidelines.md` (les six lentilles, les sévérités),
`docs/rules/hard-won-rules.md` (règles 4, 6, 8, 10, 12),
`docs/rules/simulation-principles.md` (principe n°1 via `CLAUDE.md`),
`docs/rules/full-auto-pipeline.md`, `harness/pipeline/config.yaml`,
`hermes/README.md`, `harness/tests/test_merge_bot_policy.py`.

# 9. Périmètre et limites de cet audit

- **Lecture seule.** Aucun fichier du dépôt audité n'a été modifié ; les
  commandes du § 5 ont tourné dans un worktree jetable (`/tmp/wt27`), sauf la
  régénération du § 5.2 qui écrit `hermes/DASHBOARD.md` **dans ce worktree**
  et n'a jamais été committée.
- **Non vérifié** : l'état de la protection de branche de `master`
  (`gh api …/branches/master/protection` renvoie `403 Resource not accessible
  by integration` avec le jeton disponible). Je ne peux donc pas dire si le
  `git push origin master` de `hermes-dashboard.yml:109` aboutira ou échouera
  toutes les 6 heures. Je m'abstiens de conclure là-dessus plutôt que de
  supposer — le pushing direct sur `master` par un bot est par ailleurs un
  motif déjà en place dans `pipeline-orchestrate.yml`, donc pas un élément
  nouveau que j'aurais à re-litiguer.
- **Non vérifié** : le comportement de l'API Cursor si `model.id` est un
  alias ambigu (`opus-latest`). Le constat P2-7 porte sur l'ambiguïté du
  choix côté workflow, qui est établie par la liste elle-même, pas sur ce que
  l'API en ferait.
- **Cet audit ne préautorise rien** : aucun de ses points n'est « à
  implémenter » ; les trois flags `*_authorized` du frontmatter sont `false`.
