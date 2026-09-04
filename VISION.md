# Vision — infrastructure dont les agents ont besoin

Ce document prime sur tout autre document de ce dépôt en cas de conflit.

Il a été gelé du premier jour au **4 septembre 2026**. Ce jour-là, le
propriétaire a retiré la fusion de ses mains : son rôle ordinaire se
limite désormais à donner des directions. La section « Ce qui ne se
négocie pas » a changé en conséquence, et rien d'autre. Le document est
de nouveau gelé.

## Ce que nous construisons

Pas un agent de plus. Pas une démo IA. Une **infrastructure** pour que
des agents déjà choisis — Claude, Cursor, Codex, Hermes, ou un autre —
travaillent de manière fiable sur un dépôt produit.

Le premier client est [ForgeHistory](https://github.com/PLiagre/ForgeHistory).
Ce n'est pas le seul client possible. L'atelier ne connaît pas le jeu ;
il connaît un **lot**, un **périmètre**, et des **rôles**.

## Une phrase

Plusieurs lots entrent, plusieurs PR sortent, la machine intègre celles
dont les preuves sont là — et seulement celles-là.

## Les huit couches

Le schéma est plus vaste que n'importe quel agent unique. Chaque
composant de ce dépôt déclare **une** couche. Un composant qui en
occupe deux est un défaut.

| couche | question | ce qu'elle n'est pas |
|---|---|---|
| **intelligence** | qui raisonne | pas le pilote, pas le juge de fusion |
| **outils** | quelles compétences se rejouent | pas une seconde source d'instruction |
| **mémoire** | ce qui survit aux sessions | pas une base parallèle au dépôt produit |
| **exécution** | où le code s'écrit | pas le dépôt commun de tout le monde |
| **orchestration** | dans quel ordre, avec quelle reprise | pas un agent, pas une fusion |
| **coordination** | qui a le quota, qui tient quel fichier | pas un verrou magique qui remplace la parole |
| **vérification** | quoi est mesuré, par qui | pas un verdict qui fusionne |
| **intégration** | qui écrit dans `master`, et à quelle condition | pas un juge, pas un agent, pas un raisonneur |

La huitième couche est **ouverte** par la décision du 4 septembre 2026,
pas encore occupée. Écrire dans `master` n'était la responsabilité
d'aucun composant : c'était le geste du propriétaire. Le lui retirer
sans nommer la couche qui le reprend l'aurait mis dans l'orchestration,
qui déclare précisément qu'elle n'en fait pas. Le lot qui l'occupe la
fera exister — une couche vide fait rougir `tests/test_couches.py`, et
c'est ce rouge qui tient la promesse de cette ligne.

## Ce qui ne se négocie pas

1. **Celui qui a écrit le code ne dit pas s'il est recevable.**
2. **Le brief est la seule source d'instruction d'un lot.** Tout le reste
   y renvoie ; rien ne le paraphrase.
3. **La fusion est mécanique.** Une PR entre dans `master` quand tous les
   contrôles requis sont verts sur sa **révision courante**, et à cette
   seule condition. Personne ne fusionne sur un avis : ni un agent, ni le
   propriétaire. Aucun agent auteur ou relecteur ne possède le droit de
   fusion.
4. **Une relecture terminée n'est pas une approbation.** Une approbation
   est un **verdict** : une donnée structurée, validée, liée à la
   révision relue et à son auteur. `PASS` autorise la suite ; `FAIL`
   bloque et renvoie le travail à son auteur ; absent, périmé ou
   illisible bloque aussi. Une prose ne verdit rien.
5. **L'intégration est séquentielle et retestée.** Plusieurs lots
   s'écrivent en même temps ; ils n'entrent jamais en même temps. Chaque
   PR est retestée sur le dernier `master` avant d'y entrer.
6. **Une absence se déclare.** L'atelier refuse de deviner un quota, un
   rôle, un fichier, un verdict.
7. **Sans `--run`, rien n'est écrit.** Un aperçu n'est pas une dépense.

## Ce que le propriétaire garde

Une chose : **donner des directions**. Une phrase suffit à déclencher la
rédaction de plusieurs lots. Il n'écrit ni les fiches, ni les briefs, ni
les cartes, et il ne fusionne pas.

Un rôle logique le sert, et un seul : l'**éclaireur**. Il lit — la
vision, le modèle du produit, la feuille de route, les briefs livrés, le
code, les tests, la CI, les PR, les incidents — et il propose les
prochains lots : progrès du jeu, stabilisation, correction de défauts,
observabilité, ou dette réellement prouvée. Il est **strictement en
lecture seule** : il ne crée ni lot, ni brief, ni fiche, et ne change
aucune priorité. Une proposition n'est pas une décision ; la décision
reste la direction que le propriétaire donne.

## Ce que nous ne construisons pas

- un cinquième agent de codage
- un tableau de bord qui devient une base de données parallèle
- une mémoire vectorielle qui invente ce que le git ne contient pas
- un juge qui fusionne sur son avis, ou une fusion qui se décide sur une prose
- un niveau de risque qui choisit le modèle à la place du propriétaire
