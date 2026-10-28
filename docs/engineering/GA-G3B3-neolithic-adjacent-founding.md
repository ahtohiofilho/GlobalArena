# GA — G3-B3: Fundação neolítica em célula terrestre adjacente não controlada

**Status:** draft de referência executável em C#; sem comando de jogador, persistência material autoritativa ou formato de hash novo.  
**Depende de:** G3-B2 (#55), G3-B1 (#54), G3-A (#53), G2 (#52).  
**Compatibilização pendente:** PR #50 propõe instalações no hash v9 e permanece independente; a integração autoritativa futura de materiais deverá ocorrer somente após conciliação explícita dessa versão.

## Objetivo de gameplay

Permitir demonstrar a fundação de um **novo ponto econômico neolítico**, em terra estratégica vizinha ainda não controlada, utilizando exclusivamente habitantes/trabalhadores transferidos de um ponto existente e materiais de madeira/pedra **já acumulados naquele ponto**.

Não exigir terminal comercial, cidade especial ou recurso metálico para esse estágio.

## Contratos adicionados

- `EconomicNeolithicFoundationRecipe`: versão declarada de catálogo e IDs de madeira/pedra **distintos**, ambas as quantidades estritamente positivas. A versão e os preços do recipe são fornecidos pelo chamador; até termos catálogo canônico aprovado, não atestam acesso tecnológico nem custo autorizado.
- `EconomicNeolithicSiteFoundingResolver.Propose(...)`: valida hash esperado do mundo, vínculo com mundo gerado, civilização, célula livre e ausência de outro ponto econômico na célula de destino; usa `EconomicConstructionMaterialDebitCandidateResolver` para propor desconto de **ambos os materiais** no ponto de origem; adiciona controle territorial em snapshot imutável e usa `EconomicAdjacentSiteRelocationResolver` para validar adjacency em **terra**, moradores, trabalhadores e alocações; retorna `ProposedWorld` e `ProposedReserves`.
- `EconomicNeolithicSiteFoundingResult`: estados propostos e hash do `WorldState` antes/depois.

**Conservação:** não existe ganho líquido de trabalhadores ou moradores. Não existem depósitos fabricados na nova célula, nem caminhos construídos. A expansão aumenta opções espaciais potenciais, mas não concede produção automaticamente.

## Segurança e fronteiras

- A operação é **tudo ou nada enquanto transformação pura**: qualquer erro lança exceção e não altera os inputs. Ainda não existe commit atômico do par `WorldState + EconomicMaterialReserveBook`; são dois valores separados.
- `WorldState` mantém seu hash v8 corrente, agora diferente por território/economia/população. **O livro de materiais NÃO entra nesse hash.**
- Embora o resultado proponha reivindicar território estratégico, não é uma autorização institucional de colonização, nem comando/evento do scheduler, nem prova de viagem ou ocupação de `PhysicalTacticalTileId`.
- O modelo não garante acesso físico real por rio visível nem custo de deslocamento: a verificação atual usa apenas **adjacência terrestre estratégica**. A regra futura de ponte dependerá da hidrologia visual/contrato de travessias.
- O recipe exige madeira e pedra mas o catálogo ainda não valida IDs e quantidades. No teste, `CommodityId(3)` é madeira e `CommodityId(2)` é pedra **somente por convenção de fixture**.
- Não existe no novo ponto uma instalação criada. `EconomicPoint` e alocações de workforce não criam automaticamente `EconomicProductionPotential`, depósitos ou fazendas.
- Um `EconomicMaterialReserveBook` externo pode ser reapresentado por um chamador. O hash esperado protege o mundo, **não prova a proveniência/autenticidade do livro**, nem evita replay coordenado sem persistência unificada. Portanto **não expor Propose como ação de gameplay**.

## Casos de teste

`GlobalArena.Tests/G3B3NeolithicSiteFoundingTests.cs` cobre:
1. origem com 10 residentes e 6 trabalhadores; após fundação 6+4 residentes e 3+3 trabalhadores, sem crescimento fictício;
2. madeira 5→3 e pedra 4→1 no livro candidato, preservando saldo e cursor de turno originais;
3. novo controle territorial somente em célula anteriormente livre;
4. recusa por falta de madeira ou pedra, controle territorial de outra civilização ou ponto já existente;
5. recusa de hash obsoleto e mundo diferente;
6. recusa de estoque disponível apenas em outro ponto;
7. receita com dois IDs iguais, quantidades nulas ou versão inválida;
8. recusa de população excedente.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3B3NeolithicSiteFoundingTests
```

Executar CI multiplataforma de kernel e hash; nenhum sucesso presumido antes do resultado.

## Gate de integração seguinte

1. Resolver a ordem de merge/reconciliação de PR #50 sem mutar a `main` sem autorização.
2. Definir posição fina do `EconomicPoint` e regras de travessia de rios visíveis.
3. Integrar reservas ao `WorldState` persistido e ao hash versionado, com processamento único do turno.
4. Integrar o catálogo de construção aprovado e uma ação de fundação real que debite madeira e pedra e crie controle + população + ponto econômico numa transição autoritativa.
5. Conciliar compras com entrega/consumo físico antes de creditar reservas de destino.
6. Introduzir a primeira instalação produtiva no novo ponto somente quando potencial geográfico, custo de material e construção forem validados.

**Sem merge automático.** G3-B3 é um avanço do caso de uso, com autoridade de jogo propositalmente bloqueada até o gate de persistência.
