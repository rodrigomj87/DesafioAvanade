# ADR-003: Autenticação JWT com JWKS

**Status**: Aceito  
**Data**: 2025-11-19  
**Decisores**: Equipe de Arquitetura, Segurança

## Contexto

Precisamos de um mecanismo de autenticação stateless, seguro e escalável para proteger as APIs dos microsserviços via API Gateway.

## Decisão

Adotar JWT (JSON Web Tokens) com algoritmo RS256 e suporte a JWKS:

1. **Formato do Token**
   - JWT padrão (RFC 7519)
   - Algoritmo: RS256 (RSA com SHA-256)
   - Claims: issuer, audience, expiration, roles, custom claims

2. **Geração de Tokens**
   - Auth Service responsável por gerar tokens
   - Chave privada RSA armazenada de forma segura
   - Tokens assinados com a chave privada

3. **Validação de Tokens**
   - API Gateway valida tokens antes de rotear requisições
   - Validação usando chave pública via JWKS
   - Dois modos de operação:
     - **Inline**: Chave pública no appsettings.json (dev)
     - **Remote**: Busca JWKS do Auth Service (prod)

4. **JWKS Endpoint**
   - Auth Service expõe `/.well-known/jwks.json`
   - Formato padrão RFC 7517
   - Gateway pode cachear JWKS

5. **Validações**
   - Assinatura do token
   - Issuer (emissor)
   - Audience (audiência)
   - Expiration (expiração)
   - Clock skew de 2 minutos

## Consequências

### Positivas
- ✅ Stateless: Sem necessidade de armazenar sessões
- ✅ Escalável: Cada instância pode validar independentemente
- ✅ Padrão da indústria: JWT é amplamente suportado
- ✅ Seguro: RS256 com chaves assimétricas
- ✅ Flexível: Claims customizados para autorização
- ✅ JWKS permite rotação de chaves

### Negativas
- ❌ Tokens não podem ser revogados facilmente
- ❌ Payload visível (base64, não criptografado)
- ❌ Tamanho maior que tokens opacos

## Mitigação

- Tokens com tempo de vida curto (expiração)
- Não incluir informações sensíveis no payload
- Refresh tokens para renovação sem re-autenticação
- Implementar blacklist se revogação for crítica

## Alternativas Consideradas

1. **Session-based**: Mais simples, mas menos escalável e stateful
2. **OAuth 2.0 completo**: Mais robusto, mas overhead para o escopo atual
3. **API Keys**: Simples, mas menos seguro e sem autorização granular
