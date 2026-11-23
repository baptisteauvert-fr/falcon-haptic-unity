# Parcours de test

Les cases constituent un relevé manuel, pas une certification automatique du matériel.

## Sans Falcon

- [ ] Ouvrir la scène principale et lancer Play : Simulation sélectionnée, forces désarmées.
- [ ] Déplacer X, Y, Z et cocher les quatre boutons : position et indicateurs cohérents.
- [ ] Essayer les six modes un à un ; relâcher le bouton 1 coupe chacun d’eux.
- [ ] Changer d’onglet ou de mode : nouvel armement nécessaire.
- [ ] Activer la surface devant la face, avancer et observer le contact et la commande.
- [ ] Balayer X/Y en contact : friction opposée au déplacement, pic observable.
- [ ] Mettre la friction à zéro et réarmer : pas de composante tangentielle.
- [ ] Changer de face et placer la surface devant : nouvelle géométrie visible, pas d’armement automatique.
- [ ] Sélectionner la surface dans l’Inspecteur : tester rigidité, amortissement, cap, rayon et bord.
- [ ] Commencer la surface déjà derrière la face : refus, puis reprise après retour devant et nouvel armement.
- [ ] Recentrer, capturer les trois axes puis terminer : limites calculées et boîte orange.
- [ ] Capturer un seul axe : calibration refusée.
- [ ] Changer échelle, rotation ou inversion de Z : repère et curseur modifiés, forces désarmées.
- [ ] Enregistrer la calibration, charger le profil exemple puis recharger le JSON : données restaurées.
- [ ] Sortir de la boîte : désarmement ; le retour ne réarme pas.
- [ ] Simuler une déconnexion, puis reconnecter : erreur explicite, reprise en lecture seule.
- [ ] Relancer la lecture : nouvelle session sans armement.
- [ ] Exporter un diagnostic : JSON créé au chemin indiqué.
- [ ] Mettre Play en pause, changer de fenêtre, arrêter Play puis relancer : aucune force automatiquement réactivée.

## Avec Falcon, séparément

- [ ] Arrêter les autres lecteurs et connecter le matériel depuis le bouton dédié.
- [ ] Vérifier SDK/pilote, initialisation, trois axes et quatre boutons avant d’armer.
- [ ] Vérifier les repères et les signes de forces à faible intensité.
- [ ] Calibrer sans forcer les butées ; enregistrer dans le fichier matériel distinct.
- [ ] Évaluer les six effets et la surface souple, dans une zone confortable.
- [ ] Mesurer et relever stabilité, cadence, saturation et délais réels de coupure pour chaque incident prévu.
- [ ] Tester les scénarios de déconnexion et de fermeture avec un protocole adapté au matériel.

La force affichée dans le simulateur est une commande calculée, pas une mesure de force. Les valeurs de rigidité, de limite et de marge sont des choix logiciels de départ.
