# Verdict — Brief `010`

## Évaluation — lot 010a (contrat des rôles et anti-auto-jugement multi-backend)

**Authored**: 2026-08-11T13:02:12
**Author**: forge-evaluateur

Commit jugé : `62a0fe2` (branche `forge/010a-contrat-roles`). Conditions
jugées : SC1, SC2, SC3, SC3b, SC4, SC5, SC6 — et elles seules.

### Avertissement préalable : ici, le producteur et le juge sont le même acteur

Ce lot a été produit par Claude et ce verdict est écrit par Claude. C'est une
décision du propriétaire du `2026-08-11`, inscrite dans l'Execution Contract
du brief, et elle est assumée — mais elle doit être dite pour ce qu'elle est.

Le contrôle corrigé par ce lot **ne sait pas détecter ce cas** : les deux
signatures sont les rôles nus, sans suffixe d'acteur (`forge-generateur` d'un
côté, `forge-evaluateur` de l'autre), et le contrôle en déduit deux acteurs
distincts. C'est le troisième angle mort consigné dans `HANDOFF.md`. Aucune
condition de ce brief ne le couvrait ; il n'est donc pas compté contre le
Générateur, mais il signifie ceci : **la séparation entre production et
jugement repose ici sur la grille écrite avant le travail (`eval-rubric.md`,
horodatée avant le premier livrable) et sur ma discipline, pas sur une
mécanique.** Ce n'est pas une garantie. `verdict.md` étant append-only, une
passe ultérieure par un autre acteur reste possible et n'effacerait rien de
ce qui suit.

### Gate mécanique

Exécuté par moi après écriture de la section ci-dessus :
`py harness/verdict_audit.py harness/queue/briefs/010-repartition-roles-full-auto`.
Sortie intégrale et code de retour : voir la fin de cette section
(« Gate mécanique rejoué »). Rappel de méthode : le gate est nécessaire et
non suffisant. Il a répondu ACCEPT sur des lots ensuite rejetés à juste
titre ; ce qui suit ne s'appuie sur aucun chiffre repris du manifeste sans
avoir été recalculé.

Méthode de reconstruction, pour toutes les preuves rouges : le code d'avant
le lot a été extrait par `git show 62a0fe2~1:harness/verdict_audit.py` (plus
sa dépendance `bare_python.py`) vers une copie jetable **hors du dépôt**, et
exécuté depuis cette copie. Le code d'après est une copie jetable du fichier
actuel du dépôt. L'arbre de travail du dépôt n'a été modifié à aucun moment.
Les jeux d'essai (« fixtures ») sont les miens, construits hors du dépôt —
je n'ai pas réutilisé ceux du Générateur.

### Condition par condition

| SC | Verdict | Preuve que j'ai produite moi-même |
|---|---|---|
| SC1 | PASS | `docs/adr/0008-codex-as-evaluateur-under-credit-cap.md`, lu en entier |
| SC2 | PASS | `py -m pytest harness/tests/test_single_source_of_instruction.py -q` + lecture du diff de la règle |
| SC3 | PASS | rouge/vert rejoués sur ma fixture `e3` |
| SC3b | **FAIL (partiel)** | rouge/vert rejoués sur `e3b` : l'exigence nommée est tenue ; la clause générale « chaque couple » ne l'est pas (sonde `adv1`) |
| SC4 | PASS | rejoué avec un acteur inventé par moi, `korrigan`, absent du dépôt |
| SC5 | PASS | gate avant/après rejoué sur les `11` répertoires de brief |
| SC6 | PASS | `py harness/verdict_audit.py harness/queue/briefs/009-full-auto-agent-invocation` |

#### SC1 — l'ADR `0008` — PASS

Commande : lecture intégrale de
`docs/adr/0008-codex-as-evaluateur-under-credit-cap.md` et
`git diff 62a0fe2~1 62a0fe2 -- docs/adr/README.md`.

