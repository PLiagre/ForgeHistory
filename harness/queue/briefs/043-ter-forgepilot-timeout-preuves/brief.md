# Brief 043-ter — ForgePilot respecte le délai de preuve

**Authored**: 2026-08-30T12:15:00Z
**Author**: Codex/OpenAI, sur demande explicite du propriétaire
**Risque**: R2 — correction du chemin durable de ForgePilot ; tout changement
dans `control-plane/**` est classé R2 par
`control-plane/workflow-policy.toml`. Aucun changement produit.

## But unique

Supprimer la borne morte de 1 800 secondes imposée par
`control-plane/forgepilot/durable.py` aux commandes de preuve. Chaque commande
lancée par `run_test_profile` doit recevoir le délai de preuve effectif du run,
déjà dérivé du profil de risque de la politique et persisté dans l'état. Cette
valeur doit être transmise explicitement sur tout le chemin durable et inscrite
dans la preuve produite.

Ce lot ne raccourcit ni n'allonge aucun profil dans la politique. Il raccorde
la décision existante à son effet. Il ne change ni les suites sélectionnées, ni
leur ordre, ni leur contenu, ni une règle du monde.

## Cause prouvée

Le candidat du lot 043-bis a terminé toute la suite du moteur avec succès :
`127 passed in 2991.60s`. ForgePilot a pourtant fait échouer ce même candidat à
l'étape `PR_TESTING` exactement après `1800.2 s`.

Les deux valeurs concurrentes sont visibles dans le dépôt :

- `control-plane/workflow-policy.toml` déclare
  `risks.R2.timeouts.proof = 21600` ;
- l'état durable R2 copie cette décision sous
  `timeouts_seconds.proof = 21600` ;
- `control-plane/forgepilot/durable.py::run_test_profile` ignore cette valeur
  et passe `_TEST_TIMEOUT_SECONDS = 1800` à chaque appel de `run_command`.

La suite n'est donc pas rouge. ForgePilot la tue avec une seconde politique
locale, plus courte que la politique effective du run. La constante
`_TEST_TIMEOUT_SECONDS` est une borne morte : elle nomme sa propre référence,
ne dérive ni du run ni de la politique, et rend inopérant le délai R2 payé et
persisté.

Avant toute édition, archiver dans le journal les sorties de ces constats :

```bash
git rev-parse HEAD
rg -n "_TEST_TIMEOUT_SECONDS|run_test_profile|timeout_seconds" control-plane/forgepilot/durable.py
rg -n -A5 "risks.R[012]\.timeouts" control-plane/workflow-policy.toml
rg -n "timeouts_seconds" control-plane/forgepilot/state.py control-plane/forgepilot/durable.py
```

La base attendue à la rédaction est
`4b732778fc7970ce3e0e108369adc5ff60b5a2a5`. Si les noms ont changé avant le
lancement, retrouver le même chemin de données sans réintroduire une table ou
une valeur de repli. Si la politique effective atteint déjà `run_command` et
la preuve persistée, arrêter : le lot est caduc.

## Contrat de correction

### Une seule source du délai

Le délai utilisé pour une preuve est `timeouts_seconds.proof` du run durable.
Ce champ est créé depuis le profil de risque de
`control-plane/workflow-policy.toml` et est remplacé par le nouveau profil si
le risque effectif monte. Ne pas recalculer ce délai depuis le nom `fast`, `pr`
ou `certify`. Ne pas créer de dictionnaire de délais, de constante globale, de
valeur par défaut ou de cas spécial R2 dans `durable.py`.

Lire cette valeur de façon fermée : elle doit être un entier strictement
positif, et un booléen n'est pas un entier recevable ici. Si le champ, la clé
`proof` ou la valeur valide manque, lever `PilotError` avec un message qui
nomme `timeouts_seconds.proof`. Ne jamais deviner un délai. Les états durables
créés par la version actuelle portent déjà ce champ ; aucune migration d'état
n'est demandée.

### Transmission explicite

Supprimer `_TEST_TIMEOUT_SECONDS`. Ajouter à `run_test_profile` un argument
nommé explicite pour le délai de preuve. Cet argument est obligatoire : aucune
valeur par défaut cachée ne doit permettre à un appelant de l'omettre.

Tous les chemins durables doivent transmettre la valeur effective jusqu'à
`run_command` :

