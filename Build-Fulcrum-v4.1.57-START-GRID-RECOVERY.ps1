$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$CoreSource = Join-Path $Root 'Fulcrum.Core'
$PluginSource = Join-Path $Root 'Fulcrum.Plugin'
$Dist = Join-Path $Root 'dist'

New-Item -ItemType Directory -Force -Path $Dist | Out-Null

if ([string]::IsNullOrWhiteSpace($env:TEMP)) { throw 'No se encontro TEMP.' }

$Work = Join-Path $env:TEMP ('Fulcrum4157_' + $PID)
$RoslynRoot = Join-Path $env:TEMP 'FulcrumRoslyn560'
$RoslynPkg = Join-Path $env:TEMP 'FulcrumRoslyn560.nupkg'

try {
    if (Test-Path $Work) { Remove-Item -LiteralPath $Work -Recurse -Force -ErrorAction SilentlyContinue }
    $WorkCoreSrc = Join-Path $Work 'core'
    $WorkPluginSrc = Join-Path $Work 'plugin'
    $WorkBuild = Join-Path $Work 'build'
    $WorkOut = Join-Path $Work 'out'
    New-Item -ItemType Directory -Force -Path $WorkCoreSrc,$WorkPluginSrc,$WorkBuild,$WorkOut | Out-Null

    Copy-Item -Path (Join-Path $CoreSource '*') -Destination $WorkCoreSrc -Recurse -Force
    Copy-Item -Path (Join-Path $PluginSource '*') -Destination $WorkPluginSrc -Recurse -Force

    $Csc472 = Join-Path $RoslynRoot 'tasks\net472\csc.exe'
    $Csc46 = Join-Path $RoslynRoot 'tasks\net46\csc.exe'
    if (-not ((Test-Path $Csc472) -or (Test-Path $Csc46))) {
        if (Test-Path $RoslynRoot) { Remove-Item -LiteralPath $RoslynRoot -Recurse -Force -ErrorAction SilentlyContinue }
        if (-not (Test-Path $RoslynPkg)) {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Write-Host 'Descargando Roslyn 5.6.0...' -ForegroundColor Cyan
            Invoke-WebRequest -UseBasicParsing -Uri 'https://www.nuget.org/api/v2/package/Microsoft.Net.Compilers.Toolset/5.6.0' -OutFile $RoslynPkg
        }
        New-Item -ItemType Directory -Force -Path $RoslynRoot | Out-Null
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($RoslynPkg,$RoslynRoot)
    }

    $csc=@($Csc472,$Csc46) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $csc) { throw 'No se encontro csc.exe.' }

    $refRoot=@(
        "${env:ProgramFiles(x86)}\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8",
        "$env:ProgramFiles\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $refRoot) { throw 'No se encontraron Reference Assemblies .NET Framework 4.8.' }

    $simHubCandidates=@()
    if ($env:SIMHUB_PATH) { $simHubCandidates += $env:SIMHUB_PATH }
    $simHubCandidates += "${env:ProgramFiles(x86)}\SimHub"
    $simHubCandidates += "$env:ProgramFiles\SimHub"
    $SimHub=$simHubCandidates | Where-Object { $_ -and (Test-Path (Join-Path $_ 'SimHub.Plugins.dll')) } | Select-Object -First 1
    if (-not $SimHub) { throw 'No se encontro SimHub. Define SIMHUB_PATH si usas otra ruta.' }

    # ---------- Compile Fulcrum.Core ----------
    $CoreOut = Join-Path $WorkOut 'Fulcrum.Core.dll'
    $CorePdb = Join-Path $WorkOut 'Fulcrum.Core.pdb'
    $CoreRsp = Join-Path $WorkBuild 'core.rsp'
    $CoreLines=New-Object System.Collections.Generic.List[string]
    foreach($o in @('/nologo','/target:library','/langversion:latest','/optimize+','/debug:pdbonly','/platform:anycpu','/nostdlib+')){$CoreLines.Add($o)}
    $CoreLines.Add(('/out:"{0}"' -f $CoreOut))
    $CoreLines.Add(('/pdb:"{0}"' -f $CorePdb))
    foreach($n in @('mscorlib.dll','System.dll','System.Core.dll','System.Data.dll','System.Data.DataSetExtensions.dll','System.Net.Http.dll','System.Xml.dll','System.Xml.Linq.dll','Microsoft.CSharp.dll')){
        $r=Join-Path $refRoot $n
        if(-not(Test-Path $r)){throw('Falta referencia Core: '+$r)}
        $CoreLines.Add(('/reference:"{0}"' -f $r))
    }
    Get-ChildItem $WorkCoreSrc -Recurse -Filter '*.cs' | Sort-Object FullName | ForEach-Object {$CoreLines.Add(('"{0}"' -f $_.FullName))}
    [IO.File]::WriteAllLines($CoreRsp,$CoreLines.ToArray(),(New-Object Text.UTF8Encoding($false)))

    Write-Host ''
    Write-Host 'Compilando Fulcrum.Core v4.1.42...' -ForegroundColor Cyan
    & $csc ("@$CoreRsp")
    if($LASTEXITCODE -ne 0 -or -not(Test-Path $CoreOut)){throw 'Fallo la compilacion de Fulcrum.Core.'}

    # Test the compiled Core before creating any distribution DLLs.
    $TestsSource = Join-Path $Root 'Tests\RegressionTests.cs'
    $ClassTestsSource = Join-Path $Root 'Tests\ClassPositionTests.cs'
    $TestsExe = Join-Path $WorkOut 'Fulcrum.RegressionTests.exe'
    $TestMscorlib = Join-Path $refRoot 'mscorlib.dll'
    $TestSystem = Join-Path $refRoot 'System.dll'
    Write-Host 'Ejecutando regresiones de multiclass, pits y cruces de meta...' -ForegroundColor Cyan
    & $csc /nologo /target:exe /langversion:latest /nostdlib+ "/out:$TestsExe" "/reference:$CoreOut" "/reference:$TestMscorlib" "/reference:$TestSystem" $TestsSource $ClassTestsSource
    if($LASTEXITCODE -ne 0 -or -not(Test-Path $TestsExe)){throw 'Fallo la compilacion de las pruebas de regresion.'}
    & $TestsExe
    if($LASTEXITCODE -ne 0){throw 'Una prueba de regresion fallo. No se generara el paquete dist.'}

    # Test the real production module and publishers, not just pure algorithms.
    # Only SimHub's property storage is replaced by a small test registry.
    $PipelineExe = Join-Path $WorkOut 'Fulcrum.RelativePipelineTests.exe'
    $PipelineRsp = Join-Path $WorkBuild 'pipeline-tests.rsp'
    $PipelineLines = New-Object System.Collections.Generic.List[string]
    foreach($o in @('/nologo','/target:exe','/langversion:latest','/nostdlib+')){$PipelineLines.Add($o)}
    $PipelineLines.Add(('/out:"{0}"' -f $PipelineExe))
    foreach($r in @($CoreOut,$TestMscorlib,$TestSystem,(Join-Path $refRoot 'System.Core.dll'))){$PipelineLines.Add(('/reference:"{0}"' -f $r))}
    foreach($relativeFile in @(
        'Tests\SimHubPropertyStub.cs',
        'Tests\RelativeIntegrationTests.cs',
        'Fulcrum.Plugin\Modules\RelativeModule.cs',
        'Fulcrum.Plugin\Modules\RelativePropertyNames.cs',
        'Fulcrum.Plugin\Settings\RelativeOverlaySettings.cs',
        'Fulcrum.Plugin\Publishing\RelativePublisher.cs',
        'Fulcrum.Plugin\Publishing\RelativeDisplayPublisher.cs',
        'Fulcrum.Plugin\Publishing\RelativeTablePublisher.cs'
    )){
        $sourceFile = Join-Path $Root $relativeFile
        if(-not(Test-Path $sourceFile)){throw('Falta prueba de integracion: '+$sourceFile)}
        $PipelineLines.Add(('"{0}"' -f $sourceFile))
    }
    [IO.File]::WriteAllLines($PipelineRsp,$PipelineLines.ToArray(),(New-Object Text.UTF8Encoding($false)))
    Write-Host 'Probando lectura real -> RelativeModule -> tabla publicada...' -ForegroundColor Cyan
    & $csc ("@$PipelineRsp")
    if($LASTEXITCODE -ne 0 -or -not(Test-Path $PipelineExe)){throw 'Fallo la compilacion del test de integracion Relative.'}
    & $PipelineExe
    if($LASTEXITCODE -ne 0){throw 'Fallo el test integrado de Relative. No instales ningun DLL.'}

    # ---------- Reuse icon from installed Fulcrum.Plugin.dll ----------
    $InstalledPlugin = Join-Path $SimHub 'Fulcrum.Plugin.dll'
    $WorkIcon = Join-Path $Work 'FS_TRANSPARENT_PREVIEW.png'
    if (Test-Path $InstalledPlugin) {
        try {
            $asm=[Reflection.Assembly]::LoadFile($InstalledPlugin)
            $stream=$asm.GetManifestResourceStream('Fulcrum.Plugin.Resources.FS_TRANSPARENT_PREVIEW.png')
            if ($stream) {
                $fs=[IO.File]::Create($WorkIcon)
                $stream.CopyTo($fs)
                $fs.Close()
                $stream.Close()
            }
        } catch {
            Write-Host 'Aviso: no se pudo reutilizar el icono del plugin instalado.' -ForegroundColor Yellow
        }
    }

    # ---------- Compile Fulcrum.Plugin ----------
    $PluginOut = Join-Path $WorkOut 'Fulcrum.Plugin.dll'
    $PluginPdb = Join-Path $WorkOut 'Fulcrum.Plugin.pdb'
    $PluginRsp = Join-Path $WorkBuild 'plugin.rsp'
    $PluginLines=New-Object System.Collections.Generic.List[string]
    foreach($o in @('/nologo','/target:library','/langversion:latest','/optimize+','/debug:pdbonly','/platform:anycpu','/nostdlib+')){$PluginLines.Add($o)}
    $PluginLines.Add(('/out:"{0}"' -f $PluginOut))
    $PluginLines.Add(('/pdb:"{0}"' -f $PluginPdb))
    foreach($n in @('mscorlib.dll','System.dll','System.Core.dll','System.Data.dll','System.Data.DataSetExtensions.dll','System.Net.Http.dll','System.Xml.dll','System.Xml.Linq.dll','Microsoft.CSharp.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll','WindowsBase.dll')){
        $r=Join-Path $refRoot $n
        if(-not(Test-Path $r)){throw('Falta referencia Plugin: '+$r)}
        $PluginLines.Add(('/reference:"{0}"' -f $r))
    }
    foreach($n in @('SimHub.Plugins.dll','GameReaderCommon.dll','iRacingSDK.dll')){
        $r=Join-Path $SimHub $n
        if(-not(Test-Path $r)){throw('Falta DLL SimHub: '+$r)}
        $PluginLines.Add(('/reference:"{0}"' -f $r))
    }
    $PluginLines.Add(('/reference:"{0}"' -f $CoreOut))
    if (Test-Path $WorkIcon) {
        $PluginLines.Add(('/resource:"{0}",Fulcrum.Plugin.Resources.FS_TRANSPARENT_PREVIEW.png' -f $WorkIcon))
    }
    Get-ChildItem $WorkPluginSrc -Recurse -Filter '*.cs' | Sort-Object FullName | ForEach-Object {$PluginLines.Add(('"{0}"' -f $_.FullName))}
    [IO.File]::WriteAllLines($PluginRsp,$PluginLines.ToArray(),(New-Object Text.UTF8Encoding($false)))

    Write-Host ''
    Write-Host 'Compilando Fulcrum.Plugin v4.1.57...' -ForegroundColor Cyan
    & $csc ("@$PluginRsp")
    if($LASTEXITCODE -ne 0 -or -not(Test-Path $PluginOut)){throw 'Fallo la compilacion de Fulcrum.Plugin.'}

    Copy-Item -LiteralPath $CoreOut -Destination (Join-Path $Dist 'Fulcrum.Core.dll') -Force
    Copy-Item -LiteralPath $PluginOut -Destination (Join-Path $Dist 'Fulcrum.Plugin.dll') -Force
    if(Test-Path $CorePdb){Copy-Item -LiteralPath $CorePdb -Destination (Join-Path $Dist 'Fulcrum.Core.pdb') -Force}
    if(Test-Path $PluginPdb){Copy-Item -LiteralPath $PluginPdb -Destination (Join-Path $Dist 'Fulcrum.Plugin.pdb') -Force}

    $coreSha=(Get-FileHash -Algorithm SHA256 (Join-Path $Dist 'Fulcrum.Core.dll')).Hash.ToLowerInvariant()
    $pluginSha=(Get-FileHash -Algorithm SHA256 (Join-Path $Dist 'Fulcrum.Plugin.dll')).Hash.ToLowerInvariant()

    Write-Host ''
    Write-Host '========================================================' -ForegroundColor Green
    Write-Host ' FULCRUM v4.1.57 START GRID RECOVERY - BUILD OK' -ForegroundColor Green
    Write-Host '========================================================' -ForegroundColor Green
    Write-Host ('Core SHA256:   '+$coreSha)
    Write-Host ('Plugin SHA256: '+$pluginSha)
    Write-Host ''
    Write-Host 'Cierra SimHub y reemplaza AMBOS DLL:' -ForegroundColor Yellow
    Write-Host '  Fulcrum.Core.dll' -ForegroundColor Yellow
    Write-Host '  Fulcrum.Plugin.dll' -ForegroundColor Yellow
} finally {
    if($Work -and (Test-Path $Work)){Remove-Item -LiteralPath $Work -Recurse -Force -ErrorAction SilentlyContinue}
}
