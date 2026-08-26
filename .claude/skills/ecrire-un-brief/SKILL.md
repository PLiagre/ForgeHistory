---
name: ecrire-un-brief
description: >
  Écrire le brief.md d'un lot ForgeHistory. À invoquer dès qu'on demande un
  brief, un lot, une commande de travail pour Cursor, ou qu'on dit « occupe-toi
  de X » sur le jeu. Le brief est la SEULE source d'instruction d'un lot
  (ADR-0019) ; il se range sous harness/queue/briefs/NNN-slug/.
---

# Écrire un brief

Depuis ADR-0019, **Claude écrit les briefs**. Hermes les fait relire et les
lance ; Cursor les exécute. Tu n'es jamais le relecteur de ton propre brief.

Avant d'écrire, lire dans cet ordre : `AGENTS.md` (les règles, les douze
règles payées, les sept modes de défaillance, les trois niveaux de fidélité,
la règle d'admission des tests), `sim/MODELE.md` (comment le monde fonctionne),
puis les fichiers que le lot va toucher. Pas plus.

## Les cinq règles qui font échouer un brief

Elles viennent des six défauts que cherche le relecteur
(`control-plane/prompts/brief-reviewer.md`). Un `PASS` exige zéro constat.

1. **Un lot = un changement.** Si deux parties du brief pourraient être livrées
   et jugées séparément, ce sont deux briefs. Le test : « si la moitié marche
   et l'autre pas, est-ce que je fusionne ? » Si oui, coupe.
2. **Chaque critère nomme une commande, un fichier ou une valeur observable**,
   et doit pouvoir échouer. « Le code est propre » n'est pas un critère.
3. **Tout compteur a un dénominateur dérivé des données.** Jamais un nombre
   attendu écrit en dur. Un échantillon vide **échoue**, il ne passe pas.
4. **Ne jamais demander de modifier un test existant.** C'est le défaut le plus
   grave : ajuster un contrôle après avoir vu une mesure est une calibration
   déguisée. Un lot **ajoute** ses cas au fichier qui porte déjà l'invariant
   concerné ; il n'ouvre pas un fichier de test par lot.
5. **Tout brief qui touche au monde dit son niveau de fidélité** (1, 2 ou 3) et
   rappelle qu'une anomalie de niveau 2 n'est pas un défaut.

Et une sixième, sur le périmètre : **n'autorise en écriture que ce que le
travail décrit exige.** Tout autre chemin est interdit, nommément.

## Les deux règles que le relecteur ne vérifiera pas

Elles viennent d'ADR-0019 § « Ce que la règle de rôle couvre », et personne
d'autre que toi ne peut les tenir.

**Cite la section de `sim/MODELE.md` dont le brief découle**, sous un titre
`## Fondement dans le modèle`. Le relecteur lit le brief et *les fichiers qu'il
cite* : c'est le seul moyen que l'affirmation de modèle entre dans son champ,
puisque rien de mécanique ne relit `MODELE.md`. Un lot qui ne touche pas au
monde le dit — « aucun fondement, ce lot ne change aucun nombre » — plutôt que
d'inventer une section.

**Devant un lot qui ne converge pas après trois itérations, tu n'as que deux
réponses** : *le brief est faux, en voici la réécriture*, ou *je ne peux pas
trancher, c'est au propriétaire*. Jamais « le brief est bon, relancez » : ce
serait juger recevable ton propre travail. Un brief réécrit repasse par
`brief-review`.

## Ce qui vieillit, et comment l'écrire

Un brief écrit aujourd'hui peut être exécuté dans trois semaines, sur un moteur
qui aura bougé. Deux conséquences (règle 12 : un compteur se cite par nom,
jamais par valeur) :

- l'**état de départ** est la commande qui le mesure, plus le fait qualitatif
  qui rend le lot caduc s'il est déjà vrai — pas une liste de nombres recopiés ;
