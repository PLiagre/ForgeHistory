# Rubrique d’évaluation — brief 029

Cette rubrique a été écrite avant le code. Le brief 029 est l’unique source
d’instruction. L’évaluateur relit chaque condition SC0 à SC12 et reconstruit les
preuves depuis le head SHA ; il ne reprend aucun compteur du producteur sans le
recalculer.

## Porte éliminatoire

Le lot est refusé si l’un des faits suivants est constaté :

- un même acteur produit le code et prononce sa recevabilité ;
- Hermes ou Cursor peut fusionner sans ordre explicite du propriétaire ;
- le risque dérivé peut être abaissé par une simple déclaration ;
- une reprise rejoue un effet déjà accompli ou perd le lien au SHA ;
- un fichier hors périmètre peut être committé ;
- un cache DEM non vérifié peut être lu ;
- un vrai zéro DEM est transformé en absence ;
- une preuve liée à un ancien SHA est présentée comme courante ;
- les suites existantes régressent ;
- la CI déclenche un runner persistant du VPS depuis une PR publique.

## Méthode

1. Vérifier la chronologie Git : rubrique et brief précèdent le code.
2. Rejouer les cas rouges ajoutés, puis les cas verts.
3. Inspecter les sorties structurées et les écrire dans le verdict avec leurs
   dénominateurs dérivés.
4. Examiner le diff à la recherche de chemins masqués, secrets, sorties
   générées et instructions dupliquées.
5. Comparer les mesures de la preuve sentinelle aux résultats antérieurs
   lorsqu’ils sont disponibles, sans citer une empreinte figée par valeur.
6. Regarder toute capture créée par le lot. Le présent lot n’en exige aucune.

## Verdict attendu

Le verdict nomme explicitement SC0 à SC12 comme `PASS`, `FAIL` ou `BLOCKED`,
cite les commandes rejouées et distingue toute preuve complète non exécutable
faute de cache. Un `BLOCKED` n’est jamais converti en `PASS` par narration.
