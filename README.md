# My Mirror

<img src="logo.svg" width="64" height="64" alt="Logo My Mirror">

My Mirror affiche l’écran d’un iPhone dans une fenêtre Windows. L’iPhone utilise la recopie AirPlay du Centre de contrôle.

## Démarrage

1. Sur un PC Windows x64, installe une fois [UxPlay Windows](https://github.com/leapbtw/uxplay-windows/releases) avec son service Bonjour. Si UxPlay tourne déjà, quitte-le depuis sa zone de notification.
2. Télécharge et ouvre `MyMirror.exe` depuis les [versions GitHub](https://github.com/7Myo/My-Mirror/releases/latest). Le récepteur démarre avec le nom **My-Mirror**. Pour le renommer, saisis le nouveau nom et choisis **Appliquer**. Le récepteur redémarre avec un nouveau code.
3. Connecte l’iPhone au même réseau que le PC. Ouvre **Centre de contrôle → Recopie de l’écran**, sélectionne **My-Mirror**, puis saisis le code affiché si l’iPhone le demande.
4. La vidéo remplace l’accueil dans une fenêtre qui reprend les proportions initiales du rendu. Ferme cette fenêtre pour revenir à l’accueil. Pour quitter l’application, tu peux aussi utiliser l’icône **My Mirror** dans la zone de notification.

Windows peut demander d’autoriser le moteur UxPlay à recevoir les connexions locales lors de sa première utilisation. My Mirror fonctionne sans droits administrateur une fois UxPlay et Bonjour installés.

Au clavier, utilise **Tab** pour parcourir les commandes, **Alt+N** pour le nom et **Alt+C** pour le code. Le code peut être sélectionné et copié. My Mirror reprend les couleurs Windows quand un thème de contraste est actif au démarrage.

## Linux (aperçu)

Installe UxPlay et Tkinter avec le gestionnaire de paquets de ta distribution, puis lance `python3 linux/my_mirror.py` depuis le dépôt. Le lanceur affiche le nom et le code. Sous Linux, UxPlay ouvre sa propre fenêtre vidéo et l’accueil reste ouvert. Cette version est à valider sur un bureau Linux.

## Compilation

Exécute `powershell.exe -NoProfile -File .\build.ps1` depuis le dossier du projet. Le script utilise le compilateur C# de .NET Framework fourni avec Windows x64 et produit `MyMirror.exe`.

## Fonctionnement

Le lanceur WinForms démarre le processus UxPlay avec le nom du récepteur et un code temporaire. Bonjour annonce le service sur le réseau local. UxPlay décode le flux AirPlay et My Mirror intègre la surface Direct3D dans sa propre fenêtre.

My Mirror sauvegarde les paramètres UxPlay avant de les modifier. Il les restaure à l’arrêt normal ou au prochain démarrage après une fermeture forcée. Si UxPlay tourne encore après cette fermeture, quitte-le depuis la zone de notification, puis clique sur **Démarrer** dans My Mirror. Si le fichier a été modifié depuis, My Mirror garde les nouveaux réglages.

La [licence MIT](LICENSE) couvre le lanceur et son logo. [UxPlay](https://github.com/FDH2/UxPlay) et [UxPlay Windows](https://github.com/leapbtw/uxplay-windows) sont installés séparément et conservent leurs licences.
