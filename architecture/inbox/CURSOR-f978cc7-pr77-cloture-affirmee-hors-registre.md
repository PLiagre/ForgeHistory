---
audit_id:                CURSOR-f978cc7-pr77-cloture-affirmee-hors-registre
auditor:                 cursor-cloud
target_branch:           forge/cloture-audit-a4de4bb-e180
target_commit:           f978cc79e20bbf42678ed2b5f7e811b4490fb88d
created_at:              2026-08-13T11:22:00Z
audit_type:              pull-request-review
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Critique de la pull request #77 — clôture de l'audit CURSOR-a4de4bb

Audit de la PR [#77](https://github.com/PLiagre/ForgeHistory/pull/77)
(4 fichiers, +780 / −0, base `master` = `da53650`, tête `f978cc7`,
branche `forge/cloture-audit-a4de4bb-e180`).

Méthode : `architecture/review-guidelines.md` — six lentilles, sévérités
P0–P3, une preuve citée par constat. Rôle : auditeur en **lecture seule**.
Cet audit **n'instruit rien** et ne vaut pas décision
(`architecture/README.md`) : il propose, la boucle tranche.

Toutes les mesures ont été rejouées sur une copie de travail séparée, sur des
fichiers temporaires (`/tmp/exp/ledger.jsonl`) ou en lecture seule via
`git show`. Aucune écriture dans le dépôt audité. Les sorties sont collées
telles quelles au § 8.

## 0. Synthèse

**Tout ce que cette PR affirme est vrai. Presque rien de ce qu'elle affirme
ne reste dans le dépôt.**

J'ai cherché à faire tomber les affirmations de la PR une par une. Elles
tiennent :

- les trois copies d'archive sont **identiques au bit près** aux originaux
  (SHA-256 comparés, § 8.A) ;
- la CI du SHA final `0e98199` est **verte, 5 runs sur 5** — la PR en cite
  quatre, le cinquième (`hermes-dashboard`) est vert aussi (§ 8.B) ;
- `0e98199` est bien un **commit de fusion à deux parents** et un ancêtre de
  `master` : « pas de squash » et « mergé » sont exacts (§ 8.C) ;
- le lot 013 a bien un `verdict.md` et le gate rejoué rend
  `VERDICT: ACCEPT` (§ 8.D) : `AUDIT_IMPLEMENTED` est **matériellement
  fondé** ;
- le rejeu du même évènement est refusé par la machine à états : le journal
  n'est pas duplicable par accident (§ 8.F).

Ce qui ne tient pas est ailleurs. Les deux lignes que cette PR ajoute pour
affirmer le succès — `AUDIT_IMPLEMENTED`, `AUDIT_VERIFIED` — sont **nues** :
ni SHA de fusion, ni identifiant de run CI, ni chemin du lot ou du verdict.
La preuve que j'ai pu vérifier aujourd'hui existe dans la **description de
la PR**, qui n'est pas un fichier du dépôt. Après fusion, `audit-ledger.jsonl`
ne saura plus répondre à « verte sur quel SHA ? quel run ? quel lot ? ».

Et j'ai vérifié que le code ne peut pas le savoir non plus : la politique
déclare `condition: ci_green_post_merge` pour cette règle, et **aucune ligne
de code n'évalue cette condition**. J'ai produit, avec une charge utile qui
ne contient qu'un `audit_id` et rien d'autre, deux lignes de journal
structurellement identiques à celles de cette PR (§ 8.E). La vérification
de CI qui a bien eu lieu ici est donc une **habitude humaine**, pas une
propriété du système : elle n'est pas reproductible et ne survit pas à la
fusion.

Deux constats P1, deux P2, deux P3. Aucun P0 : rien ici ne casse un
comportement produit, et je ne recommande pas de bloquer la fusion sur des
motifs qui préexistent à cette PR.

## 1. Intention avant diff (lentille 1)

La description de la PR est lisible et honnête, et c'est important de le
dire : elle nomme **un seul objet** (« Clôture post-fusion du cycle
`CURSOR-a4de4bb` »), énumère ce que contient la PR, cite ses preuves de CI
avec des liens, et **liste ses propres réserves non corrigées** (acteurs de
journal codés en dur, conversions différées). Elle applique explicitement le
constat 7 de `CURSOR-4c45718` (« un objet par PR ») en refusant d'embarquer
les conversions des quatre audits approuvés le matin même.

Le diff correspond à l'intention : 4 fichiers, +780/−0, aucune suppression,
aucun chemin de code, de test ou de workflow.

```
124 0 architecture/archive/CURSOR-a4de4bb-.../CLAUDE-CURSOR-a4de4bb-....md
635 0 architecture/archive/CURSOR-a4de4bb-.../CURSOR-a4de4bb-....md
 18 0 architecture/archive/CURSOR-a4de4bb-.../DECISION-CURSOR-a4de4bb-....md
  3 0 architecture/audit-ledger.jsonl
```

Le problème d'intention n'est donc pas dans ce que la PR veut faire, mais
dans **où elle met sa preuve** : dans le corps de la PR, pas dans le
registre qu'elle est justement en train d'écrire. C'est le fond des constats
1 et 3.

## 2. Portes mécaniques d'abord (lentille 3) — classification de la CI

CI de la tête auditée `f978cc7` : **verte, sans échec**, mais **incomplète**
au sens strict (`gh pr checks 77`, § 8.G) :

| état | jobs |
|---|---|
| `pass` (13) | `tests`, `sim-tests`, `f0-demo` (harness-ci), `gitleaks`, `actionlint` (security), `schema` (audit-guard), `invoke-cursor-auditor` (pipeline-audit) — chacun sur deux runs |
| `skipping` (3) | `cursor-scope` ×2 (audit-guard), `check-and-automerge` (merge-bot) |
| `pending` (1) | `Reconcile local Hermes state` (hermes-observer), run `31694077059`, `queued` depuis `11:06:50Z` |

Aucun job rouge. Deux remarques que je porte en constats plutôt qu'ici :
`cursor-scope` — la seule garde qui vérifie la portée des écritures d'un
agent Cursor — **ne s'est pas exécutée** (constat 3), et le job
`hermes-observer` tourne sur un runner auto-hébergé Windows qui peut rester
en file indéfiniment (constat 6).

CI du SHA final `0e98199`, celle sur laquelle repose `AUDIT_VERIFIED` :
**5 runs, 5 `success`** (§ 8.B). Vérifiée, exacte.

## 3. Constats

### Constat 1 — la condition `ci_green_post_merge` n'est évaluée par aucun code ; les deux lignes ajoutées sont indiscernables de lignes écrites sans vérification (P1)

`harness/pipeline/auto_policy.yaml:62-65` déclare la règle utilisée par cette
PR :

```yaml
  - id: evaluateur_pass
    event: evaluateur_pass
    condition: ci_green_post_merge
    action: ledger_AUDIT_IMPLEMENTED_then_AUDIT_VERIFIED_then_archive_source_audit
```

Le code qui implémente cette règle, `harness/pipeline/orchestrator.py:224-229`,
n'exige qu'un `audit_id` :

```python
def handle_evaluateur_pass(payload: dict, *, ledger_path: Path, **_kw) -> dict:
    _require(payload, "audit_id")
    audit_id = payload["audit_id"]
    implemented = audit_ledger.append_event(audit_id, "AUDIT_IMPLEMENTED", ...)
    verified = audit_ledger.append_event(audit_id, "AUDIT_VERIFIED", ...)
    return {"action": "ledger_append_chain", "records": [implemented, verified]}
```

Il n'y a ni appel réseau, ni lecture d'un SHA, ni consultation d'un run :
`ci_green_post_merge` est un **nom dans une table de politique**, sans
évaluateur. Je l'ai démontré (§ 8.E) : avec la charge utile minimale
`{"audit_id": "CURSOR-0000000-cas-temoin"}`, sans aucun SHA ni run,
l'orchestrateur sort en code 0 et écrit deux lignes :

```json
{"timestamp": "2026-08-13T11:12:25Z", "audit_id": "CURSOR-0000000-cas-temoin", "event": "AUDIT_IMPLEMENTED", "actor": "policy:auto"}
{"timestamp": "2026-08-13T11:12:25Z", "audit_id": "CURSOR-0000000-cas-temoin", "event": "AUDIT_VERIFIED",    "actor": "policy:auto"}
```

À comparer aux deux lignes que cette PR ajoute :

```json
{"timestamp": "2026-08-13T11:05:49Z", "audit_id": "CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois", "event": "AUDIT_IMPLEMENTED", "actor": "policy:auto"}
{"timestamp": "2026-08-13T11:05:49Z", "audit_id": "CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois", "event": "AUDIT_VERIFIED",    "actor": "policy:auto"}
```

**Elles sont de forme identique.** Un lecteur du registre ne peut pas
distinguer une clôture où quelqu'un a vraiment regardé la CI (ce qui est le
cas ici) d'une clôture où personne n'a rien regardé.

