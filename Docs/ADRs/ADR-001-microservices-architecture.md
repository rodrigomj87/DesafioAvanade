# ADR-001: Arquitetura de Microsserviços

**Status**: Aceito  
**Data**: 2025-11-19  
**Decisores**: Equipe de Arquitetura

## Contexto

Precisamos decidir a arquitetura geral da plataforma de e-commerce para garantir escalabilidade, manutenibilidade e independência de deploy.

## Decisão

Adotaremos uma arquitetura baseada em microsserviços com os seguintes princípios:

1. **Separação por Domínio de Negócio**
   - Cada microsserviço é responsável por um domínio específico (Auth, Inventory, Sales)
   - Boundaries bem definidos entre contextos

2. **Arquitetura em Camadas** (para cada serviço)
   - **Api**: Controllers, DTOs, validação de entrada
   - **Application**: Casos de uso, orquestração
   - **Domain**: Entidades, lógica de negócio, interfaces
   - **Infrastructure**: Implementação de repositórios, acesso a dados

3. **Comunicação**
   - **Síncrona**: HTTP/REST via API Gateway
   - **Assíncrona**: RabbitMQ para eventos entre serviços

4. **Gateway Centralizado**
   - YARP como reverse proxy
   - Autenticação centralizada
   - Rate limiting
   - Observabilidade

## Consequências

### Positivas
- ✅ Escalabilidade independente de cada serviço
- ✅ Deploy independente
- ✅ Tecnologias podem variar por serviço (se necessário)
- ✅ Equipes podem trabalhar de forma autônoma
- ✅ Falhas isoladas (fault tolerance)

### Negativas
- ❌ Complexidade operacional aumentada
- ❌ Necessidade de infraestrutura de observabilidade robusta
- ❌ Desafios de transações distribuídas
- ❌ Latência adicional de rede

## Alternativas Consideradas

1. **Monolito Modular**: Mais simples operacionalmente, mas com menor flexibilidade de escala e deploy
2. **Serverless**: Menor overhead operacional, mas com menos controle e vendor lock-in
