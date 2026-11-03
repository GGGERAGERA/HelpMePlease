$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$taskRoot=$PSScriptRoot
$taskManifest=Get-Content -LiteralPath (Join-Path $taskRoot 'manifest.json') -Raw | ConvertFrom-Json
foreach($taskDir in @('sprites','sources','prompts','previews')){[System.IO.Directory]::CreateDirectory((Join-Path $taskRoot $taskDir)) | Out-Null}
function Get-PixelBounds([System.Drawing.Bitmap]$Image,[int]$Threshold=1){
  $minX=$Image.Width;$minY=$Image.Height;$maxX=-1;$maxY=-1;$count=0
  for($y=0;$y -lt $Image.Height;$y++){for($x=0;$x -lt $Image.Width;$x++){
    if($Image.GetPixel($x,$y).A -ge $Threshold){$minX=[Math]::Min($minX,$x);$maxX=[Math]::Max($maxX,$x);$minY=[Math]::Min($minY,$y);$maxY=[Math]::Max($maxY,$y);$count++}
  }}
  if($count -eq 0){throw 'No visible pixels'}
  return [pscustomobject]@{X=$minX;Y=$minY;W=($maxX-$minX+1);H=($maxY-$minY+1);Count=$count}
}
function Save-Png([System.Drawing.Bitmap]$Image,[string]$Path){
  $buffer=[System.IO.MemoryStream]::new()
  $Image.Save($buffer,[System.Drawing.Imaging.ImageFormat]::Png)
  [System.IO.File]::WriteAllBytes($Path,$buffer.ToArray())
  $buffer.Dispose()
}
$taskReport=@()
foreach($taskResident in $taskManifest){
  $taskId=$taskResident.id
  $taskSourceCopy=Join-Path $taskRoot ('sources/'+$taskId+'_generated.png')
  Copy-Item -LiteralPath $taskResident.source -Destination $taskSourceCopy
  [System.IO.File]::WriteAllText((Join-Path $taskRoot ('prompts/'+$taskId+'.txt')),[string]$taskResident.prompt,[System.Text.UTF8Encoding]::new($false))
  $taskSource=[System.Drawing.Bitmap]::new($taskSourceCopy)
  $taskSourceBounds=Get-PixelBounds $taskSource 128
  if($taskSourceBounds.Count -eq $taskSource.Width*$taskSource.Height){throw "Source lacks transparent background: $taskId"}
  $taskPalette=@($taskResident.palette | ForEach-Object {[System.Drawing.ColorTranslator]::FromHtml('#'+$_)})
  if($taskPalette.Count -gt 16){throw "Too many palette colors: $taskId"}
  $taskSprite=[System.Drawing.Bitmap]::new(50,50,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  for($taskY=0;$taskY -lt 39;$taskY++){for($taskX=0;$taskX -lt 27;$taskX++){
    $taskSx=$taskSourceBounds.X+[Math]::Floor(($taskX+0.5)*$taskSourceBounds.W/27)
    $taskSy=$taskSourceBounds.Y+[Math]::Floor(($taskY+0.5)*$taskSourceBounds.H/39)
    $taskC=$taskSource.GetPixel($taskSx,$taskSy)
    if($taskC.A -lt 128){continue}
    $taskBest=$taskPalette[0];$taskBestD=[double]::PositiveInfinity
    foreach($taskP in $taskPalette){
      $taskDr=[int]$taskC.R-[int]$taskP.R;$taskDg=[int]$taskC.G-[int]$taskP.G;$taskDb=[int]$taskC.B-[int]$taskP.B
      $taskD=$taskDr*$taskDr+$taskDg*$taskDg+$taskDb*$taskDb
      if($taskD -lt $taskBestD){$taskBestD=$taskD;$taskBest=$taskP}
    }
    $taskSprite.SetPixel(12+$taskX,11+$taskY,$taskBest)
  }}
  $taskSampleBounds=Get-PixelBounds $taskSprite
  if($taskSampleBounds.X -ne 12 -or $taskSampleBounds.Y -ne 11 -or $taskSampleBounds.W -ne 27 -or $taskSampleBounds.H -ne 39){
    $taskNormalized=[System.Drawing.Bitmap]::new(50,50,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for($taskY=0;$taskY -lt 39;$taskY++){for($taskX=0;$taskX -lt 27;$taskX++){
      $taskSx=$taskSampleBounds.X+[Math]::Floor(($taskX+0.5)*$taskSampleBounds.W/27)
      $taskSy=$taskSampleBounds.Y+[Math]::Floor(($taskY+0.5)*$taskSampleBounds.H/39)
      $taskNormalized.SetPixel(12+$taskX,11+$taskY,$taskSprite.GetPixel($taskSx,$taskSy))
    }}
    $taskSprite.Dispose();$taskSprite=$taskNormalized
  }
  $taskMask=$taskSprite.Clone([System.Drawing.Rectangle]::new(0,0,50,50),[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $taskOutline=$taskPalette[0]
  for($taskY=0;$taskY -lt 50;$taskY++){for($taskX=0;$taskX -lt 50;$taskX++){
    if($taskMask.GetPixel($taskX,$taskY).A -eq 0){continue}
    $taskEdge=$taskX -eq 0 -or $taskX -eq 49 -or $taskY -eq 0 -or $taskY -eq 49
    if(-not $taskEdge){$taskEdge=$taskMask.GetPixel($taskX-1,$taskY).A -eq 0 -or $taskMask.GetPixel($taskX+1,$taskY).A -eq 0 -or $taskMask.GetPixel($taskX,$taskY-1).A -eq 0 -or $taskMask.GetPixel($taskX,$taskY+1).A -eq 0}
    if($taskEdge){$taskSprite.SetPixel($taskX,$taskY,$taskOutline)}
  }}
  $taskSpritePath=Join-Path $taskRoot ('sprites/'+$taskId+'_idle_50.png')
  Save-Png $taskSprite $taskSpritePath
  $taskColors=[System.Collections.Generic.HashSet[int]]::new()
  $taskAlpha=[System.Collections.Generic.HashSet[int]]::new()
  $taskOutlineFailures=0
  for($taskY=0;$taskY -lt 50;$taskY++){for($taskX=0;$taskX -lt 50;$taskX++){
    $taskC=$taskSprite.GetPixel($taskX,$taskY);[void]$taskAlpha.Add($taskC.A)
    if($taskC.A -eq 0){continue}
    [void]$taskColors.Add($taskC.ToArgb())
    $taskEdge=$taskX -eq 0 -or $taskX -eq 49 -or $taskY -eq 0 -or $taskY -eq 49
    if(-not $taskEdge){$taskEdge=$taskSprite.GetPixel($taskX-1,$taskY).A -eq 0 -or $taskSprite.GetPixel($taskX+1,$taskY).A -eq 0 -or $taskSprite.GetPixel($taskX,$taskY-1).A -eq 0 -or $taskSprite.GetPixel($taskX,$taskY+1).A -eq 0}
    if($taskEdge -and $taskC.ToArgb() -ne $taskOutline.ToArgb()){$taskOutlineFailures++}
  }}
  $taskBounds=Get-PixelBounds $taskSprite
  $taskEntry=[pscustomobject]@{Id=$taskId;Name=$taskResident.ru;Canvas=@(50,50);Figure=@($taskBounds.W,$taskBounds.H);Bounds=@($taskBounds.X,$taskBounds.Y,($taskBounds.X+$taskBounds.W-1),($taskBounds.Y+$taskBounds.H-1));VisibleColors=$taskColors.Count;Alpha=@($taskAlpha | Sort-Object);OutlineFailures=$taskOutlineFailures;SourceBounds=$taskSourceBounds;Path=$taskSpritePath}
  if($taskBounds.X -ne 12 -or $taskBounds.Y -ne 11 -or $taskBounds.W -ne 27 -or $taskBounds.H -ne 39 -or $taskColors.Count -gt 16 -or $taskAlpha.Count -ne 2 -or $taskOutlineFailures -ne 0){throw "Validation failed for $taskId"}
  $taskReport+=$taskEntry
  $taskPreview=[System.Drawing.Bitmap]::new(400,400,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $taskG=[System.Drawing.Graphics]::FromImage($taskPreview)
  $taskG.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
  $taskG.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
  $taskG.DrawImage($taskSprite,[System.Drawing.Rectangle]::new(0,0,400,400),0,0,50,50,[System.Drawing.GraphicsUnit]::Pixel)
  Save-Png $taskPreview (Join-Path $taskRoot ('previews/'+$taskId+'_8x.png'))
  $taskG.Dispose();$taskPreview.Dispose();$taskMask.Dispose();$taskSprite.Dispose();$taskSource.Dispose()
  Write-Output "$taskId OK: 50x50, 27x39, $($taskColors.Count) colors"
}
$taskReport | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskRoot 'validation.json') -Encoding UTF8
$taskAtlas=[System.Drawing.Bitmap]::new(250,100,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$taskAtlasG=[System.Drawing.Graphics]::FromImage($taskAtlas)
$taskSheet=[System.Drawing.Bitmap]::new(1320,704,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$taskG=[System.Drawing.Graphics]::FromImage($taskSheet)
$taskG.Clear([System.Drawing.ColorTranslator]::FromHtml('#727783'))
$taskG.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$taskG.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
$taskG.TextRenderingHint=[System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$taskFont=[System.Drawing.Font]::new('Segoe UI',18,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel)
$taskHeader=[System.Drawing.Font]::new('Segoe UI',24,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel)
$taskFormat=[System.Drawing.StringFormat]::new()
$taskFormat.Alignment=[System.Drawing.StringAlignment]::Center
$taskG.DrawString('ЖИТЕЛЬНИЦЫ БУНКЕРА · 50×50 px',$taskHeader,[System.Drawing.Brushes]::White,[System.Drawing.RectangleF]::new(0,14,1320,40),$taskFormat)
for($taskIndex=0;$taskIndex -lt $taskManifest.Count;$taskIndex++){
  $taskResident=$taskManifest[$taskIndex]
  $taskImage=[System.Drawing.Bitmap]::new((Join-Path $taskRoot ('sprites/'+$taskResident.id+'_idle_50.png')))
  $taskCol=$taskIndex%5;$taskRow=[Math]::Floor($taskIndex/5)
  $taskAtlasG.DrawImageUnscaled($taskImage,($taskCol*50),($taskRow*50))
  $taskG.DrawImage($taskImage,[System.Drawing.Rectangle]::new(($taskCol*264+7),($taskRow*320+64),250,250),0,0,50,50,[System.Drawing.GraphicsUnit]::Pixel)
  $taskG.DrawString([string]$taskResident.ru,$taskFont,[System.Drawing.Brushes]::White,[System.Drawing.RectangleF]::new(($taskCol*264),($taskRow*320+326),264,40),$taskFormat)
  $taskImage.Dispose()
}
Save-Png $taskAtlas (Join-Path $taskRoot 'BunkerWomen_atlas_250x100.png')
Save-Png $taskSheet (Join-Path $taskRoot 'BunkerWomen_contact_sheet.png')
$taskAtlasG.Dispose();$taskAtlas.Dispose();$taskG.Dispose();$taskSheet.Dispose();$taskFont.Dispose();$taskHeader.Dispose();$taskFormat.Dispose()
$taskArchiveItems=@('sprites','previews','prompts','BunkerWomen_atlas_250x100.png','BunkerWomen_contact_sheet.png','manifest.json','README.txt','validation.json') | ForEach-Object {Join-Path $taskRoot $_}
Compress-Archive -LiteralPath $taskArchiveItems -DestinationPath (Join-Path $taskRoot 'BunkerWomen_50px.zip') -Force
Write-Output "Exported and verified $($taskReport.Count) sprites."