**Élément nouveau par rapport à `CURSOR-4c45718` constat 3.** Ce motif est
déjà retenu par une décision enregistrée (`4c45718` est `AUDIT_APPROVED`,
points 1–10 retenus), donc je ne le recompte pas comme une découverte. Ce
qui est nouveau est propre à **cette** PR : elle affirme y répondre —
« *Réponse au constat 3 de `CURSOR-4c45718` : ne plus affirmer VERIFIED sans
consulter la CI* » — et elle y répond **réellement** dans le geste (les 5
runs de `0e98199` sont bien verts, je l'ai vérifié). Mais le correctif vit
dans la description de la PR, pas dans l'artefact. Deux conséquences
mesurables :

1. la description de PR n'est pas versionnée dans le dépôt : après fusion,
   la preuve n'est plus atteignable qu'en repassant par l'API GitHub ;
2. le geste n'est pas reproductible : la prochaine clôture pourra omettre la
   vérification sans qu'aucune différence n'apparaisse dans le registre.

C'est exactement le motif que la littérature 2026 sur les traces d'agents
identifie comme le point de rupture : un enregistrement doit **référencer
l'identifiant de la décision et de la politique**, pas se contenter de dire
« la politique est passée » [S1] ; et la preuve doit être produite « au
moment de l'exécution », pas reconstituée à côté [S2]. C'est aussi le motif
« correction hallucinée » de la lentille 6 : la seule affirmation non mesurée
est celle du succès.

Deuxième moitié du même écart, plus discrète : l'action déclarée par la règle
se termine par `_then_archive_source_audit`, mais `handle_evaluateur_pass`
**n'archive pas** — l'archivage a été fait ici par un second appel manuel à
`harness/audit_archive.py` (visible dans la ligne `AUDIT_ARCHIVED`, écrite
5 secondes plus tard, `11:05:54Z` contre `11:05:49Z`). La chaîne annoncée par
la politique est donc implémentée aux deux tiers, le tiers manquant étant
laissé à un geste humain.

### Constat 2 — aucun déclencheur n'émet `evaluateur_pass` : les deux transitions qui affirment le succès sont les seules sans chemin machine (P1)

Recensement de tous les évènements que la CI sait envoyer à l'orchestrateur
(§ 8.H) :

```
.github/workflows/pipeline-failure-escalate.yml:58: --event pipeline_job_failed
.github/workflows/pipeline-orchestrate.yml:107:      --event "${{ steps.resolve.outputs.event }}"
```

Et le seul déclencheur automatique de `pipeline-orchestrate.yml`
(lignes 26-30) :

```yaml
on:
  push:
    branches: [master]
    paths:
      - 'architecture/reviews/*.md'
```

`harness/pipeline/trigger_resolve.py` n'expose qu'un `resolve_push(changed_review_files, ...)` :
la résolution automatique ne connaît que le dépôt d'un contre-audit, donc
l'évènement `review_recorded`. **Aucun chemin automatique ne produit
`evaluateur_pass`.** Il ne reste que le `workflow_dispatch` manuel — ou, comme
ici, un appel CLI joué à la main dans une machine d'agent puis poussé en PR.

Conséquence architecturale, dans un pipeline dont la prémisse (ADR-0006,
`mode: full_auto`) est « sans humain dans la boucle » : le **dernier segment
du cycle est structurellement manuel**. Ce n'est pas un défaut de cette PR —
c'est ce qui l'a rendue nécessaire. Mais cela signifie que chaque clôture
future sera un geste humain de plus, et que la boucle ne se referme jamais
d'elle-même.

Trois éléments de contexte, tous vérifiés, qui montrent que la PR **a bien
joué le jeu** de ce chemin manuel :

- l'auteur git du commit est `Cursor Agent <cursoragent@cursor.com>`
  (§ 8.I) : la commande a tourné dans une machine d'agent, pas dans un job
  de CI ;
- la liste blanche de `merge-bot.yml:50` ne couvre que
  `architecture/inbox/`, `architecture/reviews/` et
  `harness/queue/briefs/*/feedback/` — ni `architecture/archive/`, ni le
  registre. La PR est donc **non auto-fusionnable par construction**, ce que
  sa description dit correctement (« À fusionner par le propriétaire ») ;
- `check-and-automerge` est bien `skipping` (§ 8.G).

La littérature d'orchestration d'agents formule la même chose autrement : un
processus durable doit **persister sa décision à la frontière de l'étape**,
sinon la reprise repasse par un jugement refait à la main [S3] ; et une étape
sans déclencheur n'est pas une étape orchestrée, c'est une étape espérée [S4].

### Constat 3 — un commit écrit par Cursor hors de `inbox/`, que la garde « Cursor » n'a pas vu passer, et un acteur de registre que le dépôt contredit (P2)

Trois faits, tous vérifiables, dont la conjonction est nouvelle.

**a)** L'auteur et le committer de `f978cc7` sont `Cursor Agent <cursoragent@cursor.com>`
(§ 8.I).

