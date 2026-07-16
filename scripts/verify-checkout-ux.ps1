$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$checkout = Get-Content -Raw (Join-Path $repoRoot 'Web\Pages\Payments\Checkout.cshtml')
$checkoutModel = Get-Content -Raw (Join-Path $repoRoot 'Web\Pages\Payments\Checkout.cshtml.cs')
$checkoutCss = Get-Content -Raw (Join-Path $repoRoot 'Web\Pages\Payments\Checkout.cshtml.css')
$packages = Get-Content -Raw (Join-Path $repoRoot 'Web\Pages\Packages\Index.cshtml')
$layout = Get-Content -Raw (Join-Path $repoRoot 'Web\Pages\Shared\_Layout.cshtml')
$siteCss = (Get-ChildItem (Join-Path $repoRoot 'Web\wwwroot\css') -Filter 'site*.css' |
    Sort-Object Name |
    ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"
$momoLogo = Get-Content -Raw (Join-Path $repoRoot 'Web\wwwroot\img\payments\momo.svg')

$checks = [ordered]@{
    'The MOMO logo renders the readable MOMO wordmark' = $momoLogo -match '>MOMO</text>'
    'The cart exposes both payment methods' = ([regex]::Matches($checkout, 'checkout-payment-option')).Count -ge 2
    'The cart can switch payment providers safely' = $checkoutModel -match 'OnPostSwitchProviderAsync'
    'The cart renders recipient name and email' = $checkout -match 'RecipientName' -and $checkout -match 'RecipientEmail'
    'The package page goes directly to checkout without an inline provider list' = $packages -notmatch 'data-payment-toggle' -and $packages -notmatch 'package-membership__payment-option'
    'Paid packages initialize a PayOS order so QR is immediately available' = $packages -match 'name="provider"\s+value="@PaymentProvider\.PayOS"'
    'Raw PayOS payloads are rendered as an in-app QR image' = $checkoutModel -match 'QRCodeGenerator\.GenerateQrCode' -and $checkout -match 'qrImageSource'
    'The checkout shows payment recipient account details' = $checkout -match 'PayeeAccountName' -and $checkout -match 'PayeeAccountNumber' -and $checkout -match 'PayeeBankBin'
    'The shared shell groups package and cart controls' = $layout -match 'rbl-topbar-commerce'
    'The grouped commerce controls occupy one topbar column' = $siteCss -match '(?s)\.rbl-topbar-commerce\s*\{[^}]*grid-column:\s*2'
    'Provider artwork has a non-growing flex basis' = $checkoutCss -match '(?s)\.checkout-payment-option\s*>\s*img\s*\{[^}]*flex:\s*0\s+0'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
foreach ($check in $checks.GetEnumerator()) {
    $status = if ($check.Value) { 'PASS' } else { 'FAIL' }
    Write-Host "[$status] $($check.Key)"
}

if ($failed.Count -gt 0) {
    throw "$($failed.Count) checkout UX regression check(s) failed."
}
