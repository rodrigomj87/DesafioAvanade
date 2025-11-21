# Script para configurar RabbitMQ com exchange, queues e bindings
# ADR-001: Mensageria assíncrona via RabbitMQ

param(
    [string]$RabbitMqHost = "localhost",
    [int]$RabbitMqPort = 15672,
    [string]$RabbitMqUser = "admin",
    [string]$RabbitMqPass = "admin123",
    [string]$RabbitMqVHost = "/"
)

$ErrorActionPreference = "Stop"

$BaseUrl = "http://${RabbitMqHost}:${RabbitMqPort}/api"
$VHostEncoded = [System.Web.HttpUtility]::UrlEncode($RabbitMqVHost)
$Credentials = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${RabbitMqUser}:${RabbitMqPass}"))
$Headers = @{
    "Authorization" = "Basic $Credentials"
    "Content-Type" = "application/json"
}

Write-Host "🔧 Configurando RabbitMQ..." -ForegroundColor Cyan
Write-Host "   Host: ${RabbitMqHost}:${RabbitMqPort}"
Write-Host "   VHost: ${RabbitMqVHost}"

# Aguarda RabbitMQ estar pronto
Write-Host "⏳ Aguardando RabbitMQ ficar disponível..." -ForegroundColor Yellow
$MaxRetries = 30
$RetryCount = 0
while ($RetryCount -lt $MaxRetries) {
    try {
        $Response = Invoke-WebRequest -Uri "$BaseUrl/overview" -Headers $Headers -Method Get -UseBasicParsing -ErrorAction Stop
        if ($Response.StatusCode -eq 200) {
            Write-Host "✅ RabbitMQ está pronto!" -ForegroundColor Green
            break
        }
    }
    catch {
        Write-Host "   RabbitMQ não está pronto, aguardando 2s... (tentativa $($RetryCount + 1)/$MaxRetries)" -ForegroundColor Gray
        Start-Sleep -Seconds 2
        $RetryCount++
    }
}

if ($RetryCount -eq $MaxRetries) {
    Write-Host "❌ Timeout: RabbitMQ não ficou disponível após ${MaxRetries} tentativas" -ForegroundColor Red
    exit 1
}

# Cria exchange sales.events (type: topic, durable)
Write-Host "`n📤 Criando exchange 'sales.events' (type: topic)..." -ForegroundColor Cyan
$ExchangeBody = @{
    type = "topic"
    durable = $true
    auto_delete = $false
    internal = $false
    arguments = @{}
} | ConvertTo-Json

try {
    Invoke-WebRequest -Uri "$BaseUrl/exchanges/$VHostEncoded/sales.events" -Headers $Headers -Method Put -Body $ExchangeBody -UseBasicParsing | Out-Null
    Write-Host "   ✅ Exchange 'sales.events' criado" -ForegroundColor Green
}
catch {
    Write-Host "   ⚠️  Exchange 'sales.events' já existe ou erro: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Cria queue inventory.order-confirmed
Write-Host "`n📥 Criando queue 'inventory.order-confirmed'..." -ForegroundColor Cyan
$QueueBody = @{
    durable = $true
    auto_delete = $false
    arguments = @{
        "x-dead-letter-exchange" = "sales.events"
        "x-dead-letter-routing-key" = "order.confirmed.dlq"
        "x-message-ttl" = 86400000
    }
} | ConvertTo-Json

try {
    Invoke-WebRequest -Uri "$BaseUrl/queues/$VHostEncoded/inventory.order-confirmed" -Headers $Headers -Method Put -Body $QueueBody -UseBasicParsing | Out-Null
    Write-Host "   ✅ Queue 'inventory.order-confirmed' criada" -ForegroundColor Green
}
catch {
    Write-Host "   ⚠️  Queue 'inventory.order-confirmed' já existe ou erro: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Cria DLQ (Dead Letter Queue)
Write-Host "`n☠️  Criando DLQ 'inventory.order-confirmed.dlq'..." -ForegroundColor Cyan
$DlqBody = @{
    durable = $true
    auto_delete = $false
    arguments = @{}
} | ConvertTo-Json

try {
    Invoke-WebRequest -Uri "$BaseUrl/queues/$VHostEncoded/inventory.order-confirmed.dlq" -Headers $Headers -Method Put -Body $DlqBody -UseBasicParsing | Out-Null
    Write-Host "   ✅ DLQ 'inventory.order-confirmed.dlq' criada" -ForegroundColor Green
}
catch {
    Write-Host "   ⚠️  DLQ 'inventory.order-confirmed.dlq' já existe ou erro: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Binding: sales.events -> inventory.order-confirmed (routing key: order.confirmed)
Write-Host "`n🔗 Criando binding: sales.events -> inventory.order-confirmed (routing key: order.confirmed)..." -ForegroundColor Cyan
$BindingBody = @{
    routing_key = "order.confirmed"
    arguments = @{}
} | ConvertTo-Json

try {
    Invoke-WebRequest -Uri "$BaseUrl/bindings/$VHostEncoded/e/sales.events/q/inventory.order-confirmed" -Headers $Headers -Method Post -Body $BindingBody -UseBasicParsing | Out-Null
    Write-Host "   ✅ Binding criado" -ForegroundColor Green
}
catch {
    Write-Host "   ⚠️  Binding já existe ou erro: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Binding: sales.events -> DLQ (routing key: order.confirmed.dlq)
Write-Host "`n🔗 Criando binding: sales.events -> inventory.order-confirmed.dlq (routing key: order.confirmed.dlq)..." -ForegroundColor Cyan
$DlqBindingBody = @{
    routing_key = "order.confirmed.dlq"
    arguments = @{}
} | ConvertTo-Json

try {
    Invoke-WebRequest -Uri "$BaseUrl/bindings/$VHostEncoded/e/sales.events/q/inventory.order-confirmed.dlq" -Headers $Headers -Method Post -Body $DlqBindingBody -UseBasicParsing | Out-Null
    Write-Host "   ✅ Binding DLQ criado" -ForegroundColor Green
}
catch {
    Write-Host "   ⚠️  Binding DLQ já existe ou erro: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host "`n🎉 Configuração do RabbitMQ concluída com sucesso!" -ForegroundColor Green
Write-Host "`n📊 Resumo:" -ForegroundColor Cyan
Write-Host "   Exchange: sales.events (type: topic, durable: true)"
Write-Host "   Queue: inventory.order-confirmed (ttl: 24h, dlx: sales.events)"
Write-Host "   DLQ: inventory.order-confirmed.dlq"
Write-Host "   Binding 1: sales.events -> inventory.order-confirmed (routing key: order.confirmed)"
Write-Host "   Binding 2: sales.events -> inventory.order-confirmed.dlq (routing key: order.confirmed.dlq)"
Write-Host "`n🌐 Management UI: http://${RabbitMqHost}:${RabbitMqPort}" -ForegroundColor Cyan
Write-Host "   Usuário: ${RabbitMqUser}"
