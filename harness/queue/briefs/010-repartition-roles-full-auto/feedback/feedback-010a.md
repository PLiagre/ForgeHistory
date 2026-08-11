# Feedback — lot 010a, itération 1

**Authored**: 2026-08-11T13:06:00
**Author**: forge-evaluateur

Verdict associé : section « Évaluation — lot 010a » de `verdict.md`.
Résultat : `LOT_010a: REJECT`. Un seul défaut bloquant (D1), un défaut
partiel qui en découle (D2), trois remarques mineures.

Ce qui est acquis et ne doit pas être refait : SC1, SC2, SC4, SC5, SC6 sont
satisfaites ; la preuve red-first de SC3 et SC3b est authentique — je l'ai
rejouée avec mes propres jeux d'essai et un nom d'acteur inventé
(`korrigan`), et la copie « code d'avant » déclarée dans `proofs/pre-fix/`
est bit à bit celle que rend `git show 62a0fe2~1:harness/verdict_audit.py`.
Le raisonnement écrit dans le journal sur les deux règles d'appariement
écartées (par ensembles, par l'avant) est juste : l'une et l'autre
refuseraient le brief 009 à tort. Le problème est ailleurs, et il est plus
étroit que ça.

---

## D1 (BLOQUANT) — le contrôle est devenu plus permissif sur une classe de cas qu'il refusait avant le lot

C'est le seul motif de rejet. Le brief le déclare disqualifiant en toutes
lettres (non-objectif 7 : « toute modification qui ferait passer un cas
aujourd'hui refusé est disqualifiante »), et la grille aussi (« rendre le
contrôle plus permissif, de quelque manière que ce soit. Ce lot ne peut que
resserrer »).

### Le cas, à reproduire tel quel

Un répertoire de brief jetable, hors du dépôt, avec :

`deliverables/generator-log.md`

```
# Journal

**Author**: forge-generateur

Lot 1 par Claude.

## Lot 2

**Author**: forge-generateur-korrigan

Lot 2 par Korrigan, pas encore jugé.
```

`verdict.md`

```
# Verdict lot 1

**Author**: forge-generateur

Le producteur signe son propre verdict.
```

C'est la signature d'auto-jugement la plus grossière qui soit : le producteur
signe son verdict de son propre nom de rôle. C'est exactement ce que le
contrôle d'origine existait pour attraper.

### Les deux sorties, obtenues par mes soins

Avant le lot (`git show 62a0fe2~1:harness/verdict_audit.py` copié hors du
dépôt, exécuté depuis cette copie) :

```
[FAIL] verdict_is_not_self_authored: same author on both: forge-generateur
VERDICT: REJECT
```

Après le lot (code actuel du dépôt) :

```
[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 1 examined pair(s): forge-generateur-korrigan<->forge-generateur
VERDICT: ACCEPT
```

La même chose se produit quand l'acteur suffixé est en tête du journal
(journal `forge-generateur-korrigan` puis `forge-generateur`, verdict signé
`forge-generateur-korrigan`) : `REJECT` avant, `ACCEPT` après.

### La cause, précisément

L'ancien contrôle comparait `read_field` à `read_field` : toujours la
**première** entrée de chaque fichier. L'égalité stricte de ces deux
premières chaînes suffisait à refuser. Le nouveau contrôle apparie les `k`
derniers auteurs de chaque fichier, `k = min(len(gen_authors),
len(ver_authors))` (`harness/verdict_audit.py`, corps de
`check_verdict_not_self_authored`). Dès que le journal compte plus d'entrées
que le verdict, `gen_authors[:-k]` est jeté sans être examiné — et avec lui
l'égalité stricte que l'ancien contrôle attrapait. Ajouter au journal une
entrée de lot non encore jugé suffit donc à effacer un auto-jugement.

### Ce qu'il faut faire

Deux ajouts, tous deux dans `check_verdict_not_self_authored`, sans toucher à
la règle d'appariement existante (qui est correcte pour ce qu'elle fait) :

1. **Rétablir l'invariant que l'ancien contrôle assurait, sur les listes
   entières.** Si une même chaîne d'auteur figure à la fois dans
   `generator-log.md` et dans `verdict.md`, refuser — quelle que soit la
   position, quelle que soit la longueur des deux listes. C'est un test
   d'intersection d'ensembles sur les **chaînes brutes**, pas sur les
   acteurs : il ne peut pas produire de faux refus, puisque la même
   signature des deux côtés est par définition la même personne. Attention :
   ne pas confondre avec l'intersection **par acteur**, que le journal
   écarte à juste titre — c'est elle, et elle seule, qui refuserait le brief
   009 à tort.

2. **Confronter les entrées de journal écartées par la troncature à
   l'ensemble des auteurs du verdict.** Pour chaque auteur de
   `gen_authors[:-k]`, refuser s'il partage son acteur avec l'un quelconque
   des auteurs de `ver_authors`. Un lot dont le producteur figure aussi côté
   verdict n'a pas à échapper à l'examen parce qu'un lot plus récent a été
   ajouté après lui.

