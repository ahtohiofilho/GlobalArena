# GA-ECON-MARKET-DEFAULT-01F — laboratório de oferta automática no Godot

**Data:** 2026-11-06  
**Base:** GA-ECON-MARKET-DEFAULT-01E / PR #285, HEAD `bef2feba729c2e74996f00447e4a9705f749dcd2`.  
**Status:** integração de QA no Godot, somente por ação explícita; não promovida como sistema comercial jogável. PR Draft, sem merge.

## Objetivo

Homologar a orquestração do ciclo 01E através de sinais reais Godot (iniciar → avançar → salvar → reabrir) sem contaminar a partida principal e preservando a diretriz de UI mapa-primeiro. A ferramenta permanece em **FERRAMENTAS TÉCNICAS (QA)** → **DETALHES E FERRAMENTAS DE LABORATÓRIO**, ambos recolhidos inicialmente. Nenhum item ocupa por padrão o viewport ou o painel tático essencial.

## Implementação

`Main.AutomaticOfferSandbox.cs` introduz os controles `GAQAOfferSandboxStart`, `GAQAOfferSandboxTurn`, `GAQAOfferSandboxSave`, `GAQAOfferSandboxLoad` e `GAQAOfferSandboxStatus`. Eles não interceptam nenhum controle jogável de avanço, venda, movimento ou save.

1. **Iniciar:** exige que a sessão principal esteja no turno zero, sem eventos e sem ofertas prévias. `CanonicalAutomaticMarketOfferTimeline.StartOptIn(_canonicalPhysicalLab)` cria clone canônico exato em memória. A sessão real permanece idêntica.
2. **Avançar:** somente a timeline opt-in avança. Se o turno fecha um ciclo, o módulo 01D bloqueia em ofertas todas as mercadorias físicas livres na cópia e respeita retenções. O HUD experimental mostra apenas eventos de oferta, turno e ciclos do laboratório; nega explicitamente vendas e receitas.
3. **Salvar:** exporta checkpoint com identidade `GA-ECON-MARKET-DEFAULT-01E-CUSTODY-V1`, checksum SHA-256, save/hash verificados pelo restore. Escreve arquivo próprio com substituição via `.tmp`: `user://ga-econ-market-default-01f-experimental.json`. Nunca grava `user://ga-vs19b-born-civilization.json` nem o sidecar de câmera.
4. **Reabrir:** restaura só esse arquivo, exigindo integridade e identidade da modalidade. A timeline é substituída apenas depois da validação completa. Save ou partida jogável nunca são sobrescritos.
5. **Erro:** sem publicação de progresso parcial e com indicação no status. O experimento não muda `_canonicalSession`, `_canonicalPhysicalLab`, o mapa, atalhos, trabalhadores ou orçamento do jogo real.

## Gates

- `GaEconMarketDefault01fGodotIntegrationTests`: usa exatamente os modos de inicialização real Godot (`AdaptiveThirty`, `Canonical01C`, `LocalCashSettlement`, `TimedAdjacent`, `BornWithPopulation`); verifica fechamento no turno 1, ofertas em custódia na cópia, preservação bit a bit do save e hash principal e reabertura independente.
- `UiE2E.gd`: roda na cena real .NET, valida que os controles estão ocultos e inativos por padrão; abre ambas as seções QA; pressiona sinais reais de iniciar, avançar, salvar, avançar novamente e reabrir; confere que a partida jogável continua no turno 0, o arquivo experimental é separado, o ruleset explícito está persistido e os painéis são recolhidos novamente.
- `GA-CI FAST`, `GA-QA-UI-E2E-001` e Windows Debug build quando escopo acionado. Não contabilizar workflows ignorados como testes aprovados.

## Limite de promoção

**A integração está visível apenas em QA:** nenhuma alteração às opções comuns de salvar/reabrir partidas e nenhuma migração para o envelope canônico oficial. O checkpoint versionado é **experimental** e não um novo formato oficial de produção. Sua identidade não é um certificado criptográfico de autoria.

A oferta por padrão em laboratório ainda não é venda. Não há clearing comercial executado, recebedor de receita, compra abstrata liquidada, transporte físico entre tiles nem satisfação nutricional calculada. A quota de alimento herdada continua restrita ao motor histórico. A regra final de ciclo econômico vinculada à mobilidade permanece não promovida, conforme `GA-CANON-DIRECTIVES-01`.

**Próximos marcos bloqueantes:** fechar origem e destino físico de toda oferta, absorção comercial com receita marginal decrescente sem teto artificial, ledger de seis alimentos, qualidade nutricional não linear e promoção expressamente versionada ao motor e ao Godot jogável, com compatibilidade histórica e homologação visual.
