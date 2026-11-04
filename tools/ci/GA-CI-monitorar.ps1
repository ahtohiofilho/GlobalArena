# GLOBAL ARENA - monitor independente para fast vs checkpoint.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][int]$PrNumero,
    [Parameter(Mandatory=$true)][string]$Sha,
    [ValidateSet('fast','checkpoint')][string]$Modo='fast',
    [int]$MinimoCheckpoint=1,
    [int]$IntervaloSegundos=75,
    [int]$LimiteMinutos=180
)
$ErrorActionPreference='Stop'
$repo='ahtohiofilho/GlobalArena'
$headers=@{Accept='application/vnd.github+json';'User-Agent'='GA-CI-VS107';'X-GitHub-Api-Version'='2022-11-28'}
$token=if($env:GH_TOKEN){$env:GH_TOKEN}else{$env:GITHUB_TOKEN}
if($token){$headers.Authorization="Bearer $token"}
function GA-GET([string]$Uri){Invoke-RestMethod -Method Get -Uri $Uri -Headers $headers -TimeoutSec 45}
function GA-PR{
    $p=GA-GET "https://api.github.com/repos/$repo/pulls/$PrNumero"
    if($p.head.sha -ne $Sha){throw "HEAD alterado para $($p.head.sha), monitor fixo em $Sha"}
    if($p.state -ne 'open' -or $p.merged){throw "PR nao esta aberta e sem merge"}
    if($Modo -eq 'checkpoint' -and @($p.labels | Where-Object{$_.name -eq 'ga-ci-full'}).Count -eq 0){
        throw "PR sem etiqueta ga-ci-full: nao homologar checkpoint"
    }
    return $p
}
function GA-Runs{
    $all=@()
    for($page=1;$page -le 10;$page++){
        $result=GA-GET "https://api.github.com/repos/$repo/actions/runs?head_sha=$Sha&event=pull_request&per_page=100&page=$page"
        $all+=@($result.workflow_runs|Where-Object{$_.head_sha -eq $Sha -and $_.event -eq 'pull_request'})
        if(@($result.workflow_runs).Count -lt 100){break}
    }
    return @($all|Group-Object workflow_id|ForEach-Object{
        $_.Group|Sort-Object run_attempt,created_at -Descending|Select-Object -First 1
    })
}
function GA-NeedsRealGodot{
    for($page=1;$page -le 10;$page++){
        $files=@(GA-GET "https://api.github.com/repos/$repo/pulls/$PrNumero/files?per_page=100&page=$page")
        foreach($file in $files){
            if($file.filename -like 'tools/GlobalArena.Godot/*'){return $true}
        }
        if($files.Count -lt 100){break}
    }
    return $false
}
$null=GA-PR
$godotNeeded=GA-NeedsRealGodot
Write-Host "GLOBAL ARENA - PR #$PrNumero | MODO $Modo" -ForegroundColor Cyan
Write-Host "HEAD $Sha | E2E Godot exigido: $godotNeeded"
$begin=Get-Date
while($true){
    $runs=@(GA-Runs)
    $fast=@($runs|Where-Object{$_.name -eq 'GA-CI FAST Incremental Quality Gate'})
    $e2e=@($runs|Where-Object{$_.name -eq 'GA-QA-UI-E2E-001 Real Godot .NET Runtime'})
    $pass=@($runs|Where-Object{$_.status -eq 'completed' -and $_.conclusion -eq 'success'})
    $fail=@($runs|Where-Object{$_.status -eq 'completed' -and $_.conclusion -eq 'failure'})
    $skip=@($runs|Where-Object{$_.status -eq 'completed' -and $_.conclusion -eq 'skipped'})
    $active=@($runs|Where-Object{$_.status -ne 'completed'})
    Write-Host ('[{0}] Total {1} | SUCCESS {2} | SKIPPED {3} | FALHAS {4} | ATIVOS {5}' -f (Get-Date -Format 'HH:mm:ss'),$runs.Count,$pass.Count,$skip.Count,$fail.Count,$active.Count)
    if($fast.Count -ne 1){
        if($fast.Count -gt 1){throw 'FAST duplicado'}
        if((Get-Date) -gt $begin.AddMinutes($LimiteMinutos)){throw 'FAST nao apareceu antes do tempo limite'}
        Start-Sleep -Seconds $IntervaloSegundos
        continue
    }
    if($fast[0].status -eq 'completed' -and $fast[0].conclusion -ne 'success'){
        throw "FAST falhou: $($fast[0].html_url)"
    }
    if($godotNeeded -and $e2e.Count -eq 1 -and $e2e[0].status -eq 'completed' -and $e2e[0].conclusion -ne 'success'){
        throw "E2E Godot falhou: $($e2e[0].html_url)"
    }
    if($Modo -eq 'fast'){
        if($fast[0].conclusion -eq 'success' -and
            (-not $godotNeeded -or ($e2e.Count -eq 1 -and $e2e[0].conclusion -eq 'success'))){
            $null=GA-PR
            Write-Host 'VALIDACAO FAST CONCLUIDA (nao equivale ao checkpoint integral).' -ForegroundColor Green
            break
        }
    }else{
        $historical=@($runs|Where-Object{$_.name -like 'GA-VS*' -or $_.name -like 'GA-QA-VISUAL*' -or $_.name -like 'GA-UI QA*'})
        $notSuccess=@($historical|Where-Object{$_.status -ne 'completed' -or $_.conclusion -ne 'success'})
        if($fail.Count){foreach($x in $fail){Write-Host "FALHA: $($x.name) $($x.html_url)" -ForegroundColor Red};throw 'Checkpoint bloqueado.'}
        if($historical.Count -ge $MinimoCheckpoint -and $notSuccess.Count -eq 0 -and
            $fast[0].conclusion -eq 'success' -and
            (-not $godotNeeded -or ($e2e.Count -eq 1 -and $e2e[0].conclusion -eq 'success'))){
            $null=GA-PR
            Write-Host "CHECKPOINT APROVADO: $($historical.Count) workflows historicos success (sem contar SKIP)." -ForegroundColor Green
            break
        }
    }
    if((Get-Date) -gt $begin.AddMinutes($LimiteMinutos)){throw "Timeout sem validacao $Modo"}
    Start-Sleep -Seconds $IntervaloSegundos
}
