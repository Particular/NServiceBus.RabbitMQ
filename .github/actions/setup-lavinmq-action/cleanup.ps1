param (
    [string]$LavinMQName
)

$resourceGroup = $Env:RESOURCE_GROUP_OVERRIDE ?? "GitHubActions-RG"
$runnerOs = $Env:RUNNER_OS ?? "Linux"

if ($runnerOs -eq "Linux") {
    Write-Output "Killing Docker container $LavinMQName"
    docker kill $LavinMQName

    Write-Output "Removing Docker container $LavinMQName"
    docker rm $LavinMQName
}
elseif ($runnerOs -eq "Windows") {
    Write-Output "Deleting Azure container $LavinMQName"
    az container delete --resource-group $resourceGroup --name $LavinMQName --yes | Out-Null
}
else {
    Write-Output "$runnerOs not supported"
    exit 1
}