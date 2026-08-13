# Feedback — Brief `012`, itération 1

**Authored**: 2026-08-13T07:22:01Z
**Author**: forge-evaluateur-cursor

Verdict associé : REJECT. Voir `../verdict.md` pour le raisonnement complet et
le périmètre exact jugé (commit `444ec45`).

Ce document est écrit pour être appliqué directement : chaque point dit ce qui
ne va pas, comment je l'ai constaté, et ce qu'il faut faire précisément. Les
points `B` bloquent l'acceptation, les points `N` non.

---

## B1 — La réserve R1 n'est pas fermée, et le manifeste du lot `011` est cassé

### Ce qui a été fait

L'entrée du compteur d'archive a été retirée du fichier
`harness/queue/briefs/011-sim-monde-vivant-amorcage/deliverables/manifest.json`.
Le journal justifie ce choix par « commande non reproductible citant un
compteur obsolète ».

### Pourquoi c'est un échec

**Premier problème : la condition qui autorisait le retrait n'est pas
remplie.** Le brief offrait deux portes. La porte « corriger la commande »
était ouverte sans condition. La porte « retirer l'entrée » n'était ouverte
qu'après avoir vérifié qu'aucun document sous
`harness/queue/briefs/011-*/` ne cite cette valeur en référence à ce compteur.

Cette vérification, refaite par moi, échoue. Rejouez-la :

```
rg -n "lignes_differentes_preuve_rouge_iter1" harness/queue/briefs/011-sim-monde-vivant-amorcage/
rg -n "\b70\b" harness/queue/briefs/011-sim-monde-vivant-amorcage/
```

Vous trouverez au moins deux citations explicites en référence à ce compteur :
dans le `verdict.md` du lot `011` (section de reconstruction des compteurs,
« Le manifeste porte en outre un compteur d'archive valant … ») et dans son
`generator-log.md`, qui nomme le compteur avec sa valeur *et* avertit que
`verdict_numbers_traceable` était en échec sur le lot `011` avant l'ajout de
ce compteur, précisément parce que le verdict cite cette valeur. La porte 2
était donc fermée.

**Second problème, plus grave : le retrait a produit du JSON invalide.**
L'entrée supprimée était la dernière du tableau `counters`, et la virgule de
séparation de l'entrée précédente est restée. Constatez-le :

```
.venv/bin/python -c "import json; json.load(open('harness/queue/briefs/011-sim-monde-vivant-amorcage/deliverables/manifest.json'))"
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/011-sim-monde-vivant-amorcage
```

La première commande lève une `JSONDecodeError`. La seconde n'affiche plus
aucun verdict mais `ERROR: audit itself failed`, avec un code de sortie `2`.
Le gate mécanique d'un lot déjà accepté ne peut plus tourner. Le même fichier
extrait du commit précédent se charge sans erreur — la cause est bien ce lot.

### Comment corriger

Prenez la **porte 1**, celle qui n'a pas de condition préalable :

1. Restaurez l'entrée `lignes_differentes_preuve_rouge_iter1` dans le tableau
   `counters` du manifeste du lot `011`, avec sa valeur et son `sample_size`
   d'origine (récupérables via
   `git show 0fb553e:harness/queue/briefs/011-sim-monde-vivant-amorcage/deliverables/manifest.json`).
2. Remplacez son champ `command` par une commande **réellement reproductible**,
   qui extrait les deux fichiers de preuve tels qu'ils étaient à l'itération 1
   plutôt que dans leur état courant. Identifiez le commit d'itération 1 avec
   `git log --oneline -- sim/tests/proof_red/run_sabotage.txt`, puis construisez
   une commande de la forme
   `diff <(git show <hash>:sim/tests/proof_red/run_sabotage.txt) <(git show <hash>:sim/tests/proof_red/run_correct.txt) | wc -l`,
   en recopiant le hash réel dans la commande.
3. **Exécutez cette commande** et vérifiez qu'elle rend bien la valeur
   déclarée. Recopiez la sortie obtenue dans le journal du lot `012`. Une
   commande qui ne redonne pas sa valeur n'est pas une correction, c'est le
   défaut d'origine sous un autre habillage.
4. Vérifiez que le fichier est de nouveau du JSON valide **et** que le gate du
   lot `011` retourne un verdict, avec les deux commandes de constat
   ci-dessus. Les deux doivent réussir.

### Le piège à éviter

