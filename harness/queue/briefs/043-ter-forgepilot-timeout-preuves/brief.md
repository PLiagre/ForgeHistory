# Brief 043-ter — ForgePilot transmet le délai de preuve effectif

**Authored**: 2026-08-30T12:15:00Z
**Author**: Codex/OpenAI, sur demande explicite du propriétaire
**Risque**: R2 — correction du chemin durable de ForgePilot ; tout changement
dans `control-plane/**` est classé R2 par
`control-plane/workflow-policy.toml`. Aucun changement produit.

## But unique

Supprimer la borne morte `_TEST_TIMEOUT_SECONDS` imposée par
`control-plane/forgepilot/durable.py` aux commandes de preuve. Le chemin
durable doit lire `timeouts_seconds.proof` dans l'état effectif du run, refuser
son absence ou une valeur invalide, puis transmettre cette valeur explicitement
jusqu'à chaque appel de `run_command` effectué pour la preuve.

Ce lot ne change ni le contenu des résumés ou des preuves, ni leur cache. Il ne
modifie aucun profil de la politique, aucune suite sélectionnée, leur ordre ou
leur contenu, ni une règle du monde.

## Cause prouvée

Le candidat du lot 043-bis a terminé toute la suite du moteur avec succès :
`127 passed in 2991.60s`. ForgePilot a pourtant fait échouer ce même candidat à
l'étape `PR_TESTING` exactement après `1800.2 s`.

À cette date, les valeurs concurrentes observées dans le dépôt étaient :

- `control-plane/workflow-policy.toml` déclarait
  `risks.R2.timeouts.proof = 21600` ;
- l'état durable R2 copiait cette décision sous
  `timeouts_seconds.proof = 21600` ;
- `control-plane/forgepilot/durable.py::run_test_profile` ignorait cette valeur
  et passait `_TEST_TIMEOUT_SECONDS = 1800` à chaque appel de `run_command`.

Ces nombres décrivent le défaut historique. Ils ne sont pas des cibles de ce
lot. La cible est toujours la valeur dérivée du profil puis portée par l'état
effectif au moment de la preuve.

Avant toute édition, archiver dans le journal les sorties de ces constats :

```bash
git rev-parse HEAD
rg -n "_TEST_TIMEOUT_SECONDS|run_test_profile|timeout_seconds" control-plane/forgepilot/durable.py
rg -n -A5 "risks.R[012]\.timeouts" control-plane/workflow-policy.toml
rg -n "timeouts_seconds" control-plane/forgepilot/state.py control-plane/forgepilot/durable.py
```

La base attendue à la rédaction est
`4b732778fc7970ce3e0e108369adc5ff60b5a2a5`. Si les noms ont changé avant le
lancement, retrouver le même chemin de données sans introduire une seconde
politique locale. Si la valeur effective de `timeouts_seconds.proof` atteint
déjà chaque `run_command` de preuve, arrêter : le lot est caduc.

## Contrat de correction

### Une seule source du délai durable

Le délai utilisé par le chemin durable est `timeouts_seconds.proof` de l'état
effectif du run. Ce champ est déjà créé depuis le profil de risque de
`control-plane/workflow-policy.toml` et remplacé par le nouveau profil si le
risque effectif monte.

Ne pas recalculer ce délai depuis le nom `fast`, `pr` ou `certify`. Ne pas créer
de dictionnaire de délais, de nouvelle constante globale, de valeur de repli ou
de cas spécial lié à un niveau de risque dans `durable.py`.

Lire la valeur de façon fermée : elle doit être un entier strictement positif,
et un booléen n'est pas un entier recevable ici. Si `timeouts_seconds`, la clé
`proof` ou une valeur valide manque, lever `PilotError` avant `run_command`. Le
message nomme `timeouts_seconds.proof`. Ne jamais deviner un délai pour un état
durable. Les états créés par la version actuelle portent déjà ce champ ; aucune
migration d'état n'est demandée.

### Transmission explicite jusqu'au processus

