---
audit_id:                CURSOR-0269d8e-hermes-console-droit-executer
auditor:                 cursor-cloud
target_branch:           master
target_commit:           0269d8e90231e554db356cbc57aea1f70bc3f507
created_at:              2026-08-12T12:33:05Z
audit_type:              architecture-and-qa
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---
# Audit du merge 0269d8e — ADR-0011 « Hermes console du propriétaire »

Audit post-fusion du rôle `cursor-auditor`
(`architecture/agents/cursor-auditor.md`), avec `cursor-qa-scout`
(`architecture/agents/cursor-qa-scout.md`) en compagnon de session : sa veille
est la section « Veille externe » ci-dessous, dans ce même fichier, comme son
contrat le prévoit.

**Un audit n'instruit rien.** Ce fichier est une *entrée* pour
`claude-challenger` puis pour le propriétaire (`architecture/README.md`, ADR-0005
/ ADR-0006). Aucun constat ci-dessous n'est une commande, et les trois flags
`*_authorized` du frontmatter valent `false`.

## Résumé en une page

Le merge fusionné est **documentaire** : quatre fichiers, +168/-8, aucune ligne
de code, aucun test, aucun workflow. Il enregistre une décision du propriétaire
(« ok pour tout ») et l'inscrit dans un ADR neuf.

Le fond de la décision est un **changement de nature d'un acteur** : Hermes
passe de « écrit dans un périmètre borné, n'exécute rien » à « exécute quatre
actions qui appartiennent au propriétaire », dont **fusionner une pull
request**. C'est le seul acteur du dépôt à obtenir une capacité d'écriture sur
GitHub, et c'est le seul dont aucun brief, aucun test et aucune porte mécanique
ne parle.

**Aucun P0.** La CI du SHA audité est verte, la suite de tests passe, le diff
est petit et son intention est lisible. Les constats portent sur l'écart entre
ce que l'ADR *promet* (« les conditions de fusion ne sont ni levées ni
affaiblies ») et ce que le dépôt *vérifie* aujourd'hui (rien, pour une PR de ce
type) — et la preuve la plus nette de cet écart est la fusion de cette PR
elle-même.

| sévérité | nombre | objet |
|---|---|---|
| P0 | 0 | — |
| P1 | 2 | clic de fusion délégué sans porte mécanique ; preuve « audit Cursor » trompeuse |
| P2 | 4 | Hermes hors du circuit des briefs ; paraphrase des quatre preuves ; ADR-0010 non amendé ; suites H2/H3 invisibles du tableau de bord |
| P3 | 3 | `pull_request_target` + jeton de fusion sur la même machine ; dépense Codex hors compteurs ; auto-audit du harnais mesuré à 20/24, pas 23/24 |

## Ce que le merge change

`git diff --stat 27aaf2a..0269d8e` (premier parent) :

```
 ROADMAP.md                                         |  23 ++--
 docs/adr/0011-hermes-console-du-proprietaire.md    | 120 +++++++++++++++++++++
 hermes/README.md                                   |  11 ++
 ...NDE-20260812-hermes-tableau-de-bord-pilotage.md |  22 +++-
 4 files changed, 168 insertions(+), 8 deletions(-)
```

1. **`docs/adr/0011-...md` (neuf)** — Hermes peut exécuter quatre actions sur
   ordre explicite : fusionner/refuser une PR, poser/retirer `pipeline/pause`,
   déclencher `pipeline-forge-run`, déposer une demande. Garde-fous annoncés :
   confirmation conversationnelle, PAT fine-grained minimal, trace dans
   `hermes/reports/`, interdits d'ADR-0010 maintenus, tableau local resté sur
   `127.0.0.1`.
2. **`ROADMAP.md`** — les secrets CI passent de « à faire » à « fait le
   2026-08-12 » ; une étape 3 « Hermes tableau unique » (H1→H4) est insérée et
   la numérotation suivante décalée.
