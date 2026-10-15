# cfdac-1020: hardened API dumper (follows the hardening rules of docs/sdd/.../tools/cecil.ps1)
# Resolves every Cecil type BEFORE any loop, fails fast, accumulates into a list, disposes the module.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Assembly,
    [Parameter(Mandatory = $true)][string]$OutFile
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$cecilPath = 'C:\Users\merlin\.nuget\packages\jotunnlib\2.30.2\build\Mono.Cecil.dll'
if (-not (Test-Path -LiteralPath $cecilPath)) { throw "Mono.Cecil not found at $cecilPath" }
if (-not (Test-Path -LiteralPath $Assembly)) { throw "Assembly not found at $Assembly" }

Add-Type -Path $cecilPath

# Resolve Cecil types once, before any loop (PowerShell can lose the type cache mid-session,
# and a failed type lookup inside a loop body is a NON-terminating error => error flood).
$typeDefType = [Mono.Cecil.TypeDefinition]
if ($null -eq $typeDefType) { throw 'Mono.Cecil.TypeDefinition did not resolve' }

$lines = New-Object System.Collections.Generic.List[string]

$asmDef = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Assembly)
try {
    $pending = New-Object System.Collections.Generic.Stack[Mono.Cecil.TypeDefinition]
    foreach ($topType in $asmDef.MainModule.Types) { $pending.Push($topType) }

    while ($pending.Count -gt 0) {
        $typeDef = $pending.Pop()
        foreach ($nested in $typeDef.NestedTypes) { $pending.Push($nested) }

        $base = ''
        if ($null -ne $typeDef.BaseType) { $base = $typeDef.BaseType.FullName }
        $lines.Add("TYPE|$($typeDef.FullName)|base=$base|$($typeDef.Attributes)")

        foreach ($methodDef in $typeDef.Methods) {
            $paramTypes = ($methodDef.Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ','
            $lines.Add("M|$($typeDef.FullName)::$($methodDef.Name)|($paramTypes)|$($methodDef.ReturnType.FullName)|$($methodDef.Attributes)")
        }
        foreach ($fieldDef in $typeDef.Fields) {
            $lines.Add("F|$($typeDef.FullName)::$($fieldDef.Name)|$($fieldDef.FieldType.FullName)|$($fieldDef.Attributes)")
        }
        foreach ($propDef in $typeDef.Properties) {
            $lines.Add("P|$($typeDef.FullName)::$($propDef.Name)|$($propDef.PropertyType.FullName)|$($propDef.Attributes)")
        }
    }
}
finally {
    if ($null -ne $asmDef) { $asmDef.Dispose() }
}

$sorted = $lines | Sort-Object -Unique
Set-Content -LiteralPath $OutFile -Value $sorted -Encoding UTF8
"$([IO.Path]::GetFileName($Assembly)) -> $([IO.Path]::GetFileName($OutFile)) : $($sorted.Count) member lines"
