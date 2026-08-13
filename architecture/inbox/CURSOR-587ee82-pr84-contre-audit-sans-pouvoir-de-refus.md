---
audit_id:                CURSOR-587ee82-pr84-contre-audit-sans-pouvoir-de-refus
auditor:                 cursor-cloud
target_branch:           master
target_commit:           587ee824c2ba5ba013887076cae9a8aa416cc560
created_at:              2026-08-13T13:05:00Z
audit_type:              pull-request-review
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Audit de la pull request #84 — un contre-audit qui ne peut pas dire non

Objet audité : [PR #84](https://github.com/PLiagre/ForgeHistory/pull/84)
« challenge: revue de l'audit CURSOR-29913c0-pr69-seuil-survie-non-borne ».

Méthode : les six lentilles de
[`architecture/review-guidelines.md`](../review-guidelines.md). Chaque constat
porte une sévérité P0–P3 et cite sa preuve (fichier + lignes, ou commande
rejouée avec sa sortie collée). Cet audit **ne prescrit rien** : il propose,
la décision reste à la boucle (`architecture/README.md`, ADR-0005/0006).

## 0. Identité de l'objet audité

| | |
|---|---|
| Auteur de la PR | `PLiagre` (contenu produit par `claude-challenger` headless, run 31693417136) |
| Diff | **1 fichier, +115 / −0** : `architecture/reviews/CLAUDE-CURSOR-29913c0-pr69-seuil-survie-non-borne.md` |
| Tête de branche | `1dc7d090a6ccd57a3f07c0f5de31b88fe8c55c7e` (branche `forge-bot/review-…-31693417136`) |
| Fusion | squash, 2026-08-13T12:49:51Z → commit `587ee824c2ba5ba013887076cae9a8aa416cc560` |

La tête de branche n'a pas survécu à la fusion (squash), donc le
`target_commit` de cet audit est le commit de fusion — seul SHA de cet état
présent dans l'historique de `master` (règle d'intégrité 4 du
`architecture/README.md`) :

```
$ git merge-base --is-ancestor 1dc7d09 origin/master && echo oui || echo non
non
$ git merge-base --is-ancestor 587ee82 origin/master && echo oui || echo non
oui
$ git log -1 --format='parents=%P%n subj=%s' 587ee82
parents=e0dcb4fb69e83e72f339295c296cd96241dfe7d7
 subj=challenge: revue CLAUDE-CURSOR-29913c0-pr69-seuil-survie-non-borne (…) (#84)
```

## 1. Classification de la CI du commit audité

**Un job est rouge** sur le commit de fusion ; tous les autres sont verts.

| Job | Résultat sur `587ee82` |
|---|---|
| `actionlint`, `gitleaks`, `schema`, `tests`, `sim-tests`, `f0-demo`, `invoke-cursor-auditor`, `orchestrate` | `success` |
| `cursor-scope`, `escalate-on-failure` | `skipped` |
| **`regenerate`** (hermes-dashboard) | **`failure`** |
| `Reconcile local Hermes state` | `queued` (×4, jamais parti) |

```
$ gh api repos/PLiagre/ForgeHistory/commits/587ee82…/check-runs \
    --jq '.check_runs[] | select(.conclusion=="failure") | .name + "\t" + .html_url'
regenerate	https://github.com/PLiagre/ForgeHistory/actions/runs/31701909809/job/94452854180
```

Sur la PR elle-même (`gh pr checks 84`), tout est `pass` sauf `cursor-scope`
(`skipping` : la branche est `forge-bot/*`, pas `cursor/*` —
`.github/workflows/audit-guard.yml:30`) et `Reconcile local Hermes state`
(`pending` au moment de la fusion). La cause du rouge est analysée en P2-1.

## 2. Ce que cette PR fait bien — vérifié, pas cru sur parole

La lentille 2 du guide demande une preuve rejouable plutôt qu'une
affirmation. J'ai donc rejoué **six** des affirmations de la revue, sur un
worktree détaché au commit qu'elle cible
(`git worktree add --detach /tmp/pr84 29913c0…`). **Les six reproduisent
exactement**, au chiffre près :

