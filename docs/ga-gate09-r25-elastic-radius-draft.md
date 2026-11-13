# Global Arena - Gate 09 R25: raio adaptativo por viabilidade e progresso

Data: 13/11/2026. Base R24 (PR #313); R20-R23 preservados.

## Racional

Os métodos trust_joint e trust_global do R24 tiveram desempenho ruim,
apesar de diminuir as oscilações imediatas de preços e cargas. No
cenário de 162 nós, houve violação forçada do raio em todos os 38
passos. Esse mecanismo merece experimento controlado, não promoção.

## Desenho

Avaliar cinco passos candidatos independentes por snapshot F-072,
conservando 100% das vendas e sem modificar as regras de economia.

Dois métodos:
1. Expansão por viabilidade: elevar o raio efetivo ao mínimo requerido
   por qualquer candidato real, caso nenhum caiba no raio anterior.
2. Expansão por oportunidade: além disso, se o conjunto elegível não
   melhora a razão conjunta dos gaps, permitir escolher proposta
   comprovadamente melhor entre os cinco candidatos já avaliados.
   Registrar a expansão; nunca tratá-la como aprovação de estabilidade.

Mérito numérico = máximo entre razão do gap agregado e razão do pior
grupo, antes e depois do passo. O raio controla mudança relativa de
preços, cargas e quantidades. O raio não é parâmetro econômico, nem
representa demanda, produção ou preferências de mercado.

Comparadores: decaimento R21, backtracking R21, trust_joint R24.
Mesma fixture, orçamento de passos e orçamento máximo de chamadas
reais do oráculo. Fronteiras fixas de custo: 25/49/97/193/385.

## Critérios

Requerer integridade R20 16/16, paridade R13/R14 nos checkpoints,
replay das referências, conservação monetária e venda integral.
A observação numérica de estabilidade só ocorre quando gap global,
gap do pior produtor, mudanças de preço, cargas e quantidades são
todos <= 1e-4 em três atualizações consecutivas.

Ganhos isolados no gap global não justificam promoção; comparar também
pior grupo e custo em avaliações de oráculo. F-059, prova de equilíbrio,
motor C# integrado, WorldState, UI e produção permanecem fora do escopo.

Governança: PR R25 Draft empilhado sobre R24, sem merge e sem alterar
as bases anteriores.
