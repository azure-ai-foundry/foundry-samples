$ErrorActionPreference = "Stop"

$requiredProviders = @(
    "Microsoft.App",
    "Microsoft.ContainerService"
)

foreach ($provider in $requiredProviders) {
    $registrationState = az provider show `
        --namespace $provider `
        --query registrationState `
        --output tsv

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to query registration state for resource provider '$provider'."
    }

    if ($registrationState -eq "Registered") {
        Write-Host "Resource provider '$provider' is already registered."
        continue
    }

    Write-Host "Registering resource provider '$provider'..."
    az provider register --namespace $provider --wait

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to register resource provider '$provider'."
    }

    $registrationState = az provider show `
        --namespace $provider `
        --query registrationState `
        --output tsv

    if ($LASTEXITCODE -ne 0 -or $registrationState -ne "Registered") {
        throw "Resource provider '$provider' registration state is '$registrationState', expected 'Registered'."
    }
}
