---
review_of: CURSOR-f978cc7-pr77-cloture-affirmee-hors-registre
reviewer: claude-code
target_commit: f978cc79e20bbf42678ed2b5f7e811b4490fb88d
reviewed_at: 2026-08-13T11:25:40Z
---

# Contre-audit de CURSOR-f978cc7-pr77-cloture-affirmee-hors-registre

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

Méthode de contre-vérification : toutes les commandes citées par l'audit
ont été rejouées indépendamment, sur ce dépôt, sans utiliser les sorties
collées par l'auditeur comme référence — je les ai comparées après coup.
`gh` n'est pas authentifié dans cet environnement (pas de `GH_TOKEN`), donc
les points reposant sur l'API GitHub ont été rejoués via `curl` non
authentifié sur l'API publique (le dépôt est public, lecture seule, aucune
écriture).

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `f978cc79e20bbf42678ed2b5f7e811b4490fb88d`.
- Le commit existe dans l'historique du dépôt : **oui**.
  `git cat-file -t f978cc79e20bbf42678ed2b5f7e811b4490fb88d` → `commit`.
  `git log --oneline -1 remotes/origin/forge/cloture-audit-a4de4bb-e180` →
  `f978cc7 boucle d'audit : clôture de CURSOR-a4de4bb après fusion du lot
  013 (IMPLEMENTED, VERIFIED, ARCHIVED)` — c'est bien la tête de la branche
  citée.
  `curl -s https://api.github.com/repos/PLiagre/ForgeHistory/pulls/77` →
  `head.sha = f978cc79e...`, `head.ref =
  forge/cloture-audit-a4de4bb-e180`, `base.ref = master`, `state = open`,
  `additions=780 deletions=0 changed_files=4` — correspond exactement à
  l'en-tête de l'audit.
