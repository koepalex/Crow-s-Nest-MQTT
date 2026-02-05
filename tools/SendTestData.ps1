# Requires -Version 7
# PowerShell script to send a PNG image to MQTT using MQTTnet 5.x (no external CLI tools)

param(
    [string]$SettingsPath = "$env:LOCALAPPDATA\CrowsNestMqtt\settings.json",
    [string]$ImagePath = "",
    [string]$VideoPath = "",
    [string]$JsonPath = "",
    [string]$BinaryPath = ""
)

# --- Resolve repo root and test data paths ---
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$defaultImagePath = Join-Path $repoRoot "tests\TestData\test-image.png"
$defaultVideoPath = Join-Path $repoRoot "tests\TestData\test-video.mp4"
$defaultJsonPath = Join-Path $repoRoot "tests\TestData\test-struct.json"
$defaultBinaryPath = Join-Path $repoRoot "tests\TestData\story.7z"

if (-not $ImagePath -or $ImagePath -eq "") { $ImagePath = $defaultImagePath }
if (-not $VideoPath -or $VideoPath -eq "") { $VideoPath = $defaultVideoPath }
if (-not $JsonPath -or $JsonPath -eq "") { $JsonPath = $defaultJsonPath }
if (-not $BinaryPath -or $BinaryPath -eq "") { $BinaryPath = $defaultBinaryPath }

# Ensure MQTTnet is available
$nuget = [System.IO.Path]::Combine($env:TEMP, "mqttnet.5.1.0.1559.nupkg")
if (-not (Test-Path $nuget)) {
    Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/MQTTnet/5.1.0.1559" -OutFile $nuget
}
$extractPath = Join-Path $env:TEMP "MQTTnet_extracted_5.1.0"
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
$clientId = "pwsh-mqtt-$(Get-Random)"



# Build MQTT client options (MQTTnet 5.x API)
$optionsBuilder = [MQTTnet.MqttClientOptionsBuilder]::new()
$optionsBuilder = $optionsBuilder.WithTcpServer($mqttHost, [int]$port)
if ($useTls) { $optionsBuilder = $optionsBuilder.WithTls() }
$options = $optionsBuilder.Build()

# Create MQTT client
$factory = [MQTTnet.MqttClientFactory]::new()
$client = $factory.CreateMqttClient()

# Connect
Write-Host "Connecting to $mqttHost : $port with client id $clientId"
$null = $client.ConnectAsync($options).GetAwaiter().GetResult()

# Read image as bytes
$imageBytes = [System.IO.File]::ReadAllBytes($ImagePath)
Write-Host "Loaded image file: $ImagePath ($($imageBytes.Length) bytes)"

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

# --- Send binary file ---

$binaryBytes = [System.IO.File]::ReadAllBytes($BinaryPath)
Write-Host "Loaded JSON file: $BinaryPath ($($binaryBytes.Length) bytes)"

$binaryMsgBuilder = [MQTTnet.MqttApplicationMessageBuilder]::new()
$binaryMsgBuilder = $jsonMsgBuilder.WithTopic("test/viewer/hex").WithPayload($binaryBytes)
$binaryMsgBuilder = $jsonMsgBuilder.WithContentType("application/octet-stream")
$binaryMsgBuilder = $jsonMsgBuilder.WithQualityOfServiceLevel([MQTTnet.Protocol.MqttQualityOfServiceLevel]::AtLeastOnce)
$binaryMessage = $binaryMsgBuilder.Build()

$null = $client.PublishAsync($binaryMessage).GetAwaiter().GetResult()
Write-Host "Binary sent to topic 'test/viewer/hex' with content-type 'application/octet-stream'."

# --- Send retained message for delete testing ---
$retainedPayload = @{
    message = "This is a retained message for testing delete functionality"
    timestamp = (Get-Date).ToString("o")
    test_data = @{
        topic = "test/retain"
        purpose = "Testing :deletetopic command"
        instructions = "Use :deletetopic test/retain to delete this retained message"
    }
} | ConvertTo-Json -Depth 3

$retainedBytes = [System.Text.Encoding]::UTF8.GetBytes($retainedPayload)
Write-Host "Prepared retained message: $($retainedBytes.Length) bytes"

$retainedMsgBuilder = [MQTTnet.MqttApplicationMessageBuilder]::new()
$retainedMsgBuilder = $retainedMsgBuilder.WithTopic("test/retain").WithPayload($retainedBytes)
$retainedMsgBuilder = $retainedMsgBuilder.WithContentType("application/json")
$retainedMsgBuilder = $retainedMsgBuilder.WithQualityOfServiceLevel([MQTTnet.Protocol.MqttQualityOfServiceLevel]::AtLeastOnce)
$retainedMsgBuilder = $retainedMsgBuilder.WithRetainFlag($true)  # This makes it a retained message
$retainedMessage = $retainedMsgBuilder.Build()