3. **`hermes/README.md`** — une section « Ce qu'Hermes peut exécuter (ADR-0011) »
   pointe vers l'ADR.
4. **`hermes/requests/DEMANDE-20260812-...md`** — `status: OPEN` →
   `REFLECTED_IN_ROADMAP`, plus une section « Décision du propriétaire ».

Lentille 1 du guide de critique (intention avant diff) : l'intention est
explicite, traçable (demande → décision → ADR → roadmap) et le diff y
correspond. Lentille 5 (taille) : 4 fichiers / 168 lignes ajoutées, très en
dessous du seuil de ~400 lignes au-delà duquel une relecture honnête décroche.
Sur la forme, ce merge est exemplaire.

## État du dépôt et CI au SHA audité

CI du commit audité — **verte**, six workflows sur l'événement `push` :

```
harness-ci        push  completed  success
audit-guard       push  completed  success
security          push  completed  success
pipeline-audit    push  completed  success
hermes-dashboard  push  completed  success
hermes-observer   workflow_run  completed  success  (x8, vagues 12:25→12:30)
```

Suite de tests du harnais rejouée sur un checkout propre du SHA :

```
$ .venv/bin/python -m pytest harness/tests/ -q
309 passed, 16 skipped in 16.89s
```

Gate de schéma des audits :

```
$ python3 harness/audit_schema.py
All 11 audit(s) valid.
```

Auto-audit du harnais : `SCORE: 20/24`, deux `[FAIL]` (voir P3-3).

## Constats

### P0 — aucun

Rien dans ce merge ne bloque la fusion *a posteriori* : pas de code, pas de
test affaibli, pas de dépendance inventée, CI verte, intention lisible. Je le
dis explicitement pour ne pas laisser croire qu'un audit doit produire un P0
pour être utile.

### P1-1 — le clic de fusion est délégué à un agent alors que les quatre preuves qu'il doit vérifier ne sont exécutées par personne

ADR-0011 affirme (lignes 78-86 du fichier neuf) que les conditions de fusion
« (CI verte, gate ACCEPT, verdict d'un acteur différent du producteur, audit
Cursor) ne sont ni levées ni affaiblies », et charge Hermes de « refuser
d'exécuter une fusion si une preuve manque ».

Preuves que rien n'exécute ces quatre lectures aujourd'hui :

- `docs/rules/conditional-merge-gate.md` lignes 1-4 : « État au 2026-08-11 :
  **spécifiée, non câblée**. Aucun workflow ne lit ce document » ; lignes 57-61 :
  « L'activation exige un lot ultérieur ». C'est le seul endroit du dépôt où les
  quatre prédicats sont définis précisément.
- `.github/workflows/merge-bot.yml` ligne 27 :
  `if: startsWith(github.head_ref, 'cursor/') || startsWith(github.head_ref, 'forge-bot/')`.
  La PR auditée venait de `forge/hermes-decision-adr-0011-c2dd` : son job
  `check-and-automerge` est `SKIPPED` dans le rollup de la PR #34. Pour une
  branche `forge/*` ou `codex/*`, la denylist du merge-bot — présentée par
  ADR-0011 lui-même comme « la seule barrière réelle » — ne s'applique jamais.
- La fusion auditée : PR #34 créée `2026-08-12T12:24:28Z`, fusionnée
  `2026-08-12T12:25:24Z` par `PLiagre`, **56 secondes**, `reviews: []`. Aucun
  fichier de `architecture/inbox/` ne porte `target_commit` égal au SHA de tête
  `bb8fe11...` (`rg -l "target_commit:.*(bb8fe11|0269d8e|27aaf2a)"
  architecture/inbox/` → aucun résultat). Le prédicat 4 de la porte que l'ADR
  invoque était donc **faux au moment de la fusion**.

