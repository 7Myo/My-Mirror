#!/bin/sh
set -eu
cd "$(dirname "$0")"
flatpak-builder --user --install-deps-from=flathub --force-clean --repo=repo build-dir io.github.SevenMyo.MyMirror.json
flatpak build-bundle repo MyMirror-Linux-x86_64.flatpak io.github.SevenMyo.MyMirror --runtime-repo=https://flathub.org/repo/flathub.flatpakrepo
