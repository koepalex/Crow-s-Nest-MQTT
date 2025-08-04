# Requires -Version 7
# PowerShell script to send a PNG image to MQTT using MQTTnet 5.x (no external CLI tools)

param(
    [string]$SettingsPath = "$env:LOCALAPPDATA\CrowsNestMqtt\settings.json",
    [string]$ImagePath = "tests\TestData\test-image.png",
    [string]$VideoPath = "tests\TestData\test-video.mp4",
    [string]$JsonPath = "tests\TestData\test-struct.json"
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
Write-Host "Image sent to topic 'test/viewer/image' with content-type 'image/png'."

# --- Send video file ---
$videoBytes = [System.IO.File]::ReadAllBytes($VideoPath)
Write-Host "Loaded video file: $VideoPath ($($videoBytes.Length) bytes)"

$videoMsgBuilder = [MQTTnet.MqttApplicationMessageBuilder]::new()
$videoMsgBuilder = $videoMsgBuilder.WithTopic("test/viewer/video").WithPayload($videoBytes)
$videoMsgBuilder = $videoMsgBuilder.WithContentType("video/mp4")
$videoMsgBuilder = $videoMsgBuilder.WithQualityOfServiceLevel([MQTTnet.Protocol.MqttQualityOfServiceLevel]::AtLeastOnce)
$videoMessage = $videoMsgBuilder.Build()

$null = $client.PublishAsync($videoMessage).GetAwaiter().GetResult()
Write-Host "Video sent to topic 'test/viewer/video' with content-type 'video/mp4'."

# --- Send JSON file ---

$jsonBytes = [System.IO.File]::ReadAllBytes($JsonPath)
Write-Host "Loaded JSON file: $JsonPath ($($jsonBytes.Length) bytes)"

$jsonMsgBuilder = [MQTTnet.MqttApplicationMessageBuilder]::new()
$jsonMsgBuilder = $jsonMsgBuilder.WithTopic("test/viewer/json").WithPayload($jsonBytes)
$jsonMsgBuilder = $jsonMsgBuilder.WithContentType("application/json")
$jsonMsgBuilder = $jsonMsgBuilder.WithQualityOfServiceLevel([MQTTnet.Protocol.MqttQualityOfServiceLevel]::AtLeastOnce)
$jsonMessage = $jsonMsgBuilder.Build()

$null = $client.PublishAsync($jsonMessage).GetAwaiter().GetResult()
Write-Host "JSON sent to topic 'test/viewer/json' with content-type 'application/json'."

# Disconnect
$opts = [MQTTnet.MqttClientDisconnectOptions]::new()
$null = $client.DisconnectAsync($opts).GetAwaiter().GetResult()