$null = $client.PublishAsync($retainedMessage).GetAwaiter().GetResult()
Write-Host "Retained message sent to topic 'test/retain' with content-type 'application/json'."

# --- MQTT 5 Request-Response Pattern with Pirate Content ---

# Subscribe to response topics first so we can receive responses
$responseTopic1 = "test/pirate/ship/response/treasure-map"
$responseTopic2 = "test/pirate/ship/response/crew-status"

Write-Host "Subscribing to response topics..."

$subscribeOptions1 = [MQTTnet.MqttClientSubscribeOptionsBuilder]::new()
$subscribeOptions1 = $subscribeOptions1.WithTopicFilter($responseTopic1, [MQTTnet.Protocol.MqttQualityOfServiceLevel]::AtLeastOnce)
$subscribeResult1 = $subscribeOptions1.Build()
$null = $client.SubscribeAsync($subscribeResult1).GetAwaiter().GetResult()

$subscribeOptions2 = [MQTTnet.MqttClientSubscribeOptionsBuilder]::new()
$subscribeOptions2 = $subscribeOptions2.WithTopicFilter($responseTopic2, [MQTTnet.Protocol.MqttQualityOfServiceLevel]::AtLeastOnce)
$subscribeResult2 = $subscribeOptions2.Build()
$null = $client.SubscribeAsync($subscribeResult2).GetAwaiter().GetResult()

Write-Host "Subscribed to response topics."

# Generate correlation data for requests
$correlationData1 = [System.Guid]::NewGuid().ToByteArray()
$correlationData2 = [System.Guid]::NewGuid().ToByteArray()

# --- Request Message 1: Treasure Map Request ---
$treasureRequestPayload = @{
    messageType = "treasure_map_request"
    shipName = "The Crow's Nest"
    captainName = "Captain Blackbeard McFeathers"
    requestId = [System.Guid]::NewGuid().ToString()
    timestamp = (Get-Date).ToString("o")
    requestDetails = @{
        treasureType = "buried_gold"
        searchArea = "Caribbean Sea"
        lastKnownLocation = @{
            latitude = "18.2208°N"
            longitude = "66.5901°W"
            island = "Dead Man's Cove"
        }
        urgency = "high"
        reason = "Mutinous crew needs convincing with shiny doubloons"
    }
    crew = @{
        totalMembers = 42
        experiencedTreasureHunters = 12
        parrots = 3
    }
} | ConvertTo-Json -Depth 4

$treasureRequestBytes = [System.Text.Encoding]::UTF8.GetBytes($treasureRequestPayload)
Write-Host "Prepared treasure map request: $($treasureRequestBytes.Length) bytes"

$treasureRequestBuilder = [MQTTnet.MqttApplicationMessageBuilder]::new()
$treasureRequestBuilder = $treasureRequestBuilder.WithTopic("test/pirate/ship/request/treasure-map").WithPayload($treasureRequestBytes)
$treasureRequestBuilder = $treasureRequestBuilder.WithContentType("application/json")
$treasureRequestBuilder = $treasureRequestBuilder.WithQualityOfServiceLevel([MQTTnet.Protocol.MqttQualityOfServiceLevel]::AtLeastOnce)
$treasureRequestBuilder = $treasureRequestBuilder.WithResponseTopic($responseTopic1)
$treasureRequestBuilder = $treasureRequestBuilder.WithCorrelationData($correlationData1)
$treasureRequestMessage = $treasureRequestBuilder.Build()

$null = $client.PublishAsync($treasureRequestMessage).GetAwaiter().GetResult()
Write-Host "Treasure map request sent to topic 'test/pirate/ship/request/treasure-map' with response topic '$responseTopic1'."

# --- Request Message 2: Crew Status Request ---
$crewStatusRequestPayload = @{
    messageType = "crew_status_request"
    shipName = "The Crow's Nest"
    captainName = "Captain Blackbeard McFeathers"
    requestId = [System.Guid]::NewGuid().ToString()
    timestamp = (Get-Date).ToString("o")
    statusInquiry = @{
        requestedInfo = @(
            "health_status",
            "morale_level",
            "rum_supplies",
            "cannon_readiness",
            "parrot_wellbeing"
        )
        urgency = "medium"
        reason = "Approaching enemy waters, need full crew assessment"
    }
    additionalNotes = "Suspicious activity spotted on the horizon - three ships flying the Jolly Roger"
} | ConvertTo-Json -Depth 3

$crewStatusRequestBytes = [System.Text.Encoding]::UTF8.GetBytes($crewStatusRequestPayload)
Write-Host "Prepared crew status request: $($crewStatusRequestBytes.Length) bytes"

