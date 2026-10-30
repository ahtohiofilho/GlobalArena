# GA-VS12 — Alinhamento geográfico do motor físico e mapa tático

## Decisão e causa
O VS11 usava o mundo V1 para decidir terra produtiva, movimentação de trabalhadores e produtividade, mas o Godot usava o terreno experimental 01C (35% terra) para pintar os mesmos IDs G(18,0). Assim um assentamento poderia aparecer no oceano.

A correção introduz uma **nova variante de regras do domínio físico tático**, sem substituir o WorldGen estratégico V1 na produção. A geografia estratégica V1 e seu hash V8 permanecem intactos.

## Contrato das novas partidas
- CanonicalTacticalPhysicalGeography gera a mesma composição 01C, semente, geometria e percentil que a visualização.
- O assentamento inicial ocupa um tile que a própria 01C considera terra trabalhável, sem gelo visível e com vizinho trabalhável.
- Trabalhadores só podem deslocar-se para terra trabalhável da 01C.
- Agricultura, madeira e pedra usam coeficientes determinísticos provisórios derivados de umidade, temperatura, relevo e bioma 01C. Agricultura rudimentar permanece possível em qualquer tile colonizável.
- Godot recebe o **mesmo objeto físico 01C da simulação**, não um segundo gerador independente.
- Malha, IDs, contornos, movimentação e produção seguem G(18,0) sem duplicações. O mundo estratégico V1 não foi alterado.

## Versão e compatibilidade
- Novas partidas usam **save formato 11**, hash físico 11, arquivo independente user://ga-vs12-geography-physical.json.
- Saves V9/V10 antigos preservam geografia LegacyV1, taxas, origem, hash e ritmos anteriores. Visualizações táticas dessas partidas passam a exibir o terreno V1 LEGACY em vez de utilizar indevidamente cores 01C.
- Carregamento prioritário: V11 → V10 → V9 → VS07. Arquivo novo corrompido falha fechado, sem fallback silencioso.
- Não existe migração automática de partidas V10 já desenvolvidas para novos terrenos: seria alteração retroativa das regras da simulação.

## Testes e limites
Gates: equivalência dos IDs físicos de todo o planeta, origem terrestre, vizinhos permitidos, coeficientes de produção positivos, save V11 com replay, V9/V10 sem mudança, CI em Windows/Linux, compilação Godot, inspeção visual na GPU Intel UHD.

Sem mercado, logística, cidades, territorialidade nova ou ativação do WorldGen V2 estratégico. A unificação também da geografia estratégica exigirá etapa e migração próprias. Não fazer merge na main; MKT-01 bloqueado.
