#!/bin/sh
# Builds bin/ClaudeTogether.exe (windowed) and bin/claude-together.exe (console) with Mono's mcs.
# On Windows use build.cmd instead. Needs: mono-mcs and the 4.5 reference assemblies.
set -e
cd "$(dirname "$0")"
mkdir -p bin
FILES=$(find src -name '*.cs' | sort)
REFS="-r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Management.dll -r:System.Web.Extensions.dll -r:System.Core.dll"
mcs -sdk:4.5 -langversion:5 -target:winexe -out:bin/ClaudeTogether.exe -win32icon:assets/app.ico $REFS $FILES
mcs -sdk:4.5 -langversion:5 -target:exe -define:CLI -out:bin/claude-together.exe -win32icon:assets/app.ico $REFS $FILES
echo "Built: bin/ClaudeTogether.exe, bin/claude-together.exe"
