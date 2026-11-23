# Vérifications du projet

Projet créé et vérifié le 19 novembre 2026 sur Windows x64 avec Unity **6000.3.24f1** et le SDK embarqué **1.2.0-preview.4**.

| Contrôle | Résultat |
| --- | --- |
| Scène principale | Générée et ouverte sans script manquant ; références du panneau, curseur, surface et profil vérifiées |
| Dépendances | SDK embarqué dans le projet et modules intégrés Unity ; aucun lien vers le dossier source externe |
| Démarrage | Simulation active, objet matériel inactif, aucune force armée |
| Entrées simulées | XYZ et masque des quatre boutons reçus correctement |
| Calibration | Capture des trois axes, application, sauvegarde JSON puis rechargement vérifiés |
| Surfaces | Six orientations et placement à 6 mm devant le curseur vérifiés |
| Friction | Paramètres avec/sans friction exportés correctement |
| Diagnostic | Export JSON créé dans le dossier temporaire de test |
| Cycle de vie | Déconnexion/reconnexion, relance de lecture et sortie de Play sans réarmement ; aucun worker restant |
| Player Windows x64 Mono | Compilation réussie ; contrôle exécuté dans le Player avec code de sortie zéro |
| Player en simulation | Connexion, entrées, mapping et références de surface vérifiés sans armement ni matériel |

Le contrôle du Player utilise les options `--playground-self-test --playground-result chemin.json`. Il n’est exécuté que si ces options sont fournies ; une ouverture normale affiche le banc interactif.

Les sources embarquées du SDK ont été comparées aux sources de la préversion fournie. Le SDK bénéficie aussi des 25 scénarios du noyau et des vérifications de quatre configurations Play Mode déjà effectués pour 1.2.0-preview.4 ; le parcours propre à ce projet a été exécuté avec les paramètres Play par défaut.

Les journaux sont conservés sur le poste de développement, hors des archives : `.validation/playground-build-scene.log`, `.validation/playground-smoke.log`, puis les journaux sous `Logs` et le résultat du Player sous `Builds/Windows`. Le journal de Play présente l’exception d’indexation interne `UnityEditor.Search.SearchDatabase` déjà observée dans les projets de validation précédents ; les assertions du parcours ont réussi. Les journaux ne sont donc pas intégralement exempts de messages d’erreur d’éditeur.

## Ce qui reste à vérifier

Les essais automatiques n’ont pas activé le Falcon réel. Les sensations, forces physiques, signes d’axes, initialisation, stabilité et délais de coupure demandent des essais matériels. Le focus et les interactions de la fenêtre Game restent à vérifier manuellement. Le Player a été contrôlé sans affichage graphique ; aucune vérification visuelle exhaustive des différents formats de fenêtre n’est revendiquée.

IL2CPP et les autres versions ou systèmes ne sont pas qualifiés par cette livraison. Ce projet teste les fonctions disponibles du SDK et ne transforme pas les fonctionnalités encore absentes en fonctions prises en charge.