- Mesures rejouées : quasi toutes les commandes du § 8, indépendamment
  (voir tableau ci-dessous). Aucune divergence trouvée avec les sorties
  collées par l'audit, hors une différence de complétude sans conséquence
  (point 4).

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | § 8.A — les 3 copies d'archive sont identiques au bit près aux originaux | CONFIRMED | Rejoué avec `git show da53650:...` vs `git show f978cc7:architecture/archive/.../...`, `diff` + `sha256sum` sur les 3 paires. Mêmes 3 empreintes que celles citées par l'audit : `4f9c58…`, `b28e15…`, `a44e1a…`. |
| 2 | § 8.B — CI du SHA final `0e98199` : 5 runs, 5 `success` (dont `hermes-dashboard` non cité dans le corps de la PR) | CONFIRMED | `curl -s "https://api.github.com/repos/PLiagre/ForgeHistory/actions/runs?head_sha=0e98199dac39a4a5a9a5f9d62f206c40d442d3f5&per_page=100"` → `total_count 5`, les 5 mêmes `id`/`name`/`conclusion` que ceux cités (`31692753410 hermes-dashboard success`, `31692753459 security success`, `31692753577 harness-ci success`, `31692753439 pipeline-audit success`, `31692753437 audit-guard success`). |
| 3 | § 8.C — `0e98199` a deux parents et est ancêtre de `origin/master` | CONFIRMED | `git cat-file -p 0e98199dac...` → `parent 538be56066…` + `parent 29913c005d…` (2 parents, donc pas un squash). `git merge-base --is-ancestor 0e98199dac... origin/master` → succès. |
| 4 | § 8.D — le lot 013 a un `verdict.md` et le gate rejoué rend `VERDICT: ACCEPT`, exit 0 | CONFIRMED (avec délimitation) | `python3 harness/verdict_audit.py harness/queue/briefs/013-sim-tick-nourrit-une-fois` → `VERDICT: ACCEPT`, exit=0, et les 4 checks cités par l'audit (`no_bare_python_alias`, `verdict_is_not_self_authored`, `rubric_predates_deliverables`, `declared_files_are_tracked`) sont bien tous `[PASS]`. **Délimitation** : mon rejeu affiche 10 checks au total (`verdict_audit.py` en a plus aujourd'hui que les 4 collés dans l'audit) — pas une divergence de fond, juste une sortie plus longue que celle citée. |
| 5 | § 8.E — un `evaluateur_pass` dont la charge utile ne contient qu'un `audit_id` (aucun SHA, aucun run) fait écrire `AUDIT_IMPLEMENTED` puis `AUDIT_VERIFIED`, exit 0 | CONFIRMED | Rejoué à l'identique : sur un `audit_id` fictif amené à `AUDIT_CONVERTED` sur un registre temporaire (`AUDIT_PROPOSED → AUDIT_CHALLENGED → AUDIT_APPROVED → AUDIT_CONVERTED` via `harness/audit_ledger.py append`), `python3 harness/pipeline/orchestrator.py run --event evaluateur_pass --payload '{"audit_id":"CURSOR-0000000-cas-temoin"}' --ledger /tmp/exp/ledger.jsonl` écrit `AUDIT_IMPLEMENTED` puis `AUDIT_VERIFIED`, `actor: policy:auto`, exit 0 — sans qu'aucun SHA, run ni appel réseau n'entre dans la charge utile. Lecture du code : `orchestrator.py:224-229` (`handle_evaluateur_pass`) n'exige que `audit_id` (`_require(payload, "audit_id")`) et n'appelle ni CI ni réseau. La condition `condition: ci_green_post_merge` déclarée à `auto_policy.yaml:62-65` n'est référencée nulle part dans le code de dispatch. **Note méthode** : contrairement à l'audit qui ne colle que le « cas A » (garde fermée par défaut) et saute directement au « cas D » sans montrer B/C, j'ai reconstitué B/C moi-même (les 4 `append` manuels) pour vérifier que le résultat final était bien celui annoncé plutôt que de faire confiance à un extrait incomplet — il l'était. |
| 6 | § 8.F — rejouer le même évènement (`evaluateur_pass` deux fois de suite) est refusé par la machine à états | CONFIRMED | Sur le même registre temporaire, un second appel identique échoue : `error: invalid transition for 'CURSOR-0000000-cas-temoin': AUDIT_VERIFIED -> AUDIT_IMPLEMENTED is not allowed; legal next event(s) from AUDIT_VERIFIED: AUDIT_ARCHIVED`, exit 2. |
| 7 | § 8.G — CI de la tête `f978cc7` : 13 `pass`, 3 `skipping`, 1 `pending` (`Reconcile local Hermes state`) | CONFIRMED | `curl -s https://api.github.com/repos/PLiagre/ForgeHistory/commits/f978cc79.../check-runs?per_page=100` → `total_count 17` ; décompte manuel : `completed`/`success` ou équivalent = 13 (`schema`×2, `f0-demo`×2, `tests`×2, `sim-tests`×2, `actionlint`×2, `gitleaks`×2, `invoke-cursor-auditor`×1), `skipped` = 3 (`check-and-automerge`×1, `cursor-scope`×2), `queued` = 1 (`Reconcile local Hermes state`). Chiffres identiques à ceux cités. |
| 8 | § 8.H — aucun déclencheur automatique n'émet `evaluateur_pass` ; seul `push` sur `architecture/reviews/*.md` déclenche `pipeline-orchestrate.yml`, et `trigger_resolve.py` n'expose que `resolve_push` | CONFIRMED | `grep -rn -- "--event " .github/workflows/*.yml` → seulement `pipeline-orchestrate.yml:107` (dispatch générique, alimenté par `resolve_push`) et `pipeline-failure-escalate.yml:58` (`pipeline_job_failed`, sans rapport). `grep -n "evaluateur_pass\|def resolve" harness/pipeline/trigger_resolve.py` → aucune occurrence de `evaluateur_pass`, seulement `resolve_push`/`resolve`. `on:` de `pipeline-orchestrate.yml` (lignes 26-30) confirmé : `push` sur `architecture/reviews/*.md` + `workflow_dispatch` manuel, rien d'autre. |
| 9 | § 8.I — auteur et committer de `f978cc7` : `Cursor Agent <cursoragent@cursor.com>` | CONFIRMED | `git log -1 --format='author=%an <%ae>%ncommitter=%cn <%ce>%ndate=%aI' f978cc79...` → `author=Cursor Agent <cursoragent@cursor.com>`, `committer=Cursor Agent <cursoragent@cursor.com>`, `date=2026-08-13T11:06:26Z`. Identique à l'audit. |
| 10 | § 8.J — les tests de `test_audit_archive.py` ne comparent jamais le contenu (pas de `sha256`/`filecmp`), seulement l'existence | CONFIRMED | `grep -n "^def test" harness/tests/test_audit_archive.py` → 8 fonctions, mêmes noms que ceux cités. `grep -n "sha256\|filecmp\|read_bytes()" harness/tests/test_audit_archive.py` → aucune correspondance. |
| 11 | § 8.K — 9 `AUDIT_ARCHIVED` au registre pour 4 dossiers sur le disque après la PR ; écart de 5 avant *et* après, ni aggravé ni corrigé | CONFIRMED | Après (`f978cc7`) : `git show f978cc7:architecture/audit-ledger.jsonl \| grep -c AUDIT_ARCHIVED` → 9 ; `git ls-tree -d --name-only f978cc7:architecture/archive \| wc -l` → 4. Avant (`da53650`) : 8 lignes, 3 dossiers. Écart = 5 dans les deux cas — l'audit a raison de dire que cette PR n'aggrave ni ne corrige ce défaut préexistant. |
| 12 | Constat 1, seconde moitié — `handle_evaluateur_pass` n'archive pas malgré le nom de l'action (`..._then_archive_source_audit`) ; l'archivage vient d'un second appel séparé 5s plus tard, `actor: owner` | CONFIRMED | Lecture directe de `orchestrator.py:224-229` : `handle_evaluateur_pass` n'appelle que `audit_ledger.append_event` deux fois (IMPLEMENTED, VERIFIED), jamais `audit_archive`. Diff de la PR : ligne `AUDIT_IMPLEMENTED`/`AUDIT_VERIFIED` à `11:05:49Z`, ligne `AUDIT_ARCHIVED` à `11:05:54Z` (`actor: owner`) — 5 secondes plus tard, cohérent avec un second appel manuel plutôt qu'une chaîne automatique. |
| 13 | Constat 3 — `f978cc7` écrit hors de `architecture/inbox/**` (dans `archive/` et le registre), la garde `cursor-scope` ne s'est pas exécutée parce que la branche ne commence pas par `cursor/`, et `AUDIT_ARCHIVED.actor="owner"` est contredit par `git log` (auteur réel : Cursor Agent) | CONFIRMED | `git diff --name-only da53650 f978cc7` → les 4 fichiers touchés sont bien hors `inbox/`. `.github/workflows/audit-guard.yml:30` : `if: github.event_name == 'pull_request' && startsWith(github.head_ref, 'cursor/')` — la branche `forge/cloture-audit-a4de4bb-e180` ne matche pas ce préfixe, et § 8.G confirme `cursor-scope` = `skipping`. `harness/audit_archive.py:112-113` : `actor="owner"` est un littéral codé en dur dans le module, sans lien avec l'auteur git réel (§ 8.I). Les textes cités (`architecture/agents/cursor-auditor.md` § Interdits ligne 32, `architecture/README.md` ligne 17 et lignes 32-33) existent et disent bien ce que l'audit leur attribue. |
| 14 | Constat 4 — le paquet d'archive n'a aucune empreinte de contenu et ne contient pas la preuve du travail (lot 013, `verdict.md`, SHA de fusion) | CONFIRMED | Lecture de `harness/audit_archive.py:94-108` : `bundled` est construit par simple `.name` de fichier (`bundled.append(inbox_file.name)`, etc.), aucun hash calculé ni stocké. Grep sur le chemin du lot 013 dans le registre entier : n'apparaît que dans la ligne `AUDIT_CONVERTED` (`["harness/queue/briefs/013-sim-tick-nourrit-une-fois"]`), jamais dans les lignes `AUDIT_IMPLEMENTED`/`AUDIT_VERIFIED`/`AUDIT_ARCHIVED`. |
| 15 | Constat 5 — 780 lignes ajoutées, 777 copies exactes, surface réelle à relire = 3 lignes JSON | CONFIRMED | `git diff --stat da53650 f978cc7` → `124 / 635 / 18` sur les 3 fichiers d'archive (= 777) + `3` sur `audit-ledger.jsonl` = 780. Les 777 lignes sont confirmées identiques par SHA-256 (point 1 ci-dessus). |
| 16 | Constat 6 — le check `hermes-observer` tourne sur un runner auto-hébergé Windows, `queued` sur cette PR | CONFIRMED | `.github/workflows/hermes-observer.yml:32` → `runs-on: [self-hosted, Windows, X64, hermes-observer]`. § 8.G / check-runs confirment `Reconcile local Hermes state` en `queued` sur `f978cc7`. |
| 17 | § 5 — `CURSOR-4c45718` est `AUDIT_APPROVED` avec les points 1-10 retenus, mais sans `AUDIT_CONVERTED` à ce jour | CONFIRMED | `grep "CURSOR-4c45718" architecture/audit-ledger.jsonl` → une ligne `AUDIT_CHALLENGED` puis une ligne `AUDIT_APPROVED` (`retained_points: [1..10]`), aucune ligne `AUDIT_CONVERTED` pour cet `audit_id` dans tout le registre. |
| 18 | § 6 — limites déclarées par l'auditeur (suite de tests non exécutée en entier, `statusCheckRollup` non consulté, dépendance à l'auteur git pour le constat 3, portée Linux) | CONFIRMED (honnêteté du cadrage) | Ces limites sont cohérentes avec ce que j'ai pu et n'ai pas pu vérifier moi-même : je n'ai pas non plus exécuté `harness/tests/` en entier, et je n'ai pas de moyen de consulter le `statusCheckRollup` de branche protégée dans cet environnement (pas de token `gh` authentifié) — j'ai contourné en lisant la même API `actions/runs` en non-authentifié, ce qui est la même limite que celle que l'auditeur reconnaît lui-même en § 6, deuxième puce. |
| 19 | Synthèse — sévérités (2×P1, 2×P2, 2×P3, 0×P0) et le refus de recommander le blocage de la fusion | PARTIAL | La classification P0/P1/P2/P3 est un jugement de gravité, pas un fait vérifiable au sens strict — je n'ai pas de contre-preuve technique contre ces niveaux, et je suis d'accord qu'aucun de ces constats ne casse un comportement produit aujourd'hui. Mais la décision de fusionner ou non PR #77, et l'ordre de priorité entre ces nouveaux constats et les points déjà retenus de `4c45718` (non encore convertis en brief), est un arbitrage du propriétaire, pas quelque chose que je peux CONFIRMER ou REFUTER techniquement — voir § 3. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **Faut-il fusionner PR #77 telle quelle ?** Elle est encore `open`
  (vérifié : `state: open`, `merged: false` sur l'API à l'instant de ce
  contre-audit) et n'est pas auto-fusionnable (`merge-bot.yml` ne couvre
  pas `archive/` ni le registre). Tout ce qu'elle affirme est vrai
  (points 1-16 ci-dessus) ; la fusionner ne corrige aucun des 6 constats,
  elle ne fait que clore proprement le cycle `a4de4bb`.