**b)** Ce commit écrit dans `architecture/archive/**` et dans
`architecture/audit-ledger.jsonl` — deux chemins hors de
`architecture/inbox/**`, la seule zone d'écriture autorisée à Cursor
(`architecture/agents/cursor-auditor.md` § Interdits ; `architecture/README.md`
§ « Cursor audite, il ne développe jamais »).

**c)** La garde qui existe précisément pour ça ne s'est pas exécutée.
`.github/workflows/audit-guard.yml:30` :

```yaml
    if: github.event_name == 'pull_request' && startsWith(github.head_ref, 'cursor/')
```

La branche s'appelant `forge/cloture-audit-a4de4bb-e180`, `cursor-scope` est
`skipping` (§ 8.G). **L'invariant « Cursor est en lecture seule » n'est donc
tenu que par le nom de la branche.**

Deux lectures sont possibles, et je ne prétends pas trancher :

- **lecture légitime** : le propriétaire a délégué à un agent l'exécution de
  commandes machine que `architecture/README.md` attribue justement à la
  « Machine (commande) » pour `archive/` et le registre. Rien d'interdit.
- **lecture problématique** : un agent Cursor a écrit hors de `inbox/` sans
  qu'aucune garde ne le voie, parce que le préfixe de branche ne
  correspondait pas.

Les deux lectures butent sur le même manque, et c'est là mon constat : **le
registre ne sait pas dire qui a exécuté la commande.** La ligne
`AUDIT_ARCHIVED` affirme `"actor": "owner"` alors que la seule trace
matérielle est un commit signé `Cursor Agent` ; les deux autres affirment
`"actor": "policy:auto"`, ce qui suggère le moteur de politique en CI alors
que la commande a tourné localement. Les valeurs sont codées en dur dans les
outils (`harness/audit_archive.py:112-113`, `orchestrator.py:227-228`), donc
elles seront identiques quel que soit l'exécutant.

**Ce que je ne recompte pas** : le codage en dur des acteurs est le constat 8
de `4c45718` (P3, retenu) et la garde indexée sur le nom de branche son
constat 4 (P1, retenu). Je n'ouvre pas de nouveau lot pour eux. L'élément
nouveau que j'apporte est leur **conjonction exhibée par cette PR** : c'est
un commit d'agent Cursor qui échappe à la garde Cursor, dans un registre qui
l'attribue au propriétaire. Le champ `actor` n'est pas seulement imprécis,
il est ici **factuellement contredit** par `git log`.

