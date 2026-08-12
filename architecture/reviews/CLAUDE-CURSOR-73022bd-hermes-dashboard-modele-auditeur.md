---
review_of: CURSOR-73022bd-hermes-dashboard-modele-auditeur
reviewer: claude-code
target_commit: 73022bdab6d2fff7c4d08812c281bcc56172dcc8
reviewed_at: 2026-08-12T11:53:47Z
---

# Contre-audit de CURSOR-73022bd-hermes-dashboard-modele-auditeur

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `73022bdab6d2fff7c4d08812c281bcc56172dcc8`.
- Le commit existe : `git cat-file -t 73022bdab6d2fff7c4d08812c281bcc56172dcc8`
  → `commit`, message « audit: résolution du modèle via GET /v1/models
  (l'identifiant deviné était invalid_model) … ». Il est bien un ancêtre de
  `master` (fusionné par `65c3ac1`, PR #27).
- Reproduction : `git worktree add /tmp/wt27 73022bd…` (lecture seule,
  worktree jetable), puis rejeu de toutes les commandes du § 5 de l'audit
  plus des vérifications structurelles supplémentaires pour chaque constat
  du § 3. Détail commande par commande sous chaque ligne du tableau ci-
  dessous.
- Environnement de cette revue : **pas de `GH_TOKEN`/`gh auth`** disponible
  (`gh auth status` → « not logged into any GitHub hosts » ; un appel anonyme
  à l'API GitHub sur le run cité renvoie `404`). Tout ce qui dépend de l'API
  GitHub/Cursor en direct (§ 5.3, § 6 de l'audit, le texte exact du corps de
  PR #27) n'a pas pu être rejoué indépendamment ici — noté explicitement en
  PARTIAL plutôt que passé sous silence.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| P1-1 | Le filtre « push documentaire » (`pipeline-audit.yml:67`) couvre `hermes/` en bloc, y compris `hermes/dashboard.py` (364 lignes exécutables, lancé avec `contents: write`) | **CONFIRMED** | Ligne rejouée verbatim : `hors_boucle="$(printf '%s\n' "$changed" | grep -vE '^(architecture/(inbox|reviews|decisions|archive)/|architecture/audit-ledger\.jsonl$|hermes/)' || true)"` à `pipeline-audit.yml:67`. `wc -l hermes/dashboard.py` → 364. `grep -n "permissions" hermes-dashboard.yml` → `contents: write` (ligne 24). Le motif resserré cité en comparaison existe bien tel quel dans `pipeline-orchestrate.yml:99` (`architecture/audit-ledger\.jsonl|architecture/decisions/|harness/queue/briefs/`). Le contrat `hermes/README.md:37` dit bien « Hermes n'écrit **jamais** : du code ». Nuance de portée de l'audit lui-même (l'étape reste active en PR via `if: github.event_name == 'push'`, ligne 57) vérifiée correcte. |
| P1-2 | « Ce qui attend le propriétaire » affiche « Rien » quand la donnée `gh pr list` est absente/vide, pas seulement quand il n'y a réellement rien ; le test verrouille `== 2` occurrences de « Non disponible » au lieu de 3 | **CONFIRMED** | Code : `dashboard.py:236-238`, `if not attentes: attentes.append("- Rien : …")`. Fallback CI : `hermes-dashboard.yml:52-54`, `gh pr list … > prs.json || echo '[]' > prs.json`. Fichier livré : `hermes/DASHBOARD.md:18` dit « Rien » à 10:13 UTC. **Reconstruit indépendamment sans API GitHub** : `git log` local montre que `04b98b5` (le commit qui a produit ce DASHBOARD.md, horodaté `2026-08-12T10:13:24Z`) précède la fusion de PR #26 (`dbd315c`, `2026-08-12T11:41:12+02:00` = 11:41 UTC) et de PR #27 elle-même (`65c3ac1`, 11:42 UTC) — donc au moment de la génération, les deux branches `forge-bot/review-CURSOR-cdc683f-…` (PR #26) et `forge/hermes-dashboard-modele-auditeur-977d` (PR #27) étaient toutes deux ouvertes. Deux PR ouvertes, affichage « Rien » : la contradiction est réelle, établie sans dépendre de la preuve GitHub-live de l'audit. Test : `grep -n "Non disponible dans cette génération"` sur `test_hermes_dashboard.py` → ligne 98, `assert contenu.count(...) == 2` — confirmé, deux sections seulement (sur trois) sont testées comme dégradables. |
| P1-3 | « Dépense CI ce mois-ci » restera `0.0 USD` indéfiniment car aucun workflow ne persiste `ci-budget-ledger.jsonl` | **CONFIRMED**, avec une réserve sur une preuve secondaire | Ledger : `wc -c ci-budget-ledger.jsonl` → 1 octet, dernier commit `cd89141` (antérieur à cette PR). `grep -n "ci_budget_guard.py" .github/workflows/*.yml` → seuls `pipeline-challenge.yml` et `pipeline-forge-run.yml` appellent `record`/`precheck` ; leurs étapes de commit (`git add architecture/reviews architecture/audit-ledger.jsonl` et `git add architecture/audit-ledger.jsonl architecture/decisions harness/queue/briefs`) n'incluent jamais `ci-budget-ledger.jsonl` — il reste donc sur le disque éphémère du runner. `pipeline-audit.yml` n'appelle même pas `ci_budget_guard`. Contradiction interne confirmée : `DASHBOARD.md:13` dit « 0.0 USD mesurés » pendant que `HANDOFF.md` (même PR) dit « 1.0615 USD équivalent … ligne réelle au `ci-budget-ledger.jsonl` ». **Réserve** : la transcription exacte de la « Preuve 2 » de l'audit (`grep -rn "ci-budget-ledger" .github/workflows/` → ligne 51 affichée) ne se reproduit pas : rejouée telle quelle sur le même worktree, cette commande renvoie **zéro correspondance** (code de sortie 1), alors que la ligne 51 citée ne contient que le mot « ledger », jamais la sous-chaîne exacte « ci-budget-ledger ». C'est une preuve mal transcrite dans l'audit — mais la conclusion qu'elle prétend étayer reste vraie, établie ci-dessus par une autre voie (absence totale de `git add`/`commit` du fichier dans les deux workflows qui l'écrivent). |
| P1-4 | Les scripts shell des deux workflows (filtre `hors_boucle`, cascade de modèle, garde de poussée) n'ont aucun test, alors que `test_merge_bot_policy.py` prouve que le dépôt sait déjà tester une frontière de workflow en l'extrayant du YAML | **CONFIRMED** | `harness/tests/test_hermes_dashboard.py` n'importe que `dashboard` (ligne 17) ; `grep -rln "pipeline-audit\.yml\|hors_boucle\|hermes-dashboard\.yml" harness/tests/` → aucune correspondance. `test_merge_bot_policy.py:17` définit bien `WORKFLOW = REPO_ROOT / ".github" / "workflows" / "merge-bot.yml"` et contient des tests qui passent au rouge quand la frontière s'élargit (`test_adding_a_branch_prefix_makes_the_boundary_assertion_red`, `test_adding_an_allowed_path_makes_the_boundary_assertion_red`). Les 4 tests de `test_hermes_dashboard.py` rejoués : `python3 -m pytest harness/tests/test_hermes_dashboard.py -q` → `4 passed in 0.04s`, identique au § 5.1 de l'audit. |
| P2-5 | Plafond mensuel recopié en dur (`200.0` × 2 dans `dashboard.py` au lieu d'importer `ci_budget_guard.DEFAULT_MONTHLY_CAP_USD`) ; `budget_du_mois` est une seconde implémentation qui avale silencieusement les lignes illisibles au lieu de lever | **CONFIRMED** | `grep -n "200.0"` → `dashboard.py:177` et `:347` (défaut argparse), jamais `--monthly-cap-usd` passé par `hermes-dashboard.yml` (grep sur le fichier : absent). `ci_budget_guard.py:41` a bien `DEFAULT_MONTHLY_CAP_USD = 200.0`, et `dashboard.py:36` fait déjà `sys.path.insert(0, … / "harness" / "pipeline")` pour importer `policy_loader` — la même astuce d'import aurait marché pour la constante. Comparaison directe des deux implémentations : `ci_budget_guard.current_month_total_usd` (`:121-134`) appelle `_parse_timestamp`/`_parse_usd`, qui **lèvent** `BudgetGuardError` sur une ligne corrompue (`load_budget_entries`, `:95-117`, `raise BudgetGuardError` sur JSON invalide ou objet non-dict) ; `dashboard.budget_du_mois` (`:115-131`) fait `except ValueError: continue` et `except (TypeError, ValueError): continue` — silencieux. La docstring du test (`test_hermes_dashboard.py:7`) confirme que ce comportement est voulu et testé comme une qualité (« une ligne de ledger corrompue n'abat pas la génération »). |
| P2-6 | Le fichier généré change à chaque exécution (l'horodatage à la minute), donc le garde `git diff --quiet` ne peut jamais se déclencher, et le cron `17 */6 * * *` produit des commits vides en continu | **CONFIRMED** | `dashboard.py:208` : `f"> Générée le {now.strftime('%Y-%m-%d %H:%M UTC')}."`. Rejoué : deux exécutions successives de `python3 hermes/dashboard.py` à 2 s d'intervalle produisent un diff d'une ligne, l'horodatage seul (`10:13` → `11:52` sur le worktree). `hermes-dashboard.yml:19-20` : `schedule: cron: '17 */6 * * *'`, plus le déclencheur `push`. Le garde à la ligne 94 (`git diff --quiet -- hermes/DASHBOARD.md`) est donc structurellement mort tel que décrit. |
| P2-7 | La cascade `grep -i opus \| grep -i thinking \| head -1` est une branche morte (aucun identifiant Cursor connu ne contient « thinking ») ; la sélection dépend de l'ordre de la liste API, pas d'une contrainte ; les alias créent des collisions ; rien ne trace durablement le modèle réellement utilisé | **PARTIAL — logique du code confirmée, preuve d'exécution live non rejouable ici** | Code rejoué verbatim : `pipeline-audit.yml:129-152` correspond exactement à la citation de l'audit (préférence `opus`+`thinking`, puis `opus`, puis `grok`, sinon défaut du compte ; sélection par `head -1` sur une liste aplatie id+alias). Ce que je **n'ai pas pu revérifier** dans cet environnement : le contenu réel de la liste `/v1/models` et le run CI `31586836026` cités en § 5.3 — pas de `GH_TOKEN` ici, `gh auth status` négatif, et un appel anonyme à l'API GitHub sur ce run renvoie `404`. Je ne peux donc ni confirmer ni infirmer indépendamment les « 99 identifiants, 0 occurrence de "thinking" » ni le « modèle retenu : claude-opus-5 » de cette exécution précise — je m'abstiens plutôt que de supposer. Le sous-point « corollaire documentaire » (le corps de PR #27 resterait sur l'ancien identifiant) porte sur un texte qui vit sur GitHub, hors du dépôt cloné : non vérifiable ici. En revanche, la cohérence interne du dépôt sur ce point est confirmée : `HANDOFF.md:36` et le commentaire `pipeline-audit.yml:107` décrivent tous deux correctement le nouveau comportement (« choisit le premier modèle « opus » (puis « grok ») ») et non l'ancien identifiant deviné, ce qui est cohérent avec la thèse de l'audit sans la prouver entièrement. |
| P2-8 | Aucun garde de budget (`precheck`/`max-budget`) côté `pipeline-audit.yml`, contrairement à ses deux workflows frères ; `config.yaml` affirme le contraire dans son commentaire et se contredit lui-même plus bas | **CONFIRMED** | `grep -n "budget\|precheck\|max-budget\|mode:" pipeline-audit.yml` → aucune occurrence. `config.yaml:19-21` affirme que les trois workflows sont « each behind … this `mode:` key, the monthly `ci_budget_guard` precheck and a per-call `--max-budget-usd` cap », puis `config.yaml:31` précise que seuls `pipeline-forge-run.yml` et `pipeline-challenge.yml` lisent `mode:` à l'exécution — auto-contradiction confirmée verbatim. Nuance de l'audit vérifiée correcte : `docs/rules/full-auto-pipeline.md:93-96` limite explicitement sa promesse de plafond aux appels « headless Claude », donc ne se contredit pas lui-même. Le seul frein confirmé sur `pipeline-audit.yml` est bien le label `pipeline/pause` (lignes 76-88). |
| P2-9 | Trois sujets non liés, +801/−13 sur 8 fichiers, au-delà du seuil du guide | **CONFIRMED** | `git diff --shortstat beb57b5 73022bd` → `8 files changed, 801 insertions(+), 13 deletions(-)`, chiffres identiques à l'audit. `config.yaml:61-64` : `auto_merge_denylist` contient bien `.github/workflows/**`. |
| P3-10 | `ETATS_EN_ATTENTE` défini, jamais utilisé | **CONFIRMED** | `grep -rn "ETATS_EN_ATTENTE" --include=*.py .` → une seule occurrence, la définition (`dashboard.py:53`). Le filtre réellement utilisé est `[a for a in audits if a["event"] != "AUDIT_ARCHIVED"]` (`:197`). |
| P3-11 | Sentinelle en texte libre dans un champ `timestamp` (`"— (fichier inbox, pas encore au ledger)"`), testée par `startswith("—")` ; assertion de test rendue inopérante par un `.replace()` qui ne retire jamais la sous-chaîne cherchée | **CONFIRMED** | `dashboard.py:103` et `:283` vérifiés verbatim. Sur l'assertion : `test_hermes_dashboard.py:82-83` fait `.replace("boucle(s) close(s)", "")` avant de tester `"CURSOR-aaa-clos" not in …` ; le motif retiré n'apparaît qu'à `dashboard.py:292` (compteur de boucles closes non listées), jamais concaténé à un identifiant d'audit — retirer cette sous-chaîne ne peut pas faire apparaître ni disparaître la sous-chaîne recherchée. Le `.replace()` est bien sans effet sur le résultat de l'assertion. |
| P3-12 | Le compteur « boucles closes : 7 » affiché au propriétaire inclut une fixture de test (`CURSOR-FIXTURE-full-auto-demo`) | **CONFIRMED** | Reconstruit indépendamment sur le worktree audité : dernier événement par `audit_id` sur `architecture/audit-ledger.jsonl` → 7 `AUDIT_ARCHIVED`, dont `CURSOR-FIXTURE-full-auto-demo` en premier de la liste. `dashboard.py:220` affiche bien ce compte brut sans l'exclure. |
| § 2 (tableau des 4 promesses) | Promesses du corps de PR vs état réel du diff | **PARTIAL — cohérent avec les verdicts ci-dessus, sous la même réserve d'accès API** | Les trois premières lignes du tableau recoupent exactement P2-5, P1-2 et P2-7 (déjà vérifiés ci-dessus). La quatrième (« le job refuse de pousser autre chose que ce seul fichier ») est confirmée : `hermes-dashboard.yml:84-89`, `git status --porcelain \| grep -v ' hermes/DASHBOARD.md$'` puis `exit 1` si non vide — placé avant le `git add`, comme décrit. |
| § 6 (CI du commit audité, tous verts) | Classification « verte », deux `SKIPPED` conformes | **NEEDS_OWNER / non vérifiable ici** | Dépend de l'état des checks GitHub Actions sur ce commit précis — inaccessible sans `GH_TOKEN` dans cet environnement de revue. Je ne conteste pas ce point faute de moyen de le contredire, mais je ne peux pas non plus le confirmer par ma propre mesure ; à confirmer côté propriétaire s'il souhaite une garantie indépendante avant fusion (la PR est déjà fusionnée dans `master` au moment de cette revue, donc la question est surtout rétrospective). |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **P1-3 — persister le ledger CI ?** L'audit pose lui-même la question sans
  trancher : faut-il que la CI committe `ci-budget-ledger.jsonl` (charge
  supplémentaire, secret d'écriture déjà présent) ou faut-il changer
  l'indicateur affiché pour dire « non mesurable dans ce mode
  d'authentification » plutôt que d'afficher un total qui restera à `0.0`
  pour toujours ? C'est un arbitrage produit, pas technique.
- **P2-9 — découpage.** L'audit propose 3 briefs distincts (indicateurs
  trompeurs / filtre anti-boucle / modèle de l'auditeur). Aucun P0 ne force
  une urgence ; le propriétaire peut aussi bien les fusionner en un seul
  passage s'il préfère limiter le nombre de PR sur `.github/workflows/**`.
- **P2-8 — faut-il un budget guard sur `pipeline-audit.yml` ?** C'est un
  choix de posture (Cursor Cloud est facturé différemment de Claude
  headless) que ni `docs/rules/full-auto-pipeline.md` ni cet audit ne
  tranchent — seulement `harness/pipeline/config.yaml` qui se contredit sur
  ce que le dépôt *croit* avoir déjà fait.
- **§ 6 (CI verte)** — voir tableau ci-dessus : à confirmer côté propriétaire
  avec accès `gh` si une garantie indépendante de l'état CI est souhaitée ;
  hors de portée de cette revue technique faute de jeton.

## 4. Synthèse

Sur 12 constats numérotés (P1-1 à P3-12) plus le tableau de promesses du
§ 2, **11 sont intégralement confirmés** par rejeu indépendant sur un
worktree du commit audité, souvent avec une preuve alternative à celle de
l'audit plutôt qu'une simple relecture de son texte (notamment P1-2, où la
contradiction « Rien » vs deux PR ouvertes est reconstruite depuis les
horodatages `git log` locaux, sans dépendre de l'appel `gh` que je ne
pouvais pas rejouer).

Un point mérite une réserve précise sans invalider sa conclusion : la
**Preuve 2 de P1-3** montre une sortie de `grep` qui ne se reproduit pas
telle quelle (la ligne citée ne contient pas la sous-chaîne cherchée) — une
transcription fautive dans l'audit, mais le fait qu'aucun workflow ne
committe `ci-budget-ledger.jsonl` reste établi par une autre voie
(inspection directe des étapes `git add` des deux workflows qui appellent
`ci_budget_guard.py record`).

Un seul point (**P2-7**) ne peut être que partiellement confirmé dans cet
environnement de revue : la logique de sélection du modèle dans
`pipeline-audit.yml` est vérifiée verbatim et correspond exactement à ce que
l'audit décrit, mais la preuve d'exécution live (liste de 99 modèles, run
CI `31586836026`) et le texte exact du corps de PR #27 sont hors de portée
sans `GH_TOKEN` ni accès à l'API GitHub — je m'abstiens de trancher plutôt
que de supposer qu'ils sont exacts.

**Aucun REFUTED.** Aucun des constats de fond ne s'effondre à la
reproduction ; l'audit est d'une fiabilité technique élevée, avec une seule
imperfection mineure et localisée (la preuve mal transcrite de P1-3) qui
n'affecte aucune de ses conclusions.

**Recommandation de traitement** : les trois briefs proposés (§ 7 de
l'audit) sont fondés sur des constats confirmés et peuvent être convertis
tels quels ou fusionnés, au choix du propriétaire (voir NEEDS_OWNER
ci-dessus). Rien ici ne bloque une décision ; cet audit peut passer à
l'étape suivante de la boucle (`/forge-audit-accept` ou `-reject`).