Supprimer `_TEST_TIMEOUT_SECONDS`. Le raccord interne emprunté par le chemin
durable porte un argument nommé explicite pour le délai. Une fois la valeur lue
et validée dans l'état, chaque appel intermédiaire la reçoit explicitement et
la transmet inchangée à `run_command` sous `timeout_seconds`.

Le raccord couvre :

- la preuve `pr` avant la première publication ;
- les preuves `fast` puis `pr` d'une itération ;
- la preuve `certify` exigée par le risque effectif ;
- la reprise de chacune de ces étapes sur le même état.

Le point commun `_run_exact_test_profile` peut porter la lecture et la
transmission. Un appel durable ne doit ni ignorer l'état, ni relire une
constante locale à la place du délai effectif.

Les façades déjà appelées par les contrôles existants, notamment
`run_test_profile` et `run_targeted_tests`, restent appelables avec exactement
les mêmes arguments et conservent leur comportement observable. Le raccord
durable peut être ajouté derrière une façade compatible ou dans une fonction
interne distincte. Il ne doit pas obliger à modifier un appel déjà présent dans
`test_acceleration.py`. Le nouveau chemin et son argument explicite sont testés
uniquement par de nouvelles méthodes de test.

En dehors de ce raccord, les valeurs de retour, exceptions, suites choisies,
arrêts au premier échec, résumés, preuves, reprises et façades conservent leur
comportement. En particulier, ne pas ajouter `timeout_seconds` à un résumé ou à
une preuve et ne pas changer une règle de lecture ou d'invalidation du cache.

## Périmètre d'écriture

Fichiers ForgePilot autorisés :

- `control-plane/forgepilot/durable.py`, uniquement pour supprimer
  `_TEST_TIMEOUT_SECONDS`, lire et valider `timeouts_seconds.proof` sur le
  chemin durable, puis le transmettre explicitement à `run_command` ;
- `control-plane/tests/test_acceleration.py`, uniquement pour ajouter de
  nouvelles méthodes de test rouge/vert de ce raccord.

Dans `control-plane/tests/test_acceleration.py`, il est interdit de modifier,
renommer, déplacer, supprimer, sauter ou relâcher une méthode de test déjà
présente. Il est également interdit de modifier leurs corps, leurs fixtures,
leurs doubles, leurs imports ou leurs appels existants. Ajouter seulement de
nouvelles méthodes. Toutes les méthodes déjà présentes doivent rester vertes
sans adaptation.

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

Avant la correction, ajouter une nouvelle méthode de test qui construit un état
durable depuis un profil chargé, dérive de cet état sa valeur
`timeouts_seconds.proof`, remplace `run_command` par un double instantané qui
enregistre `timeout_seconds`, puis exerce le raccord durable sans lancer
pytest. Sur la base, le contrôle échoue en montrant que la valeur reçue par
`run_command` diffère de celle dérivée de l'état. Le journal conserve la
commande, la sortie rouge et les deux valeurs observées.

Le contrôle ne dort pas, ne lance pas `sim/tests/` et ne simule pas une longue
durée. Il compare l'argument exact à la frontière où le processus serait
réellement lancé.

### SC2 — La valeur dérivée atteint chaque `run_command`

Après correction, le même contrôle est vert. Pour un état durable construit
depuis le profil chargé, chaque invocation de `run_command` effectuée par le
profil de test, y compris `git-diff-check` et chaque suite présente dans la
fixture, reçoit exactement la valeur lue dans `timeouts_seconds.proof`.

Le test dérive son attendu depuis le profil et l'état construits par le run. Il
ne recopie aucun nombre de politique dans l'assertion, ne valide pas la seule
présence d'un argument et ne s'arrête pas à une structure intermédiaire.

Une autre nouvelle méthode construit un run avec un autre profil de risque.
Sa propre valeur dérivée traverse le même chemin et atteint `run_command`
inchangée. Ce cas interdit le remplacement de la borne morte par une autre
constante, un maximum global ou une branche spéciale.

### SC3 — Tous les chemins durables transmettent la valeur effective

