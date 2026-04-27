param(
    [string]$Root = ".",
    [string]$OutDir = "ai_export",
    [int]$MaxLinesPerChunk = 800,
    [int]$SummaryLines = 20
)

Write-Host "==== Unity 项目导出开始 ===="

# 清理输出目录
Remove-Item $OutDir -Recurse -Force -ErrorAction Ignore
New-Item -ItemType Directory -Path $OutDir | Out-Null

# 忽略目录规则
$ignorePattern = "\\.git\\|\\Library\\|\\Temp\\|\\Logs\\|\\obj\\|\\bin\\"

# 分类定义
$fileTypes = @{
    "cs"      = "*.cs"
    "shader"  = "*.shader"
    "vfx"     = "*.vfx"
    "prefab"  = "*.prefab"
    "scene"   = "*.unity"
    "mat"     = "*.mat"
    "anim"    = "*.anim"
    "controller" = "*.controller"
}

# ==============================
# 1. 生成结构索引
# ==============================
Write-Host "生成结构索引..."
$structureFile = "$OutDir\structure.txt"

Get-ChildItem -Path $Root -Recurse |
Where-Object { $_.FullName -notmatch $ignorePattern } |
ForEach-Object {
    $_.FullName.Replace((Resolve-Path $Root), "")
} | Set-Content $structureFile -Encoding UTF8

# ==============================
# 2. 分类导出 + 摘要
# ==============================
Write-Host "分类导出文件..."

foreach ($type in $fileTypes.Keys) {

    $outputFile = "$OutDir\$type.txt"
    Write-Host "处理类型: $type"

    Get-ChildItem -Path $Root -Recurse -File -Include $fileTypes[$type] |
    Where-Object {
        $_.FullName -notmatch $ignorePattern -and
        $_.Extension -ne ".meta"
    } |
    ForEach-Object {

        $filePath = $_.FullName

        Add-Content $outputFile "`n===== FILE: $filePath ====="
        Add-Content $outputFile "----- SUMMARY (first $SummaryLines lines) -----"

        # 摘要
        try {
            Get-Content $filePath -TotalCount $SummaryLines | Add-Content $outputFile
        } catch {
            Add-Content $outputFile "[读取失败]"
        }

        Add-Content $outputFile "----- FULL CONTENT -----"

        # 全文
        try {
            Get-Content $filePath | Add-Content $outputFile
        } catch {
            Add-Content $outputFile "[读取失败]"
        }
    }
}

# ==============================
# 3. 全量分块导出（AI输入用）
# ==============================
Write-Host "生成分块文件..."

$chunkDir = "$OutDir\chunks"
New-Item -ItemType Directory -Path $chunkDir | Out-Null

$chunkIndex = 0
$currentLines = 0
$currentFile = "$chunkDir\chunk_$chunkIndex.txt"

Get-ChildItem -Path $Root -Recurse -File |
Where-Object {
    $_.FullName -notmatch $ignorePattern -and
    $_.Extension -ne ".meta"
} |
ForEach-Object {

    $filePath = $_.FullName

    try {
        $content = Get-Content $filePath
    } catch {
        return
    }

    $block = "`n===== FILE: $filePath =====`n" + ($content -join "`n")
    $lineCount = ($block -split "`n").Count

    if ($currentLines + $lineCount -gt $MaxLinesPerChunk) {
        $chunkIndex++
        $currentFile = "$chunkDir\chunk_$chunkIndex.txt"
        $currentLines = 0
    }

    Add-Content $currentFile $block
    $currentLines += $lineCount
}

Write-Host "==== 导出完成 ===="
Write-Host "输出目录: $OutDir"