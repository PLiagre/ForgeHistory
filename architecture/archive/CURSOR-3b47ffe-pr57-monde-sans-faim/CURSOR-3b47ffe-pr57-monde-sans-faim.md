---
audit_id: CURSOR-3b47ffe-pr57-monde-sans-faim
auditor: cursor-cloud
target_branch: forge/011-sim-monde-vivant-a67c
target_commit: 3b47ffe4ac808831cee71cb83817b098e08d7e49
created_at: 2026-08-12T17:15:00Z
audit_type: architecture-and-qa
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

**Objet** : critique de la pull request
[#57](https://github.com/PLiagre/ForgeHistory/pull/57) — « Brief 011 :
amorçage du moteur sim/ — monde vivant, couche 1 (F2) », 28 fichiers,
+3098 / −14, base `267888b`, tête `3b47ffe`.

Référentiel de critique : `architecture/review-guidelines.md` (six
lentilles, sévérités P0–P3, une preuve citée par constat). Cet audit
**ne décide rien** : il propose, la décision reste au propriétaire et à
la boucle (`architecture/README.md`, ADR-0005/0006).

**Ce qui est solide.** Le code livré est court, lisible, sans dépendance
tierce, et la chaîne causale est réellement découpée en quatre fonctions
qui écrivent puis relisent des champs persistés — l'esprit du principe 3
(« l'économie est physique ») est respecté dans la forme du code. Les
affirmations chiffrées de la description de PR se reproduisent : le gate
mécanique répond ACCEPT, `sim/tests/` donne 20 succès, `harness/tests/`
donne 314 succès et 16 tests ignorés (sorties collées au § 5). L'itération 1
a produit un vrai REJECT sur SC8, corrigé en itération 2 : la boucle a
mordu au moins une fois, ce qui est le contraire d'un tampon automatique.

**Ce qui ne tient pas.** Trois choses, dans l'ordre d'importance.

1. **Le monde livré ne vit pas.** Sur les 596 cellules réellement
   chargées, 200 pas de temps ne produisent **aucune** cellule affamée,
   **aucun** mort, et un stock alimentaire qui grossit d'un facteur 11.
   La chaîne faim → mortalité ne se déclenche que dans des tests dont
   l'état initial est bâti à la main, sur une cellule d'aire nulle qui
   n'existe pas dans les données. La cause est arithmétique et localisable :
   les deux constantes clés ne sont pas exprimées dans la même unité de
   temps (constats P1-2 et P1-3).
2. **La preuve de déterminisme du tick ne mesure rien** : `tick()` reçoit
   un générateur pseudo-aléatoire et ne le consomme jamais. Deux graines
   totalement différentes donnent le même condensé (constat P1-4).
3. **L'état qui va être fusionné n'a été jugé par personne**, et rien
   dans le dépôt ne trace quel acteur a tenu chacun des trois rôles : les
   six commits portent une seule et même identité git, et le dernier
   commit (hors périmètre du brief) est postérieur au verdict (constat
   P0-1).

Compte : 1 constat P0, 4 constats P1, 5 constats P2, 2 constats P3.
Trois briefs atomiques sont proposés au § 8 — jamais plus (contrat).

# 2. Lentille 1 — Intention avant diff

La spec est lisible et complète :
`harness/queue/briefs/011-sim-monde-vivant-amorcage/brief.md` (316 lignes,
12 conditions de succès, 7 non-buts, 9 compteurs exigés). La description
de PR expose l'intention, les deux itérations, et déclare de lui-même deux
réserves. C'est au-dessus de la moyenne du dépôt et cela rend la critique
possible : l'écart mesuré ci-dessous est un écart à une intention écrite,
pas une préférence d'auditeur.

Un point d'intention mérite d'être posé avant les constats. Le brief
demande (§ World-Terms Requirement) :

> Chaque maillon de cette chaîne (production, stock, consommation,
> épuisement, faim, mort) est **un état réellement écrit puis lu**.

Le diff satisfait cette phrase **au niveau du code**. Il ne la satisfait
pas **au niveau du monde** : les maillons existent, ils ne s'enchaînent
jamais sur les données réelles. Les 12 conditions de succès étant toutes
formulées sur du code ou sur des cellules construites à la main, elles
peuvent toutes passer pendant que la couche 1 « monde vivant » ne produit
aucun événement vivant. C'est un défaut de la **rubrique** autant que de
la livraison, et c'est pourquoi le constat P1-2 n'accuse pas le Générateur
d'avoir triché : il n'a pas triché, il a satisfait une spec qui ne
mesurait pas la bonne chose.

# 3. Constats

## P0-1 — L'état final de la PR n'a été jugé par aucun rôle, et l'acteur des trois rôles n'est tracé nulle part

**Preuve 1 — une seule identité git pour les six commits du lot :**

```
$ git log --format='%h %an <%ae> | %ad | %s' --date=iso-strict origin/master..pr57
3b47ffe Cursor Agent <cursoragent@cursor.com> | 2026-08-12T16:54:17+00:00 | clôture de session : brief 011 accepté — correction factuelle ROADMAP (...)
bed886f Cursor Agent <cursoragent@cursor.com> | 2026-08-12T16:51:46+00:00 | evaluateur: verdict PASS du lot 011 (itération 2)
d47dac6 Cursor Agent <cursoragent@cursor.com> | 2026-08-12T16:51:37+00:00 | generateur: lot 011 itération 2 — corrections B1, B2 et N1 à N6
5df895d Cursor Agent <cursoragent@cursor.com> | 2026-08-12T16:34:46+00:00 | evaluateur: verdict REJECT du lot 011 — SC8 non satisfaite
aec84f1 Cursor Agent <cursoragent@cursor.com> | 2026-08-12T16:34:34+00:00 | generateur: lot 011 — amorçage du moteur sim/ (couche monde vivant)
ca003f9 Cursor Agent <cursoragent@cursor.com> | 2026-08-12T16:03:46+00:00 | planificateur: brief 011 — amorçage sim/ couche monde vivant
```

**Preuve 2 — le seul contrôle mécanique de séparation lit des chaînes
auto-déclarées, pas des acteurs.** Sortie réelle du gate (§ 5) :

```
[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 2 examined pair(s): forge-generateur<->forge-evaluateur; forge-generateur<->forge-evaluateur
```

Le contrôle compare le texte `**Author**:` de `generator-log.md` à celui
de `verdict.md` (`harness/verdict_audit.py`, lignes 378-388 :
`read_all_fields(... "Author")`). Deux chaînes différentes tapées par le
même acteur suffisent à le satisfaire. Rien dans le dépôt ne contredit
cela : `harness/queue/cost-ledger.jsonl` n'enregistre que des événements
`generator-run` (aucun événement d'évaluation), donc l'acteur du rôle
Évaluateur n'existe qu'en prose.

**Preuve 3 — le dernier commit est postérieur au verdict et hors du
périmètre jugé.** `verdict.md` est horodaté `2026-08-12T16:51` (itération 2)
et affirme :

> ## Boundary Violations
> Aucune. Le périmètre est respecté : hors de `sim/`, seuls le dossier du
> brief et le registre de coût du harnais ont changé.

Or le commit `3b47ffe` (16:54:17, soit après) modifie `ROADMAP.md` et
`HANDOFF.md`. L'affirmation « périmètre respecté » était vraie quand elle
a été écrite ; elle est fausse pour l'état que la fusion produirait, et
aucun rôle n'a relu cet état.

**Pourquoi ce n'est pas un rappel de principe déjà écarté.** Le
review-guidelines interdit de répéter un motif déjà tranché sans élément
nouveau. Que Cursor ait tenu les trois rôles est **assumé** par la
description de PR et les notes de transparence des livrables, sur
instruction propriétaire ; ce n'est pas le constat. Les éléments nouveaux
sont mécaniques : (a) aucune trace machine ne dit qui a tenu quel rôle,
(b) le contrôle censé garantir la séparation est structurellement
incapable de la voir, (c) l'état final n'est couvert par aucun verdict.
Aucun ADR ne couvre par ailleurs la substitution employée : `docs/adr/`
contient 0008 (Codex Évaluateur sous plafond de crédit) et 0009 (Codex
Générateur officiel), rien sur Cursor tenant Planificateur/Générateur/
Évaluateur — alors qu'ADR-0010 range Cursor dans la colonne « n'écrit
jamais : code, CI, briefs » (ligne 32) et exige « jamais deux maillons
adjacents tenus par le même acteur sur le même lot » (lignes 26-28).

**Sources externes** : S1 (la séparation de rôles n'est pas une
vérification tant que le vérificateur n'est pas épistémiquement isolé),
S2 (identités distinctes, worktrees séparés, vérification adossée à la
spec d'origine).

## P1-1 — `ROADMAP.md` modifié dans une PR de lot, en contradiction avec ADR-0010 et avec le non-but 5 du brief

**Preuve.** `git diff origin/master..pr57 -- ROADMAP.md` modifie trois
lignes de statut et ajoute une ligne d'historique signée :

```
+| 2026-08-12 | orchestrateur Cursor (remplaçant du CTO Claude, indisponible — instruction propriétaire) | correction factuelle uniquement : brief 011 (F2, amorçage `sim/`) livré et accepté (...)
```

Deux règles écrites s'y opposent :

- `docs/adr/0010-...md` ligne 32 : la colonne « écrit » de `ROADMAP.md`
  appartient à **Hermes** seul ; la même ADR (lignes 100-106) prévoit
  explicitement le refus en revue d'une PR qui sort de son périmètre —
  la symétrie vaut pour une PR de lot qui entre dans celui d'Hermes.
  `hermes/README.md` ligne 18 confirme le périmètre.
- `brief.md` non-but 5 : « Modifier `pipeline/geo/`, `unity/`, `harness/`,
  `docs/adr/` ou tout autre sous-système hors `sim/`. »

Les deux lignes précédentes de l'historique de `ROADMAP.md` montrent la
forme attendue d'une délégation (« hermes (rédaction déléguée à Cursor,
décision propriétaire) ») : l'auteur reste Hermes. La nouvelle ligne signe
au nom du CTO substitué, ce qui est un autre acteur. Le contenu de la
correction est par ailleurs **exact** — c'est la voie d'écriture qui est
en cause, pas le fait.

## P1-2 — Sur le monde réellement chargé, la chaîne faim → mortalité ne se déclenche jamais

**Preuve — mesure rejouable (script complet et sortie au § 5) :**

```
cellules chargees      = 596
area_km2 min/max       = 1.444877 37217.766826
production par km2     = 50.0   consommation par km2 (densite nominale) = 20.0
apres N=200 ticks :
  population totale      66865505 -> 66865505 (delta 0)
  stock total (kg)       4011930300 -> 43937193599 (facteur x11.0)
  cellules avec hunger_ticks > 0 : 0
  cellules avec food_stock_kg <= 0 : 0
```

La production paramétrée (50 kg/km²/tick) dépasse structurellement la
consommation à la densité d'amorçage (10 hab/km² × 2 kg = 20 kg/km²/tick,
`sim/SEEDING.md` lignes 36 et 64). Le rapport est de 2,5 pour 1 dans
**toutes** les cellules, et l'amorçage lui-même ajoute 30 ticks de
réserves. Aucune cellule ne peut donc jamais manquer de nourriture : la
seule dynamique du « monde vivant » est un stock qui croît sans limite.

Le test d'intégration bout en bout SC7d ne contredit pas cela, il le
confirme : il obtient un rendement nul avec `area_km2 = 0.0`
(`sim/tests/test_causal_chain.py`, cellule construite ligne 138-145),
c'est-à-dire une cellule de surface nulle — alors que la plus petite
cellule réelle mesure 1,44 km². Le brief demandait « une cellule avec une
production nulle (rendement = 0) » ; la livraison a annulé la
**superficie** au lieu du **rendement**, ce qui n'est atteignable dans
aucune donnée G3.

**Source externe** : S3 — une couverture adossée à la spec doit classer et
diagnostiquer chaque cas structurellement inatteignable au lieu de le
laisser tomber silencieusement du dénominateur.

## P1-3 — Les deux constantes de l'économie alimentaire ne sont pas dans la même unité de temps (cause racine de P1-2)

**Preuve.** `sim/SEEDING.md` :

- ligne 64 : `FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK = 2.0`, justifiée
  par « ration **journalière** médiévale approx. 2 kg » → un tick = 1 jour ;
- ligne 81 : `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK = 50.0`, justifiée par
  « ~500 kg/ha/an ÷ 10 (portion cultivée) ÷ **100 (ticks/an proxy)** » →
  un tick = 3,65 jours ;
- ligne 65 : `INITIAL_FOOD_DAYS = 30`, unité déclarée « ticks », nom
  déclarant « days ».

La durée d'un tick n'est définie nulle part, et les deux constantes
supposent deux durées différentes. Les deux réparations possibles donnent
deux mondes opposés, ce qui montre que l'équilibre actuel n'est pas un
choix documenté mais un artefact :

- tick = 1/100 année : la ration devient 7,3 kg/tick, soit 73 kg/km²/tick
  contre 50 produits → **toutes** les cellules entrent en famine ;
- tick = 1 jour : la production devient 500 ÷ 10 ÷ 365 × 100 ha ≈ 13,7
  kg/km²/tick contre 20 consommés → famine également.

Autrement dit, la seule paramétrisation où rien ne se passe est celle
livrée, et elle repose sur un mélange d'unités.

## P1-4 — La preuve de déterminisme du tick ne mesure rien : `tick()` ne consomme jamais son générateur aléatoire

**Preuve — mesure rejouable (sortie complète au § 5) :**

```
hash monde graine=42, rng=42      : 3d41d13dec0c35bc26d423e580a200b27f1edde5fe7d7a90314f82d3e85e50a8
hash monde graine=42, rng=999999  : 3d41d13dec0c35bc26d423e580a200b27f1edde5fe7d7a90314f82d3e85e50a8
les deux condenses sont egaux     : True
etat interne du rng inchange apres 10 ticks : True True
```

`sim/engine.py` reçoit `rng: random.Random` dans `tick()` et aucune des
quatre fonctions de maillon ne l'utilise : le tick est intégralement
déterministe parce qu'il ne contient aucun aléa. Le compteur
`ticks_deterministes_valides = 1` (manifeste) et la condition SC5 sont
donc satisfaits par un test qui ne peut pas échouer, quelle que soit
l'implémentation. Le second test (`test_tick_different_seeds_differ`) ne
passe que grâce à l'aléa de l'**amorçage**, pas du tick.

Deux corrections possibles, symétriques et toutes deux honnêtes :
consommer réellement le générateur dans un maillon (variabilité de
rendement, par exemple), ou retirer le paramètre et l'affirmation de
déterminisme qui l'accompagne. Ce qui n'est pas tenable, c'est de garder
les deux.

**Source externe** : S4 — une vérification n'a de valeur que si l'auteur
du code ne peut pas la faire passer par construction.

## P2-1 — `World.adjacency` : 1364 arêtes chargées, jamais lues — et le test SC8 est structurellement incapable de le voir

**Preuve.** Aucune lecture de `adjacency` hors du chargement lui-même :

```
$ rg -n "adjacency" sim/*.py
sim/world.py:25:_ADJACENCY_PATH = ...
sim/world.py:50:    def __init__(self, cells: dict, adjacency: list):
sim/world.py:52:        self.adjacency = adjacency
sim/world.py:64:        raw_adjacency_doc = json.loads(...)
sim/world.py:67:        raw_adjacency = raw_adjacency_doc["adjacency"]
sim/world.py:84:        return cls(cells=cells, adjacency=raw_adjacency)
```

C'est exactement le mode d'échec n°2 de
`docs/rules/simulation-principles.md` (un état écrit que personne ne lit)
que SC8 était censé rendre impossible. Le test livré ne peut pas le
détecter : il itère sur `dataclasses.fields(Cell)`
(`sim/tests/test_write_coverage.py` lignes 103 et 169), et `World` est une
classe ordinaire, pas une dataclass. Le compteur `champs_modele_couverts`
vaut donc 5 sur 5 en excluant du dénominateur le seul champ sans lecteur.
`World.to_dict()` exclut également `adjacency`, donc l'empreinte SHA256 de
l'état ne le couvre pas non plus.

Le non-but 1 du brief (pas de commerce inter-cellules) justifie qu'aucun
maillon ne s'en serve **encore** ; il ne justifie pas que le compteur de
couverture affiche une couverture complète.

## P2-2 — Le gate ne vérifie le suivi git que de 2 fichiers déclarés sur 22

**Preuve** — dernière ligne de la sortie du gate (§ 5) :

```
[PASS] declared_files_are_tracked: all 2 in-brief declared files are tracked; 20 declared outside the brief dir, not checked: ['../../../../sim/__init__.py', ..., '../../../../harness/queue/cost-ledger.jsonl']
```

Le manifeste déclare tous les livrables `sim/` par des chemins
`../../../../`, que `harness/verdict_audit.py` (lignes 156-183) classe
« hors du dossier du brief, non vérifiés ». Le contrôle né du défaut du
brief 003 — une preuve ignorée par git est une preuve que personne ne peut
re-vérifier — est donc neutralisé pour l'intégralité du lot. Ici les
fichiers **sont** suivis (ils sont dans le diff), donc l'effet est nul
cette fois ; c'est la garde qui est perdue, pas la preuve.

Point connexe, même famille : le § Execution Contract du brief impose une
paire `must_differ_from` sous la forme d'une liste de paires à la racine
du manifeste (brief.md lignes 295-301), forme que le gate ne lit pas — il
lit une clé `must_differ_from` **par fichier** (`verdict_audit.py` lignes
205-207). Le Générateur a écrit la forme que le gate lit, donc le contrôle
a réellement tourné ; mais un lot qui suivrait le brief à la lettre
obtiendrait un `captured_differ_when_should` vide et vert. La spec et le
gate divergent.

## P2-3 — Le budget d'exécution exigé par le brief n'est ni mesuré ni waivé

**Preuve.** Le § Execution Contract du brief impose une commande de
pré-vol (`harness/budget.py split-check ... --estimated-calls 120`), un
checkpoint à 130 appels et un arrêt dur à 160. Recherche dans le journal
du Générateur :

```
$ rg -n -i "budget|appels d'outils|split-check|UNMEASURABLE" harness/queue/briefs/011-sim-monde-vivant-amorcage/deliverables/generator-log.md
(aucune correspondance)
```

`"waivers": []` dans le manifeste : aucune impossibilité déclarée non
plus. Le budget n'a donc ni valeur mesurée, ni dérogation motivée — alors
que `AGENTS.md` documente que `budget.py status` est `UNMEASURABLE` hors
session Claude locale, ce qui était précisément le cas ici et aurait pu
être déclaré en une ligne.

**Source externe** : S5 — un compteur de dépense post-hoc n'est pas une
porte ; la seule garde utile est celle qui refuse **avant**, et un budget
qu'on ne mesure pas n'existe pas.

## P2-4 — La mortalité est bien un « si faim ≥ seuil alors −N% », et `sim/SEEDING.md` restreint l'interdiction du brief aux seuls tests

**Preuve.** `sim/engine.py`, `_apply_mortality` :

```python
if cell.hunger_ticks >= HUNGER_DEATH_THRESHOLD:
    deaths = cell.population * HUNGER_DEATH_RATE_PER_TICK
```

Le brief interdit ce motif en toutes lettres (§ World-Terms Requirement) :
« Aucun résultat n'est codé en dur sous la forme "si faim alors +N% de
mortalité" ; la mortalité émerge de la lecture de l'état de faim
accumulé. » `sim/SEEDING.md` lignes 92-94 reformule l'interdiction ainsi :
« elles ne sont jamais codées directement comme "si faim alors +N% de
mortalité" **dans les tests** » — restriction qui n'est pas dans le brief.

Sur le fond, `hunger_ticks` est bien lu, mais il n'agit que comme un
interrupteur : une cellule affamée depuis 3 ticks et une affamée depuis
300 meurent au même taux, et l'ampleur du manque n'est jamais un état.
`_apply_consumption` écrase le déficit (`remaining if remaining >= 0.0
else 0.0`) : la nourriture manquante disparaît sans être comptée, ce qui
est aussi un accroc au principe 3 (rien ne se téléporte). Rendre la
mortalité fonction du déficit accumulé, et non d'un seuil binaire,
fermerait les deux points d'un coup.

## P2-5 — Aucun des 20 tests `sim/` ne tourne en intégration continue

**Preuve.** `.github/workflows/harness-ci.yml`, job `tests` :
`run: python -m pytest harness/tests/ -v`. Aucun job ne collecte
`sim/tests/`. La CI du commit audité est verte (§ 6) **sans avoir exécuté
une seule ligne** du code livré par cette PR ; les deux tests qui gardent
les modes d'échec n°2 et n°5 (`test_write_coverage.py`,
`test_no_hardcoded.py`) ne sont donc appliqués que par la mémoire d'un
agent. La description de PR le signale honnêtement comme réserve connue ;
le constat reste, parce que la lentille 3 (« portes mécaniques d'abord »)
est exactement ce qui n'est pas tenu.

## P3-1 — Deux noms d'événement différents pour la même opération dans le registre de coût

`harness/queue/cost-ledger.jsonl` gagne deux lignes, l'une avec
`"event": "generator_run"`, l'autre avec `"event": "generator-run"`, alors
que les 34 lignes précédentes et le défaut de l'outil
(`harness/backends/ledger.py` ligne 392 : `default="generator-run"`)
utilisent le tiret. `ledger.py report` compte bien les deux
(`011-sim-monde-vivant-amorcage: cursor=2`, § 5), donc l'effet est
aujourd'hui nul — c'est une divergence de forme dans un fichier destiné à
être analysé mécaniquement.

## P3-2 — Nommages trompeurs dans l'amorçage

`sim/world.py`, `_seed_food_stock` : la variable locale s'appelle
`daily_need` alors qu'elle multiplie une consommation **par tick** ;
`INITIAL_FOOD_DAYS` (`sim/constants.py`) porte « DAYS » dans son nom pour
une valeur documentée en ticks. Même famille que P1-3 : le vocabulaire du
temps n'est pas fixé.

# 4. Lentille 5 — Taille et découpage

28 fichiers, +3098 lignes, 12 conditions de succès, un sous-système neuf :
au-delà de ce qu'une relecture honnête relie à l'intention (le guide
place la limite autour de 5 fichiers / quelques centaines de lignes).
Le harnais possède déjà l'outil de pré-vol prévu pour cela
(`budget.py split-check`, avis `NEEDS_SPLIT`) et il n'a pas été exécuté
(P2-3). Un découpage naturel existait : (a) chargement du monde +
conformité ADR-0003, (b) économie alimentaire + chaîne causale, (c) tests
de garde structurels (couverture d'écriture, littéraux). Ce constat est
consigné ici comme information de méthode ; il ne demande pas de refaire
la PR.

# 5. Commandes rejouées et sorties

Toutes les commandes ci-dessous ont été exécutées sur un worktree du
commit audité `3b47ffe`, depuis la racine, avec l'interpréteur du dépôt.

**5.1 — Gate mécanique** (`.venv/bin/python harness/verdict_audit.py harness/queue/briefs/011-sim-monde-vivant-amorcage`) :

```
# verdict_audit report for harness/queue/briefs/011-sim-monde-vivant-amorcage
[PASS] files_declared_exist: all declared files present
[PASS] mtime_after_brief: all deliverables postdate the brief
[PASS] captures_differ_when_should: all declared pairs differ
[PASS] waivers_have_command_and_error: all waivers carry a command and an error
[PASS] no_empty_sample_pass: every counter has a real sample_size
[PASS] verdict_numbers_traceable: all cited numbers trace to manifest.json
[PASS] no_bare_python_alias: no bare `python` invocations found
[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 2 examined pair(s): forge-generateur<->forge-evaluateur; forge-generateur<->forge-evaluateur
[PASS] rubric_predates_deliverables: rubric (2026-08-12 15:57:00) predates earliest deliverable (2026-08-12 16:58:26.815947)
[PASS] declared_files_are_tracked: all 2 in-brief declared files are tracked; 20 declared outside the brief dir, not checked: ['../../../../sim/__init__.py', '../../../../sim/constants.py', '../../../../sim/model.py', '../../../../sim/world.py', '../../../../sim/engine.py', '../../../../sim/SEEDING.md', '../../../../sim/README.md', '../../../../sim/tests/__init__.py', '../../../../sim/tests/test_world.py', '../../../../sim/tests/test_adr_compliance.py', '../../../../sim/tests/test_seeding.py', '../../../../sim/tests/test_engine.py', '../../../../sim/tests/test_causal_chain.py', '../../../../sim/tests/test_write_coverage.py', '../../../../sim/tests/test_no_hardcoded.py', '../../../../sim/tests/proof_red/run_sabotage.txt', '../../../../sim/tests/proof_red/run_correct.txt', '../../../../sim/tests/proof_red/run_phantom_red.txt', '../../../../sim/tests/proof_red/run_phantom_green.txt', '../../../../harness/queue/cost-ledger.jsonl']

VERDICT: ACCEPT
```

**5.2 — Suites de tests** (les deux affirmations de la description de PR
se reproduisent) :

```
$ .venv/bin/python -m pytest sim/tests/ -q
....................                                                     [100%]
20 passed in 0.40s

$ .venv/bin/python -m pytest harness/tests/ -q
314 passed, 16 skipped in 17.00s
```

**5.3 — Mesure de la chaîne faim → mortalité sur le monde réel**
(constat P1-2). Script exécuté tel quel :

```python
import random
from sim.world import World
from sim import engine
from sim.constants import (FOOD_PRODUCTION_KG_PER_KM2_PER_TICK as P,
                           FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK as C,
                           INITIAL_POPULATION_PER_KM2 as D)
w = World.from_g3(rng_seed=42)
rng = random.Random(42)
areas = [c.area_km2 for c in w.cells.values()]
print("cellules chargees      =", len(w.cells))
print("aretes adjacence       =", len(w.adjacency))
print("area_km2 min/max       =", min(areas), max(areas))
print("production par km2     =", P, "  consommation par km2 (densite nominale) =", C*D)
pop0 = sum(c.population for c in w.cells.values())
stock0 = sum(c.food_stock_kg for c in w.cells.values())
N = 200
for _ in range(N):
    engine.tick(w, rng)
pop1 = sum(c.population for c in w.cells.values())
stock1 = sum(c.food_stock_kg for c in w.cells.values())
faim = [c.cell_id for c in w.cells.values() if c.hunger_ticks > 0]
vides = [c.cell_id for c in w.cells.values() if c.food_stock_kg <= 0.0]
print(f"apres N={N} ticks :")
print("  population totale      %d -> %d (delta %d)" % (pop0, pop1, pop1-pop0))
print("  stock total (kg)       %.0f -> %.0f (facteur x%.1f)" % (stock0, stock1, stock1/stock0))
print("  cellules avec hunger_ticks > 0 :", len(faim))
print("  cellules avec food_stock_kg <= 0 :", len(vides))
```

Sortie :

```
cellules chargees      = 596
aretes adjacence       = 1364
area_km2 min/max       = 1.444877 37217.766826
production par km2     = 50.0   consommation par km2 (densite nominale) = 20.0
apres N=200 ticks :
  population totale      66865505 -> 66865505 (delta 0)
  stock total (kg)       4011930300 -> 43937193599 (facteur x11.0)
  cellules avec hunger_ticks > 0 : 0
  cellules avec food_stock_kg <= 0 : 0
```

**5.4 — Le tick n'utilise pas son générateur aléatoire** (constat P1-4).
Script exécuté tel quel :

```python
import random, hashlib, json
from sim.world import World
from sim import engine
def h(seed_world, seed_rng):
    w = World.from_g3(rng_seed=seed_world)
    rng = random.Random(seed_rng)
    st_before = rng.getstate()
    for _ in range(10):
        engine.tick(w, rng)
    st_after = rng.getstate()
    return hashlib.sha256(json.dumps(w.to_dict(), sort_keys=True).encode()).hexdigest(), st_before == st_after
hA, unchangedA = h(42, 42)
hB, unchangedB = h(42, 999999)
print("hash monde graine=42, rng=42      :", hA)
print("hash monde graine=42, rng=999999  :", hB)
print("les deux condenses sont egaux     :", hA == hB)
print("etat interne du rng inchange apres 10 ticks :", unchangedA, unchangedB)
print("adjacency presente dans to_dict() :", "adjacency" in World.from_g3().to_dict())
```

Sortie :

```
hash monde graine=42, rng=42      : 3d41d13dec0c35bc26d423e580a200b27f1edde5fe7d7a90314f82d3e85e50a8
hash monde graine=42, rng=999999  : 3d41d13dec0c35bc26d423e580a200b27f1edde5fe7d7a90314f82d3e85e50a8
les deux condenses sont egaux     : True
etat interne du rng inchange apres 10 ticks : True True
adjacency presente dans to_dict() : False
```

**5.5 — Registre de coût** (constat P3-1) :

```
$ .venv/bin/python harness/backends/ledger.py report   (extrait)
  harness/queue/briefs/011-sim-monde-vivant-amorcage: cursor=2
```

**5.6 — Compteurs du manifeste re-mesurés** (contrôle de la lentille 2,
aucun écart trouvé) :

```
$ diff sim/tests/proof_red/run_sabotage.txt sim/tests/proof_red/run_correct.txt | wc -l
53
```

Le manifeste déclare 53 pour ce compteur, et déclare séparément un
compteur d'archive valant 70 (mesure de l'itération 1). Le verdict
signale lui-même cette anomalie en réserve R1 (la commande déclarée pour
le compteur d'archive ne produit pas sa valeur) : le point est donc déjà
consigné par la boucle et n'est pas soulevé ici comme constat, seulement
noté pour que la re-mesure soit complète.

# 6. Classification de la CI du commit audité

Commit `3b47ffe` — **verte**, sans exécution du code livré.

| job | état | remarque |
|---|---|---|
| `tests` (harness-ci) | pass | ne collecte que `harness/tests/` — voir P2-5 |
| `f0-demo` (harness-ci) | pass | rejet du faux brief |
| `actionlint`, `gitleaks`, `schema` (security) | pass | — |
| `invoke-cursor-auditor` (pipeline-audit) | pass | déclencheur de cette critique |
| `Reconcile local Hermes state` | pass | — |
| `cursor-scope` | skipping | ne s'applique qu'aux branches `cursor/*` |
| `check-and-automerge` | skipping | attendu : ni `sim/**` ni `ROADMAP.md` ne sont dans `auto_merge_allowlist` (`harness/pipeline/config.yaml` lignes 52-57), donc aucune fusion automatique n'était possible |

# 7. Risques par sévérité

| sévérité | constat | risque si rien n'est fait |
|---|---|---|
| P0 | P0-1 — état final non jugé, acteur des rôles non tracé | La garantie « qui produit ne juge pas » devient déclarative ; n'importe quel lot futur peut la satisfaire en tapant deux chaînes différentes, et un commit post-verdict peut entrer sans relecture. |
| P1 | P1-1 — `ROADMAP.md` hors périmètre | Le périmètre d'écriture d'ADR-0010 cesse d'être opposable ; la feuille de route devient modifiable par n'importe quel rôle « en passant ». |
| P1 | P1-2 — chaîne causale inatteignable sur le monde réel | La couche 1 est déclarée « commencée » alors qu'aucun événement de population ne peut survenir ; les couches 2+ se construiraient sur une dynamique morte. |
| P1 | P1-3 — unités de temps incohérentes | Toute future constante (natalité, rendement par culture, transport) héritera d'un tick sans durée définie ; le coût de correction croît à chaque brief. |
| P1 | P1-4 — déterminisme non mesuré | Le jour où un maillon deviendra réellement aléatoire, le test existant continuera de passer sans rien garantir. |
| P2 | P2-1 — `adjacency` écrit jamais lu, invisible pour SC8 | Le mode d'échec n°2 est présent dans le premier lot du moteur, avec un compteur qui affiche 5/5. |
| P2 | P2-2 — 20 fichiers déclarés hors contrôle de suivi git | Un lot futur pourra déclarer des preuves ignorées par git sans que le gate le voie. |
| P2 | P2-3 — budget ni mesuré ni waivé | Les seuils 120/130/160 deviennent décoratifs ; aucune dérive de coût n'est détectable. |
| P2 | P2-4 — mortalité binaire, déficit non enregistré | L'ampleur d'une famine n'existe pas comme état : toute future règle économique lira un signal appauvri. |
| P2 | P2-5 — `sim/tests/` hors CI | Une régression dans le moteur passera la CI au vert. |
| P3 | P3-1, P3-2 — noms d'événement et de constantes | Bruit d'analyse et confusion d'unités. |

# 8. Briefs atomiques proposés (3, jamais plus)

Ces propositions ne sont **pas** des instructions et ne s'autorisent
rien : la source unique d'instruction reste un brief, écrit après décision
du propriétaire (`CLAUDE.md` › Single Source of Instruction).

1. **Durée du tick et équilibre alimentaire mesuré sur le monde réel.**
   Fixer la durée d'un tick en un seul endroit documenté, réaligner
   production, consommation et stock initial sur cette durée, enregistrer
   le déficit non couvert comme un état lu par la mortalité, puis exiger
   un compteur mesuré **sur les 596 cellules chargées** (nombre de
   cellules connaissant la faim et nombre de morts après N ticks), et non
   sur une cellule construite à la main. Ferme P1-2, P1-3, P2-4, P3-2.
2. **Rendre les gardes structurelles capables d'échouer.** Étendre la
   couverture d'écriture à l'état du monde entier (pas seulement aux
   champs de dataclasses), y faire entrer `adjacency` — soit en lui
   donnant un lecteur, soit en le retirant du chargement —, et trancher
   sur `rng` dans `tick()` : le consommer réellement ou supprimer le
   paramètre et l'affirmation de déterminisme associée. Ferme P1-4, P2-1.
3. **Tracer mécaniquement l'acteur de chaque rôle et le périmètre du
   lot.** Enregistrer, pour chacun des trois rôles, l'acteur réel
   (identité git + backend) dans le registre, faire porter le contrôle de
   séparation sur cette trace plutôt que sur une chaîne auto-déclarée, et
   faire refuser le lot quand un commit est postérieur au verdict ou hors
   du périmètre déclaré par le brief. Ferme P0-1, P1-1, et rend P2-2
   observable. Ajouter `sim/tests/` à la CI relève du même sujet
   (P2-5) mais peut aussi vivre seul.

# 9. Sources externes

| # | source | date | consulté le |
|---|---|---|---|
| S1 | Flamehaven — *Role Separation Is Not Verification: The Structural Failures Hidden in Your Multi-Agent Pipeline* — <https://flamehaven.space/writing/role-separation-is-not-verification-the-structural-failures-hidden-in-your-multi-agent-pipeline/> — « une étiquette de relecteur ne rend pas un auditeur indépendant ; ce qui compte est l'isolation épistémique » | non datée par l'éditeur | 2026-08-12 |
| S2 | Augment Code — *Agentic SDLC Implementation: The Coordinator-Implementor-Verifier Pattern* — <https://www.augmentcode.com/guides/agentic-sdlc-coordinator> — identités et contextes séparés, worktrees par agent, vérification adossée à la spec d'origine | non datée par l'éditeur | 2026-08-12 |
| S3 | *GoGoTB: Agentic RTL Verification with Specification-Grounded Coverage Closure*, arXiv:2607.26181 — <https://arxiv.org/html/2607.26181> — chaque cas structurellement inatteignable est classé et diagnostiqué, aucun trou silencieusement retiré du dénominateur | 2026-07 (identifiant arXiv) | 2026-08-12 |
| S4 | *The Kitchen Loop: User-Spec-Driven Development for a Self-Evolving Codebase*, arXiv:2603.25697 — <https://arxiv.org/html/2603.25697> — « Unbeatable Tests » : une vérification que l'auteur du code ne peut pas truquer | 2026-03 (identifiant arXiv) | 2026-08-12 |
| S5 | *Token Budgets: An Empirical Catalog of 63 LLM-Agent Budget-Overrun Incidents*, arXiv:2606.04056v1 — <https://arxiv.org/html/2606.04056v1> — un compteur post-hoc n'est pas une porte ; seule une garde qui refuse avant l'appel borne la dépense | 2026-06 (identifiant arXiv) | 2026-08-12 |

Les cinq sources internes du référentiel de critique
(`architecture/review-guidelines.md` § Sources, S1–S5 de ce fichier-là)
restent celles qui définissent la forme de cet audit ; les sources
ci-dessus sont celles ajoutées pour ce commit, conformément à la preuve de
fin du contrat `architecture/agents/cursor-auditor.md`.