Le risque n'est pas que le propriétaire ait mal décidé : il a décidé, c'est son
droit. Le risque est que la seule chose qui tenait la porte pour une PR hors
allowlist était **la lenteur d'un humain qui clique**, et que ce merge remplace
cet humain par un agent conversationnel, sans rien mettre à la place. À l'état
de l'art 2026, la frontière de fusion est précisément celle qu'on garde
mécanique : les cinq agents recensés par [S1] routent tous leur sortie vers une
approbation avant fusion, [S2] montre que la garde doit vivre dans la branche
cible et la CI « pas dans une politique d'agent », et [S4] insiste sur des
règles déterministes exécutées « au niveau du runner, avant tout jugement LLM ».

### P1-2 — un `invoke-cursor-auditor` vert ne prouve pas qu'un audit existe

`.github/workflows/pipeline-audit.yml` lignes 185-196 : le job `POST`e vers
`https://api.cursor.com/v1/agents`, affiche `cursor-auditor launched -- its
audit will arrive as a cursor/* PR` et se termine en succès. C'est un
déclenchement, pas un résultat : le job réussit même si l'agent échoue, part en
boucle ou ne dépose jamais rien.

Preuve : sur la PR #34, `invoke-cursor-auditor: SUCCESS` dans le rollup, et
pourtant aucun audit ne cible son SHA de tête (même commande que ci-dessus).
ADR-0010 appelle Cursor « le maillon **critique** de chaque PR » ; mécaniquement,
c'est aujourd'hui une notification non bloquante.

Pourquoi c'est un P1 et pas un P3 de documentation : ADR-0011 demande à Hermes
de vérifier « audit Cursor » avant de fusionner. Le signal le plus visible, et
le plus facile à lire pour un agent comme pour un humain pressé, est justement
la coche verte qui ne veut pas dire ça. C'est le motif « correction hallucinée »
de la lentille 6 du guide de critique, transposé à la CI : un succès affirmé,
non mesuré [S3].

### P2-1 — Hermes acquiert des capacités sans passer par le circuit qui les encadre pour tous les autres acteurs

Dans ce dépôt, la capacité d'un acteur arrive normalement par un brief, est
mesurée par un gate et jugée par un tiers. Pour Hermes, tout est arrivé par
ADR : ADR-0010 lui donne l'écriture, ADR-0011 l'exécution, et l'ADR précise que
le câblage « est de la configuration de l'installation locale — hors dépôt ».

Preuves de l'asymétrie :

- `rg -l "hermes" harness/queue/briefs/*/brief.md` → **aucun** brief ne
  mentionne Hermes ;
- le brief 010, livré et accepté, énonce en Non-Goal 4 (ligne 216) :
  « **Donner un droit d'écriture à Hermes.** Hermes reste en lecture seule. Son
  contrat d'écriture fera l'objet d'un brief distinct. » Ce brief distinct
  n'existe pas ; les deux ADR ont pris sa place ;
- comparaison interne : `architecture/inbox/**`, qui n'instruit rien et ne peut
  rien casser, est gardé par `harness/audit_schema.py` **et** le job
  `cursor-scope` d'`audit-guard.yml`. L'acteur qui va tenir un jeton capable de
  fusionner n'a, lui, aucun schéma, aucun test, aucun compteur.

Le rapport assurance / rayon d'action est donc inversé. [S3] formule la règle
correspondante : permissions, plafonds, journalisation et approbation humaine
doivent être implémentés **une fois dans la couche d'orchestration**, sinon ils
« dérivent en un trimestre » — et c'est la seule structure qui produit une piste
d'audit cohérente.

### P2-2 — ADR-0011 paraphrase les quatre preuves au lieu de pointer le fichier qui les définit, et la paraphrase a déjà dérivé

ADR-0011 énumère les quatre conditions en prose et ne cite jamais
`docs/rules/conditional-merge-gate.md` (`rg -n "conditional-merge-gate"
docs/adr/0011-*.md` → aucun résultat ; il parle de « la porte conditionnelle du
2026-08-11 » sans chemin). Trois éléments de la spécification manquent déjà dans
la paraphrase :

