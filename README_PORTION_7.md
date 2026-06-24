# Portion 7

This portion does not change source code. It creates a local inventory of the remaining `samples` projects before converting more projects to the simple `static Run()` mode.

Run from PowerShell:

```powershell
Set-Location "D:\projects\Polar.DB"

Expand-Archive -LiteralPath "C:\Users\LuxVe\Downloads\polardb_samples_inventory_portion7.zip" `
  -DestinationPath "D:\projects\Polar.DB" `
  -Force

powershell -NoProfile -ExecutionPolicy Bypass -File ".\inspect-samples-portion7.ps1"
```

Output file:

```text
D:\projects\Polar.DB\samples-inventory-portion7.txt
```