### Constat 4 — le paquet « gelé » ne contient pas la preuve de ce qui a été fait, et rien ne détectera sa dérive (P2)

`harness/audit_archive.py:94-108` regroupe exactement trois fichiers : l'audit
d'origine, le contre-audit, la décision. La ligne `AUDIT_ARCHIVED` les
énumère par **nom seulement** :

```json
"bundled": ["CURSOR-a4de4bb-...md", "CLAUDE-CURSOR-a4de4bb-...md", "DECISION-CURSOR-a4de4bb-...md"]
```

Deux manques.

**a) Aucune empreinte.** Le mot employé par la description de la PR est
« paquet gelé », et par le module « frozen snapshot ». J'ai vérifié la
conformité aujourd'hui, à la main, par SHA-256 (§ 8.A) — les trois copies
sont identiques aux originaux. Mais **rien ne le vérifiera demain** : la
ligne de registre ne porte aucune empreinte, et les huit tests de
`harness/tests/test_audit_archive.py` ne comparent jamais le contenu, seulement
l'existence des fichiers (§ 8.J : `test_archive_copies_not_moves` n'affirme
que `assert (inbox / f"{AID}.md").exists()`). Une divergence future entre
`archive/` et l'original passerait inaperçue. C'est le point que les travaux
2026 sur les traces d'agents traitent comme un invariant et non un supplément :
intégrité de chaîne de hachage et complétude de séquence font partie de la
politique d'enregistrement, pas de l'outillage [S2, S5].

**b) La preuve du travail n'est pas dans le paquet.** Le paquet gelé
contient la critique, le contre-audit et la décision — c'est-à-dire tout le
**débat** — mais rien de la **réalisation** : ni le brief 013, ni son
`verdict.md` signé `forge-evaluateur`, ni le SHA de fusion `0e98199`, ni les
identifiants des runs verts. Le chemin du lot n'apparaît qu'une seule fois
dans tout le registre, dans la ligne `AUDIT_CONVERTED`
(`["harness/queue/briefs/013-sim-tick-nourrit-une-fois"]`). Un dossier
d'affaire classée qui ne dit pas ce qui a été fait pour la refermer est un
dossier incomplet.

### Constat 5 — 780 lignes ajoutées dont 777 sont des copies : la lentille « taille » ne s'applique pas ici (P3, information)

La lentille 5 signale les diffs qui dépassent ~5 fichiers ou quelques
centaines de lignes. Sur le papier cette PR affiche +780. Dans les faits,
777 de ces 780 lignes sont des **copies exactes** de fichiers déjà présents
dans le dépôt, ce que j'ai prouvé par SHA-256 (§ 8.A). La surface réellement
à relire est de **3 lignes JSON**. Je signale donc explicitement que je
**n'émets pas** de recommandation de découpage : ce serait un faux positif
mécanique, et la PR respecte déjà « un objet par PR ».

Ce constat n'existe que pour éviter qu'un futur lecteur — ou un futur
auditeur automatique — conclue au dépassement de seuil sur le seul compteur
d'additions.

### Constat 6 — le critère « CI verte » dépend d'un poste Windows allumé (P3)

`AUDIT_VERIFIED` signifie « Mergé, CI verte sur le SHA final »
(`architecture/README.md`). Le critère est donc central pour toute clôture
future. Or un des checks qui apparaissent sur chaque PR ne peut pas conclure
sans le poste du propriétaire : `.github/workflows/hermes-observer.yml:32`

```yaml
    runs-on: [self-hosted, Windows, X64, hermes-observer]
```

Sur cette PR, le run `31694077059` est `queued` depuis `11:06:50Z` (§ 8.G).
Le job est en lecture seule et ne juge rien, donc il ne s'agit pas d'un
risque produit — mais il rend la phrase « la CI est verte » **indécidable**
sans convention écrite : verte au sens « aucun rouge », ou verte au sens
« tous les checks ont conclu » ? Cette PR a de fait choisi la première
lecture pour `0e98199` (où les 5 runs avaient conclu, donc la question ne se
posait pas). Elle se posera dès qu'une clôture s'appuiera sur une tête de PR.

Constat d'information : je ne propose pas de lot pour lui, seulement d'écrire
la convention là où le critère est défini.

## 4. Ce qui tient (cadrage adverse — résultats négatifs)

La lentille 4 demande de chercher où l'affirmation est fausse. Voici les
hypothèses que j'ai formées **contre** cette PR et qui sont tombées. Elles
comptent autant que les constats.

1. **« Les copies d'archive ne sont pas conformes. »** Fausse. Les trois
   paires ont le même SHA-256 (§ 8.A).
2. **« La CI citée n'est pas verte, ou un run rouge est passé sous silence. »**
   Fausse, et la PR est même **plus prudente que nécessaire** : elle cite
   4 runs, il y en a 5 sur `0e98199`, et le cinquième
   (`hermes-dashboard 31692753410`) est vert aussi (§ 8.B). C'est le point
   exact sur lequel `4c45718` avait trouvé un run rouge non regardé
   (`31682196140` sur `16ff5ac`) : cette fois, il n'y en a pas.
