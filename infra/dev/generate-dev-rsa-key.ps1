# Script para gerar chave RSA para desenvolvimento com suporte a múltiplas chaves (key rotation)
# Permite especificar KeyId customizado para rotação de chaves

param(
    [Parameter(Mandatory=$false)]
    [string]$KeyId = "auth-$(Get-Date -Format 'yyyy-MM')",
    
    [Parameter(Mandatory=$false)]
    [switch]$AppendToExisting
)

Add-Type -AssemblyName System.Security

Write-Host "=== Gerando nova chave RSA 2048 bits ===" -ForegroundColor Magenta
Write-Host "KeyId: $KeyId" -ForegroundColor Yellow
Write-Host ""

$rsa = [System.Security.Cryptography.RSA]::Create(2048)
$xmlKey = $rsa.ToXmlString($true)

Write-Host "=== Chave RSA XML (privada - para Auth Service) ===" -ForegroundColor Green
Write-Host $xmlKey
Write-Host ""

$params = $rsa.ExportParameters($false)
$modulus = [Convert]::ToBase64String($params.Modulus)
$exponent = [Convert]::ToBase64String($params.Exponent)

Write-Host "=== JWKS JSON (pública - para serviços downstream) ===" -ForegroundColor Cyan
$jwks = @"
{
  "keys": [
    {
      "kty": "RSA",
      "use": "sig",
      "alg": "RS256",
      "kid": "$KeyId",
      "n": "$modulus",
      "e": "$exponent"
    }
  ]
}
"@

Write-Host $jwks
Write-Host ""

Write-Host "=== Configuração para appsettings.json (múltiplas chaves) ===" -ForegroundColor Yellow
$appSettingsEntry = @"
{
  "KeyId": "$KeyId",
  "RsaKeyXml": "$($xmlKey -replace '"', '\"')",
  "IsPrimary": true
}
"@

Write-Host $appSettingsEntry
Write-Host ""

Write-Host "Instruções:" -ForegroundColor Yellow
if ($AppendToExisting) {
    Write-Host "Modo: ROTAÇÃO DE CHAVE (adicionar à chave existente)" -ForegroundColor Cyan
    Write-Host "1. Adicione o objeto acima ao array Auth:RsaKeys no appsettings.Development.json do Auth Service"
    Write-Host "2. Defina IsPrimary=true para a NOVA chave e IsPrimary=false para a chave ANTIGA"
    Write-Host "3. O JWKS (/.well-known/jwks.json) retornará AMBAS as chaves automaticamente"
    Write-Host "4. Após 24h (graceful period), remova a chave antiga do array"
} else {
    Write-Host "Modo: PRIMEIRA CHAVE (substituir configuração existente)" -ForegroundColor Green
    Write-Host "1. Copie o objeto acima e configure Auth:RsaKeys como array no appsettings.Development.json:"
    Write-Host '   "Auth": { "RsaKeys": [ <objeto-acima> ] }'
    Write-Host "2. Copie o JWKS JSON e adicione ao appsettings.json do Inventory/Gateway em Jwt:Jwks (se necessário)"
}

Write-Host ""
Write-Host "Exemplo de rotação de chave:" -ForegroundColor Magenta
Write-Host "  .\generate-dev-rsa-key.ps1 -KeyId 'auth-2024-12' -AppendToExisting"
Write-Host ""
Write-Host "Documentação completa: Docs/runbooks/key-rotation.md" -ForegroundColor Gray

$rsa.Dispose()