| Affirmation de la revue | Rejeu indépendant | Verdict |
|---|---|---|
| point 1 — `pytest sim/tests/ -q` → 35 tests verts | `35 passed in 2.13s` | exact |
| point 1 — SC6 : survie `0.765706`, seuil `0.7488888888888889`, 536 cellules affamées, 15 666 208 morts, 2 676 487 kg | sortie de `measure_sc6_013.py` identique ligne pour ligne | exact |
| point 3 — la survie sort de la fenêtre dès `N≥1600` (`0.747480` contre borne basse `0.748889`) | sonde écrite ici : `N=200 → 0.765706` … `N=1600 → 0.747480 (hors fenêtre)` … `N=3200 → 0.746808` | exact au 6ᵉ chiffre |
| point 5 — `SURVIE_MARGE_DERIVEE = 0.15111111111111114`, écart de 0,74 % à `0.15` | `0.15111111111111114` ; écart `0.740740740740764 %` | exact |
| point 7 — la formule est prescrite mot pour mot par le brief 013 | `brief.md:128` : `cell.food_deficit_kg = max(0.0, cell.food_deficit_kg × (1 - DEFICIT_RECOVERY_RATE_PER_TICK))` | exact |
| point 9 — aucune opération n'augmente `population` | `grep -n '\.population *=' sim/engine.py` → une seule ligne, `237: cell.population = max(0, cell.population - deaths)` | exact |

Autrement dit : **le texte de cette revue tient**. La revue dit aussi
franchement où elle n'a pas pu vérifier (point 2, classification CI). C'est
la discipline « preuve d'exécution » que le guide demande, et elle est tenue.

Les constats ci-dessous ne portent donc pas sur la véracité du document,
mais sur **ce que la machine en fait** — et sur le fait qu'un document aussi
soigné n'ait, structurellement, aucun pouvoir sur l'issue.

## 3. Constats

**Aucun P0.** Rien dans cette PR ne justifiait de bloquer sa fusion.

### P1-1 — La porte de contre-audit n'a qu'une seule issue possible ; elle n'a jamais rien changé

C'est le constat central, et il est **mesuré sur tout le corpus**, pas
déduit d'un cas.

La décision automatique lit les verdicts ligne par ligne
(`harness/audit_decision.py:270-300`) et n'a que trois branches : rejeter si
**toutes** les lignes sont `REFUTED` (ligne 271), approuver dès **une seule**
ligne `CONFIRMED` ou `PARTIAL` (lignes 270, 283-290), rejeter si seuls des
`NEEDS_OWNER` subsistent (292-300).

Or, sur les 18 contre-audits présents dans `architecture/reviews/` :

```
TOTAL sur tous les contre-audits : {'CONFIRMED': 141, 'PARTIAL': 22,
                                    'NEEDS_OWNER': 11, 'REFUTED': 3}
```

soit **3 lignes `REFUTED` sur 177** (1,7 %), concentrées dans 2 fichiers, et
**aucun contre-audit intégralement `REFUTED`**. La branche « rejeter » n'a
donc jamais pu s'ouvrir. Le registre le confirme :

```
$ # distribution des décisions dans architecture/audit-ledger.jsonl
{'AUDIT_APPROVED': 14}
{('AUDIT_APPROVED', 'policy:auto'): 12, ('AUDIT_APPROVED', 'owner'): 2}
```

**14 décisions, 14 approbations, 0 rejet.** La revue de la PR #84 est le cas
extrême de cette série : 16 lignes, 14 `CONFIRMED`, 1 `PARTIAL`,
1 `NEEDS_OWNER`, **zéro `REFUTED`** — donc `AUDIT_APPROVED` était acquis dès
la première ligne du tableau, quel que soit le contenu des quinze autres.

