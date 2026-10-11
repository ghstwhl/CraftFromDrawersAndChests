<#
  Hardened Mono.Cecil probe — the ONLY sanctioned way to inspect assemblies in this project.

  WHY THIS EXISTS
  An ad-hoc probe that tests an enum type literal inside a per-instruction loop, e.g.

      if ($i.OpCode.OperandType -eq [Mono.Cecil.OperandType]::InlineMethod) { ... }

  fails with "Unable to find type [Mono.Cecil.OperandType]" when PowerShell's type cache goes stale
  (typically a re-run in a session that already loaded Mono.Cecil). PowerShell treats that as a
  NON-TERMINATING error, so the script does not stop — it emits a multi-line error record for every
  single instruction, tens of thousands of times. That looks exactly like a hang, eats 30-60 minutes,
  floods the terminal, and has cost subagents their entire session and their uncommitted work.

  This script cannot do that. It:
    * fails fast ($ErrorActionPreference = 'Stop') instead of continuing past a type error,
    * resolves every type it needs ONCE, before any loop,
    * accumulates results in a list and prints a bounded summary instead of writing inside hot loops,
    * disposes the module so it never locks a build output.

  USAGE
    # call sites of a method (who calls ConsumeResources?)
    powershell -NoProfile -ExecutionPolicy Bypass -File cecil.ps1 -Assembly <dll> -Method ConsumeResources -Callers

    # definitions (overloads + parameter types — needed to tell overloads apart)
    powershell -NoProfile -ExecutionPolicy Bypass -File cecil.ps1 -Assembly <dll> -Method HaveRequirements -Definitions

    # fields (e.g. confirm Player.m_placementStatus exists and what type it is)
    powershell -NoProfile -ExecutionPolicy Bypass -File cecil.ps1 -Assembly <dll> -Method m_placementStatus -Fields

  ALWAYS pass -Definitions as well as -Callers when a name has overloads: a call-site scan that
  matches on name alone cannot distinguish Player.HaveRequirements(Recipe,...) from
  Player.HaveRequirements(Piece, RequirementMode), and those two behave very differently.

  NOTE ON NAMING: PowerShell variable names are CASE-INSENSITIVE, so a local named $definitions is
  the SAME variable as the [switch]$Definitions parameter, and assigning a List to it throws
  "Cannot convert value ... to type SwitchParameter". Locals here are suffixed -Lines to avoid it.
#>
param(
    [Parameter(Mandatory = $true)][string]$Assembly,
    [Parameter(Mandatory = $true)][string]$Method,
    [switch]$Definitions,
    [switch]$Callers,
    [switch]$Fields,
    [int]$MaxLines = 60
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

if (-not $Definitions -and -not $Callers -and -not $Fields) { $Definitions = $true; $Callers = $true }

$cecilPath = 'C:\Users\merlin\.nuget\packages\jotunnlib\2.30.2\build\Mono.Cecil.dll'
if (-not (Test-Path -LiteralPath $cecilPath)) { throw "Mono.Cecil not found at $cecilPath" }
if (-not (Test-Path -LiteralPath $Assembly)) { throw "Target assembly not found at $Assembly" }

Add-Type -Path $cecilPath

# Resolve the types we need BEFORE any loop. This is the whole point of this script.
$methodRefType = [Mono.Cecil.MethodReference]
if ($null -eq $methodRefType) { throw 'Mono.Cecil.MethodReference did not resolve' }

$asmDef = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Assembly)

$definitionLines = New-Object System.Collections.Generic.List[string]
$callSiteLines = New-Object System.Collections.Generic.List[string]
$fieldLines = New-Object System.Collections.Generic.List[string]
$scannedMethods = 0
$scannedInstructions = 0

try {
    $pending = New-Object System.Collections.Generic.Stack[Mono.Cecil.TypeDefinition]
    foreach ($topType in $asmDef.MainModule.Types) { $pending.Push($topType) }

    while ($pending.Count -gt 0) {
        $typeDef = $pending.Pop()
        foreach ($nested in $typeDef.NestedTypes) { $pending.Push($nested) }

        if ($Fields) {
            foreach ($fieldDef in $typeDef.Fields) {
                if ($fieldDef.Name -eq $Method) {
                    $modifiers = if ($fieldDef.IsStatic) { 'static ' } else { '' }
                    # Visibility matters for the same reason it does for methods: a field that is only
                    # public in the PUBLICIZED reference must be read through AccessTools.Field, not
                    # directly. The project already reflects a vanilla field (Player.m_placementStatus),
                    # so fields are demonstrably not uniformly public.
                    $fieldAccess = if ($fieldDef.IsPublic) { 'public' }
                        elseif ($fieldDef.IsFamilyOrAssembly) { 'protected internal' }
                        elseif ($fieldDef.IsFamily) { 'protected' }
                        elseif ($fieldDef.IsAssembly) { 'internal' }
                        else { 'private' }
                    $fieldLines.Add("$fieldAccess $($typeDef.Name)::$($fieldDef.Name) : $($modifiers)$($fieldDef.FieldType.Name)")
                }
            }
        }

        foreach ($methodDef in $typeDef.Methods) {
            if ($methodDef.Name -eq $Method) {
                $paramTypes = ($methodDef.Parameters | ForEach-Object { $_.ParameterType.Name }) -join ', '
                # Visibility matters: this project may only call members that are PUBLIC in the
                # UNPUBLICIZED assembly directly. Non-public members must go through
                # AccessTools/reflection, because the publicized reference binds them at compile time
                # while the shipping assembly does not expose them - build and tests stay green and the
                # call fails or throws only at runtime.
                $access = if ($methodDef.IsPublic) { 'public' }
                    elseif ($methodDef.IsFamilyOrAssembly) { 'protected internal' }
                    elseif ($methodDef.IsFamily) { 'protected' }
                    elseif ($methodDef.IsAssembly) { 'internal' }
                    else { 'private' }
                $definitionLines.Add("$access $($typeDef.Name)::$($methodDef.Name)($paramTypes) : $($methodDef.ReturnType.Name)")
            }

            if (-not $Callers) { continue }
            if (-not $methodDef.HasBody) { continue }
            $scannedMethods++

            foreach ($ins in $methodDef.Body.Instructions) {
                $scannedInstructions++
                $operand = $ins.Operand
                if ($operand -is $methodRefType) {
                    if ($operand.Name -eq $Method) {
                        $operandParamTypes = ($operand.Parameters | ForEach-Object { $_.ParameterType.Name }) -join ', '
                        $callSiteLines.Add("$($typeDef.Name)::$($methodDef.Name) -> $($operand.DeclaringType.Name)::$($operand.Name)($operandParamTypes)")
                    }
                }
            }
        }
    }
}
finally {
    # ReadAssembly holds the file stream open; not disposing can lock a build output (MSB3026).
    if ($null -ne $asmDef) { $asmDef.Dispose() }
}

if ($Fields) {
    "==== fields named $Method ===="
    if ($fieldLines.Count -eq 0) { "  (none)" }
    else { $fieldLines | Sort-Object -Unique | Select-Object -First $MaxLines }
}

if ($Definitions) {
    "==== definitions of $Method ===="
    if ($definitionLines.Count -eq 0) { "  (none)" }
    else { $definitionLines | Sort-Object -Unique | Select-Object -First $MaxLines }
}

if ($Callers) {
    "==== call sites of $Method ===="
    if ($callSiteLines.Count -eq 0) { "  (none)" }
    else { $callSiteLines | Sort-Object -Unique | Select-Object -First $MaxLines }
    "==== scanned $scannedMethods methods / $scannedInstructions instructions ===="
}
