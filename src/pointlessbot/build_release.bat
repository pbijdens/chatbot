@echo off
dotnet publish -r ubuntu.16.04-x64 -c Release
pscp  -r bin\Release\netcoreapp2.0\ubuntu.16.04-x64\publish pbijdens@52.232.56.148:/home/pbijdens/jehoofd2