Ce que cela coûte : une invocation Claude payante par audit, plafonnée à
5 USD (`pipeline-challenge.yml:155`), dont la valeur décisionnelle mesurée
est nulle sur 14 passages. Et ce coût n'est même pas connu — le registre
budget est **vide** (`harness/pipeline/ci-budget-ledger.jsonl`, 0 octet
utile), l'étape de marquage post-hoc étant elle-même tolérante à l'échec
(`pipeline-challenge.yml:159-169`, `|| echo "::warning::…"`).

La littérature 2026 nomme exactement ce motif : une étape de vérification
qui confirme au lieu d'informer est la définition du *rubber-stamping*, et
elle est d'autant plus coûteuse que la vérification est le poste de dépense
dominant des architectures multi-agents (jusqu'aux deux tiers du budget de
jetons, ~59 % dans l'étude Tokenomics) [S3, S6, S5]. Le contre-argument
honnête existe et doit être posé : un taux de confirmation élevé peut
signifier que les audits sont simplement justes — les six rejeux de la
section 2 vont dans ce sens. Mais une porte dont **une seule** des issues
est mécaniquement atteignable ne peut pas, par construction, distinguer les
deux cas. C'est cela qui est signalé, pas la compétence du challenger.

> Preuve : `harness/audit_decision.py:270-300` ; comptage par
> `audit_decision.parse_point_verdicts` sur les 18 fichiers de
> `architecture/reviews/` ; `architecture/audit-ledger.jsonl` ;
> `harness/pipeline/ci-budget-ledger.jsonl` (vide).

### P1-2 — Les « points retenus » publiés sur `master` désignent des lignes de vérification, pas des défauts

Quatorze secondes après la fusion, le registre a publié :

```
"event": "AUDIT_APPROVED", "actor": "policy:auto",
"retained_points": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15]
```

Ces numéros sont les numéros de **lignes du tableau de la revue**, pas les
points de l'audit. Concrètement, parmi les quinze « points retenus » :

- la ligne 1 est le **résumé** de l'audit (« le double comptage est corrigé,
  gate ACCEPT, 35 tests verts ») ;
- la ligne 14 constate que **l'invariance et la conservation de la masse ont
  été rejouées avec succès** ;
- la ligne 15 s'intitule littéralement « **ce que cette PR fait bien** ».

La boucle a donc enregistré comme « à retenir » trois constats qui disent que
tout va bien. Ce n'est pas cosmétique : `harness/audit_convert.py:91-114`
recopie cette liste telle quelle dans le brief-graine (`- Points retenus :
…`). Le précédent est déjà sur disque —
`harness/queue/briefs/014-pipeline-contre-audit-porte/brief.md:11` porte
« Points retenus : 1, 2, …, 16, 18 », hérité du même mécanisme. Le prochain
brief issu de `CURSOR-29913c0` annoncera donc au Planificateur qu'il doit
traiter, entre autres, « ce que la PR fait bien ».

L'audit source, lui, numérote ses défauts `P1-1`, `P1-2`, `P2-1`… : il n'y a
**aucune correspondance** entre ces identifiants et l'index des lignes du
contre-audit, et rien dans la chaîne ne la rétablit.

> Preuve : `architecture/audit-ledger.jsonl` (ligne `AUDIT_APPROVED` du
> 2026-08-13T12:50:05Z) ; lignes 60, 73, 74 du fichier ajouté par la PR ;
> `harness/audit_convert.py:91-114` ;
> `harness/queue/briefs/014-pipeline-contre-audit-porte/brief.md:11`.

### P2-1 — Une course de poussée a laissé un job rouge sur `master`

Le job `regenerate` (hermes-dashboard) a échoué **sur le commit de fusion**,
pour une raison purement mécanique :

```
remote: Bypassed rule violations for refs/heads/master:
remote: - 5 of 5 required status checks are expected.
 ! [remote rejected] master -> master (cannot lock ref 'refs/heads/master':
   is at aa19906a57ea420b8e916e99ab8d8c5a084755a7
   but expected 587ee824c2ba5ba013887076cae9a8aa416cc560)
error: failed to push some refs to 'https://github.com/PLiagre/ForgeHistory'
```

