param(
    [string]$Password,
    [int]$Iterations = 210000,
    [int]$SaltSizeBytes = 16,
    [int]$HashSizeBytes = 32
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function ConvertTo-PlainText {
    param(
        [Parameter(Mandatory = $true)]
        [Security.SecureString]$SecureString
    )

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)

    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

if ($Iterations -le 0) {
    throw "Iterations must be greater than 0."
}

if ($SaltSizeBytes -le 0) {
    throw "SaltSizeBytes must be greater than 0."
}

if ($HashSizeBytes -le 0) {
    throw "HashSizeBytes must be greater than 0."
}

if ([string]::IsNullOrEmpty($Password)) {
    $securePassword = Read-Host -Prompt "Admin password" -AsSecureString
    $Password = ConvertTo-PlainText -SecureString $securePassword
}

if ([string]::IsNullOrWhiteSpace($Password)) {
    throw "Password cannot be empty."
}

$salt = New-Object byte[] $SaltSizeBytes
[System.Security.Cryptography.RandomNumberGenerator]::Fill($salt)

$hash = [System.Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2(
    $Password,
    $salt,
    $Iterations,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256,
    $HashSizeBytes)

'PBKDF2$SHA256${0}${1}${2}' -f $Iterations, [Convert]::ToBase64String($salt), [Convert]::ToBase64String($hash)