3. **« "Pas de squash" et "mergé" sont des approximations. »** Fausse.
   `0e98199` a deux parents (`538be56`, `29913c0`) et
   `git merge-base --is-ancestor` confirme qu'il est un ancêtre de
   `origin/master` (§ 8.C).
4. **« `AUDIT_IMPLEMENTED` est une affirmation creuse. »** Fausse. Le lot 013
   a un `verdict.md` signé `forge-evaluateur` et le gate rejoué rend
   `VERDICT: ACCEPT`, code de sortie 0 (§ 8.D). C'est la **traçabilité** qui
   manque (constat 1), pas le travail.
5. **« Un audit inventé pourrait être marqué VERIFIED. »** Fausse : la
   machine à états refuse la transition. `NONE -> AUDIT_IMPLEMENTED is not
   allowed` (§ 8.E, cas A). Il existe bien une garde, et elle est fermée par
   défaut.
6. **« Le rejeu du même évènement dupliquerait les lignes. »** Fausse :
   `AUDIT_VERIFIED -> AUDIT_IMPLEMENTED is not allowed` (§ 8.F). Le registre
   n'est pas duplicable par rejeu — c'est la propriété d'idempotence que la
   littérature d'orchestration réclame [S3, S4], et elle est ici obtenue par
   la machine à états plutôt que par une clé d'idempotence.
7. **« La PR embarque plusieurs objets. »** Fausse : un seul objet, 4
   fichiers, et les conversions sont explicitement différées.

## 5. Déjà retenu ailleurs — non recompté

Pour ne pas transformer la critique en bruit (guide § « pas de
rubber-stamping inverse »), voici ce que j'ai vu et **ne compte pas** comme
constat, avec le motif :

| observation | déjà couvert par | statut |
|---|---|---|
| Acteurs de registre codés en dur | `4c45718` constat 8 (P3) | retenu, différé au lot 014 par la PR elle-même |
| Gardes de portée indexées sur le préfixe de branche | `4c45718` constat 4 (P1) | retenu, proposition de lot 3 |
| `AUDIT_IMPLEMENTED`/`VERIFIED` sans pointeur de preuve | `4c45718` constat 3 (P1) | retenu, proposition de lot 2 — je n'en garde que la **facette nouvelle** (constat 1) |
| 9 `AUDIT_ARCHIVED` au registre pour 4 paquets sur le disque (§ 8.K) | `4c45718` constat 9 (P3) | retenu ; cette PR ajoute un de chaque, l'écart reste de 5, ni aggravé ni corrigé |
| La boucle ne mesure pas son propre coût | `4c45718` constat 10 (P3) | retenu ; rien de nouveau ici |

Précision utile pour la décision : `4c45718` est `AUDIT_APPROVED` avec les
points 1 à 10 retenus, mais **sans ligne `AUDIT_CONVERTED`** à ce jour. Les
correctifs retenus ne sont donc pas encore planifiés. Cette PR referme un
cycle en produisant exactement les artefacts que la boucle a déjà accepté de
corriger — ce n'est pas une faute, c'est un ordre de priorité que seul le
propriétaire peut arbitrer.

## 6. Limite de cet audit (à lire avant de s'en servir)

- Je n'ai pas exécuté la suite `harness/tests/` complète ; j'ai lu
  `test_audit_archive.py` et rejoué le gate du lot 013. Mon constat 4a porte
  sur **l'absence** d'assertion de contenu dans ce fichier de tests, pas sur
  un échec de test.
- Je n'ai pas vérifié l'état de la CI du SHA final via le `statusCheckRollup`
  de branche protégée, seulement via l'API `actions/runs?head_sha=…`. Si une
  règle de protection exige des checks qui n'apparaissent pas comme runs,
  mon § 8.B est incomplet dans ce sens précis.
- Le constat 3 repose sur l'auteur git du commit. Si le propriétaire a
  conduit lui-même l'agent, la « lecture légitime » s'applique — mon constat
  reste valide dans sa formulation (le registre ne permet pas de le savoir),
  mais son ton doit être lu comme une question de traçabilité, non une
  accusation de contournement.
- Mon expérience du § 8.E utilise un `audit_id` fictif sur un registre
  temporaire. Elle prouve ce que le code **accepte d'écrire**, pas ce qui a
  été écrit ici — la conformité de la ligne réelle est traitée séparément
  (§ 8.B/8.D).
- Cet audit est fait sur une machine Linux : `unity/**` est hors de portée
  et n'est de toute façon pas touché par cette PR.

## 7. Briefs atomiques proposés (3 au maximum — propositions, pas instructions)

Ce sont des **propositions**. Aucune n'est autorisée, aucune n'instruit quoi
que ce soit ; seuls le contre-audit puis la décision sont compétents.

1. **Évaluer la condition que la politique déclare, et refuser d'écrire
   sinon.** Couvre le constat 1. Périmètre : `condition: ci_green_post_merge`
   doit être vérifiée par du code avant l'écriture de `AUDIT_VERIFIED`, et la
   ligne écrite doit porter le SHA vérifié et les identifiants de runs.
   *Note d'honnêteté : ceci est une **extension** de la proposition 2 déjà
   retenue dans `4c45718` (« aucune transition d'état sans son pointeur de
   preuve »), pas un lot nouveau. La facette que j'ajoute est l'évaluation
   effective de la condition, distincte du simple enregistrement du
   pointeur.* Preuve rejouable disponible : un test qui appelle
   `evaluateur_pass` avec une charge utile sans SHA et attend un refus —
   rouge avant, vert après (mon § 8.E est le rouge).
