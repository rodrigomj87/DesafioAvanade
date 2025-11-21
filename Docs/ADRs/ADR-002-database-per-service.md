# ADR-002: Database Per Service Pattern

**Status**: Aceito  
**Data**: 2025-11-19  
**Decisores**: Equipe de Arquitetura

## Contexto

Em uma arquitetura de microsserviços, precisamos decidir como os dados serão organizados e acessados.

## Decisão

Cada microsserviço terá seu próprio banco de dados (database per service pattern):

1. **Isolamento de Dados**
   - InventoryService: Banco próprio para produtos, estoque
   - SalesService: Banco próprio para pedidos, transações
   - AuthService: Banco próprio para usuários, credenciais

2. **Tecnologia**
   - SQL Server para todos os serviços (inicialmente)
   - Possibilidade de usar tecnologias diferentes por serviço no futuro

3. **Comunicação de Dados**
   - Eventos via RabbitMQ para sincronização eventual
   - APIs para consultas cross-service quando necessário
   - Sem acesso direto entre bancos de dados

## Consequências

### Positivas
- ✅ Independência completa entre serviços
- ✅ Melhor isolamento de falhas
- ✅ Flexibilidade para escolher diferentes tecnologias de BD
- ✅ Schemas podem evoluir independentemente

### Negativas
- ❌ Joins entre serviços não são possíveis
- ❌ Consistência eventual (não imediata)
- ❌ Complexidade em manter sincronização de dados
- ❌ Possibilidade de dados duplicados

## Mitigação

- Implementar eventos de domínio para propagação de mudanças
- Usar sagas para transações distribuídas quando necessário
- Implementar políticas de retry e compensação
- Monitoramento de inconsistências