Deux workflows ont voulu pousser sur `master` dans la même seconde :
`pipeline-orchestrate` (commit `aa19906`, ligne de registre + décision) et
`hermes-dashboard` (régénération du tableau de bord). Le second a perdu.

Le corps de la PR annonce précisément une sérialisation — « ouvertes une à
une après la fin complète du run `pipeline-orchestrate` de la précédente
(sérialisation contre le conflit de rebase du ledger) ». Cette sérialisation
protège le registre entre deux PR, mais **pas** les deux écrivains
concurrents déclenchés par une seule et même fusion. Le tableau de bord
d'Hermes est donc resté en retard d'un commit, sans qu'aucune alerte ne le
dise (`escalate-on-failure` est `skipped` sur ce commit).

Le fait que ces poussées contournent la protection de branche (« Bypassed
rule violations ») est un motif **déjà audité** (`CURSOR-48a5659`,
`CURSOR-7e5244b`) : cité pour situer la ligne de log, pas recompté ici.

> Preuve : journal du job 94452854180 (run 31701909809), étape « Commit and
> push (hermes) » ; `gh api …/check-runs` en section 1 ; corps de la PR #84.

### P2-2 — (récurrence) Le registre publie un compte de verdicts que la revue ne contient pas

La revue affirme, dans sa synthèse : « Sur 16 lignes de vérification, 14 sont
CONFIRMED […], 1 est PARTIAL ». C'est **exact**, je l'ai recompté. Le
registre, lui, a publié 85 secondes après la fusion :

```
"event": "AUDIT_CHALLENGED", "actor": "claude",
"verdicts": {"CONFIRMED": 18, "REFUTED": 1, "PARTIAL": 3, "NEEDS_OWNER": 4}
```

Un `REFUTED` est ainsi publié sur `master` pour un document qui n'en contient
**aucun**. J'ai localisé chaque écart : `harness/audit_review.py:127-134`
compte les mots dans **tout** le texte, alors que la décision, elle, lit la
colonne du tableau. Le `REFUTED` fantôme provient d'**une seule ligne** — la
ligne 11 du fichier, qui est la phrase d'en-tête recopiée du gabarit :

```
ligne  11 (hors colonne verdict) : Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.
ligne  71 (hors colonne verdict) : ## 3. Points à porter au propriétaire (NEEDS_OWNER)
ligne  94 (hors colonne verdict) : 16 lignes de vérification, 14 sont CONFIRMED avec preuve rejouée
```

