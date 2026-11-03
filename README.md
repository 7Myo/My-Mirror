# My Mirror

<img src="logo.svg" width="64" height="64" alt="Logo My Mirror">

My Mirror affiche l’écran d’un iPhone dans une fenêtre Windows. L’iPhone utilise la recopie AirPlay du Centre de contrôle.

## Démarrage

1. Sur un PC Windows x64, installe une fois [UxPlay Windows](https://github.com/leapbtw/uxplay-windows/releases) avec son service Bonjour. Si UxPlay tourne déjà, quitte-le depuis sa zone de notification.
2. Télécharge `MyMirror-Windows-x64.zip` depuis les [versions GitHub](https://github.com/7Myo/My-Mirror/releases/latest), décompresse-le et lance `MyMirror.exe`. Le récepteur démarre avec le nom **My-Mirror**. Pour changer le nom, clique sur **Arrêter**, saisis le nouveau nom, puis clique sur **Démarrer**.
3. Connecte l’iPhone au même réseau que le PC. Ouvre **Centre de contrôle → Recopie de l’écran**, sélectionne **My-Mirror**, puis saisis le code affiché si l’iPhone le demande.
4. La vidéo remplace l’accueil. Ferme la fenêtre vidéo pour revenir à l’accueil et recevoir une autre connexion.

Windows peut demander d’autoriser le moteur UxPlay à recevoir les connexions locales lors de sa première utilisation. My Mirror fonctionne sans droits administrateur une fois UxPlay et Bonjour installés.

## Compilation

Exécute `powershell.exe -NoProfile -File .\build.ps1` depuis le dossier du projet. Le script utilise le compilateur C# de .NET Framework fourni avec Windows x64 et produit `MyMirror.exe`.

## Fonctionnement

Le lanceur WinForms démarre le processus UxPlay avec le nom du récepteur et un code temporaire. Bonjour annonce le service sur le réseau local. UxPlay décode le flux AirPlay et My Mirror intègre la surface Direct3D dans sa propre fenêtre. À l’arrêt normal, le lanceur restaure le fichier d’arguments UxPlay précédent.

La [licence MIT](LICENSE) couvre le lanceur et son logo. [UxPlay](https://github.com/FDH2/UxPlay) et [UxPlay Windows](https://github.com/leapbtw/uxplay-windows) sont installés séparément et conservent leurs licences.
