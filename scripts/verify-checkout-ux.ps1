$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$checkout = Get-Content -Raw (Join-Path $repoRoot 'Web\Pages\Payments\Checkout.cshtml')
$checkoutModel = Get-Content -Raw (Join-Path $repoRoot 'Web\Pages\Payments\Checkout.cshtml.cs')
$checkoutCss = Get-Content -Raw (Join-Path $repoRoot 'Web\Pages\Payments\Checkout.cshtml.css')
$momoGateway = Get-Content -Raw (Join-Path $repoRoot 'BLL\Services\Billing\Gateways\MomoPaymentGateway.cs')
$packages = Get-Content -Raw (Join-Path $repoRoot 'Web\Pages\Packages\Index.cshtml')
$layout = Get-Content -Raw (Join-Path $repoRoot 'Web\Pages\Shared\_Layout.cshtml')
$siteJs = Get-Content -Raw (Join-Path $repoRoot 'Web\wwwroot\js\site.js')
$loadingCss = Get-Content -Raw (Join-Path $repoRoot 'Web\wwwroot\css\site-loading.css')
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
    'MoMo qrCodeUrl is encoded instead of treated as an image URL' = $momoGateway -match 'ReadString\(root, "qrCodeUrl"\)' -and $checkoutModel -notmatch 'IsSafeQrImageUrl'
    'MoMo payUrl is a QR fallback for production accounts' = $momoGateway -match 'string\.IsNullOrWhiteSpace\(qrPayload\) \? checkoutUrl : qrPayload' -and $checkout -match 'activePayment\.CheckoutUrl'
    'The checkout shows payment recipient account details' = $checkout -match 'PayeeAccountName' -and $checkout -match 'PayeeAccountNumber' -and $checkout -match 'PayeeBankBin'
    'The shared shell groups package and cart controls' = $layout -match 'rbl-topbar-commerce'
    'The grouped commerce controls occupy one topbar column' = $siteCss -match '(?s)\.rbl-topbar-commerce\s*\{[^}]*grid-column:\s*2'
    'Provider artwork has a non-growing flex basis' = $checkoutCss -match '(?s)\.checkout-payment-option\s*>\s*img\s*\{[^}]*flex:\s*0\s+0'
    'The shared layout provides a page skeleton' = $layout -match 'data-page-skeleton' -and $layout -match 'site-loading\.css'
    'Navigation and form submissions activate the page skeleton' = $siteJs -match 'function initPageSkeleton' -and $siteJs -match 'addEventListener\("submit"'
    'Skeleton motion respects reduced-motion preferences' = $loadingCss -match 'prefers-reduced-motion:\s*reduce' -and $loadingCss -match 'animation:\s*none'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
foreach ($check in $checks.GetEnumerator()) {
    $status = if ($check.Value) { 'PASS' } else { 'FAIL' }
    Write-Host "[$status] $($check.Key)"
}

if ($failed.Count -gt 0) {
    throw "$($failed.Count) checkout UX regression check(s) failed."
}
