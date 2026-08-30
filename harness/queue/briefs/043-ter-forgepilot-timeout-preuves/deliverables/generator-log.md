# Journal — lot 043-ter ForgePilot timeout preuves

## Constats de base

SHA de travail au lancement :

```
25a61daf094c4a15ea3bd5ef41841fa9b9b3b761
```

(base attendue à la rédaction du brief : `4b732778fc7970ce3e0e108369adc5ff60b5a2a5`)

```bash
rg -n "_TEST_TIMEOUT_SECONDS|run_test_profile|timeout_seconds" control-plane/forgepilot/durable.py
```

Avant correction, la base portait `_TEST_TIMEOUT_SECONDS = 1800` et `run_test_profile` passait cette constante à chaque `run_command`, sans lire `timeouts_seconds.proof` de l'état.

```bash
rg -n -A5 "risks.R[012]\.timeouts" control-plane/workflow-policy.toml
```

La politique déclarait déjà des délais `proof` distincts par risque (observation historique de la base, pas une cible de ce lot).

```bash
rg -n "timeouts_seconds" control-plane/forgepilot/state.py control-plane/forgepilot/durable.py
```

L'état durable créait et mettait à jour `timeouts_seconds` depuis le profil ; le chemin de preuve ne le consommait pas encore.

## Preuve rouge (SC1)

Commande ciblée (comportement historique simulé : `run_test_profile` force encore la borne morte à la frontière processus) :

```bash
cd control-plane && python3 -m unittest \
  tests.test_acceleration.ProofTimeoutTransmissionTests.test_durable_proof_path_passes_state_proof_timeout_to_run_command -v
```

Sortie rouge observée :

```
AssertionError: 21600 != 1800
```

- valeur dérivée de l'état R2 construit depuis le profil chargé : `21600`
- valeur reçue par `run_command` sur la base historique : `1800`

Le contrôle compare l'argument exact à la frontière `run_command`, sans lancer `sim/tests/` ni simuler une longue durée.

## Preuves vertes

Après correction dans `durable.py` :

- `_proof_timeout_seconds(state)` lit et valide `timeouts_seconds.proof` (entier strictement positif, booléen refusé) ;
- `_run_exact_test_profile` transmet cette valeur à `run_test_profile(..., timeout_seconds=...)` ;
- chaque `run_command` de preuve reçoit la valeur inchangée ;
- `_TEST_TIMEOUT_SECONDS` a été supprimé.

```bash
cd control-plane && python3 -m unittest tests.test_acceleration.ProofTimeoutTransmissionTests -v
```

11 tests verts sur le raccord durable (dont la façade historique sans état).

Valeurs dérivées des états construits (jamais recopiées depuis la politique dans les assertions) :

- R2 : chaque appel enregistré reçoit la valeur lue dans `timeouts_seconds.proof` de l'état R2 ;
- R1 : valeur distincte de R2, transmise inchangée sur le même chemin ;
- profils `fast`, `pr`, `certify` : même valeur effective pour chaque `run_command` mocké ;
- itération `fast` puis `pr`, reprise `pr` après suppression du cache fichier : valeur effective conservée ;
- relèvement mécanique R1→R2 via `_raise_risk_from_paths` : l'attendu suit l'état relevé, pas le risque initial.

Refus avant `run_command` (`PilotError` nommant `timeouts_seconds.proof`) :

- `timeouts_seconds` absent ;
- clé `proof` absente ;
- valeur nulle ;
- valeur booléenne.

## Itération après relecture Grok FAIL (P1)

Finding unique : `run_test_profile` conservait une borne morte locale via `timeout_seconds = 1800` quand l'argument optionnel était absent.

Correction :

- suppression entière du repli `if timeout_seconds is None: timeout_seconds = 1800` ;
- la façade historique transmet désormais `None` tel quel à `run_command` (absence de délai, pas de seconde politique cachée) ;
- le chemin durable continue d'exiger et de transmettre l'entier validé depuis `timeouts_seconds.proof` via `_proof_timeout_seconds` et `_run_exact_test_profile`.

Nouvelle méthode de preuve :

- `ProofTimeoutTransmissionTests.test_run_test_profile_without_state_passes_none_timeout` — vérifie que chaque appel mocké reçoit `None`, jamais `1800`.

Vérification :

```bash
rg -n "1800" control-plane/forgepilot/durable.py
# aucune occurrence
```

## Suite complète ForgePilot

```bash
cd control-plane && python3 -m unittest tests.test_acceleration -v
# Ran 76 tests — 75 OK, 1 ERROR (TestRunnerTests sans pytest dans l'environnement courant)

cd control-plane && python3 -m unittest discover -s tests
# Ran 189 tests — 188 OK, 1 ERROR (même cause : pytest absent pour TestRunnerTests)
```

Les 11 tests ciblés `ProofTimeoutTransmissionTests` sont verts. L'unique erreur restante est préexistante à l'absence de `pytest` pour `TestRunnerTests` ; elle n'est pas liée au retrait du repli `1800`.

## Diff borné et périmètre

Fichiers de code modifiés :

- `control-plane/forgepilot/durable.py`
- `control-plane/tests/test_acceleration.py` (ajouts uniquement : nouvelle classe `ProofTimeoutTransmissionTests`, imports additionnels)

Livrables :

- `harness/queue/briefs/043-ter-forgepilot-timeout-preuves/deliverables/manifest.json`
- `harness/queue/briefs/043-ter-forgepilot-timeout-preuves/deliverables/generator-log.md`

`git diff --check` sur les fichiers de code : aucun problème d'espace.

Aucun fichier produit (`sim/**`, `viewer/**`, `harness/tests/**`, politique, état) n'a changé.

Les méthodes de test déjà présentes dans `test_acceleration.py` n'ont pas été modifiées.