$crewStatusRequestBuilder = [MQTTnet.MqttApplicationMessageBuilder]::new()
$crewStatusRequestBuilder = $crewStatusRequestBuilder.WithTopic("test/pirate/ship/request/crew-status").WithPayload($crewStatusRequestBytes)
$crewStatusRequestBuilder = $crewStatusRequestBuilder.WithContentType("application/json")
$crewStatusRequestBuilder = $crewStatusRequestBuilder.WithQualityOfServiceLevel([MQTTnet.Protocol.MqttQualityOfServiceLevel]::AtLeastOnce)
$crewStatusRequestBuilder = $crewStatusRequestBuilder.WithResponseTopic($responseTopic2)
$crewStatusRequestBuilder = $crewStatusRequestBuilder.WithCorrelationData($correlationData2)
$crewStatusRequestMessage = $crewStatusRequestBuilder.Build()

$null = $client.PublishAsync($crewStatusRequestMessage).GetAwaiter().GetResult()
Write-Host "Crew status request sent to topic 'test/pirate/ship/request/crew-status' with response topic '$responseTopic2'."

# --- Response Message for Treasure Map Request ---
# Simulate a response from the treasure map service
Start-Sleep -Milliseconds 500  # Small delay to simulate processing time

$treasureResponsePayload = @{
    messageType = "treasure_map_response"
    requestId = ($treasureRequestPayload | ConvertFrom-Json).requestId
    responseId = [System.Guid]::NewGuid().ToString()
    timestamp = (Get-Date).ToString("o")
    status = "success"
    treasureMap = @{
        mapId = "MAP-CARIBBEAN-001"
        authenticityVerified = $true
        treasureDetails = @{
            estimatedValue = "50,000 pieces of eight"
            treasureType = "Spanish gold doubloons and emeralds"
            containerType = "Iron-bound oak chest with skull motif"
            lastBuriedBy = "Captain Redbeard Rodriguez"
            buriedDate = "1692-03-15"
        }
        location = @{
            island = "Dead Man's Cove"
            coordinates = @{
                latitude = "18.2208°N"
                longitude = "66.5901°W"
            }
            landmarks = @(
                "Large coconut palm with carved 'X'",
                "Three rocks arranged in triangle",
                "20 paces north from old shipwreck"
            )
            depth = "6 feet below high tide mark"
        }
        warnings = @(
            "Beware of rival pirate crews in the area",
            "Local legends speak of cursed treasure",
            "Bring extra shovels - soil is rocky"
        )
        mapImageEncoded = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/59..." # Truncated for example
    }
    responseMetadata = @{
        serviceProvider = "Blackbeard's Treasure Maps & Navigation Co."
        serviceVersion = "v2.1.0"
        processingTimeMs = 487
        confidenceLevel = "high"
    }
} | ConvertTo-Json -Depth 5

$treasureResponseBytes = [System.Text.Encoding]::UTF8.GetBytes($treasureResponsePayload)
Write-Host "Prepared treasure map response: $($treasureResponseBytes.Length) bytes"

$treasureResponseBuilder = [MQTTnet.MqttApplicationMessageBuilder]::new()
$treasureResponseBuilder = $treasureResponseBuilder.WithTopic($responseTopic1).WithPayload($treasureResponseBytes)
$treasureResponseBuilder = $treasureResponseBuilder.WithContentType("application/json")
$treasureResponseBuilder = $treasureResponseBuilder.WithQualityOfServiceLevel([MQTTnet.Protocol.MqttQualityOfServiceLevel]::AtLeastOnce)
$treasureResponseBuilder = $treasureResponseBuilder.WithCorrelationData($correlationData1)  # Same correlation data as request
$treasureResponseMessage = $treasureResponseBuilder.Build()

$null = $client.PublishAsync($treasureResponseMessage).GetAwaiter().GetResult()
Write-Host "Treasure map response sent to topic '$responseTopic1' with matching correlation data."

# Display correlation information for verification
$correlationHex1 = [BitConverter]::ToString($correlationData1).Replace("-", "")
$correlationHex2 = [BitConverter]::ToString($correlationData2).Replace("-", "")

Write-Host ""
Write-Host "=== MQTT 5 Request-Response Summary ==="
Write-Host "Request 1 (Treasure Map):"
Write-Host "  Topic: test/pirate/ship/request/treasure-map"
Write-Host "  Response Topic: $responseTopic1"
Write-Host "  Correlation Data: $correlationHex1"
Write-Host "  Response Sent: YES"
Write-Host ""
Write-Host "Request 2 (Crew Status):"
Write-Host "  Topic: test/pirate/ship/request/crew-status"
Write-Host "  Response Topic: $responseTopic2"
Write-Host "  Correlation Data: $correlationHex2"
Write-Host "  Response Sent: NO (demonstrating request without response)"
Write-Host ""

# Disconnect
$opts = [MQTTnet.MqttClientDisconnectOptions]::new()
$null = $client.DisconnectAsync($opts).GetAwaiter().GetResult()
