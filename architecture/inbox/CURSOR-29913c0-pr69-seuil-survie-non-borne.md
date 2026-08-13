---
audit_id:                CURSOR-29913c0-pr69-seuil-survie-non-borne
auditor:                 cursor-cloud
target_branch:           forge/013-sim-tick-nourrit-une-fois-ddda
target_commit:           29913c005d8e537fee1da307e098d443635243ac
created_at:              2026-08-13T10:51:33Z
audit_type:              pull-request-review
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Audit de la pull request #69 — « le tick nourrit une fois » (lot 013)

PR relue : <https://github.com/PLiagre/ForgeHistory/pull/69>
Commit audité : `29913c0` (tête de `forge/013-sim-tick-nourrit-une-fois-ddda`)
Base déclarée de la PR : `forge/boucle-audits-post-pr60-ddda` (`4c45718`, branche
de la PR #65 — **pas** `master`)
Référentiel de critique : `architecture/review-guidelines.md` (six lentilles,
sévérités P0–P3, une preuve par constat).

Cet audit **ne décide rien** et n'autorise rien : il propose. La recevabilité
reste au propriétaire / au policy engine (`architecture/README.md`,
ADR-0005/0006).

---

## Résumé en une page

**Le défaut P0 que cette PR devait corriger est réellement corrigé, et je l'ai
vérifié moi-même.** La nourriture acheminée par le commerce ne nourrit plus deux
fois : le maillon commerce ne touche plus `food_deficit_kg`, une cellule
ravitaillée finit à `0.0` kg de stock et `0.0` kg de déficit, exactement comme si
elle avait possédé sa ration. Les quatre compteurs « monde réel » de SC6 se
reproduisent **au chiffre près** avec le script livré. L'invariance à l'ordre du
fichier d'adjacence et la conservation de la masse pendant l'étape commerce, je
les ai re-mesurées avec mon propre code sur les 596 cellules réelles : écart
`0.0` dans les deux cas. Les portes mécaniques sont vertes (gate `ACCEPT`, 35
tests `sim/`, CI verte). C'est un travail sérieux, et la trace de l'échec de
l'itération 1 a été conservée dans un document durable, ce qui est rare.

**Le point qui reste faux est celui-là même qui avait motivé le `REJECT` de
l'itération 1 : le seuil de survie de SC3.** Il a été transformé d'un littéral
calibré (`0.15`) en une expression calculée depuis les constantes — mais
l'expression vaut `0.151111`, soit **0,74 % de l'ancienne valeur rejetée**, et
elle ne borne pas ce qu'elle prétend borner. La marge est présentée comme la
correction d'un transitoire de 200 ticks ; or sa formule ne contient aucun terme
d'horizon, et la grandeur mesurée **converge sous la borne basse** : `0.749715` à
N=800, `0.747480` à N=1600, `0.746409` à N=6400, contre une borne à `0.748889`.
Autrement dit : la fenêtre « dérivée analytiquement » **exclut l'état
stationnaire du modèle qu'elle décrit**, et n'est verte qu'à l'horizon où elle a
été posée. Ces trois mesures sont nouvelles — l'Évaluateur avait perturbé la
densité, la production et la consommation, jamais l'horizon ni une constante de
mortalité.

Second constat de même niveau, indépendant du premier et **imputable au brief,
pas au Générateur** : la récupération graduelle du déficit efface une dette
alimentaire en kilogrammes sans qu'aucun kilogramme ne soit consommé en échange.
`1 000` kg de déficit disparaissent pendant qu'une cellule consomme `20` kg et
conserve `980` kg en stock ; un surplus de `1e-9` kg produit le même effacement.
C'est un manquement direct au principe non négociable n° 3
(`docs/rules/simulation-principles.md` ligne 20 : « Nothing teleports. Everything
has origin, transport, storage, destination »). Une dérogation datée est une
issue légitime ; le silence n'en est pas une.

Sévérités : **2 × P1, 3 × P2, 3 × P3, aucun P0**. Aucun de ces constats ne
remet en cause la correction du double comptage.

---

## Classification de la CI du commit audité

Commit `29913c0`, événement `pull_request` (PR #69) et `push`. Relevé par
`gh pr checks 69` et `gh run list --commit 29913c00…` :

| Workflow / job | État | Durée |
|---|---|---|
| `harness-ci` / `tests` | **succès** | 24 s |
| `harness-ci` / `sim-tests` | **succès** | 20 s |
| `harness-ci` / `f0-demo` | **succès** | 11 s |
| `audit-guard` / `schema` | **succès** | 10 s |
| `audit-guard` / `cursor-scope` | **ignoré** (branche non `cursor/*` — conforme à la condition `if` du workflow) | — |
| `security` / `gitleaks` | **succès** | 9 s |
| `security` / `actionlint` | **succès** | 9 s |
| `pipeline-audit` / `invoke-cursor-auditor` | **succès** | 22 s |
| `merge-bot` / `check-and-automerge` | **ignoré** | — |
| `hermes-observer` / `Reconcile local Hermes state` | **en attente** au moment de l'audit | — |

**Verdict CI : verte.** Aucun job en échec. Le seul job non terminé
(`hermes-observer`) est un observateur d'état Hermes, sans rapport avec le
contenu du moteur.

---

## Ce que j'ai rejoué moi-même (commandes + sorties collées)

Tout ce qui suit a été exécuté sur un checkout détaché du commit audité
(`git worktree add /tmp/pr69 29913c0`), avec `/workspace/.venv/bin/python`.

### 1. Portes mécaniques

```
$ .venv/bin/python -m pytest sim/tests/ -q
...................................                                      [100%]
35 passed in 2.33s

$ .venv/bin/python harness/verdict_audit.py harness/queue/briefs/013-sim-tick-nourrit-une-fois
[PASS] files_declared_exist          [PASS] mtime_after_brief
[PASS] captures_differ_when_should   [PASS] waivers_have_command_and_error
[PASS] no_empty_sample_pass          [PASS] verdict_numbers_traceable
[PASS] no_bare_python_alias          [PASS] verdict_is_not_self_authored
[PASS] rubric_predates_deliverables  [PASS] declared_files_are_tracked
VERDICT: ACCEPT
```

Dix contrôles sur dix, comme annoncé dans la PR. Les 35 tests `sim/` sont bien
là et verts.

### 2. Les compteurs SC6 se reproduisent exactement

```
$ .venv/bin/python harness/queue/briefs/013-sim-tick-nourrit-une-fois/deliverables/measure_sc6_013.py
pop_initiale = 66865505
pop_finale   = 51199297
cellules_affamees_monde_reel_re = 536   (sur 596 cellules chargées)
morts_cumules_monde_reel_re     = 15666208
kg_transportes_monde_reel_re    = 2676487
fraction_survie_monde_reel_re   = 0.765706
  SEUIL_SURVIE_POPULATION_FRACTION = 0.7488888888888889
  satisfaite : True
TOUTES LES CONDITIONS SC6 SONT SATISFAITES.
```

Les quatre valeurs du manifeste sont exactes. Aucun écart.

### 3. Le double comptage est bien mort (sonde écrite par moi)

Monde à deux cellules : une receveuse (50 habitants, besoin `100.0` kg, stock
nul) reliée à une source excédentaire.

```
transporté = 100.0 kg ; stock receveur après commerce = 100.0
besoin du tick = 100.0 kg
déficit après consommation = 0.0
stock après consommation = 0.0
```

La ration nourrit **une** fois : elle couvre le besoin du tick et ne subsiste pas
en réserve. `_apply_commerce` ne mentionne `food_deficit_kg` que dans ses
commentaires (`sim/engine.py` lignes 74–75).

### 4. Invariance à l'ordre des arêtes et conservation de la masse (mon code, pas le leur)

30 ticks sur les 596 cellules réelles, adjacence **mélangée** (`random.shuffle`,
deux graines différentes) **et** direction de chaque arête inversée :

```
état identique (graine 7) : True    état identique (graine 999) : True
cellules divergentes : 0
conservation de la masse pendant le commerce : écart max sur 200 ticks = 0.0
```

`0.0`, pas « négligeable ». SC2 et la conservation tiennent sur un protocole que
je n'ai pas repris d'eux.

### 5. Les preuves rouges sont de vraies sorties de test

`sim/tests/proof_red/run_ordre_tick_red.txt` contient une sortie pytest réelle
(`rootdir: /tmp/sabotage-013/paire-A`, `1 failed`), pas un texte rédigé à la
main.

---

## Les six lentilles

**1. Intention avant diff.** L'intention est lisible et traçable : audit
`CURSOR-a4de4bb` → décision propriétaire `APPROVED` (10 points retenus) → brief
013 → deux itérations → verdict `PASS`. Le brief 013 déclare explicitement les
points qu'il ne traite pas (constats 3, 4 et 9, renvoyés au brief 014 ou au
propriétaire, `brief.md` lignes 220–222). Sur ce plan, rien à reprocher : la
chaîne se relit. La faiblesse d'intention est ailleurs — le brief prescrit
lui-même une formule de récupération du déficit qui contredit un principe non
négociable (constat P1-2), et aucun des quatre acteurs ne l'a relevé.

**2. Preuve d'exécution, pas d'affirmation.** Très bonne tenue générale : quatre
paires rouge/verte committées, sabotages reproduits par l'Évaluateur lui-même,
compteurs reconstruits indépendamment. Une exception nette : la preuve de SC3
n'est pas une preuve mais une **constatation d'inclusion** — « la mesure tombe
dans la fenêtre » — sur un unique horizon, avec une fenêtre dont la borne active
a été posée après avoir vu la mesure de l'itération 1 (constat P1-1).

**3. Portes mécaniques d'abord.** Les portes ont tourné et sont vertes ; je n'ai
pas dépensé de jugement sur ce qu'elles couvrent. Ce que je note, c'est ce
qu'elles **ne peuvent pas** voir : `verdict_audit.py` vérifie qu'un nombre cité
est traçable au manifeste, pas qu'un seuil est légitime. Un littéral calibré
transformé en expression passe la porte à l'identique. La porte n'est pas en
faute ; l'inférence « gate ACCEPT donc le seuil est dérivé » l'est.

**4. Cadrage adverse.** J'ai cherché où les affirmations de la PR sont fausses,
et non « relu le code ». Trois sabotages que personne n'avait faits : allonger
l'horizon, doubler une constante de mortalité, dupliquer une arête d'adjacence.
Les deux premiers ont produit des constats.

**5. Taille et découpage.** 22 fichiers, `+4011 / -114`. C'est très au-delà du
seuil du guide (~5 fichiers, quelques centaines de lignes). Le découpage
revendiqué (« une PR par objet », réponse au constat 8 de l'audit source) est
réel mais incomplet (constat P2-3).

**6. Pièges du code généré par IA.** Le piège dominant est présent sous sa forme
la plus difficile à voir : non pas une correction hallucinée, mais une
**dérivation hallucinée** — une expression construite à partir des constantes du
modèle, dont la valeur reproduit à 0,74 % près le nombre calibré qui avait été
rejeté, et dont la justification textuelle assemble deux termes sans cohérence
dimensionnelle. Rien d'autre de ce registre : pas de dépendance inventée
(l'adjacence lue existe et est suivie par git), pas de structure de données naïve
(les deux passes du commerce sont indexées par dictionnaire), aucun test
supprimé, aucune assertion affaiblie — la seule adaptation d'un test existant
(`test_causal_chain.py`) est un commentaire, l'assertion est intacte.

---

## Constats

### P1-1 — La marge de survie « dérivée » ne borne pas ce qu'elle prétend borner

**Preuve 1 — la fenêtre exclut l'état stationnaire du modèle.** La marge est
justifiée comme la correction d'un transitoire sur une fenêtre finie de 200 ticks
(`sim/SEEDING.md` lignes 245–247 : « Sur N=200 ticks (fenêtre de transition), la
fraction mesurée s'écarte de cette prédiction »). Sa formule
(`sim/constants.py` lignes 135–138) ne contient **aucun terme d'horizon**. J'ai
donc prolongé l'horizon, même monde, même graine :

```
N=  200 survie=0.765706  borne_basse=0.748889  dans_fenetre=True   ecart=+0.016817
N=  400 survie=0.754826  borne_basse=0.748889  dans_fenetre=True   ecart=+0.005937
N=  800 survie=0.749715  borne_basse=0.748889  dans_fenetre=True   ecart=+0.000826
N= 1600 survie=0.747480  borne_basse=0.748889  dans_fenetre=False  ecart=-0.001408
N= 3200 survie=0.746808  borne_basse=0.748889  dans_fenetre=False  ecart=-0.002081
N= 6400 survie=0.746409  borne_basse=0.748889  dans_fenetre=False  ecart=-0.002480
```

La grandeur converge vers `≈ 0.7464`, c'est-à-dire **sous** la borne basse
`0.748889`. Une correction de transitoire qui est violée dès que le transitoire
s'achève n'est pas une correction de transitoire. Conséquence concrète :
`test_survie_derivee.py::test_fraction_dans_marge` est vert pour la seule valeur
`N_TICKS = 200` codée dans le test (ligne 34) ; tout brief ultérieur qui allonge
l'horizon le fera rougir **sans aucune régression du moteur**.

**Preuve 2 — la formule ignore les constantes qui gouvernent réellement le
résultat.** Le second terme est présenté comme « la pression nette du déficit
stochastique sur la mortalité » (`SEEDING.md` lignes 269–273), alors que la
mortalité est gouvernée par `HUNGER_DEATH_SCALE` et `MAX_DEATH_RATE_PER_TICK`,
absentes de la formule. En doublant la seule `HUNGER_DEATH_SCALE`, la marge ne
bouge pas d'un chiffre et la mesure sort de la fenêtre :

```
HUNGER_DEATH_SCALE x0.5 -> survie N=200 = 0.823327 ; marge INCHANGEE (0.1511) ; dans la fenetre : True
HUNGER_DEATH_SCALE x2.0 -> survie N=200 = 0.680871 ; marge INCHANGEE (0.1511) ; dans la fenetre : False
HUNGER_DEATH_SCALE x4.0 -> survie N=200 = 0.551459 ; marge INCHANGEE (0.1511) ; dans la fenetre : False
```

L'Évaluateur a testé quatre régimes cassés (densité, production ×2 et ÷2,
consommation) — tous des constantes qui apparaissent dans la formule, donc tous
des régimes où la marge se déplace avec le monde. Aucun ne touchait la
mortalité. C'est l'élément nouveau que j'apporte, et il change la conclusion :
la formule ne « suit » le monde que sur les axes qu'elle contient déjà.

**Preuve 3 — la coïncidence numérique avec la valeur rejetée.** L'itération 1
avait fixé la marge à `0.15` après avoir constaté que la mesure tombait hors de
`[0.80, 1.0]` ; ce recalibrage est le motif du `REJECT`
(`SEEDING.md` lignes 302–309, aveu conservé — à leur crédit). L'expression de
l'itération 2 vaut :

```
marge rejetée en itération 1 : 0.15
marge « dérivée » itération 2 : 0.151111
écart absolu = 0.001111    écart relatif = 0.74 %
```

`SEEDING.md` ligne 285–286 présente cet écart comme une différence volontaire
(« diffère volontairement du 0.15 »). Un écart de 0,74 % avec le nombre qu'on
vient de faire rejeter n'est pas une différence : c'est la signature d'une
expression construite pour retomber sur lui. Je ne peux pas prouver l'intention
— je constate que les trois propriétés attendues d'une dérivation (borner la
grandeur, dépendre de l'horizon qu'elle corrige, dépendre des constantes qui
dominent le résultat) sont absentes toutes les trois.

**Rattachement au référentiel du dépôt** : mode d'échec diagnostiqué n° 6,
« Control that names its own reference »
(`docs/rules/simulation-principles.md` ligne 31). C'est exactement la figure :
le contrôle SC3 nomme sa propre référence.

**Ce que ce constat n'est pas** : je ne rejuge pas la sincérité du Générateur ni
la qualité du contre-audit — l'Évaluateur a fait un travail supérieur à ce que le
gate exigeait et a lui-même consigné la réserve R9 (« cela ne prouve pas que la
composition des deux termes soit la bonne physique », `verdict.md` lignes
641–643). Ce constat est l'élément qui manquait à R9 pour être décidable.

### P1-2 — La récupération du déficit efface des kilogrammes sans contrepartie physique

Dans la branche « surplus » de `_apply_consumption` (`sim/engine.py` lignes
185–193), le stock résiduel est conservé **et** 10 % du déficit accumulé
disparaissent. Sonde écrite par moi :

```
taux de récupération = 0.1
stock  1000.0 -> 980.0   (consommé 20.0 kg)
déficit 10000.0 -> 9000.0 (effacé 1000.0 kg)
kg de nourriture réellement échangés contre l'effacement : 0
```

`1 000` kg de dette alimentaire s'évaporent pendant que la cellule consomme
`20` kg et garde `980` kg. Et la magnitude du surplus n'entre pas dans le
calcul :

```
surplus de 1e-9 kg -> déficit 10000.0 -> 9000.0 (effacé 1000.0 kg)
```

Un gramme au millionième près produit le même effacement qu'une récolte
pléthorique. C'est un manquement au principe non négociable n° 3
(`docs/rules/simulation-principles.md` ligne 20, « Nothing teleports. Everything
has origin, transport, storage, destination ») et, dans sa forme, au principe
n° 2 : « si surplus alors −10 % de dette » est un raccourci en termes de jeu, pas
un enchaînement en termes de monde (nourriture disponible → reprise de poids →
baisse de la surmortalité).

**Imputation.** Ce n'est pas un écart du Générateur : la formule est **prescrite
mot pour mot par le brief**, SC4 (`brief.md`, bloc
`cell.food_deficit_kg = max(0.0, cell.food_deficit_kg × (1 - DEFICIT_RECOVERY_RATE_PER_TICK))`).
Le Générateur l'a implémentée fidèlement ; l'Évaluateur a vérifié la fidélité,
pas la physique. Le constat porte donc sur la spécification et sur le fait que la
chaîne à quatre acteurs a laissé passer une formule contredisant un principe
déclaré non négociable. Une dérogation datée et motivée (proxy assumé de la
reprise démographique, en attendant un modèle de nutrition) est une issue
parfaitement légitime ; l'absence de trace n'en est pas une.

### P2-1 — Le compteur « cellules affamées » mesure un garde-manger vide, pas une sous-alimentation

`_update_hunger` incrémente `hunger_ticks` dès que `food_stock_kg <= 0.0`
(`sim/engine.py` lignes 208–210), et ce test a lieu **après** la consommation. Or
le commerce livre exactement le besoin du tick : une cellule ravitaillée termine
à `0.0` kg de stock. Ma sonde n° 3 ci-dessus le montre : déficit `0.0`, ration
complète reçue, et pourtant `hunger_ticks = 1`. Une cellule parfaitement nourrie
par ses voisins est comptée « affamée ».

**Impact mesuré aujourd'hui : nul.** J'ai décomposé le compteur sur les 200 ticks
du monde réel :

```
cellules affamées (hunger_ticks > 0) = 536 / 596
  dont avec un déficit alimentaire réel        = 536
  dont stock nul MAIS déficit nul (rassasiées) = 0
```

Les 536 cellules publiées ont toutes connu un déficit réel : **le chiffre annoncé
par la PR n'est pas gonflé**, et je ne conteste pas SC6. Le défaut est latent :
il porte sur la *définition* d'un compteur vedette de la couche « compte juste »
du ROADMAP. Le jour où la couverture du commerce s'améliore, le compteur se
mettra à compter des cellules nourries, silencieusement, sans qu'aucun test ne
rougisse.

### P2-2 — Dans le régime courant, la « fenêtre symétrique » est un test unilatéral

Le brief exige une fenêtre symétrique et le test vérifie
`borne_basse <= fraction <= borne_haute` avec
`borne_haute = 0.9 + 0.1511 = 1.051111`
(`test_survie_derivee.py` lignes 93–96). Or aucun opérateur de `sim/engine.py`
n'augmente une population — `_apply_mortality` est le seul à écrire
`cell.population`. La fraction de survie est donc majorée par `1.0` par
construction, ce que j'ai vérifié :

```
50 ticks de surabondance (1e9 kg) : population 10 -> 10
```

Avec les constantes actuelles, la borne haute est **inatteignable** : le seul côté
qui peut rougir est la borne basse, c'est-à-dire celui dont P1-1 établit qu'il a
été dimensionné autour de la mesure. Nuance à porter au crédit de l'Évaluateur :
dans le régime « production doublée » qu'il a testé, la marge se resserre à
`0.0055` et la borne haute mord effectivement (`verdict.md` ligne 634). Le
constat porte donc bien sur le régime courant, pas sur la formule en général.

### P2-3 — Le diff dépasse ce qu'une relecture honnête connecte à l'intention, et la base de la PR n'est pas celle qui atterrira

22 fichiers, `+4011 / -114` (`git diff --stat 4c45718..29913c0`). Trois éléments :

1. **Objets mélangés.** La PR revendique le découpage « une PR par objet », mais
   embarque avec le moteur trois fichiers de tenue de session :
   `HANDOFF.md` (+95), `ROADMAP.md` (+4) et `harness/queue/cost-ledger.jsonl`
   (+2). Ce sont des objets distincts du moteur, et ils sont la partie du diff
   la plus susceptible de conflit.
2. **Base non finale.** La base de la PR est `forge/boucle-audits-post-pr60-ddda`
   (branche de la PR #65), pas `master`. Le diff relu ici n'est donc pas celui
   qui atterrira sur `master`, et l'ordre de fusion est porté par une consigne en
   prose dans la description (« Fusionner la PR #65 d'abord »), pas par une
   garde.
3. **Stratégie de fusion portée par une consigne en prose.** La description
   demande « Pas de squash » au motif que des compteurs d'archive lisent des
   commits épinglés. Je n'ai trouvé qu'une occurrence réelle de lecture épinglée
   (`verdict.md` ligne 71, `git show ea7e093`), donc le risque est plus faible
   qu'annoncé — mais il existe, et rien de mécanique ne le protège.

### P3-1 — Une arête d'adjacence dupliquée franchit le plafond par arête (latent)

`_apply_commerce` empile une demande par arête sans dédoublonner la paire
(`sim/engine.py` lignes 98–117). Sonde : deux arêtes décrivant la même paire,
notées `{a:1,b:2}` et `{a:2,b:1}`.

```
2 arêtes identiques (1,2) et (2,1) -> transporté 400.0 kg (plafond par arête = 200.0)
```

`TRADE_CAPACITY_KG_PER_EDGE_PER_TICK` vaut `200.0` : le plafond physique déclaré
est franchi d'un facteur 2. **Latent, sans impact aujourd'hui** — j'ai vérifié
l'artefact réel : `1364` arêtes, `1364` paires distinctes, `0` doublon, `0`
boucle `a == b`. La garantie « au plus une arête par tick » repose donc sur une
propriété du producteur de l'adjacence, jamais énoncée ni testée côté moteur.

### P3-2 — L'écrêtage côté receveur ne réalloue pas le surplus libéré

La passe 1d (`sim/engine.py` lignes 137–159) réduit les livraisons excédentaires
et laisse la différence chez la source. La conservation est parfaite (je l'ai
mesurée : écart `0.0`), mais le surplus ainsi libéré n'est pas réoffert à un
autre demandeur du même tick. Le commerce sous-livre donc structurellement quand
plusieurs sources visent le même receveur. Information, pas défaut : c'est un
choix implicite de simplicité qui mériterait d'être écrit dans `SEEDING.md` avec
les autres proxys.

### P3-3 — Rappel : le budget de jetons d'un Générateur Cursor reste non mesuré

**Déjà signalé** par `CURSOR-6231186-execution-budgets` (point 2, `P1`,
« Le backend Cursor n'est pas mesurable par ce budget »). Je ne le rouvre pas —
je consigne l'instance, qui est nouvelle : le lot 013 est produit par le backend
`cursor` (deux lignes `generator-run` dans `cost-ledger.jsonl`, sans jeton, sans
durée, sans entrée pour le rôle Évaluateur), SC8 « registre de coût tracé » est
déclaré `PASS` sur cette seule présence d'événement, et l'outil de budget répond :

```
$ .venv/bin/python harness/budget.py status --brief harness/queue/briefs/013-sim-tick-nourrit-une-fois
status     : UNMEASURABLE
reason     : no agent transcript naming 013-sim-tick-nourrit-une-fois
Nothing is being enforced. This is not OK -- it is unmeasured.
```

C'est la première fois qu'un lot de **code moteur** franchit la chaîne complète
avec un budget structurellement non mesurable. L'état de l'art 2026 est explicite
sur ce point : un plafond qui n'est pas imposé pendant la session, mais seulement
observé après coup, n'est pas un plafond [S6, S8].

---

## Ce que cette PR fait bien, et qu'il faut dire

Le cadrage adverse n'oblige pas à taire ce qui tient. Quatre points méritent
d'être notés, car ils sont plus rares que les défauts ci-dessus :

1. **Le P0 est réellement corrigé, et vérifiable en trois lignes de code par un
   tiers.** L'ordre du tick est la bonne réponse au bon problème.
2. **La trace de l'échec survit dans un document durable.** L'aveu du recalibrage
   de l'itération 1 est écrit dans `sim/SEEDING.md` (« ce qui était faux au
   regard de la chronologie réelle »), pas seulement dans un journal de lot. La
   littérature 2026 sur la revue de code généré par IA identifie précisément
   l'inverse — l'effacement des traces d'échec — comme le mode de défaillance
   dominant [S2, S3].
3. **L'Évaluateur a produit ses propres sabotages** au lieu de rejouer ceux du
   Générateur, ce qui est la seule forme de contre-audit qui vaut [S4, S7].
4. **Les compteurs sont reproductibles au chiffre près** par un tiers, avec le
   script livré et sur un checkout propre. C'est la définition de la preuve
   rejouable [S3, S9].

---

## Briefs atomiques proposés (3, plafond du contrat)

Ce sont des **propositions**. Aucun n'est autorisé par cet audit ; la conversion
en brief appartient au propriétaire.

**B-1 — Remplacer la fenêtre de survie par une garde indépendante de l'horizon.**
Objet : SC3. La garde actuelle est verte pour `N_TICKS = 200` et rouge dès
`N ≥ 1600` sans régression. Piste sans prescrire l'implémentation : borner l'état
**stationnaire** (mesurer la convergence, puis vérifier que la valeur limite est
compatible avec la capacité de charge analytique), ou assumer explicitement une
garde de transitoire en faisant apparaître l'horizon dans la formule et dans le
nom de la constante. Condition de recevabilité minimale : la garde doit rougir
quand une constante de mortalité change de régime, ce que la version actuelle ne
fait pas.

**B-2 — Rendre la reprise après famine physique, ou enregistrer la dérogation.**
Objet : SC4 / `_apply_consumption`. Deux issues acceptables et une seule
inacceptable. Acceptable : la dette alimentaire est remboursée en kilogrammes
effectivement consommés (la reprise a une origine, un transport, un stock et une
destination), ou bien un ADR daté assume `DEFICIT_RECOVERY_RATE_PER_TICK` comme
proxy provisoire, avec la contradiction au principe n° 3 nommée. Inacceptable :
laisser la formule sans trace de la décision.

**B-3 — Distinguer « garde-manger vide » de « sous-alimentée » dans les
compteurs de faim.** Objet : `_update_hunger` et les compteurs SC6. Aujourd'hui
une cellule nourrie à ras bord par le commerce est comptée affamée. Le compteur
publié n'est pas faux dans la mesure actuelle (536 = 536, vérifié) : il le
deviendra silencieusement. Un test qui rougit sur le cas « ravitaillée exactement
à son besoin » ferme le trou.

---

## Ce que je n'ai pas pu vérifier

- **Le rapport tokens/coût réel** des trois rôles sur ce lot : les transcripts ne
  sont pas versionnés et `budget.py` ne sait pas lire un run Cursor
  (voir P3-3). Aucun chiffre de coût n'est donc audité ici.
- **Le comportement de la fusion** : ni les réglages de protection de branche, ni
  la stratégie de fusion effective du dépôt ne m'étaient accessibles en lecture.
  Le constat P2-3 porte sur ce qui est écrit dans la PR, pas sur ce que GitHub
  fera.
- **Unity** : hors de portée sur un runner Linux (`AGENTS.md`), et sans rapport
  avec ce lot.
- **Le job `hermes-observer`** était encore en attente au moment de l'audit ; sa
  conclusion n'est pas classifiée ici.

---

# Sources externes

Recherche web du 2026-08-13. Les cinq sources du référentiel de critique
(`architecture/review-guidelines.md`, S1–S5) sont utilisées comme grille et ne
sont pas recomptées ici ; les suivantes sont propres à cet audit.

| # | Source | Consulté le |
|---|---|---|
| S6 | *AI Coding Agents in 2026: A Practical Implementation Guide* — la porte de vérification comme décision centrale d'un pipeline autonome (« an agent whose only gate is *the model thinks it's done* will confidently ship broken code ») — <https://www.jainmehul.com/guides/ai-coding-agents> | 2026-08-13 |
| S7 | *The Agentic SDLC: Build, Test & Verify AI Code in 2026* (TestQuality) — chaîne constructeur/validateur, le validateur n'ayant pas accès au raisonnement du constructeur — <https://testquality.com/agentic-sdlc-guide-build-test-verify-ai-generated-code/> | 2026-08-13 |
| S8 | *AI Agent Token Budget Enforcement [2026]* (Waxell) — un plafond de jetons doit être **imposé pendant** la session, pas estimé puis espéré — <https://waxell.ai/blog/ai-agent-token-budget-enforcement> | 2026-08-13 |
| S9 | *The Verification Gap Behind Every AI-Generated Release* (DevOps.com) — le même modèle qui écrit et qui vérifie équivaut à laisser l'élève corriger sa copie ; investir dans la vérification au même rythme que dans la génération — <https://devops.com/the-verification-gap-behind-every-ai-generated-release/> | 2026-08-13 |
| S10 | *Token Budget as Architecture Constraint* (2026-04-13) — plafonds par trace, caps d'itération et détection d'anomalie de dépense plutôt que continuation silencieuse — <https://tianpan.co/blog/2026-04-13-token-budget-as-architecture-constraint> | 2026-08-13 |
| S11 | *A Metamodel-Based General-Purpose Autocalibration Tool for Simulation Models* — la calibration d'un modèle de simulation doit être validée hors échantillon (nouveaux régimes de paramètres), pas sur le point où elle a été ajustée — <https://sage.cnpereading.com/doi/10.1177/0272989X261452258> | 2026-08-13 |
| S12 | *Overfitting, Model Tuning, and Evaluation of Prediction Performance* (NCBI Bookshelf) — un hyperparamètre ajusté sur l'échantillon qui sert ensuite à le valider ne mesure plus rien : d'où la séparation ajustement / validation / test — <https://www.ncbi.nlm.nih.gov/books/NBK583970/> | 2026-08-13 |

S11 et S12 étayent P1-1 : ce que le lot appelle « dérivation » est, en termes de
modélisation, une calibration validée sur son propre point d'ajustement — et la
mesure hors échantillon (horizon allongé, constante de mortalité modifiée) la
réfute.

---

## Référence des commandes de cet audit

```bash
git worktree add /tmp/pr69 29913c005d8e537fee1da307e098d443635243ac
gh pr view 69 --json additions,deletions,changedFiles,baseRefName,headRefName
gh pr checks 69
gh run list --commit 29913c005d8e537fee1da307e098d443635243ac
git diff --stat 4c45718..29913c0
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/013-sim-tick-nourrit-une-fois
.venv/bin/python harness/queue/briefs/013-sim-tick-nourrit-une-fois/deliverables/measure_sc6_013.py
.venv/bin/python harness/budget.py status --brief harness/queue/briefs/013-sim-tick-nourrit-une-fois
```

Les sondes des constats P1-1, P1-2, P2-1, P2-2, P3-1 et P3-2 ont été écrites hors
du dépôt (`/tmp`), en lecture seule sur le checkout audité ; leurs sorties sont
collées intégralement ci-dessus. Aucun fichier du dépôt n'a été modifié pour les
produire.
