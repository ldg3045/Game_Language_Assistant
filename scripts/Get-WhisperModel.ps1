$ErrorActionPreference = 'Stop'
$modelDirectory = Join-Path $PSScriptRoot '../models'
$modelPath = Join-Path $modelDirectory 'ggml-base.bin'
$expectedHash = '60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe'
New-Item -ItemType Directory -Path $modelDirectory -Force | Out-Null
if ((Test-Path -LiteralPath $modelPath) -and (Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash -eq $expectedHash) {
    Write-Output 'Whisper base model is already verified.'
    exit 0
}
$partialPath = "$modelPath.partial"
Write-Output 'Downloading multilingual Whisper base (147,951,465 bytes)...'
Invoke-WebRequest -Uri 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin' -OutFile $partialPath
if ((Get-FileHash -LiteralPath $partialPath -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'Model checksum mismatch. The incomplete download was not installed. Run this script again.'
}
Move-Item -LiteralPath $partialPath -Destination $modelPath -Force
Write-Output 'Whisper base model downloaded and SHA-256 verified.'