Ce défaut est **déjà documenté deux fois** (`CURSOR-786ec32` pour la PR #74,
`CURSOR-4b6dcff` pour la PR #73). Je ne le recompte pas comme une
découverte ; l'**élément nouveau** est l'explication de sa persistance :

```
$ grep CURSOR-786ec32-pr74-verdicts-fantomes-au-registre architecture/audit-ledger.jsonl
$ grep CURSOR-4b6dcff-pr73-contre-audit-recompte-a-tort  architecture/audit-ledger.jsonl
   (aucune ligne, dans les deux cas)
```

Les deux audits qui signalent ce défaut n'ont **aucune ligne dans le
registre** : ils ne sont jamais entrés dans la boucle, donc jamais
contre-audités, jamais décidés, jamais convertis. Le défaut ne persiste pas
faute d'avoir été vu ; il persiste parce que le signalement lui-même n'est
pas consommé.

> Preuve : `architecture/audit-ledger.jsonl` ; `harness/audit_review.py:127-134`
> et `:174` ; `harness/audit_decision.py:75-78` ; lignes 11, 71, 94 du fichier
> ajouté par la PR.

### P3-1 — (récurrence) Le point 2 restera `PARTIAL` à chaque tour, par construction

La revue classe son point 2 `PARTIAL` avec cette raison : « pas de `GH_TOKEN`
dans cet environnement, `gh` échoue explicitement ». C'est exact et c'est
structurel : l'étape qui invoque Claude ne reçoit que trois variables
(`pipeline-challenge.yml:146-149` : `CLAUDE_CODE_OAUTH_TOKEN`,
`ANTHROPIC_API_KEY`, `AUDIT_ID`), alors que l'étape de publication, dans le
même job, en reçoit un (`:174`, `GH_TOKEN`). Le challenger ne pourra donc
**jamais** vérifier la classification CI d'un audit — quel que soit l'audit.

Motif déjà audité (`CURSOR-9e35764`, son P2-1) : signalé ici uniquement parce
qu'il produit, sur cette PR, la seule réserve du document.

### P3-2 — (récurrence) La porte `schema` était verte sans avoir validé un seul fichier du diff

`harness/audit_schema.py:26,92,98` ne lit que `architecture/inbox/CURSOR-*.md`.
Rejeu : `All 39 audit(s) valid.` — 39 fichiers qu'aucune ligne de cette PR ne
touche. Le contenu réel du diff (`architecture/reviews/**`) n'a donc reçu
aucune validation mécanique avant fusion ; la seule vérification structurelle
arrive **après**, dans `record_challenge`. Déjà relevé en P2-2 de
`CURSOR-786ec32` : cité, non recompté.

### P3-3 — Taille, découpage et intention : rien à redire

1 fichier, +115/−0, un seul objet. Très en deçà du seuil d'environ 400 lignes
où une relecture honnête décroche [S1, S2]. L'intention est lisible dans le
corps de la PR (origine, contenu, contrainte de sérialisation, cause de
l'ouverture manuelle). Aucune recommandation de découpage.

## 4. Ce que je n'ai pas vérifié

- Les points 4, 6, 8, 11, 12, 13 de la revue (sondes de mortalité, de
  récupération de déficit, d'adjacence dupliquée, de budget) : non rejoués,
  faute de budget d'appels. Les six points rejoués l'ayant tous été
  exactement, je n'ai **aucun élément** laissant penser qu'ils seraient
  faux — mais je ne l'affirme pas.
- Le transcript du run 31693417136 (coût réel de l'invocation) : non lu ;
  je m'appuie sur le fait que le registre budget est vide.
- Les réglages de protection de branche du dépôt (je constate le contournement
  dans le journal, je ne lis pas la configuration).

## 5. Briefs atomiques proposés

Trois au maximum, comme le veut le contrat. Ce sont des **propositions** ; la
conversion en brief appartient à la boucle, pas à cet audit.

- **B-1 (issu de P1-1)** — Faire en sorte que l'issue de la porte de
  contre-audit dépende du contenu du contre-audit. Le besoin, en termes
  observables : qu'il existe au moins une issue autre qu'`AUDIT_APPROVED`
  atteignable en pratique, et que le taux de confirmation du corpus soit un
  compteur suivi. La forme (seuil, échantillonnage, révision de la table de
  règles) est un arbitrage, pas un fait technique.
- **B-2 (issu de P1-2)** — Rétablir la correspondance entre les points d'un
  audit et les lignes de son contre-audit, pour qu'un « point retenu »
  publié sur `master` et recopié dans un brief-graine désigne un défaut et
  non une ligne de vérification.
- **B-3 (issu de P2-1)** — Empêcher deux écrivains automatiques déclenchés
  par la même fusion de se disputer `master`, afin qu'aucune fusion ne
  laisse un job rouge et un tableau de bord en retard.

## 6. Sources externes

| # | source | consulté le |
|---|---|---|
| S1 | The New Stack — *Move code review before the code* — <https://thenewstack.io/move-code-review-upstream/> | 2026-08-13 |
| S2 | Augment Code — *Reviewing AI-Generated Code: A Verification Discipline for the Loop* — <https://www.augmentcode.com/guides/reviewing-ai-generated-code> | 2026-08-13 |
| S3 | Howardism — *Is Human Review of AI-Authored Code Still a Real Control, or Already Rubber-Stamping?* — <https://www.howardism.dev/articles/human-review-real-control-or-rubber-stamp> | 2026-08-13 |
| S4 | arXiv 2607.14890 — *Proof-or-Stop: Don't Trust the Agent, Trust the Evidence — Loop Engineering for Verifiable Evidence-Gated Lifecycle Control* — <https://arxiv.org/html/2607.14890v1> | 2026-08-13 |
| S5 | tianpan.co — *Token Budget as Architecture Constraint* (2026-04-13) — <https://tianpan.co/blog/2026-04-13-token-budget-as-architecture-constraint> | 2026-08-13 |
| S6 | Fluid Attacks — *AI token economics and cost control* — <https://fluidattacks.com/blog/ai-token-economics-cost-control> | 2026-08-13 |
| S7 | MindStudio — *What Is the Dark Factory Approach to AI Agent Pipelines?* — <https://www.mindstudio.ai/blog/dark-factory-ai-agent-pipeline> | 2026-08-13 |

Ce que ces sources apportent, en une phrase chacune : S1/S2 fixent le seuil
au-delà duquel une relecture décroche et la primauté des portes mécaniques ;
S3 documente qu'une revue à haut volume devient un tampon si elle n'est pas
redessinée (échantillonnage, gate par compréhension) — c'est le cadre de
P1-1 ; S4 pose la règle « une affirmation ne fait pas avancer l'état, seule
une preuve fraîche liée à l'arbre source le fait » — c'est le cadre de P1-2
et P2-2 ; S5 et S6 chiffrent le coût de l'étape de vérification (jusqu'aux
deux tiers du budget de jetons ; ~59 % dans l'étude Tokenomics citée par S6),
ce qui rend une étape sans pouvoir décisionnel coûteuse et pas seulement
inutile ; S7 décrit la bascule « humain au-dessus de la boucle » et la
gouvernance par tolérances agrégées plutôt que par approbation unitaire.

## 7. Annexe — commandes rejouées

```
$ git worktree add --detach /tmp/pr84 29913c005d8e537fee1da307e098d443635243ac
$ cd /tmp/pr84 && python -m pytest sim/tests/ -q
35 passed in 2.13s

$ python harness/queue/briefs/013-sim-tick-nourrit-une-fois/deliverables/measure_sc6_013.py
cellules_affamees_monde_reel_re = 536
morts_cumules_monde_reel_re = 15666208
kg_transportes_monde_reel_re = 2676487
fraction_survie_monde_reel_re = 0.765706
  SEUIL_SURVIE_POPULATION_FRACTION = 0.7488888888888889
  satisfaite : True
TOUTES LES CONDITIONS SC6 SONT SATISFAITES.

$ python -c "from sim.constants import SURVIE_MARGE_DERIVEE; print(repr(SURVIE_MARGE_DERIVEE))"
0.15111111111111114
   écart relatif à 0.15 = 0.740740740740764 %

$ # sonde d'horizon écrite pour cet audit (World.from_g3(42), random.Random(42))
N=  200 survie=0.765706 borne_basse=0.748889 dans_fenetre=True
N=  400 survie=0.754826 borne_basse=0.748889 dans_fenetre=True
N=  800 survie=0.749715 borne_basse=0.748889 dans_fenetre=True
N= 1600 survie=0.747480 borne_basse=0.748889 dans_fenetre=False
N= 3200 survie=0.746808 borne_basse=0.748889 dans_fenetre=False

$ grep -n '\.population *=' sim/engine.py
237:        cell.population = max(0, cell.population - deaths)

$ python harness/audit_schema.py | tail -1
All 39 audit(s) valid.

$ # comptage des verdicts du fichier ajouté par la PR
compteur registre  (parse_verdicts, mots dans TOUT le texte) : {'CONFIRMED': 18, 'REFUTED': 1, 'PARTIAL': 3, 'NEEDS_OWNER': 4}
verdicts reels     (parse_point_verdicts, colonne du tableau): {'CONFIRMED': 14, 'PARTIAL': 1, 'NEEDS_OWNER': 1}
```
