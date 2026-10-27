param(
    [Parameter(Mandatory=$true)][string]$PostgresBin,
    [string]$Filter,
    [string]$ResultName = 'native-postgres'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('finsight-pg-tests-' + [Guid]::NewGuid().ToString('N'))
$data = Join-Path $scratch 'data'
$passwordFile = Join-Path $scratch 'bootstrap-password'
$oldConnection = $env:FINSIGHT_TEST_POSTGRES_ADMIN
$started = $false
$testExit = 1
New-Item -ItemType Directory -Path $scratch | Out-Null
try {
    $random = New-Object byte[] 48
    [Security.Cryptography.RandomNumberGenerator]::Fill($random)
    $password = [Convert]::ToBase64String($random)
    [IO.File]::WriteAllText($passwordFile, $password)
    & (Join-Path $PostgresBin 'initdb.exe') -D $data -U postgres -A scram-sha-256 --pwfile=$passwordFile --encoding=UTF8 --locale=C *> (Join-Path $scratch 'init.log')
    if ($LASTEXITCODE -ne 0) { throw "Disposable cluster initialization failed. See $scratch\init.log" }
    Remove-Item -LiteralPath $passwordFile
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    $server = Start-Process -FilePath (Join-Path $PostgresBin 'pg_ctl.exe') -ArgumentList @('-D', "`"$data`"", '-l', "`"$scratch\postgres.log`"", '-o', "`"-h 127.0.0.1 -p $port`"", '-w', 'start') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $scratch 'start.log') -RedirectStandardError (Join-Path $scratch 'start-error.log')
    $server.WaitForExit()
    if ($server.ExitCode -ne 0) { throw "Disposable cluster startup failed. See $scratch\start.log" }
    $started = $true
    $env:FINSIGHT_TEST_POSTGRES_ADMIN = "Host=127.0.0.1;Port=$port;Database=postgres;Username=postgres;Password=$password;Include Error Detail=false"
    $password = $null
    Push-Location $root
    try {
        $arguments = @('test', '--no-restore', '--logger', "trx;LogFilePrefix=$ResultName")
        if ($Filter) { $arguments += @('--filter', $Filter) }
        & dotnet @arguments
        $testExit = $LASTEXITCODE
    } finally { Pop-Location }
} finally {
    $env:FINSIGHT_TEST_POSTGRES_ADMIN = $oldConnection
    if (Test-Path -LiteralPath $passwordFile) { Remove-Item -LiteralPath $passwordFile }
    if ($started) { & (Join-Path $PostgresBin 'pg_ctl.exe') -D $data -m fast -w stop *> (Join-Path $scratch 'stop.log') }
    # Keep the isolated cluster's diagnostics in TEMP for failure investigation.
    Write-Host "Native test runner finished. Diagnostics: $scratch"
}
exit $testExit
