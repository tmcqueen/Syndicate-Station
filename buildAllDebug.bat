@echo off
:: cd ../../

:: call git submodule update --init --recursive
call dotnet build --verbosity detailed --property WarningLevel=1 /clp:ErrorsOnly

:: pause