- preuve `pr` avant la première publication ;
- preuve `fast`, puis preuve `pr`, lors d'une itération ;
- preuve `certify` exigée par un run R2 ;
- reprise d'une de ces étapes sur le même état.

Le point commun `_run_exact_test_profile` peut porter cette transmission, mais
ses appelants durables doivent lui fournir explicitement la valeur issue de
l'état. Un appel durable ne doit ni ignorer `settings`, ni relire une constante
locale à la place du run effectif.

La façade `run_targeted_tests` reste présente. Adapter sa signature et sa
transmission au nouvel argument obligatoire, sans réintroduire de délai par
défaut. Adapter les doubles et appels des tests existants au contrat explicite.
En dehors de ce nouvel argument et du champ de preuve décrit ci-dessous, les
valeurs de retour, exceptions, suites choisies, arrêts au premier échec et
façades conservent leur comportement.

### Preuve durable et cache

Le résumé écrit par `run_test_profile` porte le délai effectif sous un champ
explicite `timeout_seconds`, sur succès comme sur échec. Le même résumé est
ensuite inclus dans l'entrée `proofs` de l'état par le mécanisme existant : la
preuve persistée doit donc permettre de lire le délai réellement donné à
`run_command`.

Un résultat mis en cache ne peut être réutilisé comme preuve du run courant
que si son `timeout_seconds` correspond à la valeur effective demandée. Une
ancienne preuve sans ce champ, ou portant une autre valeur, est rejouée puis
réécrite ; elle n'est pas complétée en mémoire après coup. Ne pas ajouter le
délai au nom du fichier : l'identité Git existante reste l'identité du
candidat, et une discordance de configuration invalide simplement le contenu
mis en cache.

## Périmètre d'écriture

Fichiers ForgePilot autorisés :

- `control-plane/forgepilot/durable.py`, uniquement pour supprimer la borne
  morte, valider et transmettre le délai effectif, le persister dans le résumé
  de preuve et empêcher la réutilisation d'un cache portant un autre délai ;
- `control-plane/tests/test_acceleration.py`, uniquement pour adapter les
  appels existants strictement nécessaires et ajouter les preuves unitaires
  rouge/vert de ce contrat.

Livrables minimaux autorisés :

- `harness/queue/briefs/043-ter-forgepilot-timeout-preuves/deliverables/manifest.json` ;
- `harness/queue/briefs/043-ter-forgepilot-timeout-preuves/deliverables/generator-log.md`.

Tout autre chemin est interdit. En particulier, ne modifier ni
`control-plane/workflow-policy.toml`, ni `control-plane/forgepilot/policy.py`,
ni `control-plane/forgepilot/state.py`, ni une autre suite de tests, ni
`sim/**`, ni `viewer/**`, ni le harnais hors des deux livrables, ni le brief
043-bis, ni ce brief, ni une grille, ni un `verdict.md`.

## Conditions de succès

### SC1 — Le rouge prouve la borne morte sans suite longue

Avant la correction, ajouter le contrôle unitaire qui charge la politique R2,
prend sa valeur `timeouts.proof`, remplace `run_command` par un double instantané
qui enregistre `timeout_seconds`, puis exerce `run_test_profile` sans lancer
pytest. Sur la base, le contrôle échoue en montrant que la valeur reçue vaut
`1800` au lieu de la valeur `proof` R2. Le journal conserve la commande, la
sortie rouge et l'écart observé.

Le contrôle ne dort pas, ne lance pas `sim/tests/` et ne simule pas une durée de
six heures. Il vérifie l'argument transmis à la frontière où le processus
serait réellement lancé.

### SC2 — La valeur R2 atteint chaque `run_command`

Après correction, le même contrôle est vert. Avec un run dont
`timeouts_seconds.proof` vaut `21600`, chaque invocation de `run_command`
effectuée par `run_test_profile`, y compris `git-diff-check` et chaque suite
présente dans le fixture, reçoit exactement `timeout_seconds=21600`.

Le test dérive d'abord la valeur depuis la politique ou l'état construit par
le run, puis vérifie explicitement qu'elle vaut `21600`. Il ne valide pas la
seule présence d'un nouvel argument et n'inspecte pas seulement une structure
intermédiaire.

### SC3 — Un profil plus court garde sa propre valeur

Un second cas construit un run d'un risque dont `timeouts.proof` est inférieur
à celui de R2. Sa propre valeur traverse le même chemin et atteint
`run_command` inchangée. Le test dérive cette valeur du profil chargé ; il ne
recopie pas un second nombre dans une table de test ou de production.