1. la spécification exige un `Forge-Brief: harness/queue/briefs/<id>/` unique
   dans le corps de la PR, absent de l'ADR ;
2. elle impose de **refaire** les quatre lectures juste avant la tentative, et
   de les invalider si le SHA de tête change ;
3. elle est **inactive**, ce que l'ADR ne dit pas.

`CLAUDE.md` (« Single Source of Instruction », `docs/rules/` « never paraphrased
elsewhere ») interdit exactement ce montage. Même motif, plus léger, dans
`hermes/README.md` lignes 43-52 : la section énumère les quatre actions et les
garde-fous tout en affirmant « ce fichier ne les paraphrase pas ». Deux listes
qui disent la même chose finiront par dire deux choses différentes.

### P2-3 — ADR-0010 reste `accepted` sans marque d'amendement, et la route documentée mène encore à sa version d'avant

ADR-0011 retire la prémisse portante d'ADR-0010 : « Aucun workflow n'exécute ce
que Hermes écrit » (ADR-0010 ligne 97), reprise telle quelle dans
`hermes/README.md` ligne 41. Or :

- `docs/adr/0010-...md` garde `**Status**: accepted` et ne mentionne pas 0011 ;
- `docs/adr/template.md` ligne 4 prévoit pourtant la case :
  `proposed | accepted | deprecated | superseded by ADR-NNNN` ;
- la table de routage de `CLAUDE.md` envoie `ROADMAP.md` et `hermes/**` vers
  « `hermes/README.md` + ADR-0010 » — ADR-0011 n'y figure pas.

Un lecteur (ou un agent) qui entre par la route documentée obtient donc la règle
d'avant le merge. Correction plausible : une ligne de statut dans ADR-0010 et une
mention dans la table de routage. Je ne propose pas de brief pour ça : ce n'est
pas un lot, c'est une correction d'une ligne, et la décision reste au
propriétaire.

### P2-4 — le travail créé par cette décision est invisible du seul endroit où le propriétaire est censé regarder

