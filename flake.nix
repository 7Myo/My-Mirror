{
  description = "My Mirror pour Linux";
  inputs.nixpkgs.url = "github:NixOS/nixpkgs/f9a75f41be3a74eab55d3a7679d34ad6cc0da977";
  outputs = { self, nixpkgs }:
    let
      systems = [ "x86_64-linux" "aarch64-linux" ];
      eachSystem = nixpkgs.lib.genAttrs systems;
    in {
      packages = eachSystem (system: {
        default = nixpkgs.legacyPackages.${system}.callPackage ./linux/package.nix { };
      });
      apps = eachSystem (system: {
        default = {
          type = "app";
          program = "${self.packages.${system}.default}/bin/my-mirror";
        };
      });
    };
}