### Vérifications à faire avant de conclure que c'est réparé

- les deux cas ci-dessus repassent à `REJECT` (preuve rouge d'abord : montrer
  qu'ils passent `ACCEPT` avec le code actuel, puis `REJECT` après) ;
- le brief 009 reste `ACCEPT`. Je l'ai vérifié à l'avance pour les deux
  ajouts : l'intersection des chaînes d'auteur y est vide (journal =
  `forge-generateur`, `forge-generateur-codex` ; verdict =
  `forge-evaluateur`, `forge-evaluateur-codex`, `forge-evaluateur`), et
  aucune entrée de journal n'y est écartée par la troncature (2 auteurs de
  journal contre 3 de verdict). Les deux ajouts sont donc inertes sur le
  brief 009 ;
- SC5 est rejoué sur les 11 répertoires, dans les deux sens cette fois :
  aucun PASS→FAIL **et** aucun FAIL→PASS ;
- les deux cas deviennent des tests committés dans
  `harness/tests/test_verdict_audit_actor_identity.py`, sinon la prochaine
  refonte de l'appariement rouvrira la même porte sans que rien ne le dise.

---

## D2 (découle de D1) — SC3b : « chaque couple » n'est pas honoré quand les deux fichiers n'ont pas le même nombre d'entrées

L'exigence littérale de SC3b — le brief à deux lots équilibré — est tenue, et
sa preuve red-first est bonne. Mais la clause d'entrée de SC3b dit que le
contrôle examine *chaque* couple auteur du brief. Ma sonde : journal =
`forge-generateur-korrigan` (lot 1) puis `forge-generateur` (lot 2) ;
verdict = `forge-evaluateur-korrigan` (le lot 1 jugé par son propre
producteur). Sortie après correctif :

```
[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 1 examined pair(s): forge-generateur<->forge-evaluateur-korrigan
VERDICT: ACCEPT
```

Le seul couple auto-jugé du dossier n'a jamais été regardé. Ce n'est pas une
régression (le code d'avant l'acceptait aussi), mais SC3b annonce fermer
cette porte-là. Le correctif de D1, point 2, la ferme.

Ce cas n'est pas théorique : c'est la forme qu'aura ce brief-ci dès que
`010b` sera produit alors que `010a` n'a qu'un verdict.

---

## D3 (mineur) — le test de SC4 utilise un nom d'acteur qui existe déjà dans le dépôt

`test_unseen_actor_name_is_refused_without_naming_it_in_the_control` emploie
`gemini`, et n'affirme l'absence de ce nom que dans `verdict_audit.py`. Or
SC4 demande « un nom d'acteur qui n'apparaît nulle part ailleurs dans le
dépôt », et `gemini` figure dans
`docs/adr/0002-pluggable-generator-backend.md`. Je n'ai pas compté ce point
contre le lot, parce que j'ai établi la généralité moi-même avec `korrigan`,
qui n'apparaît nulle part. Correction suggérée, à coût nul : remplacer le nom
du test par un nom inventé et étendre l'assertion à l'ensemble du dépôt, ou
au minimum écrire dans le test pourquoi l'assertion se limite au contrôle.

## D4 (mineur) — le compteur `author_pairs_examined_per_brief` est exact mais plus étroit que son nom

Le dénominateur exigé est « le nombre réel de couples présents dans ces deux
fichiers ». Compté à la source : le journal du brief 009 porte 2 champs
`Author` (lignes 1 et 596), son `verdict.md` en porte 3 (lignes 4, 252, 420).
La valeur 2 est juste pour « couples examinés », mais elle ne dit pas qu'une
signature d'évaluateur sur trois n'est jamais regardée. Ajouter la mention de
ce qui est écarté (par exemple un champ « signatures non appariées ») rendrait
le compteur honnête sans changer sa valeur.

## D5 (hors périmètre, pour mémoire) — le cas du backend natif

Le couple `forge-generateur` / `forge-evaluateur` — rôles nus, sans suffixe
d'acteur — reste indétecté, et c'est sous ce cas que le verdict de ce lot est
écrit : le gate a répondu `ACCEPT` sur `verdict_is_not_self_authored` en
affirmant deux acteurs distincts là où c'est Claude des deux côtés. **Ce
n'est un échec ni de SC3 ni de SC4** : aucune condition de ce brief ne le
demandait, le manque est dans la spécification. À ne pas corriger dans
l'itération 2 du lot 010a — cela sortirait du périmètre. Cela mérite un brief
à soi, parce que distinguer deux sessions du même backend demande une trace
que le dépôt ne porte pas encore.

---

## Ce qui suffit pour l'itération 2

Corriger D1 (les deux ajouts), ce qui règle D2 par la même occasion ; livrer
les preuves rouges puis vertes des deux cas ; rejouer SC5 dans les deux sens
sur les 11 répertoires ; et joindre les nouveaux tests. D3 et D4 sont des
améliorations d'une ligne chacune, souhaitables mais non bloquantes. Rien
d'autre n'est à refaire : le reste du lot tient.
