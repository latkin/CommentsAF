param(
    [string] $DisqusXmlPath,
    [string] $AdminKey,
    [string] $AdminUrl = $env:CAF_ADMIN_URL
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

if(-not $AdminKey){
    $tmp = Read-Host -AsSecureString -Prompt "Enter admin API key"
    $adminKey = [pscredential]::new('dummy', $tmp).GetNetworkCredential().Password
}

if(-not $AdminUrl){
    Write-Error "Must specify URL"
}

$dsq = [xml] (Get-Content $DisqusXmlPath -Encoding UTF8)

$idTable = @{}

$dsq.disqus.thread | % {
    $ids = $_.id
    [pscustomobject]@{
        DsqId = $ids[0]
        PostId = $ids[1]
    }
} | % { 
    $idTable[$_.DsqId] = $_.PostId
}

$posts = $dsq.disqus.post |? isDeleted -eq 'false' | % {
    [pscustomobject]@{
        commentHtml = $_.message.'#cdata-section'
        name = $_.author.name
        createdAt = $_.createdAt
        ipAddress = $_.ipaddress
        postid = $idTable[$_.thread.id]
    }
}

$postCount = $posts.Count
$posts | select -First 10 | Out-String | Write-Host
Read-Host "10 example posts shown of $postCount total. If this looks right, press enter to continue. Otherwise press Ctrl+C" | Out-Null

$postJson = @($posts) | ConvertTo-Json
$postJsonBytes = [System.Text.Encoding]::UTF8.GetBytes($postJson)

Invoke-WebRequest -Uri "${AdminUrl}?code=$AdminKey" -Method Post -Body $postJsonBytes -ContentType 'application/json'