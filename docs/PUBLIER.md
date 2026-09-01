# Publier ce dépôt sur GitHub

L'agent qui a ouvert l'atelier n'avait pas le droit `repo` de *création*
sur le jeton GitHub (POST `/user/repos` → 403). Le code vit donc, en
attendant, sur une branche orpheline de ForgeHistory :

```text
https://github.com/PLiagre/ForgeHistory/tree/cursor/forgeatelier-ced6
```

Cette branche n'est **pas** à fusionner dans `master` : elle *est* le
dépôt. Pour la détacher :

```bash
gh repo create PLiagre/ForgeAtelier --public \
  --description "Infrastructure d'agents pour exécuter des lots. Pas un agent de plus."

git clone --branch cursor/forgeatelier-ced6 --single-branch \
  https://github.com/PLiagre/ForgeHistory.git ForgeAtelier
cd ForgeAtelier
git checkout -B master
git remote rename origin forgehistory
git remote add origin https://github.com/PLiagre/ForgeAtelier.git
git push -u origin master
```

Ensuite, sur ForgeHistory, on peut effacer la branche orpheline.
Le fichier `atelier.toml` du jeu pointe déjà ici.
