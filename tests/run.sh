#!/bin/sh
# Compiles the core with CoreTests and runs it in a throw-away home folder. Needs Mono.
set -e
cd "$(dirname "$0")/.."
WORK=$(mktemp -d)
BIN="$WORK/bin"; FAKE_HOME="$WORK/home"
mkdir -p "$BIN" "$FAKE_HOME/.config" "$WORK/fakebin"
# Stand-in for 'cmd /c mklink /J <link> <target>' so folder links work off Windows.
printf '#!/bin/sh\nln -s "$5" "$4"\n' > "$WORK/fakebin/cmd.exe"
chmod +x "$WORK/fakebin/cmd.exe"
CORE="src/Localization.cs src/Core/*.cs"
mcs -sdk:4.5 -langversion:5 -main:CoreTests -out:"$BIN/tests.exe" \
  -r:System.Management.dll -r:System.Web.Extensions.dll -r:System.Core.dll \
  $CORE tests/CoreTests.cs
HOME="$FAKE_HOME" XDG_CONFIG_HOME="$FAKE_HOME/.config" PATH="$WORK/fakebin:$PATH" mono "$BIN/tests.exe"
