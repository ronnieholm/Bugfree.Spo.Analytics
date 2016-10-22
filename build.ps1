$sqlcmd = "C:\Program Files\Microsoft SQL Server\110\Tools\Binn\sqlcmd.exe"
$base = Split-Path $PSCommandPath
$web_base = "$($base)\src\Bugfree.Spo.Analytics.Cli"
$db_name = "Bugfree.Spo.Analytics"
$mdf_file = "$($web_base)\$($db_name).mdf"
$log_file = "$($web_base)\$($db_name).ldf"
$detachdb = "$($web_base)\detachdb.sql"
$createdb = "$($web_base)\createdb.sql"

$detach_db_sql = @"
    USE master;
    GO
    EXEC sp_detach_db @dbname = '$db_name';
    GO
"@

$detach_db_sql | Out-File $detachdb

if (Test-Path $mdf_file) {
    Remove-Item $mdf_file
}

if (Test-Path $log_file) {
    Remove-Item $log_file
}

$create_db_sql = @"
    USE master;
    GO
    CREATE DATABASE [$db_name]
    ON
    (NAME = '$($db_name)_dat',
         FILENAME = '$mdf_file',
         SIZE = 10MB,
         MAXSIZE = 50MB,
         FILEGROWTH = 5MB)
    LOG ON
    (NAME = '$($db_name)_log',
         FILENAME = '$log_file',
         SIZE = 5MB,
         MAXSIZE = 25MB,
         FILEGROWTH = 5MB)
    GO

    USE [$db_name];
"@

$schema = [system.io.file]::ReadAllText("$($web_base)\schema.sql")
$create_db_sql = $create_db_sql + $schema
$create_db_sql | Out-File $createdb
&$sqlcmd -S "(localdb)\v11.0" -i $createdb
&$sqlcmd -S "(localdb)\v11.0" -i $detachdb

$msbuild = "C:\Program Files (x86)\MSBuild\14.0\bin\amd64\msbuild.exe"
&$msbuild "$($base)\src\Bugfree.Spo.Analytics.sln"