Ce cas interdit un correctif qui remplacerait simplement `1800` par `21600`,
un maximum global ou une branche spéciale pour `certify`.

### SC4 — Tous les appelants durables transmettent le délai effectif

Les essais du flux durable prouvent, avec des doubles instantanés, que les
preuves `fast`, `pr` et `certify` reçoivent le délai `proof` porté par l'état au
moment de leur exécution. Au minimum, un flux R2 doit observer `21600` sur ses
preuves `pr` et `certify`, et le cas d'itération doit couvrir `fast` et `pr`.

Si le risque est relevé mécaniquement avant la preuve, la valeur transmise est
celle du risque relevé, déjà enregistrée dans `timeouts_seconds`, et non celle
du risque demandé au départ. Ces contrôles n'invoquent aucun agent réel, aucun
processus long et aucune suite `sim`.

### SC5 — La preuve nomme le délai réellement utilisé

Sur succès, le JSON normalisé produit et l'entrée correspondante de `proofs`
portent `result.timeout_seconds` égal à l'argument observé par `run_command`.
Sur échec simulé, le fichier de résumé existe encore et porte le même champ,
comme il porte déjà la suite rouge.

Un cache de même candidat, même profil et autre `timeout_seconds` — ou sans ce
champ — ne court-circuite pas l'exécution. Un cache portant la valeur effective
et un résultat vert conserve le comportement de reprise existant. Les tests
prouvent ces deux branches sans lancer de suite longue.

### SC6 — L'absence est refusée, pas remplacée

Des cas unitaires couvrent au minimum l'absence de
`timeouts_seconds.proof`, une valeur nulle et une valeur booléenne. Chacun
échoue avant `run_command` avec un `PilotError` qui nomme
`timeouts_seconds.proof`. Aucun cas ne retombe sur `1800`, `21600` ou une autre
valeur implicite.

### SC7 — ForgePilot reste vert, sans changement produit

Exécuter uniquement les suites du plan de contrôle ForgePilot :

```bash
cd control-plane && ../.venv/bin/python -m unittest tests.test_acceleration.TestRunnerTests -v
cd control-plane && ../.venv/bin/python -m unittest discover -s tests
```

La première commande peut être ajustée au nom exact des nouveaux cas dans le
même fichier, mais elle doit rester ciblée sur les contrôles de délai. La
seconde suite complète doit être verte. Ne pas lancer `sim/tests/` : le lot ne
touche pas au produit, et sa preuve repose sur des doubles de processus.

Le diff hors livrables contient seulement `durable.py` et
`test_acceleration.py`. La sélection et l'ordre des suites candidates sont
inchangés. Aucun test n'est supprimé, sauté, marqué comme succès attendu ou
relâché.

## Livrables et séparation des rôles

Le manifeste déclare les deux fichiers de code/test autorisés, les deux
livrables et les commandes unitaires exécutées. Le journal contient :

- les constats de base et le SHA ;
- la preuve rouge ciblée montrant `1800` face au délai R2 dérivé ;
- la preuve verte où `21600` atteint `run_command` ;
- le cas plus court et sa valeur dérivée ;
- les refus des valeurs absente, nulle et booléenne ;
- les cas succès, échec et cache, avec `timeout_seconds` persisté ;
- le résultat de la suite complète `control-plane/tests` ;
- le diff borné et la confirmation qu'aucun fichier produit n'a changé.

L'exécutant n'écrit pas de `verdict.md`, ne juge pas la recevabilité de son
travail, ne fusionne rien et ne pousse pas directement sur `master`.

## Hors périmètre

- modifier les valeurs ou profils de `workflow-policy.toml` ;
- accélérer, découper, filtrer ou réordonner les suites candidates ;
- donner un délai distinct à chaque sous-suite : ce lot raccorde le délai de
  preuve existant à chaque commande du profil ;
- ajouter une variable d'environnement, une option CLI ou une configuration
  parallèle ;
- remplacer la constante `1800` par une autre constante globale ;
- rendre le nouvel argument facultatif ou lui donner une valeur de repli ;
- migrer les états durables, changer leur schéma ou changer l'identité Git des
  preuves ;
- modifier le moteur, ses tests, la carte, le viewer ou une règle du monde ;
- relancer la suite longue du lot 043-bis pour prouver cette correction.