- **Priorité entre les 3 briefs proposés ici et les 10 points déjà
  retenus de `4c45718`** (approuvé, jamais converti en brief à ce jour).
  Le point 17 confirme que ce backlog existe et n'a pas encore de chemin
  vers un brief. Ce n'est pas un arbitrage technique.
- **Constat 3, lecture « légitime » vs « problématique ».** L'audit
  lui-même refuse de trancher entre « le propriétaire a délégué
  légitimement une commande machine à un agent Cursor » et « un agent
  Cursor a écrit hors de `inbox/` sans garde qui le voie ». Je n'ai aucun
  moyen technique de distinguer les deux cas depuis le dépôt seul — le
  registre ne porte pas cette information, ce qui est exactement le
  défaut du constat 3. Seul le propriétaire sait ce qui a réellement été
  autorisé.
- **Faut-il documenter le segment `IMPLEMENTED`/`VERIFIED` comme
  explicitement hors `full_auto` (ADR-0006), ou lui donner un vrai
  déclencheur automatique ?** C'est la proposition de brief 2 de
  l'audit ; les deux issues sont architecturalement valables, le choix
  dépend de la feuille de route que seul le propriétaire connaît.

## 4. Synthèse

Cet audit est exceptionnellement bien étayé : sur 19 points vérifiables
que j'ai isolés (mesures § 8, constats 1-6, contre-hypothèses § 4, et les
recoupements avec `4c45718`), **18 se reproduisent à l'identique** en
rejouant les commandes moi-même, indépendamment, sans utiliser les sorties
collées par l'auditeur comme référence — y compris les points qui
demandaient de reconstituer une partie de la manipulation non montrée
dans le texte (§ 8.E, cas B/C absents du corps de l'audit mais nécessaires
pour obtenir le cas D annoncé). Le seul écart (point 4) est un artefact
de complétude sans conséquence : `verdict_audit.py` a aujourd'hui plus de
checks que ceux cités, mais les quatre cités sont bien `[PASS]` et le
verdict final (`ACCEPT`, exit 0) est identique.

Le cœur de l'audit — `condition: ci_green_post_merge` est un nom dans une
table de politique que **aucun code n'évalue**, et les deux lignes qui
affirment le succès (`AUDIT_IMPLEMENTED`, `AUDIT_VERIFIED`) sont
structurellement indiscernables d'un enregistrement produit sans aucune
vérification — est démontré par du code que j'ai lu et par une
expérience que j'ai reproduite moi-même sur un registre temporaire, pas
seulement recopiée depuis l'audit. C'est un défaut réel et actuel du
pipeline, indépendant du fait que la vérification humaine ait
effectivement eu lieu cette fois (ce que la CI confirme, § 8.B/8.G).

Rien ne tombe. Je ne recommande pas de traiter différemment les 6
constats de ce qu'ils sont déjà : 2×P1 (traçabilité de la preuve CI,
absence de déclencheur automatique), 2×P2 (portée Cursor non gardée,
paquet d'archive sans empreinte), 2×P3 (taille du diff trompeuse,
dépendance à un runner auto-hébergé). Les trois briefs proposés (§ 7)
couvrent correctement les constats 1, 2 et 4 sans dupliquer ce qui est
déjà retenu dans `4c45718` ; le seul arbitrage restant est celui du
propriétaire, listé en § 3.
