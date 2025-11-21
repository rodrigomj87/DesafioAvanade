# Script para gerar chave RSA fixa para desenvolvimento
# Esta chave será compartilhada entre Auth Service e serviços downstream

Add-Type -AssemblyName System.Security

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
      "kid": "auth-stub",
      "n": "$modulus",
      "e": "$exponent"
    }
  ]
}
"@

Write-Host $jwks
Write-Host ""
Write-Host "Instruções:" -ForegroundColor Yellow
Write-Host "1. Copie a chave XML e adicione ao appsettings.Development.json do Auth Service em Auth:RsaKeyXml"
Write-Host "2. Copie o JWKS JSON e adicione ao appsettings.json do Inventory Service em Jwt:Jwks (como string escapada)"
Write-Host "3. Copie o JWKS JSON e adicione ao appsettings.json do Gateway em Jwt:Jwks (como string escapada)"

$rsa.Dispose()
