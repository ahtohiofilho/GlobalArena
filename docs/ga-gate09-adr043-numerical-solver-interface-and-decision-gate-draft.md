# Global Arena — ADR-043: interface e gate de seleção do solucionador de complementaridade

**Referência normativa:** docs/ga-gate09-adr043-continuous-inverse-score-vi-architecture-draft.md.
**Natureza:** contrato de arquitetura/prova, NENHUMA nova família iterativa implementada.
**Estado:** D4 formulação selecionada; método numérico concreto ainda sujeito a decisão.

## Objeto de decisão

O R40 encerrou três tentativas de encontrar equilíbrio por regras
de atualização de fluxos. O ADR-042 tornou possível verificar um
ESTADO congelado. O ADR-043 demonstrou a EXISTÊNCIA de estado F-072
em um grafo finito com oferta, demanda e custos positivos, por meio
da desigualdade variacional contínua de custo recíproco H.

A próxima engenharia deve resolver uma VI/MCP com as restrições de
simplex e gerador de caminhos, **não** redescobrir inversões de
ranking nem repetir um quarto iterador de best-response.

## Contrato sugerido de interfaces (pseudocódigo; não executável)

Input:
- Snapshot econômico original imutável: rede, produtores, mercados,
  capacidades, c_free, propriedade, tarifas, origem, vendedor,
  mercadoria e oferta. Sem demanda endógena nova.
- Colunas de rotas atualmente financiadas ou caminhos válidos
  gerados por separação; todo grupo precisa de alguma rota.
- Orçamento explícito prévio de chamadas de oráculo F-072,
  varreduras de caminhos e colunas ativas.
- Semente inicial válida no simplex físico, com classificação
  de mercados estruturalmente alcançáveis.

Problem:
- X = produto de simplexes não negativos por produtor,
  sum_c x_gc=s_g.
- Q, B e custos de aresta estritamente conforme o R20 original.
- H_gc=(1+C_gc)*sqrt(Q_m/D_m), contínuo inclusive em Q_m=0.
- VI(H,X): H(x*)·(y−x*)>=0 para todo y em X.
- P=sqrt(D/Q) ORIGINAL só é calculado para Q>0; H na borda
  é ferramenta da formulação, NÃO cotação artificial.

Outer loop contract (algoritmo AINDA não escolhido):
1. Partir de conjunto restrito de colunas A_g que contenha todas
   as rotas financiadas e acesso físico por grupo.
2. Solicitar a um núcleo de complementaridade uma tentativa
   que satisfaça viabilidade de x, conservação por produtor
   e minimização do resíduo VI restrito.
3. Recriar Q, B e custos no vetor CANDIDATO, com pesos específicos
   por vendedor. NÃO reutilizar caminhos/prices de outro estado.
4. Separar globalmente: varredura de menor caminho R14 por
   (origem, vendedor), comparar todos os mercados compatíveis,
   inclusive rotas ainda não integrantes de A_g.
5. Se uma rota omitida reduzir o H mínimo além da tolerância
   numérica declarada, expandir A_g e continuar, sem perder
   produção atualmente alocada.
6. Se todos os mercados alcançáveis tiverem Q>0, rodar
   ADR-042 INDEPENDENTE. Só PASS de gap global e pior grupo
   F-072 <=1e-4 autoriza declarar **sucesso comercial estático**.
7. Se algum Q=0 no candidato, NÃO rodar R14 com preço infinito:
   usar apenas H matemático para o núcleo e retornar com
   diagnóstico de domínio; o candidato NÃO pode ser certificado.
8. Em qualquer estouro de orçamento, falha de separação,
   inconsistência financeira, fluxo inválido ou exceção:
   NÃO publicar saldo/produção e reportar motivo verificável.

Output:
- Candidato físico somente após validação de 100% da oferta,
  preços originais finitos, R13/R14 e certificação ADR-042.
- Status separados: certificado comercial estático,
  robustez dinâmica, conservação F084F, política F059,
  orçamento consumido, determinismo de replay e evidência.
- Auditoria de custos: cada chamada R14/R13, cada scan de
  menor caminho, cada geração/pruning de coluna, candidatos
  rejeitados, tentativas que visitaram Q=0, tempo/custo CPU.
- NUNCA retornar "equilíbrio único" ou "convergência global"
  a partir de uma única CI ou do teorema de existência.

## Critérios numéricos e de escolha do futuro núcleo

Aceitar um núcleo somente após o planejamento de:
- Solução de **complementaridade e viabilidade** no espaço
  de fluxo (não mera otimização de um mérito arbitrário).
- Tratamento de não suavidade da raiz em Q=0, empates
  de caminhos e múltiplas soluções, sem singularidade
  de preço no verificador final.
- Condições explícitas de parada baseadas no ADR-042 e
  separação global, não apenas norma do gradiente do
  problema restrito.
- Mecanismo de recuperação física após falha/estouro
  de orçamento: manter estado original sem mutação.
- Escalas pequenas (dois exemplos analíticos),
  42/162 nós, oferta/demanda heterogênea, K×0,20
  e tarifas dependentes do vendedor.
- Orçamento contado por avaliações REAIS em cada
  oráculo; controles R21/R26 originais apenas
  como benchmark, não como definição econômica.
- Critério de eficiência por custo e melhor gap F-072,
  sem prometer que a formulação assegure velocidade.

Técnicas como MCP de conjunto ativo, VI com geração
de colunas ou equações de NCP podem ser pesquisadas
**depois** da seleção de domínio; nenhum desses
métodos foi implementado neste incremento. O
teorema de existência não valida Newton simples,
descida por projeção, nem uma política específica
de alpha ou temperatura.

## Gate e limites finais

**Pode ser adotado agora:** VI por custo H no
domínio fechado X e oráculo de separação global
para representar o equilíbrio ORIGINAL F-072.
É uma mudança de FORMULAÇÃO, não de economia.

**Pendente para outra revisão:** escolha do
núcleo algorítmico e orçamento de execução
antes de qualquer PR com código de alocação.

**Não autorizado:** merge, alterações
C#/Godot, valores artificiais de Q/D,
ratificação financeira F059, crédito,
estoques, uma quarta família iterativa
disfarçada, nem reabertura dos diagnósticos.
