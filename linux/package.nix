{ lib, stdenvNoCC, makeWrapper, python3, uxplay, makeDesktopItem }:
let
  python = python3.withPackages (ps: [ ps.tkinter ]);
  desktop = makeDesktopItem {
    name = "my-mirror";
    desktopName = "My Mirror";
    exec = "my-mirror";
    icon = "my-mirror";
    categories = [ "AudioVideo" "Network" ];
  };
in stdenvNoCC.mkDerivation {
  pname = "my-mirror";
  version = "1.4.0";
  src = ../.;
  nativeBuildInputs = [ makeWrapper ];
  dontBuild = true;
  installPhase = ''
    mkdir -p $out/bin $out/share/my-mirror $out/share/icons/hicolor/scalable/apps
    cp linux/my_mirror.py $out/share/my-mirror/
    cp logo.svg $out/share/icons/hicolor/scalable/apps/my-mirror.svg
    cp -r ${desktop}/share/applications $out/share/
    makeWrapper ${python}/bin/python3 $out/bin/my-mirror \
      --add-flags "$out/share/my-mirror/my_mirror.py" \
      --prefix PATH : ${lib.makeBinPath [ uxplay ]}
  '';
  meta = {
    description = "Récepteur AirPlay My Mirror";
    license = lib.licenses.mit;
    platforms = lib.platforms.linux;
    mainProgram = "my-mirror";
  };
}
