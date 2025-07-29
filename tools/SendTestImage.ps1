# Requires -Version 7
# PowerShell script to send a PNG image to MQTT using MQTTnet 5.x (no external CLI tools)

param(
    [string]$SettingsPath = "$env:LOCALAPPDATA\CrowsNestMqtt\settings.json",
    [string]$ImagePath = "tests\TestData\test-image.png"
)

# Ensure MQTTnet is available
$nuget = [System.IO.Path]::Combine($env:TEMP, "MQTTnet.5.0.1.1416.nupkg")
if (-not (Test-Path $nuget)) {
    Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/MQTTnet/5.0.1.1416" -OutFile $nuget
}
$extractPath = Join-Path $env:TEMP "MQTTnet_extracted"
if (-not (Test-Path $extractPath)) {
    Expand-Archive -Path $nuget -DestinationPath $extractPath -Force
}
$dllPath = Join-Path $extractPath "lib\net8.0\MQTTnet.dll"
Add-Type -Path $dllPath

# Read settings
$settings = Get-Content $SettingsPath | ConvertFrom-Json
$mqttHost = $settings.Hostname
$port = $settings.Port
$useTls = $settings.UseTls
$clientId = if ($settings.ClientId -and $settings.ClientId -ne "") { $settings.ClientId } else { "pwsh-mqtt-$(Get-Random)" }

# Read image as bytes
$imageBytes = [System.IO.File]::ReadAllBytes($ImagePath)
Write-Host "Loaded image file: $ImagePath ($($imageBytes.Length) bytes)"

# Build MQTT client options (MQTTnet 5.x API)
$optionsBuilder = [MQTTnet.MqttClientOptionsBuilder]::new()
$optionsBuilder = $optionsBuilder.WithTcpServer($mqttHost, [int]$port)
if ($useTls) { $optionsBuilder = $optionsBuilder.WithTls() }
$options = $optionsBuilder.Build()

# Create MQTT client
$factory = [MQTTnet.MqttClientFactory]::new()
$client = $factory.CreateMqttClient()

# Connect
$null = $client.ConnectAsync($options).GetAwaiter().GetResult()

# Prepare message with content-type and QoS 1
$msgBuilder = [MQTTnet.MqttApplicationMessageBuilder]::new()
$msgBuilder = $msgBuilder.WithTopic("test/viewer/image").WithPayload($imageBytes)
$msgBuilder = $msgBuilder.WithContentType("image/png")
$msgBuilder = $msgBuilder.WithQualityOfServiceLevel([MQTTnet.Protocol.MqttQualityOfServiceLevel]::AtLeastOnce)
$message = $msgBuilder.Build()

# Publish
$null = $client.PublishAsync($message).GetAwaiter().GetResult()

# Disconnect
$opts = [MQTTnet.MqttClientDisconnectOptions]::new()
$null = $client.DisconnectAsync($opts).GetAwaiter().GetResult()

Write-Host "Image sent to topic 'test/viewer/image' with content-type 'image/png'."