Ne « réparez » pas en supprimant simplement la virgule orpheline pour rendre
le JSON valide tout en laissant l'entrée retirée. Cela corrigerait le symptôme
et laisserait la faute : la condition du retrait reste fausse, et le contrôle
`verdict_numbers_traceable` du lot `011` risque de repasser en échec puisque
son verdict cite toujours cette valeur. Vérifiez le gate du lot `011`, pas
seulement la syntaxe du fichier.

---

## B2 — La commande déclarée pour `cellules_affamees_monde_reel` ne produit pas sa valeur

### Le constat

La **valeur** du compteur est juste : je l'ai reconstruite moi-même avec le
script de la rubrique et j'obtiens exactement 261 cellules sur 596. Ce n'est
pas la mesure qui est en cause, c'est la commande déclarée dans le manifeste.

Rejouez-la telle qu'elle est écrite : elle affiche `579`, pas 261.

### Pourquoi elle est fausse

La commande enferme l'appel au tick dans la condition d'une expression
génératrice parcourant les cellules :

`... if tick(world, rng) or c.hunger_ticks > 0 ...`

Deux défauts en découlent. D'abord, le tick est appelé **une fois par
cellule** au lieu d'une fois par pas de temps : la boucle avance le monde
entier des centaines de fois par itération externe, au lieu des `200` ticks
annoncés. Ensuite, l'opérateur `or` est court-circuitant : dès que le tick
retourne un nombre de kilogrammes transportés non nul, la cellule courante est
comptée **sans que sa faim ne soit jamais examinée**. La commande mesure donc
l'activité du commerce, pas la faim.

### Comment corriger

Remplacez la commande par le script réellement utilisé pour produire la
valeur — celui qui figure d'ailleurs déjà, sous forme lisible, dans la section
SC5 du journal. Séparez l'appel au tick de l'accumulation :

```
for _ in range(200):
    tick(world, rng)
    for cid, c in world.cells.items():
        if c.hunger_ticks > 0:
            faim.add(cid)
```

Si la commande tient mal sur une ligne, déposez le script mesuré dans le
dossier des livrables et faites pointer le champ `command` vers son exécution.
Puis **exécutez la commande déclarée** et vérifiez qu'elle affiche bien la
valeur du compteur avant de l'inscrire au manifeste.

### Portée

Les trois autres compteurs du monde réel sont exempts de ce défaut : leurs
commandes se rejouent et rendent leur valeur au chiffre près. Ne les touchez
pas.

---

## N1 — La commande de `constantes_temporelles_coherentes` ne peut pas échouer

Non bloquant, mais à corriger : c'est une garde structurellement vide, ce que
la hard-won rule 4 proscrit.

La commande calcule, pour chacun des trois noms de constantes,
`n in src and 'TICK_DURATION_DAYS' in src`. Le second membre ne dépend pas de
`n` : il est vrai dès que la chaîne `TICK_DURATION_DAYS` apparaît **n'importe
où** dans le fichier, y compris dans sa propre déclaration ou dans un
commentaire. La commande rend donc 3 même si aucune constante n'est dérivée.

Je l'ai vérifié plutôt que de le supposer : dans une copie hors dépôt, j'ai
supprimé les trois facteurs `* TICK_DURATION_DAYS` des trois constantes, puis
rejoué la commande déclarée — elle affiche toujours 3.

Sur le fond, la condition SC1 est bien satisfaite : j'ai lu `sim/constants.py`
et les trois constantes sont réellement écrites comme un produit par
`TICK_DURATION_DAYS`. Seule la mesure est vide.

**Correction** : testez la dérivation ligne par ligne, pas le fichier entier.
Par exemple, pour chaque nom, isolez la ligne d'affectation qui commence par
ce nom et vérifiez que `TICK_DURATION_DAYS` figure **dans cette ligne**. Puis
validez la garde en cassant volontairement une dérivation dans une copie hors
dépôt : le compteur doit tomber à `2`.

**Point connexe, à trancher explicitement.** Le brief nomme les trois
constantes temporelles attendues : production, consommation et **constante de
réserve initiale**. Le compteur a substitué à la troisième la capacité de
transport. Or `INITIAL_FOOD_RESERVE_TICKS` ne cite pas `TICK_DURATION_DAYS`.
Ce n'est pas nécessairement une faute — cette constante est désormais exprimée
en ticks, donc indépendante de la durée d'un tick, ce qui est précisément la
correction d'unité que le brief demandait. Mais la substitution a été
silencieuse. Dites-le explicitement dans `sim/SEEDING.md` et dans le journal :
soit vous justifiez que la réserve initiale est unitairement neutre et que le
dénominateur du compteur porte sur les trois constantes réellement dérivées,
soit vous l'exprimez comme un produit par `TICK_DURATION_DAYS`. Ne laissez pas
le lecteur découvrir l'écart en comparant le brief au manifeste.

