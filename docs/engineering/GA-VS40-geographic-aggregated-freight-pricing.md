# GA-VS40 — Cotação econômica agregada por geografia e rede construída (laboratório)

**Data:** 31/10/2026. **Base:** GA-VS39 PR #145, 10/10 workflows aprovados. **Governança:** PR draft empilhado, não promover `main`.

## Compromissos de produto

A movimentação e a guerra permanecem simples para o usuário: selecionar unidade, escolher destino e emitir a ordem. A economia comercial é **agregada no processador**, não se cria uma unidade física para cada produto ou uma sequência manual de embarques. Uma única base geográfica (VS38) serve a ambos, mas **as políticas de custo são independentes**.

## Escopo da VS40

- `ExperimentalGeographicFreightItineraryPricing` implementa a interface econômica existente `IEconomicFineTransportItineraryPricing`. Requer um serviço **explicitamente oferecido**, ativos construídos **operacionais**, continuidade física real, tecnologia versionada e rede VS39 compatível.
- Distância física é a **corda unitária** quantizada do WorldGen 01C; subida é a soma de desníveis positivos **no sentido da viagem**; umidade média é ponderada por distância. Tudo vem do snapshot VS38, não dos **pontos de movimento de caminhada VS37**.
- Cada tecnologia usa coeficientes de **preço monetário por grama** fornecidos pelo chamador (sem tarifas globais, classes fixas de cavalo/trem, IDs hardcoded nem preço inferido de velocidade). Perfil de volume e regraset versionados explicitamente.
- Custo por grama para previsão de carga física `q` em microgramas:

```
C(q) = geographicBase / 1_000_000
     + serviceOverhead / (q + overheadSmoothing)
     + saturationWeight × q² / saturationScale²
```

A primeira parcela incorpora distância, subida e umidade com pesos monetários próprios. O segundo termo representa amortização **suavizada** do custo de oferta do serviço; o terceiro cresce progressivamente, **sem um ponto rígido de sobrecarga**. A fórmula é uma **hipótese de calibração**, não a solução econômica definitiva. Não define capacidade real, custo de combustível, transbordo, terminal ou tempo efetivo.

- A avaliação usa `BigInteger` e `FoodTransportCostFraction`, sem aproximação monetária em `double`. `q=0` é definido, `q` enorme continua admissível (saturação progressiva), `q<0` é rejeitado.
- A função é avaliada na previsão **ex-ante do ciclo econômico** pelo sistema existente `EconomicTurnFreightQuote`. O valor fica **congelado durante a liquidação daquele ciclo**; uma previsão futura pode gerar cotação diferente sem alterar a anterior.
- A VS40 não liga a função ao mercado em produção automaticamente, não constrói linhas/portos, não cria serviços nem autoriza transbordos. Se a ferrovia está ausente, a cotação ferroviária não é oferecida. Somente terra trabalhável; mar/rios/estreitos ficam na issue #140.

## Reutilização / invalidação

- Fatos geográficos VS38 são estáveis por mundo; infraestrutura VS39 tem `NetworkRevision` independente. Oferta e preço monetário são refeitos pelo **snapshot do ciclo econômico**. Nunca reutilizar tarifa de um ciclo anterior só porque a geografia é igual.
- Não há cache global nem presunção de ganho percentual; benchmark de múltiplos serviços e densidades maiores deve medir pré-cálculo versus buscas repetidas.
- A política econômica agrega os trechos de **um único serviço de mesma tecnologia**. Uma jornada multimodal precisa de interfaces explícitas de terminal, transbordo e formulação do preço do itinerário inteiro; nenhum terminal é inferido da mera coincidência de nós.

## Gates

1. Custos geográficos econômicos reproduzem exatamente o conjunto de cordas/desníveis/umidade física por percurso, sem usar pontos de caminhada.
2. Curva exata contínua, amortização e saturação monotonicamente crescente na parte de congestionamento, sem corte de capacidade, sem `float`.
3. Diferentes previsões geram cotações congeladas por turno e respectivas cobranças financeiras corretas.
4. Sem caminho por água ou sem infraestrutura, não há frete implícito; nenhum transbordo ou ativo é inventado.
5. Integração de ponta a ponta via `EconomicFineTransportServiceItineraryDiscovery` → `EconomicTransportServiceQuoteResolver`, sem mutar save/hash/eventos.
6. Regressões Linux/Windows, xUnit integral no Windows, Godot Mono Debug/Release; PR só pode ser homologado no head testado.

## Próximos ciclos

- Comparar esta **hipótese laboratorial** com benchmarks econômicos reais, incluindo escolhas de rota por custo monetário da **viagem inteira**, saturação/reutilização de serviços e trade-offs de escala.
- Separar tarifa de serviços logísticos, custo fixo de transbordo e mecanismos de escolha entre modalidades, sem microcomandos.
- Não introduzir bancos/crédito ou fichas de guerra 1–5 nesta fase; não ativar movimentação escalar nem novos saves.