`hermes/README.md` désigne `hermes/DASHBOARD.md` comme « l'endroit où le
propriétaire regarde d'abord ». Sa section « Ce qui attend le propriétaire »
(`hermes/dashboard.py` lignes 225-239) ne lit que deux sources : les PR ouvertes
et les audits `AUDIT_APPROVED`. Elle ne lit jamais `hermes/requests/**`
(`rg -n "requests|reports|DEMANDE" hermes/dashboard.py` → une seule occurrence,
ligne 325, dans un texte d'explication).

Conséquence concrète de ce merge : la demande est passée à
`REFLECTED_IN_ROADMAP`, donc « traitée » du point de vue du cycle, alors que
H1 (branchement lecture), H2 et H3 (briefs à écrire par le CTO) et H4 (câblage)
restent à faire — uniquement en prose dans `ROADMAP.md`. Le tableau n'en dira
rien.

Je **ne propose pas** de brief pour ce point : la roadmap l'a déjà attribué au
CTO sous les étiquettes H2/H3 (« export machine du tableau de bord », « liste
d'attentes propriétaire exhaustive »). Le signaler suffit ; en faire un brief
serait un doublon avec du travail déjà planifié.

### P3-1 — `pull_request_target` et un jeton capable de fusionner sur la même machine : forme du « trio fatal », latent aujourd'hui

`.github/workflows/hermes-observer.yml` s'exécute sur `pull_request_target`,
sur un runner auto-hébergé persistant (`runs-on: [self-hosted, Windows, X64,
hermes-observer]`) qui est la machine du propriétaire, et transmet la charge
utile brute de l'événement à un script local
(`C:\Users\liagr\...\runner-event.ps1`). ADR-0011 place désormais sur cette même
machine un PAT `contents` + `pull-requests` + `actions`. On obtient les trois
pattes décrites par [S5] : contenu non fiable ingéré, données privées
accessibles, capacité d'action externe — la configuration où une instruction
glissée dans un titre ou un corps de PR devient une action ([S6] documente
« Comment and Control » : trois agents CI détournés par un simple titre de PR ;
[S7] documente la même chaîne via un PAT trop large).

Ce qui **atténue** franchement le risque aujourd'hui, et pourquoi je classe P3
et non P1 :

- le dépôt est **privé** (`gh repo view` → `"visibility":"PRIVATE"`,
  `isFork:false`) : pas de PR de fork d'un inconnu ;
- le workflow déclare des `permissions:` toutes en lecture ;
- il ne fait **aucun** `checkout` du code de la PR ;
- les seules interpolations sont `github.event_name` et `github.event_path`,
  ni l'une ni l'autre contrôlable par un auteur de PR.

À réévaluer si le dépôt s'ouvre, accueille un contributeur externe, ou si le
script local se met à traiter des champs textuels de l'événement comme des
consignes.

### P3-2 — la délégation à Codex CLI dépense en dehors de tous les compteurs existants

ADR-0011 tranche que « pour les analyses lourdes, Hermes délègue au Codex CLI
local ». Le dépôt mesure la dépense via `harness/backends/ledger.py` et
l'affiche dans la ligne « Dépense CI ce mois-ci » du tableau
(`hermes/dashboard.py`), alimentée par le ledger de coût. Les appels Codex
déclenchés par Hermes n'y entrent par aucun chemin : la ligne restera vraie sur
son propre périmètre et fausse comme image de la dépense réelle. L'état de l'art
place le plafond au niveau de l'orchestration, par session ou par exécution de
workflow, et distingue observer de **plafonner** ([S8], [S9], [S10]).

### P3-3 — l'auto-audit du harnais mesure 20/24 sur un checkout propre, là où `AGENTS.md` annonce 23/24 avec un seul échec connu

Mesuré au SHA audité :

```
$ python3 harness/harness_audit.py
[FAIL] (3 pt) fake_honest_demo_pair: missing: ['run_demo.log (has it been run?)']
[FAIL] (1 pt) no_premature_stub_content: unexpected files in stub dirs: [...]
SCORE: 20/24
```

Cause du premier échec : `.gitignore` ligne 7 ignore `*.log`, donc
`harness/demo/fake_brief_001/run_demo.log` n'existe dans **aucun** clone frais
(`git ls-files harness/demo/fake_brief_001/` ne le liste pas). Le critère n'est
donc satisfiable qu'après avoir lancé la démo localement. Ce n'est pas un défaut
du merge audité — c'est un écart entre un chiffre documenté et le chiffre
mesurable, du même genre que ceux que le harnais traque chez les autres. Je le
consigne parce que la règle de la maison est qu'un compteur se mesure, pas se
recopie.

## Veille externe — section `cursor-qa-scout`

Thème du cycle : **frontière de fusion et plafonds** (les deux axes que le merge
touche). Comparaison dépôt / état de l'art, sur deux des trois axes prévus par
le contrat du compagnon.

**Axe « portes de fusion / merge queues ».** L'état de l'art 2026 ne recommande
plus « jamais d'auto-fusion » mais une politique **par rayon d'impact**, câblée
dans la CI et la protection de branche, pas dans la politique de l'agent :
gate à la frontière de la branche cible [S2], échelle d'autonomie où l'infra
plafonne au rung « l'humain fusionne » [S4], approbation exigée à l'étape merge
chez les cinq agents industriels recensés [S1], et trois portes explicites — spec
liée, tests verts, diff relu par un propriétaire nommé pour les chemins
sensibles [S10]. Position du dépôt : il a **la meilleure moitié** de cela — une
denylist explicite, une spécification écrite des quatre prédicats
(`docs/rules/conditional-merge-gate.md`), une allowlist de chemins étroite, et
un `merge-bot.yml` que le test SC12 du brief 010 lit dans le fichier plutôt que
de recopier ses valeurs. Ce qui manque est le câblage : la protection de branche
est indisponible sur ce plan GitHub (constat du 2026-08-11, repris par le brief
010 et par ADR-0011 ; je n'ai pas pu le confirmer moi-même — l'API
`branches/master/protection` me répond `HTTP 403 Resource not accessible by
integration`, ce qui dit que **mon** jeton n'a pas le droit de lire ce réglage,
pas que la protection est absente), et la porte des quatre preuves reste
inactive. Le dépôt est donc, sur cet axe, **en avance sur la spécification et en
retard sur l'exécution** — et ADR-0011 creuse cet écart d'un cran en déplaçant
le clic.

**Axe « plafonds de coût ».** L'état de l'art distingue nettement *observer*
(traces, coût par tâche) de *plafonner* (interruption avant l'appel suivant) :
budget par session ou par exécution de workflow, coupe-circuits sur le taux de
dépense, plafonds évalués dans le chemin critique [S8], [S9], et cadrage « coût
par tâche réussie plutôt que par jeton » [S3]. Position du dépôt : il mesure
déjà bien (`harness/budget.py` avec ses seuils 100 / 130 / 160,
`harness/backends/ledger.py tokens`, plafond mensuel affiché au tableau) et le
brief 009 a donné un plafond à la dépense CI récurrente. Le point aveugle est
celui du P3-2 : un exécutant nouveau (Codex via Hermes) dépense hors de ces
compteurs.

**Doublons vérifiés — déclaration explicite.** J'ai lu les onze briefs de
`harness/queue/briefs/` et vérifié en particulier 006, 008 (les deux), 009 et
010, plus les statuts consignés dans `HANDOFF.md` :

- **P1-1 recoupe le brief 010, lot 010c, sans le dupliquer.** SC15 a *spécifié*
  la porte et interdit son activation (Non-Goal 2 : « ne touche pas » à
  `.github/workflows/`), et la spécification livrée dit elle-même que
  l'activation « exige un lot ultérieur ». Ce lot ultérieur n'existe pas et
  n'appartient à aucun brief ouvert : c'est la proposition 1 ci-dessous, pas une
  reprise de 010c.
- **P1-2 n'est couvert par aucun brief.** Le prédicat 4 de SC15 traite l'audit
  Cursor dans une porte *future* ; la nature trompeuse de la coche verte
  *aujourd'hui* n'est écrite nulle part.
- **P2-1, P2-2, P2-4, P3-2 : aucun doublon.** Aucun brief ne mentionne Hermes
  (`rg -l "hermes" harness/queue/briefs/*/brief.md` → vide), et le seul endroit
  qui prévoyait un brief pour son contrat est un Non-Goal du brief 010 déjà
  clos.
- **Écarté volontairement comme doublon** : la restitution des attentes du
  propriétaire dans le tableau de bord (P2-4) — déjà attribuée au CTO en H3 par
  la roadmap de ce merge même. Signalée, non proposée.

## Briefs proposés (3 au plus — ici 3)

Propositions, pas instructions : un brief ne naît que d'une conversion décidée
par le propriétaire, et c'est alors le brief qui devient la source unique.

**Proposition 1 — traduire en code la porte conditionnelle déjà spécifiée, en
refus visible et sans élargir aucune allowlist.** Périmètre : les quatre
prédicats de `docs/rules/conditional-merge-gate.md` deviennent une commande
lisible (par exemple `py harness/merge_gate.py --pr <n>`) qui répond
`ALLOW`/`REFUSE` avec, pour chaque prédicat, la preuve lue et sa source. Rendre
la commande utilisable par un humain, par Hermes et par un job — sans qu'aucun
`gh pr merge` ne soit ajouté dans ce lot. Intérêt : la promesse d'ADR-0011
(« refuser si une preuve manque ») cesse de reposer sur la mémoire d'un agent.
Découpage suggéré : la commande d'abord, son éventuel câblage ensuite, chacun
avec sa propre évaluation.

**Proposition 2 — donner à Hermes-exécutant le même niveau de preuve que ce que
le dépôt exige de ses auditeurs.** Périmètre : un format de trace d'action sous
`hermes/reports/` (par exemple `kind: action`, avec l'action, l'ordre reçu,
l'horodatage, la cible, le résultat), un validateur stdlib sur le modèle de
`harness/audit_schema.py`, et le compteur correspondant au tableau de bord. Ce
que ça referme : les quatre actions d'ADR-0011 laissent une trace vérifiable par
une machine, et non par la bonne volonté de l'installation locale. Ce lot ne
prend aucune décision sur le périmètre des actions — il rend seulement
observable celui que le propriétaire a déjà fixé.

**Proposition 3 — rendre non trompeuse la preuve « audit Cursor ».** Périmètre :
une commande qui répond, pour un SHA donné, s'il existe un audit
`auditor: cursor-cloud` avec `target_commit` exact et schéma valide (réutiliser
`harness/audit_schema.py`, ne pas le réimplémenter), et faire apparaître le
manque au tableau de bord plutôt que dans le nom d'un job de CI. Ce lot ne
modifie pas `pipeline-audit.yml` : il ajoute la lecture qui manque, pour que
« invocation lancée » et « audit déposé » ne se confondent plus.

## Sources externes

Recherche web du 2026-08-12. Chaque source porte son URL et sa date de
consultation ; les axes couverts sont ceux exigés par le contrat (pipeline de
développement autonome, orchestration d'agents en CI, budget de jetons).

| # | source | consulté le |
|---|---|---|
| S1 | Augment Code — *From Assisted to Autonomous: How Far Can the Engineering Loop Close?* (état de juillet 2026 ; la fusion reste la frontière approuvée par un humain chez Copilot, Devin, Claude Code, Sentry Seer, Datadog Bits) — <https://www.augmentcode.com/guides/autonomous-engineering-loop> | 2026-08-12 |
| S2 | Jon Roosevelt — *Gate the Boundary, Not Every Merge* (la garde vit dans la branche cible + protection de branche, pas dans la politique de l'agent) — <https://jonroosevelt.com/blog/gate-the-boundary-not-every-merge/> | 2026-08-12 |
| S3 | TechTIQ — *AI Orchestration: Enterprise Architecture Guide (2026)* (permissions, plafonds et approbation implémentés une fois dans la couche d'orchestration, sinon dérive en un trimestre ; coût par tâche plutôt que par jeton) — <https://techtiq.com/blog/ai-orchestration/> | 2026-08-12 |
| S4 | zolty.systems — *The autonomy ladder in practice: letting agents commit, then merge* (2026-07-24 ; règles CI déterministes avant tout jugement LLM, infra plafonnée au rung 2) — <https://blog.zolty.systems/posts/2026-07-24-autonomy-ladder-in-practice/> | 2026-08-12 |
| S5 | Agent Patterns Catalog — *Lethal Trifecta Threat Model* (interdit qu'un chemin d'exécution cumule données privées, contenu non fiable et canal d'action ; la vérification appartient à l'hôte, pas au prompt) — <https://www.agentpatternscatalog.org/patterns/lethal-trifecta-threat-model/> | 2026-08-12 |
| S6 | safeguard.sh — *Prompt Injection Attacks on AI Agents in CI/CD* (« Comment and Control », début 2026 : trois agents CI détournés par un titre de PR ; séparer l'étape qui lit du contenu non fiable de l'étape privilégiée) — <https://safeguard.sh/resources/blog/prompt-injection-via-ai-agent-cicd-workflow-tampering> | 2026-08-12 |
| S7 | DEV Community — *GitHub's Agentic Workflows Vulnerable to Indirect Prompt Injection via PATs* (« GitLost » : portée de PAT trop large = proxy pour l'attaquant ; restreindre au dépôt et aux permissions strictes) — <https://dev.to/kserude/githubs-agentic-workflows-vulnerable-to-indirect-prompt-injection-attacks-via-pats-mitigation-1aac> | 2026-08-12 |
| S8 | Waxell — *AI Agent Token Budget Enforcement [2026]* (plafond évalué dans le chemin critique de l'appel ; observer ≠ plafonner) — <https://waxell.ai/blog/ai-agent-token-budget-enforcement> | 2026-08-12 |
| S9 | AgentBudget — *Real-Time Cost Enforcement for AI Agents* (livre blanc v1 : enveloppe de budget par session, coupe-circuit, budgets imbriqués pour le multi-agents) — <https://agentbudget.dev/agentbudget_whitepaper_v1.pdf> | 2026-08-12 |
| S10 | stdub.org — *The Merge Gate* (2026-06-10 ; trois portes : spec liée, tests verts, diff relu ; propriétaire nommé sur les chemins sensibles) — <https://stdub.org/ai/technical/2026/06/10/The-Merge-Gate.html> | 2026-08-12 |

## Commandes rejouées

Toutes exécutées sur un checkout propre de `0269d8e9...` (branche
`cursor/audit-de-commit-master-e7e6`, aucune écriture hors
`architecture/inbox/`).

```
$ git show --stat --format='%H%n%ci%n%s%n%P' 0269d8e9...
0269d8e90231e554db356cbc57aea1f70bc3f507
2026-08-12 14:25:23 +0200
Merge pull request #34 from PLiagre/forge/hermes-decision-adr-0011-c2dd
27aaf2a4503f96f934ef0b6a3f0db95284fb30de bb8fe11b860f8383e5178994f35ca116f89da2fd
 4 files changed, 168 insertions(+), 8 deletions(-)

$ gh pr view 34 --json createdAt,mergedAt,mergedBy,reviews
createdAt 2026-08-12T12:24:28Z | mergedAt 2026-08-12T12:25:24Z | mergedBy PLiagre | reviews []
   checks: invoke-cursor-auditor SUCCESS | check-and-automerge SKIPPED | cursor-scope SKIPPED
           schema/tests/actionlint/f0-demo/gitleaks SUCCESS | "Reconcile local Hermes state" CANCELLED

$ rg -l "target_commit:.*(bb8fe11|0269d8e|27aaf2a)" architecture/inbox/
(aucun résultat)

$ rg -l "hermes" harness/queue/briefs/*/brief.md
(aucun résultat)

$ gh api repos/PLiagre/forgehistory/branches/master/protection
HTTP 403 -- Resource not accessible by integration
   (limite de mon jeton d'auditeur, pas une preuve d'absence de protection)

$ gh repo view --json name,visibility,isFork
{"name":"ForgeHistory","visibility":"PRIVATE","isFork":false}

$ .venv/bin/python -m pytest harness/tests/ -q
309 passed, 16 skipped in 16.89s

$ python3 harness/audit_schema.py
All 11 audit(s) valid.

$ python3 harness/harness_audit.py
SCORE: 20/24   (FAIL fake_honest_demo_pair 3 pt ; FAIL no_premature_stub_content 1 pt)

$ git ls-files harness/demo/fake_brief_001/ ; git check-ignore -v .../run_demo.log
run_demo.log non suivi ; .gitignore:7:*.log
```

## Ce que cet audit n'autorise pas

- Il n'autorise aucune implémentation, aucune modification de CI, aucun
  changement de code : les trois flags du frontmatter valent `false`.
- Il ne prononce ni `APPROVED` ni `REJECTED` : ce statut appartient au
  propriétaire, après le contre-audit de `claude-challenger`.
- Il ne remet pas en cause la décision « ok pour tout ». Le périmètre des quatre
  actions est un choix du propriétaire, enregistré. Les constats ci-dessus
  portent sur l'écart entre ce que les documents promettent et ce que le dépôt
  vérifie — pas sur l'opportunité de la décision.
- Il ne touche ni ne réécrit aucun audit existant : `inbox/` est append-only,
  ce fichier est neuf.
