#!/bin/bash
# Script para configurar RabbitMQ com exchange, queues e bindings
# ADR-001: Mensageria assíncrona via RabbitMQ

set -e

RABBITMQ_HOST="${RABBITMQ_HOST:-localhost}"
RABBITMQ_PORT="${RABBITMQ_PORT:-15672}"
RABBITMQ_USER="${RABBITMQ_USER:-admin}"
RABBITMQ_PASS="${RABBITMQ_PASS:-admin123}"
RABBITMQ_VHOST="${RABBITMQ_VHOST:-/}"

BASE_URL="http://${RABBITMQ_HOST}:${RABBITMQ_PORT}/api"

echo "🔧 Configurando RabbitMQ..."
echo "   Host: ${RABBITMQ_HOST}:${RABBITMQ_PORT}"
echo "   VHost: ${RABBITMQ_VHOST}"

# Aguarda RabbitMQ estar pronto
echo "⏳ Aguardando RabbitMQ ficar disponível..."
until curl -f -s -u "${RABBITMQ_USER}:${RABBITMQ_PASS}" "${BASE_URL}/overview" > /dev/null 2>&1; do
    echo "   RabbitMQ não está pronto, aguardando 2s..."
    sleep 2
done
echo "✅ RabbitMQ está pronto!"

# Cria exchange sales.events (type: topic, durable)
echo "📤 Criando exchange 'sales.events' (type: topic)..."
curl -u "${RABBITMQ_USER}:${RABBITMQ_PASS}" -X PUT \
    "${BASE_URL}/exchanges/%2F/sales.events" \
    -H "Content-Type: application/json" \
    -d '{
        "type": "topic",
        "durable": true,
        "auto_delete": false,
        "internal": false,
        "arguments": {}
    }'
echo ""

# Cria queue inventory.order-confirmed
echo "📥 Criando queue 'inventory.order-confirmed'..."
curl -u "${RABBITMQ_USER}:${RABBITMQ_PASS}" -X PUT \
    "${BASE_URL}/queues/%2F/inventory.order-confirmed" \
    -H "Content-Type: application/json" \
    -d '{
        "durable": true,
        "auto_delete": false,
        "arguments": {
            "x-dead-letter-exchange": "sales.events",
            "x-dead-letter-routing-key": "order.confirmed.dlq",
            "x-message-ttl": 86400000
        }
    }'
echo ""

# Cria DLQ (Dead Letter Queue)
echo "☠️  Criando DLQ 'inventory.order-confirmed.dlq'..."
curl -u "${RABBITMQ_USER}:${RABBITMQ_PASS}" -X PUT \
    "${BASE_URL}/queues/%2F/inventory.order-confirmed.dlq" \
    -H "Content-Type: application/json" \
    -d '{
        "durable": true,
        "auto_delete": false,
        "arguments": {}
    }'
echo ""

# Binding: sales.events -> inventory.order-confirmed (routing key: order.confirmed)
echo "🔗 Criando binding: sales.events -> inventory.order-confirmed (routing key: order.confirmed)..."
curl -u "${RABBITMQ_USER}:${RABBITMQ_PASS}" -X POST \
    "${BASE_URL}/bindings/%2F/e/sales.events/q/inventory.order-confirmed" \
    -H "Content-Type: application/json" \
    -d '{
        "routing_key": "order.confirmed",
        "arguments": {}
    }'
echo ""

# Binding: sales.events -> DLQ (routing key: order.confirmed.dlq)
echo "🔗 Criando binding: sales.events -> inventory.order-confirmed.dlq (routing key: order.confirmed.dlq)..."
curl -u "${RABBITMQ_USER}:${RABBITMQ_PASS}" -X POST \
    "${BASE_URL}/bindings/%2F/e/sales.events/q/inventory.order-confirmed.dlq" \
    -H "Content-Type: application/json" \
    -d '{
        "routing_key": "order.confirmed.dlq",
        "arguments": {}
    }'
echo ""

echo "🎉 Configuração do RabbitMQ concluída com sucesso!"
echo ""
echo "📊 Resumo:"
echo "   Exchange: sales.events (type: topic, durable: true)"
echo "   Queue: inventory.order-confirmed (ttl: 24h, dlx: sales.events)"
echo "   DLQ: inventory.order-confirmed.dlq"
echo "   Binding 1: sales.events -> inventory.order-confirmed (routing key: order.confirmed)"
echo "   Binding 2: sales.events -> inventory.order-confirmed.dlq (routing key: order.confirmed.dlq)"
echo ""
echo "🌐 Management UI: http://${RABBITMQ_HOST}:${RABBITMQ_PORT}"
echo "   Usuário: ${RABBITMQ_USER}"
