# Changelog

## 1.2.0-preview.5

- Repère d’axes observés `FalconAxisAlignment` : permutations, signes et bases orthonormales, calculés depuis trois gestes distincts (droite, haut, vers soi).
- Mapping commun des positions, vitesses, géométrie et forces ; conservation du travail par la transposée, y compris après réorientation.
- `FalconWorkspace` et profils de calibration conservent les directions ; les anciens profils gardent leur comportement sans alignement explicite.
- Gizmos corrigés pour les repères réorientés et leurs boîtes brutes transformées.
- Réglage de matériau de surface via `SetMaterial`, avec validation et désarmement sur changement.
- Playground 1.1 : parcours guidé, lecture seule avant confirmation des directions, sauvegarde JSON v2 complète.

Validation numérique des 48 permutations signées et du cas gauche/droite → haut/bas dans Unity. Validation des gestes et forces sur le Falcon physique encore nécessaire.

## 1.2.0-preview.4

- Friction dynamique tangentielle régularisée, configurable par coefficient et vitesse physique de transition.
- Budget vectoriel commun : priorité au contact normal, friction bornée par la charge et le plafond total existant.
- Direction opposée au mouvement courant, remise à zéro à l’arrêt et hors contact ; aucune adhérence statique ajoutée.
- Montée partagée et suppression de la mémoire tangentielle qui assisterait un changement de direction.
- `SetFriction` désarme sur modification ; export immuable des paramètres vers la boucle dédiée.
- Démonstration avec friction à 0,5, activation/désactivation et pic de friction par prise pour visualiser les commandes brèves.

La friction reste désactivée par défaut sur les surfaces existantes. La limite de force maximale reste inchangée et la validation matérielle reste nécessaire.

## 1.2.0-preview.3

- Première face haptique finie liée à un `BoxCollider`, exportée en données numériques dans le repère physique.
- Ressort amorti unilatéral, outil sphérique, atténuation aux bords, montée limitée et plafond configurable jusqu’à 1,2 N.
- Calcul indépendant du rendu dans la boucle dédiée ; aucune API Unity utilisée par le contact.
- Refus de démarrage derrière la face et désarmement sur pénétration excessive, changement de données ou géométrie périmée.
- Composant et inspecteur `FalconSurface`, diagnostic de contact et scène `Surface Demo` utilisable sans Falcon.
- Une surface à la fois, exclusive des six effets historiques ; objets avec Rigidbody refusés. Friction, arêtes, contacts multiples et proxy contraint restent à réaliser.

## 1.2.0-preview.2

- Composant `FalconWorkspace` : origine physique, mapping vers le Transform de la scène, échelles par axe, inversion de Z et gizmos.
- Capture des extrema observés avec validation des trois axes et marge configurable ; recentrage sur lecture récente.
- Profils de calibration persistants, chargement et enregistrement explicites depuis l’inspecteur.
- Conversions indépendantes de Unity pour positions, vitesses et forces conjuguées, avec conservation du travail virtuel.
- Boîte logicielle facultative, limitation visuelle du curseur et désarmement dans le worker hors zone.
- Désarmement lors des changements de repère et pendant la capture ; préservation du mapping historique sans `FalconWorkspace`.

Les extrema ne certifient pas la géométrie accessible du Falcon. L’initialisation matérielle reste distincte de la calibration utilisateur. Les six effets existants restent calculés dans le repère matériel ; les conversions de forces sont proposées aux futurs effets et surfaces.

## 1.2.0-preview.1

- Extraction de `IFalconBackend` et de `FalconSession`, indépendants de Unity.
- Backend simulé avec position, boutons, déconnexion injectée et force enregistrée.
- État horodaté et séquencé, détection des lectures périmées et statistiques d’intervalles.
- Arrêt coopératif confirmé, contrôle du thread propriétaire DHD et connexion unique par processus.
- Nettoyage natif centralisé, erreurs remontées et DLL conservée si la fermeture échoue.
- Gestion des transitions Play, pauses et rechargements d’assemblies dans l’éditeur.
- Tests sans matériel et vérification reproductible des quatre configurations de Play Mode.

Les six effets et leurs plafonds sont conservés. Calibration, colliders, IL2CPP et validation matérielle de cette version restent à réaliser. Aucun mode 4/8 N ni rappel après relâchement n’est ajouté.
