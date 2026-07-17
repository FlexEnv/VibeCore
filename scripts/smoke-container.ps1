param(
    [string]$Image = "vibecore:smoke",
    [int]$Port = 18080
)

$ErrorActionPreference = "Stop"
$containerName = "vibecore-smoke-$PID"

try {
    docker build --tag $Image .
    if ($LASTEXITCODE -ne 0) {
        throw "Docker build failed."
    }

    docker run --detach --rm `
        --name $containerName `
        --publish "${Port}:8080" `
        --env "ConnectionStrings__DefaultConnection=Host=127.0.0.1;Database=vibecore;Username=vibecore;Password=smoke-only" `
        --env "DataProtection__PersistKeysToDatabase=false" `
        $Image
    if ($LASTEXITCODE -ne 0) {
        throw "Container failed to start."
    }

    $deadline = (Get-Date).AddSeconds(30)
    do {
        try {
            $response = Invoke-WebRequest "http://localhost:$Port/health/live" -UseBasicParsing
            if ($response.StatusCode -eq 200) {
                Write-Host "Container smoke test passed."
                exit 0
            }
        }
        catch {
            Start-Sleep -Seconds 1
        }
    } while ((Get-Date) -lt $deadline)

    docker logs $containerName
    throw "Container did not become healthy within 30 seconds."
}
finally {
    docker stop $containerName 2>$null | Out-Null
}
