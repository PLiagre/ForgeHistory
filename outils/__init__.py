"""Les outils du dépôt : ce que la CI décide, et rien d'autre.

Cinq commandes, aucune écriture sur GitHub. `relecture` dit si une PR a
été relue par quelqu'un qui ne l'a pas écrite ; `integration` dit quelle
PR entre dans `master` ; `palier` dit quand une couche finie appelle son
lot de stabilisation ; `tableau` écrit la page du travail ; `saisie` lit
une demande de lot. `palier --ecrire` et `saisie --ecrire` posent une
fiche au registre, localement. C'est le workflow qui parle à GitHub,
jamais eux.

Cette séparation n'est pas une élégance. Une décision qui appelle le
réseau ne s'éprouve qu'en ligne, et un contrôle qu'on ne peut pas jouer
hors ligne est un contrôle qu'on ne joue pas.
"""
