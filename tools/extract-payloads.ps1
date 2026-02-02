# Extract Payload property from JSON files and save as properly formatted JSON

Get-ChildItem -Path $PSScriptRoot -Filter "*.json" | ForEach-Object {
    $jsonFile = $_
    
    # Skip files that are already extracted
    if ($jsonFile.Name -like "*_extracted.json") {
        Write-Host "Skipping already extracted file: $($jsonFile.Name)" -ForegroundColor Yellow
        return
    }
    
    Write-Host "Processing: $($jsonFile.Name)" -ForegroundColor Cyan
    
    try {
        # Read and parse the JSON file
        $content = Get-Content -Path $jsonFile.FullName -Raw -Encoding UTF8
        $jsonObject = $content | ConvertFrom-Json
        
        # Check if Payload property exists
        if ($null -eq $jsonObject.Payload) {
            Write-Host "  No Payload property found, skipping." -ForegroundColor Yellow
            return
        }
        
        # Extract the Payload (it's a JSON string that needs to be parsed)
        $payloadString = $jsonObject.Payload
        
        # Parse the payload string into a JSON object
        $payloadObject = $payloadString | ConvertFrom-Json
        
        # Create output filename
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($jsonFile.Name)
        $outputFileName = "$baseName`_extracted.json"
        $outputPath = Join-Path -Path $jsonFile.DirectoryName -ChildPath $outputFileName
        
        # Convert to properly formatted JSON with indentation
        $formattedJson = $payloadObject | ConvertTo-Json -Depth 100
        
        # Save to file
        $formattedJson | Out-File -FilePath $outputPath -Encoding UTF8
        
        Write-Host "  Created: $outputFileName" -ForegroundColor Green
    }
    catch {
        Write-Host "  Error processing file: $_" -ForegroundColor Red
    }
}

Write-Host "`nExtraction complete!" -ForegroundColor Green
