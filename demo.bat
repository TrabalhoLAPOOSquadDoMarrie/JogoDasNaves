@echo off
echo ===============================================
echo    ASTEROIDES MULTIPLAYER - DEMO SCRIPT
echo ===============================================
echo.

echo [1/3] Compilando projetos...
dotnet build AsteroidesMultiplayer.sln
if %ERRORLEVEL% neq 0 (
    echo ERRO: Falha na compilacao!
    pause
    exit /b 1
)

echo.
echo [2/3] Iniciando servidor...
echo O servidor sera iniciado em uma nova janela.
echo Aguarde a mensagem "Servidor iniciado! Aguardando conexoes..."
start "Servidor Asteroides" cmd /k "dotnet run --project AsteroidesServidor"

echo.
echo [3/3] Aguardando 3 segundos para iniciar cliente...
timeout /t 3 /nobreak > nul

echo Iniciando primeiro cliente...
start "Cliente 1" cmd /k "dotnet run --project AsteroidesCliente"

echo.
echo ===============================================
echo Para testar multiplayer, execute novamente:
echo   dotnet run --project AsteroidesCliente
echo.
echo Para parar o servidor, pressione 'q' na janela do servidor
echo ===============================================
pause