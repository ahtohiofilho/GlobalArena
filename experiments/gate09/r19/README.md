# Global Arena — Gate 09 R19 | Consolidacao experimental

**Estado:** pronto para CI / homologacao, **NAO** integrado ao motor autoritativo.

## Escopo

A REV4 do Gate 09 R18 foi aprovada no Windows (.NET 10.0.402) para **23 cenarios comerciais e 270 razoes convertidas exatamente em IEEE-754**. O R19 coloca **o codigo-fonte exato aprovado** em arquivos versionaveis, usando **um unico helper compartilhado** por port e testemunho. Sua origem e o ZIP de evidencias `GA-Gate09-R18-REV4-Evidencias-20261112-160717.zip`.

O projeto `src/GA.Gate09.R19.CacheParity` reproduz o avaliador experimental F-072. `src/GA.Gate09.R19.RatioWitness` prova os bits; ambos usam `src/ExactRatioBinary64.cs` (sha 55489852...). O programa de benchmark calcula somente custo de conversao numerica, nunca o custo end-to-end do clearing.

- Sem alteracoes em `GlobalArena.Kernel`, `GlobalArena.Runtime`, `GlobalArena.Simulation`, F-084F, tesouros ou journal.
- O PR experimental deve continuar em **Draft**, sem merge automatico.
- Os 55 casos positivos historicos e 19 negativas de R15–R18 pertencem ao **relatorio de homologacao REV4**. A suite de CI isolada do R19 exercita **23 + 270**; nao declara que reexecutou os outros historicos.

## Execucao local / Windows

Requer .NET SDK 10 e Python 3.10+. Extraia a pasta R19 e rode, de qualquer diretorio:

```powershell
python "$env:USERPROFILE\Downloads\GA-Gate09-R19-Experimental\ci\run_r19.py" --benchmark
```

Pode executar `python tests/test_r19_contract.py` para os testes de integridade/referencia Python. O executor gera `GA-Gate09-R19-Evidencias-*.zip` na pasta Downloads, com logs, hashes e medicao Release se `--benchmark` foi pedido.

## CI

A workflow de exemplo `ga-gate09-r19-parity.yml` deve ser copiada para `.github/workflows/` no repositorio e usa runner Windows/Linux, .NET 10 e Python 3.12. Executa tests Python, verificador binario e benchmark Release. O benchmark e **observacional** e nunca e condicao automatica de promocao.

## Benchmark

- 270 razoes oficiais, 7 repeticoes e 1.500 loops por repeticao (405.000 conversoes por rodada e metodo).
- Mede mediana em milissegundos de `ExactRatioBinary64.Convert` e a via `decimal` anterior, com numeradores precomputados, Release .NET 10, sem serializacao por iteracao.
- A comparacao temporal **nao** implica equivalencia numerica do caminho legado e nao prova desempenho de simulacao.
- Nao ha numeros medidos ate a execucao C# do R19. Qualquer regressao deve ser discutida em revisao, sem escolher um teto artificial de performance.

## Pendencias para producao

F-059 financeira; replay do Gate 08 296 variaveis/48 caminhos; equilibrio endogeno em grafo maior; calibragem C+W e congestionamento; paridade direta com F-084F inteiro autoritativo; benchmark de clearing completo; integracao ao journal. A aprovacao R18/REV4 nao elimina essas dependencias.

## FIX1 — 12/11/2026

Correcao operacional: o executor agora cria os diretorios `json/` e `logs/` da pasta de evidencias **antes** de chamar os processos C# (incluindo o benchmark Release). Evita `DirectoryNotFoundException` na escrita de `actual-port.json`; nao altera o codigo C#, vetores, formulas economicas ou a suite numerica da REV4. Teste de regressao cobre a criacao idempotente desses diretorios.

Para instalar, extraia `GA_R19_FIX1.zip` em `Downloads` usando `Expand-Archive -Force`, preservando o mesmo nome de pasta `GA-Gate09-R19-Experimental`.