---

## N2 — Superficies de test sous le plancher, et une annotation manquante

Non bloquant : aucun compteur SC5 ne s'appuie sur ces tests, et le cas
`SC7d` est correctement annoté comme hors données G3, en en-tête de module
comme dans la docstring de la fonction. C'est fait proprement, il manque juste
la même rigueur ailleurs.

Le brief pose une règle générale : « Tous les tests unitaires construisant des
cellules à la main utilisent `area_km2 ≥ 1.0` km² ». Deux cellules de
`sim/tests/test_causal_chain.py` ne la respectent pas :

- `test_sc7a_stock_decreases_when_production_lt_consumption` utilise une
  superficie de `0.001` km², sans annotation ;
- `test_sc7b_hunger_ticks_increments_when_stock_empty` utilise une superficie
  nulle, sans annotation, alors que la rubrique demande que toute occurrence
  de superficie nulle soit annotée comme structurellement hors données G3.

**Correction** : pour `SC7a`, remontez la superficie à `1.0` km² et ajustez la
population pour conserver l'écart production/consommation voulu — le test
garde son sens et respecte le plancher. Pour `SC7b`, la superficie n'est même
pas lue par le maillon testé : mettez `1.0` km², ou ajoutez la même annotation
« cas hors données G3 » que celle de `SC7d` si vous tenez à la valeur nulle.

---

## N3 — Le sabotage de la paire « couverture étendue » teste la garde de l'ancien lot

Non bloquant : le fichier rouge livré contient bien des échecs réels et je l'ai
reproduit octet pour octet depuis mon propre sabotage — il est authentique.

Mais le sabotage retenu ajoute un champ fantôme à la dataclass **existante**.
C'est exactement ce que la garde du lot `011` détectait déjà, et c'est
d'ailleurs ce que faisait sa propre paire de preuve rouge. La capacité
**nouvelle** apportée par R2 — découvrir par introspection une dataclass
entièrement nouvelle — n'est donc pas exercée par le fichier rouge livré.

J'ai vérifié séparément que cette capacité fonctionne : avec une seconde
dataclass portant un champ sans écrivain ni lecteur, le test échoue en nommant
la classe et le champ fautifs. R2 est donc bien fermée sur le fond ; c'est la
preuve archivée qui est plus faible que la garde qu'elle est censée
documenter.

**Correction** : régénérez la paire en sabotant par l'ajout d'une seconde
dataclass, et non par l'ajout d'un champ à la première. Le fichier rouge
prouvera alors la capacité nouvelle, et non celle du lot précédent.

---

## R4 — resté optionnel, non fait

Les deux fichiers de preuve verte du lot `011` sont toujours identiques octet
pour octet (`diff` vide). Le brief le déclarait explicitement optionnel et non
bloquant. Aucune action requise.

---

## Ce qu'il ne faut surtout pas faire

- **Ne modifiez pas `brief.md`, `eval-rubric.md` ni `verdict.md`.** La
  rubrique classe cette modification parmi les échecs disqualifiants, quel que
  soit le reste du travail.
- **Ne committez pas et ne poussez pas.** Le contrat d'exécution l'interdit
  explicitement, et cela a déjà été fait à cette itération, sur une branche
  au préfixe `cursor/` réservé aux dépôts d'audits. L'orchestrateur committe ;
  vous produisez.
- **Ne « fermez » pas B2 en corrigeant la valeur au lieu de la commande.** La
  valeur 261 est juste, je l'ai reconstruite. Toucher au monde ou aux
  paramètres pour faire coller la commande fautive détruirait une mesure
  correcte.
- **Ne recalibrez pas les constantes.** Les quatre conditions du monde réel
  sont satisfaites simultanément et se reproduisent au chiffre près. Toute
  modification de production, consommation, réserve initiale, capacité de
  transport ou paramètres de mortalité changerait tous les compteurs SC5 et
  vous obligerait à tout remesurer, pour un gain nul.
- **Ne retirez aucun champ d'une dataclass** pour ajuster la couverture : la
  rubrique en fait un échec disqualifiant.
- **Ne recopiez aucune valeur hexadécimale de condensé** dans un test ou un
  document. L'itération actuelle est irréprochable sur ce point — les
  condensés sont calculés et comparés par nom de variable. Conservez cette
  discipline.