Les quatre points sont présents, nommés, et non paraphrasés vaguement :
(a) « Codex may hold the Évaluateur role » (section Decision) ; (b) « Only in
a session distinct from, and triggered by a party other than, the one that
produced the lot… never the Générateur session itself, and never a sub-agent
that session spawns » ; (c) l'option sous-agent est écartée **explicitement**
(« and not merely left unmentioned ») avec sa raison écrite — le producteur
rédige l'instruction de son juge, choisit les preuves montrées et consolide
la réponse — et elle est reprise en Alternative `2` puis en Risques ;
(d) « The triggering fact is Claude's credit cap being reached — not
convenience ». Champ `Status` : `accepted`, non vide. `docs/adr/README.md`
a bien gagné sa ligne `0008`, avec statut et date.

Le piège nommé par la grille (« une formulation qui autorise implicitement le
sous-agent en n'en parlant pas ») est évité de façon vérifiable : le refus du
sous-agent est écrit trois fois, dont une en anticipation de l'évasion par
renommage.

#### SC2 — la règle, source unique — PASS

Commande et sortie : `py -m pytest harness/tests/test_single_source_of_instruction.py -q`
→ `- 1 passed in 0.18s`

Lecture du diff : la ligne « Évaluateur » du tableau de
`docs/rules/harness-roles.md` ne dit plus que le rôle est réservé à Claude
(elle renvoie à l'exception), et une section nouvelle énonce les trois
conditions en pointant vers l'ADR. Le critère de refus de la grille (« le
fichier de règle dit encore que l'Évaluateur est réservé à Claude ») ne
s'applique donc plus.

Réserve, non bloquante : le test ci-dessus ne vérifie que l'absence de
titres de brief hors `brief.md` — il ne prouve pas l'absence de paraphrase.
Je l'ai donc vérifiée à la lecture. La section « Decision » de l'ADR énonce
bien (a)-(d) en langage normatif, mais SC1 l'exige explicitement, et l'ADR
dit lui-même qu'il n'est pas une seconde surface d'instruction et renvoie au
fichier de règle. Aucun troisième fichier ne les reprend.

#### SC3 — acteur, pas rôle — PASS

Ma fixture `e3` : journal signé `**Author**: forge-generateur-korrigan`,
verdict signé `**Author**: forge-evaluateur-korrigan`.

Rouge, contre le code d'avant le lot
(`py <copie-jetable>/pre/verdict_audit.py <fixture e3>`) :

- `[PASS] verdict_is_not_self_authored: generator=forge-generateur-korrigan, evaluator=forge-evaluateur-korrigan`
- `VERDICT: ACCEPT` — `exit=0`

Vert, contre le code actuel du dépôt
(`py <copie-jetable>/post/verdict_audit.py <fixture e3>`) :

- `[FAIL] verdict_is_not_self_authored: same actor on 1/1 examined pair(s): forge-generateur-korrigan==forge-evaluateur-korrigan`
- `VERDICT: REJECT`

Le défaut était bien une **absence de refus** dans le code d'avant, et le
refus existe dans le code d'après. J'ai aussi vérifié que la copie déclarée
par le Générateur comme « code d'avant »
(`deliverables/proofs/pre-fix/verdict_audit.py.orig`) est **identique** à ce
que rend `git show 62a0fe2~1:harness/verdict_audit.py` — `diff -q` muet. La
preuve rouge du Générateur n'est donc pas une reconstitution complaisante.

#### SC3b — tous les couples, pas seulement le premier — FAIL (partiel)

Ce qui est tenu, et je le reconstruis : ma fixture `e3b` (journal
`forge-generateur` puis `forge-generateur-korrigan` ; verdict
`forge-evaluateur` puis `forge-evaluateur-korrigan`, le second couple étant
auto-jugé).

- avant : `[PASS] verdict_is_not_self_authored: generator=forge-generateur, evaluator=forge-evaluateur` → `VERDICT: ACCEPT`
- après : `[FAIL] verdict_is_not_self_authored: same actor on 1/2 examined pair(s): forge-generateur-korrigan==forge-evaluateur-korrigan` → `VERDICT: REJECT`

L'exigence littérale de SC3b (le cas à deux lots équilibré) est donc
satisfaite, red-first, et le raccourci que la grille interdit — « lire le
dernier auteur au lieu du premier » — n'a pas été pris.

Ce qui n'est **pas** tenu : la clause d'entrée de SC3b dit que le contrôle
examine *chaque* couple auteur du brief. La règle retenue apparie les `k`
derniers auteurs de chaque fichier avec `k = min(nombre d'auteurs du journal,
nombre d'auteurs du verdict)`. Quand le journal porte plus d'entrées que le
verdict, les entrées de journal les plus anciennes sont **jetées sans être
examinées**. Ma sonde `adv1` : journal = `forge-generateur-korrigan` (lot 1)
puis `forge-generateur` (lot 2) ; verdict = `forge-evaluateur-korrigan` (le
lot 1 jugé par son propre producteur).

- après correctif : `[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 1 examined pair(s): forge-generateur<->forge-evaluateur-korrigan` → `VERDICT: ACCEPT`

Le seul couple auto-jugé du dossier n'a jamais été regardé : il est sorti par
la troncature. Ce n'est pas une hypothèse d'école — c'est exactement la forme
que prendra ce brief-ci quand `010b` sera produit alors que `010a` n'a qu'un
verdict.

#### SC4 — généralité, sans liste en dur — PASS

J'ai inventé un nom d'acteur, `korrigan`, et vérifié qu'il n'apparaît nulle
part dans le dépôt (recherche insensible à la casse sur tout l'arbre : aucun
fichier). Aucune modification du contrôle entre la mesure de SC3 et
celle-ci. Résultat déjà cité en SC3 : refus, `VERDICT: REJECT`.

J'ai aussi lu le corps du contrôle : `_actor_suffix` ne fait que retirer le
préfixe de rôle ; les seules occurrences de `codex` ou `cursor` dans
`harness/verdict_audit.py` sont dans les commentaires explicatifs, jamais
dans une condition. Aucune énumération de backends.
Réserve mineure : le test livré, `test_unseen_actor_name_is_refused_without_naming_it_in_the_control`,
utilise `gemini`, qui figure déjà dans `docs/adr/0002-pluggable-generator-backend.md` ;
le test n'assure l'absence du nom que dans le contrôle lui-même, pas dans
tout le dépôt comme SC4 le demande à la lettre. Ma propre exécution avec
`korrigan` établit la généralité indépendamment, donc je ne compte pas ce
détail contre le lot.

#### SC5 — aucune invalidation rétroactive — PASS

Ma commande : boucle sur `harness/queue/briefs/*/`, exécutant pour chaque
répertoire la copie jetable d'avant puis celle d'après, et extrayant le seul
statut de `verdict_is_not_self_authored`. Nombre de répertoires compté par
`ls -d harness/queue/briefs/*/ | wc -l` → `11`.

- `001-spatial-primary-key-adr    avant=PASS  apres=PASS`
- `002-geo-pipeline-coastline-1400    avant=PASS  apres=PASS`
- `003-port-unity-game    avant=PASS  apres=PASS`
- `004-polish-visuel    avant=PASS  apres=PASS`
- `005-refonte-visuelle-carte    avant=PASS  apres=PASS`
- `006-full-auto-agent-pipeline    avant=PASS  apres=PASS`
- `007-geo-pipeline-cells-adjacency    avant=PASS  apres=PASS`
- `008-contexte-opus5-right-sizing    avant=FAIL  apres=FAIL`
- `008-full-auto-automation-gaps    avant=PASS  apres=PASS`
- `009-full-auto-agent-invocation    avant=PASS  apres=PASS`
- `010-repartition-roles-full-auto    avant=FAIL  apres=FAIL`

`11` répertoires comparés, zéro passage PASS→FAIL. Le chiffre du manifeste
est reconstruit à l'identique. Les deux FAIL préexistants le restent pour la
même cause qu'avant (auteur absent, faute de `verdict.md`).

Réserve de méthode, qui n'enlève rien à SC5 mais compte pour la suite : SC5
ne mesure que le sens PASS→FAIL sur les répertoires **existants**. Le brief
demande pourtant la preuve « dans les deux sens » (Non-objectif `7`). Le sens
FAIL→PASS n'est couvert par aucune des sept conditions, et c'est précisément
là que ce lot échoue — voir la section suivante.

#### SC6 — le jugement croisé légitime passe toujours — PASS

Ma commande :
`py harness/verdict_audit.py harness/queue/briefs/009-full-auto-agent-invocation`

- `[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 2 examined pair(s): forge-generateur<->forge-evaluateur-codex; forge-generateur-codex<->forge-evaluateur`
- `VERDICT: ACCEPT` — `exit=0`

Le garde-fou inverse tient : le contrôle n'est pas devenu un refus-tout. Ma
fixture `e6` (deux lots honnêtes croisés) passe aussi, avec les deux couples
examinés. C'est bien un resserrement, pas une paralysie.

### Sondes adverses — ce qui met la règle d'appariement en défaut

La règle des `k` derniers couples est correcte sur le brief `009` réel. Elle
repose sur une hypothèse : que les entrées de verdict les plus récentes
parlent de l'état courant. J'ai cherché des agencements qui la mettent en
défaut, et j'en ai trouvé deux. Le second est disqualifiant.

**Sonde `adv1` — un couple auto-jugé échappe à l'examen** (détail en SC3b).
Journal plus long que le verdict : le couple auto-jugé, poussé hors de la
fenêtre, n'est jamais regardé. Avant le lot : ACCEPT. Après : ACCEPT. Pas une
régression, mais un angle mort qui reste ouvert alors que SC3b dit le
fermer.

**Sonde `adv3` / `adv4` — un cas refusé hier est accepté aujourd'hui.**
C'est le point bloquant. Fixture `adv4` : journal portant
`**Author**: forge-generateur` (lot 1) puis `**Author**: forge-generateur-korrigan`
(lot 2) ; `verdict.md` signé `**Author**: forge-generateur` — c'est-à-dire le
producteur signant son propre verdict, sans même changer de rôle. C'est la
signature d'auto-jugement la plus grossière qui soit, et c'est exactement ce
que le contrôle d'origine existait pour attraper.

- avant le lot : `[FAIL] verdict_is_not_self_authored: same author on both: forge-generateur` → `VERDICT: REJECT`
- après le lot : `[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 1 examined pair(s): forge-generateur-korrigan<->forge-generateur` → `VERDICT: ACCEPT`

Fixture `adv3`, même forme avec l'acteur en tête
(`**Author**: forge-generateur-korrigan` en première entrée du journal et
comme unique signature du verdict) :

- avant le lot : `[FAIL] verdict_is_not_self_authored: same author on both: forge-generateur-korrigan` → `VERDICT: REJECT`
- après le lot : `[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 1 examined pair(s): forge-generateur<->forge-generateur-korrigan` → `VERDICT: ACCEPT`

Cause : l'ancien contrôle comparait `read_field` à `read_field`, donc
toujours la **première** entrée de chaque fichier ; l'égalité stricte de ces
deux premières chaînes suffisait à refuser. Le nouveau contrôle n'examine
plus la première entrée du journal dès que le journal compte plus d'entrées
que le verdict. Ajouter une entrée de journal non encore jugée suffit donc à
faire disparaître un auto-jugement que le dépôt refusait la veille.

C'est littéralement le non-objectif `7` du brief : « toute modification qui
ferait passer un cas aujourd'hui refusé est disqualifiante ». Et c'est
littéralement la ligne disqualifiante de la grille pour ce lot : « rendre le
contrôle plus permissif, de quelque manière que ce soit. Ce lot ne peut que
resserrer. »

**Sonde `adv2`, non retenue contre le lot.** Journal à une entrée
(`forge-generateur-korrigan`), verdict à deux passes dont la première est
auto-jugée et la seconde indépendante : le contrôle n'examine que la seconde
et accepte. C'est défendable — une passe supplantée par une passe
indépendante ultérieure — et c'est le comportement que le Générateur a
explicitement choisi et argumenté. Je le consigne sans le compter en défaut.

### Compteurs reconstruits

| compteur | valeur du manifeste | ma reconstruction | accord |
|---|---|---|---|
| `self_authored_multibackend_refused_test_count` | `3` | `3` fonctions de refus lues dans `harness/tests/test_verdict_audit_actor_identity.py` (`test_same_actor_different_role_string_is_refused`, `test_self_judged_pair_in_second_lot_is_no_longer_invisible`, `test_unseen_actor_name_is_refused_without_naming_it_in_the_control`) | oui |
| `author_pairs_examined_per_brief` | `2` | `2` couples examinés sur le brief `009`, sortie du gate citée en SC6 | oui, avec réserve |
| `second_position_self_judgment_refused` | `1` | `1`, rejoué sur ma fixture `e3b` (avant : ACCEPT ; après : REJECT) | oui |
| `unknown_actor_refused_without_code_change` | `1` | `1`, rejoué avec `korrigan` | oui |
| `briefs_gate_verdict_unchanged_count` | `11` | `11`, table complète ci-dessus | oui |
| `cross_actor_judgment_still_accepted` | `1` | `1`, gate réel sur le brief `009` | oui |

Réserve sur `author_pairs_examined_per_brief` : le dénominateur exigé est
« le nombre réel de couples présents dans ces deux fichiers ». J'ai compté
les signatures à la source : le journal du brief `009` porte `2` champs
`Author` (lignes `1` et `596`), son `verdict.md` en porte `3` (lignes `4`,
`252`, `420`). Le contrôle en examine `2`. La valeur `2` est donc exacte pour
« couples examinés », mais elle masque qu'une signature d'évaluateur sur
trois n'est jamais regardée. Ce n'est pas un faux chiffre ; c'est un chiffre
dont la portée est plus étroite que ce que le mot « couples » laisse croire,
et c'est la même troncature que la sonde `adv1` exploite.

Suite complète du dépôt, rejouée par moi : `py -m pytest harness/tests/ -q`
→ `- 302 passed in 24.88s`. Aucun test existant n'a été affaibli : le diff
de `62a0fe2` sur `harness/tests/` n'ajoute qu'un fichier et n'en modifie
aucun.

### Frontières et non-objectifs

Vérifié par `git diff 62a0fe2~1 62a0fe2 --stat` : aucun fichier sous
`.github/workflows/`, aucun fichier de
`harness/queue/briefs/009-full-auto-agent-invocation/`, `VISION.md` non
touché, aucun `TODO(operator` retiré, aucun fichier des lots `010b`/`010c`.
Le lot est resté dans son périmètre. Une remarque de forme : le diff du
commit inclut aussi `architecture/decisions/DECISION-…` et
`harness/queue/cost-ledger.jsonl`, qui relèvent de la boucle d'audit et non
du lot ; ce n'est pas une violation de non-objectif.

### Gate mécanique rejoué

`py harness/verdict_audit.py harness/queue/briefs/010-repartition-roles-full-auto`,
exécuté avant l'écriture de cette section, alors que `verdict.md` n'existait
pas encore : tous les contrôles au vert **sauf** les deux qui dépendent de
`verdict.md` (`verdict_numbers_traceable` : fichier manquant ;
`verdict_is_not_self_authored` : signature d'auteur manquante), donc
`VERDICT: REJECT`, `exit=1`. Les deux échecs que le Générateur avait
constatés sur l'horodatage et sur `no_bare_python_alias` ont disparu depuis
la correction du brief par le Planificateur : `mtime_after_brief`,
`rubric_predates_deliverables` et `no_bare_python_alias` sont maintenant au
vert. Le gate est rejoué une dernière fois après l'écriture de ce verdict ;
son résultat ne change rien à ce qui suit, puisque le gate est nécessaire et
non suffisant, et que le motif de rejet ci-dessous est un cas d'espèce qu'il
ne teste pas.

### LOT_010a: REJECT

Un seul motif bloquant, et il est mécanique, pas une impression : **le
contrôle est devenu plus permissif sur une classe de cas qu'il refusait
avant ce lot** (sondes `adv3` et `adv4`, sorties recopiées ci-dessus). Le
brief le déclare disqualifiant en toutes lettres (non-objectif `7`), et la
grille écrite avant le travail aussi. S'y ajoute, comme conséquence de la
même cause, le manquement partiel de SC3b : la clause « chaque couple » n'est
pas honorée quand le journal compte plus d'entrées que le verdict.

Ce rejet ne porte pas sur la qualité du travail, qui est réelle et que je
tiens à porter au procès-verbal :

- la preuve red-first est authentique et je l'ai rejouée avec mes propres
  jeux d'essai et mon propre nom d'acteur — rien n'a été reconstitué après
  coup ; la copie « code d'avant » déclarée est bit à bit celle du dépôt ;
- SC1, SC2, SC4, SC5 et SC6 sont satisfaites sans réserve bloquante ;
- les deux angles morts nommés par le brief sont réellement fermés dans les
  cas qu'il nomme, et le garde-fou inverse tient (le jugement croisé
  légitime passe) ;
- le Générateur a écarté par écrit deux règles d'appariement plus simples,
  avec la raison de chaque rejet, et il a eu raison sur les deux : l'appariement
  par ensembles et l'appariement par l'avant refuseraient tous deux le brief
  `009` à tort. Le problème n'est pas d'avoir mal réfléchi ; c'est d'avoir
  laissé la troncature retirer des entrées de journal de l'examen.

### Ce qu'il faut corriger — voir `feedback/feedback-010a.md`

En deux mots, et sans que ce soit une instruction déguisée : rétablir
l'invariant que l'ancien contrôle assurait (aucune chaîne d'auteur ne figure
des deux côtés) **en plus** de l'appariement par acteur, et confronter les
entrées de journal écartées par la troncature à l'ensemble des auteurs du
verdict. J'ai vérifié que ces deux ajouts ne touchent pas au brief `009` :
l'intersection des chaînes d'auteur y est vide et aucune entrée de journal
n'y est écartée. Le détail, avec les cas à rejouer, est dans le feedback.

### Suite à donner, hors périmètre de ce lot

Le cas `forge-generateur` / `forge-evaluateur` — rôles nus, backend natif —
reste indétecté, et c'est sous ce cas que le présent verdict est écrit. Ce
n'est un échec ni de SC3 ni de SC4 : aucune condition de ce brief ne le
demandait. Le manque est dans la spécification, pas dans la production. Il
mérite un brief à lui seul, parce qu'il est le seul angle mort qui ne se
referme pas par une correction de code : distinguer deux sessions Claude
demande une trace que le dépôt ne porte pas encore.

### Post-scriptum — le gate, rejoué une fois ce verdict écrit

`py harness/verdict_audit.py harness/queue/briefs/010-repartition-roles-full-auto`
répond désormais `VERDICT: ACCEPT`, `exit=0` : les dix contrôles sont au
vert. Il faut lire attentivement l'un d'eux, parce qu'il illustre à lui seul
l'avertissement du haut de ce verdict :

- `[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 1 examined pair(s): forge-generateur<->forge-evaluateur`

Le contrôle affirme que producteur et juge sont des acteurs différents. Ils
ne le sont pas : c'est Claude des deux côtés. Le gate mécanique prononce donc
ACCEPT sur un lot que j'évalue REJECT, et il se trompe en outre sur la seule
chose qu'il croit avoir vérifiée à mon sujet. C'est la démonstration
concrète, sur ce lot même, que le gate est nécessaire et non suffisant — et
que la valeur de ce verdict tient à la grille écrite avant le travail, pas à
cette ligne verte.