De nouvelles méthodes exercent, avec des doubles instantanés, les preuves
`fast`, `pr` et `certify`. Elles comparent chacune la valeur reçue par
`run_command` à `timeouts_seconds.proof` lu dans l'état au moment de
l'exécution. Le cas d'itération couvre `fast` puis `pr` ; un autre flux couvre
`pr` puis `certify` ; une reprise couvre au moins une de ces étapes.

Si le risque est relevé mécaniquement avant la preuve, l'attendu est dérivé de
l'état relevé, déjà enregistré dans `timeouts_seconds`, et non du risque
demandé au départ. Aucun contrôle ne contient une valeur numérique cible, ne
lance un agent réel, un processus long ou une suite `sim`.

### SC4 — L'absence et les valeurs invalides sont refusées

De nouvelles méthodes couvrent au minimum :

- l'absence de `timeouts_seconds` ;
- l'absence de `timeouts_seconds.proof` ;
- une valeur nulle ;
- une valeur booléenne.

Chaque cas échoue avant `run_command` avec un `PilotError` qui nomme
`timeouts_seconds.proof`. Aucun cas ne retombe sur la borne historique, une
valeur de politique recopiée ou une autre valeur implicite.

### SC5 — ForgePilot reste vert, sans changement annexe

Exécuter uniquement les suites du plan de contrôle ForgePilot :

```bash
cd control-plane && ../.venv/bin/python -m unittest tests.test_acceleration -v
cd control-plane && ../.venv/bin/python -m unittest discover -s tests
```

La première commande prouve ensemble les méthodes anciennes inchangées et les
nouvelles méthodes de raccord. La seconde suite complète doit être verte. Ne
pas lancer `sim/tests/` : le lot ne touche pas au produit, et sa preuve repose
sur des doubles de processus.

Le diff hors livrables contient seulement `durable.py` et
`test_acceleration.py`. Dans ce dernier fichier, le diff ne contient que des
ajouts de méthodes. La sélection et l'ordre des suites candidates sont
inchangés. `_TEST_TIMEOUT_SECONDS` a disparu et aucune borne globale
équivalente ne la remplace. Les résumés, les preuves et les règles de cache
n'ont pas changé.

## Livrables et séparation des rôles

Le manifeste déclare les deux fichiers de code/test autorisés, les deux
livrables et les commandes unitaires exécutées. Le journal contient :

- les constats de base et le SHA ;
- la preuve rouge ciblée, avec la valeur dérivée et la valeur alors reçue par
  `run_command` ;
- les preuves vertes où les valeurs dérivées des différents états atteignent
  exactement `run_command` ;
- les observations `fast`, `pr`, `certify` et reprise ;
- les refus des champs absents et des valeurs nulle et booléenne ;
- le résultat de la suite complète `control-plane/tests` ;
- le diff borné, la preuve que les anciennes méthodes de test sont inchangées
  et la confirmation qu'aucun fichier produit n'a changé.

Le journal ne présente aucun nombre comme délai cible. Il peut conserver les
nombres du constat historique, explicitement qualifiés comme observations de
la base avant correction.

L'exécutant n'écrit pas de `verdict.md`, ne juge pas la recevabilité de son
travail, ne fusionne rien et ne pousse pas directement sur `master`.

## Hors périmètre

- ajouter `timeout_seconds` à un résumé, à un JSON de preuve ou à l'état ;
- modifier la validation, l'identité, la lecture ou l'invalidation du cache des
  preuves ;
- modifier les valeurs ou profils de `workflow-policy.toml` ;
- modifier une méthode ou un appel déjà présent dans
  `control-plane/tests/test_acceleration.py` ;
- accélérer, découper, filtrer ou réordonner les suites candidates ;
- donner un délai distinct à chaque sous-suite : ce lot raccorde le délai de
  preuve existant à chaque commande du profil ;
- ajouter une variable d'environnement, une option CLI ou une configuration
  parallèle ;
- remplacer la borne morte par une autre constante globale, une table ou un
  cas spécial ;
- deviner une valeur quand `timeouts_seconds.proof` manque ou est invalide ;
- migrer les états durables ou changer leur schéma ;
- modifier le moteur, ses tests, la carte, le viewer ou une règle du monde ;
- relancer la suite longue du lot 043-bis pour prouver cette correction.
