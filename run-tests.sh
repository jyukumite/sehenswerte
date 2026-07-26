#!/bin/bash
# Headless test runner for the Sehens/Core suite under Wine (macOS dev).
# Cross-building net6.0-windows on macOS emits only Use.dll (no apphost .exe), so run the managed
# dll through the Windows dotnet host inside the wine prefix.
#
# Usage: ./run-tests.sh [classSubstr] [methodSubstr]
#   ./run-tests.sh                 # run every [TestClass]/[TestMethod]
#   ./run-tests.sh HorizontalAffine # only test classes whose name contains "HorizontalAffine"
#
# On ACTUAL WINDOWS (no Wine needed) - any of:
#   dotnet build example\Use.csproj -c Debug
#   example\bin\Debug\net6.0-windows\Use.exe runtest [classSubstr] [methodSubstr]   # apphost exists on Windows
#   # or run the managed dll directly:
#   dotnet example\bin\Debug\net6.0-windows\Use.dll runtest [classSubstr] [methodSubstr]
#   # or use the normal MSTest runners (no runtest verb, runs the whole suite):
#   dotnet test src\core\Core.csproj      # and \src\sehens\Sehens.csproj, or via the VS Test Explorer
set -e
cd "$(dirname "$0")"
dotnet build example/Use.csproj -c Debug -v quiet --nologo -p:CheckEolTargetFramework=false >/dev/null
OUT="example/bin/Debug/net6.0-windows"
( cd "$OUT" && WINEDEBUG=-all wine "C:/Program Files/dotnet/dotnet.exe" Use.dll runtest "$@" 2>/dev/null )