2. **Donner un déclencheur à la clôture, ou écrire noir sur blanc qu'elle
   est manuelle.** Couvre le constat 2. Deux issues acceptables et
   opposées : soit un chemin automatique émet `evaluateur_pass` (par exemple
   sur fusion d'une PR portant un `verdict.md` accepté), soit
   `docs/rules/full-auto-pipeline.md` et ADR-0006 déclarent explicitement le
   segment `IMPLEMENTED`/`VERIFIED` comme **hors** du périmètre `full_auto`.
   Le défaut à corriger n'est pas « c'est manuel », c'est « c'est manuel
   alors que la documentation laisse croire le contraire ».
3. **Que le paquet d'archive porte l'empreinte de ce qu'il gèle et la preuve
   de ce qui a été fait.** Couvre le constat 4. Périmètre : empreinte
   SHA-256 par fichier dans la ligne `AUDIT_ARCHIVED`, contrôle mécanique de
   conformité archive/original, et inclusion (ou référence explicite) du lot
   et de son `verdict.md` dans le paquet.

Je ne propose **rien** pour les constats 3, 5 et 6 : le 3 est la conjonction
de deux points déjà retenus ailleurs, le 5 est une information destinée à
éviter un faux positif, le 6 demande une phrase de convention, pas un lot.

## 8. Commandes rejouées (sorties collées)

### 8.A — Conformité des copies d'archive

```
$ git show pr77:architecture/inbox/CURSOR-a4de4bb-....md      > /tmp/a1
$ git show pr77:architecture/archive/CURSOR-a4de4bb-.../CURSOR-a4de4bb-....md > /tmp/a2
$ diff /tmp/a1 /tmp/a2 && echo IDENTIQUE
IDENTIQUE
$ diff /tmp/b1 /tmp/b2 && echo IDENTIQUE     # contre-audit
IDENTIQUE
$ diff /tmp/c1 /tmp/c2 && echo IDENTIQUE     # décision
IDENTIQUE
$ sha256sum /tmp/a1 /tmp/a2 /tmp/b1 /tmp/b2 /tmp/c1 /tmp/c2
4f9c5814dd2d34cc5543b771b6a48ec8a533d8aae4e3366d645310f5148610e5  /tmp/a1
4f9c5814dd2d34cc5543b771b6a48ec8a533d8aae4e3366d645310f5148610e5  /tmp/a2
b28e15cd21188a787566fd8b9f7bb0cbf524d371fd6e0901094e90a536fc7602  /tmp/b1
b28e15cd21188a787566fd8b9f7bb0cbf524d371fd6e0901094e90a536fc7602  /tmp/b2
a44e1acaefff304fe35d5c39e9aa9e2f80f3820823f6be8e38e5022fc7f46ec2  /tmp/c1
a44e1acaefff304fe35d5c39e9aa9e2f80f3820823f6be8e38e5022fc7f46ec2  /tmp/c2
```

### 8.B — Tous les runs CI du SHA final `0e98199`

```
$ gh api "repos/PLiagre/ForgeHistory/actions/runs?head_sha=0e98199dac39a4a5a9a5f9d62f206c40d442d3f5&per_page=100"
total_count 5
31692753459 security               push  completed success
31692753437 audit-guard            push  completed success
31692753410 hermes-dashboard       push  completed success
31692753439 pipeline-audit         push  completed success
31692753577 harness-ci             push  completed success
```

(La PR cite les quatre premiers sauf `hermes-dashboard` ; celui-ci est vert
également. Aucun run rouge, aucun run omis qui contredirait la PR.)

### 8.C — Forme du commit de fusion et appartenance à `master`

```
$ git cat-file -p 0e98199dac39a4a5a9a5f9d62f206c40d442d3f5 | grep ^parent
parent 538be56066df48084b4e1989ff83e14d90375fab
parent 29913c005d8e537fee1da307e098d443635243ac
$ git merge-base --is-ancestor 0e98199dac...  origin/master && echo OUI
OUI: 0e98199 est un ancêtre de origin/master
```

### 8.D — Le lot 013 existe et le gate rejoué l'accepte

```
$ .venv/bin/python harness/verdict_audit.py harness/queue/briefs/013-sim-tick-nourrit-une-fois
[PASS] no_bare_python_alias: no bare `python` invocations found
[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 2 examined pair(s)
[PASS] rubric_predates_deliverables: rubric (2026-08-13 08:43:00) predates earliest deliverable
[PASS] declared_files_are_tracked: all 3 in-brief declared files are tracked

VERDICT: ACCEPT
exit=0
```

### 8.E — `evaluateur_pass` sans aucune preuve de CI dans la charge utile

Cas A, audit inexistant (la machine à états refuse — garde présente) :

```
$ .venv/bin/python harness/pipeline/orchestrator.py run --event evaluateur_pass \
    --payload '{"audit_id":"CURSOR-0000000-audit-totalement-invente"}' --ledger /tmp/exp/ledger.jsonl
error: invalid transition for 'CURSOR-0000000-audit-totalement-invente': NONE (no prior event
for this audit_id) -> AUDIT_IMPLEMENTED is not allowed; legal next event(s) from NONE:
AUDIT_CHALLENGED, AUDIT_PROPOSED, AUDIT_STALE
exit=2
```

Cas D, audit amené à `AUDIT_CONVERTED` sur un registre temporaire, puis
`evaluateur_pass` avec un `audit_id` **et rien d'autre** :

```
$ .venv/bin/python harness/pipeline/orchestrator.py run --event evaluateur_pass \
    --payload '{"audit_id":"CURSOR-0000000-cas-temoin"}' --ledger /tmp/exp/ledger.jsonl
{"action": "ledger_append_chain", "records": [
  {"timestamp": "2026-08-13T11:12:25Z", "audit_id": "CURSOR-0000000-cas-temoin", "event": "AUDIT_IMPLEMENTED", "actor": "policy:auto"},
  {"timestamp": "2026-08-13T11:12:25Z", "audit_id": "CURSOR-0000000-cas-temoin", "event": "AUDIT_VERIFIED",    "actor": "policy:auto"}],
 "event": "evaluateur_pass", "matched_rules": ["evaluateur_pass"]}
exit=0
```

Aucun SHA, aucun run, aucune requête réseau : deux lignes de succès écrites,
code de sortie 0.

### 8.F — Rejeu du même évènement (idempotence)

```
$ .venv/bin/python harness/pipeline/orchestrator.py run --event evaluateur_pass \
    --payload '{"audit_id":"CURSOR-0000000-cas-temoin"}' --ledger /tmp/exp/ledger.jsonl
error: invalid transition for 'CURSOR-0000000-cas-temoin': AUDIT_VERIFIED -> AUDIT_IMPLEMENTED
is not allowed; legal next event(s) from AUDIT_VERIFIED: AUDIT_ARCHIVED
exit=2
```

### 8.G — Checks de la tête auditée `f978cc7`

```
$ gh pr checks 77
actionlint                    | pass     | 10s | .../runs/31694076758/job/94427697516
actionlint                    | pass     |  9s | .../runs/31694052207/job/94427617285
check-and-automerge           | skipping |  0  | .../runs/31694076818/job/94427734139
cursor-scope                  | skipping |  0  | .../runs/31694052252/job/94427618360
cursor-scope                  | skipping |  0  | .../runs/31694076951/job/94427699129
f0-demo                       | pass     | 11s | ...
gitleaks                      | pass     | 10s | ...
invoke-cursor-auditor         | pass     | 19s | .../runs/31694076764/job/94427697513
Reconcile local Hermes state  | pending  |  0  | .../runs/31694077059/job/94427698479
schema                        | pass     | 11s | ...
sim-tests                     | pass     | 17s | ...
tests                         | pass     | 20s | ...
(13 pass, 3 skipping, 1 pending, 0 fail)

$ gh api repos/PLiagre/ForgeHistory/actions/runs/31694077059
hermes-observer queued None 2026-08-13T11:06:50Z 2026-08-13T11:06:50Z
```

### 8.H — Quels workflows émettent quels évènements d'orchestrateur

```
$ grep -rn "\-\-event " .github/workflows/*.yml
pipeline-failure-escalate.yml:58:  python harness/pipeline/orchestrator.py run --event pipeline_job_failed --payload "$payload"
pipeline-orchestrate.yml:107:       --event "${{ steps.resolve.outputs.event }}" \

$ grep -n "evaluateur_pass\|def resolve" harness/pipeline/trigger_resolve.py
136:def resolve_push(changed_review_files: list[str], *, ledger_path: Path = LEDGER_PATH) -> ResolveOutcome:
207:def resolve(
(aucune occurrence de `evaluateur_pass`)
```

### 8.I — Auteur du commit audité

```
$ git log -1 --format='author=%an <%ae>%ncommitter=%cn <%ce>%ndate=%aI' pr77
author=Cursor Agent <cursoragent@cursor.com>
committer=Cursor Agent <cursoragent@cursor.com>
date=2026-08-13T11:06:26+00:00
```

### 8.J — Ce que les tests d'archive vérifient

```
$ grep -n "^def test" harness/tests/test_audit_archive.py
64:def test_archive_rejected_bundles_all_three(tmp_path)
75:def test_archive_copies_not_moves(tmp_path)
82:def test_archive_verified_ok(tmp_path)
88:def test_archive_refuses_in_flight(tmp_path)
100:def test_archive_refuses_clobber(tmp_path)
107:def test_archive_advances_state(tmp_path)
113:def test_cli_archive_exits_zero(tmp_path)
125:def test_cli_archive_in_flight_exits_two(tmp_path)

$ grep -n "sha256\|filecmp\|read_bytes()" harness/tests/test_audit_archive.py
(aucune correspondance)
```

### 8.K — Écart registre / disque pour les archives

```
$ git show pr77:architecture/audit-ledger.jsonl | grep -c AUDIT_ARCHIVED
9
$ git ls-tree -d --name-only pr77:architecture/archive | wc -l
4
```

### 8.L — Chaîne d'états complète de l'audit clôturé

```
2026-08-13T08:40:11Z AUDIT_CHALLENGED  actor=claude       champs: review, verdicts
2026-08-13T08:40:11Z AUDIT_APPROVED    actor=policy:auto  champs: decision, reason, retained_points
2026-08-13T08:40:34Z AUDIT_CONVERTED   actor=owner        champs: briefs
2026-08-13T11:05:49Z AUDIT_IMPLEMENTED actor=policy:auto  champs: (aucun)
2026-08-13T11:05:49Z AUDIT_VERIFIED    actor=policy:auto  champs: (aucun)
2026-08-13T11:05:54Z AUDIT_ARCHIVED    actor=owner        champs: archive, bundled
```

Les deux seules lignes sans aucun champ de preuve sont les deux qui
affirment le succès.

## 9. Risques par sévérité

| # | sévérité | risque | preuve |
|---|---|---|---|
| 1 | **P1** | `condition: ci_green_post_merge` déclarée mais évaluée par aucun code ; les lignes de succès sont indiscernables de lignes non vérifiées, et la preuve réelle ne vit que dans la description de la PR | `auto_policy.yaml:62-65`, `orchestrator.py:224-229`, § 8.E |
| 2 | **P1** | aucun déclencheur n'émet `evaluateur_pass` : le dernier segment du cycle est structurellement manuel dans un pipeline documenté « sans humain » | § 8.H, `pipeline-orchestrate.yml:26-30`, ADR-0006 |
| 3 | **P2** | commit d'agent Cursor écrivant hors de `inbox/`, non vu par `cursor-scope` (garde indexée sur le nom de branche) ; ligne de registre attribuée à `owner` que `git log` contredit | § 8.I, `audit-guard.yml:30`, § 8.G, `audit_archive.py:112-113` |
| 4 | **P2** | paquet « gelé » sans empreinte et sans la preuve du travail (lot, verdict, SHA de fusion) ; aucune détection de dérive future | `audit_archive.py:94-108`, § 8.J, § 8.L |
| 5 | **P3** | +780 lignes dont 777 copies exactes : risque de faux positif « diff trop gros » pour un futur lecteur ou auditeur automatique | § 8.A, § 1 |
| 6 | **P3** | « CI verte » indécidable quand un check dépend d'un runner auto-hébergé en file d'attente | `hermes-observer.yml:32`, § 8.G |

Aucun **P0** : aucun comportement produit n'est cassé par cette PR, et les
faits qu'elle affirme sont vrais.

## 10. Sources externes

Recherche web menée le 2026-08-13 sur les trois thèmes imposés par
`architecture/agents/cursor-auditor.md` (« autonomous AI dev pipeline »,
« agent orchestration CI », « token budget LLM agents »).

| # | source | date de publication | consulté le |
|---|---|---|---|
| S1 | Zylos Research — *Agent Identity and Signed Provenance: Building Audit Trails for Autonomous Runtime Actions* — <https://zylos.ai/research/2026-04-25-agent-identity-provenance-signed-audit-trails> — « *Tool-call records should reference that decision ID rather than merely saying "policy passed"* » | 2026-04-25 | 2026-08-13 |
| S2 | IETF — *AI Agent Execution Profile of SCITT* (`draft-emirdag-scitt-ai-agent-execution-00`) — <https://datatracker.ietf.org/doc/draft-emirdag-scitt-ai-agent-execution/> — preuve « complète (omissions détectables), capturée au moment de l'exécution, inviolable » ; politique d'enregistrement = intégrité de chaîne de hachage + ordre temporel + complétude de séquence | 2026 (draft-00) | 2026-08-13 |
| S3 | *AI agents need more than job orchestration* (S. Moran) — <https://medium.com/@sean.j.moran/ai-agents-need-more-than-job-orchestration-43e493bb4749> — une décision doit devenir un **état enregistré** à la frontière de l'étape, sinon la reprise refait le jugement | 2026 | 2026-08-13 |
| S4 | *Agent Orchestration: The Distributed Systems Problem We Keep Ignoring* — <https://www.linkedin.com/pulse/agent-orchestration-distributed-systems-problem-we-keep-sarma-ypg7f> — clés d'idempotence obligatoires sur toute opération changeant l'état ; liste de contrôle avant production | 2026 | 2026-08-13 |
| S5 | IETF — *Verifiable AI Provenance Framework (VAP)* (`draft-kamimura-vap-framework-01`) — <https://datatracker.ietf.org/doc/draft-kamimura-vap-framework/> — les journaux applicatifs ordinaires n'offrent pas les garanties nécessaires à une vérification **indépendante** | 2026 (draft-01) | 2026-08-13 |
| S6 | Cockroach Labs — *Managing Agentic AI Costs at Scale* — <https://www.cockroachlabs.com/blog/agentic-ai-costs-at-scale/> — l'unité pertinente n'est plus le coût par requête mais le **coût par tâche achevée** (5 à 30× plus de jetons par tâche qu'un échange simple) | 2026 | 2026-08-13 |
| S7 | Multigrid — *Token Budgets: Designing a Prompt Around a Cost Ceiling* — <https://multigrid.ai/learn/token-budgeting> — le plafond doit être un cumul avec arrêt dur et **comportement d'arrêt défini** ; journaliser le budget restant à chaque tâche, pas seulement à l'épuisement | 2026 | 2026-08-13 |

S6 et S7 éclairent un point que je ne compte pas comme constat (déjà retenu :
`4c45718` constat 10) : cette clôture a consommé des jetons d'agent pour
écrire trois lignes, et le registre n'en garde aucune trace. La pratique 2026
consiste à journaliser le budget restant **à chaque tâche**, précisément pour
voir la dégradation avant l'incident [S7].

---

Fin de l'audit. `status: PROPOSED` — aucune autorisation d'implémentation,
de modification de CI ou de code n'est accordée ni implicite par ce document
(`architecture/README.md`, `architecture/agents/cursor-auditor.md`).