- les critères d'effet comparent deux mesures via `must_differ_from_git` contre
  la référence Git de base, jamais contre un nombre écrit dans le brief.

Un nombre ne s'écrit dans un brief que s'il est un **paramètre décidé avant
l'exécution** (un facteur de rendement, un plafond), jamais s'il est un
résultat attendu.

## Le squelette

Chaque lot est un dossier `harness/queue/briefs/NNN-slug/` contenant
`brief.md` **et** `eval-rubric.md` (`harness/verdict_audit.py` opère sur un
dossier qui porte les deux). Le prochain numéro libre se lit avec
`ls harness/queue/briefs/` et l'historique des lots archivés
(`git ls-tree --name-only da1596d:harness/queue/briefs`).

```markdown
# Brief NNN — <ce que le monde saura faire après>

**Authored**: <ISO 8601 UTC>
**Author**: Claude
**Risque**: R0 documentaire | R1 produit borné | R2 critique

## But unique
Une phrase sur ce qui change dans le monde. Puis ce que ce lot ne fait PAS,
nommément, pour les voisins évidents.

## État de départ mesuré
Le SHA de base, les commandes qui donnent l'état, et le fait qualitatif qui
rendrait ce lot caduc. Aucune valeur recopiée comme cible.

## Règle du monde
Le mécanisme, et son niveau de fidélité. Les paramètres décidés d'avance,
sous forme de constantes nommées.

## Source de vérité et raccord au moteur
D'où vient la donnée lue, et où exactement la formule vit. Ce qui reste
unique. Ce qui doit lever une erreur plutôt que deviner.

## Périmètre d'écriture
Les fichiers produit autorisés, puis les livrables autorisés. Puis :
« Tout autre chemin est interdit », avec les pièges nommés.

## Conditions de succès
SC1…SCn. Chacune nomme une commande, un fichier ou une valeur observable,
et peut échouer. Le rouge est prouvé avant la correction.

## Compteurs exigés
| compteur | source d'échantillon | dénominateur dérivé |
Aucun compteur d'affirmation réelle ne finit à -1 ; un zéro mesuré n'est
jamais « non calculé ».

## Livrables et porte mécanique
manifest.json (commandes exactes + compteurs), generator-log.md (français
clair : rouge avant correction, fichiers touchés, commandes, limites),
measure_NNN.py rejouable.

## Hors périmètre
Ce que le lot ne touche pas. Nommément.

## Interdictions pour l'exécutant
N'écrit pas de verdict.md, ne modifie ni le brief ni la grille, ne juge pas
son propre travail, ne fusionne rien, ne pousse pas sur master.
```

Un brief passé par la porte et par le relecteur, à relire comme modèle :

```bash
git show 09e36dc:harness/queue/briefs/033-relief-dans-le-rendement/brief.md
```

## Quand tu écris plusieurs briefs d'affilée

`brief-review` lit **un** brief isolément. Il ne verra jamais qu'un lot dépend
d'un autre, que deux lots se réclament chacun « seul endroit » de la même
chose, que l'un promet un état qu'aucun autre ne produit, ou que deux lots
touchent la même grandeur sans dire comment ils se composent. Relis la série
toi-même sur ces quatre points, et écris les dépendances dans `ROADMAP.md`,
qui est le seul endroit où l'ordre est publié.

Une dépendance s'écrit dans les deux sens : le lot amont dit ce qu'il ouvre,
le lot aval dit ce qu'il suppose et se déclare **bloqué** — jamais « à
adapter » — si ce n'est pas là.

## Après l'écriture

Tu ne relis pas ton brief et tu ne le lances pas. Hermes le fait relire :

```bash
forgepilot brief-review harness/queue/briefs/NNN-slug/brief.md --repo /srv/ForgeHistory --run
```

Un `FAIL` se corrige dans le **brief**, jamais dans le code — et par toi, son
auteur. Le déroulé complet d'un lot est dans `docs/MODE-EMPLOI.md`.
