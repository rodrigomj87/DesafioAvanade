# Ambiente Dev - Containers Locais

## Requisitos
- Docker Desktop ou engine compatível.

## Subindo serviços
```powershell
docker compose -f infra/dev/docker-compose.yml up -d
```
Serviços disponíveis:
- RabbitMQ `amqp://admin:admin123@localhost:5672` (console em http://localhost:15672)
- SQL Server `localhost,1433` (usuário `sa`, senha `YourStrong@Passw0rd`)

## Encerrando
```powershell
docker compose -f infra/dev/docker-compose.yml down
```

## Observações
- Ajuste senhas/usuários antes de provisionar ambientes compartilhados.
- Utilize scripts de migrations para preparar schemas após subir o SQL Server.
