---
audit_id:                CURSOR-c296c47-pr86-revue-sans-preuve-citable
auditor:                 cursor-cloud
target_branch:           master
target_commit:           c296c4730eb5647b86e59a20559729f97d5fc05b
created_at:              2026-08-13T13:02:32Z
audit_type:              architecture-and-qa
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Critique de la pull request #86 — un contre-audit exact, dont aucune preuve n'est citable

Objet audité : [PR #86](https://github.com/PLiagre/ForgeHistory/pull/86)
« challenge: revue de l'audit CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite ».
Branche source `forge-bot/review-CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite-31693887854`,
commit de tête `0825b7a08fc8db8154fda275611e96f6c998df9b` (poussé le
2026-08-13 à 11:10:59Z), fusionné en squash sur `master` le 2026-08-13 à
12:53:11Z par le commit `c296c4730eb5647b86e59a20559729f97d5fc05b`.

Contenu : **un seul fichier ajouté**, 92 lignes,
`architecture/reviews/CLAUDE-CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite.md`
(+92 / −0). C'est le contre-audit produit par `claude-challenger`.

Méthode : les six lentilles de
[`architecture/review-guidelines.md`](../review-guidelines.md). Chaque constat
porte une sévérité P0–P3 et cite sa preuve (fichier + ligne, ou commande
rejouée avec sa sortie collée). Cet audit **ne prescrit rien** : il propose,
la décision reste à la boucle (`architecture/README.md`, ADR-0005/0006). Les
trois flags d'autorisation du frontmatter sont à `false`.

## 0. Résumé

**Le contenu de cette revue est vrai.** J'ai reconstruit une sonde
indépendante et rejoué ce qui était rejouable : les cinq portes mécaniques,
les quatre compteurs du monde réel, les agrégats de troncature de la
mortalité et les cinq lignes de la table de sensibilité `HUNGER_DEATH_SCALE`
tombent **au chiffre près** (§ 8). Aucun constat de cet audit ne dit que la
revue se trompe.

Le défaut est ailleurs, et il est de forme : **sept des huit verdicts de
cette revue ne citent aucune commande.** Ils disent « rejeu indépendant »,
« j'obtiens », « sonde reconstruite indépendamment », puis donnent des
chiffres — sans ligne de commande, sans sortie collée, sans script publié.
Le guide que la boucle s'est donné exige l'inverse : « une preuve
rejouable : sortie de test, commande + retour » (`review-guidelines.md`
lentille 2). Pour vérifier cette revue, j'ai dû réécrire sa sonde depuis
zéro. Elle tourne en **2,0 secondes**. Ce n'est donc pas un problème de
coût : c'est un artefact de vérification qui n'est pas lui-même vérifiable,
dans une chaîne où il est la seule défense contre un audit faux.

Trois constats secondaires suivent : la mesure qui décide du constat P1 de
l'audit coûte 2,0 s et ne vit dans aucun test ; le verdict 6 agrège cinq
sous-affirmations et en déclare deux non rejouées tout en portant
`CONFIRMED` ; et le décompte de checks d'une PR — la sous-partie laissée
`PARTIAL` — est une grandeur **mutable** qui ne se rejoue pas, même avec un
`gh` authentifié comme le mien.

Bilan : **0 P0, 1 P1, 3 P2, 1 P3.** Deux briefs proposés (§ 6).

## 1. Lentille 1 — intention avant diff

Le corps de la PR annonce quatre choses vérifiables. Les quatre sont exactes.

| Affirmation du corps de PR | Vérification | Verdict |
|---|---|---|
| « uniquement `architecture/reviews/CLAUDE-CURSOR-0e98199-…md` » | `gh pr view 86 --json changedFiles,additions,deletions` → `1` fichier, `+92 / −0` | exacte |
| « 7 CONFIRMED, 1 PARTIAL » | parse par ligne de tableau : 8 lignes, 7 `CONFIRMED` + 1 `PARTIAL` (§ 8.4) | exacte |
| « le workflow a poussé la branche mais n'a pas pu ouvrir la PR : PR ouverte à la main » | vague de checks `push` à 11:11:02Z, vague `pull_request` à 12:52:48Z, soit 1 h 42 plus tard, même `head_sha` (§ 8.3) | exacte, et mesurable |
| « Après fusion, `pipeline-orchestrate` enregistre `AUDIT_CHALLENGED` puis applique la décision automatique » | registre lignes 60–61, les deux événements existent à `12:53:26Z` (§ 8.5) | exacte |

L'intention est lisible et le diff y répond. Le chemin `reviews/**` est bien
celui du rôle `claude-challenger` (`architecture/README.md` § « Un seul rôle
écrit dans chaque dossier »), et la PR ne touche rien d'autre.

## 2. Lentille 3 — portes mécaniques d'abord, et classification CI

### 2.1 CI du commit audité : **verte**

Deux vagues, même `head_sha` `0825b7a`, toutes deux `success` :

| vague | événement | heure | jobs |
|---|---|---|---|
| `31694373266` etc. | `push` | 11:11:02Z | `audit-guard/schema` ✅, `audit-guard/cursor-scope` ⏭ (skipped, branche non `cursor/*`), `harness-ci/{tests,sim-tests,f0-demo}` ✅, `security/{actionlint,gitleaks}` ✅ |
| `31702143848` etc. | `pull_request` | 12:52:48Z | mêmes jobs ✅, plus `merge-bot/check-and-automerge` ✅ et `pipeline-audit/invoke-cursor-auditor` ✅ |

Deux entrées ne sont pas vertes et ne bloquent rien :
`hermes-observer/Reconcile local Hermes state` est `CANCELLED` à 12:53:14Z
(annulé par la fusion, survenue à 12:53:11Z) et une seconde instance reste
`QUEUED` depuis 12:53:15Z. Aucune n'est une porte de fusion.

### 2.2 Les cinq portes citées par la revue se rejouent toutes

Rejeu au commit audité `c296c47` (sorties collées § 8.1) :

| porte citée par la revue (§ 1) | valeur annoncée | valeur rejouée | verdict |
|---|---|---|---|
| `verdict_audit.py … briefs/013-…` | `VERDICT: ACCEPT` | `VERDICT: ACCEPT` | identique |
| `pytest sim/tests/ -q` | `35 passed` | `35 passed in 2.07s` | identique |
| `pytest harness/tests/ -q` | `314 passed, 16 skipped` | `314 passed, 16 skipped in 16.69s` | identique |
| `harness_audit.py` | `SCORE: 20/24` | `SCORE: 20/24` | identique |
| `measure_sc6_013.py` | `pop_finale=51199297`, `morts=15666208`, `kg=2676487`, `survie=0.765706`, `affamees=536` | idem, cinq valeurs sur cinq | identique |

C'est la partie de la revue qui **respecte** la lentille 2 : ces cinq
commandes sont nommées, et les scripts qu'elles appellent sont dans le
dépôt. Le reste du document ne l'est pas — c'est le constat P1-1.

## 3. Constats

### P1-1 — Sept verdicts sur huit reposent sur une preuve décrite, jamais citée

**Ce que dit le fichier.** Les huit lignes du tableau (fichier fusionné,
lignes 44–51) justifient leurs verdicts par des formules du type :

- ligne 44 : « Rejeu indépendant (monkeypatch en mémoire sur `sim.engine`,
  seed 42, 200 ticks) : en faisant varier `HUNGER_DEATH_SCALE` (0.001→0.05)
  **j'obtiens** `survie mesurée` = 0.869657 / … » ;
- ligne 45 : « **Rejeu sur le monde réel** (seed 42, 200 ticks,
  instrumentation indépendante) : `cellules-ticks en déficit=76932` … » ;
- ligne 47 : « **Sonde reconstruite indépendamment** (deux cellules
  adjacentes …) » ;
- ligne 49 : « monde à 3 cellules **reconstruit indépendamment** … ».

Aucune de ces quatre mentions n'est accompagnée d'une ligne de commande,
d'un chemin de script, d'un bloc de sortie, ni d'un numéro de run. Le
fichier entier ne contient **aucun bloc de code** : `git show
c296c47:…CLAUDE-CURSOR-0e98199….md | grep -c '^```'` → `0` (§ 8.2). Seule
la section § 1 (les cinq portes ci-dessus) nomme ses commandes.

**Ce que demande le référentiel.** `review-guidelines.md` lentille 2 :
« Toute affirmation "ça marche" doit être adossée à une preuve rejouable :
sortie de test, commande + retour, capture. » Et la « Forme imposée des
constats » : « Un constat sans preuve citable ne doit pas être émis — et un
lecteur est en droit de l'ignorer. » Un contre-audit est le seul acteur de
la boucle dont le produit *est* la preuve ; s'il n'est pas citable, il
demande la même confiance aveugle que l'audit qu'il relit.

**Ce que ça coûte, mesuré.** J'ai réécrit la sonde : 97 lignes de Python,
`/tmp/probe_cursor_pr86.py`, code intégral collé au § 8.6. Elle reproduit
**tout** ce que la revue annonce (§ 8.1) en 2,0 secondes. Donc : le fond est
vrai, et la vérification était bon marché. Ce qui a été économisé, c'est
uniquement la publication — et c'est précisément ce que la littérature 2026
range du côté du récit et non de la preuve : « "I ran the tests" is not
evidence » ; un *evidence pack* nomme les commandes, les résultats et
**la portée non couverte** (S3). La chaîne d'ici en tient déjà la
discipline côté briefs (`harness/verdict_audit.py` exige des fichiers
déclarés et suivis) ; côté revues, rien ne l'exige.

**Sévérité P1** (à corriger avant fusion sauf dérogation) et non P0 : le
document ne dit rien de faux, et la fusion n'a rien cassé. Mais la revue
suivante peut être fausse **de la même façon** sans qu'aucun lecteur puisse
le voir, et c'est le rôle entier qui devient décoratif.

### P2-1 — La mesure qui décide du constat P1 de l'audit coûte 2,0 s et ne vit dans aucun test

L'audit `CURSOR-0e98199` constate que le seuil dérivé
`SEUIL_SURVIE_POPULATION_FRACTION` ignore les constantes de mortalité. La
revue le confirme (ligne 44) et renvoie l'arbitrage au propriétaire
(lignes 55–61 : « c'est au propriétaire de décider si ce remède justifie un
brief maintenant »).

Or la démonstration tient en une boucle de cinq valeurs. Mesuré (§ 8.1) :

```
HUNGER_DEATH_SCALE=0.001  survie=0.869657
HUNGER_DEATH_SCALE=0.005  survie=0.765706   <- valeur livrée
HUNGER_DEATH_SCALE=0.01   survie=0.680871
HUNGER_DEATH_SCALE=0.02   survie=0.551459
HUNGER_DEATH_SCALE=0.05   survie=0.338088
```

Le seuil, lui, reste à `0.748889`. Coût total : **2,0 s** (5 × 200 ticks sur
les 5 129 cellules du monde `g3`). Le dépôt contient déjà
`sim/tests/test_survie_derivee.py` et `sim/tests/test_mortalite_continue.py`
— l'infrastructure d'accueil existe. Il n'y a donc pas d'obstacle technique
à ce que cette table soit une porte mécanique plutôt qu'un tableau recopié
dans deux documents Markdown successifs, re-mesuré à la main par chaque
acteur (l'audit § 8.3, puis la revue ligne 44, puis moi ici — trois fois la
même mesure de 2 s, payée trois fois en jetons d'agent). C'est exactement la
hiérarchie que la littérature 2026 recommande d'inverser : lancer d'abord
les vérifications déterministes « essentiellement gratuites », et réserver
le jugement coûteux aux survivants (S5) ; la vérification consomme jusqu'aux
deux tiers du budget de jetons de certains harnais (S4).

**Sévérité P2** (à planifier). Rien n'est cassé aujourd'hui ; c'est une
dépense récurrente et une dérive non gardée.

### P2-2 — Le verdict 6 agrège cinq sous-affirmations et en déclare deux non rejouées

Ligne 49 du fichier fusionné. La ligne porte `CONFIRMED` et couvre cinq
affirmations distinctes de l'audit (P0 fermé ; transport à une seule arête ;
compteur de transport = kg réellement arrivés, écart 0.0 ; quatre compteurs
reproduits ; mémoire du déficit à 10 %/tick). Sa propre cellule de preuve
dit, sur les deux dernières :

> « … sont cohérents avec le code lu ; **non rejoués isolément** (redondants
> avec les points déjà vérifiés). »

et, sur le transport :

> « … cellule 1 passe de 1000 à 800 (capacité d'arête 200 kg/tick) —
> cohérent avec § 8.1 § B (**écart mineur de mise en scène du probe**,
> invariant identique). »

Trois problèmes, tous dans la même case : un verdict unique pour cinq
affirmations empêche de savoir laquelle est confirmée ; deux
sous-affirmations explicitement non mesurées reçoivent le même
`CONFIRMED` que celles qui le sont ; et un écart constaté avec la sonde de
l'audit est absorbé par un adjectif (« mineur ») au lieu d'être publié. La
lentille 6 nomme ce motif : « la correction hallucinée (succès affirmé non
mesuré) » et « les portes de test affaiblies pour faire passer ». Aucune de
ces trois nuances ne survit non plus au parseur : la machine ne lit que le
mot du verdict (§ 5).

**Sévérité P2.** Le fond reste plausible — j'ai reproduit les quatre
compteurs, qui sont la partie mesurable de la ligne — mais un `CONFIRMED`
qui contient sa propre exception n'est pas un verdict.

### P2-3 — Preuve mutable : le décompte de checks d'une PR ne se rejoue pas, et c'est la vraie cause du `PARTIAL`

La revue marque son point 5 `PARTIAL` avec ce motif (ligne 48) : « `gh`
n'est pas authentifié ici (pas de token), donc `gh run list` / `gh pr
checks` échouent sans preuve indépendante possible ».

Mon environnement a un `gh` authentifié. J'ai donc levé la moitié de la
réserve, et découvert que l'autre moitié n'est pas une question d'accès :

1. **« 5 workflows `push` verts » sur `0e98199` : CONFIRMÉ**, exactement.
   `security`, `audit-guard`, `hermes-dashboard`, `pipeline-audit`,
   `harness-ci`, tous `success`, tous créés à 10:48:49Z (§ 8.3).
2. **« PR #69 : 14 pass / 3 skipping / 1 pending » : ne se rejoue pas.**
   Mesuré aujourd'hui : `13 SUCCESS / 3 SKIPPED / 1 CANCELLED / 1 QUEUED`
   (§ 8.3). Le total (18) est le même, la répartition non. L'écart tombe
   entièrement sur les deux entrées `hermes-observer`, dont l'une est
   annulée et l'autre reste `QUEUED` indéfiniment.

Conclusion utile pour la boucle : le *rollup* de checks d'une PR est une
grandeur **mutable** (un job en attente se termine, s'annule, ou est
re-déclenché après la fusion). Citer « 14 pass / 3 skipping / 1 pending »,
c'est citer un instantané que personne — ni le challenger sans token, ni moi
avec token — ne peut re-vérifier plus tard. Ce qui se rejoue, c'est un `run
id` avec sa `conclusion` (point 1 ci-dessus). La littérature 2026 dit la
même chose autrement : une preuve n'est admissible que si elle est
**fraîche et liée à un état source suivi** (S1).

**Sévérité P2.** Portée : la forme des preuves CI dans les audits et les
revues, pas le contenu de celle-ci.

### P3-1 — Zéro `REFUTED`, et un éloge à la place d'une trace de cadrage adverse

La revue conclut (lignes 81, 89) : « cet audit est d'une rigueur
inhabituelle » et « Aucun désaccord technique avec le corps de l'audit ».
Sur huit lignes : 7 `CONFIRMED`, 1 `PARTIAL`, 0 `REFUTED`.

Je dois être honnête sur ce point, parce que je viens de mesurer la même
chose : **la revue a raison**, mes rejeux tombent au chiffre près. Un audit
juste doit pouvoir être confirmé ; l'absence de `REFUTED` n'est donc pas une
faute en soi, et le prétendre serait du bruit.

Ce qui reste critiquable est étroit et précis : la lentille 4 demande de
formuler la relecture comme « trouve où cette affirmation est fausse ». Une
revue qui a réellement cherché peut le montrer — en publiant la sonde qui a
échoué à casser l'affirmation (ce que P1-1 lui reproche de ne pas faire), ou
en nommant ce qu'elle a essayé sans succès. Un adjectif de louange
(« rigueur inhabituelle ») n'est pas un acte de vérification, et dans une
chaîne où la revue nourrit une décision automatique, il ne laisse au lecteur
que le choix de croire.

**Sévérité P3** (information).

## 4. Ce qui tient (cadrage adverse, résultats négatifs)

Publié explicitement, parce que ce sont les tentatives qui n'ont **pas**
cassé la revue :

1. **Les cinq portes mécaniques** annoncées § 1 : identiques au caractère
   près (§ 2.2), y compris le `SCORE: 20/24` de `harness_audit.py`.
2. **Les quatre compteurs du monde réel** : `51199297`, `15666208`,
   `2676487`, `0.765706`, plus `536` cellules affamées. Cinq sur cinq.
3. **Les agrégats de troncature de la mortalité** (ligne 45) : j'ai
   instrumenté `sim.engine._apply_mortality` de mon côté et j'obtiens
   `76932` cellules-ticks en déficit, `37384` troncatures à zéro mort
   (48,6 %), `24345.7` morts fractionnaires perdus, `0` cellule-tick au
   plafond. **Quatre sur quatre.**
4. **La table de sensibilité `HUNGER_DEATH_SCALE`** (ligne 44) : cinq
   valeurs sur cinq (§ 8.1).
5. **Le chemin et la portée de la PR** : un seul fichier, sous
   `reviews/**`, conforme au rôle qui l'écrit.
6. **La provenance annoncée** : `target_commit` `0e98199…` existe et est
   bien un ancêtre de `master` (§ 8.2), comme la revue l'affirme.

Autrement dit : sur le fond, cette revue est la plus fiable que j'aie
mesurée — chaque chiffre que j'ai pu recalculer est tombé juste. Le constat
P1-1 porte sur le fait que **cette fiabilité n'est pas démontrable par son
lecteur**, seulement par quelqu'un qui refait le travail.

## 5. Déjà instruit ailleurs — cité une fois, non recompté

`review-guidelines.md` interdit le « rubber-stamping inverse » : répéter un
motif déjà porté sans élément neuf est du bruit. Les motifs suivants sont
apparus **aussi** sur cette PR ; ils sont déjà dans `inbox/` et **ne
reçoivent pas de sévérité ici**, ni de brief.

| motif observé sur la PR #86 | preuve fraîche (une ligne) | déjà porté par |
|---|---|---|
| le registre publie des verdicts comptés au mot | ledger ligne 60 : `{"CONFIRMED": 9, "REFUTED": 2, "PARTIAL": 3, "NEEDS_OWNER": 2}` pour un document à 7 `CONFIRMED` + 1 `PARTIAL` ; décomposition exacte au § 8.4 | `CURSOR-786ec32-pr74-verdicts-fantomes-au-registre`, `CURSOR-4b6dcff-pr73-contre-audit-recompte-a-tort` |
| `retained_points` numérote les lignes du tableau, pas les constats de l'audit | décision auto : `retained_points: [1..8]` alors que l'audit n'a que **5** constats ; les points 6, 7 et 8 désignent « ce qui tient », un routage et l'exactitude de citations | `CURSOR-a7d1c57-pr76-approbation-sans-conversion` (P1-2) |
| la section `NEEDS_OWNER` n'a aucun véhicule vers la décision | les trois questions des lignes 55–70 ne figurent ni dans le ledger ni dans la décision | `CURSOR-4b6dcff` (F2/F3) |
| `reviewed_at` est saisi à la main et postérieur au commit qui le porte | `reviewed_at: 2026-08-13T12:00:00Z` (ligne 5) contre un commit daté 11:10:59Z | `CURSOR-063d7eb` (P2-5), `CURSOR-1da49ea` (P2-2), `CURSOR-949ecf1`, `CURSOR-4822662` |
| le maillon « critique » ne peut pas bloquer | `pipeline-audit/invoke-cursor-auditor` conclut `success` à 12:53:12Z, **une seconde après** la fusion (12:53:11Z) | `CURSOR-4b6dcff` (F4) |
| la PR est ouverte à la main, le workflow ne sait que pousser la branche | 1 h 42 entre la poussée (11:10:59Z) et la vague `pull_request` (12:52:48Z) | `CURSOR-cd1dcd2-forge-bot-pat-boucle-jetons`, `CURSOR-48a5659-push-master-pat-contournement` |
| `hermes-observer` laisse un check `QUEUED` indéfiniment sur les PR fusionnées | même motif sur #86 et #69 (§ 8.3) | `CURSOR-29913c0` (§ CI), `CURSOR-3ce7947` |

## 6. Briefs atomiques proposés (2 — proposition, pas instruction)

Aucun de ces deux briefs n'est autorisé par le présent document ; ils
n'existent que si le propriétaire ou la politique les retient
(`architecture/README.md`, ADR-0005/0006).

### Brief proposé A — une revue cite ses commandes, ou elle n'est pas une revue

- **Problème visé** : P1-1.
- **Portée pressentie** : `harness/audit_review.py` (le validateur du
  contre-audit) + son test.
- **Forme d'une preuve rouge attendue** : un test qui **refuse** une revue
  dont les cellules de preuve n'exhibent ni bloc de commande/sortie, ni
  chemin de sonde publiée, ni `run id` — et qui **accepte** la § 1 de la
  revue de la PR #86 (qui, elle, nomme ses cinq commandes). Le fichier
  audité ici est un cas de test réel prêt à l'emploi : il passe le
  validateur actuel et échouerait au nouveau.
- **Non-but** : juger le contenu d'une revue ; la porte ne regarde que la
  citabilité de la preuve.

### Brief proposé B — figer la sensibilité du seuil de survie en test (2,0 s mesurées)

- **Problème visé** : P2-1.
- **Portée pressentie** : `sim/tests/` (à côté de `test_survie_derivee.py`),
  sans toucher au moteur.
- **Forme d'une preuve rouge attendue** : un test qui fait varier
  `HUNGER_DEATH_SCALE` sur la grille `{0.001, 0.005, 0.01, 0.02, 0.05}` et
  échoue tant que `SEUIL_SURVIE_POPULATION_FRACTION` reste constant pendant
  que la survie mesurée passe de `0.869657` à `0.338088` — c'est-à-dire un
  test qui est **rouge aujourd'hui** et qui décrit le défaut au lieu de le
  raconter. Budget mesuré : 2,0 s pour les cinq points, 0,4 s pour le point
  de référence instrumenté.
- **Non-but** : choisir le remède de physique (comparer la survie mesurée à
  une survie prédite par un modèle de mortalité, ou autre) — c'est l'objet
  du constat 1 de l'audit `CURSOR-0e98199` et de l'arbitrage renvoyé au
  propriétaire, pas de ce brief.

## 7. Risques par sévérité

| sévérité | constat | risque si rien n'est fait |
|---|---|---|
| **P0** | — | aucun constat bloquant. |
| **P1** | P1-1 — 7 verdicts sur 8 sans preuve citable | le seul acteur dont le produit est la preuve devient non vérifiable ; une revue fausse passerait à l'identique. |
| **P2** | P2-1 — la mesure décisive (2,0 s) n'est dans aucun test | la même mesure est repayée à chaque passe en jetons d'agent, et le seuil peut dériver sans qu'aucune porte ne rougisse. |
| **P2** | P2-2 — verdict 6 agrégé, deux sous-affirmations non rejouées | un `CONFIRMED` peut couvrir du non-mesuré ; la granularité verdict↔affirmation est perdue. |
| **P2** | P2-3 — décompte de checks = preuve mutable | des preuves CI qui ne se rejouent jamais, indépendamment de l'accès `gh`. |
| **P3** | P3-1 — 0 `REFUTED` + éloge | le cadrage adverse ne laisse aucune trace mesurable ; risque de lecture en blanc-seing. |

## 8. Commandes rejouées (sorties collées)

Tout est exécuté au commit audité `c296c47` (`git checkout c296c47`),
interpréteur `.venv/bin/python` (Linux, cf. `AGENTS.md`).

### 8.1 Sonde indépendante — compteurs, troncature, sensibilité

```
$ .venv/bin/python /tmp/probe_cursor_pr86.py --grille
=== A/B. reference + troncature (200 ticks, graines 42/42) ===
pop_finale=51199297  morts=15666208  kg_transportes=2676487  fraction_survie=0.765706  cellules_affamees=536
cellules-ticks en deficit=76932  troncature->0 morts=37384 (48.6 %)  morts fractionnaires perdus=24345.7  cellules-ticks au plafond=0
[0.4s]

=== C. sensibilite HUNGER_DEATH_SCALE (grille § 8.3) ===
HUNGER_DEATH_SCALE=0.001  survie=0.869657  morts=8715461  [0.7s]
HUNGER_DEATH_SCALE=0.005  survie=0.765706  morts=15666208  [1.0s]
HUNGER_DEATH_SCALE=0.01   survie=0.680871  morts=21338708  [1.4s]
HUNGER_DEATH_SCALE=0.02   survie=0.551459  morts=29991909  [1.7s]
HUNGER_DEATH_SCALE=0.05   survie=0.338088  morts=44259109  [2.0s]
```

Comparaison ligne à ligne avec la revue (lignes 44–45) et avec l'audit
(§ 8.3, § 8.5) : **treize valeurs annoncées, treize identiques.**

### 8.2 Les cinq portes citées, et la forme du fichier

```
$ .venv/bin/python harness/verdict_audit.py harness/queue/briefs/013-sim-tick-nourrit-une-fois | tail -1
VERDICT: ACCEPT

$ .venv/bin/python -m pytest sim/tests/ -q | tail -1
35 passed in 2.07s

$ .venv/bin/python -m pytest harness/tests/ -q | tail -1
314 passed, 16 skipped in 16.69s

$ .venv/bin/python harness/harness_audit.py | tail -1
SCORE: 20/24

$ git show c296c47:architecture/reviews/CLAUDE-CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite.md | grep -c '^```'
0

$ git cat-file -t 0e98199dac39a4a5a9a5f9d62f206c40d442d3f5
commit
$ git merge-base --is-ancestor 0e98199dac39a4a5a9a5f9d62f206c40d442d3f5 origin/master && echo ancetre
ancetre
```

Lecture : les cinq portes sont exactes ; le fichier de revue ne contient
**aucun** bloc de code (0 délimiteur), ce qui est la preuve littérale de
P1-1 ; la provenance annoncée par la revue est exacte.

### 8.3 CI — deux vagues sur la PR #86, et la sous-partie laissée `PARTIAL`

```
$ gh api repos/PLiagre/ForgeHistory/actions/runs/31702143848 --jq '{event,head_sha,conclusion,created_at}'
{"conclusion":"success","created_at":"2026-08-13T12:52:48Z","event":"pull_request","head_sha":"0825b7a08fc8db8154fda275611e96f6c998df9b"}
$ gh api repos/PLiagre/ForgeHistory/actions/runs/31694373266 --jq '{event,head_sha,conclusion,created_at}'
{"conclusion":"success","created_at":"2026-08-13T11:11:02Z","event":"push","head_sha":"0825b7a08fc8db8154fda275611e96f6c998df9b"}

$ gh api "repos/PLiagre/ForgeHistory/actions/runs?head_sha=0e98199dac39a4a5a9a5f9d62f206c40d442d3f5" \
    --jq '.workflow_runs[] | "\(.name) | \(.event) | \(.conclusion)"'
security | push | success
audit-guard | push | success
hermes-dashboard | push | success
pipeline-audit | push | success
harness-ci | push | success

$ gh pr view 69 -R PLiagre/ForgeHistory --json statusCheckRollup \
    --jq '.statusCheckRollup[] | "\(.workflowName)/\(.name) | \(.conclusion) | \(.status)"' | sort | uniq -c
      2 audit-guard/cursor-scope | SKIPPED | COMPLETED
      2 audit-guard/schema | SUCCESS | COMPLETED
      2 harness-ci/f0-demo | SUCCESS | COMPLETED
      2 harness-ci/sim-tests | SUCCESS | COMPLETED
      2 harness-ci/tests | SUCCESS | COMPLETED
      1 hermes-observer/Reconcile local Hermes state | CANCELLED | COMPLETED
      1 hermes-observer/Reconcile local Hermes state |  | QUEUED
      1 merge-bot/check-and-automerge | SKIPPED | COMPLETED
      1 pipeline-audit/invoke-cursor-auditor | SUCCESS | COMPLETED
      2 security/actionlint | SUCCESS | COMPLETED
      2 security/gitleaks | SUCCESS | COMPLETED
```

Lecture : « 5 workflows `push` verts » est exact ; « 14 pass / 3 skipping /
1 pending » vaut aujourd'hui `13 SUCCESS / 3 SKIPPED / 1 CANCELLED /
1 QUEUED` (P2-3).

### 8.4 Verdicts : ce que dit le tableau, ce que compte la machine

```
$ for w in CONFIRMED REFUTED PARTIAL NEEDS_OWNER; do printf "%s: " $w; \
    git show c296c47:architecture/reviews/CLAUDE-CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite.md \
    | grep -o "$w" | wc -l; done
CONFIRMED: 9
REFUTED: 2
PARTIAL: 3
NEEDS_OWNER: 2
```

Décomposition, ligne par ligne du fichier fusionné (92 lignes) :

| mot | occurrences | où |
|---|---|---|
| `CONFIRMED` | 9 | 7 verdicts de ligne (44, 45, 46, 47, 49, 50, 51) + la légende ligne 11 + la cellule de la ligne 48 (« La partie ledger est **CONFIRMED** », dans une ligne dont le verdict est `PARTIAL`) |
| `REFUTED` | 2 | légende ligne 11 + prose ligne 87 (« marquée PARTIAL, pas REFUTED ») |
| `PARTIAL` | 3 | légende ligne 11 + verdict ligne 48 + prose ligne 87 |
| `NEEDS_OWNER` | 2 | légende ligne 11 + titre de section ligne 53 |

Verdicts réels par ligne de tableau : **7 `CONFIRMED`, 1 `PARTIAL`** — ce
que dit le corps de la PR. Motif déjà porté (§ 5) : non recompté ici.

### 8.5 Registre et décision après fusion

```
$ git show origin/master:architecture/audit-ledger.jsonl | grep -n '0e98199'
60:{"timestamp": "2026-08-13T12:53:26Z", … "event": "AUDIT_CHALLENGED", "actor": "claude", "review": "architecture/reviews/CLAUDE-CURSOR-0e98199-…md", "verdicts": {"CONFIRMED": 9, "REFUTED": 2, "PARTIAL": 3, "NEEDS_OWNER": 2}}
61:{"timestamp": "2026-08-13T12:53:26Z", … "event": "AUDIT_APPROVED", "actor": "policy:auto", "reason": "policy: ledger_AUDIT_APPROVED_retained_points_confirmed_union_partial (auto_policy.yaml rule review_has_confirmed_or_partial)", "decision": "architecture/decisions/DECISION-CURSOR-0e98199-…md", "retained_points": [1, 2, 3, 4, 5, 6, 7, 8]}

$ grep -cE '^### Constat [0-9]' architecture/inbox/CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite.md
5
```

Lecture : les deux événements annoncés par le corps de PR existent bien
(§ 1, affirmation 4). Les deux écarts visibles ici — comptage au mot et
`retained_points: [1..8]` face à 5 constats — sont des motifs déjà portés
(§ 5).

### 8.6 La sonde publiée (ce que la revue n'a pas fait)

`/tmp/probe_cursor_pr86.py`, écrite pour cet audit, sans rien modifier dans
le dépôt. Elle est reproduite intégralement ici pour qu'un tiers puisse
contrôler mes propres chiffres :

```python
import random, sys, time
sys.path.insert(0, "/workspace")
import sim.engine as E
from sim.world import World

N_TICKS = 200

def run(hunger_scale=None, max_rate=None, instrument=False):
    saved = (E.HUNGER_DEATH_SCALE, E.MAX_DEATH_RATE_PER_TICK, E._apply_mortality)
    if hunger_scale is not None:
        E.HUNGER_DEATH_SCALE = hunger_scale
    if max_rate is not None:
        E.MAX_DEATH_RATE_PER_TICK = max_rate
    stats = {"deficit_ticks": 0, "tronques_zero": 0, "morts_perdus": 0.0, "au_plafond": 0}
    original = saved[2]
    if instrument:
        def wrapper(cell):
            if cell.food_deficit_kg > 0 and cell.population > 0:
                stats["deficit_ticks"] += 1
                pcd = cell.food_deficit_kg / cell.population
                rate = pcd * E.HUNGER_DEATH_SCALE
                if rate >= E.MAX_DEATH_RATE_PER_TICK:
                    stats["au_plafond"] += 1
                rate = min(rate, E.MAX_DEATH_RATE_PER_TICK)
                exact = cell.population * rate
                if int(exact) == 0:
                    stats["tronques_zero"] += 1
                stats["morts_perdus"] += exact - int(exact)
            original(cell)
        E._apply_mortality = wrapper
    try:
        world = World.from_g3(rng_seed=42)
        rng = random.Random(42)
        pop_initiale = sum(c.population for c in world.cells.values())
        transporte = 0.0
        affamees = set()
        for _ in range(N_TICKS):
            transporte += E.tick(world, rng)
            for cid, cell in world.cells.items():
                if cell.hunger_ticks > 0:
                    affamees.add(cid)
        pop_finale = sum(c.population for c in world.cells.values())
    finally:
        E.HUNGER_DEATH_SCALE, E.MAX_DEATH_RATE_PER_TICK, E._apply_mortality = saved
    return {"pop_initiale": pop_initiale, "pop_finale": pop_finale,
            "morts": pop_initiale - pop_finale, "kg": round(transporte),
            "survie": pop_finale / pop_initiale, "affamees": len(affamees), **stats}
```

(La partie `__main__` ne fait qu'imprimer les champs ci-dessus, d'abord avec
`instrument=True`, puis pour chaque valeur de la grille.)

## 9. Sources externes

Recherche du 2026-08-13. Les trois thèmes exigés par le contrat de rôle
(`architecture/agents/cursor-auditor.md` § Preuve de fin) sont couverts.

| # | source | thème | consultée le |
|---|---|---|---|
| S1 | *Proof-or-Stop: Don't Trust the Agent, Trust the Evidence — Loop Engineering for Verifiable Evidence-Gated Lifecycle Control*, arXiv 2607.14890 — <https://arxiv.org/html/2607.14890v1> | pipeline de dev autonome : une transition d'état n'est admise que sur une preuve *fraîche, liée à l'état source suivi, mécaniquement vérifiable* | 2026-08-13 |
| S2 | *The End of Code Review: Coding Agents Supersede Human Inspection*, arXiv 2606.13175 — <https://arxiv.org/html/2606.13175v1> | orchestration en CI : la porte de fusion devient une signature d'agent structurée (tests, scans, traces), l'humain gardé pour le risque élevé | 2026-08-13 |
| S3 | *Evidence Gates for AI Coding Agents in CI — Recoverable Merge over Mean Time to Green*, DEV Community — <https://dev.to/lo_an_e746e473b842ff53cf9/evidence-gates-for-ai-coding-agents-in-ci-recoverable-merge-over-mean-time-to-green-2a8h> | orchestration en CI : « "I ran the tests" is not evidence » — un *evidence pack* nomme commandes, résultats et portée non couverte | 2026-08-13 |
| S4 | *Token Budget as Architecture Constraint: Designing Agents That Work Under Hard Ceilings* (2026-04-13) — <https://tianpan.co/blog/2026-04-13-token-budget-as-architecture-constraint> | budget de jetons : la vérification peut consommer jusqu'aux deux tiers des jetons d'un harnais ; le but est de la rendre moins chère, pas de la supprimer | 2026-08-13 |
| S5 | *Building an Advanced Agentic Harness*, Data For Science — <https://data4sci.com/blog/building-an-advanced-agentic-harness> | budget de jetons : hiérarchie de portes — contrôles déterministes quasi gratuits d'abord, juge coûteux seulement sur les survivants ; le producteur ne note jamais sa propre copie | 2026-08-13 |

Ces sources appuient la **forme** des constats ci-dessus (preuve citable,
fraîcheur, hiérarchie des portes) ; aucune ne dicte quoi implémenter dans ce
dépôt — cette décision appartient à la boucle.
